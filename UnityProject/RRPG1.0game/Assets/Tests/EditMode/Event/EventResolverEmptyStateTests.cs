using NUnit.Framework;
using RRPG.Event;
using static RRPG.Event.Tests.EventResolverTestKit;
using static RRPG.Event.Tests.EventResolverTestWorld;

namespace RRPG.Event.Tests
{
    /// <summary>
    /// WP-Event-1B：合法空态与上下文冻结。覆盖：R22、R23、R26。
    /// </summary>
    public class EventResolverEmptyStateTests
    {
        private const string VillageTrigger = "VillageRecognitionRaised";
        private const string ExpeditionTrigger = "ExpeditionEventNodeReached";

        [Test]
        public void R22_NoEventCondition_IsLegal()
        {
            var catalog = SingleEventCatalog(Evt("evt_no_cond"), VillageTrigger);

            var result = Resolve(catalog, Request(VillageTrigger, "k_no_cond"));

            Assert.IsTrue(result.Success, result.Diagnostic);
            Assert.IsTrue(result.Trace.Find("evt_no_cond").ConditionPassed);
        }

        [Test]
        public void R22_NoRole_IsLegal()
        {
            var catalog = SingleEventCatalog(Evt("evt_no_role"), VillageTrigger);

            var result = Resolve(catalog, Request(VillageTrigger, "k_no_role"));

            Assert.IsTrue(result.Success, result.Diagnostic);
            Assert.IsEmpty(result.Instance.RoleBindings);
            Assert.IsTrue(result.Trace.Find("evt_no_role").RoleBindingPassed);
        }

        [Test]
        public void R23_NoListeningEvent_ReturnsNoCandidateEvent_NotException()
        {
            var catalog = SingleEventCatalog(Evt("evt_other"), ExpeditionTrigger);

            var result = Resolve(catalog, Request(VillageTrigger, "k_none"));

            Assert.IsFalse(result.Success);
            Assert.AreEqual(EventResolutionCode.NoCandidateEvent, result.Code);
            Assert.IsNull(result.Instance);
            Assert.IsNotEmpty(result.Diagnostic);
            Assert.AreEqual(0, result.Trace.CandidateCount);
        }

        [Test]
        public void R23_EmptyCatalog_ReturnsNoCandidateEvent()
        {
            var result = Resolve(Catalog(), Request(VillageTrigger, "k_empty"));

            Assert.IsFalse(result.Success);
            Assert.AreEqual(EventResolutionCode.NoCandidateEvent, result.Code);
        }

        [Test]
        public void R26_ResolvedInstance_KeepsContextSnapshot_AfterCallerMutatesContext()
        {
            var catalog = SingleEventCatalog(Evt("evt_frozen"), VillageTrigger);
            var context = new EventContext()
                .SetPerson("Observer", "p1")
                .SetString("RecognitionType", "TemporaryDraftBuildup")
                .SetInt("ObservedCount", 4);

            var result = Resolve(catalog, Request(VillageTrigger, "k_frozen", 7, context));
            Assert.IsTrue(result.Success, result.Diagnostic);

            context.SetPerson("Observer", "p2");
            context.SetString("RecognitionType", "Changed");
            context.SetInt("ObservedCount", 99);

            string observer;
            Assert.IsTrue(result.Instance.ContextSnapshot.TryGetPerson("Observer", out observer));
            Assert.AreEqual("p1", observer, "已解析实例的 Context 快照不得被外部修改污染");

            string type;
            Assert.IsTrue(result.Instance.ContextSnapshot.TryGetString("RecognitionType", out type));
            Assert.AreEqual("TemporaryDraftBuildup", type);

            int count;
            Assert.IsTrue(result.Instance.ContextSnapshot.TryGetInt("ObservedCount", out count));
            Assert.AreEqual(4, count);
        }
    }
}
