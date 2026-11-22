using System;

/// <summary>
/// M6-A Formation Test / Not Frozen 数值与算法配置（制作人裁决 1）。
///
/// 允许使用可注入的 Test / Not Frozen Formation Config 验证：
///   Strength growth / decay、Qualification、Candidate hysteresis、Dominance、Stability、Ambiguity。
/// 这些参数不得成为正式游戏数值，不得在正式产品路径中被描述为 Frozen / Current 数值。
/// Runtime Core 只依赖本配置的注入（由调用方 / 桥提供 FormationRuntimeServices），
/// 不内置任何「正式」默认值；正式数值落地后可整组替换本配置。
///
/// 成长语义（§12.6 方向规则）：
///   - 增长：慢 → 快 → 慢 —— 用 Logistic 累计成长曲线的逐日增量表达（S 形累计）。
///     每天有该 Profession 的有效 Evidence 时 ActiveCareerDays +1，Strength 增加当日曲线增量；
///   - 衰减：慢 → 快 → 慢 —— 用 Logistic 累计损失的逐日增量表达。
///     衰减只依据「距该 Profession 上一次有效相关活动已经中断多久」（InactiveDays）。
///
/// 阈值 / Dominance / Stability / Ambiguity 语义（§12.8 / §12.11 / §12.12 / §12.13）：
///   统一 Qualification 语义尺度；Candidate 带轻度滞后退出；Target 由 Candidate 与
///   Dominance Gap 决定；Stability 在持续一致 Target 时增长、Ambiguous/转向时回落。
/// 本类全部为测试值（Test / Not Frozen）。
/// </summary>
public class FormationTestConfig
{
    /// <summary>Strength 参考上限（语义尺度；Test / Not Frozen）。</summary>
    public float StrengthMax = 100f;

    /// <summary>Qualification Threshold：Strength 达到该值进入成熟区 → Formation Candidate（§12.8）。</summary>
    public float QualificationThreshold = 45f;

    /// <summary>Candidate 轻度滞后：已是 Candidate 时，Strength 跌到 Threshold - Hysteresis 以下才退出。</summary>
    public float CandidateExitHysteresis = 5f;

    /// <summary>Dominance 所需 Strength 领先幅度（多 Candidate 且无此 Gap → Ambiguous；§12.11）。</summary>
    public float DominanceStrengthGap = 10f;

    /// <summary>Target 持续一致时 Stability 每日增量（§12.12）。</summary>
    public float StabilityGainPerConsistentDay = 0.4f;

    /// <summary>Target 明确转向时，旧 Target Stability 的损失（更快下降；一次性损失）。</summary>
    public float StabilityLossOnTargetChange = 6f;

    /// <summary>非 Target / 无 Target 时的轻量被动衰减（每日；不参与 Strength）。</summary>
    public float StabilityPassiveDecayPerDay = 0.06f;

    /// <summary>Ambiguous / 无 Target 期间 Stability 缓慢下降（每日）。</summary>
    public float StabilityDecayPerAmbiguousDay = 0.3f;

    /// <summary>自动 Formation 所需的 Stability 下限（§12.13：Candidate + 明确 Target + 足够 Stability）。</summary>
    public float FormationStabilityThreshold = 10f;

    /// <summary>Stability 累积上限（防御性封顶；Test / Not Frozen）。</summary>
    public float StabilityMax = 30f;

    // ---------- 成长曲线（Logistic 累计成长；慢 → 快 → 慢） ----------

    /// <summary>成长 Logistic 陡度（Test / Not Frozen）。</summary>
    public float GrowthSteepness = 0.12f;

    /// <summary>成长 Logistic 中点（累计有效相关活动日；Test / Not Frozen）。</summary>
    public float GrowthMidDay = 60f;

    // ---------- 衰减曲线（Logistic 累计损失；慢 → 快 → 慢） ----------

    /// <summary>长期中断后最大损失占 StrengthMax 的比例上限（保留残余记忆，不归零到无意义的强度语义）。</summary>
    public float DecayMaxFraction = 0.85f;

    /// <summary>衰减 Logistic 陡度（Test / Not Frozen）。</summary>
    public float DecaySteepness = 0.1f;

    /// <summary>衰减 Logistic 中点（中断天数；Test / Not Frozen）。</summary>
    public float DecayMidDay = 40f;

    /// <summary>当前「累计有效相关活动日」d 对应的累计成长值（S 形累计；d&lt;=0 为 0）。</summary>
    public float CumulativeGrowth(int activeCareerDays)
    {
        if (activeCareerDays <= 0) return 0f;
        float at = Logistic(GrowthSteepness * (activeCareerDays - GrowthMidDay));
        float atZero = Logistic(-GrowthSteepness * GrowthMidDay);
        return Math.Max(0f, StrengthMax * (at - atZero));
    }

    /// <summary>第 d 个有效相关活动日的成长增量（= 累计成长曲线逐日差；慢 → 快 → 慢）。</summary>
    public float GrowthDelta(int activeCareerDays)
    {
        if (activeCareerDays <= 0) return 0f;
        return Math.Max(0f, CumulativeGrowth(activeCareerDays) - CumulativeGrowth(activeCareerDays - 1));
    }

    /// <summary>中断第 inactiveDays 天的累计损失（S 形累计损失；inactiveDays&lt;=0 为 0）。</summary>
    public float CumulativeDecayLoss(int inactiveDays)
    {
        if (inactiveDays <= 0) return 0f;
        float at = Logistic(DecaySteepness * (inactiveDays - DecayMidDay));
        float atZero = Logistic(-DecaySteepness * DecayMidDay);
        return Math.Max(0f, DecayMaxFraction * StrengthMax * (at - atZero));
    }

    /// <summary>中断第 inactiveDays 天的当日衰减量（= 累计损失曲线逐日差；慢 → 快 → 慢）。</summary>
    public float DecayDelta(int inactiveDays)
    {
        if (inactiveDays <= 0) return 0f;
        return Math.Max(0f, CumulativeDecayLoss(inactiveDays) - CumulativeDecayLoss(inactiveDays - 1));
    }

    private static float Logistic(float x)
    {
        return 1f / (1f + (float)Math.Exp(-x));
    }
}
