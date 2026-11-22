using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Battle V0.1 Gate 0 规则引擎模块验证（纯逻辑，不进入 Unity 场景）。
///
/// 覆盖（对应 Gate 0 Development Spec §13 Verification Matrix）：
///   - Card Runtime：Base / Actual 分离、Additive Modifier 可累积、Clamp ≥ 0、实例隔离、不写静态模板；
///   - Deck Cycle：Discard 不 Reset、Retain 不 Reset、Discard → Reshuffle Reset、只 Reset 真正进入新 Cycle 的牌；
///   - Selector：Actual Cost MaxBy、并列取最左、排除自身、结果确定；
///   - Operation Applicability：无 Damage 的牌仍可被加工，ModifyDamage 记为 Not Applicable；
///   - Play Requirement：无合法结果不可打、Bonus 不成立仍可打、判断发生在支付之前；
///   - Shield / Move / Swap 的规则层行为（写入经 IBattleRuleContext，模块验证用 Fake Context）。
///
/// 正式产品集成（真实 Person Owner / Run Deck / Shared Deck / Shared AP / Battle UI）由 PlayMode 测试覆盖。
/// </summary>
public class BattleGate0RuleEngineTests
{
    // ==================== Card Runtime ====================

    [Test]
    public void CardRuntime_BaseAndActualAreSeparated_AndModifiersAreAdditive()
    {
        var card = MakeCard("bcard_sword", 3, 8, "person_a");

        Assert.AreEqual(3, card.BaseCost, "基础费用应来自模板");
        Assert.AreEqual(8, card.BaseDamage, "基础伤害应来自模板");
        Assert.AreEqual(3, card.ActualCost, "未加工时 Actual = Base");
        Assert.AreEqual(8, card.ActualDamage, "未加工时 Actual = Base");

        card.AddCostModifier(-1);
        card.AddDamageModifier(-2);
        Assert.AreEqual(2, card.ActualCost, "第一次加工：费用 3 → 2");
        Assert.AreEqual(6, card.ActualDamage, "第一次加工：伤害 8 → 6");

        card.AddCostModifier(-1);
        card.AddDamageModifier(-2);
        Assert.AreEqual(1, card.ActualCost, "第二次加工可累积：费用 2 → 1");
        Assert.AreEqual(4, card.ActualDamage, "第二次加工可累积：伤害 6 → 4");

        Assert.AreEqual(3, card.BaseCost, "Actual 变化不得修改 Base Cost");
        Assert.AreEqual(8, card.BaseDamage, "Actual 变化不得修改 Base Damage");
    }

    [Test]
    public void CardRuntime_ActualValuesClampAtZero()
    {
        var card = MakeCard("bcard_sword", 1, 2, "person_a");

        card.AddCostModifier(-5);
        card.AddDamageModifier(-5);

        Assert.AreEqual(0, card.ActualCost, "Cost 不得低于 0");
        Assert.AreEqual(0, card.ActualDamage, "Damage 不得低于 0");
    }

    [Test]
    public void CardRuntime_MultipleInstances_DoNotShareModifierState()
    {
        var template = MakeTemplate("bcard_sword", 3, 8);
        var entry = new BattlePersonCardEntry(template, "person_a", 2);

        var first = BattleCard.FromEntry(entry, 0);
        var second = BattleCard.FromEntry(entry, 1);

        first.AddCostModifier(-2);
        first.AddDamageModifier(-4);

        Assert.AreEqual(1, first.ActualCost);
        Assert.AreEqual(3, second.ActualCost, "同一模板的另一实例不得串状态");
        Assert.AreEqual(8, second.ActualDamage, "同一模板的另一实例不得串状态");
        Assert.AreEqual(3, template.Cost, "静态模板不得被写入");
        Assert.AreEqual(8, template.EffectValue, "静态模板不得被写入");
    }

    [Test]
    public void CardRuntime_DifferentOwners_DoNotShareModifierState()
    {
        var template = MakeTemplate("bcard_sword", 3, 8);

        var cook = BattleCard.FromEntry(new BattlePersonCardEntry(template, "person_cook", 1), 0);
        var hunter = BattleCard.FromEntry(new BattlePersonCardEntry(template, "person_hunter", 1), 0);

        cook.AddCostModifier(-1);

        Assert.AreEqual(2, cook.ActualCost);
        Assert.AreEqual(3, hunter.ActualCost, "不同 Person 拥有同模板牌时不串状态");
    }

    // ==================== Deck Cycle Reset ====================

    [Test]
    public void DeckCycle_DiscardKeepsModifier_ButReshuffleResetsIt()
    {
        var deck = new SharedDeck(new System.Random(1234));
        var template = MakeTemplate("bcard_sword", 3, 8);
        deck.BuildDeck(new List<BattlePersonCardEntry> { new BattlePersonCardEntry(template, "person_a", 1) });
        deck.Reshuffle();

        var card = deck.Draw(1)[0];
        card.AddCostModifier(-1);
        card.AddDamageModifier(-2);
        Assert.AreEqual(2, card.ActualCost);

        // 打出 / 进入弃牌堆：临时状态仍在
        Assert.IsTrue(deck.Discard(card));
        Assert.AreEqual(2, card.ActualCost, "进入弃牌堆本身不得清除 Current Deck Cycle 修正");
        Assert.AreEqual(6, card.ActualDamage);

        // 弃牌堆洗回抽牌堆（新的 Deck Cycle）：该具体牌重置
        deck.Draw(1);
        Assert.AreEqual(3, card.ActualCost, "重新进入 Draw Pile 后应恢复本场基础状态");
        Assert.AreEqual(8, card.ActualDamage, "重新进入 Draw Pile 后应恢复本场基础状态");
        Assert.IsFalse(card.HasCurrentDeckCycleModifier);
    }

    [Test]
    public void DeckCycle_RetainedCardIsNotResetByOtherCardsReshuffle()
    {
        var deck = new SharedDeck(new System.Random(99));
        var retainedTemplate = MakeTemplate("bcard_stoke_fire", 1, 0);
        var otherTemplate = MakeTemplate("bcard_sword", 3, 8);
        deck.BuildDeck(new List<BattlePersonCardEntry>
        {
            new BattlePersonCardEntry(retainedTemplate, "person_cook", 1),
            new BattlePersonCardEntry(otherTemplate, "person_cook", 1)
        });
        deck.Reshuffle();

        // 抽空手牌：始终把第一张当作 Retain 牌处理
        var hand = deck.Draw(deck.DrawPileCount);
        BattleCard retained = null;
        foreach (var card in hand)
        {
            if (card.TemplateId == "bcard_stoke_fire") retained = card;
        }
        Assert.IsNotNull(retained, "应抽到 Retain 牌");
        foreach (var card in hand)
        {
            if (card != retained) deck.Discard(card);
        }

        deck.Retain(retained);
        retained.AddCostModifier(-1);
        Assert.AreEqual(0, retained.ActualCost, "1 费牌加工 -1 后为 0");

        // 弃牌堆洗回（其它牌进入新 Cycle）——Retain 牌仍在手牌中，不应被重置
        deck.Draw(1);

        Assert.IsTrue(deck.Hand.Contains(retained), "Retain 牌应留在手牌");
        Assert.IsTrue(retained.HasCurrentDeckCycleModifier, "未进入新 Cycle 的 Retain 牌不得被其它牌重洗重置");
    }

    // ==================== Selector / 自动目标（《切配》结构） ====================

    [Test]
    public void Selector_PicksHighestActualCostCard_AndExcludesSourceCard()
    {
        var context = new FakeRuleContext();
        context.AddAlly("u_cook", "person_cook", 20, 5);
        var source = context.AddHandCard(MakeCard("bcard_prep_cut", 0, 0, "person_cook", PrepCutProgram()));
        context.AddHandCard(MakeCard("bcard_cheap", 1, 4, "person_cook"));
        var expensive = context.AddHandCard(MakeCard("bcard_expensive", 3, 8, "person_cook"));

        var legality = BattleCardRuleResolver.CheckPlayable(source, context);

        Assert.IsTrue(legality.IsLegal, "手牌另有牌时应可打出");
        Assert.AreEqual(1, legality.PrimaryTargets.Count, "MaxBy 选择器应只绑定一张牌");
        Assert.AreSame(expensive, legality.PrimaryTargets[0].Card, "应选中 Actual Cost 最高的牌");
        Assert.AreNotSame(source, legality.PrimaryTargets[0].Card, "不得选中《切配》自身");
    }

    [Test]
    public void Selector_UsesActualCost_NotBaseCost()
    {
        var context = new FakeRuleContext();
        context.AddAlly("u_cook", "person_cook", 20, 5);
        var source = context.AddHandCard(MakeCard("bcard_prep_cut", 0, 0, "person_cook", PrepCutProgram()));

        var highBase = MakeCard("bcard_high_base", 3, 8, "person_cook");
        highBase.AddCostModifier(-2);              // Actual = 1（Base 仍为 3）
        context.AddHandCard(highBase);
        var steady = context.AddHandCard(MakeCard("bcard_steady", 2, 6, "person_cook")); // Actual = 2

        var legality = BattleCardRuleResolver.CheckPlayable(source, context);

        Assert.IsTrue(legality.IsLegal);
        Assert.AreSame(steady, legality.PrimaryTargets[0].Card,
            "Selector 读取 Actual Cost：已加工到 1 费的牌不应再按 Base Cost(3) 被选中");
    }

    [Test]
    public void Selector_TieBreaksByLeftmostHandOrder()
    {
        var context = new FakeRuleContext();
        context.AddAlly("u_cook", "person_cook", 20, 5);
        var source = context.AddHandCard(MakeCard("bcard_prep_cut", 0, 0, "person_cook", PrepCutProgram()));
        var leftmost = context.AddHandCard(MakeCard("bcard_first", 2, 6, "person_cook"));
        context.AddHandCard(MakeCard("bcard_second", 2, 6, "person_cook"));

        var first = BattleCardRuleResolver.CheckPlayable(source, context);
        var second = BattleCardRuleResolver.CheckPlayable(source, context);

        Assert.AreSame(leftmost, first.PrimaryTargets[0].Card, "Actual Cost 并列时应取手牌最左");
        Assert.AreSame(leftmost, second.PrimaryTargets[0].Card, "重复判断结果必须确定、非随机");
    }

    [Test]
    public void PlayRequirement_PrepCut_IsUnplayableWithoutAnotherCard()
    {
        var context = new FakeRuleContext();
        context.AddAlly("u_cook", "person_cook", 20, 5);
        var source = context.AddHandCard(MakeCard("bcard_prep_cut", 0, 0, "person_cook", PrepCutProgram()));

        var legality = BattleCardRuleResolver.CheckPlayable(source, context);

        Assert.IsFalse(legality.IsLegal, "手牌除自身外没有其它 Card → 不可打出");
        Assert.AreEqual(BattleCardRuleLegalityKind.PrimaryResolutionUnavailable, legality.Kind);
    }

    [Test]
    public void PlayRequirement_FillGap_IsUnplayableWithoutEmptyCell()
    {
        var context = new FakeRuleContext();
        context.AddAlly("u_worker", "person_worker", 20, 5);
        var card = context.AddHandCard(MakeCard("bcard_fill_gap", 1, 0, "person_worker", FillGapProgram()));
        context.OccupyAllCells(); // 我方战场已无空格

        var legality = BattleCardRuleResolver.CheckPlayable(card, context);

        Assert.IsFalse(legality.IsLegal, "没有合法我方空格 → 不可打出");
        Assert.AreEqual(BattleCardRuleLegalityKind.NoLegalManualTarget, legality.Kind,
            "空格目标牌先由手选目标集合判定（与 Primary Resolution 同源）");
        Assert.IsFalse(BattleCardRuleResolver.CheckPlayable(card, context).IsLegal, "重复判断仍必须不可打出");
    }

    [Test]
    public void PlayRequirement_SwapPost_IsUnplayableWithoutOtherAlly()
    {
        var context = new FakeRuleContext();
        context.AddAlly("u_worker", "person_worker", 20, 5);
        var card = context.AddHandCard(MakeCard("bcard_swap_post", 1, 0, "person_worker", SwapPostProgram()));

        var legality = BattleCardRuleResolver.CheckPlayable(card, context);

        Assert.IsFalse(legality.IsLegal, "没有另一名合法可交换友方 → 不可打出");
        Assert.AreEqual(BattleCardRuleLegalityKind.NoLegalManualTarget, legality.Kind);
    }

    // ==================== Bonus / Operation Applicability / Target Binding ====================

    [Test]
    public void Bonus_UnmetConditionStillPlayable_AndMetConditionAddsValue()
    {
        var context = new FakeRuleContext();
        context.AddAlly("u_hunter", "person_hunter", 20, 6);
        var healthy = context.AddEnemy("u_enemy_full", 30, 30, false);
        var wounded = context.AddEnemy("u_enemy_wounded", 10, 30, false);

        var program = new BattleCardRuleProgram
        {
            Trigger = BattleRuleTrigger.OnPlay,
            ManualTargetKind = BattleRuleManualTargetKind.EnemyUnit
        };
        program.Steps.Add(DamageStep(BattleRuleTargetSource.ManualTarget, BattleRuleValueSource.CasterAttack, 0,
            BattleRuleCondition.TargetHpPercentAtOrBelow, 50, 2));

        var healthyCard = context.AddHandCard(MakeCard("bcard_healthy_target", 1, 0, "person_hunter", program));
        var woundedCard = context.AddHandCard(MakeCard("bcard_wounded_target", 1, 0, "person_hunter", program));

        var healthyPlan = BattleCardRuleResolver.BuildPlan(healthyCard, context,
            BattleRuleTargetRef.ForUnit(context.GetUnitSnapshot(healthy)));
        var healthyResult = BattleCardRuleExecutor.Execute(healthyPlan, context);

        var woundedPlan = BattleCardRuleResolver.BuildPlan(woundedCard, context,
            BattleRuleTargetRef.ForUnit(context.GetUnitSnapshot(wounded)));
        var woundedResult = BattleCardRuleExecutor.Execute(woundedPlan, context);

        Assert.AreEqual(6, healthyResult.Outcomes[0].Value, "低血 Bonus 不成立时只结算基础伤害");
        Assert.IsFalse(healthyResult.Outcomes[0].BonusApplied, "条件不成立 → Bonus 不附加");
        Assert.AreEqual(BattleRuleStepOutcomeKind.Applied, healthyResult.Outcomes[0].Kind,
            "Bonus 不成立不得让整张牌不可打或空结算");

        Assert.AreEqual(8, woundedResult.Outcomes[0].Value, "低血 Bonus 成立时附加固定伤害");
        Assert.IsTrue(woundedResult.Outcomes[0].BonusApplied);
        Assert.AreEqual(24, context.GetHp("u_enemy_full"), "未满足条件的目标只承受基础伤害 6（30 → 24）");
        Assert.AreEqual(2, context.GetHp("u_enemy_wounded"), "基础 6 + Bonus 2 = 8（10 → 2）");
    }

    [Test]
    public void Execute_ModifyDamage_IsNotApplicableOnCardWithoutDamage_ButCostModifyApplies()
    {
        var context = new FakeRuleContext();
        context.AddAlly("u_cook", "person_cook", 20, 5);
        var source = context.AddHandCard(MakeCard("bcard_prep_cut", 0, 0, "person_cook", PrepCutProgram()));
        var healCard = context.AddHandCard(MakeCard("bcard_heal", 3, 0, "person_cook")); // 无 Damage（治疗牌）
        var damageCard = context.AddHandCard(MakeCard("bcard_sword", 1, 4, "person_cook"));

        var plan = BattleCardRuleResolver.BuildPlan(source, context);
        var resolution = BattleCardRuleExecutor.Execute(plan, context);

        Assert.AreEqual(2, resolution.Outcomes.Count, "两条步骤都应有结果记录");
        Assert.AreSame(healCard, plan.PrimaryTargets[0].Card, "费用最高的牌应被绑定为加工目标");

        Assert.AreEqual(BattleRuleStepOutcomeKind.Applied, resolution.Outcomes[0].Kind, "Cost 修改适用");
        Assert.AreEqual(2, healCard.ActualCost, "无 Damage 的治疗牌仍然合法：Cost 修改正常执行");

        Assert.AreEqual(BattleRuleStepOutcomeKind.NotApplicable, resolution.Outcomes[1].Kind,
            "目标没有 Damage → Damage 修改为 Not Applicable");
        Assert.AreEqual(1, damageCard.ActualCost, "未被选中的牌不应被加工");
        Assert.IsTrue(resolution.AnyApplied, "整张牌不得因为一条 Not Applicable 而整体失败");
    }

    [Test]
    public void Execute_TargetBindingStaysStable_AfterCostWasModified()
    {
        var context = new FakeRuleContext();
        context.AddAlly("u_cook", "person_cook", 20, 5);
        var source = context.AddHandCard(MakeCard("bcard_prep_cut", 0, 0, "person_cook", PrepCutProgram()));
        var biggest = context.AddHandCard(MakeCard("bcard_biggest", 3, 8, "person_cook"));
        var other = context.AddHandCard(MakeCard("bcard_other", 3, 8, "person_cook"));

        var plan = BattleCardRuleResolver.BuildPlan(source, context);
        Assert.AreSame(biggest, plan.PrimaryTargets[0].Card, "绑定阶段应选中手牌最左的最高费牌");

        var resolution = BattleCardRuleExecutor.Execute(plan, context);

        // 第一个步骤把 biggest 从 3 费降到 2 费；第二个步骤必须仍作用于同一张牌，不得改选 other
        Assert.AreEqual(2, biggest.ActualCost);
        Assert.AreEqual(6, biggest.ActualDamage, "Damage 修改必须仍作用于绑定目标");
        Assert.AreEqual(3, other.ActualCost, "不得因费用变化误选另一张牌");
        Assert.AreEqual(8, other.ActualDamage, "不得因费用变化误选另一张牌");
        Assert.AreEqual(2, resolution.Outcomes.Count);
    }

    [Test]
    public void Execute_SharedActionPointsZeroBonus_IsEvaluatedAfterPayment()
    {
        var context = new FakeRuleContext();
        context.AddAlly("u_cook", "person_cook", 20, 5);
        context.SharedActionPoints = 1;

        var program = new BattleCardRuleProgram { Trigger = BattleRuleTrigger.OnPlay };
        program.Steps.Add(new BattleCardRuleStep
        {
            Operation = BattleRuleOperation.Shield,
            TargetSource = BattleRuleTargetSource.CardOwner,
            ValueSource = BattleRuleValueSource.Fixed,
            ValueParam = 3,
            BonusCondition = BattleRuleCondition.SharedActionPointsIsZero,
            BonusValueSource = BattleRuleValueSource.Fixed,
            BonusValueParam = 3
        });
        var card = context.AddHandCard(MakeCard("bcard_stoke_fire", 1, 0, "person_cook", program));

        var plan = BattleCardRuleResolver.BuildPlan(card, context);
        context.SharedActionPoints = 0;   // 模拟支付 1 费后共享行动力归零
        var resolution = BattleCardRuleExecutor.Execute(plan, context);

        Assert.AreEqual(6, resolution.Outcomes[0].Value, "AP 归零条件应在支付之后按真实状态判定");
        Assert.AreEqual(6, context.GetShield("u_cook"), "护盾必须写入真实战场状态");
    }

    // ==================== 空间（Move / Swap 原子性） ====================

    [Test]
    public void Execute_MoveToLegalEmptyCell_MovesUnitAndKeepsOccupancyConsistent()
    {
        var context = new FakeRuleContext();
        var worker = context.AddAlly("u_worker", "person_worker", 20, 5);
        var card = context.AddHandCard(MakeCard("bcard_fill_gap", 1, 0, "person_worker", FillGapProgram()));

        var plan = BattleCardRuleResolver.BuildPlan(card, context, BattleRuleTargetRef.ForCell(2, 0, null));
        var result = BattleCardRuleExecutor.Execute(plan, context);

        Assert.AreEqual(BattleRuleStepOutcomeKind.Applied, result.Outcomes[0].Kind, "合法空格移动应成功");
        Assert.AreEqual(2, worker.Row);
        Assert.AreEqual(0, worker.Column);
        Assert.AreEqual("u_worker", context.GetCellOccupant(2, 0), "移动后占位与单位位置必须一致");
        Assert.IsNull(context.GetCellOccupant(0, 0), "旧格必须被释放");
    }

    [Test]
    public void Execute_IllegalMove_WritesNothing()
    {
        var context = new FakeRuleContext();
        var worker = context.AddAlly("u_worker", "person_worker", 20, 5);
        var cook = context.AddAlly("u_cook", "person_cook", 20, 5);
        var card = context.AddHandCard(MakeCard("bcard_fill_gap", 1, 0, "person_worker", FillGapProgram()));

        int rowBefore = worker.Row;
        int colBefore = worker.Column;

        // 目标格已被占据 → 非法
        var plan = BattleCardRuleResolver.BuildPlan(card, context,
            BattleRuleTargetRef.ForCell(cook.Row, cook.Column, null));
        var result = BattleCardRuleExecutor.Execute(plan, context);

        Assert.AreEqual(BattleRuleStepOutcomeKind.Failed, result.Outcomes[0].Kind, "目标格被占据 → 移动失败");
        Assert.AreEqual(rowBefore, worker.Row, "失败操作零部分写入");
        Assert.AreEqual(colBefore, worker.Column, "失败操作零部分写入");
        Assert.AreEqual("u_cook", context.GetCellOccupant(cook.Row, cook.Column), "被占据格不得被覆盖");
    }

    [Test]
    public void Execute_SwapPost_SwapsBothUnitsAtomically()
    {
        var context = new FakeRuleContext();
        var worker = context.AddAlly("u_worker", "person_worker", 20, 5);
        var cook = context.AddAlly("u_cook", "person_cook", 20, 5);
        worker.Row = 0; worker.Column = 1;
        cook.Row = 2; cook.Column = 1;

        var card = context.AddHandCard(MakeCard("bcard_swap_post", 1, 0, "person_worker", SwapPostProgram()));
        var plan = BattleCardRuleResolver.BuildPlan(card, context,
            BattleRuleTargetRef.ForUnit(context.GetUnitSnapshot(cook)));
        var result = BattleCardRuleExecutor.Execute(plan, context);

        Assert.AreEqual(BattleRuleStepOutcomeKind.Applied, result.Outcomes[0].Kind);
        Assert.AreEqual(2, worker.Row, "Worker 应移动到目标单位的位置");
        Assert.AreEqual(0, cook.Row, "目标单位应移动到 Worker 的位置");
        Assert.AreEqual("u_worker", context.GetCellOccupant(2, 1));
        Assert.AreEqual("u_cook", context.GetCellOccupant(0, 1));
    }

    [Test]
    public void Execute_SwapPost_CannotTargetDownedOrSelf()
    {
        var context = new FakeRuleContext();
        context.AddAlly("u_worker", "person_worker", 20, 5);
        var downed = context.AddAlly("u_downed", "person_downed", 0, 5);
        var card = context.AddHandCard(MakeCard("bcard_swap_post", 1, 0, "person_worker", SwapPostProgram()));

        var legality = BattleCardRuleResolver.CheckPlayable(card, context,
            BattleRuleTargetRef.ForUnit(context.GetUnitSnapshot(downed)));

        Assert.IsFalse(legality.IsLegal, "失去战斗能力的单位不得成为交换目标");
        Assert.AreEqual(BattleCardRuleLegalityKind.NoLegalManualTarget, legality.Kind);
    }

    /// <summary>创建一张静态牌模板（不进入内容资产，仅用于模块测试）</summary>
    private static BattleCardData MakeTemplate(string cardId, int cost, int damage)
    {
        var template = ScriptableObject.CreateInstance<BattleCardData>();
        template.CardId = cardId;
        template.DisplayName = cardId;
        template.CardKind = BattleCardKind.Character;
        template.Cost = cost;
        template.Effect = damage > 0 ? BattleCardEffectType.Damage : BattleCardEffectType.None;
        template.EffectValue = damage;
        return template;
    }

    /// <summary>创建一张运行时牌实例（归属指定 Person）</summary>
    private static BattleCard MakeCard(string cardId, int cost, int damage, string personId,
                                       BattleCardRuleProgram program = null)
    {
        var template = MakeTemplate(cardId, cost, damage);
        template.RuleProgram = program ?? new BattleCardRuleProgram();
        return BattleCard.FromEntry(new BattlePersonCardEntry(template, personId, 1), 0);
    }

    /// <summary>《切配》结构的规则程序（自动目标：手牌 Actual Cost 最高的其它牌）</summary>
    private static BattleCardRuleProgram PrepCutProgram()
    {
        var program = new BattleCardRuleProgram { Trigger = BattleRuleTrigger.OnPlay };
        program.PlayRequirement = new BattleCardPlayRequirement
        {
            TargetSource = BattleRuleTargetSource.HandOthers,
            Select = BattleRuleSelect.MaxByActualCostLeftmost,
            MinCount = 1
        };
        program.Steps.Add(new BattleCardRuleStep
        {
            Operation = BattleRuleOperation.ModifyCost,
            TargetSource = BattleRuleTargetSource.HandOthers,
            Select = BattleRuleSelect.MaxByActualCostLeftmost,
            ValueSource = BattleRuleValueSource.Fixed,
            ValueParam = -1,
            Lifetime = BattleRuleLifetime.CurrentDeckCycle
        });
        program.Steps.Add(new BattleCardRuleStep
        {
            Operation = BattleRuleOperation.ModifyDamage,
            TargetSource = BattleRuleTargetSource.HandOthers,
            Select = BattleRuleSelect.MaxByActualCostLeftmost,
            ValueSource = BattleRuleValueSource.Fixed,
            ValueParam = -2,
            Lifetime = BattleRuleLifetime.CurrentDeckCycle
        });
        return program;
    }

    /// <summary>《补位》结构的规则程序（手选我方空格 + 移动）</summary>
    private static BattleCardRuleProgram FillGapProgram()
    {
        var program = new BattleCardRuleProgram
        {
            Trigger = BattleRuleTrigger.OnPlay,
            ManualTargetKind = BattleRuleManualTargetKind.AllyCell
        };
        program.PlayRequirement = new BattleCardPlayRequirement
        {
            TargetSource = BattleRuleTargetSource.EmptyAllyCells,
            Select = BattleRuleSelect.All,
            MinCount = 1
        };
        program.Steps.Add(new BattleCardRuleStep
        {
            Operation = BattleRuleOperation.Move,
            TargetSource = BattleRuleTargetSource.ManualTarget,
            ValueSource = BattleRuleValueSource.None
        });
        return program;
    }

    /// <summary>《换岗》结构的规则程序（手选其它我方单位 + 交换位置）</summary>
    private static BattleCardRuleProgram SwapPostProgram()
    {
        var program = new BattleCardRuleProgram
        {
            Trigger = BattleRuleTrigger.OnPlay,
            ManualTargetKind = BattleRuleManualTargetKind.AllyUnit
        };
        program.PlayRequirement = new BattleCardPlayRequirement
        {
            TargetSource = BattleRuleTargetSource.OtherAllyUnits,
            Select = BattleRuleSelect.All,
            MinCount = 1
        };
        program.Steps.Add(new BattleCardRuleStep
        {
            Operation = BattleRuleOperation.Swap,
            TargetSource = BattleRuleTargetSource.ManualTarget,
            ValueSource = BattleRuleValueSource.None
        });
        return program;
    }

    private static BattleCardRuleStep DamageStep(BattleRuleTargetSource source, BattleRuleValueSource valueSource,
                                                 int valueParam,
                                                 BattleRuleCondition bonusCondition = BattleRuleCondition.None,
                                                 int bonusConditionParam = 0,
                                                 int bonusValueParam = 0)
    {
        return new BattleCardRuleStep
        {
            Operation = BattleRuleOperation.Damage,
            TargetSource = source,
            ValueSource = valueSource,
            ValueParam = valueParam,
            BonusCondition = bonusCondition,
            BonusConditionParam = bonusConditionParam,
            BonusValueSource = bonusCondition == BattleRuleCondition.None ? BattleRuleValueSource.None : BattleRuleValueSource.Fixed,
            BonusValueParam = bonusValueParam
        };
    }

    /// <summary>
    /// 模块验证用的内存 Battle Fact 视图（不进入 Unity 场景 / Assembly-CSharp）。
    /// 语义与 BattleRuleContextRuntime 保持一致：占位与单位坐标同源，失败写入零副作用。
    /// </summary>
    private sealed class FakeRuleContext : IBattleRuleContext
    {
        private readonly List<BattleRuleUnitFacts> _allies = new List<BattleRuleUnitFacts>();
        private readonly List<BattleRuleUnitFacts> _enemies = new List<BattleRuleUnitFacts>();
        private readonly List<BattleCard> _hand = new List<BattleCard>();

        public int SharedActionPoints;

        public int SharedActionPointsCurrent
        {
            get { return SharedActionPoints; }
        }

        public BattleRuleUnitFacts AddAlly(string unitId, string personId, int hp, int attack)
        {
            var facts = NewUnit(unitId, personId, hp, hp, attack, true);
            PlaceInFirstFreeCell(facts);
            _allies.Add(facts);
            return facts;
        }

        public BattleRuleUnitFacts AddEnemy(string unitId, int hp, int maxHp, bool intentIsDefend)
        {
            var facts = NewUnit(unitId, null, hp, maxHp, 1, false);
            facts.IntentIsDefend = intentIsDefend;
            PlaceInFirstFreeCell(facts);
            _enemies.Add(facts);
            return facts;
        }

        public BattleCard AddHandCard(BattleCard card)
        {
            _hand.Add(card);
            return card;
        }

        /// <summary>填满全部我方格子（用于“没有合法空格”的验证场景）</summary>
        public void OccupyAllCells()
        {
            for (int row = 0; row < 3; row++)
            {
                for (int col = 0; col < 3; col++)
                {
                    if (GetCellOccupant(row, col) != null) continue;
                    var blocker = NewUnit("u_blocker_" + row + "_" + col, null, 10, 10, 1, false);
                    blocker.Row = row;
                    blocker.Column = col;
                    _enemies.Add(blocker);
                }
            }
        }

        public int GetHp(string unitId)
        {
            var unit = FindUnit(unitId);
            return unit != null ? unit.CurrentHP : -1;
        }

        public int GetShield(string unitId)
        {
            var unit = FindUnit(unitId);
            return unit != null ? unit.Shield : -1;
        }

        public BattleRuleUnitFacts GetUnitSnapshot(BattleRuleUnitFacts unit)
        {
            return FindUnit(unit != null ? unit.UnitId : null);
        }

        public string GetCellOccupant(int row, int col)
        {
            foreach (var ally in _allies)
            {
                if (ally.Row == row && ally.Column == col) return ally.UnitId;
            }
            foreach (var enemy in _enemies)
            {
                if (enemy.Row == row && enemy.Column == col) return enemy.UnitId;
            }
            return null;
        }

        public IReadOnlyList<BattleRuleUnitFacts> GetAllies()
        {
            return _allies;
        }

        public IReadOnlyList<BattleRuleUnitFacts> GetEnemies()
        {
            return _enemies;
        }

        public IReadOnlyList<BattleRuleCellFacts> GetAlliedCells()
        {
            var cells = new List<BattleRuleCellFacts>();
            for (int row = 0; row < 3; row++)
            {
                for (int col = 0; col < 3; col++)
                {
                    string occupant = GetCellOccupant(row, col);
                    cells.Add(new BattleRuleCellFacts
                    {
                        Row = row,
                        Column = col,
                        Occupied = occupant != null,
                        OccupantPersonId = occupant
                    });
                }
            }
            return cells;
        }

        public IReadOnlyList<BattleCard> GetHandCards()
        {
            return new List<BattleCard>(_hand);
        }

        public bool TryGetUnit(string unitId, out BattleRuleUnitFacts unit)
        {
            unit = FindUnit(unitId);
            return unit != null;
        }

        public bool TryGetOwnerUnitId(BattleCard card, out string unitId)
        {
            unitId = null;
            if (card == null || string.IsNullOrEmpty(card.OwnerPersonId)) return false;
            foreach (var ally in _allies)
            {
                if (ally.PersonId == card.OwnerPersonId)
                {
                    unitId = ally.UnitId;
                    return true;
                }
            }
            return false;
        }

        public IReadOnlyList<BattleRuleUnitFacts> GetAdjacentAllies(string unitId)
        {
            var result = new List<BattleRuleUnitFacts>();
            var origin = FindUnit(unitId);
            if (origin == null) return result;

            foreach (var ally in _allies)
            {
                if (ally == origin || !ally.IsCombatCapable) continue;
                if (ally.Row == origin.Row && Math.Abs(ally.Column - origin.Column) == 1) result.Add(ally);
                else if (ally.Column == origin.Column && Math.Abs(ally.Row - origin.Row) == 1) result.Add(ally);
            }
            return result;
        }

        public bool OwnerIsAloneInRow(BattleCard card)
        {
            string unitId;
            if (!TryGetOwnerUnitId(card, out unitId)) return false;
            var owner = FindUnit(unitId);
            if (owner == null) return false;

            foreach (var ally in _allies)
            {
                if (ally == owner || !ally.IsCombatCapable) continue;
                if (ally.Row == owner.Row) return false;
            }
            return true;
        }

        public bool ApplyDamage(string unitId, int amount)
        {
            var unit = FindUnit(unitId);
            if (unit == null || !unit.IsCombatCapable || amount <= 0) return false;

            int absorbed = Math.Min(unit.Shield, amount);
            unit.Shield -= absorbed;
            amount -= absorbed;
            if (amount > 0) unit.CurrentHP = Math.Max(0, unit.CurrentHP - amount);
            return true;
        }

        public bool ApplyHeal(string unitId, int amount)
        {
            var unit = FindUnit(unitId);
            if (unit == null || !unit.IsCombatCapable || amount <= 0) return false;
            unit.CurrentHP = Math.Min(unit.MaxHP, unit.CurrentHP + amount);
            return true;
        }

        public bool ApplyShield(string unitId, int amount)
        {
            var unit = FindUnit(unitId);
            if (unit == null || !unit.IsCombatCapable || amount <= 0) return false;
            unit.Shield += amount;
            return true;
        }

        public bool DrawCards(int count)
        {
            // 模块测试不覆盖抽牌来源（抽牌由产品集成测试覆盖真实 SharedDeck）
            return false;
        }

        public bool ApplyCostModifier(BattleCard card, int delta)
        {
            if (card == null || delta == 0) return false;
            card.AddCostModifier(delta);
            return true;
        }

        public bool ApplyDamageModifier(BattleCard card, int delta)
        {
            if (card == null || delta == 0) return false;
            card.AddDamageModifier(delta);
            return true;
        }

        public bool TryMoveOwnerTo(BattleCard card, int row, int column)
        {
            if (row < 0 || row > 2 || column < 0 || column > 2) return false;

            string unitId;
            if (!TryGetOwnerUnitId(card, out unitId)) return false;
            var owner = FindUnit(unitId);
            if (owner == null || !owner.IsCombatCapable) return false;
            if (GetCellOccupant(row, column) != null) return false;

            owner.Row = row;
            owner.Column = column;
            return true;
        }

        public bool TrySwapOwnerWith(BattleCard card, string otherUnitId)
        {
            string unitId;
            if (!TryGetOwnerUnitId(card, out unitId)) return false;
            if (string.IsNullOrEmpty(otherUnitId) || otherUnitId == unitId) return false;

            var owner = FindUnit(unitId);
            var other = FindUnit(otherUnitId);
            if (owner == null || other == null) return false;
            if (!owner.IsCombatCapable || !other.IsCombatCapable) return false;

            int row = owner.Row;
            int col = owner.Column;
            owner.Row = other.Row;
            owner.Column = other.Column;
            other.Row = row;
            other.Column = col;
            return true;
        }

        private BattleRuleUnitFacts FindUnit(string unitId)
        {
            if (string.IsNullOrEmpty(unitId)) return null;
            foreach (var ally in _allies)
            {
                if (ally.UnitId == unitId) return ally;
            }
            foreach (var enemy in _enemies)
            {
                if (enemy.UnitId == unitId) return enemy;
            }
            return null;
        }

        private static BattleRuleUnitFacts NewUnit(string unitId, string personId, int hp, int maxHp,
                                                   int attack, bool isAlly)
        {
            return new BattleRuleUnitFacts
            {
                UnitId = unitId,
                PersonId = personId,
                IsAlly = isAlly,
                CurrentHP = hp,
                MaxHP = maxHp,
                Attack = attack,
                Row = -1,
                Column = -1
            };
        }

        private void PlaceInFirstFreeCell(BattleRuleUnitFacts unit)
        {
            for (int row = 0; row < 3; row++)
            {
                for (int col = 0; col < 3; col++)
                {
                    if (GetCellOccupant(row, col) != null) continue;
                    unit.Row = row;
                    unit.Column = col;
                    return;
                }
            }
        }
    }
}
