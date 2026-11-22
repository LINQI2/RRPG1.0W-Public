using System.Collections.Generic;
using System.Linq;
namespace RRPG.AutonomousActivity
{
    public enum ActivityTargetParticipation { Exclusive = 0, ReceptionAtArrival = 1 }
    /// <summary>Immutable offering; no registry writes, selection or persistence.</summary>
    public sealed class ActivityCandidate
    {
        public string CandidateKey { get; }
        public string ActivityType { get; }
        public string ProviderId { get; }
        public string ActorPersonId { get; }
        public string TargetPersonId { get; }
        public string PlaceId { get; }
        public IReadOnlyList<string> ExclusiveParticipantPersonIds { get; }
        public ActivityTargetParticipation TargetParticipation { get; }
        public ActivityCandidate(string key, string type, string provider, string actor,
            string target = null, string place = null, IEnumerable<string> exclusiveParticipants = null,
            ActivityTargetParticipation targetParticipation = ActivityTargetParticipation.Exclusive)
        {
            CandidateKey = key; ActivityType = type; ProviderId = provider; ActorPersonId = actor;
            TargetPersonId = target; PlaceId = place;
            TargetParticipation = targetParticipation;
            ExclusiveParticipantPersonIds = ActivityDailyWindow.Freeze(
                (exclusiveParticipants ?? Enumerable.Empty<string>()).Where(id => id != actor && (targetParticipation!=ActivityTargetParticipation.ReceptionAtArrival || id!=target)));
        }
        internal bool IsValid => !string.IsNullOrWhiteSpace(CandidateKey)
            && !string.IsNullOrWhiteSpace(ActivityType) && !string.IsNullOrWhiteSpace(ActorPersonId)
            && (TargetParticipation==ActivityTargetParticipation.Exclusive
                || TargetParticipation==ActivityTargetParticipation.ReceptionAtArrival && !string.IsNullOrWhiteSpace(TargetPersonId));
        // Target is a participant in V0.1 Major Activity, even if the offering omitted its claim requirement.
        internal IReadOnlyList<string> Participants => ActivityDailyWindow.Freeze(
            ExclusiveParticipantPersonIds.Concat(new[] { ActorPersonId, TargetPersonId }));
        public IReadOnlyList<string> ClaimParticipantPersonIds => ActivityDailyWindow.Freeze(
            ExclusiveParticipantPersonIds.Concat(TargetParticipation==ActivityTargetParticipation.ReceptionAtArrival
                ? new[]{ActorPersonId} : new[] {ActorPersonId,TargetPersonId}));
    }
}
