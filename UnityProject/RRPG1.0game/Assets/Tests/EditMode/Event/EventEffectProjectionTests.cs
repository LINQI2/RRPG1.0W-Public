using NUnit.Framework;
using RRPG.Event;
using static RRPG.Event.Tests.EventExecutionTestKit;
using static RRPG.Event.Tests.EventEffectTransactionTests;
using static RRPG.Event.Tests.EventResolverTestWorld;

namespace RRPG.Event.Tests
{
    /// <summary>
    /// WP-Event-1C：projected state / Terminal Effect 的运行时防御。覆盖：C12、C13。
    /// </summary>
    public class EventEffectProjectionTests
    {
        [Test]
        public void C12_ProjectedState_FirstEffect_Enables_SecondEffect()
        {
            // Effect 1: +20 Food；Effect 2: -30 Food；初始 Food=20。
            // 单独看 Effect 2 会失败，但按顺序整体合法 → 必须通过。
            var catalog = BuildCatalog("opt_chain", "Hide",
                Effect("eff_plus", "out_opt_chain", 10, "VillageResourceDelta", targetKey: "Food", intValue: 20),
                Effect("eff_minus", "out_opt_chain", 20, "VillageResourceDelta", targetKey: "Food", intValue: -30));

            var gateway = new EventEffectGatewayFixture(ingredients: 50, food: 20, supply: 20);
            var worldState = new EventWorldState();

            var result = Execute(catalog, "opt_chain", worldState, new EventExecutionState(), gateway);

            Assert.IsTrue(result.Success, "整体顺序合法就必须通过 Preflight：" + result.Diagnostic
                + "\n" + result.Trace.ToText());
            Assert.AreEqual(10, gateway.VillageResources["Food"], "20 + 20 - 30 = 10");

            // projected 证据：第二条效果看到的起点是 40（而不是初始 20）
            Assert.AreEqual(2, result.Effects.Count);
            Assert.AreEqual(40, result.Effects[1].ProjectedBefore);
            Assert.AreEqual(10, result.Effects[1].ProjectedAfter);
        }

        [Test]
        public void C12_OverallIllegal_IsRejected_WithZeroMutation()
        {
            var catalog = BuildCatalog("opt_break", "Hide",
                Effect("eff_a", "out_opt_break", 10, "VillageResourceDelta", targetKey: "Food", intValue: -30));

            var gateway = new EventEffectGatewayFixture(ingredients: 50, food: 20, supply: 20);
            var worldState = new EventWorldState();

            var result = Execute(catalog, "opt_break", worldState, new EventExecutionState(), gateway);

            Assert.IsFalse(result.Success);
            Assert.AreEqual(EventExecutionCode.EffectPreflightFailed, result.Code);
            Assert.AreEqual(20, gateway.VillageResources["Food"], "Preflight 失败不得 mutation");
            Assert.AreEqual(0, gateway.TotalMutationCalls);
            Assert.AreEqual(0, worldState.History.OccurrenceCount(EventId));
        }

        [Test]
        public void C13_StartBattle_NotLast_IsRejectedAtTwoLayers()
        {
            // 层 1（配置层）：Catalog 自身必须判定非法（StartBattle 不是最后一个效果）。
            var badCatalog = BuildCatalog("opt_terminal", "Hide",
                Effect("eff_battle", "out_opt_terminal", 10, "StartBattle", targetKey: "pool_test_general"),
                Effect("eff_after", "out_opt_terminal", 20, "SetFlag", targetKey: "flag_after"));

            Assert.IsFalse(badCatalog.IsValid, "配置层必须拒绝 StartBattle 不是最后一个效果的 Outcome");
            bool mentioned = false;
            foreach (string error in badCatalog.Errors)
            {
                if (error.Contains("StartBattle")) mentioned = true;
            }
            Assert.IsTrue(mentioned, "错误信息必须指出 StartBattle：" + string.Join(" | ", badCatalog.Errors));

            // 层 2（运行时防御）：即使拿到非法投影，执行也必须在 Plan 阶段显式失败且零 mutation。
            var goodCatalog = BuildCatalog("opt_terminal", "Hide",
                Effect("eff_after", "out_opt_terminal", 10, "SetFlag", targetKey: "flag_after"),
                Effect("eff_battle", "out_opt_terminal", 20, "StartBattle", targetKey: "pool_test_general"));

            var instance = ResolveInstance(goodCatalog);
            Assert.IsNotNull(instance, "合法配置必须能解析出实例");

            var gateway = new EventEffectGatewayFixture();
            var worldState = new EventWorldState();

            var result = EventOptionExecutor.Execute(instance, "opt_terminal", badCatalog,
                StandardWorld(), worldState, new ExpeditionEventSessionState(), new EventExecutionState(),
                gateway);

            Assert.IsFalse(result.Success);
            Assert.AreEqual(EventExecutionCode.InvalidRuntimeConfig, result.Code);
            StringAssert.Contains("StartBattle", result.Diagnostic);
            Assert.AreEqual(0, gateway.TotalMutationCalls, "计划阶段失败 → 不得 mutation");
            Assert.IsEmpty(worldState.Flags);
            Assert.AreEqual(0, worldState.History.OccurrenceCount(EventId));
        }

        [Test]
        public void C13_StartBattle_AsLastEffect_Succeeds_AndIsMarkedTerminal()
        {
            var catalog = BuildCatalog("opt_battle", "Hide",
                Effect("eff_supply", "out_opt_battle", 10, "SupplyDelta", intValue: -5),
                Effect("eff_battle", "out_opt_battle", 20, "StartBattle", targetKey: "pool_test_general"));

            var gateway = new EventEffectGatewayFixture(supply: 20);
            var worldState = new EventWorldState();

            var result = Execute(catalog, "opt_battle", worldState, new EventExecutionState(), gateway);

            Assert.IsTrue(result.Success, result.Diagnostic + "\n" + result.Trace.ToText());
            Assert.IsTrue(result.HasTerminalEffect);
            Assert.AreEqual(2, result.AppliedEffectCount);
            Assert.AreEqual(15, gateway.ExpeditionSupply);
            CollectionAssert.AreEqual(new[] { "pool_test_general" }, gateway.BattleStarts);
            Assert.AreEqual(EventExecutionCode.Success, result.Code);
        }
    }
}
