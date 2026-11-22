using System.Collections.Generic;
using NUnit.Framework;
using RRPG.Event;
using RRPG.StaticConfig;
using static RRPG.Event.Tests.EventResolverTestKit;
using static RRPG.Event.Tests.EventResolverTestWorld;

namespace RRPG.Event.Tests
{
    /// <summary>
    /// WP-Event-1B：Anti-Hang 搜索上限（§11）。覆盖：R24。
    /// 上限耗尽必须与「无解（NoLegalRoleBinding）」严格区分。
    /// </summary>
    public class EventResolverSearchLimitTests
    {
        private const string VillageTrigger = "VillageRecognitionRaised";

        private static EventWorldViewSnapshot ManyPersonsWorld()
        {
            return new EventWorldViewSnapshot()
                .AddPerson("p1", inVillage: true, available: false)
                .AddPerson("p2", inVillage: true, available: true)
                .AddPerson("p3", inVillage: true, available: false)
                .AddPerson("p4", inVillage: true, available: false)
                .SetVillageState(100, 0, 0)
                .SetExpeditionState(0, 0, 0, 0);
        }

        private static EventStaticCatalog ThreeRoleCatalog()
        {
            return SingleEventCatalog(Evt("evt_limit"), VillageTrigger,
                roles: new List<EventRoleDefinition>
                {
                    Role("role_1", "evt_limit", "VillagePerson"),
                    Role("role_2", "evt_limit", "VillagePerson"),
                    Role("role_3", "evt_limit", "VillagePerson")
                },
                roleConditions: new List<EventRoleConditionDefinition>
                {
                    RoleCondition("rc_3", "role_3", "PersonAvailable")
                });
        }

        [Test]
        public void R24_SearchLimitExhausted_IsDistinctFromNoLegalRoleBinding()
        {
            var result = Resolve(ThreeRoleCatalog(), Request(VillageTrigger, "k_limit", 3),
                world: ManyPersonsWorld(), options: new EventResolverOptions(maxSearchNodes: 1));

            Assert.IsFalse(result.Success);
            Assert.AreEqual(EventResolutionCode.SearchLimitExhausted, result.Code,
                "搜索上限耗尽必须与 NoLegalRoleBinding 区分");
            Assert.AreNotEqual(EventResolutionCode.NoLegalRoleBinding, result.Code);
            StringAssert.Contains("上限", result.Diagnostic);
            Assert.AreEqual(EventRejectStage.SearchLimit, result.Trace.Find("evt_limit").RejectStage);
        }

        [Test]
        public void R24_DefaultOptions_DoNotHitSearchLimit_ForSmallContent()
        {
            var result = Resolve(ThreeRoleCatalog(), Request(VillageTrigger, "k_limit_ok", 3),
                world: ManyPersonsWorld());

            Assert.IsTrue(result.Success, result.Diagnostic);
            Assert.AreEqual(EventResolutionCode.Success, result.Code);
            Assert.Less(result.Trace.TotalSearchNodes, EventResolverOptions.DefaultMaxSearchNodes);
        }

        [Test]
        public void R24_GenerousLimit_ReportsSuccess_RatherThanLimit()
        {
            var result = Resolve(ThreeRoleCatalog(), Request(VillageTrigger, "k_limit_ok", 3),
                world: ManyPersonsWorld(), options: new EventResolverOptions(maxSearchNodes: 1000));

            Assert.IsTrue(result.Success, result.Diagnostic);
            Assert.AreEqual(EventResolutionCode.Success, result.Code);
        }
    }
}
