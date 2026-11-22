using System;
using System.Collections.Generic;

/// <summary>
/// WP-1 Stage2 —— LegacyMapGraphAdapter（Compatibility-only 定义导入层；Assembly-CSharp）。
///
/// 职责（FDS §7.4 / §9 / §11.2B）：
/// - 把 Legacy `MapGenerator.Generate` 产出的 4×3 点阵 Map 一次性导入为
///   `ExpeditionGraph`（ExpeditionNodeData 定义 + Connections + 入口 NodeId）；
/// - NodeId 与 Legacy MapNode.NodeId 保持一致（视图镜像可复用）；
/// - NodeKind 映射：Battle/Elite/Boss → 对应 kind；Event → Event；
///   Legacy Shop/Rest/Recruit 是旧生态占位（FP-V0.1 不做正式 Shop/Recruit/Rest 生态）：
///   Shop/Recruit 映射为 Event 占位、Rest → Rest，仅为保留 4×3 拓扑连通的兼容映射，
///   不构成正式内容生态，也不代表任何新 Progression Authority；
/// - SuccessfulRouteTerminal = Legacy NodeType.Boss（Boss 作为 FP-V0.1 Legacy 映射的成功路线终点）；
/// - 不读取 Reachable/Visited/Selected/CurrentNode 等 Legacy runtime flags 作为规则依据；
///   入口 NodeId 通过「图中唯一零入度节点」这一图结构性质确定。
///
/// 边界：只做定义导入与类型映射；不产生运行规则、不持久化、不修改 MapGenerator/MapNode。
/// 正式 Node 生成/内容池未来替换时，只需替换本 adapter。
/// </summary>
public static class LegacyMapGraphAdapter
{
    /// <summary>
    /// 把已 Generate 的 MapGenerator 映射为 ExpeditionGraph（一次性定义导入）。
    /// </summary>
    /// <param name="generator">已调用 Generate(rows,columns) 的 Legacy 图源（Compatibility Graph 源）。</param>
    /// <param name="nodeElapsedDaysFor">可选：按 NodeId 提供 Test/Not Frozen 的 NodeElapsedDays；默认 0。</param>
    /// <exception cref="ArgumentNullException">generator 为 null。</exception>
    /// <exception cref="InvalidOperationException">generator.Nodes 为空（未 Generate）。</exception>
    public static ExpeditionGraph BuildGraph(
        MapGenerator generator,
        Func<string, int> nodeElapsedDaysFor = null)
    {
        if (generator == null)
            throw new ArgumentNullException(nameof(generator));
        if (generator.Nodes == null || generator.Nodes.Count == 0)
            throw new InvalidOperationException(
                "LegacyMapGraphAdapter：MapGenerator.Nodes 为空，请先调用 Generate(rows, columns)。");

        var nodes = generator.Nodes;
        string entryNodeId = ResolveEntryNodeId(nodes);

        var defs = new List<ExpeditionNodeData>(nodes.Count);
        foreach (var n in nodes)
        {
            if (n == null || string.IsNullOrEmpty(n.NodeId))
                throw new InvalidOperationException("LegacyMapGraphAdapter：遇到 null/空 NodeId 的 MapNode。");

            int elapsed = 0;
            if (nodeElapsedDaysFor != null)
                elapsed = Math.Max(0, nodeElapsedDaysFor(n.NodeId));

            defs.Add(new ExpeditionNodeData(
                n.NodeId,
                MapNodeKind(n.Type),
                n.Connections,
                elapsed,
                0,                            // Legacy 图无 Explicit Extra Supply Cost
                n.Type == NodeType.Boss));    // Legacy Boss = 显式成功路线终点（FP-V0.1 兼容映射）
        }

        return new ExpeditionGraph(entryNodeId, defs);
    }

    /// <summary>
    /// 图结构方式确定入口：唯一零入度 Node。
    /// 不读取 Reachable/CurrentNode 等 Legacy runtime flags（它们不是 Progression Authority）。
    /// 理论上 4×3 生成保证唯一根；若出现多个零入度节点（防御），取 Nodes 顺序首个。
    /// </summary>
    private static string ResolveEntryNodeId(IReadOnlyList<MapNode> nodes)
    {
        var inbound = new HashSet<string>();
        foreach (var n in nodes)
        {
            if (n.Connections == null) continue;
            foreach (var connId in n.Connections)
            {
                if (!string.IsNullOrEmpty(connId)) inbound.Add(connId);
            }
        }

        string entry = null;
        foreach (var n in nodes)
        {
            if (inbound.Contains(n.NodeId)) continue;
            if (entry == null)
            {
                entry = n.NodeId;
            }
            else
            {
                // 防御：多根时保留首个，同时暴露可发现性（不应出现；由 graph ctor 之外的拓扑保证）。
                UnityEngine.Debug.LogWarning(
                    "[LegacyMapGraphAdapter] 检测到多个零入度节点，使用首个作为入口：" + entry
                    + "（多余候选：" + n.NodeId + "）");
            }
        }

        if (entry == null && nodes.Count > 0)
        {
            entry = nodes[0].NodeId;
            UnityEngine.Debug.LogWarning(
                "[LegacyMapGraphAdapter] 未找到零入度节点，回退 Nodes[0] 作为入口：" + entry);
        }

        if (entry == null)
            throw new InvalidOperationException("LegacyMapGraphAdapter：无法解析入口 NodeId。");

        return entry;
    }

    /// <summary>
    /// Legacy NodeType → ExpeditionNodeKind（Compatibility-only 映射）。
    /// Shop/Recruit 在 FP-V0.1 无正式生态，映射为 Event 占位以保留拓扑；
    /// 内容分派是否进入/跳转由后续 Stage 的产品接线决定，不在本 adapter 产生规则。
    /// </summary>
    public static ExpeditionNodeKind MapNodeKind(NodeType type)
    {
        switch (type)
        {
            case NodeType.Battle: return ExpeditionNodeKind.Battle;
            case NodeType.Elite:  return ExpeditionNodeKind.Elite;
            case NodeType.Boss:   return ExpeditionNodeKind.Boss;
            case NodeType.Event:  return ExpeditionNodeKind.Event;
            case NodeType.Rest:   return ExpeditionNodeKind.Rest;
            case NodeType.Shop:   return ExpeditionNodeKind.Event;   // Legacy 占位（FP-V0.1 无正式商店生态）
            case NodeType.Recruit:return ExpeditionNodeKind.Event;   // Legacy 占位（FP-V0.1 无正式招募生态）
            default:              return ExpeditionNodeKind.Event;
        }
    }
}
