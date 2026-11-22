using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace RRPG.StaticConfig
{
    [Serializable]
    public sealed class RelationalFactTypeConfigRow { public string FactType; public string DisplayName; }
    [Serializable]
    public sealed class RelationshipInterpretationRuleConfigRow
    {
        public string RuleId;
        public string FactType;
        public string SourceRole;
        public string TargetRole;
        public string RelationshipChannel;
        public string Polarity;
        public string Strength;
    }
    [Serializable]
    public sealed class SocialCalibrationConfigRow { public string CalibrationKey; public float Value; }
    public sealed class SocialConfigDatabase : ScriptableObject
    {
        [SerializeField] private List<RelationalFactTypeConfigRow> factTypes = new List<RelationalFactTypeConfigRow>();
        [SerializeField] private List<RelationshipInterpretationRuleConfigRow> rules = new List<RelationshipInterpretationRuleConfigRow>();
        [SerializeField] private List<SocialCalibrationConfigRow> calibration = new List<SocialCalibrationConfigRow>();
        public IReadOnlyList<RelationalFactTypeConfigRow> RelationalFactType => factTypes.AsReadOnly();
        public IReadOnlyList<RelationshipInterpretationRuleConfigRow> RelationshipInterpretationRule => rules.AsReadOnly();
        public IReadOnlyList<SocialCalibrationConfigRow> SocialCalibration => calibration.AsReadOnly();
        public int TotalCount => factTypes.Count + rules.Count + calibration.Count;
        public static void ValidateContent(IEnumerable<RelationalFactTypeConfigRow> facts, IEnumerable<RelationshipInterpretationRuleConfigRow> interpretation, IEnumerable<SocialCalibrationConfigRow> values)
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var f in facts)
                if (f == null || string.IsNullOrWhiteSpace(f.FactType) || !ids.Add(f.FactType)) throw new ArgumentException("Invalid/duplicate FactType");
            var calibrations = new Dictionary<string, float>(StringComparer.Ordinal);
            foreach (var c in values)
            {
                if (c == null || string.IsNullOrWhiteSpace(c.CalibrationKey) || float.IsNaN(c.Value) || float.IsInfinity(c.Value) || c.Value <= 0 || calibrations.ContainsKey(c.CalibrationKey)) throw new ArgumentException("Invalid/duplicate SocialCalibration");
                calibrations.Add(c.CalibrationKey, c.Value);
            }
            foreach (var key in new[] { "AffinityDeltaWeak", "AffinityDeltaMedium", "AffinityDeltaStrong" })
                if (!calibrations.ContainsKey(key)) throw new ArgumentException("Missing SocialCalibration " + key);
            if (calibrations["AffinityDeltaWeak"] > calibrations["AffinityDeltaMedium"] || calibrations["AffinityDeltaMedium"] > calibrations["AffinityDeltaStrong"]) throw new ArgumentException("Invalid strength ordering");
            var ruleIds = new HashSet<string>(StringComparer.Ordinal); var pairs = new HashSet<string>(StringComparer.Ordinal);
            foreach (var r in interpretation)
                if (r == null || string.IsNullOrWhiteSpace(r.RuleId) || !ruleIds.Add(r.RuleId) || !ids.Contains(r.FactType)
                    || (r.SourceRole != "Actor" && r.SourceRole != "Target") || (r.TargetRole != "Actor" && r.TargetRole != "Target") || r.SourceRole == r.TargetRole
                    || r.RelationshipChannel != "Affinity" || (r.Polarity != "Positive" && r.Polarity != "Negative") || !new[] { "Weak", "Medium", "Strong" }.Contains(r.Strength)
                    || !pairs.Add(r.FactType + ":" + r.SourceRole + ":" + r.TargetRole)) throw new ArgumentException("Invalid/duplicate RelationshipInterpretationRule");
        }
        public void ApplyGeneratedContent(List<RelationalFactTypeConfigRow> facts, List<RelationshipInterpretationRuleConfigRow> interpretation, List<SocialCalibrationConfigRow> values)
        {
            // Validate the complete candidate content before replacing current generated data.
            if (facts.Count + interpretation.Count + values.Count > 0)
                ValidateContent(facts, interpretation, values);
            factTypes = new List<RelationalFactTypeConfigRow>(facts); rules = new List<RelationshipInterpretationRuleConfigRow>(interpretation); calibration = new List<SocialCalibrationConfigRow>(values);
        }
    }
}
