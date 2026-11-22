using System.Collections.Generic;

/// <summary>规则步骤目标的对象类别。</summary>
public enum BattleRuleTargetKind
{
    None = 0,

    /// <summary>单位目标（我方 / 敌方）</summary>
    Unit = 1,

    /// <summary>我方战场格子目标（空格；用于 Move）</summary>
    Cell = 2,

    /// <summary>共享手牌中的一张牌（用于 ModifyCost / ModifyDamage）</summary>
    HandCard = 3
}

/// <summary>
/// 已绑定的目标引用（结算期间不再重新选择）。
/// 只承载标识与坐标，不承载 Battle 状态，因此可安全在纯逻辑层传递与测试。
/// </summary>
public struct BattleRuleTargetRef
{
    /// <summary>目标类别</summary>
    public BattleRuleTargetKind Kind;

    /// <summary>单位目标的本场单位 ID</summary>
    public string UnitId;

    /// <summary>单位目标的 PersonId（日志/调试用）</summary>
    public string PersonId;

    /// <summary>格子目标的排索引</summary>
    public int Row;

    /// <summary>格子目标的列索引</summary>
    public int Column;

    /// <summary>手牌目标的牌实例</summary>
    public BattleCard Card;

    public static BattleRuleTargetRef None()
    {
        return new BattleRuleTargetRef { Kind = BattleRuleTargetKind.None };
    }

    public static BattleRuleTargetRef ForUnit(BattleRuleUnitFacts unit)
    {
        if (unit == null) return None();
        return new BattleRuleTargetRef
        {
            Kind = BattleRuleTargetKind.Unit,
            UnitId = unit.UnitId,
            PersonId = unit.PersonId
        };
    }

    public static BattleRuleTargetRef ForCell(int row, int column, string occupantPersonId)
    {
        return new BattleRuleTargetRef
        {
            Kind = BattleRuleTargetKind.Cell,
            Row = row,
            Column = column,
            PersonId = occupantPersonId
        };
    }

    public static BattleRuleTargetRef ForCard(BattleCard card)
    {
        return new BattleRuleTargetRef
        {
            Kind = BattleRuleTargetKind.HandCard,
            Card = card
        };
    }

    /// <summary>是否指向有效对象</summary>
    public bool IsValid
    {
        get
        {
            switch (Kind)
            {
                case BattleRuleTargetKind.Unit: return !string.IsNullOrEmpty(UnitId);
                case BattleRuleTargetKind.Cell: return Row >= 0 && Column >= 0;
                case BattleRuleTargetKind.HandCard: return Card != null;
                default: return false;
            }
        }
    }

    /// <summary>目标等价判断（用于“所选目标是否合法”校验）</summary>
    public bool SameAs(BattleRuleTargetRef other)
    {
        if (Kind != other.Kind) return false;
        switch (Kind)
        {
            case BattleRuleTargetKind.Unit: return UnitId == other.UnitId;
            case BattleRuleTargetKind.Cell: return Row == other.Row && Column == other.Column;
            case BattleRuleTargetKind.HandCard: return ReferenceEquals(Card, other.Card);
            default: return false;
        }
    }

    /// <summary>可读描述（日志 / UI 预览用）</summary>
    public string Describe()
    {
        switch (Kind)
        {
            case BattleRuleTargetKind.Unit:
                return string.IsNullOrEmpty(PersonId) ? UnitId : UnitId + "(" + PersonId + ")";
            case BattleRuleTargetKind.Cell:
                return "格子[" + Row + "," + Column + "]";
            case BattleRuleTargetKind.HandCard:
                return Card != null ? Card.DisplayName : "牌";
            default:
                return "无目标";
        }
    }
}

/// <summary>绑定完成的规则步骤：定义 + 支付 AP 之前一次绑定的目标集合。</summary>
public class BattleRuleBoundStep
{
    /// <summary>静态配置</summary>
    public BattleCardRuleStep Definition;

    /// <summary>已绑定目标（空 = 本步骤 Not Applicable，不影响打出合法性）</summary>
    public List<BattleRuleTargetRef> Targets = new List<BattleRuleTargetRef>();
}

/// <summary>
/// 出牌计划：结算前完成目标绑定，随后支付 AP，再按序执行（数值在执行时读取当前 Battle Fact）。
/// </summary>
public class BattleCardRulePlan
{
    /// <summary>被结算的牌</summary>
    public BattleCard Card;

    /// <summary>牌上的规则程序</summary>
    public BattleCardRuleProgram Program;

    /// <summary>按序绑定的步骤</summary>
    public List<BattleRuleBoundStep> Steps = new List<BattleRuleBoundStep>();

    /// <summary>出牌前置绑定的 Primary Resolution 目标（自动目标牌的可预测结果）</summary>
    public List<BattleRuleTargetRef> PrimaryTargets = new List<BattleRuleTargetRef>();

    /// <summary>玩家手选目标（无则 Kind=None）</summary>
    public BattleRuleTargetRef ManualTarget;
}

/// <summary>出牌合法性检查结果（必须在支付 AP 之前得到）。</summary>
public class BattleCardRuleLegality
{
    /// <summary>是否可打出</summary>
    public bool IsLegal;

    /// <summary>不可打出的原因（可读，供 UI 提示与测试断言）</summary>
    public string Reason = string.Empty;

    /// <summary>归因类别（供测试与 UI 区分“目标非法 / 前置不成立”等）</summary>
    public BattleCardRuleLegalityKind Kind = BattleCardRuleLegalityKind.None;

    /// <summary>本次检查解析出的 Primary Resolution 目标</summary>
    public List<BattleRuleTargetRef> PrimaryTargets = new List<BattleRuleTargetRef>();
}

/// <summary>出牌合法性失败归因。</summary>
public enum BattleCardRuleLegalityKind
{
    None = 0,

    /// <summary>合法</summary>
    Legal = 1,

    /// <summary>非法手选目标（不在合法目标集合内）</summary>
    IllegalManualTarget = 2,

    /// <summary>不存在任何合法手选目标</summary>
    NoLegalManualTarget = 3,

    /// <summary>Primary Resolution 不存在合法结果</summary>
    PrimaryResolutionUnavailable = 4
}
