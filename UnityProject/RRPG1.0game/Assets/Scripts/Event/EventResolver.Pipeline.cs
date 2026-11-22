using System;
using System.Collections.Generic;
using RRPG.StaticConfig;

namespace RRPG.Event
{
    /// <summary>EventResolver：候选流水线与单事件求解（WP-Event-1B §7）。</summary>
    public static partial class EventResolver
    {
        /// <summary>
        /// 候选流水线：返回 null = 整次求解被终止（搜索上限 / 运行时不变量破坏），
        /// 由调用方按 <see cref="ResolveContext"/> 的标记给出最终结果码。
        /// </summary>
        private static List<EventCandidate> ResolveCandidates(ResolveContext ctx, out int matchedCount,
            out bool allFailuresWereRoleBinding)
        {
            var candidates = new List<EventCandidate>();
            matchedCount = 0;
            allFailuresWereRoleBinding = true;

            var listening = ctx.Catalog.GetEnabledEventsByTrigger(ctx.Request.TriggerType);

            foreach (var eventRecord in listening)
            {
                if (eventRecord == null) continue;

                matchedCount++;

                var entry = ctx.Trace.Begin(eventRecord.EventId);
                entry.TriggerMatched = true;
                entry.Weight = eventRecord.Weight;

                var outcome = ResolveOneEvent(ctx, eventRecord, entry);
                if (outcome == SolveOutcome.SearchLimit || outcome == SolveOutcome.InvalidConfig) return null;
                if (outcome == SolveOutcome.Rejected)
                {
                    if (entry.RejectStage != EventRejectStage.RoleBinding) allFailuresWereRoleBinding = false;
                    continue;
                }

                candidates.Add(new EventCandidate(eventRecord, new List<EventRoleBinding>(entry.RoleBindings)));
            }

            return candidates;
        }

        /// <summary>单个事件的求解：Repeat / Cooldown → Role Binding → EventCondition。</summary>
        private static SolveOutcome ResolveOneEvent(ResolveContext ctx, EventDefinition eventRecord,
            EventResolutionTraceEntry entry)
        {
            string reason;

            if (!RepeatAllows(ctx, eventRecord, out reason))
            {
                entry.RejectStage = EventRejectStage.Repeat;
                entry.RejectReason = reason;
                return SolveOutcome.Rejected;
            }
            entry.RepeatPassed = true;

            if (!CooldownAllows(ctx, eventRecord, out reason))
            {
                entry.RejectStage = EventRejectStage.Cooldown;
                entry.RejectReason = reason;
                return SolveOutcome.Rejected;
            }
            entry.CooldownPassed = true;

            List<EventRoleBinding> bindings;
            if (!TrySolveRoles(ctx, eventRecord, entry, out bindings, out reason))
            {
                if (ctx.SearchLimitExceeded)
                {
                    entry.RejectStage = EventRejectStage.SearchLimit;
                    entry.RejectReason = reason;
                    return SolveOutcome.SearchLimit;
                }

                entry.RejectStage = EventRejectStage.RoleBinding;
                entry.RejectReason = reason;
                return SolveOutcome.Rejected;
            }
            entry.RoleBindingPassed = true;
            entry.RoleBindings = bindings;

            var condition = EvaluateEventConditions(ctx, eventRecord, bindings, out reason);
            if (condition == EventConditionOutcome.InvalidConfig)
            {
                entry.RejectStage = EventRejectStage.Condition;
                entry.RejectReason = reason;
                ctx.InvalidConfigReason = reason;
                return SolveOutcome.InvalidConfig;
            }
            if (condition == EventConditionOutcome.Fail)
            {
                entry.RejectStage = EventRejectStage.Condition;
                entry.RejectReason = reason;
                return SolveOutcome.Rejected;
            }
            entry.ConditionPassed = true;

            entry.IsCandidate = true;
            entry.RejectStage = EventRejectStage.None;
            entry.RejectReason = string.Empty;
            return SolveOutcome.Candidate;
        }
    }
}
