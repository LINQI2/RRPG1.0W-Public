using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 地图节点类型
/// </summary>
public enum NodeType
{
    Battle,     // 战斗
    Elite,      // 精英
    Boss,       // Boss
    Event,      // 事件（占位）
    Shop,       // 商店（占位）
    Rest,       // 休息（占位）
    Recruit     // 招募（占位）
}

/// <summary>
/// 地图节点
///
/// 负责：存储单个节点的类型、位置、连接关系
/// 不负责：地图生成逻辑、UI显示
/// </summary>
public class MapNode
{
    /// <summary>节点唯一ID</summary>
    public string NodeId;

    /// <summary>节点类型</summary>
    public NodeType Type;

    /// <summary>节点在UI中的X位置（0~1）</summary>
    public float PosX;

    /// <summary>节点在UI中的Y位置（0~1）</summary>
    public float PosY;

    /// <summary>已连接的后续节点ID列表</summary>
    public List<string> Connections;

    /// <summary>是否已访问</summary>
    public bool Visited;

    /// <summary>是否可到达（前置节点已访问）</summary>
    public bool Reachable;

    /// <summary>当前节点是否被选中</summary>
    public bool Selected;

    public MapNode(string nodeId, NodeType type, float posX, float posY)
    {
        NodeId = nodeId;
        Type = type;
        PosX = posX;
        PosY = posY;
        Connections = new List<string>();
        Visited = false;
        Reachable = false;
        Selected = false;
    }

    /// <summary>
    /// 获取节点类型的显示名称
    /// </summary>
    public string GetTypeName()
    {
        switch (Type)
        {
            case NodeType.Battle: return "战斗";
            case NodeType.Elite: return "精英";
            case NodeType.Boss: return "Boss";
            case NodeType.Event: return "事件";
            case NodeType.Shop: return "商店";
            case NodeType.Rest: return "休息";
            case NodeType.Recruit: return "招募";
            default: return "未知";
        }
    }
}