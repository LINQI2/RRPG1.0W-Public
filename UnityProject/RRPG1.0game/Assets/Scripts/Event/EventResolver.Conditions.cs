using System;
using System.Collections.Generic;
using RRPG.StaticConfig;

namespace RRPG.Event
{
    /// <summary>
    /// EventResolver：**事件成立条件**求值（WP-Event-1B §13 / §14）。
    ///
    /// 1C 起本方法只做两件事：把配置投影成 <see cref="EventConditionView"/>，然后交给**唯一解释器**
    /// <see cref="EventConditionGroupEvaluator"/>（与「选项可用条件」共用同一套语义，避免第二套实现）。
    ///
    /// 条件组语义：同 GroupId 内 AND；不同 GroupId 之间 OR；无条件 = true。
    /// 未知 ConditionType / 缺参数 / 悬空 SubjectRoleId → InvalidConfig（绝不默认 true / false 后继续）。
    /// </summary>
    public static partial class EventResolver
    {
        private static EventConditionOutcome EvaluateEventConditions(ResolveContext ctx, EventDefinition eventRecord,
            IReadOnlyList<EventRoleBinding> bindings, out string reason)
        {
            reason = string.Empty;

            var views = EventConditionView.FromEventConditions(ctx.Catalog.GetConditions(eventRecord.EventId));
            if (views.Count == 0) return EventConditionOutcome.Pass;   // §14：无条件 = true

            var evaluation = EventConditionEvaluation.Of(ctx.Request.ContextOrEmpty, ctx.World, ctx.WorldState,
                bindings);

            string detail;
            var outcome = EventConditionGroupEvaluator.Evaluate(views, evaluation, out detail);

            if (outcome == EventConditionOutcome.InvalidConfig)
            {
                reason = "事件 " + eventRecord.EventId + " 的" + detail;
                return EventConditionOutcome.InvalidConfig;
            }

            if (outcome == EventConditionOutcome.Fail)
            {
                reason = "EventCondition 未成立（" + detail + "）。";
                return EventConditionOutcome.Fail;
            }

            return EventConditionOutcome.Pass;
        }
    }
}
