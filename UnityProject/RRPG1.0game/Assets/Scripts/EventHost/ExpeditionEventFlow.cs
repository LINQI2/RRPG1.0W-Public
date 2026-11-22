using System;
using System.Collections.Generic;
using RRPG.Event;
using RRPG.StaticConfig;

/// <summary>
/// **ExpeditionEventFlow（远征统一事件产品流）** —— WP-Event-1D Gate D2 的核心编排。
///
/// 正式时序（真实产品链）：
/// <code>
/// Journey（旅行完成）
///   → ValidateEnterNode（纯查询）
///   → EventResolutionRequest(TriggerType=ExpeditionEventNodeReached, TriggerKey, ResolutionSeed, Context)
///   → EventResolver（只读 WorldView + World State + 远征会话历史）
///   → 成功 → TryEnterNode（唯一 mutation 点）+ 提交**同一个** ResolvedEventInstance 为 ActiveExpeditionEvent
///   → 正式 Event 卡（Title / Body / Options 来自 Event Config）
///   → Option → EventOptionExecutor（Preflight → Commit → History → Instance Completed）
///        · 非战斗 Outcome → Result Mode →「继续」→ TryResolveCurrentNode(Success, pendingExtraDays)
///        · StartBattle → 同一份预备 EnemyRoster → Prep / Battle → Victory/Defeat → 结算**同一个** Event Node
/// </code>
///
/// 硬约束：
///   - **不拥有**状态：Event 世界状态属 `EventRuntimeBridge`，远征会话状态 / Active Event 属 `AdventureManager`，
///     静态内容属 Static Config；本类只做「投影 + 编排 + 诊断」；
///   - **零 mutation 失败**：进入 / Option Preflight / 结算任一阶段失败都不得留下半成品
///     （节点未进入、Pending 保留、History 不写、Supply / elapsed 不变）；
///   - **不 fallback**：求解失败绝不回退占位 Continue / 默认事件 / NodeId 文本事件；
///   - **确定性**：TriggerKey / ResolutionSeed / 战斗 Seed 全部由真实身份稳定派生（无 Guid / 时间 / 引擎随机）。
/// </summary>
public static class ExpeditionEventFlow
{
    /// <summary>正式触发时机（`RRPG_Events.xlsx / EventTrigger.TriggerType`）。</summary>
    public const string TriggerType = EventTriggerTypes.ExpeditionEventNodeReached;

    /// <summary>Context：本次要进入的 NodeId（Stable String Context）。</summary>
    public const string ContextNodeIdKey = "NodeId";

    /// <summary>Context：本次要进入的 Node 的投影深度（Stable Int Context）。</summary>
    public const string ContextDepthKey = "Depth";

    /// <summary>最近一次编排的可操作诊断（成功后为空串；不刷 Console）。</summary>
    public static string LastDiagnostic { get; private set; }

    /// <summary>最近一次 Resolver 结果码文本（`ConfigUnavailable` / `NoCandidateEvent` / `Success` …）。</summary>
    public static string LastResolutionCode { get; private set; }

    /// <summary>最近一次 Option 执行的结果码文本（`Success` / `EffectPreflightFailed` …）。</summary>
    public static string LastExecutionCode { get; private set; }

    /// <summary>测试 / 会话重置：只清诊断（不触碰任何 Authority 状态）。</summary>
    public static void ResetDiagnostics()
    {
        LastDiagnostic = string.Empty;
        LastResolutionCode = string.Empty;
        LastExecutionCode = string.Empty;
    }

    // ==================== 稳定派生（确定性；不得使用 Guid / 时间） ====================

    /// <summary>本次 Event Node 的稳定触发身份：`expedition-event-node|&lt;NodeId&gt;`。</summary>
    public static string BuildTriggerKey(string nodeId)
    {
        return "expedition-event-node|" + (nodeId ?? string.Empty);
    }

    /// <summary>
    /// ResolutionSeed：稳定派生自 **Expedition 会话 Seed + NodeId + TriggerType**
    /// （同一次远征的同一个 Node 永远得到同一个 Seed → UI 重开 / 卡片刷新不重抽）。
    /// </summary>
    public static int DeriveResolutionSeed(int sessionSeed, string nodeId, string triggerType)
    {
        return StableHash32("expedition-event-seed|1"
            + "|session:" + sessionSeed
            + "|node:" + (nodeId ?? string.Empty)
            + "|trigger:" + (triggerType ?? string.Empty));
    }

    /// <summary>FNV-1a 32 位（只取非负值；与既有 Event / Encounter 派生同族原语）。</summary>
    public static int StableHash32(string text)
    {
        if (string.IsNullOrEmpty(text)) return 0;

        unchecked
        {
            const uint offsetBasis = 2166136261u;
            const uint prime = 16777619u;

            uint hash = offsetBasis;
            for (int i = 0; i < text.Length; i++)
            {
                hash ^= text[i];
                hash *= prime;
            }
            return (int)(hash & 0x7FFFFFFFu);
        }
    }

    // ==================== 节点分类 / 活动事件复用 ====================

    /// <summary>
    /// 该 Node 是否走**正式 Event 产品路径**：所有非战斗类 Node（Event / Story / Discovery / Rest）
    /// 都是 Event 节点（`LegacyMapGraphAdapter` 已把 Legacy Shop / Recruit 占位映射为 Event kind）；
    /// 战斗类（Battle / Elite / Boss）走既有 Battle 原子进入入口，不经本 Flow。
    /// </summary>
    public static bool IsFormalEventNode(AdventureManager adventure, string nodeId,
        out ExpeditionNodeKind kind)
    {
        kind = ExpeditionNodeKind.Event;

        var state = adventure != null ? adventure.ProgressionState : null;
        if (state == null || state.Graph == null || string.IsNullOrEmpty(nodeId)) return false;

        ExpeditionNodeData node;
        if (!state.Graph.TryGetNode(nodeId, out node) || node == null) return false;

        kind = node.NodeKind;
        return kind != ExpeditionNodeKind.Battle
            && kind != ExpeditionNodeKind.Elite
            && kind != ExpeditionNodeKind.Boss;
    }

    /// <summary>
    /// 复用当前活动事件（§3 / §20）：UI 重开 / 卡片刷新必须复用**同一个** EventInstance。
    /// `nodeId` 为空时只检查「是否存在活动事件」。
    /// </summary>
    public static bool TryGetActiveEvent(AdventureManager adventure, string nodeId,
        out ActiveExpeditionEventState active, out string diagnostic)
    {
        active = adventure != null ? adventure.ActiveExpeditionEvent : null;
        diagnostic = string.Empty;

        if (adventure == null)
        {
            diagnostic = "AdventureManager 为空：没有活动事件。";
            return false;
        }

        if (active == null)
        {
            diagnostic = "当前没有活动事件。";
            return false;
        }

        if (!string.IsNullOrEmpty(nodeId)
            && !string.Equals(active.NodeId, nodeId, StringComparison.Ordinal))
        {
            diagnostic = "活动事件属于 Node " + active.NodeId + "，与请求的 " + nodeId + " 不一致。";
            return false;
        }

        return true;
    }

    /// <summary>
    /// 会话结束（Return / Retreat / Expedition End）：清除活动事件与会话事件状态。
    /// 与 `AdventureManager.ResetExpeditionEventSession` 同一实现，只暴露产品语义入口。
    /// </summary>
    public static void ClearForSessionEnd(AdventureManager adventure)
    {
        if (adventure == null) return;
        adventure.ResetExpeditionEventSession();
    }

    /// <summary>本 Node 的现有 Active Event（只读；测试 / 诊断用）。</summary>
    public static ActiveExpeditionEventState ActiveOf(AdventureManager adventure)
    {
        return adventure != null ? adventure.ActiveExpeditionEvent : null;
    }

    // ==================== 1) Event Node 原子进入（§4） ====================

    /// <summary>
    /// **Event Node 的原子进入**（与 WP-Encounter-1C-R 同一原则）：
    /// `ValidateEnterNode`（纯查询）→ 投影深度 / Context → `EventResolver` → **成功才** `TryEnterNode`
    /// → 提交**同一个** `ResolvedEventInstance` 为活动事件。
    ///
    /// 失败 ⇒ **零 mutation**：节点不进入、CurrentNode 不变、Supply / elapsed 不变、ActiveEvent = null；
    /// 且**不**回退 Placeholder Continue / 默认事件 / NodeId 文本事件。
    ///
    /// 幂等复用：同一 Node 已有活动事件 → 返回**同一个实例**（true），不重新求解、不重复进入。
    /// </summary>
    public static bool TryEnterEventNode(AdventureManager adventure, string nodeId,
        out ResolvedEventInstance instance, out string diagnostic)
    {
        instance = null;
        diagnostic = string.Empty;
        LastDiagnostic = string.Empty;
        LastResolutionCode = string.Empty;

        if (adventure == null)
        {
            diagnostic = "AdventureManager 为空：无法进入 Event 节点。";
            LastDiagnostic = diagnostic;
            return false;
        }

        if (adventure.Progression == null || adventure.ProgressionState == null)
        {
            diagnostic = "Progression 会话未初始化：无法进入 Event 节点。";
            LastDiagnostic = diagnostic;
            return false;
        }

        if (string.IsNullOrEmpty(nodeId))
        {
            diagnostic = "nodeId 为空：无法进入 Event 节点。";
            LastDiagnostic = diagnostic;
            return false;
        }

        ExpeditionNodeKind kind;
        if (!IsFormalEventNode(adventure, nodeId, out kind))
        {
            diagnostic = "节点 " + nodeId + " 不是 Event 类节点（战斗类节点走既有 Battle 原子进入入口）。";
            LastDiagnostic = diagnostic;
            return false;
        }

        // 幂等：同一 Node 已有活动事件 → 复用同一实例（UI 重开 / 重复点击不产生第二实例）
        var existing = adventure.ActiveExpeditionEvent;
        if (existing != null)
        {
            if (string.Equals(existing.NodeId, nodeId, StringComparison.Ordinal) && existing.Instance != null)
            {
                instance = existing.Instance;
                return true;
            }

            diagnostic = "已有其它 Node 的活动事件（" + existing
                + "）：V0.1 同一会话至多一条，拒绝进入新 Event 节点（先结算或撤退）。";
            LastDiagnostic = diagnostic;
            return false;
        }

        // 1) 进入校验（纯查询；含 Supply affordability / Phase / 后继合法性）
        var enterCheck = adventure.Progression.ValidateEnterNode(nodeId);
        if (enterCheck != ExpeditionEnterResult.Success)
        {
            diagnostic = "该节点当前不可进入（" + enterCheck
                + "）：节点未被进入，Progression / Supply / elapsed 均未改变。";
            LastDiagnostic = diagnostic;
            return false;
        }

        // 2) 静态配置投影（失败 = 环境问题，明确失败，不进入节点）
        EventStaticCatalog catalog;
        string catalogDiagnostic;
        if (!VillageEventStaticConfigAdapter.TryBuild(out catalog, out catalogDiagnostic))
        {
            LastResolutionCode = EventResolutionCode.ConfigUnavailable.ToString();
            diagnostic = catalogDiagnostic;
            LastDiagnostic = diagnostic;
            return false;
        }

        EventRuntimeBridge.EnsureInitialized();

        // 3) 求解（纯函数；只读 WorldView + World State + 本次远征会话历史）
        int projectedDepth = adventure.ProjectedDepthForNode(nodeId);
        string triggerKey = BuildTriggerKey(nodeId);
        var context = new EventContext()
            .SetString(ContextNodeIdKey, nodeId)
            .SetInt(ContextDepthKey, projectedDepth);

        var request = new EventResolutionRequest(TriggerType, triggerKey,
            DeriveResolutionSeed(adventure.EncounterSessionSeed, nodeId, TriggerType), context);

        var result = EventResolver.Resolve(request, catalog,
            new ExpeditionEventWorldView(adventure, nodeId),
            EventRuntimeBridge.World, adventure.ExpeditionEventSession);

        LastResolutionCode = result.Code.ToString();

        if (!result.Success)
        {
            diagnostic = "该 Event 节点当前无法产生事件（" + result.Code + "）：" + result.Diagnostic
                + " —— 节点未被进入、Supply / elapsed 未改变；本 WP 不提供占位 / 默认事件回退。";
            LastDiagnostic = diagnostic;
            return false;
        }

        // 4) 唯一 mutation 点：进入节点（失败则丢弃本次解析，零半成品）
        var entered = adventure.Progression.TryEnterNode(nodeId);
        if (entered != ExpeditionEnterResult.Success)
        {
            diagnostic = "事件已解析但节点进入失败（" + entered
                + "）：已丢弃本次解析，节点未进入、无常驻活动事件。";
            LastDiagnostic = diagnostic;
            return false;
        }

        // 5) 提交**同一个**实例为活动事件
        string beginDiagnostic;
        if (!adventure.TryBeginActiveExpeditionEvent(nodeId, result.Instance, out beginDiagnostic))
        {
            diagnostic = "活动事件建立失败：" + beginDiagnostic;
            LastDiagnostic = diagnostic;
            return false;
        }

        instance = result.Instance;
        LastDiagnostic = string.Empty;
        return true;
    }

    // ==================== 2) Option → Outcome → Effect（§9 / §10 / §11 / §12 / §13） ====================

    /// <summary>
    /// 预测本次 Option 将选中的 Outcome（与正式执行**同一**选择函数 → 同一 Outcome）。
    /// 用途：StartBattle Preflight 与 Node Settlement 可行性检查都必须在 Commit **之前**知道结果身份。
    /// </summary>
    public static bool TryPreviewOutcome(AdventureManager adventure, string optionId,
        out EventOutcomeDefinition outcome, out string diagnostic)
    {
        outcome = null;
        diagnostic = string.Empty;

        ActiveExpeditionEventState active;
        if (!TryGetActiveEvent(adventure, null, out active, out diagnostic)) return false;

        EventStaticCatalog catalog;
        string catalogDiagnostic;
        if (!VillageEventStaticConfigAdapter.TryBuild(out catalog, out catalogDiagnostic))
        {
            diagnostic = catalogDiagnostic;
            return false;
        }

        outcome = EventOptionExecutor.PreviewOutcome(active.Instance, optionId, catalog);
        if (outcome == null)
        {
            diagnostic = "选项 " + optionId + " 没有可确定的 Outcome（没有配置 / 实例为空）。";
            return false;
        }

        return true;
    }

    /// <summary>
    /// **Node Settlement 可行性检查（§9 / §10）**：按**有序** Effect 序列投影补给与额外时间，
    /// 然后证明「执行完整 Effects 后当前 Event Node 仍能完成 Progression Settlement」：
    ///
    /// <code>
    /// projectedSupply = Supply + Σ SupplyDelta（按 Order 顺序，逐条不得 &lt; 0）
    /// pendingExtraDays = 已累积 + Σ AdvanceExpeditionTime
    /// totalElapsed = Node.NodeElapsedDays + pendingExtraDays
    /// totalCost    = PartySize × totalElapsed × PerPersonDailyRate + Node.ExtraSupplyCost
    /// 要求：projectedSupply &gt;= totalCost
    /// </code>
    ///
    /// 不通过 ⇒ Option 必须被拒绝（零 mutation）——否则会出现
    /// 「Option 已执行 + History 已写，但最后 Continue 时才发现补给不足 → Node 永久卡住」的软锁。
    /// </summary>
    public static bool TryEvaluateSettlementFeasibility(AdventureManager adventure,
        IReadOnlyList<EventEffectDefinition> effects,
        out int projectedSupply, out int pendingExtraDays, out int totalCost, out string diagnostic)
    {
        projectedSupply = 0;
        pendingExtraDays = 0;
        totalCost = 0;
        diagnostic = string.Empty;

        var state = adventure != null ? adventure.ProgressionState : null;
        if (state == null || state.Supply == null)
        {
            diagnostic = "没有进行中的远征会话：无法校验 Node Settlement 可行性。";
            return false;
        }

        string nodeId = state.CurrentNodeId;
        ExpeditionNodeData node;
        if (string.IsNullOrEmpty(nodeId) || state.Graph == null || !state.Graph.TryGetNode(nodeId, out node)
            || node == null)
        {
            diagnostic = "当前没有已进入的 Node：无法校验 Node Settlement 可行性。";
            return false;
        }

        int supply = state.Supply.CurrentCarryUnits;
        int extraDays = adventure.ActiveExpeditionEvent != null
            ? adventure.ActiveExpeditionEvent.PendingExtraElapsedDays
            : 0;

        if (effects != null)
        {
            for (int i = 0; i < effects.Count; i++)
            {
                var effect = effects[i];
                if (effect == null) continue;

                if (string.Equals(effect.EffectType, EventEffectTypes.SupplyDelta, StringComparison.Ordinal))
                {
                    int after = supply + effect.IntValue;
                    if (after < 0)
                    {
                        diagnostic = "按 Order 顺序执行效果后远征补给会变为 " + after + "（不允许 < 0）："
                            + "该 Option 被拒绝（零 mutation：Pending 保留、History 不写）。";
                        // Producer Acceptance Repair（Issue B/C）：失败路径同样回填投影值，
                        // 供只读预测计算「至少还差多少补给」（不改变任何判定逻辑）。
                        projectedSupply = after;
                        pendingExtraDays = extraDays;
                        totalCost = 0;
                        return false;
                    }
                    supply = after;
                }
                else if (string.Equals(effect.EffectType, EventEffectTypes.AdvanceExpeditionTime,
                             StringComparison.Ordinal))
                {
                    extraDays += effect.IntValue;
                }
            }
        }

        long totalElapsed;
        try { totalElapsed = checked(node.DurationMinutes + (long)extraDays * 1440); }
        catch (OverflowException) { diagnostic = "Expedition duration overflow"; projectedSupply = supply; pendingExtraDays = extraDays; totalCost = 0; return false; }
        if (totalElapsed < 0 || totalElapsed / 1440 > int.MaxValue)
        {
            diagnostic = "额外远征时间使总时间越界（NodeElapsedDays=" + node.NodeElapsedDays
                + " + 额外 " + extraDays + "）：该 Option 被拒绝（零 mutation）。";
            projectedSupply = supply;
            pendingExtraDays = extraDays;
            totalCost = 0;
            return false;
        }

        if (!state.Supply.TryComputeMinuteCost(state.ExpeditionElapsedMinutes, totalElapsed, node.ExtraSupplyCost, out totalCost, out _))
        { diagnostic = "Expedition supply overflow"; projectedSupply = supply; pendingExtraDays = extraDays; return false; }
        if (supply < totalCost)
        {
            diagnostic = "执行该 Option 后本 Node 无法完成结算：预计结算时补给 " + supply
                + " < 本 Node 总成本 " + totalCost + "（NodeElapsedDays=" + node.NodeElapsedDays
                + " + 额外 " + extraDays + "；party=" + state.Supply.PartySize
                + "，rate=" + state.Supply.PerPersonDailyConsumptionRate
                + "，extraCost=" + node.ExtraSupplyCost + "）"
                + "：该 Option 被拒绝（零 mutation：Pending 保留、History 不写、可改选其它 Option）。";
            projectedSupply = supply;
            pendingExtraDays = extraDays;
            return false;
        }

        projectedSupply = supply;
        pendingExtraDays = extraDays;
        return true;
    }

    /// <summary>
    /// **Option 可行性只读预测（WP-Event-1 Producer Acceptance Repair｜Issue C）**：
    /// 在玩家**点击之前**用只读方式证明「这个 Option 当前必然无法执行」（例如结算补给不足），
    /// 供卡面直接置灰 + 显示短原因，而不是等玩家点击后由 Executor 的 Preflight 报错。
    ///
    /// **不复制判定逻辑**：复用既有两个只读能力 ——
    ///   - <see cref="TryPreviewOutcome"/>（与正式执行**同一**选择函数 → 同一 Outcome 身份）；
    ///   - <see cref="TryEvaluateSettlementFeasibility"/>（有序 SupplyDelta / AdvanceExpeditionTime 投影 + 结算成本）。
    ///
    /// 只读保证：不 Commit、不写 History、不改变 Outcome、不消耗 / 改变 RNG、不修改任何状态
    ///（StartBattle 需要预生成阵容 → 本方法**不**预生成，因此也不会「证明可执行」，只可能证明不可行）。
    ///
    /// 语义：返回 false = 当前**无法评估**（不做任何 UI 判断）；返回 true 时看
    /// <see cref="EventOptionFeasibilityPreview.Applicable"/> —— false（含「能证明可结算」）表示
    /// **不得**据此置灰；true 且 `Feasible == false` 才置灰。
    /// </summary>
    public static bool TryPreviewOptionFeasibility(AdventureManager adventure, string optionId,
        out EventOptionFeasibilityPreview preview)
    {
        preview = default(EventOptionFeasibilityPreview);   // Applicable=false → 调用方不得据此置灰
        if (adventure == null || string.IsNullOrEmpty(optionId)) return false;

        // 前置：必须有进行中的 Progression 会话与已进入的 Node（否则无法给出确定性结论）。
        var state = adventure.ProgressionState;
        ExpeditionNodeData node;
        if (state == null || state.Supply == null || state.Graph == null
            || string.IsNullOrEmpty(state.CurrentNodeId)
            || !state.Graph.TryGetNode(state.CurrentNodeId, out node) || node == null)
            return false;

        ActiveExpeditionEventState active;
        string activeDiagnostic;
        if (!TryGetActiveEvent(adventure, null, out active, out activeDiagnostic)) return false;
        if (active.Phase != ExpeditionEventPhase.AwaitingOption) return false;

        EventStaticCatalog catalog;
        string catalogDiagnostic;
        if (!VillageEventStaticConfigAdapter.TryBuild(out catalog, out catalogDiagnostic)) return false;

        var outcome = EventOptionExecutor.PreviewOutcome(active.Instance, optionId, catalog);
        if (outcome == null) return false;      // Outcome 不确定 → 不预测

        var effects = catalog.GetEffects(outcome.OutcomeId);

        int projectedSupply;
        int pendingDays;
        int totalCost;
        string feasibility;
        if (TryEvaluateSettlementFeasibility(adventure, effects, out projectedSupply, out pendingDays,
                out totalCost, out feasibility))
        {
            // 能证明「执行完 Effects 后本 Node 仍可结算」→ 不置灰（仍可能有运行期前置，如 StartBattle 阵容）。
            return true;
        }

        preview.Applicable = true;
        preview.Feasible = false;
        preview.ProjectedSupply = projectedSupply;
        preview.PendingExtraDays = pendingDays;
        preview.TotalCost = totalCost;
        preview.Diagnostic = feasibility;
        preview.PlayerReason = EventOptionFeasibilityPreview.BuildPlayerReason(projectedSupply, totalCost);
        return true;
    }

    /// <summary>
    /// **正式执行当前活动事件的 Option**（产品唯一入口）：
    ///   1. 阶段守卫（AwaitingOption；重复执行 / 其它阶段一律拒绝，exactly-once）；
    ///   2. 预确定 Outcome（与正式执行同一选择函数）；
    ///   3. **§11 StartBattle Preflight**：按 Event 指定 PoolId 预生成合法 EnemyRoster（失败即整笔拒绝）；
    ///   4. **§9/§10 Node Settlement 可行性**：有序投影后仍能结算（失败即整笔拒绝）；
    ///   5. `EventOptionExecutor.Execute`（条件重检 → Outcome → Plan → Preflight → Commit → History → Completed）。
    ///
    /// 任一前置失败 = **零 mutation**：Pending 保留、History 不写、Completed 不写、Supply / elapsed 不变，
    /// 节点**仍停留 Event Pending**（玩家可重试或选择其它 Option）。
    /// </summary>
    public static bool TryExecuteActiveOption(AdventureManager adventure, string optionId,
        out EventOptionExecutionResult result, out string diagnostic)
    {
        result = null;
        diagnostic = string.Empty;
        LastExecutionCode = string.Empty;

        ActiveExpeditionEventState active;
        if (!TryGetActiveEvent(adventure, null, out active, out diagnostic))
        {
            LastDiagnostic = diagnostic;
            return false;
        }

        if (active.Phase != ExpeditionEventPhase.AwaitingOption)
        {
            diagnostic = "活动事件当前阶段为 " + active.Phase
                + "：拒绝重复执行 Option（exactly-once；请「继续」或走 Battle 结果结算）。";
            LastDiagnostic = diagnostic;
            return false;
        }

        if (string.IsNullOrEmpty(optionId))
        {
            diagnostic = "optionId 为空：无法执行选项。";
            LastDiagnostic = diagnostic;
            return false;
        }

        EventStaticCatalog catalog;
        string catalogDiagnostic;
        if (!VillageEventStaticConfigAdapter.TryBuild(out catalog, out catalogDiagnostic))
        {
            diagnostic = catalogDiagnostic;
            LastDiagnostic = diagnostic;
            return false;
        }

        var outcome = EventOptionExecutor.PreviewOutcome(active.Instance, optionId, catalog);
        if (outcome == null)
        {
            diagnostic = "选项 " + optionId + " 没有可确定的 Outcome。";
            LastDiagnostic = diagnostic;
            return false;
        }

        var effects = catalog.GetEffects(outcome.OutcomeId);
        var gateway = new ExpeditionEventEffectGateway(adventure, active, optionId, outcome.OutcomeId);

        // §11：StartBattle 必须在**零 mutation 阶段**完成阵容预生成（失败即整笔 Option 失败）
        for (int i = 0; i < effects.Count; i++)
        {
            var effect = effects[i];
            if (effect == null) continue;
            if (!string.Equals(effect.EffectType, EventEffectTypes.StartBattle, StringComparison.Ordinal)) continue;

            string battleReason;
            if (!gateway.CanStartBattle(effect.TargetKey, out battleReason))
            {
                diagnostic = "StartBattle Preflight 失败（零 mutation：Pending 保留、History 不写、"
                    + "节点仍停留 Event Pending）：" + battleReason;
                active.LastDiagnostic = diagnostic;
                LastDiagnostic = diagnostic;
                return false;
            }
        }

        // §9 / §10：执行完整有序 Effect 后必须仍能完成当前 Node 的 Progression Settlement
        int projectedSupply;
        int pendingDays;
        int totalCost;
        string feasibility;
        if (!TryEvaluateSettlementFeasibility(adventure, effects, out projectedSupply, out pendingDays,
                out totalCost, out feasibility))
        {
            diagnostic = feasibility;
            active.LastDiagnostic = diagnostic;
            LastDiagnostic = diagnostic;
            return false;
        }

        EventRuntimeBridge.EnsureInitialized();

        var execution = EventOptionExecutor.Execute(active.Instance, optionId, catalog,
            new ExpeditionEventWorldView(adventure, active.NodeId), EventRuntimeBridge.World,
            adventure.ExpeditionEventSession, adventure.ExpeditionEventExecution, gateway);

        result = execution;
        LastExecutionCode = execution.Code.ToString();

        if (!execution.Success)
        {
            diagnostic = execution.Code + "：" + execution.Diagnostic;
            active.LastDiagnostic = diagnostic;
            LastDiagnostic = diagnostic;
            return false;
        }

        active.LastOptionId = optionId;
        active.LastOutcomeId = execution.OutcomeId;
        active.LastOutcomeHasBattle = execution.HasTerminalEffect;
        active.LastResultText = RenderResultText(adventure, active.Instance, outcome);
        active.LastDiagnostic = string.Empty;
        active.Phase = execution.HasTerminalEffect
            ? ExpeditionEventPhase.AwaitingBattleOutcome
            : ExpeditionEventPhase.AwaitingResultContinue;

        LastDiagnostic = string.Empty;
        return true;
    }

    /// <summary>
    /// 渲染 Outcome 的结果文本（唯一渲染器 `EventTemplateRenderer`）。
    /// 渲染失败 → 返回空串（Fail Closed：不把原始占位符显示给玩家；原因写入 <see cref="LastDiagnostic"/>）。
    /// </summary>
    public static string RenderResultText(AdventureManager adventure, ResolvedEventInstance instance,
        EventOutcomeDefinition outcome)
    {
        if (instance == null || outcome == null) return string.Empty;

        var rendered = EventTemplateRenderer.Render(outcome.ResultTextTemplate, instance,
            PersonDisplayNameResolver(adventure), "Outcome.ResultTextTemplate");

        if (rendered.Success) return rendered.Text;

        LastDiagnostic = rendered.Diagnostic;
        return string.Empty;
    }

    /// <summary>
    /// Person 显示名解析：只查正式长期人物真相源（本次远征的 PlayerCollection），
    /// 找不到 → false（绝不用 RoleName / PersonId 冒充显示名）。
    /// </summary>
    public static EventPersonDisplayNameResolver PersonDisplayNameResolver(AdventureManager adventure)
    {
        return (string personId, out string displayName) =>
        {
            displayName = null;
            if (string.IsNullOrEmpty(personId) || adventure == null) return false;

            var source = adventure.PlayerData;
            if (source == null) return false;

            var person = source.FindPersonByPersonId(personId);
            if (person == null || string.IsNullOrEmpty(person.DisplayName)) return false;

            displayName = person.DisplayName;
            return true;
        };
    }

    // ==================== 3) Node 结算（§8 / §14 / §15） ====================

    /// <summary>
    /// **非战斗结果「继续」**（§15）：把当前 Event Node 以 `Success + PendingExtraElapsedDays` 结算
    /// （elapsed 与 Supply 成本在同一次 exactly-once settlement 内完成）。
    /// 成功 → 清除活动事件（Journey 继续）；失败 → **明确诊断**，节点停留 Event Pending（不假装已经继续）。
    /// </summary>
    public static bool TryContinueActiveEvent(AdventureManager adventure,
        out ExpeditionNodeResolutionData data, out string diagnostic)
    {
        data = null;
        diagnostic = string.Empty;

        ActiveExpeditionEventState active;
        if (!TryGetActiveEvent(adventure, null, out active, out diagnostic))
        {
            LastDiagnostic = diagnostic;
            return false;
        }

        if (active.Phase != ExpeditionEventPhase.AwaitingResultContinue)
        {
            diagnostic = "活动事件阶段为 " + active.Phase + "：只有非战斗结果可以「继续」结算当前 Node。";
            LastDiagnostic = diagnostic;
            return false;
        }

        var resolve = adventure.TryResolveCurrentNode(ExpeditionNodeOutcome.Success,
            active.PendingExtraElapsedDays, out data);

        if (resolve != ExpeditionResolveResult.Success)
        {
            diagnostic = "当前 Event Node 结算未成功（" + resolve + "）：节点停留 Event Pending，未假装继续；"
                + "PendingExtraElapsedDays=" + active.PendingExtraElapsedDays + "。";
            active.LastDiagnostic = diagnostic;
            LastDiagnostic = diagnostic;
            return false;
        }

        string clearDiagnostic;
        adventure.ClearActiveExpeditionEvent(out clearDiagnostic);
        LastDiagnostic = string.Empty;
        return true;
    }

    /// <summary>
    /// **Event 内 Battle 的最终 outcome → 结算同一个 Event Node**（§14）：
    /// Victory → `Success` / Defeat → `Failure`；两者都使用 `PendingExtraElapsedDays` 完成
    /// elapsed + Supply 的 exactly-once settlement，并在结算成功后清除活动事件（Active Encounter
    /// 由 `AdventureManager.TryResolveCurrentNode(..., additionalElapsedDays, ...)` 一并清除）。
    /// </summary>
    public static bool TryResolveActiveEventBattle(AdventureManager adventure, bool victory,
        out ExpeditionNodeResolutionData data, out ExpeditionResolveResult resolveResult, out string diagnostic)
    {
        data = null;
        resolveResult = ExpeditionResolveResult.NotInitialized;
        diagnostic = string.Empty;

        ActiveExpeditionEventState active;
        if (!TryGetActiveEvent(adventure, null, out active, out diagnostic))
        {
            LastDiagnostic = diagnostic;
            return false;
        }

        if (active.Phase != ExpeditionEventPhase.AwaitingBattleOutcome)
        {
            diagnostic = "活动事件阶段为 " + active.Phase + "：当前不是 Event 内战斗的等待结算阶段。";
            LastDiagnostic = diagnostic;
            return false;
        }

        var outcome = victory ? ExpeditionNodeOutcome.Success : ExpeditionNodeOutcome.Failure;
        resolveResult = adventure.TryResolveCurrentNode(outcome, active.PendingExtraElapsedDays, out data);

        if (resolveResult != ExpeditionResolveResult.Success)
        {
            diagnostic = "Event 内战斗结果未能结算当前 Node（" + resolveResult + "）：节点停留 Event Pending，"
                + "活动事件保留（不静默丢弃）；PendingExtraElapsedDays=" + active.PendingExtraElapsedDays + "。";
            active.LastDiagnostic = diagnostic;
            LastDiagnostic = diagnostic;
            return false;
        }

        string clearDiagnostic;
        adventure.ClearActiveExpeditionEvent(out clearDiagnostic);
        LastDiagnostic = string.Empty;
        return true;
    }
}






