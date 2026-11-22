using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Battle V0.1 Gate 0 原型战斗牌内容生成工具（3P Shared Deck｜Cook / Worker / Hunter 九张 Proof Card）。
///
/// 负责：
///   - 在正式内容目录（Assets/Resources/ScriptableObjects/BattleCards）创建 / 更新九张原型牌 SO；
///   - 把牌效果写成有限的规则配置（Trigger / Play Requirement / Steps），不写任何按卡牌 ID 分支的代码；
///   - 幂等：已存在的资产只更新字段，不重建 GUID。
///
/// 不负责：
///   - 规则执行（BattleCardRuleExecutor）；SO 本体即内容资产，本工具只是作者化入口；
///   - 正式数值冻结：本脚本写入的费用 / 伤害 / 护盾全部是 Prototype Test Value（Not Frozen）。
///
/// 使用方式：Unity 菜单 RRPG/Battle Gate0/01 生成 3P 原型战斗牌；
///           或批处理 -executeMethod BattleGate0CardContentBuilder.BatchBuildGate0Cards
/// </summary>
public static class BattleGate0CardContentBuilder
{
    private const string CardDir = "Assets/Resources/ScriptableObjects/BattleCards";

    [MenuItem("RRPG/Battle Gate0/01 生成 3P 原型战斗牌（Cook / Worker / Hunter）")]
    public static void BuildGate0Cards()
    {
        if (!Directory.Exists(CardDir)) Directory.CreateDirectory(CardDir);

        int count = 0;
        count += BuildCookCards();
        count += BuildWorkerCards();
        count += BuildHunterCards();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[Gate0Cards] 已生成 / 更新 " + count + " 张原型战斗牌（Prototype Test Value，未冻结）→ " + CardDir);
    }

    /// <summary>批处理入口（-executeMethod BattleGate0CardContentBuilder.BatchBuildGate0Cards）</summary>
    public static void BatchBuildGate0Cards()
    {
        BuildGate0Cards();
    }

    // ==================== Cook ====================

    private static int BuildCookCards()
    {
        int count = 0;

        // 《收火》Starter：获得固定护盾；若使用后共享行动力恰好为 0 则获得更多护盾。
        count += CreateOrUpdate("bcard_stoke_fire", "收火", card =>
        {
            card.Cost = 1;
            card.Description = "获得固定护盾；使用后共享行动力恰好为 0 时获得更多护盾。";
            card.RuleProgram.Steps.Add(Step(BattleRuleOperation.Shield, BattleRuleTargetSource.CardOwner,
                BattleRuleValueSource.Fixed, 3,
                bonusCondition: BattleRuleCondition.SharedActionPointsIsZero,
                bonusValueSource: BattleRuleValueSource.Fixed, bonusValueParam: 3));
        });

        // 《切配》：自动加工当前手牌中 Actual Cost 最高的一张（并列取手牌最左）。
        count += CreateOrUpdate("bcard_prep_cut", "切配", card =>
        {
            card.Cost = 0;
            card.Description = "自动加工手牌中费用最高的一张牌：降低其费用并降低其伤害。";
            card.RuleProgram.PlayRequirement = new BattleCardPlayRequirement
            {
                TargetSource = BattleRuleTargetSource.HandOthers,
                Select = BattleRuleSelect.MaxByActualCostLeftmost,
                MinCount = 1
            };
            card.RuleProgram.Steps.Add(Step(BattleRuleOperation.ModifyCost, BattleRuleTargetSource.HandOthers,
                BattleRuleValueSource.Fixed, -1, BattleRuleSelect.MaxByActualCostLeftmost,
                BattleRuleLifetime.CurrentDeckCycle));
            card.RuleProgram.Steps.Add(Step(BattleRuleOperation.ModifyDamage, BattleRuleTargetSource.HandOthers,
                BattleRuleValueSource.Fixed, -2, BattleRuleSelect.MaxByActualCostLeftmost,
                BattleRuleLifetime.CurrentDeckCycle));
        });

        // 《分食》：主目标恢复生命；其邻接我方恢复较少生命。
        count += CreateOrUpdate("bcard_share_meal", "分食", card =>
        {
            card.Cost = 1;
            card.TargetScope = BattleCardTargetScope.AllyUnit;
            card.Description = "主目标恢复生命；其邻接我方恢复较少生命。";
            card.RuleProgram.ManualTargetKind = BattleRuleManualTargetKind.AllyUnit;
            card.RuleProgram.Steps.Add(Step(BattleRuleOperation.Heal, BattleRuleTargetSource.ManualTarget,
                BattleRuleValueSource.Fixed, 4));
            card.RuleProgram.Steps.Add(Step(BattleRuleOperation.Heal, BattleRuleTargetSource.ManualTargetAdjacentAllies,
                BattleRuleValueSource.Fixed, 2));
        });

        return count;
    }

    // ==================== Worker ====================

    private static int BuildWorkerCards()
    {
        int count = 0;

        // 《搭把手》Starter：基础固定伤害。
        // 邻接轻量协作收益在内容工作台标记为“具体 Operation 未冻结”，因此本原型不配置该条件奖励（Awaiting Decision）。
        count += CreateOrUpdate("bcard_lend_hand", "搭把手", card =>
        {
            card.Cost = 1;
            card.Effect = BattleCardEffectType.Damage;
            card.EffectValue = 5;
            card.TargetScope = BattleCardTargetScope.EnemyUnit;
            card.Description = "造成固定伤害。（邻接协作收益未冻结，本原型未配置该条件奖励）";
            card.RuleProgram.ManualTargetKind = BattleRuleManualTargetKind.EnemyUnit;
            card.RuleProgram.Steps.Add(Step(BattleRuleOperation.Damage, BattleRuleTargetSource.ManualTarget,
                BattleRuleValueSource.Fixed, 5));
        });

        // 《补位》：移动到任意合法我方空格（费用为 Prototype 测试值 1）。
        count += CreateOrUpdate("bcard_fill_gap", "补位", card =>
        {
            card.Cost = 1;
            card.Description = "移动到任意合法我方空格，以改变后续空间关系。";
            card.RuleProgram.ManualTargetKind = BattleRuleManualTargetKind.AllyCell;
            card.RuleProgram.PlayRequirement = new BattleCardPlayRequirement
            {
                TargetSource = BattleRuleTargetSource.EmptyAllyCells,
                Select = BattleRuleSelect.All,
                MinCount = 1
            };
            card.RuleProgram.Steps.Add(Step(BattleRuleOperation.Move, BattleRuleTargetSource.ManualTarget,
                BattleRuleValueSource.None, 0));
        });

        // 《换岗》：与另一名仍具备战斗能力的我方 Person 交换位置（费用为 Prototype 测试值 1）。
        count += CreateOrUpdate("bcard_swap_post", "换岗", card =>
        {
            card.Cost = 1;
            card.TargetScope = BattleCardTargetScope.AllyUnit;
            card.Description = "与目标我方 Person 交换位置。";
            card.RuleProgram.ManualTargetKind = BattleRuleManualTargetKind.AllyUnit;
            card.RuleProgram.PlayRequirement = new BattleCardPlayRequirement
            {
                TargetSource = BattleRuleTargetSource.OtherAllyUnits,
                Select = BattleRuleSelect.All,
                MinCount = 1
            };
            card.RuleProgram.Steps.Add(Step(BattleRuleOperation.Swap, BattleRuleTargetSource.ManualTarget,
                BattleRuleValueSource.None, 0));
        });

        return count;
    }

    // ==================== Hunter ====================

    private static int BuildHunterCards()
    {
        int count = 0;

        // 《猎残》Starter：造成牌主攻击力伤害；目标生命 ≤ 50% 时额外固定伤害。
        count += CreateOrUpdate("bcard_finish_prey", "猎残", card =>
        {
            card.Cost = 1;
            card.Effect = BattleCardEffectType.Damage;
            card.TargetScope = BattleCardTargetScope.EnemyUnit;
            card.Description = "造成等同自身攻击力的伤害；低生命目标额外固定伤害。";
            card.RuleProgram.ManualTargetKind = BattleRuleManualTargetKind.EnemyUnit;
            card.RuleProgram.Steps.Add(Step(BattleRuleOperation.Damage, BattleRuleTargetSource.ManualTarget,
                BattleRuleValueSource.CasterAttack, 0,
                bonusCondition: BattleRuleCondition.TargetHpPercentAtOrBelow, bonusConditionParam: 50,
                bonusValueSource: BattleRuleValueSource.Fixed, bonusValueParam: 2));
        });

        // 《破势》：造成固定伤害；目标当前 Intent 为防御时额外固定伤害。
        count += CreateOrUpdate("bcard_break_poise", "破势", card =>
        {
            card.Cost = 1;
            card.Effect = BattleCardEffectType.Damage;
            card.EffectValue = 4;
            card.TargetScope = BattleCardTargetScope.EnemyUnit;
            card.Description = "造成固定伤害；目标当前 Intent 为防御时额外固定伤害。";
            card.RuleProgram.ManualTargetKind = BattleRuleManualTargetKind.EnemyUnit;
            card.RuleProgram.Steps.Add(Step(BattleRuleOperation.Damage, BattleRuleTargetSource.ManualTarget,
                BattleRuleValueSource.Fixed, 4,
                bonusCondition: BattleRuleCondition.EnemyIntentIsDefend,
                bonusValueSource: BattleRuleValueSource.Fixed, bonusValueParam: 3));
        });

        // 《独猎》：造成牌主攻击力伤害；独狼（一人排）时额外固定伤害。
        count += CreateOrUpdate("bcard_solo_hunt", "独猎", card =>
        {
            card.Cost = 1;
            card.Effect = BattleCardEffectType.Damage;
            card.TargetScope = BattleCardTargetScope.EnemyUnit;
            card.Description = "造成等同自身攻击力的伤害；独狼时额外固定伤害。";
            card.RuleProgram.ManualTargetKind = BattleRuleManualTargetKind.EnemyUnit;
            card.RuleProgram.Steps.Add(Step(BattleRuleOperation.Damage, BattleRuleTargetSource.ManualTarget,
                BattleRuleValueSource.CasterAttack, 0,
                bonusCondition: BattleRuleCondition.OwnerIsAloneInRow,
                bonusValueSource: BattleRuleValueSource.Fixed, bonusValueParam: 2));
        });

        return count;
    }

    // ==================== 资产与步骤构造 ====================

    /// <summary>
    /// 创建或更新一张原型牌资产（幂等：已存在时只更新字段，保留 GUID）。
    /// 统一设置：角色牌 / 非 Retain / 非 Exhaust / 无距离修正 / Trigger=OnPlay。
    /// </summary>
    private static int CreateOrUpdate(string cardId, string displayName, System.Action<BattleCardData> configure)
    {
        string path = CardDir + "/" + cardId + "_" + displayName + ".asset";
        var asset = AssetDatabase.LoadAssetAtPath<BattleCardData>(path);
        bool created = asset == null;
        if (created) asset = ScriptableObject.CreateInstance<BattleCardData>();

        asset.CardId = cardId;
        asset.DisplayName = displayName;
        asset.CardKind = BattleCardKind.Character;
        asset.Retain = false;
        asset.IsExhaust = false;
        asset.RangeModifier = 0;
        asset.Effect = BattleCardEffectType.None;
        asset.EffectValue = 0;
        asset.TargetScope = BattleCardTargetScope.None;
        asset.RuleProgram = new BattleCardRuleProgram { Trigger = BattleRuleTrigger.OnPlay };

        configure(asset);

        if (created) AssetDatabase.CreateAsset(asset, path);
        EditorUtility.SetDirty(asset);
        return 1;
    }

    /// <summary>构造一条规则步骤（有限零件；参数语义见 BattleCardRuleStep）。</summary>
    private static BattleCardRuleStep Step(BattleRuleOperation operation,
                                           BattleRuleTargetSource targetSource,
                                           BattleRuleValueSource valueSource,
                                           int valueParam,
                                           BattleRuleSelect select = BattleRuleSelect.All,
                                           BattleRuleLifetime lifetime = BattleRuleLifetime.BattleRuntime,
                                           BattleRuleCondition bonusCondition = BattleRuleCondition.None,
                                           int bonusConditionParam = 0,
                                           BattleRuleValueSource bonusValueSource = BattleRuleValueSource.None,
                                           int bonusValueParam = 0)
    {
        return new BattleCardRuleStep
        {
            Operation = operation,
            TargetSource = targetSource,
            Select = select,
            ValueSource = valueSource,
            ValueParam = valueParam,
            Lifetime = lifetime,
            BonusCondition = bonusCondition,
            BonusConditionParam = bonusConditionParam,
            BonusValueSource = bonusValueSource,
            BonusValueParam = bonusValueParam
        };
    }
}
