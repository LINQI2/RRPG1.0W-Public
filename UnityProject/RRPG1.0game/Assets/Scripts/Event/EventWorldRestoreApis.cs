using System;
using System.Collections.Generic;

namespace RRPG.Event
{
    /// <summary>World Event History：1D 存档恢复用的显式写入口（清空 / 逐条写入精确值）。</summary>
    public sealed partial class WorldEventHistory
    {
        /// <summary>
        /// **存档恢复专用**：清空全部历史。
        /// 与 `RecordOccurrence`（+1）不同，恢复必须能写回**精确**计数，因此单独提供。
        /// </summary>
        public void ClearAll()
        {
            _records.Clear();
        }

        /// <summary>
        /// **存档恢复专用**：写入一条精确历史（非法条目由调用方过滤；本方法不做猜测）。
        /// 已存在同 EventId → 覆盖（恢复语义 = 以存档为准）。
        /// </summary>
        public void RestoreRecord(WorldEventOccurrenceRecord record)
        {
            if (record == null || string.IsNullOrEmpty(record.EventId)) return;

            _records[record.EventId] = record;
        }

        /// <summary>构造一条历史记录（供存档恢复；不做 +1）。</summary>
        public static WorldEventOccurrenceRecord CreateRecord(string eventId, int occurrenceCount,
            int lastVillageDayOrdinal, bool hasLastVillageDay, string lastEventInstanceId)
        {
            return new WorldEventOccurrenceRecord
            {
                EventId = eventId,
                OccurrenceCount = occurrenceCount,
                LastOccurredVillageDayOrdinal = lastVillageDayOrdinal,
                HasLastVillageDay = hasLastVillageDay,
                LastEventInstanceId = lastEventInstanceId
            };
        }
    }

    /// <summary>Expedition Session History：1D 存档恢复用的显式写入口。</summary>
    public sealed partial class ExpeditionEventSessionState
    {
        public void ClearAll()
        {
            _records.Clear();
        }

        public void RestoreRecord(ExpeditionEventOccurrenceRecord record)
        {
            if (record == null || string.IsNullOrEmpty(record.EventId)) return;

            _records[record.EventId] = record;
        }

        public static ExpeditionEventOccurrenceRecord CreateRecord(string eventId, int occurrenceCount,
            int lastNodeOrdinal, bool hasLastNodeOrdinal, string lastEventInstanceId)
        {
            return new ExpeditionEventOccurrenceRecord
            {
                EventId = eventId,
                OccurrenceCount = occurrenceCount,
                LastOccurredNodeOrdinal = lastNodeOrdinal,
                HasLastNodeOrdinal = hasLastNodeOrdinal,
                LastEventInstanceId = lastEventInstanceId
            };
        }
    }

    /// <summary>Event World State：Flags 的存档恢复入口（清空）。</summary>
    public sealed partial class EventWorldState
    {
        /// <summary>**存档恢复专用**：清空全部 Flag。</summary>
        public void ClearFlags()
        {
            _flags.Clear();
        }
    }
}
