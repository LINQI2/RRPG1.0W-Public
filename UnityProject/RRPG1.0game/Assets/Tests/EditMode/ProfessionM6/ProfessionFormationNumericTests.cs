using System.Collections.Generic;
using NUnit.Framework;

/// <summary>
/// M6 专项 — Formation 数值 / 算法行为（Strength growth·decay、Qualification、
/// Candidate hysteresis、Dominance、Stability、Ambiguity）。全部为 Test / Not Frozen 配置，
/// 不冻结任何正式数值。使用 M6TestFormationHost（纯逻辑替身）。
/// </summary>
public class ProfessionFormationNumericTests
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
    public void GrowthCurve_IsSlowFastSlow()
    {
        var cfg = new FormationTestConfig { StrengthMax = 100f, GrowthSteepness = 0.12f, GrowthMidDay = 60f };
        float early = cfg.GrowthDelta(1);
        float mid = cfg.GrowthDelta(60);
        float late = cfg.GrowthDelta(150);
        Assert.Greater(early, 0f, "成长首日应有正增量");
        Assert.Greater(mid, early, "成长应 慢→快（中期日增量大于早期）");
        Assert.Greater(mid, late, "成长应 快→慢（后期日增量小于中期）");
        Assert.Greater(late, 0f, "长期仍有极缓慢增长（不应归零到停止成长）");
    }

    [Test]
    public void DecayCurve_IsSlowFastSlow()
    {
        var cfg = new FormationTestConfig { StrengthMax = 100f, DecayMaxFraction = 0.85f, DecaySteepness = 0.1f, DecayMidDay = 40f };
        float early = cfg.DecayDelta(1);
        float mid = cfg.DecayDelta(40);
        float late = cfg.DecayDelta(150);
        Assert.Greater(early, 0f, "中断后应有正衰减量");
        Assert.Greater(mid, early, "衰减应 慢→快（中断中期日衰减大于早期）");
        Assert.Greater(mid, late, "衰减应 快→慢（长期后日衰减回落）");
        Assert.Greater(cfg.CumulativeDecayLoss(500), cfg.CumulativeDecayLoss(100), "累计损失应随时间累积");
        Assert.Less(cfg.CumulativeDecayLoss(5000), cfg.StrengthMax, "累计损失应有上限（保留残余职业记忆）");
    }

    [Test]
    public void Strength_GrowsWithWorkAndDecaysOnInterruption()
    {
        var host = new M6TestFormationHost("person_m6_numeric");
        var cfg = new FormationTestConfig
        {
            StrengthMax = 100f,
            QualificationThreshold = 90f, // 只看 Strength 演化，不形成
            CandidateExitHysteresis = 5f,
            DominanceStrengthGap = 10f,
            FormationStabilityThreshold = 100f, // 永不形成
            GrowthSteepness = 0.3f,
            GrowthMidDay = 10f,
            DecayMaxFraction = 0.85f,
            DecaySteepness = 0.1f,
            DecayMidDay = 20f
        };

        var start = new FormationDayContext(1, 1, 1);
        for (int i = 0; i < 10; i++)
        {
            var day = new FormationDayContext(start.Year, start.Month, start.Day + i);
            FormationResolver.AdvanceDay(host, day, new List<FormationEvidenceContribution>
            {
                ProfessionM6TestKit.Work("profession_test_a", 1f)
            }, cfg, null);
        }
        var entryA = host.FormationState.FindEntry("profession_test_a");
        Assert.IsNotNull(entryA, "有 Work Evidence 的职业应建立 Person×Profession 独立条目");
        Assert.AreEqual(10, entryA.ActiveCareerDays, "每天实际工作一天 → ActiveCareerDays 精确 +1（不是累计 TotalExecutedDays 驱动）");
        float grown = entryA.Strength;
        Assert.Greater(grown, 0f, "Strength 应随实际工作增长");

        // 中断 15 天（无任何贡献）：只按中断衰减，不得因 Assignment 挂名继续获得 Evidence。
        for (int i = 0; i < 15; i++)
        {
            var day = new FormationDayContext(start.Year, start.Month, start.Day + 10 + i);
            FormationResolver.AdvanceDay(host, day, null, cfg, null);
        }
        Assert.AreEqual(15, entryA.InactiveDays, "中断天数应精确累计");
        Assert.Greater(grown, entryA.Strength, "长期中断应衰减 Strength（慢→快→慢）");

        // 恢复工作 10 天：Strength 重新上升。
        float afterDecay = entryA.Strength;
        for (int i = 0; i < 10; i++)
        {
            var day = new FormationDayContext(start.Year, start.Month, start.Day + 25 + i);
            FormationResolver.AdvanceDay(host, day, new List<FormationEvidenceContribution>
            {
                ProfessionM6TestKit.Work("profession_test_a", 1f)
            }, cfg, null);
        }
        Assert.Greater(entryA.Strength, afterDecay, "恢复有效工作后 Strength 应重新增长");
        Assert.AreEqual(0, entryA.InactiveDays, "恢复活动当天中断清零");
    }
}
