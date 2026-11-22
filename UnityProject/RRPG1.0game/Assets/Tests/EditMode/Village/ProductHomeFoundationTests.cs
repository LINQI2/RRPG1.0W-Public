using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace RRPG.ProductHome.Tests
{
public sealed class ProductHomeFoundationTests
{
    private const BindingFlags F=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static;
    private object _player;
    private string _id;
    private static Type T(string name)=>Type.GetType(name+", Assembly-CSharp",true);
    private static object Call(string type,string method,params object[] args)=>T(type).GetMethod(method,F).Invoke(null,args);
    private static ProductHomeSnapshot Read()=>(ProductHomeSnapshot)Call("ProductHomeReadAdapter","Read");
    private static VillageTime Clock=>(VillageTime)T("VillageRuntimeBridge").GetField("Time",F).GetValue(null);
    private static string SaveState()=>JsonUtility.ToJson(Call("VillageRuntimeBridge","BuildSaveData"))+JsonUtility.ToJson(Call("ActivityRuntimeBridge","BuildSaveData"));
    [SetUp] public void Setup()
    {
        Call("VillageRuntimeBridge","ResetForTests");
        _player=Call("SaveSystem","NewGame");Call("VillageRuntimeBridge","EnsureInitialized",new object[]{null});
        Call("VillageRuntimeBridge","SetSimulationPersonSource",_player);Clock.Pause();
        _id=Read().Persons[0].PersonId;
    }
    [TearDown] public void Cleanup()=>Call("VillageRuntimeBridge","ResetForTests");
    [Test] public void IdentityAndNameAreFromLongTermSource()
    {
        var input=(System.Collections.IEnumerable)Call("VillageRuntimeBridge","BuildPersonInputs",_player);
        foreach (VillagePersonInput p in input) { Assert.AreEqual(p.DisplayName,Read().Person(p.PersonId).DisplayName); }
        Assert.IsTrue(Read().Persons.Select(p=>p.PersonId).Distinct().Count()==Read().Persons.Count);
    }
    [Test] public void AssignmentCapacityAndReservedOccupancyUseExistingAuthority()
    {
        Assert.AreEqual(VillageAssignmentResult.Success,Call("VillageRuntimeBridge","TryAssignPerson",_id,"ws_farm_01"));
        var snapshot=Read();var assignments=(VillageAssignmentSystem)T("VillageRuntimeBridge").GetField("Assignments",F).GetValue(null);
        Assert.AreEqual(assignments.GetWorkplaceId(_id),snapshot.Person(_id).AssignmentId);
        Assert.AreEqual(assignments.GetWorkplaceDef("ws_farm_01").Capacity,snapshot.Place("ws_farm_01").Capacity);
        CollectionAssert.AreEquivalent(assignments.GetOccupantPersonIds("ws_farm_01"),snapshot.Place("ws_farm_01").AssignedPersonIds);
        Assert.IsFalse(snapshot.Place("ws_farm_01").OccupantPersonIds.Contains(_id),"Assignment alone is not arrival");
    }
    [Test] public void SnapshotAndSelectionDoNotWriteRuntimeOrRegistry()
    {
        string before=SaveState();var registry=(VillageWorldRegistry)T("VillageRuntimeBridge").GetField("World",F).GetValue(null);
        int count=registry.Places.Count;
        var s=new HomeSelectionContext();s.SelectPerson(_id);s.SelectPlace(Read().Places[0].PlaceId);s.Clear();
        for(int i=0;i<20;i++) Read();
        Assert.AreEqual(before,SaveState());Assert.AreEqual(count,registry.Places.Count);
    }
    [Test] public void CollectionsCannotBeMutatedThroughSnapshot()
    {
        var snapshot=Read();Assert.Throws<NotSupportedException>(()=>((System.Collections.Generic.IList<HomePersonSnapshot>)snapshot.Persons).Clear());
        Assert.Throws<NotSupportedException>(()=>((System.Collections.Generic.IList<string>)snapshot.Places[0].AssignedPersonIds).Add(_id));
    }
}
}
