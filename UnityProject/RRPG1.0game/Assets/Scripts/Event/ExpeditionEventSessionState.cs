using System;
using System.Collections.Generic;

namespace RRPG.Event
{
    /// <summary>远征会话事件历史中的一条记录（纯数据，**不落档**）。</summary>
    public sealed class ExpeditionEventOccurrenceRecord
    {
        public string EventId { get; internal set; }
        public int OccurrenceCount { get; internal set; }
        public int LastOccurredNodeOrdinal { get; internal set; }
        public bool HasLastNodeOrdinal { get; internal set; }

        /// <summary>最近一次发生的 EventInstanceId（同一次事件实例可追溯；旧数据可为空）。</summary>
        public string LastEventInstanceId { get; internal set; }

        public override string ToString()
        {
            return EventId + " ×" + OccurrenceCount
                + (HasLastNodeOrdinal ? " lastNode=" + LastOccurredNodeOrdinal : " lastNode=-");
        }
    }

    /// <summary>
    /// **Expedition Event Session State（本次远征会话事件历史）** —— WP-Event-1B §5。
    ///
    /// 语义支持：
    ///   - `OncePerExpedition`：本会话 OccurrenceCount &gt; 0 → 不再进入候选；
    ///   - `ExpeditionNodes` 冷却：LastOccurredNodeOrdinal 与当前远征节点序比较。
    ///
    /// 边界：
    ///   - **Runtime Only**：不进 Save、不持久化；新的远征 = 新建一个本对象（历史自然清空）；
    ///   - 只读 + 测试种子；正式写入属 1C。
    /// </summary>
    public sealed partial class ExpeditionEventSessionState
    {
        private readonly Dictionary<string, ExpeditionEventOccurrenceRecord> _records =
            new Dictionary<string, ExpeditionEventOccurrenceRecord>(StringComparer.Ordinal);

        /// <summary>本次远征的稳定标识（仅用于诊断 / Trace；不参与规则判定）。</summary>
        public string SessionKey { get; private set; }

        public ExpeditionEventSessionState(string sessionKey = null)
        {
            SessionKey = string.IsNullOrEmpty(sessionKey) ? "expedition_session" : sessionKey;
        }

        public int OccurrenceCount(string eventId)
        {
            var record = Find(eventId);
            return record == null ? 0 : record.OccurrenceCount;
        }

        public bool HasOccurred(string eventId)
        {
            return OccurrenceCount(eventId) > 0;
        }

        /// <summary>最近一次发生的远征节点序（从未发生 → false）。</summary>
        public bool TryGetLastNodeOrdinal(string eventId, out int ordinal)
        {
            ordinal = 0;
            var record = Find(eventId);
            if (record == null || !record.HasLastNodeOrdinal) return false;

            ordinal = record.LastOccurredNodeOrdinal;
            return true;
        }

        public IReadOnlyList<ExpeditionEventOccurrenceRecord> Records
        {
            get
            {
                var list = new List<ExpeditionEventOccurrenceRecord>(_records.Values);
                list.Sort((a, b) => string.CompareOrdinal(a.EventId, b.EventId));
                return list;
            }
        }

        /// <summary>
        /// **正式写入口（WP-Event-1C）**：记录一次**本次远征会话**内的事件发生。
        ///
        /// 唯一合法调用方 = Execution / Transaction 层，且在 Commit 全部成功**之后**；
        /// 只用于 **Expedition Domain** 事件（Village Event 不写 Session History，FDS §14）。
        /// <see cref="EventResolver"/> 绝不调用本方法。
        /// </summary>
        public void RecordOccurrence(string eventId, int nodeOrdinal, string eventInstanceId = null)
        {
            if (string.IsNullOrEmpty(eventId)) return;

            var record = Find(eventId);
            if (record == null)
            {
                record = new ExpeditionEventOccurrenceRecord
                {
                    EventId = eventId,
                    OccurrenceCount = 0,
                    LastOccurredNodeOrdinal = 0,
                    HasLastNodeOrdinal = false,
                    LastEventInstanceId = null
                };
                _records.Add(eventId, record);
            }

            record.OccurrenceCount++;
            record.LastOccurredNodeOrdinal = nodeOrdinal;
            record.HasLastNodeOrdinal = true;
            if (!string.IsNullOrEmpty(eventInstanceId)) record.LastEventInstanceId = eventInstanceId;
        }

        /// <summary>
        /// 【测试别名】与 <see cref="RecordOccurrence"/> 完全等价（同一实现）；
        /// 保留它只是为了让 1B 的会话历史夹具用例保持可读。
        /// </summary>
        public void SeedOccurrenceForTesting(string eventId, int nodeOrdinal)
        {
            RecordOccurrence(eventId, nodeOrdinal, null);
        }

        private ExpeditionEventOccurrenceRecord Find(string eventId)
        {
            if (string.IsNullOrEmpty(eventId)) return null;

            ExpeditionEventOccurrenceRecord record;
            return _records.TryGetValue(eventId, out record) ? record : null;
        }
    }

    /// <summary>
    /// **Event World State** —— 世界级事件标记（Flags）+ 世界长期事件历史（WP-Event-1B §5 / 1C §12）。
    ///
    /// 边界：
    ///   - Flag 是 **Event Core 自己拥有**的状态（不像资源 / 补给那样属于别的 Authority）；
    ///   - 但它的 mutation 仍然只发生在 **Effect Transaction 的 Commit 阶段**（不得在 Preflight 提前改）；
    ///   - SetFlag / ClearFlag 都是**幂等**的；
    ///   - 本轮（1C）**不进 Save**：Flags / History 仍只存在于 Runtime（§16）。
    /// </summary>
    public sealed partial class EventWorldState
    {
        private readonly HashSet<string> _flags = new HashSet<string>(StringComparer.Ordinal);

        public EventWorldState()
        {
            History = new WorldEventHistory();
        }

        /// <summary>世界长期事件历史（`EventNotSeen` / `EventSeenCountAtLeast` / `OncePerWorld` / `VillageDays`）。</summary>
        public WorldEventHistory History { get; private set; }

        public bool IsFlagSet(string flag)
        {
            return !string.IsNullOrEmpty(flag) && _flags.Contains(flag);
        }

        /// <summary>全部已设 Flag（按键规范排序；仅诊断 / 测试用）。</summary>
        public IReadOnlyList<string> Flags
        {
            get
            {
                var list = new List<string>(_flags);
                list.Sort(StringComparer.Ordinal);
                return list;
            }
        }

        /// <summary>
        /// **正式写入口（WP-Event-1C）**：设置世界事件标记（幂等）。
        /// 只在 Effect Transaction 的 Commit 阶段调用；<see cref="EventResolver"/> 绝不调用。
        /// </summary>
        public void SetFlag(string flag)
        {
            if (!string.IsNullOrEmpty(flag)) _flags.Add(flag);
        }

        /// <summary>
        /// **正式写入口（WP-Event-1C）**：清除世界事件标记（幂等：不存在也算成功）。
        /// 只在 Effect Transaction 的 Commit 阶段调用。
        /// </summary>
        public void ClearFlag(string flag)
        {
            if (!string.IsNullOrEmpty(flag)) _flags.Remove(flag);
        }

        /// <summary>【测试别名】与 <see cref="SetFlag"/> 等价（同一实现）。</summary>
        public void SeedFlagForTesting(string flag)
        {
            SetFlag(flag);
        }

        /// <summary>【测试别名】与 <see cref="ClearFlag"/> 等价（同一实现）。</summary>
        public void SeedClearFlagForTesting(string flag)
        {
            ClearFlag(flag);
        }
    }
}
