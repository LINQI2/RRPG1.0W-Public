using System.Collections.Generic;
using NUnit.Framework;

/// <summary>
/// V2A Assignment Core 纯逻辑测试（Village.Runtime 强类型）。
/// 覆盖：Primary Assignment 指定 / 每 Person 最多一个 / 持久不消失 / Capacity 满员拒绝 /
///      未占用 Person 实际执行产生 Test Contribution / Expedition・Unavailable 等 Temporary Occupancy
///      暂停执行但不删除 / 释放后自动恢复执行。
/// </summary>
public class VillageAssignmentCoreTests
{
    private static List<VillageWorkplaceDef> Defs()
    {
        return new List<VillageWorkplaceDef>
        {
            new VillageWorkplaceDef { WorkplaceId = "ws_a", DisplayName = "A(Test)", Capacity = 2, TestContributionPerDay = 1 },
            new VillageWorkplaceDef { WorkplaceId = "ws_b", DisplayName = "B(Test)", Capacity = 1, TestContributionPerDay = 3 }
        };
    }

    private static VillageAssignmentSystem NewSystem()
    {
        return new VillageAssignmentSystem(Defs());
    }

    // ==================== Primary Assignment：指定 ====================

    [Test]
    public void Assign_FirstPerson_Succeeds_AndIsVisible()
    {
        var s = NewSystem();
        Assert.AreEqual(VillageAssignmentResult.Success, s.TryAssign("char_001", "ws_a"));
        Assert.AreEqual("ws_a", s.GetWorkplaceId("char_001"));
        Assert.IsNotNull(s.GetRecord("char_001"));
        Assert.AreEqual(1, s.GetOccupantCount("ws_a"));
        Assert.IsFalse(s.IsWorkplaceFull("ws_a"));
    }

    [Test]
    public void SamePerson_MaxOnePrimaryAssignment_SecondWorkplaceRejected()
    {
        var s = NewSystem();
        Assert.AreEqual(VillageAssignmentResult.Success, s.TryAssign("char_001", "ws_a"));
        Assert.AreEqual(VillageAssignmentResult.AlreadyAssigned, s.TryAssign("char_001", "ws_b"),
            "同一 Person 最多一个 Primary Assignment，改派到其它岗位必须拒绝");
        Assert.AreEqual("ws_a", s.GetWorkplaceId("char_001"), "原 Assignment 不得被覆盖删除");
        Assert.AreEqual(0, s.GetOccupantCount("ws_b"));
    }

    [Test]
    public void Assign_SameWorkplaceTwice_IsIdempotentSuccess()
    {
        var s = NewSystem();
        Assert.AreEqual(VillageAssignmentResult.Success, s.TryAssign("char_001", "ws_a"));
        Assert.AreEqual(VillageAssignmentResult.Success, s.TryAssign("char_001", "ws_a"));
        Assert.AreEqual(1, s.GetOccupantCount("ws_a"), "重复同岗位分配不应产生第二条占用");
    }

    [Test]
    public void Assign_InvalidPersonOrUnknownWorkplace_Rejected()
    {
        var s = NewSystem();
        Assert.AreEqual(VillageAssignmentResult.InvalidPersonId, s.TryAssign("", "ws_a"));
        Assert.AreEqual(VillageAssignmentResult.InvalidPersonId, s.TryAssign(null, "ws_a"));
        Assert.AreEqual(VillageAssignmentResult.UnknownWorkplace, s.TryAssign("char_001", "ws_ghost"));
        Assert.AreEqual(0, s.GetOccupantCount("ws_a"));
    }

    // ==================== Capacity：满员拒绝 ====================

    [Test]
    public void CapacityFull_RejectsAdditionalPerson()
    {
        var s = NewSystem();
        Assert.AreEqual(VillageAssignmentResult.Success, s.TryAssign("char_001", "ws_a"));
        Assert.AreEqual(VillageAssignmentResult.Success, s.TryAssign("char_002", "ws_a"));
        Assert.AreEqual(2, s.GetOccupantCount("ws_a"));
        Assert.IsTrue(s.IsWorkplaceFull("ws_a"));

        Assert.AreEqual(VillageAssignmentResult.CapacityFull, s.TryAssign("char_003", "ws_a"),
            "Capacity 满员后不能再正式分配额外 Person");
        Assert.AreEqual(2, s.GetOccupantCount("ws_a"));
        Assert.IsNull(s.GetWorkplaceId("char_003"));
    }

    [Test]
    public void DifferentWorkplaces_HaveIndependentCapacity()
    {
        var s = NewSystem();
        Assert.AreEqual(VillageAssignmentResult.Success, s.TryAssign("char_001", "ws_a"));
        Assert.AreEqual(VillageAssignmentResult.Success, s.TryAssign("char_002", "ws_b"));
        Assert.IsTrue(s.IsWorkplaceFull("ws_b"));
        Assert.IsFalse(s.IsWorkplaceFull("ws_a"), "不同岗位容量相互独立");
    }

    // ==================== Assignment 持久性 ====================

    [Test]
    public void Assignment_Persists_AcrossManyDayTicks_NoAutoRemove()
    {
        var s = NewSystem();
        Assert.AreEqual(VillageAssignmentResult.Success, s.TryAssign("char_001", "ws_a"));

        for (int i = 0; i < 30; i++)
        {
            s.OnDayTick(null); // 未占用 → 实际执行
        }

        Assert.IsNotNull(s.GetRecord("char_001"), "Assignment 是长期持久安排，不因正常 Tick 消失");
        Assert.AreEqual("ws_a", s.GetWorkplaceId("char_001"));
        Assert.AreEqual(30, s.GetRecord("char_001").TotalExecutedDays);
        Assert.AreEqual(30, s.TotalTestContribution);
    }

    // ==================== 实际执行 / Test Contribution ====================

    [Test]
    public void UnoccupiedPerson_Executes_ProducesTestContribution()
    {
        var s = NewSystem();
        Assert.AreEqual(VillageAssignmentResult.Success, s.TryAssign("char_001", "ws_b"));

        s.OnDayTick(null);
        s.OnDayTick(null);

        Assert.AreEqual(2, s.GetRecord("char_001").TotalExecutedDays);
        Assert.AreEqual(6, s.TotalTestContribution, "Test Contribution 应按岗位 TestContributionPerDay=3 × 2 天累计");
    }

    // ==================== Temporary Occupancy：暂停但不删除 ====================

    [Test]
    public void ExpeditionOccupied_PausesExecution_AndResumesAfterRelease()
    {
        var s = NewSystem();
        Assert.AreEqual(VillageAssignmentResult.Success, s.TryAssign("char_001", "ws_a"));

        s.OnDayTick(new[] { "char_001" }); // Expedition 占用
        s.OnDayTick(new[] { "char_001" });
        Assert.IsNotNull(s.GetRecord("char_001"), "Expedition 占用不得删除 Primary Assignment");
        Assert.AreEqual(0, s.GetRecord("char_001").TotalExecutedDays);
        Assert.AreEqual(0, s.TotalTestContribution, "Expedition 下不得错误产生 Assignment Contribution");

        s.OnDayTick(null); // 释放/返回 → 自动恢复
        s.OnDayTick(null);
        Assert.AreEqual(2, s.GetRecord("char_001").TotalExecutedDays);
        Assert.AreEqual(2, s.TotalTestContribution);
    }

    [Test]
    public void UnavailableOccupied_PausesExecution_AndResumesAfterRelease()
    {
        var s = NewSystem();
        Assert.AreEqual(VillageAssignmentResult.Success, s.TryAssign("char_002", "ws_a"));

        s.OnDayTick(new[] { "char_002" });
        Assert.AreEqual(0, s.GetRecord("char_002").TotalExecutedDays);
        Assert.AreEqual(0, s.TotalTestContribution, "Unavailable（暂不可用）占用下不得继续执行");

        s.OnDayTick(null);
        Assert.AreEqual(1, s.GetRecord("char_002").TotalExecutedDays, "占用解除后自动恢复执行，不要求重新分配");
    }

    [Test]
    public void RemoveAssignment_ReleasesCapacity_AndClearsPerson()
    {
        var s = NewSystem();
        Assert.AreEqual(VillageAssignmentResult.Success, s.TryAssign("char_001", "ws_b"));

        Assert.IsTrue(s.RemoveAssignment("char_001"));
        Assert.IsNull(s.GetRecord("char_001"));
        Assert.AreEqual(0, s.GetOccupantCount("ws_b"), "移除后岗位容量占用释放");
        Assert.IsFalse(s.RemoveAssignment("char_001"), "重复移除返回 false");

        Assert.AreEqual(VillageAssignmentResult.Success, s.TryAssign("char_002", "ws_b"), "释放后可重新正式分配");
    }
}
