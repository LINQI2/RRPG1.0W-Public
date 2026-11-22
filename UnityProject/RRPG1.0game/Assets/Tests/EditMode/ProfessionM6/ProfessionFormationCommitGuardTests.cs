using NUnit.Framework;

/// <summary>
/// M6 专项 — Formation Commit 前置校验（Stage 3：必须精确 PersonId、只允许普通 Person、
/// ActiveProfessionProgress == null、Target/Candidate/Stability 合法、Registry 唯一合法、unknown 不改投）。
/// </summary>
public class ProfessionFormationCommitGuardTests
{
    [Test]
    public void Commit_DirectCalls_RejectIllegalPreconditions()
    {
        var registry = ProfessionFormationCommitTests.FarmerRegistry();
        var cfg = ProfessionFormationCommitTests.Fast();
        var day = new FormationDayContext(1, 1, 1);
        const string farmer = ProfessionFormationCommitTests.Farmer;

        // 空 PersonId → NotOrdinaryPerson（精确 PersonId 前提；不用 CharacterId/HeroId 兜底）。
        var empty = new M6TestFormationHost("");
        empty.FormationState.GetOrAddEntry(farmer).IsCandidate = true;
        Assert.AreEqual(FormationCommitStatus.NotOrdinaryPerson,
            FormationCommitter.Commit(empty, farmer, day, cfg, registry).Status);

        // 非 Candidate → NotCandidate。
        var notCandidate = new M6TestFormationHost("person_m6_pc1");
        notCandidate.FormationState.GetOrAddEntry(farmer).Stability = 20f;
        notCandidate.FormationState.FormationTargetProfessionId = farmer;
        Assert.AreEqual(FormationCommitStatus.NotCandidate,
            FormationCommitter.Commit(notCandidate, farmer, day, cfg, registry).Status);

        // Candidate 但不是 Target → NotFormationTarget（禁止点击旁路直接指定职业）。
        var wrongTarget = new M6TestFormationHost("person_m6_pc2");
        wrongTarget.FormationState.GetOrAddEntry(farmer).IsCandidate = true;
        wrongTarget.FormationState.FormationTargetProfessionId = "profession_test_other";
        Assert.AreEqual(FormationCommitStatus.NotFormationTarget,
            FormationCommitter.Commit(wrongTarget, farmer, day, cfg, registry).Status);

        // Stability 不足 → StabilityBelowThreshold。
        var lowStability = ProfessionFormationCommitTests.NewFarmerCandidateHost("person_m6_pc3");
        lowStability.FormationState.FindEntry(farmer).Stability = 0.01f;
        Assert.AreEqual(FormationCommitStatus.StabilityBelowThreshold,
            FormationCommitter.Commit(lowStability, farmer, day, cfg, registry).Status);

        // 已有 Active Profession → AlreadyHasActiveProfession（重复 Commit no-op）。
        var active = ProfessionFormationCommitTests.NewFarmerCandidateHost("person_m6_pc4");
        var first = FormationCommitter.Commit(active, farmer, day, cfg, registry);
        Assert.IsTrue(first.IsSuccess);
        Assert.AreEqual(FormationCommitStatus.AlreadyHasActiveProfession,
            FormationCommitter.Commit(active, farmer, day, cfg, registry).Status);

        // unknown Profession（Registry 不含该模板）→ UnknownProfession，且不改投其它职业。
        var unknown = ProfessionFormationCommitTests.NewFarmerCandidateHost("person_m6_pc5");
        var unknownResult = FormationCommitter.Commit(unknown, "profession_test_not_in_registry", day, cfg, registry);
        Assert.AreEqual(FormationCommitStatus.UnknownProfession, unknownResult.Status);
        Assert.IsFalse(unknown.HasActiveProfession, "unknown Profession 不得被改投/伪造成功");
        // 既有 Farmer 条目原样保留（ProfessionId 未被改写成 unknown / 不把 unknown 投成 Farmer 身份）。
        var farmerEntry = unknown.FormationState.FindEntry(ProfessionFormationCommitTests.Farmer);
        Assert.IsNotNull(farmerEntry, "原 Candidate 条目仍应存在");
        Assert.AreEqual(ProfessionFormationCommitTests.Farmer, farmerEntry.ProfessionId);
        Assert.IsNull(unknown.FormationState.FindEntry("profession_test_not_in_registry"),
            "Commit 失败不得新增 unknown 条目改投到其它职业");

        // null Registry → UnknownProfession（不 Commit、不猜 Registry）。
        var noRegistry = ProfessionFormationCommitTests.NewFarmerCandidateHost("person_m6_pc6");
        Assert.AreEqual(FormationCommitStatus.UnknownProfession,
            FormationCommitter.Commit(noRegistry, farmer, day, cfg, null).Status);
    }

    [Test]
    public void ActiveProfession_BlocksCommitAndNoTransition_EvidenceContinuesToChange()
    {
        var host = ProfessionFormationCommitTests.NewFarmerCandidateHost("person_m6_no_transition");
        var registry = ProfessionFormationCommitTests.FarmerRegistry();
        var cfg = ProfessionFormationCommitTests.Fast();
        const string farmer = ProfessionFormationCommitTests.Farmer;

        var first = FormationCommitter.Commit(host, farmer, new FormationDayContext(1, 3, 1), cfg, registry);
        Assert.IsTrue(first.IsSuccess);
        float farmerStrengthAtFormation = host.FormationState.FindEntry(farmer).Strength;

        // 形成后继续生活：另一个 Profession（smith）的 Evidence 继续增长。
        int smithDays = 12;
        for (int i = 0; i < smithDays; i++)
        {
            var day = new FormationDayContext(1, 3, 2 + i);
            var report = FormationResolver.AdvanceDay(host, day,
                new System.Collections.Generic.List<FormationEvidenceContribution>
                {
                    ProfessionM6TestKit.Work("profession_test_smith", 1f),
                    ProfessionM6TestKit.Work(farmer, 1f)
                }, cfg, registry);
            Assert.IsNull(report.Formation, "已形成 Person 的 Resolver 不得再次自动 Commit / Transition");
        }

        var smith = host.FormationState.FindEntry("profession_test_smith");
        Assert.IsNotNull(smith, "形成后其它 Profession Evidence 应建立条目");
        Assert.Greater(smith.Strength, 0f, "形成后其它 Profession Evidence 继续变化");
        Assert.AreEqual(farmer, host.ActiveProfessionProgress.ProfessionId, "Active Profession 不得被自动替换（无 Transition）");
        Assert.IsTrue(host.FormationState.FindEntry(farmer).Strength >= farmerStrengthAtFormation - 1e-3f,
            "形成后原 Profession Evidence 也继续增长/保留（不清零）");
        Assert.AreEqual(1, host.LifeHistory.CountProfessionFormed(farmer), "Life History 仍只记录一次");
    }
}
