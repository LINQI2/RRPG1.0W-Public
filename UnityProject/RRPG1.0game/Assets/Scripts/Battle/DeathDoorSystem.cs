using System.Collections.Generic;

/// <summary>
/// 死亡之门系统
///
/// 负责：角色生命值归零后的死亡判定
/// 不负责：伤害计算、技能释放、回合管理
///
/// 数据流：
///   BattleUnit → DeathDoorSystem → CharacterInstance
///   不经过 InjurySystem 中转
///
/// 规则：
///   仅角色进入死亡之门（英雄/怪物不进入）
///   初始死亡概率：10%，每被击中一次：+10%
///   MVP阶段：立即结算，不进行回合判定
///
/// 判定结果：
///   存活 → 维持倒地状态，不恢复HP
///   轻伤/重伤/残疾 → 写入 Injury 状态
///   死亡 → 角色永久消失，IsDead = true
/// </summary>
public class DeathDoorSystem
{
    private List<CharacterInstance> _characterInstances;
    private InjuryProbabilityConfig _probabilityConfig;

    /// <summary>
    /// 创建死亡之门系统
    /// </summary>
    /// <param name="characterInstances">角色实例列表（存档数据）</param>
    /// <param name="probabilityConfig">受伤概率配置</param>
    public DeathDoorSystem(List<CharacterInstance> characterInstances, InjuryProbabilityConfig probabilityConfig)
    {
        _characterInstances = characterInstances;
        _probabilityConfig = probabilityConfig;
    }

    /// <summary>
    /// 尝试进入死亡之门
    ///
    /// 角色HP归零时调用此方法
    /// 英雄/怪物不进入死亡之门
    /// </summary>
    /// <param name="unit">战斗单位</param>
    /// <param name="hitCount">本场战斗中被击中的次数</param>
    public void TryEnterDeathDoor(BattleUnit unit, int hitCount)
    {
        if (unit.UnitType == UnitType.Character)
        {
            // 角色进入死亡之门
            unit.IsDown = true;
            unit.InDeathDoor = true;

            // 发布死亡之门事件
            EventBus.Publish(new DeathDoorEvent(unit.UnitId));

            // 立即判定
            ResolveDeathDoor(unit, hitCount);
        }
        else if (unit.UnitType == UnitType.Hero)
        {
            // 英雄被击倒 → 强制下阵，不进入死亡之门
            unit.IsDown = true;
            unit.InDeathDoor = false;

            // 发布死亡事件（英雄倒地，非永久死亡）
            EventBus.Publish(new DeathEvent(unit.UnitId, UnitType.Hero));
        }
        else
        {
            // 怪物直接死亡
            unit.IsDown = true;
            unit.InDeathDoor = false;

            EventBus.Publish(new DeathEvent(unit.UnitId, UnitType.Monster));
        }
    }

    /// <summary>
    /// 结算死亡之门结果
    /// 结果直接写入 CharacterInstance
    ///
    /// 判定逻辑（修正版）：
    /// 1. 先判定是否存活：roll > deathProbability → 存活
    /// 2. 若未存活，按 InjuryProbabilityConfig 区间判定受伤/死亡
    ///    轻伤(≤LightInjuryMax) → 重伤(≤HeavyInjuryMax) → 残疾(≥DisabledMin) → 死亡
    /// </summary>
    private void ResolveDeathDoor(BattleUnit unit, int hitCount)
    {
        // 计算死亡概率
        int deathProbability = 10 + (hitCount * 10);
        if (deathProbability > 100) deathProbability = 100;

        // 随机判定
        int roll = UnityEngine.Random.Range(0, 101);

        // 查找对应的 CharacterInstance
        var character = _characterInstances.Find(c => c.CharacterId == unit.SourceId);
        if (character == null) return;

        // 先判定是否存活
        if (roll > deathProbability)
        {
            // 存活，恢复站立状态（IsDown 会被设为 true，需重置）
            unit.IsDown = false;
            unit.InDeathDoor = false;
            unit.CurrentHP = 1; // 留1HP表示存活
            return;
        }

        // 受伤/死亡判定（roll 在 [0, deathProbability] 区间内）
        // 将 roll 映射到受伤概率配置的完整区间 [0, 100] 进行判定
        int injuryRoll = UnityEngine.Random.Range(0, 101);

        if (injuryRoll <= _probabilityConfig.LightInjuryMax)
        {
            character.Injury = InjuryStatus.LightInjury;
        }
        else if (injuryRoll <= _probabilityConfig.HeavyInjuryMax)
        {
            character.Injury = InjuryStatus.HeavyInjury;
        }
        else if (injuryRoll >= _probabilityConfig.DisabledMin)
        {
            character.Injury = InjuryStatus.Disabled;
        }
        else
        {
            // 死亡
            character.IsDead = true;
            EventBus.Publish(new DeathEvent(unit.UnitId, UnitType.Character));
        }
    }
}