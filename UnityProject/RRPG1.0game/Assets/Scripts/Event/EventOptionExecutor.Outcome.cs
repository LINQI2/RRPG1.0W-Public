using System;
using System.Globalization;
using RRPG.StaticConfig;

namespace RRPG.Event
{
    /// <summary>
    /// EventOptionExecutor：Outcome 选择 → Plan → Preflight → Commit → History → Completed。
    ///
    /// 这个顺序是 FDS §19 冻结的，**不得**调换：任何一步失败都必须停在原地并如实报告，
    /// 绝不允许「先写 History 再执行效果」或「效果没成功就说事件发生了」。
    /// </summary>
    public static partial class EventOptionExecutor
    {
        private static EventOptionExecutionResult ExecuteWithOption(ExecutionContext ctx)
        {
            var trace = ctx.Trace;
            var instance = ctx.Instance;

            // ---------------- Outcome（确定性；先查再执行） ----------------
            trace.Stage = "Outcome";

            var outcome = EventOutcomeSelector.Select(instance.EventInstanceId, ctx.OptionId,
                ctx.Catalog.GetOutcomes(ctx.OptionId));
            if (outcome == null)
            {
                return Fail(trace, EventExecutionCode.NoOutcome, instance, ctx.OptionId,
                    "选项 " + ctx.OptionId + " 没有配置任何 Outcome（Enabled 事件必须每个 Option 至少一个 Outcome）。");
            }

            trace.OutcomeId = outcome.OutcomeId;
            trace.Line("Outcome = " + outcome.OutcomeId
                + " (weight " + outcome.Weight.ToString(CultureInfo.InvariantCulture)
                + ", seed " + EventOutcomeSelector.SeedFor(instance.EventInstanceId, ctx.OptionId)
                    .ToString(CultureInfo.InvariantCulture) + ")");

            // ---------------- Effect Plan ----------------
            trace.Stage = "Plan";

            string planError;
            var plan = BuildEffectPlan(ctx, outcome, out planError);
            if (plan == null)
            {
                return Fail(trace, EventExecutionCode.InvalidRuntimeConfig, instance, ctx.OptionId, planError);
            }

            trace.Effects = plan.Entries;
            trace.Line("Effects = " + plan.Entries.Count.ToString(CultureInfo.InvariantCulture)
                + " 条（Order 升序）");

            // ---------------- Preflight（projected state，零 mutation） ----------------
            trace.Stage = "Preflight";

            string preflightError;
            var preflight = RunPreflight(ctx, plan, out preflightError);
            if (preflight == ExecutionStep.InvalidConfig)
            {
                return Fail(trace, EventExecutionCode.InvalidRuntimeConfig, instance, ctx.OptionId, preflightError,
                    outcome.OutcomeId);
            }
            if (preflight == ExecutionStep.Failed)
            {
                return Fail(trace, EventExecutionCode.EffectPreflightFailed, instance, ctx.OptionId,
                    "Effect Preflight 失败（本次零 mutation：资源 / 补给 / Flag / History / Instance 全部未改变）："
                    + plan.FirstFailureReason, outcome.OutcomeId);
            }

            // ---------------- Commit（唯一真实 mutation 点） ----------------
            trace.Stage = "Commit";
            trace.CommitAttempted = true;

            int applied;
            string commitError;
            if (!CommitAll(ctx, plan, out applied, out commitError))
            {
                return Fail(trace, EventExecutionCode.EffectCommitFailed, instance, ctx.OptionId,
                    "Effect Commit 失败（不写 Event History、不标记 Instance 完成）：" + commitError,
                    outcome.OutcomeId);
            }

            trace.CommitSucceeded = true;
            trace.AppliedEffectCount = applied;

            // ---------------- History（Commit 成功之后才写；§19） ----------------
            trace.Stage = "History";
            RecordHistory(ctx);

            trace.HistoryRecorded = true;
            trace.WorldHistoryCount = ctx.WorldState.History.OccurrenceCount(instance.EventId);
            trace.SessionHistoryCount = IsExpeditionDomain(instance.TriggerType)
                ? ctx.SessionState.OccurrenceCount(instance.EventId)
                : 0;
            trace.Line("History = Recorded (world count="
                + trace.WorldHistoryCount.ToString(CultureInfo.InvariantCulture) + ", session count="
                + trace.SessionHistoryCount.ToString(CultureInfo.InvariantCulture) + ")");

            // ---------------- Completed（exactly-once 记号；§17） ----------------
            trace.Stage = "Instance";
            ctx.ExecutionState.MarkCompleted(instance, ctx.OptionId, outcome.OutcomeId,
                ctx.World.VillageDayOrdinal, ctx.World.ExpeditionNodeOrdinal);
            trace.InstanceCompleted = true;
            trace.Line("Instance = Completed (" + instance.EventInstanceId + ")");

            return EventOptionExecutionResult.Create(EventExecutionCode.Success, instance, ctx.OptionId,
                outcome.OutcomeId, plan, applied, string.Empty, trace);
        }

        /// <summary>
        /// 写 History（§14 / §15）：
        ///   - **World Event History**：所有 Domain 的事件都计数（Village + Expedition），
        ///     这样 EventNotSeen / EventSeenCountAtLeast 能跨 Context 查询；
        ///   - **Expedition Session History**：只有 Expedition Domain 事件写；
        ///     日序 / 节点序一律取自当前 World View，**不 invent** 任何时间。
        /// </summary>
        private static void RecordHistory(ExecutionContext ctx)
        {
            var instance = ctx.Instance;

            ctx.WorldState.History.RecordOccurrence(instance.EventId, ctx.World.VillageDayOrdinal,
                instance.EventInstanceId);

            if (IsExpeditionDomain(instance.TriggerType))
            {
                ctx.SessionState.RecordOccurrence(instance.EventId, ctx.World.ExpeditionNodeOrdinal,
                    instance.EventInstanceId);
            }
        }
    }
}
