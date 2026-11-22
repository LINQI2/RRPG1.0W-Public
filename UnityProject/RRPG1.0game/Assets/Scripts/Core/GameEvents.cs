/// <summary>
/// 战斗开始事件
/// 由 BattleSystem 发布
/// </summary>
public class BattleStartEvent
{
}

/// <summary>
/// 战斗结束事件
/// 由 BattleSystem 在胜负判定后发布
/// </summary>
public class BattleEndEvent
{
    /// <summary>是否胜利</summary>
    public bool IsVictory;

    public BattleEndEvent(bool isVictory)
    {
        IsVictory = isVictory;
    }
}

/// <summary>
/// 回合开始事件
/// 由 TurnSystem 发布
/// </summary>
public class TurnStartEvent
{
    /// <summary>当前回合数</summary>
    public int Turn;

    public TurnStartEvent(int turn)
    {
        Turn = turn;
    }
}

/// <summary>
/// 回合结束事件
/// 由 TurnSystem 发布
/// </summary>
public class TurnEndEvent
{
    /// <summary>当前回合数</summary>
    public int Turn;

    public TurnEndEvent(int turn)
    {
        Turn = turn;
    }
}

/// <summary>
/// 技能施放事件
/// 由 SkillSystem 在技能释放时发布
/// </summary>
public class SkillCastEvent
{
    /// <summary>技能ID</summary>
    public string SkillId;

    /// <summary>来源单位ID</summary>
    public string SourceUnitId;

    public SkillCastEvent(string skillId, string sourceUnitId)
    {
        SkillId = skillId;
        SourceUnitId = sourceUnitId;
    }
}

/// <summary>
/// 技能执行事件
/// 由 SkillSystem 在技能效果执行后发布
/// </summary>
public class SkillExecuteEvent
{
    /// <summary>技能ID</summary>
    public string SkillId;

    /// <summary>来源单位ID</summary>
    public string SourceUnitId;

    public SkillExecuteEvent(string skillId, string sourceUnitId)
    {
        SkillId = skillId;
        SourceUnitId = sourceUnitId;
    }
}

/// <summary>
/// 伤害事件
/// 由 SkillSystem 在造成伤害时发布
/// </summary>
public class DamageEvent
{
    /// <summary>目标单位ID</summary>
    public string TargetUnitId;

    /// <summary>伤害值</summary>
    public int Damage;

    public DamageEvent(string targetUnitId, int damage)
    {
        TargetUnitId = targetUnitId;
        Damage = damage;
    }
}

/// <summary>
/// 治疗事件
/// 由 SkillSystem 在恢复生命时发布
/// </summary>
public class HealEvent
{
    /// <summary>目标单位ID</summary>
    public string TargetUnitId;

    /// <summary>治疗量</summary>
    public int HealAmount;

    public HealEvent(string targetUnitId, int healAmount)
    {
        TargetUnitId = targetUnitId;
        HealAmount = healAmount;
    }
}

/// <summary>
/// 状态添加事件
/// 由 StatusSystem 在添加状态时发布
/// </summary>
public class StatusAddEvent
{
    /// <summary>目标单位ID</summary>
    public string TargetUnitId;

    /// <summary>状态ID</summary>
    public string StatusId;

    public StatusAddEvent(string targetUnitId, string statusId)
    {
        TargetUnitId = targetUnitId;
        StatusId = statusId;
    }
}

/// <summary>
/// 状态移除事件
/// 由 StatusSystem 在移除状态时发布
/// </summary>
public class StatusRemoveEvent
{
    /// <summary>目标单位ID</summary>
    public string TargetUnitId;

    /// <summary>状态ID</summary>
    public string StatusId;

    public StatusRemoveEvent(string targetUnitId, string statusId)
    {
        TargetUnitId = targetUnitId;
        StatusId = statusId;
    }
}

/// <summary>
/// 死亡之门事件
/// 由 DeathDoorSystem 在单位进入死亡之门时发布
/// </summary>
public class DeathDoorEvent
{
    /// <summary>单位ID</summary>
    public string UnitId;

    public DeathDoorEvent(string unitId)
    {
        UnitId = unitId;
    }
}

/// <summary>
/// 死亡事件
/// 由 DeathDoorSystem 在单位死亡时发布
/// </summary>
public class DeathEvent
{
    /// <summary>单位ID</summary>
    public string UnitId;

    /// <summary>单位类型</summary>
    public UnitType UnitType;

    public DeathEvent(string unitId, UnitType unitType)
    {
        UnitId = unitId;
        UnitType = unitType;
    }
}