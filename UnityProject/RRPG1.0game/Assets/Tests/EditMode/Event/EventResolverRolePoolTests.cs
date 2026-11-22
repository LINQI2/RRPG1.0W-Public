using System.Collections.Generic;
using NUnit.Framework;
using RRPG.Event;
using RRPG.StaticConfig;
using static RRPG.Event.Tests.EventResolverTestKit;
using static RRPG.Event.Tests.EventResolverTestWorld;

namespace RRPG.Event.Tests
{
    /// <summary>
    /// WP-Event-1B：候选池型 Role（§8）。覆盖：R10（VillagePerson）、R11（ExpeditionPartyMember）。
    /// </summary>
    public class EventResolverRolePoolTests
    {
        private const string VillageTrigger = "VillageRecognitionRaised";
        private const string ExpeditionTrigger = "ExpeditionEventNodeReached";

        [Test]
        public void R10_VillagePerson_ComesFromVillagePool_AndHonoursRoleConditions()
        {
            var freeCatalog = SingleEventCatalog(Evt("evt_village"), VillageTrigger,
                roles: new List<EventRoleDefinition> { Role("role_v", "evt_village", "VillagePerson") });

            var free = Resolve(freeCatalog, Request(VillageTrigger, "k_village", 5));
            Assert.IsTrue(free.Success, free.Diagnostic);

            string anyPerson;
            Assert.IsTrue(free.Instance.TryGetBoundPerson("role_v", out anyPerson));
            Assert.IsTrue(anyPerson == "p1" || anyPerson == "p2" || anyPerson == "p4",
                "只能从村庄候选池中选择：" + anyPerson);

            // 加角色条件 PersonAvailable → p4（不可用）必须被排除
            var restrictedCatalog = SingleEventCatalog(Evt("evt_village"), VillageTrigger,
                roles: new List<EventRoleDefinition> { Role("role_v", "evt_village", "VillagePerson") },
                roleConditions: new List<EventRoleConditionDefinition>
                {
                    RoleCondition("rc_available", "role_v", "PersonAvailable")
                });

            for (int seed = 0; seed < 30; seed++)
            {
                var restricted = Resolve(restrictedCatalog, Request(VillageTrigger, "k_village", seed));
                Assert.IsTrue(restricted.Success, restricted.Diagnostic);

                string personId;
                Assert.IsTrue(restricted.Instance.TryGetBoundPerson("role_v", out personId));
                Assert.AreNotEqual("p4", personId, "PersonAvailable 必须排除不可用人物");
            }
        }

        [Test]
        public void R11_ExpeditionPartyMember_ComesFromPartyPoolOnly()
        {
            var catalog = SingleEventCatalog(Evt("evt_party"), ExpeditionTrigger,
                roles: new List<EventRoleDefinition> { Role("role_scout", "evt_party", "ExpeditionPartyMember") });

            for (int seed = 0; seed < 20; seed++)
            {
                var result = Resolve(catalog, Request(ExpeditionTrigger, "k_party", seed));
                Assert.IsTrue(result.Success, result.Diagnostic);

                string personId;
                Assert.IsTrue(result.Instance.TryGetBoundPerson("role_scout", out personId));
                Assert.AreEqual("p3", personId, "远征队伍里只有 p3，绝不能从村庄池取人");
            }
        }

        [Test]
        public void R11_EmptyParty_MakesEventInvalid()
        {
            var catalog = SingleEventCatalog(Evt("evt_party"), ExpeditionTrigger,
                roles: new List<EventRoleDefinition> { Role("role_scout", "evt_party", "ExpeditionPartyMember") });

            var emptyPartyWorld = new EventWorldViewSnapshot()
                .AddPerson("p1", inVillage: true, available: true)
                .SetVillageState(100, 0, 0)
                .SetExpeditionState(0, 0, 0, 0);

            var result = Resolve(catalog, Request(ExpeditionTrigger, "k_party"), world: emptyPartyWorld);

            Assert.IsFalse(result.Success);
            Assert.AreEqual(EventResolutionCode.NoLegalRoleBinding, result.Code);
            StringAssert.Contains("远征队伍候选池为空", result.Trace.Find("evt_party").RejectReason);
        }
    }
}
