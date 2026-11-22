using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using RRPG.AutonomousActivity;

namespace RRPG.AutonomousSelection
{
    internal static class Frozen
    {
        internal static IReadOnlyList<T> List<T>(IEnumerable<T> s) => new List<T>(s ?? Enumerable.Empty<T>()).AsReadOnly();
        internal static IReadOnlyDictionary<string, double> Values(IEnumerable<KeyValuePair<string, double>> source)
        {
            var d = new Dictionary<string, double>(StringComparer.Ordinal);
            foreach (var p in source ?? Enumerable.Empty<KeyValuePair<string, double>>())
            {
                Number(p.Value);
                if (string.IsNullOrWhiteSpace(p.Key))
                    throw new ArgumentException("Empty key");
                d.Add(p.Key, p.Value);
            }

            return new ReadOnlyDictionary<string, double>(d);
        }

        internal static void Number(double v)
        {
            if (double.IsNaN(v) || double.IsInfinity(v))
                throw new ArgumentException("Non-finite");
        }

        internal static double Clamp(double v, double min, double max) => Math.Max(min, Math.Min(max, v));
        internal static string Part(string v) => (v ?? "").Length.ToString(CultureInfo.InvariantCulture) + ":" + (v ?? "");
    }

    public sealed class BehaviorChannelDefinition
    {
        public string ChannelId { get; }
        public string DisplayName { get; }
        public string Description { get; }
        public double NeutralValue { get; }
        public double Min { get; }
        public double Max { get; }

        public BehaviorChannelDefinition(string id, string name, string description, double neutral, double min, double max)
        {
            Frozen.Number(neutral);
            Frozen.Number(min);
            Frozen.Number(max);
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(description) || min < 0 || max > 1 || min >= max || neutral <= min || neutral >= max)
                throw new ArgumentException("Invalid channel");
            ChannelId = id;
            DisplayName = name;
            Description = description;
            NeutralValue = neutral;
            Min = min;
            Max = max;
        }
    }

    public sealed class ResolvedBehaviorProfile
    {
        private readonly IReadOnlyDictionary<string, double> values;
        public ResolvedBehaviorProfile(IEnumerable<KeyValuePair<string, double>> source = null)
        {
            values = Frozen.Values(source);
        }

        public int Count => values.Count;

        public double Query(BehaviorChannelDefinition d) => values.TryGetValue(d.ChannelId, out var v) ? Frozen.Clamp(v, d.Min, d.Max) : d.NeutralValue;
        public double Normalized(BehaviorChannelDefinition d)
        {
            var delta = Query(d) - d.NeutralValue;
            return delta / (delta >= 0 ? d.Max - d.NeutralValue : d.NeutralValue - d.Min);
        }
    }

    public sealed class SpecificPreferenceView
    {
        private readonly IReadOnlyDictionary<string, double> values;
        public SpecificPreferenceView(IEnumerable<KeyValuePair<string, double>> source = null)
        {
            values = Frozen.Values(source);
            if (values.Values.Any(v => v < -1 || v > 1))
                throw new ArgumentException("Preference range");
        }

        public int Count => values.Count;

        public double Query(string key) => values.TryGetValue(key ?? "", out var v) ? v : 0;
    }

    public sealed class DecisionPersonSnapshot
    {
        public string PersonId { get; }
        public string PersonDefinitionId { get; }
        public string Diagnostic { get; }
        public ResolvedBehaviorProfile Behavior { get; }
        public SpecificPreferenceView Preferences { get; }
        public ActivityDayStamp? LastParticipationDay { get; }
        public ActivityDayStamp? LastInitiatedDay { get; }
        public string LastInitiatedType { get; }
        public string LastMissedActivityType { get; }
        public string LastMissedTargetPersonId { get; }
        public long? LastMissedMinute { get; }

        public DecisionPersonSnapshot(string id, string definition, ResolvedBehaviorProfile behavior = null, SpecificPreferenceView preferences = null, ActivityDayStamp? participation = null, ActivityDayStamp? initiated = null, string type = null, string diagnostic = null, string missedType = null, string missedTarget = null, long? missedMinute = null)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("Missing PersonId");
            PersonId = id;
            PersonDefinitionId = definition;
            Behavior = behavior ?? new ResolvedBehaviorProfile();
            Preferences = preferences ?? new SpecificPreferenceView();
            LastParticipationDay = participation;
            LastInitiatedDay = initiated;
            LastInitiatedType = type;
            Diagnostic = diagnostic ?? "";
            LastMissedActivityType=missedType; LastMissedTargetPersonId=missedTarget; LastMissedMinute=missedMinute;
        }
    }

    public sealed class RelationshipSnapshot
    {
        public string SourcePersonId { get; }
        public string TargetPersonId { get; }
        public double Affinity { get; }

        public RelationshipSnapshot(string source, string target, double affinity)
        {
            Frozen.Number(affinity);
            if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(target) || source == target)
                throw new ArgumentException("Invalid relationship");
            SourcePersonId = source;
            TargetPersonId = target;
            Affinity = affinity;
        }
    }

    public sealed class ContextSignal
    {
        public string Key { get; }
        public string SourceDomain { get; }
        public string SubjectRole { get; }
        public string PersonId { get; }
        public double Magnitude { get; }

        public ContextSignal(string key, string domain, string role, string person, double magnitude)
        {
            Frozen.Number(magnitude);
            if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(domain) || string.IsNullOrWhiteSpace(person) || (role != "Actor" && role != "Target") || magnitude < 0 || magnitude > 1)
                throw new ArgumentException("Invalid context");
            Key = key;
            SourceDomain = domain;
            SubjectRole = role;
            PersonId = person;
            Magnitude = magnitude;
        }
    }

    public sealed class DecisionSnapshot
    {
        public ActivityDailyWindow Window { get; }
        public ActivityDayStamp Day => Window.DayStamp;
        public string RoundIdentity { get; }
        public IReadOnlyList<DecisionPersonSnapshot> Persons { get; }
        public IReadOnlyList<RelationshipSnapshot> Relationships { get; }
        public IReadOnlyList<ContextSignal> Context { get; }
        public SelectionCatalog Catalog { get; }
        public int ReceptionCooldownMinutes { get; }

        public DecisionSnapshot(ActivityDailyWindow window, IEnumerable<DecisionPersonSnapshot> persons, IEnumerable<RelationshipSnapshot> relationships, IEnumerable<ContextSignal> context, SelectionCatalog catalog, int receptionCooldownMinutes = 120)
        {
            if(receptionCooldownMinutes<=0) throw new ArgumentOutOfRangeException(nameof(receptionCooldownMinutes));
            ReceptionCooldownMinutes=receptionCooldownMinutes;
            Window = window ?? throw new ArgumentNullException(nameof(window));
            if (!window.DayStamp.IsValid)
                throw new ArgumentException("Invalid day");
            RoundIdentity = window.IsIntraday ? window.OpportunityId
                : string.Format(CultureInfo.InvariantCulture, "village.daily.major|{0}|{1}|{2}", Day.Year, Day.Month, Day.Day);
            Persons = Frozen.List(persons.OrderBy(p => p.PersonId, StringComparer.Ordinal));
            if (Persons.Select(p => p.PersonId).Distinct(StringComparer.Ordinal).Count() != Persons.Count)
                throw new ArgumentException("Duplicate PersonId");
            foreach (var id in window.ActorEligiblePersonIds.Concat(window.ParticipantAvailablePersonIds))
                if (!Persons.Any(p => p.PersonId == id))
                    throw new ArgumentException("Missing window person " + id);
            Relationships = Frozen.List((relationships ?? Enumerable.Empty<RelationshipSnapshot>()).OrderBy(r => r.SourcePersonId, StringComparer.Ordinal).ThenBy(r => r.TargetPersonId, StringComparer.Ordinal));
            if (Relationships.Select(r => Frozen.Part(r.SourcePersonId) + Frozen.Part(r.TargetPersonId)).Distinct().Count() != Relationships.Count)
                throw new ArgumentException("Duplicate relationship");
            Context = Frozen.List((context ?? Enumerable.Empty<ContextSignal>()).OrderBy(c => c.PersonId, StringComparer.Ordinal).ThenBy(c => c.Key, StringComparer.Ordinal).ThenBy(c => c.SubjectRole, StringComparer.Ordinal));
            if (Context.Select(c => Frozen.Part(c.PersonId) + Frozen.Part(c.Key) + Frozen.Part(c.SubjectRole)).Distinct().Count() != Context.Count)
                throw new ArgumentException("Duplicate context");
            Catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            foreach (var c in Context)
                if (!((c.Key == "Target.InjurySeverity" && c.SourceDomain == "Person" && c.SubjectRole == "Target") || (c.Key == "Target.HelpNeedSeverity" && c.SourceDomain == "TestContext" && c.SubjectRole == "Target")))
                    throw new ArgumentException("Ungoverned context signal");
        }

        public DecisionPersonSnapshot Person(string id) => Persons.FirstOrDefault(p => p.PersonId == id);
        public double Affinity(string a, string b) => Relationships.FirstOrDefault(r => r.SourcePersonId == a && r.TargetPersonId == b)?.Affinity ?? 0;
        public double Signal(string key, string domain, string role, string person) => Context.FirstOrDefault(c => c.Key == key && c.SourceDomain == domain && c.SubjectRole == role && c.PersonId == person)?.Magnitude ?? 0;
    }
}
