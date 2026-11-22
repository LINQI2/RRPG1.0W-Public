using System.Collections.Generic;

/// <summary>
/// Expedition Progression 唯一规则入口（FDS §7.3）。
///
/// 职责边界：
/// - Enter ≠ Resolve/Complete：TryEnterNode 只表达「到达」；TryResolveCurrentNode 才是内容最终 outcome 的唯一结算入口；
/// - 结算 exactly-once：同一 CurrentNodeId 只能 Resolve 一次；elapsed 累加与 Supply 扣减在同一原子 resolution 内；
/// - 无论 Victory 还是 Defeat，只要内容确实发生，已发生的 NodeElapsedDays 与 Supply 都 exactly-once 结算；
///   Defeat → Resolved-not-Completed、不开放普通后继；
/// - 普通可预知成本 Node 在进入前完成 affordability validation（Supply 不足 → InsufficientSupply，内容不启动）；
/// - 0 successor 只表示 progression terminal；RouteCompleted 只由显式 SuccessfulRouteTerminal 的成功 outcome 产生；
/// - Return/Retreat 是 Expedition 层独立操作，不属于任何 Node 后继。
///
/// 本层不持久化、不 Save、不触碰 VillageTime / V4F；这些属于 Return handoff（Stage 3）。
/// </summary>
public class ExpeditionProgressionResolver
{
    /// <summary>当前会话真相；InitializeProgression 成功后非空。所有状态写操作只发生在本类。</summary>
    public ExpeditionProgressionState State { get; private set; }

    /// <summary>
    /// 初始化会话（只允许一次；重复调用为 no-op，不重置既有会话）。
    /// 不引入 PartyContext 抽象：只传最小必要值 partySize / initialCarryUnits / perPersonDailyConsumptionRate。
    /// </summary>
    public ExpeditionProgressionInitResult InitializeProgression(
        ExpeditionGraph graph,
        int partySize,
        int initialCarryUnits,
        int perPersonDailyConsumptionRate)
    {
        if (State != null)
            return ExpeditionProgressionInitResult.AlreadyInitialized;
        if (graph == null)
            return ExpeditionProgressionInitResult.NullGraph;

        var supply = new ExpeditionSupplyState(partySize, initialCarryUnits, perPersonDailyConsumptionRate);
        State = new ExpeditionProgressionState(graph, supply);
        return ExpeditionProgressionInitResult.Success;
    }

    // ==================== 候选 / 校验（纯查询，不 mutation） ====================

    /// <summary>
    /// 当前合法后继。
    /// - NotStarted → [入口 Node]；
    /// - AwaitingNext（上一 Node 成功完成）→ 上一 Node 的 Connections（0/1/N）；
    /// - Entered / FailedResolved / ReturnRequested → 空。
    /// </summary>
    public IReadOnlyList<ExpeditionNodeData> GetAvailableNextNodes()
    {
        if (State == null)
            return new List<ExpeditionNodeData>();

        if (State.Phase == ExpeditionProgressionPhase.NotStarted)
        {
            var entry = State.Graph.GetEntryNode();
            return new List<ExpeditionNodeData> { entry };
        }

        if (State.Phase == ExpeditionProgressionPhase.AwaitingNext)
            return GetSuccessorsOf(State.CurrentNodeId);

        return new List<ExpeditionNodeData>();
    }

    private List<ExpeditionNodeData> GetSuccessorsOf(string nodeId)
    {
        var result = new List<ExpeditionNodeData>();
        if (State == null || string.IsNullOrEmpty(nodeId))
            return result;
        if (!State.Graph.TryGetNode(nodeId, out var node))
            return result;

        foreach (var connId in node.Connections)
        {
            if (State.IsNodeEntered(connId) || State.IsNodeCompleted(connId))
                continue;
            if (State.Graph.TryGetNode(connId, out var next))
                result.Add(next);
        }
        return result;
    }

    /// <summary>纯查询：该 Node 现在能否被进入（含 affordability validation；不改状态）。</summary>
    public ExpeditionEnterResult ValidateEnterNode(string nodeId)
    {
        if (State == null)
            return ExpeditionEnterResult.NotInitialized;
        if (!State.Graph.TryGetNode(nodeId, out var node))
            return ExpeditionEnterResult.NoSuchNode;
        if (State.IsNodeCompleted(nodeId))
            return ExpeditionEnterResult.AlreadyCompleted;
        if (State.IsNodeEntered(nodeId))
            return ExpeditionEnterResult.AlreadyEntered;

        switch (State.Phase)
        {
            case ExpeditionProgressionPhase.NotStarted:
                if (nodeId != State.Graph.EntryNodeId)
                    return ExpeditionEnterResult.NotAvailableNow;
                break;
            case ExpeditionProgressionPhase.AwaitingNext:
                if (!IsSuccessorOfCurrent(nodeId))
                    return ExpeditionEnterResult.NotAvailableNow;
                break;
            default:
                return ExpeditionEnterResult.WrongPhase;
        }

        // 普通可预知成本 Node：进入内容前完成 affordability validation。
        if (!State.Supply.TryComputeMinuteCost(State.ExpeditionElapsedMinutes, node.DurationMinutes,
            node.ExtraSupplyCost, out int projectedTotalCost, out _)) return ExpeditionEnterResult.InsufficientSupply;
        if (!State.Supply.CanAfford(projectedTotalCost))
            return ExpeditionEnterResult.InsufficientSupply;

        return ExpeditionEnterResult.Success;
    }

    private bool IsSuccessorOfCurrent(string nodeId)
    {
        if (string.IsNullOrEmpty(State.CurrentNodeId))
            return false;
        if (!State.Graph.TryGetNode(State.CurrentNodeId, out var current))
            return false;
        foreach (var connId in current.Connections)
        {
            if (connId == nodeId)
                return true;
        }
        return false;
    }

    // ==================== 进入 ====================

    /// <summary>进入 Node（只表达到达；不产生 elapsed/supply 结算）。</summary>
    public ExpeditionEnterResult TryEnterNode(string nodeId)
    {
        var validation = ValidateEnterNode(nodeId);
        if (validation != ExpeditionEnterResult.Success)
            return validation;

        State.AddEntered(nodeId);
        State.CurrentNodeId = nodeId;
        State.Phase = ExpeditionProgressionPhase.Entered;
        return ExpeditionEnterResult.Success;
    }

    /// <summary>Branch 选择：N>1 时只能选合法后继；N=1 由 controller 直接继续；0 后继无候选可进入。</summary>
    public ExpeditionEnterResult TryChooseBranch(string nextNodeId)
    {
        return TryEnterNode(nextNodeId);
    }

    // ==================== 结算（内容最终 outcome 的唯一入口） ====================

    /// <summary>结算当前 Node 的最终 outcome（out 明细版本；等价于 additionalElapsedDays = 0）。</summary>
    public ExpeditionResolveResult TryResolveCurrentNode(ExpeditionNodeOutcome outcome, out ExpeditionNodeResolutionData data)
    {
        return TryResolveCurrentNode(outcome, 0, out data);
    }

    /// <summary>
    /// **WP-Event-1D §23**：结算当前 Node 的最终 outcome，并可额外计入 Event 请求的远征时间。
    ///
    /// 语义：
    ///   - `additionalElapsedDays &gt;= 0`（负数 → InvalidAdditionalElapsedDays，不夹取）；
    ///   - `TotalElapsedDays = Node.NodeElapsedDays + additionalElapsedDays`；
    ///   - `ExpeditionElapsedDays += TotalElapsedDays`；
    ///   - 基础 Supply 成本 = `PartySize × TotalElapsedDays × PerPersonDailyConsumptionRate`，
    ///     再叠加既有 `Node.ExtraSupplyCost`，一次 exactly-once 扣除；
    ///   - `additionalElapsedDays = 0` 与旧行为**完全等价**（旧入口委托到本方法）。
    ///
    /// 为什么不让 Event 直接改 elapsed：时间与补给必须**同一次原子结算**，
    /// 否则会出现「时间加了但补给没扣」或「事件成功后 Node 结算失败」的半状态（§22 / §25）。
    /// </summary>
    public ExpeditionResolveResult TryResolveCurrentNode(ExpeditionNodeOutcome outcome, int additionalElapsedDays,
        out ExpeditionNodeResolutionData data)
    {
        data = null;

        if (additionalElapsedDays < 0)
            return ExpeditionResolveResult.InvalidAdditionalElapsedDays;

        if (State == null)
            return ExpeditionResolveResult.NotInitialized;
        if (string.IsNullOrEmpty(State.CurrentNodeId))
            return ExpeditionResolveResult.NoCurrentNode;
        if (!State.IsNodeEntered(State.CurrentNodeId))
            return ExpeditionResolveResult.NotEntered;
        if (State.IsNodeResolved(State.CurrentNodeId))
            return ExpeditionResolveResult.AlreadyResolved;
        if (State.Phase != ExpeditionProgressionPhase.Entered)
            return ExpeditionResolveResult.NotEntered;

        if (!State.Graph.TryGetNode(State.CurrentNodeId, out var node))
            return ExpeditionResolveResult.NoCurrentNode;

        // WP-Event-1D：总时间 = Node 本身时间 + Event 额外时间（溢出防护；超界 → 明确拒绝，不静默截断）。
        long durationMinutes, afterMinutes;
        try
        {
            durationMinutes = checked(node.DurationMinutes + (long)additionalElapsedDays * 1440);
            afterMinutes = checked(State.ExpeditionElapsedMinutes + durationMinutes);
            // The legacy day-based adapters must remain representable until retired.
            if (afterMinutes / 1440 > int.MaxValue) return ExpeditionResolveResult.InvalidAdditionalElapsedDays;
        }
        catch (System.OverflowException) { return ExpeditionResolveResult.InvalidAdditionalElapsedDays; }
        int totalElapsedDays = (int)(durationMinutes / 1440);

        // 结算前置校验（防御边界；正常路径已在 Enter 前校验，此处仅保证原子失败不产生部分结算）。
        if (!State.Supply.TryComputeMinuteCost(State.ExpeditionElapsedMinutes, durationMinutes,
            node.ExtraSupplyCost, out int totalCost, out int baseIncrement)) return ExpeditionResolveResult.InvalidAdditionalElapsedDays;
        if (!State.Supply.CanAfford(totalCost))
            return ExpeditionResolveResult.InsufficientSupply;

        // —— exactly-once 原子结算：Resolved 标记 + elapsed 累加 + Supply 扣减 ——
        State.AddResolved(node.NodeId);
        State.ExpeditionElapsedMinutes = afterMinutes;
        State.Supply.TryConsume(totalCost);

        bool completed = outcome == ExpeditionNodeOutcome.Success;
        bool routeCompleted = false;
        if (completed)
        {
            State.AddCompleted(node.NodeId);
            if (node.SuccessfulRouteTerminal)
            {
                routeCompleted = true;
                State.RouteCompleted = true;
            }
            State.Phase = ExpeditionProgressionPhase.AwaitingNext;
        }
        else
        {
            // Defeat：Resolved-but-not-Completed；不开放普通后继。
            State.Phase = ExpeditionProgressionPhase.FailedResolved;
        }

        data = new ExpeditionNodeResolutionData
        {
            NodeId = node.NodeId,
            Outcome = outcome,
            NodeElapsedDaysSettled = node.NodeElapsedDays,
            AdditionalElapsedDays = additionalElapsedDays,
            TotalElapsedDaysSettled = totalElapsedDays,
            BaseSupplyConsumption = baseIncrement,
            DurationMinutesSettled = durationMinutes,
            ExpeditionElapsedMinutesTotal = afterMinutes,
            ExtraSupplyCostApplied = node.ExtraSupplyCost,
            TotalSupplyCost = totalCost,
            ExpeditionElapsedDaysTotal = State.ExpeditionElapsedDays,
            CurrentCarryUnits = State.Supply.CurrentCarryUnits,
            Completed = completed,
            RouteCompleted = routeCompleted
        };
        return ExpeditionResolveResult.Success;
    }

    /// <summary>结算当前 Node 的最终 outcome（忽略明细版本）。</summary>
    public ExpeditionResolveResult TryResolveCurrentNode(ExpeditionNodeOutcome outcome)
    {
        return TryResolveCurrentNode(outcome, 0, out _);
    }

    // ==================== Return（Expedition 层独立操作） ====================

    /// <summary>
    /// 请求 Return：置 ReturnRequested 并结束推进（handoff 到既有 V4F transaction）。
    /// 若当前 Node 内容已进入但未结算 → 拒绝（必须先结算已发生成本）；若尚未启动任何 Node 内容 → 直接允许（无额外结算）。
    /// </summary>
    public ExpeditionReturnResult RequestReturn()
    {
        if (State == null)
            return ExpeditionReturnResult.NotInitialized;
        if (State.Phase == ExpeditionProgressionPhase.Entered)
            return ExpeditionReturnResult.NodePendingResolution;
        if (State.Phase == ExpeditionProgressionPhase.ReturnRequested)
            return ExpeditionReturnResult.AlreadyRequested;

        State.Phase = ExpeditionProgressionPhase.ReturnRequested;
        return ExpeditionReturnResult.Success;
    }
}
