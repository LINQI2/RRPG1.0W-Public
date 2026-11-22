using System.Collections.Generic;
using NUnit.Framework;

/// <summary>
/// integer ExpeditionElapsedDays exactly-once（FDS §5 Y5 契约）：
/// Victory / Defeat 的最终 outcome 都只结算一次；重复 Resolve 不重复累计；
/// Node count ≠ elapsed（0 天 Node 证明）。
/// </summary>
public class ExpeditionElapsedDaysExactlyOnceTests
{
    private static ExpeditionGraph DaysChain(params int[] days)
    {
        var nodes = new List<ExpeditionNodeData>();
        for (int i = 0; i < days.Length; i++)
        {
            string id = "d" + i;
            string[] next = i < days.Length - 1 ? new[] { "d" + (i + 1) } : new string[0];
            nodes.Add(ExpeditionProgressionTestKit.Node(id, ExpeditionNodeKind.Battle,
                elapsedDays: days[i], extraCost: 0, successfulRouteTerminal: false, next: next));
        }
        return ExpeditionProgressionTestKit.SingleEntryGraph(nodes.ToArray());
    }

    private static ExpeditionProgressionResolver SettleSuccessive(
        ExpeditionProgressionResolver resolver, params string[] nodeIds)
    {
        foreach (var id in nodeIds)
            ExpeditionProgressionTestKit.EnterAndSettle(resolver, id, ExpeditionNodeOutcome.Success);
        return resolver;
    }

    [Test]
    public void Victory_NodeElapsedDays_AccumulatedExactlyOnce()
    {
        var resolver = ExpeditionProgressionTestKit.NewResolver(
            ExpeditionProgressionTestKit.Chain("n", 2, elapsedDays: 2), 1, 100, 1);
        Assert.AreEqual(ExpeditionEnterResult.Success, resolver.TryEnterNode("n0"));
        Assert.AreEqual(0, resolver.State.ExpeditionElapsedDays, "Enter 本身不累计 elapsed");

        Assert.AreEqual(ExpeditionResolveResult.Success,
            resolver.TryResolveCurrentNode(ExpeditionNodeOutcome.Success, out var data));
        Assert.AreEqual(2, data.NodeElapsedDaysSettled);
        Assert.AreEqual(2, data.ExpeditionElapsedDaysTotal);
        Assert.AreEqual(2, resolver.State.ExpeditionElapsedDays);
        Assert.IsTrue(data.Completed);

        // 重复 Resolve（UI 重开 / 重复调用）不得二次累计。
        Assert.AreEqual(ExpeditionResolveResult.AlreadyResolved,
            resolver.TryResolveCurrentNode(ExpeditionNodeOutcome.Success, out _));
        Assert.AreEqual(2, resolver.State.ExpeditionElapsedDays, "exactly-once：不得重复累计");
    }

    [Test]
    public void Defeat_NodeElapsedDays_AccumulatedExactlyOnce()
    {
        var resolver = ExpeditionProgressionTestKit.NewResolver(
            ExpeditionProgressionTestKit.Chain("n", 2, elapsedDays: 2), 1, 100, 1);
        ExpeditionProgressionTestKit.EnterAndSettle(resolver, "n0", ExpeditionNodeOutcome.Failure);

        Assert.AreEqual(2, resolver.State.ExpeditionElapsedDays,
            "Defeat 也必须结算这场已经发生的 NodeElapsedDays");
        Assert.IsFalse(resolver.State.IsNodeCompleted("n0"), "Defeat 不标记 Completed");
        Assert.AreEqual(ExpeditionProgressionPhase.FailedResolved, resolver.State.Phase);
    }

    [Test]
    public void ZeroDayNodes_ManyCompletions_ElapsedStaysZero_NodeCountIsNotElapsed()
    {
        var resolver = ExpeditionProgressionTestKit.NewResolver(DaysChain(0, 0, 0), 1, 100, 1);
        SettleSuccessive(resolver, "d0", "d1", "d2");

        Assert.AreEqual(3, resolver.State.GetCompletedNodeIds().Count, "完成了 3 个 Node");
        Assert.AreEqual(0, resolver.State.ExpeditionElapsedDays,
            "Node count ≠ elapsed days：0 天 Node 不累计任何 Village Day");
    }

    [Test]
    public void MixedDaysChain_ElapsedEqualsSumOfCompletedNodeDays()
    {
        var resolver = ExpeditionProgressionTestKit.NewResolver(DaysChain(1, 0, 3, 2), 1, 100, 1);
        SettleSuccessive(resolver, "d0", "d1", "d2", "d3");

        Assert.AreEqual(6, resolver.State.ExpeditionElapsedDays, "1+0+3+2 = 6");
        Assert.AreEqual(4, resolver.State.GetCompletedNodeIds().Count);
    }

    [Test]
    public void RepeatedResolve_Calls_DoNotReaccumulateElapsed()
    {
        var resolver = ExpeditionProgressionTestKit.NewResolver(
            ExpeditionProgressionTestKit.Chain("n", 2, elapsedDays: 2), 1, 100, 1);
        ExpeditionProgressionTestKit.EnterAndSettle(resolver, "n0", ExpeditionNodeOutcome.Success);

        for (int i = 0; i < 5; i++)
        {
            Assert.AreEqual(ExpeditionResolveResult.AlreadyResolved,
                resolver.TryResolveCurrentNode(ExpeditionNodeOutcome.Success, out _));
        }
        Assert.AreEqual(2, resolver.State.ExpeditionElapsedDays);
        Assert.AreEqual(98, resolver.State.Supply.CurrentCarryUnits, "重复 Resolve 不得重复扣 Supply");
    }
}
