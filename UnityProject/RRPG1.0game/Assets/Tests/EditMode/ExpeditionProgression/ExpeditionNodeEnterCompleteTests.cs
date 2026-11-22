using System.Linq;
using NUnit.Framework;

/// <summary>
/// Enter / Resolve / Complete 语义（FDS §7.3 与制作人修订约束）：
/// Enter ≠ Resolve/Complete；合法/非法进入；Enter 前 affordability validation；
/// Victory → Completed + 开放后继；Defeat → Resolved-not-Completed、不开放后继。
/// </summary>
public class ExpeditionNodeEnterCompleteTests
{
    [Test]
    public void EnterEntryNode_SetsEntered_NoElapsedNoSupplySettlement()
    {
        var resolver = ExpeditionProgressionTestKit.NewResolver(
            ExpeditionProgressionTestKit.Chain("n", 3, elapsedDays: 1), 1, 100, 1);

        Assert.AreEqual(ExpeditionEnterResult.Success, resolver.TryEnterNode("n0"));
        var state = resolver.State;
        Assert.AreEqual(ExpeditionProgressionPhase.Entered, state.Phase);
        Assert.AreEqual("n0", state.CurrentNodeId);
        Assert.IsTrue(state.IsNodeEntered("n0"));
        Assert.IsFalse(state.IsNodeResolved("n0"), "Enter 不等于 Resolve");
        Assert.IsFalse(state.IsNodeCompleted("n0"), "Enter 不等于 Complete");
        Assert.AreEqual(0, state.ExpeditionElapsedDays, "Enter 不结算 elapsed");
        Assert.AreEqual(100, state.Supply.CurrentCarryUnits, "Enter 不扣减 Supply");
        Assert.AreEqual(0, state.GetResolvedNodeIds().Count);
    }

    [Test]
    public void TryEnter_NonEntryNode_BeforeStart_IsNotAvailableNow()
    {
        var resolver = ExpeditionProgressionTestKit.NewResolver(
            ExpeditionProgressionTestKit.Chain("n", 3));
        Assert.AreEqual(ExpeditionEnterResult.NotAvailableNow, resolver.TryEnterNode("n1"));
        Assert.AreEqual(ExpeditionProgressionPhase.NotStarted, resolver.State.Phase);
        Assert.AreEqual(0, resolver.State.GetEnteredNodeIds().Count);
    }

    [Test]
    public void TryEnter_SameNodeTwice_SecondReturnsAlreadyEntered()
    {
        var resolver = ExpeditionProgressionTestKit.NewResolver(
            ExpeditionProgressionTestKit.Chain("n", 2));
        Assert.AreEqual(ExpeditionEnterResult.Success, resolver.TryEnterNode("n0"));
        Assert.AreEqual(ExpeditionEnterResult.AlreadyEntered, resolver.TryEnterNode("n0"));
    }

    [Test]
    public void AfterVictory_OnlySuccessorsAreEnterable()
    {
        var graph = ExpeditionProgressionTestKit.SingleEntryGraph(
            ExpeditionProgressionTestKit.Node("n0", next: new[] { "n1", "n2" }),
            ExpeditionProgressionTestKit.Node("n1", next: new[] { "n3" }),
            ExpeditionProgressionTestKit.Node("n2"),
            ExpeditionProgressionTestKit.Node("n3"));
        var resolver = ExpeditionProgressionTestKit.NewResolver(graph, 1, 100, 1);

        ExpeditionProgressionTestKit.EnterAndSettle(resolver, "n0", ExpeditionNodeOutcome.Success);

        CollectionAssert.AreEquivalent(new[] { "n1", "n2" },
            ExpeditionProgressionTestKit.AvailableIds(resolver));
        Assert.AreEqual(ExpeditionEnterResult.AlreadyCompleted, resolver.TryEnterNode("n0"),
            "已完成 Node 不可再进入");
        Assert.AreEqual(ExpeditionEnterResult.NotAvailableNow, resolver.TryEnterNode("n3"),
            "非上一完成 Node 后继不可直接进入");
        Assert.AreEqual(ExpeditionEnterResult.Success, resolver.TryEnterNode("n1"));
    }

    [Test]
    public void VictoryResolve_CompletesNode_OpensSuccessors()
    {
        var resolver = ExpeditionProgressionTestKit.NewResolver(
            ExpeditionProgressionTestKit.Chain("n", 3, elapsedDays: 1), 1, 100, 1);
        ExpeditionProgressionTestKit.EnterAndSettle(resolver, "n0", ExpeditionNodeOutcome.Success);

        var state = resolver.State;
        Assert.IsTrue(state.IsNodeResolved("n0"));
        Assert.IsTrue(state.IsNodeCompleted("n0"));
        Assert.AreEqual(ExpeditionProgressionPhase.AwaitingNext, state.Phase);
        Assert.AreEqual(1, state.ExpeditionElapsedDays);
        CollectionAssert.AreEqual(new[] { "n1" }, ExpeditionProgressionTestKit.AvailableIds(resolver));
    }

    [Test]
    public void DefeatResolve_ResolvedButNotCompleted_NoSuccessors()
    {
        var resolver = ExpeditionProgressionTestKit.NewResolver(
            ExpeditionProgressionTestKit.Chain("n", 3, elapsedDays: 2), 1, 100, 1);
        ExpeditionProgressionTestKit.EnterAndSettle(resolver, "n0", ExpeditionNodeOutcome.Failure);

        var state = resolver.State;
        Assert.IsTrue(state.IsNodeResolved("n0"), "Defeat 也是已发生的最终 outcome，必须结算");
        Assert.IsFalse(state.IsNodeCompleted("n0"), "Defeat 不得标记 Node Completed");
        Assert.AreEqual(ExpeditionProgressionPhase.FailedResolved, state.Phase);
        Assert.AreEqual(0, ExpeditionProgressionTestKit.AvailableIds(resolver).Count,
            "Defeat 不开放普通后继");
    }

    [Test]
    public void Resolve_WithoutAnyEnter_IsRejected()
    {
        var resolver = ExpeditionProgressionTestKit.NewResolver(
            ExpeditionProgressionTestKit.Chain("n", 2));
        // 尚无任何进入的 Node → 无可结算的当前内容。
        Assert.AreEqual(ExpeditionResolveResult.NoCurrentNode,
            resolver.TryResolveCurrentNode(ExpeditionNodeOutcome.Success, out _));
    }

    [Test]
    public void Resolve_Twice_SecondIsAlreadyResolved_NoDoubleSettlement()
    {
        var resolver = ExpeditionProgressionTestKit.NewResolver(
            ExpeditionProgressionTestKit.Chain("n", 2, elapsedDays: 3), 1, 100, 1);
        Assert.AreEqual(ExpeditionEnterResult.Success, resolver.TryEnterNode("n0"));

        Assert.AreEqual(ExpeditionResolveResult.Success,
            resolver.TryResolveCurrentNode(ExpeditionNodeOutcome.Success, out var first));
        Assert.AreEqual(3, first.NodeElapsedDaysSettled);

        Assert.AreEqual(ExpeditionResolveResult.AlreadyResolved,
            resolver.TryResolveCurrentNode(ExpeditionNodeOutcome.Success, out _));
        Assert.AreEqual(3, resolver.State.ExpeditionElapsedDays, "重复 Resolve 不得重复累计 elapsed");
    }

    [Test]
    public void Enter_PreKnownCostNotAffordable_RejectedBeforeContentStarts()
    {
        // Party 2 × days 1 × rate 1 = 2 > carry 1 → 进入前拒绝，内容不启动。
        var resolver = ExpeditionProgressionTestKit.NewResolver(
            ExpeditionProgressionTestKit.Chain("n", 2, elapsedDays: 1), 2, 1, 1);

        Assert.AreEqual(ExpeditionEnterResult.InsufficientSupply, resolver.TryEnterNode("n0"));
        var state = resolver.State;
        Assert.AreEqual(ExpeditionProgressionPhase.NotStarted, state.Phase, "内容不得启动");
        Assert.AreEqual(0, state.GetEnteredNodeIds().Count);
        Assert.AreEqual(0, state.ExpeditionElapsedDays);
        Assert.AreEqual(1, state.Supply.CurrentCarryUnits, "不足拒绝不得扣减");
    }

    [Test]
    public void RequestReturn_WhileNodeEnteredUnresolved_IsRejected()
    {
        var resolver = ExpeditionProgressionTestKit.NewResolver(
            ExpeditionProgressionTestKit.Chain("n", 2, elapsedDays: 1));
        Assert.AreEqual(ExpeditionEnterResult.Success, resolver.TryEnterNode("n0"));
        Assert.AreEqual(ExpeditionReturnResult.NodePendingResolution, resolver.RequestReturn());
        Assert.AreEqual(ExpeditionProgressionPhase.Entered, resolver.State.Phase,
            "未结算内容前不允许跳过 Return");
    }
}
