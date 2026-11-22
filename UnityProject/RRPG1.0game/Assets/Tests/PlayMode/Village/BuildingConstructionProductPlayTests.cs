using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace RRPG.Building.Tests
{
public sealed class BuildingConstructionProductPlayTests
{
    private const BindingFlags S=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static;
    private const BindingFlags I=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance;
    private static Type T(string name)=>Type.GetType(name+", Assembly-CSharp",true);
    private static object Static(string type,string method,params object[] args)=>T(type).GetMethod(method,S).Invoke(null,args);
    private static object Call(object value,string method,params object[] args)=>value.GetType().GetMethods(I).First(m=>m.Name==method && m.GetParameters().Length==args.Length).Invoke(value,args);
    private static object Get(object value,string name)=>value.GetType().GetProperty(name,I).GetValue(value);
    private static object Find(string type)=>UnityEngine.Object.FindObjectOfType(T(type),true);
    private VillageTime Clock=>(VillageTime)T("VillageRuntimeBridge").GetField("Time",S).GetValue(null);
    private VillageProjectSystem Projects=>(VillageProjectSystem)T("VillageRuntimeBridge").GetField("Projects",S).GetValue(null);
    private VillageAssignmentSystem Assignments=>(VillageAssignmentSystem)T("VillageRuntimeBridge").GetField("Assignments",S).GetValue(null);
    private object _host,_player;
    private ProductHomeSnapshot Snapshot=>(ProductHomeSnapshot)Get(_host,"Snapshot");
    private HomeSelectionContext Selection=>(HomeSelectionContext)Get(_host,"Selection");
    private string SavePath=>(string)Static("SaveSystem","GetSavePath");
    private VillageSaveData Save=>(VillageSaveData)Static("VillageRuntimeBridge","BuildSaveData");
    private Component BuildingView(string id)=>(Component)((IDictionary)Get(_host,"BuildingViews"))[id];
    [UnitySetUp] public IEnumerator Setup()
    {
        Static("VillageRuntimeBridge","ResetForTests");SceneManager.LoadScene("SampleScene");yield return null;yield return null;
        _player=Static("SaveSystem","NewGame");Assert.IsTrue((bool)Static("SaveSystem","Save",_player));
        Call(Find("MainMenuUI"),"OnContinue");yield return null;yield return null;
        _player=Get(Find("TownUI"),"PlayerData");Clock.Pause();_host=Find("ProductHomeWorldHost");Call(_host,"RefreshNow");
    }
    [TearDown] public void Cleanup(){Static("VillageRuntimeBridge","ResetForTests");if(File.Exists(SavePath)) File.Delete(SavePath);}
    private string Place(float x)
    {
        var build=Get(Get(_host,"Shell"),"Build");Call(build,"OpenCatalog");Assert.IsTrue((bool)Call(build,"Choose","building_farm"));
        Call(build,"SetCandidate",new Vector2(x,-4));Assert.IsTrue((bool)Call(build,"Confirm"));Call(build,"Cancel");Call(_host,"RefreshNow");return Selection.Id;
    }
    private void WorkerButton(string building,string person)
    {
        Selection.SelectBuilding(building);Call(_host,"RefreshNow");
        var inspector=(Component)Get(_host,"Inspector");var row=inspector.GetComponentsInChildren<Transform>(true).LastOrDefault(t=>t.name=="Worker_"+person && t.gameObject.activeInHierarchy);
        Assert.IsNotNull(row,"Player worker command must be present");
        var button=row.GetComponents<Component>().Single(c=>c.GetType().FullName=="UnityEngine.UI.Button");
        Call(Get(button,"onClick"),"Invoke");Call(_host,"RefreshNow");
    }
    private void Advance(long minute)
    {
        var result=Clock.AdvanceTo(new WorldInstant(minute));int retries=0;
        while(result.Outcome==WorldAdvanceOutcome.Yielded && retries++<100) result=Clock.ResumeAdvance(Clock.AdvanceSession.RequestId);
        Assert.AreEqual(WorldAdvanceOutcome.Completed,result.Outcome,result.Diagnostic);Call(_host,"RefreshNow");
    }
    [UnityTest] public IEnumerator HomeWorkersActualLaborTwoIndependentFunctionsAndContinue()
    {
        string a=Place(4),b=Place(8),p1=Snapshot.Persons[0].PersonId,p2=Snapshot.Persons[1].PersonId;
        Static("VillageRuntimeBridge","TryAssignPerson",p1,"ws_farm_01");Static("VillageRuntimeBridge","TryAssignPerson",p2,"ws_kitchen_01");
        WorkerButton(a,p1);WorkerButton(b,p2);var firstView=BuildingView(a);
        Assert.AreEqual("ws_farm_01",Assignments.GetWorkplaceId(p1));
        Advance(479);Assert.AreEqual(0,Projects.ActiveProjects[0].LaborRemainder);Advance(480);Advance(481);
        Assert.AreEqual("construction:"+a,Snapshot.Person(p1).CurrentPlaceId);Assert.Greater(Projects.ActiveProjects[0].LaborRemainder,0);
        WorkerButton(a,p1);Assert.AreEqual(0,Projects.ActiveProjects.Single(p=>p.ParticipantPersonIds.Contains(p2)==false).ParticipantPersonIds.Count);
        WorkerButton(a,p1);Advance(17280);
        Assert.AreSame(firstView,BuildingView(a));Assert.AreEqual(BuildingLifecycleState.Completed,Snapshot.Building(a).LifecycleState);
        Assert.AreEqual(BuildingLifecycleState.Completed,Snapshot.Building(b).LifecycleState);
        Static("VillageRuntimeBridge","RemovePersonAssignment",p1);Static("VillageRuntimeBridge","RemovePersonAssignment",p2);
        WorkerButton(a,p1);WorkerButton(b,p2);string wa="workplace:"+a+":main",wb="workplace:"+b+":main";
        Assert.AreEqual(wa,Assignments.GetWorkplaceId(p1));Assert.AreEqual(wb,Assignments.GetWorkplaceId(p2));
        Advance(17761);Assert.AreEqual(wa,Snapshot.Person(p1).CurrentPlaceId);Assert.AreEqual(wb,Snapshot.Person(p2).CurrentPlaceId);
        Assert.IsTrue((bool)Static("SaveSystem","Save",_player));string json=File.ReadAllText(SavePath);
        StringAssert.DoesNotContain("\"PlaceId\": \"construction:",json);StringAssert.DoesNotContain("FunctionProfileId",json);
        Call(Find("MainMenuUI"),"OnContinue");yield return null;yield return null;Clock.Pause();Call(_host,"RefreshNow");
        Assert.AreEqual(wa,Assignments.GetWorkplaceId(p1));Assert.AreEqual(wb,Assignments.GetWorkplaceId(p2));
        Assert.AreEqual(2,Snapshot.Buildings.Count);Assert.AreEqual(1,Assignments.GetOccupantCount(wa));Assert.AreEqual(1,Assignments.GetOccupantCount(wb));
    }
    [UnityTest] public IEnumerator ZeroWorkersAndLateLoadFailureRetainsWholeConstructionWorld()
    {
        string a=Place(4),person=Snapshot.Persons[0].PersonId;WorkerButton(a,person);Advance(600);WorkerButton(a,person);
        Assert.IsTrue((bool)Static("SaveSystem","Save",_player));string zero=File.ReadAllText(SavePath);
        Assert.IsNotNull(Static("SaveSystem","Load"));Assert.AreEqual(0,Projects.ActiveProjects[0].ParticipantPersonIds.Count);
        Place(8);string before=JsonUtility.ToJson(Save);
        string bad=System.Text.RegularExpressions.Regex.Replace(zero,"\"LastOpportunityMinute\": [0-9]+","\"LastOpportunityMinute\": 999999");
        Assert.IsTrue(zero!=bad,"Future marker injection must change the serialized input");File.WriteAllText(SavePath,bad);LogAssert.Expect(LogType.Warning,"[SaveSystem] Load rejected: future opportunity marker.");
        Assert.IsNull(Static("SaveSystem","Load"));Assert.AreEqual(before,JsonUtility.ToJson(Save));
        Assert.IsNotNull(Static("SaveSystem","NewGame"));Assert.AreEqual(0,Save.Buildings.Instances.Count);
        Assert.IsEmpty(Projects.ActiveProjects);Assert.IsFalse(Assignments.WorkplaceDefs.Any(w=>w.WorkplaceId.StartsWith("workplace:")));
        yield return null;
    }
}
}
