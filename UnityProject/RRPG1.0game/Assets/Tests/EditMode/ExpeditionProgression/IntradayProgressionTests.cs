using NUnit.Framework;
public class IntradayProgressionTests
{
    private static ExpeditionProgressionResolver Build(long duration,int carry=10,int extra=0)
    {var r=new ExpeditionProgressionResolver();r.InitializeProgression(new ExpeditionGraph("n",new[]{new ExpeditionNodeData("n",ExpeditionNodeKind.Battle,durationMinutes:duration,extraSupplyCost:extra)}),1,carry,1);return r;}
    [TestCase(ExpeditionNodeOutcome.Success)] [TestCase(ExpeditionNodeOutcome.Failure)]
    public void NativeDuration_SettledOnce_EnterReadOnly(ExpeditionNodeOutcome outcome)
    {var r=Build(360);Assert.AreEqual(ExpeditionEnterResult.Success,r.TryEnterNode("n"));Assert.AreEqual(0,r.State.ExpeditionElapsedMinutes);Assert.AreEqual(10,r.State.Supply.CurrentCarryUnits);
     Assert.AreEqual(ExpeditionResolveResult.Success,r.TryResolveCurrentNode(outcome,0,out var data));Assert.AreEqual(360,r.State.ExpeditionElapsedMinutes);Assert.AreEqual(9,r.State.Supply.CurrentCarryUnits);
     Assert.AreEqual(outcome==ExpeditionNodeOutcome.Success,data.Completed);Assert.AreEqual(360,data.DurationMinutesSettled);
     Assert.AreNotEqual(ExpeditionResolveResult.Success,r.TryResolveCurrentNode(outcome));Assert.AreEqual(360,r.State.ExpeditionElapsedMinutes);Assert.AreEqual(9,r.State.Supply.CurrentCarryUnits);}
    [Test] public void OldEventDayPlusNativeDuration_UsesCumulativeMinuteCost()
    {var r=Build(360);r.TryEnterNode("n");Assert.AreEqual(ExpeditionResolveResult.Success,r.TryResolveCurrentNode(ExpeditionNodeOutcome.Success,1,out var d));Assert.AreEqual(1800,r.State.ExpeditionElapsedMinutes);Assert.AreEqual(2,d.TotalSupplyCost);}
    [Test] public void AdditionalTimeUnaffordable_ZeroMutation()
    {var r=Build(900,1);r.TryEnterNode("n");Assert.AreEqual(ExpeditionResolveResult.InsufficientSupply,r.TryResolveCurrentNode(ExpeditionNodeOutcome.Success,1,out _));Assert.AreEqual(0,r.State.ExpeditionElapsedMinutes);Assert.AreEqual(1,r.State.Supply.CurrentCarryUnits);Assert.IsFalse(r.State.IsNodeResolved("n"));}
    [Test] public void OverflowAtEnterOrResolve_ZeroMutation()
    {var r=Build(long.MaxValue);Assert.AreEqual(ExpeditionEnterResult.InsufficientSupply,r.TryEnterNode("n"));Assert.AreEqual(0,r.State.ExpeditionElapsedMinutes);Assert.AreEqual(10,r.State.Supply.CurrentCarryUnits);Assert.IsFalse(r.State.IsNodeEntered("n"));}
    [Test] public void ZeroDurationExplicitExtra()
    {var r=Build(0,10,3);Assert.AreEqual(ExpeditionEnterResult.Success,r.TryEnterNode("n"));Assert.AreEqual(ExpeditionResolveResult.Success,r.TryResolveCurrentNode(ExpeditionNodeOutcome.Success,0,out var d));Assert.AreEqual(0,r.State.ExpeditionElapsedMinutes);Assert.AreEqual(3,d.TotalSupplyCost);}
}