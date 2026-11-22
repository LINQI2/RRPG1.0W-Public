using System.Collections.Generic;
using RRPG.Event;
using RRPG.Encounter;

/// <summary>
/// **ActiveExpeditionEventState（单次远征会话内的当前 Event Node 状态）** —— WP-Event-1D Gate D2 §3。
///
/// 唯一 Authority：一次远征中「当前正在处理的那个 Event Node 的 Unified Event Core 实例」只有这一份。
///
/// 生命周期（由 <see cref="ExpeditionEventFlow"/> / 产品入口执行）：
///   - Event Node 原子进入成功 → 建立（持有 **同一个** <see cref="ResolvedEventInstance"/> 对象）；
///   - UI 重开 / 卡片刷新 / 选项预览 → **复用**同一份（EventInstanceId / EventId / RoleBindings /
///     Context / Outcome 预览都不重抽）；
///   - 非战斗 Outcome 成功 + 玩家「继续」→ 结算当前 Node 后清除；
///   - StartBattle Outcome → 保持到 Battle Victory / Defeat 结算当前 Node 后清除；
///   - Retreat / Return / Expedition End → 清除。
///
/// 边界：**Runtime Only** —— 不进 Save、不进 Person、不进 RunState、不改变 SaveVersion；
/// 只保存「本 Node 事件身份 / 待结算额外时间 / Battle 预备阵容引用」这些运行时事实。
/// </summary>
public enum ExpeditionEventPhase
{
    /// <summary>无活动事件（正常态）。</summary>
    None = 0,

    /// <summary>事件卡已呈现，等待玩家选择 Option（尚未执行任何效果）。</summary>
    AwaitingOption = 1,

    /// <summary>Option 已成功执行（History / Instance 已完成），等待玩家「继续」结算当前 Node。</summary>
    AwaitingResultContinue = 2,

    /// <summary>Option 已成功执行且包含 StartBattle（Terminal Effect）：等待 Battle 最终 outcome 结算当前 Node。</summary>
    AwaitingBattleOutcome = 3
}

/// <summary>
/// **Active Expedition Event State**（见 <see cref="ExpeditionEventPhase"/> 的生命周期说明）。
/// </summary>
public sealed class ActiveExpeditionEventState
{
    /// <summary>本事件所属的 Progression NodeId（必须等于当前 CurrentNodeId 才允许结算）。</summary>
    public string NodeId;

    /// <summary>本次 Node 的已解析事件实例（**同一对象**贯穿卡片 / Option / 结算；不重抽）。</summary>
    public ResolvedEventInstance Instance;

    /// <summary>
    /// Event 请求的额外远征时间（`AdvanceExpeditionTime` 累计）。
    /// **不直接改 ExpeditionElapsedDays** —— 由
    /// `ExpeditionProgressionResolver.TryResolveCurrentNode(outcome, additionalElapsedDays, ...)`
    /// 在 Node Settlement 时**一次性**计入（§8）。
    /// </summary>
    public int PendingExtraElapsedDays;

    /// <summary>当前事件生命周期阶段。</summary>
    public ExpeditionEventPhase Phase;

    // ==================== StartBattle 预备（Preflight 生成 → Commit 消费同一份） ====================

    /// <summary>Preflight 阶段已生成、等待 Commit 的**同一份** EnemyRoster（null = 无预备战斗）。</summary>
    public EnemyRoster PreparedBattleRoster;

    /// <summary>预备战斗的 EncounterPoolId（= EventEffect.TargetKey）。</summary>
    public string PreparedBattlePoolId;

    /// <summary>预备战斗使用的 EncounterDepth。</summary>
    public int PreparedBattleDepth;

    /// <summary>预备战斗使用的确定性 Seed（由远征 Session Seed + NodeId + EventInstanceId + OptionId + OutcomeId 派生）。</summary>
    public int PreparedBattleSeed;

    /// <summary>已提交的 Active Encounter（Battle 真正开始时由 Gateway Commit 建立；诊断用）。</summary>
    public ActiveEncounterState CommittedBattle;

    // ==================== 最近一次 Option 执行结果（Result Mode 用） ====================

    /// <summary>最近一次成功的 OptionId。</summary>
    public string LastOptionId;

    /// <summary>最近一次成功的 OutcomeId。</summary>
    public string LastOutcomeId;

    /// <summary>结果文本（由 `EventTemplateRenderer` 渲染 Outcome.ResultTextTemplate；可为空串）。</summary>
    public string LastResultText;

    /// <summary>最近一次成功的 Outcome 是否含 Terminal Effect（StartBattle）。</summary>
    public bool LastOutcomeHasBattle;

    /// <summary>最近一次编排的可操作诊断（成功后为空串）。</summary>
    public string LastDiagnostic;

    public bool HasInstance { get { return Instance != null; } }

    public bool HasPendingBattle
    {
        get { return PreparedBattleRoster != null && !string.IsNullOrEmpty(PreparedBattlePoolId); }
    }

    public override string ToString()
    {
        return "ActiveExpeditionEvent[node=" + NodeId + " phase=" + Phase
            + " event=" + (Instance != null ? Instance.EventId : "<null>")
            + " instance=" + (Instance != null ? Instance.EventInstanceId : "<null>")
            + " pendingExtraDays=" + PendingExtraElapsedDays
            + (HasPendingBattle ? " preparedBattle=" + PreparedBattlePoolId : string.Empty)
            + "]";
    }
}
