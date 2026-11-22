using UnityEngine;

/// <summary>
/// 英雄配置数据（ScriptableObject）
///
/// 负责：存放英雄静态模板配置
/// 不负责：运行时等级/经验
///
/// 运行时数据由 HeroInstance 管理
/// </summary>
[CreateAssetMenu(fileName = "NewHeroData", menuName = "RRPG/英雄配置")]
public class HeroData : ScriptableObject
{
    /// <summary>
    /// 唯一英雄ID
    /// 全局标识英雄模板
    /// </summary>
    [Header("基础信息")]
    public string HeroId;

    /// <summary>
    /// 英雄展示名称
    /// 用于 UI 显示
    /// </summary>
    public string DisplayName;

    /// <summary>
    /// 基础生命值
    /// 所有英雄必有，出厂配置值
    /// 升级时可在此基础上增加
    /// </summary>
    [Header("基础属性")]
    public int BaseHP;

    /// <summary>
    /// 基础攻击力
    /// 所有英雄必有，出厂配置值
    /// 升级时可在此基础上增加
    /// </summary>
    public int BaseAttack;

    /// <summary>
    /// 攻击距离
    /// 决定能攻击到前方第几排
    /// 默认值为 1（可攻击到前方第一排）
    /// </summary>
    public int AttackRange;

    /// <summary>
    /// 攻击次数
    /// 一回合内可进行攻击的次数上限
    /// 默认值为 1
    /// </summary>
    public int AttackCount;

    /// <summary>
    /// 行动力上限
    /// 英雄个人资源，用于攻击、技能、援护
    /// 默认值为 1，每回合开始阶段回满
    /// </summary>
    public int ActionPoints;

    /// <summary>
    /// 阵营
    /// 所有英雄必有阵营属性
    /// 影响技能交互、套牌归属等机制
    /// </summary>
    [Header("阵营与技能")]
    public string Faction;

    /// <summary>
    /// 初始技能ID列表
    /// 英雄出厂时拥有的固定技能
    /// 引用 SkillData 配置，技能通过 Effect 拼装实现
    /// </summary>
    public string[] InitialSkillIds;
}