using System;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// WP-B Expedition Return Integrity｜EditMode 长期最小契约（V4F / Bridge seam）。
///
/// 契约（Development Spec §2 I-1..I-8 / §13）：
///   - 全部 ProfessionExp ack 才能越过 Settlement；未 ack Pending 保留、已 ack 重试 +0；
///   - Retreat / Defeat 不得经 Discard 语义丢弃未 ack ProfessionExp；
///   - 未完成 Return Record 不得重新 Begin（resume 而不是重建）；
///   - Retryable Catch-up 从现有 DaysSimulated 继续（已完成 Day 绝不重复）；
///   - PartialDayFailure → Failed + 禁止自动恢复 / 重新 Begin；
///   - Carry / Consequence 拒绝 = Fail Closed（不 Clear、不落盘）；
///   - Final Save false 只允许重试 Save（不重跑已完成阶段、不重复返还 Carry）；
///   - Completed Return exactly-once。
///
/// 产品 caller（ExpeditionUI.FinalizeReturn 相位机 / OnReturnToTown / Retreat / Battle 出口）
/// 的真实 GUI 覆盖由 PlayMode `ExpeditionReturnIntegrityProductPlayTests` 承担；
/// 两条 seam 相互补充。全部内容 Test / Not Frozen。
/// </summary>
public class ExpeditionReturnIntegrityTests
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

    // ==================== 辅助 ====================

    /// <summary>带 RunEndMode 的 Begin（产品入口语义：原始出口模式随会话保留用于 resume）。</summary>
    private static bool BeginReturn(int days, RunEndMode mode) =>
        (bool)V4FTestKit.InvokeStatic(V4FTestKit.Bridge, "BeginExpeditionReturn", days, mode);

    private static object SettleDetailed(object adventure, RunEndMode mode) =>
        V4FTestKit.Call(adventure, "SettleReturnDetailed", mode);

    private static ReturnCarryStageResult CarryStage(int units) =>
        (ReturnCarryStageResult)V4FTestKit.InvokeStatic(V4FTestKit.Bridge, "ReturnRemainingCarryStage", units);

    private static object TryCommitConsequence(ExpeditionRecord record, RunState run, RunEndMode mode,
        object personSource) =>
        V4FTestKit.InvokeStatic(V4FTestKit.Asm("VillageReturnPersonConsequence"), "TryCommit",
            record, run, mode, V4FTestKit.BridgeTime(), personSource);

    private static VillageResourceState Resources() =>
        (VillageResourceState)V4FTestKit.GetStaticMember(V4FTestKit.Bridge, "Resources");

    /// <summary>读取未完成 Return 会话的原始出口模式（out 参数经反射回读）。</summary>
    private static bool TryGetPendingReturnMode(out RunEndMode mode)
    {
        var m = V4FTestKit.Bridge.GetMethod("TryGetPendingReturnMode",
            BindingFlags.Public | BindingFlags.Static);
        Assert.IsNotNull(m, "VillageRuntimeBridge.TryGetPendingReturnMode 应存在");
        object[] args = { null };
        bool ok = (bool)m.Invoke(null, args);
        mode = (RunEndMode)args[0];
        return ok;
    }

    private static ProfessionProgress NewProfessionProgress(string professionId, int exp)
    {
        return new ProfessionProgress
        {
            ProfessionId = professionId,
            ProfessionLevel = 0,
            ProfessionExp = exp,
            UnlockedPoolCardIds = new System.Collections.Generic.List<string> { "card_wpb" },
            ProfessionTraitLevel = 0
        };
    }

    private static int ActiveProfessionExp(object character)
    {
        var progress = (ProfessionProgress)V4FTestKit.Get(character, "ActiveProfessionProgress");
        return progress != null ? progress.ProfessionExp : -1;
    }

    private static void AddPersonWithCard(object collection, string templateId, string cardId,
        out object character, out string personId)
    {
        V4FTestKit.AddCharacterWithCard(collection, templateId, cardId, out character, out personId);
    }

    // ==================== T1｜Settlement Acknowledgement ====================

    [Test]
    public void ReturnSettlement_AllProfessionExpAckRequired()
    {
        object collection = V4FTestKit.NewPlayerCollection();
        object charA;
        string pidA;
        AddPersonWithCard(collection, "char_001", "card_wpb_a", out charA, out pidA);
        V4FTestKit.Set(charA, "ActiveProfessionProgress", NewProfessionProgress("profession_wpb_a", 0));

        object charB;
        string pidB;
        AddPersonWithCard(collection, "char_002", "card_wpb_b", out charB, out pidB);
        // B 没有 Active Profession → sink 无法 ack（TryAddProfessionExp = false）

        object adventure = V4FTestKit.NewAdventureManager(collection, null);
        RunState run = V4FTestKit.RunStateOf(adventure);
        run.AddPendingProfessionExp(pidA, 10);
        run.AddPendingProfessionExp(pidB, 5);

        V4FTestKit.SetExpedition(new[] { pidA, pidB });
        Assert.IsTrue(BeginReturn(0, RunEndMode.Retreat), "Return 会话应能开始");

        LogAssert.Expect(LogType.Error, new Regex("Return Settlement 未成功"));
        object result = SettleDetailed(adventure, RunEndMode.Retreat);

        Assert.AreEqual(10, (int)V4FTestKit.Get(result, "ProfessionExpAcknowledged"),
            "只有具备 Active Profession 的 Person 被 ack");
        Assert.AreEqual(5, (int)V4FTestKit.Get(result, "ProfessionExpPendingRemaining"),
            "未 ack 的 ProfessionExp 必须保留");
        Assert.IsFalse((bool)V4FTestKit.Get(result, "AllProfessionExpAcknowledged"),
            "任一 Person ack=false → Settlement 不成功");
        Assert.AreEqual(10, ActiveProfessionExp(charA), "已 ack Person 写入长期 Profession Growth");
        Assert.AreEqual(0, run.GetPerson(pidA).PendingProfessionExp, "已 ack Person 的 Pending 清零");
        Assert.AreEqual(5, run.GetPerson(pidB).PendingProfessionExp, "未 ack Person 的 Pending 不得被清除");
        Assert.IsTrue((bool)V4FTestKit.Get(adventure, "IsActive"),
            "Settlement 未成功 → IsActive 必须保持 true（不得让「已结束冒险」分支绕过未完成 Return）");
        Assert.AreEqual(ExpeditionReturnPhase.Active, V4FTestKit.ActiveRecord().Phase,
            "Settlement 未 ack 完成 → 相位不得越过 ReturnSettled");
        Assert.AreEqual("<no-save>", V4FTestKit.SaveHash(), "Settlement 从不 Save；失败更不得落盘");
    }

    [Test]
    public void ReturnSettlement_PartialAckRetry_NoDuplicate()
    {
        object collection = V4FTestKit.NewPlayerCollection();
        object charA;
        string pidA;
        AddPersonWithCard(collection, "char_001", "card_wpb_a", out charA, out pidA);
        V4FTestKit.Set(charA, "ActiveProfessionProgress", NewProfessionProgress("profession_wpb_a", 0));

        object charB;
        string pidB;
        AddPersonWithCard(collection, "char_002", "card_wpb_b", out charB, out pidB);

        object adventure = V4FTestKit.NewAdventureManager(collection, null);
        RunState run = V4FTestKit.RunStateOf(adventure);
        run.AddPendingProfessionExp(pidA, 10);
        run.AddPendingProfessionExp(pidB, 5);

        V4FTestKit.SetExpedition(new[] { pidA, pidB });
        Assert.IsTrue(BeginReturn(0, RunEndMode.Retreat));

        LogAssert.Expect(LogType.Error, new Regex("Return Settlement 未成功"));
        SettleDetailed(adventure, RunEndMode.Retreat);
        Assert.AreEqual(10, ActiveProfessionExp(charA));
        Assert.AreEqual(5, run.GetPerson(pidB).PendingProfessionExp);
        Assert.IsTrue((bool)V4FTestKit.Get(adventure, "IsActive"), "部分 ack → Settlement 未成功");

        // 解除未 ack 原因（同一 Run / 同一 Pending，不重建任何状态）后同会话重试。
        V4FTestKit.Set(charB, "ActiveProfessionProgress", NewProfessionProgress("profession_wpb_b", 0));
        object retry = SettleDetailed(adventure, RunEndMode.Retreat);

        Assert.AreEqual(5, (int)V4FTestKit.Get(retry, "ProfessionExpAcknowledged"),
            "重试只 ack 尚未兑现的 Person");
        Assert.IsTrue((bool)V4FTestKit.Get(retry, "AllProfessionExpAcknowledged"),
            "重试后全部 ack → Settlement 成功");
        Assert.AreEqual(0, run.GetPendingProfessionExpTotal(), "全部 ack → 无残留 Pending");
        Assert.AreEqual(10, ActiveProfessionExp(charA), "已 ack Person 重试 +0（不重复长期写入）");
        Assert.AreEqual(5, ActiveProfessionExp(charB), "未 ack Person 重试后正确写入");
        Assert.IsFalse((bool)V4FTestKit.Get(adventure, "IsActive"),
            "全部 ack 后 Settlement 成功 → IsActive=false");
        Assert.AreEqual("<no-save>", V4FTestKit.SaveHash(), "Settlement 自身从不 Save");
    }

    private static void UnackedProfessionExpNeverDiscarded(RunEndMode mode)
    {
        object collection = V4FTestKit.NewPlayerCollection();
        object charA;
        string pidA;
        AddPersonWithCard(collection, "char_001", "card_wpb_c", out charA, out pidA);
        // 无 Active Profession → 一定无法 ack

        object adventure = V4FTestKit.NewAdventureManager(collection, null);
        RunState run = V4FTestKit.RunStateOf(adventure);
        run.AddPendingProfessionExp(pidA, 7);
        run.AddPendingGold(20);
        run.AddPendingBuildingMaterial(3);
        run.AddPendingGrowthExp(pidA, 9);

        LogAssert.Expect(LogType.Error, new Regex("Return Settlement 未成功"));
        SettleDetailed(adventure, mode);

        Assert.AreEqual(7, run.GetPerson(pidA).PendingProfessionExp,
            mode + "：未 ack 的 ProfessionExp 绝不被 Discard 语义清除");
        Assert.AreEqual(7, run.GetPendingProfessionExpTotal());
        Assert.AreEqual(0, run.PendingGold, mode + "：非 ProfessionExp Pending 仍按既有占位语义丢弃");
        Assert.AreEqual(0, run.PendingBuildingMaterial);
        Assert.AreEqual(0, run.GetPerson(pidA).PendingGrowthExp);
        Assert.AreEqual("<no-save>", V4FTestKit.SaveHash(), mode + "：Settlement 从不 Save");
    }

    [Test]
    public void Retreat_UnackedProfessionExpNeverDiscarded()
    {
        UnackedProfessionExpNeverDiscarded(RunEndMode.Retreat);
    }

    [Test]
    public void Defeat_UnackedProfessionExpNeverDiscarded()
    {
        UnackedProfessionExpNeverDiscarded(RunEndMode.Defeat);
    }

    // ==================== T2｜Return Resume State Machine ====================

    [Test]
    public void ReturnSettled_RecordCannotBeRebegun()
    {
        V4FTestKit.SetExpedition(new[] { "char_exp_a" });
        Assert.IsTrue(BeginReturn(3, RunEndMode.Retreat));
        Assert.IsTrue(V4FTestKit.MarkSettled(), "Settlement 成功后置 ReturnSettled");

        var record = V4FTestKit.ActiveRecord();
        Assert.AreEqual(ExpeditionReturnPhase.ReturnSettled, record.Phase);

        Assert.IsFalse(BeginReturn(3, RunEndMode.Retreat),
            "ReturnSettled 未完成记录不得重新 Begin（否则 DaysSimulated 归零并重复已执行日）");
        Assert.IsFalse(V4FTestKit.BeginReturn(3), "无出口模式语境的 Begin 同样拒绝");
        Assert.AreEqual(0, record.DaysSimulated, "拒绝路径不得改变会话记录");

        // 完成 2 天后再尝试 Begin：仍拒绝，且 DaysSimulated 不被重置（此处正是 P1-B 的重复执行风险）。
        V4FTestKit.SetBeforeDayHook(day =>
        {
            if (day == 2) throw new InvalidOperationException("stop before day 2");
        });
        Assert.AreEqual(ExpeditionCatchUpResult.RetryableFailure, V4FTestKit.RunCatchUp());
        V4FTestKit.SetBeforeDayHook(null);
        Assert.AreEqual(2, record.DaysSimulated, "已完整执行 2 天");

        Assert.IsFalse(BeginReturn(3, RunEndMode.SafeReturn), "已有 2 天进度时仍不得重新 Begin");
        Assert.AreEqual(2, record.DaysSimulated, "拒绝重新 Begin 后 DaysSimulated 必须保持");

        Assert.AreEqual(ExpeditionCatchUpResult.Completed, V4FTestKit.RunCatchUp(),
            "Resume 从 DaysSimulated 继续完成剩余天数");
        Assert.AreEqual(3, record.DaysSimulated);

        // 显式 Discard = 文档化恢复路径：之后才允许重新开始会话。
        V4FTestKit.DiscardRecord();
        Assert.IsNull(V4FTestKit.ActiveRecord());
        Assert.IsTrue(BeginReturn(3, RunEndMode.Retreat), "显式 Discard 后允许重新开始会话");
    }

    [Test]
    public void ReturnResume_UsesOriginalRunEndMode()
    {
        // 原始出口模式属于会话状态：Resume 必须沿用，不得被其它出口改写。
        V4FTestKit.SetExpedition(new[] { "char_exp_a" });
        Assert.IsTrue(BeginReturn(1, RunEndMode.Defeat));

        Assert.IsTrue(TryGetPendingReturnMode(out RunEndMode mode));
        Assert.AreEqual(RunEndMode.Defeat, mode, "未完成会话保留原始出口模式（Defeat）");

        V4FTestKit.DiscardRecord();
        Assert.IsFalse(TryGetPendingReturnMode(out _),
            "Discard 会话记录后不得残留上一会话的出口模式所有权");

        Assert.IsTrue(BeginReturn(1, RunEndMode.SafeReturn));
        Assert.IsTrue(TryGetPendingReturnMode(out RunEndMode resumed));
        Assert.AreEqual(RunEndMode.SafeReturn, resumed);
    }

    // ==================== T3｜Catch-up 续跑 / 失败边界 ====================

    [Test]
    public void CatchUp_RetryContinuesFromDaysSimulated()
    {
        const string home = "char_home_work";
        V4FTestKit.BindWorkPersons(home);
        Assert.AreEqual(VillageAssignmentResult.Success, V4FTestKit.Assign(home, "ws_test_02"));
        V4FTestKit.SetExpedition(new[] { "char_exp_a" });

        Assert.IsTrue(BeginReturn(4, RunEndMode.Retreat));
        Assert.IsTrue(V4FTestKit.MarkSettled());

        int baseState = V4FTestKit.BridgeState().TestSurvivalValue;
        V4FTestKit.SetBeforeDayHook(day =>
        {
            if (day == 2) throw new InvalidOperationException("between-day failure before day 2");
        });

        Assert.AreEqual(ExpeditionCatchUpResult.RetryableFailure, V4FTestKit.RunCatchUp(),
            "Between-Day 失败 → RetryableFailure（无部分写入）");
        var record = V4FTestKit.ActiveRecord();
        Assert.AreEqual(2, record.DaysSimulated, "已完成 Day0 / Day1");
        Assert.AreEqual(ExpeditionReturnPhase.ReturnSettled, record.Phase,
            "RetryableFailure 保持 ReturnSettled（可同会话续跑）");
        Assert.AreEqual(3, V4FTestKit.BridgeTime().Day, "1/1/1 + 2 天 = 1/1/3");
        Assert.AreEqual(baseState + 2, V4FTestKit.BridgeState().TestSurvivalValue);
        Assert.AreEqual(2, V4FTestKit.Assignments().GetRecord(home).TotalExecutedDays);

        V4FTestKit.SetBeforeDayHook(null);
        Assert.AreEqual(ExpeditionCatchUpResult.Completed, V4FTestKit.RunCatchUp(), "Resume 只补剩余 2 天");

        Assert.AreEqual(4, record.DaysSimulated);
        Assert.AreEqual(5, V4FTestKit.BridgeTime().Day, "1/1/1 + 4 天 = 1/1/5");
        Assert.AreEqual(baseState + 4, V4FTestKit.BridgeState().TestSurvivalValue,
            "Day0 / Day1 绝不重复执行（State 恰好 +4）");
        Assert.AreEqual(4, V4FTestKit.Assignments().GetRecord(home).TotalExecutedDays,
            "逐日 Assignment 效果恰好 4 次");
        Assert.AreEqual("<no-save>", V4FTestKit.SaveHash(), "Catch-up 全程不落盘（最终 Save 由产品链唯一执行）");
    }

    [Test]
    public void ProjectReservationAndBoundaryRetry_DoesNotRepeatCompletedIntervals()
    {
        const string home = "char_home_work";
        V4FTestKit.BindWorkPersons(home);
        const string prj = "char_prj_member";
        const string exp = "char_exp_a";

        Assert.AreEqual(VillageAssignmentResult.Success, V4FTestKit.Assign(home, "ws_test_02"));
        Assert.AreEqual(VillageProjectResult.Success, V4FTestKit.StartProject("prj_test_01", prj));
        string projectId = V4FTestKit.Projects().ActiveProjects[0].ProjectId;

        V4FTestKit.SetExpedition(new[] { exp });
        Assert.IsTrue(BeginReturn(4, RunEndMode.Retreat));
        Assert.IsTrue(V4FTestKit.MarkSettled());

        int baseState = V4FTestKit.BridgeState().TestSurvivalValue;

        // 在第 3 天推进前注入异常重叠数据（等价 Restore / 异常层防御；产品入口本身拒绝该组合）。
        bool injected = false;
        V4FTestKit.SetBeforeDayHook(day =>
        {
            if (day != 2 || injected) return;
            injected = true;
            Assert.AreEqual(VillageProjectResult.Success,
                V4FTestKit.Projects().TryAddParticipant(projectId, exp),
                "低层 fixture 添加容量预约，远征者仍不实际劳动");
            throw new InvalidOperationException("injected boundary preparation failure");
        });

        Assert.AreEqual(ExpeditionCatchUpResult.RetryableFailure, V4FTestKit.RunCatchUp(),
            "逐日推进前必须拒绝远征者与 Project 参与者重叠");
        V4FTestKit.SetBeforeDayHook(null);

        var record = V4FTestKit.ActiveRecord();
        Assert.IsTrue(injected);
        Assert.AreEqual(2, record.DaysSimulated, "新边界完整 preflight 先拒绝异常重叠，保留已完成 Day0 / Day1");
        Assert.AreEqual(ExpeditionReturnPhase.ReturnSettled, record.Phase,
            "OverlapRejected 不产生半完成记录（可同会话从原进度 resume）");
        Assert.AreEqual(baseState + 2, V4FTestKit.BridgeState().TestSurvivalValue);
        Assert.AreEqual(2, V4FTestKit.Assignments().GetRecord(home).TotalExecutedDays);
        Assert.AreEqual("<no-save>", V4FTestKit.SaveHash(), "拒绝路径不落盘");

        // 问题解除（Test-only 清理异常数据）后同会话 resume：只补剩余 1 天。
        Assert.IsTrue(V4FTestKit.Projects().TryRemoveActiveProject(projectId));
        Assert.AreEqual(ExpeditionCatchUpResult.Completed, V4FTestKit.RunCatchUp());

        Assert.AreEqual(4, record.DaysSimulated, "从 DaysSimulated 继续，只执行第 4 天");
        Assert.AreEqual(5, V4FTestKit.BridgeTime().Day, "1/1/1 + 4 天 = 1/1/5");
        Assert.AreEqual(baseState + 4, V4FTestKit.BridgeState().TestSurvivalValue,
            "已完成的 3 天绝不重复执行（State 恰好 +4）");
        Assert.AreEqual(4, V4FTestKit.Assignments().GetRecord(home).TotalExecutedDays,
            "逐日 Assignment 效果恰好 4 次");
    }

    [Test]
    public void PartialDayFailure_BlocksAutomaticResume()
    {
        V4FTestKit.SetExpedition(new[] { "char_exp_a" });
        Assert.IsTrue(BeginReturn(2, RunEndMode.Retreat));
        Assert.IsTrue(V4FTestKit.MarkSettled());

        var time = V4FTestKit.BridgeTime();
        Action<int> boom = _ => throw new InvalidOperationException("mid-day partial failure");
        time.DayTicked += boom;
        try
        {
            LogAssert.Expect(LogType.Error, new Regex("Return Catch-up 在 Day 中途失败"));
            Assert.AreEqual(ExpeditionCatchUpResult.PartialDayFailure, V4FTestKit.RunCatchUp(),
                "Day 内异常（部分效果可能已写入）→ PartialDayFailure");
        }
        finally
        {
            time.DayTicked -= boom;
        }

        var record = V4FTestKit.ActiveRecord();
        Assert.AreEqual(ExpeditionReturnPhase.Failed, record.Phase, "PartialDayFailure → 记录转 Failed");
        Assert.AreEqual(0, record.DaysSimulated, "失败日不计入 DaysSimulated");
        Assert.IsFalse(record.Completed);

        // 不自动重试该日 / 不继续后续 Day / 不重新 Begin。
        Assert.AreEqual(ExpeditionCatchUpResult.RejectedFailedRecord, V4FTestKit.RunCatchUp(),
            "Failed 记录禁止自动续跑");
        Assert.IsFalse(BeginReturn(2, RunEndMode.Retreat), "Failed 记录存在时不得重新 Begin");
        Assert.IsFalse(V4FTestKit.BeginReturn(2));
        Assert.AreEqual(ExpeditionReturnPhase.Failed, record.Phase);
        Assert.AreEqual(0, record.DaysSimulated);
        Assert.AreEqual("<no-save>", V4FTestKit.SaveHash(), "Failed 路径不得落盘");

        // 恢复路径 = 丢弃会话记录（再从最后一次成功 Save 重开；本测试不模拟重开加载）。
        V4FTestKit.DiscardRecord();
        Assert.IsNull(V4FTestKit.ActiveRecord());
    }

    // ==================== T4｜Carry / Consequence Fail Closed ====================

    [Test]
    public void CarryRejected_DoesNotClearOccupancyOrSave()
    {
        V4FTestKit.SetExpedition(new[] { "char_exp_a" });
        Assert.IsTrue(BeginReturn(0, RunEndMode.Retreat));
        Assert.IsTrue(V4FTestKit.MarkSettled());
        Assert.AreEqual(ExpeditionCatchUpResult.AlreadyCaughtUp, V4FTestKit.RunCatchUp());

        var record = V4FTestKit.ActiveRecord();
        Assert.AreEqual(ExpeditionReturnPhase.CatchUpComplete, record.Phase);

        // 资源宿主不可用（Test seam）→ Carry 阶段 Rejected。
        V4FTestKit.SetStaticField(V4FTestKit.Bridge, "Resources", null);
        Assert.AreEqual(ReturnCarryStageResult.Rejected, CarryStage(4), "资源不可用 → Rejected");
        Assert.IsFalse((bool)V4FTestKit.InvokeStatic(V4FTestKit.Bridge, "ReturnRemainingCarry", 4),
            "既有 bool 语义：Rejected 仍返回 false（公共语义零回归）");
        Assert.IsFalse(record.RemainingCarryReturned, "拒绝不得置位 exactly-once 标记");
        Assert.AreEqual(ExpeditionReturnPhase.CatchUpComplete, record.Phase, "Carry 未成功 → 不得越过");
        Assert.AreEqual(1, V4FTestKit.ExpeditionIds().Count, "Carry 未成功 → 保留 Expedition Occupancy");
        Assert.AreEqual("<no-save>", V4FTestKit.SaveHash(), "Carry 未成功 → 不得落盘");

        // 恢复资源宿主后同会话重试：Applied → 继续；再调用 → AlreadyApplied（幂等可继续，不是失败）。
        V4FTestKit.EnsureInitialized();
        Assert.AreEqual(ReturnCarryStageResult.Applied, CarryStage(4), "恢复后重试成功返还");
        Assert.AreEqual(ReturnCarryStageResult.AlreadyApplied, CarryStage(4),
            "会话内已返还 → AlreadyApplied（可继续后续阶段）");
        Assert.AreEqual(4, Resources().Food, "幂等命中不重复返还");

        V4FTestKit.ClearExpedition();
        Assert.AreEqual(ExpeditionReturnPhase.OccupancyCleared, record.Phase, "前置阶段完成后才 Clear");
    }

    [Test]
    public void ConsequenceRejected_DoesNotClearOccupancyOrSave()
    {
        object collection = V4FTestKit.NewPlayerCollection();
        object character;
        string personId;
        AddPersonWithCard(collection, "char_001", "card_wpb_cons", out character, out personId);

        V4FTestKit.SetExpedition(new[] { personId });
        Assert.IsTrue(BeginReturn(0, RunEndMode.Defeat));
        Assert.IsTrue(V4FTestKit.MarkSettled());
        Assert.AreEqual(ExpeditionCatchUpResult.AlreadyCaughtUp, V4FTestKit.RunCatchUp());

        var record = V4FTestKit.ActiveRecord();
        Assert.AreEqual(ExpeditionReturnPhase.CatchUpComplete, record.Phase);
        Assert.AreEqual(ReturnCarryStageResult.Applied, CarryStage(0));

        RunState run = new RunState();
        run.GetOrCreatePerson(personId, "WPB", 30);
        run.GetPerson(personId).CurrentHP = 5; // ratio ≈ 0.17 → HeavyInjury（Test / Not Frozen）

        // 参数不齐（person source 不可用）→ Rejected：不写任何长期状态、不 Clear、不落盘。
        object rejected = TryCommitConsequence(record, run, RunEndMode.Defeat, null);
        Assert.IsTrue((bool)V4FTestKit.Get(rejected, "Rejected"), "person source 不可用 → Rejected");
        Assert.IsFalse(string.IsNullOrEmpty((string)V4FTestKit.Get(rejected, "RejectReason")),
            "拒绝必须带可诊断原因");
        Assert.IsFalse(record.HealthConsequenceCommitted, "拒绝不得置位 exactly-once 标记");
        Assert.IsFalse(record.ExperienceCommitted);
        Assert.AreEqual(ExpeditionReturnPhase.CatchUpComplete, record.Phase, "Consequence 未提交 → 不得越过");
        Assert.AreEqual(1, V4FTestKit.ExpeditionIds().Count, "不得清除 Expedition Occupancy");
        Assert.AreEqual("<no-save>", V4FTestKit.SaveHash(), "不得落盘");
        Assert.AreEqual("Normal", V4FTestKit.Get(character, "Injury").ToString(), "拒绝时零长期写入");

        // 参数齐备后同会话重试：成功提交 → 才允许 Clear。
        object accepted = TryCommitConsequence(record, run, RunEndMode.Defeat, collection);
        Assert.IsFalse((bool)V4FTestKit.Get(accepted, "Rejected"));
        Assert.AreEqual(1, (int)V4FTestKit.Get(accepted, "HealthWritten"));
        Assert.AreEqual(1, (int)V4FTestKit.Get(accepted, "ExperienceWritten"));
        Assert.AreEqual("HeavyInjury", V4FTestKit.Get(character, "Injury").ToString());

        V4FTestKit.ClearExpedition();
        Assert.AreEqual(ExpeditionReturnPhase.OccupancyCleared, record.Phase);
    }

    // ==================== T5｜Final Save retry + Completed exactly-once ====================

    [Test]
    public void FinalSaveFailure_RetryOnlySave()
    {
        V4FTestKit.SetExpedition(new[] { "char_exp_a" });
        Assert.IsTrue(BeginReturn(0, RunEndMode.Retreat));
        Assert.IsTrue(V4FTestKit.MarkSettled());
        Assert.AreEqual(ExpeditionCatchUpResult.AlreadyCaughtUp, V4FTestKit.RunCatchUp());

        var record = V4FTestKit.ActiveRecord();
        Assert.AreEqual(ReturnCarryStageResult.Applied, CarryStage(5));

        object collection = V4FTestKit.NewPlayerCollection();
        RunState run = new RunState();
        object accepted = TryCommitConsequence(record, run, RunEndMode.Retreat, collection);
        Assert.IsFalse((bool)V4FTestKit.Get(accepted, "Rejected"), "前置阶段完成后 Consequence 可提交");

        V4FTestKit.ClearExpedition();
        Assert.AreEqual(ExpeditionReturnPhase.OccupancyCleared, record.Phase);

        int foodAfterCarry = Resources().Food;
        int daysBefore = record.DaysSimulated;

        // 最终 Save 失败：保持 OccupancyCleared / Completed=false（可只重试 Save）。
        V4FTestKit.CompleteSave(false);
        Assert.IsFalse(record.Completed, "Save false → 不得置 Completed");
        Assert.AreEqual(ExpeditionReturnPhase.OccupancyCleared, record.Phase);

        // 只 retry Save：不重跑 Settlement / Catch-up / Carry / Consequence / Clear。
        Assert.AreEqual(daysBefore, record.DaysSimulated, "不得重补算");
        Assert.IsTrue(record.RemainingCarryReturned, "Carry exactly-once 标记保持");
        Assert.IsTrue(record.HealthConsequenceCommitted, "Consequence exactly-once 标记保持");
        Assert.IsTrue(record.ExperienceCommitted);
        Assert.AreEqual(ReturnCarryStageResult.AlreadyApplied, CarryStage(5), "Carry 只允许幂等命中");
        Assert.AreEqual(foodAfterCarry, Resources().Food, "不得重复返还 Carry");
        Assert.AreEqual(0, V4FTestKit.ExpeditionIds().Count, "Clear 已生效且不得回退 Occupancy");

        V4FTestKit.CompleteSave(true);
        Assert.IsTrue(record.Completed, "重试 Save 成功 → Completed");
        Assert.AreEqual(ExpeditionReturnPhase.Completed, record.Phase);
        Assert.AreEqual(daysBefore, record.DaysSimulated, "重试 Save 不改变已完成阶段");
    }

    [Test]
    public void CompletedReturn_IsExactlyOnce()
    {
        V4FTestKit.SetExpedition(new[] { "char_exp_a" });
        Assert.IsTrue(BeginReturn(2, RunEndMode.Retreat));
        Assert.IsTrue(V4FTestKit.MarkSettled());
        Assert.AreEqual(ExpeditionCatchUpResult.Completed, V4FTestKit.RunCatchUp());

        var record = V4FTestKit.ActiveRecord();
        Assert.AreEqual(ReturnCarryStageResult.Applied, CarryStage(3));
        int foodAfterCarry = Resources().Food;
        V4FTestKit.ClearExpedition();
        V4FTestKit.CompleteSave(true);
        Assert.IsTrue(record.Completed);

        int dayAfter = V4FTestKit.BridgeTime().Day;
        Assert.AreEqual(3, dayAfter, "1/1/1 + 2 天 = 1/1/3");

        // Completed 后所有 Return 入口 no-op / 拒绝（跨调用 exactly-once）。
        Assert.IsFalse(BeginReturn(2, RunEndMode.Retreat), "Completed → 重复 Begin no-op");
        Assert.AreEqual(ExpeditionCatchUpResult.AlreadyCompleted, V4FTestKit.RunCatchUp());
        Assert.AreEqual(ReturnCarryStageResult.Rejected, CarryStage(3), "Completed → 不再返还 Carry");
        Assert.IsFalse((bool)V4FTestKit.InvokeStatic(V4FTestKit.Bridge, "ReturnRemainingCarry", 3));
        Assert.IsFalse(V4FTestKit.MarkSettled(), "Completed → MarkSettled no-op");
        V4FTestKit.CompleteSave(true); // 幂等重放

        Assert.IsTrue(record.Completed);
        Assert.AreEqual(ExpeditionReturnPhase.Completed, record.Phase);
        Assert.AreEqual(2, record.DaysSimulated, "Completed 会话不得被重放改变");
        Assert.AreEqual(3, V4FTestKit.BridgeTime().Day, "Completed 后不得再推进 Village 时间");
        Assert.AreEqual(foodAfterCarry, Resources().Food, "Completed 后资源不再变化");
        Assert.AreEqual("<no-save>", V4FTestKit.SaveHash(),
            "bridge 自身从不 Save（唯一最终 Return Save 由产品 caller 执行）");
    }
}
