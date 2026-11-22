using System.Collections.Generic;
using NUnit.Framework;
using RRPG.Event;
using RRPG.StaticConfig;
using static RRPG.Event.Tests.EventResolverTestKit;
using static RRPG.Event.Tests.EventResolverTestWorld;

namespace RRPG.Event.Tests
{
    /// <summary>
    /// WP-Event-1B：权重公平性与结构化 Trace（§18 / §24）。覆盖：R14、R25。
    /// </summary>
    public class EventResolverTraceAndWeightTests
    {
        private const string VillageTrigger = "VillageRecognitionRaised";

        /// <summary>
        /// A：一个 Role 有多个合法人物（很多 Binding）；B：固定人物（只有一个 Binding）。
        /// 两者都只能进入候选池**一次**：Weight 只决定 Event 之间的相对选择。
        /// </summary>
        private static EventStaticCatalog WeightCatalog()
        {
            return Catalog(
                new List<EventDefinition>
                {
                    Evt("evt_many_bindings", weight: 100),
                    Evt("evt_single_binding", weight: 50)
                },
                new List<EventTriggerDefinition>
                {
                    Trigger("trg_a", "evt_many_bindings", VillageTrigger),
                    Trigger("trg_b", "evt_single_binding", VillageTrigger)
                },
                new List<EventRoleDefinition>
                {
                    Role("role_many", "evt_many_bindings", "VillagePerson"),
                    Role("role_one", "evt_single_binding", "FixedPerson", null, "p1")
                });
        }

        [Test]
        public void R14_BindingCount_DoesNotInflateEventWeight()
        {
            var result = Resolve(WeightCatalog(), Request(VillageTrigger, "k_weight", 5));

            Assert.IsTrue(result.Success, result.Diagnostic);
            Assert.AreEqual(2, result.Trace.CandidateCount, "每个事件只占一张票");

            var many = result.Trace.Find("evt_many_bindings");
            var one = result.Trace.Find("evt_single_binding");

            Assert.IsTrue(many.IsCandidate);
            Assert.IsTrue(one.IsCandidate);
            Assert.AreEqual(100, many.Weight, "候选权重必须等于配置权重，不因 Binding 数量放大");
            Assert.AreEqual(50, one.Weight);
        }

        [Test]
        public void R14_BothCandidates_CanBeSelected_OverDifferentSeeds()
        {
            var seen = new HashSet<string>();

            for (int seed = 0; seed < 200; seed++)
            {
                var result = Resolve(WeightCatalog(), Request(VillageTrigger, "k_weight", seed));
                Assert.IsTrue(result.Success, result.Diagnostic);
                seen.Add(result.Instance.EventId);
            }

            CollectionAssert.AreEquivalent(
                new[] { "evt_many_bindings", "evt_single_binding" }, new List<string>(seen),
                "权重 100 / 50 下两个候选都应被执行到（证明权重是可用的相对概率，而不是 Binding 数量）");
        }

        // ---------------- R25：Trace ----------------

        [Test]
        public void R25_Trace_ExplainsRejection_AndIdentifiesSelection()
        {
            var catalog = Catalog(
                new List<EventDefinition>
                {
                    Evt("evt_ok", weight: 10),
                    Evt("evt_cooldown", cooldownType: "VillageDays", cooldownValue: 5)
                },
                new List<EventTriggerDefinition>
                {
                    Trigger("trg_ok", "evt_ok", VillageTrigger),
                    Trigger("trg_cd", "evt_cooldown", VillageTrigger)
                });

            var worldState = new EventWorldState();
            worldState.History.SeedOccurrenceForTesting("evt_cooldown", 99);   // 差 1 < 5

            var result = Resolve(catalog, Request(VillageTrigger, "k_trace", 8), worldState: worldState);

            Assert.IsTrue(result.Success, result.Diagnostic);
            Assert.AreEqual("evt_ok", result.Trace.SelectedEventId);
            Assert.AreEqual(result.Instance.EventId, result.Trace.SelectedEventId);

            var rejected = result.Trace.Find("evt_cooldown");
            Assert.IsTrue(rejected.TriggerMatched);
            Assert.IsTrue(rejected.RepeatPassed);
            Assert.IsFalse(rejected.CooldownPassed);
            Assert.IsFalse(rejected.IsCandidate);
            Assert.AreEqual(EventRejectStage.Cooldown, rejected.RejectStage);
            StringAssert.Contains("VillageDays", rejected.RejectReason);

            var selected = result.Trace.Find("evt_ok");
            Assert.IsTrue(selected.IsCandidate);
            Assert.IsTrue(selected.RoleBindingPassed);
            Assert.IsTrue(selected.ConditionPassed);
            Assert.AreEqual(EventRejectStage.None, selected.RejectStage);
            Assert.AreEqual(10, selected.Weight);

            string text = result.Trace.ToText();
            StringAssert.Contains("evt_cooldown", text);
            StringAssert.Contains("evt_ok", text);
            StringAssert.Contains("selected=evt_ok", text);
            StringAssert.Contains("candidates=1", text);
        }

        [Test]
        public void R25_Trace_IsStructured_NotConsoleSpam()
        {
            // Resolver 自身不写 Console：Trace 只随结果对象返回，调用方按需输出。
            var result = Resolve(WeightCatalog(), Request(VillageTrigger, "k_trace2", 1));

            Assert.IsNotNull(result.Trace);
            Assert.Greater(result.Trace.EvaluatedEventCount, 0);
            Assert.IsNotEmpty(result.Trace.ToText());
            Assert.IsNotNull(result.Trace.Entries);
        }
    }
}
