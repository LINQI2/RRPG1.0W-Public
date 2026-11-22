using System;
using System.Collections.Generic;
using System.Text;

namespace RRPG.Event
{
    /// <summary>
    /// Event Resolution Trace（WP-Event-1B §24）—— **V0.1 必做**。
    ///
    /// 用途：EditMode / Debug 诊断「为什么这次没有事件 / 为什么是它」。
    /// 边界：**不是** Player UI、不写长期 Save、不默认每次打印完整日志
    /// （调用方按需 <see cref="ToText"/>；Resolver 自身不写 Console）。
    /// </summary>
    public sealed class EventResolutionTrace
    {
        private readonly List<EventResolutionTraceEntry> _entries = new List<EventResolutionTraceEntry>();

        public string TriggerType { get; internal set; }
        public string TriggerKey { get; internal set; }
        public int ResolutionSeed { get; internal set; }

        /// <summary>最终被选中的 EventId（无成功结果时为 null）。</summary>
        public string SelectedEventId { get; internal set; }

        /// <summary>评估过的 Event 数（含未命中触发的）。</summary>
        public int EvaluatedEventCount { get { return _entries.Count; } }

        /// <summary>成为候选的 Event 数。</summary>
        public int CandidateCount { get; internal set; }

        /// <summary>本次调用累计评估的 (Role, 候选 Person) 次数。</summary>
        public int TotalSearchNodes { get; internal set; }

        public IReadOnlyList<EventResolutionTraceEntry> Entries { get { return _entries; } }

        internal EventResolutionTraceEntry Begin(string eventId)
        {
            var entry = new EventResolutionTraceEntry
            {
                EventId = eventId,
                RejectStage = EventRejectStage.None,
                RejectReason = string.Empty,
                RoleBindings = new List<EventRoleBinding>()
            };
            _entries.Add(entry);
            return entry;
        }

        /// <summary>按 EventId 取追踪条目（未找到 → null）。</summary>
        public EventResolutionTraceEntry Find(string eventId)
        {
            if (string.IsNullOrEmpty(eventId)) return null;

            for (int i = 0; i < _entries.Count; i++)
            {
                if (string.Equals(_entries[i].EventId, eventId, StringComparison.Ordinal)) return _entries[i];
            }
            return null;
        }

        /// <summary>结构化文本（Debug / 测试断言用；不进玩家 UI）。</summary>
        public string ToText()
        {
            var sb = new StringBuilder();
            sb.Append("[Event Resolution] trigger=").Append(TriggerType)
                .Append(" key=").Append(TriggerKey)
                .Append(" seed=").Append(ResolutionSeed.ToString(System.Globalization.CultureInfo.InvariantCulture))
                .Append(" evaluated=").Append(EvaluatedEventCount.ToString(System.Globalization.CultureInfo.InvariantCulture))
                .Append(" candidates=").Append(CandidateCount.ToString(System.Globalization.CultureInfo.InvariantCulture))
                .Append(" nodes=").Append(TotalSearchNodes.ToString(System.Globalization.CultureInfo.InvariantCulture))
                .Append(" selected=").Append(string.IsNullOrEmpty(SelectedEventId) ? "<none>" : SelectedEventId);

            foreach (var entry in _entries)
            {
                sb.Append('\n').Append("  ").Append(entry.ToString());

                if (entry.RoleBindings != null && entry.RoleBindings.Count > 0)
                {
                    foreach (var binding in entry.RoleBindings)
                    {
                        sb.Append('\n').Append("      ").Append(binding.ToString());
                    }
                }
            }

            return sb.ToString();
        }

        public override string ToString()
        {
            return ToText();
        }
    }
}
