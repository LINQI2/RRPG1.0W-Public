using System;
using System.IO;
using NUnit.Framework;
public class IntradaySaveIntegrationTests
{
    [SetUp] public void Setup(){V4FTestKit.ResetBridge();V4FTestKit.EnsureInitialized();V4FTestKit.DeleteSave();}
    [TearDown] public void TearDown(){V4FTestKit.ResetBridge();V4FTestKit.DeleteSave();}
    private static VillageTime Time => V4FTestKit.BridgeTime();
    private static object Player()=>V4FTestKit.NewPlayerCollection();
    [Test] public void RealSave_ExactMinuteRemainder_NoLoadReplay_VersionOne()
    {Time.AdvanceTo(new WorldInstant(2280));Time.Update(.0001f);var saved=Time.CurrentInstant;var remainder=Time.DriverRemainder;
     Assert.IsTrue(V4FTestKit.Save(Player())); Assert.That(File.ReadAllText(V4FTestKit.SavePath()),Does.Contain("\"SaveVersion\": 1"));
     Time.TickDay();int ticks=0;Time.DayTicked+=_=>ticks++;
     Assert.IsNotNull(V4FTestKit.Load());Assert.AreEqual(saved,Time.CurrentInstant);
     // JsonUtility decimal round trip may differ by one double ULP; no meaningful minute is lost.
     Assert.That(Time.DriverRemainder,Is.EqualTo(remainder).Within(1e-12));Assert.AreEqual(0,ticks);}
    [TestCase(-1)] [TestCase(0)] [TestCase(2)]
    public void InvalidSchema_PreflightRetainsTimeAndDailyState(int schema)
    {Time.AdvanceTo(new WorldInstant(2280));Assert.IsTrue(V4FTestKit.Save(Player()));
     string json=File.ReadAllText(V4FTestKit.SavePath()).Replace("\"SchemaVersion\": 1","\"SchemaVersion\": "+schema);
     File.WriteAllText(V4FTestKit.SavePath(),json);Time.TickDay();var before=Time.CurrentInstant;int state=V4FTestKit.BridgeState().TestSurvivalValue;
     Assert.IsNull(V4FTestKit.Load());Assert.AreEqual(before,Time.CurrentInstant);Assert.AreEqual(state,V4FTestKit.BridgeState().TestSurvivalValue);}
    [TestCase(false)] [TestCase(true)]
    public void LegacyAbsentOrNull_ApproximateAndDiagnostic(bool explicitNull)
    {string block=explicitNull?",\"IntradayTime\":null":"";
     File.WriteAllText(V4FTestKit.SavePath(),"{\"SaveVersion\":1,\"Player\":{\"Gold\":7},\"Village\":{\"Time\":{\"Year\":2,\"Month\":3,\"Day\":4,\"Speed\":1,\"SecondsIntoDay\":2.5"+block+"}}}");
     Assert.IsNotNull(V4FTestKit.Load());Assert.AreEqual(720,Time.MinuteOfDay);Assert.AreEqual(2,Time.Year);StringAssert.Contains("LegacyTimeApproximation",Time.LastRestoreDiagnostic);}
    [Test] public void ActiveRequest_AllOrdinarySaveLoadNewGameDeleteRejected()
    {var p=Player();Assert.IsTrue(V4FTestKit.Save(p));string before=V4FTestKit.SaveHash();
     Time.AdvanceTo(new WorldInstant(4000),requestId:"pending",budget:1);
     Assert.IsFalse((bool)V4FTestKit.InvokeStatic(V4FTestKit.Asm("SaveSystem"),"Save",p));
     Assert.IsFalse((bool)V4FTestKit.InvokeStatic(V4FTestKit.Asm("SaveSystem"),"SaveWithVillage",p,null));
     Assert.IsNull(V4FTestKit.Load());Assert.IsNull(V4FTestKit.InvokeStatic(V4FTestKit.Asm("SaveSystem"),"NewGame"));
     V4FTestKit.InvokeStatic(V4FTestKit.Asm("SaveSystem"),"DeleteSave");Assert.AreEqual(before,V4FTestKit.SaveHash());Assert.IsNotNull(Time.AdvanceSession);}
    [Test] public void AbortLoad_OnlyClearsRequestAfterSuccessfulRestore()
    {Assert.IsTrue(V4FTestKit.Save(Player()));Time.AdvanceTo(new WorldInstant(4000),requestId:"pending",budget:1);
     string json=File.ReadAllText(V4FTestKit.SavePath());File.WriteAllText(V4FTestKit.SavePath(),"{invalid");
     Assert.IsNull(V4FTestKit.InvokeStatic(V4FTestKit.Asm("SaveSystem"),"AbortAdvanceAndLoad"));Assert.IsNotNull(Time.AdvanceSession);
     File.WriteAllText(V4FTestKit.SavePath(),json);Assert.IsNotNull(V4FTestKit.InvokeStatic(V4FTestKit.Asm("SaveSystem"),"AbortAdvanceAndLoad"));Assert.IsNull(Time.AdvanceSession);Assert.AreEqual(0,Time.CurrentInstant.Minute);}
    [Test] public void InvalidDriverRemainder_PreflightRetainsCurrent()
    {Time.AdvanceTo(new WorldInstant(480));Assert.IsTrue(V4FTestKit.Save(Player()));string json=File.ReadAllText(V4FTestKit.SavePath()).Replace("\"DriverRemainder\": 0.0","\"DriverRemainder\": 1.0");File.WriteAllText(V4FTestKit.SavePath(),json);Assert.IsNull(V4FTestKit.Load());Assert.AreEqual(480,Time.MinuteOfDay);}
}
