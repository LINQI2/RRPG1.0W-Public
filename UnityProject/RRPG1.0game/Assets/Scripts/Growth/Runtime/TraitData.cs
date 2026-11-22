using UnityEngine;

/// <summary>
/// Trait 配置：人物唯一专属被动能力。正式效果与最大等级未冻结（演示/测试值）。
/// </summary>
[CreateAssetMenu(fileName = "NewTrait", menuName = "RRPG/人物成长/Trait（测试值）")]
public class TraitData : ScriptableObject
{
    [Tooltip("唯一 Trait ID")]
    public string TraitId;

    [Tooltip("显示名称")]
    public string DisplayName;

    [TextArea(2, 4)]
    [Tooltip("说明（正式效果未冻结，仅展示）")]
    public string Description;

    [Tooltip("Trait 最大等级（测试值，未冻结）")]
    public int MaxLevel = 3;
}