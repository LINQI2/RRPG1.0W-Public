using UnityEngine;

/// <summary>
/// 道具目标类型
/// </summary>
public enum ItemTargetType
{
    /// <summary>只能对己方（友方）使用</summary>
    Ally,
    /// <summary>只能对敌方使用</summary>
    Enemy,
    /// <summary>对任意目标使用</summary>
    Any
}

/// <summary>
/// 道具卡配置数据（ScriptableObject）
///
/// 负责：存放道具卡静态配置
/// 战斗内循环卡牌
/// 使用后进入弃牌堆，抽牌堆为空时弃牌堆洗回抽牌堆
/// </summary>
[CreateAssetMenu(fileName = "NewItemCardData", menuName = "RRPG/道具卡配置")]
public class ItemCardData : ScriptableObject
{
    /// <summary>
    /// 卡牌ID
    /// 唯一标识此卡牌
    /// </summary>
    [Header("基础信息")]
    public string CardId;

    /// <summary>
    /// 道具名称
    /// 用于 UI 显示
    /// </summary>
    public string DisplayName;

    /// <summary>
    /// 消耗TP
    /// 打出此道具卡需要消耗的战术点
    /// </summary>
    [Header("消耗")]
    public int CostTP;

    /// <summary>
    /// 效果列表
    /// 使用道具卡时执行的效果
    /// 每个效果由 EffectData 定义
    /// </summary>
    [Header("效果")]
    public EffectData[] Effects;

    /// <summary>
    /// 目标类型
    /// 决定道具可以对哪些目标使用
    /// </summary>
    [Header("目标限制")]
    public ItemTargetType TargetType = ItemTargetType.Ally;
}
