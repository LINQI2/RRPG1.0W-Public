using System.Collections.Generic;

/// <summary>
/// Expedition Progression 会话内最小推进相位（内嵌于 State 文件；FDS 允许内嵌 enum）。
/// Enter ≠ Resolve/Complete：Entered 表示已到达但内容尚未产生最终 outcome。
/// </summary>
public enum ExpeditionProgressionPhase
{
    /// <summary>初始化完成，尚未进入任何 Node（可进入唯一入口 Node 或 RequestReturn）。</summary>
    NotStarted,

    /// <summary>已进入 Node，内容尚未产生最终 outcome（不可重复 Enter、不可重复 Resolve）。</summary>
    Entered,

    /// <summary>上一个 Node 以成功 outcome 结算完成；可进入后继（0/1/N）或 RequestReturn。</summary>
    AwaitingNext,

    /// <summary>上一个 Node 以失败 outcome（如 Battle Defeat）结算：Resolved-but-not-Completed，无普通后继，只可 Return。</summary>
    FailedResolved,

    /// <summary>玩家已请求 Return（Expedition 层独立操作）；后续由既有 V4F transaction 接管。</summary>
    ReturnRequested
}

/// <summary>
/// Expedition Progression 会话内真相（唯一运行 Authority；不持久化、不进 SaveData、不提升 SaveVersion）。
///
/// 只读查询面向外部（UI / controller / V4F handoff）；所有写操作必须经 Resolver（同程序集 internal），
/// 禁止 UI / Map 直接 mutation 本 State 作为规则执行。
/// </summary>
public class ExpeditionProgressionState
{
    private readonly HashSet<string> _entered = new HashSet<string>();
    private readonly HashSet<string> _resolved = new HashSet<string>();
    private readonly HashSet<string> _completed = new HashSet<string>();

    /// <summary>会话内 Node 定义图（只读）。</summary>
    public ExpeditionGraph Graph { get; }

    /// <summary>会话内 Supply / Carry 消费侧状态。</summary>
    public ExpeditionSupplyState Supply { get; }

    /// <summary>当前推进相位。</summary>
    public ExpeditionProgressionPhase Phase { get; internal set; }

    /// <summary>当前 NodeId（Entered 时 = 内容 Node；AwaitingNext/FailedResolved 时 = 上一已结算 Node；未进入时为 null）。</summary>
    public string CurrentNodeId { get; internal set; }

    /// <summary>integer Village Days（Node 成功/失败最终 outcome exactly-once 结算后累加）。</summary>
    public long ExpeditionElapsedMinutes { get; internal set; }
    public int ExpeditionElapsedDays => checked((int)(ExpeditionElapsedMinutes / 1440));

    /// <summary>成功路线完成标记：仅当显式 SuccessfulRouteTerminal Node 的成功 outcome 结算后才置 true。</summary>
    public bool RouteCompleted { get; internal set; }

    internal ExpeditionProgressionState(ExpeditionGraph graph, ExpeditionSupplyState supply)
    {
        Graph = graph;
        Supply = supply;
        Phase = ExpeditionProgressionPhase.NotStarted;
        CurrentNodeId = null;
        ExpeditionElapsedMinutes = 0;
        RouteCompleted = false;
    }

    public bool IsNodeEntered(string nodeId)
    {
        return !string.IsNullOrEmpty(nodeId) && _entered.Contains(nodeId);
    }

    public bool IsNodeResolved(string nodeId)
    {
        return !string.IsNullOrEmpty(nodeId) && _resolved.Contains(nodeId);
    }

    public bool IsNodeCompleted(string nodeId)
    {
        return !string.IsNullOrEmpty(nodeId) && _completed.Contains(nodeId);
    }

    /// <summary>已进入 NodeId 快照。</summary>
    public IReadOnlyCollection<string> GetEnteredNodeIds()
    {
        return new List<string>(_entered);
    }

    /// <summary>已结算 outcome 的 NodeId 快照（含失败 Resolved-not-Completed）。</summary>
    public IReadOnlyCollection<string> GetResolvedNodeIds()
    {
        return new List<string>(_resolved);
    }

    /// <summary>已成功完成的 NodeId 快照。</summary>
    public IReadOnlyCollection<string> GetCompletedNodeIds()
    {
        return new List<string>(_completed);
    }

    // ==================== 仅 Resolver（同程序集）使用 ====================

    internal void AddEntered(string nodeId)
    {
        _entered.Add(nodeId);
    }

    internal void AddResolved(string nodeId)
    {
        _resolved.Add(nodeId);
    }

    internal void AddCompleted(string nodeId)
    {
        _completed.Add(nodeId);
    }
}
