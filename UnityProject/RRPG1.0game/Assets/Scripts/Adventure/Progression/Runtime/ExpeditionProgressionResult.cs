/// <summary>
/// Node 内容最终 outcome（FP-V0.1 最小两值；Stage 2 由 Battle 结果等映射）。
/// 只要内容确实发生并得到最终 outcome（Victory / Defeat），该 Node 已发生的
/// NodeElapsedDays 与 Base Supply 都必须 exactly-once 结算。
/// </summary>
public enum ExpeditionNodeOutcome
{
    /// <summary>成功 outcome（产品映射 Battle Victory）→ 该 Node 加入 CompletedNodeIds，开放后继；
    /// 若 Node 显式 SuccessfulRouteTerminal=true 才置 RouteCompleted。</summary>
    Success,

    /// <summary>失败 outcome（产品映射 Battle Defeat）→ Resolved-but-not-Completed；
    /// 不开放普通后继；elapsed/supply 已在结算中 exactly-once 消耗。</summary>
    Failure
}

/// <summary>InitializeProgression 结果。</summary>
public enum ExpeditionProgressionInitResult
{
    Success,

    /// <summary>已经初始化；重复 initialize 为 no-op，不重置既有会话。</summary>
    AlreadyInitialized,

    /// <summary>传入 graph 为 null。</summary>
    NullGraph
}

/// <summary>TryEnterNode / ValidateEnterNode 结果。</summary>
public enum ExpeditionEnterResult
{
    Success,

    /// <summary>尚未 InitializeProgression。</summary>
    NotInitialized,

    /// <summary>NodeId 不存在。</summary>
    NoSuchNode,

    /// <summary>当前相位下该 Node 不是合法进入候选（非入口 / 非上一完成 Node 后继）。</summary>
    NotAvailableNow,

    /// <summary>该 Node 已进入（重复 Enter no-op）。</summary>
    AlreadyEntered,

    /// <summary>该 Node 已完成（不可再进入）。</summary>
    AlreadyCompleted,

    /// <summary>当前相位不允许进入（Entered 中有未结算内容 / FailedResolved / ReturnRequested）。</summary>
    WrongPhase,

    /// <summary>普通可预知成本 Node：进入前 Supply affordability validation 失败；
    /// 内容未启动、无任何结算。Return 不受本结果阻塞。</summary>
    InsufficientSupply
}

/// <summary>TryResolveCurrentNode 结果（Node 内容 outcome 的唯一结算入口）。</summary>
public enum ExpeditionResolveResult
{
    Success,

    /// <summary>尚未 InitializeProgression。</summary>
    NotInitialized,

    /// <summary>当前无进入的 Node。</summary>
    NoCurrentNode,

    /// <summary>当前相位不是 Entered（未进入内容 / 已结算完成）。</summary>
    NotEntered,

    /// <summary>该 Node 已结算过最终 outcome；重复 Resolve no-op（elapsed/supply 不重复）。</summary>
    AlreadyResolved,

    /// <summary>结算时 Supply 不足（防御边界；正常路径在 Enter 前已被拒，不会出现「打完后才发现补给不够所以不算」）。</summary>
    InsufficientSupply,

    /// <summary>
    /// **WP-Event-1D**：`additionalElapsedDays &lt; 0`（Event 请求的额外远征时间为负）。
    /// 明确拒绝而不是夹取为 0：负数会让 elapsed / Supply 语义失真。
    /// </summary>
    InvalidAdditionalElapsedDays
}

/// <summary>RequestReturn 结果（Expedition 层独立操作，不受 Branch / Supply 阻塞）。</summary>
public enum ExpeditionReturnResult
{
    Success,

    /// <summary>尚未 InitializeProgression。</summary>
    NotInitialized,

    /// <summary>当前 Node 内容已进入但未产生最终 outcome；必须先 TryResolveCurrentNode 结算已发生成本后才能 Return。</summary>
    NodePendingResolution,

    /// <summary>已请求过 Return；重复 RequestReturn no-op。</summary>
    AlreadyRequested
}

/// <summary>TryResolveCurrentNode 成功时的结算明细（Test / Not Frozen）。</summary>
public class ExpeditionNodeResolutionData
{
    public long DurationMinutesSettled { get; internal set; }
    public long ExpeditionElapsedMinutesTotal { get; internal set; }
    /// <summary>被结算的 NodeId。</summary>
    public string NodeId { get; internal set; }

    /// <summary>产生本次结算的最终 outcome。</summary>
    public ExpeditionNodeOutcome Outcome { get; internal set; }

    /// <summary>本次结算计入的 NodeElapsedDays（integer Village Days；**不含** Event 额外时间）。</summary>
    public int NodeElapsedDaysSettled { get; internal set; }

    /// <summary>
    /// WP-Event-1D：本次结算额外计入的 Event 时间（`AdvanceExpeditionTime` 累计；无 Event 时为 0）。
    /// </summary>
    public int AdditionalElapsedDays { get; internal set; }

    /// <summary>
    /// WP-Event-1D：本次结算实际计入的总时间 = NodeElapsedDaysSettled + AdditionalElapsedDays。
    /// </summary>
    public int TotalElapsedDaysSettled { get; internal set; }

    /// <summary>Base Supply Consumption = PartySize × NodeElapsedDays × PerPersonDailyRate。</summary>
    public int BaseSupplyConsumption { get; internal set; }

    /// <summary>Explicit Extra Supply Cost（仅特殊内容）。</summary>
    public int ExtraSupplyCostApplied { get; internal set; }

    /// <summary>Total Supply Cost = Base + Extra。</summary>
    public int TotalSupplyCost { get; internal set; }

    /// <summary>结算后累计 ExpeditionElapsedDays。</summary>
    public int ExpeditionElapsedDaysTotal { get; internal set; }

    /// <summary>结算后当前 Carry。</summary>
    public int CurrentCarryUnits { get; internal set; }

    /// <summary>成功 outcome 时为 true；失败（Defeat）outcome 为 false（Resolved-not-Completed）。</summary>
    public bool Completed { get; internal set; }

    /// <summary>仅当显式 SuccessfulRouteTerminal 的成功 outcome 结算后为 true。</summary>
    public bool RouteCompleted { get; internal set; }
}
