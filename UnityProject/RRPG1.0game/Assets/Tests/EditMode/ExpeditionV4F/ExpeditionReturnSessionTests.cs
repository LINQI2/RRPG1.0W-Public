using NUnit.Framework;

/// <summary>
/// V4F Return 会话状态机契约（Final Development Spec 修订 2）：
/// 冻结顺序相位推进（Active → ReturnSettled → CatchUpComplete → OccupancyCleared → Completed）、
/// ExpeditionRecord.Completed 跨调用 exactly-once、重复 Return no-op。
/// </summary>
public class ExpeditionReturnSessionTests
{
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
        V4FTestKit.ResetBridge();
        V4FTestKit.DeleteSave();
    }

    private static void CompleteZeroDayReturn(out ExpeditionRecord record)
    {
        V4FTestKit.SetExpedition(new[] { "char_001" });
        Assert.IsTrue(V4FTestKit.BeginReturn(0), "0 天 Return 会话应能开始");
        Assert.IsTrue(V4FTestKit.MarkSettled(), "Settlement 后应置 ReturnSettled");
        Assert.AreEqual(ExpeditionCatchUpResult.AlreadyCaughtUp, V4FTestKit.RunCatchUp(),
            "0 有效天 = 空跑，返回 AlreadyCaughtUp");
        V4FTestKit.ClearExpedition();
        Assert.AreEqual(0, V4FTestKit.ExpeditionIds().Count, "Clear 后远征登记应清空");
        V4FTestKit.CompleteSave(true);
        record = V4FTestKit.ActiveRecord();
        Assert.IsNotNull(record);
        Assert.AreEqual(ExpeditionReturnPhase.Completed, record.Phase);
        Assert.IsTrue(record.Completed, "最终 Return Save 成功后 Completed=true");
    }

    [Test]
    public void ZeroDay_FullReturn_PhasesAdvanceToCompleted()
    {
        V4FTestKit.SetTime(1, 1, 1);
        CompleteZeroDayReturn(out ExpeditionRecord record);

        Assert.AreEqual(1, record.DepartureYear);
        Assert.AreEqual(1, record.DepartureMonth);
        Assert.AreEqual(1, record.DepartureDay);
        Assert.AreEqual(0, record.EffectiveElapsedDays);
        Assert.AreEqual(0, record.DaysSimulated);
        Assert.AreEqual(1, record.PersonIds.Count, "记录应快照本次远征者");
        Assert.AreEqual("char_001", record.PersonIds[0]);
    }

    [Test]
    public void T11a_RepeatedReturn_AfterCompleted_IsNoOp()
    {
        CompleteZeroDayReturn(out _);

        // Completed 之后：任何 Return / Catch-up 入口都必须 no-op（跨调用 exactly-once）。
        Assert.IsFalse(V4FTestKit.BeginReturn(0), "Completed 后 Begin 重复 Return → false no-op");
        Assert.IsFalse(V4FTestKit.BeginReturn(5), "Completed 后带天数的 Begin 同样 no-op");
        Assert.AreEqual(ExpeditionCatchUpResult.AlreadyCompleted, V4FTestKit.RunCatchUp(),
            "Completed 后 Catch-up → AlreadyCompleted no-op");

        var record = V4FTestKit.ActiveRecord();
        Assert.IsTrue(record.Completed);
        Assert.AreEqual(ExpeditionReturnPhase.Completed, record.Phase);
        Assert.AreEqual(0, record.DaysSimulated, "重复 Return 不得改变已完成的会话");
    }

    [Test]
    public void NewDeparture_ResetsCompletedSessionRecord()
    {
        CompleteZeroDayReturn(out ExpeditionRecord record);
        Assert.IsTrue(record.Completed);

        // 一次新的出征登记 = 新会话开始：旧 Completed 记录不得跨会话携带。
        V4FTestKit.SetExpedition(new[] { "char_009" });
        Assert.IsNull(V4FTestKit.ActiveRecord(), "新出征登记应清空上一 Return 会话记录");
    }
}
