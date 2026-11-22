using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace RRPG.StaticConfig.EditorTools
{
    internal static class SocialMirrorReader
    {
        internal sealed class Payload
        {
            internal List<RelationalFactTypeConfigRow> FactTypes = new List<RelationalFactTypeConfigRow>();
            internal List<RelationshipInterpretationRuleConfigRow> Rules = new List<RelationshipInterpretationRuleConfigRow>();
            internal List<SocialCalibrationConfigRow> Calibration = new List<SocialCalibrationConfigRow>();
            internal int Count => FactTypes.Count + Rules.Count + Calibration.Count;
        }
        internal static Payload Read(string dir, AutonomousLifeMirrorReader.Payload autonomous)
        {
            var p = new Payload();
            var names = new[] { "RelationalFactType", "RelationshipInterpretationRule", "SocialCalibration" };
            if (names.All(n => !File.Exists(Path.Combine(dir, n + ".json"))) && autonomous.ActivitySocialProjection.Count == 0) return p;
            foreach (var n in names)
                if (!File.Exists(Path.Combine(dir, n + ".json"))) throw new InvalidDataException("Incomplete Social mirror: " + n);
            string cell(IReadOnlyDictionary<string, string> r, string f) => AutonomousLifeMirrorReader.Cell(r, f);
            foreach (var r in AutonomousLifeMirrorReader.ReadTable(dir, "RelationalFactType", new[] { "FactType", "DisplayName" }))
                p.FactTypes.Add(new RelationalFactTypeConfigRow { FactType = cell(r, "FactType"), DisplayName = cell(r, "DisplayName") });
            foreach (var r in AutonomousLifeMirrorReader.ReadTable(dir, "RelationshipInterpretationRule", new[] { "RuleId", "FactType", "SourceRole", "TargetRole", "RelationshipChannel", "Polarity", "Strength" }))
                p.Rules.Add(new RelationshipInterpretationRuleConfigRow { RuleId = cell(r, "RuleId"), FactType = cell(r, "FactType"), SourceRole = cell(r, "SourceRole"), TargetRole = cell(r, "TargetRole"), RelationshipChannel = cell(r, "RelationshipChannel"), Polarity = cell(r, "Polarity"), Strength = cell(r, "Strength") });
            foreach (var r in AutonomousLifeMirrorReader.ReadTable(dir, "SocialCalibration", new[] { "CalibrationKey", "Value" }))
                p.Calibration.Add(new SocialCalibrationConfigRow { CalibrationKey = cell(r, "CalibrationKey"), Value = float.Parse(cell(r, "Value"), CultureInfo.InvariantCulture) });
            SocialConfigDatabase.ValidateContent(p.FactTypes, p.Rules, p.Calibration);
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var mapping in autonomous.ActivitySocialProjection)
                if (string.IsNullOrWhiteSpace(mapping.ActivityType) || !ids.Add(mapping.ActivityType)
                    || !autonomous.Activity.Any(a => a.ActivityType == mapping.ActivityType) || !p.Rules.Any(r => r.FactType == mapping.PersistentRelationalFactType))
                    throw new InvalidDataException("Invalid ActivitySocialProjection reference/rule");
            return p;
        }
    }
}
