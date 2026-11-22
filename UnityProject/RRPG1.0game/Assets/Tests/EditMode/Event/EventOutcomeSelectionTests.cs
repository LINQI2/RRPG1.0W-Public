using System.Collections.Generic;
using NUnit.Framework;
using RRPG.Event;
using RRPG.StaticConfig;
using static RRPG.Event.Tests.EventExecutionTestKit;
using static RRPG.Event.Tests.EventResolverTestKit;
using static RRPG.Event.Tests.EventResolverTestWorld;

namespace RRPG.Event.Tests
{
    /// <summary>
    /// WP-Event-1C：Outcome 确定性（§6 / §24）。覆盖：C3。
    /// 核心规则：同一个 EventInstance + 同一个 Option，无论 UI 重开 / 预览 / 重试多少次，都得到同一个 Outcome。
    /// </summary>
    public class EventOutcomeSelectionTests
    {
        private const string EventId = "evt_outcome";

        private static EventStaticCatalog BuildCatalog()
        {
            return Catalog(
                new List<EventDefinition> { Evt(EventId) },
                new List<EventTriggerDefinition> { Trigger("trg_outcome", EventId, VillageTrigger) },
                options: new List<EventOptionDefinition>
                {
                    Option("opt_one", EventId, 1, "Hide"),
                    Option("opt_two", EventId, 2, "Hide")
                },
                outcomes: new List<EventOutcomeDefinition>
                {
                    Outcome("out_a", "opt_one", 70),
                    Outcome("out_b", "opt_one", 30),
                    Outcome("out_solo", "opt_two", 100)
                },
                effects: new List<EventEffectDefinition>
                {
                    Effect("eff_solo", "out_solo", 1, "SetFlag", targetKey: "flag_solo")
                });
        }

        [Test]
        public void C3_Outcome_IsStable_AcrossRepeatedPreviews_AndExecute()
        {
            var catalog = BuildCatalog();
            var instance = ResolveInstance(catalog, seed: 11);
            Assert.IsNotNull(instance);

            var first = EventOptionExecutor.PreviewOutcome(instance, "opt_one", catalog);
            Assert.IsNotNull(first);

            for (int i = 0; i < 5; i++)
            {
                var again = EventOptionExecutor.PreviewOutcome(instance, "opt_one", catalog);
                Assert.AreEqual(first.OutcomeId, again.OutcomeId,
                    "同一个 EventInstance + 同一个 Option 无论预览多少次都必须得到同一个 Outcome");
            }

            var execution = new EventExecutionState();
            var result = EventOptionExecutor.Execute(instance, "opt_one", catalog, StandardWorld(),
                new EventWorldState(), new ExpeditionEventSessionState(), execution, new EventEffectGatewayFixture());

            Assert.IsTrue(result.Success, result.Diagnostic + "\n" + result.Trace.ToText());
            Assert.AreEqual(first.OutcomeId, result.OutcomeId, "预览与正式执行必须是同一个 Outcome");

            // 第二次执行同一个实例（同一个 ExecutionState）→ exactly-once
            var second = EventOptionExecutor.Execute(instance, "opt_one", catalog, StandardWorld(),
                new EventWorldState(), new ExpeditionEventSessionState(), execution, new EventEffectGatewayFixture());
            Assert.AreEqual(EventExecutionCode.InstanceAlreadyResolved, second.Code);
        }

        [Test]
        public void C3_SingleOutcome_GoesThroughTheSameFlow()
        {
            var catalog = BuildCatalog();
            var instance = ResolveInstance(catalog, seed: 3);
            Assert.IsNotNull(instance);

            var preview = EventOptionExecutor.PreviewOutcome(instance, "opt_two", catalog);
            Assert.IsNotNull(preview);
            Assert.AreEqual("out_solo", preview.OutcomeId);

            var worldState = new EventWorldState();
            var result = EventOptionExecutor.Execute(instance, "opt_two", catalog, StandardWorld(), worldState,
                new ExpeditionEventSessionState(), new EventExecutionState(), new EventEffectGatewayFixture());

            Assert.IsTrue(result.Success, result.Diagnostic + "\n" + result.Trace.ToText());
            Assert.AreEqual("out_solo", result.OutcomeId);
            Assert.IsTrue(worldState.IsFlagSet("flag_solo"), "单一 Outcome 也必须真的执行 Effect");
        }
    }
}
