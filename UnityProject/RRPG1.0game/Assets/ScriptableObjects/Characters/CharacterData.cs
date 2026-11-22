using UnityEngine;

/// <summary>
/// 角色配置数据（ScriptableObject）—— **Combat Compatibility（战斗定义）**
///
/// 负责：存放角色静态战斗模板配置（HP / ATK / 射程 / 攻击次数 / AP / 阵营 / 初始技能）
/// 不负责：运行时等级/经验/伤残/死亡状态（由 CharacterInstance 管理）
/// 不负责：人物身份与人物定义（Initial Person &amp; Profession Bootstrap 起，人物是谁来自
///        PersonDefinition + 独立 Runtime PersonId；本类不得再承担 Person Identity Authority）
///
/// 命名说明：类名/字段名为迁移前历史命名，本轮按最小修改保留（物理类型删除属后续
/// 「Legacy Character Template Retirement」任务）；语义上本类 = 战斗定义 + Legacy 兼容。
/// </summary>
[CreateAssetMenu(fileName = "NewCharacterData", menuName = "RRPG/角色配置")]
public class CharacterData : ScriptableObject
{
    /// <summary>
    /// 战斗定义 ID（Legacy 命名 CharacterId）。
    /// **不是**人物身份：具体人物身份只有 Runtime PersonId；同一定义可被多个 Person 引用。
    /// </summary>
    [Header("基础信息")]
    public string CharacterId;

    /// <summary>
    /// 【Combat fallback only — NOT Person Identity Authority】
    /// 战斗/卡面显示的 Legacy 兜底名。正常产品人物显示必须优先取具体 Person.DisplayName /
    /// PersonDefinition.DisplayName；禁止再以本字段回答「这个人是谁」。
    /// </summary>
    public string DisplayName;

    /// <summary>
    /// 基础生命值
    /// 所有角色必有，出厂配置值
    /// 升级时可在此基础上增加
    /// </summary>
    [Header("基础属性")]
    public int BaseHP;

    /// <summary>
    /// 基础攻击力
    /// 所有角色必有，出厂配置值
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
    /// 角色个人资源，用于攻击、技能、援护
    /// 默认值为 1，每回合开始阶段回满
    /// </summary>
    public int ActionPoints;

    /// <summary>
    /// 阵营
    /// 所有角色必有阵营属性
    /// 影响技能交互、套牌归属等机制
    /// </summary>
    [Header("阵营与技能")]
    public string Faction;

    /// <summary>
    /// 初始技能ID列表
    /// 角色出厂时拥有的固定技能
    /// 引用 SkillData 配置，技能通过 Effect 拼装实现
    /// </summary>
    public string[] InitialSkillIds;
}
