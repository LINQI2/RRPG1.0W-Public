using System;
using System.Collections;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace RRPG.Building.Tests
{
public sealed class BuildingPlacementProductPlayTests
{
    private const BindingFlags S=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static;
    private const BindingFlags I=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance;
    private static Type T(string name)=>Type.GetType(name+", Assembly-CSharp",true);
    private static object Static(string type,string method,params object[] args)=>T(type).GetMethod(method,S).Invoke(null,args);
    private static object Call(object instance,string method,params object[] args)=>instance.GetType().GetMethod(method,I).Invoke(instance,args);
    private static object Get(object instance,string name)=>instance.GetType().GetProperty(name,I).GetValue(instance);
    private static object Find(string type)=>UnityEngine.Object.FindObjectOfType(T(type),true);
    private object _player;
    private VillageTime Clock=>(VillageTime)T("VillageRuntimeBridge").GetField("Time",S).GetValue(null);
    private object Runtime=>T("VillageRuntimeBridge").GetProperty("Buildings",S).GetValue(null);
    private VillageBuildingsSaveData Buildings=>(VillageBuildingsSaveData)Call(Runtime,"ToSaveData");
    private string Path=>(string)Static("SaveSystem","GetSavePath");
    [UnitySetUp] public IEnumerator Setup()
    {
        Static("VillageRuntimeBridge","ResetForTests");SceneManager.LoadScene("SampleScene");yield return null;yield return null;
        _player=Static("SaveSystem","NewGame");Assert.IsNotNull(_player);Assert.IsTrue((bool)Static("SaveSystem","Save",_player));
        Call(Find("MainMenuUI"),"OnContinue");yield return null;yield return null;
        _player=Get(Find("TownUI"),"PlayerData");Clock.Pause();
    }
    [TearDown] public void Cleanup(){Static("VillageRuntimeBridge","ResetForTests");if(File.Exists(Path)) File.Delete(Path);}
    private void Place(Vector2 point)
    {object[] args={"building_house",point.x,point.y,null,null};Assert.IsTrue((bool)Static("VillageRuntimeBridge","TryPlaceBuilding",args),(string)args[4]);}
    [UnityTest] public IEnumerator UnknownDefinitionOrFailedLoadRetainsCurrentBuildings()
    {
        Place(new Vector2(5,-4));Assert.IsTrue((bool)Static("SaveSystem","Save",_player));string valid=File.ReadAllText(Path);
        Place(new Vector2(8,-4));string before=JsonUtility.ToJson(Buildings);
        File.WriteAllText(Path,valid.Replace("\"BuildingDefinitionId\": \"building_house\"","\"BuildingDefinitionId\": \"unknown\""));
        LogAssert.Expect(LogType.Warning,"[SaveSystem] Load rejected: Unknown BuildingDefinitionId: unknown");Assert.IsNull(Static("SaveSystem","Load"));Assert.AreEqual(before,JsonUtility.ToJson(Buildings));
        File.WriteAllText(Path,valid.Replace("\"SaveVersion\": 1","\"SaveVersion\": 999"));LogAssert.Expect(LogType.Warning,"[SaveSystem] Unsupported save version: 999");
        Assert.IsNull(Static("SaveSystem","Load"));Assert.AreEqual(before,JsonUtility.ToJson(Buildings));yield return null;
        string invalidTime=valid.Replace("\"CurrentWorldMinute\": 0","\"CurrentWorldMinute\": -1");Assert.AreNotEqual(valid,invalidTime);
        File.WriteAllText(Path,invalidTime);LogAssert.Expect(LogType.Warning,new System.Text.RegularExpressions.Regex("\\[SaveSystem\\] Load rejected: .*"));
        Assert.IsNull(Static("SaveSystem","Load"));Assert.AreEqual(before,JsonUtility.ToJson(Buildings),"Prepared one-building snapshot must not replace current two-building world on a later Load rejection");
    }
}
}
