using System;
using System.Collections.Generic;

/// <summary>
/// JourneyEncounterKind —— Encounter Card 的**表现分类**（只影响表现，不决定规则合法性 / 节点类型 Authority）。
/// None = 不需要事件卡确认；Battle = 战斗前事件卡；Content = 非战斗占位内容事件卡。
/// </summary>
public enum JourneyEncounterKind
{
    None = 0,
    Battle = 1,
    Content = 2,
    Branch = 3,

    /// <summary>
    /// **正式 Event 节点**（WP-Event-1D Gate D2）：内容 / 身份来自 Unified Event Core；
    /// 进入前**不**显示占位卡（正式事件卡在「原子进入成功」后由产品路径统一渲染）。
    /// </summary>
    Event = 4
}

/// <summary>
/// JourneyNodeRef —— Journey 表现层只读的节点引用（NodeId + 玩家可见占位文案）。
/// 不携带 NodeKind enum / 内部术语；只用于表现，不构成规则 Authority。
/// </summary>
public struct JourneyNodeRef
{
    public string NodeId;
    public string DisplayLabel;

    /// <summary>本节点在进入前是否需要 Encounter Card 确认（表现层门禁；不改变规则入口）。</summary>
    public JourneyEncounterKind EncounterKind;

    public JourneyNodeRef(string nodeId, string displayLabel)
    {
        NodeId = nodeId;
        DisplayLabel = displayLabel;
        EncounterKind = JourneyEncounterKind.None;
    }

    public JourneyNodeRef(string nodeId, string displayLabel, JourneyEncounterKind encounterKind)
    {
        NodeId = nodeId;
        DisplayLabel = displayLabel;
        EncounterKind = encounterKind;
    }

    public bool IsValid => !string.IsNullOrEmpty(NodeId);

    /// <summary>
    /// 进入前是否需要 Encounter Card 确认（表现层门禁；不改变规则入口）。
    ///
    /// WP-Event-1D Gate D2：**正式 Event 节点（<see cref="JourneyEncounterKind.Event"/>）不需要**——
    /// 它的事件卡在「原子进入成功后」由产品路径按 Event Config 渲染，不得先弹一张占位卡；
    /// Battle 仍需要「进入战斗」确认卡；Content 保持既有占位语义（尚未迁移的兼容面）。
    /// </summary>
    public bool RequiresEncounterConfirmation
    {
        get { return EncounterKind == JourneyEncounterKind.Battle || EncounterKind == JourneyEncounterKind.Content; }
    }
}

/// <summary>0 / 1 / N 后继的**表现投影**（只投影数量，不决定合法性）。</summary>
public enum JourneyProjection
{
    None = 0,
    Single = 1,
    Branch = 2
}

/// <summary>
/// Journey 表现驱动接缝：由 View 实现（播放 / 暂停 / 覆盖层 / 分支选择）。
/// 协调器只通过本接口要求表现，不直接操作 RectTransform。
/// </summary>
public interface IJourneyPresentationDriver
{
    /// <summary>N=1：播放一段向右旅行；自动完成或 Skip 后**恰好一次**回调 onComplete。</summary>
    void PresentTravel(JourneyNodeRef node, int cycleIndex, Action onComplete);

    /// <summary>N&gt;1：播放至 Branch；完成后恰好一次回调 onComplete（此时只显示 Branch Choice）。</summary>
    void PresentTravelToBranch(IList<JourneyNodeRef> nodes, int cycleIndex, Action onComplete);

    /// <summary>N=0：显示当前无普通后继 / 可撤退状态（不自动判胜）。</summary>
    void PresentTerminal(IList<JourneyNodeRef> none, int cycleIndex);

    /// <summary>显示当次合法 Branch 选项（按钮只能来自本次快照）。</summary>
    void ShowBranchChoice(IList<JourneyNodeRef> nodes, Action<string> onChosen);

    void HideBranchChoice();

    /// <summary>非 Battle 占位内容：短暂显示占位 Encounter，然后回调（不发明 Event 规则）。</summary>
    void PresentPlaceholderEncounter(JourneyNodeRef node, Action onShown);

    /// <summary>使当前正在进行的所有表现失效（Return / Battle / Hide / dispose / 新 Cycle）。</summary>
    void CancelPresentation();
}

/// <summary>
/// JourneyPresentationCycleController —— **WP-E 冻结契约的实现**（纯逻辑，可 EditMode 测试）：
///
///   1. 每个 Presentation Cycle 最多消费一个 Progression Transition；
///   2. 消费后当前 Cycle 立即结束，下一步必须重新读取 Current Progression State；
///   3. 新 Cycle 绝不复用旧 Cycle 的完成回调（generation / cycleIndex 失效）；
///   4. 自动完成与 Continue Skip 汇合到唯一入口 CompleteCurrentTravelPresentation(reason)；
///   5. Branch 不被 Skip 代选；选择前重新验证合法性；
///   6. 消费后不得在同一调用栈内继续消费下一个 Transition（必须经过 deferred 重调度）。
///
/// 调度机制（deferred dispatch）不冻结：由构造注入的 dispatch 提供（下一帧 / 队列 / 协程均可）。
/// </summary>
public sealed class JourneyPresentationCycleController
{
    private readonly Func<IReadOnlyList<JourneyNodeRef>> _readAvailableNodes;
    /// <summary>
    /// 产品 Node Entry 委托（Issue E）：返回**真实成败**。
    /// 表现层只消费该结果，**不**自己判断 Supply / Node legality / Progression state。
    /// </summary>
    private readonly Func<string, bool> _enterNode;
    private readonly Action<Action> _dispatch;
    private readonly IJourneyPresentationDriver _driver;

    private readonly List<JourneyNodeRef> _cycleNodes = new List<JourneyNodeRef>();

    private int _cycleIndex;
    private bool _active;
    private bool _invalidated;
    private bool _cycleOpen;            // 本 Cycle 是否仍可消费 Transition
    private bool _pendingDispatch;
    private string _pendingTransitionNodeId;

    public JourneyPresentationCycleController(
        Func<IReadOnlyList<JourneyNodeRef>> readAvailableNodes,
        Func<string, bool> enterNode,
        Action<Action> dispatch,
        IJourneyPresentationDriver driver)
    {
        _readAvailableNodes = readAvailableNodes;
        _enterNode = enterNode;
        _dispatch = dispatch;
        _driver = driver;
    }

    /// <summary>已开始的 Cycle 序号（从 1 开始；0 = 尚未开始）。</summary>
    public int CycleIndex => _cycleIndex;

    /// <summary>最近一次投影结果。</summary>
    public JourneyProjection LastProjection { get; private set; }

    /// <summary>是否仍在运行（Invalidate / 未 Begin 时为 false）。</summary>
    public bool IsActive => _active && !_invalidated;

    /// <summary>进入 AwaitingBranch（等待玩家选择）时为 true。</summary>
    public bool IsAwaitingBranch { get; private set; }

    /// <summary>是否已经排了一个 deferred 重调度（避免重复排队）。</summary>
    public bool HasPendingReSchedule => _pendingDispatch;

    /// <summary>本 Cycle 已消费的 Transition 数（恒为 0 或 1）。</summary>
    public int ConsumedTransitionsInCurrentCycle { get; private set; }

    /// <summary>
    /// **最近一次产品 Node Entry 是否被拒绝**（Issue E 诊断 / 测试）：
    /// `true` = 最近一次尝试被产品入口拒绝（例：`InsufficientSupply`）→ 表现**未**推进。
    /// </summary>
    public bool LastEntryRejected { get; private set; }

    /// <summary>最近一次被拒绝的 NodeId（未被拒绝时为 null）。</summary>
    public string LastEntryRejectedNodeId { get; private set; }

    // ==================== 生命周期 ====================

    /// <summary>开始第一轮调度（真实产品接线在 Confirm 成功后调用）。</summary>
    public void Begin()
    {
        _invalidated = false;
        _active = true;
        StartNewCycle();
    }

    /// <summary>
    /// Return / Hide / Rebind / Battle / dispose：使旧 Cycle 与旧回调全部失效。
    /// 失效后任何旧 onComplete / ChooseBranch 都不得推进 Progression。
    /// </summary>
    public void Invalidate()
    {
        _invalidated = true;
        _active = false;
        _cycleOpen = false;
        _pendingDispatch = false;
        _pendingTransitionNodeId = null;
        IsAwaitingBranch = false;
        _cycleNodes.Clear();
        _driver.HideBranchChoice();
        _driver.CancelPresentation();
    }

    /// <summary>从 Current Progression State 开始一个新的 Presentation Cycle。</summary>
    public void StartNewCycle()
    {
        if (!IsActive) return;

        _pendingDispatch = false;
        _cycleIndex++;
        _cycleOpen = true;
        ConsumedTransitionsInCurrentCycle = 0;
        IsAwaitingBranch = false;
        _pendingTransitionNodeId = null;
        _cycleNodes.Clear();

        IReadOnlyList<JourneyNodeRef> nodes = _readAvailableNodes != null
            ? _readAvailableNodes()
            : null;

        int count = nodes != null ? nodes.Count : 0;
        for (int i = 0; i < count; i++)
        {
            if (nodes[i].IsValid) _cycleNodes.Add(nodes[i]);
        }

        LastProjection = Project(_cycleNodes.Count);
        int cycle = _cycleIndex;

        switch (LastProjection)
        {
            case JourneyProjection.None:
                _driver.PresentTerminal(_cycleNodes, cycle);
                _cycleOpen = false; // N=0 无可消费 Transition
                break;

            case JourneyProjection.Single:
                _pendingTransitionNodeId = _cycleNodes[0].NodeId;
                _driver.PresentTravel(_cycleNodes[0], cycle, () => CompleteCurrentTravelPresentation("auto"));
                break;

            default:
                _driver.PresentTravelToBranch(_cycleNodes, cycle, () => OnArrivedAtBranch(cycle));
                break;
        }
    }

    /// <summary>0 / 1 / N 表现投影（只按数量分流，不决定合法性）。</summary>
    public static JourneyProjection Project(int availableCount)
    {
        if (availableCount <= 0) return JourneyProjection.None;
        return availableCount == 1 ? JourneyProjection.Single : JourneyProjection.Branch;
    }

    // ==================== 唯一完成入口（自动 + Skip 汇合） ====================

    /// <summary>
    /// 自动旅行完成与「继续前进」Skip 的**唯一**逻辑入口。
    /// 保证：每 Cycle 最多消费一个 Transition；消费后立即结束当前 Cycle 并 deferred 重调度。
    /// </summary>
    public bool CompleteCurrentTravelPresentation(string reason)
    {
        if (!IsActive || !_cycleOpen) return false;              // 旧 / 已消费 / 已失效 → no-op
        if (string.IsNullOrEmpty(_pendingTransitionNodeId)) return false;

        string nodeId = _pendingTransitionNodeId;

        // WP-Event-1 Producer Acceptance Repair 2（Issue E）：产品 Node Entry 必须真正成功才允许消费本 Cycle 的
        // Transition 并推进表现；失败（例如 InsufficientSupply）→ 当前 Node / Progression / Supply / elapsed
        // 均零 mutation，本 Cycle 保持 open 且**不**重调度（不推进、不生成后续节点表现、不自动重试）。
        if (!TryEnterNodeViaProductEntry(nodeId))
        {
            return false;
        }
        _cycleOpen = false;                                      // 本 Cycle 的 Transition 预算用完
        ConsumedTransitionsInCurrentCycle = 1;
        _pendingTransitionNodeId = null;
        _cycleIndex++;                                           // 旧回调自此失效

        // Issue E：这里原本是产品入口调用（现已上移到方法开头的守卫，唯一调用点）：                              // 唯一产品入口（可能同步 resolve）

        // 关键：绝不在同一调用栈内继续消费下一个 Transition；必须经过 deferred 重调度。
        RequestReSchedule("consume");
        return true;
    }

    // ==================== Branch ====================

    private void OnArrivedAtBranch(int cycle)
    {
        if (!IsActive || cycle != _cycleIndex || !_cycleOpen) return;

        IsAwaitingBranch = true;
        var snapshot = new List<JourneyNodeRef>(_cycleNodes);
        _driver.ShowBranchChoice(snapshot, nodeId => { ChooseBranch(nodeId); });
    }

    /// <summary>
    /// 玩家选择 Branch。Skip / Continue **不得**代选：只有本方法会消费 Branch 的 Transition。
    /// 选择前重新验证该 Node 仍在当次合法后继内；失效则拒绝并刷新，不静默改选。
    /// </summary>
    public bool ChooseBranch(string nodeId)
    {
        if (!IsActive || !IsAwaitingBranch || !_cycleOpen) return false;
        if (string.IsNullOrEmpty(nodeId)) return false;

        IReadOnlyList<JourneyNodeRef> current = _readAvailableNodes != null ? _readAvailableNodes() : null;
        if (current == null || !ContainsNode(current, nodeId))
        {
            RequestBranchRefresh();
            return false;
        }

        // WP-Event-1 Producer Acceptance Repair 2（Issue E）：先请求**产品** Node Entry；
        // 只有真正成功才允许消费本 Cycle 的 Transition 并推进表现（失败 → 保持当前 Branch 呈现与可交互状态、
        // 不重调度、不自动改选、不自动重试）。
        if (!TryEnterNodeViaProductEntry(nodeId))
        {
            return false;
        }

        IsAwaitingBranch = false;
        _cycleOpen = false;
        ConsumedTransitionsInCurrentCycle = 1;
        _pendingTransitionNodeId = null;
        _cycleIndex++;
        _driver.HideBranchChoice();

        RequestReSchedule("branch");
        return true;
    }

    /// <summary>使当次 Branch 快照失效并重新读取 Current Progression State 刷新可选道路。</summary>
    private void RequestBranchRefresh()
    {
        if (!IsActive) return;
        IsAwaitingBranch = false;
        _cycleOpen = false;
        _cycleIndex++;
        _driver.HideBranchChoice();
        RequestReSchedule("branch-refresh");
    }

    // ==================== 延迟重调度 ====================

    private void RequestReSchedule(string reason)
    {
        if (!IsActive || _pendingDispatch) return;
        _pendingDispatch = true;
        int expected = _cycleIndex;
        Action action = () =>
        {
            _pendingDispatch = false;
            if (!IsActive || expected != _cycleIndex) return;   // 期间已被 Invalidate / 已建新 Cycle
            StartNewCycle();
        };

        if (_dispatch != null) _dispatch(action);
        else action();                                          // 无注入 dispatch → 直接串行（测试可用）
    }

    /// <summary>
    /// 请求**产品** Node Entry（Issue E）：唯一调用点，返回真实成败并记录诊断。
    /// 表现层**不**自行计算 Supply / Node legality / Progression —— 只消费产品入口结果。
    /// </summary>
    private bool TryEnterNodeViaProductEntry(string nodeId)
    {
        bool entered = _enterNode != null && _enterNode(nodeId);
        LastEntryRejected = !entered;
        LastEntryRejectedNodeId = entered ? null : nodeId;
        return entered;
    }

    private static bool ContainsNode(IReadOnlyList<JourneyNodeRef> nodes, string nodeId)
    {
        for (int i = 0; i < nodes.Count; i++)
        {
            if (nodes[i].NodeId == nodeId) return true;
        }
        return false;
    }
}
