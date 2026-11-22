using System;
using System.Collections.Generic;
using System.Globalization;

namespace RRPG.Event
{
    /// <summary>
    /// EventOptionExecutor：**Preflight（预计执行）** —— WP-Event-1C §10。
    ///
    /// 语义（正式冻结）：
    ///   - 按 Order **完整**走一遍有序 Effect Sequence，在 **projected state** 上校验业务不变量
    ///     （资源 / 补给不允许变负）；
    ///   - projected state 的起点来自 Gateway 的只读读取，之后由本类自己累加
    ///     —— 因此「Effect 1: +20 Food」会真实影响「Effect 2: -30 Food」的判定（C12）；
    ///   - 全过程**零 mutation**：不改资源、不改 Flag、不写 History、不标 Completed；
    ///   - 失败会给出「expected / actual projected」可诊断文本（§23）。
    /// </summary>
    public static partial class EventOptionExecutor
    {
        /// <summary>一次 preflight 内维护的预计状态（只活在本函数内，绝不外泄）。</summary>
        private sealed class ProjectedState
        {
            public readonly Dictionary<string, int> VillageResources = new Dictionary<string, int>(StringComparer.Ordinal);
            public int ExpeditionSupply;
            public bool ExpeditionSupplyRead;
        }

        private static ExecutionStep RunPreflight(ExecutionContext ctx, EventEffectPlan plan, out string error)
        {
            error = string.Empty;
            var projected = new ProjectedState();

            for (int i = 0; i < plan.Entries.Count; i++)
            {
                var entry = plan.Entries[i];

                string detail;
                var step = PreflightOne(ctx, entry, projected, out detail);

                if (step == ExecutionStep.InvalidConfig)
                {
                    error = detail;
                    return ExecutionStep.InvalidConfig;
                }

                if (step == ExecutionStep.Failed)
                {
                    ctx.Trace.Line("Effect " + entry.Describe() + " : Preflight FAIL（"
                        + entry.PreflightReason + "）");
                    continue;   // 继续评估其余效果，给出完整诊断（整体必然失败）
                }

                MarkPreflightOk(ctx, entry);
            }

            plan.Evaluate();
            return plan.AllPreflightPassed ? ExecutionStep.Ok : ExecutionStep.Failed;
        }

        private static void MarkPreflightOk(ExecutionContext ctx, EventEffectPlanEntry entry)
        {
            entry.Preflight = EventEffectPreflightState.Ok;
            entry.PreflightReason = string.Empty;

            ctx.Trace.Line("Effect " + entry.Describe() + " : Preflight OK"
                + (entry.HasProjection
                    ? "（projected " + entry.ProjectedAfter.ToString(CultureInfo.InvariantCulture) + "）"
                    : string.Empty));
        }

        private static void MarkPreflightFailed(EventEffectPlanEntry entry, string reason)
        {
            entry.Preflight = EventEffectPreflightState.Failed;
            entry.PreflightReason = reason;
        }

        private static string Signed(int value)
        {
            return (value >= 0 ? "+" : string.Empty) + value.ToString(CultureInfo.InvariantCulture);
        }
    }
}
