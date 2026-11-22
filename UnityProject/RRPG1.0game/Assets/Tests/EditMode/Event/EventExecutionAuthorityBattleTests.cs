using System.Collections.Generic;
using System;
using NUnit.Framework;
using RRPG.Event;
using RRPG.StaticConfig;

namespace RRPG.Event.Tests
{
    /// <summary>WP-Event-1C §27：多 Outcome 覆盖与 Battle Outcome 的 Terminal StartBattle（经 Fixture Gateway）。</summary>
    public partial class EventExecutionAuthoritySmokeTests
    {
        [Test]
        public void Authority_MultiOutcomeOption_CoversAllLegalOutcomes()
        {
            EventConfigDatabase database;
            string diagnostic;
            Assert.IsTrue(StaticConfigRegistry.TryGetEventConfig(out database, out diagnostic), diagnostic);

            var catalog = Project(database);
            var shapes = ScanShapes(catalog);
            Assert.IsTrue(shapes.MultiOutcome, "本用例需要内容里存在多 Outcome 的 Option");

            EventOptionDefinition option;
            catalog.TryGetOption(shapes.MultiOutcomeOption, out option);

            var legal = new HashSet<string>(StringComparer.Ordinal);
            foreach (var outcome in catalog.GetOutcomes(shapes.MultiOutcomeOption))
            {
                legal.Add(outcome.OutcomeId);
            }
            Assert.Greater(legal.Count, 1, "多 Outcome 选项应至少有 2 个合法 Outcome");

            var seen = new HashSet<string>(StringComparer.Ordinal);

            for (int i = 0; i < 60; i++)
            {
                var instance = ResolveForOption(catalog, shapes.MultiOutcomeOption,
                    "smoke:multi:" + option.EventId + ":" + i, i);

                var preview = EventOptionExecutor.PreviewOutcome(instance, shapes.MultiOutcomeOption, catalog);
                Assert.IsNotNull(preview);
                Assert.IsTrue(legal.Contains(preview.OutcomeId),
                    "抽到的 Outcome 必须在配置的合法集合内：" + preview.OutcomeId);

                Assert.AreEqual(preview.OutcomeId,
                    EventOptionExecutor.PreviewOutcome(instance, shapes.MultiOutcomeOption, catalog).OutcomeId,
                    "同一个实例的结果必须稳定");

                seen.Add(preview.OutcomeId);
            }

            CollectionAssert.AreEquivalent(new List<string>(legal), new List<string>(seen),
                "60 个不同实例应覆盖该 Option 的全部合法 Outcome");
        }

        [Test]
        public void Authority_BattleOutcome_StartBattle_IsTerminal_AndStagedViaGateway()
        {
            EventConfigDatabase database;
            string diagnostic;
            Assert.IsTrue(StaticConfigRegistry.TryGetEventConfig(out database, out diagnostic), diagnostic);

            var catalog = Project(database);
            var shapes = ScanShapes(catalog);
            Assert.IsTrue(shapes.Battle, "本用例需要内容里存在 StartBattle");

            EventOptionDefinition option;
            catalog.TryGetOption(shapes.BattleOption, out option);

            bool exercised = false;
            string battleFailure = null;

            for (int i = 0; i < 200 && !exercised; i++)
            {
                var instance = ResolveForOption(catalog, shapes.BattleOption,
                    "smoke:battle:" + option.EventId + ":" + i, i);

                var selected = EventOptionExecutor.PreviewOutcome(instance, shapes.BattleOption, catalog);
                if (selected == null) continue;

                EventEffectDefinition battleEffect = null;
                foreach (var effect in catalog.GetEffects(selected.OutcomeId))
                {
                    if (string.Equals(effect.EffectType, EventEffectTypes.StartBattle, StringComparison.Ordinal))
                    {
                        battleEffect = effect;
                    }
                }
                if (battleEffect == null) continue;   // 该实例抽到的是非战斗 Outcome：合法，继续找

                var fixture = EventResolverSmokeFixture.Build(catalog, option.EventId);
                var gateway = new EventEffectGatewayFixture(ingredients: 9999, food: 9999, supply: 999);
                var worldState = new EventWorldState();

                var result = EventOptionExecutor.Execute(instance, shapes.BattleOption, catalog, fixture.World,
                    worldState, new ExpeditionEventSessionState("smoke_session"), new EventExecutionState(),
                    gateway);

                if (!result.Success)
                {
                    battleFailure = result.Code + " / " + result.Diagnostic + "\n" + result.Trace.ToText();
                    continue;
                }

                Assert.IsTrue(result.HasTerminalEffect, "战斗 Outcome 必须包含 Terminal Effect");
                Assert.IsTrue(result.Effects[result.Effects.Count - 1].IsTerminal,
                    "StartBattle 必须是该 Outcome 的最后一个效果");
                Assert.AreEqual(1, gateway.BattleStarts.Count, "StartBattle 必须经 Gateway 请求（不启动真实 Battle）");
                Assert.AreEqual(battleEffect.TargetKey, gateway.BattleStarts[0]);
                exercised = true;
            }

            Assert.IsTrue(exercised, "应在限次内抽到战斗 Outcome 并走通（未走通说明内容/配置有问题）"
                + (battleFailure == null ? string.Empty : "；一次失败样例：" + battleFailure));
        }
    }
}
