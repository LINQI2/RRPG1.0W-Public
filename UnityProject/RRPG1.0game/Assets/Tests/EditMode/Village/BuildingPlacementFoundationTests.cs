using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using RRPG.StaticConfig;
using UnityEngine;

namespace RRPG.Building.Tests
{
public sealed class BuildingPlacementFoundationTests
{
    private const BindingFlags S=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static;
    private const BindingFlags I=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance;
    private static Type T(string name)=>Type.GetType(name+", Assembly-CSharp",true);
    private static object Call(object target,string name,params object[] args)=>target.GetType().GetMethod(name,I).Invoke(target,args);
    private static object Static(string type,string name,params object[] args)=>T(type).GetMethod(name,S).Invoke(null,args);
    private static object Get(object target,string name)=>target.GetType().GetProperty(name,I).GetValue(target);
    private object _runtime;private BuildingRegistry _definitions;
    [SetUp] public void Setup()
    {Static("VillageRuntimeBridge","ResetForTests");_definitions=new BuildingRegistry(new[]{new BuildingDefinition{BuildingDefinitionId="building_house",DisplayName="House",Category="Test",FootprintWidth=2,FootprintDepth=2}});_runtime=Activator.CreateInstance(T("BuildingRuntime"));}
    [TearDown] public void Cleanup()=>Static("VillageRuntimeBridge","ResetForTests");
    private object Place(float x,float y)
    {object[] args={_definitions,"building_house",x,y,null,null};Assert.IsTrue((bool)Call(_runtime,"TryPlace",args),(string)args[5]);return args[4];}
    private string Preview(float x,float y)=>(string)Call(_runtime,"Preview",_definitions,"building_house",x,y);
    private VillageBuildingsSaveData Save()=>(VillageBuildingsSaveData)Call(_runtime,"ToSaveData");
    private bool Prepare(VillageBuildingsSaveData data,out object result)
    {object[] args={data,_definitions,null,null};bool ok=(bool)Static("BuildingRuntime","TryPrepareRestore",args);result=args[2];return ok;}
    [Test] public void TwoOfSameDefinitionHaveIndependentStableIdentityAndCoordinates()
    {
        Assert.IsTrue(Prepare(null,out var empty));Assert.AreEqual(0,((VillageBuildingsSaveData)Call(empty,"ToSaveData")).Instances.Count);
        var first=Place(0,0);var second=Place(3,0);Assert.AreNotEqual(Get(first,"InstanceId"),Get(second,"InstanceId"));
        Assert.AreEqual("building_house",Get(first,"BuildingDefinitionId"));Assert.AreEqual(BuildingLifecycleState.UnderConstruction,Get(first,"LifecycleState"));
        Assert.AreSame(first,Call(_runtime,"Find",Get(first,"InstanceId")));Assert.AreEqual(2,((ICollection)Get(_runtime,"All")).Count);
        Assert.IsTrue(Prepare(Save(),out var restored));Assert.AreEqual(JsonUtility.ToJson(Save()),JsonUtility.ToJson(Call(restored,"ToSaveData")));
        _runtime=restored;var third=Place(6,0);Assert.AreNotEqual(Get(second,"InstanceId"),Get(third,"InstanceId"));
    }
    [Test] public void OverlapRejectedAdjacencyValidAndStalePreviewRevalidated()
    {Assert.AreEqual("",Preview(0,0));Place(0,0);Assert.IsNotEmpty(Preview(0,0));Assert.AreEqual("",Preview(2,0));object[] args={_definitions,"building_house",0f,0f,null,null};Assert.IsFalse((bool)Call(_runtime,"TryPlace",args));Assert.AreEqual(1,Save().Instances.Count);Place(2,0);}
    [Test] public void UnknownDefinitionAndInvalidAllocatorFailWithoutMutatingCurrent()
    {
        Place(0,0);string before=JsonUtility.ToJson(Save());var bad=Save();bad.Instances[0].BuildingDefinitionId="unknown";
        Assert.IsFalse(Prepare(bad,out _));Assert.AreEqual(before,JsonUtility.ToJson(Save()));
        bad=Save();bad.NextSequence=1;Assert.IsFalse(Prepare(bad,out _));Assert.AreEqual(before,JsonUtility.ToJson(Save()));
    }
}
}
