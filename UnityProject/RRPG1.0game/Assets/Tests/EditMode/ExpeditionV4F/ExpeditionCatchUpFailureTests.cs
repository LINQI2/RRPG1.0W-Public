using System;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// V4F Return Catch-up 失败契约（Final Development Spec 修订 2 §2.3 / 测试计划 T12 / T13）。
/// 只通过注入的失败点制造失败，不做随机注入：
///   - T12 Between-Day（首个 Day 前）：无部分写入 → 可从 DaysSimulated 干净续跑，不重复；
///   - T13 Day 中途：部分效果已写入 → 当日不计 DaysSimulated、记录转 Failed、
///        禁止自动续跑该日（不产生重复补算），恢复 = Discard + 从最后成功 Save 重开。
/// </summary>
public class ExpeditionCatchUpFailureTests
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
    public void T12_BetweenDayFailure_BeforeFirstDay_NoEffects_RetryFromDay0Clean()
    {
        const string home = "char_home_work";
        V4FTestKit.BindWorkPersons(home);
        Assert.AreEqual(VillageAssignmentResult.Success, V4FTestKit.Assign(home, "ws_test_02"));
        V4FTestKit.SetExpedition(new[] { "char_exp_a" });

        Assert.IsTrue(V4FTestKit.BeginReturn(3));
        Assert.IsTrue(V4FTestKit.MarkSettled());

        int baseState = V4FTestKit.BridgeState().TestSurvivalValue;
        bool threw = false;
        V4FTestKit.SetBeforeDayHook(day =>
        {
            if (day == 0 && !threw)
            {
                threw = true;
                throw new InvalidOperationException("T12 inject between-day failure before first day");
            }
        });

        var failed = V4FTestKit.RunCatchUp();
        Assert.AreEqual(ExpeditionCatchUpResult.RetryableFailure, failed, "首个 Day 前失败 → RetryableFailure");
        Assert.IsTrue(threw);
        var record = V4FTestKit.ActiveRecord();
        Assert.AreEqual(0, record.DaysSimulated, "无任何 Day 效果");
        Assert.IsFalse(record.Completed);
        Assert.AreEqual(ExpeditionReturnPhase.ReturnSettled, record.Phase,
            "Between-Day 失败无部分写入 → 相位保持 ReturnSettled（可续跑）");
        Assert.AreEqual(1, V4FTestKit.BridgeTime().Day, "未推进任何 Day");
        Assert.AreEqual(baseState, V4FTestKit.BridgeState().TestSurvivalValue, "未产生任何 State 效果");
        Assert.AreEqual(0, V4FTestKit.Assignments().GetRecord(home).TotalExecutedDays,
            "失败前不得执行 home 的 Assignment");
        Assert.AreEqual("<no-save>", V4FTestKit.SaveHash(), "失败路径不得落盘");

        // 重试：从 Day 0 干净重跑 3 天，已结算日绝不重跑（不重复）。
        V4FTestKit.SetBeforeDayHook(null);
        var retried = V4FTestKit.RunCatchUp();
        Assert.AreEqual(ExpeditionCatchUpResult.Completed, retried);
        Assert.AreEqual(3, record.DaysSimulated, "重试应完成全部 3 天");
        Assert.AreEqual(3, V4FTestKit.Assignments().GetRecord(home).TotalExecutedDays,
            "重试不得把已失败前的 0 天重复计算（总数恰 3）");
        Assert.AreEqual(4, V4FTestKit.BridgeTime().Day, "重试完成后时间 = 1/1/4");
        Assert.AreEqual(baseState + 3, V4FTestKit.BridgeState().TestSurvivalValue,
            "State 恰好 +3（无重复）");
        Assert.AreEqual("<no-save>", V4FTestKit.SaveHash(), "Catch-up 全程仍不落盘（Save 只发生在最终 Return Save）");
    }

    [Test]
    public void T13_MidDayFailure_PartialEffects_RecordFailed_NoAutoResume_ThenDiscard()
    {
        V4FTestKit.SetExpedition(new[] { "char_exp_a" });
        int baseState = V4FTestKit.BridgeState().TestSurvivalValue;

        Assert.IsTrue(V4FTestKit.BeginReturn(2));
        Assert.IsTrue(V4FTestKit.MarkSettled());

        // 中途失败注入：bridge 的 HandleDayTick 之后抛异常（DayTicked 多播顺序 = 订阅顺序，
        // bridge 先订阅 → 每日全部世界效果先完整写入，随后本订阅者抛异常模拟“某 Day 内部分效果已写入”）。
        var time = V4FTestKit.BridgeTime();
        bool boomFired = false;
        Action<int> boom = null;
        boom = day =>
        {
            if (boomFired) return;
            if (day != 2) return; // 第一个被推进的 Day 值（1/1 → 1/2）
            boomFired = true;
            throw new InvalidOperationException("T13 inject mid-day partial failure");
        };
        time.DayTicked += boom;
        try
        {
            // 该 Debug.LogError 是 bridge 对 PartialDayFailure 的既定记录——先声明期望（正则匹配），避免 LogAssert 误判。
            LogAssert.Expect(LogType.Error, new Regex("Return Catch-up 在 Day 中途失败"));
            var failed = V4FTestKit.RunCatchUp();
            Assert.AreEqual(ExpeditionCatchUpResult.PartialDayFailure, failed);
        }
        finally
        {
            time.DayTicked -= boom;
        }

        Assert.IsTrue(boomFired, "中途失败注入应触发");
        var record = V4FTestKit.ActiveRecord();
        Assert.AreEqual(0, record.DaysSimulated, "部分 Day 不得计入 DaysSimulated");
        Assert.AreEqual(ExpeditionReturnPhase.Failed, record.Phase, "记录转 Failed");
        Assert.IsFalse(record.Completed);
        Assert.AreEqual(2, V4FTestKit.BridgeTime().Day, "部分效果已写入（Time 已进入新的一天）");
        Assert.AreEqual(baseState + 1, V4FTestKit.BridgeState().TestSurvivalValue,
            "部分效果已写入（State +1）——这正是禁止自动续跑该日的原因");
        Assert.AreEqual("<no-save>", V4FTestKit.SaveHash(), "失败路径不得落盘（未发生 Save）");

        // 禁止自动续跑该日：任何 Catch-up / Begin 入口都必须拒绝。
        Assert.AreEqual(ExpeditionCatchUpResult.RejectedFailedRecord, V4FTestKit.RunCatchUp(),
            "Failed 记录禁止自动续跑（避免重复补算）");
        Assert.IsFalse(V4FTestKit.BeginReturn(2), "Failed 记录存在时 Begin 新 Return 被拒绝");
        Assert.AreEqual(0, record.DaysSimulated, "拒绝路径不改变 DaysSimulated");

        // 恢复路径：丢弃该会话记录（并从最后一次成功 Save 重开；本测试不模拟重开加载）。
        V4FTestKit.DiscardRecord();
        Assert.IsNull(V4FTestKit.ActiveRecord(), "Discard 后无半完成记录可被误重试");
    }
}
