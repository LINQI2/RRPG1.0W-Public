using System.Collections.Generic;
using System;
using NUnit.Framework;
using RRPG.Event;
using RRPG.StaticConfig;

namespace RRPG.Event.Tests
{
    /// <summary>
    /// WP-Event-1C §27：对当前 Authority 内容的**行为**验证（全部经 Fixture Gateway）。
    ///
    /// 验证：GrantPersonRest 真的下发给「该 Role 绑定的人物」；无 Effect 的 Option 也能成功并记录；
    /// 多 Outcome 的 Option 在多个实例上覆盖全部合法 Outcome；Battle Outcome 的 StartBattle
    /// 经 Gateway 请求（不启动真实 Battle）且必须是最后一个效果。
    /// </summary>
    public partial class EventExecutionAuthoritySmokeTests
    {
        private static ResolvedEventInstance ResolveForOption(EventStaticCatalog catalog, string optionId,
            string triggerKey, int seed)
        {
            EventOptionDefinition option;
            Assert.IsTrue(catalog.TryGetOption(optionId, out option), "配置里应存在该 Option：" + optionId);

            var triggers = catalog.GetTriggers(option.EventId);
            Assert.IsNotEmpty(triggers, "该事件必须有触发：" + option.EventId);

            var fixture = EventResolverSmokeFixture.Build(catalog, option.EventId);

            var resolution = EventResolver.Resolve(
                new EventResolutionRequest(triggers[0].TriggerType, triggerKey, seed, fixture.Context),
                catalog, fixture.World, fixture.WorldState, new ExpeditionEventSessionState("smoke_session"));

            Assert.IsTrue(resolution.Success, "应能解析出实例：" + option.EventId + " → " + resolution.Diagnostic);
            return resolution.Instance;
        }

        [Test]
        public void Authority_GrantPersonRest_GoesThroughGateway_ForTheBoundPerson()
        {
            EventConfigDatabase database;
            string diagnostic;
            Assert.IsTrue(StaticConfigRegistry.TryGetEventConfig(out database, out diagnostic), diagnostic);

            var catalog = Project(database);
            var shapes = ScanShapes(catalog);
            Assert.IsTrue(shapes.GrantRest, "本用例需要内容里存在 GrantPersonRest");

            var instance = ResolveForOption(catalog, shapes.GrantRestOption, "smoke:grant_rest", SmokeSeed);

            EventOptionDefinition option;
            catalog.TryGetOption(shapes.GrantRestOption, out option);
            var fixture = EventResolverSmokeFixture.Build(catalog, option.EventId);

            var gateway = new EventEffectGatewayFixture(ingredients: 9999, food: 9999, supply: 999);
            var execution = new EventExecutionState();
            var worldState = new EventWorldState();

            var result = EventOptionExecutor.Execute(instance, shapes.GrantRestOption, catalog, fixture.World,
                worldState, new ExpeditionEventSessionState("smoke_session"), execution, gateway);

            Assert.IsTrue(result.Success, "GrantPersonRest 选项必须执行成功：" + result.Diagnostic
                + "\n" + result.Trace.ToText());

            Assert.AreEqual(1, gateway.GrantRestCalls, "GrantPersonRest 必须经 Gateway 下发（而不是直接写产品状态）");
            Assert.AreEqual(1, gateway.RestGrants.Count);
            Assert.IsNotEmpty(gateway.RestGrants[0], "下发对象必须是具体人物 Id");
            Assert.AreEqual(1, worldState.History.OccurrenceCount(instance.EventId));
        }

        [Test]
        public void Authority_NoEffectOption_Succeeds_AndRecordsHistory()
        {
            EventConfigDatabase database;
            string diagnostic;
            Assert.IsTrue(StaticConfigRegistry.TryGetEventConfig(out database, out diagnostic), diagnostic);

            var catalog = Project(database);
            var shapes = ScanShapes(catalog);
            Assert.IsTrue(shapes.NoEffect, "本用例需要内容里存在「无 Effect」的 Option");

            var instance = ResolveForOption(catalog, shapes.NoEffectOption, "smoke:no_effect", SmokeSeed);

            EventOptionDefinition option;
            catalog.TryGetOption(shapes.NoEffectOption, out option);
            var fixture = EventResolverSmokeFixture.Build(catalog, option.EventId);

            var gateway = new EventEffectGatewayFixture(ingredients: 9999, food: 9999, supply: 999);
            var worldState = new EventWorldState();
            var execution = new EventExecutionState();

            var result = EventOptionExecutor.Execute(instance, shapes.NoEffectOption, catalog, fixture.World,
                worldState, new ExpeditionEventSessionState("smoke_session"), execution, gateway);

            Assert.IsTrue(result.Success, "无 Effect 的 Option 也必须成功：" + result.Diagnostic);
            Assert.AreEqual(0, result.AppliedEffectCount);
            Assert.AreEqual(0, gateway.TotalMutationCalls, "无 Effect 不应调用任何 Gateway 提交入口");
            Assert.AreEqual(1, worldState.History.OccurrenceCount(instance.EventId), "仍必须记录 History");
            Assert.IsTrue(execution.IsCompleted(instance.EventInstanceId), "仍必须标记 Completed");
        }
    }
}
