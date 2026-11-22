using System;
using System.IO;
using System.Reflection;
using NUnit.Framework;

namespace RRPG.EventProduct.Tests
{
    /// <summary>
    /// **WP-Event-1 Producer Acceptance Repair**（制作人人工验收问题）的长期契约（EditMode，不依赖 Scene）。
    ///
    /// Issue A：Battle Encounter 卡「进入战斗」后不得被 Branch / 正式 Event 卡抢占；
    ///          Branch 投影的 Travel **绝不**弹 Encounter / Battle 确认卡（根因：曾把候选列表第一个节点当待进入节点）。
    /// Issue B：储备口粮三时点投影（S0 → S1 = S0+2 → S2 = S0+2−成本）必须与产品公式一致
    ///          （用于区分「真 SupplyDelta Bug」与「Test Content 表意：+2 但两人队净 0」）。
    /// Issue C：补给不足的 Option 必须在点击前置灰 + 短原因；预测路径必须**只读**。
    /// </summary>
    public class ProducerAcceptanceRepairTests
    {
        private const BindingFlags Flags =
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

        private static string SourcePath(string relativeUnderScripts)
        {
            return Path.Combine(ApplicationDataPath(), relativeUnderScripts.Replace('/', Path.DirectorySeparatorChar));
        }

        private static string ApplicationDataPath()
        {
            return UnityEngine.Application.dataPath;
        }

        private static string Slice(string source, string startMarker, string endMarker)
        {
            int start = source.IndexOf(startMarker, StringComparison.Ordinal);
            Assert.GreaterOrEqual(start, 0, "源码中找不到起点标记：" + startMarker);

            int end = source.IndexOf(endMarker, start + startMarker.Length, StringComparison.Ordinal);
            if (end < 0) end = source.Length;

            return source.Substring(start, end - start);
        }

        private static Type TypeOf(string name, string assembly)
        {
            var type = Type.GetType(name + ", " + assembly);
            Assert.IsNotNull(type, "类型不存在：" + name + " @" + assembly);
            return type;
        }

        // ==================== Issue A：Branch 投影不得弹 Encounter / Battle 确认卡 ====================

        [Test]
        public void PA_E1_BranchProjectionTravel_NeverPresentsEncounterCard()
        {
            var kindType = TypeOf("JourneyEncounterKind", "ExpeditionJourney.Runtime");
            var nodeType = TypeOf("JourneyNodeRef", "ExpeditionJourney.Runtime");
            var viewType = TypeOf("ExpeditionJourneyView", "Assembly-CSharp");

            var gate = viewType.GetMethod("ShouldPresentEncounterCard", Flags);
            Assert.IsNotNull(gate, "ExpeditionJourneyView.ShouldPresentEncounterCard 必须存在（Travel 完成闸口）");

            object battleNode = Activator.CreateInstance(nodeType, "node_battle", string.Empty,
                Enum.Parse(kindType, "Battle"));
            object eventNode = Activator.CreateInstance(nodeType, "node_event", string.Empty,
                Enum.Parse(kindType, "Event"));

            Assert.IsFalse((bool)gate.Invoke(null, new[] { battleNode, (object)true }),
                "Branch 投影（候选尚未选中）**绝不**弹 Encounter / Battle 确认卡 —— 这正是人工验收看到的"
                + "「点进入战斗却不进战斗、随后出现岔路口」的根因。");
            Assert.IsTrue((bool)gate.Invoke(null, new[] { battleNode, (object)false }),
                "玩家真正选中并进入 Battle 节点时，仍必须先显示「进入战斗」确认卡");
            Assert.IsFalse((bool)gate.Invoke(null, new[] { eventNode, (object)false }),
                "正式 Event 节点进入前不需要占位确认卡（卡面在原子进入成功后渲染）");

            // 源码级守卫：修复必须落在真实闸口与真实产品入口上（防止回退）
            string view = File.ReadAllText(SourcePath("Scripts/UI/ExpeditionJourneyView.cs"));
            StringAssert.Contains("ShouldPresentEncounterCard(_pendingConfirmNode, _travelIsBranchProjection)", view,
                "Travel 完成闸口必须按 Branch 投影区分（不得只看 RequiresEncounterConfirmation）");
            StringAssert.Contains("BeginTravel(target, onComplete, true)", view,
                "PresentTravelToBranch 必须以 Branch 投影语义开始 Travel");
            StringAssert.Contains("PresentationLockedForBattle", view,
                "Battle / Prep 期间必须有「不得出现新卡面」的锁");

            string coordinator = File.ReadAllText(SourcePath("Scripts/UI/ExpeditionJourneyCoordinator.cs"));
            StringAssert.Contains("view.LockPresentationForBattle()", coordinator,
                "Battle 真正开始（HideForEncounter）时必须上锁");
        }

        // ==================== Issue B：储备口粮三时点投影（与产品公式一致） ====================

        [Test]
        public void PA_E2_StoreRationsThreeStageProjection_MatchesProductFormula()
        {
            const int partySize = 2;            // 制作人当前队伍
            const int rate = 1;                 // 每人每天消耗
            const int initialCarry = 5;
            const int nodeElapsedDays = 0;
            const int extraDays = 1;            // 「储备口粮」= AdvanceExpeditionTime +1
            const int extraSupplyCost = 0;

            var supply = new ExpeditionSupplyState(partySize, initialCarry, rate);

            int s0 = supply.CurrentCarryUnits;
            Assert.AreEqual(5, s0, "S0 = Option 前补给");

            int cost = supply.ComputeTotalCost(nodeElapsedDays + extraDays, extraSupplyCost);
            Assert.AreEqual(2, cost, "总成本 = party × 总时间 × rate + ExtraSupplyCost = 2 × 1 × 1 + 0 = 2");

            // S1：Effect Commit（SupplyDelta +2）后、Node Settlement 前
            Assert.IsTrue(supply.TryApplyDelta(2), "SupplyDelta +2 必须能应用（产品正式增量入口）");
            int s1 = supply.CurrentCarryUnits;
            Assert.AreEqual(s0 + 2, s1, "期待 S1 = S0 + 2");

            // S2：Node Resolve 后（按 NodeElapsedDays + 额外天数结算）
            Assert.IsTrue(supply.CanAfford(cost), "S1 必须能承担本 Node 总成本");
            Assert.IsTrue(supply.TryConsume(cost), "结算必须扣减总成本");
            int s2 = supply.CurrentCarryUnits;
            Assert.AreEqual(s0 + 2 - cost, s2,
                "期待 S2 = S0 + 2 − party × (NodeElapsedDays + 1) × rate − ExtraSupplyCost");
            Assert.AreEqual(s0, s2,
                "两人队 + rate 1 时该 Test 内容净值为 0（+2 补给 / −2 时间成本）："
                + "「看起来没增加」是算术预期，不是 SupplyDelta Bug（Test / Not Frozen 表意问题）");
        }

        // ==================== Issue C：可行性格挡短原因 + 默认语义 ====================

        [Test]
        public void PA_E3_FeasibilityPreviewReason_IsShortAndActionable()
        {
            var previewType = TypeOf("EventOptionFeasibilityPreview", "Assembly-CSharp");
            var buildReason = previewType.GetMethod("BuildPlayerReason", Flags);
            Assert.IsNotNull(buildReason, "EventOptionFeasibilityPreview.BuildPlayerReason 必须存在");

            Assert.AreEqual("补给不足：本次行动至少还需要 2 点补给",
                (string)buildReason.Invoke(null, new object[] { 0, 2 }),
                "结算成本不足时必须给出「至少还差多少补给」的短原因");

            Assert.AreEqual("补给不足：本次行动至少还需要 2 点补给",
                (string)buildReason.Invoke(null, new object[] { -2, 0 }),
                "按 Order 执行后补给会变负时，同样给出需要补多少");

            var boxed = Activator.CreateInstance(previewType);
            var applicable = previewType.GetField("Applicable", Flags);
            Assert.IsNotNull(applicable, "EventOptionFeasibilityPreview.Applicable 必须存在");
            Assert.IsFalse((bool)applicable.GetValue(boxed),
                "默认 Applicable=false：调用方**不得**据此置灰（只有「证明必然失败」才置灰）");
        }

        // ==================== Issue C：预测必须只读 + 卡面必须点击前置灰 ====================

        [Test]
        public void PA_E4_FeasibilityPreview_IsReadOnly_AndDisablesBeforeClick()
        {
            string flow = File.ReadAllText(SourcePath("Scripts/EventHost/ExpeditionEventFlow.cs"));
            string preview = Slice(flow, "public static bool TryPreviewOptionFeasibility",
                "public static bool TryExecuteActiveOption");

            StringAssert.Contains("EventOptionExecutor.PreviewOutcome", preview,
                "预测必须复用既有只读 Outcome 预测（不复制判定逻辑）");
            StringAssert.Contains("TryEvaluateSettlementFeasibility", preview,
                "预测必须复用既有只读结算可行性检查（不复制判定逻辑）");
            StringAssert.DoesNotContain("EventOptionExecutor.Execute(", preview, "预测不得执行 Option");
            StringAssert.DoesNotContain("Commit(", preview, "预测不得 Commit");
            StringAssert.DoesNotContain("History.", preview, "预测不得写 History");
            StringAssert.DoesNotContain("TryConsume(", preview, "预测不得扣减补给");
            StringAssert.DoesNotContain("Generate", preview, "预测不得预生成阵容（不消耗 RNG）");

            string adapter = File.ReadAllText(SourcePath("Scripts/EventHost/ExpeditionEventCardAdapter.cs"));
            StringAssert.Contains("TryPreviewOptionFeasibility", adapter,
                "卡面构造必须做「点击前」只读可行性预测");
            StringAssert.Contains("preview.Applicable && !preview.Feasible", adapter,
                "只有「有确定性结论且不可行」时才置灰");

            string ui = File.ReadAllText(SourcePath("Scripts/UI/ExpeditionUI.cs"));
            StringAssert.Contains("if (!option.Interactable)", ui,
                "产品侧必须拒绝不可点 Option（点击路径零 mutation）");
        }
    }
}
