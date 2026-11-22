using System;
using System.Collections.Generic;
using System.Globalization;
using RRPG.StaticConfig;

namespace RRPG.Event
{
    /// <summary>
    /// **EventResolver（事件解析器）** —— WP-Event-1B 的核心。
    ///
    /// 职责（且仅此）：给定 `Trigger + EventContext + ResolutionSeed + 只读世界状态`，
    /// **确定性地**找到一个当前真正合法的 Event，并冻结为 <see cref="ResolvedEventInstance"/>。
    ///
    /// 只负责：**事件为什么能出现、出现的是谁**。
    /// 不负责（属后续检查点）：
    ///   - 不执行玩家 Option、不执行 Effect（1C）；
    ///   - 不写 Event History（1C：Option → Outcome → Effect 全部成功后才记录 occurrence）；
    ///   - 不改产品 UI、不接 Journey / Village 产品流程（1D）；
    ///   - 不渲染模板（表现接入前完成）。
    ///
    /// 流水线（§7 正式冻结顺序）：
    ///   Trigger → 监听该 TriggerType 的 Enabled Event → Repeat / Cooldown 预筛选
    ///   → 完整 Role Binding → EventCondition → Candidate（**每个 Event 只一张票**）
    ///   → 按 Event.Weight 确定性抽取一个 → 冻结为 ResolvedEventInstance。
    ///
    /// 确定性硬约束（§17）：不使用 `UnityEngine.Random` / 时间 / Guid / 字典遍历顺序；
    /// 随机性只来自 `ResolutionSeed`；候选列表进入随机过程前一律规范排序。
    /// 纯函数式：只读输入、不改任何输入对象、不写 Console（Trace 由调用方按需输出）。
    /// </summary>
    public static partial class EventResolver
    {
        /// <summary>解析入口。任何「这次没有事件发生」都以结果码返回，绝不用异常表示游戏状态。</summary>
        public static EventResolutionResult Resolve(
            EventResolutionRequest request,
            EventStaticCatalog catalog,
            IEventWorldView world,
            EventWorldState worldState,
            ExpeditionEventSessionState sessionState,
            EventResolverOptions options = null)
        {
            var trace = new EventResolutionTrace
            {
                TriggerType = request != null ? request.TriggerType : null,
                TriggerKey = request != null ? request.TriggerKey : null,
                ResolutionSeed = request != null ? request.ResolutionSeed : 0
            };

            EventResolutionResult invalid;
            if (!ValidateInputs(request, catalog, world, worldState, sessionState, trace, out invalid))
            {
                return invalid;
            }

            var ctx = new ResolveContext
            {
                Request = request,
                Catalog = catalog,
                World = world,
                WorldState = worldState,
                SessionState = sessionState,
                Options = options ?? EventResolverOptions.Default,
                Trace = trace
            };

            int matchedCount;
            bool allFailuresWereRoleBinding;
            var candidates = ResolveCandidates(ctx, out matchedCount, out allFailuresWereRoleBinding);

            trace.TotalSearchNodes = ctx.SearchNodes;

            if (ctx.SearchLimitExceeded)
            {
                return EventResolutionResult.Fail(EventResolutionCode.SearchLimitExhausted,
                    "搜索上限耗尽（上限 " + ctx.Options.MaxSearchNodes.ToString(CultureInfo.InvariantCulture)
                    + " 次候选评估，事件 " + ctx.LimitExceededEventId + "）：这不是「无解」，"
                    + "只说明在给定上限内未完成求解；请调大 EventResolverOptions.MaxSearchNodes 或收敛配置。",
                    trace);
            }

            if (candidates == null)
            {
                return EventResolutionResult.Fail(EventResolutionCode.InvalidRuntimeConfig,
                    ctx.InvalidConfigReason ?? "求解被终止（无明细诊断，请检查 Catalog 与历史模型）。", trace);
            }

            // 未监听本次触发的事件也进 Trace（回答「为什么不是它」）
            foreach (var eventRecord in catalog.Events)
            {
                if (eventRecord != null && trace.Find(eventRecord.EventId) == null) trace.Begin(eventRecord.EventId);
            }

            trace.CandidateCount = candidates.Count;

            if (candidates.Count == 0)
            {
                if (matchedCount == 0)
                {
                    return EventResolutionResult.Fail(EventResolutionCode.NoCandidateEvent,
                        "没有任何启用事件监听触发时机 '" + request.TriggerType + "'。", trace);
                }

                if (allFailuresWereRoleBinding)
                {
                    return EventResolutionResult.Fail(EventResolutionCode.NoLegalRoleBinding,
                        "命中触发的事件都无法求出合法角色绑定（共 "
                        + matchedCount.ToString(CultureInfo.InvariantCulture) + " 个）；详见 Trace。", trace);
                }

                return EventResolutionResult.Fail(EventResolutionCode.NoCandidateEvent,
                    "命中触发的 " + matchedCount.ToString(CultureInfo.InvariantCulture)
                    + " 个事件都被预筛选 / 条件排除（详见 Trace 的每事件拒绝阶段）。", trace);
            }

            var selected = SelectByWeight(ctx, candidates);
            if (selected == null)
            {
                return EventResolutionResult.Fail(EventResolutionCode.NoCandidateEvent,
                    "候选池非空但抽取失败（不应发生，请检查 Catalog）。", trace);
            }

            trace.SelectedEventId = selected.Event.EventId;

            var instance = new ResolvedEventInstance(
                BuildEventInstanceId(request.TriggerKey, selected.Event.EventId, request.TriggerType,
                    request.ResolutionSeed),
                selected.Event.EventId,
                request.TriggerType,
                request.TriggerKey,
                request.ResolutionSeed,
                selected.RoleBindings,
                request.ContextOrEmpty.Freeze());

            return EventResolutionResult.Ok(instance, trace);
        }
    }
}
