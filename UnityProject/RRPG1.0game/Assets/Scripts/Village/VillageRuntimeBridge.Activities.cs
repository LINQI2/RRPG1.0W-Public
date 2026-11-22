using System;
using System.Collections.Generic;
using RRPG.AutonomousActivity;

public static partial class VillageRuntimeBridge
{
    public static IntradayActivitySchedule ActivitySchedule { get; private set; } = new IntradayActivitySchedule();
    public static string IntradayActivityDiagnostic { get; private set; }
    public static void ConfigureActivitySchedule(IntradayActivitySchedule schedule)
    {
        if(!GuardWorldWrite()) return;
        ActivitySchedule=schedule ?? new IntradayActivitySchedule();
    }
    private static WorldInstant? NextActivityBoundary(WorldInstant from,WorldInstant target)
    {
        long next=long.MaxValue;
        long day=from.Minute-from.MinuteOfDay;
        foreach(int offset in ActivitySchedule.OpportunityMinutes)
        {
            long point=day+offset;
            if(point<=from.Minute) point+=1440;
            next=Math.Min(next,point);
        }
        foreach(var instance in ActivityRuntimeBridge.QueryActiveInstances())
        {
            if(!instance.IsScheduled) continue;
            long point=instance.State==ActivityLifecycleState.Committed ? instance.ArrivalMinute : instance.CompletionMinute;
            if(point>from.Minute) next=Math.Min(next,point);
        }
        foreach(var state in ActivityRuntimeBridge.QueryPersonStates())
            if(state.LastCompletionMinute.HasValue)
            {
                long point=checked(state.LastCompletionMinute.Value+ActivitySchedule.RepeatOpportunityDelay);
                if(point>from.Minute) next=Math.Min(next,point);
            }
        return next<=target.Minute ? new WorldInstant(next) : (WorldInstant?)null;
    }
    private static bool IsActivityOpportunity(WorldInstant point)
    {
        foreach(int offset in ActivitySchedule.OpportunityMinutes) if(offset==point.MinuteOfDay) return true;
        foreach(var state in ActivityRuntimeBridge.QueryPersonStates())
            if(state.LastCompletionMinute.HasValue && state.LastCompletionMinute.Value+ActivitySchedule.RepeatOpportunityDelay==point.Minute)
                return true;
        return false;
    }
    private static void CompleteActivityBoundary(WorldInstant point)
    {
        try
        {
            var present=new HashSet<string>(PresentWorkPersons(),StringComparer.Ordinal);
            bool workPriority=point.MinuteOfDay==IntradayWork.Schedule.MorningStart-IntradayWork.Schedule.CommuteMinutes
                || point.MinuteOfDay==IntradayWork.Schedule.AfternoonStart;
            // Begin solo arrivals first; receiver presence must not depend on PersonId iteration.
            foreach(var solo in ActivityRuntimeBridge.QueryActiveInstances())
                if(solo.IsScheduled && solo.State==ActivityLifecycleState.Committed && solo.ArrivalMinute==point.Minute
                    && solo.Candidate.TargetParticipation==ActivityTargetParticipation.Exclusive && present.Contains(solo.Candidate.ActorPersonId)
                    && !(workPriority && !string.IsNullOrEmpty(WorkDestination(solo.Candidate.ActorPersonId))))
                { ActivityRuntimeBridge.RecordActualPresence(solo.Candidate.ActorPersonId,solo.DestinationPlaceId,point.Minute);
                    ActivityRuntimeBridge.TryBeginExecutionAt(solo.ActivityInstanceId,point.Minute); }
            foreach(var instance in ActivityRuntimeBridge.QueryActiveInstances())
            {
                bool interrupted=false;
                foreach(string id in instance.Candidate.ClaimParticipantPersonIds)
                    if(!present.Contains(id) || workPriority && !string.IsNullOrEmpty(WorkDestination(id))) interrupted=true;
                if(interrupted) { FinishOrInterruptReception(instance,point); continue; }
                bool reception=instance.Candidate.TargetParticipation==ActivityTargetParticipation.ReceptionAtArrival;
                if(reception && instance.State==ActivityLifecycleState.Executing)
                { var target=QueryIntradayPresence(instance.Candidate.TargetPersonId);
                    if(!target.CanReceive || target.PlaceId!=instance.DestinationPlaceId
                        || workPriority && !string.IsNullOrEmpty(WorkDestination(instance.Candidate.TargetPersonId))) { FinishOrInterruptReception(instance,point);continue; } }
                if(!instance.IsScheduled) continue;
                if(instance.State==ActivityLifecycleState.Committed && instance.ArrivalMinute==point.Minute)
                    {
                    ActivityRuntimeBridge.RecordActualPresence(instance.Candidate.ActorPersonId,instance.DestinationPlaceId,point.Minute);
                    var target=reception ? QueryIntradayPresence(instance.Candidate.TargetPersonId) : null;
                    if(reception && (!target.CanReceive || target.PlaceId!=instance.DestinationPlaceId
                        || workPriority && !string.IsNullOrEmpty(WorkDestination(instance.Candidate.TargetPersonId))))
                    { FinishOrInterruptReception(instance,point);IntradayActivityDiagnostic="MissedReception:"+instance.Candidate.ActorPersonId; }
                    else ActivityRuntimeBridge.TryBeginExecutionAt(instance.ActivityInstanceId,point.Minute);
                }
                else if(instance.State==ActivityLifecycleState.Executing && instance.CompletionMinute==point.Minute)
                {
                    point.GetDate(out int year,out int month,out int day);
                    var result=reception ? FinishOrInterruptReception(instance,point)
                        : AutonomousSocialFeedbackRuntimeBridge.TryCompleteAt(instance.ActivityInstanceId,point.Minute,
                        new ActivityDayStamp(year,month,day),out _);
                    if(result!=ActivityResult.Success && result!=ActivityResult.Duplicate)
                    {
                        IntradayActivityDiagnostic="CompletionFailed:"+result+":"+AutonomousSocialFeedbackRuntimeBridge.LastCompletionDiagnostic;
                        ActivityRuntimeBridge.TryFail(instance.ActivityInstanceId);
                    }
                }
            }
        }
        catch(Exception ex) { IntradayActivityDiagnostic="ActivityBoundaryFailure:"+ex.GetType().Name+":"+ex.Message; }
    }
    private static IReadOnlyList<string> Participants(ActivityCandidate candidate)
    {
        var ids=new SortedSet<string>(candidate.ExclusiveParticipantPersonIds,StringComparer.Ordinal);
        ids.Add(candidate.ActorPersonId);
        if(!string.IsNullOrWhiteSpace(candidate.TargetPersonId)) ids.Add(candidate.TargetPersonId);
        return new List<string>(ids).AsReadOnly();
    }
    private static void OpenIntradayOpportunity(WorldInstant point)
    {
        if(!IsActivityOpportunity(point)) return;
        if(SimulationPersonSource==null)
        { LastActivityDailyPhaseResult=ActivityDailyPhaseResult.MissingPersonSource; IntradayActivityDiagnostic="MissingPersonSource";
            AutonomousSelectionRuntimeBridge.ReportOpportunityFailure(RRPG.AutonomousSelection.DecisionDailyResult.MissingPersonSource,IntradayActivityDiagnostic);return; }
        if(!AutonomousSocialFeedbackRuntimeBridge.DrainBarrier().Settled)
        { LastActivityDailyPhaseResult=ActivityDailyPhaseResult.SocialFeedbackUnsettled; IntradayActivityDiagnostic="SocialFeedbackUnsettled";
            AutonomousSelectionRuntimeBridge.ReportOpportunityFailure(RRPG.AutonomousSelection.DecisionDailyResult.InvalidPhase,IntradayActivityDiagnostic);return; }
        try
        {
            var eligible=new List<string>();
            foreach(string id in PresentWorkPersons())
                if(ActivityRuntimeBridge.QueryClaim(id)==null && !IsExecutingWork(id)
                    && !(IntradayWork.Schedule.IsWorking(point.MinuteOfDay) && !string.IsNullOrEmpty(WorkDestination(id)))) eligible.Add(id);
            point.GetDate(out int year,out int month,out int day);
            string identity="village.intraday|"+point.Minute.ToString(System.Globalization.CultureInfo.InvariantCulture)+"|free";
            var stamp=new ActivityDayStamp(year,month,day);
            var window=new ActivityDailyWindow(stamp,eligible,eligible,identity,point.Minute);
            var result=ActivityRuntimeBridge.OpenOpportunity(window);
            LastActivityDailyPhaseResult=result==ActivityResult.Success ? ActivityDailyPhaseResult.Opened
                : result==ActivityResult.Duplicate ? ActivityDailyPhaseResult.Duplicate : ActivityDailyPhaseResult.WindowConflict;
            if(result!=ActivityResult.Success && result!=ActivityResult.Duplicate) { IntradayActivityDiagnostic="WindowConflict"; return; }
            var decision=AutonomousSelectionRuntimeBridge.RunOpportunity(stamp,SimulationPersonSource,point.Minute,ActivitySchedule);
            IntradayActivityDiagnostic=decision.Result+":"+decision.Diagnostic;
            // New personal departure intents end reception at this exact minute, not the next arrival.
            ReconcileReceptionPresence();
        }
        catch(Exception ex) { IntradayActivityDiagnostic="OpportunityFailure:"+ex.GetType().Name+":"+ex.Message; }
    }
    internal static bool ValidateActivityRestoreAt(ActivityWorldSaveData data,WorldInstant instant)
    {
        if(!ActivityWorldPersistence.IsValid(data)) return false;
        foreach(var row in data?.ActiveInstances ?? new ActiveActivitySaveData[0])
        {
            try
            {
                var commit=new WorldInstant(row.CommittedMinute);new WorldInstant(row.ArrivalMinute);new WorldInstant(row.CompletionMinute);
                commit.GetDate(out int y,out int m,out int d);
                if(y!=row.Year || m!=row.Month || d!=row.Day || row.CommittedMinute>instant.Minute
                    || row.State==(int)ActivityLifecycleState.Committed && row.ArrivalMinute<=instant.Minute
                    || row.State==(int)ActivityLifecycleState.Executing && (row.ArrivalMinute>instant.Minute || row.CompletionMinute<=instant.Minute)) return false;
            }
            catch(ArgumentOutOfRangeException) { return false; }
        }
        foreach(var row in data?.PersonActivityStates ?? new PersonActivitySaveData[0])
            if(row!=null && (row.HasLastCompletionMinute && row.LastCompletionMinute>instant.Minute
                || row.HasPresence && row.LastPresenceMinute>instant.Minute || row.HasMissedVisit && row.LastMissedMinute>instant.Minute)) return false;
        return true;
    }
}
