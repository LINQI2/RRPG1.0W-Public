using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

/// <summary>
/// ExpeditionProgression.Runtime Stage 1 EditMode 测试共享工具（仅测试程序集内部使用）。
/// </summary>
internal static class ExpeditionProgressionTestKit
{
    public static ExpeditionNodeData Node(string id,
        ExpeditionNodeKind kind = ExpeditionNodeKind.Battle,
        int elapsedDays = 0,
        int extraCost = 0,
        bool successfulRouteTerminal = false,
        params string[] next)
    {
        return new ExpeditionNodeData(id, kind, next, elapsedDays, extraCost, successfulRouteTerminal);
    }

    public static ExpeditionGraph SingleEntryGraph(params ExpeditionNodeData[] nodes)
    {
        return new ExpeditionGraph(nodes[0].NodeId, nodes);
    }

    /// <summary>单链测试图：n0→n1→…；可把末节点标记为显式 SuccessfulRouteTerminal。</summary>
    public static ExpeditionGraph Chain(string prefix, int count, int elapsedDays = 1, bool routeTerminalOnLast = false)
    {
        var nodes = new List<ExpeditionNodeData>();
        for (int i = 0; i < count; i++)
        {
            string id = prefix + i;
            string[] next = i < count - 1 ? new[] { prefix + (i + 1) } : new string[0];
            nodes.Add(new ExpeditionNodeData(id, ExpeditionNodeKind.Battle, next, elapsedDays, 0,
                routeTerminalOnLast && i == count - 1));
        }
        return SingleEntryGraph(nodes.ToArray());
    }

    public static ExpeditionProgressionResolver NewResolver(ExpeditionGraph graph,
        int partySize = 1, int initialCarry = 100, int perPersonDailyRate = 1)
    {
        var resolver = new ExpeditionProgressionResolver();
        Assert.AreEqual(ExpeditionProgressionInitResult.Success,
            resolver.InitializeProgression(graph, partySize, initialCarry, perPersonDailyRate));
        return resolver;
    }

    public static List<string> AvailableIds(ExpeditionProgressionResolver resolver)
    {
        return resolver.GetAvailableNextNodes().Select(n => n.NodeId).ToList();
    }

    public static void EnterAndSettle(ExpeditionProgressionResolver resolver, string nodeId, ExpeditionNodeOutcome outcome)
    {
        Assert.AreEqual(ExpeditionEnterResult.Success, resolver.TryEnterNode(nodeId), "TryEnterNode 应成功: " + nodeId);
        Assert.AreEqual(ExpeditionResolveResult.Success, resolver.TryResolveCurrentNode(outcome),
            "TryResolveCurrentNode 应成功: " + nodeId);
    }

/// <summary>
/// InitializeProgression 初始化契约（FDS Stage 1）。
/// </summary>
public class ExpeditionProgressionInitTests
{
    [Test]
    public void Initialize_SetsDefaults_EntryNodeReady()
    {
        var graph = ExpeditionProgressionTestKit.Chain("n", 2, elapsedDays: 1);
        var resolver = new ExpeditionProgressionResolver();
        Assert.AreEqual(ExpeditionProgressionInitResult.Success,
            resolver.InitializeProgression(graph, partySize: 2, initialCarryUnits: 50,
                perPersonDailyConsumptionRate: 1));

        var state = resolver.State;
        Assert.IsNotNull(state);
        Assert.AreEqual(ExpeditionProgressionPhase.NotStarted, state.Phase);
        Assert.IsNull(state.CurrentNodeId);
        Assert.AreEqual(0, state.ExpeditionElapsedDays);
        Assert.IsFalse(state.RouteCompleted);
        Assert.AreEqual(50, state.Supply.CurrentCarryUnits);
        Assert.AreEqual(50, state.Supply.InitialCarryUnits);
        Assert.AreEqual(2, state.Supply.PartySize);
        CollectionAssert.AreEqual(new[] { "n0" }, ExpeditionProgressionTestKit.AvailableIds(resolver));
        Assert.AreEqual(0, state.GetEnteredNodeIds().Count);
        Assert.AreEqual(0, state.GetResolvedNodeIds().Count);
        Assert.AreEqual(0, state.GetCompletedNodeIds().Count);
    }

    [Test]
    public void Initialize_WithNullGraph_ReturnsNullGraph_AndNoState()
    {
        var resolver = new ExpeditionProgressionResolver();
        Assert.AreEqual(ExpeditionProgressionInitResult.NullGraph,
            resolver.InitializeProgression(null, 1, 100, 1));
        Assert.IsNull(resolver.State);
    }

    [Test]
    public void Initialize_Twice_SecondCall_IsNoOp_StatePreserved()
    {
        var resolver = ExpeditionProgressionTestKit.NewResolver(
            ExpeditionProgressionTestKit.Chain("n", 2, elapsedDays: 1), 1, 100, 1);
        var firstState = resolver.State;

        Assert.AreEqual(ExpeditionProgressionInitResult.AlreadyInitialized,
            resolver.InitializeProgression(ExpeditionProgressionTestKit.Chain("m", 2), 2, 7, 3));
        Assert.AreSame(firstState, resolver.State, "重复 initialize 不得替换既有会话");
        Assert.AreEqual(ExpeditionProgressionPhase.NotStarted, resolver.State.Phase);
        Assert.AreEqual(100, resolver.State.Supply.CurrentCarryUnits, "重复 initialize 不得重置 Supply");
    }
}

}
