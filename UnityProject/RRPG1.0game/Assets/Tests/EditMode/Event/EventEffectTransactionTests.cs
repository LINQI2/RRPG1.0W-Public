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
    /// WP-Event-1C：Effect Transaction 基础（§8 / §10 / §12）。覆盖：C5、C6、C7。
    /// </summary>
    public class EventEffectTransactionTests
    {
        internal const string EventId = "evt_effect";

        internal static EventStaticCatalog BuildCatalog(string optionId, string unavailableMode,
            params EventEffectDefinition[] effects)
        {
            return Catalog(
                new List<EventDefinition> { Evt(EventId) },
                new List<EventTriggerDefinition> { Trigger("trg_effect", EventId, VillageTrigger) },
                roles: new List<EventRoleDefinition> { Role("role_who", EventId, "VillagePerson") },
                options: new List<EventOptionDefinition> { Option(optionId, EventId, 1, unavailableMode) },
                outcomes: new List<EventOutcomeDefinition> { Outcome("out_" + optionId, optionId, 100) },
                effects: new List<EventEffectDefinition>(effects));
        }

        internal static EventOptionExecutionResult Execute(EventStaticCatalog catalog, string optionId,
            EventWorldState worldState, EventExecutionState execution, EventEffectGatewayFixture gateway,
            EventWorldViewSnapshot world = null)
        {
            var instance = ResolveInstance(catalog, world: world);
            Assert.IsNotNull(instance, "应先解析出实例");

            return EventOptionExecutor.Execute(instance, optionId, catalog, world ?? StandardWorld(), worldState,
                new ExpeditionEventSessionState(), execution, gateway);
        }

        [Test]
        public void C5_OutcomeWithoutEffects_Succeeds_AndStillRecords()
        {
            var worldState = new EventWorldState();
            var result = Execute(BuildCatalog("opt_none", "Hide"), "opt_none", worldState,
                new EventExecutionState(), new EventEffectGatewayFixture());

            Assert.IsTrue(result.Success, result.Diagnostic + "\n" + result.Trace.ToText());
            Assert.AreEqual(0, result.AppliedEffectCount);
            Assert.IsFalse(result.HasTerminalEffect);
            Assert.AreEqual(1, worldState.History.OccurrenceCount(EventId), "无 Effect 也必须记录 History");
        }

        [Test]
        public void C6_SetFlag_IsApplied_AtCommit_AndIsIdempotent()
        {
            var catalog = BuildCatalog("opt_set", "Hide",
                Effect("eff_set", "out_opt_set", 1, "SetFlag", targetKey: "shrine_opened"));

            var worldState = new EventWorldState();
            worldState.SetFlag("shrine_opened");   // 已存在 → 幂等成功

            var result = Execute(catalog, "opt_set", worldState, new EventExecutionState(),
                new EventEffectGatewayFixture());

            Assert.IsTrue(result.Success, result.Diagnostic);
            Assert.IsTrue(worldState.IsFlagSet("shrine_opened"));
            Assert.AreEqual(1, result.AppliedEffectCount);
        }

        [Test]
        public void C7_ClearFlag_IsIdempotent()
        {
            var catalog = BuildCatalog("opt_clear", "Hide",
                Effect("eff_clear", "out_opt_clear", 1, "ClearFlag", targetKey: "shrine_opened"));

            var worldState = new EventWorldState();
            worldState.SetFlag("shrine_opened");

            var result = Execute(catalog, "opt_clear", worldState, new EventExecutionState(),
                new EventEffectGatewayFixture());

            Assert.IsTrue(result.Success, result.Diagnostic);
            Assert.IsFalse(worldState.IsFlagSet("shrine_opened"));

            // 清理不存在的 Flag 也必须是幂等成功
            var other = BuildCatalog("opt_clear2", "Hide",
                Effect("eff_clear2", "out_opt_clear2", 1, "ClearFlag", targetKey: "never_set"));

            Assert.IsTrue(Execute(other, "opt_clear2", new EventWorldState(), new EventExecutionState(),
                new EventEffectGatewayFixture()).Success);
        }
    }
}
