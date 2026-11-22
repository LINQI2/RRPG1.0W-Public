using System.Collections.Generic;

/// <summary>
/// 技能系统
///
/// 负责：管理技能释放流程，处理Trigger + Effect架构
/// 不负责：战斗回合推进、UI显示
///
/// 所有技能统一消耗行动力（ActionPoints）
/// 所有Effect由通用效果脚本执行，采用EffectType枚举驱动
/// </summary>
public class SkillSystem
{
    private BattleData _battleData;
    private StatusSystem _statusSystem;
    private List<SkillData> _allSkills;
    // 技能ID → 拥有该技能的单位SourceId列表
    private Dictionary<string, List<string>> _skillOwners;

    /// <summary>
    /// 创建技能系统
    /// </summary>
    /// <param name="battleData">战斗数据</param>
    /// <param name="statusSystem">状态系统（用于 AddBuff/RemoveBuff 效果）</param>
    public SkillSystem(BattleData battleData, StatusSystem statusSystem = null)
    {
        _battleData = battleData;
        _statusSystem = statusSystem;
        _allSkills = new List<SkillData>();
        _skillOwners = new Dictionary<string, List<string>>();
    }

    /// <summary>
    /// 注册技能到系统（不带拥有者）
    /// </summary>
    public void RegisterSkill(SkillData skill)
    {
        if (!_allSkills.Contains(skill))
        {
            _allSkills.Add(skill);
        }
    }

    /// <summary>
    /// 注册技能并关联拥有者单位
    /// </summary>
    /// <param name="skill">技能数据</param>
    /// <param name="ownerId">拥有该技能的SourceId</param>
    public void RegisterSkillWithOwner(SkillData skill, string ownerId)
    {
        if (!_allSkills.Contains(skill))
            _allSkills.Add(skill);

        if (!_skillOwners.ContainsKey(skill.SkillId))
            _skillOwners[skill.SkillId] = new List<string>();

        if (!_skillOwners[skill.SkillId].Contains(ownerId))
            _skillOwners[skill.SkillId].Add(ownerId);
    }

    /// <summary>
    /// 批量注册技能并关联拥有者
    /// </summary>
    public void RegisterSkillsWithOwner(IEnumerable<SkillData> skills, string ownerId)
    {
        foreach (var skill in skills)
        {
            RegisterSkillWithOwner(skill, ownerId);
        }
    }

    /// <summary>
    /// 批量注册技能
    /// </summary>
    public void RegisterSkills(IEnumerable<SkillData> skills)
    {
        foreach (var skill in skills)
        {
            RegisterSkill(skill);
        }
    }

    /// <summary>
    /// 尝试主动释放技能
    /// </summary>
    /// <param name="caster">施法单位</param>
    /// <param name="skill">技能数据</param>
    /// <param name="target">目标单位（可为null）</param>
    /// <returns>true=释放成功</returns>
    public bool CastSkill(BattleUnit caster, SkillData skill, BattleUnit target)
    {
        // 检查行动力是否足够
        if (caster.CurrentActionPoints < skill.CostActionPoints)
            return false;

        // 检查触发时机是否为主动释放
        if (skill.Trigger != TriggerType.Active)
            return false;

        // 消耗行动力
        caster.CurrentActionPoints -= skill.CostActionPoints;

        // 发布技能施放事件
        EventBus.Publish(new SkillCastEvent(skill.SkillId, caster.UnitId));

        // 执行效果
        ExecuteEffects(skill.Effects, caster, target);

        // 发布技能执行事件
        EventBus.Publish(new SkillExecuteEvent(skill.SkillId, caster.UnitId));

        return true;
    }

    /// <summary>
    /// 触发出指定触发时机的技能
    /// 用于 BattleStart / TurnStart / TurnEnd 等自动触发
    /// 只触发属于该单位的技能（通过 _skillOwners 检查）
    /// </summary>
    /// <param name="trigger">触发时机</param>
    /// <param name="unit">触发单位（可为null）</param>
    public void TriggerSkills(TriggerType trigger, BattleUnit unit)
    {
        if (unit == null) return;

        foreach (var skill in _allSkills)
        {
            if (skill.Trigger != trigger) continue;

            // 检查该技能是否属于这个单位
            if (_skillOwners.ContainsKey(skill.SkillId) &&
                !_skillOwners[skill.SkillId].Contains(unit.SourceId))
                continue;

            // 检查行动力
            if (unit.CurrentActionPoints < skill.CostActionPoints)
                continue;

            // 消耗行动力
            unit.CurrentActionPoints -= skill.CostActionPoints;

            // 发布技能施放事件
            EventBus.Publish(new SkillCastEvent(skill.SkillId, unit.UnitId));

            // 根据目标规则决定如何执行
            if (skill.Effects != null && skill.Effects.Length > 0)
            {
                TargetRule rule = skill.Effects[0].TargetRule;

                if (rule == TargetRule.AdjacentAlly)
                {
                    // 邻接己方单位：为该单位的所有邻接己方单位添加效果
                    foreach (var ally in _battleData.PlayerUnits)
                    {
                        if (ally == unit || ally.IsDown || ally.CurrentHP <= 0) continue;
                        if (IsAdjacentAlly(unit, ally))
                        {
                            ExecuteEffects(skill.Effects, unit, ally);
                            EventBus.Publish(new SkillExecuteEvent(skill.SkillId, ally.UnitId));
                        }
                    }
                }
                else
                {
                    // 默认：效果作用到触发单位自身
                    ExecuteEffects(skill.Effects, unit, unit);
                    EventBus.Publish(new SkillExecuteEvent(skill.SkillId, unit.UnitId));
                }
            }
        }
    }

    /// <summary>
    /// 判断两个单位是否为邻接关系（统一邻接接口，Stage 3A 起对外可见）
    ///
    /// 同排规则：
    ///   Left ↔ Center（邻接）
    ///   Center ↔ Right（邻接）
    ///   Left ↔ Right（不邻接，除非该排 Center 列无同阵营存活单位）
    ///
    /// 相邻排规则：
    ///   同列之间邻接
    ///   如果某排没有中列同阵营存活单位，该排与相邻排的中列邻接
    ///
    /// 双 Board（R5.0 / R5.1 / R6.3）：
    ///   邻接只在该单位所属 Board 内计算，跨 Player / Enemy Board 永不邻接；
    ///   Center 占位只读取该 Board 内仍具战斗能力的同阵营单位。
    ///   不改动已确认的邻接玩法规则本身。
    /// </summary>
    public bool IsAdjacentAlly(BattleUnit a, BattleUnit b)
    {
        if (a == null || b == null || a == b) return false;

        string faction = a.Faction;

        // 所属 Board 读取使用唯一判定入口（不缓存第二套空间真相）；不在同一 Board 上永不邻接
        var board = _battleData != null ? _battleData.GetBoardOf(a) : null;
        if (board == null || _battleData.GetBoardOf(b) != board) return false;

        int rowDiff = System.Math.Abs((int)a.Row - (int)b.Row);
        int colDiff = System.Math.Abs((int)a.Column - (int)b.Column);

        // 同排
        if (rowDiff == 0)
        {
            // 相邻列：Left↔Center, Center↔Right
            if (colDiff == 1) return true;
            // 左右列：如果该排没有同阵营中列存活单位，左右列相互邻接
            if (colDiff == 2)
                return !board.HasAliveUnitInFaction(a.Row, BattleColumn.Center, faction);
            return false;
        }

        // 相邻排（排差为1）
        if (rowDiff == 1)
        {
            // 同列邻接
            if (colDiff == 0) return true;
            // 列差为1：一方在Center，另一方在Left/Right
            if (colDiff == 1)
            {
                BattleRow aRow = a.Row;
                BattleRow bRow = b.Row;
                BattleColumn aCol = a.Column;
                BattleColumn bCol = b.Column;

                // a在Center，检查b所在排是否没有同阵营中列存活单位
                if (aCol == BattleColumn.Center && !board.HasAliveUnitInFaction(bRow, BattleColumn.Center, faction))
                    return true;
                // b在Center，检查a所在排是否没有同阵营中列存活单位
                if (bCol == BattleColumn.Center && !board.HasAliveUnitInFaction(aRow, BattleColumn.Center, faction))
                    return true;
            }
            return false;
        }

        return false;
    }

    /// <summary>
    /// 执行效果列表
    /// </summary>
    private void ExecuteEffects(EffectData[] effects, BattleUnit caster, BattleUnit target)
    {
        if (effects == null) return;

        foreach (var effect in effects)
        {
            ExecuteEffect(effect, caster, target);
        }
    }

    /// <summary>
    /// 执行单个效果
    /// 根据 EffectType 枚举驱动
    /// </summary>
    private void ExecuteEffect(EffectData effect, BattleUnit caster, BattleUnit target)
    {
        if (target == null) return;

        switch (effect.EffectType)
        {
            case EffectType.Damage:
                target.CurrentHP -= effect.Value;
                EventBus.Publish(new DamageEvent(target.UnitId, effect.Value));
                break;

            case EffectType.Heal:
                target.CurrentHP += effect.Value;
                if (target.CurrentHP > target.MaxHP)
                    target.CurrentHP = target.MaxHP;
                EventBus.Publish(new HealEvent(target.UnitId, effect.Value));
                break;

            case EffectType.SubHP:
                target.CurrentHP -= effect.Value;
                EventBus.Publish(new DamageEvent(target.UnitId, effect.Value));
                break;

            case EffectType.AddAttack:
                target.Attack += effect.Value;
                break;

            case EffectType.SubAttack:
                target.Attack -= effect.Value;
                if (target.Attack < 0) target.Attack = 0;
                break;

            case EffectType.SubActionPoints:
                target.CurrentActionPoints -= effect.Value;
                if (target.CurrentActionPoints < 0)
                    target.CurrentActionPoints = 0;
                break;

            case EffectType.DrawCard:
                // 抽牌逻辑由 CardSystem 处理
                break;

            case EffectType.AddTP:
                // TP由 TPSystem 处理
                break;

            case EffectType.AddBuff:
                if (_statusSystem != null && !string.IsNullOrEmpty(effect.StatusId))
                {
                    var statusData = _statusSystem.GetStatusData(effect.StatusId);
                    if (statusData != null)
                    {
                        _statusSystem.AddStatus(target, statusData, caster.SourceId);
                    }
                }
                break;

            case EffectType.RemoveBuff:
                if (_statusSystem != null && !string.IsNullOrEmpty(effect.StatusId))
                {
                    _statusSystem.RemoveStatus(target, effect.StatusId);
                }
                break;

            case EffectType.DisableAttack:
            case EffectType.DisableSkill:
            case EffectType.DisableAction:
                // 通过状态系统实现
                break;

            case EffectType.DeathDoor:
                target.InDeathDoor = true;
                EventBus.Publish(new DeathDoorEvent(target.UnitId));
                break;
        }
    }

    /// <summary>
    /// 根据距离规则筛选目标
    /// </summary>
    /// <param name="caster">施法者</param>
    /// <param name="targetRule">目标规则</param>
    /// <param name="skillRange">技能距离</param>
    /// <returns>符合条件的目标列表</returns>
    public List<BattleUnit> GetValidTargets(BattleUnit caster, TargetRule targetRule, int skillRange)
    {
        var targets = new List<BattleUnit>();
        // 双 Board：候选集合 = 两条 Board 上的逻辑单位（不新建第二套空间真相源）
        var allUnits = new List<BattleUnit>();
        if (_battleData != null)
        {
            allUnits.AddRange(_battleData.PlayerField.GetAllUnits());
            allUnits.AddRange(_battleData.EnemyField.GetAllUnits());
        }

        foreach (var unit in allUnits)
        {
            if (IsValidTarget(caster, unit, targetRule, skillRange))
            {
                targets.Add(unit);
            }
        }

        return targets;
    }

    /// <summary>
    /// 判断单位是否为合法目标
    /// </summary>
    private bool IsValidTarget(BattleUnit caster, BattleUnit target, TargetRule targetRule, int skillRange)
    {
        bool isValid = false;

        switch (targetRule)
        {
            case TargetRule.Self:
                isValid = target == caster;
                break;
            case TargetRule.Single:
                isValid = target != caster && target.Faction != caster.Faction;
                break;
            case TargetRule.SameRow:
                isValid = target.Row == caster.Row;
                break;
            case TargetRule.All:
                isValid = true;
                break;
        }

        return isValid;
    }
}