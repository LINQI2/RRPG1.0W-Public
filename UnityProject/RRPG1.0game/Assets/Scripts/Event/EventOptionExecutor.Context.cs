using System;

namespace RRPG.Event
{
    /// <summary>EventOptionExecutor：内部执行上下文与结果工厂（实现细节）。</summary>
    public static partial class EventOptionExecutor
    {
        /// <summary>一次执行的内部状态（不公开）。</summary>
        private sealed class ExecutionContext
        {
            public ResolvedEventInstance Instance;
            public StaticConfig.EventOptionDefinition Option;
            public string OptionId;
            public EventStaticCatalog Catalog;
            public IEventWorldView World;
            public EventWorldState WorldState;
            public ExpeditionEventSessionState SessionState;
            public EventExecutionState ExecutionState;
            public IEventEffectGateway Gateway;
            public EventExecutionOptions Options;
            public EventExecutionTrace Trace;
        }

        private static EventOptionExecutionResult Fail(EventExecutionTrace trace, EventExecutionCode code,
            ResolvedEventInstance instance, string optionId, string diagnostic, string outcomeId = null)
        {
            trace.Line("Result = " + code + "：" + diagnostic);
            return EventOptionExecutionResult.Create(code, instance, optionId, outcomeId, null, 0, diagnostic,
                trace);
        }

        /// <summary>
        /// 本实例是否为 Expedition Domain（决定是否额外写 Expedition Session History，FDS §14）。
        /// Domain 由触发时机推导（1B 词表），不需要额外配置。
        /// </summary>
        private static bool IsExpeditionDomain(string triggerType)
        {
            return string.Equals(EventTriggerTypes.ResolveDomain(triggerType), "Expedition",
                StringComparison.Ordinal);
        }
    }
}
