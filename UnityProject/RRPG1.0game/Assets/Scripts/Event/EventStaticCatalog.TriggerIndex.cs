using System;
using System.Collections.Generic;
using RRPG.StaticConfig;

namespace RRPG.Event
{
    /// <summary>EventStaticCatalog：启用触发索引与通用索引工具（WP-Event-1B）。</summary>
    public sealed partial class EventStaticCatalog
    {
        /// <summary>为每个启用的（TriggerType, EventId）建立索引；同一事件同一类型只登记一次。</summary>
        private void BuildEnabledTriggerIndex()
        {
            foreach (var eventRecord in _events)
            {
                if (eventRecord == null || !eventRecord.Enabled) continue;

                var triggers = Get(_triggersByEvent, eventRecord.EventId);
                var addedTypes = new HashSet<string>(StringComparer.Ordinal);

                foreach (var trigger in triggers)
                {
                    if (trigger == null || string.IsNullOrEmpty(trigger.TriggerType)) continue;
                    if (!addedTypes.Add(trigger.TriggerType)) continue;

                    Add(_enabledByTriggerType, trigger.TriggerType, eventRecord);
                }
            }

            SortAll(_enabledByTriggerType, (a, b) =>
                string.CompareOrdinal(a.EventId ?? string.Empty, b.EventId ?? string.Empty));
        }

        private static void Add<T>(Dictionary<string, List<T>> index, string key, T value)
        {
            if (string.IsNullOrEmpty(key)) return;

            List<T> list;
            if (!index.TryGetValue(key, out list))
            {
                list = new List<T>();
                index.Add(key, list);
            }
            list.Add(value);
        }

        private static void SortAll<T>(Dictionary<string, List<T>> index, Comparison<T> comparison)
        {
            foreach (var list in index.Values) list.Sort(comparison);
        }
    }
}
