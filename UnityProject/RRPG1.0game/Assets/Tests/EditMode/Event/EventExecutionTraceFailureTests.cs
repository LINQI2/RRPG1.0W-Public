using NUnit.Framework;
using RRPG.Event;
using static RRPG.Event.Tests.EventExecutionTestKit;
using static RRPG.Event.Tests.EventResolverTestKit;
using static RRPG.Event.Tests.EventExecutionTraceTests;

namespace RRPG.Event.Tests
{
    /// <summary>
    /// WP-Event-1C：Preflight 失败的 Trace 必须给出 expected / actual projected（§23）。覆盖：C20（失败路径）。
    /// </summary>
    public class EventExecutionTraceFailureTests
    {
        [Test]
        public void C20_PreflightFailureTrace_ShowsExpectedAndActual()
        {
            const string EventId = "evt_trace_fail";
            var catalog = SuccessCatalog(EventId);
            var instance = ResolveInstance(catalog);
            Assert.IsNotNull(instance);

            // Food=20，第一条效果就要扣 30 → Preflight 失败（零 mutation）
            var poorWorld = new EventWorldViewSnapshot()
                .AddPerson("p1", inVillage: true, available: true)
                .SetVillageState(100, 10, 20)
                .SetExpeditionState(3, 20, 2, 5);

            var gateway = new EventEffectGatewayFixture(ingredients: 10, food: 20, supply: 20);
            var worldState = new EventWorldState();

            var result = EventOptionExecutor.Execute(instance, "opt_" + EventId, catalog, poorWorld, worldState,
                new ExpeditionEventSessionState(), new EventExecutionState(), gateway);

            Assert.AreEqual(EventExecutionCode.EffectPreflightFailed, result.Code);

            string text = result.Trace.ToText();
            StringAssert.Contains("Preflight FAIL", text);
            StringAssert.Contains("actual projected", text);
            StringAssert.Contains("Result = EffectPreflightFailed", text);

            Assert.AreEqual("Preflight", result.Trace.Stage);
            Assert.IsFalse(result.Trace.CommitAttempted, "Preflight 失败不得进入 Commit");
            Assert.IsFalse(result.Trace.HistoryRecorded);
            Assert.IsFalse(result.Trace.InstanceCompleted);
            Assert.AreEqual(0, gateway.TotalMutationCalls);
            Assert.IsFalse(worldState.IsFlagSet("shrine_opened"));
        }
    }
}
