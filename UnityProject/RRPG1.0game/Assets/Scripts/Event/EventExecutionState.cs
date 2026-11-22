using System;
using System.Collections.Generic;

namespace RRPG.Event
{
    /// <summary>一次已完成的 Event 实例记录（Runtime Only，不进 Save）。</summary>
    public sealed class EventExecutionCompletionRecord
    {
        public string EventInstanceId { get; internal set; }
        public string EventId { get; internal set; }
        public string OptionId { get; internal set; }
        public string OutcomeId { get; internal set; }

        /// <summary>完成时的村庄绝对日序（来自 World View；用于诊断，不用于规则）。</summary>
        public int VillageDayOrdinal { get; internal set; }

        /// <summary>完成时的远征节点序（来自 World View；用于诊断，不用于规则）。</summary>
        public int ExpeditionNodeOrdinal { get; internal set; }

        public override string ToString()
        {
            return EventInstanceId + " [" + EventId + "] " + OptionId + " → " + OutcomeId;
        }
    }

    /// <summary>
    /// **Event Execution State（事件实例执行状态）** —— WP-Event-1C §17 / §18。
    ///
    /// 职责（唯一）：保证 **exactly-once**：
    ///   - 一个成功执行完成的 `EventInstanceId` 不允许再次执行任何 Option；
    ///   - 第二次执行明确返回 `InstanceAlreadyResolved`，**不再**扣资源 / 改 Flag / 写 History；
    ///   - 失败（Preflight 失败 / Commit 失败）**不**标记完成 —— 修复条件后可以重新尝试；
    ///   - Completed 以 `EventInstanceId` 为边界，**不以 OptionId 为边界**
    ///     （同一个 Instance 最终只能有一个 Option 成功）。
    ///
    /// 本轮（1C）**不进 Save**：它是 Runtime 结构；持久化策略属 1D（FDS §16）。
    /// </summary>
    public sealed class EventExecutionState
    {
        private readonly Dictionary<string, EventExecutionCompletionRecord> _completed =
            new Dictionary<string, EventExecutionCompletionRecord>(StringComparer.Ordinal);

        public bool IsCompleted(string eventInstanceId)
        {
            return !string.IsNullOrEmpty(eventInstanceId) && _completed.ContainsKey(eventInstanceId);
        }

        public bool TryGetCompletion(string eventInstanceId, out EventExecutionCompletionRecord record)
        {
            record = null;
            if (string.IsNullOrEmpty(eventInstanceId)) return false;
            return _completed.TryGetValue(eventInstanceId, out record);
        }

        /// <summary>已完成的实例（按 EventInstanceId 规范排序；仅诊断 / 测试用）。</summary>
        public IReadOnlyList<EventExecutionCompletionRecord> Completions
        {
            get
            {
                var list = new List<EventExecutionCompletionRecord>(_completed.Values);
                list.Sort((a, b) => string.CompareOrdinal(a.EventInstanceId, b.EventInstanceId));
                return list;
            }
        }

        /// <summary>
        /// 标记实例已完成。**只允许** <see cref="EventOptionExecutor"/> 在
        /// 「Preflight 通过 + Commit 成功 + History 已记录」之后调用。
        /// </summary>
        internal void MarkCompleted(ResolvedEventInstance instance, string optionId, string outcomeId,
            int villageDayOrdinal, int expeditionNodeOrdinal)
        {
            if (instance == null || string.IsNullOrEmpty(instance.EventInstanceId)) return;

            _completed[instance.EventInstanceId] = new EventExecutionCompletionRecord
            {
                EventInstanceId = instance.EventInstanceId,
                EventId = instance.EventId,
                OptionId = optionId,
                OutcomeId = outcomeId,
                VillageDayOrdinal = villageDayOrdinal,
                ExpeditionNodeOrdinal = expeditionNodeOrdinal
            };
        }
    }
}
