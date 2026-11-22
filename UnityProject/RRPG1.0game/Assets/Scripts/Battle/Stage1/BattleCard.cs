using System;
using System.Collections.Generic;

/// <summary>
/// Battle V0.1 战斗牌运行时实例（纯逻辑，不依赖 UnityEngine）
///
/// 职责：表示一套共享牌库中一张具体的牌在单场战斗中的状态。
/// 由"牌模板 + 具体人物归属 + 本Run份数"（BattlePersonCardEntry）实例化而来；
/// 每个实例是唯一对象（模板ID + 人物归属 + 副本序号）。
///
/// 不负责：牌堆管理（由 SharedDeck 负责）、效果执行（后续 Stage 接入）。
/// </summary>
public class BattleCard
{
    /// <summary>本场唯一实例ID（模板ID_人物归属_副本序号）</summary>
    public string CardId;

    /// <summary>引用 BattleCardData.CardId</summary>
    public string TemplateId;

    /// <summary>显示名称</summary>
    public string DisplayName;

    /// <summary>角色牌 / 通用牌</summary>
    public BattleCardKind CardKind;

    /// <summary>
    /// 具体人物归属（Person/Character Instance ID）。
    /// 角色牌 = 当前拥有这张牌的具体人物实例ID；通用牌 = null。
    /// 归属存在于 Run/Battle 运行时结构，不来自静态模板。
    /// </summary>
    public string OwnerPersonId;

    /// <summary>
    /// 基础费用（静态模板值；出牌实际使用 ActualCost）。
    /// </summary>
    public int Cost;

    /// <summary>Retain 滞留标记（回合结束不清手牌）</summary>
    public bool Retain;

    /// <summary>Exhaust 消耗标记（打出后进消耗区）</summary>
    public bool IsExhaust;

    /// <summary>效果说明（Stage 1 仅展示）</summary>
    public string Description;

    /// <summary>最小效果类型（来自模板；None/Damage/Heal）</summary>
    public BattleCardEffectType Effect;

    /// <summary>最小效果数值（来自模板）</summary>
    public int EffectValue;

    /// <summary>最小目标范围（来自模板；None=无需目标 / EnemyUnit / AllyUnit）</summary>
    public BattleCardTargetScope TargetScope;

    /// <summary>攻击距离修正（来自模板；0=使用人物 AttackRange；正值扩大可打击范围）</summary>
    public int RangeModifier;

    /// <summary>
    /// 规则程序（来自模板；未迁移的 Legacy 牌为 null，走既有最小效果路径）。
    /// 运行时只读引用，不修改静态模板。
    /// </summary>
    public BattleCardRuleProgram RuleProgram;

    /// <summary>
    /// 本场 Current Deck Cycle 内的临时费用修正（Additive，可为负）。
    /// 只属于本实例：不同副本、不同牌主互不影响；不写回静态模板。
    /// </summary>
    private readonly List<int> _costModifiers = new List<int>();

    /// <summary>
    /// 本场 Current Deck Cycle 内的临时伤害修正（Additive，可为负）。
    /// </summary>
    private readonly List<int> _damageModifiers = new List<int>();

    /// <summary>基础费用（静态模板值；运行期永不修改）</summary>
    public int BaseCost
    {
        get { return Cost; }
    }

    /// <summary>实际费用 = 基础费用 + 本 Cycle 临时修正，Clamp ≥ 0（出牌合法性 / 支付使用）</summary>
    public int ActualCost
    {
        get { return Math.Max(0, Cost + SumModifiers(_costModifiers)); }
    }

    /// <summary>本牌是否具有 Damage（ModifyDamage 的适用性判断依据）</summary>
    public bool HasDamage
    {
        get { return Effect == BattleCardEffectType.Damage; }
    }

    /// <summary>基础伤害（静态模板值；本牌无 Damage 时为 0）</summary>
    public int BaseDamage
    {
        get { return HasDamage ? EffectValue : 0; }
    }

    /// <summary>当前伤害 = 基础伤害 + 本 Cycle 临时修正，Clamp ≥ 0</summary>
    public int ActualDamage
    {
        get { return HasDamage ? Math.Max(0, EffectValue + SumModifiers(_damageModifiers)) : 0; }
    }

    /// <summary>本 Cycle 费用修正合计（UI 显示“加工状态”用）</summary>
    public int CostModifierTotal
    {
        get { return SumModifiers(_costModifiers); }
    }

    /// <summary>本 Cycle 伤害修正合计</summary>
    public int DamageModifierTotal
    {
        get { return SumModifiers(_damageModifiers); }
    }

    /// <summary>本牌当前是否带有 Current Deck Cycle 临时修正</summary>
    public bool HasCurrentDeckCycleModifier
    {
        get { return _costModifiers.Count > 0 || _damageModifiers.Count > 0; }
    }

    /// <summary>追加一条费用修正（Additive，可共存累积）</summary>
    public void AddCostModifier(int delta)
    {
        if (delta == 0) return;
        _costModifiers.Add(delta);
    }

    /// <summary>追加一条伤害修正（Additive，可共存累积）</summary>
    public void AddDamageModifier(int delta)
    {
        if (delta == 0) return;
        _damageModifiers.Add(delta);
    }

    /// <summary>
    /// 清除本牌全部 Current Deck Cycle 临时修正。
    /// 只在“该具体牌经弃牌堆重新进入新的抽牌循环”时由 SharedDeck 调用；
    /// 回合结束、进入弃牌堆本身都不清除（Gate 0 Development Spec §4.5）。
    /// </summary>
    public void ResetCurrentDeckCycleModifiers()
    {
        if (_costModifiers.Count == 0 && _damageModifiers.Count == 0) return;
        _costModifiers.Clear();
        _damageModifiers.Clear();
    }

    private static int SumModifiers(List<int> modifiers)
    {
        if (modifiers == null || modifiers.Count == 0) return 0;
        int sum = 0;
        for (int i = 0; i < modifiers.Count; i++) sum += modifiers[i];
        return sum;
    }

    /// <summary>
    /// 从"运行时人物牌条目"创建牌实例
    /// </summary>
    /// <param name="entry">牌模板 + 具体人物归属 + 本Run份数（对应第 copyIndex 份）</param>
    /// <param name="copyIndex">该人物该模板的副本序号（0 起）</param>
    public static BattleCard FromEntry(BattlePersonCardEntry entry, int copyIndex)
    {
        if (entry == null) throw new ArgumentNullException(nameof(entry));
        if (entry.Template == null) throw new ArgumentNullException(nameof(entry) + ".Template");

        var data = entry.Template;
        string personTag = string.IsNullOrEmpty(entry.PersonId) ? "common" : entry.PersonId;

        return new BattleCard
        {
            CardId = data.CardId + "_" + personTag + "_" + copyIndex,
            TemplateId = data.CardId,
            DisplayName = string.IsNullOrEmpty(data.DisplayName) ? data.name : data.DisplayName,
            CardKind = data.CardKind,
            OwnerPersonId = data.CardKind == BattleCardKind.Character ? entry.PersonId : null,
            Cost = Math.Max(0, data.Cost),
            Retain = data.Retain,
            IsExhaust = data.IsExhaust,
            Description = data.Description ?? string.Empty,
            Effect = data.Effect,
            EffectValue = data.EffectValue,
            TargetScope = data.TargetScope,
            RangeModifier = data.RangeModifier,
            RuleProgram = data.RuleProgram
        };
    }
}

