using System;
using System.Collections.Generic;
using System.Linq;

namespace RRPG.Social
{
    public sealed class RelationshipInterpretationRule
    {
        public string RuleId { get; }
        public string FactType { get; }
        public string SourceRole { get; }
        public string TargetRole { get; }
        public string Channel { get; }
        public string Polarity { get; }
        public string Strength { get; }
        public RelationshipInterpretationRule(string id, string fact, string source, string target, string channel, string polarity, string strength)
        { RuleId = id; FactType = fact; SourceRole = source; TargetRole = target; Channel = channel; Polarity = polarity; Strength = strength; }
    }

    /// <summary>Social-owned immutable configuration. Numeric values remain prototype calibration.</summary>
    public sealed class SocialRuleCatalog
    {
        private readonly HashSet<string> facts;
        private readonly IReadOnlyList<RelationshipInterpretationRule> rules;
        private readonly Dictionary<string, float> calibration;
        public SocialRuleCatalog(IEnumerable<string> factTypes, IEnumerable<RelationshipInterpretationRule> interpretationRules,
            IEnumerable<KeyValuePair<string, float>> values)
        {
            facts = new HashSet<string>(StringComparer.Ordinal);
            foreach (var fact in factTypes ?? throw new ArgumentNullException(nameof(factTypes)))
                if (string.IsNullOrWhiteSpace(fact) || !facts.Add(fact)) throw new ArgumentException("Invalid/duplicate fact type");
            calibration = new Dictionary<string, float>(StringComparer.Ordinal);
            foreach (var v in values ?? throw new ArgumentNullException(nameof(values)))
            {
                if (string.IsNullOrWhiteSpace(v.Key) || float.IsNaN(v.Value) || float.IsInfinity(v.Value) || v.Value <= 0 || calibration.ContainsKey(v.Key))
                    throw new ArgumentException("Invalid/duplicate social calibration");
                calibration.Add(v.Key, v.Value);
            }
            foreach (var key in new[] { "AffinityDeltaWeak", "AffinityDeltaMedium", "AffinityDeltaStrong" })
                if (!calibration.ContainsKey(key)) throw new ArgumentException("Missing social calibration " + key);
            if (calibration["AffinityDeltaWeak"] > calibration["AffinityDeltaMedium"] || calibration["AffinityDeltaMedium"] > calibration["AffinityDeltaStrong"])
                throw new ArgumentException("Invalid social strength ordering");
            var copy = (interpretationRules ?? throw new ArgumentNullException(nameof(interpretationRules))).ToList();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var pairs = new HashSet<string>(StringComparer.Ordinal);
            foreach (var r in copy)
            {
                if (r == null || string.IsNullOrWhiteSpace(r.RuleId) || !ids.Add(r.RuleId) || !facts.Contains(r.FactType)
                    || !Role(r.SourceRole) || !Role(r.TargetRole) || r.SourceRole == r.TargetRole || r.Channel != "Affinity"
                    || (r.Polarity != "Positive" && r.Polarity != "Negative") || !calibration.ContainsKey("AffinityDelta" + r.Strength)
                    || !new[] { "Weak", "Medium", "Strong" }.Contains(r.Strength)
                    || !pairs.Add(r.FactType + ":" + r.SourceRole + ":" + r.TargetRole))
                    throw new ArgumentException("Invalid/duplicate social rule");
            }
            rules = copy.OrderBy(r => r.RuleId, StringComparer.Ordinal).ToList().AsReadOnly();
        }
        private static bool Role(string role) => role == "Actor" || role == "Target";
        public bool CanInterpret(string factType) => facts.Contains(factType ?? "") && rules.Any(r => r.FactType == factType);
        public bool CanInterpret(RelationalFactRecord fact) => fact != null && fact.IsValid && CanInterpret(fact.FactType);
        internal IEnumerable<RelationshipMutation> Interpret(RelationalFactRecord fact)
        {
            if (!CanInterpret(fact)) throw new ArgumentException("Uninterpretable fact");
            foreach (var rule in rules.Where(r => r.FactType == fact.FactType))
                yield return new RelationshipMutation(rule.SourceRole == "Actor" ? fact.ActorPersonId : fact.TargetPersonId,
                    rule.TargetRole == "Actor" ? fact.ActorPersonId : fact.TargetPersonId,
                    calibration["AffinityDelta" + rule.Strength] * (rule.Polarity == "Positive" ? 1 : -1));
        }
    }

    public sealed class ConfiguredRelationshipInterpreter : IRelationshipInterpreter
    {
        private readonly SocialRuleCatalog catalog;
        public ConfiguredRelationshipInterpreter(SocialRuleCatalog catalog) { this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog)); }
        public IEnumerable<RelationshipMutation> Interpret(RelationalFactRecord fact, IRelationshipQuery query) => catalog.Interpret(fact);
    }
}
