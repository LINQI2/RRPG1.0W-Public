using System;
using System.Collections.Generic;
using NUnit.Framework;

public class IntradayTimeTests
{
    private sealed class Sink : IWorldAdvanceSink
    {
        public bool Block, FailPreflight, FailBoundary;
        public int Intervals, Boundaries;
        public WorldInstant? NextBoundary(WorldInstant from, WorldInstant target)
        {
            long noon = from.Minute / 1440 * 1440 + 720;
            return noon > from.Minute && noon <= target.Minute ? new WorldInstant(noon) : (WorldInstant?)null;
        }
        public string PreflightBoundary(WorldInstant boundary) => FailPreflight ? "before" : null;
        public void CompleteInterval(WorldInstant from, WorldInstant to) { Intervals++; }
        public void CompleteBoundary(WorldInstant boundary) { Boundaries++; if (FailBoundary) throw new Exception("partial"); }
        public bool TryGetBlockingInteraction(out string id, out string diagnostic)
        { id = Block ? "event:1" : null; diagnostic = null; return Block; }
    }
    private static VillageTime At(int y = 1, int m = 1, int d = 1, int minute = 480)
    {
        var t = new VillageTime();
        t.Restore(new VillageTimeSaveData { Year=y, Month=m, Day=d, Speed=1, IntradayTime=new VillageIntradayTimeSaveData {SchemaVersion=1, CurrentWorldMinute=WorldInstant.FromDate(y,m,d,minute).Minute, LastCompletedDailyBoundaryMinute=WorldInstant.FromDate(y,m,d).Minute} });
        return t;
    }
    [Test] public void ThirtyHours_ExactArrival()
    {
        var t=At(); int days=0; t.DayTicked += _ => days++;
        Assert.AreEqual(WorldAdvanceOutcome.Completed,t.AdvanceTo(t.CurrentInstant.AddMinutes(1800)).Outcome);
        Assert.AreEqual(2,t.Day); Assert.AreEqual(840,t.MinuteOfDay); Assert.AreEqual(1,days);
    }
    [TestCase(2024,2,28,2,29)] [TestCase(2023,2,28,3,1)] [TestCase(2023,12,31,1,1)]
    public void GregorianBoundary(int y,int m,int d,int nextMonth,int nextDay)
    { var t=At(y,m,d); t.TickDay(); Assert.AreEqual(nextMonth,t.Month); Assert.AreEqual(nextDay,t.Day); Assert.AreEqual(480,t.MinuteOfDay); }
    [Test] public void Jump_ToFirstMidnight_MonthBeforeDay()
    { var t=At(1,1,31); var calls=new List<string>(); t.MonthTicked+=_=>calls.Add("month"); t.DayTicked+=_=>calls.Add("day"); t.JumpToNextMonth(); CollectionAssert.AreEqual(new[]{"month","day"},calls); Assert.AreEqual(0,t.MinuteOfDay); }
    [Test] public void Yield_RetainsTargetCursor_NoIncomingDelta()
    {
        var t=At(); var sink=new Sink(); t.AdvanceSink=sink; var target=t.CurrentInstant.AddMinutes(1800);
        Assert.AreEqual(WorldAdvanceOutcome.Yielded,t.AdvanceTo(target,requestId:"r",budget:1).Outcome);
        Assert.AreEqual(720,t.MinuteOfDay); Assert.AreEqual(target,t.AdvanceSession.OriginalTarget);
        long cursor=t.CurrentInstant.Minute; t.Update(1000); t.TickDay(); t.JumpToNextMonth();
        Assert.AreEqual(cursor,t.CurrentInstant.Minute); Assert.IsFalse(t.CanSaveOrdinarily);
        while(t.HasActiveAdvance) t.ResumeAdvance("r",1);
        Assert.AreEqual(target,t.CurrentInstant); Assert.AreEqual(4,sink.Boundaries);
    }
    [Test] public void Waiting_FirstBoundary_StaleAckRejected_ResumeSameTarget()
    {
        var t=At(); var sink=new Sink{Block=true}; t.AdvanceSink=sink;
        var target=t.CurrentInstant.AddMinutes(1800);
        Assert.AreEqual(WorldAdvanceOutcome.WaitingForInteraction,t.AdvanceTo(target,requestId:"r").Outcome);
        Assert.AreEqual(0,sink.Intervals); t.Update(1000); Assert.AreEqual(480,t.MinuteOfDay);
        Assert.IsFalse(t.AcknowledgeInteraction("r","stale")); sink.Block=false;
        Assert.IsTrue(t.AcknowledgeInteraction("r","event:1")); t.ResumeAdvance("r"); Assert.AreEqual(target,t.CurrentInstant);
    }
    [Test] public void PreflightFailure_HasNoMutation_ThenResumes()
    { var t=At(); var sink=new Sink{FailPreflight=true}; t.AdvanceSink=sink; var old=t.CurrentInstant; var target=old.AddMinutes(1800);
      Assert.AreEqual(WorldAdvanceOutcome.RetryableBoundaryFailure,t.AdvanceTo(target,requestId:"r").Outcome);
      Assert.AreEqual(old,t.CurrentInstant); Assert.AreEqual(old,t.AdvanceSession.LastCompletedInstant);
      sink.FailPreflight=false; Assert.AreEqual(WorldAdvanceOutcome.Completed,t.ResumeAdvance("r").Outcome); }
    [Test] public void PartialFailure_NoReplay_NoSave()
    { var t=At(); var sink=new Sink{FailBoundary=true}; t.AdvanceSink=sink; var old=t.CurrentInstant;
      Assert.AreEqual(WorldAdvanceOutcome.PartialBoundaryFailure,t.AdvanceTo(old.AddMinutes(1800),requestId:"r").Outcome);
      Assert.AreEqual(old,t.AdvanceSession.LastCompletedInstant); sink.FailBoundary=false;
      Assert.AreEqual(WorldAdvanceOutcome.PartialBoundaryFailure,t.ResumeAdvance("r").Outcome); Assert.AreEqual(1,sink.Intervals); Assert.IsFalse(t.CanSaveOrdinarily); }
    [Test] public void ReturnCompletion_HoldsLockUntilSaveAck()
    { var t=At(); t.AdvanceTo(t.CurrentInstant.AddMinutes(30),WorldAdvanceOrigin.Return,"return");
      Assert.IsTrue(t.HasActiveAdvance); Assert.IsTrue(t.HoldForFinalSave("return"));
      var old=t.CurrentInstant; t.Update(100); Assert.AreEqual(old,t.CurrentInstant);
      Assert.IsFalse(t.CompleteFinalSave("wrong")); Assert.IsTrue(t.CompleteFinalSave("return")); Assert.IsTrue(t.CanSaveOrdinarily); }
    [Test] public void InvalidTargetAndBudget_NoMutation()
    { var t=At(); var old=t.CurrentInstant; Assert.AreEqual(WorldAdvanceOutcome.InvalidTarget,t.AdvanceTo(new WorldInstant(0)).Outcome);
      Assert.AreEqual(WorldAdvanceOutcome.InvalidTarget,t.AdvanceTo(old.AddMinutes(10),budget:0).Outcome);
      Assert.AreEqual(old,t.CurrentInstant); Assert.IsNull(t.AdvanceSession); Assert.Throws<OverflowException>(()=>old.AddMinutes(long.MaxValue)); }
    [Test] public void ZeroAndSameTarget_NoTicks()
    { var t=At(); int count=0;t.DayTicked+=_=>count++;t.AdvanceTo(t.CurrentInstant);t.AdvanceTo(t.CurrentInstant);Assert.AreEqual(0,count); }
    [Test] public void DriverSlices_RemainderPreserved()
    { var a=At();var b=At();for(int i=0;i<100;i++)a.Update(.015625f);b.Update(1.5625f);Assert.AreEqual(b.CurrentInstant.Minute,a.CurrentInstant.Minute);Assert.AreEqual(b.DriverRemainder,a.DriverRemainder,0.0001); }
    [Test] public void PauseFast_OneClock()
    {var t=At();t.Pause();t.Update(100);Assert.AreEqual(480,t.MinuteOfDay);t.SetSpeed(VillageTime.VillageTimeSpeed.Fast);t.Update(t.SecondsPerDay/4);Assert.AreEqual(2,t.Day);Assert.AreEqual(480,t.MinuteOfDay);}
    [Test] public void NewSaveRoundTrip_NoTicks()
    { var a=At(); a.Update(.001f);var b=new VillageTime(); int n=0;b.DayTicked+=_=>n++;Assert.IsTrue(b.TryRestore(a.ToSaveData(),true));Assert.AreEqual(a.CurrentInstant,b.CurrentInstant);Assert.AreEqual(a.DriverRemainder,b.DriverRemainder);Assert.AreEqual(0,n); }
    [TestCase(-1)] [TestCase(0)] [TestCase(2)]
    public void InvalidSchema_PreservesOldState(int schema)
    {var t=At();var dto=t.ToSaveData();dto.IntradayTime.SchemaVersion=schema;Assert.IsFalse(t.TryRestore(dto,true));Assert.AreEqual(480,t.MinuteOfDay);}
    [Test] public void InvalidMarker_PreservesState()
    {var t=At();var dto=t.ToSaveData();dto.IntradayTime.LastCompletedDailyBoundaryMinute=1440;Assert.IsFalse(t.TryRestore(dto,true));Assert.AreEqual(480,t.MinuteOfDay);}
    [TestCase(-1f)] [TestCase(5f)] [TestCase(float.NaN)] [TestCase(float.PositiveInfinity)]
    public void LegacyInvalidTime_PreservesState(float seconds)
    {var t=At();Assert.IsFalse(t.TryRestore(new VillageTimeSaveData{Year=1,Month=1,Day=1,SecondsIntoDay=seconds},false));Assert.AreEqual(480,t.MinuteOfDay);}
    [Test] public void LegacyMigration_DiagnosticAndNoReplay()
    {var t=new VillageTime();int n=0;t.DayTicked+=_=>n++;Assert.IsTrue(t.TryRestore(new VillageTimeSaveData{Year=2024,Month=2,Day=29,SecondsIntoDay=2.5f},false));Assert.AreEqual(720,t.MinuteOfDay);Assert.AreEqual(0,n);StringAssert.Contains("LegacyTimeApproximation",t.LastRestoreDiagnostic);}
}
