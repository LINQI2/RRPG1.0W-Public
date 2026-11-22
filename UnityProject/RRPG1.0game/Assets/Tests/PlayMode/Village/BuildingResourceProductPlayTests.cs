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
public sealed class BuildingResourceProductPlayTests
{
    private const BindingFlags S=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static;
    private const BindingFlags I=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance;
    private static Type T(string name)=>Type.GetType(name+", Assembly-CSharp",true);
    private static object Static(string type,string method,params object[] args)=>T(type).GetMethod(method,S).Invoke(null,args);
    private static object Call(object value,string method,params object[] args)=>value.GetType().GetMethods(I).First(m=>m.Name==method && m.GetParameters().Length==args.Length).Invoke(value,args);
    private static object Get(object value,string name)=>value.GetType().GetProperty(name,I).GetValue(value);
    private static object Find(string type)=>UnityEngine.Object.FindObjectOfType(T(type),true);
    private VillageTime Clock=>(VillageTime)T("VillageRuntimeBridge").GetField("Time",S).GetValue(null);
    private VillageResourceState Resources=>(VillageResourceState)T("VillageRuntimeBridge").GetField("Resources",S).GetValue(null);
    private VillageIntradayWork Work=>(VillageIntradayWork)T("VillageRuntimeBridge").GetProperty("IntradayWork",S).GetValue(null);
    private VillageAssignmentSystem Assignments=>(VillageAssignmentSystem)T("VillageRuntimeBridge").GetField("Assignments",S).GetValue(null);
    private object _host,_player;
    private ProductHomeSnapshot Snapshot=>(ProductHomeSnapshot)Get(_host,"Snapshot");
    private HomeSelectionContext Selection=>(HomeSelectionContext)Get(_host,"Selection");
    private string SavePath=>(string)Static("SaveSystem","GetSavePath");
    private VillageSaveData Save=>(VillageSaveData)Static("VillageRuntimeBridge","BuildSaveData");
    [UnitySetUp] public IEnumerator Setup()
    {
        Static("VillageRuntimeBridge","ResetForTests");SceneManager.LoadScene("SampleScene");yield return null;yield return null;
        _player=Static("SaveSystem","NewGame");Assert.IsTrue((bool)Static("SaveSystem","Save",_player));
        Call(Find("MainMenuUI"),"OnContinue");yield return null;yield return null;
        _player=Get(Find("TownUI"),"PlayerData");Clock.Pause();_host=Find("ProductHomeWorldHost");Call(_host,"RefreshNow");
    }
    [TearDown] public void Cleanup(){Static("VillageRuntimeBridge","ResetForTests");if(File.Exists(SavePath)) File.Delete(SavePath);}
    private string Place(string definition,float x)
    {
        var build=Get(Get(_host,"Shell"),"Build");Call(build,"OpenCatalog");Assert.IsTrue((bool)Call(build,"Choose",definition));
        Call(build,"SetCandidate",new Vector2(x,-4));Assert.IsTrue((bool)Call(build,"Confirm"));Call(build,"Cancel");Call(_host,"RefreshNow");return Selection.Id;
    }
    private void WorkerButton(string building,string person)
    {
        Selection.SelectBuilding(building);Call(_host,"RefreshNow");
        var inspector=(Component)Get(_host,"Inspector");var row=inspector.GetComponentsInChildren<Transform>(true).LastOrDefault(t=>t.name=="Worker_"+person && t.gameObject.activeInHierarchy);
        Assert.IsNotNull(row);var button=row.GetComponents<Component>().Single(c=>c.GetType().FullName=="UnityEngine.UI.Button");
        Call(Get(button,"onClick"),"Invoke");Call(_host,"RefreshNow");
    }
    private void Advance(long minute)
    {
        var result=Clock.AdvanceTo(new WorldInstant(minute));int attempts=0;
        while(result.Outcome==WorldAdvanceOutcome.Yielded && attempts++<100) result=Clock.ResumeAdvance(Clock.AdvanceSession.RequestId);
        Assert.AreEqual(WorldAdvanceOutcome.Completed,result.Outcome,result.Diagnostic);Call(_host,"RefreshNow");
    }
    [UnityTest] public IEnumerator HomeBuildActualConstructionProductionAndContinuePaidBatch()
    {
        string a=Place("building_farm",4),b=Place("building_kitchen",8),p1=Snapshot.Persons[0].PersonId,p2=Snapshot.Persons[1].PersonId;
        WorkerButton(a,p1);WorkerButton(b,p2);Advance(479);
        Assert.AreEqual(0,Resources.Ingredients);Assert.AreEqual(0,Resources.Food);Assert.IsEmpty(Work.ToSaveData().Batches);
        Advance(14400);Assert.AreEqual(BuildingLifecycleState.Completed,Snapshot.Building(a).LifecycleState);
        Assert.AreEqual(BuildingLifecycleState.Completed,Snapshot.Building(b).LifecycleState);
        WorkerButton(a,p1);WorkerButton(b,p2);string wa="workplace:"+a+":main",wb="workplace:"+b+":main";
        Advance(14880);Assert.AreEqual(wa,Snapshot.Person(p1).CurrentPlaceId);Assert.AreEqual(wb,Snapshot.Person(p2).CurrentPlaceId);
        Advance(14920);Assert.AreEqual(1,Resources.Ingredients);Assert.AreEqual(0,Resources.Food);
        Advance(14940);Assert.AreEqual(0,Resources.Ingredients);Assert.AreEqual(240,Work.ToSaveData().Batches.Single(x=>x.WorkplaceId==wb).LaborNumerator);
        Assert.IsTrue((bool)Static("SaveSystem","Save",_player));string saved=JsonUtility.ToJson(Save);
        StringAssert.Contains("\"SaveVersion\": 1",File.ReadAllText(SavePath));
        Call(Find("MainMenuUI"),"OnContinue");yield return null;yield return null;Clock.Pause();Call(_host,"RefreshNow");
        Assert.AreEqual(saved,JsonUtility.ToJson(Save));Assert.AreEqual(wa,Assignments.GetWorkplaceId(p1));Assert.AreEqual(wb,Assignments.GetWorkplaceId(p2));
        Advance(14960);Assert.AreEqual(1,Resources.Food);Assert.AreEqual(1,Resources.Ingredients);
        Assert.AreEqual(0,Work.ToSaveData().Batches.Single(x=>x.WorkplaceId==wb).LaborNumerator);
    }
}
}
