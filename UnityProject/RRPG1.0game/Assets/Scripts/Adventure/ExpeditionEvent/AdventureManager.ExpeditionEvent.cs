using RRPG.Encounter;
using RRPG.Event;
using UnityEngine;

/// <summary>
/// **AdventureManager 的 Expedition Event 适配层**（WP-Event-1D Gate D2 §3）。
///
/// 为什么放在这里：`AdventureManager` 已是本次远征会话的唯一 Orchestrator
/// （持有 Progression / RunState / ActiveEncounter / 远征人物集合）；
/// 单次远征会话内唯一的活动事件与事件会话历史必须挂在**同一会话对象**上，
/// 不得新建第二套顶层 Manager（与 WP-Encounter-1C 同一原则）。
///
/// 职责（且仅此）：
///   1. 持有 <see cref="ActiveExpeditionEventState"/>（当前 Event Node 的唯一 Authority）；
///   2. 持有 Expedition Event **会话历史**（`ExpeditionEventSessionState`：OncePerExpedition / 冷却）
///      与**运行时 exactly-once 记号**（`EventExecutionState`）；
///   3. 提供「建立 / 清除 / 会话重置」的唯一写入口（只有 <see cref="ExpeditionEventFlow"/> 调用）。
///
/// 边界：**Runtime Only** —— 不进 Save、不进 Person / RunState、不改变 SaveVersion；
/// 世界级长期状态（Flags / World History）仍属 `EventRuntimeBridge`，本层不复制。
/// </summary>
public partial class AdventureManager
{
    private ActiveExpeditionEventState _activeExpeditionEvent;
    private ExpeditionEventSessionState _expeditionEventSession;
    private EventExecutionState _expeditionEventExecution;

    /// <summary>当前活动 Event（null = 无）。只读；写入只能经本类的方法。</summary>
    public ActiveExpeditionEventState ActiveExpeditionEvent { get { return _activeExpeditionEvent; } }

    public bool HasActiveExpeditionEvent { get { return _activeExpeditionEvent != null; } }

    /// <summary>
    /// 本次远征的事件**会话历史**（OncePerExpedition / ExpeditionNodes 冷却）。
    /// 懒创建：新远征 = 新对象（历史自然清空）；Return / 会话结束由 <see cref="ResetExpeditionEventSession"/> 清理。
    /// </summary>
    public ExpeditionEventSessionState ExpeditionEventSession
    {
        get
        {
            if (_expeditionEventSession == null)
                _expeditionEventSession = new ExpeditionEventSessionState(BuildExpeditionEventSessionKey());
            return _expeditionEventSession;
        }
    }

    /// <summary>本次远征的事件 exactly-once 实例记号（Runtime Only；不持久化）。</summary>
    public EventExecutionState ExpeditionEventExecution
    {
        get
        {
            if (_expeditionEventExecution == null) _expeditionEventExecution = new EventExecutionState();
            return _expeditionEventExecution;
        }
    }

    /// <summary>
    /// **本 Node 的预计 Event / Encounter 深度**（= 进入本 Node 后的已进入数量 − 1；
    /// 与 `AdventureManager.TryPrepareAndEnterBattleNode` 使用**同一** <see cref="EncounterDepthRule"/>，
    /// 不建立第二套深度语义）。
    /// </summary>
    public int ProjectedDepthForNode(string nodeId)
    {
        var state = ProgressionState;
        if (state == null) return 0;
        return EncounterDepthRule.DepthFor(state.GetEnteredNodeIds().Count + 1);
    }

    /// <summary>当前深度（= 已进入数量 − 1；未初始化 = 0）。</summary>
    public int CurrentExpeditionDepth
    {
        get
        {
            var state = ProgressionState;
            return state == null ? 0 : EncounterDepthRule.DepthFor(state.GetEnteredNodeIds().Count);
        }
    }

    /// <summary>
    /// **建立活动事件**（Event Node 原子进入成功后的唯一提交点）。
    /// V0.1 至多一条：已存在活动事件 → false + 明确诊断（不覆盖、不排队）。
    /// </summary>
    public bool TryBeginActiveExpeditionEvent(string nodeId, ResolvedEventInstance instance,
        out string diagnostic)
    {
        if (string.IsNullOrEmpty(nodeId))
        {
            diagnostic = "nodeId 为空：无法建立活动事件。";
            return false;
        }

        if (instance == null)
        {
            diagnostic = "已解析事件实例为 null：拒绝建立活动事件（不得创建空事件占位）。";
            return false;
        }

        if (_activeExpeditionEvent != null)
        {
            diagnostic = "已存在活动事件（" + _activeExpeditionEvent + "）：V0.1 至多一条，不覆盖、不排队。";
            return false;
        }

        _activeExpeditionEvent = new ActiveExpeditionEventState
        {
            NodeId = nodeId,
            Instance = instance,
            PendingExtraElapsedDays = 0,
            Phase = ExpeditionEventPhase.AwaitingOption,
            LastDiagnostic = string.Empty
        };

        diagnostic = string.Empty;
        return true;
    }

    /// <summary>清除活动事件（结算完成 / Return / Retreat / 会话结束 / 防御性清理）。</summary>
    public void ClearActiveExpeditionEvent(out string diagnostic)
    {
        diagnostic = string.Empty;
        if (_activeExpeditionEvent == null) return;

        var removed = _activeExpeditionEvent;
        _activeExpeditionEvent = null;
        diagnostic = "已清除活动事件（" + removed.NodeId + " / "
            + (removed.Instance != null ? removed.Instance.EventId : "<null>") + "）。";
    }

    /// <summary>清除活动事件（忽略诊断；供防御路径与测试使用）。</summary>
    public void ClearActiveExpeditionEvent()
    {
        string ignored;
        ClearActiveExpeditionEvent(out ignored);
    }

    /// <summary>
    /// 会话重置（新远征开始 / Return 完成 / Expedition End）：
    /// 清除活动事件、重建事件会话历史与 exactly-once 记号。
    /// **不触碰** Progression / ActiveEnemyRoster（各自有既有生命周期）。
    /// </summary>
    public void ResetExpeditionEventSession()
    {
        _activeExpeditionEvent = null;
        _expeditionEventSession = null;
        _expeditionEventExecution = null;
    }

    private string BuildExpeditionEventSessionKey()
    {
        return "expedition_event_session|sessionSeed:" + _encounterSessionSeed;
    }

    // ==================== Event Option 的 StartBattle：预生成 / 提交（§11 / §12 / §13） ====================

    /// <summary>
    /// **Event Option 的 StartBattle 预生成**（纯查询语义：不写 Progression、不写 ActiveEncounter、
    /// 不改 Supply / elapsed）。
    ///
    /// 与 <see cref="TryPrepareAndEnterBattleNode"/> 的区别：Event 内战斗发生时**当前 Node 已经进入**
    /// （它是 Event Node，不是 Battle Node），因此这里**不**再进入节点，只生成并返回一份合法 EnemyRoster；
    /// 池 ID 与 Seed 由调用方（Event 侧）显式提供（Event Authoring 指定池；Seed 由 Event 身份稳定派生）。
    ///
    /// 失败 = 明确失败：**不**回退「全部 Monster Registry」、**不**生成默认阵容、**不**进入空战斗。
    /// </summary>
    public bool TryPrepareEventBattleRoster(string nodeId, string poolId, int depth, int seed,
        out EnemyRoster roster, out string diagnostic)
    {
        roster = null;

        if (string.IsNullOrEmpty(poolId))
        {
            diagnostic = "EncounterPoolId 为空：拒绝生成敌方阵容（EventEffect.TargetKey 必须指定 EncounterPool）。";
            return false;
        }

        var generator = GetEncounterGenerator(out string generatorDiagnostic);
        if (generator == null)
        {
            diagnostic = "无法生成敌方阵容：" + generatorDiagnostic;
            return false;
        }

        var result = generator.GenerateFromPool(poolId, depth, seed);
        if (!result.Success)
        {
            diagnostic = "Event 指定的敌方遭遇生成失败（node=" + nodeId + " pool=" + poolId
                + " depth=" + depth + " seed=" + seed + "）：" + result.Failure + " —— " + result.Diagnostic
                + DescribeAttempts(result);

            Debug.LogError("[AdventureManager] " + diagnostic
                + "（本次 Option Preflight 失败：节点停留 Event Pending，可选其它 Option 或重试）");
            return false;
        }

        Debug.Log("[AdventureManager] Event 敌方阵容已生成（未提交）：" + result.Roster
            + "（node=" + nodeId + " pool=" + poolId + " depth=" + depth + " seed=" + seed + "）");

        roster = result.Roster;
        diagnostic = string.Empty;
        return true;
    }

    /// <summary>
    /// **提交 Event 内战斗的 Active Encounter**（Commit 阶段唯一入口）：
    /// 使用 Preflight 已生成的**同一份** EnemyRoster（不得在此重新 Generate）。
    /// </summary>
    public bool CommitPreparedEventBattleEncounter(string nodeId, string poolId, int depth, int seed,
        EnemyRoster roster, out string diagnostic)
    {
        if (roster == null)
        {
            diagnostic = "预备敌方阵容为 null：拒绝提交 Active Encounter（不得开空战斗）。";
            return false;
        }

        ActiveEncounter = new ActiveEncounterState
        {
            NodeId = nodeId,
            PoolId = poolId,
            Depth = depth,
            Seed = seed,
            Roster = roster
        };

        Debug.Log("[AdventureManager] Event 内战斗已提交 Active Encounter：" + ActiveEncounter
            + "（sessionSeed=" + _encounterSessionSeed + "）");

        diagnostic = string.Empty;
        return true;
    }

    /// <summary>
    /// **Event 额外时间版本的 Node 结算转发**（WP-Event-1D Gate D2 §8）：
    /// `TotalElapsedDays = Node.NodeElapsedDays + additionalElapsedDays`，与 Supply 成本在同一次
    /// exactly-once 结算内完成；成功时同样清除该 Node 的 Active EnemyRoster。
    /// </summary>
    public ExpeditionResolveResult TryResolveCurrentNode(ExpeditionNodeOutcome outcome, int additionalElapsedDays,
        out ExpeditionNodeResolutionData data)
    {
        if (Progression == null)
        {
            data = null;
            return ExpeditionResolveResult.NotInitialized;
        }

        var result = Progression.TryResolveCurrentNode(outcome, additionalElapsedDays, out data);

        // 与既有两个重载保持同一生命周期语义：Node 已被最终 outcome 结算 → Active EnemyRoster 立即失效。
        if (result == ExpeditionResolveResult.Success) ClearActiveEnemyRoster();

        return result;
    }
}
