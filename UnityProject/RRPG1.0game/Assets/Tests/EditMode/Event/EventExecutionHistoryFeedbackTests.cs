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
    /// WP-Event-1C：执行后的历史必须反过来影响 Resolver。覆盖：C18、C19。
    /// </summary>
    public class EventExecutionHistoryFeedbackTests
    {
        [Test]
        public void C18_AfterCompletion_OncePerWorld_IsExcludedByResolver()
        {
            var catalog = VillageCatalog(oncePerWorld: true);
            var worldState = new EventWorldState();

            var first = ResolveInstance(catalog, worldState: worldState);
            Assert.IsNotNull(first);

            var executed = EventOptionExecutor.Execute(first, "opt_v", catalog,
                EventResolverTestWorld.StandardWorld(), worldState, new ExpeditionEventSessionState(),
                new EventExecutionState(), new EventEffectGatewayFixture());
            Assert.IsTrue(executed.Success, executed.Diagnostic);

            var second = EventResolver.Resolve(
                new EventResolutionRequest(VillageTrigger, "key_second", 2), catalog,
                EventResolverTestWorld.StandardWorld(), worldState, new ExpeditionEventSessionState());

            Assert.IsFalse(second.Success);
            Assert.AreEqual(EventResolutionCode.NoCandidateEvent, second.Code);
            Assert.AreEqual(EventRejectStage.Repeat, second.Trace.Find(VillageEvent).RejectStage,
                "Repeat 判定必须读到 1C 写入的世界历史");
        }

        [Test]
        public void C18_AfterCompletion_EventNotSeen_ConditionFlips()
        {
            var catalog = Catalog(
                new List<EventDefinition> { Evt("evt_source"), Evt("evt_gate") },
                new List<EventTriggerDefinition>
                {
                    Trigger("trg_source", "evt_source", VillageTrigger),
                    Trigger("trg_gate", "evt_gate", ExpeditionTrigger)
                },
                conditions: new List<EventConditionDefinition>
                {
                    Condition("cond_gate", "evt_gate", 1, "EventNotSeen", targetKey: "evt_source")
                },
                options: new List<EventOptionDefinition>
                {
                    Option("opt_source", "evt_source", 1, "Hide"),
                    Option("opt_gate", "evt_gate", 1, "Hide")
                },
                outcomes: new List<EventOutcomeDefinition>
                {
                    Outcome("out_source", "opt_source", 100),
                    Outcome("out_gate", "opt_gate", 100)
                });

            var worldState = new EventWorldState();

            var before = EventResolver.Resolve(
                new EventResolutionRequest(ExpeditionTrigger, "k_gate_before", 1), catalog,
                EventResolverTestWorld.StandardWorld(), worldState, new ExpeditionEventSessionState());
            Assert.IsTrue(before.Success, "evt_source 未发生时 evt_gate 应当合法");

            var source = ResolveInstance(catalog, worldState: worldState);
            Assert.IsNotNull(source);
            Assert.AreEqual("evt_source", source.EventId);

            var executed = EventOptionExecutor.Execute(source, "opt_source", catalog,
                EventResolverTestWorld.StandardWorld(), worldState, new ExpeditionEventSessionState(),
                new EventExecutionState(), new EventEffectGatewayFixture());
            Assert.IsTrue(executed.Success, executed.Diagnostic);

            var after = EventResolver.Resolve(
                new EventResolutionRequest(ExpeditionTrigger, "k_gate_after", 1), catalog,
                EventResolverTestWorld.StandardWorld(), worldState, new ExpeditionEventSessionState());

            Assert.IsFalse(after.Success);
            Assert.AreEqual(EventRejectStage.Condition, after.Trace.Find("evt_gate").RejectStage,
                "EventNotSeen 必须读到 1C 写入的世界历史");
        }

        [Test]
        public void C19_OncePerExpedition_ExcludedInSameSession_AllowedInNewSession()
        {
            var catalog = ExpeditionCatalog(oncePerExpedition: true);
            var worldState = new EventWorldState();
            var session = new ExpeditionEventSessionState("session_a");

            var instance = ResolveInstance(catalog, triggerType: ExpeditionTrigger, triggerKey: "k_exp",
                worldState: worldState, session: session);
            Assert.IsNotNull(instance);

            var executed = EventOptionExecutor.Execute(instance, "opt_e", catalog,
                EventResolverTestWorld.StandardWorld(), worldState, session, new EventExecutionState(),
                new EventEffectGatewayFixture());
            Assert.IsTrue(executed.Success, executed.Diagnostic);

            var sameSession = EventResolver.Resolve(
                new EventResolutionRequest(ExpeditionTrigger, "k_exp_again", 2), catalog,
                EventResolverTestWorld.StandardWorld(), worldState, session);
            Assert.IsFalse(sameSession.Success, "本会话已经发生过 → OncePerExpedition 必须排除");
            Assert.AreEqual(EventRejectStage.Repeat, sameSession.Trace.Find(ExpeditionEvent).RejectStage);

            var newSession = EventResolver.Resolve(
                new EventResolutionRequest(ExpeditionTrigger, "k_exp_new", 2), catalog,
                EventResolverTestWorld.StandardWorld(), worldState, new ExpeditionEventSessionState("session_b"));
            Assert.IsTrue(newSession.Success, "新的一次远征可以再次出现（会话历史不共享）");
        }
    }
}
