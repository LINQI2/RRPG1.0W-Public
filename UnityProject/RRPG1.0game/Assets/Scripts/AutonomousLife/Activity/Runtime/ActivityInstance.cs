namespace RRPG.AutonomousActivity
{
    /// <summary>Immutable snapshot. Lifecycle replaces instances instead of exposing mutable references.</summary>
    public sealed class ActivityInstance
    {
        public string ActivityInstanceId { get; }
        public ActivityCandidate Candidate { get; }
        public ActivityDayStamp DayStamp { get; }
        public ActivityLifecycleState State { get; }
        public string OpportunityId { get; }
        public long CommittedMinute { get; }
        public long ArrivalMinute { get; }
        public long CompletionMinute { get; }
        public bool IsScheduled => OpportunityId != null;
        public string DestinationPlaceId { get; }
        public string MemberResultId => "activity:"+ActivityInstanceId;
        internal ActivityInstance(string id, ActivityCandidate candidate, ActivityDayStamp day, ActivityLifecycleState state,
            string opportunityId = null, long committedMinute = 0, long arrivalMinute = 0, long completionMinute = 0,
            string destinationPlaceId = null)
        {
            ActivityInstanceId = id; Candidate = candidate; DayStamp = day; State = state;
            OpportunityId = opportunityId; CommittedMinute = committedMinute;
            ArrivalMinute = arrivalMinute; CompletionMinute = completionMinute;
            DestinationPlaceId=destinationPlaceId ?? candidate.PlaceId;
        }
        internal ActivityInstance WithState(ActivityLifecycleState state)
            => new ActivityInstance(ActivityInstanceId, Candidate, DayStamp, state, OpportunityId,
                CommittedMinute, ArrivalMinute, CompletionMinute,DestinationPlaceId);
    }
}
