/// <summary>
/// **EventOptionFeasibilityPreview（Option 可行性只读预测结果）** —— WP-Event-1 Producer Acceptance Repair §Issue C。
///
/// 用途：在玩家**点击之前**，用**只读**方式证明「这个 Option 当前必然无法执行」，
/// 让卡面直接把 Option 置灰并显示简短原因，而不是等玩家点击后由 Executor 的 Preflight 报错。
///
/// 硬约束：
///   - 只读：不 Commit、不写 History、不改变 Outcome、不消耗 / 改变 RNG、不修改任何状态；
///   - **只用于「置灰」**：`Applicable == false` 表示**证明不了**必然失败（例如需要预生成阵容的 StartBattle、
///     阶段不是 AwaitingOption、Outcome 不确定）→ 调用方必须保持原可用性判断，**不得**据此置灰；
///   - 判定逻辑来自既有只读能力（`EventOptionExecutor.PreviewOutcome` +
///     `ExpeditionEventFlow.TryEvaluateSettlementFeasibility`），**不复制**第二套判定。
///
/// 条件变化（补给 / 时间 / 条件）后**重新构造卡面**即可重新评估 → Option 可以重新 Available。
/// </summary>
public struct EventOptionFeasibilityPreview
{
    /// <summary>是否得到了确定性结论（false = 证明不了必然失败 → 不得据此置灰）。</summary>
    public bool Applicable;

    /// <summary>`Applicable == true` 时有效：该 Option 当前是否（必然）可执行。</summary>
    public bool Feasible;

    /// <summary>玩家可读的短原因（仅在 `Applicable &amp;&amp; !Feasible` 时非空）。</summary>
    public string PlayerReason;

    /// <summary>内部诊断（与 Executor Preflight 同源；用于日志 / 报告，不直接给玩家）。</summary>
    public string Diagnostic;

    /// <summary>有序投影后的补给（与 Preflight 同一公式）。</summary>
    public int ProjectedSupply;

    /// <summary>有序投影后的额外天数（含已累积的 PendingExtraElapsedDays）。</summary>
    public int PendingExtraDays;

    /// <summary>本 Node 结算总成本（party × 总时间 × rate + ExtraSupplyCost）。</summary>
    public int TotalCost;

    /// <summary>
    /// 玩家可读短原因（纯函数；与 Preflight 诊断同源、但只说「差多少」）。
    /// 例：`补给不足：本次行动至少还需要 2 点补给`
    /// </summary>
    public static string BuildPlayerReason(int projectedSupply, int totalCost)
    {
        int shortfall = totalCost - projectedSupply;
        if (shortfall <= 0) shortfall = 1;   // 防御：理论上不会发生（不可行才调用）
        return "补给不足：本次行动至少还需要 " + shortfall + " 点补给";
    }
}
