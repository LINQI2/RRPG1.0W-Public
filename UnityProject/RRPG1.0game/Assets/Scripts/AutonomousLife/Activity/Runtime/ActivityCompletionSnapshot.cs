using System.Collections.Generic;
namespace RRPG.AutonomousActivity
{
    public sealed class ActivityCompletionSnapshot
    {
        public string ActivityInstanceId { get; }
        public string ActivityType { get; }
        public string ActorPersonId { get; }
        public string TargetPersonId { get; }
        public string PlaceId { get; }
        public IReadOnlyList<string> ParticipantPersonIds { get; }
        public ActivityDayStamp DayStamp { get; }
        internal ActivityCompletionSnapshot(ActivityInstance instance, ActivityDayStamp? completionDay = null)
        {
            ActivityInstanceId = instance.ActivityInstanceId; ActivityType = instance.Candidate.ActivityType;
            ActorPersonId = instance.Candidate.ActorPersonId; TargetPersonId = instance.Candidate.TargetPersonId;
            PlaceId = instance.DestinationPlaceId; DayStamp = completionDay ?? instance.DayStamp;
            ParticipantPersonIds = ActivityDailyWindow.Freeze(instance.Candidate.Participants);
        }
    }
}
