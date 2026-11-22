using System.IO;
using NUnit.Framework;

/// <summary>
/// M1 Gate Correction — 重复招募（RecruitUI 语义）隔离。
/// RecruitUI 每轮招募产生新实例 + Guid 化新 CardId + 显式绑定；
/// 两张重复招募 owned 卡必须独立（CardId 不同、PersonId 不同、绑定稳定、Save/Load 不塌缩）。
/// </summary>
public class DuplicateRecruitIsolationTests
{
    private static System.Type SaveSystemType => M1TestKit.Asm("SaveSystem");
    private static System.Type VillageBridgeType => M1TestKit.Asm("VillageRuntimeBridge");

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

    /// <summary>模拟一轮招募：新实例 + 新 Guid 化 CardId 卡 + 显式绑定。</summary>
    private static object[] RecruitOnce(object collection, object character, string cardId)
    {
        object card = M1TestKit.NewCharacterCard(cardId, (string)M1TestKit.Get(character, "CharacterId"));
        M1TestKit.OwnedCharacters(collection).Add(character);
        M1TestKit.OwnedCharacterCards(collection).Add(card);
        M1TestKit.InvokeInstance(collection, "BindCharacterCardOwner", cardId, M1TestKit.PersonIdOf(character));
        return new object[] { character, card };
    }

    [Test]
    public void RepeatedRecruit_SameTemplate_ProduceDistinctOwnedCards_AndStableBindings()
    {
        object collection = M1TestKit.NewPlayerCollection();
        var p1 = RecruitOnce(collection, M1TestKit.NewCharacter("char_001", "战士甲", 30), "card_char_001_aaaaaa");
        var p2 = RecruitOnce(collection, M1TestKit.NewCharacter("char_001", "战士乙", 30), "card_char_001_bbbbbb");

        string pid1 = M1TestKit.PersonIdOf(p1[0]);
        string pid2 = M1TestKit.PersonIdOf(p2[0]);
        Assert.AreNotEqual(pid1, pid2, "重复招募同模板两实例 PersonId 必须不同");
        string cid1 = (string)M1TestKit.Get(p1[1], "CardId");
        string cid2 = (string)M1TestKit.Get(p2[1], "CardId");
        Assert.AreNotEqual(cid1, cid2, "重复招募的 owned 卡必须拥有不同 CardId（RecruitUI Guid 化语义）");

        object r1 = M1TestKit.InvokeInstance(collection, "GetCharacterInstanceForCard", p1[1]);
        object r2 = M1TestKit.InvokeInstance(collection, "GetCharacterInstanceForCard", p2[1]);
        Assert.AreSame(p1[0], r1, "招募卡1 必须解析到人物1");
        Assert.AreSame(p2[0], r2, "招募卡2 必须解析到人物2");

        // Save → Load 后绑定保持（每卡仍各自归位，不塌缩）
        Assert.IsTrue((bool)Save(collection));
        object loaded = Load();
        Assert.IsNotNull(loaded);
        Assert.AreEqual(2, M1TestKit.OwnedCharacters(loaded).Count);
        Assert.AreEqual(2, M1TestKit.OwnedCharacterCards(loaded).Count);

        object l1 = M1TestKit.InvokeInstance(loaded, "GetCharacterInstanceForCard", M1TestKit.OwnedCharacterCards(loaded)[0]);
        object l2 = M1TestKit.InvokeInstance(loaded, "GetCharacterInstanceForCard", M1TestKit.OwnedCharacterCards(loaded)[1]);
        Assert.IsNotNull(l1, "Load 后卡1 应保留显式绑定");
        Assert.IsNotNull(l2, "Load 后卡2 应保留显式绑定");
        Assert.AreNotSame(l1, l2, "Load 后两张重复招募卡不得解析到同一人物");
    }
}
