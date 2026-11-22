using System.Collections.Generic;
using NUnit.Framework;

/// <summary>
/// M6 专项 — CharacterSaveData additive Formation / LifeHistory 存档（SaveVersion 保持 1）：
/// 空状态往返、已形成状态往返（Evidence/Starter/Life History 连续）。
/// </summary>
public class ProfessionFormationSaveTests
{
    [SetUp]
    public void SetUp()
    {
        ProfessionM6TestKit.DeleteSave();
        ProfessionM6TestKit.ResetBridge();
    }

    [TearDown]
    public void TearDown()
    {
        ProfessionM6TestKit.DeleteSave();
        ProfessionM6TestKit.ResetBridge();
    }

    [Test]
    public void SaveVersion_StaysOne()
    {
        int current = (int)ProfessionM6TestKit.GetStaticField("SaveSystem", "CurrentSaveVersion");
        Assert.AreEqual(1, current, "SaveVersion 保持 1（additive 增量，不提升）");
    }

    [Test]
    public void EmptyFormationState_SaveLoad_RestoresEmptyState()
    {
        object collection = ProfessionM6TestKit.NewPlayerCollection();
        ProfessionM6TestKit.OwnedCharacters(collection)
            .Add(ProfessionM6TestKit.NewCharacter("char_001", "农夫甲", 30, 6));
        Assert.IsTrue(ProfessionM6TestKit.Save(collection));

        object loaded = ProfessionM6TestKit.Load();
        Assert.IsNotNull(loaded);
        object loadedChar = ProfessionM6TestKit.OwnedCharacters(loaded)[0];
        var host = ProfessionM6TestKit.AsHost(loadedChar);

        Assert.IsFalse(host.HasActiveProfession, "空 Formation 状态不得自动 Formation");
        Assert.IsNotNull(host.FormationState, "Load 后 FormationState 非 null（空状态合法）");
        Assert.AreEqual(0, host.FormationState.Entries.Count);
        Assert.IsNotNull(host.LifeHistory, "Load 后 Life History 非 null（空记录合法）");
        Assert.AreEqual(0, host.LifeHistory.Events.Count);
    }

    [Test]
    public void CommittedState_SaveLoad_PreservesProgressFormationAndLifeHistory()
    {
        object collection = ProfessionM6TestKit.NewPlayerCollection();
        object character = ProfessionM6TestKit.NewCharacter("char_001", "农夫甲", 30, 6);
        ProfessionM6TestKit.OwnedCharacters(collection).Add(character);

        var host = ProfessionM6TestKit.AsHost(character);
        var entry = host.FormationState.GetOrAddEntry(ProfessionFormationCommitTests.Farmer);
        entry.Strength = 66f;
        entry.ActiveCareerDays = 21;
        entry.IsCandidate = true;
        entry.Stability = 9f;
        host.FormationState.FormationWindowOpen = true;
        host.FormationState.FormationTargetProfessionId = ProfessionFormationCommitTests.Farmer;

        var other = host.FormationState.GetOrAddEntry("profession_test_unknown_kept");
        other.Strength = 3f;
        other.ActiveCareerDays = 4;

        Assert.IsTrue(FormationCommitter.Commit(host, ProfessionFormationCommitTests.Farmer,
            new FormationDayContext(1, 6, 15), ProfessionFormationCommitTests.Fast(),
            ProfessionFormationCommitTests.FarmerRegistry()).IsSuccess);
        Assert.IsTrue(ProfessionM6TestKit.Save(collection));

        object loaded = ProfessionM6TestKit.Load();
        Assert.IsNotNull(loaded);
        object loadedChar = ProfessionM6TestKit.OwnedCharacters(loaded)[0];
        var restored = ProfessionM6TestKit.AsHost(loadedChar);

        Assert.IsTrue(restored.HasActiveProfession, "已形成 Profession 的 Save→Load 后仍应 Active");
        Assert.AreEqual(ProfessionFormationCommitTests.Farmer, restored.ActiveProfessionProgress.ProfessionId);
        Assert.AreEqual(1, restored.ActiveProfessionProgress.ProfessionLevel);
        Assert.AreEqual(0, restored.ActiveProfessionProgress.ProfessionExp);
        Assert.AreEqual(1, restored.ActiveProfessionProgress.ProfessionTraitLevel);
        CollectionAssert.AreEqual(new[] { "card_f1", "card_f2" },
            restored.ActiveProfessionProgress.UnlockedPoolCardIds);

        // Formation Evidence 在 Save/Load 后保留（不清零）；unknown 条目保留原样。
        Assert.AreEqual(2, restored.FormationState.Entries.Count, "Farmer + unknown 两条 Evidence 都应保留");
        Assert.AreEqual(66f, restored.FormationState.FindEntry(ProfessionFormationCommitTests.Farmer).Strength, 1e-3f,
            "Formation Evidence/Strength 存档连续");
        Assert.IsNotNull(restored.FormationState.FindEntry("profession_test_unknown_kept"),
            "unknown Profession 条目不得在 Save/Load 中被丢弃或改投");
        Assert.AreEqual(3f, restored.FormationState.FindEntry("profession_test_unknown_kept").Strength, 1e-3f);

        // Life History 恰好一次（含日期）。
        Assert.AreEqual(1, restored.LifeHistory.CountProfessionFormed(ProfessionFormationCommitTests.Farmer));
        Assert.AreEqual(1, restored.LifeHistory.Events.Count);
        Assert.AreEqual(1, restored.LifeHistory.Events[0].Year);
        Assert.AreEqual(6, restored.LifeHistory.Events[0].Month);
        Assert.AreEqual(15, restored.LifeHistory.Events[0].Day);

        // Starter 原子替换结果同样存档连续。
        var cardIds = new List<string>();
        foreach (var s in restored.StarterSlots) cardIds.Add(s.CardId);
        CollectionAssert.AreEqual(new[] { "card_f1", "card_f3" }, cardIds,
            "Formation 原子替换后的 Starter 应随 Save/Load 连续");
    }
}
