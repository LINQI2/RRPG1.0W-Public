using System;
using System.Collections.Generic;
using System.Globalization;
using RRPG.Deterministic;
using RRPG.StaticConfig;

namespace RRPG.Event
{
    /// <summary>EventResolver：权重抽取、实例身份、内部状态（WP-Event-1B）。</summary>
    public static partial class EventResolver
    {
        /// <summary>
        /// 按 Event.Weight 确定性抽取一个候选（§18）。
        /// 权重只决定 Event 之间的相对选择：候选池里每个 Event **恰好一条**
        /// （与合法 Binding 数量 / Condition 行数无关）。
        /// </summary>
        private static EventCandidate SelectByWeight(ResolveContext ctx, List<EventCandidate> candidates)
        {
            var ordered = new List<EventCandidate>(candidates);
            ordered.Sort((a, b) => string.CompareOrdinal(a.Event.EventId, b.Event.EventId));

            var eventList = new List<EventDefinition>(ordered.Count);
            var weightList = new List<int>(ordered.Count);
            foreach (var candidate in ordered)
            {
                eventList.Add(candidate.Event);
                weightList.Add(candidate.Event.Weight);
            }

            var selectionRng = new DeterministicRandom(ctx.Request.ResolutionSeed)
                .Derive("event-select|" + ctx.Request.TriggerType);

            var order = selectionRng.WeightedPermutation(eventList, weightList);
            if (order.Count == 0) return null;

            var selected = order[0];
            for (int i = 0; i < ordered.Count; i++)
            {
                if (ReferenceEquals(ordered[i].Event, selected)) return ordered[i];
            }
            return null;
        }

        /// <summary>候选事件（模板 + 已求出的角色绑定）。</summary>
        private sealed class EventCandidate
        {
            public EventDefinition Event { get; private set; }
            public List<EventRoleBinding> RoleBindings { get; private set; }

            public EventCandidate(EventDefinition eventRecord, List<EventRoleBinding> roleBindings)
            {
                Event = eventRecord;
                RoleBindings = roleBindings ?? new List<EventRoleBinding>();
            }
        }

        /// <summary>
        /// EventInstanceId（§20）：非 Guid、不用时间；由 `TriggerKey + EventId + TriggerType + ResolutionSeed`
        /// 稳定哈希（FNV-1a，与共享 RNG 同一算法）而来 —— **不等于** EventId，
        /// 且同一个 TriggerKey 在相同输入下重复解析得到同一个 Id。
        /// </summary>
        public static string BuildEventInstanceId(string triggerKey, string eventId, string triggerType, int seed)
        {
            ulong hash = DeterministicRandom.StableHash64(
                (triggerKey ?? string.Empty) + "\u001f"
                + (eventId ?? string.Empty) + "\u001f"
                + (triggerType ?? string.Empty) + "\u001f"
                + seed.ToString(CultureInfo.InvariantCulture));

            return "evtinst_" + hash.ToString("x16", CultureInfo.InvariantCulture);
        }

        /// <summary>一次 Resolve 调用的内部状态（实现细节，不公开）。</summary>
        private sealed class ResolveContext
        {
            public EventResolutionRequest Request;
            public EventStaticCatalog Catalog;
            public IEventWorldView World;
            public EventWorldState WorldState;
            public ExpeditionEventSessionState SessionState;
            public EventResolverOptions Options;
            public EventResolutionTrace Trace;

            /// <summary>已评估的 (Role, 候选 Person) 组合次数（Anti-Hang 计数）。</summary>
            public int SearchNodes;

            /// <summary>本次是否触发了搜索上限。</summary>
            public bool SearchLimitExceeded;

            /// <summary>触发上限的事件（诊断用）。</summary>
            public string LimitExceededEventId;

            /// <summary>运行时不变量被破坏时的可操作原因（整次 Resolve 以 InvalidRuntimeConfig 终止）。</summary>
            public string InvalidConfigReason;
        }
    }
}
