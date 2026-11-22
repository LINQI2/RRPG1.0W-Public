using System.Collections.Generic;
using NUnit.Framework;
using RRPG.Event;
using RRPG.StaticConfig;
using static RRPG.Event.Tests.EventResolverTestKit;
using static RRPG.Event.Tests.EventResolverTestWorld;

namespace RRPG.Event.Tests
{
    /// <summary>
    /// WP-Event-1B：Resolver 确定性 / 触发过滤。覆盖：R1、R2、R3。
    /// </summary>
    public class EventResolverCoreTests
    {
        private const string VillageTrigger = "VillageRecognitionRaised";
        private const string ExpeditionTrigger = "ExpeditionEventNodeReached";

        private static EventStaticCatalog TwoVillageEvents(int weightA = 100, int weightB = 100)
        {
            return Catalog(
                new List<EventDefinition> { Evt("evt_a", weight: weightA), Evt("evt_b", weight: weightB) },
                new List<EventTriggerDefinition>
                {
                    Trigger("trg_a", "evt_a", VillageTrigger),
                    Trigger("trg_b", "evt_b", VillageTrigger)
                });
        }

        [Test]
        public void R1_SameInputs_ProduceIdenticalInstance()
        {
            var catalog = TwoVillageEvents();
            var context = new EventContext()
                .SetPerson("Observer", "p1")
                .SetString("RecognitionType", "TemporaryDraftBuildup")
                .SetInt("ObservedCount", 4);

            var first = Resolve(catalog, Request(VillageTrigger, "recognition:p1:year_2", 2026, context));
            var second = Resolve(catalog, Request(VillageTrigger, "recognition:p1:year_2", 2026, context));

            Assert.IsTrue(first.Success, first.Diagnostic);
            Assert.IsTrue(second.Success, second.Diagnostic);

            Assert.AreEqual(first.Instance.EventId, second.Instance.EventId);
            Assert.AreEqual(first.Instance.EventInstanceId, second.Instance.EventInstanceId);
            Assert.AreEqual(first.Instance.TriggerKey, second.Instance.TriggerKey);
            Assert.AreEqual(first.Instance.ResolutionSeed, second.Instance.ResolutionSeed);
            Assert.AreEqual(first.Instance.ContextSnapshot.ToText(), second.Instance.ContextSnapshot.ToText());
            Assert.AreEqual(first.Trace.CandidateCount, second.Trace.CandidateCount);
            Assert.AreEqual(first.Trace.SelectedEventId, second.Trace.SelectedEventId);
        }

        [Test]
        public void R1_InstanceId_IsStableDerivation_NotGuid_NotEventId()
        {
            var catalog = TwoVillageEvents();

            var a1 = Resolve(catalog, Request(VillageTrigger, "k1", 11));
            Assert.IsTrue(a1.Success, a1.Diagnostic);

            string id = a1.Instance.EventInstanceId;
            Assert.IsFalse(string.IsNullOrEmpty(id));
            Assert.AreNotEqual(a1.Instance.EventId, id, "InstanceId 不得直接等于 EventId");
            Assert.IsTrue(id.StartsWith("evtinst_"), "InstanceId 必须是稳定派生格式：" + id);
            Assert.IsFalse(id.Contains("-"), "InstanceId 不得是 Guid 形态：" + id);

            var a2 = Resolve(catalog, Request(VillageTrigger, "k1", 12));
            Assert.IsTrue(a2.Success, a2.Diagnostic);
            Assert.AreNotEqual(id, a2.Instance.EventInstanceId, "不同 Seed 应得到不同的解析身份");
        }

        [Test]
        public void R2_DifferentSeeds_CanSelectDifferentEvents_AllLegal()
        {
            var catalog = TwoVillageEvents();
            var seen = new HashSet<string>();

            for (int seed = 0; seed < 100; seed++)
            {
                var result = Resolve(catalog, Request(VillageTrigger, "k_seed", seed));
                Assert.IsTrue(result.Success, result.Diagnostic);

                Assert.IsTrue(result.Instance.EventId == "evt_a" || result.Instance.EventId == "evt_b",
                    "必须选中合法候选之一：" + result.Instance.EventId);
                seen.Add(result.Instance.EventId);
            }

            CollectionAssert.AreEquivalent(new[] { "evt_a", "evt_b" }, new List<string>(seen),
                "等权重的两个合法候选都应在不同 Seed 下被执行到");
        }

        [Test]
        public void R3_OnlyEventsListeningToRequestedTrigger_AreEvaluated()
        {
            var catalog = Catalog(
                new List<EventDefinition> { Evt("evt_village"), Evt("evt_expedition") },
                new List<EventTriggerDefinition>
                {
                    Trigger("trg_v1", "evt_village", VillageTrigger),
                    Trigger("trg_v2", "evt_village", VillageTrigger),   // 同事件第二条同类型触发
                    Trigger("trg_e1", "evt_expedition", ExpeditionTrigger)
                });

            var result = Resolve(catalog, Request(VillageTrigger, "k_filter"));

            Assert.IsTrue(result.Success, result.Diagnostic);
            Assert.AreEqual("evt_village", result.Instance.EventId);
            Assert.AreEqual(1, result.Trace.CandidateCount, "同一事件多行同类型触发仍然只占一张票");

            var expeditionEntry = result.Trace.Find("evt_expedition");
            Assert.IsNotNull(expeditionEntry);
            Assert.IsFalse(expeditionEntry.TriggerMatched, "未监听本次触发的事件不得进入候选");
        }

        [Test]
        public void R3_DisabledEvent_IsNotEvaluated()
        {
            var catalog = Catalog(
                new List<EventDefinition> { Evt("evt_disabled", enabled: false), Evt("evt_enabled") },
                new List<EventTriggerDefinition>
                {
                    Trigger("trg_d", "evt_disabled", VillageTrigger),
                    Trigger("trg_e", "evt_enabled", VillageTrigger)
                });

            var result = Resolve(catalog, Request(VillageTrigger, "k_disabled"));

            Assert.IsTrue(result.Success, result.Diagnostic);
            Assert.AreEqual("evt_enabled", result.Instance.EventId);
        }
    }
}
