using System.Collections.Generic;
using NUnit.Framework;
using RRPG.Event;
using RRPG.StaticConfig;
using static RRPG.Event.Tests.EventExecutionTestKit;
using static RRPG.Event.Tests.EventResolverTestKit;

namespace RRPG.Event.Tests
{
    /// <summary>
    /// WP-Event-1C：Execution Trace（§23）。覆盖：C20（成功路径 + Option 失败路径）。
    /// </summary>
    public class EventExecutionTraceTests
    {
        internal static EventStaticCatalog SuccessCatalog(string eventId)
        {
            return Catalog(
                new List<EventDefinition> { Evt(eventId) },
                new List<EventTriggerDefinition> { Trigger("trg_" + eventId, eventId, VillageTrigger) },
                options: new List<EventOptionDefinition> { Option("opt_" + eventId, eventId, 1, "Hide") },
                outcomes: new List<EventOutcomeDefinition> { Outcome("out_" + eventId, "opt_" + eventId, 100) },
                effects: new List<EventEffectDefinition>
                {
                    Effect("eff_" + eventId, "out_" + eventId, 10, "VillageResourceDelta", targetKey: "Food",
                        intValue: -30),
                    Effect("eff_flag_" + eventId, "out_" + eventId, 20, "SetFlag", targetKey: "shrine_opened")
                });
        }

        [Test]
        public void C20_SuccessTrace_ShowsAllStages()
        {
            const string EventId = "evt_trace_ok";
            var catalog = SuccessCatalog(EventId);
            var instance = ResolveInstance(catalog);
            Assert.IsNotNull(instance);

            var result = EventOptionExecutor.Execute(instance, "opt_" + EventId, catalog,
                EventResolverTestWorld.StandardWorld(), new EventWorldState(),
                new ExpeditionEventSessionState(), new EventExecutionState(), new EventEffectGatewayFixture());

            Assert.IsTrue(result.Success, result.Diagnostic + "\n" + result.Trace.ToText());

            string text = result.Trace.ToText();
            StringAssert.Contains("OptionCondition = Available", text);
            StringAssert.Contains("Outcome = out_" + EventId, text);
            StringAssert.Contains("Effects = 2", text);
            StringAssert.Contains("VillageResourceDelta Food -30", text);
            StringAssert.Contains("Preflight OK", text);
            StringAssert.Contains("projected", text);
            StringAssert.Contains("SetFlag shrine_opened", text);
            StringAssert.Contains("Commit = Success", text);
            StringAssert.Contains("History = Recorded", text);
            StringAssert.Contains("Instance = Completed", text);

            Assert.AreEqual("Instance", result.Trace.Stage);
            Assert.IsTrue(result.Trace.CommitSucceeded);
            Assert.IsTrue(result.Trace.HistoryRecorded);
            Assert.IsTrue(result.Trace.InstanceCompleted);
        }

        [Test]
        public void C20_Trace_IsStructured_OnOptionFailures()
        {
            const string EventId = "evt_trace_option";
            var catalog = Catalog(
                new List<EventDefinition> { Evt(EventId) },
                new List<EventTriggerDefinition> { Trigger("trg", EventId, VillageTrigger) },
                options: new List<EventOptionDefinition> { Option("opt_hidden", EventId, 1, "Hide") },
                optionConditions: new List<EventOptionConditionDefinition>
                {
                    OptionCondition("oc", "opt_hidden", 1, "VillageResourceAtLeast", null, "Food", 1000)
                },
                outcomes: new List<EventOutcomeDefinition> { Outcome("out", "opt_hidden", 100) });

            var instance = ResolveInstance(catalog);
            Assert.IsNotNull(instance);

            var result = EventOptionExecutor.Execute(instance, "opt_hidden", catalog,
                EventResolverTestWorld.StandardWorld(), new EventWorldState(),
                new ExpeditionEventSessionState(), new EventExecutionState(), new EventEffectGatewayFixture());

            Assert.AreEqual(EventExecutionCode.OptionHidden, result.Code);

            string text = result.Trace.ToText();
            StringAssert.Contains("OptionCondition = Hidden", text);
            StringAssert.Contains("Result = OptionHidden", text);
            Assert.AreEqual("OptionCondition", result.Trace.Stage);
            Assert.IsFalse(result.Trace.CommitAttempted);
        }
    }
}
