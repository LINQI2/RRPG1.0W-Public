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
    /// WP-Event-1C：Option 可用性（§4）与动态条件（§3）。覆盖：C1、C2。
    /// </summary>
    public class EventOptionAvailabilityTests
    {
        private const string EventId = "evt_opt";

        private static EventStaticCatalog BuildCatalog()
        {
            return Catalog(
                new List<EventDefinition> { Evt(EventId) },
                new List<EventTriggerDefinition> { Trigger("trg_opt", EventId, VillageTrigger) },
                options: new List<EventOptionDefinition>
                {
                    Option("opt_free", EventId, 1, "Hide"),
                    Option("opt_hide", EventId, 2, "Hide"),
                    Option("opt_disable", EventId, 3, "Disable")
                },
                optionConditions: new List<EventOptionConditionDefinition>
                {
                    OptionCondition("oc_hide", "opt_hide", 1, "VillageResourceAtLeast", null, "Food", 1000),
                    OptionCondition("oc_disable", "opt_disable", 1, "VillageResourceAtLeast", null, "Food", 1000)
                },
                outcomes: new List<EventOutcomeDefinition>
                {
                    Outcome("out_free", "opt_free"),
                    Outcome("out_hide", "opt_hide"),
                    Outcome("out_disable", "opt_disable")
                });
        }

        private static EventWorldViewSnapshot WorldWithFood(int food)
        {
            return new EventWorldViewSnapshot()
                .AddPerson("p1", inVillage: true, available: true)
                .SetVillageState(100, 10, food)
                .SetExpeditionState(3, 20, 2, 5);
        }

        // ---------------- C1：Available / Hidden / Disabled ----------------

        [Test]
        public void C1_Availability_Available_Hidden_Disabled()
        {
            var catalog = BuildCatalog();
            var instance = ResolveInstance(catalog);
            Assert.IsNotNull(instance, "应先解析出实例");

            var world = StandardWorld();     // Food = 40
            var worldState = new EventWorldState();

            var available = EventOptionEvaluator.Evaluate(instance, "opt_free", catalog, world, worldState);
            Assert.AreEqual(EventOptionAvailability.Available, available.Availability);
            Assert.IsTrue(available.IsSelectable);

            var hidden = EventOptionEvaluator.Evaluate(instance, "opt_hide", catalog, world, worldState);
            Assert.AreEqual(EventOptionAvailability.Hidden, hidden.Availability);
            Assert.IsFalse(hidden.IsSelectable);

            var disabled = EventOptionEvaluator.Evaluate(instance, "opt_disable", catalog, world, worldState);
            Assert.AreEqual(EventOptionAvailability.Disabled, disabled.Availability);
            Assert.IsFalse(disabled.IsSelectable);
        }

        [Test]
        public void C1_Availability_QueryIsReadOnly_AndUnknownOptionIsInvalid()
        {
            var catalog = BuildCatalog();
            var instance = ResolveInstance(catalog);
            var world = StandardWorld();
            var worldState = new EventWorldState();

            EventOptionEvaluator.Evaluate(instance, "opt_free", catalog, world, worldState);
            EventOptionEvaluator.Evaluate(instance, "opt_disable", catalog, world, worldState);

            Assert.IsEmpty(worldState.Flags, "查询可用性不得写 Flag");
            Assert.AreEqual(0, worldState.History.OccurrenceCount(EventId), "查询可用性不得写 History");

            var unknown = EventOptionEvaluator.Evaluate(instance, "opt_missing", catalog, world, worldState);
            Assert.AreEqual(EventOptionAvailability.Invalid, unknown.Availability);
            StringAssert.Contains("opt_missing", unknown.Diagnostic);
        }

        // ---------------- C2：同一实例，世界变化 → 可用性变化 ----------------

        [Test]
        public void C2_DynamicOptionCondition_SameInstance_ChangesWithCurrentWorld()
        {
            var catalog = BuildCatalog();
            var instance = ResolveInstance(catalog);
            var worldState = new EventWorldState();

            var before = EventOptionEvaluator.Evaluate(instance, "opt_disable", catalog, WorldWithFood(20), worldState);
            Assert.AreEqual(EventOptionAvailability.Disabled, before.Availability);

            var after = EventOptionEvaluator.Evaluate(instance, "opt_disable", catalog, WorldWithFood(2000), worldState);
            Assert.AreEqual(EventOptionAvailability.Available, after.Availability,
                "Role / Context 冻结，但选项能力必须随「点击此刻」的世界变化");

            // 正好等于阈值 → 包含相等值（>= 语义）
            Assert.AreEqual(EventOptionAvailability.Available,
                EventOptionEvaluator.Evaluate(instance, "opt_disable", catalog, WorldWithFood(1000), worldState)
                    .Availability);
        }
    }
}
