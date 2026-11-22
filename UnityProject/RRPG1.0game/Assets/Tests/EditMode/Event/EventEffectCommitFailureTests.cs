using NUnit.Framework;
using RRPG.Event;
using static RRPG.Event.Tests.EventExecutionTestKit;
using static RRPG.Event.Tests.EventEffectTransactionTests;
using static RRPG.Event.Tests.EventResolverTestWorld;

namespace RRPG.Event.Tests
{
    /// <summary>
    /// WP-Event-1C：Commit 真实失败必须如实报告（§11）。覆盖：C14。
    ///
    /// 注意（刻意的语义边界）：1C **不承诺**跨系统万能 rollback —— 若前序效果已经真实提交、
    /// 后续效果失败，Event Core 只保证「如实失败 + 不写 History + 不标 Completed」；
    /// 产品 Gateway 必须在 1D 用能兑现「Preflight 通过 ⇒ Commit 成功」的 Authority 接口实现。
    /// </summary>
    public class EventEffectCommitFailureTests
    {
        [Test]
        public void C14_CommitFailure_ReportsFailure_AndDoesNotRecordHistoryOrComplete()
        {
            var catalog = BuildCatalog("opt_commit_fail", "Hide",
                Effect("eff_battle", "out_opt_commit_fail", 10, "StartBattle", targetKey: "pool_test_general"));

            var gateway = new EventEffectGatewayFixture { FailStartBattle = true };
            var worldState = new EventWorldState();
            var execution = new EventExecutionState();
            var instance = ResolveInstance(catalog);
            Assert.IsNotNull(instance);

            var result = EventOptionExecutor.Execute(instance, "opt_commit_fail", catalog, StandardWorld(),
                worldState, new ExpeditionEventSessionState(), execution, gateway);

            Assert.IsFalse(result.Success);
            Assert.AreEqual(EventExecutionCode.EffectCommitFailed, result.Code);
            Assert.AreEqual(1, gateway.StartBattleCalls, "StartBattle 必须被真实尝试过（不是静默跳过）");
            Assert.AreEqual(0, worldState.History.OccurrenceCount(EventId), "Commit 失败不得写 History");
            Assert.IsFalse(execution.IsCompleted(instance.EventInstanceId), "Commit 失败不得标记完成");
            StringAssert.Contains("StartBattle", result.Diagnostic);
        }

        [Test]
        public void C14_GatewayThrows_IsReportedAsCommitFailure_NotSwallowed()
        {
            var catalog = BuildCatalog("opt_throw", "Hide",
                Effect("eff_throw", "out_opt_throw", 10, "StartBattle", targetKey: "pool_test_general"));

            var gateway = new EventEffectGatewayFixture { ThrowOnStartBattle = true };
            var worldState = new EventWorldState();
            var execution = new EventExecutionState();
            var instance = ResolveInstance(catalog);
            Assert.IsNotNull(instance);

            var result = EventOptionExecutor.Execute(instance, "opt_throw", catalog, StandardWorld(), worldState,
                new ExpeditionEventSessionState(), execution, gateway);

            Assert.IsFalse(result.Success);
            Assert.AreEqual(EventExecutionCode.EffectCommitFailed, result.Code);
            StringAssert.Contains("异常", result.Diagnostic);
            Assert.AreEqual(0, worldState.History.OccurrenceCount(EventId));
            Assert.IsFalse(execution.IsCompleted(instance.EventInstanceId));
        }

        [Test]
        public void C14_CommitFailure_KeepsEarlierAppliedEffects_HonestlyReported()
        {
            // 前一条效果（SetFlag）会真实提交；第二条（SupplyDelta，被注入失败）失败。
            // 不谎报成功：Code = EffectCommitFailed；History / Completed 都不写。
            var catalog = BuildCatalog("opt_partial", "Hide",
                Effect("eff_set", "out_opt_partial", 10, "SetFlag", targetKey: "flag_committed"),
                Effect("eff_supply", "out_opt_partial", 20, "SupplyDelta", intValue: -5));

            var gateway = new EventEffectGatewayFixture { FailApplySupplyDelta = true };
            var worldState = new EventWorldState();
            var execution = new EventExecutionState();
            var instance = ResolveInstance(catalog);
            Assert.IsNotNull(instance);

            var result = EventOptionExecutor.Execute(instance, "opt_partial", catalog, StandardWorld(), worldState,
                new ExpeditionEventSessionState(), execution, gateway);

            Assert.IsFalse(result.Success);
            Assert.AreEqual(EventExecutionCode.EffectCommitFailed, result.Code);
            Assert.IsTrue(worldState.IsFlagSet("flag_committed"), "已提交的前序效果保持（1C 不做万能 rollback）");
            Assert.AreEqual(0, worldState.History.OccurrenceCount(EventId));
            Assert.IsFalse(execution.IsCompleted(instance.EventInstanceId));
            Assert.AreEqual(20, gateway.ExpeditionSupply, "失败的效果不得改 Supply");
        }
    }
}
