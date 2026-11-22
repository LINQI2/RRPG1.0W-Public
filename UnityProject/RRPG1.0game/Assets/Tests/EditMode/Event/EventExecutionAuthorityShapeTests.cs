using System.Collections.Generic;
using NUnit.Framework;
using RRPG.Event;
using RRPG.StaticConfig;

namespace RRPG.Event.Tests
{
    /// <summary>
    /// WP-Event-1C §27：当前两条 Test / Not Frozen 事件的**语义形状**必须真的走得通
    /// （形状从 Authority 推导，不写死事件 ID）：
    ///   - Village：一个 Option 下发 GrantPersonRest / 另一个 Option 无 Effect；
    ///   - Expedition：一个 Option 有多个 Outcome（70/30）/ 含 SupplyDelta / AdvanceExpeditionTime /
    ///     Battle Outcome 的 StartBattle 必须是 terminal。
    ///
    /// 全部经 Fixture Gateway：**不**碰真实 SocialResponseLog，**不**启动真实 Battle。
    /// </summary>
    public partial class EventExecutionAuthoritySmokeTests
    {
        private sealed class Shapes
        {
            public bool GrantRest;
            public bool NoEffect;
            public bool MultiOutcome;
            public bool SupplyDelta;
            public bool AdvanceTime;
            public bool Battle;

            public string GrantRestOption;
            public string NoEffectOption;
            public string MultiOutcomeOption;
            public string BattleOption;
        }

        private static Shapes ScanShapes(EventStaticCatalog catalog)
        {
            var shapes = new Shapes();

            foreach (var eventRecord in catalog.Events)
            {
                if (!eventRecord.Enabled) continue;

                foreach (var option in catalog.GetOptions(eventRecord.EventId))
                {
                    var outcomes = catalog.GetOutcomes(option.OptionId);
                    if (outcomes.Count > 1 && shapes.MultiOutcomeOption == null)
                    {
                        shapes.MultiOutcome = true;
                        shapes.MultiOutcomeOption = option.OptionId;
                    }

                    bool anyEffect = false;
                    foreach (var outcome in outcomes)
                    {
                        foreach (var effect in catalog.GetEffects(outcome.OutcomeId))
                        {
                            anyEffect = true;

                            if (string.Equals(effect.EffectType, EventEffectTypes.GrantPersonRest,
                                    System.StringComparison.Ordinal) && shapes.GrantRestOption == null)
                            {
                                shapes.GrantRest = true;
                                shapes.GrantRestOption = option.OptionId;
                            }
                            else if (string.Equals(effect.EffectType, EventEffectTypes.SupplyDelta,
                                         System.StringComparison.Ordinal))
                            {
                                shapes.SupplyDelta = true;
                            }
                            else if (string.Equals(effect.EffectType, EventEffectTypes.AdvanceExpeditionTime,
                                         System.StringComparison.Ordinal))
                            {
                                shapes.AdvanceTime = true;
                            }
                            else if (string.Equals(effect.EffectType, EventEffectTypes.StartBattle,
                                         System.StringComparison.Ordinal))
                            {
                                shapes.Battle = true;
                                if (shapes.BattleOption == null) shapes.BattleOption = option.OptionId;
                            }
                        }
                    }

                    if (!anyEffect && shapes.NoEffectOption == null)
                    {
                        shapes.NoEffect = true;
                        shapes.NoEffectOption = option.OptionId;
                    }
                }
            }

            return shapes;
        }

        [Test]
        public void Authority_TodayContent_HasTheExpectedSemanticShapes()
        {
            EventConfigDatabase database;
            string diagnostic;
            Assert.IsTrue(StaticConfigRegistry.TryGetEventConfig(out database, out diagnostic), diagnostic);

            var shapes = ScanShapes(Project(database));

            Assert.IsTrue(shapes.GrantRest, "当前 Village 内容应包含 GrantPersonRest（Option 1：下发恢复）");
            Assert.IsTrue(shapes.NoEffect, "当前 Village 内容应有一个无 Effect 的 Option（Option 2：只记录选择）");
            Assert.IsTrue(shapes.MultiOutcome, "当前 Expedition 内容应有一个多 Outcome 的 Option（调查 70/30）");
            Assert.IsTrue(shapes.SupplyDelta, "当前 Expedition 内容应包含 SupplyDelta");
            Assert.IsTrue(shapes.AdvanceTime, "当前 Expedition 内容应包含 AdvanceExpeditionTime");
            Assert.IsTrue(shapes.Battle, "当前 Expedition 内容应包含 StartBattle");
        }
    }
}
