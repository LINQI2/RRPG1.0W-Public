using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace RRPG.ProductHome.Tests
{
public sealed class ProductHomeFoundationPlayTests
{
    private const BindingFlags S=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static;
    private const BindingFlags I=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance;
    private object _host,_town,_player;
    private string _id;
    private static Type T(string name)=>Type.GetType(name+", Assembly-CSharp",true);
    private static object Static(string type,string method,params object[] args)=>T(type).GetMethod(method,S).Invoke(null,args);
    private static object Call(object instance,string method,params object[] args)=>instance.GetType().GetMethod(method,I).Invoke(instance,args);
    private static object Get(object instance,string property)=>instance.GetType().GetProperty(property,I).GetValue(instance);
    private static object Find(string type)=>UnityEngine.Object.FindObjectOfType(T(type),true);
    private static VillageTime Clock=>(VillageTime)T("VillageRuntimeBridge").GetField("Time",S).GetValue(null);
    private ProductHomeSnapshot Snapshot=>(ProductHomeSnapshot)Get(_host,"Snapshot");
    private HomeSelectionContext Selection=>(HomeSelectionContext)Get(_host,"Selection");
    private Component View(string id,bool person=true)=>(Component)((IDictionary)Get(_host,person ? "PersonViews" : "PlaceViews"))[id];
    private string Domain()=>JsonUtility.ToJson(Static("VillageRuntimeBridge","BuildSaveData"))+JsonUtility.ToJson(Static("ActivityRuntimeBridge","BuildSaveData"));
    [UnitySetUp] public IEnumerator Setup()
    {
        Static("VillageRuntimeBridge","ResetForTests");
        SceneManager.LoadScene("SampleScene");yield return null;yield return null;
        _player=Static("SaveSystem","NewGame");Assert.IsNotNull(_player);Assert.IsTrue((bool)Static("SaveSystem","Save",_player));
        Call(Find("MainMenuUI"),"OnContinue");yield return null;yield return null;
        _town=Find("TownUI");_player=Get(_town,"PlayerData");Clock.Pause();
        _host=Find("ProductHomeWorldHost");Assert.IsNotNull(_host);Call(_host,"RefreshNow");_id=Snapshot.Persons[0].PersonId;
    }
    [TearDown] public void Cleanup()
    {
        Static("VillageRuntimeBridge","ResetForTests");
        string path=(string)Static("SaveSystem","GetSavePath");if(File.Exists(path)) File.Delete(path);
    }

    [UnityTest] public IEnumerator StableEntity_AwayReturnAndSimulationGating()
    {
        var view=View(_id);var place=View(Snapshot.Places[0].PlaceId,false);var home=View("Home:"+_id,false);
        Assert.IsTrue((bool)Get(view,"ResolvedSharedAsset"));
        Static("VillageRuntimeBridge","TryAssignPerson",_id,"ws_farm_01");Call(_host,"RefreshNow");
        Assert.AreSame(view,View(_id));
        Static("VillageRuntimeBridge","SetExpeditionPersonIds",(object)new[]{_id});Call(_host,"RefreshNow");
        Assert.IsFalse(view.gameObject.activeSelf);Assert.IsTrue(Snapshot.Person(_id).Away);
        Assert.AreSame(home,View("Home:"+_id,false));
        Static("VillageRuntimeBridge","ClearExpeditionPersonIds");Call(_host,"RefreshNow");
        Assert.AreSame(view,View(_id));Assert.IsTrue(view.gameObject.activeSelf);
        Assert.AreSame(home,View("Home:"+_id,false));
        Assert.AreSame(place,View(place.GetType().GetProperty("Identity").GetValue(place) as string,false));
        Static("VillageRuntimeBridge","SetSimulationActive",false);yield return null;
        Assert.IsFalse((bool)Get(_host,"WorldVisible"));Assert.AreSame(view,View(_id));
        Static("VillageRuntimeBridge","SetSimulationActive",true);yield return null;
        Assert.IsTrue((bool)Get(_host,"WorldVisible"));Assert.AreSame(view,View(_id));
    }
    [UnityTest] public IEnumerator Traveling_DoesNotArriveOrAdvanceClock_ThenUsesActualArrival()
    {
        // Establish an actual origin; bootstrap person ordering must not make this a no-op farm assignment.
        Static("VillageRuntimeBridge","RemovePersonAssignment",_id);
        Assert.AreEqual(VillageAssignmentResult.Success,Static("VillageRuntimeBridge","TryAssignPerson",_id,"ws_kitchen_01"));
        Assert.AreEqual(WorldAdvanceOutcome.Completed,Clock.AdvanceTo(new WorldInstant(500)).Outcome);
        Call(_host,"RefreshNow");Assert.AreEqual("ws_kitchen_01",Snapshot.Person(_id).CurrentPlaceId);
        Assert.IsTrue((bool)Static("VillageRuntimeBridge","RemovePersonAssignment",_id));
        Assert.AreEqual(VillageAssignmentResult.Success,Static("VillageRuntimeBridge","TryAssignPerson",_id,"ws_farm_01"));
        Call(_host,"RefreshNow");Selection.SelectPerson(_id);Call(_host,"RefreshNow");
        var view=View(_id);Assert.IsTrue(Snapshot.Person(_id).Traveling);Assert.IsNull(Snapshot.Person(_id).CurrentPlaceId);
        string before=Domain();long minute=Clock.CurrentInstant.Minute;
        Call(view,"StepPresentation",100f);Call(_host,"RefreshNow");
        Assert.AreEqual(minute,Clock.CurrentInstant.Minute);Assert.AreEqual(before,Domain());
        StringAssert.Contains("尚未到达",(string)Get(Get(_host,"Inspector"),"DisplayedText"));
        var destination=View("ws_farm_01",false);Assert.Greater(Vector2.Distance(view.transform.position,destination.transform.position),.05f);
        Vector3 beforeArrival=view.transform.position;
        Clock.AdvanceTo(new WorldInstant(520));Call(_host,"RefreshNow");
        Assert.AreEqual(beforeArrival,view.transform.position,"实际到场只更新目标，不瞬移表现位置");
        Assert.IsFalse(Snapshot.Person(_id).Traveling);Assert.AreEqual("ws_farm_01",Snapshot.Person(_id).CurrentPlaceId);
        Assert.AreSame(view,View(_id));CollectionAssert.Contains(Snapshot.Place("ws_farm_01").OccupantPersonIds,_id);
        before=Domain();
        Call(view,"StepPresentation",.1f);
        Assert.Greater(Vector3.Distance(beforeArrival,view.transform.position),0);
        Assert.LessOrEqual(Vector3.Distance(beforeArrival,view.transform.position),.201f);
        Call(_host,"RefreshNow");
        Vector3 target=(Vector3)view.GetType().GetField("_target",I).GetValue(view);
        Call(view,"StepPresentation",100f);
        Assert.AreEqual(target,view.transform.position);
        Assert.AreEqual(before,Domain(),"走完最后一段不修改模拟事实");
        yield return null;
    }
    [UnityTest] public IEnumerator CameraAndSelectionNotSaved_LoadRebuildsFromExistingAuthority()
    {
        Static("VillageRuntimeBridge","TryAssignPerson",_id,"ws_farm_01");Call(_host,"RefreshNow");
        string before=Domain();Selection.SelectPerson(_id);Assert.IsTrue((bool)Call(_host,"FocusSelected"));
        var cameraControl=Get(_host,"WorldCamera");var camera=(Camera)Get(cameraControl,"ViewCamera");
        Assert.AreEqual(View(_id).transform.position.x,camera.transform.position.x);
        Call(cameraControl,"Pan",new Vector2(2,1));Call(cameraControl,"Zoom",-2f);Assert.Less(camera.orthographicSize,7.5f);
        Selection.SelectPlace("ws_farm_01");Assert.IsTrue((bool)Call(_host,"FocusSelected"));
        Assert.AreEqual(View("ws_farm_01",false).transform.position.x,camera.transform.position.x);Assert.AreEqual(before,Domain());
        Assert.IsTrue((bool)Static("SaveSystem","Save",_player));
        string json=File.ReadAllText((string)Static("SaveSystem","GetSavePath"));
        foreach(string token in new[]{"ProductHome","HomeSelection","WorldCamera","WorldOrigin","PresentationIntent"}) StringAssert.DoesNotContain(token,json);
        var loaded=Static("SaveSystem","Load");Assert.IsNotNull(loaded);Call(_town,"Initialize",loaded);Call(_host,"RefreshNow");
        Assert.AreEqual(_id,Snapshot.Person(_id).PersonId);Assert.AreEqual("ws_farm_01",Snapshot.Person(_id).AssignmentId);
        Assert.IsNotNull(View(_id));Assert.IsNotNull(Snapshot.Place("ws_farm_01"));yield return null;
    }
}
}
