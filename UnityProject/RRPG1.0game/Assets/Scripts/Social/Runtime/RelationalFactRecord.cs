using System;
namespace RRPG.Social
{
    /// <summary>Immutable canonical occurrence, keyed by literal ordinal SourceOccurrenceId.</summary>
    public sealed class RelationalFactRecord
    {
        public string SourceOccurrenceId { get; }
        public string FactType { get; }
        public string ActorPersonId { get; }
        public string TargetPersonId { get; }
        public int Year { get; }
        public int Month { get; }
        public int Day { get; }
        public RelationalFactRecord(string sourceOccurrenceId, string factType, string actorPersonId,
            string targetPersonId, int year, int month, int day)
        {
            SourceOccurrenceId = sourceOccurrenceId; FactType = factType;
            ActorPersonId = actorPersonId; TargetPersonId = targetPersonId;
            Year = year; Month = month; Day = day;
        }
        internal bool IsValid => !string.IsNullOrWhiteSpace(SourceOccurrenceId)
            && !string.IsNullOrWhiteSpace(FactType)
            && DirectedRelationshipRegistry.ValidPair(ActorPersonId, TargetPersonId)
            && Year > 0 && Month >= 1 && Month <= 12 && Day > 0;
        internal bool SamePayload(RelationalFactRecord other) => other != null
            && string.Equals(SourceOccurrenceId, other.SourceOccurrenceId, StringComparison.Ordinal)
            && string.Equals(FactType, other.FactType, StringComparison.Ordinal)
            && string.Equals(ActorPersonId, other.ActorPersonId, StringComparison.Ordinal)
            && string.Equals(TargetPersonId, other.TargetPersonId, StringComparison.Ordinal)
            && Year == other.Year && Month == other.Month && Day == other.Day;
    }
}
