using System.IO;
using NUnit.Framework;

/// <summary>
/// M6 专项 — 旧档兼容（SaveVersion 1，additive 增量）：
/// M6 前旧档无 Formation/LifeHistory → 空状态；绝不从 CharacterId / Assignment /
/// Legacy Growth / TotalExecutedDays 猜 Formation；Hero Save 层不进入 Formation。
/// </summary>
public class ProfessionFormationLegacySaveTests
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
    public void LegacySave_WithoutFormationBlocks_LoadsEmptyState_NoGuessing()
    {
        // 字符 JSON 为 M6 之前（M3/M5 时代）形态：无 FormationState / LifeHistory / ActiveProfessionProgress 字段。
        string json = "{\"SaveVersion\":1,\"GameVersion\":\"0.2.3\",\"SavedAtUnixTime\":0," +
                      "\"Player\":{\"Gold\":0,\"Heroes\":[],\"Characters\":[" +
                      "{\"CharacterId\":\"char_001\",\"PersonId\":\"person_old_m6_001\",\"Level\":5,\"Exp\":120," +
                      "\"CurrentAttack\":6,\"Injury\":0,\"IsDead\":false,\"MaxHP\":30}" +
                      "],\"OwnedCharacterCardIds\":[],\"OwnedItemCardIds\":[]}}";
        File.WriteAllText(ProfessionM6TestKit.SavePath(), json);

        object loaded = ProfessionM6TestKit.Load();
        Assert.IsNotNull(loaded, "旧档必须可加载");
        object loadedChar = ProfessionM6TestKit.OwnedCharacters(loaded)[0];
        var host = ProfessionM6TestKit.AsHost(loadedChar);

        Assert.AreEqual("person_old_m6_001", host.PersonId, "旧档 PersonId 应保持");
        Assert.IsFalse(host.HasActiveProfession, "旧档不得自动 Formation（无 Progress）");
        Assert.AreEqual(0, host.FormationState.Entries.Count, "旧档缺 Formation → 空状态");
        Assert.AreEqual(0, host.LifeHistory.Events.Count, "旧档缺 Life History → 空记录");
        Assert.AreEqual(5, (int)ProfessionM6TestKit.Get(loadedChar, "Level"), "Legacy Level 原样保留");
        Assert.AreEqual(120, (int)ProfessionM6TestKit.Get(loadedChar, "Exp"), "Legacy Exp 原样保留");

        // 迁移稳定性：再次 Save → Load 仍为空状态（不会“顺手补一个 Formation”）。
        Assert.IsTrue(ProfessionM6TestKit.Save(loaded));
        object loaded2 = ProfessionM6TestKit.Load();
        var host2 = ProfessionM6TestKit.AsHost(ProfessionM6TestKit.OwnedCharacters(loaded2)[0]);
        Assert.IsFalse(host2.HasActiveProfession);
        Assert.AreEqual(0, host2.FormationState.Entries.Count);
        Assert.AreEqual(0, host2.LifeHistory.Events.Count);
    }

    [Test]
    public void HeroSaveLoad_RemainsUntouched_NoFormationStructure()
    {
        object hero = ProfessionM6TestKit.NewHero("hero_001", "青鹿", 35);
        object collection = ProfessionM6TestKit.NewPlayerCollection();
        ProfessionM6TestKit.OwnedHeroes(collection).Add(hero);
        Assert.IsTrue(ProfessionM6TestKit.Save(collection));

        object loaded = ProfessionM6TestKit.Load();
        Assert.AreEqual(1, ProfessionM6TestKit.OwnedHeroes(loaded).Count);
        object loadedHero = ProfessionM6TestKit.OwnedHeroes(loaded)[0];
        var t = loadedHero.GetType();
        Assert.IsFalse(ProfessionM6TestKit.ImplementsIFormationHost(t), "Hero 永不进入 Formation（Save 层）");
        Assert.IsNull(t.GetProperty("FormationState"));
        Assert.IsNull(t.GetProperty("LifeHistory"));
    }
}
