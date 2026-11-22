using NUnit.Framework;
public class IntradaySupplyTests
{
    [TestCase(new long[]{360,360,360,360},new int[]{1,0,0,0},1,1)]
    [TestCase(new long[]{900,900},new int[]{1,1},1,1)]
    [TestCase(new long[]{1,1439,1},new int[]{1,0,1},1,1)]
    [TestCase(new long[]{240,240,240},new int[]{1,0,0},2,1)]
    [TestCase(new long[]{1440,360},new int[]{1,1},1,1)]
    [TestCase(new long[]{0,360},new int[]{0,0},1,0)]
    public void CumulativeCeiling_NodeOnlyIncrement(long[] durations,int[] expected,int party,int rate)
    { var s=new ExpeditionSupplyState(party,100,rate);long prior=0;for(int i=0;i<durations.Length;i++){Assert.IsTrue(s.TryComputeMinuteCost(prior,durations[i],0,out int cost,out int b));Assert.AreEqual(expected[i],b);Assert.AreEqual(b,cost);Assert.IsTrue(s.TryConsume(cost));prior+=durations[i];}}
    [Test] public void ExplicitExtras_AreIndependent()
    {var s=new ExpeditionSupplyState(1,100,1);long prior=0;int sum=0;int[] extras={2,0,3,0};for(int i=0;i<4;i++){Assert.IsTrue(s.TryComputeMinuteCost(prior,360,extras[i],out int cost,out _));sum+=cost;prior+=360;}Assert.AreEqual(6,sum);}
    [Test] public void ZeroDurationExtra_AndZeroRate()
    {var s=new ExpeditionSupplyState(1,10,0);Assert.IsTrue(s.TryComputeMinuteCost(0,0,3,out int cost,out int b));Assert.AreEqual(3,cost);Assert.AreEqual(0,b);}
    [Test] public void Overflow_FailClosed_CarryUnchanged()
    {var s=new ExpeditionSupplyState(int.MaxValue,10,int.MaxValue);Assert.IsFalse(s.TryComputeMinuteCost(0,long.MaxValue,0,out _,out _));Assert.AreEqual(10,s.CurrentCarryUnits);}
    [Test] public void IncrementAffordability_RejectsWithoutConsumption()
    {var s=new ExpeditionSupplyState(1,1,1);Assert.IsTrue(s.TryComputeMinuteCost(0,1800,0,out int cost,out _));Assert.AreEqual(2,cost);Assert.IsFalse(s.CanAfford(cost));Assert.IsFalse(s.TryConsume(cost));Assert.AreEqual(1,s.CurrentCarryUnits);}
}