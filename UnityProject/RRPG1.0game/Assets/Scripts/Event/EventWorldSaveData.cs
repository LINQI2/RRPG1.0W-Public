using System;
using System.Collections.Generic;

namespace RRPG.Event
{
    /// <summary>
    /// **Event World 存档 DTO**（WP-Event-1D §3；纯数据，不引用 SaveSystem / Unity）。
    ///
    /// 只保存**不可派生**的长期事实：
    ///   - World Flags；
    ///   - World Event History（EventId / 发生次数 / 最近村庄日序 / 最近 EventInstanceId）；
    ///   - 至多一条**未处理的 Village Event**（完整 `ResolvedEventInstance`：身份 + 触发 + 种子 + 角色绑定 + Context 快照）。
    ///
    /// 刻意**不保存**：EventDefinition 文本 / Options / Effects / Conditions（Load 后按 `EventId → 当前 Static Config` 读取）、
    /// Expedition Session History（Runtime Only，§5）、Completed Instance 集合（不做无限增长列表，§4）。
    /// </summary>
    [Serializable]
    public class EventWorldSaveData
    {
        /// <summary>世界事件标记（Flag key 列表）。</summary>
        public List<string> Flags;

        /// <summary>世界事件历史（每个发生过的事件一条）。</summary>
        public List<EventHistorySaveData> History;

        /// <summary>未处理的 Village Event（至多一条；null = 无 Pending）。</summary>
        public PendingEventInstanceSaveData PendingVillageEvent;
    }

    /// <summary>单个事件的世界历史存档条目。</summary>
    [Serializable]
    public class EventHistorySaveData
    {
        public string EventId;
        public int OccurrenceCount;

        /// <summary>最近一次发生的村庄绝对日序（无合法值时 HasLastVillageDay=false）。</summary>
        public int LastOccurredVillageDayOrdinal;
        public bool HasLastVillageDay;

        /// <summary>最近一次发生的 EventInstanceId（可空）。</summary>
        public string LastEventInstanceId;
    }

    /// <summary>一条未处理事件的完整存档（足以在 Load 后**不重抽**地继续）。</summary>
    [Serializable]
    public class PendingEventInstanceSaveData
    {
        public string EventInstanceId;
        public string EventId;
        public string TriggerType;
        public string TriggerKey;
        public int ResolutionSeed;

        public List<RoleBindingSaveData> RoleBindings;
        public List<ContextEntrySaveData> ContextSnapshot;
    }

    /// <summary>角色绑定存档条目（Role → Person）。</summary>
    [Serializable]
    public class RoleBindingSaveData
    {
        public string RoleId;
        public string RoleName;
        public string SelectorType;
        public string PersonId;
        public string SourceKey;
    }

    /// <summary>Context 快照条目存档（Kind 为 `EventContextEntryKind` 枚举序：0=String,1=Int,2=Person）。</summary>
    [Serializable]
    public class ContextEntrySaveData
    {
        public int Kind;
        public string Key;
        public string StringValue;
        public int IntValue;
    }
}
