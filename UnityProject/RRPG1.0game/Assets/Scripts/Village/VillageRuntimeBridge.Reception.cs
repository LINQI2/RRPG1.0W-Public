using System;
using System.Linq;
using RRPG.AutonomousActivity;

public sealed class IntradayPersonPresence
{
    public string PersonId { get; }
    public string PlaceId { get; }
    public string State { get; }
    public string DestinationPlaceId { get; }
    public bool CanReceive { get; }
    public IntradayPersonPresence(string id,string place,string state,string destination=null,bool canReceive=false)
    { PersonId=id;PlaceId=place;State=state;DestinationPlaceId=destination;CanReceive=canReceive; }
}

public static partial class VillageRuntimeBridge
{
    public static IntradayReceptionPolicy ReceptionPolicy { get; private set; }=new IntradayReceptionPolicy();
    public static void ConfigureReceptionPolicy(IntradayReceptionPolicy policy)
    { if(GuardWorldWrite()) ReceptionPolicy=policy ?? new IntradayReceptionPolicy(); }
    // Pure query: known destination never substitutes for physical arrival.
    public static IntradayPersonPresence QueryIntradayPresence(string personId)
    {
        if(SimulationPersonSource==null) return new IntradayPersonPresence(personId,null,"人物来源缺失");
        if(!PresentWorkPersons().Contains(personId)) return new IntradayPersonPresence(personId,null,"离村 / 不可用");
        var claim=ActivityRuntimeBridge.QueryClaim(personId);
        var instance=claim==null ? null : ActivityRuntimeBridge.QueryActivityInstance(claim);
        if(instance!=null)
        {
            if(instance.State==ActivityLifecycleState.Committed)
                return new IntradayPersonPresence(personId,null,"通勤",instance.DestinationPlaceId);
            bool resting=instance.Candidate.ActivityType=="Rest" && instance.DestinationPlaceId==ReceptionPolicy.KnownHome(personId);
            return new IntradayPersonPresence(personId,instance.DestinationPlaceId,resting ? "在家休息" : instance.Candidate.ActivityType,
                null,resting);
        }
        if(IsExecutingWork(personId)) return new IntradayPersonPresence(personId,WorkDestination(personId),GetWorkExecutionLabel(personId));
        if(IntradayWork.Schedule.IsWorking(Time.MinuteOfDay) && !string.IsNullOrEmpty(WorkDestination(personId)))
            return new IntradayPersonPresence(personId,null,"通勤",WorkDestination(personId));
        string place=ActivityRuntimeBridge.QueryPersonActivityState(personId).PresencePlaceId ?? ReceptionPolicy.KnownHome(personId);
        return new IntradayPersonPresence(personId,place,"空闲",null,place==ReceptionPolicy.KnownHome(personId));
    }
    public static ActivityResult TryLeaveIntradayActivity(string personId)
    {
        if(!GuardWorldWrite()) return ActivityResult.IllegalTransition;
        string id=ActivityRuntimeBridge.QueryClaim(personId);
        var instance=id==null ? null : ActivityRuntimeBridge.QueryActivityInstance(id);
        if(instance==null) return ActivityResult.MissingInstance;
        return FinishOrInterruptReception(instance,Time.CurrentInstant);
    }
    private static void ReconcileReceptionPresence()
    {
        if(Time==null || SimulationPersonSource==null) return;
        foreach(var instance in ActivityRuntimeBridge.QueryActiveInstances())
        {
            if(instance.Candidate.TargetParticipation!=ActivityTargetParticipation.ReceptionAtArrival || instance.State!=ActivityLifecycleState.Executing) continue;
            var target=QueryIntradayPresence(instance.Candidate.TargetPersonId);
            if(!target.CanReceive || target.PlaceId!=instance.DestinationPlaceId) FinishOrInterruptReception(instance,Time.CurrentInstant);
        }
    }
    private static ActivityResult FinishOrInterruptReception(ActivityInstance instance,WorldInstant point)
    {
        if(instance.Candidate.TargetParticipation==ActivityTargetParticipation.ReceptionAtArrival
            && instance.State==ActivityLifecycleState.Executing
            && point.Minute-instance.ArrivalMinute>=ReceptionPolicy.MinimumParticipationMinutes)
        {
            point.GetDate(out int y,out int m,out int d);
            var completionResult=AutonomousSocialFeedbackRuntimeBridge.TryCompleteMemberResult(instance.ActivityInstanceId,point.Minute,
                new ActivityDayStamp(y,m,d),ReceptionPolicy.MinimumParticipationMinutes,out _);
            if(completionResult!=ActivityResult.Success && completionResult!=ActivityResult.Duplicate)
            { IntradayActivityDiagnostic="MemberProjectionFailed:"+AutonomousSocialFeedbackRuntimeBridge.LastCompletionDiagnostic;
                ActivityRuntimeBridge.TryFail(instance.ActivityInstanceId); }
            return completionResult;
        }
        var result=ActivityRuntimeBridge.TryInterrupt(instance.ActivityInstanceId);
        if(result==ActivityResult.Success && instance.Candidate.TargetParticipation==ActivityTargetParticipation.ReceptionAtArrival)
            ActivityRuntimeBridge.RecordMissedVisit(instance.Candidate.ActorPersonId,instance.Candidate.ActivityType,
                instance.Candidate.TargetPersonId,point.Minute);
        return result;
    }
}
