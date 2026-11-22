using System.Reflection;
using NUnit.Framework;

/// <summary>
/// V4F Return 守卫契约（Final Development Spec 修订 2）：
/// IsReturnCatchUpRunning（防重入）与 ExpeditionRecord.Completed（跨调用 exactly-once）语义分离；
/// 最终 Save 失败只重试 Save；Settlement 前不得推进任何 Day。
/// </summary>
public class ExpeditionReturnGuardTests
{
    private static readonly BindingFlags StaticF =
        BindingFlags.Public | BindingFlags.Static;

    private static PropertyInfo RunningProp()
    {
        var p = V4FTestKit.Bridge.GetProperty("IsReturnCatchUpRunning", StaticF);
        Assert.IsNotNull(p, "IsReturnCatchUpRunning 应存在");
        return p;
    }

    [SetUp]
    public void SetUp()
    {
        V4FTestKit.ResetBridge();
        V4FTestKit.DeleteSave();
        V4FTestKit.EnsureInitialized();
    }

    [TearDown]
    public void TearDown()
    {
        V4FTestKit.SetBeforeDayHook(null);
        V4FTestKit.ResetBridge();
        V4FTestKit.DeleteSave();
    }

    [Test]
    public void T11b_ReentrantCatchUp_IsRejected_DuringRunning()
    {
        V4FTestKit.SetExpedition(new[] { "char_001" });
        V4FTestKit.SetTime(1, 1, 1);
        Assert.IsTrue(V4FTestKit.BeginReturn(3));
        Assert.IsTrue(V4FTestKit.MarkSettled());

        bool nestedBegin = true;
        ExpeditionCatchUpResult nestedCatch = ExpeditionCatchUpResult.Completed;
        bool nestedRunning = false;
        bool fired = false;
        var runningProp = RunningProp();

        V4FTestKit.SetBeforeDayHook(day =>
        {
            if (fired) return;
            fired = true;
            nestedRunning = (bool)runningProp.GetValue(null, null);
            nestedBegin = V4FTestKit.BeginReturn(9);
            nestedCatch = V4FTestKit.RunCatchUp();
        });

        var result = V4FTestKit.RunCatchUp();
        Assert.AreEqual(ExpeditionCatchUpResult.Completed, result, "外层 Catch-up 应正常完成");
        Assert.IsTrue(fired, "每日前钩子应触发一次");
        Assert.IsTrue(nestedRunning, "Catch-up 执行期间 IsReturnCatchUpRunning 应为 true");
        Assert.IsFalse(nestedBegin, "Catch-up 执行期间再次 Begin → 拒绝（防重入）");
        Assert.AreEqual(ExpeditionCatchUpResult.RejectedReentrant, nestedCatch,
            "Catch-up 执行期间再次 Catch-up → RejectedReentrant");

        var record = V4FTestKit.ActiveRecord();
        Assert.AreEqual(3, record.DaysSimulated, "重入请求不得干扰外层逐日计数");
        Assert.AreEqual(ExpeditionReturnPhase.CatchUpComplete, record.Phase);
        Assert.IsFalse((bool)runningProp.GetValue(null, null), "finally 应复位 IsReturnCatchUpRunning");
    }

    [Test]
    public void CatchUp_BeforeSettlement_IsInvalidPhase()
    {
        V4FTestKit.SetExpedition(new[] { "char_001" });
        V4FTestKit.SetTime(1, 1, 1);
        Assert.IsTrue(V4FTestKit.BeginReturn(2));
        // 未 MarkSettled：Phase 仍是 Active
        var result = V4FTestKit.RunCatchUp();
        Assert.AreEqual(ExpeditionCatchUpResult.InvalidPhase, result);
        Assert.AreEqual(0, V4FTestKit.ActiveRecord().DaysSimulated, "未 Settlement 不得推进任何 Day");
        Assert.AreEqual(1, V4FTestKit.BridgeTime().Day, "未 Settlement 不得改变 VillageTime");
    }

    [Test]
    public void FinalSaveFailed_KeepsOccupancyCleared_RetrySaveOnly()
    {
        V4FTestKit.SetExpedition(new[] { "char_001" });
        Assert.IsTrue(V4FTestKit.BeginReturn(0));
        Assert.IsTrue(V4FTestKit.MarkSettled());
        V4FTestKit.RunCatchUp();
        V4FTestKit.ClearExpedition();

        var record = V4FTestKit.ActiveRecord();
        Assert.AreEqual(ExpeditionReturnPhase.OccupancyCleared, record.Phase);

        // Save 失败 → 保持 OccupancyCleared、Completed=false（可只重试 Save，不重结算/不重补算）。
        V4FTestKit.CompleteSave(false);
        Assert.IsFalse(record.Completed);
        Assert.AreEqual(ExpeditionReturnPhase.OccupancyCleared, record.Phase);

        // 重试仅 Save：不会再次结算 / 再次补算（DaysSimulated / Phase 不变）。
        V4FTestKit.CompleteSave(true);
        Assert.IsTrue(record.Completed, "重试 Save 成功后才置 Completed");
        Assert.AreEqual(ExpeditionReturnPhase.Completed, record.Phase);
        Assert.AreEqual(0, record.DaysSimulated);
    }
}
