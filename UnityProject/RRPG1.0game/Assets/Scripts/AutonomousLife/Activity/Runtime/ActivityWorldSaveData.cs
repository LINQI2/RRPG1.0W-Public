using System;
namespace RRPG.AutonomousActivity
{
    [Serializable] public sealed class ActivityWorldSaveData
    {
        public int IntradaySchemaVersion;
        public PersonActivitySaveData[] PersonActivityStates;
        public ActiveActivitySaveData[] ActiveInstances;
    }
    [Serializable] public sealed class ActiveActivitySaveData
    {
        public string ActivityInstanceId, OpportunityId, CandidateKey, ActivityType, ProviderId;
        public string ActorPersonId, TargetPersonId, PlaceId;
        public string[] ExclusiveParticipantPersonIds;
        public int Year, Month, Day, State;
        public long CommittedMinute, ArrivalMinute, CompletionMinute;
        public int TargetParticipation;
        public string DestinationPlaceId;
    }
    [Serializable]
    public sealed class PersonActivitySaveData
    {
        public string PersonId;
        public int LastParticipationYear, LastParticipationMonth, LastParticipationDay;
        public int LastInitiatedYear, LastInitiatedMonth, LastInitiatedDay;
        public string LastInitiatedActivityType;
        public bool HasLastCompletionMinute;
        public long LastCompletionMinute;
        public bool HasPresence, HasMissedVisit;
        public string PresencePlaceId,LastMissedActivityType,LastMissedTargetPersonId;
        public long LastPresenceMinute,LastMissedMinute;
    }
}
