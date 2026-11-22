using System;
using System.Globalization;

namespace RRPG.Event
{
    /// <summary>
    /// 条件分派（唯一解释器的入口，按类型路由到 Context / World / Numeric 三段实现）。
    ///
    /// 为什么拆成三个文件：保持每个文件可读（Context 语义 / 世界事实 / 数值阈值），
    /// 但**解释器只有一个**（不存在第二套语义）。
    /// </summary>
    public static partial class EventConditionEvaluator
    {
        internal static EventConditionOutcome EvaluateSingle(EventConditionView condition,
            EventConditionEvaluation context, out string detail)
        {
            detail = string.Empty;
            if (condition == null)
            {
                detail = "条件记录为空。";
                return EventConditionOutcome.InvalidConfig;
            }

            string type = condition.ConditionType;

            if (string.Equals(type, EventConditionTypes.ContextStringEquals, StringComparison.Ordinal)
                || string.Equals(type, EventConditionTypes.ContextIntAtLeast, StringComparison.Ordinal)
                || string.Equals(type, EventConditionTypes.ContextIntAtMost, StringComparison.Ordinal))
            {
                return EvaluateContext(condition, context, out detail);
            }

            if (string.Equals(type, EventConditionTypes.FlagSet, StringComparison.Ordinal)
                || string.Equals(type, EventConditionTypes.FlagNotSet, StringComparison.Ordinal)
                || string.Equals(type, EventConditionTypes.EventNotSeen, StringComparison.Ordinal)
                || string.Equals(type, EventConditionTypes.EventSeenCountAtLeast, StringComparison.Ordinal)
                || EventConditionTypes.IsPersonConditionType(type))
            {
                return EvaluateWorld(condition, context, out detail);
            }

            return EvaluateNumeric(condition, context, out detail);
        }

        // ---------------- Context（读取 Context 来源：请求 Context 或实例冻结快照） ----------------

        private static EventConditionOutcome EvaluateContext(EventConditionView condition,
            EventConditionEvaluation context, out string detail)
        {
            detail = string.Empty;
            string type = condition.ConditionType;

            if (string.Equals(type, EventConditionTypes.ContextStringEquals, StringComparison.Ordinal))
            {
                if (string.IsNullOrEmpty(condition.TargetKey))
                {
                    detail = "缺少 TargetKey（Context 键）。";
                    return EventConditionOutcome.InvalidConfig;
                }

                string actual = null;
                bool found = context.Context != null && context.Context.TryGetString(condition.TargetKey, out actual);
                bool pass = found && string.Equals(actual, condition.StringValue, StringComparison.Ordinal);

                detail = "string[" + condition.TargetKey + "] expected='" + condition.StringValue + "' actual="
                    + (found ? "'" + actual + "'" : "<missing>");
                return pass ? EventConditionOutcome.Pass : EventConditionOutcome.Fail;
            }

            if (string.IsNullOrEmpty(condition.TargetKey) || !condition.HasIntValue)
            {
                detail = "缺少 TargetKey 或 IntValue。";
                return EventConditionOutcome.InvalidConfig;
            }

            int value = 0;
            bool has = context.Context != null && context.Context.TryGetInt(condition.TargetKey, out value);
            bool atLeast = string.Equals(type, EventConditionTypes.ContextIntAtLeast, StringComparison.Ordinal);
            bool ok = has && (atLeast ? value >= condition.IntValue : value <= condition.IntValue);

            detail = "int[" + condition.TargetKey + "] " + (atLeast ? "expected>=" : "expected<=")
                + condition.IntValue.ToString(CultureInfo.InvariantCulture)
                + " actual=" + (has ? value.ToString(CultureInfo.InvariantCulture) : "<missing>");
            return ok ? EventConditionOutcome.Pass : EventConditionOutcome.Fail;
        }
    }
}
