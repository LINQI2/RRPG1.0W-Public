using System;
using System.Collections.Generic;
using System.Globalization;
using RRPG.StaticConfig;

namespace RRPG.Event
{
    /// <summary>
    /// EventResolver：**Role Binding 求解器**（WP-Event-1B §8 / §10 / §11 / §12）。
    ///
    /// 原则（硬约束）：
    ///   - **求解，不是抽一次**：在**完整候选空间**里回溯寻找至少一个合法绑定；
    ///     候选顺序由 (Seed, EventId, RoleId) 派生（确定性），但绝不因为「第一组尝试失败」就宣告无解；
    ///   - **人物唯一性**：同一个实例内不同 Role 默认必须绑定**不同** PersonId（§10）；
    ///   - **Anti-Hang**：候选评估次数受 <see cref="EventResolverOptions.MaxSearchNodes"/> 限制，
    ///     超限 → SearchLimitExhausted（与「无解」严格区分）；
    ///   - 每个 Role 的全部条件 = **AND**（§9）。
    ///
    /// 每个 Role 只需要确定**一套**合法绑定（§12）；Event 只凭「有解」进入候选池一次。
    /// </summary>
    public static partial class EventResolver
    {
        private static bool TrySolveRoles(ResolveContext ctx, EventDefinition eventRecord,
            EventResolutionTraceEntry entry, out List<EventRoleBinding> bindings, out string reason)
        {
            bindings = new List<EventRoleBinding>();
            reason = string.Empty;

            var roles = ctx.Catalog.GetRoles(eventRecord.EventId);
            if (roles.Count == 0) return true;   // 0 Role 合法（§16 允许）

            var candidateLists = new List<List<string>>(roles.Count);

            for (int i = 0; i < roles.Count; i++)
            {
                List<string> candidates;
                string detail;
                if (!BuildCandidates(ctx, eventRecord, roles[i], out candidates, out detail))
                {
                    reason = "角色 " + roles[i].RoleId + "（" + roles[i].SelectorType + "）无候选：" + detail;
                    return false;
                }

                if (candidates.Count == 0)
                {
                    // 立即给出「为什么池里没有人」的可操作原因（而不是等到回溯失败才报通用文案）
                    reason = "角色 " + roles[i].RoleId + "（" + roles[i].SelectorType + "）没有候选人物："
                        + (string.IsNullOrEmpty(detail) ? "候选池为空。" : detail);
                    return false;
                }

                candidateLists.Add(candidates);
            }

            var bound = new string[roles.Count];
            int nodesBefore = ctx.SearchNodes;

            string failReason;
            if (!SearchBindings(ctx, eventRecord, roles, candidateLists, bound, 0, out failReason))
            {
                reason = failReason;
                return false;
            }

            entry.SearchNodes = ctx.SearchNodes - nodesBefore;

            for (int i = 0; i < roles.Count; i++)
            {
                string sourceKey = string.Equals(roles[i].SelectorType, EventSelectorTypes.ContextPerson,
                    StringComparison.Ordinal)
                    ? roles[i].SourceKey
                    : null;

                bindings.Add(new EventRoleBinding(roles[i].RoleId, roles[i].RoleName, roles[i].SelectorType,
                    bound[i], sourceKey));
            }

            return true;
        }

        /// <summary>回溯搜索（§11 / §12）：候选顺序已确定，搜索保证找到解或明确说明为什么没有。</summary>
        private static bool SearchBindings(ResolveContext ctx, EventDefinition eventRecord,
            IReadOnlyList<EventRoleDefinition> roles, List<List<string>> candidateLists, string[] bound, int index,
            out string failReason)
        {
            failReason = string.Empty;

            if (index >= roles.Count) return true;

            var candidates = candidateLists[index];

            for (int i = 0; i < candidates.Count; i++)
            {
                string personId = candidates[i];

                ctx.SearchNodes++;
                if (ctx.SearchNodes > ctx.Options.MaxSearchNodes)
                {
                    ctx.SearchLimitExceeded = true;
                    ctx.LimitExceededEventId = eventRecord.EventId;
                    failReason = "搜索上限耗尽：已评估 " + ctx.SearchNodes.ToString(CultureInfo.InvariantCulture)
                        + " 次候选组合（上限 "
                        + ctx.Options.MaxSearchNodes.ToString(CultureInfo.InvariantCulture)
                        + "），事件 " + eventRecord.EventId + "。";
                    return false;
                }

                if (IsAlreadyBound(bound, index, personId)) continue;   // §10 唯一性

                string detail;
                if (!RoleConditionsPass(ctx, roles[index], personId, out detail)) continue;

                bound[index] = personId;
                if (SearchBindings(ctx, eventRecord, roles, candidateLists, bound, index + 1, out failReason))
                {
                    return true;
                }

                bound[index] = null;
            }

            if (string.IsNullOrEmpty(failReason))
            {
                failReason = "角色 " + roles[index].RoleId + "（" + roles[index].SelectorType
                    + "）在完整候选空间内没有可行人物（候选 "
                    + candidates.Count.ToString(CultureInfo.InvariantCulture)
                    + " 个；已考虑人物唯一性与角色条件）。";
            }
            return false;
        }

        private static bool IsAlreadyBound(string[] bound, int index, string personId)
        {
            for (int i = 0; i < index; i++)
            {
                if (string.Equals(bound[i], personId, StringComparison.Ordinal)) return true;
            }
            return false;
        }
    }
}
