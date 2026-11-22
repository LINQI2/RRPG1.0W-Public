using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using RRPG.StaticConfig;

namespace RRPG.StaticConfig.EditorTools
{
    internal static class AutonomousLifeMirrorReader
    {
        internal sealed class Payload
        {
            internal List<BehaviorChannelConfigRow> BehaviorChannel = new List<BehaviorChannelConfigRow>();
            internal List<PersonBehaviorConfigRow> PersonBehavior = new List<PersonBehaviorConfigRow>();
            internal List<PersonPreferenceConfigRow> PersonPreference = new List<PersonPreferenceConfigRow>();
            internal List<PlaceConfigRow> Place = new List<PlaceConfigRow>();
            internal List<ActivityConfigRow> Activity = new List<ActivityConfigRow>();
            internal List<ActivityProviderConfigRow> ActivityProvider = new List<ActivityProviderConfigRow>();
            internal List<ActivityBehaviorBindingConfigRow> ActivityBehaviorBinding = new List<ActivityBehaviorBindingConfigRow>();
            internal List<ActivityPreferenceBindingConfigRow> ActivityPreferenceBinding = new List<ActivityPreferenceBindingConfigRow>();
            internal List<ActivityRelationshipBindingConfigRow> ActivityRelationshipBinding = new List<ActivityRelationshipBindingConfigRow>();
            internal List<ActivityContextBindingConfigRow> ActivityContextBinding = new List<ActivityContextBindingConfigRow>();
            internal List<ActivityRepetitionConfigRow> ActivityRepetition = new List<ActivityRepetitionConfigRow>();
            internal List<AutonomousLifeCalibrationConfigRow> AutonomousLifeCalibration = new List<AutonomousLifeCalibrationConfigRow>();
            internal List<ActivitySocialProjectionConfigRow> ActivitySocialProjection = new List<ActivitySocialProjectionConfigRow>();
            internal int Count => ActivitySocialProjection.Count + BehaviorChannel.Count + PersonBehavior.Count + PersonPreference.Count + Place.Count + Activity.Count + ActivityProvider.Count + ActivityBehaviorBinding.Count + ActivityPreferenceBinding.Count + ActivityRelationshipBinding.Count + ActivityContextBinding.Count + ActivityRepetition.Count + AutonomousLifeCalibration.Count;
        }

        internal static Payload Read(string dir)
        {
            var p = new Payload();
            var names = new[]
            {
                "BehaviorChannel",
                "PersonBehavior",
                "PersonPreference",
                "Place",
                "Activity",
                "ActivityProvider",
                "ActivityBehaviorBinding",
                "ActivityPreferenceBinding",
                "ActivityRelationshipBinding",
                "ActivityContextBinding",
                "ActivityRepetition",
                "AutonomousLifeCalibration",
                "ActivitySocialProjection"
            };
            if (names.All(n => !File.Exists(Path.Combine(dir, n + ".json"))))
                return p;
            foreach (var n in names)
                if (!File.Exists(Path.Combine(dir, n + ".json")))
                    throw new InvalidDataException("Incomplete AutonomousLife mirror: " + n);
            foreach (var r in ReadTable(dir, "BehaviorChannel", new[] { "ChannelId", "DisplayName", "Description", "NeutralValue", "Min", "Max" }))
                p.BehaviorChannel.Add(new BehaviorChannelConfigRow { ChannelId = Cell(r, "ChannelId"), DisplayName = Cell(r, "DisplayName"), Description = Cell(r, "Description"), NeutralValue = double.Parse(Cell(r, "NeutralValue"), CultureInfo.InvariantCulture), Min = double.Parse(Cell(r, "Min"), CultureInfo.InvariantCulture), Max = double.Parse(Cell(r, "Max"), CultureInfo.InvariantCulture) });
            foreach (var r in ReadTable(dir, "PersonBehavior", new[] { "PersonDefinitionId", "ChannelId", "Value" }))
                p.PersonBehavior.Add(new PersonBehaviorConfigRow { PersonDefinitionId = Cell(r, "PersonDefinitionId"), ChannelId = Cell(r, "ChannelId"), Value = double.Parse(Cell(r, "Value"), CultureInfo.InvariantCulture) });
            foreach (var r in ReadTable(dir, "PersonPreference", new[] { "PersonDefinitionId", "PreferenceKey", "Value" }))
                p.PersonPreference.Add(new PersonPreferenceConfigRow { PersonDefinitionId = Cell(r, "PersonDefinitionId"), PreferenceKey = Cell(r, "PreferenceKey"), Value = double.Parse(Cell(r, "Value"), CultureInfo.InvariantCulture) });
            foreach (var r in ReadTable(dir, "Place", new[] { "PlaceId", "DisplayName" }))
                p.Place.Add(new PlaceConfigRow { PlaceId = Cell(r, "PlaceId"), DisplayName = Cell(r, "DisplayName") });
            foreach (var r in ReadTable(dir, "Activity", new[] { "ActivityType", "DisplayName", "BaseDesire" }))
                p.Activity.Add(new ActivityConfigRow { ActivityType = Cell(r, "ActivityType"), DisplayName = Cell(r, "DisplayName"), BaseDesire = double.Parse(Cell(r, "BaseDesire"), CultureInfo.InvariantCulture) });
            foreach (var r in ReadTable(dir, "ActivityProvider", new[] { "ProviderId", "ActivityType", "PlaceId", "RequiresTarget", "RequiredContextKey", "ContextDomain" }))
                p.ActivityProvider.Add(new ActivityProviderConfigRow { ProviderId = Cell(r, "ProviderId"), ActivityType = Cell(r, "ActivityType"), PlaceId = Cell(r, "PlaceId"), RequiresTarget = bool.Parse(Cell(r, "RequiresTarget")), RequiredContextKey = Cell(r, "RequiredContextKey"), ContextDomain = Cell(r, "ContextDomain") });
            foreach (var r in ReadTable(dir, "ActivityBehaviorBinding", new[] { "BindingId", "ActivityType", "Key", "Strength", "Direction" }))
                p.ActivityBehaviorBinding.Add(new ActivityBehaviorBindingConfigRow { BindingId = Cell(r, "BindingId"), ActivityType = Cell(r, "ActivityType"), Key = Cell(r, "Key"), Strength = Cell(r, "Strength"), Direction = int.Parse(Cell(r, "Direction"), CultureInfo.InvariantCulture) });
            foreach (var r in ReadTable(dir, "ActivityPreferenceBinding", new[] { "BindingId", "ActivityType", "Key", "Strength", "Direction" }))
                p.ActivityPreferenceBinding.Add(new ActivityPreferenceBindingConfigRow { BindingId = Cell(r, "BindingId"), ActivityType = Cell(r, "ActivityType"), Key = Cell(r, "Key"), Strength = Cell(r, "Strength"), Direction = int.Parse(Cell(r, "Direction"), CultureInfo.InvariantCulture) });
            foreach (var r in ReadTable(dir, "ActivityRelationshipBinding", new[] { "BindingId", "ActivityType", "Key", "Strength", "Direction" }))
                p.ActivityRelationshipBinding.Add(new ActivityRelationshipBindingConfigRow { BindingId = Cell(r, "BindingId"), ActivityType = Cell(r, "ActivityType"), Key = Cell(r, "Key"), Strength = Cell(r, "Strength"), Direction = int.Parse(Cell(r, "Direction"), CultureInfo.InvariantCulture) });
            foreach (var r in ReadTable(dir, "ActivityContextBinding", new[] { "BindingId", "ActivityType", "Key", "Strength", "Direction", "SourceDomain", "SubjectRole" }))
                p.ActivityContextBinding.Add(new ActivityContextBindingConfigRow { BindingId = Cell(r, "BindingId"), ActivityType = Cell(r, "ActivityType"), Key = Cell(r, "Key"), Strength = Cell(r, "Strength"), Direction = int.Parse(Cell(r, "Direction"), CultureInfo.InvariantCulture), SourceDomain = Cell(r, "SourceDomain"), SubjectRole = Cell(r, "SubjectRole") });
            foreach (var r in ReadTable(dir, "ActivityRepetition", new[] { "BindingId", "ActivityType", "Key", "Strength", "Direction" }))
                p.ActivityRepetition.Add(new ActivityRepetitionConfigRow { BindingId = Cell(r, "BindingId"), ActivityType = Cell(r, "ActivityType"), Key = Cell(r, "Key"), Strength = Cell(r, "Strength"), Direction = int.Parse(Cell(r, "Direction"), CultureInfo.InvariantCulture) });
            foreach (var r in ReadTable(dir, "AutonomousLifeCalibration", new[] { "CalibrationKey", "Value" }))
                p.AutonomousLifeCalibration.Add(new AutonomousLifeCalibrationConfigRow { CalibrationKey = Cell(r, "CalibrationKey"), Value = double.Parse(Cell(r, "Value"), CultureInfo.InvariantCulture) });
            foreach (var r in ReadTable(dir, "ActivitySocialProjection", new[] { "ActivityType", "PersistentRelationalFactType" }))
                p.ActivitySocialProjection.Add(new ActivitySocialProjectionConfigRow { ActivityType = Cell(r, "ActivityType"), PersistentRelationalFactType = Cell(r, "PersistentRelationalFactType") });
            AutonomousLifeConfigDatabase.ValidatePrototypeInjuryCalibration(p.AutonomousLifeCalibration);
            return p;
        }

        internal static string Cell(IReadOnlyDictionary<string, string> r, string f) => MirrorJsonText.Unquote(r, f);
        internal static List<Dictionary<string, string>> ReadTable(string dir, string name, string[] fields)
        {
            var json = File.ReadAllText(Path.Combine(dir, name + ".json"));
            if (!MirrorJsonText.TryReadHeader(json, name, out var format, out var hash, out var failure) || !MirrorJsonText.TryReadSheet(json, name, name, out failure) || !MirrorJsonText.TryReadColumns(json, name, fields, out failure) || !MirrorJsonText.TryReadRows(json, name, "rowCount", "rows", out var count, out var rows, out failure))
                throw new InvalidDataException(failure);
            return rows;
        }
    }
}
