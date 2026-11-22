using System;
using System.Collections.Generic;

namespace RRPG.Event
{
    /// <summary>
    /// **Event World 存档映射**（WP-Event-1D §3 / §9）：纯逻辑，双向映射
    /// `EventWorldState + Pending ResolvedEventInstance` ⇄ `EventWorldSaveData`。
    ///
    /// 语义：
    ///   - 恢复是**精确写回**（计数 / 最近日序 / 最近实例），不是 +1；
    ///   - Pending 实例恢复后 **EventInstanceId / 角色绑定 / Context 快照完全不变**
    ///     → Load 之后 Outcome 仍由 `EventInstanceId + OptionId` 派生，**不会重抽**；
    ///   - 非法 / 残缺条目一律丢弃（不猜、不伪造）。
    /// </summary>
    public static partial class EventWorldPersistence
    {
        // ==================== Build（runtime → DTO） ====================

        public static EventWorldSaveData Build(EventWorldState world, ResolvedEventInstance pendingVillageEvent)
        {
            var data = new EventWorldSaveData
            {
                Flags = new List<string>(),
                History = new List<EventHistorySaveData>(),
                PendingVillageEvent = ToSaveData(pendingVillageEvent)
            };

            if (world == null) return data;

            foreach (string flag in world.Flags)
            {
                if (!string.IsNullOrEmpty(flag)) data.Flags.Add(flag);
            }

            foreach (var record in world.History.Records)
            {
                if (record == null || string.IsNullOrEmpty(record.EventId)) continue;
                data.History.Add(new EventHistorySaveData
                {
                    EventId = record.EventId,
                    OccurrenceCount = record.OccurrenceCount,
                    LastOccurredVillageDayOrdinal = record.LastOccurredVillageDayOrdinal,
                    HasLastVillageDay = record.HasLastVillageDay,
                    LastEventInstanceId = record.LastEventInstanceId
                });
            }

            return data;
        }

        public static PendingEventInstanceSaveData ToSaveData(ResolvedEventInstance instance)
        {
            if (instance == null) return null;

            var data = new PendingEventInstanceSaveData
            {
                EventInstanceId = instance.EventInstanceId,
                EventId = instance.EventId,
                TriggerType = instance.TriggerType,
                TriggerKey = instance.TriggerKey,
                ResolutionSeed = instance.ResolutionSeed,
                RoleBindings = new List<RoleBindingSaveData>(),
                ContextSnapshot = new List<ContextEntrySaveData>()
            };

            foreach (var binding in instance.RoleBindings)
            {
                if (binding == null) continue;
                data.RoleBindings.Add(new RoleBindingSaveData
                {
                    RoleId = binding.RoleId,
                    RoleName = binding.RoleName,
                    SelectorType = binding.SelectorType,
                    PersonId = binding.PersonId,
                    SourceKey = binding.SourceKey
                });
            }

            var snapshot = instance.ContextSnapshot;
            if (snapshot != null)
            {
                foreach (var entry in snapshot.Entries)
                {
                    if (entry == null) continue;
                    data.ContextSnapshot.Add(new ContextEntrySaveData
                    {
                        Kind = (int)entry.Kind,
                        Key = entry.Key,
                        StringValue = entry.StringValue,
                        IntValue = entry.IntValue
                    });
                }
            }

            return data;
        }
    }
}
