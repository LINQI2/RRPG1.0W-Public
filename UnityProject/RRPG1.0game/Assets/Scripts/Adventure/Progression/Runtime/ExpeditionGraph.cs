using System;
using System.Collections.Generic;

/// <summary>
/// 会话内 Node 定义容器（只读；远征开始时一次性导入，之后不再变化）。
///
/// 本类型只承载「定义层」：Node 集合、唯一入口 NodeId、NodeId 唯一性、连接目标存在性校验。
/// 运行规则真相位于 ExpeditionProgressionState / Resolver，Legacy Map flags 不是本类型的 Authority。
/// </summary>
public class ExpeditionGraph
{
    private readonly Dictionary<string, ExpeditionNodeData> _nodes;

    /// <summary>唯一根入口 NodeId（远征第一个可进入 Node）。</summary>
    public string EntryNodeId { get; }

    /// <summary>
    /// 构造并校验：nodes 非空、NodeId 唯一、入口 NodeId 存在、所有连接目标存在于图中。
    /// 违反任意一条立即抛 ArgumentException（图定义错误属于编程错误，不做静默修正）。
    /// </summary>
    public ExpeditionGraph(string entryNodeId, IEnumerable<ExpeditionNodeData> nodes)
    {
        if (string.IsNullOrEmpty(entryNodeId))
            throw new ArgumentException("EntryNodeId 不能为空", nameof(entryNodeId));
        if (nodes == null)
            throw new ArgumentNullException(nameof(nodes));

        _nodes = new Dictionary<string, ExpeditionNodeData>();
        foreach (var node in nodes)
        {
            if (node == null)
                throw new ArgumentException("Node 列表不得包含 null", nameof(nodes));
            if (_nodes.ContainsKey(node.NodeId))
                throw new ArgumentException("NodeId 重复：" + node.NodeId, nameof(nodes));
            _nodes.Add(node.NodeId, node);
        }

        if (!_nodes.ContainsKey(entryNodeId))
            throw new ArgumentException("入口 NodeId 不在图中：" + entryNodeId, nameof(entryNodeId));

        foreach (var node in _nodes.Values)
        {
            if (node.Connections == null) continue;
            foreach (var conn in node.Connections)
            {
                if (!_nodes.ContainsKey(conn))
                    throw new ArgumentException("连接目标不存在：" + conn + "（来自 " + node.NodeId + "）", nameof(nodes));
            }
        }

        EntryNodeId = entryNodeId;
    }

    /// <summary>图中是否存在该 Node。</summary>
    public bool ContainsNode(string nodeId)
    {
        return !string.IsNullOrEmpty(nodeId) && _nodes.ContainsKey(nodeId);
    }

    public bool TryGetNode(string nodeId, out ExpeditionNodeData node)
    {
        if (!string.IsNullOrEmpty(nodeId) && _nodes.TryGetValue(nodeId, out node))
            return true;
        node = null;
        return false;
    }

    /// <summary>入口 Node（图已保证存在）。</summary>
    public ExpeditionNodeData GetEntryNode()
    {
        return _nodes[EntryNodeId];
    }

    /// <summary>全部 Node 快照（只读）。</summary>
    public IReadOnlyList<ExpeditionNodeData> GetNodes()
    {
        var snapshot = new ExpeditionNodeData[_nodes.Count];
        int i = 0;
        foreach (var node in _nodes.Values)
            snapshot[i++] = node;
        return snapshot;
    }
}
