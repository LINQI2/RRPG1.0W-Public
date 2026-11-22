using System;
using System.Collections.Generic;
using System.Linq;
namespace RRPG.AutonomousActivity
{
    public static class ActivityWorldPersistence
    {
        public static ActivityWorldSaveData Build(ActivityWorldState world)
        {
            return new ActivityWorldSaveData { IntradaySchemaVersion = 1,
                ActiveInstances = world == null ? new ActiveActivitySaveData[0] : world.QueryActiveInstances()
                    .Where(i => i.IsScheduled).Select(i => new ActiveActivitySaveData {
                        ActivityInstanceId=i.ActivityInstanceId,OpportunityId=i.OpportunityId,
                        CandidateKey=i.Candidate.CandidateKey,ActivityType=i.Candidate.ActivityType,ProviderId=i.Candidate.ProviderId,
                        ActorPersonId=i.Candidate.ActorPersonId,TargetPersonId=i.Candidate.TargetPersonId,PlaceId=i.Candidate.PlaceId,
                        ExclusiveParticipantPersonIds=i.Candidate.ExclusiveParticipantPersonIds.ToArray(),
                        Year=i.DayStamp.Year,Month=i.DayStamp.Month,Day=i.DayStamp.Day,State=(int)i.State,
                        CommittedMinute=i.CommittedMinute,ArrivalMinute=i.ArrivalMinute,CompletionMinute=i.CompletionMinute,
                        TargetParticipation=(int)i.Candidate.TargetParticipation,DestinationPlaceId=i.DestinationPlaceId }).ToArray(),
                PersonActivityStates = world == null ? new PersonActivitySaveData[0]
                : world.Persons.Values.OrderBy(p => p.PersonId, StringComparer.Ordinal).Select(p => new PersonActivitySaveData {
                    PersonId = p.PersonId,
                    LastParticipationYear = p.LastMajorParticipationDay?.Year ?? 0,
                    LastParticipationMonth = p.LastMajorParticipationDay?.Month ?? 0,
                    LastParticipationDay = p.LastMajorParticipationDay?.Day ?? 0,
                    LastInitiatedYear = p.LastInitiatedActivityDay?.Year ?? 0,
                    LastInitiatedMonth = p.LastInitiatedActivityDay?.Month ?? 0,
                    LastInitiatedDay = p.LastInitiatedActivityDay?.Day ?? 0,
                    LastInitiatedActivityType = p.LastInitiatedActivityType,
                    HasLastCompletionMinute=p.LastCompletionMinute.HasValue,
                    LastCompletionMinute=p.LastCompletionMinute ?? 0,
                    HasPresence=p.LastPresenceMinute.HasValue,PresencePlaceId=p.PresencePlaceId,LastPresenceMinute=p.LastPresenceMinute ?? 0,
                    HasMissedVisit=p.LastMissedMinute.HasValue,LastMissedActivityType=p.LastMissedActivityType,
                    LastMissedTargetPersonId=p.LastMissedTargetPersonId,LastMissedMinute=p.LastMissedMinute ?? 0 }).ToArray() };
        }
        public static bool IsValid(ActivityWorldSaveData data) => PrepareActive(data,out _);
        private static bool PrepareActive(ActivityWorldSaveData data, out List<ActivityInstance> active)
        {
            active = new List<ActivityInstance>();
            if (data == null) return true;
            if (data.IntradaySchemaVersion != 0 && data.IntradaySchemaVersion != 1) return false;
            if (data.IntradaySchemaVersion == 0 && (data.ActiveInstances?.Length ?? 0) != 0) return false;
            var ids=new HashSet<string>(StringComparer.Ordinal);
            var claimed=new HashSet<string>(StringComparer.Ordinal);
            foreach(var row in data.ActiveInstances ?? new ActiveActivitySaveData[0])
            {
                if(row==null || string.IsNullOrWhiteSpace(row.ActivityInstanceId) || !ids.Add(row.ActivityInstanceId)
                    || string.IsNullOrWhiteSpace(row.OpportunityId) || row.CommittedMinute<0
                    || row.ArrivalMinute<row.CommittedMinute || row.CompletionMinute<=row.ArrivalMinute
                    || (row.State!=(int)ActivityLifecycleState.Committed && row.State!=(int)ActivityLifecycleState.Executing)) return false;
                var day=new ActivityDayStamp(row.Year,row.Month,row.Day);
                var candidate=new ActivityCandidate(row.CandidateKey,row.ActivityType,row.ProviderId,row.ActorPersonId,
                    row.TargetPersonId,row.PlaceId,row.ExclusiveParticipantPersonIds,(ActivityTargetParticipation)row.TargetParticipation);
                if(row.DestinationPlaceId!=null && string.IsNullOrWhiteSpace(row.DestinationPlaceId)) return false;
                var exclusive=row.ExclusiveParticipantPersonIds ?? new string[0];
                if(exclusive.Any(string.IsNullOrWhiteSpace) || exclusive.Distinct(StringComparer.Ordinal).Count()!=exclusive.Length
                    || exclusive.Contains(row.ActorPersonId) || row.TargetParticipation==1 && exclusive.Contains(row.TargetPersonId)) return false;
                if(!day.IsValid || !candidate.IsValid || candidate.TargetPersonId==candidate.ActorPersonId) return false;
                foreach(string person in candidate.ClaimParticipantPersonIds) if(!claimed.Add(person)) return false;
                active.Add(new ActivityInstance(row.ActivityInstanceId,candidate,day,(ActivityLifecycleState)row.State,
                    row.OpportunityId,row.CommittedMinute,row.ArrivalMinute,row.CompletionMinute,row.DestinationPlaceId));
            }
            if (data.IntradaySchemaVersion == 1)
            {
                ids.Clear();
                foreach(var row in data.PersonActivityStates ?? new PersonActivitySaveData[0])
                {
                    if(row==null || string.IsNullOrWhiteSpace(row.PersonId) || !ids.Add(row.PersonId)
                        || row.HasLastCompletionMinute && row.LastCompletionMinute<0) return false;
                    bool emptyParticipation=row.LastParticipationYear==0 && row.LastParticipationMonth==0 && row.LastParticipationDay==0;
                    if(!new ActivityDayStamp(row.LastParticipationYear,row.LastParticipationMonth,row.LastParticipationDay).IsValid
                        && !(emptyParticipation && (row.HasPresence || row.HasMissedVisit))) return false;
                    if(emptyParticipation && row.HasLastCompletionMinute) return false;
                    if(row.HasPresence && (string.IsNullOrWhiteSpace(row.PresencePlaceId) || row.LastPresenceMinute<0)
                        || row.HasMissedVisit && (string.IsNullOrWhiteSpace(row.LastMissedActivityType)
                            || string.IsNullOrWhiteSpace(row.LastMissedTargetPersonId) || row.LastMissedMinute<0
                            || row.LastMissedTargetPersonId==row.PersonId)) return false;
                    bool empty=row.LastInitiatedYear==0 && row.LastInitiatedMonth==0 && row.LastInitiatedDay==0
                        && string.IsNullOrEmpty(row.LastInitiatedActivityType);
                    if(!empty && (!new ActivityDayStamp(row.LastInitiatedYear,row.LastInitiatedMonth,row.LastInitiatedDay).IsValid
                        || string.IsNullOrWhiteSpace(row.LastInitiatedActivityType))) return false;
                }
            }
            return true;
        }
        /// <summary>Replacement; first valid duplicate wins, inactive identities preserved. No replay.</summary>
        public static int Restore(ActivityWorldState world, ActivityWorldSaveData data)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            if (!PrepareActive(data,out var active)) return -1;
            var restored = new Dictionary<string, PersonActivityState>(StringComparer.Ordinal);
            int filtered = 0;
            foreach (var row in data?.PersonActivityStates ?? new PersonActivitySaveData[0])
            {
                if (row == null || string.IsNullOrWhiteSpace(row.PersonId) || restored.ContainsKey(row.PersonId))
                { filtered++; continue; }
                var participation = new ActivityDayStamp(row.LastParticipationYear, row.LastParticipationMonth, row.LastParticipationDay);
                var initiated = new ActivityDayStamp(row.LastInitiatedYear, row.LastInitiatedMonth, row.LastInitiatedDay);
                bool empty = row.LastInitiatedYear == 0 && row.LastInitiatedMonth == 0 && row.LastInitiatedDay == 0
                    && string.IsNullOrEmpty(row.LastInitiatedActivityType);
                bool presenceOnly=data.IntradaySchemaVersion==1 && (row.HasPresence || row.HasMissedVisit)
                    && row.LastParticipationYear==0 && row.LastParticipationMonth==0 && row.LastParticipationDay==0;
                if ((!participation.IsValid && !presenceOnly) || (!empty && (!initiated.IsValid || string.IsNullOrWhiteSpace(row.LastInitiatedActivityType))))
                { filtered++; continue; }
                restored.Add(row.PersonId, new PersonActivityState(row.PersonId, presenceOnly ? (ActivityDayStamp?)null : participation,
                    empty ? (ActivityDayStamp?)null : initiated, empty ? null : row.LastInitiatedActivityType,
                    row.HasLastCompletionMinute ? (long?)row.LastCompletionMinute : null,
                    row.HasPresence ? row.PresencePlaceId : null,row.HasPresence ? (long?)row.LastPresenceMinute : null,
                    row.HasMissedVisit ? row.LastMissedActivityType : null,row.HasMissedVisit ? row.LastMissedTargetPersonId : null,
                    row.HasMissedVisit ? (long?)row.LastMissedMinute : null));
            }
            world.ClearTransient(); world.Persons.Clear();
            foreach (var pair in restored) world.Persons.Add(pair.Key, pair.Value);
            foreach(var instance in active)
            { world.Instances.Add(instance.ActivityInstanceId,instance); world.Claims.Acquire(instance.Candidate.ClaimParticipantPersonIds,instance.ActivityInstanceId); }
            return filtered;
        }
    }
}
