using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 地图生成器（点阵分层算法）
///
/// 节点类型生成规则（不含起始节点和 Boss 节点）：
///   1. 普通战斗占一半 — 偶数时一半，奇数时一半减一（保证普通最多）
///   2. 固定保底：1 精英、1 招募、1 事件、1 商店
///   3. 剩余按加权池随机：精英15% 招募15% 事件35% 商店35%
/// </summary>
public class MapGenerator
{
    public List<MapNode> Nodes { get; private set; }
    public MapNode CurrentNode { get; private set; }
    public MapNode BossNode { get; private set; }
    public bool IsAtBoss { get; private set; }

    public void Generate(int rows, int columns)
    {
        Nodes = new List<MapNode>();
        IsAtBoss = false;
        int nodeId = 0;

        var startNode = new MapNode("node_" + nodeId++, NodeType.Battle, 0.5f, 1f);
        startNode.Reachable = true;
        Nodes.Add(startNode);

        int totalNonBoss = (rows - 1) * columns;
        var typeList = BuildTypeList(totalNonBoss);
        Shuffle(typeList);

        int typeIndex = 0;
        for (int row = 1; row < rows; row++)
        {
            for (int col = 0; col < columns; col++)
            {
                float posX = (col + 0.5f) / columns;
                float posY = 1f - (row / (float)rows);
                var node = new MapNode("node_" + nodeId++, typeList[typeIndex++], posX, posY);
                Nodes.Add(node);
            }
        }

        BossNode = new MapNode("node_" + nodeId++, NodeType.Boss, 0.5f, 0f);
        Nodes.Add(BossNode);
        SetupConnections(columns, rows);
        CurrentNode = startNode;
    }

    private List<NodeType> BuildTypeList(int total)
    {
        var types = new List<NodeType>();

        // 1. 普通战斗占一半（必须最多）
        int battleCount = total % 2 == 0 ? total / 2 : (total - 1) / 2;
        for (int i = 0; i < battleCount; i++)
            types.Add(NodeType.Battle);

        // 2. 剩余节点
        int remaining = total - battleCount;

        // 固定保底：1 精英、1 招募、1 事件、1 商店
        if (remaining >= 4)
        {
            types.Add(NodeType.Elite);
            types.Add(NodeType.Recruit);
            types.Add(NodeType.Event);
            types.Add(NodeType.Shop);
            remaining -= 4;

            // 剩余按加权池随机
            NodeType[] pool = {
                NodeType.Event, NodeType.Event, NodeType.Event, NodeType.Event,
                NodeType.Event, NodeType.Event, NodeType.Event,  // 35%
                NodeType.Shop, NodeType.Shop, NodeType.Shop, NodeType.Shop,
                NodeType.Shop, NodeType.Shop, NodeType.Shop,      // 35%
                NodeType.Elite, NodeType.Elite, NodeType.Elite,  // 15%
                NodeType.Recruit, NodeType.Recruit, NodeType.Recruit // 15%
            };
            for (int i = 0; i < remaining; i++)
                types.Add(pool[Random.Range(0, pool.Length)]);
        }
        else
        {
            var guaranteed = new List<NodeType> {
                NodeType.Elite, NodeType.Recruit, NodeType.Event, NodeType.Shop
            };
            Shuffle(guaranteed);
            for (int i = 0; i < remaining; i++)
                types.Add(guaranteed[i]);
        }

        return types;
    }

    private void SetupConnections(int columns, int rows)
    {
        var layers = new List<List<MapNode>>();
        for (int i = 0; i <= rows; i++)
            layers.Add(new List<MapNode>());

        foreach (var node in Nodes)
        {
            int layer = Mathf.RoundToInt((1f - node.PosY) * rows);
            layer = Mathf.Clamp(layer, 0, rows);
            layers[layer].Add(node);
        }

        for (int l = 0; l < layers.Count - 1; l++)
        {
            var upperLayer = layers[l];
            var lowerLayer = layers[l + 1];
            if (upperLayer.Count == 0 || lowerLayer.Count == 0) continue;

            foreach (var upper in upperLayer)
            {
                if (upper == BossNode) continue;
                int connections = Random.Range(2, 4);
                var shuffled = new List<MapNode>(lowerLayer);
                Shuffle(shuffled);
                int count = 0;
                foreach (var lower in shuffled)
                {
                    if (count >= connections) break;
                    if (!upper.Connections.Contains(lower.NodeId))
                    {
                        upper.Connections.Add(lower.NodeId);
                        count++;
                    }
                }
            }

            var lowerConnected = new HashSet<string>();
            foreach (var upper in upperLayer)
                foreach (var connId in upper.Connections)
                    lowerConnected.Add(connId);

            foreach (var lower in lowerLayer)
            {
                if (!lowerConnected.Contains(lower.NodeId))
                {
                    Shuffle(upperLayer);
                    foreach (var upper in upperLayer)
                    {
                        if (!upper.Connections.Contains(lower.NodeId))
                        {
                            upper.Connections.Add(lower.NodeId);
                            break;
                        }
                    }
                }
            }
        }
    }

    private void Shuffle<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            T temp = list[i];
            list[i] = list[j];
            list[j] = temp;
        }
    }

    public void SelectNode(MapNode node)
    {
        if (CurrentNode != null) CurrentNode.Visited = true;
        CurrentNode = node;
        if (node == BossNode) IsAtBoss = true;
        foreach (var n in Nodes) n.Reachable = false;
        foreach (var connId in node.Connections)
        {
            var next = Nodes.Find(n => n.NodeId == connId);
            if (next != null) next.Reachable = true;
        }
    }

    public List<MapNode> GetNextNodes()
    {
        var nextNodes = new List<MapNode>();
        if (CurrentNode == null) return nextNodes;
        foreach (var connId in CurrentNode.Connections)
        {
            var node = Nodes.Find(n => n.NodeId == connId);
            if (node != null) nextNodes.Add(node);
        }
        return nextNodes;
    }
}