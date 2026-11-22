using System;
using System.Collections.Generic;

namespace RRPG.Event
{
    /// <summary>
    /// 事件选项执行结果码（WP-Event-1C §22）。
    ///
    /// 「执行失败」是明确结果而不是异常：调用方必须能区分
    /// 「实例已经完成（exactly-once）」/「选项不存在 / 当前不可选」/「没有结果」/
    /// 「Preflight 预计失败（零 mutation）」/「Commit 真实失败（不得写 History）」/「配置非法」。
    /// </summary>
    public enum EventExecutionCode
    {
        Success = 0,

        /// <summary>该 EventInstance 已经成功完成过一次（exactly-once；本次零 mutation）。</summary>
        InstanceAlreadyResolved = 1,

        OptionNotFound = 2,

        /// <summary>选项条件不成立 + UnavailableMode = Hide。</summary>
        OptionHidden = 3,

        /// <summary>选项条件不成立 + UnavailableMode = Disable。</summary>
        OptionDisabled = 4,

        /// <summary>该选项没有任何 Outcome（配置非法，但用明确结果码表达）。</summary>
        NoOutcome = 5,

        /// <summary>整个有序 Effect Sequence 的 Preflight 未通过：**零 mutation**（资源 / Flag / History 都不变）。</summary>
        EffectPreflightFailed = 6,

        /// <summary>Commit 阶段真实失败（Gateway 返回 false / 契约违规 / 程序错误）：不写 History、不标 Completed。</summary>
        EffectCommitFailed = 7,

        /// <summary>运行时不变量被破坏（未知枚举 / 悬空引用 / 缺参数 / 缺依赖）。</summary>
        InvalidRuntimeConfig = 8
    }

    /// <summary>
    /// Event Option 执行结果（成功 = 完整的 EventInstance 结算；失败 = 明确原因 + Trace）。
    /// </summary>
    public sealed class EventOptionExecutionResult
    {
        public EventExecutionCode Code { get; private set; }

        public bool Success { get { return Code == EventExecutionCode.Success; } }

        public string EventInstanceId { get; private set; }
        public string EventId { get; private set; }
        public string OptionId { get; private set; }

        /// <summary>成功时选中的 OutcomeId（失败且尚未选到结果时为 null）。</summary>
        public string OutcomeId { get; private set; }

        /// <summary>已应用的 Effect 计划（含每条效果的 preflight 结论与是否真实提交）。</summary>
        public IReadOnlyList<EventEffectPlanEntry> Effects { get; private set; }

        public int AppliedEffectCount { get; private set; }

        /// <summary>本次结果是否包含 Terminal Effect（V0.1 唯一：StartBattle）。</summary>
        public bool HasTerminalEffect { get; private set; }

        /// <summary>可操作中文诊断（成功时为空字符串）。</summary>
        public string Diagnostic { get; private set; }

        public EventExecutionTrace Trace { get; private set; }

        private EventOptionExecutionResult() { }

        internal static EventOptionExecutionResult Create(EventExecutionCode code, ResolvedEventInstance instance,
            string optionId, string outcomeId, EventEffectPlan plan, int appliedEffectCount, string diagnostic,
            EventExecutionTrace trace)
        {
            return new EventOptionExecutionResult
            {
                Code = code,
                EventInstanceId = instance != null ? instance.EventInstanceId : null,
                EventId = instance != null ? instance.EventId : null,
                OptionId = optionId,
                OutcomeId = outcomeId,
                Effects = plan != null ? plan.Entries : (IReadOnlyList<EventEffectPlanEntry>)new List<EventEffectPlanEntry>(),
                AppliedEffectCount = appliedEffectCount,
                HasTerminalEffect = plan != null && plan.HasTerminalEffect,
                Diagnostic = diagnostic ?? string.Empty,
                Trace = trace ?? new EventExecutionTrace()
            };
        }

        public override string ToString()
        {
            if (Success)
            {
                return "OK " + EventInstanceId + " " + OptionId + " → " + OutcomeId
                    + "（effects=" + AppliedEffectCount + "）";
            }
            return "FAILED " + Code + "：" + Diagnostic;
        }
    }
}
