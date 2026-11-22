using UnityEngine;

/// <summary>
/// 成长节点配置：某一等级升到时触发的成长内容（数据驱动，不在代码硬编码 Lv2/Lv5/Lv10）。
/// </summary>
[CreateAssetMenu(fileName = "NewGrowthNode", menuName = "RRPG/人物成长/成长节点（测试值）")]
public class GrowthNodeConfig : ScriptableObject
{
    [Tooltip("该节点生效的目标等级（玩家升到该等级时触发）")]
    public int Level;

    [Tooltip("节点类型")]
    public GrowthNodeType NodeType;

    [Tooltip("仅 UnlockedPoolExpand：本次扩展新增的牌模板ID（BattleCardData.CardId）")]
    public string[] ExpandCardIds;

    [Tooltip("仅 TraitUpgrade：要强化的 Trait；留空时强化人物当前 Trait（由 TraitCatalog 初始分配）")]
    public TraitData Trait;

    public bool IsCardGrowth => NodeType == GrowthNodeType.CardGrowth;
}
