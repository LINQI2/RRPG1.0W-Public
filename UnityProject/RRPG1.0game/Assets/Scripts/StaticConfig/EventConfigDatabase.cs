using System.Collections.Generic;
using UnityEngine;

namespace RRPG.StaticConfig
{
    /// <summary>
    /// 生成容器：Event 领域（统一事件内容基础）的只读静态配置库（WP-Event-1A）。
    ///
    /// 承载由 Canonical Mirror 经 Unity Builder 生成的 10 张表的记录
    /// （Event / EventTrigger / EventRole / EventRoleCondition / EventCondition / EventPresentation /
    ///   EventOption / EventOptionCondition / EventOutcome / EventEffect）—— **生成物**，不是人工 Authoring 对象。
    ///
    /// 为什么 10 张表放进同一个库：
    ///   - 它们的唯一消费者是同一件事（事件内容读取），且跨表引用密集（Trigger→Event、Role→Event、
    ///     Condition→Event/Role、Option→Event、Outcome→Option、Effect→Outcome）；放同一个库里，
    ///     Runtime 不会读到「一半新一半旧」的跨库不一致；
    ///   - 领域身份仍与工作簿一致：`RRPG_Events.xlsx` 的 10 张表。
    ///
    /// 语义（与 MonsterConfigDatabase / EncounterConfigDatabase 同一套约定）：
    ///   - 每个列表的顺序 = Mirror 的 Canonical 顺序（按身份排序的产物顺序，可复现）；
    ///   - 本类**只提供只读访问**：不求解事件、不绑定人物、不判断条件、不执行效果、不渲染模板。
    ///
    /// 注意：Unity 要求 ScriptableObject 与其资产脚本**同名文件**（本文件即该约定）。
    /// </summary>
    public sealed class EventConfigDatabase : ScriptableObject
    {
        [SerializeField] private string formatVersion = RRPGStaticConfigRoot.SupportedFormatVersion;
        [SerializeField] private string sourceMirrorSha256 = string.Empty;

        [SerializeField] private List<EventDefinition> events = new List<EventDefinition>();
        [SerializeField] private List<EventTriggerDefinition> triggers = new List<EventTriggerDefinition>();
        [SerializeField] private List<EventRoleDefinition> roles = new List<EventRoleDefinition>();
        [SerializeField] private List<EventRoleConditionDefinition> roleConditions =
            new List<EventRoleConditionDefinition>();
        [SerializeField] private List<EventConditionDefinition> conditions = new List<EventConditionDefinition>();
        [SerializeField] private List<EventPresentationDefinition> presentations =
            new List<EventPresentationDefinition>();
        [SerializeField] private List<EventOptionDefinition> options = new List<EventOptionDefinition>();
        [SerializeField] private List<EventOptionConditionDefinition> optionConditions =
            new List<EventOptionConditionDefinition>();
        [SerializeField] private List<EventOutcomeDefinition> outcomes = new List<EventOutcomeDefinition>();
        [SerializeField] private List<EventEffectDefinition> effects = new List<EventEffectDefinition>();

        public string FormatVersion { get { return formatVersion; } }

        /// <summary>来源 Mirror 的语义指纹（Event.json 的 <c>_generated.sourceSha256</c>）。</summary>
        public string SourceMirrorSha256 { get { return sourceMirrorSha256; } }

        public int EventCount { get { return events == null ? 0 : events.Count; } }
        public int TriggerCount { get { return triggers == null ? 0 : triggers.Count; } }
        public int RoleCount { get { return roles == null ? 0 : roles.Count; } }
        public int RoleConditionCount { get { return roleConditions == null ? 0 : roleConditions.Count; } }
        public int ConditionCount { get { return conditions == null ? 0 : conditions.Count; } }
        public int PresentationCount { get { return presentations == null ? 0 : presentations.Count; } }
        public int OptionCount { get { return options == null ? 0 : options.Count; } }
        public int OptionConditionCount { get { return optionConditions == null ? 0 : optionConditions.Count; } }
        public int OutcomeCount { get { return outcomes == null ? 0 : outcomes.Count; } }
        public int EffectCount { get { return effects == null ? 0 : effects.Count; } }

        /// <summary>全部记录条数（用于 no-op 判定与诊断，不参与生成）。</summary>
        public int TotalCount
        {
            get
            {
                return EventCount + TriggerCount + RoleCount + RoleConditionCount + ConditionCount
                    + PresentationCount + OptionCount + OptionConditionCount + OutcomeCount + EffectCount;
            }
        }

        /// <summary>稳定顺序的全部事件（顺序 = Mirror 的 Canonical 顺序）。</summary>
        public IReadOnlyList<EventDefinition> Events { get { return List(events); } }

        public IReadOnlyList<EventTriggerDefinition> Triggers { get { return List(triggers); } }

        public IReadOnlyList<EventRoleDefinition> Roles { get { return List(roles); } }

        public IReadOnlyList<EventRoleConditionDefinition> RoleConditions { get { return List(roleConditions); } }

        public IReadOnlyList<EventConditionDefinition> Conditions { get { return List(conditions); } }

        public IReadOnlyList<EventPresentationDefinition> Presentations { get { return List(presentations); } }

        public IReadOnlyList<EventOptionDefinition> Options { get { return List(options); } }

        public IReadOnlyList<EventOptionConditionDefinition> OptionConditions { get { return List(optionConditions); } }

        public IReadOnlyList<EventOutcomeDefinition> Outcomes { get { return List(outcomes); } }

        public IReadOnlyList<EventEffectDefinition> Effects { get { return List(effects); } }

        /// <summary>
        /// 按 EventId 命中事件定义（未命中 → false；不抛、不猜、不返回别的记录）。
        /// 这是**身份查找**，不是事件求解：不判断条件、不绑定人物、不执行效果。
        /// </summary>
        public bool TryGetEvent(string eventId, out EventDefinition definition)
        {
            definition = null;
            if (string.IsNullOrEmpty(eventId) || events == null) return false;

            for (int i = 0; i < events.Count; i++)
            {
                var candidate = events[i];
                if (candidate != null && string.Equals(candidate.EventId, eventId, System.StringComparison.Ordinal))
                {
                    definition = candidate;
                    return true;
                }
            }
            return false;
        }

        /// <summary>Builder 唯一写入口（Editor-only 调用；Runtime 只读）。</summary>
        public void ApplyGeneratedContent(
            string format,
            string eventMirrorSha256,
            List<EventDefinition> eventEntries,
            List<EventTriggerDefinition> triggerEntries,
            List<EventRoleDefinition> roleEntries,
            List<EventRoleConditionDefinition> roleConditionEntries,
            List<EventConditionDefinition> conditionEntries,
            List<EventPresentationDefinition> presentationEntries,
            List<EventOptionDefinition> optionEntries,
            List<EventOptionConditionDefinition> optionConditionEntries,
            List<EventOutcomeDefinition> outcomeEntries,
            List<EventEffectDefinition> effectEntries)
        {
            formatVersion = format;
            sourceMirrorSha256 = eventMirrorSha256 ?? string.Empty;

            events = eventEntries ?? new List<EventDefinition>();
            triggers = triggerEntries ?? new List<EventTriggerDefinition>();
            roles = roleEntries ?? new List<EventRoleDefinition>();
            roleConditions = roleConditionEntries ?? new List<EventRoleConditionDefinition>();
            conditions = conditionEntries ?? new List<EventConditionDefinition>();
            presentations = presentationEntries ?? new List<EventPresentationDefinition>();
            options = optionEntries ?? new List<EventOptionDefinition>();
            optionConditions = optionConditionEntries ?? new List<EventOptionConditionDefinition>();
            outcomes = outcomeEntries ?? new List<EventOutcomeDefinition>();
            effects = effectEntries ?? new List<EventEffectDefinition>();
        }

        private static IReadOnlyList<T> List<T>(List<T> source)
        {
            return source ?? (IReadOnlyList<T>)new List<T>();
        }
    }
}
