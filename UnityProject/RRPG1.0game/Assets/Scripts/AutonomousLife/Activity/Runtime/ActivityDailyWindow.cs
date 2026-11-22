using System;
using System.Collections.Generic;
using System.Linq;
namespace RRPG.AutonomousActivity
{
    public sealed class ActivityDailyWindow
    {
        public ActivityDayStamp DayStamp { get; }
        public string OpportunityId { get; }
        public long OpportunityMinute { get; }
        public bool IsIntraday => OpportunityId != null;
        public IReadOnlyList<string> ActorEligiblePersonIds { get; }
        public IReadOnlyList<string> ParticipantAvailablePersonIds { get; }
        internal static IReadOnlyList<string> Freeze(IEnumerable<string> ids)
            => Array.AsReadOnly((ids ?? Enumerable.Empty<string>()).Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.Ordinal).OrderBy(id => id, StringComparer.Ordinal).ToArray());
        public ActivityDailyWindow(ActivityDayStamp day, IEnumerable<string> actors, IEnumerable<string> participants)
        { DayStamp = day; ActorEligiblePersonIds = Freeze(actors); ParticipantAvailablePersonIds = Freeze(participants); }
        public ActivityDailyWindow(ActivityDayStamp day, IEnumerable<string> actors, IEnumerable<string> participants,
            string opportunityId, long opportunityMinute) : this(day, actors, participants)
        {
            if (string.IsNullOrWhiteSpace(opportunityId) || opportunityMinute < 0)
                throw new ArgumentException("Invalid opportunity identity");
            OpportunityId = opportunityId; OpportunityMinute = opportunityMinute;
        }
        internal bool SameSnapshot(ActivityDailyWindow other) => DayStamp == other.DayStamp
            && OpportunityId == other.OpportunityId && OpportunityMinute == other.OpportunityMinute
            && ActorEligiblePersonIds.SequenceEqual(other.ActorEligiblePersonIds)
            && ParticipantAvailablePersonIds.SequenceEqual(other.ParticipantAvailablePersonIds);
    }
}
