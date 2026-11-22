using System.Collections.Generic;
using NUnit.Framework;
using RRPG.Event;
using RRPG.StaticConfig;
using static RRPG.Event.Tests.EventResolverTestKit;
using static RRPG.Event.Tests.EventResolverTestWorld;

namespace RRPG.Event.Tests
{
    /// <summary>WP-Event-1B：Context 条件（§13）。覆盖：R16。</summary>
    public class EventResolverConditionContextTests
    {
        private const string VillageTrigger = "VillageRecognitionRaised";

        [Test]
        public void R16_ContextIntBoundaries_AreInclusive()
        {
            var atLeast = SingleEventCatalog(Evt("evt_al"), VillageTrigger,
                conditions: new List<EventConditionDefinition>
                {
                    Condition("c", "evt_al", 1, "ContextIntAtLeast", targetKey: "N", intValue: 5)
                });

            Assert.IsTrue(
                Resolve(atLeast, Request(VillageTrigger, "k", 1, new EventContext().SetInt("N", 5))).Success,
                ">= 边界必须包含相等值");
            Assert.IsFalse(
                Resolve(atLeast, Request(VillageTrigger, "k", 1, new EventContext().SetInt("N", 4))).Success);

            var atMost = SingleEventCatalog(Evt("evt_am"), VillageTrigger,
                conditions: new List<EventConditionDefinition>
                {
                    Condition("c", "evt_am", 1, "ContextIntAtMost", targetKey: "N", intValue: 5)
                });

            Assert.IsTrue(
                Resolve(atMost, Request(VillageTrigger, "k", 1, new EventContext().SetInt("N", 5))).Success,
                "<= 边界必须包含相等值");
            Assert.IsFalse(
                Resolve(atMost, Request(VillageTrigger, "k", 1, new EventContext().SetInt("N", 6))).Success);
        }

        [Test]
        public void R16_ContextStringEquals_RequiresExactMatch()
        {
            var catalog = SingleEventCatalog(Evt("evt_str"), VillageTrigger,
                conditions: new List<EventConditionDefinition>
                {
                    Condition("c", "evt_str", 1, "ContextStringEquals", targetKey: "RecognitionType",
                        stringValue: "TemporaryDraftBuildup")
                });

            Assert.IsTrue(Resolve(catalog,
                Request(VillageTrigger, "k", 1,
                    new EventContext().SetString("RecognitionType", "TemporaryDraftBuildup"))).Success);

            Assert.IsFalse(Resolve(catalog,
                Request(VillageTrigger, "k", 1,
                    new EventContext().SetString("RecognitionType", "temporarydraftbuildup"))).Success,
                "字符串比较必须是精确匹配（Ordinal）");
        }

        [Test]
        public void R16_MissingContextKey_IsNotSilentlyAccepted()
        {
            var catalog = SingleEventCatalog(Evt("evt_str"), VillageTrigger,
                conditions: new List<EventConditionDefinition>
                {
                    Condition("c", "evt_str", 1, "ContextStringEquals", targetKey: "RecognitionType",
                        stringValue: "TemporaryDraftBuildup")
                });

            var missing = Resolve(catalog, Request(VillageTrigger, "k", 1, new EventContext()));

            Assert.IsFalse(missing.Success, "缺少 Context 键时条件不成立，而不是静默通过");
            StringAssert.Contains("RecognitionType", missing.Trace.Find("evt_str").RejectReason);
        }
    }
}
