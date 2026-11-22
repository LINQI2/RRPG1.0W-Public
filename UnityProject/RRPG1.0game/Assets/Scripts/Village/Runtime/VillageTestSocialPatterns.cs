/// <summary>
/// VillageTestSocialPatterns —— V3B 第一个 Recognition Pattern 的 Test / Not Frozen 规则目录。
///
/// 本目录是「3 次 / 自然年窗口 / 休整至当月末」等数值与窗口的唯一承载位置；
/// VillageSocialResponseLog / VillageRuntimeBridge 等 Runtime 结构不得硬编码这些产品语义。
/// 窗口与阈值尚未冻结（Village-GameSpec-V0.1 §8）：任何数值升级正式设计前不得移出本类。
/// </summary>
public static class VillageTestSocialPatterns
{
    /// <summary>Pattern 标识：同一 Person 在同一年内反复被从 Primary Assignment 抽去 Project 临时工程。</summary>
    public const string BuildupDraftPatternKey = "TemporaryDraftBuildup";

    /// <summary>【Test / Not Frozen】成立 Recognition 的年度 Temporary Commitment 阈值（3 次）。</summary>
    public static int BuildupDraftThreshold => 3;

    /// <summary>【Test / Not Frozen】Pattern 计数窗口描述：自然年（VillageTime.Year）。</summary>
    public const string WindowDescription = "自然年（Test / Not Frozen）";

    /// <summary>
    /// 【Test / Not Frozen】Choice A 的恢复窗口：
    /// 至本自然月最后一天（含当天仍被 Rest Gate 拦截）；CurrentDate &gt; Until（即下月 1 日）起恢复。
    /// </summary>
    public const string RestWindowDescription = "至本月最后一天（含当天仍拦截；下月 1 日起恢复，Test / Not Frozen）";

    // ==================== V3C：Event Urgency / PauseOnOpen 最小语义（Test / Not Frozen） ====================
    // 只做「需要及时处理 / 可忽略」两种最小区分；不建 Event 队列、不做 Deadline Scheduler、
    // 不做 Ignore Penalty、不做多事件排队、不做 Notification Framework、不建新 Event System。

    /// <summary>
    /// 【Test / Not Frozen】Case A（TemporaryDraftBuildup）当前使用的 Urgency 分类：
    /// 该回应正在堵塞玩家的下一次调度决定 → 需要及时处理。
    /// </summary>
    public static VillageSocialUrgency BuildupDraftUrgency => VillageSocialUrgency.Timely;

    /// <summary>
    /// 【Test / Not Frozen】PauseOnOpen 默认值：需要及时处理的事件「打开即暂停 VillageTime」。
    /// 最终值可由 UI Inspector（VillageSocialEventUI.pauseOnOpen）覆盖；可忽略事件恒不强制暂停。
    /// </summary>
    public const bool DefaultPauseOnOpen = true;

    /// <summary>
    /// 【Test / Not Frozen】按 PatternKey 暴露该 Event 的最小 Urgency 语义（Event 携带 / 暴露的唯一来源）。
    /// 未知 PatternKey → 安全默认 Timely（不静默降级为「可忽略」）。
    /// </summary>
    public static VillageSocialUrgency ResolveUrgency(string patternKey)
    {
        if (patternKey == BuildupDraftPatternKey) return BuildupDraftUrgency;
        return VillageSocialUrgency.Timely;
    }

    /// <summary>
    /// 【Test / Not Frozen】PauseOnOpen 最小策略：可忽略（Ignorable）事件绝不强制暂停；
    /// 其余按配置值（configuredPauseOnOpen）。本方法不引入任何 Deadline / 计时 / 惩罚。
    /// </summary>
    public static bool ShouldPauseOnOpen(bool configuredPauseOnOpen, VillageSocialUrgency urgency)
    {
        if (urgency == VillageSocialUrgency.Ignorable) return false;
        return configuredPauseOnOpen;
    }
}
