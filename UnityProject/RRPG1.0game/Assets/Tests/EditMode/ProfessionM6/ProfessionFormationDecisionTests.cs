using NUnit.Framework;

/// <summary>
/// M6 专项 — Qualification / Candidate hysteresis / Dominance / Stability / Ambiguity 决策行为
/// （Test / Not Frozen 配置；纯逻辑替身 M6TestFormationHost）。
/// </summary>
public class ProfessionFormationDecisionTests
{
    private static FormationDayContext Day1 => new FormationDayContext(1, 1, 1);

    private static FormationTestConfig DecisionConfig()
    {
        // 大尺度 + 极小衰减：便于精确设置 Strength 观察决策边界（Test / Not Frozen）。
        return new FormationTestConfig
        {
            StrengthMax = 1000f,
            QualificationThreshold = 500f,
            CandidateExitHysteresis = 50f,
            DominanceStrengthGap = 80f,
            StabilityGainPerConsistentDay = 1f,
            StabilityLossOnTargetChange = 6f,
            StabilityPassiveDecayPerDay = 0.1f,
            StabilityDecayPerAmbiguousDay = 0.2f,
            StabilityMax = 100f,
            FormationStabilityThreshold = 10f,
            GrowthSteepness = 0.1f,
            GrowthMidDay = 400f,
            DecayMaxFraction = 0.1f,
            DecaySteepness = 0.01f,
            DecayMidDay = 500f
        };
    }

    [Test]
    public void Qualification_ReachedThreshold_BecomesCandidate()
    {
        var host = new M6TestFormationHost("person_m6_qual");
        host.FormationState.GetOrAddEntry("profession_test_a").Strength = 520f; // ≥ Threshold 500
        host.FormationState.GetOrAddEntry("profession_test_a").IsCandidate = false;

        FormationResolver.AdvanceDay(host, Day1, null, DecisionConfig(), null);

        var entry = host.FormationState.FindEntry("profession_test_a");
        Assert.IsTrue(entry.IsCandidate, "Strength ≥ Qualification Threshold → Candidate");
        Assert.IsTrue(host.FormationState.FormationWindowOpen, "首个 Candidate 出现 → Formation Window 打开（§12.9）");
    }

    [Test]
    public void CandidateHysteresis_OnlyDropsBelowExitBound_AndDoesNotJoinBelowThreshold()
    {
        // 相同 Strength = 495（低于 Threshold 500、高于退出界 450）：
        // 已是 Candidate → 保持（轻度滞后）；从未 Candidate → 不进入。
        var cfg = DecisionConfig();

        var candidateHost = new M6TestFormationHost("person_m6_hyst_keep");
        candidateHost.FormationState.GetOrAddEntry("profession_test_a").Strength = 495f;
        candidateHost.FormationState.GetOrAddEntry("profession_test_a").IsCandidate = true;
        FormationResolver.AdvanceDay(candidateHost, Day1, null, cfg, null);
        Assert.IsTrue(candidateHost.FormationState.FindEntry("profession_test_a").IsCandidate,
            "已是 Candidate 且未跌破 Threshold - Hysteresis → 保持 Candidate（避免边界来回跳）");

        var freshHost = new M6TestFormationHost("person_m6_hyst_fresh");
        freshHost.FormationState.GetOrAddEntry("profession_test_a").Strength = 495f;
        freshHost.FormationState.GetOrAddEntry("profession_test_a").IsCandidate = false;
        FormationResolver.AdvanceDay(freshHost, Day1, null, cfg, null);
        Assert.IsFalse(freshHost.FormationState.FindEntry("profession_test_a").IsCandidate,
            "未达 Threshold 的新条目不得成为 Candidate（Qualification 是进入门槛）");

        // 跌破退出界 → Candidate 退出。
        var exitHost = new M6TestFormationHost("person_m6_hyst_exit");
        exitHost.FormationState.GetOrAddEntry("profession_test_a").Strength = 440f; // < 450
        exitHost.FormationState.GetOrAddEntry("profession_test_a").IsCandidate = true;
        FormationResolver.AdvanceDay(exitHost, Day1, null, cfg, null);
        Assert.IsFalse(exitHost.FormationState.FindEntry("profession_test_a").IsCandidate,
            "跌破 Threshold - Hysteresis → Candidate 退出");
    }

    [Test]
    public void ClearLeaderCandidate_BecomesTarget_AndStabilityAccumulates()
    {
        var host = new M6TestFormationHost("person_m6_target");
        var cfg = DecisionConfig();
        var a = host.FormationState.GetOrAddEntry("profession_test_a");
        a.Strength = 600f; a.IsCandidate = true;
        var b = host.FormationState.GetOrAddEntry("profession_test_b");
        b.Strength = 500f; b.IsCandidate = true;

        // 第 1 天：Target = 明确 Leader（600-500 ≥ DominanceGap 80）。
        var r1 = FormationResolver.AdvanceDay(host, Day1, null, cfg, null);
        Assert.AreEqual("profession_test_a", host.FormationState.FormationTargetProfessionId);
        Assert.IsFalse(host.FormationState.IsAmbiguous);
        Assert.AreEqual("profession_test_a", r1.TargetAfter);

        // 持续一致 Target：Stability 逐日累积（§12.12）。
        for (int i = 1; i <= 5; i++)
        {
            var day = new FormationDayContext(1, 1, 1 + i);
            FormationResolver.AdvanceDay(host, day, null, cfg, null);
        }
        Assert.Greater(host.FormationState.FindEntry("profession_test_a").Stability, 0f,
            "Target 持续一致 → Stability 应增长");
    }

    [Test]
    public void TwoEqualCandidates_Ambiguous_NoTarget()
    {
        var host = new M6TestFormationHost("person_m6_amb");
        var cfg = DecisionConfig();
        var a = host.FormationState.GetOrAddEntry("profession_test_a");
        a.Strength = 600f; a.IsCandidate = true;
        var b = host.FormationState.GetOrAddEntry("profession_test_b");
        b.Strength = 600f; b.IsCandidate = true;

        var report = FormationResolver.AdvanceDay(host, Day1, null, cfg, null);

        Assert.IsTrue(report.AmbiguousAfter, "双 Candidate 势均力敌无明确 Leader → Ambiguous");
        Assert.IsTrue(host.FormationState.IsAmbiguous);
        Assert.IsNull(host.FormationState.FormationTargetProfessionId, "Ambiguous 时无明确 Target");
        Assert.IsNull(report.Formation, "Ambiguous 不得 Commit");
    }
}
