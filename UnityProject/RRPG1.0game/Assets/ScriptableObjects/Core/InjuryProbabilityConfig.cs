using UnityEngine;

/// <summary>
/// 受伤概率配置（ScriptableObject）
///
/// 负责：定义受伤判定概率阈值
/// 不负责：死亡之门判定逻辑
///
/// MVP 阶段仅记录状态，不实现具体数值惩罚
/// </summary>
[CreateAssetMenu(fileName = "NewInjuryProbabilityConfig", menuName = "RRPG/受伤概率配置")]
public class InjuryProbabilityConfig : ScriptableObject
{
    /// <summary>
    /// 轻伤概率阈值
    /// 死亡概率 ≤ 此值时判定为轻伤
    /// 默认值：20（≤20%）
    /// </summary>
    [Header("受伤概率")]
    public int LightInjuryMax = 20;

    /// <summary>
    /// 重伤概率阈值
    /// 死亡概率在此值与 LightInjuryMax 之间时判定为重伤
    /// 默认值：60（20%~60%）
    /// </summary>
    public int HeavyInjuryMax = 60;

    /// <summary>
    /// 残疾概率阈值
    /// 死亡概率 > 此值时判定为残疾
    /// 默认值：61（>60%）
    /// </summary>
    public int DisabledMin = 61;
}