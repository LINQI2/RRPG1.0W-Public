using System;
using System.Collections.Generic;

namespace RRPG.Event
{
    /// <summary>世界事件历史中的一条记录（纯数据）。</summary>
    public sealed class WorldEventOccurrenceRecord
    {
        public string EventId { get; internal set; }
        public int OccurrenceCount { get; internal set; }

        /// <summary>最近一次发生的村庄绝对日序（从未发生 → 无意义，见 <see cref="HasLastVillageDay"/>）。</summary>
        public int LastOccurredVillageDayOrdinal { get; internal set; }

        public bool HasLastVillageDay { get; internal set; }

        /// <summary>最近一次发生的 EventInstanceId（同一次事件实例可追溯；旧数据可为空）。</summary>
        public string LastEventInstanceId { get; internal set; }

        public override string ToString()
        {
            return EventId + " ×" + OccurrenceCount
                + (HasLastVillageDay ? " lastDay=" + LastOccurredVillageDayOrdinal : " lastDay=-");
        }
    }

    /// <summary>
    /// **World Event History（世界长期事件历史）** —— WP-Event-1B §5。
    ///
    /// 语义支持：
    ///   - `OncePerWorld`：OccurrenceCount &gt; 0 → 不再进入候选；
    ///   - `VillageDays` 冷却：LastOccurredVillageDayOrdinal 与当前村庄日序比较；
    ///   - `EventNotSeen` / `EventSeenCountAtLeast`：**统一查询本对象**（世界长期历史）。
    ///
    /// 边界（本轮硬约束）：
    ///   - 这是**只读 + 测试种子**模型：正式写入（RecordOccurrence）属 **1C**（Option → Outcome →
    ///     Effect 全部成功之后）；
    ///   - Resolver **只读**：解析出事件不等于玩家已经选择并执行结果，因此绝不在解析时记 occurrence；
    ///   - **不进 Save**：1B 不修改 SaveData / SaveVersion / SaveSystem。
    /// </summary>
    public sealed partial class WorldEventHistory
    {
        private readonly Dictionary<string, WorldEventOccurrenceRecord> _records =
            new Dictionary<string, WorldEventOccurrenceRecord>(StringComparer.Ordinal);

        /// <summary>发生次数（未发生过 → 0）。</summary>
        public int OccurrenceCount(string eventId)
        {
            var record = Find(eventId);
            return record == null ? 0 : record.OccurrenceCount;
        }

        public bool HasOccurred(string eventId)
        {
            return OccurrenceCount(eventId) > 0;
        }

        /// <summary>最近一次发生的村庄日序（从未发生 → false）。</summary>
        public bool TryGetLastVillageDayOrdinal(string eventId, out int ordinal)
        {
            ordinal = 0;
            var record = Find(eventId);
            if (record == null || !record.HasLastVillageDay) return false;

            ordinal = record.LastOccurredVillageDayOrdinal;
            return true;
        }

        /// <summary>全部记录（按 EventId 规范排序）。</summary>
        public IReadOnlyList<WorldEventOccurrenceRecord> Records
        {
            get
            {
                var list = new List<WorldEventOccurrenceRecord>(_records.Values);
                list.Sort((a, b) => string.CompareOrdinal(a.EventId, b.EventId));
                return list;
            }
        }

        // ---------------- 正式写入口（1C）与测试种子 ----------------

        /// <summary>
        /// **正式写入口（WP-Event-1C）**：记录一次世界事件发生。
        ///
        /// 唯一合法调用方 = Event Option 的 Execution / Transaction 层，且只在
        /// 「Preflight 全部通过 → Commit 全部成功」**之后**调用（FDS §19 的时序冻结）。
        /// <see cref="EventResolver"/> **绝不**调用本方法（解析出事件 ≠ 玩家已选择并执行结果）。
        ///
        /// 说明：世界历史对 Village / Expedition 两种 Domain 的事件**都**计数（§14 / §15），
        /// 从而让 `EventNotSeen` / `EventSeenCountAtLeast` 能跨 Context 查询；
        /// 但 `VillageDays` 冷却仍只用于 Village Event（编译期已禁止用于 Expedition Event）。
        /// **不 invent Village 时间**：日序由调用方从 World View 传入。
        /// </summary>
        public void RecordOccurrence(string eventId, int villageDayOrdinal, string eventInstanceId = null)
        {
            if (string.IsNullOrEmpty(eventId)) return;

            var record = Find(eventId);
            if (record == null)
            {
                record = new WorldEventOccurrenceRecord
                {
                    EventId = eventId,
                    OccurrenceCount = 0,
                    LastOccurredVillageDayOrdinal = 0,
                    HasLastVillageDay = false,
                    LastEventInstanceId = null
                };
                _records.Add(eventId, record);
            }

            record.OccurrenceCount++;
            record.LastOccurredVillageDayOrdinal = villageDayOrdinal;
            record.HasLastVillageDay = true;
            if (!string.IsNullOrEmpty(eventInstanceId)) record.LastEventInstanceId = eventInstanceId;
        }

        /// <summary>
        /// 【测试别名】Fixture 种子：与 <see cref="RecordOccurrence"/> 完全等价（同一实现）。
        /// 保留它只是为了让 1B 的历史夹具用例保持可读；产品写入请直接使用 RecordOccurrence。
        /// </summary>
        public void SeedOccurrenceForTesting(string eventId, int villageDayOrdinal)
        {
            RecordOccurrence(eventId, villageDayOrdinal, null);
        }

        public bool HasRecord(string eventId)
        {
            return Find(eventId) != null;
        }

        private WorldEventOccurrenceRecord Find(string eventId)
        {
            if (string.IsNullOrEmpty(eventId)) return null;

            WorldEventOccurrenceRecord record;
            return _records.TryGetValue(eventId, out record) ? record : null;
        }
    }
}
