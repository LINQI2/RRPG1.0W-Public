using UnityEngine;

/// <summary>
/// 经验曲线配置：从当前等级升到下一级所需经验。
/// ExpRequiredToLevel[i] = 从 Lv (i+1) 升到 Lv (i+2) 所需经验。
/// 正式经验曲线 / 最高等级未冻结；此处为演示/测试值。
/// </summary>
[CreateAssetMenu(fileName = "NewGrowthCurve", menuName = "RRPG/人物成长/经验曲线（测试值）")]
public class GrowthCurveConfig : ScriptableObject
{
    [Tooltip("正式最高等级未冻结；此处为演示/测试值")]
    public int MaxLevel = 10;

    [Tooltip("Starter 牌 Proficiency 正式上限未冻结；此处为演示/测试值")]
    public int ProficiencyCap = 5;

    [Tooltip("ExpRequiredToLevel[i] = 从 Lv(i+1) 升到 Lv(i+2) 所需经验（演示/测试值）")]
    public int[] ExpRequiredToLevel = { 10, 15, 20, 25, 30, 40, 50, 60, 80 };

    /// <summary>当前等级升到下一级所需经验；已达 MaxLevel 返回 0（不可再升）</summary>
    public int GetExpRequired(int currentLevel)
    {
        if (currentLevel < 1) return 0;
        if (currentLevel >= MaxLevel) return 0;
        if (ExpRequiredToLevel == null) return 0;
        int idx = currentLevel - 1;
        if (idx < 0 || idx >= ExpRequiredToLevel.Length) return 0;
        return Mathf.Max(0, ExpRequiredToLevel[idx]);
    }

    public bool IsMaxLevel(int currentLevel) => currentLevel >= MaxLevel;
}
