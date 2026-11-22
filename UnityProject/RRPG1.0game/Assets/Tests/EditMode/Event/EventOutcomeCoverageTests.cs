using System.Collections.Generic;
using NUnit.Framework;
using RRPG.Event;
using RRPG.StaticConfig;
using static RRPG.Event.Tests.EventExecutionTestKit;
using static RRPG.Event.Tests.EventResolverTestKit;

namespace RRPG.Event.Tests
{
    /// <summary>
    /// WP-Event-1C：Outcome 权重覆盖（§6）。覆盖：C4。
    /// 断言的是「多个合法 Outcome 都能被执行到 + 不依赖 UI 重开」，不写死「Seed N 必须 Outcome X」。
    /// </summary>
    public class EventOutcomeCoverageTests
    {
        private const string EventId = "evt_outcome";

        private static EventStaticCatalog BuildCatalog()
        {
            return Catalog(
                new List<EventDefinition> { Evt(EventId) },
                new List<EventTriggerDefinition> { Trigger("trg_outcome", EventId, VillageTrigger) },
                options: new List<EventOptionDefinition> { Option("opt_one", EventId, 1, "Hide") },
                outcomes: new List<EventOutcomeDefinition>
                {
                    Outcome("out_a", "opt_one", 70),
                    Outcome("out_b", "opt_one", 30)
                });
        }

        [Test]
        public void C4_WeightedOutcomes_AllAppear_AcrossDifferentInstances()
        {
            var catalog = BuildCatalog();
            var seen = new HashSet<string>();

            for (int i = 0; i < 80; i++)
            {
                var instance = ResolveInstance(catalog, triggerKey: "key_" + i, seed: i);
                Assert.IsNotNull(instance);

                var outcome = EventOptionExecutor.PreviewOutcome(instance, "opt_one", catalog);
                Assert.IsNotNull(outcome);
                Assert.IsTrue(outcome.OutcomeId == "out_a" || outcome.OutcomeId == "out_b",
                    "必须落在合法 Outcome 集合内：" + outcome.OutcomeId);
                seen.Add(outcome.OutcomeId);
            }

            CollectionAssert.AreEquivalent(new[] { "out_a", "out_b" }, new List<string>(seen),
                "权重 70/30 下两个合法 Outcome 都应被执行到");
        }

        [Test]
        public void C4_SameInstance_NeverChanges_AcrossManyQueries()
        {
            var catalog = BuildCatalog();
            var instance = ResolveInstance(catalog, triggerKey: "k_stable", seed: 9);

            string first = EventOptionExecutor.PreviewOutcome(instance, "opt_one", catalog).OutcomeId;
            for (int i = 0; i < 50; i++)
            {
                Assert.AreEqual(first, EventOptionExecutor.PreviewOutcome(instance, "opt_one", catalog).OutcomeId,
                    "同一个实例的结果不得因为反复查询而改变（玩家不能靠开关事件卡重抽）");
            }
        }

        [Test]
        public void C4_UnknownOptionOrEvent_ReturnsNullPreview()
        {
            var catalog = BuildCatalog();
            var instance = ResolveInstance(catalog, triggerKey: "k_null", seed: 1);

            Assert.IsNull(EventOptionExecutor.PreviewOutcome(instance, "opt_missing", catalog));
            Assert.IsNull(EventOptionExecutor.PreviewOutcome(null, "opt_one", catalog));
        }
    }
}
