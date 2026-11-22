using System.Collections.Generic;
using NUnit.Framework;

/// <summary>
/// M6 专项 — 原子 Formation Commit（§12.15 / Stage 3）：自动 Formation、Progress 内容、
/// Starter 原子替换、Evidence 不清零、Life History exactly-once、重复 Commit no-op、无 Strength 加成。
/// 内容全部为 Test / Not Frozen。
/// </summary>
public class ProfessionFormationCommitTests
{
    internal const string Farmer = "profession_test_farmer";
    internal const string FarmerTrait = "trait_profession_farmer";

    internal static FormationTestConfig Fast() => ProfessionM6TestKit.FastConfig();

    internal static ProfessionDefinitionRegistry FarmerRegistry()
    {
        return ProfessionM6TestKit.NewRegistry(ProfessionM6TestKit.NewDefinition(
            Farmer,
            new[] { "card_f1", "card_f2", "card_f3" },
            new[] { "card_f1", "card_f2" },
            FarmerTrait,
            new List<StarterSlotEntry>
            {
                new StarterSlotEntry("card_f1", 1),
                new StarterSlotEntry("card_f3", 2)
            }));
    }

    internal static M6TestFormationHost NewFarmerCandidateHost(string personId = "person_m6_commit")
    {
        var host = new M6TestFormationHost(personId);
        var entry = host.FormationState.GetOrAddEntry(Farmer);
        entry.Strength = 60f;
        entry.IsCandidate = true;
        host.FormationState.FormationWindowOpen = true;
        host.FormationState.FormationTargetProfessionId = Farmer;
        entry.Stability = 5f;
        return host;
    }

    [Test]
    public void Resolver_AutoForms_WhenCandidateTargetAndStabilityMet()
    {
        var host = new M6TestFormationHost("person_m6_auto");
        var registry = FarmerRegistry();
        var cfg = Fast();

        FormationDayReport last = null;
        int maxDays = 60;
        for (int i = 0; i < maxDays; i++)
        {
            var day = new FormationDayContext(1, 1, 1 + i);
            last = FormationResolver.AdvanceDay(host, day,
                new List<FormationEvidenceContribution> { ProfessionM6TestKit.Work(Farmer, 1f) },
                cfg, registry);
            if (host.HasActiveProfession) break;
        }

        Assert.IsTrue(host.HasActiveProfession, "Candidate + 明确 Target + 足够 Stability → 应自动首次 Formation");
        Assert.IsNotNull(last != null ? last.Formation : null, "触发日报告应携带 Formation 结果");
        if (last != null && last.Formation != null)
        {
            Assert.IsTrue(last.Formation.IsSuccess);
            Assert.AreEqual(FarmerTrait, last.Formation.TraitDefinitionId,
                "Trait Definition 来自 Profession Template 的 ProfessionTraitId");
        }
    }

    [Test]
    public void Commit_AtomicContent_DeepCopiesStarter_EvidenceRetained_LifeHistoryOnce()
    {
        object character = ProfessionM6TestKit.NewCharacter("char_001", "农夫甲", 30, 6);
        var host = ProfessionM6TestKit.AsHost(character);

        // 预设该 Person 的 Legacy/Test Starter（Formation 必须原子替换、绝不合并）。
        host.StarterSlots.Add(new StarterSlotEntry("legacy_card_x", 9));
        host.StarterSlots.Add(new StarterSlotEntry("legacy_card_y", 5));

        // 准备合法 Candidate / Target / Stability 状态。
        var entry = host.FormationState.GetOrAddEntry(Farmer);
        entry.Strength = 80f;
        entry.IsCandidate = true;
        entry.Stability = 8f;
        host.FormationState.FormationWindowOpen = true;
        host.FormationState.FormationTargetProfessionId = Farmer;

        var registry = FarmerRegistry();
        var definition = registry.GetById(Farmer);

        var result = FormationCommitter.Commit(host, Farmer,
            new FormationDayContext(1, 6, 15), Fast(), registry);

        Assert.IsTrue(result.IsSuccess, "合法状态 Commit 应成功: " + result.Status);
        Assert.AreEqual(FarmerTrait, result.TraitDefinitionId);

        Assert.IsTrue(host.HasActiveProfession);
        var progress = host.ActiveProfessionProgress;
        Assert.IsNotNull(progress);
        Assert.AreEqual(Farmer, progress.ProfessionId);
        Assert.AreEqual(1, progress.ProfessionLevel, "Level=1（不依据 Strength 高低奖励初始等级）");
        Assert.AreEqual(0, progress.ProfessionExp, "Exp=0");
        Assert.AreEqual(1, progress.ProfessionTraitLevel, "TraitLevel=1");
        CollectionAssert.AreEqual(definition.InitialUnlockedCards, progress.UnlockedPoolCardIds,
            "Unlocked Pool = clone 模板 InitialUnlockedCards");
        Assert.AreNotSame(definition.InitialUnlockedCards, progress.UnlockedPoolCardIds,
            "Unlocked Pool 不得共享模板 List 引用");

        // Starter 原子替换：Legacy/Test Starter 被整组替换为模板 InitialStarterBuild 深拷贝。
        Assert.AreEqual(2, host.StarterSlots.Count, "Legacy Starter 不得残留（原子替换，不合并）");
        Assert.AreEqual("card_f1", host.StarterSlots[0].CardId);
        Assert.AreEqual(1, host.StarterSlots[0].Proficiency);
        Assert.AreEqual("card_f3", host.StarterSlots[1].CardId);
        Assert.AreEqual(2, host.StarterSlots[1].Proficiency);
        Assert.AreNotSame(definition.InitialStarterBuild, host.StarterSlots, "Starter List 不得共享模板引用");
        Assert.AreNotSame(definition.InitialStarterBuild[0], host.StarterSlots[0], "Starter 条目必须深拷贝");

        // Formation Evidence 不清零：FormationState 条目原样保留（Strength 仍在）。
        var after = host.FormationState.FindEntry(Farmer);
        Assert.IsNotNull(after, "Formation Evidence 不得清零/删除");
        Assert.AreEqual(80f, after.Strength, 1e-3f, "Formation Evidence / Strength 在首次 Formation 后保留");

        // Life History 恰好一次 ProfessionFormed。
        Assert.IsTrue(host.LifeHistory.HasProfessionFormed(Farmer));
        Assert.AreEqual(1, host.LifeHistory.CountProfessionFormed(Farmer), "Life History 只追加一次 ProfessionFormed");
        Assert.AreEqual(1, host.LifeHistory.Events.Count, "M6 只记录 ProfessionFormed 一条事实");

        // 重复 Commit（同一 Person 再次 Commit）→ no-op（AlreadyHasActiveProfession）。
        var again = FormationCommitter.Commit(host, Farmer,
            new FormationDayContext(1, 7, 1), Fast(), registry);
        Assert.AreEqual(FormationCommitStatus.AlreadyHasActiveProfession, again.Status, "重复 Commit 必须 no-op");
        Assert.AreEqual(1, host.LifeHistory.CountProfessionFormed(Farmer), "重复 Commit 不得重复记录人生事实");
        Assert.AreSame(progress, host.ActiveProfessionProgress, "重复 Commit 不得替换已有 Progress");
    }

    [Test]
    public void Commit_NoInitialBonusFromHighStrength()
    {
        var host = NewFarmerCandidateHost("person_m6_nobonus");
        host.FormationState.FindEntry(Farmer).Strength = 99f; // 极端高的 Evidence / Strength
        host.FormationState.FindEntry(Farmer).Stability = 50f;
        var registry = FarmerRegistry();

        var result = FormationCommitter.Commit(host, Farmer, new FormationDayContext(1, 1, 1), Fast(), registry);
        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(1, host.ActiveProfessionProgress.ProfessionLevel, "高强度不得转化为初始 Level 奖励");
        Assert.AreEqual(0, host.ActiveProfessionProgress.ProfessionExp, "高强度不得转化为初始 Exp 奖励");
        Assert.AreEqual(2, host.ActiveProfessionProgress.UnlockedPoolCardIds.Count,
            "高强度不得转化为额外初始卡池奖励（= 模板 InitialUnlockedCards 固定数）");
        Assert.AreEqual(2, host.StarterSlots.Count, "高强度不得转化为额外 Starter 奖励");
        Assert.AreEqual(1, host.ActiveProfessionProgress.ProfessionTraitLevel, "Trait 初始等级固定 1");
    }
}
