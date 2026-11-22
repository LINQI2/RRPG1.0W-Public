using System;

/// <summary>
/// 效果配置数据
///
/// 负责：定义技能的单一效果
/// 技能实际执行内容
///
/// 所有技能最终拆解为 Effect
/// 每个 Effect 由 EffectType + Value + TargetRule 组成
/// </summary>
[Serializable]
public class EffectData
{
    /// <summary>
    /// 效果类型
    /// 决定此效果执行何种操作
    /// </summary>
    public EffectType EffectType;

    /// <summary>
    /// 效果数值
    /// 伤害值、治疗量、增减幅等
    /// </summary>
    public int Value;

    /// <summary>
    /// 目标规则
    /// 决定效果作用于哪些目标
    /// </summary>
    public TargetRule TargetRule;

    /// <summary>
    /// 状态ID（可选）
    /// 用于 AddBuff/RemoveBuff 效果，指定操作哪个状态
    /// </summary>
    public string StatusId;
}
