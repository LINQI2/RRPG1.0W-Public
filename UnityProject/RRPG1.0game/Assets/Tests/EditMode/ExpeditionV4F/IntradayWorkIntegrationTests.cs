using System;
using System.IO;
using NUnit.Framework;

public class IntradayWorkIntegrationTests
{
    private object _player;
    private string _farmPerson, _kitchenPerson;
    private static Type Bridge => V4FTestKit.Bridge;
    private static VillageTime Clock => V4FTestKit.BridgeTime();
    private static VillageIntradayWork Work => (VillageIntradayWork)V4FTestKit.GetStaticMember(Bridge,"IntradayWork");
    private static VillageResourceState Resources => (VillageResourceState)V4FTestKit.GetStaticMember(Bridge,"Resources");
    [SetUp] public void Setup()
    {
        V4FTestKit.ResetBridge();V4FTestKit.EnsureInitialized();V4FTestKit.DeleteSave();
        _player=V4FTestKit.NewPlayerCollection();
        V4FTestKit.AddCharacterWithCard(_player,"char_001","farm",out _,out _farmPerson);
        V4FTestKit.AddCharacterWithCard(_player,"char_002","kitchen",out _,out _kitchenPerson);
        V4FTestKit.InvokeStatic(Bridge,"SetSimulationPersonSource",_player);
        Assert.AreEqual(VillageAssignmentResult.Success,V4FTestKit.InvokeStatic(Bridge,"TryAssignPerson",_farmPerson,"ws_farm_01"));
        Assert.AreEqual(VillageAssignmentResult.Success,V4FTestKit.InvokeStatic(Bridge,"TryAssignPerson",_kitchenPerson,"ws_kitchen_01"));
    }
    [TearDown] public void Teardown(){V4FTestKit.ResetBridge();V4FTestKit.DeleteSave();}
    private static void Advance(long minute)
    {
        var result=Clock.AdvanceTo(new WorldInstant(minute));
        while(result.Outcome==WorldAdvanceOutcome.Yielded)result=Clock.ResumeAdvance(Clock.AdvanceSession.RequestId);
        Assert.AreEqual(WorldAdvanceOutcome.Completed,result.Outcome,result.Diagnostic);
    }
    [Test] public void RealClock_ActualArrival_Batches_LunchBreak_NoMidnightDoubleProduction()
    {
        Advance(479);Assert.AreEqual(0,Resources.Ingredients);
        Advance(520);Assert.AreEqual(1,Resources.Ingredients);Assert.AreEqual(0,Resources.Food);
        Advance(560);Assert.AreEqual(1,Resources.Food);
        Advance(720);var before=Resources.ToSaveData();Advance(780);
        Assert.AreEqual(before.Ingredients,Resources.Ingredients);Assert.AreEqual(before.Food,Resources.Food);
        Advance(1020);before=Resources.ToSaveData();Advance(1440);
        Assert.AreEqual(before.Ingredients,Resources.Ingredients);Assert.AreEqual(before.Food,Resources.Food);
        Assert.AreEqual(1,V4FTestKit.Assignments().GetRecord(_farmPerson).TotalExecutedDays);
    }
    [Test] public void ProjectReservation_ExpeditionNoLabor_ReturnResumesWithoutJoin()
    {
        Assert.AreEqual(VillageProjectResult.Success,V4FTestKit.StartProject("prj_test_01",_farmPerson));
        var project=V4FTestKit.Projects().ActiveProjects[0];V4FTestKit.SetExpedition(new[]{_farmPerson});
        Advance(1440);Assert.AreEqual(0,project.Progress);Assert.IsTrue(project.ExecutionPaused);Assert.Contains(_farmPerson,project.ParticipantPersonIds);
        V4FTestKit.ClearExpedition();Advance(2880);
        Assert.AreEqual(1,project.Progress);Assert.AreEqual(1,project.ParticipantPersonIds.Count);
    }
    [Test] public void MidBatchRealSaveLoad_NoProductionOrLunchReplay_VersionOne()
    {
        Advance(540);Assert.IsTrue(V4FTestKit.Save(_player));var before=Resources.ToSaveData();
        Assert.IsNotNull(V4FTestKit.Load());Assert.AreEqual(540,Clock.MinuteOfDay);
        Assert.AreEqual(before.Food,Resources.Food);Advance(560);Assert.AreEqual(1,Resources.Food);
        StringAssert.Contains("\"SaveVersion\": 1",File.ReadAllText(V4FTestKit.SavePath()));
    }
    [Test] public void InvalidWorkBlock_PreflightKeepsCurrentAllWorlds()
    {
        Advance(540);Assert.IsTrue(V4FTestKit.Save(_player));string json=File.ReadAllText(V4FTestKit.SavePath());
        File.WriteAllText(V4FTestKit.SavePath(),json.Replace("\"LaborNumerator\": 240","\"LaborNumerator\": 480"));
        Assert.IsNull(V4FTestKit.Load());Assert.AreEqual(540,Clock.MinuteOfDay);Assert.AreEqual(240,Work.ToSaveData().Batches.Find(b=>b.WorkplaceId=="ws_kitchen_01").LaborNumerator);
    }
    [Test] public void BoundEmptyVersusMissingSource_NoInventedLabor()
    {
        V4FTestKit.InvokeStatic(Bridge,"SetSimulationPersonSource",(object)null);Advance(1020);
        Assert.AreEqual(0,Resources.Food);Assert.AreEqual(0,Resources.Ingredients);Assert.AreEqual(-1,Work.LastLunchMinute);
    }
}
