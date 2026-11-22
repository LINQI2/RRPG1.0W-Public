using RRPG.Encounter;
using UnityEngine;

/// <summary>
/// **当前遭遇运行时状态**（Active Encounter；WP-Encounter-1C §6）。
///
/// 唯一 Authority：本次远征中「当前 Battle 类 Node 的敌方阵容」只有这一份。
/// 生命周期规则（由 <see cref="AdventureManager"/> 持有并执行）：
///   - 第一次进入当前 Battle Node → 生成一次 EnemyRoster；
///   - Prep 重开 / Battle 真正开始 / Battle UI 重建 → **复用**同一份（不重抽）；
///   - Battle 成功/失败 Resolve 当前 Node 后、Return / Retreat / Expedition End → 清除。
///
/// 边界：Runtime Only —— 不进 Save、不进 Person、不进 RunState；只保存诊断所需的
/// (NodeId / PoolId / Depth / Seed / Roster)。
/// </summary>
public sealed class ActiveEncounterState
{
    /// <summary>本状态对应的 Progression NodeId（复用判据：必须等于当前 CurrentNodeId）。</summary>
    public string NodeId;

    /// <summary>本次使用的 EncounterPoolId（V0.1 Compatibility 选择器结果或诊断覆盖值）。</summary>
    public string PoolId;

    /// <summary>本次 EncounterDepth（= 已进入 Node 数量 - 1）。</summary>
    public int Depth;

    /// <summary>本次实际使用的 EncounterSeed（便于诊断 / 复现；由 SessionSeed + NodeId + Depth 稳定派生）。</summary>
    public int Seed;

    /// <summary>冻结的敌方阵容（本场 Prep 与 Battle 消费的同一份对象）。</summary>
    public EnemyRoster Roster;

    /// <summary>阵容来源遭遇（诊断用）。</summary>
    public string EncounterId { get { return Roster != null ? Roster.EncounterId : null; } }

    public override string ToString()
    {
        return "ActiveEncounter[node=" + NodeId + " pool=" + PoolId + " depth=" + Depth
            + " seed=" + Seed + " " + (Roster != null ? Roster.ToString() : "<null>") + "]";
    }
}

/// <summary>
/// AdventureManager 的 **Encounter 适配层**（WP-Encounter-1C §5 / §6 / §7）。
///
/// 为什么放在这里而不是新建 Manager：现有 AdventureManager 已是本次远征的唯一 Orchestrator
/// （持有 Progression / RunState / 玩家数据）；本轮只做**最小扩展**，不新建第二套顶层 Manager。
///
/// 职责：
///   1. Expedition Session Seed：每次新远征会话生成一次 Runtime Only 种子（不进 Save）；
///   2. 单场 EncounterSeed：(SessionSeed, CurrentNodeId, EncounterDepth) 稳定派生；
///   3. Active EnemyRoster 的唯一宿主与生命周期（生成 / 复用 / 清除）；
///   4. 生成失败时**明确失败**：不进入空战斗、不回退「全部 Monster Registry」、不生成默认 3 怪。
/// </summary>
public partial class AdventureManager
{
    /// <summary>
    /// 本次远征会话的 EncounterSessionSeed（Runtime Only；不进 Save，不由 UI 重开 / Scene 往返改变）。
    /// 来源：产品适配层一次性随机（环境安全随机源），EncounterGenerator 内部仍完全确定性。
    /// </summary>
    private readonly int _encounterSessionSeed = NewEncounterSessionSeed();

    /// <summary>懒创建的生成器（只读快照 + 正式静态配置；失败时不缓存，允许后续重试）。</summary>
    private EncounterGenerator _encounterGenerator;

    /// <summary>
    /// 【V0.1 Compatibility / Test Pool Selector 覆盖 seam】为 null 时按节点类别映射；
    /// 非 null 时强制使用该 PoolId（诊断 / 测试「指定 Pool 缺失必须明确失败」）。
    /// 这是**兼容与诊断用**临时 seam，不是长期 Node Content → EncounterPool 设计。
    /// </summary>
    public string EncounterPoolOverrideForDiagnostics { get; set; }

    /// <summary>当前 Active Encounter（未进入 Battle 类 Node / 已清除时为 null）。</summary>
    public ActiveEncounterState ActiveEncounter { get; private set; }

    /// <summary>本次远征会话的 EncounterSessionSeed（只读；诊断 / 复现用）。</summary>
    public int EncounterSessionSeed { get { return _encounterSessionSeed; } }

    /// <summary>当前是否已冻结 Active EnemyRoster（只读诊断）。</summary>
    public bool HasActiveEnemyRoster
    {
        get { return ActiveEncounter != null && ActiveEncounter.Roster != null; }
    }

    /// <summary>
    /// 取当前 Battle 类 Node 的 **Active EnemyRoster**（唯一正式入口）。
    ///
    /// 复用规则：当前 Node 已有 Active Encounter → 直接返回同一份（Prep 重开 / Battle 开始 /
    /// Battle UI 重建都不会重抽）；否则生成一次并冻结。
    ///
    /// 失败（静态配置不可用 / 池不存在 / 当前深度无候选 / 全部遭遇无解 / 搜索上限耗尽）→
    /// 返回 false + 可操作诊断，且 **ActiveEncounter 保持 null**：
    /// 调用方必须因此**拒绝进入战斗**（不得回退全部怪物、不得生成默认 3 怪、不得空战斗）。
    /// </summary>
    public bool TryGetActiveEnemyRoster(out EnemyRoster roster, out string diagnostic)
    {
        var state = ProgressionState;
        string currentNodeId = state != null ? state.CurrentNodeId : null;

        // 1. 复用：同一 CurrentNode 已有冻结阵容
        if (ActiveEncounter != null
            && ActiveEncounter.Roster != null
            && !string.IsNullOrEmpty(currentNodeId)
            && ActiveEncounter.NodeId == currentNodeId)
        {
            roster = ActiveEncounter.Roster;
            diagnostic = string.Empty;
            return true;
        }

        // 2. 陈旧状态（已换 Node）：先清除，避免跨 Node 误用
        if (ActiveEncounter != null) ClearActiveEnemyRoster();

        return TryGenerateActiveEnemyRoster(currentNodeId, out roster, out diagnostic);
    }

    /// <summary>清除 Active Encounter（Node 已结算 / Return / Retreat / Expedition End）。</summary>
    public void ClearActiveEnemyRoster()
    {
        ActiveEncounter = null;
    }

    // ==================== WP-Encounter-1C-R（R1）：Battle 类节点的原子进入 ====================

    /// <summary>
    /// **Battle / Elite / Boss 节点的原子进入**（WP-Encounter-1C-R R1）。
    ///
    /// 正式规则：先「进入校验（纯查询）→ 预生成 Encounter」全部成功，之后才真正 mutation 为 Entered。
    /// 遭遇生成失败 ⇒ **零 mutation**：节点不进入、CurrentNode 不变、Supply / elapsed 不变、
    /// 不留半成品 ActiveEncounter；玩家仍停在原 Journey 状态，可重新选择其它合法路径或重试（不需要 Retreat）。
    ///
    /// 顺序（不得调换）：
    ///   1. <see cref="ExpeditionProgressionResolver.ValidateEnterNode"/>（纯查询：后继合法性 / Supply / Phase）；
    ///   2. ProjectedEncounterDepth =「进入后的 EnteredNodeCount − 1」= 当前已进入数量（不读 PosY / UI 坐标）；
    ///   3. 用 (SessionSeed, targetNodeId, ProjectedDepth, 兼容池选择器) **预生成** EnemyRoster；
    ///   4. 预生成成功后才 <see cref="AdventureManager.TryEnterNode"/>（唯一 mutation 点）；此处失败则丢弃预生成阵容；
    ///   5. Enter 成功 → 把**同一份**预生成 Roster 提交为 ActiveEncounter（后续 Prep / Battle 不得重新 Generate）。
    /// </summary>
    public bool TryPrepareAndEnterBattleNode(string nodeId, out EnemyRoster roster, out string diagnostic)
    {
        roster = null;

        if (Progression == null)
        {
            diagnostic = "Progression 会话未初始化，无法进入战斗节点。";
            return false;
        }

        if (string.IsNullOrEmpty(nodeId))
        {
            diagnostic = "nodeId 为空，无法进入战斗节点。";
            return false;
        }

        // 1. 纯查询校验（零 mutation）
        var enterResult = Progression.ValidateEnterNode(nodeId);
        if (enterResult != ExpeditionEnterResult.Success)
        {
            diagnostic = "该节点当前不可进入（" + enterResult + "）：节点未被进入，Progression / Supply / elapsed 均未改变。";
            return false;
        }

        // 2. 预计遭遇深度 = 进入后的 EnteredNodeCount - 1（例：尚未进入任何 Node → 0；已进入 1 个 → 1）
        var state = Progression.State;
        int enteredCount = state.GetEnteredNodeIds().Count;
        int projectedDepth = EncounterDepthRule.DepthFor(enteredCount + 1);

        // 3. 预生成（不写任何状态）
        EnemyRoster prepared;
        string poolId;
        int seed;
        if (!TryGenerateRosterFor(nodeId, projectedDepth, out prepared, out poolId, out seed, out diagnostic))
        {
            // 明确告知调用方 / 玩家：这里是**零 mutation** 失败，节点仍可重选或重试（不需要 Retreat）。
            diagnostic = diagnostic
                + "（节点未被进入：Progression / Supply / elapsed 均未改变，可重新选择其它路径或重试）";
            return false;
        }

        // 4. 预生成成功后才正式进入（唯一 mutation 点）
        var entered = Progression.TryEnterNode(nodeId);
        if (entered != ExpeditionEnterResult.Success)
        {
            // 意外状态变化：丢弃预生成阵容，不建立 ActiveEncounter（仍然零半成品）
            diagnostic = "遭遇已预生成，但正式进入节点失败（" + entered
                + "）：已丢弃预生成阵容，未建立 Active Encounter，节点未进入。";
            Debug.LogError("[AdventureManager] " + diagnostic);
            return false;
        }

        // 5. 提交：预生成的同**一份** Roster 成为本场 Active EnemyRoster（Prep / Battle 复用，不重新生成）
        ActiveEncounter = new ActiveEncounterState
        {
            NodeId = nodeId,
            PoolId = poolId,
            Depth = projectedDepth,
            Seed = seed,
            Roster = prepared
        };

        Debug.Log("[AdventureManager] Battle 节点原子进入成功：" + ActiveEncounter
            + "（sessionSeed=" + _encounterSessionSeed + "）");

        roster = prepared;
        diagnostic = string.Empty;
        return true;
    }

    // ==================== 内部：生成 ====================

    private bool TryGenerateActiveEnemyRoster(string currentNodeId, out EnemyRoster roster, out string diagnostic)
    {
        roster = null;

        if (string.IsNullOrEmpty(currentNodeId))
        {
            diagnostic = "当前没有进行中的 Progression Node（无法确定本场敌方遭遇）；"
                + "Battle 类 Node 必须先在 Progression 会话中成功 Enter。";
            return false;
        }

        var state = ProgressionState;
        int depth = EncounterDepthRule.DepthFor(state.GetEnteredNodeIds().Count);

        EnemyRoster generated;
        string poolId;
        int seed;
        if (!TryGenerateRosterFor(currentNodeId, depth, out generated, out poolId, out seed, out diagnostic)) return false;

        ActiveEncounter = new ActiveEncounterState
        {
            NodeId = currentNodeId,
            PoolId = poolId,
            Depth = depth,
            Seed = seed,
            Roster = generated
        };

        Debug.Log("[AdventureManager] 敌方阵容已冻结：" + ActiveEncounter
            + "（sessionSeed=" + _encounterSessionSeed + "）");

        roster = generated;
        return true;
    }

    /// <summary>
    /// **纯生成**（零状态写入）：按 (nodeId, depth, 兼容池选择器, SessionSeed) 生成一份 EnemyRoster。
    /// 失败返回 false + 可操作诊断（含失败码 / PoolId / NodeId / 逐候选尝试）。
    ///
    /// 两类调用方：
    ///   - <see cref="TryPrepareAndEnterBattleNode"/>（R1 原子进入）：生成成功后才 Enter 并提交；
    ///   - <see cref="TryGenerateActiveEnemyRoster"/>（按需冻结）：生成成功后立即提交为 ActiveEncounter。
    /// </summary>
    private bool TryGenerateRosterFor(string nodeId, int depth, out EnemyRoster roster,
        out string poolId, out int seed, out string diagnostic)
    {
        roster = null;

        string poolReason;
        poolId = ResolveEncounterPoolId(nodeId, out poolReason);
        seed = EncounterSeedDerivation.DeriveSeed(_encounterSessionSeed, nodeId, depth);

        var generator = GetEncounterGenerator(out string generatorDiagnostic);
        if (generator == null)
        {
            diagnostic = "无法生成敌方阵容：" + generatorDiagnostic;
            return false;
        }

        var result = generator.GenerateFromPool(poolId, depth, seed);
        if (!result.Success)
        {
            diagnostic = "Node " + nodeId + " 的敌方遭遇生成失败（pool=" + poolId
                + " depth=" + depth + " seed=" + seed + "）：" + result.Failure + " —— " + result.Diagnostic
                + DescribeAttempts(result);

            Debug.LogError("[AdventureManager] " + diagnostic + "（不进入战斗、不回退全部怪物、不生成默认阵容）");
            return false;
        }

        Debug.Log("[AdventureManager] 敌方阵容已生成（未提交）：" + result.Roster
            + "（pool 来源：" + poolReason + " / sessionSeed=" + _encounterSessionSeed + "）");

        roster = result.Roster;
        diagnostic = string.Empty;
        return true;
    }

    private EncounterGenerator GetEncounterGenerator(out string diagnostic)
    {
        if (_encounterGenerator != null)
        {
            diagnostic = string.Empty;
            return _encounterGenerator;
        }

        var generator = EncounterGenerator.FromStaticConfig(out diagnostic);
        if (generator == null) return null;

        _encounterGenerator = generator;
        return _encounterGenerator;
    }

    /// <summary>
    /// V0.1 Compatibility / Test Pool Selector（§7）：
    /// 诊断覆盖 &gt; 节点类别映射（普通 → Normal 池；精英 / Boss → Deep 池）。
    /// 不新增正式内容 Authority：Node → Pool 的长期关系仍未建立。
    /// </summary>
    private string ResolveEncounterPoolId(string nodeId, out string reason)
    {
        if (!string.IsNullOrEmpty(EncounterPoolOverrideForDiagnostics))
        {
            reason = "诊断覆盖（EncounterPoolOverrideForDiagnostics）";
            return EncounterPoolOverrideForDiagnostics;
        }

        var category = ResolveEncounterNodeCategory(nodeId);
        reason = "V0.1 Compatibility 映射（节点类别 " + category + "）";
        return EncounterPoolSelector.PoolIdFor(category);
    }

    private EncounterNodeCategory ResolveEncounterNodeCategory(string nodeId)
    {
        var state = ProgressionState;
        ExpeditionNodeData node;
        if (state != null && state.Graph != null && state.Graph.TryGetNode(nodeId, out node) && node != null)
        {
            switch (node.NodeKind)
            {
                case ExpeditionNodeKind.Elite: return EncounterNodeCategory.Elite;
                case ExpeditionNodeKind.Boss: return EncounterNodeCategory.Boss;
                default: return EncounterNodeCategory.Normal;
            }
        }

        return EncounterNodeCategory.Normal;
    }

    private static string DescribeAttempts(EncounterGenerationResult result)
    {
        if (result == null || result.Attempts == null || result.Attempts.Count == 0) return string.Empty;

        var builder = new System.Text.StringBuilder("；池内逐个候选尝试：");
        for (int i = 0; i < result.Attempts.Count; i++)
        {
            if (i > 0) builder.Append("；");
            builder.Append(result.Attempts[i]);
        }
        return builder.ToString();
    }

    /// <summary>
    /// **每次远征会话只调用一次**：生成 SessionSeed。
    ///
    /// 这是产品适配层的「环境安全随机源」，不进入 Encounter 域：
    /// <see cref="EncounterGenerator"/> 内部仍然禁止 UnityEngine.Random / 时间 / Guid（1B 源码级守卫）。
    /// 本值 Runtime Only：不进 Save、不由 UI 重开 / Battle Scene 往返改变。
    /// </summary>
    private static int NewEncounterSessionSeed()
    {
        var bytes = System.Guid.NewGuid().ToByteArray();
        return System.BitConverter.ToInt32(bytes, 0);
    }
}
