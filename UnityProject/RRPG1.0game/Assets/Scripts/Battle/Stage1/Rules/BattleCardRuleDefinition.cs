using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Battle V0.1 Gate 0（3P Shared Deck）战斗牌规则定义层——有限、强类型、可配置的效果零件。
///
/// 负责：
///   - 用固定的设计职责（Trigger / Query / Predicate / Selector / Value Source / Operation / Lifetime / Play Requirement）
///     描述一张牌“打出后发生什么”；
///   - 提供 ScriptableObject 可直接序列化的 [Serializable] 配置结构。
///
/// 不负责：
///   - 任何具体卡牌的专属执行逻辑（禁止 CardId 分支）；
///   - 任意脚本表达式 / Effect Graph / 万能 DSL；
///   - 战斗状态写入（由 IBattleRuleContext 的宿主实现调用既有 Battle Authority）。
///
/// 边界（Gate 0 Development Spec §3.2 / §8.3）：
///   零件数量只由当前真实内容逼出来，不提前承载未来机制（百分比、乘法、优先层、Focus、History 等）。
/// </summary>
public enum BattleRuleTrigger
{
    /// <summary>无触发声明（纯 Legacy 牌）</summary>
    None = 0,

    /// <summary>主动出牌结算（Gate 0 唯一支持的触发时机）</summary>
    OnPlay = 1
}

/// <summary>一条规则步骤实际执行的操作（Operation）。</summary>
public enum BattleRuleOperation
{
    None = 0,

    /// <summary>对单位造成伤害</summary>
    Damage = 1,

    /// <summary>对单位恢复生命</summary>
    Heal = 2,

    /// <summary>对单位赋予本场临时护盾</summary>
    Shield = 3,

    /// <summary>从共享牌库抽牌</summary>
    Draw = 4,

    /// <summary>修改一张手牌的 Actual Cost（Additive，可为负）</summary>
    ModifyCost = 5,

    /// <summary>修改一张手牌的 Actual Damage（Additive，可为负；目标无 Damage 时为 Not Applicable）</summary>
    ModifyDamage = 6,

    /// <summary>移动牌主单位到我方合法空格</summary>
    Move = 7,

    /// <summary>牌主单位与另一名我方单位交换位置</summary>
    Swap = 8
}

/// <summary>
/// 步骤目标对象集合的来源（Query 域）。
/// 每个来源都在运行时通过 IBattleRuleContext 读取当前 Battle Fact，不缓存第二套战场真相。
/// </summary>
public enum BattleRuleTargetSource
{
    None = 0,

    /// <summary>玩家在 UI 选择的目标（敌方单位 / 我方单位 / 我方空格）</summary>
    ManualTarget = 1,

    /// <summary>牌主单位（角色牌归属 Person 对应的本场单位）</summary>
    CardOwner = 2,

    /// <summary>玩家所选目标的邻接我方单位</summary>
    ManualTargetAdjacentAllies = 3,

    /// <summary>共享手牌中除本牌以外的牌</summary>
    HandOthers = 4,

    /// <summary>我方战场空格</summary>
    EmptyAllyCells = 5,

    /// <summary>除牌主以外的仍具备战斗能力的我方单位</summary>
    OtherAllyUnits = 6
}

/// <summary>目标集合如何归约（Selector / Aggregate）。</summary>
public enum BattleRuleSelect
{
    /// <summary>全部符合条件的目标</summary>
    All = 0,

    /// <summary>Actual Cost 最高的一张；并列时取手牌最左（确定性、非随机）</summary>
    MaxByActualCostLeftmost = 1
}

/// <summary>步骤数值来源（Value Source）。</summary>
public enum BattleRuleValueSource
{
    None = 0,

    /// <summary>配置常量（ValueParam；修正类操作可为负）</summary>
    Fixed = 1,

    /// <summary>牌主单位的 Attack</summary>
    CasterAttack = 2
}

/// <summary>
/// 附加收益（Bonus）的成立条件（Predicate）。
/// 条件不成立不等于卡牌不可打——只影响 Bonus 是否附加（Gate 0 Development Spec §3.3）。
/// </summary>
public enum BattleRuleCondition
{
    None = 0,

    /// <summary>目标单位当前生命 ≤ 其最大生命的 Param%（1..100）</summary>
    TargetHpPercentAtOrBelow = 1,

    /// <summary>目标敌方单位当前 Intent 为防御</summary>
    EnemyIntentIsDefend = 2,

    /// <summary>牌主单位为独狼（所在排无其他仍具战斗能力的我方单位）</summary>
    OwnerIsAloneInRow = 3,

    /// <summary>结算时共享行动力为 0（含本牌费用已支付）</summary>
    SharedActionPointsIsZero = 4
}

/// <summary>
/// 临时修正的生命周期。
/// CurrentDeckCycle = 该具体牌经弃牌堆重新进入新的抽牌循环时重置（Gate 0 唯一使用的修正生命周期）。
/// </summary>
public enum BattleRuleLifetime
{
    /// <summary>默认：随 Battle Runtime 存在，不进入新 Cycle 重置（仅用于非修正类操作）</summary>
    BattleRuntime = 0,

    /// <summary>Current Deck Cycle：Discard → Reshuffle into Draw Pile 时重置</summary>
    CurrentDeckCycle = 1
}

/// <summary>一张牌是否需要玩家手选目标，以及需要哪一类目标（UI 目标选择宿主据此分流）。</summary>
public enum BattleRuleManualTargetKind
{
    /// <summary>无需手选目标（自动目标或仅作用于牌主）</summary>
    None = 0,

    /// <summary>需要选择敌方单位</summary>
    EnemyUnit = 1,

    /// <summary>需要选择我方单位</summary>
    AllyUnit = 2,

    /// <summary>需要选择我方战场空格</summary>
    AllyCell = 3
}

/// <summary>
/// 一条规则步骤（Rule Step）：对某个目标集合执行一次 Operation。
///
/// 结构对应 Gate 0 Development Spec §3.2 的设计职责：
///   TargetSource = Query；BonusCondition = Predicate；Select = Selector；
///   ValueSource = Value Source；Operation = Operation；Lifetime = Lifetime。
///
/// 结算绑定：目标集合在“支付 AP 之前”一次绑定，步骤内部不再重新选目标，
/// 避免数值变化（如 Actual Cost 被修改）导致后续步骤误选另一对象。
/// </summary>
[Serializable]
public class BattleCardRuleStep
{
    [Tooltip("执行的操作（Damage / Heal / Shield / Draw / ModifyCost / ModifyDamage / Move / Swap）")]
    public BattleRuleOperation Operation = BattleRuleOperation.None;

    [Tooltip("目标对象集合来源（Query 域）")]
    public BattleRuleTargetSource TargetSource = BattleRuleTargetSource.ManualTarget;

    [Tooltip("目标集合归约方式（Selector / Aggregate）")]
    public BattleRuleSelect Select = BattleRuleSelect.All;

    [Tooltip("基础数值来源")]
    public BattleRuleValueSource ValueSource = BattleRuleValueSource.None;

    [Tooltip("基础数值参数（Fixed 时为常量；CasterAttack 时忽略）")]
    public int ValueParam;

    [Tooltip("附加收益条件（Predicate）；不成立时本步骤仍以基础数值结算")]
    public BattleRuleCondition BonusCondition = BattleRuleCondition.None;

    [Tooltip("附加收益条件参数（当前仅百分比阈值使用）")]
    public int BonusConditionParam;

    [Tooltip("附加收益数值来源")]
    public BattleRuleValueSource BonusValueSource = BattleRuleValueSource.None;

    [Tooltip("附加收益数值参数")]
    public int BonusValueParam;

    [Tooltip("修正类操作的生命周期（ModifyCost / ModifyDamage 使用）")]
    public BattleRuleLifetime Lifetime = BattleRuleLifetime.BattleRuntime;
}

/// <summary>
/// 出牌前置（Play Requirement）：Primary Resolution 是否已存在合法结果。
///
/// Gate 0 Development Spec §3.3：一张主动 Card 如果其 Primary Resolution 不存在合法结果，
/// 则当前不可打出；判断必须发生在支付 AP 之前。TargetSource 为 None 表示本牌不需要该前置。
/// </summary>
[Serializable]
public class BattleCardPlayRequirement
{
    [Tooltip("Primary Resolution 的目标集合来源；None = 不需要出牌前置")]
    public BattleRuleTargetSource TargetSource = BattleRuleTargetSource.None;

    [Tooltip("目标集合归约方式（与结算步骤一致，保证玩家可预测）")]
    public BattleRuleSelect Select = BattleRuleSelect.All;

    [Tooltip("至少需要的合法结果数量")]
    public int MinCount = 1;
}

/// <summary>
/// 一张战斗牌的规则程序（Rule Program）——静态配置，随 BattleCardData 序列化。
///
/// 语义边界：
///   - 只描述“这张牌怎么结算”，不含任何牌名 / 卡牌 ID 判断；
///   - Trigger=None 或空 Steps 表示该牌未迁移到新管线（走既有 Legacy 最小效果路径）；
///   - 同一张牌不会新旧两套效果重复结算（执行端按 HasRuleProgram 二选一）。
/// </summary>
[Serializable]
public class BattleCardRuleProgram
{
    [Tooltip("触发时机（Gate 0 仅 OnPlay = 主动出牌结算）")]
    public BattleRuleTrigger Trigger = BattleRuleTrigger.None;

    [Tooltip("是否需要玩家手选目标；None = 自动目标 / 只作用于牌主")]
    public BattleRuleManualTargetKind ManualTargetKind = BattleRuleManualTargetKind.None;

    [Tooltip("出牌前置（Primary Resolution 合法性）；TargetSource=None 表示无前置")]
    public BattleCardPlayRequirement PlayRequirement = new BattleCardPlayRequirement();

    [Tooltip("按顺序结算的规则步骤")]
    public List<BattleCardRuleStep> Steps = new List<BattleCardRuleStep>();

    /// <summary>是否是可执行的规则程序（Trigger 已声明且至少有一条步骤）</summary>
    public bool IsExecutable
    {
        get { return Trigger != BattleRuleTrigger.None && Steps != null && Steps.Count > 0; }
    }

    /// <summary>是否需要玩家手选目标</summary>
    public bool RequiresManualTarget
    {
        get { return ManualTargetKind != BattleRuleManualTargetKind.None; }
    }
}
