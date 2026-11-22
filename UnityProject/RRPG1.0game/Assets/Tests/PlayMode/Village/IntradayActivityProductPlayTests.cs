using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public partial class VillageCommitmentProductIntegrationPlayTests
{
    [UnityTest] public IEnumerator IntradayActivity_RealTownDriver_CommutesThenExecutes_SaveDoesNotComplete()
    {
        SceneManager.LoadScene("SampleScene");yield return null;yield return null;
        var town=EnterTown();yield return null;InvokeStatic(BridgeType,"SetSimulationActive",false);
        var clock=(VillageTime)GetBridgeField("Time");
        var activity=Type.GetType("ActivityRuntimeBridge, Assembly-CSharp",true);
        var schedule=Activator.CreateInstance(Type.GetType("IntradayActivitySchedule, Assembly-CSharp",true),new object[]{new int[0],15,30,null});
        InvokeStatic(BridgeType,"ConfigureActivitySchedule",schedule);
        string person=FirstPersonId(town);long now=clock.CurrentInstant.Minute;
        var stamp=Activator.CreateInstance(Type.GetType("RRPG.AutonomousActivity.ActivityDayStamp, AutonomousActivity.Runtime",true),clock.Year,clock.Month,clock.Day);
        var window=Activator.CreateInstance(Type.GetType("RRPG.AutonomousActivity.ActivityDailyWindow, AutonomousActivity.Runtime",true),
            stamp,new[]{person},new[]{person},"product:commute",now);
        var candidate=Activator.CreateInstance(Type.GetType("RRPG.AutonomousActivity.ActivityCandidate, AutonomousActivity.Runtime",true),
            "product:rest","Rest","product",person,null,"Home",null,Enum.ToObject(Type.GetType("RRPG.AutonomousActivity.ActivityTargetParticipation, AutonomousActivity.Runtime",true),0));
        Assert.AreEqual("Success",InvokeStatic(activity,"OpenOpportunity",window).ToString());
        Assert.AreEqual("Success",InvokeStatic(activity,"TryCommitScheduled","product:instance",candidate,now,now+15,now+75).ToString());
        Func<string> state=()=>InvokeStatic(activity,"QueryActivityInstance","product:instance").GetType().GetProperty("State")
            .GetValue(InvokeStatic(activity,"QueryActivityInstance","product:instance")).ToString();
        Assert.AreEqual("Committed",state());
        object player=town.GetType().GetProperty("PlayerData").GetValue(town);
        Assert.IsTrue((bool)InvokeStatic(SaveSystemType,"Save",player));Assert.AreEqual("Committed",state());
        clock.SecondsPerDay=120;InvokeStatic(BridgeType,"SetSimulationActive",true);
        float deadline=Time.realtimeSinceStartup+5;
        while(clock.CurrentInstant.Minute<now+15 && Time.realtimeSinceStartup<deadline) yield return null;
        Assert.GreaterOrEqual(clock.CurrentInstant.Minute,now+15);Assert.Less(clock.CurrentInstant.Minute,now+75);
        Assert.AreEqual("Executing",state());Assert.AreEqual("product:instance",InvokeStatic(activity,"QueryClaim",person));
        Assert.IsTrue(((Component)town).gameObject.activeInHierarchy);
        deadline=Time.realtimeSinceStartup+8;
        while(clock.CurrentInstant.Minute<now+75 && Time.realtimeSinceStartup<deadline) yield return null;
        InvokeStatic(BridgeType,"SetSimulationActive",false);
        Assert.GreaterOrEqual(clock.CurrentInstant.Minute,now+75);Assert.AreEqual("Completed",state());
        Assert.IsNull(InvokeStatic(activity,"QueryClaim",person));
    }
}
