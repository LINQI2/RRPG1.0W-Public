using NUnit.Framework;

/// <summary>
/// V4F Return 顺序契约（Final Development Spec 修订 1 冻结顺序）+ 测试计划 T14：
///   Settlement 后 / Catch-up 前 / Clear 前均不落盘；Clear 后仅一次最终 Save；
///   Save 内容同时含 Settlement（Pending 兑现）与 Catch-up 后村庄状态（Time/State）。
/// </summary>
public class ExpeditionReturnOrderSaveTests
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
        V4FTestKit.ResetBridge();
        V4FTestKit.DeleteSave();
    }

    [Test]
    public void T14_ReturnOrder_NoSaveBetweenPhases_SingleFinalSave_ContainsSettlementAndCatchUp()
    {
        // 离村前状态（最后成功 Save 基线）
        var collection = V4FTestKit.NewPlayerCollection();
        V4FTestKit.SetGold(collection, 100);
        var state = V4FTestKit.BridgeState();
        state.TestSurvivalValue = 50;
        Assert.IsTrue(V4FTestKit.Save(collection), "离村前 Save 应成功（基线）");
        string baseHash = V4FTestKit.SaveHash();
        Assert.AreNotEqual("<no-save>", baseHash);

        // 出征登记 + Run 带有待结算 Pending Gold（SafeReturn 将全额兑现 +20）
        V4FTestKit.SetExpedition(new[] { "char_exp_a" });
        object adventure = V4FTestKit.NewAdventureManager(collection, null);
        var run = V4FTestKit.RunStateOf(adventure);
        run.AddPendingGold(20);

        // Phase 1 —— Settlement（不内含 Save）
        Assert.IsTrue(V4FTestKit.BeginReturn(3), "Return 会话应开始");
        bool oldSaveRuleIndication = (bool)V4FTestKit.Call(adventure, "SettleReturn", RunEndMode.SafeReturn);
        Assert.IsTrue(oldSaveRuleIndication, "SafeReturn 按旧 EndRun 规则本应立即 Save——SettleReturn 只返回该指示、自身不 Save");
        Assert.AreEqual(baseHash, V4FTestKit.SaveHash(), "Settlement 之后不得发生文件 Save");
        Assert.AreEqual(120, V4FTestKit.GoldOf(collection), "Settlement 已把 Pending Gold 兑现到长期（最终 Save 需包含）");
        Assert.IsTrue(V4FTestKit.MarkSettled(), "Settlement 成功后置 ReturnSettled");

        // Phase 2 —— Return Catch-up 3 天（不落盘）
        var catchUp = V4FTestKit.RunCatchUp();
        Assert.AreEqual(ExpeditionCatchUpResult.Completed, catchUp);
        Assert.AreEqual(baseHash, V4FTestKit.SaveHash(), "Catch-up 期间不得发生文件 Save");
        Assert.AreEqual(4, V4FTestKit.BridgeTime().Day, "Catch-up 已把 3 天逐日推进");
        Assert.AreEqual(53, state.TestSurvivalValue, "Catch-up 已应用逐日 VillageState 规则");

        // Phase 3 —— Clear Expedition Occupancy（不落盘）
        V4FTestKit.ClearExpedition();
        Assert.AreEqual(0, V4FTestKit.ExpeditionIds().Count, "Clear 后远征登记清空");
        Assert.AreEqual(baseHash, V4FTestKit.SaveHash(), "Clear Occupancy 不得触发 Save");
        var record = V4FTestKit.ActiveRecord();
        Assert.AreEqual(ExpeditionReturnPhase.OccupancyCleared, record.Phase);

        // Phase 4 —— 最终 Return Save（唯一一次落盘）
        Assert.IsTrue(V4FTestKit.Save(collection), "最终 Return Save 应成功");
        V4FTestKit.CompleteSave(true);
        Assert.AreNotEqual(baseHash, V4FTestKit.SaveHash(), "最终 Return Save 必须改变存档内容");
        Assert.IsTrue(record.Completed, "最终 Save 成功后才置 Completed");
        Assert.AreEqual(ExpeditionReturnPhase.Completed, record.Phase);

        // 回读：单一存档同时包含 Settlement 与 Catch-up 后村庄状态
        V4FTestKit.ResetBridge();
        object loaded = V4FTestKit.Load();
        Assert.IsNotNull(loaded, "最终存档应可加载");
        Assert.AreEqual(120, V4FTestKit.GoldOf(loaded), "存档含 Settlement 兑现后的长期金币");
        var t2 = V4FTestKit.BridgeTime();
        Assert.AreEqual(1, t2.Year);
        Assert.AreEqual(1, t2.Month);
        Assert.AreEqual(4, t2.Day, "存档含 Catch-up 后的 VillageTime");
        Assert.AreEqual(53, V4FTestKit.BridgeState().TestSurvivalValue, "存档含 Catch-up 后的 VillageState");
    }

    [Test]
    public void EndRun_LegacyEntry_StillSavesExactlyAsBefore()
    {
        // 回归（M5 S10 依赖语义）：EndRun = SettleReturn + 按旧规则立即 Save（行为零回归）。
        var collection = V4FTestKit.NewPlayerCollection();
        V4FTestKit.SetGold(collection, 100);
        Assert.IsTrue(V4FTestKit.Save(collection));
        string baseHash = V4FTestKit.SaveHash();

        object adventure = V4FTestKit.NewAdventureManager(collection, null);
        var run = V4FTestKit.RunStateOf(adventure);
        run.AddPendingGold(20);

        V4FTestKit.Call(adventure, "EndRun", RunEndMode.SafeReturn);
        Assert.AreEqual(120, V4FTestKit.GoldOf(collection), "EndRun 应兑现 Pending");
        Assert.AreNotEqual(baseHash, V4FTestKit.SaveHash(), "EndRun 旧语义：SafeReturn 结算后立即 Save");
    }
}
