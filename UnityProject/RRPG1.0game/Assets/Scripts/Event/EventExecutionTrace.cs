using System;
using System.Collections.Generic;
using System.Text;

namespace RRPG.Event
{
    /// <summary>
    /// **Execution Trace（事件执行追踪）** —— WP-Event-1C §23，延续 1B 的诊断思想。
    ///
    /// 边界：EditMode / Debug 用；**不是** Player UI、不写 Save、不写 Console。
    /// 成功与失败都应能看出：条件阶段 / Outcome / 每个效果的 Preflight / Commit / History / Instance 状态。
    /// </summary>
    public sealed class EventExecutionTrace
    {
        private readonly List<string> _lines = new List<string>();

        public string EventInstanceId { get; internal set; }
        public string EventId { get; internal set; }
        public string OptionId { get; internal set; }

        /// <summary>最后进入的阶段：OptionCondition / Outcome / Plan / Preflight / Commit / History / Instance。</summary>
        public string Stage { get; internal set; }

        public EventConditionOutcome ConditionOutcome { get; internal set; }
        public string ConditionDetail { get; internal set; }

        public string OutcomeId { get; internal set; }

        /// <summary>本结果涉及的效果计划（含 preflight 结论）。</summary>
        public IReadOnlyList<EventEffectPlanEntry> Effects { get; internal set; }

        public bool CommitAttempted { get; internal set; }
        public bool CommitSucceeded { get; internal set; }
        public int AppliedEffectCount { get; internal set; }

        public bool HistoryRecorded { get; internal set; }
        public int WorldHistoryCount { get; internal set; }
        public int SessionHistoryCount { get; internal set; }

        public bool InstanceCompleted { get; internal set; }

        public IReadOnlyList<string> Lines { get { return _lines; } }

        internal void Line(string text)
        {
            if (!string.IsNullOrEmpty(text)) _lines.Add(text);
        }

        /// <summary>结构化文本（Debug / 测试断言用；不进玩家 UI）。</summary>
        public string ToText()
        {
            var sb = new StringBuilder();
            sb.Append("[Event Execution] instance=").Append(EventInstanceId ?? "<none>")
                .Append(" event=").Append(EventId ?? "<none>")
                .Append(" option=").Append(OptionId ?? "<none>")
                .Append(" stage=").Append(Stage ?? "<none>");

            foreach (var line in _lines)
            {
                sb.Append('\n').Append("  ").Append(line);
            }

            return sb.ToString();
        }

        public override string ToString()
        {
            return ToText();
        }
    }
}
