using System.Collections.Generic;
using NUnit.Framework;
using RRPG.Event;
using RRPG.StaticConfig;
using static RRPG.Event.Tests.EventResolverTestKit;
using static RRPG.Event.Tests.EventResolverTestWorld;

namespace RRPG.Event.Tests
{
    /// <summary>
    /// WP-Event-1B：World Flag 与 Event History 条件（§13）。覆盖：R17、R18。
    ///
    /// V0.1 冻结语义：`EventNotSeen` / `EventSeenCountAtLeast` **统一查询 World Event History**
    /// （世界长期历史）；远征会话历史不参与，也不允许同一个 ConditionType 按 Context 偷换语义。
    /// </summary>
    public class EventResolverConditionFlagHistoryTests
    {
        private const string VillageTrigger = "VillageRecognitionRaised";

        private static EventStaticCatalog FlagCatalog(string conditionType)
        {
            return SingleEventCatalog(Evt("evt_flag"), VillageTrigger,
                conditions: new List<EventConditionDefinition>
                {
                    Condition("c", "evt_flag", 1, conditionType, targetKey: "flag_test_done")
                });
        }

        [Test]
        public void R17_FlagSet_And_FlagNotSet()
        {
            var worldState = new EventWorldState();
            worldState.SeedFlagForTesting("flag_test_done");

            Assert.IsTrue(Resolve(FlagCatalog("FlagSet"), Request(VillageTrigger, "k_flag", 1),
                worldState: worldState).Success, "Flag 已设置 → FlagSet 成立");

            Assert.IsFalse(Resolve(FlagCatalog("FlagNotSet"), Request(VillageTrigger, "k_flag", 1),
                worldState: worldState).Success, "Flag 已设置 → FlagNotSet 不成立");

            var emptyState = new EventWorldState();
            Assert.IsFalse(Resolve(FlagCatalog("FlagSet"), Request(VillageTrigger, "k_flag", 1),
                worldState: emptyState).Success);
            Assert.IsTrue(Resolve(FlagCatalog("FlagNotSet"), Request(VillageTrigger, "k_flag", 1),
                worldState: emptyState).Success);
        }

        [Test]
        public void R18_EventNotSeen_QueriesWorldHistory()
        {
            var catalog = SingleEventCatalog(Evt("evt_seen"), VillageTrigger,
                conditions: new List<EventConditionDefinition>
                {
                    Condition("c", "evt_seen", 1, "EventNotSeen", targetKey: "evt_other")
                });

            Assert.IsTrue(Resolve(catalog, Request(VillageTrigger, "k_seen", 1)).Success,
                "世界历史里从未发生 → EventNotSeen 成立");

            var worldState = new EventWorldState();
            worldState.History.SeedOccurrenceForTesting("evt_other", 10);

            Assert.IsFalse(Resolve(catalog, Request(VillageTrigger, "k_seen", 1), worldState: worldState).Success,
                "世界历史已发生 → EventNotSeen 必须不成立");
        }

        [Test]
        public void R18_EventSeenCountAtLeast_UsesWorldHistoryCount()
        {
            var catalog = SingleEventCatalog(Evt("evt_count"), VillageTrigger,
                conditions: new List<EventConditionDefinition>
                {
                    Condition("c", "evt_count", 1, "EventSeenCountAtLeast", targetKey: "evt_other", intValue: 2)
                });

            var oneTime = new EventWorldState();
            oneTime.History.SeedOccurrenceForTesting("evt_other", 10);
            Assert.IsFalse(Resolve(catalog, Request(VillageTrigger, "k_count", 1), worldState: oneTime).Success,
                "发生 1 次 < 2 → 不成立");

            var twoTimes = new EventWorldState();
            twoTimes.History.SeedOccurrenceForTesting("evt_other", 10);
            twoTimes.History.SeedOccurrenceForTesting("evt_other", 20);
            Assert.IsTrue(Resolve(catalog, Request(VillageTrigger, "k_count", 1), worldState: twoTimes).Success,
                "发生 2 次 >= 2 → 成立（边界包含）");
        }

        [Test]
        public void R18_SessionHistory_DoesNotAffectWorldHistoryConditions()
        {
            var catalog = SingleEventCatalog(Evt("evt_seen"), VillageTrigger,
                conditions: new List<EventConditionDefinition>
                {
                    Condition("c", "evt_seen", 1, "EventNotSeen", targetKey: "evt_other")
                });

            // 本次远征里发生过 evt_other，但世界历史没有 → EventNotSeen 仍成立（语义不随 Context 偷换）
            var session = new ExpeditionEventSessionState("s");
            session.SeedOccurrenceForTesting("evt_other", 3);

            var result = Resolve(catalog, Request(VillageTrigger, "k_seen", 1), sessionState: session);
            Assert.IsTrue(result.Success, result.Diagnostic);
        }
    }
}
