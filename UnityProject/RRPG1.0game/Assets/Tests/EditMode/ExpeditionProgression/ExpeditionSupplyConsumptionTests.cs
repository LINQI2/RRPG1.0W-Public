using NUnit.Framework;

/// <summary>
/// Supply / Carry 结算契约（FDS §6）：
/// Base = PartySize × NodeElapsedDays × Rate；Explicit Extra Supply Cost 仅特殊 Node；
/// 普通 Node 无 Time + fixed 双扣；普通已知成本不足在进入前原子拒绝；
/// Victory/Defeat outcome 均 exactly-once 扣除已发生成本；Return 不受阻塞且不清空 Carry。
/// </summary>
public class ExpeditionSupplyConsumptionTests
{
    [Test]
    public void Victory_BaseConsumption_FollowsFormula()
    {
        // Party2 × days4 × rate3 = 24；carry 100 → 76。
        var resolver = ExpeditionProgressionTestKit.NewResolver(
            ExpeditionProgressionTestKit.Chain("n", 2, elapsedDays: 4), 2, 100, 3);
        Assert.AreEqual(ExpeditionEnterResult.Success, resolver.TryEnterNode("n0"));
        Assert.AreEqual(ExpeditionResolveResult.Success,
            resolver.TryResolveCurrentNode(ExpeditionNodeOutcome.Success, out var data));

        Assert.AreEqual(24, data.BaseSupplyConsumption);
        Assert.AreEqual(0, data.ExtraSupplyCostApplied);
        Assert.AreEqual(24, data.TotalSupplyCost);
        Assert.AreEqual(76, data.CurrentCarryUnits);
        Assert.AreEqual(76, resolver.State.Supply.CurrentCarryUnits);
    }

    [Test]
    public void ExplicitExtraSupplyCost_OnlyWhenNodeDefinesIt()
    {
        // days1 + extra5，Party1 rate1 → total 6。
        var graph = ExpeditionProgressionTestKit.SingleEntryGraph(
            ExpeditionProgressionTestKit.Node("x0", elapsedDays: 1, extraCost: 5));
        var resolver = ExpeditionProgressionTestKit.NewResolver(graph, 1, 100, 1);
        ExpeditionProgressionTestKit.EnterAndSettle(resolver, "x0", ExpeditionNodeOutcome.Success);

        Assert.AreEqual(94, resolver.State.Supply.CurrentCarryUnits);
        Assert.AreEqual(6, resolver.State.Supply.ComputeTotalCost(1, 5), "Base1 + Extra5 = 6");
    }

    [Test]
    public void NormalNode_NoTimePlusFixedDoubleCharge_ResolveRepeatNoOp()
    {
        var resolver = ExpeditionProgressionTestKit.NewResolver(
            ExpeditionProgressionTestKit.Chain("n", 2, elapsedDays: 4), 2, 100, 3);
        ExpeditionProgressionTestKit.EnterAndSettle(resolver, "n0", ExpeditionNodeOutcome.Success);
        Assert.AreEqual(76, resolver.State.Supply.CurrentCarryUnits, "只扣一次 24");

        for (int i = 0; i < 3; i++)
        {
            Assert.AreEqual(ExpeditionResolveResult.AlreadyResolved,
                resolver.TryResolveCurrentNode(ExpeditionNodeOutcome.Success, out _));
        }
        Assert.AreEqual(76, resolver.State.Supply.CurrentCarryUnits,
            "普通 Node 不得出现第二次 Supply 扣减（无双扣）");
    }

    [Test]
    public void Defeat_ConsumesAlreadyHappenedSupply_SameAsHappened()
    {
        // days2 rate1 party1：Victory 与 Defeat 都结算 2。
        var victory = ExpeditionProgressionTestKit.NewResolver(
            ExpeditionProgressionTestKit.Chain("n", 2, elapsedDays: 2), 1, 100, 1);
        ExpeditionProgressionTestKit.EnterAndSettle(victory, "n0", ExpeditionNodeOutcome.Success);
        Assert.AreEqual(98, victory.State.Supply.CurrentCarryUnits);

        var defeat = ExpeditionProgressionTestKit.NewResolver(
            ExpeditionProgressionTestKit.Chain("n", 2, elapsedDays: 2), 1, 100, 1);
        ExpeditionProgressionTestKit.EnterAndSettle(defeat, "n0", ExpeditionNodeOutcome.Failure);
        Assert.AreEqual(98, defeat.State.Supply.CurrentCarryUnits,
            "Defeat 也 exactly-once 扣除已经发生的 Supply");
        Assert.AreEqual(ExpeditionProgressionPhase.FailedResolved, defeat.State.Phase);
    }

    [Test]
    public void InsufficientSupply_PreKnownCost_RejectedBeforeEntering_ContentNeverStarts()
    {
        // Party1 × days3 × rate1 + extra 4 = 7 > carry 5 → 进入前拒绝。
        var graph = ExpeditionProgressionTestKit.SingleEntryGraph(
            ExpeditionProgressionTestKit.Node("x0", elapsedDays: 3, extraCost: 4, next: new[] { "x1" }),
            ExpeditionProgressionTestKit.Node("x1"));
        var resolver = ExpeditionProgressionTestKit.NewResolver(graph, 1, 5, 1);

        Assert.AreEqual(ExpeditionEnterResult.InsufficientSupply, resolver.TryEnterNode("x0"));
        Assert.AreEqual(ExpeditionEnterResult.InsufficientSupply,
            resolver.ValidateEnterNode("x0"), "CanEnter 纯查询也返回不可承担");
        Assert.AreEqual(ExpeditionProgressionPhase.NotStarted, resolver.State.Phase,
            "不得启动 Battle/Event 内容");
        Assert.AreEqual(0, resolver.State.GetEnteredNodeIds().Count);
        Assert.AreEqual(5, resolver.State.Supply.CurrentCarryUnits, "内容未启动不得扣减");
        Assert.AreEqual(0, resolver.State.ExpeditionElapsedDays);
    }

    [Test]
    public void AffordabilityEdge_CarryEqualsExactCost_Allowed()
    {
        // cost = 1×1×1 = 1，carry 恰好 1 → 允许进入并结算到 0。
        var resolver = ExpeditionProgressionTestKit.NewResolver(
            ExpeditionProgressionTestKit.Chain("n", 2, elapsedDays: 1), 1, 1, 1);
        Assert.AreEqual(ExpeditionEnterResult.Success, resolver.TryEnterNode("n0"));
        Assert.AreEqual(ExpeditionResolveResult.Success,
            resolver.TryResolveCurrentNode(ExpeditionNodeOutcome.Success, out _));
        Assert.AreEqual(0, resolver.State.Supply.CurrentCarryUnits);
    }

    [Test]
    public void ZeroCostNode_ConsumesNothing()
    {
        var resolver = ExpeditionProgressionTestKit.NewResolver(
            ExpeditionProgressionTestKit.Chain("n", 2, elapsedDays: 0), 2, 100, 9);
        ExpeditionProgressionTestKit.EnterAndSettle(resolver, "n0", ExpeditionNodeOutcome.Success);
        Assert.AreEqual(100, resolver.State.Supply.CurrentCarryUnits);
    }

    [Test]
    public void EmptyCarry_DoesNotBlockReturn_AndReturnDoesNotClearCarry()
    {
        var resolver = ExpeditionProgressionTestKit.NewResolver(
            ExpeditionProgressionTestKit.Chain("n", 2, elapsedDays: 1), 1, 1, 1);
        ExpeditionProgressionTestKit.EnterAndSettle(resolver, "n0", ExpeditionNodeOutcome.Success);
        Assert.AreEqual(0, resolver.State.Supply.CurrentCarryUnits);

        Assert.AreEqual(ExpeditionReturnResult.Success, resolver.RequestReturn(),
            "Supply 为 0 不阻塞 Expedition 层 Return");
        Assert.AreEqual(ExpeditionProgressionPhase.ReturnRequested, resolver.State.Phase);
        Assert.AreEqual(0, resolver.State.Supply.CurrentCarryUnits,
            "Return 不得提前清空 Carry（返还 hook 属 WP-3）");
    }

    [Test]
    public void RequestReturn_WithoutAnyNodeStarted_NoCostSettled()
    {
        var resolver = ExpeditionProgressionTestKit.NewResolver(
            ExpeditionProgressionTestKit.Chain("n", 2, elapsedDays: 3), 2, 100, 2);
        Assert.AreEqual(ExpeditionReturnResult.Success, resolver.RequestReturn(),
            "尚未启动任何 Node 内容的主动 Retreat 应直接允许");
        Assert.AreEqual(0, resolver.State.ExpeditionElapsedDays, "未发生的 Node 成本不额外结算");
        Assert.AreEqual(100, resolver.State.Supply.CurrentCarryUnits);
    }
}
