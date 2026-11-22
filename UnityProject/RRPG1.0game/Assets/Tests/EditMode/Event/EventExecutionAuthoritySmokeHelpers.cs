using System;
using NUnit.Framework;
using RRPG.Event;
using RRPG.StaticConfig;

namespace RRPG.Event.Tests
{
    /// <summary>EventExecutionAuthoritySmokeTests：单个 Option 的一致性与执行验证。</summary>
    public partial class EventExecutionAuthoritySmokeTests
    {
        /// <summary>返回 true 表示该 Option 当前可选（并已真实执行成功）。</summary>
        private static bool CheckOption(EventStaticCatalog catalog, ResolvedEventInstance instance,
            EventOptionDefinition option, EventResolverSmokeFixture fixture, bool isExpedition)
        {
            var availability = EventOptionEvaluator.Evaluate(instance, option.OptionId, catalog, fixture.World,
                fixture.WorldState);

            var execution = new EventExecutionState();
            var gateway = new EventEffectGatewayFixture(ingredients: 9999, food: 9999, supply: 999);
            var worldState = new EventWorldState();
            var session = new ExpeditionEventSessionState("smoke_session");

            var result = EventOptionExecutor.Execute(instance, option.OptionId, catalog, fixture.World, worldState,
                session, execution, gateway);

            if (!availability.IsSelectable)
            {
                Assert.IsTrue(result.Code == EventExecutionCode.OptionHidden
                              || result.Code == EventExecutionCode.OptionDisabled,
                    "不可选选项必须被一致拒绝（可用性=" + availability.Availability + "，执行=" + result.Code
                    + "）：" + option.OptionId);
                Assert.AreEqual(0, gateway.TotalMutationCalls, "不可选选项不得 mutation：" + option.OptionId);
                Assert.AreEqual(0, worldState.History.OccurrenceCount(instance.EventId));
                return false;
            }

            Assert.IsTrue(result.Success, "可选选项必须执行成功：" + instance.EventId + "/" + option.OptionId
                + " → " + result.Code + " / " + result.Diagnostic + "\n" + result.Trace.ToText());

            foreach (var entry in result.Effects)
            {
                Assert.AreEqual(EventEffectPreflightState.Ok, entry.Preflight,
                    "效果未通过 Preflight：" + entry.Describe());
            }

            if (result.HasTerminalEffect)
            {
                Assert.IsTrue(result.Effects[result.Effects.Count - 1].IsTerminal,
                    "Terminal Effect 必须是最后一个效果：" + instance.EventId);
            }

            Assert.AreEqual(1, worldState.History.OccurrenceCount(instance.EventId), "成功必须写 World History");
            Assert.AreEqual(isExpedition ? 1 : 0, session.OccurrenceCount(instance.EventId),
                "只有 Expedition Domain 事件写 Session History");

            var preview = EventOptionExecutor.PreviewOutcome(instance, option.OptionId, catalog);
            Assert.IsNotNull(preview);
            Assert.AreEqual(result.OutcomeId, preview.OutcomeId, "预览与执行必须是同一个 Outcome");

            var second = EventOptionExecutor.Execute(instance, option.OptionId, catalog, fixture.World, worldState,
                session, execution, gateway);
            Assert.AreEqual(EventExecutionCode.InstanceAlreadyResolved, second.Code,
                "同一个实例第二次执行必须 AlreadyResolved");

            return true;
        }
    }
}
