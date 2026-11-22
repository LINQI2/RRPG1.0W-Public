using UnityEngine;

/// <summary>
/// 状态配置数据（ScriptableObject）
///
/// 负责：存放状态静态配置
/// 不负责：状态的运行时计数/叠加/来源
///
/// Buff、Debuff 统一结构
/// MVP 阶段不拆分基础状态/控制状态
/// </summary>
[CreateAssetMenu(fileName = "NewStatusData", menuName = "RRPG/状态配置")]
public class StatusData : ScriptableObject
{
    /// <summary>
    /// 唯一状态ID
    /// 全局标识状态模板
    /// </summary>
    [Header("基础信息")]
    public string StatusId;

    /// <summary>
    /// 状态名称
    /// 用于 UI 显示
    /// </summary>
    public string Name;

    /// <summary>
    /// 持续回合数
    /// 状态默认持续回合数
    /// </summary>
    [Header("持续规则")]
    public int Duration;

    /// <summary>
    /// 到期后转移到的状态ID（可选）
    /// 状态过期移除后，自动为目标添加新状态
    /// 用于链式状态转换（如微醺→醉醺）
    /// </summary>
    public string OnExpireStatusId;

    /// <summary>
    /// 效果列表
    /// 状态生效时执行的多个效果
    /// 每个效果由 EffectData 定义
    /// </summary>
    [Header("效果")]
    public EffectData[] Effects;
}
