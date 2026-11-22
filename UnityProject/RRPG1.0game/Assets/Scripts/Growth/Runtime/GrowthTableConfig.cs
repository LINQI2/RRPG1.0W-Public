using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 成长表：等级 → 成长节点 的数据驱动映射。
/// </summary>
[CreateAssetMenu(fileName = "NewGrowthTable", menuName = "RRPG/人物成长/成长表（测试值）")]
public class GrowthTableConfig : ScriptableObject
{
    [Tooltip("成长表标识（测试值，未冻结）")]
    public string TableId = "test_table";

    [Tooltip("等级 → 节点 映射（每个等级至多一个节点；未配置的等级无成长节点）")]
    public List<GrowthNodeConfig> Nodes = new List<GrowthNodeConfig>();

    public GrowthNodeConfig GetNodeForLevel(int level)
    {
        if (Nodes == null) return null;
        foreach (var n in Nodes)
        {
            if (n != null && n.Level == level) return n;
        }
        return null;
    }
}
