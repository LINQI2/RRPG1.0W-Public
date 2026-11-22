using System.Collections.Generic;
using NUnit.Framework;

/// <summary>
/// M1 §15.8：两个同 CharacterId 人物的 Village Primary Assignment 互不影响。
/// </summary>
public class VillageAssignmentIdentityIsolationTests
{
    private static readonly System.Type BridgeType = M1TestKit.Asm("VillageRuntimeBridge");

    [SetUp]
    public void SetUp() => M1TestKit.InvokeStatic(BridgeType, "ResetForTests");

    [TearDown]
    public void TearDown() => M1TestKit.InvokeStatic(BridgeType, "ResetForTests");

    [Test]
    public void AssigningP1_DoesNotAssignP2()
    {
        object p1 = M1TestKit.NewCharacter("char_001", "战士甲", 30);
        object p2 = M1TestKit.NewCharacter("char_001", "战士乙", 30);
        string pid1 = M1TestKit.PersonIdOf(p1);
        string pid2 = M1TestKit.PersonIdOf(p2);

        object result = M1TestKit.InvokeStatic(BridgeType, "TryAssignPerson", pid1, "ws_test_01");
        Assert.IsNotNull(result);
        Assert.IsTrue(result.ToString().StartsWith("Success"), "P1 分配 ws_test_01 应成功，结果=" + result);

        string workplace1 = (string)M1TestKit.InvokeStatic(BridgeType, "GetPersonWorkplaceId", pid1);
        string workplace2 = (string)M1TestKit.InvokeStatic(BridgeType, "GetPersonWorkplaceId", pid2);
        Assert.AreEqual("ws_test_01", workplace1, "P1 应持有 Primary Assignment ws_test_01");
        Assert.IsNull(workplace2, "同模板的 P2 不得被 P1 的分配牵连");
    }
}

/// <summary>
/// M1 §15.9：同模板双实例中 P1 出征时只有 P1 变为 Expedition，P2 仍 InVillage。
/// </summary>
public class ExpeditionOccupancyIdentityIsolationTests
{
    private static readonly System.Type BridgeType = M1TestKit.Asm("VillageRuntimeBridge");

    private static List<VillagePersonView> Availability(object collection)
    {
        return (List<VillagePersonView>)M1TestKit.InvokeStatic(BridgeType, "GetPersonAvailability", collection);
    }

    [SetUp]
    public void SetUp() => M1TestKit.InvokeStatic(BridgeType, "ResetForTests");

    [TearDown]
    public void TearDown() => M1TestKit.InvokeStatic(BridgeType, "ResetForTests");

    [Test]
    public void P1Expedition_P2StaysInVillage()
    {
        object p1 = M1TestKit.NewCharacter("char_001", "战士甲", 30);
        object p2 = M1TestKit.NewCharacter("char_001", "战士乙", 30);
        object collection = M1TestKit.NewPlayerCollection();
        M1TestKit.OwnedCharacters(collection).Add(p1);
        M1TestKit.OwnedCharacters(collection).Add(p2);
        string pid1 = M1TestKit.PersonIdOf(p1);
        string pid2 = M1TestKit.PersonIdOf(p2);

        // P1 出征登记（真实 PersonId；同模板 P2 不受影响）
        M1TestKit.InvokeStatic(BridgeType, "SetExpeditionPersonIds", new List<string> { pid1 });

        var views = Availability(collection);
        Assert.AreEqual(2, views.Count, "两个同模板人物都必须出现在 Village 人物视图");

        var view1 = views.Find(v => v.PersonId == pid1);
        var view2 = views.Find(v => v.PersonId == pid2);
        Assert.IsNotNull(view1, "P1 视图应存在");
        Assert.IsNotNull(view2, "P2 视图应存在");
        Assert.AreEqual(VillageAvailability.Expedition, view1.Availability, "P1 应为 Expedition");
        Assert.AreEqual(VillageAvailability.InVillage, view2.Availability,
            "不能因为 CharacterId 相同把 P2 也标成 Expedition");
    }
}

/// <summary>
/// M1 §15.10：PendingGrowthExp 兑现只作用到对应 PersonId 人物（同模板双实例经验互不影响）。
/// 本测试仍按 M1 当前旧 Growth 语义（Level/Exp）执行；M5 才迁移 ProfessionExp。
/// </summary>
public class GrowthRewardIdentityIsolationTests
{
    [Test]
    public void RewardExp_OnlyAppliesToTargetedPerson()
    {
        object p1 = M1TestKit.NewCharacter("char_001", "战士甲", 30);
        object p2 = M1TestKit.NewCharacter("char_001", "战士乙", 30);
        object collection = M1TestKit.NewPlayerCollection();
        M1TestKit.OwnedCharacters(collection).Add(p1);
        M1TestKit.OwnedCharacters(collection).Add(p2);
        string pid1 = M1TestKit.PersonIdOf(p1);
        string pid2 = M1TestKit.PersonIdOf(p2);

        var sink = (IRunRewardSink)M1TestKit.NewAsm("PlayerRunRewardSink", collection);
        sink.AddGrowthExp(pid1, 10);

        Assert.AreEqual(10, (int)M1TestKit.Get(p1, "Exp"), "P1 兑现 +10");
        Assert.AreEqual(0, (int)M1TestKit.Get(p2, "Exp"), "P2 必须保持 +0，不受 P1 兑现影响");
    }
}
