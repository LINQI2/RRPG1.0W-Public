using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using RRPG.Event;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

public partial class ExpeditionReturnIntegrityProductPlayTests
{
    [UnityTest] public IEnumerator Intraday_NativeProgression_ReturnsAtActualArrival()
    {
        var r = new ProductRun { StartMinute = 480 };
        yield return EnterProductRun(r);
        var time = (VillageTime)GetBridgeStatic("Time");
        var departure = time.CurrentInstant;
        var progression = new ExpeditionProgressionResolver();
        var graph = new ExpeditionGraph("arrival", new[] {
            new ExpeditionNodeData("arrival", ExpeditionNodeKind.Battle, durationMinutes: 1800) });
        Assert.AreEqual(ExpeditionProgressionInitResult.Success,
            progression.InitializeProgression(graph, r.ExpectedIds.Count, 100, 1));
        r.Adventure.GetType().GetProperty("Progression", F).SetValue(r.Adventure, progression);
        CallStatic("VillageRuntimeBridge", "SetExpeditionElapsedMinutesProvider",
            (Func<long>)(() => (long)GetPropertyOrField(r.Adventure, "ExpeditionElapsedMinutes")));
        Assert.AreEqual(ExpeditionEnterResult.Success, progression.TryEnterNode("arrival"));
        Assert.AreEqual(departure, time.CurrentInstant, "Node entry does not advance the world");
        Assert.AreEqual(ExpeditionResolveResult.Success,
            progression.TryResolveCurrentNode(ExpeditionNodeOutcome.Success, 0, out var resolution));
        Assert.AreEqual(1800, resolution.DurationMinutesSettled);
        Assert.AreEqual((r.ExpectedIds.Count * 1800L + 1439) / 1440, resolution.TotalSupplyCost);
        Call(r.ExpUI, "OnReturnToTown");
        var record = (ExpeditionRecord)GetBridgeStatic("ActiveReturnRecord");
        Assert.IsTrue(record.Completed);
        Assert.AreEqual(departure.AddMinutes(1800), time.CurrentInstant);
        Assert.AreEqual(840, time.MinuteOfDay);
        Assert.IsTrue(((GameObject)GetField(r.Town, "canvasRoot")).activeInHierarchy);
    }

    [UnityTest] public IEnumerator Intraday_ReturnPending_IsVisibleClickable_ResumesOriginalTarget()
    {
        var r=new ProductRun { StartMinute=480 };
        yield return EnterProductRun(r);
        var time=(VillageTime)GetBridgeStatic("Time");
        var start=time.CurrentInstant;
        SetElapsedProvider(()=>0);
        CallStatic("VillageRuntimeBridge","SetExpeditionElapsedMinutesProvider",(Func<long>)(()=>1800));
        var pending=EventWorldPersistence.FromSaveData(new PendingEventInstanceSaveData {
            EventInstanceId="intraday:pending:1",EventId="evt_test_village_temp_draft",
            TriggerType="VillageRecognitionRaised",TriggerKey="intraday:test",ResolutionSeed=123,
            RoleBindings=new List<RoleBindingSaveData>{new RoleBindingSaveData{RoleId="role_test_village_requester",
                RoleName="请求者",SelectorType="ContextPerson",PersonId=r.ExpectedIds[0],SourceKey="RecognitionPerson"}},
            ContextSnapshot=new List<ContextEntrySaveData>{new ContextEntrySaveData{Kind=2,Key="RecognitionPerson",StringValue=r.ExpectedIds[0]},
                new ContextEntrySaveData{Kind=0,Key="RecognitionType",StringValue="TemporaryDraftBuildup"},
                new ContextEntrySaveData{Kind=1,Key="ObservedCount",IntValue=3}}
        });
        Assert.IsNotNull(pending);
        Action<int> inject= _ => {
            object[] args={pending,null};
            Assert.IsTrue((bool)GetType("EventRuntimeBridge").GetMethod("SetPendingVillageEvent",F).Invoke(null,args));
        };
        time.DayTicked+=inject;
        Call(r.ExpUI,"OnReturnToTown");
        time.DayTicked-=inject;
        Assert.AreEqual(WorldAdvanceState.WaitingForInteraction,time.AdvanceSession.State);
        Assert.AreEqual(start.AddMinutes(1800),time.AdvanceSession.OriginalTarget);
        Assert.AreEqual(0,time.MinuteOfDay);
        Assert.IsFalse(((GameObject)GetField(r.Town,"canvasRoot")).activeInHierarchy);
        var ui=(Component)FindInstance("VillageSocialEventUI");Assert.IsNotNull(ui);
        var root=(GameObject)GetField(ui,"root");
        Assert.IsTrue(root.activeInHierarchy,"existing event canvas must be visible while Town is hidden");
        Assert.AreEqual(RenderMode.ScreenSpaceOverlay,root.GetComponent<Canvas>().renderMode);
        Assert.IsNotNull(root.GetComponent<GraphicRaycaster>());
        Assert.IsNotNull(EventSystem.current);
        string eventBefore = JsonUtility.ToJson(CallStatic("EventRuntimeBridge", "BuildSaveData"));
        Call(ui, "OnEventOptionClicked", "invalid_intraday_option");
        Assert.AreEqual(eventBefore, JsonUtility.ToJson(CallStatic("EventRuntimeBridge", "BuildSaveData")),
            "failed option cannot append history or consume Pending");
        Assert.AreEqual(WorldAdvanceState.WaitingForInteraction, time.AdvanceSession.State);
        var failedClose = (Button)GetField(ui, "closeButton");
        ExecuteEvents.Execute(failedClose.gameObject,
            new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left },
            ExecuteEvents.pointerClickHandler);
        Assert.AreEqual(WorldAdvanceState.WaitingForInteraction, time.AdvanceSession.State,
            "closing an error does not acknowledge the interaction");
        ui.gameObject.SetActive(false);
        Assert.AreEqual(WorldAdvanceState.WaitingForInteraction, time.AdvanceSession.State,
            "disabling the view cannot unlock the world");
        yield return null;
        Assert.IsTrue(root.activeInHierarchy, "the same Pending reopens on the existing driver");
        Assert.AreEqual(eventBefore, JsonUtility.ToJson(CallStatic("EventRuntimeBridge", "BuildSaveData")));
        yield return null;
        Canvas.ForceUpdateCanvases();
        Button button=null;
        foreach(var b in root.GetComponentsInChildren<Button>(true))
            if(b.name=="EventOption_opt_test_village_continue")button=b;
        Assert.IsNotNull(button);Assert.IsTrue(button.interactable);Assert.IsTrue(button.gameObject.activeInHierarchy);
        var rect=(RectTransform)button.transform;var screen=RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center));
        var pointer=new PointerEventData(EventSystem.current){position=screen};
        var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(pointer,hits);
        bool hit=false;foreach(var h in hits)if(h.gameObject==button.gameObject||h.gameObject.transform.IsChildOf(button.transform))hit=true;
        Assert.IsTrue(hit,"existing rendered option must receive an EventSystem raycast");
        ExecuteEvents.Execute(button.gameObject,pointer,ExecuteEvents.pointerClickHandler);
        Assert.IsNull(GetType("EventRuntimeBridge").GetProperty("PendingVillageEvent",F).GetValue(null));
        Assert.AreEqual(WorldAdvanceState.WaitingForInteraction,time.AdvanceSession.State,"choice alone does not resume");
        var close=(Button)GetField(ui,"closeButton");Assert.IsTrue(close.gameObject.activeInHierarchy);
        ExecuteEvents.Execute(close.gameObject,new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left},ExecuteEvents.pointerClickHandler);
        Assert.AreEqual(WorldAdvanceState.Yielded,time.AdvanceSession.State);
        // Resume runs on the existing VillageTimeDriver's next frame, never nested in the UI callback.
        for(int i=0;i<8 && !((ExpeditionRecord)GetBridgeStatic("ActiveReturnRecord")).Completed;i++)yield return null;
        var record=(ExpeditionRecord)GetBridgeStatic("ActiveReturnRecord");
        Assert.IsTrue(record.Completed);Assert.AreEqual(start.AddMinutes(1800),time.CurrentInstant);
        Assert.AreEqual(840,time.MinuteOfDay);Assert.IsTrue(((GameObject)GetField(r.Town,"canvasRoot")).activeInHierarchy);Assert.IsNull(time.AdvanceSession);
        Debug.Log("INTRADAY_EVENT_UI_REPORT visible=True raycast=True click=True resultClose=True resumed=Day2 14:00 completed=True");
    }

    [UnityTest] public IEnumerator Intraday_FinalSaveFailure_FreezesWorldAndManualOperations()
    {
        var r=new ProductRun { StartMinute=480 };yield return EnterProductRun(r);
        var time=(VillageTime)GetBridgeStatic("Time");
        CallStatic("VillageRuntimeBridge","SetExpeditionElapsedMinutesProvider",(Func<long>)(()=>1800));
        SetSaveFailure(()=>true);
        UnityEngine.TestTools.LogAssert.Expect(LogType.Error,new System.Text.RegularExpressions.Regex("最终 Return Save 失败"));
        Call(r.ExpUI,"OnReturnToTown");
        var record=(ExpeditionRecord)GetBridgeStatic("ActiveReturnRecord");
        Assert.AreEqual(ExpeditionReturnPhase.OccupancyCleared,record.Phase);Assert.IsFalse(record.Completed);
        Assert.AreEqual(WorldAdvanceState.AwaitingFinalSave,time.AdvanceSession.State);
        var old=time.CurrentInstant;var resources=GetBridgeStatic("Resources");int food=(int)GetPropertyOrField(resources,"Food");
        string worldBefore=JsonUtility.ToJson(CallStatic("VillageRuntimeBridge","BuildSaveData"));
        string socialBefore=JsonUtility.ToJson(CallStatic("SocialRuntimeBridge","BuildSaveData"));
        string decisionBefore=JsonUtility.ToJson(CallStatic("AutonomousSelectionRuntimeBridge","BuildSaveData"));
        string savePath=(string)CallStatic("SaveSystem","GetSavePath");
        byte[] saveBefore=System.IO.File.ReadAllBytes(savePath);
        Assert.IsFalse((bool)GetBridgeStatic("CanSaveWorld"));
        Assert.IsFalse((bool)CallStatic("VillageRuntimeBridge","GrantPersonRestUntilMonthEnd",r.ExpectedIds[0]));
        Assert.IsNull(CallStatic("SaveSystem","Load"));Assert.IsNull(CallStatic("SaveSystem","NewGame"));
        time.Update(100);time.TickDay();time.JumpToNextMonth();
        time.SetSpeed(VillageTime.VillageTimeSpeed.Fast);time.Resume();
        Assert.IsFalse((bool)CallStatic("SaveSystem","Save",r.PlayerData));
        Assert.IsFalse((bool)CallStatic("SaveSystem","SaveWithVillage",r.PlayerData,null));
        yield return new WaitForSecondsRealtime(10);
        Assert.AreEqual(old,time.CurrentInstant);Assert.AreEqual(food,(int)GetPropertyOrField(resources,"Food"));
        Assert.AreEqual(worldBefore,JsonUtility.ToJson(CallStatic("VillageRuntimeBridge","BuildSaveData")));
        Assert.AreEqual(socialBefore,JsonUtility.ToJson(CallStatic("SocialRuntimeBridge","BuildSaveData")));
        Assert.AreEqual(decisionBefore,JsonUtility.ToJson(CallStatic("AutonomousSelectionRuntimeBridge","BuildSaveData")));
        CollectionAssert.AreEqual(saveBefore,System.IO.File.ReadAllBytes(savePath));
        Assert.IsFalse(((GameObject)GetField(r.Town,"canvasRoot")).activeInHierarchy);
        SetSaveFailure(null);Call(r.ExpUI,"OnReturnToTown");Assert.IsTrue(record.Completed);
        Assert.AreEqual(old,time.CurrentInstant);Assert.AreEqual(food,(int)GetPropertyOrField(resources,"Food"));
        Assert.IsTrue(((GameObject)GetField(r.Town,"canvasRoot")).activeInHierarchy);
    }
}
