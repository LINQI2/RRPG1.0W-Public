using System.Collections.Generic;
using NUnit.Framework;

namespace RRPG.EventProduct.Tests
{
    /// <summary>
    /// WP-Event-1D · Gate D0 §22/§23：Event 额外远征时间必须与 Supply **同一次 exactly-once 结算**。
    /// </summary>
    public class ExpeditionEventTimeTests
    {
        private const int PartySize = 2;
        private const int PerPersonRate = 1;
        private const int NodeElapsedDays = 2;
        private const int BaseCostPerDay = PartySize * PerPersonRate;   // = 2

        private static ExpeditionProgressionResolver NewSession(int initialSupply, string nodeId = "node_event",
            int extraCost = 0)
        {
            var node = new ExpeditionNodeData(nodeId, ExpeditionNodeKind.Event,
                connections: null, nodeElapsedDays: NodeElapsedDays, extraSupplyCost: extraCost);
            var graph = new ExpeditionGraph(nodeId, new List<ExpeditionNodeData> { node });

            var resolver = new ExpeditionProgressionResolver();
            Assert.AreEqual(ExpeditionProgressionInitResult.Success,
                resolver.InitializeProgression(graph, PartySize, initialSupply, PerPersonRate));
            Assert.AreEqual(ExpeditionEnterResult.Success, resolver.TryEnterNode(nodeId));
            return resolver;
        }

        [Test]
        public void AdditionalElapsedDays_SettlesTimeAndSupplyExactlyOnce()
        {
            var resolver = NewSession(initialSupply: 100);

            ExpeditionNodeResolutionData data;
            var result = resolver.TryResolveCurrentNode(ExpeditionNodeOutcome.Success, 1, out data);

            Assert.AreEqual(ExpeditionResolveResult.Success, result, "额外 1 天必须可结算");
            Assert.AreEqual(1, data.AdditionalElapsedDays);
            Assert.AreEqual(NodeElapsedDays + 1, data.TotalElapsedDaysSettled);
            Assert.AreEqual(NodeElapsedDays + 1, resolver.State.ExpeditionElapsedDays, "elapsed 只加一次总时间");
            Assert.AreEqual(BaseCostPerDay * (NodeElapsedDays + 1), data.TotalSupplyCost,
                "基础消耗必须按 TotalElapsedDays 计算（Event 加时间必须真的消耗补给）");
            Assert.AreEqual(100 - BaseCostPerDay * (NodeElapsedDays + 1), resolver.State.Supply.CurrentCarryUnits);

            // exactly-once：重复 Resolve 不再结算
            ExpeditionNodeResolutionData again;
            Assert.AreEqual(ExpeditionResolveResult.AlreadyResolved,
                resolver.TryResolveCurrentNode(ExpeditionNodeOutcome.Success, 1, out again));
            Assert.AreEqual(NodeElapsedDays + 1, resolver.State.ExpeditionElapsedDays);
        }

        [Test]
        public void AdditionalElapsedDays_Zero_IsEquivalentToLegacyBehaviour()
        {
            var resolver = NewSession(initialSupply: 100);

            ExpeditionNodeResolutionData data;
            Assert.AreEqual(ExpeditionResolveResult.Success,
                resolver.TryResolveCurrentNode(ExpeditionNodeOutcome.Success, 0, out data));

            Assert.AreEqual(0, data.AdditionalElapsedDays);
            Assert.AreEqual(NodeElapsedDays, data.TotalElapsedDaysSettled);
            Assert.AreEqual(BaseCostPerDay * NodeElapsedDays, data.TotalSupplyCost);
        }

        [Test]
        public void AdditionalElapsedDays_Negative_IsRejected_WithoutSettlement()
        {
            var resolver = NewSession(initialSupply: 100);

            ExpeditionNodeResolutionData data;
            Assert.AreEqual(ExpeditionResolveResult.InvalidAdditionalElapsedDays,
                resolver.TryResolveCurrentNode(ExpeditionNodeOutcome.Success, -1, out data));

            Assert.IsNull(data);
            Assert.AreEqual(0, resolver.State.ExpeditionElapsedDays, "拒绝时不得结算 elapsed");
            Assert.AreEqual(100, resolver.State.Supply.CurrentCarryUnits, "拒绝时不得扣补给");
            Assert.IsFalse(resolver.State.IsNodeResolved("node_event"), "拒绝时 Node 不得被标记已结算");
        }

        [Test]
        public void NotAffordableWithAdditionalTime_IsRejected_WithoutSoftLock()
        {
            // 初始补给刚好够 Node 本身（4）但不够「Node + 额外 1 天」（6）→ 必须拒绝，而不是软锁。
            var resolver = NewSession(initialSupply: 5);

            ExpeditionNodeResolutionData data;
            Assert.AreEqual(ExpeditionResolveResult.InsufficientSupply,
                resolver.TryResolveCurrentNode(ExpeditionNodeOutcome.Success, 1, out data));

            Assert.IsNull(data);
            Assert.AreEqual(5, resolver.State.Supply.CurrentCarryUnits);
            Assert.AreEqual(0, resolver.State.ExpeditionElapsedDays);
            Assert.IsTrue(resolver.State.IsNodeEntered("node_event"), "Node 仍处于已进入未结算（可重试其它选项）");

            // 不带额外时间仍可正常结算 → 不产生软锁
            Assert.AreEqual(ExpeditionResolveResult.Success,
                resolver.TryResolveCurrentNode(ExpeditionNodeOutcome.Success, 0, out data));
            Assert.AreEqual(BaseCostPerDay * NodeElapsedDays, data.TotalSupplyCost);
        }

        [Test]
        public void ExtraSupplyCost_IsStillAddedOnce()
        {
            var resolver = NewSession(initialSupply: 100, extraCost: 3);

            ExpeditionNodeResolutionData data;
            Assert.AreEqual(ExpeditionResolveResult.Success,
                resolver.TryResolveCurrentNode(ExpeditionNodeOutcome.Success, 1, out data));

            Assert.AreEqual(3, data.ExtraSupplyCostApplied);
            Assert.AreEqual(BaseCostPerDay * (NodeElapsedDays + 1) + 3, data.TotalSupplyCost);
        }
    }
}
