using RRPG.Event;

/// <summary>
/// **VillageEventOptionView（选项视图 DTO）** —— WP-Event-1D Gate D1 §13/§14。
///
/// 定位：把 Event Core 的求值结果（`EventOptionDefinition` + `EventOptionEvaluator` 的可用性）
/// 投影成**UI 只做渲染**所需的行数据。
///
/// 硬约束：
///   - UI **不得**自己重新实现条件判断（可用性一律来自 `EventOptionEvaluator`）；
///   - 可用性语义按 `UnavailableMode`：`Hidden` 不进入本列表（UI 隐藏整条），
///     `Disabled` 进入列表但 `IsSelectable=false`；
///   - `Invalid`（配置 / 运行时异常）进入列表但不可选 + 带可操作诊断 —— 显式失败，不静默隐藏。
/// </summary>
public sealed class VillageEventOptionView
{
    public string OptionId;
    public int SortOrder;

    /// <summary>渲染后的选项文案（来自 `LabelTemplate`；渲染失败 → 明确的不可选诊断行）。</summary>
    public string Label;

    /// <summary>渲染后的辅助说明（`SubLabelTemplate`；空则空串）。</summary>
    public string SubLabel;

    public EventOptionAvailability Availability;

    /// <summary>玩家当前是否可以选择（只有 `Available` 为 true）。</summary>
    public bool IsSelectable;

    /// <summary>可操作中文诊断（可用时为空串）。</summary>
    public string Diagnostic;

    public override string ToString()
    {
        return OptionId + "[" + Availability + "] " + Label;
    }
}
