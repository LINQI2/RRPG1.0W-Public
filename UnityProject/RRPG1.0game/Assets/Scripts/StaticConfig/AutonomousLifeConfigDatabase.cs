using System;
using System.Collections.Generic;
using UnityEngine;

namespace RRPG.StaticConfig
{
    [Serializable]
    public sealed class BehaviorChannelConfigRow
    {
        public string ChannelId;
        public string DisplayName;
        public string Description;
        public double NeutralValue;
        public double Min;
        public double Max;
    }

    [Serializable]
    public sealed class PersonBehaviorConfigRow
    {
        public string PersonDefinitionId;
        public string ChannelId;
        public double Value;
    }

    [Serializable]
    public sealed class PersonPreferenceConfigRow
    {
        public string PersonDefinitionId;
        public string PreferenceKey;
        public double Value;
    }

    [Serializable]
    public sealed class PlaceConfigRow
    {
        public string PlaceId;
        public string DisplayName;
    }

    [Serializable]
    public sealed class ActivityConfigRow
    {
        public string ActivityType;
        public string DisplayName;
        public double BaseDesire;
    }

    [Serializable]
    public sealed class ActivityProviderConfigRow
    {
        public string ProviderId;
        public string ActivityType;
        public string PlaceId;
        public bool RequiresTarget;
        public string RequiredContextKey;
        public string ContextDomain;
    }

    [Serializable]
    public sealed class ActivityBehaviorBindingConfigRow
    {
        public string BindingId;
        public string ActivityType;
        public string Key;
        public string Strength;
        public int Direction;
    }

    [Serializable]
    public sealed class ActivityPreferenceBindingConfigRow
    {
        public string BindingId;
        public string ActivityType;
        public string Key;
        public string Strength;
        public int Direction;
    }

    [Serializable]
    public sealed class ActivityRelationshipBindingConfigRow
    {
        public string BindingId;
        public string ActivityType;
        public string Key;
        public string Strength;
        public int Direction;
    }

    [Serializable]
    public sealed class ActivityContextBindingConfigRow
    {
        public string BindingId;
        public string ActivityType;
        public string Key;
        public string Strength;
        public int Direction;
        public string SourceDomain;
        public string SubjectRole;
    }

    [Serializable]
    public sealed class ActivityRepetitionConfigRow
    {
        public string BindingId;
        public string ActivityType;
        public string Key;
        public string Strength;
        public int Direction;
    }

    [Serializable]
    public sealed class AutonomousLifeCalibrationConfigRow
    {
        public string CalibrationKey;
        public double Value;
    }

    [Serializable]
    public sealed class ActivitySocialProjectionConfigRow
    {
        public string ActivityType;
        public string PersistentRelationalFactType;
    }

    /// <summary>Generated domain database; authoring ownership stays in XLSX/Compiler.</summary>
    public sealed class AutonomousLifeConfigDatabase : ScriptableObject
    {
        [SerializeField] private List<ActivitySocialProjectionConfigRow> activitySocialProjection = new List<ActivitySocialProjectionConfigRow>();
        public IReadOnlyList<ActivitySocialProjectionConfigRow> ActivitySocialProjection => activitySocialProjection.AsReadOnly();
        [SerializeField]
        private List<BehaviorChannelConfigRow> behaviorchannel = new List<BehaviorChannelConfigRow>();
        public IReadOnlyList<BehaviorChannelConfigRow> BehaviorChannel => behaviorchannel.AsReadOnly();

        [SerializeField]
        private List<PersonBehaviorConfigRow> personbehavior = new List<PersonBehaviorConfigRow>();
        public IReadOnlyList<PersonBehaviorConfigRow> PersonBehavior => personbehavior.AsReadOnly();

        [SerializeField]
        private List<PersonPreferenceConfigRow> personpreference = new List<PersonPreferenceConfigRow>();
        public IReadOnlyList<PersonPreferenceConfigRow> PersonPreference => personpreference.AsReadOnly();

        [SerializeField]
        private List<PlaceConfigRow> place = new List<PlaceConfigRow>();
        public IReadOnlyList<PlaceConfigRow> Place => place.AsReadOnly();

        [SerializeField]
        private List<ActivityConfigRow> activity = new List<ActivityConfigRow>();
        public IReadOnlyList<ActivityConfigRow> Activity => activity.AsReadOnly();

        [SerializeField]
        private List<ActivityProviderConfigRow> activityprovider = new List<ActivityProviderConfigRow>();
        public IReadOnlyList<ActivityProviderConfigRow> ActivityProvider => activityprovider.AsReadOnly();

        [SerializeField]
        private List<ActivityBehaviorBindingConfigRow> activitybehaviorbinding = new List<ActivityBehaviorBindingConfigRow>();
        public IReadOnlyList<ActivityBehaviorBindingConfigRow> ActivityBehaviorBinding => activitybehaviorbinding.AsReadOnly();

        [SerializeField]
        private List<ActivityPreferenceBindingConfigRow> activitypreferencebinding = new List<ActivityPreferenceBindingConfigRow>();
        public IReadOnlyList<ActivityPreferenceBindingConfigRow> ActivityPreferenceBinding => activitypreferencebinding.AsReadOnly();

        [SerializeField]
        private List<ActivityRelationshipBindingConfigRow> activityrelationshipbinding = new List<ActivityRelationshipBindingConfigRow>();
        public IReadOnlyList<ActivityRelationshipBindingConfigRow> ActivityRelationshipBinding => activityrelationshipbinding.AsReadOnly();

        [SerializeField]
        private List<ActivityContextBindingConfigRow> activitycontextbinding = new List<ActivityContextBindingConfigRow>();
        public IReadOnlyList<ActivityContextBindingConfigRow> ActivityContextBinding => activitycontextbinding.AsReadOnly();

        [SerializeField]
        private List<ActivityRepetitionConfigRow> activityrepetition = new List<ActivityRepetitionConfigRow>();
        public IReadOnlyList<ActivityRepetitionConfigRow> ActivityRepetition => activityrepetition.AsReadOnly();

        [SerializeField]
        private List<AutonomousLifeCalibrationConfigRow> autonomouslifecalibration = new List<AutonomousLifeCalibrationConfigRow>();
        public IReadOnlyList<AutonomousLifeCalibrationConfigRow> AutonomousLifeCalibration => autonomouslifecalibration.AsReadOnly();
        public int TotalCount => activitySocialProjection.Count + behaviorchannel.Count + personbehavior.Count + personpreference.Count + place.Count + activity.Count + activityprovider.Count + activitybehaviorbinding.Count + activitypreferencebinding.Count + activityrelationshipbinding.Count + activitycontextbinding.Count + activityrepetition.Count + autonomouslifecalibration.Count;

        /// <summary>Prototype Adapter input integrity only; values remain authoring calibration.</summary>
        public static void ValidatePrototypeInjuryCalibration(IEnumerable<AutonomousLifeCalibrationConfigRow> rows)
        {
            if (rows == null)
                throw new ArgumentNullException(nameof(rows));
            var values = new Dictionary<string, double>(StringComparer.Ordinal);
            foreach (var row in rows)
            {
                if (row == null)
                    throw new ArgumentException("Null calibration row");
                if (row.CalibrationKey != "InjuryLight" && row.CalibrationKey != "InjuryHeavy" && row.CalibrationKey != "InjuryDisabled")
                    continue;
                if (values.ContainsKey(row.CalibrationKey))
                    throw new ArgumentException("Duplicate calibration " + row.CalibrationKey);
                values.Add(row.CalibrationKey, row.Value);
            }

            foreach (var key in new[] { "InjuryLight", "InjuryHeavy", "InjuryDisabled" })
                if (!values.TryGetValue(key, out var value) || double.IsNaN(value) || double.IsInfinity(value) || value < 0 || value > 1)
                    throw new ArgumentException("Invalid/missing prototype injury calibration " + key);
        }

        public void ApplyGeneratedContent(List<BehaviorChannelConfigRow> behaviorchannelRows, List<PersonBehaviorConfigRow> personbehaviorRows, List<PersonPreferenceConfigRow> personpreferenceRows, List<PlaceConfigRow> placeRows, List<ActivityConfigRow> activityRows, List<ActivityProviderConfigRow> activityproviderRows, List<ActivityBehaviorBindingConfigRow> activitybehaviorbindingRows, List<ActivityPreferenceBindingConfigRow> activitypreferencebindingRows, List<ActivityRelationshipBindingConfigRow> activityrelationshipbindingRows, List<ActivityContextBindingConfigRow> activitycontextbindingRows, List<ActivityRepetitionConfigRow> activityrepetitionRows, List<AutonomousLifeCalibrationConfigRow> autonomouslifecalibrationRows, List<ActivitySocialProjectionConfigRow> projectionRows = null)
        {
            activitySocialProjection = new List<ActivitySocialProjectionConfigRow>(projectionRows ?? new List<ActivitySocialProjectionConfigRow>());
            behaviorchannel = new List<BehaviorChannelConfigRow>(behaviorchannelRows);
            personbehavior = new List<PersonBehaviorConfigRow>(personbehaviorRows);
            personpreference = new List<PersonPreferenceConfigRow>(personpreferenceRows);
            place = new List<PlaceConfigRow>(placeRows);
            activity = new List<ActivityConfigRow>(activityRows);
            activityprovider = new List<ActivityProviderConfigRow>(activityproviderRows);
            activitybehaviorbinding = new List<ActivityBehaviorBindingConfigRow>(activitybehaviorbindingRows);
            activitypreferencebinding = new List<ActivityPreferenceBindingConfigRow>(activitypreferencebindingRows);
            activityrelationshipbinding = new List<ActivityRelationshipBindingConfigRow>(activityrelationshipbindingRows);
            activitycontextbinding = new List<ActivityContextBindingConfigRow>(activitycontextbindingRows);
            activityrepetition = new List<ActivityRepetitionConfigRow>(activityrepetitionRows);
            autonomouslifecalibration = new List<AutonomousLifeCalibrationConfigRow>(autonomouslifecalibrationRows);
        }
    }
}
