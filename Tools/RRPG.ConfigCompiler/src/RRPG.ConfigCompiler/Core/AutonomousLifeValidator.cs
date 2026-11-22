using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace RRPG.ConfigCompiler.Core
{
    /// <summary>AutonomousLife cross-column governance. Generic schema/ref/duplicate validation remains in the existing pipeline.</summary>
    public static class AutonomousLifeValidator
    {
        public static readonly IReadOnlyDictionary<string, string[]> Fields = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            {
                "ActivitySocialProjection",
                new[] { "ActivityType", "PersistentRelationalFactType" }
            },
            {
                "BehaviorChannel",
                new[]
                {
                    "ChannelId",
                    "DisplayName",
                    "Description",
                    "NeutralValue",
                    "Min",
                    "Max"
                }
            },
            {
                "PersonBehavior",
                new[]
                {
                    "PersonDefinitionId",
                    "ChannelId",
                    "Value"
                }
            },
            {
                "PersonPreference",
                new[]
                {
                    "PersonDefinitionId",
                    "PreferenceKey",
                    "Value"
                }
            },
            {
                "Place",
                new[]
                {
                    "PlaceId",
                    "DisplayName"
                }
            },
            {
                "Activity",
                new[]
                {
                    "ActivityType",
                    "DisplayName",
                    "BaseDesire"
                }
            },
            {
                "ActivityProvider",
                new[]
                {
                    "ProviderId",
                    "ActivityType",
                    "PlaceId",
                    "RequiresTarget",
                    "RequiredContextKey",
                    "ContextDomain"
                }
            },
            {
                "ActivityBehaviorBinding",
                new[]
                {
                    "BindingId",
                    "ActivityType",
                    "Key",
                    "Strength",
                    "Direction"
                }
            },
            {
                "ActivityPreferenceBinding",
                new[]
                {
                    "BindingId",
                    "ActivityType",
                    "Key",
                    "Strength",
                    "Direction"
                }
            },
            {
                "ActivityRelationshipBinding",
                new[]
                {
                    "BindingId",
                    "ActivityType",
                    "Key",
                    "Strength",
                    "Direction"
                }
            },
            {
                "ActivityContextBinding",
                new[]
                {
                    "BindingId",
                    "ActivityType",
                    "Key",
                    "Strength",
                    "Direction",
                    "SourceDomain",
                    "SubjectRole"
                }
            },
            {
                "ActivityRepetition",
                new[]
                {
                    "BindingId",
                    "ActivityType",
                    "Key",
                    "Strength",
                    "Direction"
                }
            },
            {
                "AutonomousLifeCalibration",
                new[]
                {
                    "CalibrationKey",
                    "Value"
                }
            }
        };
        public static void Validate(IReadOnlyList<CompiledSheet> sheets, DiagnosticBag bag)
        {
            var domain = sheets.Where(s => Fields.ContainsKey(s.Spec.SheetName)).ToList();
            if (domain.Count == 0)
                return;
            var anchor = domain[0];
            Action<string> error = m => bag.Error("DAT_AL_INVALID", m, DiagContext.Of(anchor.Spec.Workbook, anchor.Spec.SheetName, 0));
            foreach (var entry in Fields)
            {
                var table = domain.FirstOrDefault(s => s.Spec.SheetName == entry.Key);
                if (table == null)
                {
                    error("AutonomousLife missing table " + entry.Key);
                    continue;
                }

                foreach (var field in entry.Value)
                    if (!table.Spec.Fields.Any(f => f.Field == field))
                        error(entry.Key + " missing field " + field);
                foreach (var row in table.Rows)
                {
                    string text(string key) => Convert.ToString(row.Get(key)?.Value, CultureInfo.InvariantCulture);
                    double number(string key) => double.TryParse(text(key), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : double.NaN;
                    void bad(string m) => bag.Error("DAT_AL_INVALID", m, DiagContext.Of(row.Sheet.Workbook, row.Sheet.SheetName, row.ExcelRow));
                    if (entry.Key == "BehaviorChannel" && (number("Min") != 0 || number("Max") != 1 || number("NeutralValue") != .5 || !new[]
                    {
                        "social.sociability",
                        "social.supportiveness"
                    }.Contains(text("ChannelId"))))
                        bad("Invalid governed V0.1 channel/domain");
                    if (entry.Key == "PersonBehavior" && (number("Value") < 0 || number("Value") > 1 || double.IsNaN(number("Value"))))
                        bad("Behavior range");
                    if (entry.Key == "PersonPreference" && (text("PreferenceKey") != "leisure.drink" || number("Value") < -1 || number("Value") > 1 || double.IsNaN(number("Value"))))
                        bad("Preference key/range");
                    if (entry.Key.StartsWith("Activity") && entry.Key != "Activity" && entry.Key != "ActivityProvider" && entry.Key != "ActivitySocialProjection")
                    {
                        if (!new[]
                        {
                            "Weak",
                            "Medium",
                            "Strong"
                        }.Contains(text("Strength")))
                            bad("Invalid strength");
                        if (number("Direction") != 1 && number("Direction") != -1)
                            bad("Direction must be +1/-1");
                        if (entry.Key == "ActivityRepetition" && (number("Direction") != -1 || text("Key") != "LastInitiatedType"))
                            bad("Invalid minimal repetition");
                        if (entry.Key == "ActivityPreferenceBinding" && text("Key") != "leisure.drink")
                            bad("Ungoverned preference");
                        if (entry.Key == "ActivityRelationshipBinding" && text("Key") != "Affinity")
                            bad("Unknown relationship channel");
                        if (entry.Key == "ActivityContextBinding" && !((text("Key") == "Target.InjurySeverity" && text("SourceDomain") == "Person" && text("SubjectRole") == "Target") || (text("Key") == "Target.HelpNeedSeverity" && text("SourceDomain") == "TestContext" && text("SubjectRole") == "Target")))
                            bad("Ungoverned context semantics");
                    }

                    if (entry.Key == "ActivityProvider")
                    {
                        var key = text("RequiredContextKey");
                        var d = text("ContextDomain");
                        if (key != "" && text("RequiresTarget").ToLowerInvariant() != "true")
                            bad("Context provider needs Target");
                        if (!((key == "" && d == "") || (key == "Target.InjurySeverity" && d == "Person") || (key == "Target.HelpNeedSeverity" && d == "TestContext")))
                            bad("Invalid provider context");
                    }
                }
            }

            var calibration = domain.FirstOrDefault(t => t.Spec.SheetName == "AutonomousLifeCalibration");
            if (calibration != null)
            {
                var values = new Dictionary<string, double>(StringComparer.Ordinal);
                foreach (var row in calibration.Rows)
                    if (double.TryParse(Convert.ToString(row.Get("Value")?.Value, CultureInfo.InvariantCulture), NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
                        values[Convert.ToString(row.Get("CalibrationKey")?.Value, CultureInfo.InvariantCulture)] = v;
                foreach (var key in new[]
                {
                    "BehaviorBudget",
                    "PreferenceBudget",
                    "RelationshipBudget",
                    "ContextBudget",
                    "RepetitionBudget",
                    "Weak",
                    "Medium",
                    "Strong",
                    "Threshold",
                    "WeightSpan",
                    "WeightGain",
                    "WeightPower",
                    "NoMajorWeight",
                    "RelationshipReferenceMagnitude"
                }

                )
                    if (!values.ContainsKey(key) || double.IsNaN(values[key]) || double.IsInfinity(values[key]) || (key != "Threshold" && values[key] <= 0))
                        error("Invalid/missing calibration " + key);
                // Prototype Person adapter magnitudes follow the existing Context 0..1 contract.
                foreach (var key in new[] { "InjuryLight", "InjuryHeavy", "InjuryDisabled" })
                    if (!values.TryGetValue(key, out var value) || double.IsNaN(value) || double.IsInfinity(value) || value < 0 || value > 1)
                        error("Invalid/missing prototype injury calibration " + key);
                if (values.ContainsKey("Weak") && values.ContainsKey("Medium") && values.ContainsKey("Strong") && (values["Weak"] > values["Medium"] || values["Medium"] > values["Strong"] || values["Strong"] > 1))
                    error("Invalid strength ordering");
            }
        }
    }
}
