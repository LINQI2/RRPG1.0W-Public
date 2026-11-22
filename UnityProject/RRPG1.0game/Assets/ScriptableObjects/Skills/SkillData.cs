using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 技能配置数据（ScriptableObject）
///
/// 负责：存放技能静态配置
/// 不负责：技能运行时逻辑
///
/// 采用 Trigger + Effect 架构
/// 所有技能统一消耗行动力（ActionPoints）
/// </summary>
[CreateAssetMenu(fileName = "NewSkillData", menuName = "RRPG/技能配置")]
public class SkillData : ScriptableObject
{
    /// <summary>
    /// 唯一技能ID
    /// 全局标识技能模板
    /// </summary>
    [Header("基础信息")]
    public string SkillId;

    /// <summary>
    /// 技能名称
    /// 用于 UI 显示
    /// </summary>
    public string Name;

    /// <summary>
    /// 触发时机
    /// 决定技能在何时自动触发或可主动释放
    /// </summary>
    [Header("触发条件")]
    public TriggerType Trigger;

    /// <summary>
    /// 消耗行动力
    /// 释放此技能需要消耗的行动力点数
    /// 0=被动技能，1=主动技能
    /// </summary>
    public int CostActionPoints;

    /// <summary>
    /// 技能距离
    /// 基于逻辑距离计算，决定技能可作用的目标范围
    /// 默认值为 1
    /// </summary>
    public int SkillRange;

    /// <summary>
    /// 效果列表
    /// 技能实际执行的一个或多个效果
    /// 每个效果由 EffectData 定义
    /// </summary>
    [Header("效果")]
    public EffectData[] Effects;
}