using NUnit.Framework;
using RRPG.Event;
using static RRPG.Event.Tests.EventResolverTestKit;
using static RRPG.Event.Tests.EventResolverTestWorld;

namespace RRPG.Event.Tests
{
    /// <summary>
    /// WP-Event-1B：Cooldown 预筛选（§16）。覆盖：R6、R7，并证明 Resolver 只读历史（§26）。
    /// 边界冻结为 `currentOrdinal - lastOrdinal &gt;= CooldownValue`（只用序数差，绝不用现实时间）。
    /// </summary>
    public class EventResolverCooldownTests
    {
        private const string VillageTrigger = "VillageRecognitionRaised";
        private const string ExpeditionTrigger = "ExpeditionEventNodeReached";

        [Test]
        public void R6_VillageDaysCooldown_BoundaryIsInclusiveAtRequiredDelta()
        {
            var catalog = SingleEventCatalog(
                Evt("evt_cd_days", cooldownType: "VillageDays", cooldownValue: 3), VillageTrigger);

            // 当前村庄日序 = 100（StandardWorld）
            var blockedState = new EventWorldState();
            blockedState.History.SeedOccurrenceForTesting("evt_cd_days", 98);   // 差 2 < 3

            var blocked = Resolve(catalog, Request(VillageTrigger, "k_cd"), worldState: blockedState);
            Assert.IsFalse(blocked.Success);
            Assert.AreEqual(EventRejectStage.Cooldown, blocked.Trace.Find("evt_cd_days").RejectStage);
            StringAssert.Contains("2/3", blocked.Trace.Find("evt_cd_days").RejectReason);

            var readyState = new EventWorldState();
            readyState.History.SeedOccurrenceForTesting("evt_cd_days", 97);     // 差 3 >= 3

            var ready = Resolve(catalog, Request(VillageTrigger, "k_cd"), worldState: readyState);
            Assert.IsTrue(ready.Success, ready.Diagnostic);
        }

        [Test]
        public void R7_ExpeditionNodesCooldown_BoundaryIsInclusiveAtRequiredDelta()
        {
            var catalog = SingleEventCatalog(
                Evt("evt_cd_nodes", cooldownType: "ExpeditionNodes", cooldownValue: 2), ExpeditionTrigger);

            // 当前远征节点序 = 5（StandardWorld）
            var blockedSession = new ExpeditionEventSessionState("s");
            blockedSession.SeedOccurrenceForTesting("evt_cd_nodes", 4);   // 差 1 < 2

            var blocked = Resolve(catalog, Request(ExpeditionTrigger, "k_cd_nodes"),
                sessionState: blockedSession);
            Assert.IsFalse(blocked.Success);
            Assert.AreEqual(EventRejectStage.Cooldown, blocked.Trace.Find("evt_cd_nodes").RejectStage);

            var readySession = new ExpeditionEventSessionState("s");
            readySession.SeedOccurrenceForTesting("evt_cd_nodes", 3);     // 差 2 >= 2

            var ready = Resolve(catalog, Request(ExpeditionTrigger, "k_cd_nodes"), sessionState: readySession);
            Assert.IsTrue(ready.Success, ready.Diagnostic);
        }

        [Test]
        public void Cooldown_NeverOccurred_IsReady()
        {
            var catalog = SingleEventCatalog(
                Evt("evt_cd_new", cooldownType: "VillageDays", cooldownValue: 30), VillageTrigger);

            var result = Resolve(catalog, Request(VillageTrigger, "k_cd_new"));
            Assert.IsTrue(result.Success, result.Diagnostic);
        }

        [Test]
        public void Cooldown_ExpeditionNodes_IgnoresWorldHistory()
        {
            var catalog = SingleEventCatalog(
                Evt("evt_cd_nodes", cooldownType: "ExpeditionNodes", cooldownValue: 2), ExpeditionTrigger);

            // 世界历史有记录，但本次远征会话没有 → ExpeditionNodes 冷却不生效
            var worldState = new EventWorldState();
            worldState.History.SeedOccurrenceForTesting("evt_cd_nodes", 1);

            var result = Resolve(catalog, Request(ExpeditionTrigger, "k_mix"), worldState: worldState);
            Assert.IsTrue(result.Success, result.Diagnostic);
        }

        [Test]
        public void History_IsReadOnly_DuringResolution()
        {
            var catalog = SingleEventCatalog(
                Evt("evt_readonly", cooldownType: "VillageDays", cooldownValue: 1), VillageTrigger);

            var worldState = new EventWorldState();
            var session = new ExpeditionEventSessionState("s");

            var result = Resolve(catalog, Request(VillageTrigger, "k_readonly"), worldState: worldState,
                sessionState: session);
            Assert.IsTrue(result.Success, result.Diagnostic);

            Assert.AreEqual(0, worldState.History.OccurrenceCount("evt_readonly"),
                "Resolver 不得因为「解析出事件」就记录 occurrence（写入属 1C）");
            Assert.AreEqual(0, session.OccurrenceCount("evt_readonly"));
            Assert.IsEmpty(worldState.Flags);
        }
    }
}
