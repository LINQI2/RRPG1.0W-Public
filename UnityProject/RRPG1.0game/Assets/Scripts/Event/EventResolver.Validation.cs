using System;
using System.Collections.Generic;
using System.Globalization;

namespace RRPG.Event
{
    /// <summary>EventResolver：输入校验与错误摘要（WP-Event-1B §23）。</summary>
    public static partial class EventResolver
    {
        /// <summary>单个事件求解的四种结局。</summary>
        private enum SolveOutcome
        {
            /// <summary>成为合法候选。</summary>
            Candidate = 0,

            /// <summary>被拒（拒绝阶段与原因已写入 Trace 条目）。</summary>
            Rejected = 1,

            /// <summary>搜索上限耗尽（整次 Resolve 直接终止）。</summary>
            SearchLimit = 2,

            /// <summary>运行时不变量被破坏（整次 Resolve 以 InvalidRuntimeConfig 终止）。</summary>
            InvalidConfig = 3
        }

        /// <summary>
        /// 输入校验：把「环境不可用 / 调用方错误 / 配置非法」与「真的没有事件」区分开。
        /// 返回 false 时 <paramref name="failure"/> 给出最终结果。
        /// </summary>
        private static bool ValidateInputs(EventResolutionRequest request, EventStaticCatalog catalog,
            IEventWorldView world, EventWorldState worldState, ExpeditionEventSessionState sessionState,
            EventResolutionTrace trace, out EventResolutionResult failure)
        {
            failure = null;

            if (catalog == null)
            {
                failure = EventResolutionResult.Fail(EventResolutionCode.ConfigUnavailable,
                    "静态事件配置不可用：未提供 EventStaticCatalog"
                    + "（请先确认 1A 一键导表已生成 Static Config，并由适配器投影出 Catalog）。", trace);
                return false;
            }

            if (request == null)
            {
                failure = EventResolutionResult.Fail(EventResolutionCode.InvalidRuntimeConfig,
                    "解析请求为 null：EventResolver 需要 TriggerType / TriggerKey / ResolutionSeed。", trace);
                return false;
            }

            if (string.IsNullOrEmpty(request.TriggerType))
            {
                failure = EventResolutionResult.Fail(EventResolutionCode.InvalidRuntimeConfig,
                    "TriggerType 为空：调用方必须给出正式触发时机。", trace);
                return false;
            }

            if (string.IsNullOrEmpty(request.TriggerKey))
            {
                failure = EventResolutionResult.Fail(EventResolutionCode.InvalidRuntimeConfig,
                    "TriggerKey 为空：它是本次触发的稳定身份，EventInstanceId 依赖它"
                    + "（不得使用 Guid / 时间）。", trace);
                return false;
            }

            if (!EventTriggerTypes.IsKnown(request.TriggerType))
            {
                failure = EventResolutionResult.Fail(EventResolutionCode.UnknownTrigger,
                    "TriggerType 不是 V0.1 已知触发时机：'" + request.TriggerType + "'（已知："
                    + EventTriggerTypes.KnownListText() + "）。", trace);
                return false;
            }

            if (!catalog.IsValid)
            {
                failure = EventResolutionResult.Fail(EventResolutionCode.InvalidRuntimeConfig,
                    "静态事件配置自洽性校验失败：" + DescribeErrors(catalog, 3), trace);
                return false;
            }

            if (world == null || worldState == null || sessionState == null)
            {
                failure = EventResolutionResult.Fail(EventResolutionCode.InvalidRuntimeConfig,
                    "World View / Event World State / Expedition Session State 不得为 null"
                    + "（条件求值需要只读世界事实与历史模型）。", trace);
                return false;
            }

            return true;
        }

        /// <summary>Catalog 错误摘要（最多列 <paramref name="max"/> 条 + 总数）。</summary>
        private static string DescribeErrors(EventStaticCatalog catalog, int max)
        {
            var errors = catalog.Errors;
            if (errors.Count == 0) return "(无明细)";

            var parts = new List<string>();
            for (int i = 0; i < errors.Count && i < max; i++) parts.Add(errors[i]);

            string text = string.Join(" | ", parts);
            if (errors.Count > max)
            {
                text += " | …（共 " + errors.Count.ToString(CultureInfo.InvariantCulture) + " 项）";
            }
            return text;
        }
    }
}
