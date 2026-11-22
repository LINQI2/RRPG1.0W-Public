using System;
using System.Collections.Generic;
using System.Globalization;
using RRPG.StaticConfig;

namespace RRPG.Event
{
    /// <summary>一步执行的结论。</summary>
    public enum ExecutionStep
    {
        Ok = 0,
        Failed = 1,

        /// <summary>运行时不变量被破坏（未知枚举 / 缺参数 / 悬空引用）→ InvalidRuntimeConfig。</summary>
        InvalidConfig = 2
    }

    /// <summary>
    /// EventOptionExecutor：**Effect Plan 构建**（§8 / §21）。
    ///
    /// 1A 的 Compiler 已在编译期保证效果形状；这里是运行时**第二道防线**
    /// （Runtime 可能拿到非编译器产生的投影）。任一违规 → InvalidRuntimeConfig，绝不「尽力执行」。
    /// </summary>
    public static partial class EventOptionExecutor
    {
        /// <summary>返回 null 表示计划非法，<paramref name="error"/> 给出可操作原因。</summary>
        private static EventEffectPlan BuildEffectPlan(ExecutionContext ctx, EventOutcomeDefinition outcome,
            out string error)
        {
            error = string.Empty;

            var definitions = ctx.Catalog.GetEffects(outcome.OutcomeId);
            if (definitions.Count > ctx.Options.MaxEffectsPerOutcome)
            {
                error = "结果 " + outcome.OutcomeId + " 的效果数量 "
                    + definitions.Count.ToString(CultureInfo.InvariantCulture) + " 超过上限 "
                    + ctx.Options.MaxEffectsPerOutcome.ToString(CultureInfo.InvariantCulture)
                    + "（Anti-Hang；不会静默截断）。";
                return null;
            }

            var plan = new EventEffectPlan { OutcomeId = outcome.OutcomeId };

            for (int i = 0; i < definitions.Count; i++)
            {
                var definition = definitions[i];
                if (definition == null) continue;

                if (!EventEffectTypes.IsKnown(definition.EffectType))
                {
                    error = "效果 " + definition.EffectId + " 的 EffectType 未知：'" + definition.EffectType
                        + "'（允许：" + EventEffectTypes.KnownListText() + "）。";
                    return null;
                }

                var entry = new EventEffectPlanEntry
                {
                    Effect = definition,
                    EffectId = definition.EffectId,
                    EffectType = definition.EffectType,
                    Order = definition.Order,
                    IsTerminal = EventEffectTypes.IsTerminal(definition.EffectType),
                    TargetRoleId = definition.TargetRoleId,
                    TargetKey = definition.TargetKey,
                    HasIntValue = definition.HasIntValue,
                    IntValue = definition.IntValue,
                    StringValue = definition.StringValue,
                    Preflight = EventEffectPreflightState.Pending
                };

                if (!ValidateEffectShape(ctx, entry, i, definitions.Count, out error)) return null;

                plan.Add(entry);
                if (entry.IsTerminal) plan.HasTerminalEffect = true;
            }

            return plan;
        }
    }
}
