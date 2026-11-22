using System.Collections.Generic;
using NUnit.Framework;
using RRPG.Event;
using RRPG.StaticConfig;
using static RRPG.Event.Tests.EventExecutionTestKit;
using static RRPG.Event.Tests.EventResolverTestKit;

namespace RRPG.Event.Tests
{
    /// <summary>
    /// WP-Event-1C：History 写入（§14 / §15）。覆盖：C8、C9、C10。
    /// </summary>
    public class EventExecutionHistoryTests
    {
        internal const string VillageEvent = "evt_village_hist";
        internal const string ExpeditionEvent = "evt_exp_hist";

        internal static EventStaticCatalog VillageCatalog(bool oncePerWorld = false)
        {
            return Catalog(
                new List<EventDefinition>
                {
                    Evt(VillageEvent, repeatPolicy: oncePerWorld ? "OncePerWorld" : "Repeatable")
                },
                new List<EventTriggerDefinition> { Trigger("trg_v", VillageEvent, VillageTrigger) },
                options: new List<EventOptionDefinition> { Option("opt_v", VillageEvent, 1, "Hide") },
                outcomes: new List<EventOutcomeDefinition> { Outcome("out_v", "opt_v", 100) },
                effects: new List<EventEffectDefinition>
                {
                    Effect("eff_v", "out_v", 1, "SetFlag", targetKey: "flag_v")
                });
        }

        internal static EventStaticCatalog ExpeditionCatalog(bool oncePerExpedition = true)
        {
            return Catalog(
                new List<EventDefinition>
                {
                    Evt(ExpeditionEvent, repeatPolicy: oncePerExpedition ? "OncePerExpedition" : "Repeatable")
                },
                new List<EventTriggerDefinition> { Trigger("trg_e", ExpeditionEvent, ExpeditionTrigger) },
                options: new List<EventOptionDefinition> { Option("opt_e", ExpeditionEvent, 1, "Hide") },
                outcomes: new List<EventOutcomeDefinition> { Outcome("out_e", "opt_e", 100) });
        }

        [Test]
        public void C8_SuccessfulExecution_RecordsWorldHistory()
        {
            var catalog = VillageCatalog();
            var worldState = new EventWorldState();
            var instance = ResolveInstance(catalog);
            Assert.IsNotNull(instance);

            var result = EventOptionExecutor.Execute(instance, "opt_v", catalog,
                EventResolverTestWorld.StandardWorld(), worldState, new ExpeditionEventSessionState(),
                new EventExecutionState(), new EventEffectGatewayFixture());

            Assert.IsTrue(result.Success, result.Diagnostic + "\n" + result.Trace.ToText());
            Assert.AreEqual(1, worldState.History.OccurrenceCount(VillageEvent));

            int lastDay;
            Assert.IsTrue(worldState.History.TryGetLastVillageDayOrdinal(VillageEvent, out lastDay));
            Assert.AreEqual(100, lastDay, "应用当前 World View 的村庄日序（不 invent 时间）");
            Assert.AreEqual(instance.EventInstanceId, worldState.History.Records[0].LastEventInstanceId);
        }

        [Test]
        public void C9_ExpeditionEvent_WritesWorldAndSessionHistory()
        {
            var catalog = ExpeditionCatalog();
            var worldState = new EventWorldState();
            var session = new ExpeditionEventSessionState("session_a");

            var instance = ResolveInstance(catalog, triggerType: ExpeditionTrigger, triggerKey: "k_exp");
            Assert.IsNotNull(instance);

            var result = EventOptionExecutor.Execute(instance, "opt_e", catalog,
                EventResolverTestWorld.StandardWorld(), worldState, session, new EventExecutionState(),
                new EventEffectGatewayFixture());

            Assert.IsTrue(result.Success, result.Diagnostic + "\n" + result.Trace.ToText());
            Assert.AreEqual(1, worldState.History.OccurrenceCount(ExpeditionEvent), "Expedition 事件也计入世界历史");
            Assert.AreEqual(1, session.OccurrenceCount(ExpeditionEvent), "Expedition 事件必须另写会话历史");

            int lastNode;
            Assert.IsTrue(session.TryGetLastNodeOrdinal(ExpeditionEvent, out lastNode));
            Assert.AreEqual(5, lastNode, "Session 记录当前远征节点序");
        }

        [Test]
        public void C10_VillageEvent_WritesWorldHistoryOnly()
        {
            var catalog = VillageCatalog();
            var worldState = new EventWorldState();
            var session = new ExpeditionEventSessionState("session_a");
            var instance = ResolveInstance(catalog);
            Assert.IsNotNull(instance);

            var result = EventOptionExecutor.Execute(instance, "opt_v", catalog,
                EventResolverTestWorld.StandardWorld(), worldState, session, new EventExecutionState(),
                new EventEffectGatewayFixture());

            Assert.IsTrue(result.Success, result.Diagnostic);
            Assert.AreEqual(1, worldState.History.OccurrenceCount(VillageEvent));
            Assert.AreEqual(0, session.OccurrenceCount(VillageEvent), "Village 事件不得写 Session History");
        }
    }
}
