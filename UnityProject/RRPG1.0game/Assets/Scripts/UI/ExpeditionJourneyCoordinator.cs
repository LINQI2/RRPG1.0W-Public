using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ExpeditionJourneyCoordinator —— WP-E Journey Presentation Coordinator（**表现协调；不拥有规则 Authority**）。
///
/// 单向依赖（禁止反向）：
///   AdventureManager / ExpeditionProgressionResolver（只读：PersonIds、合法后继、Supply、elapsed days）
///     → 本协调器（0/1/N 投影、Presentation Cycle、exactly-once / single-transition guard）
///       → ExpeditionJourneyView（Visual Hosts）
///
/// 硬约束：
///   - 只调用既有产品入口：`ExpeditionUI.OnProgressionNodeChosen(nodeId)` 与 `ExpeditionUI.RequestRetreatFromProgression()`；
///   - 不决定合法 Node、不写 Progression / Supply / elapsed / CurrentNode / VillageTime；
///   - 每个 Presentation Cycle 最多消费一个 Progression Transition，消费后必须重新读取 Current Progression State；
///   - Pause / Continue / 行走时间不产生任何规则变化；
///   - Return / Battle / Hide / 新 Cycle 使旧回调失效。
/// </summary>
public class ExpeditionJourneyCoordinator : MonoBehaviour
{
    [Header("View（未绑定时在本对象层级内查找）")]
    public ExpeditionJourneyView view;

    [Header("诊断")]
    public bool logDiagnostics = true;

    private AdventureManager _adventure;
    private ExpeditionUI _owner;
    private JourneyPresentationCycleController _cycle;
    private readonly Queue<Action> _dispatchQueue = new Queue<Action>();

    // ==================== 只读诊断 / 测试 ====================

    public bool IsActive => _cycle != null && _cycle.IsActive;
    public int CycleIndex => _cycle != null ? _cycle.CycleIndex : 0;
    public JourneyProjection LastProjection => _cycle != null ? _cycle.LastProjection : JourneyProjection.None;
    public bool IsAwaitingBranch => _cycle != null && _cycle.IsAwaitingBranch;
    public int PendingDispatchCount => _dispatchQueue.Count;
    public ExpeditionJourneyView View => view;

    public void ShowMessage(string message)
    {
        ResolveView();
        if (view != null) view.ShowMessage(message);
    }

    /// <summary>
    /// **最近一次产品 Node Entry 是否被拒绝**（Issue E 诊断 / 测试）：
    /// `true` = 入口被拒绝（例：`InsufficientSupply`）→ Journey 表现**未**推进、节点零 mutation。
    /// </summary>
    public bool LastEntryRejected => _cycle != null && _cycle.LastEntryRejected;

    /// <summary>最近一次被产品入口拒绝的 NodeId（未被拒绝时为 null）。</summary>
    public string LastEntryRejectedNodeId => _cycle != null ? _cycle.LastEntryRejectedNodeId : null;

    // ==================== WP-Event-1D Gate D2：正式事件卡（转发 View；单一卡面） ====================

    /// <summary>
    /// 渲染**正式 Event 卡**（配置来自 Event Config，由产品 Adapter 投影）：
    /// 只负责渲染 + 上报「玩家选择了第 index 个 Option」，规则执行仍由产品入口负责。
    /// </summary>
    public void PresentFormalEventCard(JourneyEventCardConfig config, Action<int> onOptionChosen)
    {
        ResolveView();
        if (view == null)
        {
            Debug.LogError("[JourneyCoordinator] 未找到 ExpeditionJourneyView：无法渲染正式事件卡。");
            return;
        }
        view.PresentFormalEventCard(config, onOptionChosen);
    }

    /// <summary>正式素材缺失时的类别级占位插图（转发 View；只作 fallback，不决定事件内容 / 身份）。</summary>
    public Sprite ContentEncounterIllustration
    {
        get
        {
            ResolveView();
            return view != null ? view.ContentEncounterIllustration : null;
        }
    }

    private void Awake()
    {
        ResolveView();
    }

    private void ResolveView()
    {
        if (view == null) view = GetComponentInChildren<ExpeditionJourneyView>(true);
    }

    // ==================== 产品接线入口 ====================

    /// <summary>
    /// Confirm 成功后：绑定当前 Adventure → 绑定 Journey Party → 显示 Journey → 开始第一轮 Cycle。
    /// </summary>
    public void Show(AdventureManager adventure, ExpeditionUI owner)
    {
        ResolveView();
        if (view == null)
        {
            Debug.LogError("[JourneyCoordinator] 未找到 ExpeditionJourneyView：Journey 无法显示（不得以 Debug Panel 代替）。");
            return;
        }
        if (adventure == null)
        {
            Debug.LogError("[JourneyCoordinator] Show 收到空 AdventureManager：拒绝开始 Journey。");
            return;
        }

        _adventure = adventure;
        _owner = owner;
        view.ReturnRequested = RequestReturnViaProductEntry;
        view.SetVisible(true);
        RefreshParty();
        RefreshHud();
        view.ClearMessage();
        view.CancelPresentation();
        StartCycles();
        if (logDiagnostics) Debug.Log("[JourneyCoordinator] Journey 已接入真实产品路径（成员 " + view.PartyMemberCount + " 人）。");
    }

    /// <summary>Battle 开始：隐藏 / 冻结 Journey，并取消当前 Presentation Cycle（旧回调失效）。</summary>
    public void HideForEncounter()
    {
        CancelPresentation();
        if (view != null) view.HideForEncounter();
        // WP-Event-1 Producer Acceptance Repair（Issue A）产品硬规则：
        // 真正的 Battle / Prep 生命周期开始 → 上锁；Journey 再次可见（战斗结束）前不得出现新的 Branch / Event 卡。
        if (view != null) view.LockPresentationForBattle();
    }

    /// <summary>Battle 普通结束 / 状态更新后：重新显示 Journey 并**重新读取** Current Progression State。</summary>
    public void ShowAfterEncounter(AdventureManager adventure, ExpeditionUI owner)
    {
        if (adventure == null || !adventure.IsActive) return;
        Show(adventure, owner);
    }

    /// <summary>Return / 离开远征：取消未完成 callback 并隐藏 Journey。</summary>
    public void Hide()
    {
        CancelPresentation();
        if (view != null) view.SetVisible(false);
        _adventure = null;
    }

    /// <summary>拒绝 / 无法推进原因：粘性显示（自动演示不得覆盖，成功推进后清除）。</summary>
    public void ShowDenied(string describe)
    {
        if (view == null) return;
        view.ShowDeniedMessage("无法进入：" + describe);
    }

    /// <summary>成功进入 Node 后清除粘性拒绝原因。</summary>
    public void ClearDeniedMessage()
    {
        if (view == null) return;
        view.ClearStickyMessage();
    }

    /// <summary>统一 Event Card 是否可见（诊断 / 测试）。</summary>
    public bool IsEventCardVisible => view != null && view.IsEventCardVisible;

    /// <summary>当前 Event Card 的类型与选项数（诊断 / 测试）。</summary>
    public int EventCardOptionCount => view != null ? view.EventCardOptionCount : 0;

    /// <summary>非 Battle 占位内容的表现由 Journey 的统一 Event Card 承担（见 View）；
    /// 协调器不再提供第二套 Encounter 表现入口，避免重复卡面路径。</summary>

    public void CancelPresentation()
    {
        _dispatchQueue.Clear();
        _cycle?.Invalidate();
    }

    // ==================== Cycle ====================

    private void StartCycles()
    {
        _cycle = new JourneyPresentationCycleController(
            ReadAvailableNodes,
            EnterNodeViaProductEntry,
            EnqueueDispatch,
            view);
        _cycle.Begin();
        if (logDiagnostics)
        {
            Debug.Log("[JourneyCoordinator] Presentation Cycle #" + _cycle.CycleIndex + " 开始（" + _cycle.LastProjection + "）。");
        }
    }

    private void Update()
    {
        // deferred dispatch：每个 Cycle 边界至少跨一帧（防止同一旧 callback 同步吞多个 Transition）。
        if (_dispatchQueue.Count == 0) return;
        Action next = _dispatchQueue.Dequeue();
        next?.Invoke();
    }

    private void EnqueueDispatch(Action action)
    {
        if (action == null) return;
        _dispatchQueue.Enqueue(action);
    }

    /// <summary>只读：Current 合法后继（0 / 1 / N）。</summary>
    private IReadOnlyList<JourneyNodeRef> ReadAvailableNodes()
    {
        var result = new List<JourneyNodeRef>();
        if (_adventure == null) return result;

        var available = _adventure.GetAvailableProgressionNodes();
        if (available == null) return result;

        for (int i = 0; i < available.Count; i++)
        {
            string nodeId = available[i].NodeId;
            if (string.IsNullOrEmpty(nodeId)) continue;

            // 表现分类（只影响表现：是否先出统一 Event Card，以及卡面文案 / 插图）；
            // 合法性与是否真正可进入仍由既有 Resolver / 产品入口决定。
            //
            // WP-Event-1D Gate D2：非战斗类节点全部是**正式 Event 节点**（内容与身份来自 Unified Event Core）
            // → 分类为 JourneyEncounterKind.Event：进入前不出占位卡，正式事件卡在原子进入成功后由产品渲染。
            string kind = available[i].NodeKind.ToString();
            JourneyEncounterKind encounterKind =
                (kind == "Battle" || kind == "Elite" || kind == "Boss")
                    ? JourneyEncounterKind.Battle
                    : JourneyEncounterKind.Event;

            // 玩家文案不暴露 NodeId / NodeKind / enum 等开发术语（正式路线名称未冻结）。
            result.Add(new JourneyNodeRef(nodeId, string.Empty, encounterKind));
        }
        return result;
    }

    /// <summary>
    /// 唯一 Node 进入产品入口（复用既有 Resolver 校验与拒绝反馈路径）。
    /// WP-Event-1 Producer Acceptance Repair 2（Issue E）：返回**真实成败** → Presentation Cycle
    /// 只在 Entry 真正成功后推进（失败 → 当前表现保持，不推进、不重调度、不自动重试）。
    /// </summary>
    private bool EnterNodeViaProductEntry(string nodeId)
    {
        if (_owner == null) return false;
        bool entered = _owner.OnProgressionNodeChosen(nodeId);
        RefreshHud();
        return entered;
    }

    private void RequestReturnViaProductEntry()
    {
        CancelPresentation();
        if (_owner == null) return;
        _owner.RequestRetreatFromProgression();
    }

    // ==================== HUD ====================

    private void RefreshParty()
    {
        if (view == null || _adventure == null) return;

        var personIds = _adventure.GetExpeditionPersonIds();
        var collection = _owner != null ? _owner.PlayerDataForJourney : null;
        int shown = view.ApplyParty(personIds, collection);
        if (shown != (personIds != null ? personIds.Count : 0))
        {
            Debug.LogWarning("[JourneyCoordinator] Journey Party 显示人数（" + shown + "）与当前远征人物集合（"
                + (personIds != null ? personIds.Count : 0) + "）不一致：请检查 Member Prefab / Party Host 绑定。");
        }
    }

    private void RefreshHud()
    {
        if (view == null || _adventure == null) return;

        var state = _adventure.ProgressionState;
        int current = 0;
        int initial = 0;
        if (state != null && state.Supply != null)
        {
            current = state.Supply.CurrentCarryUnits;
            initial = state.Supply.InitialCarryUnits;
        }
        view.SetMinuteHud(current, initial, _adventure.ExpeditionElapsedMinutes);
    }
}
