using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

public partial class VillageCommitmentProductIntegrationPlayTests
{
    private static void ClickOnsite(string name)
    {
        var go=GameObject.Find(name);Assert.IsNotNull(go,name);var button=go.GetComponent<Button>();
        Assert.IsTrue(button.interactable);Canvas.ForceUpdateCanvases();
        var rect=go.GetComponent<RectTransform>();var screen=RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center));
        Assert.IsTrue(screen.x>=0 && screen.x<=Screen.width && screen.y>=0 && screen.y<=Screen.height,name+" outside screen");
        var pointer=new PointerEventData(EventSystem.current){position=screen,button=PointerEventData.InputButton.Left};
        var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(pointer,hits);Assert.IsNotEmpty(hits);
        Assert.AreEqual(button,ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject).GetComponent<Button>(),"Input blocked: "+name);
        ExecuteEvents.Execute(button.gameObject,pointer,ExecuteEvents.pointerClickHandler);
    }
    [UnityTest] public IEnumerator IntradayOnsite_RealTownVisibleClickableCloseDoesNotChangeSimulation()
    {
        SceneManager.LoadScene("SampleScene");yield return null;yield return null;var town=EnterTown();yield return null;
        InvokeInstance(FindInstance("HomeProductShell"),"ToggleCompatibility");yield return null;
        var clock=(VillageTime)GetBridgeField("Time");clock.SecondsPerDay=86400; // keep true driver, avoid frame-dependent minute changes in UI assertions
        yield return new WaitForSecondsRealtime(.3f);ClickOnsite("OpenOnsite");yield return null;
        var layerType=Type.GetType("VillageWorldLayer, Assembly-CSharp",true);var layer=UnityEngine.Object.FindObjectOfType(layerType);
        Assert.IsNotNull(layer);Assert.IsTrue((bool)layerType.GetProperty("OnsitePanelVisible").GetValue(layer));
        string summary=(string)layerType.GetProperty("OnsiteSummary").GetValue(layer);
        StringAssert.Contains("现场",summary);StringAssert.Contains(":",summary);
        ClickOnsite("OnsitePerson0");yield return null;Assert.IsNotNull(GameObject.Find("OnsiteDetail"));
        string before=JsonUtility.ToJson(InvokeStatic(Type.GetType("ActivityRuntimeBridge, Assembly-CSharp",true),"BuildSaveData"));
        ClickOnsite("CloseOnsite");yield return null;
        Assert.IsFalse((bool)layerType.GetProperty("OnsitePanelVisible").GetValue(layer));
        Assert.AreEqual(before,JsonUtility.ToJson(InvokeStatic(Type.GetType("ActivityRuntimeBridge, Assembly-CSharp",true),"BuildSaveData")));
        Assert.IsTrue((bool)GetBridgeField("IsSimulationActive"));
        var schedule=Activator.CreateInstance(Type.GetType("IntradayActivitySchedule, Assembly-CSharp",true),new object[]{new int[0],15,30,null});
        InvokeStatic(BridgeType,"ConfigureActivitySchedule",schedule);long now=clock.CurrentInstant.Minute;
        Assert.AreEqual(WorldAdvanceOutcome.Completed,clock.AdvanceTo(clock.CurrentInstant.AddMinutes(1)).Outcome);
        Assert.AreEqual(now+1,clock.CurrentInstant.Minute);ClickOnsite("OpenOnsite");yield return null;
        Assert.IsTrue((bool)layerType.GetProperty("OnsitePanelVisible").GetValue(layer));
        ClickOnsite("OnsitePerson0");
        string person=(string)layerType.GetField("_selectedPerson",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).GetValue(layer);
        var activity=Type.GetType("ActivityRuntimeBridge, Assembly-CSharp",true);
        var stamp=Activator.CreateInstance(Type.GetType("RRPG.AutonomousActivity.ActivityDayStamp, AutonomousActivity.Runtime",true),clock.Year,clock.Month,clock.Day);
        now=clock.CurrentInstant.Minute;
        var window=Activator.CreateInstance(Type.GetType("RRPG.AutonomousActivity.ActivityDailyWindow, AutonomousActivity.Runtime",true),stamp,new[]{person},new[]{person},"onsite:op",now);
        var candidate=Activator.CreateInstance(Type.GetType("RRPG.AutonomousActivity.ActivityCandidate, AutonomousActivity.Runtime",true),"onsite:rest","Rest","product",person,null,"Home",null,
            Enum.ToObject(Type.GetType("RRPG.AutonomousActivity.ActivityTargetParticipation, AutonomousActivity.Runtime",true),0));
        Assert.AreEqual("Success",InvokeStatic(activity,"OpenOpportunity",window).ToString());
        Assert.AreEqual("Success",InvokeStatic(activity,"TryCommitScheduledAtPlace","onsite:instance",candidate,now,now+15,now+75,"Home:"+person).ToString());
        ClickOnsite("CloseOnsite");yield return null;
        Assert.AreEqual(WorldAdvanceOutcome.Completed,clock.AdvanceTo(new WorldInstant(now+15)).Outcome);
        Assert.AreEqual("Executing",InvokeStatic(activity,"QueryActivityInstance","onsite:instance").GetType().GetProperty("State").GetValue(InvokeStatic(activity,"QueryActivityInstance","onsite:instance")).ToString());
        ClickOnsite("OpenOnsite");yield return null;ClickOnsite("OnsitePerson0");ClickOnsite("LeaveOnsiteActivity");yield return null;
        Assert.IsNull(InvokeStatic(activity,"QueryClaim",person));
        Assert.AreEqual("Interrupted",InvokeStatic(activity,"QueryActivityInstance","onsite:instance").GetType().GetProperty("State").GetValue(InvokeStatic(activity,"QueryActivityInstance","onsite:instance")).ToString());
        UnityEngine.ScreenCapture.CaptureScreenshot("TestResults/D-onsite-product.png");yield return null;
    }
}
