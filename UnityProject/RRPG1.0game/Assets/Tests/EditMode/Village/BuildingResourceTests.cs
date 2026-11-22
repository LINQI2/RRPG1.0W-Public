using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace RRPG.Building.Tests
{
public sealed class BuildingResourceTests
{
    private const BindingFlags S=BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
    private static Type Bridge=>Type.GetType("VillageRuntimeBridge, Assembly-CSharp",true);
    private static object Call(string name,params object[] args)=>Bridge.GetMethod(name,S).Invoke(null,args);
    private static T Field<T>(string name)=>(T)Bridge.GetField(name,S).GetValue(null);
    private static T Property<T>(string name)=>(T)Bridge.GetProperty(name,S).GetValue(null);
    private VillageTime Clock=>Field<VillageTime>("Time");
    private VillageAssignmentSystem Assignments=>Field<VillageAssignmentSystem>("Assignments");
    private VillageProjectSystem Projects=>Field<VillageProjectSystem>("Projects");
    private VillageIntradayWork Work=>Property<VillageIntradayWork>("IntradayWork");
    private VillageResourceState Resources=>Field<VillageResourceState>("Resources");
    [SetUp] public void Setup()
    {
        Call("ResetForTests");Call("EnsureInitialized",new object[]{null});
        IntradayVillageTestSource.Bind(IntradayVillageTestSource.Create("p1","p2","p3","p4","p5","p6"));
    }
    [TearDown] public void Cleanup()=>Call("ResetForTests");
    private string Place(string definition,float x,string worker)
    {
        object[] args={definition,x,-4f,null,null};Assert.IsTrue((bool)Call("TryPlaceBuilding",args),args[4]?.ToString());
        var b=args[3];string id=(string)b.GetType().GetProperty("InstanceId").GetValue(b);
        Assert.AreEqual(VillageProjectResult.Success,Call("TryAddConstructionWorker",id,worker));
        string project=(string)b.GetType().GetProperty("ConstructionProjectId").GetValue(b);
        Projects.GetActiveProject(project).Progress=9;return "workplace:"+id+":main";
    }
    private void Assign(string person,string workplace)=>Assert.AreEqual(VillageAssignmentResult.Success,Call("TryAssignPerson",person,workplace));
    private void Advance(long minute)
    {
        var result=Clock.AdvanceTo(new WorldInstant(minute));int attempts=0;
        while(result.Outcome==WorldAdvanceOutcome.Yielded && attempts++<100) result=Clock.ResumeAdvance(Clock.AdvanceSession.RequestId);
        Assert.AreEqual(WorldAdvanceOutcome.Completed,result.Outcome,result.Diagnostic);
    }
    private VillageWorkBatchSaveData Batch(string id)=>Work.ToSaveData().Batches.Single(b=>b.WorkplaceId==id);
    private VillageSaveData Save=>(VillageSaveData)Call("BuildSaveData");
    private object Prepare(VillageSaveData save,bool valid=true)
    {
        object[] buildings={save.Buildings,null,null};Assert.IsTrue((bool)Call("TryPrepareBuildings",buildings));
        object[] plan={save,buildings[1],null,null};Assert.AreEqual(valid,Call("TryPrepareBuildingWorld",plan),plan[3]?.ToString());return plan[2];
    }
    [Test] public void TwoFarmsRemovalReassignmentAndCapacityKeepIndependentRemainders()
    {
        string a=Place("building_farm",4,"p1"),b=Place("building_farm",8,"p2");Advance(1440);
        Assign("p1",a);Advance(1940);Assign("p2",b);Advance(1960);
        Assert.AreEqual(1,Resources.Ingredients);Assert.AreEqual(0,Batch(a).LaborNumerator);Assert.AreEqual(60,Batch(b).LaborNumerator);
        Call("RemovePersonAssignment","p1");Advance(1980);Assert.AreEqual(0,Batch(a).LaborNumerator);Assert.AreEqual(300,Batch(b).LaborNumerator);
        Assign("p3",a);Advance(2000);Assert.AreEqual(2,Resources.Ingredients);Assert.AreEqual(60,Batch(a).LaborNumerator);Assert.AreEqual(60,Batch(b).LaborNumerator);
        Assign("p4",a);Assert.AreEqual(VillageAssignmentResult.CapacityFull,Call("TryAssignPerson","p5",a));
    }
    [Test] public void TwoKitchensReserveOneInputOnceAndNoMaterialMeansNoLabor()
    {
        string a=Place("building_kitchen",4,"p1"),b=Place("building_kitchen",8,"p2");Advance(1440);
        Assign("p1",a);Assign("p2",b);Advance(1940);Assert.IsEmpty(Work.ToSaveData().Batches);Assert.IsEmpty(Work.ToSaveData().Evidence);
        Resources.AddIngredients(1);Advance(1960);Assert.AreEqual(0,Resources.Ingredients);Assert.AreEqual(0,Resources.Food);
        Assert.IsTrue(Batch(a).InputPaid);Assert.AreEqual(240,Batch(a).LaborNumerator);
        Assert.IsFalse(Work.ToSaveData().Batches.Any(x=>x.WorkplaceId==b));
        Advance(1980);Assert.AreEqual(1,Resources.Food);Assert.AreEqual(0,Batch(a).LaborNumerator);
        Assert.IsFalse(Work.ToSaveData().Evidence.Any(x=>x.WorkplaceId==b));
    }
    [Test] public void FarmOutputAtIntervalEndCannotRetroactivelyFundKitchenLabor()
    {
        string farm=Place("building_farm",4,"p1"),kitchen=Place("building_kitchen",8,"p2");Advance(1440);
        Assign("p1",farm);Assign("p2",kitchen);Advance(1960);
        Assert.AreEqual(1,Resources.Ingredients);Assert.IsFalse(Work.ToSaveData().Batches.Any(x=>x.WorkplaceId==kitchen));
        Advance(2000);Assert.AreEqual(1,Resources.Food);
    }
    [Test] public void ReplacementRestorePreservesPaidBatchAndIsIdempotent()
    {
        string kitchen=Place("building_kitchen",4,"p1");Advance(1440);Assign("p1",kitchen);Resources.AddIngredients(1);Advance(1940);
        var save=Save;var plan=Prepare(save);Call("CommitBuildingWorld",plan);Call("CommitBuildingWorld",Prepare(save));
        Assert.AreEqual(kitchen,Assignments.GetWorkplaceId("p1"));Assert.AreEqual(240,Batch(kitchen).LaborNumerator);Assert.IsTrue(Batch(kitchen).InputPaid);
        Advance(1960);Assert.AreEqual(1,Resources.Food);Assert.AreEqual(0,Resources.Ingredients);
        Call("ObserveProjectCompletions");Assert.AreEqual(1,Work.ToSaveData().Batches.Count);
    }
    [Test] public void InvalidDynamicBatchRejectsCandidateWithoutHalfRegistrationAndLegacyNewGameClear()
    {
        string farm=Place("building_farm",4,"p1");Advance(1440);Assign("p1",farm);Advance(1940);
        string before=JsonUtility.ToJson(Save);var invalid=Save;
        invalid.IntradayWork.Batches.Add(new VillageWorkBatchSaveData{WorkplaceId="workplace:missing:main",LaborNumerator=12});
        Prepare(invalid,false);Assert.AreEqual(before,JsonUtility.ToJson(Save));
        Call("ResetBuildingsForNewGame");Assert.IsEmpty(Work.ToSaveData().Batches);
        Assert.IsFalse(Assignments.WorkplaceDefs.Any(d=>d.WorkplaceId.StartsWith("workplace:")));
        var legacy=Save;legacy.Buildings=null;legacy.IntradayWork=null;Call("CommitBuildingWorld",Prepare(legacy));
        Assign("p1","ws_farm_01");Advance(2000);Assert.Greater(Resources.Ingredients,0);
    }
    [TestCase(VillageTime.VillageTimeSpeed.Fast24)]
    public void SpeedAndPauseMatchMinuteStepping(VillageTime.VillageTimeSpeed speed)
    {
        string farm=Place("building_farm",4,"p1"),kitchen=Place("building_kitchen",8,"p2");Advance(1440);
        Assign("p1",farm);Assign("p2",kitchen);var start=Save;
        for(long minute=1441;minute<=2520;minute++) Advance(minute);
        string expected=JsonUtility.ToJson(Resources.ToSaveData())+JsonUtility.ToJson(Work.ToSaveData());
        Clock.Restore(start.Time);Resources.Restore(start.Resources);Call("CommitBuildingWorld",Prepare(start));
        Clock.Pause();string paused=JsonUtility.ToJson(Save);Clock.Update(300);Assert.AreEqual(paused,JsonUtility.ToJson(Save));
        Clock.SetSpeed(speed);int seconds=(int)(1080/Clock.SpeedMultiplier);
        for(int i=0;i<seconds;i++) {Clock.Update(1);while(Clock.HasActiveAdvance) Clock.ResumeAdvance(Clock.AdvanceSession.RequestId);}
        Assert.AreEqual(2520,Clock.CurrentInstant.Minute);Assert.AreEqual(expected,JsonUtility.ToJson(Resources.ToSaveData())+JsonUtility.ToJson(Work.ToSaveData()));
    }
}
}
