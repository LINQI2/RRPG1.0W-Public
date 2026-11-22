using System;
using System.Collections.Generic;
using System.Linq;
using RRPG.Social;

namespace RRPG.AutonomousSocialFeedback
{
    public enum FeedbackStatus { Pending, Faulted }
    public enum FeedbackEnqueueResult { Success, Duplicate, Conflict, InvalidPayload }
    public sealed class SocialFeedbackEntry
    {
        public RelationalFactRecord Fact { get; }
        public FeedbackStatus Status { get; }
        public string FaultCode { get; }
        internal SocialFeedbackEntry(RelationalFactRecord fact, FeedbackStatus status, string faultCode)
        { Fact = fact; Status = status; FaultCode = faultCode; }
    }
    public sealed class FeedbackDeliveryTrace
    {
        public string SourceOccurrenceId { get; }
        public SocialCommitResult Result { get; }
        public FeedbackDeliveryTrace(string id, SocialCommitResult result) { SourceOccurrenceId = id; Result = result; }
    }
    public sealed class FeedbackDrainResult
    {
        public bool Settled { get; }
        public IReadOnlyList<FeedbackDeliveryTrace> Deliveries { get; }
        public IReadOnlyList<SocialFeedbackEntry> Remaining { get; }
        public string Diagnostic { get; }
        internal FeedbackDrainResult(IEnumerable<FeedbackDeliveryTrace> trace, IEnumerable<SocialFeedbackEntry> remaining, string diagnostic = "")
        {
            Deliveries = trace.ToList().AsReadOnly(); Remaining = remaining.ToList().AsReadOnly();
            Settled = Remaining.Count == 0 && diagnostic == "";
            Diagnostic = diagnostic != "" ? diagnostic : Settled ? "" : "UnsettledSocialFeedback";
        }
    }
    /// <summary>Delivery state, never canonical history. Runtime payloads and query entries are immutable.</summary>
    public sealed class SocialFeedbackWorldState
    {
        private Dictionary<string, SocialFeedbackEntry> entries = new Dictionary<string, SocialFeedbackEntry>(StringComparer.Ordinal);
        private bool draining;
        public IReadOnlyList<SocialFeedbackEntry> Entries => entries.Values.OrderBy(e => e.Fact.SourceOccurrenceId, StringComparer.Ordinal).ToList().AsReadOnly();
        internal static bool Valid(RelationalFactRecord fact) => fact != null
            && !string.IsNullOrWhiteSpace(fact.SourceOccurrenceId) && fact.SourceOccurrenceId.StartsWith("activity:", StringComparison.Ordinal)
            && fact.SourceOccurrenceId.Length > "activity:".Length && !string.IsNullOrWhiteSpace(fact.FactType)
            && !string.IsNullOrWhiteSpace(fact.ActorPersonId) && !string.IsNullOrWhiteSpace(fact.TargetPersonId)
            && fact.ActorPersonId != fact.TargetPersonId && fact.Year > 0 && fact.Month >= 1 && fact.Month <= 12 && fact.Day >= 1 && fact.Day <= 31;
        private static bool Same(RelationalFactRecord a, RelationalFactRecord b) => a.FactType == b.FactType
            && a.ActorPersonId == b.ActorPersonId && a.TargetPersonId == b.TargetPersonId && a.Year == b.Year && a.Month == b.Month && a.Day == b.Day;
        public FeedbackEnqueueResult CanEnqueue(RelationalFactRecord fact)
        {
            if (!Valid(fact)) return FeedbackEnqueueResult.InvalidPayload;
            return !entries.TryGetValue(fact.SourceOccurrenceId, out var prior) ? FeedbackEnqueueResult.Success
                : Same(prior.Fact, fact) ? FeedbackEnqueueResult.Duplicate : FeedbackEnqueueResult.Conflict;
        }
        public FeedbackEnqueueResult Enqueue(RelationalFactRecord fact)
        {
            var result = CanEnqueue(fact);
            if (result == FeedbackEnqueueResult.Success) entries.Add(fact.SourceOccurrenceId, new SocialFeedbackEntry(fact, FeedbackStatus.Pending, ""));
            return result;
        }
        public FeedbackDrainResult Drain(Func<RelationalFactRecord, SocialCommitResult> deliver)
        {
            var trace = new List<FeedbackDeliveryTrace>();
            if (draining) return new FeedbackDrainResult(trace, Entries, "DrainInProgress");
            draining = true;
            try
            {
                foreach (var entry in Entries)
                {
                    if (entry.Status == FeedbackStatus.Faulted) continue;
                    SocialCommitResult result;
                    try { result = deliver == null ? SocialCommitResult.InterpreterFailure : deliver(entry.Fact); }
                    catch (Exception) { result = SocialCommitResult.InterpreterFailure; }
                    trace.Add(new FeedbackDeliveryTrace(entry.Fact.SourceOccurrenceId, result));
                    if (result == SocialCommitResult.Success || result == SocialCommitResult.Duplicate)
                        entries.Remove(entry.Fact.SourceOccurrenceId);
                    else if (result != SocialCommitResult.CommitInProgress)
                        entries[entry.Fact.SourceOccurrenceId] = new SocialFeedbackEntry(entry.Fact, FeedbackStatus.Faulted, result.ToString());
                }
                return new FeedbackDrainResult(trace, Entries);
            }
            finally { draining = false; }
        }
        internal void Replace(IEnumerable<SocialFeedbackEntry> replacement)
        { entries = replacement.ToDictionary(e => e.Fact.SourceOccurrenceId, e => e, StringComparer.Ordinal); }
    }
}
