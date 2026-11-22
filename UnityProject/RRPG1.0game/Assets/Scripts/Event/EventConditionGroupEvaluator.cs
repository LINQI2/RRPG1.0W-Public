using System;
using System.Collections.Generic;
using System.Globalization;

namespace RRPG.Event
{
    /// <summary>
    /// **唯一**的条件解释器（WP-Event-1C 起同时服务「事件成立条件」与「选项可用条件」）。
    ///
    /// 条件组语义（1B 冻结，1C 复用）：
    ///   - 同一个 `GroupId` 内 = **AND**；
    ///   - 不同 `GroupId` 之间 = **OR**：`(A1 AND A2) OR (B1 AND B2)`；
    ///   - 没有任何条件 = **true**。
    ///
    /// 三态返回：Pass / Fail / InvalidConfig（配置形态被破坏时**绝不**默认 true 或 false 后继续）。
    /// 失败详情由 <c>failureDetail</c> 带出，具体文案由调用方（Resolver / Option 求值）组合。
    /// </summary>
    public static class EventConditionGroupEvaluator
    {
        public static EventConditionOutcome Evaluate(IReadOnlyList<EventConditionView> conditions,
            EventConditionEvaluation context, out string failureDetail)
        {
            failureDetail = string.Empty;
            if (conditions == null || conditions.Count == 0) return EventConditionOutcome.Pass;

            string firstFailure = null;
            int groupId = int.MinValue;
            bool groupOpen = false;
            bool groupPassed = true;

            for (int i = 0; i < conditions.Count; i++)
            {
                var condition = conditions[i];
                if (condition == null) continue;

                if (!groupOpen || condition.GroupId != groupId)
                {
                    if (groupOpen && groupPassed) return EventConditionOutcome.Pass;   // OR：前一组已成立

                    groupId = condition.GroupId;
                    groupOpen = true;
                    groupPassed = true;
                }

                string detail;
                var outcome = EventConditionEvaluator.EvaluateSingle(condition, context, out detail);
                if (outcome == EventConditionOutcome.InvalidConfig)
                {
                    failureDetail = "条件 " + condition.ConditionId + " 无法求值（配置非法）：" + detail;
                    return EventConditionOutcome.InvalidConfig;
                }

                if (outcome == EventConditionOutcome.Fail)
                {
                    groupPassed = false;
                    if (firstFailure == null)
                    {
                        firstFailure = "组 " + condition.GroupId.ToString(CultureInfo.InvariantCulture)
                            + "（AND）中的 " + condition.ConditionId + "：" + detail;
                    }
                }
            }

            if (groupOpen && groupPassed) return EventConditionOutcome.Pass;

            failureDetail = firstFailure ?? "全部条件组均不满足";
            return EventConditionOutcome.Fail;
        }
    }
}
