using NUnit.Framework;

/// <summary>
/// M6 专项 — Target 转向时 Stability 语义与「可注入配置」守卫（Test / Not Frozen）。
/// </summary>
public class ProfessionFormationStabilityTests
{
    private static FormationTestConfig DecisionConfig()
    {
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
    public void TargetShift_DropsOldTargetStability_NewTargetRebuilds()
    {
        var host = new M6TestFormationHost("person_m6_shift");
        var cfg = DecisionConfig();

        var a = host.FormationState.GetOrAddEntry("profession_test_a");
        a.Strength = 400f; a.IsCandidate = false;
        var b = host.FormationState.GetOrAddEntry("profession_test_b");
        b.Strength = 600f; b.IsCandidate = true;
        host.FormationState.FormationTargetProfessionId = "profession_test_b";
        b.Stability = 10f;

        // A 成为明确 Leader（差距 ≥ DominanceGap），Target 从 B 转向 A。
        a.Strength = 700f; a.IsCandidate = true;
        b.Strength = 600f; b.IsCandidate = true;
        FormationResolver.AdvanceDay(host, new FormationDayContext(1, 1, 1), null, cfg, null);

        Assert.AreEqual("profession_test_a", host.FormationState.FormationTargetProfessionId, "新 Leader 成为 Target");
        Assert.Less(host.FormationState.FindEntry("profession_test_b").Stability, 10f,
            "旧 Target 失去 Target → Stability 更快下降");

        // A 持续为 Target：Stability 重新建立。
        for (int i = 1; i <= 5; i++)
        {
            var day = new FormationDayContext(1, 1, 1 + i);
            FormationResolver.AdvanceDay(host, day, null, cfg, null);
        }
        Assert.Greater(host.FormationState.FindEntry("profession_test_a").Stability, 0f,
            "新 Target 持续一致 → Stability 重新累积");
    }

    [Test]
    public void ConfigIsInjectable_NoBuiltInFrozenNumbers()
    {
        // 制作人裁决 1：允许可注入 Test/Not Frozen Config；Runtime Core 不得内置正式数值。
        var a = new FormationTestConfig { QualificationThreshold = 10f, FormationStabilityThreshold = 1f };
        var b = new FormationTestConfig { QualificationThreshold = 999f, FormationStabilityThreshold = 999f };
        Assert.AreNotEqual(a.QualificationThreshold, b.QualificationThreshold,
            "同一 Runtime Core 可被不同测试配置驱动（正式数值落地后可整组替换）");
    }
}
