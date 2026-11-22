using System;
using System.Reflection;
using NUnit.Framework;
using RRPG.StaticConfig;
using UnityEngine;

namespace RRPG.Building.Tests
{
public sealed class BuildingConstructionTests
{
    private const BindingFlags S=BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
    private const BindingFlags I=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    private static Type Bridge=>Type.GetType("VillageRuntimeBridge, Assembly-CSharp",true);
    private static object Call(string name,params object[] args)=>Bridge.GetMethod(name,S).Invoke(null,args);
    private static object Property(string name)=>Bridge.GetProperty(name,S).GetValue(null);
    private static object Get(object value,string name)=>value.GetType().GetProperty(name,I).GetValue(value);
    private VillageTime Clock=>(VillageTime)Bridge.GetField("Time",S).GetValue(null);
    private VillageProjectSystem Projects=>(VillageProjectSystem)Bridge.GetField("Projects",S).GetValue(null);
    private VillageAssignmentSystem Assignments=>(VillageAssignmentSystem)Bridge.GetField("Assignments",S).GetValue(null);
    [SetUp] public void Setup(){Call("ResetForTests");Call("EnsureInitialized",new object[]{null});IntradayVillageTestSource.BindDefaults();}
    [TearDown] public void Cleanup()=>Call("ResetForTests");
    private object Place(float x)
    {object[] args={"building_farm",x,-4f,null,null};Assert.IsTrue((bool)Call("TryPlaceBuilding",args),args[4]?.ToString());return args[3];}
    private string Id(object b)=>(string)Get(b,"InstanceId");
    private VillageProject Project(object b)=>Projects.GetActiveProject((string)Get(b,"ConstructionProjectId"));
    private void Advance(long minute)
    {
        var result=Clock.AdvanceTo(new WorldInstant(minute));int attempts=0;
        while(result.Outcome==WorldAdvanceOutcome.Yielded && attempts++<100) result=Clock.ResumeAdvance(Clock.AdvanceSession.RequestId);
        Assert.AreEqual(WorldAdvanceOutcome.Completed,result.Outcome,result.Diagnostic);
    }
    [Test] public void AddRemovePreservesPrimaryProgressAndZeroWorkerSave()
    {
        var b=Place(4);Assignments.TryAssign("char_001","ws_farm_01");
        Assert.AreEqual(VillageProjectResult.Success,Call("TryAddConstructionWorker",Id(b),"char_001"));
        var p=Project(b);p.Progress=3;
        Assert.AreEqual(VillageProjectResult.Success,Call("TryAddConstructionWorker",Id(b),"char_001"));Assert.AreEqual(1,p.ParticipantPersonIds.Count);
        Assert.AreEqual(VillageProjectResult.Success,Call("TryRemoveConstructionWorker",Id(b),"char_001"));
        Assert.AreEqual("ws_farm_01",Assignments.GetWorkplaceId("char_001"));Advance(1440);Assert.AreEqual(3,p.Progress);
        var save=(VillageSaveData)Call("BuildSaveData");object[] prepare={save,Property("Buildings"),null,null};
        Assert.IsTrue((bool)Call("TryPrepareBuildingWorld",prepare),prepare[3]?.ToString());Call("CommitBuildingWorld",prepare[2]);
        Assert.AreEqual(3,Projects.ActiveProjects[0].Progress);Assert.IsEmpty(Projects.ActiveProjects[0].ParticipantPersonIds);
    }
    [Test] public void ActualArrivalRequiredAndCompletionKeepsIdentityOnce()
    {
        var b=Place(4);Call("TryAddConstructionWorker",Id(b),"char_001");var p=Project(b);
        Advance(479);Assert.AreEqual(0,p.Progress);Assert.AreEqual(0,p.LaborRemainder);
        Advance(480);Assert.AreEqual(0,p.LaborRemainder);Advance(481);Assert.AreEqual(1,p.LaborRemainder);
        var presence=Call("QueryIntradayPresence","char_001");
        Assert.AreEqual("construction:"+Id(b),presence.GetType().GetProperty("PlaceId")?.GetValue(presence));
        p.Progress=9;p.LaborRemainder=0;Advance(2880);
        Assert.AreEqual(BuildingLifecycleState.Completed,Get(b,"LifecycleState"));Assert.IsNull(Get(b,"ConstructionProjectId"));
        Assert.IsNotNull(Assignments.GetWorkplaceDef("workplace:"+Id(b)+":main"));int count=Assignments.WorkplaceDefs.Count;
        Call("ObserveProjectCompletions");Assert.AreEqual(count,Assignments.WorkplaceDefs.Count);
        Assert.IsFalse(((VillageWorldRegistry)Bridge.GetField("World",S).GetValue(null)).ContainsPlace("construction:"+Id(b)));
    }
    [Test] public void TwoCompletedInstancesRestoreBeforeIndependentAssignments()
    {
        var a=Place(4);var b=Place(8);Call("TryAddConstructionWorker",Id(a),"char_001");Call("TryAddConstructionWorker",Id(b),"char_002");
        Project(a).Progress=9;Project(b).Progress=9;Advance(1440);
        string wa="workplace:"+Id(a)+":main",wb="workplace:"+Id(b)+":main";
        Assert.AreEqual(VillageAssignmentResult.Success,Assignments.TryAssign("char_001",wa));Assert.AreEqual(VillageAssignmentResult.Success,Assignments.TryAssign("char_002",wb));
        var save=(VillageSaveData)Call("BuildSaveData");object[] buildings={save.Buildings,null,null};Assert.IsTrue((bool)Call("TryPrepareBuildings",buildings));
        object[] prepare={save,buildings[1],null,null};Assert.IsTrue((bool)Call("TryPrepareBuildingWorld",prepare),prepare[3]?.ToString());Call("CommitBuildingWorld",prepare[2]);
        Assert.AreEqual(wa,Assignments.GetWorkplaceId("char_001"));Assert.AreEqual(wb,Assignments.GetWorkplaceId("char_002"));
        Assert.AreEqual(1,Assignments.GetOccupantCount(wa));Assert.AreEqual(1,Assignments.GetOccupantCount(wb));
    }
    [Test] public void DuplicateBindingAndDanglingDynamicAssignmentFailBeforePublication()
    {
        var a=Place(4);Place(8);Call("TryAddConstructionWorker",Id(a),"char_001");var save=(VillageSaveData)Call("BuildSaveData");
        save.Buildings.Instances[1].ConstructionProjectId=save.Buildings.Instances[0].ConstructionProjectId;
        object[] buildings={save.Buildings,null,null};Assert.IsTrue((bool)Call("TryPrepareBuildings",buildings));object[] prepare={save,buildings[1],null,null};
        Assert.IsFalse((bool)Call("TryPrepareBuildingWorld",prepare));Assert.AreEqual(1,Projects.ActiveProjects.Count);
        save=(VillageSaveData)Call("BuildSaveData");save.Assignments.Records.Add(new VillageAssignmentSaveData{PersonId="char_002",WorkplaceId="workplace:missing:main"});
        prepare=new object[]{save,Property("Buildings"),null,null};Assert.IsFalse((bool)Call("TryPrepareBuildingWorld",prepare));Assert.IsNull(Assignments.GetWorkplaceId("char_002"));
    }
    [Test] public void FullPersonTimeCompletesAndAllocatorSurvivesEmptyActiveSnapshot()
    {
        var b=Place(4);Call("TryAddConstructionWorker",Id(b),"char_001");var p=Project(b);string first=p.ProjectId;
        Advance(14400);Assert.AreEqual(10,p.Progress);Assert.AreEqual(BuildingLifecycleState.Completed,Get(b,"LifecycleState"));
        var save=Projects.ToSaveData();Assert.IsEmpty(save.ActiveProjects);Projects.Restore(save);
        Assert.AreEqual(VillageProjectResult.Success,Projects.TryCreateProject("prj_test_01",out var next));Assert.AreNotEqual(first,next.ProjectId);
    }
    [Test] public void RecoverCompletedBindingWithoutReplayingCompletionAndRejectOverflowAssignments()
    {
        var a=Place(4);Call("TryAddConstructionWorker",Id(a),"char_001");var save=(VillageSaveData)Call("BuildSaveData");
        save.Projects.ActiveProjects[0].Progress=10;object[] buildings={save.Buildings,null,null};Assert.IsTrue((bool)Call("TryPrepareBuildings",buildings));
        object[] prepare={save,buildings[1],null,null};Assert.IsTrue((bool)Call("TryPrepareBuildingWorld",prepare),prepare[3]?.ToString());
        Assert.AreEqual(BuildingLifecycleState.UnderConstruction,Get(a,"LifecycleState"),"Candidate recovery cannot mutate published buildings");
        Call("CommitBuildingWorld",prepare[2]);Call("ObserveProjectCompletions");
        Assert.IsNotNull(Assignments.GetWorkplaceDef("workplace:"+Id(a)+":main"));
        Assert.AreEqual(0,((VillageWorldRegistry)Bridge.GetField("World",S).GetValue(null)).CountPlacesOfKind(VillagePlaceKind.ProjectSite));
        save=(VillageSaveData)Call("BuildSaveData");string workplace="workplace:"+Id(a)+":main";
        save.Assignments.Records.Add(new VillageAssignmentSaveData{PersonId="char_001",WorkplaceId=workplace});
        save.Assignments.Records.Add(new VillageAssignmentSaveData{PersonId="char_002",WorkplaceId=workplace});
        save.Assignments.Records.Add(new VillageAssignmentSaveData{PersonId="char_003",WorkplaceId=workplace});
        prepare=new object[]{save,Property("Buildings"),null,null};Assert.IsFalse((bool)Call("TryPrepareBuildingWorld",prepare));Assert.AreEqual(0,Assignments.GetOccupantCount(workplace));
    }
}
}
