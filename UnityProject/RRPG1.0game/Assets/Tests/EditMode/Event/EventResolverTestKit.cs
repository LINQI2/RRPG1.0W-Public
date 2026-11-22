using System.Collections.Generic;
using RRPG.Event;
using RRPG.StaticConfig;

namespace RRPG.Event.Tests
{
    /// <summary>
    /// WP-Event-1B：Event Resolver 的静态配置夹具（**只存在于测试程序集**）。
    ///
    /// 为什么自建：Resolver 的输入刻意是「纯逻辑 + 只读契约」（Catalog / World View / History），
    /// 因此测试可以用最小 Fixture 精确表达「这一次为什么合法 / 为什么不合法」，
    /// 而不依赖任何产品运行时、Scene 或 Save。
    ///
    /// 约定：默认值都取「最宽松的合法值」，每个用例只覆盖它真正关心的那一条规则。
    /// </summary>
    internal static class EventResolverTestKit
    {
        public static EventDefinition Evt(string eventId, bool enabled = true, int weight = 100,
            string repeatPolicy = "Repeatable", string cooldownType = "None", int cooldownValue = 0,
            string displayName = null)
        {
            return new EventDefinition
            {
                EventId = eventId,
                DisplayName = displayName ?? eventId,
                Enabled = enabled,
                Weight = weight,
                RepeatPolicy = repeatPolicy,
                CooldownType = cooldownType,
                CooldownValue = cooldownValue
            };
        }

        public static EventTriggerDefinition Trigger(string triggerId, string eventId, string triggerType)
        {
            return new EventTriggerDefinition
            {
                TriggerId = triggerId,
                EventId = eventId,
                TriggerType = triggerType
            };
        }

        public static EventRoleDefinition Role(string roleId, string eventId, string selectorType,
            string sourceKey = null, string fixedPersonId = null, string roleName = null)
        {
            return new EventRoleDefinition
            {
                RoleId = roleId,
                EventId = eventId,
                RoleName = roleName ?? roleId,
                SelectorType = selectorType,
                SourceKey = sourceKey,
                FixedPersonId = fixedPersonId
            };
        }

        public static EventRoleConditionDefinition RoleCondition(string conditionId, string roleId,
            string conditionType)
        {
            return new EventRoleConditionDefinition
            {
                ConditionId = conditionId,
                RoleId = roleId,
                ConditionType = conditionType,
                HasIntValue = false,
                IntValue = 0
            };
        }

        public static EventConditionDefinition Condition(string conditionId, string eventId, int groupId,
            string conditionType, string subjectRoleId = null, string targetKey = null, int? intValue = null,
            string stringValue = null)
        {
            return new EventConditionDefinition
            {
                ConditionId = conditionId,
                EventId = eventId,
                GroupId = groupId,
                ConditionType = conditionType,
                SubjectRoleId = subjectRoleId,
                TargetKey = targetKey,
                HasIntValue = intValue.HasValue,
                IntValue = intValue.GetValueOrDefault(),
                StringValue = stringValue
            };
        }

        public static EventStaticCatalog Catalog(
            IReadOnlyList<EventDefinition> events = null,
            IReadOnlyList<EventTriggerDefinition> triggers = null,
            IReadOnlyList<EventRoleDefinition> roles = null,
            IReadOnlyList<EventRoleConditionDefinition> roleConditions = null,
            IReadOnlyList<EventConditionDefinition> conditions = null)
        {
            return new EventStaticCatalog(
                events ?? new List<EventDefinition>(),
                triggers ?? new List<EventTriggerDefinition>(),
                roles ?? new List<EventRoleDefinition>(),
                roleConditions ?? new List<EventRoleConditionDefinition>(),
                conditions ?? new List<EventConditionDefinition>());
        }

        /// <summary>便捷单事件 Catalog（1 个 Event + 1 个 Trigger，可带 Role / RoleCondition / Condition）。</summary>
        public static EventStaticCatalog SingleEventCatalog(EventDefinition eventRecord, string triggerType,
            string triggerId = "trg_test", IReadOnlyList<EventRoleDefinition> roles = null,
            IReadOnlyList<EventRoleConditionDefinition> roleConditions = null,
            IReadOnlyList<EventConditionDefinition> conditions = null)
        {
            return Catalog(
                new List<EventDefinition> { eventRecord },
                new List<EventTriggerDefinition> { Trigger(triggerId, eventRecord.EventId, triggerType) },
                roles,
                roleConditions,
                conditions);
        }
    }
}
