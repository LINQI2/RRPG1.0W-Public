using System.Collections.Generic;
using NUnit.Framework;
using RRPG.Event;
using RRPG.StaticConfig;
using static RRPG.Event.Tests.EventResolverTestKit;
using static RRPG.Event.Tests.EventResolverTestWorld;

namespace RRPG.Event.Tests
{
    /// <summary>
    /// WP-Event-1B：Person / Village / Expedition 条件（§13）。覆盖：R19、R20、R21。
    /// StandardWorld：p1 在村可用；p3 在远征可用；p4 在村但不可用；
    /// Ingredients 50 / Food 40；Depth 3 / Supply 20 / PartySize 2。
    /// </summary>
    public class EventResolverConditionWorldStateTests
    {
        private const string VillageTrigger = "VillageRecognitionRaised";

        private static EventStaticCatalog PersonConditionCatalog(string conditionType)
        {
            return SingleEventCatalog(Evt("evt_person"), VillageTrigger,
                roles: new List<EventRoleDefinition>
                {
                    Role("role_who", "evt_person", "ContextPerson", "Who")
                },
                conditions: new List<EventConditionDefinition>
                {
                    Condition("c", "evt_person", 1, conditionType, subjectRoleId: "role_who")
                });
        }

        private static EventResolutionResult ResolveForPerson(string conditionType, string personId)
        {
            return Resolve(PersonConditionCatalog(conditionType),
                Request(VillageTrigger, "k_person", 1, ContextWithPerson("Who", personId)));
        }

        // ---------------- R19：Person 条件（经 SubjectRoleId） ----------------

        [Test]
        public void R19_PersonExists()
        {
            Assert.IsTrue(ResolveForPerson("PersonExists", "p1").Success);
            Assert.IsFalse(ResolveForPerson("PersonExists", "p_ghost").Success,
                "上下文人物不存在时连 Role 绑定都失败 → 事件不合法");
        }

        [Test]
        public void R19_PersonInVillage_And_PersonOnExpedition()
        {
            Assert.IsTrue(ResolveForPerson("PersonInVillage", "p1").Success, "p1 在村");
            Assert.IsFalse(ResolveForPerson("PersonInVillage", "p3").Success, "p3 在远征，不在村");

            Assert.IsTrue(ResolveForPerson("PersonOnExpedition", "p3").Success, "p3 在远征");
            Assert.IsFalse(ResolveForPerson("PersonOnExpedition", "p1").Success, "p1 不在远征");
        }

        [Test]
        public void R19_PersonAvailable()
        {
            Assert.IsTrue(ResolveForPerson("PersonAvailable", "p1").Success, "p1 可用");
            Assert.IsFalse(ResolveForPerson("PersonAvailable", "p4").Success, "p4 在村但不可用");
        }

        // ---------------- R20：Village Resource ----------------

        [Test]
        public void R20_VillageResources_IngredientsAndFood_WithBoundaries()
        {
            Assert.IsTrue(Resolve(SingleEventCatalog(Evt("evt_res"), VillageTrigger,
                    conditions: new List<EventConditionDefinition>
                    {
                        Condition("c", "evt_res", 1, "VillageResourceAtLeast", targetKey: "Ingredients", intValue: 50)
                    }), Request(VillageTrigger, "k_res", 1)).Success, "Ingredients 50 >= 50");

            Assert.IsFalse(Resolve(SingleEventCatalog(Evt("evt_res"), VillageTrigger,
                    conditions: new List<EventConditionDefinition>
                    {
                        Condition("c", "evt_res", 1, "VillageResourceAtLeast", targetKey: "Ingredients", intValue: 51)
                    }), Request(VillageTrigger, "k_res", 1)).Success, "Ingredients 50 < 51");

            Assert.IsTrue(Resolve(SingleEventCatalog(Evt("evt_food"), VillageTrigger,
                    conditions: new List<EventConditionDefinition>
                    {
                        Condition("c", "evt_food", 1, "VillageResourceAtMost", targetKey: "Food", intValue: 40)
                    }), Request(VillageTrigger, "k_food", 1)).Success, "Food 40 <= 40");
        }

        // ---------------- R21：Expedition ----------------

        [Test]
        public void R21_ExpeditionDepth_Supply_PartySize_WithBoundaries()
        {
            Assert.IsTrue(Resolve(SingleEventCatalog(Evt("evt_d"), VillageTrigger,
                    conditions: new List<EventConditionDefinition>
                    {
                        Condition("c", "evt_d", 1, "ExpeditionDepthAtLeast", intValue: 3)
                    }), Request(VillageTrigger, "k_d", 1)).Success, "Depth 3 >= 3");

            Assert.IsFalse(Resolve(SingleEventCatalog(Evt("evt_d"), VillageTrigger,
                    conditions: new List<EventConditionDefinition>
                    {
                        Condition("c", "evt_d", 1, "ExpeditionDepthAtLeast", intValue: 4)
                    }), Request(VillageTrigger, "k_d", 1)).Success, "Depth 3 < 4");

            Assert.IsTrue(Resolve(SingleEventCatalog(Evt("evt_s"), VillageTrigger,
                    conditions: new List<EventConditionDefinition>
                    {
                        Condition("c", "evt_s", 1, "SupplyAtLeast", intValue: 20)
                    }), Request(VillageTrigger, "k_s", 1)).Success, "Supply 20 >= 20");

            Assert.IsFalse(Resolve(SingleEventCatalog(Evt("evt_p"), VillageTrigger,
                    conditions: new List<EventConditionDefinition>
                    {
                        Condition("c", "evt_p", 1, "PartySizeAtLeast", intValue: 3)
                    }), Request(VillageTrigger, "k_p", 1)).Success, "PartySize 2 < 3");

            Assert.IsTrue(Resolve(SingleEventCatalog(Evt("evt_p2"), VillageTrigger,
                    conditions: new List<EventConditionDefinition>
                    {
                        Condition("c2", "evt_p2", 1, "PartySizeAtMost", intValue: 2)
                    }), Request(VillageTrigger, "k_p2", 1)).Success, "PartySize 2 <= 2");
        }
    }
}
