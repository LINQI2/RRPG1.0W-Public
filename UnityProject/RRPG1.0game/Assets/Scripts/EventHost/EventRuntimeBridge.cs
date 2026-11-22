using System;
using RRPG.Event;
using RRPG.StaticConfig;

/// <summary>
/// **EventRuntimeBridge（产品层唯一 Event World Host）** —— WP-Event-1D §2。
///
/// 职责（且仅此）：持有**长期 Event 世界状态**，并把它接进 Save / Load。
///
/// | 持有 | 不持有 |
/// |---|---|
/// | `EventWorldState`（Flags + World Event History） | Person（仍属 Person Authority / PlayerCollection） |
/// | 当前 Pending Village `ResolvedEventInstance`（至多一条，§12） | Village Resource（仍属 VillageResourceState） |
/// | Village Event 的 `EventExecutionState`（Runtime exactly-once） | Expedition 状态 / Session History（属本次远征会话，§5） |
/// | — | Static Event Config（仍从 `StaticConfigRegistry` 读取） |
///
/// 边界：
///   - 本类是**产品层**（Assembly-CSharp）：Event.Runtime 保持纯逻辑，不知道 Save / Unity；
///   - `ExecutionState` 的 Completed 集合**不持久化**（§4）：Load 后从零开始；
///     长期重复规则一律由 World Event History 负责；
///   - Expedition Session History 由调用方（远征会话）自持，不进入本 Host。
/// </summary>
public static class EventRuntimeBridge
{
    /// <summary>长期世界事件状态（Flags + World Event History）。</summary>
    public static EventWorldState World { get; private set; }

    /// <summary>Village Event 的 exactly-once 实例守卫（Runtime Only；不进 Save）。</summary>
    public static EventExecutionState Execution { get; private set; }

    /// <summary>当前待处理的 Village Event（至多一条；null = 无）。</summary>
    public static ResolvedEventInstance PendingVillageEvent { get; private set; }

    /// <summary>最近一次 Pending 相关操作的可操作诊断（无则 null）。</summary>
    public static string LastPendingDiagnostic { get; private set; }

    public static bool HasPendingVillageEvent => PendingVillageEvent != null;

    /// <summary>幂等初始化（Save 前 / 产品进入 Village 前调用）。</summary>
    public static void EnsureInitialized()
    {
        if (World == null) World = new EventWorldState();
        if (Execution == null) Execution = new EventExecutionState();
    }

    // ==================== Save / Load ====================

    public static EventWorldSaveData BuildSaveData()
    {
        EnsureInitialized();
        return EventWorldPersistence.Build(World, PendingVillageEvent);
    }

    /// <summary>
    /// 从存档恢复。语义：
    ///   - World State（Flags / History）**精确写回**；
    ///   - Pending Village Event 完整还原（EventInstanceId / 绑定 / Context 不变 → Outcome 不重抽）；
    ///   - `ExecutionState` 重建为空（Completed 不持久化，§4）。
    /// </summary>
    public static void Restore(EventWorldSaveData data)
    {
        EnsureInitialized();

        EventWorldPersistence.Restore(World, data);
        Execution = new EventExecutionState();

        PendingVillageEvent = EventWorldPersistence.FromSaveData(data != null ? data.PendingVillageEvent : null);
        LastPendingDiagnostic = null;
    }

    // ==================== Pending Village Event（至多一条，§12） ====================

    /// <summary>
    /// 建立 Pending Village Event。
    /// **V0.1 至多一条**：已存在 Pending → 返回 false + `PendingExists` 诊断
    /// （不排队、不覆盖、不写 History —— 本次 Recognition 不产生事件）。
    /// </summary>
    public static bool SetPendingVillageEvent(ResolvedEventInstance instance, out string diagnostic)
    {
        EnsureInitialized();

        if (instance == null)
        {
            diagnostic = "InvalidInstance：待处理事件实例为 null。";
            LastPendingDiagnostic = diagnostic;
            return false;
        }

        if (PendingVillageEvent != null)
        {
            diagnostic = "PendingExists：已有未处理的 Village Event（" + PendingVillageEvent.EventId + " / "
                + PendingVillageEvent.EventInstanceId + "）；V0.1 不建立事件队列，本次 Recognition 不产生新事件。";
            LastPendingDiagnostic = diagnostic;
            return false;
        }

        PendingVillageEvent = instance;
        LastPendingDiagnostic = null;
        diagnostic = string.Empty;
        return true;
    }

    /// <summary>清除 Pending（只在事件成功结算 / 明确放弃时调用）。</summary>
    public static bool ClearPendingVillageEvent()
    {
        if (PendingVillageEvent == null) return false;

        PendingVillageEvent = null;
        LastPendingDiagnostic = null;
        return true;
    }

    /// <summary>
    /// Load 后校验：Pending 事件的 `EventId` 是否仍在**当前** Static Config 里（§3：EventId → 当前配置）。
    /// 返回 false 时 <paramref name="diagnostic"/> 说明原因（不静默丢弃 Pending）。
    /// </summary>
    public static bool ValidatePendingAgainstStaticConfig(out string diagnostic)
    {
        diagnostic = string.Empty;
        if (PendingVillageEvent == null) return true;

        EventConfigDatabase database;
        string configDiagnostic;
        if (!StaticConfigRegistry.TryGetEventConfig(out database, out configDiagnostic))
        {
            diagnostic = "ConfigUnavailable：Pending 事件 " + PendingVillageEvent.EventId
                + " 无法校验（" + configDiagnostic + "）。";
            return false;
        }

        EventDefinition definition;
        if (!database.TryGetEvent(PendingVillageEvent.EventId, out definition))
        {
            diagnostic = "EventRemoved：Pending 事件 " + PendingVillageEvent.EventId
                + " 已不在当前静态配置中（内容被改名 / 删除）→ 不能静默丢弃，请协调层裁决。";
            return false;
        }

        return true;
    }

    public static void ResetForTests()
    {
        World = null;
        Execution = null;
        PendingVillageEvent = null;
        LastPendingDiagnostic = null;
    }
}
