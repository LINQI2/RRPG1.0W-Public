using System.Collections.Generic;
using NUnit.Framework;
using RRPG.Event;
using RRPG.StaticConfig;
using static RRPG.Event.Tests.EventResolverTestKit;
using static RRPG.Event.Tests.EventResolverTestWorld;

namespace RRPG.Event.Tests
{
    /// <summary>
    /// WP-Event-1B：条件组语义（§14）。覆盖：R15。
    /// 同 GroupId 内 AND；不同 GroupId 之间 OR；无条件 = true。
    /// </summary>
    public class EventResolverConditionGroupTests
    {
        private const string VillageTrigger = "VillageRecognitionRaised";

        private static EventStaticCatalog GroupCatalog()
        {
            return SingleEventCatalog(Evt("evt_groups"), VillageTrigger,
                conditions: new List<EventConditionDefinition>
                {
                    // Group 1 = (ObservedCount >= 3) AND (RecognitionType == TemporaryDraftBuildup)
                    Condition("c1", "evt_groups", 1, "ContextIntAtLeast", targetKey: "ObservedCount", intValue: 3),
                    Condition("c2", "evt_groups", 1, "ContextStringEquals", targetKey: "RecognitionType",
                        stringValue: "TemporaryDraftBuildup"),
                    // Group 2 = (ObservedCount >= 100)
                    Condition("c3", "evt_groups", 2, "ContextIntAtLeast", targetKey: "ObservedCount", intValue: 100)
                });
        }

        [Test]
        public void R15_Group1Satisfied_WithGroup2Unsatisfied_IsCandidate()
        {
            var context = new EventContext()
                .SetInt("ObservedCount", 4)
                .SetString("RecognitionType", "TemporaryDraftBuildup");

            var result = Resolve(GroupCatalog(), Request(VillageTrigger, "k_groups", 1, context));

            Assert.IsTrue(result.Success, result.Diagnostic);
            Assert.IsTrue(result.Trace.Find("evt_groups").ConditionPassed);
        }

        [Test]
        public void R15_Group2Satisfied_WithGroup1Unsatisfied_IsCandidate()
        {
            var context = new EventContext()
                .SetInt("ObservedCount", 100)
                .SetString("RecognitionType", "OtherType");

            var result = Resolve(GroupCatalog(), Request(VillageTrigger, "k_groups", 1, context));

            Assert.IsTrue(result.Success, "不同条件组之间是 OR：" + result.Diagnostic);
        }

        [Test]
        public void R15_AllGroupsUnsatisfied_IsRejectedAtConditionStage()
        {
            var context = new EventContext()
                .SetInt("ObservedCount", 4)
                .SetString("RecognitionType", "OtherType");

            var result = Resolve(GroupCatalog(), Request(VillageTrigger, "k_groups", 1, context));

            Assert.IsFalse(result.Success);
            Assert.AreEqual(EventResolutionCode.NoCandidateEvent, result.Code);

            var entry = result.Trace.Find("evt_groups");
            Assert.AreEqual(EventRejectStage.Condition, entry.RejectStage);
            Assert.IsFalse(entry.ConditionPassed);
            StringAssert.Contains("c2", entry.RejectReason);   // 首个不成立的条件（组 1 内 c1 通过、c2 失败）
        }

        [Test]
        public void R15_WithinGroup_IsAnd_NotOr()
        {
            // 组 1 只满足一半（RecognitionType 对、ObservedCount 不足），组 2 不满足 → 整体不成立
            var context = new EventContext()
                .SetInt("ObservedCount", 1)
                .SetString("RecognitionType", "TemporaryDraftBuildup");

            var result = Resolve(GroupCatalog(), Request(VillageTrigger, "k_groups", 1, context));

            Assert.IsFalse(result.Success, "同一条件组内是 AND，不得因为其中一个条件成立就通过");
            Assert.AreEqual(EventRejectStage.Condition, result.Trace.Find("evt_groups").RejectStage);
        }
    }
}
