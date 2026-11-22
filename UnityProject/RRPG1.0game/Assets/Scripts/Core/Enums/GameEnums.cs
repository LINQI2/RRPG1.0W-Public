/// <summary>
/// 战斗阶段枚举
///
/// 用于 BattleSystem / TurnSystem / UI 统一管理战斗阶段
/// </summary>
public enum BattlePhase
{
    /// <summary>战前准备</summary>
    Preparation,

    /// <summary>战斗开始</summary>
    BattleStart,

    /// <summary>回合开始</summary>
    TurnStart,

    /// <summary>抽牌阶段</summary>
    Draw,

    /// <summary>决策阶段</summary>
    Decision,

    /// <summary>敌方攻击阶段</summary>
    EnemyAction,

    /// <summary>回合结束</summary>
    TurnEnd
}

/// <summary>
/// 单位类型枚举
/// </summary>
public enum UnitType
{
    Character,
    Hero,
    Monster
}

/// <summary>
/// 战场排枚枚举
/// </summary>
public enum BattleRow
{
    Front,
    Middle,
    Back
}

/// <summary>
/// 战场列枚举
/// </summary>
public enum BattleColumn
{
    Left,
    Center,
    Right
}

/// <summary>
/// 技能触发时机枚举
///
/// 决定技能在何时自动触发或可主动释放
/// </summary>
public enum TriggerType
{
    /// <summary>战斗开始</summary>
    BattleStart,

    /// <summary>回合开始</summary>
    TurnStart,

    /// <summary>抽牌后</summary>
    OnDraw,

    /// <summary>主动释放</summary>
    Active,

    /// <summary>攻击时</summary>
    OnAttack,

    /// <summary>受击时</summary>
    OnHit,

    /// <summary>击杀时</summary>
    OnKill,

    /// <summary>进入死亡之门</summary>
    OnDeathDoor,

    /// <summary>回合结束</summary>
    TurnEnd
}

/// <summary>
/// 效果类型枚举
///
/// 定义技能效果的具体类型
/// MVP 不实现复活和产卡效果
/// </summary>
public enum EffectType
{
    /// <summary>造成伤害</summary>
    Damage,

    /// <summary>恢复生命</summary>
    Heal,

    /// <summary>增加攻击力（增益）</summary>
    AddAttack,

    /// <summary>增加防御力（增益）</summary>
    AddDefense,

    /// <summary>抽牌</summary>
    DrawCard,

    /// <summary>增加TP</summary>
    AddTP,

    /// <summary>添加状态</summary>
    AddBuff,

    /// <summary>移除状态</summary>
    RemoveBuff,

    /// <summary>无法攻击</summary>
    DisableAttack,

    /// <summary>无法使用技能</summary>
    DisableSkill,

    /// <summary>无法行动</summary>
    DisableAction,

    /// <summary>死亡之门判定</summary>
    DeathDoor,

    /// <summary>减少生命值（减益）</summary>
    SubHP,

    /// <summary>减少攻击力（减益）</summary>
    SubAttack,

    /// <summary>减少行动力（减益）</summary>
    SubActionPoints
}

/// <summary>
/// 目标规则枚举
///
/// 定义技能效果的目标选择规则
/// </summary>
public enum TargetRule
{
    /// <summary>自身</summary>
    Self,

    /// <summary>单体目标</summary>
    Single,

    /// <summary>同排</summary>
    SameRow,

    /// <summary>邻接单位</summary>
    Adjacent,

    /// <summary>全体</summary>
    All,

    /// <summary>前排</summary>
    FrontRow,

    /// <summary>中排</summary>
    MiddleRow,

    /// <summary>后排</summary>
    BackRow,

    /// <summary>邻接己方单位（同排相邻列）</summary>
    AdjacentAlly
}