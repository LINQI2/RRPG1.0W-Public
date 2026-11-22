using NUnit.Framework;
using RRPG.Event;
using static RRPG.Event.Tests.EventExecutionTestKit;
using static RRPG.Event.Tests.EventEffectTransactionTests;
using static RRPG.Event.Tests.EventResolverTestWorld;

namespace RRPG.Event.Tests
{
    /// <summary>WP-Event-1C：Preflight 失败 = 零 mutation，且可以重试。覆盖：C11、C17。</summary>
    public class EventEffectPreflightFailureTests
    {
        [Test]
        public void C11_PreflightFailure_LeavesEverythingUntouched()
        {
            // Effect 1 本来会写 Flag；Effect 2 会让 Supply 变负 → 整笔必须在 Preflight 阶段停下。
            var catalog = BuildCatalog("opt_zero", "Hide",
                Effect("eff_flag", "out_opt_zero", 10, "SetFlag", targetKey: "flag_never"),
                Effect("eff_supply", "out_opt_zero", 20, "SupplyDelta", intValue: -999));

            var gateway = new EventEffectGatewayFixture(supply: 20);
            var worldState = new EventWorldState();
            var execution = new EventExecutionState();
            var instance = ResolveInstance(catalog);
            Assert.IsNotNull(instance);

            var result = EventOptionExecutor.Execute(instance, "opt_zero", catalog, StandardWorld(), worldState,
                new ExpeditionEventSessionState(), execution, gateway);

            Assert.IsFalse(result.Success);
            Assert.AreEqual(EventExecutionCode.EffectPreflightFailed, result.Code);

            Assert.IsEmpty(worldState.Flags, "Preflight 失败不得写 Flag");
            Assert.AreEqual(0, worldState.History.OccurrenceCount(EventId), "Preflight 失败不得写 History");
            Assert.AreEqual(20, gateway.ExpeditionSupply, "Preflight 失败不得改 Supply");
            Assert.AreEqual(0, gateway.TotalMutationCalls);
            Assert.IsFalse(execution.IsCompleted(instance.EventInstanceId), "失败不得标记完成");
            StringAssert.Contains("Preflight", result.Diagnostic);
        }

        [Test]
        public void C17_FailedPreflight_CanBeRetried_AfterWorldIsFixed_SameOutcome()
        {
            var catalog = BuildCatalog("opt_retry", "Hide",
                Effect("eff_pay", "out_opt_retry", 10, "VillageResourceDelta", targetKey: "Food", intValue: -30));

            var worldState = new EventWorldState();
            var execution = new EventExecutionState();

            var poorWorld = new EventWorldViewSnapshot()
                .AddPerson("p1", inVillage: true, available: true)
                .SetVillageState(100, 50, 20)
                .SetExpeditionState(3, 20, 2, 5);

            var instance = ResolveInstance(catalog, world: poorWorld);
            Assert.IsNotNull(instance);

            // 第一次：Food 20 < 30 → Preflight 失败（零 mutation）
            var poorGateway = new EventEffectGatewayFixture(ingredients: 50, food: 20, supply: 20);
            var first = EventOptionExecutor.Execute(instance, "opt_retry", catalog, poorWorld, worldState,
                new ExpeditionEventSessionState(), execution, poorGateway);

            Assert.AreEqual(EventExecutionCode.EffectPreflightFailed, first.Code);
            Assert.AreEqual(0, poorGateway.TotalMutationCalls);
            Assert.IsFalse(execution.IsCompleted(instance.EventInstanceId));

            // 补齐资源后重试（同一个实例、同一个 Option）
            var richGateway = new EventEffectGatewayFixture(ingredients: 50, food: 100, supply: 20);
            var richWorld = new EventWorldViewSnapshot()
                .AddPerson("p1", inVillage: true, available: true)
                .SetVillageState(100, 50, 100)
                .SetExpeditionState(3, 20, 2, 5);

            var second = EventOptionExecutor.Execute(instance, "opt_retry", catalog, richWorld, worldState,
                new ExpeditionEventSessionState(), execution, richGateway);

            Assert.IsTrue(second.Success, second.Diagnostic + "\n" + second.Trace.ToText());
            Assert.AreEqual(first.OutcomeId, second.OutcomeId, "重试必须仍得到同一个 Outcome（选择已冻结）");
            Assert.AreEqual(70, richGateway.VillageResources["Food"], "100 - 30 = 70");
            Assert.IsTrue(execution.IsCompleted(instance.EventInstanceId), "成功之后才标记完成");
            Assert.AreEqual(1, worldState.History.OccurrenceCount(EventId), "只应记录一次");
        }
    }
}
