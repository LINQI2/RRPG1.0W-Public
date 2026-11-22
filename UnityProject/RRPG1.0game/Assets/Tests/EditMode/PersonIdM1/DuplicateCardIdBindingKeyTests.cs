using System.IO;
using NUnit.Framework;

/// <summary>
/// M1 Gate Correction — 绑定键歧义防线：
///   1) 同 CharacterId 多候选且卡未绑定 → Resolve 必须返回 null（不静默取首个存活实例）；
///   2) Rebuild 补建在多候选时跳过（不猜测）；
///   3) 同卡册重复同一静态 CardId（共享 asset）的异常旧档 → Load 归一化为独立 owned 条目，
///      不得以单一 CardId 键把两人塌缩。
/// </summary>
public class DuplicateCardIdBindingKeyTests
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

    [Test]
    public void UnboundCard_WithMultipleSameTemplateInstances_ReturnsNull_NotSilentlyFirst()
    {
        object collection = M1TestKit.NewPlayerCollection();
        object p1 = M1TestKit.NewCharacter("char_001", "战士甲", 30);
        object p2 = M1TestKit.NewCharacter("char_001", "战士乙", 30);
        M1TestKit.OwnedCharacters(collection).Add(p1);
        M1TestKit.OwnedCharacters(collection).Add(p2);

        // 一张未绑定卡（同模板 char_001，模板多候选）
        object card = M1TestKit.NewCharacterCard("ccard_unbound", "char_001");
        M1TestKit.OwnedCharacterCards(collection).Add(card);

        // Gate Correction：多候选未绑定 → 必须返回 null（拒绝静默首个），且不新增绑定
        object owner = M1TestKit.InvokeInstance(collection, "GetCharacterInstanceForCard", card);
        Assert.IsNull(owner, "同模板多候选且未绑定时不得静默返回首个实例");
        var exported = (System.Collections.IList)M1TestKit.InvokeInstance(collection, "BuildCharacterCardOwnerSaveData");
        Assert.AreEqual(0, exported.Count, "未绑定的歧义卡不得被悄悄绑定/导出");
    }

    [Test]
    public void Rebuild_SkipsUnboundDuplicateTemplateCandidates_WithNoSilentBinding()
    {
        object collection = M1TestKit.NewPlayerCollection();
        object p1 = M1TestKit.NewCharacter("char_001", "战士甲", 30);
        object p2 = M1TestKit.NewCharacter("char_001", "战士乙", 30);
        M1TestKit.OwnedCharacters(collection).Add(p1);
        M1TestKit.OwnedCharacters(collection).Add(p2);

        object cardA = M1TestKit.NewCharacterCard("ccard_old_a", "char_001");
        object cardB = M1TestKit.NewCharacterCard("ccard_old_b", "char_001");
        M1TestKit.OwnedCharacterCards(collection).Add(cardA);
        M1TestKit.OwnedCharacterCards(collection).Add(cardB);

        M1TestKit.InvokeInstance(collection, "RebuildCharacterCardOwnersFromCardOrder");

        object ownerA = M1TestKit.InvokeInstance(collection, "GetCharacterInstanceForCard", cardA);
        object ownerB = M1TestKit.InvokeInstance(collection, "GetCharacterInstanceForCard", cardB);
        Assert.IsNull(ownerA, "无绑定 + 多候选的旧档重建不得猜测绑定（卡A）");
        Assert.IsNull(ownerB, "无绑定 + 多候选的旧档重建不得猜测绑定（卡B）");
    }

    [Test]
    public void OldSave_DuplicateSameStaticCardId_NormalizesToIndependentEntries_NoSilentBinding()
    {
        // 异常旧档：两个 char_001 实例 + 卡册两条完全相同的静态 CardId ccard_001（共享 asset）
        string json = "{\"SaveVersion\":1,\"GameVersion\":\"0.2.3\",\"SavedAtUnixTime\":0," +
                      "\"Player\":{\"Gold\":100,\"Heroes\":[],\"Characters\":[" +
                      "{\"CharacterId\":\"char_001\",\"Level\":1,\"Exp\":0,\"CurrentAttack\":6,\"Injury\":0,\"IsDead\":false,\"MaxHP\":30}," +
                      "{\"CharacterId\":\"char_001\",\"Level\":1,\"Exp\":0,\"CurrentAttack\":6,\"Injury\":0,\"IsDead\":false,\"MaxHP\":30}" +
                      "],\"OwnedCharacterCardIds\":[\"ccard_001\",\"ccard_001\"],\"OwnedItemCardIds\":[]}}";
        File.WriteAllText(SavePath(), json);

        object loaded = Load();
        Assert.IsNotNull(loaded, "重复 CardId 旧档必须可加载");
        Assert.AreEqual(2, M1TestKit.OwnedCharacters(loaded).Count, "两个同模板实例都必须存在");

        string pidA = M1TestKit.PersonIdOf(M1TestKit.OwnedCharacters(loaded)[0]);
        string pidB = M1TestKit.PersonIdOf(M1TestKit.OwnedCharacters(loaded)[1]);
        Assert.AreNotEqual(pidA, pidB, "两条实例迁移后 PersonId 必须不同");

        // 卡册：两条 owned 卡必须被归一化为独立条目（CardId 不再重复）
        Assert.AreEqual(2, M1TestKit.OwnedCharacterCards(loaded).Count);
        string cidX = (string)M1TestKit.Get(M1TestKit.OwnedCharacterCards(loaded)[0], "CardId");
        string cidY = (string)M1TestKit.Get(M1TestKit.OwnedCharacterCards(loaded)[1], "CardId");
        Assert.AreNotEqual(cidX, cidY, "重复静态 CardId 必须归一化为两个不同 CardId 的独立 owned 条目");

        // Gate Correction：无显式绑定 + 同模板多候选 → 解析不得塌缩到同一实例
        object ownerX = M1TestKit.InvokeInstance(loaded, "GetCharacterInstanceForCard", M1TestKit.OwnedCharacterCards(loaded)[0]);
        object ownerY = M1TestKit.InvokeInstance(loaded, "GetCharacterInstanceForCard", M1TestKit.OwnedCharacterCards(loaded)[1]);
        if (ownerX != null && ownerY != null)
        {
            Assert.AreNotSame(ownerX, ownerY, "归一化后的两张卡不得同时解析到同一个实例");
        }

        // 再次 Save → Load 后归一化保持稳定（CardId 集合唯一），PersonId 稳定
        Assert.IsTrue((bool)Save(loaded));
        object loaded2 = Load();
        Assert.AreEqual(2, M1TestKit.OwnedCharacters(loaded2).Count);
        Assert.AreEqual(pidA, M1TestKit.PersonIdOf(M1TestKit.OwnedCharacters(loaded2)[0]));
        Assert.AreEqual(pidB, M1TestKit.PersonIdOf(M1TestKit.OwnedCharacters(loaded2)[1]));
        Assert.AreEqual(2, M1TestKit.OwnedCharacterCards(loaded2).Count);
        string cidX2 = (string)M1TestKit.Get(M1TestKit.OwnedCharacterCards(loaded2)[0], "CardId");
        string cidY2 = (string)M1TestKit.Get(M1TestKit.OwnedCharacterCards(loaded2)[1], "CardId");
        Assert.AreNotEqual(cidX2, cidY2, "二次 Load 后 CardId 仍必须唯一");
    }
}
