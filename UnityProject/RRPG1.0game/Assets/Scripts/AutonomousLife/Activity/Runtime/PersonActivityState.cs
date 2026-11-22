namespace RRPG.AutonomousActivity
{
    /// <summary>Derived participation and initiated recency, not a history ledger.</summary>
    public sealed class PersonActivityState
    {
        public string PersonId { get; }
        public ActivityDayStamp? LastMajorParticipationDay { get; }
        public ActivityDayStamp? LastInitiatedActivityDay { get; }
        public string LastInitiatedActivityType { get; }
        public long? LastCompletionMinute { get; }
        public string PresencePlaceId { get; }
        public long? LastPresenceMinute { get; }
        public string LastMissedActivityType { get; }
        public string LastMissedTargetPersonId { get; }
        public long? LastMissedMinute { get; }
        internal PersonActivityState(string id, ActivityDayStamp? participation = null,
            ActivityDayStamp? initiated = null, string type = null, long? completionMinute = null,
            string place=null,long? presenceMinute=null,string missedType=null,string missedTarget=null,long? missedMinute=null)
        { PersonId = id; LastMajorParticipationDay = participation; LastInitiatedActivityDay = initiated;
            LastInitiatedActivityType = type; LastCompletionMinute = completionMinute;
            PresencePlaceId=place;LastPresenceMinute=presenceMinute;LastMissedActivityType=missedType;
            LastMissedTargetPersonId=missedTarget;LastMissedMinute=missedMinute; }
        internal PersonActivityState WithPresence(string place,long minute) => new PersonActivityState(PersonId,
            LastMajorParticipationDay,LastInitiatedActivityDay,LastInitiatedActivityType,LastCompletionMinute,
            place,minute,LastMissedActivityType,LastMissedTargetPersonId,LastMissedMinute);
        internal PersonActivityState WithMissed(string type,string target,long minute) => new PersonActivityState(PersonId,
            LastMajorParticipationDay,LastInitiatedActivityDay,LastInitiatedActivityType,LastCompletionMinute,
            PresencePlaceId,LastPresenceMinute,type,target,minute);
        internal PersonActivityState WithCompletion(ActivityDayStamp day,string type,bool actor,long? minute)
            => new PersonActivityState(PersonId,day,actor?day:LastInitiatedActivityDay,actor?type:LastInitiatedActivityType,
                minute ?? LastCompletionMinute,PresencePlaceId,LastPresenceMinute,LastMissedActivityType,LastMissedTargetPersonId,LastMissedMinute);
    }
}
