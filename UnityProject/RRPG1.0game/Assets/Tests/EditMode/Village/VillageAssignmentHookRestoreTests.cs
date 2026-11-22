using System.Collections.Generic;
using NUnit.Framework;

/// <summary>
/// V2A Profession Formation Source Hook (Test) 与 Save/Load 恢复纯逻辑测试（Village.Runtime 强类型）。
/// 覆盖：Hook 只在 Assignment 实际执行时产生可验证事实；占用期间事实不增长；无 Assignment 无事实；
///      ToSaveData/Restore 往返后 Assignment / Capacity 占用 / 执行事实一致；非法存档数据的防御性恢复。
/// </summary>
public class VillageAssignmentHookRestoreTests
{
    private static VillageAssignmentSystem NewSystem()
    {
        return new VillageAssignmentSystem(new List<VillageWorkplaceDef>
        {
            new VillageWorkplaceDef { WorkplaceId = "ws_a", DisplayName = "A(Test)", Capacity = 2, TestContributionPerDay = 1 },
            new VillageWorkplaceDef { WorkplaceId = "ws_b", DisplayName = "B(Test)", Capacity = 1, TestContributionPerDay = 3 }
        });
    }

    // ==================== Profession Formation Source Hook (Test) ====================

    [Test]
    public void HookFacts_OnlyAppear_AfterActualExecution()
    {
        var s = NewSystem();

        // 无 Assignment → 无事实
        Assert.AreEqual(0, s.GetProfessionFormationSourceHookFacts().Count, "无 Assignment 不得出现执行事实");

        // 已指定但尚未实际执行 → 无事实（Hook 只暴露真实执行来源，不含 0 天占位）
        Assert.AreEqual(VillageAssignmentResult.Success, s.TryAssign("char_001", "ws_a"));
        Assert.AreEqual(0, s.GetProfessionFormationSourceHookFacts().Count, "尚未执行时不得产生 Hook 事实");

        // 实际执行 1 天 → 出现事实
        s.OnDayTick(null);
        var facts = s.GetProfessionFormationSourceHookFacts();
        Assert.AreEqual(1, facts.Count);
        Assert.AreEqual("char_001", facts[0].PersonId);
        Assert.AreEqual("ws_a", facts[0].WorkplaceId);
        Assert.AreEqual(1, facts[0].TotalExecutedDays);
    }

    [Test]
    public void HookFacts_DoNotGrow_WhileOccupied_OnlyGrowWhenExecuting()
    {
        var s = NewSystem();
        Assert.AreEqual(VillageAssignmentResult.Success, s.TryAssign("char_001", "ws_a"));

        s.OnDayTick(null);
        s.OnDayTick(null);
        Assert.AreEqual(2, s.GetProfessionFormationSourceHookFacts()[0].TotalExecutedDays);

        // 被 Expedition / Unavailable 占用期间：Assignment 保留，但 Hook 事实不得继续增长
        s.OnDayTick(new[] { "char_001" });
        s.OnDayTick(new[] { "char_001" });
        Assert.IsNotNull(s.GetRecord("char_001"), "占用期间 Primary Assignment 不得被删除");
        Assert.AreEqual(2, s.GetProfessionFormationSourceHookFacts()[0].TotalExecutedDays,
            "Temporary Occupancy 下不得继续产生该 Assignment 的执行事实");

        // 解除占用后恢复增长
        s.OnDayTick(null);
        Assert.AreEqual(3, s.GetProfessionFormationSourceHookFacts()[0].TotalExecutedDays);
    }

    // ==================== Save / Load：往返一致 ====================

    [Test]
    public void ToSaveData_Restore_RoundTrip_PreservesAssignmentsCapacityAndFacts()
    {
        var s = NewSystem();
        Assert.AreEqual(VillageAssignmentResult.Success, s.TryAssign("char_001", "ws_a"));
        Assert.AreEqual(VillageAssignmentResult.Success, s.TryAssign("char_002", "ws_a"));
        Assert.AreEqual(VillageAssignmentResult.CapacityFull, s.TryAssign("char_003", "ws_a"));
        s.OnDayTick(null);
        s.OnDayTick(null);

        var data = s.ToSaveData();

        var s2 = NewSystem();
        s2.Restore(data);

        // Primary Assignment 恢复
        Assert.AreEqual("ws_a", s2.GetWorkplaceId("char_001"));
        Assert.AreEqual("ws_a", s2.GetWorkplaceId("char_002"));
        Assert.IsNull(s2.GetWorkplaceId("char_003"));

        // Capacity 占用保持一致（仍满员）
        Assert.AreEqual(2, s2.GetOccupantCount("ws_a"));
        Assert.IsTrue(s2.IsWorkplaceFull("ws_a"));
        Assert.AreEqual(VillageAssignmentResult.CapacityFull, s2.TryAssign("char_003", "ws_a"),
            "Load 后 Capacity 满员状态必须保持一致");

        // 执行事实保持一致，并可持续累计
        Assert.AreEqual(2, s2.GetRecord("char_001").TotalExecutedDays);
        Assert.AreEqual(2, s2.GetRecord("char_002").TotalExecutedDays);
        Assert.AreEqual(4, s2.TotalTestContribution);
        s2.OnDayTick(null);
        Assert.AreEqual(3, s2.GetRecord("char_001").TotalExecutedDays, "Load 后 Day Tick 应继续累计执行事实");
        Assert.AreEqual(6, s2.TotalTestContribution);
    }

    [Test]
    public void Restore_Null_ClearsToEmptySystem()
    {
        var s = NewSystem();
        Assert.AreEqual(VillageAssignmentResult.Success, s.TryAssign("char_001", "ws_a"));
        s.OnDayTick(null);
        s.Restore(null);
        Assert.AreEqual(0, s.Records.Count);
        Assert.AreEqual(0, s.GetOccupantCount("ws_a"));
        Assert.AreEqual(0, s.TotalTestContribution);
    }

    // ==================== 非法状态防御 ====================

    [Test]
    public void Restore_DuplicatePersonRecords_KeepsFirst()
    {
        var s = NewSystem();
        s.Restore(new VillageAssignmentsSaveData
        {
            Records = new List<VillageAssignmentSaveData>
            {
                new VillageAssignmentSaveData { PersonId = "char_001", WorkplaceId = "ws_a", TotalExecutedDays = 2 },
                new VillageAssignmentSaveData { PersonId = "char_001", WorkplaceId = "ws_b", TotalExecutedDays = 5 }
            }
        });

        Assert.AreEqual(1, s.Records.Count, "同一 Person 多条 Primary Assignment 记录为非法状态，只保留首条");
        Assert.AreEqual("ws_a", s.GetWorkplaceId("char_001"));
        Assert.AreEqual(0, s.GetOccupantCount("ws_b"));
    }

    [Test]
    public void Restore_UnknownWorkplaceRecord_IsDropped()
    {
        var s = NewSystem();
        s.Restore(new VillageAssignmentsSaveData
        {
            Records = new List<VillageAssignmentSaveData>
            {
                new VillageAssignmentSaveData { PersonId = "char_001", WorkplaceId = "ws_ghost", TotalExecutedDays = 3 }
            },
            TotalTestContribution = 3
        });

        Assert.AreEqual(0, s.Records.Count, "指向已不存在岗位的 Assignment 应被丢弃，避免 Person 被无法执行的安排锁定");
        Assert.IsNull(s.GetWorkplaceId("char_001"));

        s.OnDayTick(null);
        Assert.AreEqual(0, s.TotalTestContribution, "岗位无效时不得产生任何执行/贡献");
    }

    [Test]
    public void Restore_NegativeOrInvalidValues_AreClampedToSafeDefaults()
    {
        var s = NewSystem();
        s.Restore(new VillageAssignmentsSaveData
        {
            Records = new List<VillageAssignmentSaveData>
            {
                new VillageAssignmentSaveData { PersonId = "", WorkplaceId = "ws_a" },
                new VillageAssignmentSaveData { PersonId = "char_001", WorkplaceId = "ws_a", TotalExecutedDays = -5 }
            },
            TotalTestContribution = -3
        });

        Assert.AreEqual(1, s.Records.Count, "空 PersonId 记录被丢弃");
        Assert.AreEqual(0, s.GetRecord("char_001").TotalExecutedDays, "负数执行天数回退 0");
        Assert.AreEqual(0, s.TotalTestContribution, "负数贡献回退 0");
    }
}
