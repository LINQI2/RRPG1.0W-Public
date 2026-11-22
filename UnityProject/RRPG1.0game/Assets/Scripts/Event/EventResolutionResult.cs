using System;

namespace RRPG.Event
{
    /// <summary>
    /// 事件解析结果码（WP-Event-1B §23）。
    ///
    /// 设计原则：**「这次没有事件发生」是正常结果，不是异常崩溃** ——
    /// 调用方必须能区分「配置不可用」/「不是已知触发」/「就是没有候选」/
    /// 「有候选事件但角色无解」/「搜索上限截断」这几类完全不同的情况。
    /// </summary>
    public enum EventResolutionCode
    {
        /// <summary>成功：已冻结为 <see cref="ResolvedEventInstance"/>。</summary>
        Success = 0,

        /// <summary>静态事件配置不可用（未提供 Catalog / Root 未生成）—— 环境问题，不是内容问题。</summary>
        ConfigUnavailable = 1,

        /// <summary>请求的 TriggerType 不是 V0.1 已知触发时机（配置被改坏 / 调用方错误）。</summary>
        UnknownTrigger = 2,

        /// <summary>触发成立但没有任何合法候选事件（**合法 Gameplay 结果**）。</summary>
        NoCandidateEvent = 3,

        /// <summary>有事件命中了触发，但全部因「无法求出合法角色绑定」被排除（V0.1 顶层使用）。</summary>
        NoLegalRoleBinding = 4,

        /// <summary>
        /// EventCondition 未成立。**V0.1 只作为每个事件内部的 Trace 拒绝阶段**使用
        /// （顶层统一归入 <see cref="NoCandidateEvent"/>，避免让调用方以为「条件」与「抽取」是两件事）。
        /// </summary>
        ConditionUnsatisfied = 5,

        /// <summary>搜索上限耗尽（**不是**无解：只说明在给定上限内没找到解；默认上限下不应出现）。</summary>
        SearchLimitExhausted = 6,

        /// <summary>运行时配置自相矛盾（未知枚举 / 悬空引用 / 重复身份）—— 必须显式失败，不得静默继续。</summary>
        InvalidRuntimeConfig = 7
    }

    /// <summary>
    /// 事件解析结果：成功 → 冻结实例；失败 → 明确原因 + 可操作诊断 + 结构化 Trace。
    /// 工厂方法保证：失败时绝不返回残缺的 <see cref="ResolvedEventInstance"/>。
    /// </summary>
    public sealed class EventResolutionResult
    {
        public EventResolutionCode Code { get; private set; }

        /// <summary>成功时非 null 的已解析事件实例。</summary>
        public ResolvedEventInstance Instance { get; private set; }

        /// <summary>中文可操作诊断（成功时为空字符串）。</summary>
        public string Diagnostic { get; private set; }

        /// <summary>本次解析的结构化追踪（成功 / 失败都有）。</summary>
        public EventResolutionTrace Trace { get; private set; }

        public bool Success
        {
            get { return Code == EventResolutionCode.Success && Instance != null; }
        }

        private EventResolutionResult() { }

        public static EventResolutionResult Ok(ResolvedEventInstance instance, EventResolutionTrace trace)
        {
            if (instance == null) throw new ArgumentNullException("instance");

            return new EventResolutionResult
            {
                Code = EventResolutionCode.Success,
                Instance = instance,
                Diagnostic = string.Empty,
                Trace = trace ?? new EventResolutionTrace()
            };
        }

        public static EventResolutionResult Fail(EventResolutionCode code, string diagnostic,
            EventResolutionTrace trace = null)
        {
            if (code == EventResolutionCode.Success)
            {
                throw new ArgumentException("失败结果不得使用 Success；成功的唯一构造入口是 Ok()。", "code");
            }

            return new EventResolutionResult
            {
                Code = code,
                Instance = null,
                Diagnostic = diagnostic ?? string.Empty,
                Trace = trace ?? new EventResolutionTrace()
            };
        }

        public override string ToString()
        {
            if (Success) return "OK " + Instance;
            return "FAILED " + Code + "：" + Diagnostic;
        }
    }
}
