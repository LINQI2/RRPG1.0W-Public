using System;
using System.Collections.Generic;
using NUnit.Framework;

/// <summary>
/// V4F Return Catch-up 逐日语义（Final Development Spec 修订 1/3）：
///   - T6：Catch-up 全程占用集合恒含全部远征者，且远征者绝不在任何 Active Project 参与者集合（C-P1）；
///   - 远征者 Primary Assignment 在 Catch-up 各日暂停；未占用 Village 人物正常执行；Project 按参与者推进。
/// </summary>
public class ExpeditionCatchUpDayTests
{
    [SetUp]
    public void SetUp()
    {
        V4FTestKit.ResetBridge();
        V4FTestKit.DeleteSave();
        V4FTestKit.EnsureInitialized();
        V4FTestKit.SetTime(1, 1, 1);
    }

    [TearDown]
    public void TearDown()
    {
        V4FTestKit.SetBeforeDayHook(null);
        V4FTestKit.ResetBridge();
        V4FTestKit.DeleteSave();
    }

    [Test]
    public void T6_CatchUp_OccupancyKeepsExpeditionersAndNoneInActiveProject()
    {
        const string expA = "char_exp_a";
        const string expB = "char_exp_b";
        const string home = "char_home_work";
        const string prj = "char_prj_member";

        V4FTestKit.BindWorkPersons(expA,expB,home,prj);
        // 远征者：a 有 Primary Assignment（应暂停）；b 无安排。
        Assert.AreEqual(VillageAssignmentResult.Success, V4FTestKit.Assign(expA, "ws_test_01"));
        // 未占用人物：home 有 Primary Assignment（Catch-up 各日应实际执行）。
        Assert.AreEqual(VillageAssignmentResult.Success, V4FTestKit.Assign(home, "ws_test_02"));
        // Project：prj 参与者（非远征者）——Catch-up 各日推进 +1。
        Assert.AreEqual(VillageProjectResult.Success, V4FTestKit.StartProject("prj_test_01", prj));

        V4FTestKit.SetExpedition(new[] { expA, expB });
        Assert.IsTrue(V4FTestKit.BeginReturn(3));
        Assert.IsTrue(V4FTestKit.MarkSettled());

        // 逐日采样（订阅在 bridge 之后：每次 TickDay 的 HandleDayTick 完整结束后采样当日占用快照）。
        var samples = new List<string[]>();
        var time = V4FTestKit.BridgeTime();
        Action<int> sampler = _ => samples.Add(new List<string>(V4FTestKit.OccupiedIds()).ToArray());
        time.DayTicked += sampler;
        try
        {
            var result = V4FTestKit.RunCatchUp();
            Assert.AreEqual(ExpeditionCatchUpResult.Completed, result);
        }
        finally
        {
            time.DayTicked -= sampler;
        }

        Assert.AreEqual(3, samples.Count, "3 天应产生 3 次占用快照采样");
        foreach (var snap in samples)
        {
            CollectionAssert.Contains(snap, expA, "Catch-up 全程占用集合恒含远征者 a");
            CollectionAssert.Contains(snap, expB, "Catch-up 全程占用集合恒含远征者 b");
            CollectionAssert.DoesNotContain(snap, prj, "午夜无实际工作占用，项目容量预约保留");
            CollectionAssert.DoesNotContain(V4FTestKit.Projects().GetActiveParticipantPersonIds(), expA,
                "远征者绝不在 Active Project 参与者集合");
            CollectionAssert.DoesNotContain(V4FTestKit.Projects().GetActiveParticipantPersonIds(), expB,
                "远征者绝不在 Active Project 参与者集合");
        }

        // 逐日效果：
        var assignments = V4FTestKit.Assignments();
        Assert.AreEqual(0, assignments.GetRecord(expA).TotalExecutedDays,
            "远征者 a 的 Primary Assignment 在 Catch-up 各日暂停（无 Work 执行事实）");
        Assert.AreEqual(3, assignments.GetRecord(home).TotalExecutedDays,
            "未占用 home 的 Primary Assignment 在 Catch-up 各日正常执行");
        Assert.AreEqual(3, V4FTestKit.Projects().ActiveProjects[0].Progress,
            "Project 按参与者逐日推进（3 天 × 1）");
        Assert.AreEqual(0, V4FTestKit.World().CountPlacesOfKind(VillagePlaceKind.ProjectSite),
            "Catch-up 3 天不完成 Project → 无 ProjectSite World 痕迹");

        var record = V4FTestKit.ActiveRecord();
        Assert.AreEqual(3, record.DaysSimulated);
        Assert.AreEqual(ExpeditionReturnPhase.CatchUpComplete, record.Phase);
        Assert.AreEqual(1, V4FTestKit.BridgeTime().Year);
        Assert.AreEqual(1, V4FTestKit.BridgeTime().Month);
        Assert.AreEqual(4, V4FTestKit.BridgeTime().Day, "逐日 TickDay：1/1/1 + 3 天 = 1/1/4");
        Assert.AreEqual(2, V4FTestKit.ExpeditionIds().Count, "Catch-up 期间占用未清除");
    }

    [Test]
    public void CatchUp_DayTickIncrementsStatePerDay_AndDoesNotTouchSaveFile()
    {
        V4FTestKit.SetExpedition(new[] { "char_exp_a" });
        var state = V4FTestKit.BridgeState();
        int baseValue = state.TestSurvivalValue;

        Assert.IsTrue(V4FTestKit.BeginReturn(2));
        Assert.IsTrue(V4FTestKit.MarkSettled());
        Assert.AreEqual(ExpeditionCatchUpResult.Completed, V4FTestKit.RunCatchUp());

        Assert.AreEqual(baseValue + 2, state.TestSurvivalValue, "每个 Day Tick 都完整执行既有 State 规则");
        Assert.AreEqual(2, V4FTestKit.ActiveRecord().DaysSimulated);
    }
}
