using System;

/// <summary>
/// Battle V0.1 Stage 5：敌人 Intent 数据结构（纯逻辑，不依赖 UnityEngine）
///
/// 设计（Current GameSpec R14.x + Milestone C 约束）：
///   - 敌人下一次主要行动只公开粗粒度类型（Attack / Defend / Special）；
///   - Action 与 Target 解耦：决策时锁定 ActionType + TargetRule，
///     实际执行时由 TargetRule 在当前战况重新选择具体目标；
///   - 不公开具体目标、完整 AI 行为逻辑、后续多回合行动序列。
///
/// 决策框架：Condition → Priority → Weight → Action → TargetRule。
/// Cooldown / OncePerBattle 为本 Milestone 的最小扩展点：
///   OncePerBattle 由决策器（EnemyActionPlanner）支持；
///   Cooldown 保留字段供后续扩展，当前演示未强制启用。
/// </summary>

/// <summary>敌人行动类型（玩家可见的粗粒度 Intent）</summary>
public enum EnemyActionType
{
    /// <summary>进攻：对目标造成普通攻击伤害</summary>
    Attack,
    /// <summary>防御：本回合不攻击，进入防御状态（下次受击减伤）</summary>
    Defend,
    /// <summary>特殊：条件/优先级表达的特殊行动（本演示=高倍率攻击）</summary>
    Special
}

/// <summary>目标选择规则（执行时在当前战况下按规则选目标，目标不提前公开）</summary>
public enum EnemyTargetRule
{
    /// <summary>物理前排优先（同排内当前生命低者优先）</summary>
    Frontmost,
    /// <summary>在合法目标中选择当前生命最低的单位</summary>
    LowestHp
}

/// <summary>行动可用条件（Condition）</summary>
public enum EnemyConditionId
{
    /// <summary>无条件（总是可用）</summary>
    None,
    /// <summary>自身当前生命比例 ≤ 阈值（防御型低血转防御）</summary>
    SelfLowHp,
    /// <summary>存在生命比例 ≤ 阈值 的我方合法目标（刺客型收割）</summary>
    AnyTargetLowHp
}

/// <summary>
/// 一条敌人行动规则：Condition → Priority → Weight → Action → TargetRule。
/// 由敌人内容配置（当前为 EnemyBehaviorDemoConfig 演示映射）提供。
/// </summary>
[Serializable]
public class EnemyActionRule
{
    public EnemyActionType ActionType;
    public EnemyConditionId Condition = EnemyConditionId.None;

    /// <summary>优先级：数值越大越先考虑；同满足时只取最高 Priority 组</summary>
    public int Priority;

    /// <summary>权重：同一 Priority 组内按权重加权随机</summary>
    public int Weight = 100;

    /// <summary>执行时如何选择目标（Action 决定做什么，TargetRule 决定对谁做）</summary>
    public EnemyTargetRule TargetRule = EnemyTargetRule.Frontmost;

    /// <summary>冷却回合数（扩展点：0=无冷却；当前演示不强制实现）</summary>
    public int Cooldown;

    /// <summary>整场战斗仅一次（扩展点：决策器支持，使用后不再选）</summary>
    public bool OncePerBattle;

    /// <summary>Special 行动伤害倍率（内容层可配，默认 1；Attack/Defend 恒为 1）</summary>
    public int SpecialDamageMultiplier = 1;

    public EnemyActionRule() { }

    public EnemyActionRule(EnemyActionType action, EnemyConditionId condition, int priority, int weight,
                           EnemyTargetRule target = EnemyTargetRule.Frontmost,
                           int specialDamageMultiplier = 1, bool oncePerBattle = false)
    {
        ActionType = action;
        Condition = condition;
        Priority = priority;
        Weight = weight;
        TargetRule = target;
        SpecialDamageMultiplier = specialDamageMultiplier;
        OncePerBattle = oncePerBattle;
    }
}

/// <summary>
/// 敌人已锁定的下一次行动（Action 锁定；Target 不锁定，执行时由 TargetRule 重新选择）。
/// 不包含任何具体目标信息，避免把 Target 写入玩家可见 Intent。
/// </summary>
[Serializable]
public class EnemyIntent
{
    public EnemyActionType ActionType;
    public EnemyTargetRule TargetRule;

    /// <summary>来源规则（供 OncePerBattle 追踪；null=回退行动）</summary>
    public EnemyActionRule SourceRule;

    /// <summary>伤害倍率（Special 可 &gt;1；Attack/Defend 为 1）</summary>
    public int DamageMultiplier = 1;
}
