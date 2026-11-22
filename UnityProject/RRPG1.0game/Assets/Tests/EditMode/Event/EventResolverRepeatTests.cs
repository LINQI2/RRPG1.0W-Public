using NUnit.Framework;
using RRPG.Event;
using static RRPG.Event.Tests.EventResolverTestKit;
using static RRPG.Event.Tests.EventResolverTestWorld;

namespace RRPG.Event.Tests
{
    /// <summary>
    /// WP-Event-1B：RepeatPolicy 预筛选（§15）。覆盖：R4、R5。
    /// </summary>
    public class EventResolverRepeatTests
    {
        private const string VillageTrigger = "VillageRecognitionRaised";
        private const string ExpeditionTrigger = "ExpeditionEventNodeReached";

        [Test]
        public void R4_OncePerWorld_AlreadyOccurred_IsExcluded()
        {
            var catalog = SingleEventCatalog(Evt("evt_once_world", repeatPolicy: "OncePerWorld"),
                VillageTrigger);

            var worldState = new EventWorldState();
            worldState.History.SeedOccurrenceForTesting("evt_once_world", 10);

            var result = Resolve(catalog, Request(VillageTrigger, "k_once_world"), worldState: worldState);

            Assert.IsFalse(result.Success);
            Assert.AreEqual(EventResolutionCode.NoCandidateEvent, result.Code);

            var entry = result.Trace.Find("evt_once_world");
            Assert.AreEqual(EventRejectStage.Repeat, entry.RejectStage);
            StringAssert.Contains("OncePerWorld", entry.RejectReason);
        }

        [Test]
        public void R4_OncePerWorld_NeverOccurred_IsCandidate()
        {
            var catalog = SingleEventCatalog(Evt("evt_once_world", repeatPolicy: "OncePerWorld"),
                VillageTrigger);

            var result = Resolve(catalog, Request(VillageTrigger, "k_once_world"));

            Assert.IsTrue(result.Success, result.Diagnostic);
            Assert.AreNotEqual(EventRejectStage.Repeat, result.Trace.Find("evt_once_world").RejectStage);
        }

        [Test]
        public void R5_OncePerExpedition_SameSessionExcluded_NewSessionAllowed()
        {
            var catalog = SingleEventCatalog(Evt("evt_once_exp", repeatPolicy: "OncePerExpedition"),
                ExpeditionTrigger);

            var session = new ExpeditionEventSessionState("session_1");
            session.SeedOccurrenceForTesting("evt_once_exp", 3);

            var blocked = Resolve(catalog, Request(ExpeditionTrigger, "k_once_exp"), sessionState: session);
            Assert.IsFalse(blocked.Success);
            Assert.AreEqual(EventRejectStage.Repeat, blocked.Trace.Find("evt_once_exp").RejectStage);
            StringAssert.Contains("OncePerExpedition", blocked.Trace.Find("evt_once_exp").RejectReason);

            // 新的一次远征 = 新的 Session State → 可以再次出现
            var fresh = Resolve(catalog, Request(ExpeditionTrigger, "k_once_exp"),
                sessionState: new ExpeditionEventSessionState("session_2"));
            Assert.IsTrue(fresh.Success, fresh.Diagnostic);
        }

        [Test]
        public void R4_Repeatable_IgnoresHistory()
        {
            var catalog = SingleEventCatalog(Evt("evt_repeatable"), VillageTrigger);

            var worldState = new EventWorldState();
            worldState.History.SeedOccurrenceForTesting("evt_repeatable", 50);

            var result = Resolve(catalog, Request(VillageTrigger, "k_repeatable"), worldState: worldState);
            Assert.IsTrue(result.Success, result.Diagnostic);
        }
    }
}
