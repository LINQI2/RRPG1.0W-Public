using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace RRPG.ConfigCompiler.Core
{
    /// <summary>Social authoring governance inside the existing Compiler pipeline.</summary>
    public static class SocialValidator
    {
        public static readonly IReadOnlyDictionary<string, string[]> Fields = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            { "RelationalFactType", new[] { "FactType", "DisplayName" } },
            { "RelationshipInterpretationRule", new[] { "RuleId", "FactType", "SourceRole", "TargetRole", "RelationshipChannel", "Polarity", "Strength" } },
            { "SocialCalibration", new[] { "CalibrationKey", "Value" } }
        };
        public static void Validate(IReadOnlyList<CompiledSheet> sheets, DiagnosticBag bag)
        {
            var domain = sheets.Where(s => Fields.ContainsKey(s.Spec.SheetName)).ToList();
            var projection = sheets.FirstOrDefault(s => s.Spec.SheetName == "ActivitySocialProjection");
            if (domain.Count == 0 && (projection == null || projection.Rows.Count == 0)) return;
            var anchor = domain.FirstOrDefault() ?? projection;
            void error(string message) => bag.Error("DAT_SOCIAL_INVALID", message, DiagContext.Of(anchor.Spec.Workbook, anchor.Spec.SheetName, 0));
            string text(CompiledRow row, string key) => Convert.ToString(row.Get(key)?.Value, CultureInfo.InvariantCulture);
            foreach (var f in Fields)
            {
                var table = domain.FirstOrDefault(s => s.Spec.SheetName == f.Key);
                if (table == null) { error("Missing Social table " + f.Key); continue; }
                foreach (var field in f.Value)
                    if (!table.Spec.Fields.Any(c => c.Field == field)) error(f.Key + " missing field " + field);
            }
            var facts = domain.FirstOrDefault(t => t.Spec.SheetName == "RelationalFactType");
            var rules = domain.FirstOrDefault(t => t.Spec.SheetName == "RelationshipInterpretationRule");
            var values = domain.FirstOrDefault(t => t.Spec.SheetName == "SocialCalibration");
            var factIds = new HashSet<string>(StringComparer.Ordinal);
            if (facts != null) foreach (var row in facts.Rows)
                if (string.IsNullOrWhiteSpace(text(row, "FactType")) || !factIds.Add(text(row, "FactType"))) error("Invalid/duplicate FactType");
            var pairs = new HashSet<string>(StringComparer.Ordinal);
            var ruleIds = new HashSet<string>(StringComparer.Ordinal);
            var interpretable = new HashSet<string>(StringComparer.Ordinal);
            if (rules != null) foreach (var r in rules.Rows)
            {
                var source = text(r, "SourceRole"); var target = text(r, "TargetRole"); var fact = text(r, "FactType");
                if (string.IsNullOrWhiteSpace(text(r, "RuleId")) || !ruleIds.Add(text(r, "RuleId")) || !factIds.Contains(fact)
                    || !new[] { "Actor", "Target" }.Contains(source) || !new[] { "Actor", "Target" }.Contains(target) || source == target
                    || text(r, "RelationshipChannel") != "Affinity" || !new[] { "Positive", "Negative" }.Contains(text(r, "Polarity"))
                    || !new[] { "Weak", "Medium", "Strong" }.Contains(text(r, "Strength")) || !pairs.Add(fact + ":" + source + ":" + target))
                    error("Invalid/duplicate RelationshipInterpretationRule");
                else interpretable.Add(fact);
            }
            var calibration = new Dictionary<string, double>(StringComparer.Ordinal);
            if (values != null) foreach (var r in values.Rows)
            {
                var key = text(r, "CalibrationKey");
                if (string.IsNullOrWhiteSpace(key) || calibration.ContainsKey(key)
                    || !double.TryParse(text(r, "Value"), NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
                    || double.IsNaN(value) || double.IsInfinity(value) || value <= 0 || value > float.MaxValue)
                    error("Invalid/duplicate SocialCalibration");
                else calibration.Add(key, value);
            }
            foreach (var key in new[] { "AffinityDeltaWeak", "AffinityDeltaMedium", "AffinityDeltaStrong" })
                if (!calibration.ContainsKey(key)) error("Missing SocialCalibration " + key);
            if (calibration.TryGetValue("AffinityDeltaWeak", out var weak) && calibration.TryGetValue("AffinityDeltaMedium", out var medium)
                && calibration.TryGetValue("AffinityDeltaStrong", out var strong) && (weak > medium || medium > strong)) error("Invalid Social strength ordering");
            if (projection != null)
            {
                var activities = sheets.FirstOrDefault(s => s.Spec.SheetName == "Activity");
                var ids = new HashSet<string>(activities?.Rows.Select(r => text(r, "ActivityType")) ?? Enumerable.Empty<string>(), StringComparer.Ordinal);
                var projected = new HashSet<string>(StringComparer.Ordinal);
                foreach (var r in projection.Rows)
                    if (!ids.Contains(text(r, "ActivityType")) || !projected.Add(text(r, "ActivityType")) || !interpretable.Contains(text(r, "PersistentRelationalFactType")))
                        error("Invalid projection Activity/Fact/rule reference");
            }
        }
    }
}
