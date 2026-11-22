using System.Collections.Generic;

/// <summary>
/// Expedition Return 相位（V4F Home↔Expedition Continuity Foundation；Test / Not Frozen）。
/// 状态机（Final Development Spec 修订 2）：
///   Active → ReturnSettled（Settlement 成功）→ CatchUpComplete（Catch-up 逐日全部完成）
///   → OccupancyCleared（Clear Expedition Occupancy）→ Completed（最终 Return Save 成功）。
///   Failed：Catch-up 某日中途失败（部分效果可能已写入）——禁止自动续跑该日，恢复 = 丢弃记录 + 从最后一次成功 Save 重开。
/// </summary>
public enum ExpeditionReturnPhase
{
    Active = 0,
    ReturnSettled = 1,
    CatchUpComplete = 2,
    OccupancyCleared = 3,
    Completed = 4,
    Failed = 5
}

/// <summary>
/// VillageRuntimeBridge.RunReturnCatchUp 的返回结果（V4F；Test / Not Frozen）。
/// 语义：
///   - Completed / AlreadyCaughtUp：逐日补算已完成（或 0 天空跑），可继续 Clear → 最终 Return Save；
///   - AlreadyCompleted：记录 Completed=true（跨调用 exactly-once 终态），再次 Return → no-op；
///   - RejectedReentrant：IsReturnCatchUpRunning 期间再次触发（防重入，不承载完成语义）；
///   - RejectedFailedRecord：存在 Failed 记录，禁止自动续跑该日；
///   - InvalidPhase：Settlement 尚未成功（Phase != ReturnSettled）即触发补算；
///   - OverlapRejected：Catch-up 每日占用检查发现 Project 参与者与远征者重叠（互斥红线，拒推进）；
///   - RetryableFailure：两个完整 Day 之间失败（无部分写入），可从 DaysSimulated 安全续跑；
///   - PartialDayFailure：某 Day 内部分效果已写入，当日不计入 DaysSimulated，记录转 Failed。
/// </summary>
public enum ExpeditionCatchUpResult
{
    Completed,
    AlreadyCaughtUp,
    AlreadyCompleted,
    RejectedReentrant,
    RejectedFailedRecord,
    InvalidPhase,
    OverlapRejected,
    RetryableFailure,
    PartialDayFailure,
    Yielded,
    WaitingForInteraction,
    InvalidTimeline
}

/// <summary>
/// VillageRuntimeBridge.ReturnRemainingCarryStage 的结果（WP-B Return Integrity；Test / Not Frozen）。
/// 语义（I-6：「成功 / 已应用 / 拒绝」三态，只有前两者可推进下一阶段）：
///   - Applied：本次真实发生一次返还（Resource +remainingCarryUnits）；
///   - AlreadyApplied：本 Return 会话内已返还过（exactly-once 幂等，可继续后续阶段）；
///   - Rejected：守卫拒绝（无会话 / Completed / Failed / 未越过 Catch-up / 资源不可用 / 非法数值）→ 必须 Fail Closed。
/// </summary>
public enum ReturnCarryStageResult
{
    Applied,
    AlreadyApplied,
    Rejected
}

/// <summary>
/// 一次 Expedition Return 的会话记录（V4F）。
///
/// 只存在 Return 会话生命周期内（随 Return 开始创建，随最终 Return Save 成功 / 下一次出征登记 / ResetForTests 结束），
/// 不持久化、不进 SaveData、不提升 SaveVersion。字段均为 Test / Not Frozen（elapsed-days 正式公式未冻结）。
///
/// 职责边界（修订 2，与 IsReturnCatchUpRunning 分离）：
///   - IsReturnCatchUpRunning（Bridge 进程内瞬态）只防 Reentrant / 并发，finally 复位，不承载完成语义；
///   - 本记录 Completed 是跨调用 exactly-once 终态，仅在最终 Return Save 成功后置 true；
///     任何后续 Return / Catch-up 入口见 Completed==true → no-op。
/// </summary>
public class ExpeditionRecord
{
    public long DepartureWorldMinute { get; }
    public long SettledElapsedMinutes { get; }
    public string SessionIdentity { get; }
    private readonly System.Func<int> _completedDays;
    public ExpeditionRecord() { }
    public ExpeditionRecord(long departure, long elapsed, string identity, System.Func<int> completedDays)
    { DepartureWorldMinute = departure; SettledElapsedMinutes = elapsed; SessionIdentity = identity; _completedDays = completedDays; }
    /// <summary>本次 Return 覆盖的远征者 PersonId 快照（BeginExpeditionReturn 时从 Bridge 当前远征登记快照复制）。</summary>
    public List<string> PersonIds = new List<string>();

    /// <summary>离村日期（Time 当前日期；测试/会话记录用，Test / Not Frozen）。</summary>
    public int DepartureYear;
    public int DepartureMonth;
    public int DepartureDay;

    /// <summary>需要逐日 Catch-up 的有效天数（elapsed-days 公式未冻结；0 = 空跑）。</summary>
    public int EffectiveElapsedDays;

    /// <summary>当前 Return 相位（见 ExpeditionReturnPhase）。</summary>
    public ExpeditionReturnPhase Phase = ExpeditionReturnPhase.Active;

    /// <summary>已完整执行（DaysSimulated 已推进）的 Catch-up 天数；失败日绝不推进。</summary>
    public int DaysSimulated => _completedDays != null ? _completedDays() : 0;

    /// <summary>跨调用 exactly-once 终态标记；仅最终 Return Save 成功后由 Bridge 置 true。</summary>
    public bool Completed;

    /// <summary>
    /// WP-3（Gate 4）：剩余 Carry 是否已返还 Village（Session 内 exactly-once，属于本 Return Transaction Authority）。
    /// 不持久化、不进 VillageResourceState / UI；随 _returnRecord 生命周期重置（新出征登记 / ResetForTests）。
    /// 返还时机固定：Catch-up 全部完成后、Clear Occupancy 之前，由 VillageRuntimeBridge.ReturnRemainingCarry 置位。
    /// </summary>
    public bool RemainingCarryReturned;

    /// <summary>
    /// V4 Gate 2：本次 Return 的长期人物健康后果（Run HP → 长期 Injury）是否已提交（Session 内 exactly-once）。
    /// 不持久化、不进 SaveData；提交时机固定：Catch-up 全部成功之后、Clear Occupancy 之前。
    /// </summary>
    public bool HealthConsequenceCommitted;

    /// <summary>
    /// V4 Gate 2：本次 Return 的 Character Expedition Experience 是否已写入 PersonLifeHistory（Session 内 exactly-once）。
    /// 不持久化、不进 SaveData；提交时机固定：Catch-up 全部成功之后、Clear Occupancy 之前。
    /// </summary>
    public bool ExperienceCommitted;
}
