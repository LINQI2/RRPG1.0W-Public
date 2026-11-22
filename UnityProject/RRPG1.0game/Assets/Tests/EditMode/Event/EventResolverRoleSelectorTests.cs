using System.Collections.Generic;
using NUnit.Framework;
using RRPG.Event;
using RRPG.StaticConfig;
using static RRPG.Event.Tests.EventResolverTestKit;
using static RRPG.Event.Tests.EventResolverTestWorld;

namespace RRPG.Event.Tests
{
    /// <summary>
    /// WP-Event-1B：Role SelectorType 求解（§8）。覆盖：R8（ContextPerson）、R9（FixedPerson）。
    /// </summary>
    public class EventResolverRoleSelectorTests
    {
        private const string VillageTrigger = "VillageRecognitionRaised";

        [Test]
        public void R8_ContextPerson_BindsFromSourceKey()
        {
            var catalog = SingleEventCatalog(Evt("evt_ctx"), VillageTrigger,
                roles: new List<EventRoleDefinition> { Role("role_observer", "evt_ctx", "ContextPerson", "Observer") });

            var context = ContextWithPerson("Observer", "p1");
            var result = Resolve(catalog, Request(VillageTrigger, "k_ctx", 3, context));

            Assert.IsTrue(result.Success, result.Diagnostic);

            string personId;
            Assert.IsTrue(result.Instance.TryGetBoundPerson("role_observer", out personId));
            Assert.AreEqual("p1", personId);
            Assert.AreEqual("Observer", result.Instance.RoleBindings[0].SourceKey);
        }

        [Test]
        public void R8_ContextPerson_MissingContextPerson_MakesEventInvalid()
        {
            var catalog = SingleEventCatalog(Evt("evt_ctx"), VillageTrigger,
                roles: new List<EventRoleDefinition> { Role("role_observer", "evt_ctx", "ContextPerson", "Observer") });

            var result = Resolve(catalog, Request(VillageTrigger, "k_ctx"));

            Assert.IsFalse(result.Success);
            Assert.AreEqual(EventResolutionCode.NoLegalRoleBinding, result.Code);

            var entry = result.Trace.Find("evt_ctx");
            Assert.AreEqual(EventRejectStage.RoleBinding, entry.RejectStage);
            StringAssert.Contains("person[Observer]", entry.RejectReason);
        }

        [Test]
        public void R8_ContextPerson_UnknownPersonInWorld_MakesEventInvalid()
        {
            var catalog = SingleEventCatalog(Evt("evt_ctx"), VillageTrigger,
                roles: new List<EventRoleDefinition> { Role("role_observer", "evt_ctx", "ContextPerson", "Observer") });

            var context = ContextWithPerson("Observer", "p_ghost");
            var result = Resolve(catalog, Request(VillageTrigger, "k_ctx", 3, context));

            Assert.IsFalse(result.Success);
            Assert.AreEqual(EventResolutionCode.NoLegalRoleBinding, result.Code);
            StringAssert.Contains("p_ghost", result.Trace.Find("evt_ctx").RejectReason);
        }

        [Test]
        public void R9_FixedPerson_RequiresExistingPerson()
        {
            var okCatalog = SingleEventCatalog(Evt("evt_fixed"), VillageTrigger,
                roles: new List<EventRoleDefinition> { Role("role_npc", "evt_fixed", "FixedPerson", null, "p2") });

            var ok = Resolve(okCatalog, Request(VillageTrigger, "k_fixed"));
            Assert.IsTrue(ok.Success, ok.Diagnostic);

            string personId;
            Assert.IsTrue(ok.Instance.TryGetBoundPerson("role_npc", out personId));
            Assert.AreEqual("p2", personId);

            var missingCatalog = SingleEventCatalog(Evt("evt_fixed"), VillageTrigger,
                roles: new List<EventRoleDefinition> { Role("role_npc", "evt_fixed", "FixedPerson", null, "p_ghost") });

            var missing = Resolve(missingCatalog, Request(VillageTrigger, "k_fixed"));
            Assert.IsFalse(missing.Success);
            Assert.AreEqual(EventResolutionCode.NoLegalRoleBinding, missing.Code);
            StringAssert.Contains("p_ghost", missing.Trace.Find("evt_fixed").RejectReason);
        }
    }
}
