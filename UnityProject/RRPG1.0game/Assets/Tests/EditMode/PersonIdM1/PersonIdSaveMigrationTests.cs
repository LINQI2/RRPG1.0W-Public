using System;
using System.IO;
using System.Reflection;
using NUnit.Framework;

/// <summary>
/// M1 §15.3 / §15.4 / §15.5：PersonId Save/Load 往返稳定 + 旧档迁移。
/// 真实 SaveSystem（Assembly-CSharp 反射）+ DataManager（Editor Resources）。
/// </summary>
public class PersonIdSaveRoundTripTests
{
    private static Type SaveSystemType => M1TestKit.Asm("SaveSystem");
    private static Type VillageBridgeType => M1TestKit.Asm("VillageRuntimeBridge");

    private static string SavePath() => (string)M1TestKit.InvokeStatic(SaveSystemType, "GetSavePath");

    private static void DeleteSave()
    {
        string p = SavePath();
        if (p != null && File.Exists(p)) File.Delete(p);
    }

    [SetUp]
    public void SetUp()
    {
        DeleteSave();
        M1TestKit.InvokeStatic(VillageBridgeType, "ResetForTests");
    }

    [TearDown]
    public void TearDown()
    {
        DeleteSave();
        M1TestKit.InvokeStatic(VillageBridgeType, "ResetForTests");
    }

    private static object Save(object collection)
    {
        return M1TestKit.InvokeStatic(SaveSystemType, "Save", collection);
    }

    private static object Load()
    {
        return M1TestKit.InvokeStatic(SaveSystemType, "Load");
    }

    [Test]
    public void PersonId_RoundTrips_ThroughSaveLoad()
    {
        object character = M1TestKit.NewCharacter("char_001", "战士", 30);
        object hero = M1TestKit.NewHero();
        object collection = M1TestKit.NewPlayerCollection();
        M1TestKit.OwnedCharacters(collection).Add(character);
        M1TestKit.OwnedHeroes(collection).Add(hero);

        string pidBefore = M1TestKit.PersonIdOf(character);
        Assert.IsFalse(string.IsNullOrEmpty(pidBefore));
        Assert.IsTrue((bool)Save(collection), "Save 应成功");

        object loaded = Load();
        Assert.IsNotNull(loaded, "Load 应成功");
        Assert.AreEqual(1, M1TestKit.OwnedCharacters(loaded).Count);
        Assert.AreEqual(1, M1TestKit.OwnedHeroes(loaded).Count);

        string pidAfter = M1TestKit.PersonIdOf(M1TestKit.OwnedCharacters(loaded)[0]);
        Assert.AreEqual(pidBefore, pidAfter, "角色 PersonId Save→Load 必须完全一致");
        Assert.AreEqual(M1TestKit.PersonIdOf(hero), M1TestKit.PersonIdOf(M1TestKit.OwnedHeroes(loaded)[0]),
            "英雄 PersonId Save→Load 必须完全一致");
    }

    [Test]
    public void DuplicateTemplateTwoInstances_SaveLoad_BothKeepDistinctPersonIds()
    {
        object p1 = M1TestKit.NewCharacter("char_001", "战士甲", 30);
        object p2 = M1TestKit.NewCharacter("char_001", "战士乙", 30);
        object collection = M1TestKit.NewPlayerCollection();
        M1TestKit.OwnedCharacters(collection).Add(p1);
        M1TestKit.OwnedCharacters(collection).Add(p2);
        // 两张同模板卡分别绑定两个实例（模拟同模板双人共存）
        object card1 = M1TestKit.NewCharacterCard("ccard_dup_1", "char_001");
        object card2 = M1TestKit.NewCharacterCard("ccard_dup_2", "char_001");
        M1TestKit.OwnedCharacterCards(collection).Add(card1);
        M1TestKit.OwnedCharacterCards(collection).Add(card2);
        M1TestKit.InvokeInstance(collection, "BindCharacterCardOwner", "ccard_dup_1", M1TestKit.PersonIdOf(p1));
        M1TestKit.InvokeInstance(collection, "BindCharacterCardOwner", "ccard_dup_2", M1TestKit.PersonIdOf(p2));

        string pid1Before = M1TestKit.PersonIdOf(p1);
        string pid2Before = M1TestKit.PersonIdOf(p2);
        Assert.AreNotEqual(pid1Before, pid2Before);

        Assert.IsTrue((bool)Save(collection));
        object loaded = Load();
        Assert.IsNotNull(loaded);
        Assert.AreEqual(2, M1TestKit.OwnedCharacters(loaded).Count, "同模板两个实例都必须存在，不能合并");

        string pid1After = M1TestKit.PersonIdOf(M1TestKit.OwnedCharacters(loaded)[0]);
        string pid2After = M1TestKit.PersonIdOf(M1TestKit.OwnedCharacters(loaded)[1]);
        Assert.AreNotEqual(pid1After, pid2After, "Load 后两个实例 PersonId 必须仍不同");
        Assert.AreEqual(pid1Before, pid1After, "实例1 PersonId 往返一致");
        Assert.AreEqual(pid2Before, pid2After, "实例2 PersonId 往返一致");
    }

    [Test]
    public void OldSave_WithoutPersonId_MigratesStably()
    {
        // 旧档：CharacterSaveData 只有 CharacterId，无 PersonId；也无 OwnedCharacterCardOwners 绑定
        string json = "{\"SaveVersion\":1,\"GameVersion\":\"0.2.3\",\"SavedAtUnixTime\":0," +
                      "\"Player\":{\"Gold\":100,\"Heroes\":[],\"Characters\":[" +
                      "{\"CharacterId\":\"char_001\",\"Level\":1,\"Exp\":0,\"CurrentAttack\":6,\"Injury\":0,\"IsDead\":false,\"MaxHP\":30}" +
                      "],\"OwnedCharacterCardIds\":[\"ccard_old_char_001_1\"],\"OwnedItemCardIds\":[]}}";
        File.WriteAllText(SavePath(), json);

        object loaded1 = Load();
        Assert.IsNotNull(loaded1, "旧档必须可加载");
        Assert.AreEqual(1, M1TestKit.OwnedCharacters(loaded1).Count);
        string pid1 = M1TestKit.PersonIdOf(M1TestKit.OwnedCharacters(loaded1)[0]);
        Assert.IsFalse(string.IsNullOrEmpty(pid1), "旧档实例 Load 后应自动生成 PersonId");
        Assert.AreNotEqual(pid1, "char_001", "不能用 CharacterId 充当 PersonId");
        // 旧档生成后立即回写一次（迁移稳定性）
        Assert.IsTrue((bool)Save(loaded1), "迁移后第一次保存应成功");

        object loaded2 = Load();
        Assert.IsNotNull(loaded2);
        Assert.AreEqual(1, M1TestKit.OwnedCharacters(loaded2).Count);
        string pid2 = M1TestKit.PersonIdOf(M1TestKit.OwnedCharacters(loaded2)[0]);
        Assert.AreEqual(pid1, pid2, "旧档第一次 Load 生成 PersonId 后，再次 Load 必须读取同一 PersonId");
    }

    [Test]
    public void OldSave_TwoSameCharacterIdInstances_MigrateToDistinctPersonIds()
    {
        string json = "{\"SaveVersion\":1,\"GameVersion\":\"0.2.3\",\"SavedAtUnixTime\":0," +
                      "\"Player\":{\"Gold\":100,\"Heroes\":[],\"Characters\":[" +
                      "{\"CharacterId\":\"char_001\",\"Level\":1,\"Exp\":0,\"CurrentAttack\":6,\"Injury\":0,\"IsDead\":false,\"MaxHP\":30}," +
                      "{\"CharacterId\":\"char_001\",\"Level\":1,\"Exp\":0,\"CurrentAttack\":6,\"Injury\":0,\"IsDead\":false,\"MaxHP\":30}" +
                      "],\"OwnedCharacterCardIds\":[\"ccard_old_char_001_1\",\"ccard_old_char_001_2\"],\"OwnedItemCardIds\":[]}}";
        File.WriteAllText(SavePath(), json);

        object loaded = Load();
        Assert.IsNotNull(loaded, "同模板两条实例的旧档必须可加载");
        Assert.AreEqual(2, M1TestKit.OwnedCharacters(loaded).Count, "两条同模板实例都必须存在，不能合并");

        string pidA = M1TestKit.PersonIdOf(M1TestKit.OwnedCharacters(loaded)[0]);
        string pidB = M1TestKit.PersonIdOf(M1TestKit.OwnedCharacters(loaded)[1]);
        Assert.IsFalse(string.IsNullOrEmpty(pidA));
        Assert.IsFalse(string.IsNullOrEmpty(pidB));
        Assert.AreNotEqual(pidA, pidB, "同模板两条实例迁移后 PersonId 必须不同");

        // 卡册应有 2 张卡；旧档没有显式绑定且模板多候选 → 不得静默把两张卡都绑到同一个“首个”实例
        Assert.AreEqual(2, M1TestKit.OwnedCharacterCards(loaded).Count);
        object owner1 = M1TestKit.InvokeInstance(loaded, "GetCharacterInstanceForCard", M1TestKit.OwnedCharacterCards(loaded)[0]);
        object owner2 = M1TestKit.InvokeInstance(loaded, "GetCharacterInstanceForCard", M1TestKit.OwnedCharacterCards(loaded)[1]);
        // Gate Correction：无绑定 + 同模板多候选必须 fail（null），禁止静默塌缩到同一实例
        if (owner1 != null && owner2 != null)
        {
            Assert.AreNotSame(owner1, owner2, "不允许把两张无绑定卡同时绑到同一个（首个）实例");
        }
        Assert.IsTrue((owner1 == null || (string)M1TestKit.Get(owner1, "CharacterId") == "char_001"));
        Assert.IsTrue((owner2 == null || (string)M1TestKit.Get(owner2, "CharacterId") == "char_001"));

        // 迁移稳定性：Save → Load 后 PersonId 不变
        Assert.IsTrue((bool)Save(loaded));
        object loaded2 = Load();
        Assert.AreEqual(2, M1TestKit.OwnedCharacters(loaded2).Count);
        string pidA2 = M1TestKit.PersonIdOf(M1TestKit.OwnedCharacters(loaded2)[0]);
        string pidB2 = M1TestKit.PersonIdOf(M1TestKit.OwnedCharacters(loaded2)[1]);
        Assert.AreNotEqual(pidA2, pidB2);
        Assert.AreEqual(pidA, pidA2);
        Assert.AreEqual(pidB, pidB2);
    }
}

