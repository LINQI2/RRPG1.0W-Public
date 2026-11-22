using System.Collections.Generic;
using RRPG.Event;
using RRPG.StaticConfig;

namespace RRPG.Event.Tests
{
    /// <summary>
    /// WP-Event-1C：Option / Outcome / Effect 的静态配置夹具（测试程序集专用）。
    ///
    /// 定位：1C 的执行用例需要「1B 的解析结果 + 1C 的选项/结果/效果」在**同一个 Catalog** 上，
    /// 因此这里构造 9 张表的完整 Catalog，并复用 1B 的 `EventResolverTestKit` / `EventResolverTestWorld`。
    /// </summary>
    internal static class EventExecutionTestKit
    {
        public const string VillageTrigger = "VillageRecognitionRaised";
        public const string ExpeditionTrigger = "ExpeditionEventNodeReached";

        public static EventOptionDefinition Option(string optionId, string eventId, int sortOrder,
            string unavailableMode = "Hide", string label = null, string subLabel = null)
        {
            return new EventOptionDefinition
            {
                OptionId = optionId,
                EventId = eventId,
                SortOrder = sortOrder,
                LabelTemplate = label ?? optionId,
                SubLabelTemplate = subLabel,
                UnavailableMode = unavailableMode
            };
        }

        public static EventOptionConditionDefinition OptionCondition(string conditionId, string optionId, int groupId,
            string conditionType, string subjectRoleId = null, string targetKey = null, int? intValue = null,
            string stringValue = null)
        {
            return new EventOptionConditionDefinition
            {
                ConditionId = conditionId,
                OptionId = optionId,
                GroupId = groupId,
                ConditionType = conditionType,
                SubjectRoleId = subjectRoleId,
                TargetKey = targetKey,
                HasIntValue = intValue.HasValue,
                IntValue = intValue.GetValueOrDefault(),
                StringValue = stringValue
            };
        }

        public static EventOutcomeDefinition Outcome(string outcomeId, string optionId, int weight = 100)
        {
            return new EventOutcomeDefinition
            {
                OutcomeId = outcomeId,
                OptionId = optionId,
                Weight = weight,
                ResultTextTemplate = null
            };
        }

        public static EventEffectDefinition Effect(string effectId, string outcomeId, int order, string effectType,
            string targetRoleId = null, string targetKey = null, int? intValue = null, string stringValue = null)
        {
            return new EventEffectDefinition
            {
                EffectId = effectId,
                OutcomeId = outcomeId,
                Order = order,
                EffectType = effectType,
                TargetRoleId = targetRoleId,
                TargetKey = targetKey,
                HasIntValue = intValue.HasValue,
                IntValue = intValue.GetValueOrDefault(),
                StringValue = stringValue
            };
        }

        public static EventStaticCatalog Catalog(
            IReadOnlyList<EventDefinition> events,
            IReadOnlyList<EventTriggerDefinition> triggers = null,
            IReadOnlyList<EventRoleDefinition> roles = null,
            IReadOnlyList<EventRoleConditionDefinition> roleConditions = null,
            IReadOnlyList<EventConditionDefinition> conditions = null,
            IReadOnlyList<EventOptionDefinition> options = null,
            IReadOnlyList<EventOptionConditionDefinition> optionConditions = null,
            IReadOnlyList<EventOutcomeDefinition> outcomes = null,
            IReadOnlyList<EventEffectDefinition> effects = null)
        {
            return new EventStaticCatalog(events, triggers, roles, roleConditions, conditions, options,
                optionConditions, outcomes, effects);
        }

        /// <summary>经 1B Resolver 得到一个真实冻结的 <see cref="ResolvedEventInstance"/>（1C 的唯一合法输入来源）。</summary>
        public static ResolvedEventInstance ResolveInstance(EventStaticCatalog catalog,
            string triggerType = VillageTrigger, string triggerKey = "key_exec", int seed = 1,
            EventContext context = null, IEventWorldView world = null, EventWorldState worldState = null,
            ExpeditionEventSessionState session = null)
        {
            var result = EventResolver.Resolve(
                new EventResolutionRequest(triggerType, triggerKey, seed, context),
                catalog,
                world ?? EventResolverTestWorld.StandardWorld(),
                worldState ?? new EventWorldState(),
                session ?? new ExpeditionEventSessionState());

            return result.Success ? result.Instance : null;
        }
    }
}
