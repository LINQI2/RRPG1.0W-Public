using System;
using System.Collections.Generic;
using System.Linq;
using RRPG.AutonomousActivity;
using RRPG.Deterministic;

namespace RRPG.AutonomousSelection
{
    public enum AcceptanceReason
    {
        Accepted,
        ReceptionDeferred,
        ParticipantUnavailable,
        InvalidParticipant,
        HardReject
    }

    public sealed class ParticipantAcceptanceResult
    {
        public AcceptanceReason Reason { get; }
        public bool Accepted => Reason == AcceptanceReason.Accepted || Reason == AcceptanceReason.ReceptionDeferred;

        public ParticipantAcceptanceResult(AcceptanceReason reason)
        {
            Reason = reason;
        }
    }

    public interface IParticipantAcceptance
    {
        ParticipantAcceptanceResult Evaluate(DecisionSnapshot snapshot, Proposal proposal);
    }

    public sealed class MinimalAcceptance : IParticipantAcceptance
    {
        public ParticipantAcceptanceResult Evaluate(DecisionSnapshot s, Proposal p)
        {
            if (p.Candidate.TargetPersonId == p.Candidate.ActorPersonId || p.Participants.Any(id => s.Person(id) == null))
                return new ParticipantAcceptanceResult(AcceptanceReason.InvalidParticipant);
            if (p.Participants.Any(id => id != p.Candidate.ActorPersonId && !s.Window.ParticipantAvailablePersonIds.Contains(id)
                || !s.Window.IsIntraday && s.Person(id).LastParticipationDay == s.Day))
                return new ParticipantAcceptanceResult(AcceptanceReason.ParticipantUnavailable);
            return new ParticipantAcceptanceResult(p.Candidate.TargetParticipation==ActivityTargetParticipation.ReceptionAtArrival ? AcceptanceReason.ReceptionDeferred : AcceptanceReason.Accepted);
        }
    }

    /// <summary>Arbitration input deliberately contains no desire, weight or evaluation reference.</summary>
    public sealed class Proposal
    {
        public string Identity { get; }
        public ActivityCandidate Candidate { get; }
        public IReadOnlyList<string> Participants { get; }
        public ulong Priority { get; }

        public Proposal(string round, ActivityCandidate candidate)
        {
            Candidate = candidate ?? throw new ArgumentNullException(nameof(candidate));
            Participants = DesireEvaluator.Participants(candidate);
            Identity = round + "|" + Frozen.Part(candidate.ActorPersonId) + "|" + Frozen.Part(candidate.CandidateKey);
            Priority = DeterministicRandom.StableHash64(round + "|" + Identity + "|arbitration");
        }
    }

    public sealed class ProposalResult
    {
        public Proposal Proposal { get; }
        public int Round { get; }
        public string Result { get; }

        public ProposalResult(Proposal p, int round, string result)
        {
            Proposal = p;
            Round = round;
            Result = result;
        }
    }

    public static class FairArbitration
    {
        public static IReadOnlyList<ProposalResult> Resolve(IEnumerable<Proposal> proposals, IEnumerable<string> previouslyAllocated, int round)
        {
            var used = new HashSet<string>(previouslyAllocated ?? Enumerable.Empty<string>(), StringComparer.Ordinal);
            var results = new List<ProposalResult>();
            foreach (var p in proposals.OrderBy(p => p.Priority).ThenBy(p => p.Identity, StringComparer.Ordinal))
            {
                bool conflict = p.Participants.Any(used.Contains);
                results.Add(new ProposalResult(p, round, conflict ? "ConflictLoss" : "Winner"));
                if (!conflict)
                    foreach (var id in p.Participants)
                        used.Add(id);
            }

            return Frozen.List(results);
        }
    }

    public sealed class ActorDecisionTrace
    {
        public string ActorPersonId { get; }
        public IReadOnlyList<CandidateEvaluationResult> Evaluations { get; }
        public IReadOnlyList<SelectionOption> Order { get; }
        public IReadOnlyList<ProposalResult> Proposals { get; }
        public string FinalResult { get; }

        internal ActorDecisionTrace(string actor, IEnumerable<CandidateEvaluationResult> evaluations, IEnumerable<SelectionOption> order, IEnumerable<ProposalResult> proposals, string final)
        {
            ActorPersonId = actor;
            Evaluations = Frozen.List(evaluations);
            Order = Frozen.List(order);
            Proposals = Frozen.List(proposals);
            FinalResult = final;
        }
    }

    public sealed class DecisionPlan
    {
        public DecisionSnapshot Snapshot { get; }
        public IReadOnlyList<Proposal> Winners { get; }
        public IReadOnlyList<ActorDecisionTrace> Trace { get; }

        internal DecisionPlan(DecisionSnapshot snapshot, IEnumerable<Proposal> winners, IEnumerable<ActorDecisionTrace> trace)
        {
            Snapshot = snapshot;
            Winners = Frozen.List(winners);
            Trace = Frozen.List(trace);
        }
    }

    public sealed class DecisionPlanner
    {
        private readonly IDecisionCandidateProvider provider;
        private readonly IParticipantAcceptance acceptance;
        public DecisionPlanner(IDecisionCandidateProvider provider = null, IParticipantAcceptance acceptance = null)
        {
            this.provider = provider ?? new PrototypeActivityProvider();
            this.acceptance = acceptance ?? new MinimalAcceptance();
        }

        private sealed class Cursor
        {
            internal string Actor;
            internal IReadOnlyList<CandidateEvaluationResult> Evaluations;
            internal IReadOnlyList<SelectionOption> Order;
            internal int Index;
            internal string Final;
            internal List<ProposalResult> Results = new List<ProposalResult>();
        }

        /// <summary>All external callbacks finish here before the execution driver mutates Activity.</summary>
        public DecisionPlan Plan(DecisionSnapshot snapshot)
        {
            var cursors = new List<Cursor>();
            foreach (var id in snapshot.Window.ActorEligiblePersonIds)
            {
                var candidates = (provider.Collect(snapshot, id) ?? throw new ArgumentException("Null candidate collection")).ToList();
                if (candidates.Any(c => c == null || c.ActorPersonId != id) || candidates.Select(c => c.CandidateKey).Distinct(StringComparer.Ordinal).Count() != candidates.Count)
                    throw new ArgumentException("Invalid provider output");
                var evaluations = Frozen.List(candidates.OrderBy(c => c.CandidateKey, StringComparer.Ordinal).Select(c => DesireEvaluator.Evaluate(snapshot, c)));
                cursors.Add(new Cursor { Actor = id, Evaluations = evaluations, Order = WeightedCandidateOrder.Build(snapshot, id, evaluations) });
            }

            var allocated = new HashSet<string>(StringComparer.Ordinal);
            var winners = new List<Proposal>();
            int limit = cursors.Sum(c => c.Order.Count) + cursors.Count + 1;
            for (int round = 1; cursors.Any(c => c.Final == null); round++)
            {
                if (round > limit)
                    throw new InvalidOperationException("Unbounded arbitration");
                var proposals = new List<Proposal>();
                foreach (var c in cursors.Where(c => c.Final == null))
                {
                    if (allocated.Contains(c.Actor))
                    {
                        c.Final = "Participating";
                        continue;
                    }

                    if (c.Index >= c.Order.Count)
                    {
                        c.Final = "Exhausted";
                        continue;
                    }

                    var option = c.Order[c.Index];
                    if (option.IsNoMajor)
                    {
                        c.Final = "NoMajor";
                        continue;
                    }

                    var proposal = new Proposal(snapshot.RoundIdentity, option.Candidate);
                    var result = acceptance.Evaluate(snapshot, proposal) ?? throw new ArgumentException("Null acceptance");
                    if (!Enum.IsDefined(typeof(AcceptanceReason), result.Reason))
                        throw new ArgumentException("Invalid acceptance result");
                    if (!result.Accepted)
                    {
                        c.Results.Add(new ProposalResult(proposal, round, result.Reason.ToString()));
                        c.Index++;
                    }
                    else
                        proposals.Add(proposal);
                }

                foreach (var result in FairArbitration.Resolve(proposals, allocated, round))
                {
                    var c = cursors.Single(x => x.Actor == result.Proposal.Candidate.ActorPersonId);
                    c.Results.Add(result);
                    if (result.Result == "Winner")
                    {
                        winners.Add(result.Proposal);
                        c.Final = "Planned";
                        foreach (var id in result.Proposal.Participants)
                            allocated.Add(id);
                    }
                    else
                        c.Index++;
                }
            }

            return new DecisionPlan(snapshot, winners, cursors.Select(c => new ActorDecisionTrace(c.Actor, c.Evaluations, c.Order, c.Results, c.Final)));
        }
    }
}
