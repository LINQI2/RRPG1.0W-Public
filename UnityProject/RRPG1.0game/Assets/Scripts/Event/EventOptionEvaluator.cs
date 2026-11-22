using System;
using RRPG.StaticConfig;

namespace RRPG.Event
{
    /// <summary>
    /// **Option 可用性求值**（WP-Event-1C §3 / §4 / §5）。
    ///
    /// 关键语义（与 1B 的分工）：
    ///   - Event Instance 冻结的是「**为什么触发**」+「**出现的是谁**」（Context 快照 + Role 绑定）；
    ///   - 玩家「**现在**能不能选择这个选项」用**点击此刻**的 World View 重新判断
    ///     —— **绝不**使用事件创建时的世界资源快照；
    ///   - 条件解释复用**唯一解释器** <see cref="EventConditionGroupEvaluator"/>
    ///     （与事件成立条件同一套 V0.1 语义：Group 内 AND、Group 间 OR、无条件 = 可用）；
    ///   - 本类**纯查询**：绝不 mutation、绝不产生 Outcome（§7）。UI 只消费它的结果，
    ///     不得自己重新实现条件判断。
    /// </summary>
    public static class EventOptionEvaluator
    {
        public static EventOptionAvailabilityResult Evaluate(ResolvedEventInstance instance, string optionId,
            EventStaticCatalog catalog, IEventWorldView world, EventWorldState worldState)
        {
            string eventId = instance != null ? instance.EventId : null;

            if (instance == null)
            {
                return EventOptionAvailabilityResult.Create(null, null, optionId, EventOptionAvailability.Invalid,
                    "事件实例为 null：Option 可用性必须有已解析实例作为主体。", null);
            }

            if (catalog == null)
            {
                return Invalid(instance, optionId, "静态事件配置不可用（未提供 EventStaticCatalog）。");
            }

            if (world == null || worldState == null)
            {
                return Invalid(instance, optionId,
                    "World View / Event World State 不得为 null（选项条件必须基于当前世界事实判断）。");
            }

            if (string.IsNullOrEmpty(optionId))
            {
                return Invalid(instance, optionId, "OptionId 为空。");
            }

            EventOptionDefinition option;
            if (!catalog.TryGetOption(optionId, out option))
            {
                return Invalid(instance, optionId, "选项不存在：" + optionId + "。");
            }

            if (!string.Equals(option.EventId, instance.EventId, StringComparison.Ordinal))
            {
                return Invalid(instance, optionId, "选项 " + optionId + " 不属于事件 " + eventId
                    + "（实际属于 " + option.EventId + "）。");
            }

            var views = EventConditionView.FromOptionConditions(catalog.GetOptionConditions(optionId));
            if (views.Count == 0)
            {
                return EventOptionAvailabilityResult.Create(instance.EventInstanceId, eventId, optionId,
                    EventOptionAvailability.Available, string.Empty,
                    "无 OptionCondition → 默认可用（V0.1 冻结）。");
            }

            var evaluation = EventConditionEvaluation.Of(instance.ContextSnapshot, world, worldState,
                instance.RoleBindings);

            string detail;
            var outcome = EventConditionGroupEvaluator.Evaluate(views, evaluation, out detail);

            if (outcome == EventConditionOutcome.InvalidConfig)
            {
                return EventOptionAvailabilityResult.Create(instance.EventInstanceId, eventId, optionId,
                    EventOptionAvailability.Invalid,
                    "选项 " + optionId + " 的条件无法求值（配置非法）：" + detail, detail);
            }

            if (outcome == EventConditionOutcome.Pass)
            {
                return EventOptionAvailabilityResult.Create(instance.EventInstanceId, eventId, optionId,
                    EventOptionAvailability.Available, string.Empty, detail);
            }

            // 条件不成立 → 由 UnavailableMode 决定「隐藏」还是「显示但不可选」
            if (string.Equals(option.UnavailableMode, "Hide", StringComparison.Ordinal))
            {
                return EventOptionAvailabilityResult.Create(instance.EventInstanceId, eventId, optionId,
                    EventOptionAvailability.Hidden,
                    "选项条件不成立 → Hide（UnavailableMode=Hide）：" + detail, detail);
            }

            if (string.Equals(option.UnavailableMode, "Disable", StringComparison.Ordinal))
            {
                return EventOptionAvailabilityResult.Create(instance.EventInstanceId, eventId, optionId,
                    EventOptionAvailability.Disabled,
                    "选项条件不成立 → Disabled（UnavailableMode=Disable）：" + detail, detail);
            }

            return EventOptionAvailabilityResult.Create(instance.EventInstanceId, eventId, optionId,
                EventOptionAvailability.Invalid,
                "UnavailableMode 未知：'" + option.UnavailableMode + "'（允许：Hide / Disable）。", detail);
        }

        private static EventOptionAvailabilityResult Invalid(ResolvedEventInstance instance, string optionId,
            string diagnostic)
        {
            return EventOptionAvailabilityResult.Create(
                instance != null ? instance.EventInstanceId : null,
                instance != null ? instance.EventId : null,
                optionId, EventOptionAvailability.Invalid, diagnostic, null);
        }
    }
}
