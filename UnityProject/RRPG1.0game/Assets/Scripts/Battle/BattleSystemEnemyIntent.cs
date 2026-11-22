using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// BattleSystem Stage 5 partial：敌人 Intent 生命周期与执行（Milestone C）
///
/// 生命周期（Current GameSpec R14.1/R14.2 + Milestone C 约束）：
///   进入玩家回合 → LockEnemyIntents() 为仍具战斗能力的敌人锁定/确认下一 Action
///   → UI 显示 Intent（进攻/防御/特殊 + HP + 攻击力 + AttackRange）
///   → 玩家行动（不触碰已锁定 Action）
///   → EnemyTurn：按已锁定 Action + TargetRule 在当前战况重新选择目标执行
///   → 下一玩家回合重新锁定。
///
/// 原则：
///   - Action 锁定，Target 不锁定（玩家可通过治疗/失能/改变战况影响最终目标）；
///   - Intent 只公开 ActionType（粗粒度），不公开具体目标/完整 AI 逻辑；
///   - 敌人失能后不再生成/执行 Intent；
///   - 实际攻击仍复用 Milestone B 统一距离规则（GetBlockingRowCount / IsInRange）。
/// </summary>
public partial class BattleSystem
{
    private readonly List<EnemyActionResolution> _enemyActionResults = new List<EnemyActionResolution>();
    public IReadOnlyList<EnemyActionResolution> LastEnemyActions => _enemyActionResults.AsReadOnly();

    internal static BattleRuleFactSnapshot CaptureUnitFact(BattleUnit u)
    {
        return u == null ? null : new BattleRuleFactSnapshot { UnitId = u.UnitId, HP = u.CurrentHP,
            MaxHP = u.MaxHP, Shield = u.Shield, Row = (int)u.Row, Column = (int)u.Column, IsDown = u.IsDown };
    }
    // ---------- Stage 5 配置（内容层可调；BattleSystem 为纯逻辑类，测试/宿主可直接赋值） ----------

    /// <summary>SelfLowHp 条件：自身当前生命比例 ≤ 该值 时满足</summary>
    public float EnemySelfLowHpRatio = 0.5f;

    /// <summary>AnyTargetLowHp 条件：存在我方目标当前生命比例 ≤ 该值 时满足</summary>
    public float EnemyTargetLowHpRatio = 0.4f;

    /// <summary>防御减伤比例：防御状态下受到的下一次伤害按该比例结算（0.5=减半）</summary>
    public float EnemyDefendDamageRatio = 0.5f;

    // ---------- Stage 5 运行时状态 ----------

    /// <summary>已锁定 Intent：UnitId → EnemyIntent（玩家回合开始生成，EnemyTurn 执行后消费）</summary>
    private readonly Dictionary<string, EnemyIntent> _lockedEnemyIntents = new Dictionary<string, EnemyIntent>();

    /// <summary>防御中的敌人 UnitId（受到下一次伤害减免后移除）</summary>
    private readonly HashSet<string> _defendingEnemyIds = new HashSet<string>();

    /// <summary>OncePerBattle 已使用规则（整场战斗内不再选）</summary>
    private readonly HashSet<EnemyActionRule> _usedOncePerBattleRules = new HashSet<EnemyActionRule>();

    /// <summary>怪物行为规则缓存（SourceId → rules，避免每回合重建配置列表）</summary>
    private readonly Dictionary<string, List<EnemyActionRule>> _enemyBehaviorCache = new Dictionary<string, List<EnemyActionRule>>();

    private readonly System.Random _enemyRng = new System.Random();

    /// <summary>清空 Stage 5 敌人 Intent 运行时状态（InitializeBattle 时调用，防多场战斗残留）</summary>
    private void ResetEnemyIntentState()
    {
        _enemyActionResults.Clear();
        _lockedEnemyIntents.Clear();
        _defendingEnemyIds.Clear();
        _usedOncePerBattleRules.Clear();
        _enemyBehaviorCache.Clear();
    }

    /// <summary>
    /// 进入玩家回合时调用：为仍具战斗能力的敌人锁定/确认下一 Action。
    /// - 已锁定且敌人仍存活的 Action 保持不变（确认语义，不在玩家行动期间重随机）；
    /// - 失能敌人的锁定被清除（不再生成/执行 Intent）。
    /// </summary>
    public void LockEnemyIntents()
    {
        if (_battleData == null) return;

        // 清理失能/离场敌人的已锁定 Intent（含战斗结束后的残留，保证失能敌人不再有 Intent）
        var stale = _lockedEnemyIntents.Keys
            .Where(id => _battleData.EnemyUnits.All(u => u.UnitId != id || u.IsDown || u.CurrentHP <= 0))
            .ToList();
        foreach (var id in stale)
            _lockedEnemyIntents.Remove(id);

        if (_battleData.IsBattleOver) return;

        foreach (var enemy in _battleData.EnemyUnits)
        {
            if (enemy.IsDown || enemy.CurrentHP <= 0) continue;
            if (!_lockedEnemyIntents.ContainsKey(enemy.UnitId))
                _lockedEnemyIntents[enemy.UnitId] = DecideEnemyIntent(enemy);
        }
    }

    /// <summary>读取敌人已锁定的 Intent（UI 显示与测试用；null=未锁定/失能）</summary>
    public EnemyIntent GetEnemyIntent(BattleUnit enemy)
    {
        if (enemy == null) return null;
        return _lockedEnemyIntents.TryGetValue(enemy.UnitId, out var intent) ? intent : null;
    }

    /// <summary>敌人当前是否处于防御状态（受到下一次伤害减免）</summary>
    public bool IsEnemyDefending(BattleUnit enemy)
    {
        return enemy != null && _defendingEnemyIds.Contains(enemy.UnitId);
    }

    /// <summary>EnemyTurn 执行结束后消费本回合 Intent（下一玩家回合重新锁定）</summary>
    public void ConsumeEnemyIntents()
    {
        _lockedEnemyIntents.Clear();
    }

    /// <summary>当前已锁定的 Intent 数量（诊断/测试用）</summary>
    public int GetLockedEnemyIntentCount()
    {
        return _lockedEnemyIntents.Count;
    }

    /// <summary>未锁定时的回退行动：普通攻击（保持旧行为）</summary>
    private EnemyIntent CreateFallbackEnemyIntent()
    {
        return new EnemyIntent
        {
            ActionType = EnemyActionType.Attack,
            TargetRule = EnemyTargetRule.Frontmost,
            DamageMultiplier = 1
        };
    }

    /// <summary>为该敌人按 条件→优先级→权重 决策下一次 Action（锁定 Action + TargetRule，不含具体目标）</summary>
    private EnemyIntent DecideEnemyIntent(BattleUnit enemy)
    {
        var rules = GetEnemyRules(enemy.SourceId);
        if (rules == null || rules.Count == 0)
            return CreateFallbackEnemyIntent();

        var intent = EnemyActionPlanner.Pick(rules,
            conditionMet: rule => IsEnemyConditionMet(rule, enemy),
            available: rule => !(rule.OncePerBattle && _usedOncePerBattleRules.Contains(rule)),
            rng: _enemyRng);

        return intent ?? CreateFallbackEnemyIntent();
    }

    /// <summary>读取怪物行为规则（未配置回退普通攻击）</summary>
    private List<EnemyActionRule> GetEnemyRules(string sourceId)
    {
        if (string.IsNullOrEmpty(sourceId)) return null;
        if (_enemyBehaviorCache.TryGetValue(sourceId, out var cached)) return cached;

        var rules = EnemyBehaviorDemoConfig.GetRules(sourceId);
        _enemyBehaviorCache[sourceId] = rules;
        return rules;
    }

    /// <summary>条件判定：该规则的条件当前是否满足（内容层扩展点：新增条件在此扩展）</summary>
    private bool IsEnemyConditionMet(EnemyActionRule rule, BattleUnit enemy)
    {
        switch (rule.Condition)
        {
            case EnemyConditionId.None:
                return true;

            case EnemyConditionId.SelfLowHp:
                return enemy.MaxHP > 0 && (float)enemy.CurrentHP / enemy.MaxHP <= EnemySelfLowHpRatio;

            case EnemyConditionId.AnyTargetLowHp:
                return _battleData.PlayerUnits.Any(p =>
                    !p.IsDown && p.CurrentHP > 0 && p.MaxHP > 0 &&
                    (float)p.CurrentHP / p.MaxHP <= EnemyTargetLowHpRatio);

            default:
                Debug.LogWarning("[BattleSystem] 未知敌人条件 ConditionId=" + rule.Condition + "，视为不满足");
                return false;
        }
    }

    /// <summary>
    /// 按 TargetRule 在当前战况选择合法目标（Action 已锁定，Target 不锁定）。
    /// 合法目标：存活我方单位 且 在攻击距离内（复用统一距离判定 IsInRange）。
    /// </summary>
    private BattleUnit FindEnemyTargetByRule(BattleUnit enemy, EnemyTargetRule rule)
    {
        var candidates = new List<BattleUnit>();
        foreach (var player in _battleData.PlayerUnits)
        {
            // R15.4：失去战斗能力的单位不再作为正常攻击目标
            if (player.IsDown || player.CurrentHP <= 0) continue;
            if (IsInRange(enemy, player))
                candidates.Add(player);
        }
        if (candidates.Count == 0) return null;

        if (rule == EnemyTargetRule.LowestHp)
        {
            candidates.Sort((a, b) =>
            {
                int cmp = a.CurrentHP.CompareTo(b.CurrentHP);
                return cmp != 0 ? cmp : GetPhysicalRowIndexFor(a).CompareTo(GetPhysicalRowIndexFor(b));
            });
        }
        else // Frontmost：物理排最前优先（同排低血优先）
        {
            candidates.Sort((a, b) =>
            {
                int aRow = GetPhysicalRowIndexFor(a);
                int bRow = GetPhysicalRowIndexFor(b);
                if (aRow != bRow) return aRow.CompareTo(bRow);
                return a.CurrentHP.CompareTo(b.CurrentHP);
            });
        }

        return candidates[0];
    }

    /// <summary>执行一个敌人的已锁定 Action（Attack/Special 打伤害；Defend 进入防御）</summary>
    private EnemyActionResolution ExecuteEnemyIntent(BattleUnit enemy, EnemyIntent intent)
    {
        var result = new EnemyActionResolution { EnemyUnitId = enemy.UnitId,
            IntentType = intent != null ? intent.ActionType : EnemyActionType.Attack };
        if (intent == null) return result;

        // 防御：本回合不攻击，进入防御状态（下次受击减伤，由 ApplyBattleDamage 结算）
        if (intent.ActionType == EnemyActionType.Defend)
        {
            _defendingEnemyIds.Add(enemy.UnitId);
            result.Applied = true;
            return result;
        }

        // Attack / Special：需要行动力
        if (enemy.CurrentActionPoints <= 0) return result;

        var target = FindEnemyTargetByRule(enemy, intent.TargetRule);
        if (target == null) return result;
        result.TargetUnitId = target.UnitId;
        result.Before = CaptureUnitFact(target);
        result.RequestedValue = enemy.Attack * Mathf.Max(1, intent.DamageMultiplier);

        // 消耗行动力
        enemy.CurrentActionPoints--;

        // Current GameSpec R15.x：敌方伤害统一走 ApplyBattleDamage（0HP 立即失能，不进入死亡之门）
        result.Applied = ApplyBattleDamage(target, result.RequestedValue);
        result.After = CaptureUnitFact(target);

        // OncePerBattle：使用后标记，整场战斗不再选
        if (intent.SourceRule != null && intent.SourceRule.OncePerBattle)
            _usedOncePerBattleRules.Add(intent.SourceRule);
        return result;
    }
}

/// <summary>敌方既有同步行动的只读捕获结果，不控制 AI 或结算。</summary>
public sealed class EnemyActionResolution
{
    public string EnemyUnitId, TargetUnitId;
    public EnemyActionType IntentType;
    public BattleRuleFactSnapshot Before, After;
    public int RequestedValue;
    public bool Applied;
}
