using System.Collections.Generic;
using NUnit.Framework;

/// <summary>
/// M6 专项 — Stage 2 唯一接线：VillageTime.DayTicked → VillageRuntimeBridge.HandleDayTick →
/// Day End 统一运行一次 Formation Resolver。Assignment daily delta 事实 → Work Evidence；
/// 占用/未实际执行日不产生 Work Evidence；绝不把累计 TotalExecutedDays 当 delta；
/// PersonId no-fallback（不用 CharacterId/HeroId 猜 Person truth）。
/// </summary>
public class ProfessionFormationVillageDailyTests
{
    private const string Workplace = "ws_test_01";
    private object _collection;
    private object _character;

    [SetUp]
    public void SetUp()
    {
        ProfessionM6TestKit.DeleteSave();
        ProfessionM6TestKit.ResetBridge();
        ProfessionM6TestKit.EnsureVillageInitialized();

        _collection = ProfessionM6TestKit.NewPlayerCollection();
        _character = ProfessionM6TestKit.NewCharacter("char_001", "农夫甲", 30, 6);
        ProfessionM6TestKit.OwnedCharacters(_collection).Add(_character);
        ProfessionM6TestKit.SetSimulationPersonSource(_collection);

        // 注入 Test / Not Frozen Formation Services（Registry + Config + Work 映射）。
        ProfessionM6TestKit.SetBridgeFormation(new FormationRuntimeServices
        {
            Registry = ProfessionFormationCommitTests.FarmerRegistry(),
            Config = ProfessionFormationCommitTests.Fast(),
            WorkEvidenceMapper = ProfessionM6TestKit.NewFarmerWorkMapping(1f)
        });
    }

    [TearDown]
    public void TearDown()
    {
        ProfessionM6TestKit.ResetBridge();
        ProfessionM6TestKit.DeleteSave();
    }

    private static void Assign(string personId)
    {
        var assignments = ProfessionM6TestKit.BridgeAssignments();
        Assert.AreEqual(VillageAssignmentResult.Success, assignments.TryAssign(personId, Workplace),
            "测试岗位 ws_test_01 应可分配");
    }

    [Test]
    public void DayTicks_ExecuteAssignment_ProduceWorkEvidence_ThroughBridge()
    {
        var host = ProfessionM6TestKit.AsHost(_character);
        Assign(host.PersonId);

        var time = ProfessionM6TestKit.BridgeTime();
        for (int i = 0; i < 5; i++) time.TickDay();

        var entry = host.FormationState.FindEntry(ProfessionFormationCommitTests.Farmer);
        Assert.IsNotNull(entry, "DayTick 实际执行 → 应建立 Work Evidence 条目");
        Assert.AreEqual(5, entry.ActiveCareerDays, "Work Evidence 逐日精确 +1（每天实际执行一次）");
        Assert.AreEqual(5, ProfessionM6TestKit.BridgeAssignments().GetRecord(host.PersonId).TotalExecutedDays,
            "Assignment 累计执行天数与 Formation Work Evidence 一致（真实执行宿主）");
        Assert.Greater(entry.Strength, 0f, "Strength 随 Work Evidence 增长");
    }

    [Test]
    public void OccupiedDay_DoesNotExecuteAssignment_NoWorkEvidence()
    {
        var host = ProfessionM6TestKit.AsHost(_character);
        Assign(host.PersonId);
        var time = ProfessionM6TestKit.BridgeTime();

        time.TickDay();
        time.TickDay();
        var entry = host.FormationState.FindEntry(ProfessionFormationCommitTests.Farmer);
        Assert.AreEqual(2, entry.ActiveCareerDays, "前两日实际执行");

        // Temporary Occupancy 日：Assignment 保留但不实际执行 → 当天无 Work Evidence（不产生 delta 事实）。
        ProfessionM6TestKit.SetUnavailablePersonIds(new[] { host.PersonId });
        time.TickDay();
        Assert.AreEqual(2, host.FormationState.FindEntry(ProfessionFormationCommitTests.Farmer).ActiveCareerDays,
            "占用日不得计入 ActiveCareerDays（无 Work Evidence）");
        Assert.AreEqual(1, host.FormationState.FindEntry(ProfessionFormationCommitTests.Farmer).InactiveDays,
            "占用日按中断日处理（衰减只依赖中断时长，不额外获得 Evidence）");

        // 恢复可用 → 次日起自动恢复实际执行（Opportunity Cost / Auto Return 语义不变）。
        ProfessionM6TestKit.SetUnavailablePersonIds(null);
        time.TickDay();
        Assert.AreEqual(3, host.FormationState.FindEntry(ProfessionFormationCommitTests.Farmer).ActiveCareerDays,
            "恢复后下一个实际执行日继续产生 Work Evidence");
    }

    [Test]
    public void JumpToNextMonth_ContinuesUniqueDayTick_FormationExactlyOnce_NoTransition()
    {
        var host = ProfessionM6TestKit.AsHost(_character);
        Assign(host.PersonId);
        var time = ProfessionM6TestKit.BridgeTime();

        // JumpToNextMonth 完整执行剩余日 Tick（1 月 1 日 → 31 天），不得跳过 / 新建第二套 Day Loop。
        time.JumpToNextMonth();
        while(time.HasActiveAdvance) Assert.AreEqual(WorldAdvanceOutcome.Completed,time.ResumeAdvance(time.AdvanceSession.RequestId,10000).Outcome);

        var entry = host.FormationState.FindEntry(ProfessionFormationCommitTests.Farmer);
        Assert.IsNotNull(entry);
        Assert.AreEqual(31, entry.ActiveCareerDays, "JumpToNextMonth 走同一唯一 DayTick 路径（31 次实际执行）");
        Assert.IsTrue(host.HasActiveProfession, "足够 Stability + Target → 唯一 Day End Resolver 自动首次 Formation");
        Assert.AreEqual(ProfessionFormationCommitTests.Farmer, host.ActiveProfessionProgress.ProfessionId);
        Assert.AreEqual(1, host.LifeHistory.CountProfessionFormed(ProfessionFormationCommitTests.Farmer),
            "Life History exactly-once");
        Assert.AreEqual(31, ProfessionM6TestKit.BridgeAssignments().GetRecord(host.PersonId).TotalExecutedDays);
    }

    [Test]
    public void PersonIdNoFallback_AssignmentUnderCharacterIdLikeKey_DoesNotFormForRealPerson()
    {
        var host = ProfessionM6TestKit.AsHost(_character);
        Assert.IsFalse(host.PersonId.StartsWith("char_"), "PersonId 不应是 CharacterId 样式（no fallback 前提）");

        // 模拟异常旧数据：Assignment 记录用了 CharacterId 样式 key（而非真实 PersonId）。
        Assign("char_001");
        var time = ProfessionM6TestKit.BridgeTime();
        for (int i = 0; i < 3; i++) time.TickDay();

        Assert.AreEqual(0, ProfessionM6TestKit.BridgeAssignments().GetRecord("char_001").TotalExecutedDays,
            "错误身份无真实在场 Person，不产生虚构劳动");
        Assert.AreEqual(0, host.FormationState.Entries.Count,
            "真实 Person 不得因 CharacterId 样式 key 的 Work Evidence 被误投（PersonId no-fallback）");
        Assert.IsFalse(host.HasActiveProfession, "不得用 CharacterId 兜底触发 Formation");
    }

    [Test]
    public void NoInjectedConfig_BridgeDayEndDoesNothing()
    {
        ProfessionM6TestKit.SetBridgeFormation(null); // 未注入 → 不运行（不猜测数值 / 不产生行为）
        var host = ProfessionM6TestKit.AsHost(_character);
        Assign(host.PersonId);
        var time = ProfessionM6TestKit.BridgeTime();
        for (int i = 0; i < 3; i++) time.TickDay();

        Assert.AreEqual(0, host.FormationState.Entries.Count,
            "未注入 Test/Not Frozen 配置时 Formation 不产生行为（数值不得被当作正式值冻结）");
        Assert.IsFalse(host.HasActiveProfession);
    }
}
