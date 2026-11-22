using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Starter Slot 与 Unlocked Pool 纯逻辑测试。
/// 覆盖：初始 1 槽、同模板多槽、空槽/替换/熟练+1 规则、熟练上限、
///      替换后 Proficiency 不保留、Unlocked Pool 三选一候选与扩展读取。
/// </summary>
public class StarterAndPoolTests
{
    private static BattleCardData NewCard(string cardId, BattleCardKind kind = BattleCardKind.Character)
    {
        var c = ScriptableObject.CreateInstance<BattleCardData>();
        c.CardId = cardId;
        c.DisplayName = cardId;
        c.CardKind = kind;
        return c;
    }

    private static GrowthCurveConfig NewCurve(int cap = 5)
    {
        var c = ScriptableObject.CreateInstance<GrowthCurveConfig>();
        c.MaxLevel = 10;
        c.ProficiencyCap = cap;
        c.ExpRequiredToLevel = new[] { 10, 15, 20, 25, 30 };
        return c;
    }

    private static TestPerson NewPerson(string personId = "char_001", string name = "战士")
    {
        return new TestPerson(personId, name);
    }

    [Test]
    public void EnsureSeeded_InitialStarter_OneSlot_BasicAttack()
    {
        var person = NewPerson();
        GrowthDefaults.EnsureSeeded(person);

        Assert.AreEqual(1, person.StarterSlots.Count, "初始 Starter = 1 槽");
        Assert.AreEqual("bcard_aim", person.StarterSlots[0].CardId, "初始槽为普通攻击牌");
        Assert.IsTrue(person.UnlockedPoolCardIds.Count >= 1, "初始 Unlocked Pool 应有测试种子");
    }

    [Test]
    public void EnsureSeeded_IsIdempotent()
    {
        var person = NewPerson();
        GrowthDefaults.EnsureSeeded(person);
        int slots = person.StarterSlots.Count;
        int pool = person.UnlockedPoolCardIds.Count;

        GrowthDefaults.EnsureSeeded(person);
        Assert.AreEqual(slots, person.StarterSlots.Count, "重复播种不应增加槽");
        Assert.AreEqual(pool, person.UnlockedPoolCardIds.Count, "重复播种不应增加池");
    }

    [Test]
    public void SameTemplateCard_CanOccupyMultipleStarterSlots()
    {
        var person = NewPerson();
        GrowthDefaults.EnsureSeeded(person);
        person.StarterSlots.Add(new StarterSlotEntry("bcard_sword", 1));
        person.StarterSlots.Add(new StarterSlotEntry("bcard_sword", 1));

        Assert.AreEqual(2, person.StarterSlots.Count(s => s.CardId == "bcard_sword"), "同模板牌允许占多个 Starter 槽");
    }

    [Test]
    public void AddToEmptySlot_FillsSpecifiedEmptySlot()
    {
        var person = NewPerson();
        GrowthDefaults.EnsureSeeded(person);
        person.StarterSlots.Add(new StarterSlotEntry()); // 空槽

        var r = CardGrowthResolver.ApplyToStarter(person, "bcard_sword", StarterCardStrategy.AddToEmptySlot, 1, NewCurve());
        Assert.IsTrue(r.Applied);
        Assert.AreEqual("bcard_sword", person.StarterSlots[1].CardId);
        Assert.AreEqual(1, person.StarterSlots[1].Proficiency);
    }

    [Test]
    public void AddToEmptySlot_AutoFirstEmptySlot_WhenIndexNegative()
    {
        var person = NewPerson();
        GrowthDefaults.EnsureSeeded(person);
        person.StarterSlots.Add(new StarterSlotEntry()); // 空槽

        var r = CardGrowthResolver.ApplyToStarter(person, "bcard_sword", StarterCardStrategy.AddToEmptySlot, -1, NewCurve());
        Assert.IsTrue(r.Applied);
        Assert.AreEqual("bcard_sword", person.StarterSlots[1].CardId, "应自动填入第一个空槽");
    }

    [Test]
    public void AddToEmptySlot_NoEmptySlot_Fails()
    {
        var person = NewPerson();
        GrowthDefaults.EnsureSeeded(person); // 只有 1 个满槽

        var r = CardGrowthResolver.ApplyToStarter(person, "bcard_sword", StarterCardStrategy.AddToEmptySlot, -1, NewCurve());
        Assert.IsFalse(r.Applied, "无空槽时应失败（提示可替换）");
    }

    [Test]
    public void EmptySlotExists_StillAllowsReplace()
    {
        var person = NewPerson();
        GrowthDefaults.EnsureSeeded(person);
        person.StarterSlots.Add(new StarterSlotEntry()); // 有空槽
        string original = person.StarterSlots[0].CardId;

        // 有空槽时仍允许主动替换已有槽（不强制填空槽）
        var r = CardGrowthResolver.ApplyToStarter(person, "bcard_sword", StarterCardStrategy.ReplaceSlot, 0, NewCurve());
        Assert.IsTrue(r.Applied);
        Assert.AreEqual("bcard_sword", person.StarterSlots[0].CardId);
        Assert.AreNotEqual(original, person.StarterSlots[0].CardId);
    }

    [Test]
    public void MatchingCard_ProficiencyUp_InsteadOfNewSlot()
    {
        var person = NewPerson();
        GrowthDefaults.EnsureSeeded(person);
        person.StarterSlots[0].CardId = "bcard_sword";
        person.StarterSlots[0].Proficiency = 1;

        var r = CardGrowthResolver.ApplyToStarter(person, "bcard_sword", StarterCardStrategy.ProficiencyUp, -1, NewCurve());
        Assert.IsTrue(r.Applied);
        Assert.AreEqual(2, person.StarterSlots[0].Proficiency, "同模板牌应 Proficiency +1");
        Assert.AreEqual(1, person.StarterSlots.Count, "熟练+1 不新增槽");
    }

    [Test]
    public void ProficiencyUp_RespectsCap()
    {
        var person = NewPerson();
        GrowthDefaults.EnsureSeeded(person);
        person.StarterSlots[0].CardId = "bcard_sword";
        person.StarterSlots[0].Proficiency = 5;
        var curve = NewCurve(cap: 5);

        var r = CardGrowthResolver.ApplyToStarter(person, "bcard_sword", StarterCardStrategy.ProficiencyUp, -1, curve);
        Assert.IsFalse(r.Applied, "已达 Proficiency 上限不应继续提升");
        Assert.AreEqual(5, person.StarterSlots[0].Proficiency);
    }

    [Test]
    public void ReplaceSlot_ResetsAdvancedProficiency()
    {
        var person = NewPerson();
        GrowthDefaults.EnsureSeeded(person);
        person.StarterSlots[0].CardId = "bcard_aim";
        person.StarterSlots[0].Proficiency = 5;

        var r = CardGrowthResolver.ApplyToStarter(person, "bcard_sword", StarterCardStrategy.ReplaceSlot, 0, NewCurve());
        Assert.IsTrue(r.Applied);
        Assert.AreEqual("bcard_sword", person.StarterSlots[0].CardId);
        Assert.AreEqual(1, person.StarterSlots[0].Proficiency, "替换后高级 Proficiency 不保留");
    }

    [Test]
    public void BuildOffer_FromUnlockedPool_UpToThreeDistinct()
    {
        var person = NewPerson();
        person.UnlockedPoolCardIds.AddRange(new[] { "bcard_aim", "bcard_sword", "bcard_heal", "bcard_rally" });
        var templates = new List<BattleCardData> { NewCard("bcard_aim"), NewCard("bcard_sword"), NewCard("bcard_heal") };

        var offer = CardGrowthResolver.BuildOffer(person, templates);
        Assert.AreEqual(3, offer.Count, "最多三选一");
        Assert.AreEqual(offer.Count, offer.Select(c => c.CardId).Distinct().Count(), "候选去重");
    }

    [Test]
    public void BuildOffer_AfterPoolExpand_IncludesNewCard()
    {
        var person = new TestPerson("char_002", "猎人");
        // 初始池：[sword, heal]；Lv5 UnlockedPoolExpand 新增 rally
        person.UnlockedPoolCardIds.AddRange(new[] { "bcard_sword", "bcard_heal" });
        person.UnlockedPoolCardIds.Add("bcard_rally");

        var templates = new List<BattleCardData>
        {
            NewCard("bcard_sword"), NewCard("bcard_heal"), NewCard("bcard_rally")
        };
        var offer = CardGrowthResolver.BuildOffer(person, templates);
        Assert.AreEqual(3, offer.Count, "三选一候选来自 Unlocked Pool");
        Assert.IsTrue(offer.Any(c => c.CardId == "bcard_rally"), "池扩展后新牌应进入长期三选一候选");
    }

    [Test]
    public void BuildOffer_EmptyPool_ReturnsEmpty()
    {
        var person = new TestPerson("char_005", "法师");
        var offer = CardGrowthResolver.BuildOffer(person, new List<BattleCardData>());
        Assert.IsEmpty(offer);
    }
}

