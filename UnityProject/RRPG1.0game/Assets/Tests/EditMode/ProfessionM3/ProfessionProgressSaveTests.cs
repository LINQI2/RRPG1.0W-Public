using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;

/// <summary>
/// M3 专项 — Profession Progress Save Foundation（真实 SaveSystem / VillageRuntimeBridge，经反射）：
///   Test 5  无 ActiveProfessionProgress 的 Character：Save → Load 仍为 null；
///   Test 6  有 Active Profession 的 Character：Save → Load 后五字段全部一致；
///   Test 7  旧格式无 ProfessionProgress 存档：Load 成功、ActiveProfessionProgress = null；
///   Test 8  不自动 Formation：CharacterId / 旧 Level/Exp 不会推导出 ProfessionId；
///   Test 9  Hero Guard（Save 层）：Hero Save→Load 不受影响、无 Profession 结构。
/// </summary>
public class ProfessionProgressSaveTests
{
    [SetUp]
    public void SetUp()
    {
        ProfessionM3TestKit.DeleteSave();
        ProfessionM3TestKit.ResetBridge();
    }

    [TearDown]
    public void TearDown()
    {
        ProfessionM3TestKit.DeleteSave();
        ProfessionM3TestKit.ResetBridge();
    }

    private static object NewCollectionWithCharacter()
    {
        object collection = ProfessionM3TestKit.NewPlayerCollection();
        ProfessionM3TestKit.OwnedCharacters(collection).Add(ProfessionM3TestKit.NewCharacter("char_001", "测试人物", 30, 6));
        return collection;
    }

    private static void AssertSameProgress(ProfessionProgress expected, ProfessionProgress actual)
    {
        Assert.IsNotNull(actual, "Save→Load 后 ActiveProfessionProgress 不应为 null");
        Assert.AreEqual(expected.ProfessionId, actual.ProfessionId, "ProfessionId 必须一致");
        Assert.AreEqual(expected.ProfessionLevel, actual.ProfessionLevel, "ProfessionLevel 必须一致");
        Assert.AreEqual(expected.ProfessionExp, actual.ProfessionExp, "ProfessionExp 必须一致");
        CollectionAssert.AreEqual(expected.UnlockedPoolCardIds, actual.UnlockedPoolCardIds, "UnlockedPool 必须一致");
        Assert.AreEqual(expected.ProfessionTraitLevel, actual.ProfessionTraitLevel, "ProfessionTraitLevel 必须一致");
    }

    [Test]
    public void CharacterWithoutProgress_SaveLoad_RestoresNull()
    {
        // Test 5 — null ActiveProfessionProgress 的 Save/Load 往返后仍为 null（合法）
        object collection = NewCollectionWithCharacter();
        Assert.IsTrue(ProfessionM3TestKit.Save(collection), "Save 应成功");

        object loaded = ProfessionM3TestKit.Load();
        Assert.IsNotNull(loaded, "Load 应成功");
        Assert.AreEqual(1, ProfessionM3TestKit.OwnedCharacters(loaded).Count);

        object loadedCharacter = ProfessionM3TestKit.OwnedCharacters(loaded)[0];
        Assert.IsNull(ProfessionM3TestKit.ActiveProgress(loadedCharacter),
            "无 Active Profession 的 Character：Save → Load 后仍必须是 null");
        Assert.IsFalse(ProfessionM3TestKit.HasActiveProfession(loadedCharacter));
    }

    [Test]
    public void CharacterWithProgress_SaveLoad_PreservesAllFields()
    {
        // Test 6 — Active Profession 五字段 Save→Load 完全一致
        ProfessionProgress progress = ProfessionM3TestKit.NewHunterProgress();
        progress.ProfessionLevel = 3;
        progress.ProfessionExp = 80;
        progress.UnlockedPoolCardIds = new List<string> { "card_a", "card_b", "card_c" };
        progress.ProfessionTraitLevel = 2;

        object character = ProfessionM3TestKit.NewCharacter("char_001", "测试人物", 30, 6);
        ProfessionM3TestKit.Set(character, "ActiveProfessionProgress", progress);

        object collection = ProfessionM3TestKit.NewPlayerCollection();
        ProfessionM3TestKit.OwnedCharacters(collection).Add(character);
        Assert.IsTrue(ProfessionM3TestKit.Save(collection));

        object loaded = ProfessionM3TestKit.Load();
        Assert.IsNotNull(loaded);
        Assert.AreEqual(1, ProfessionM3TestKit.OwnedCharacters(loaded).Count);

        object loadedCharacter = ProfessionM3TestKit.OwnedCharacters(loaded)[0];
        Assert.IsTrue(ProfessionM3TestKit.HasActiveProfession(loadedCharacter));
        AssertSameProgress(progress, ProfessionM3TestKit.ActiveProgress(loadedCharacter));

        // 恢复的是独立新实例（不与存档数据 / 原实例共享 List）
        Assert.AreNotSame(progress, ProfessionM3TestKit.ActiveProgress(loadedCharacter));
        Assert.AreNotSame(progress.UnlockedPoolCardIds,
            ProfessionM3TestKit.ActiveProgress(loadedCharacter).UnlockedPoolCardIds);
    }

    [Test]
    public void SavedJson_ActiveProgressUsesSentinelFlag()
    {
        // Save Foundation 证据（Tuanjie JsonUtility 会把 null class 字段物化为默认实例，因此 Active 语义必须以
        // 显式哨兵 HasActiveProfessionProgress 表达）：有 Active Profession → true + 真实数据区块；
        // 无 Active（置空后）→ false，且不再携带任何真实职业内容。
        ProfessionProgress progress = ProfessionM3TestKit.NewHunterProgress();
        object character = ProfessionM3TestKit.NewCharacter("char_001", "测试人物", 30, 6);
        ProfessionM3TestKit.Set(character, "ActiveProfessionProgress", progress);

        object collection = ProfessionM3TestKit.NewPlayerCollection();
        ProfessionM3TestKit.OwnedCharacters(collection).Add(character);
        Assert.IsTrue(ProfessionM3TestKit.Save(collection));

        string jsonActive = File.ReadAllText(ProfessionM3TestKit.SavePath());
        string compactActive = jsonActive.Replace(" ", "").Replace("\t", "").Replace("\r", "").Replace("\n", "");
        StringAssert.Contains("\"HasActiveProfessionProgress\":true", compactActive,
            "有 Active Profession 的 Character 存档必须带 HasActiveProfessionProgress=true");
        StringAssert.Contains("\"ProfessionId\":\"profession_test_hunter\"", compactActive,
            "有 Active Profession 的 Character 存档必须包含真实 ProfessionId");
        StringAssert.Contains("\"ProfessionTraitLevel\"", compactActive);

        // 清空后再次保存：哨兵必须回到 false，且不再包含任何真实职业内容
        ProfessionM3TestKit.Set(character, "ActiveProfessionProgress", null);
        Assert.IsTrue(ProfessionM3TestKit.Save(collection));
        string jsonNull = File.ReadAllText(ProfessionM3TestKit.SavePath());
        string compactNull = jsonNull.Replace(" ", "").Replace("\t", "").Replace("\r", "").Replace("\n", "");
        StringAssert.Contains("\"HasActiveProfessionProgress\":false", compactNull,
            "无 Active Profession 的 Character 存档必须带 HasActiveProfessionProgress=false");
        Assert.IsFalse(compactNull.Contains("profession_test_hunter"),
            "无 Active Profession 的 Character 存档不应残留真实职业内容");
    }

    [Test]
    public void LegacySave_WithoutProfessionBlock_LoadsWithNullProgress()
    {
        // Test 7 — 旧格式无 ProfessionProgress：Load 成功 → ActiveProfessionProgress = null → 不自动生成 ProfessionId
        // 字符 JSON 为 M3 之前（M1/M2）的存档形态：无 ActiveProfessionProgress 字段。
        string json = "{\"SaveVersion\":1,\"GameVersion\":\"0.2.3\",\"SavedAtUnixTime\":0," +
                      "\"Player\":{\"Gold\":0,\"Heroes\":[],\"Characters\":[" +
                      "{\"CharacterId\":\"char_001\",\"PersonId\":\"person_old_m3_001\",\"Level\":5,\"Exp\":120," +
                      "\"CurrentAttack\":6,\"Injury\":0,\"IsDead\":false,\"MaxHP\":30}" +
                      "],\"OwnedCharacterCardIds\":[],\"OwnedItemCardIds\":[]}}";
        File.WriteAllText(ProfessionM3TestKit.SavePath(), json);

        object loaded = ProfessionM3TestKit.Load();
        Assert.IsNotNull(loaded, "旧档（无 ProfessionProgress）必须可加载");
        Assert.AreEqual(1, ProfessionM3TestKit.OwnedCharacters(loaded).Count);

        object loadedCharacter = ProfessionM3TestKit.OwnedCharacters(loaded)[0];
        Assert.AreEqual("person_old_m3_001", ProfessionM3TestKit.PersonIdOf(loadedCharacter),
            "旧档 PersonId 应保持");
        Assert.AreEqual("char_001", (string)ProfessionM3TestKit.Get(loadedCharacter, "CharacterId"),
            "CharacterId 应保持为模板 ID");
        Assert.IsNull(ProfessionM3TestKit.ActiveProgress(loadedCharacter),
            "旧档无 ProfessionProgress → Load 后必须为 null（不允许猜测职业）");
        Assert.IsFalse(ProfessionM3TestKit.HasActiveProfession(loadedCharacter));

        // 迁移稳定性：再次 Save → Load，仍为 null（不会“顺手补一个职业”）
        Assert.IsTrue(ProfessionM3TestKit.Save(loaded));
        object loaded2 = ProfessionM3TestKit.Load();
        Assert.IsNotNull(loaded2);
        Assert.IsNull(ProfessionM3TestKit.ActiveProgress(ProfessionM3TestKit.OwnedCharacters(loaded2)[0]),
            "旧档迁移后的第一次 Save→Load 不得自动 Formation");
    }

    [Test]
    public void OldLevelAndExp_DoNotAutoFormProfession()
    {
        // Test 8 — 不自动 Formation：CharacterId / 旧 Level / 旧 Exp 都不能推导 ProfessionId。
        // （Assignment 也在独立 Village 状态中，当前没有任何代码路径把 Assignment → Profession；此处校验人物侧不推断。）
        object character = ProfessionM3TestKit.NewCharacter("char_001", "资深人物", 30, 6);
        ProfessionM3TestKit.Set(character, "Level", 7);
        ProfessionM3TestKit.Set(character, "Exp", 340);

        object collection = ProfessionM3TestKit.NewPlayerCollection();
        ProfessionM3TestKit.OwnedCharacters(collection).Add(character);
        Assert.IsTrue(ProfessionM3TestKit.Save(collection));

        object loaded = ProfessionM3TestKit.Load();
        Assert.IsNotNull(loaded);
        object loadedCharacter = ProfessionM3TestKit.OwnedCharacters(loaded)[0];

        // Legacy 成长值原样保留，但不产生任何职业
        Assert.AreEqual(7, (int)ProfessionM3TestKit.Get(loadedCharacter, "Level"),
            "旧 Level 不应被清除（Legacy Compatibility Field 原样保留）");
        Assert.AreEqual(340, (int)ProfessionM3TestKit.Get(loadedCharacter, "Exp"),
            "旧 Exp 不应被清除");
        Assert.IsNull(ProfessionM3TestKit.ActiveProgress(loadedCharacter),
            "不能因为旧 Level/Exp 较高就自动伪造 Profession");
        Assert.IsFalse(ProfessionM3TestKit.HasActiveProfession(loadedCharacter));
    }

    [Test]
    public void HeroSaveLoad_RemainsUntouched_NoProfession()
    {
        // Test 9（Save 层）— Hero 保存/恢复路径完全不受 M3 影响，Hero 无 Profession 结构
        object hero = ProfessionM3TestKit.NewHero("hero_001", "青鹿", 35);
        ProfessionM3TestKit.Set(hero, "Level", 4);
        ProfessionM3TestKit.Set(hero, "Exp", 60);

        object collection = ProfessionM3TestKit.NewPlayerCollection();
        ProfessionM3TestKit.OwnedHeroes(collection).Add(hero);
        Assert.IsTrue(ProfessionM3TestKit.Save(collection));

        object loaded = ProfessionM3TestKit.Load();
        Assert.IsNotNull(loaded);
        Assert.AreEqual(1, ProfessionM3TestKit.OwnedHeroes(loaded).Count);

        object loadedHero = ProfessionM3TestKit.OwnedHeroes(loaded)[0];
        Assert.AreEqual(4, (int)ProfessionM3TestKit.Get(loadedHero, "Level"),
            "Hero Level 应原样保存/恢复");
        Assert.AreEqual(60, (int)ProfessionM3TestKit.Get(loadedHero, "Exp"),
            "Hero Exp 应原样保存/恢复");
        Assert.IsNull(
            loadedHero.GetType().GetProperty("ActiveProfessionProgress"),
            "HeroInstance 不应具备 ActiveProfessionProgress（Hero 不进入 M3）");
    }
}
