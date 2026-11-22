using System;
using System.Collections.Generic;
using System.Linq;
using RRPG.AutonomousActivity;

namespace RRPG.AutonomousSelection
{
    public enum ContributionFamily
    {
        Behavior,
        Preference,
        Relationship,
        Context,
        Repetition
    }

    public enum DesignerStrength
    {
        Weak,
        Medium,
        Strong
    }

    public sealed class DesireBinding
    {
        public ContributionFamily Family { get; }
        public string Key { get; }
        public DesignerStrength Strength { get; }
        public int Direction { get; }
        public string SourceDomain { get; }
        public string SubjectRole { get; }

        public DesireBinding(ContributionFamily family, string key, DesignerStrength strength, int direction = 1, string domain = null, string role = null)
        {
            if (!Enum.IsDefined(typeof(ContributionFamily), family) || !Enum.IsDefined(typeof(DesignerStrength), strength) || string.IsNullOrWhiteSpace(key) || (direction != 1 && direction != -1))
                throw new ArgumentException("Invalid binding");
            if (family == ContributionFamily.Repetition && direction != -1)
                throw new ArgumentException("Repetition must be negative");
            if (family == ContributionFamily.Context && (string.IsNullOrWhiteSpace(domain) || (role != "Actor" && role != "Target")))
                throw new ArgumentException("Context semantics");
            Family = family;
            Key = key;
            Strength = strength;
            Direction = direction;
            SourceDomain = domain;
            SubjectRole = role;
        }
    }

    public sealed class ActivityDesireProfile
    {
        public string ActivityType { get; }
        public double BaseDesire { get; }
        public IReadOnlyList<DesireBinding> Bindings { get; }

        public ActivityDesireProfile(string type, double basis, IEnumerable<DesireBinding> bindings = null)
        {
            Frozen.Number(basis);
            if (string.IsNullOrWhiteSpace(type))
                throw new ArgumentException("ActivityType");
            ActivityType = type;
            BaseDesire = basis;
            Bindings = Frozen.List(bindings);
        }
    }

    public sealed class PrototypeProviderDefinition
    {
        public string ProviderId { get; }
        public string ActivityType { get; }
        public string PlaceId { get; }
        public bool RequiresTarget { get; }
        public string RequiredContextKey { get; }
        public string ContextDomain { get; }
        public ActivityTargetParticipation TargetParticipation { get; }

        public PrototypeProviderDefinition(string id, string type, string place, bool target, string requirement = null, string domain = null, ActivityTargetParticipation participation = ActivityTargetParticipation.Exclusive)
        {
            TargetParticipation=participation;
            if (!Enum.IsDefined(typeof(ActivityTargetParticipation),participation)) throw new ArgumentException("Participation");
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(type) || string.IsNullOrWhiteSpace(place))
                throw new ArgumentException("Provider");
            ProviderId = id;
            ActivityType = type;
            PlaceId = place;
            RequiresTarget = target;
            RequiredContextKey = requirement;
            ContextDomain = domain;
            if (!string.IsNullOrEmpty(requirement) && (!target || string.IsNullOrWhiteSpace(domain)))
                throw new ArgumentException("Context requirement");
        }
    }

    /// <summary>Calibration is supplied by validated StaticConfig; no production balance constants.</summary>
    public sealed class SelectionCalibration
    {
        private readonly IReadOnlyDictionary<string, double> values;
        public static IReadOnlyList<string> RequiredKeys { get; } = Array.AsReadOnly(new[] { "BehaviorBudget", "PreferenceBudget", "RelationshipBudget", "ContextBudget", "RepetitionBudget", "Weak", "Medium", "Strong", "Threshold", "WeightSpan", "WeightGain", "WeightPower", "NoMajorWeight", "RelationshipReferenceMagnitude" });

        public SelectionCalibration(IEnumerable<KeyValuePair<string, double>> source)
        {
            values = Frozen.Values(source);
            foreach (var key in RequiredKeys)
                if (!values.ContainsKey(key))
                    throw new ArgumentException("Missing calibration " + key);
            foreach (var key in RequiredKeys.Where(k => k != "Threshold"))
                if (values[key] <= 0)
                    throw new ArgumentException("Non-positive calibration " + key);
            if (values["Weak"] > values["Medium"] || values["Medium"] > values["Strong"] || values["Strong"] > 1)
                throw new ArgumentException("Strength ordering");
        }

        public double Query(string key) => values[key];
        public double Budget(ContributionFamily family) => Query(family + "Budget");
        public double Strength(DesignerStrength strength) => Query(strength.ToString());
    }

    public sealed class SelectionCatalog
    {
        public IReadOnlyList<BehaviorChannelDefinition> Channels { get; }
        public IReadOnlyList<ActivityDesireProfile> Activities { get; }
        public IReadOnlyList<PrototypeProviderDefinition> Providers { get; }
        public IReadOnlyList<string> Places { get; }
        public SelectionCalibration Calibration { get; }

        public SelectionCatalog(IEnumerable<BehaviorChannelDefinition> channels, IEnumerable<ActivityDesireProfile> activities, IEnumerable<PrototypeProviderDefinition> providers, IEnumerable<string> places, SelectionCalibration calibration)
        {
            Channels = Frozen.List(channels.OrderBy(c => c.ChannelId, StringComparer.Ordinal));
            Activities = Frozen.List(activities.OrderBy(a => a.ActivityType, StringComparer.Ordinal));
            Providers = Frozen.List(providers.OrderBy(p => p.ProviderId, StringComparer.Ordinal));
            Places = Frozen.List(places.OrderBy(p => p, StringComparer.Ordinal));
            Calibration = calibration ?? throw new ArgumentNullException(nameof(calibration));
            if (Channels.Select(c => c.ChannelId).Distinct().Count() != Channels.Count || Activities.Select(a => a.ActivityType).Distinct().Count() != Activities.Count || Providers.Select(p => p.ProviderId).Distinct().Count() != Providers.Count || Places.Distinct().Count() != Places.Count)
                throw new ArgumentException("Duplicate config");
            foreach (var p in Providers)
                if (!Activities.Any(a => a.ActivityType == p.ActivityType) || !Places.Contains(p.PlaceId))
                    throw new ArgumentException("Dangling provider");
            foreach (var a in Activities)
                foreach (var b in a.Bindings)
                {
                    if (b.Family == ContributionFamily.Behavior && !Channels.Any(c => c.ChannelId == b.Key))
                        throw new ArgumentException("Unknown channel");
                    if (b.Family == ContributionFamily.Preference && b.Key != "leisure.drink")
                        throw new ArgumentException("Ungoverned preference");
                    if (b.Family == ContributionFamily.Relationship && b.Key != "Affinity")
                        throw new ArgumentException("Unknown relationship channel");
                    if (b.Family == ContributionFamily.Context && !((b.Key == "Target.InjurySeverity" && b.SourceDomain == "Person" && b.SubjectRole == "Target") || (b.Key == "Target.HelpNeedSeverity" && b.SourceDomain == "TestContext" && b.SubjectRole == "Target")))
                        throw new ArgumentException("Ungoverned context");
                }
        }
    }

    public interface IDecisionCandidateProvider
    {
        IEnumerable<ActivityCandidate> Collect(DecisionSnapshot snapshot, string actor);
    }

    public sealed class PrototypeActivityProvider : IDecisionCandidateProvider
    {
        public IEnumerable<ActivityCandidate> Collect(DecisionSnapshot snapshot, string actor)
        {
            foreach (var p in snapshot.Catalog.Providers)
            {
                var targets = p.RequiresTarget ? (p.TargetParticipation==ActivityTargetParticipation.ReceptionAtArrival ? snapshot.Persons.Select(x=>x.PersonId) : snapshot.Window.ParticipantAvailablePersonIds).Where(id => id != actor) : new string[]
                {
                    null
                };
                foreach (var target in targets)
                {
                    if (!string.IsNullOrEmpty(p.RequiredContextKey) && snapshot.Signal(p.RequiredContextKey, p.ContextDomain, "Target", target) <= 0)
                        continue;
                    var recent=snapshot.Person(actor);
                    if(p.TargetParticipation==ActivityTargetParticipation.ReceptionAtArrival && recent.LastMissedMinute.HasValue
                        && recent.LastMissedActivityType==p.ActivityType && recent.LastMissedTargetPersonId==target
                        && snapshot.Window.OpportunityMinute-recent.LastMissedMinute.Value<snapshot.ReceptionCooldownMinutes) continue;
                    var key = Frozen.Part(p.ProviderId) + Frozen.Part(actor) + Frozen.Part(target) + Frozen.Part(p.PlaceId);
                    yield return new ActivityCandidate(key, p.ActivityType, p.ProviderId, actor, target, p.PlaceId, null, p.TargetParticipation);
                }
            }
        }
    }
}
