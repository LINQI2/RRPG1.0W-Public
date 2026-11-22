using System;
using System.Collections.Generic;

namespace RRPG.Event
{
    /// <summary>Event World 存档映射：Restore（DTO → runtime）。</summary>
    public static partial class EventWorldPersistence
    {
        /// <summary>把 DTO 写回 World State（清空后精确恢复；null → 清空为空状态）。</summary>
        public static void Restore(EventWorldState world, EventWorldSaveData data)
        {
            if (world == null) return;

            world.ClearFlags();
            world.History.ClearAll();

            if (data == null) return;

            if (data.Flags != null)
            {
                foreach (string flag in data.Flags)
                {
                    world.SetFlag(flag);
                }
            }

            if (data.History == null) return;

            foreach (var entry in data.History)
            {
                if (entry == null || string.IsNullOrEmpty(entry.EventId)) continue;
                if (entry.OccurrenceCount <= 0) continue;

                world.History.RestoreRecord(WorldEventHistory.CreateRecord(entry.EventId,
                    entry.OccurrenceCount, entry.LastOccurredVillageDayOrdinal, entry.HasLastVillageDay,
                    entry.LastEventInstanceId));
            }
        }

        /// <summary>
        /// 把 Pending DTO 还原为 <see cref="ResolvedEventInstance"/>（身份 / 绑定 / Context 不变）。
        /// 残缺（缺 Id / EventId / TriggerType / TriggerKey）→ null（不猜、不造）。
        /// </summary>
        public static ResolvedEventInstance FromSaveData(PendingEventInstanceSaveData data)
        {
            if (data == null) return null;
            if (string.IsNullOrEmpty(data.EventInstanceId) || string.IsNullOrEmpty(data.EventId)) return null;
            if (string.IsNullOrEmpty(data.TriggerType) || string.IsNullOrEmpty(data.TriggerKey)) return null;

            var bindings = new List<EventRoleBinding>();
            if (data.RoleBindings != null)
            {
                foreach (var binding in data.RoleBindings)
                {
                    if (binding == null || string.IsNullOrEmpty(binding.RoleId)) continue;
                    if (string.IsNullOrEmpty(binding.PersonId)) continue;

                    bindings.Add(new EventRoleBinding(binding.RoleId, binding.RoleName, binding.SelectorType,
                        binding.PersonId, binding.SourceKey));
                }
            }

            var entries = new List<EventContextEntry>();
            if (data.ContextSnapshot != null)
            {
                foreach (var entry in data.ContextSnapshot)
                {
                    if (entry == null || string.IsNullOrEmpty(entry.Key)) continue;
                    if (entry.Kind < 0 || entry.Kind > (int)EventContextEntryKind.Person) continue;

                    entries.Add(new EventContextEntry((EventContextEntryKind)entry.Kind, entry.Key,
                        entry.StringValue, entry.IntValue));
                }
            }

            return new ResolvedEventInstance(data.EventInstanceId, data.EventId, data.TriggerType, data.TriggerKey,
                data.ResolutionSeed, bindings, new EventContextSnapshot(entries));
        }
    }
}
