using System.Collections.Generic;
using NUnit.Framework;
using RRPG.Event;
using RRPG.StaticConfig;
using static RRPG.Event.Tests.EventExecutionTestKit;
using static RRPG.Event.Tests.EventResolverTestKit;
using static RRPG.Event.Tests.EventExecutionHistoryTests;

namespace RRPG.Event.Tests
{
    /// <summary>
    /// WP-Event-1C：Explicitly-once 语义（§17 / §18）。覆盖：C15、C16。
    /// Completed 以 EventInstanceId 为边界，不以 OptionId 为边界。
    /// </summary>
    public class EventExecutionExactlyOnceTests
    {
        [Test]
        public void C15_SecondExecution_IsAlreadyResolved_WithZeroMutation()
        {
            var catalog = VillageCatalog();
            var worldState = new EventWorldState();
            var session = new ExpeditionEventSessionState("session_a");
            var execution = new EventExecutionState();
            var gateway = new EventEffectGatewayFixture();

            var instance = ResolveInstance(catalog);
            Assert.IsNotNull(instance);

            var first = EventOptionExecutor.Execute(instance, "opt_v", catalog,
                EventResolverTestWorld.StandardWorld(), worldState, session, execution, gateway);
            Assert.IsTrue(first.Success, first.Diagnostic);

            int flagCount = worldState.Flags.Count;
            int historyCount = worldState.History.OccurrenceCount(VillageEvent);
            int mutationCalls = gateway.TotalMutationCalls;

            var second = EventOptionExecutor.Execute(instance, "opt_v", catalog,
                EventResolverTestWorld.StandardWorld(), worldState, session, execution, gateway);

            Assert.AreEqual(EventExecutionCode.InstanceAlreadyResolved, second.Code);
            Assert.IsFalse(second.Success);
            Assert.AreEqual(flagCount, worldState.Flags.Count, "第二次不得再写 Flag");
            Assert.AreEqual(historyCount, worldState.History.OccurrenceCount(VillageEvent), "第二次不得再写 History");
            Assert.AreEqual(mutationCalls, gateway.TotalMutationCalls, "第二次不得再碰 Gateway");
            Assert.AreEqual(1, execution.Completions.Count);
            Assert.IsTrue(execution.IsCompleted(instance.EventInstanceId));
        }

        [Test]
        public void C16_DifferentOptionAfterCompletion_IsAlsoAlreadyResolved()
        {
            var catalog = Catalog(
                new List<EventDefinition> { Evt("evt_two_opts") },
                new List<EventTriggerDefinition> { Trigger("trg_two", "evt_two_opts", VillageTrigger) },
                options: new List<EventOptionDefinition>
                {
                    Option("opt_a", "evt_two_opts", 1, "Hide"),
                    Option("opt_b", "evt_two_opts", 2, "Hide")
                },
                outcomes: new List<EventOutcomeDefinition>
                {
                    Outcome("out_a", "opt_a", 100),
                    Outcome("out_b", "opt_b", 100)
                },
                effects: new List<EventEffectDefinition>
                {
                    Effect("eff_a", "out_a", 1, "SetFlag", targetKey: "flag_a"),
                    Effect("eff_b", "out_b", 1, "SetFlag", targetKey: "flag_b")
                });

            var worldState = new EventWorldState();
            var execution = new EventExecutionState();
            var gateway = new EventEffectGatewayFixture();

            var instance = ResolveInstance(catalog);
            Assert.IsNotNull(instance);

            var first = EventOptionExecutor.Execute(instance, "opt_a", catalog,
                EventResolverTestWorld.StandardWorld(), worldState, new ExpeditionEventSessionState(), execution,
                gateway);
            Assert.IsTrue(first.Success, first.Diagnostic);
            Assert.IsTrue(worldState.IsFlagSet("flag_a"));

            var second = EventOptionExecutor.Execute(instance, "opt_b", catalog,
                EventResolverTestWorld.StandardWorld(), worldState, new ExpeditionEventSessionState(), execution,
                gateway);

            Assert.AreEqual(EventExecutionCode.InstanceAlreadyResolved, second.Code,
                "同一个实例最终只能成功执行一个 Option");
            Assert.IsFalse(worldState.IsFlagSet("flag_b"), "第二个 Option 的效果不得执行");
            Assert.AreEqual(1, worldState.History.OccurrenceCount("evt_two_opts"));
        }

        [Test]
        public void C16_FailedExecution_DoesNotBlockRetry_ButSuccessDoes()
        {
            // 失败不标 Completed（§17）；一旦成功，后续全部 AlreadyResolved。
            var catalog = Catalog(
                new List<EventDefinition> { Evt("evt_retry2") },
                new List<EventTriggerDefinition> { Trigger("trg_retry2", "evt_retry2", VillageTrigger) },
                options: new List<EventOptionDefinition> { Option("opt_r", "evt_retry2", 1, "Disable") },
                optionConditions: new List<EventOptionConditionDefinition>
                {
                    OptionCondition("oc_r", "opt_r", 1, "VillageResourceAtLeast", null, "Food", 1000)
                },
                outcomes: new List<EventOutcomeDefinition> { Outcome("out_r", "opt_r", 100) });

            var worldState = new EventWorldState();
            var execution = new EventExecutionState();
            var gateway = new EventEffectGatewayFixture();
            var instance = ResolveInstance(catalog);
            Assert.IsNotNull(instance);

            // Food=40 < 1000 → Disabled（条件不成立 + Disable）
            var blocked = EventOptionExecutor.Execute(instance, "opt_r", catalog,
                EventResolverTestWorld.StandardWorld(), worldState, new ExpeditionEventSessionState(), execution,
                gateway);

            Assert.AreEqual(EventExecutionCode.OptionDisabled, blocked.Code);
            Assert.IsFalse(execution.IsCompleted(instance.EventInstanceId), "不可选不算已经完成");
        }
    }
}
