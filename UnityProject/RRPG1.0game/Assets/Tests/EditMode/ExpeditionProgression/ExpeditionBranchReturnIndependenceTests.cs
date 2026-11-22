using System.Linq;
using NUnit.Framework;

/// <summary>
/// Branch 0/1/N 与 Return 独立性（FDS §8 / 制作人修订约束 1）：
/// 0 successor 只表示 progression terminal，不得自动等于 RouteCompleted / SafeReturn；
/// RouteCompleted 只由显式 SuccessfulRouteTerminal 的成功 outcome 产生；
/// Return 是 Expedition 层独立操作，不属于任何 Node 后继。
/// </summary>
public class ExpeditionBranchReturnIndependenceTests
{
    private static ExpeditionProgressionResolver ForkResolver()
    {
        var graph = ExpeditionProgressionTestKit.SingleEntryGraph(
            ExpeditionProgressionTestKit.Node("fork", ExpeditionNodeKind.Battle,
                elapsedDays: 1, extraCost: 0, successfulRouteTerminal: false,
                next: new[] { "n1", "n2" }),
            ExpeditionProgressionTestKit.Node("n1", next: new string[0]),
            ExpeditionProgressionTestKit.Node("n2", next: new string[0]));
        return ExpeditionProgressionTestKit.NewResolver(graph, 1, 100, 1);
    }

    [Test]
    public void ZeroSuccessor_DoesNotAutoRouteCompleted_NorAutoVictory()
    {
        // 无后继且未显式标记 SuccessfulRouteTerminal：成功完成只表示没有普通后继。
        var graph = ExpeditionProgressionTestKit.SingleEntryGraph(
            ExpeditionProgressionTestKit.Node("end", elapsedDays: 1, successfulRouteTerminal: false));
        var resolver = ExpeditionProgressionTestKit.NewResolver(graph, 1, 100, 1);

        ExpeditionProgressionTestKit.EnterAndSettle(resolver, "end", ExpeditionNodeOutcome.Success);

        var state = resolver.State;
        Assert.IsTrue(state.IsNodeCompleted("end"));
        Assert.AreEqual(0, ExpeditionProgressionTestKit.AvailableIds(resolver).Count,
            "0 后继 = progression terminal");
        Assert.IsFalse(state.RouteCompleted,
            "0 successor 本身不得推导 RouteCompleted / SafeReturn");
        Assert.AreEqual(ExpeditionProgressionPhase.AwaitingNext, state.Phase);
    }

    [Test]
    public void SuccessfulRouteTerminal_SuccessOutcome_SetsRouteCompleted()
    {
        var graph = ExpeditionProgressionTestKit.SingleEntryGraph(
            ExpeditionProgressionTestKit.Node("boss", ExpeditionNodeKind.Boss,
                elapsedDays: 1, successfulRouteTerminal: true));
        var resolver = ExpeditionProgressionTestKit.NewResolver(graph, 1, 100, 1);

        ExpeditionProgressionTestKit.EnterAndSettle(resolver, "boss", ExpeditionNodeOutcome.Success);

        Assert.IsTrue(resolver.State.RouteCompleted,
            "显式 SuccessfulRouteTerminal 的成功 outcome 才置 RouteCompleted");
        Assert.AreEqual(ExpeditionProgressionPhase.AwaitingNext, resolver.State.Phase);
        Assert.AreEqual(0, ExpeditionProgressionTestKit.AvailableIds(resolver).Count);
    }

    [Test]
    public void SuccessfulRouteTerminal_DefeatOutcome_DoesNotSetRouteCompleted()
    {
        var graph = ExpeditionProgressionTestKit.SingleEntryGraph(
            ExpeditionProgressionTestKit.Node("boss", ExpeditionNodeKind.Boss,
                elapsedDays: 1, successfulRouteTerminal: true));
        var resolver = ExpeditionProgressionTestKit.NewResolver(graph, 1, 100, 1);

        ExpeditionProgressionTestKit.EnterAndSettle(resolver, "boss", ExpeditionNodeOutcome.Failure);

        Assert.IsFalse(resolver.State.RouteCompleted,
            "Defeat 不得产生 RouteCompleted / SafeReturn 资格");
        Assert.IsFalse(resolver.State.IsNodeCompleted("boss"));
        Assert.AreEqual(ExpeditionProgressionPhase.FailedResolved, resolver.State.Phase);
    }

    [Test]
    public void OneSuccessor_NoBranchChoiceNeeded_AvailableIsSingle()
    {
        var resolver = ExpeditionProgressionTestKit.NewResolver(
            ExpeditionProgressionTestKit.Chain("n", 3, elapsedDays: 1), 1, 100, 1);
        ExpeditionProgressionTestKit.EnterAndSettle(resolver, "n0", ExpeditionNodeOutcome.Success);
        CollectionAssert.AreEqual(new[] { "n1" }, ExpeditionProgressionTestKit.AvailableIds(resolver),
            "N=1 不制造无意义选择");
    }

    [Test]
    public void ManySuccessors_OnlyLegalSuccessorsAreCandidates()
    {
        var resolver = ForkResolver();
        ExpeditionProgressionTestKit.EnterAndSettle(resolver, "fork", ExpeditionNodeOutcome.Success);
        CollectionAssert.AreEquivalent(new[] { "n1", "n2" },
            ExpeditionProgressionTestKit.AvailableIds(resolver));
    }

    [Test]
    public void Branch_ToNodeOutsideCandidates_IsRejected()
    {
        var resolver = ForkResolver();
        ExpeditionProgressionTestKit.EnterAndSettle(resolver, "fork", ExpeditionNodeOutcome.Success);
        // 不存在的 / 已完成 / 非候选的 Node 都不得作为分支进入。
        Assert.AreEqual(ExpeditionEnterResult.NoSuchNode, resolver.TryChooseBranch("nope"));
        Assert.AreEqual(ExpeditionEnterResult.AlreadyCompleted, resolver.TryChooseBranch("fork"));
    }

    [Test]
    public void Return_IsIndependentFromBranch_AvailableBetweenNodes()
    {
        var resolver = ForkResolver();
        ExpeditionProgressionTestKit.EnterAndSettle(resolver, "fork", ExpeditionNodeOutcome.Success);
        Assert.AreEqual(2, ExpeditionProgressionTestKit.AvailableIds(resolver).Count,
            "存在 N>1 合法后继，但玩家仍可随时 Return");

        Assert.AreEqual(ExpeditionReturnResult.Success, resolver.RequestReturn());
        Assert.AreEqual(ExpeditionProgressionPhase.ReturnRequested, resolver.State.Phase);
        Assert.AreEqual(0, ExpeditionProgressionTestKit.AvailableIds(resolver).Count,
            "ReturnRequested 后不再提供普通推进候选");
    }

    [Test]
    public void AfterReturnRequested_NoFurtherEnterAllowed()
    {
        var resolver = ExpeditionProgressionTestKit.NewResolver(
            ExpeditionProgressionTestKit.Chain("n", 2, elapsedDays: 1), 1, 100, 1);
        ExpeditionProgressionTestKit.EnterAndSettle(resolver, "n0", ExpeditionNodeOutcome.Success);
        Assert.AreEqual(ExpeditionReturnResult.Success, resolver.RequestReturn());
        Assert.AreEqual(ExpeditionEnterResult.WrongPhase, resolver.TryEnterNode("n1"));
        Assert.AreEqual(ExpeditionResolveResult.AlreadyResolved,
            resolver.TryResolveCurrentNode(ExpeditionNodeOutcome.Success, out _),
            "已结算 Node 在 ReturnRequested 后重复 Resolve 仍是 no-op（exactly-once 终态）");
    }

    [Test]
    public void RequestReturn_Twice_SecondIsAlreadyRequested()
    {
        var resolver = ExpeditionProgressionTestKit.NewResolver(
            ExpeditionProgressionTestKit.Chain("n", 2), 1, 100, 1);
        Assert.AreEqual(ExpeditionReturnResult.Success, resolver.RequestReturn());
        Assert.AreEqual(ExpeditionReturnResult.AlreadyRequested, resolver.RequestReturn());
    }

    [Test]
    public void RouteCompleted_ThenRequestReturn_Allowed()
    {
        var graph = ExpeditionProgressionTestKit.SingleEntryGraph(
            ExpeditionProgressionTestKit.Node("boss", ExpeditionNodeKind.Boss,
                elapsedDays: 1, successfulRouteTerminal: true));
        var resolver = ExpeditionProgressionTestKit.NewResolver(graph, 1, 100, 1);
        ExpeditionProgressionTestKit.EnterAndSettle(resolver, "boss", ExpeditionNodeOutcome.Success);
        Assert.IsTrue(resolver.State.RouteCompleted);

        Assert.AreEqual(ExpeditionReturnResult.Success, resolver.RequestReturn());
        Assert.AreEqual(ExpeditionProgressionPhase.ReturnRequested, resolver.State.Phase);
        Assert.IsTrue(resolver.State.RouteCompleted, "RouteCompleted 在 Return 后仍为 true（供 SafeReturn 资格判定）");
    }
}
