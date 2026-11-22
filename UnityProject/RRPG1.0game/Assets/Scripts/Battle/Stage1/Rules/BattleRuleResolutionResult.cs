using System.Collections.Generic;

/// <summary>单个规则步骤的结算结果类别。</summary>
public enum BattleRuleStepOutcomeKind
{
    /// <summary>已成功执行</summary>
    Applied = 0,

    /// <summary>操作对本目标不适用（例如 ModifyDamage 作用于无 Damage 的牌），不影响整张牌结算</summary>
    NotApplicable = 1,

    /// <summary>执行被拒绝（例如空间写入非法）</summary>
    Failed = 2
}

/// <summary>单个规则步骤的结算记录（供 UI 日志与测试断言）。</summary>
public class BattleRuleStepOutcome
{
    public BattleRuleTargetRef TargetRef;
    public List<BattleRuleFactDelta> FactChanges = new List<BattleRuleFactDelta>();
    /// <summary>步骤操作</summary>
    public BattleRuleOperation Operation;

    /// <summary>结果类别</summary>
    public BattleRuleStepOutcomeKind Kind;

    /// <summary>本次实际使用的数值（含 Bonus；无数值操作时为 0）</summary>
    public int Value;

    /// <summary>目标描述</summary>
    public string Target = string.Empty;

    /// <summary>补充说明（NotApplicable / Failed 的原因）</summary>
    public string Note = string.Empty;

    /// <summary>该步骤是否附带了 Bonus</summary>
    public bool BonusApplied;

    public override string ToString()
    {
        return Operation + "(" + Value + ")->" + Target
            + (BonusApplied ? "[bonus]" : string.Empty)
            + (string.IsNullOrEmpty(Note) ? string.Empty : " {" + Note + "}");
    }
}

/// <summary>一张牌的完整结算结果。</summary>
public class BattleCardRuleResolutionResult
{
    /// <summary>按序记录的步骤结果</summary>
    public List<BattleRuleStepOutcome> Outcomes = new List<BattleRuleStepOutcome>();

    /// <summary>是否至少有一个步骤成功执行（用于判断“支付 AP 后核心效果完全空结算”）</summary>
    public bool AnyApplied
    {
        get
        {
            foreach (var outcome in Outcomes)
            {
                if (outcome.Kind == BattleRuleStepOutcomeKind.Applied) return true;
            }
            return false;
        }
    }

    /// <summary>可读结算摘要（BattleUI 日志使用）</summary>
    public string Describe()
    {
        if (Outcomes.Count == 0) return "无规则步骤";
        var parts = new List<string>();
        foreach (var outcome in Outcomes) parts.Add(outcome.ToString());
        return string.Join("; ", parts.ToArray());
    }
}
