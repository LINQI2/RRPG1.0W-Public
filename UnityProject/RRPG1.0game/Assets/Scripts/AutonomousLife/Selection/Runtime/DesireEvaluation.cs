using System;
using System.Collections.Generic;
using System.Linq;
using RRPG.AutonomousActivity;
using RRPG.Deterministic;

namespace RRPG.AutonomousSelection
{
    public sealed class BindingContribution
    {
        public DesireBinding Binding { get; }
        public double Input { get; }
        public double Value { get; }

        public BindingContribution(DesireBinding binding, double input, double value)
        {
            Binding = binding;
            Input = input;
            Value = value;
        }
    }

    public sealed class ContributionBreakdown
    {
        public double BaseDesire { get; }
        public IReadOnlyList<BindingContribution> Bindings { get; }
        public IReadOnlyDictionary<string, double> Families { get; }
        public double Score { get; }

        internal ContributionBreakdown(double basis, IEnumerable<BindingContribution> bindings, SelectionCalibration c)
        {
            BaseDesire = basis;
            Bindings = Frozen.List(bindings);
            Families = Frozen.Values(Enum.GetValues(typeof(ContributionFamily)).Cast<ContributionFamily>().Select(f => new KeyValuePair<string, double>(f.ToString(), Frozen.Clamp(Bindings.Where(b => b.Binding.Family == f).Sum(b => b.Value), -c.Budget(f), f == ContributionFamily.Repetition ? 0 : c.Budget(f)))));
            Score = basis + Families.Values.Sum();
            Frozen.Number(Score);
        }
    }

    public sealed class CandidateEvaluationResult
    {
        public ActivityCandidate Candidate { get; }
        public bool Eligible { get; }
        public string EligibilityReason { get; }
        public ContributionBreakdown Breakdown { get; }
        public bool Plausible { get; }
        public double Weight { get; }

        internal CandidateEvaluationResult(ActivityCandidate candidate, bool eligible, string reason, ContributionBreakdown breakdown, double threshold, double weight)
        {
            Candidate = candidate;
            Eligible = eligible;
            EligibilityReason = reason;
            Breakdown = breakdown;
            Plausible = eligible && breakdown.Score >= threshold;
            Weight = Plausible ? weight : 0;
        }
    }

    public static class DesireEvaluator
    {
        public static double Weight(double score, SelectionCalibration c)
        {
            Frozen.Number(score);
            var x = Frozen.Clamp((score - c.Query("Threshold")) / c.Query("WeightSpan"), 0, 1);
            var w = 1 + c.Query("WeightGain") * Math.Pow(x, c.Query("WeightPower"));
            Frozen.Number(w);
            return w;
        }

        public static CandidateEvaluationResult Evaluate(DecisionSnapshot s, ActivityCandidate candidate)
        {
            if (candidate == null || string.IsNullOrWhiteSpace(candidate.CandidateKey) || string.IsNullOrWhiteSpace(candidate.ActivityType) || string.IsNullOrWhiteSpace(candidate.ProviderId) || candidate.CandidateKey == SelectionOption.NoMajorKey)
                throw new ArgumentException("Invalid candidate");
            var p = s.Person(candidate.ActorPersonId) ?? throw new ArgumentException("Unknown actor");
            var profile = s.Catalog.Activities.Single(a => a.ActivityType == candidate.ActivityType);
            var eligibility = Eligibility(s, candidate);
            var c = s.Catalog.Calibration;
            var contributions = new List<BindingContribution>();
            foreach (var b in profile.Bindings)
            {
                double input = 0;
                switch (b.Family)
                {
                    case ContributionFamily.Behavior:
                        input = p.Behavior.Normalized(s.Catalog.Channels.Single(d => d.ChannelId == b.Key));
                        break;
                    case ContributionFamily.Preference:
                        input = p.Preferences.Query(b.Key);
                        break;
                    case ContributionFamily.Relationship:
                        input = Frozen.Clamp(s.Affinity(p.PersonId, candidate.TargetPersonId) / c.Query("RelationshipReferenceMagnitude"), -1, 1);
                        break;
                    case ContributionFamily.Context:
                        input = s.Signal(b.Key, b.SourceDomain, b.SubjectRole, b.SubjectRole == "Actor" ? p.PersonId : candidate.TargetPersonId);
                        break;
                    // Minimal last-initiated-type comparison only. No streak, history array or invented time window.
                    case ContributionFamily.Repetition:
                        input = p.LastInitiatedDay.HasValue && p.LastInitiatedType == candidate.ActivityType ? 1 : 0;
                        break;
                }

                contributions.Add(new BindingContribution(b, input, input * b.Direction * c.Strength(b.Strength) * c.Budget(b.Family)));
            }

            var breakdown = new ContributionBreakdown(profile.BaseDesire, contributions, c);
            return new CandidateEvaluationResult(candidate, eligibility == "Eligible", eligibility, breakdown, c.Query("Threshold"), Weight(breakdown.Score, c));
        }

        public static IReadOnlyList<string> Participants(ActivityCandidate c) => c.ClaimParticipantPersonIds;
        public static string Eligibility(DecisionSnapshot s, ActivityCandidate c)
        {
            if (!s.Window.ActorEligiblePersonIds.Contains(c.ActorPersonId))
                return "ActorIneligible";
            if (!string.IsNullOrWhiteSpace(c.TargetPersonId) && s.Person(c.TargetPersonId)==null) return "InvalidTarget";
            if (c.TargetPersonId == c.ActorPersonId)
                return "SelfTarget";
            foreach (var id in Participants(c))
            {
                if (s.Person(id) == null)
                    return "InvalidParticipant";
                if (id != c.ActorPersonId && !s.Window.ParticipantAvailablePersonIds.Contains(id))
                    return "ParticipantUnavailable";
                if (!s.Window.IsIntraday && s.Person(id).LastParticipationDay == s.Day)
                    return "AlreadyParticipated";
            }

            return "Eligible";
        }
    }

    public sealed class SelectionOption
    {
        public const string NoMajorKey = "special.no_major_activity";
        public ActivityCandidate Candidate { get; }
        public string StableKey => Candidate?.CandidateKey ?? NoMajorKey;
        public bool IsNoMajor => Candidate == null;
        public double Weight { get; }
        public string RandomIdentity { get; }
        public double RandomValue { get; }
        public double OrderKey { get; }
        public CandidateEvaluationResult Evaluation { get; }

        public SelectionOption(string round, string actor, ActivityCandidate candidate, double weight, CandidateEvaluationResult evaluation = null)
        {
            Frozen.Number(weight);
            if (weight <= 0)
                throw new ArgumentException("Weight");
            Candidate = candidate;
            Weight = weight;
            Evaluation = evaluation;
            RandomIdentity = round + "|" + Frozen.Part(actor) + "|" + Frozen.Part(StableKey) + "|selection";
            // 52 bits plus half a unit: strictly inside (0,1), including extreme hashes.
            RandomValue = ((DeterministicRandom.StableHash64(RandomIdentity) >> 12) + 0.5) / 4503599627370496.0;
            OrderKey = -Math.Log(RandomValue) / weight;
        }
    }

    public static class WeightedCandidateOrder
    {
        public static IReadOnlyList<SelectionOption> Build(DecisionSnapshot snapshot, string actor, IEnumerable<CandidateEvaluationResult> evaluations)
        {
            var list = evaluations.Where(e => e.Plausible).Select(e => new SelectionOption(snapshot.RoundIdentity, actor, e.Candidate, e.Weight, e)).ToList();
            if (list.Select(o => o.StableKey).Distinct(StringComparer.Ordinal).Count() != list.Count)
                throw new ArgumentException("Duplicate CandidateKey");
            list.Add(new SelectionOption(snapshot.RoundIdentity, actor, null, snapshot.Catalog.Calibration.Query("NoMajorWeight")));
            return Frozen.List(list.OrderBy(o => o.OrderKey).ThenBy(o => o.StableKey, StringComparer.Ordinal));
        }
    }
}
