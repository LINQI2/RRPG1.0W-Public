using System;
using System.Collections.Generic;
using RRPG.StaticConfig;

namespace RRPG.Event
{
    /// <summary>
    /// **Event Option 执行器** —— WP-Event-1C 的核心（分片实现，见同目录 `EventOptionExecutor.*.cs`）。
    ///
    /// 正式时序（FDS §19，冻结）：
    /// <code>
    /// OptionCondition 重检（点击此刻的世界）
    ///   ↓ 确定 Outcome（EventInstanceId + OptionId 确定性派生）
    ///   ↓ 构建完整 Effect Plan（按 Order）
    ///   ↓ 对整个有序序列做 Preflight（projected state，含前序效果对后序的影响）
    ///   ↓ 全部通过 → Commit（真实 mutation）
    ///   ↓ Record World / Session History
    ///   ↓ Mark EventInstance Completed
    ///   ↓ Success
    /// </code>
    ///
    /// 硬边界：
    ///   - 查询（可用性 / Outcome / Preview）永不 mutation；
    ///   - Preflight 失败 = **零 mutation**（资源 / 补给 / Flag / History / ExecutionState 都不变）；
    ///   - Commit 真实失败 = 不写 History、不标 Completed、不谎报成功（不建跨系统万能 rollback，§11）；
    ///   - 外部产品域效果一律经 <see cref="IEventEffectGateway"/>；Event Core 只自己拥有 Flag；
    ///   - Terminal Effect（StartBattle）成功开始才允许整笔 Commit 成功；
    ///   - 本轮不接产品生命周期、不进 Save、不改 UI（1D 才做 Product Cutover）。
    /// </summary>
    public static partial class EventOptionExecutor
    {
        /// <summary>
        /// **Preview**：只算 Outcome，不写任何状态（§7 / §24）。
        /// 与 <see cref="Execute"/> 使用同一选择逻辑，因此「预览结果 = 正式结果」。
        /// </summary>
        public static EventOutcomeDefinition PreviewOutcome(ResolvedEventInstance instance, string optionId,
            EventStaticCatalog catalog)
        {
            if (instance == null || catalog == null || string.IsNullOrEmpty(optionId)) return null;

            return EventOutcomeSelector.Select(instance.EventInstanceId, optionId, catalog.GetOutcomes(optionId));
        }

        public static EventOptionExecutionResult Execute(
            ResolvedEventInstance instance,
            string optionId,
            EventStaticCatalog catalog,
            IEventWorldView world,
            EventWorldState worldState,
            ExpeditionEventSessionState sessionState,
            EventExecutionState executionState,
            IEventEffectGateway gateway,
            EventExecutionOptions options = null)
        {
            var trace = new EventExecutionTrace
            {
                EventInstanceId = instance != null ? instance.EventInstanceId : null,
                EventId = instance != null ? instance.EventId : null,
                OptionId = optionId,
                Stage = "Validate"
            };

            var failure = CheckPreconditions(instance, catalog, world, worldState, sessionState, executionState,
                gateway, optionId, trace);
            if (failure != null) return failure;

            failure = CheckExactlyOnce(instance, executionState, optionId, trace);
            if (failure != null) return failure;

            EventOptionDefinition option;
            if (!TryResolveOption(instance, catalog, optionId, trace, out option, out failure)) return failure;

            if (!RequireAvailable(instance, optionId, catalog, world, worldState, trace, out failure)) return failure;

            var ctx = new ExecutionContext
            {
                Instance = instance,
                Option = option,
                OptionId = optionId,
                Catalog = catalog,
                World = world,
                WorldState = worldState,
                SessionState = sessionState,
                ExecutionState = executionState,
                Gateway = gateway,
                Options = options ?? EventExecutionOptions.Default,
                Trace = trace
            };

            return ExecuteWithOption(ctx);
        }
    }
}
