using System;
using System.Linq;
namespace RRPG.AutonomousActivity
{
    /// <summary>All business validation precedes mutation; no callbacks, content, social writes or RNG.</summary>
    public sealed class ActivityLifecycleService
    {
        private readonly ActivityWorldState world;
        public ActivityLifecycleService(ActivityWorldState world) { this.world = world ?? throw new ArgumentNullException(nameof(world)); }
        public ActivityResult OpenDailyWindow(ActivityDailyWindow window)
        {
            if (window == null || !window.DayStamp.IsValid) return ActivityResult.InvalidWindow;
            if(window.IsIntraday) return ActivityResult.InvalidWindow;
            if(world.QueryActiveInstances().Any(i=>i.IsScheduled)) return ActivityResult.WindowConflict;
            if (world.Window != null && world.Window.DayStamp == window.DayStamp)
                return world.Window.SameSnapshot(window) ? ActivityResult.Duplicate : ActivityResult.WindowConflict;
            world.ClearTransient(); world.Window = window;
            return ActivityResult.Success;
        }
        public ActivityResult OpenOpportunity(ActivityDailyWindow window)
        {
            if (window == null || !window.IsIntraday || !window.DayStamp.IsValid) return ActivityResult.InvalidWindow;
            if (world.Window != null && world.Window.IsIntraday)
            {
                if (window.OpportunityMinute < world.Window.OpportunityMinute) return ActivityResult.WindowConflict;
                if (window.OpportunityMinute == world.Window.OpportunityMinute)
                    return world.Window.SameSnapshot(window) ? ActivityResult.Duplicate : ActivityResult.WindowConflict;
            }
            // Opportunity refresh never clears active instances or their claims.
            foreach (var id in world.Instances.Where(p => p.Value.State != ActivityLifecycleState.Committed
                && p.Value.State != ActivityLifecycleState.Executing).Select(p => p.Key).ToArray()) world.Instances.Remove(id);
            world.Window = window; return ActivityResult.Success;
        }
        public ActivityResult TryCommitActivity(string id, ActivityCandidate candidate)
            => candidate?.TargetParticipation==ActivityTargetParticipation.ReceptionAtArrival ? ActivityResult.InvalidCandidate : Commit(id,candidate,null,0,0,0);
        public ActivityResult TryCommitScheduled(string id, ActivityCandidate candidate, long now, long arrival, long completion)
            => TryCommitScheduledAtPlace(id,candidate,now,arrival,completion,candidate?.PlaceId);
        public ActivityResult TryCommitScheduledAtPlace(string id,ActivityCandidate candidate,long now,long arrival,long completion,string destination)
        {
            if (world.Window == null || !world.Window.IsIntraday || now != world.Window.OpportunityMinute
                || arrival < now || completion <= arrival) return ActivityResult.InvalidWindow;
            if(string.IsNullOrWhiteSpace(destination)) return ActivityResult.InvalidCandidate;
            return Commit(id,candidate,world.Window.OpportunityId,now,arrival,completion,destination);
        }
        private ActivityResult Commit(string id, ActivityCandidate candidate, string opportunity, long now, long arrival, long completion,string destination=null)
        {
            if (world.Window == null) return ActivityResult.MissingWindow;
            if (string.IsNullOrWhiteSpace(id)) return ActivityResult.InvalidInstanceId;
            if (candidate == null || !candidate.IsValid) return ActivityResult.InvalidCandidate;
            if(candidate.ActorPersonId==candidate.TargetPersonId) return ActivityResult.InvalidCandidate;
            if (world.Instances.ContainsKey(id)) return ActivityResult.InstanceConflict;
            if (!world.Window.ActorEligiblePersonIds.Contains(candidate.ActorPersonId)) return ActivityResult.ActorIneligible;
            var people = candidate.ClaimParticipantPersonIds;
            foreach (var person in people)
            {
                if (person != candidate.ActorPersonId && !world.Window.ParticipantAvailablePersonIds.Contains(person))
                    return ActivityResult.ParticipantUnavailable;
                if (world.QueryClaim(person) != null) return ActivityResult.ClaimConflict;
                if (opportunity == null && world.QueryPersonActivityState(person).LastMajorParticipationDay == world.Window.DayStamp)
                    return ActivityResult.AlreadyParticipated;
            }
            if (opportunity == null && world.Window.IsIntraday) return ActivityResult.InvalidWindow;
            var instance = new ActivityInstance(id, candidate, world.Window.DayStamp, ActivityLifecycleState.Committed,
                opportunity,now,arrival,completion,destination);
            world.Instances.Add(id, instance); world.Claims.Acquire(people, id);
            return ActivityResult.Success;
        }
        public ActivityResult TryBeginExecution(string id)
            => Begin(id,null);
        public ActivityResult TryBeginExecutionAt(string id, long minute) => Begin(id,minute);
        private ActivityResult Begin(string id, long? minute)
        {
            var instance = world.QueryActivityInstance(id);
            if (instance == null) return ActivityResult.MissingInstance;
            if (instance.State != ActivityLifecycleState.Committed) return ActivityResult.IllegalTransition;
            if (instance.IsScheduled && (!minute.HasValue || minute.Value != instance.ArrivalMinute)) return ActivityResult.IllegalTransition;
            world.Instances[id] = instance.WithState(ActivityLifecycleState.Executing);
            return ActivityResult.Success;
        }
        public ActivityResult TryComplete(string id, out ActivityCompletionSnapshot snapshot)
            => Complete(id,null,null,out snapshot);
        public ActivityResult TryCompleteAt(string id, long minute, ActivityDayStamp completionDay, out ActivityCompletionSnapshot snapshot)
            => Complete(id,minute,completionDay,out snapshot);
        public ActivityResult TryCompleteMemberResult(string id,long minute,ActivityDayStamp day,int minimumParticipation,out ActivityCompletionSnapshot snapshot)
        {
            snapshot=null;var instance=world.QueryActivityInstance(id);
            if(instance==null) return ActivityResult.MissingInstance;
            if(!instance.IsScheduled || instance.Candidate.TargetParticipation!=ActivityTargetParticipation.ReceptionAtArrival
                || minimumParticipation<1 || minute<instance.ArrivalMinute || minute-instance.ArrivalMinute<minimumParticipation
                || minute>instance.CompletionMinute) return ActivityResult.IllegalTransition;
            return Complete(id,minute,day,out snapshot,true);
        }
        private ActivityResult Complete(string id, long? minute, ActivityDayStamp? completionDay, out ActivityCompletionSnapshot snapshot,bool early=false)
        {
            snapshot = null;
            var instance = world.QueryActivityInstance(id);
            if (instance == null) return ActivityResult.MissingInstance;
            if (instance.State == ActivityLifecycleState.Completed) return ActivityResult.Duplicate;
            if (instance.State != ActivityLifecycleState.Executing) return ActivityResult.IllegalTransition;
            if (instance.IsScheduled && (!minute.HasValue || !early && minute.Value != instance.CompletionMinute
                || !completionDay.HasValue || !completionDay.Value.IsValid)) return ActivityResult.IllegalTransition;
            snapshot = new ActivityCompletionSnapshot(instance,completionDay);
            foreach (var person in snapshot.ParticipantPersonIds)
            {
                var old = world.QueryPersonActivityState(person);
                bool actor = person == snapshot.ActorPersonId;
                world.Persons[person] = old.WithCompletion(snapshot.DayStamp,snapshot.ActivityType,actor,minute);
            }
            world.Instances[id] = instance.WithState(ActivityLifecycleState.Completed);
            world.Claims.Release(instance.Candidate.ClaimParticipantPersonIds);
            return ActivityResult.Success;
        }
        public ActivityResult TryFail(string id) => EndWithoutSuccess(id, ActivityLifecycleState.Failed);
        public ActivityResult TryInterrupt(string id) => EndWithoutSuccess(id, ActivityLifecycleState.Interrupted);
        private ActivityResult EndWithoutSuccess(string id, ActivityLifecycleState terminal)
        {
            var instance = world.QueryActivityInstance(id);
            if (instance == null) return ActivityResult.MissingInstance;
            if (instance.State != ActivityLifecycleState.Committed && instance.State != ActivityLifecycleState.Executing)
                return ActivityResult.IllegalTransition;
            world.Instances[id] = instance.WithState(terminal); world.Claims.Release(instance.Candidate.ClaimParticipantPersonIds);
            return ActivityResult.Success;
        }
        public bool RecordActualPresence(string personId,string place,long minute)
        {
            if(string.IsNullOrWhiteSpace(personId) || string.IsNullOrWhiteSpace(place) || minute<0) return false;
            var old=world.QueryPersonActivityState(personId);
            if(old.LastPresenceMinute.HasValue && minute<old.LastPresenceMinute.Value) return false;
            world.Persons[personId]=old.WithPresence(place,minute);return true;
        }
        public bool RecordMissedVisit(string personId,string type,string target,long minute)
        {
            if(string.IsNullOrWhiteSpace(personId) || string.IsNullOrWhiteSpace(type) || string.IsNullOrWhiteSpace(target) || minute<0) return false;
            var old=world.QueryPersonActivityState(personId);
            if(old.LastMissedMinute.HasValue && minute<old.LastMissedMinute.Value) return false;
            world.Persons[personId]=old.WithMissed(type,target,minute);return true;
        }
    }
}
