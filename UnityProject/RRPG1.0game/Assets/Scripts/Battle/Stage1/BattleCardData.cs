using UnityEngine;

/// <summary>
/// Battle V0.1 战斗牌类别
/// Character = 角色牌（属于具体人物的牌，仅当该人物上阵时进入本场牌库）
/// Common = 通用牌（不依赖具体人物，总是有效）
/// </summary>
public enum BattleCardKind
{
    Character,
    Common
}

/// <summary>
/// Battle V0.1 最小目标范围（Stage 2 最小效果结算使用；完整目标/效果管线后续 Stage 接入）
/// None = 无需目标，点击卡牌即可打出；EnemyUnit = 需选择敌方单位；AllyUnit = 需选择我方单位。
/// </summary>
public enum BattleCardTargetScope
{
    None,
    EnemyUnit,
    AllyUnit
}

/// <summary>
/// Battle V0.1 最小效果类型（Stage 2 最小效果结算；完整效果管线后续 Stage 接入）。
/// 定义在本程序集内（BattleStage1.Runtime），不依赖 Assembly-CSharp 的 Legacy EffectType。
/// </summary>
public enum BattleCardEffectType
{
    None,
    Damage,
    Heal
}

/// <summary>
/// Battle V0.1 战斗牌模板（ScriptableObject，静态配置）
///
/// 职责：定义一张牌的静态模板（叫什么、什么类型、消耗多少、Retain/Exhaust 等）。
/// 这是对旧 CharacterCardData / ItemCardData 分离模型的替代：
///   角色牌（CardKind=Character）与通用牌（CardKind=Common）统一为同一种模板。
///
/// 明确不包含（由 Run/Battle 运行时结构承载，见 BattlePersonCardEntry）：
///   - 具体人物归属：同一角色牌模板可被不同具体人物各自拥有，归属（PersonId）不属于静态模板。
///   - Run 牌组份数：同一模板在不同 Run、不同人物可拥有不同份数，份数不属于静态模板，
///     不得靠修改全局 SO 表示 Run 牌组状态。
///
/// 本类只定义"牌是什么"，不定义"牌打出后产生什么效果"。
/// 效果（Effect / Trigger / Target）在后续 Stage 以独立效果管线接入。
/// </summary>
[CreateAssetMenu(fileName = "NewBattleCard", menuName = "RRPG/Battle V0.1/战斗牌配置")]
public class BattleCardData : ScriptableObject
{
    [Header("基础信息")]
    [Tooltip("唯一卡牌模板ID")]
    public string CardId;

    [Tooltip("UI 显示名称；为空时使用资源名")]
    public string DisplayName;

    [Tooltip("角色牌 / 通用牌")]
    public BattleCardKind CardKind;

    [Header("牌堆与消耗")]
    [Tooltip("打出此牌消耗的共享行动力")]
    public int Cost;

    [Tooltip("Retain 滞留：回合结束时仍留在手牌，不被弃入弃牌堆")]
    public bool Retain;

    [Tooltip("Exhaust 消耗：打出后进入消耗区，不再回到本场牌循环")]
    public bool IsExhaust;

    [Header("说明")]
    [TextArea(2, 4)]
    [Tooltip("效果说明（Stage 1 仅用于展示，不参与规则）")]
    public string Description;

    [Header("距离修正（Stage 3A）")]
    [Tooltip("攻击距离修正：正值扩大可打击范围（可跨过的非空阻挡排数上限增加），0=默认（使用人物 AttackRange）。仅伤害牌目标判定时生效")]
    public int RangeModifier;

    [Header("最小效果（Stage 2 最小结算；完整效果管线后续 Stage 接入）")]
    [Tooltip("最小效果类型：None=无效果 / Damage=造成伤害 / Heal=恢复生命")]
    public BattleCardEffectType Effect;

    [Tooltip("最小效果数值（伤害/治疗量；内容层可配置，不硬编码）")]
    public int EffectValue;

    [Tooltip("最小目标范围：None=无需目标 / EnemyUnit=选择敌方单位 / AllyUnit=选择我方单位")]
    public BattleCardTargetScope TargetScope;

    [Header("效果规则（Gate 0 配置化零件；未配置时走上方 Legacy 最小效果路径）")]
    [Tooltip("规则程序：Trigger / Play Requirement / Steps（有限强类型零件，禁止按 CardId 写死效果）")]
    public BattleCardRuleProgram RuleProgram = new BattleCardRuleProgram();
}

