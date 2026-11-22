using System;
using System.Linq;
using System.Collections.Generic;
using RRPG.AutonomousActivity;
using RRPG.AutonomousSocialFeedback;
using RRPG.StaticConfig;

/// <summary>Product integration adapter; owns delivery state, not Activity or Social authority.</summary>
public static class AutonomousSocialFeedbackRuntimeBridge
{
    private static readonly SocialFeedbackWorldState world = new SocialFeedbackWorldState();
    public static FeedbackDrainResult LastDrain { get; private set; }
    public static string LastCompletionDiagnostic { get; private set; } = "";
    public static IReadOnlyList<SocialFeedbackEntry> QueryOutbox() => world.Entries;
    public static FeedbackDrainResult DrainBarrier()
    {
        // Drain catches delivery exceptions. No callbacks escape into Village DayTick.
        return LastDrain = world.Drain(SocialRuntimeBridge.CommitRelationalFact);
    }
    public static ActivityResult TryComplete(string instanceId, out ActivityCompletionSnapshot completion)
            => Complete(instanceId,null,null,out completion);
        public static ActivityResult TryCompleteAt(string instanceId,long minute,ActivityDayStamp day,out ActivityCompletionSnapshot completion)
        {
            completion=null;
            var clock=VillageRuntimeBridge.Time;
            if(clock==null || clock.CurrentInstant.Minute!=minute || day!=new ActivityDayStamp(clock.Year,clock.Month,clock.Day))
                return ActivityResult.IllegalTransition;
            return Complete(instanceId,minute,day,out completion);
        }
        public static ActivityResult TryCompleteMemberResult(string id,long minute,ActivityDayStamp day,int minimum,out ActivityCompletionSnapshot completion)
        {
            completion=null;var clock=VillageRuntimeBridge.Time;
            if(clock==null || clock.CurrentInstant.Minute!=minute || day!=new ActivityDayStamp(clock.Year,clock.Month,clock.Day)) return ActivityResult.IllegalTransition;
            return Complete(id,minute,day,out completion,minimum);
        }
        private static ActivityResult Complete(string instanceId,long? minute,ActivityDayStamp? day,out ActivityCompletionSnapshot completion,int? minimum=null)
    {
        completion = null;
        LastCompletionDiagnostic = "";
        var instance = ActivityRuntimeBridge.QueryActivityInstance(instanceId);
        if (instance == null) return ActivityResult.MissingInstance;
            if(instance.IsScheduled && (!minute.HasValue || (minimum.HasValue ? minimum.Value<=0 || instance.Candidate.TargetParticipation!=ActivityTargetParticipation.ReceptionAtArrival || minute.Value-instance.ArrivalMinute<minimum.Value || minute.Value>instance.CompletionMinute : minute.Value!=instance.CompletionMinute) || !day.HasValue || !day.Value.IsValid))
                return ActivityResult.IllegalTransition;
        if (instance.State != ActivityLifecycleState.Executing)
                return CompleteLifecycle(instanceId,minute,day,out completion,minimum);
        PreparedSocialProjection prepared;
        try
        {
            if (!StaticConfigRegistry.TryGetAutonomousLifeConfig(out var db, out var diagnostic))
                throw new InvalidOperationException(diagnostic);
            var mappings = db.ActivitySocialProjection;
            // NoFeedback is a legitimate policy, and requires no Social mutation or interpreter call.
            if (!mappings.Any(p => p.ActivityType == instance.Candidate.ActivityType))
                return CompleteLifecycle(instanceId,minute,day,out completion,minimum);
            prepared = new ActivitySocialFactProjector(mappings.Select(p => new KeyValuePair<string, string>(p.ActivityType, p.PersistentRelationalFactType)),
                SocialRuntimeBridge.BuildProductionRules()).Prepare(instance,day);
            if (prepared.Result == ProjectionResult.InvalidProjection)
                throw new InvalidOperationException(prepared.Diagnostic);
            var enqueue = world.CanEnqueue(prepared.Fact);
            if (enqueue != FeedbackEnqueueResult.Success && enqueue != FeedbackEnqueueResult.Duplicate)
                throw new InvalidOperationException("Outbox" + enqueue);
        }
        catch (Exception ex)
        {
            LastCompletionDiagnostic = "ProjectionFailure: " + ex.GetType().Name + ": " + ex.Message;
            return ActivityResult.InvalidCandidate;
        }
        // No further authoring queries after this boundary. Activity completion has no external callbacks.
        var result = CompleteLifecycle(instanceId,minute,day,out completion,minimum);
        if (result == ActivityResult.Success)
        {
            world.Enqueue(prepared.Fact);
            DrainBarrier(); // Delivery failure leaves Activity Completed, and durable Pending/Faulted entries.
        }
        return result;
    }
    private static ActivityResult CompleteLifecycle(string id,long? minute,ActivityDayStamp? day,out ActivityCompletionSnapshot completion,int? minimum=null)
        => minimum.HasValue && minute.HasValue && day.HasValue ? ActivityRuntimeBridge.TryCompleteMemberResult(id,minute.Value,day.Value,minimum.Value,out completion) : minute.HasValue && day.HasValue ? ActivityRuntimeBridge.TryCompleteAt(id,minute.Value,day.Value,out completion)
            : ActivityRuntimeBridge.TryComplete(id,out completion);
    public static SocialFeedbackWorldSaveData BuildSaveData() => SocialFeedbackWorldPersistence.Build(world);
    public static bool Restore(SocialFeedbackWorldSaveData data)
    {
        if (!SocialFeedbackWorldPersistence.Restore(world, data)) return false;
        LastDrain = null; LastCompletionDiagnostic = "";
        return true;
    }
    public static void ResetForNewGame() => Restore(null);
    public static void ResetForTests() => Restore(null);
}
