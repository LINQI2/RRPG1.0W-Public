using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// M4-correction 专项 — 无 Profession 门禁 / Person 独立 / 模板隔离：
///   Test B1  普通 Character 无 Active Profession：Growth（升级/扩池/Trait/CardGrowth Offer/Starter 处理）一律失败，
///            不 fallback 旧字段；
///   Test B2  同 Profession 的两个 Character Growth 完全独立；
///   Test B3  Person Growth 不修改 ProfessionDefinition（模板仍三字段共享）。
/// </summary>
public class ProfessionGrowthGateAndIndependenceTests
{
    private static GrowthCurveConfig NewCurve(params int[] expPerLevel)
    {
        var c = ScriptableObject.CreateInstance<GrowthCurveConfig>();
        c.MaxLevel = expPerLevel.Length + 1;
        c.ProficiencyCap = 5;
        c.ExpRequiredToLevel = expPerLevel;
        return c;
    }

    private static GrowthTableConfig EmptyTable()
    {
        var t = ScriptableObject.CreateInstance<GrowthTableConfig>();
        t.TableId = "test";
        t.Nodes = new List<GrowthNodeConfig>();
        return t;
    }

    private static GrowthNodeConfig NewNode(int level, GrowthNodeType type)
    {
        var n = ScriptableObject.CreateInstance<GrowthNodeConfig>();
        n.Level = level;
        n.NodeType = type;
        return n;
    }

    private static BattleCardData NewCard(string cardId)
    {
        var c = ScriptableObject.CreateInstance<BattleCardData>();
        c.CardId = cardId;
        c.DisplayName = cardId;
        c.CardKind = BattleCardKind.Character;
        return c;
    }

    [Test]
    public void CharacterWithoutProfession_AllGrowthFails_NoLegacyFallback()
    {
        // Test B1 — 无 Profession 的普通 Character：Profession Growth 不可执行，绝不 fallback
        object character = ProfessionM4TestKit.NewCharacter("char_001", "战士", 30, 6);
        ProfessionM4TestKit.Set(character, "Level", 7);
        ProfessionM4TestKit.Set(character, "Exp", 999);
        ProfessionM4TestKit.Set(character, "UnlockedPoolCardIds",
            new List<string> { "legacy_a", "legacy_b", "legacy_c" });

        var g = (IPersonGrowth)character;
        IGrowthAuthority auth = GrowthAuthority.Resolve(g);
        Assert.IsNotNull(auth, "普通 Character 应可解析出 IGrowthAuthority");
        Assert.IsFalse(auth.IsAvailable, "无 Profession 的普通 Character Growth 不可用");
        Assert.IsFalse(auth.IsProfessionGrowth);
        Assert.IsNull(ProfessionM4TestKit.Get(character, "ActiveProfessionProgress"));

        var curve = NewCurve(10, 15, 20);
        Assert.IsFalse(CharacterGrowthService.CanLevelUp(g, curve),
            "即使 Legacy Exp 充足也不可升级（不 fallback 旧 Level/Exp）");
        Assert.AreEqual(0, CharacterGrowthService.GetExpRequired(g, curve),
            "不可用 Growth 不返回升级需求");

        Assert.IsFalse(CharacterGrowthService.TryLevelUpOnce(g, curve, EmptyTable(), out var node),
            "TryLevelUpOnce 必须失败");
        Assert.IsNull(node);

        // Legacy 字段原样保留，且不得被任何 Growth 写改
        Assert.AreEqual(7, ProfessionM4TestKit.Get(character, "Level"));
        Assert.AreEqual(999, ProfessionM4TestKit.Get(character, "Exp"));

        var expandNode = NewNode(5, GrowthNodeType.UnlockedPoolExpand);
        expandNode.ExpandCardIds = new[] { "legacy_a", "legacy_d" };
        var r = CharacterGrowthService.ApplyFixedNode(g, expandNode, null);
        Assert.IsFalse(r.Applied, "无 Profession 不得应用扩池节点");
        var pool = (List<string>)ProfessionM4TestKit.Get(character, "UnlockedPoolCardIds");
        CollectionAssert.AreEqual(new[] { "legacy_a", "legacy_b", "legacy_c" }, pool,
            "Legacy 池不得被 Growth fallback 扩池");

        var traitNode = NewNode(4, GrowthNodeType.TraitUpgrade);
        Assert.IsFalse(CharacterGrowthService.ApplyFixedNode(g, traitNode, null).Applied,
            "无 Profession 不得应用 TraitUpgrade");

        var offer = CardGrowthResolver.BuildOffer(g, new List<BattleCardData> { NewCard("legacy_a") });
        Assert.IsEmpty(offer, "无 Profession 的 CardGrowth Offer 必须为空（不 fallback Legacy 池）");
    }

    [Test]
    public void TwoSameProfessionCharacters_GrowthFullyIndependent()
    {
        // Test B2 — 同 Profession 的 A/B：各自 ProfessionProgress，成长互不影响
        object personA = ProfessionM4TestKit.NewCharacter("char_001", "猎人甲", 30, 6);
        object personB = ProfessionM4TestKit.NewCharacter("char_001", "猎人乙", 30, 6);
        Assert.AreNotEqual(ProfessionM4TestKit.PersonIdOf(personA), ProfessionM4TestKit.PersonIdOf(personB));

        ProfessionProgress ppA = ProfessionM4TestKit.NewProgress("profession_test_hunter", 1, 10,
            new[] { "card_a", "card_b" }, 0);
        ProfessionProgress ppB = ProfessionM4TestKit.NewProgress("profession_test_hunter", 1, 3,
            new[] { "card_a", "card_b" }, 0);
        ProfessionM4TestKit.SetActiveProgress(personA, ppA);
        ProfessionM4TestKit.SetActiveProgress(personB, ppB);

        var gA = (IPersonGrowth)personA;
        var gB = (IPersonGrowth)personB;
        var curve = NewCurve(10, 15, 20);
        Assert.IsTrue(CharacterGrowthService.TryLevelUpOnce(gA, curve, EmptyTable(), out _), "A 可升级");

        // A 已升级
        Assert.AreEqual(2, ppA.ProfessionLevel);
        Assert.AreEqual(0, ppA.ProfessionExp);
        // B 完全不受影响
        Assert.AreEqual(1, ppB.ProfessionLevel, "B 的 ProfessionLevel 不受 A 升级影响");
        Assert.AreEqual(3, ppB.ProfessionExp, "B 的 ProfessionExp 不受 A 升级影响");
        Assert.AreEqual(1, ProfessionM4TestKit.Get(personB, "Level"), "B Legacy Level 不受影响");

        // A 扩池只影响 A
        var expandNode = NewNode(5, GrowthNodeType.UnlockedPoolExpand);
        expandNode.ExpandCardIds = new[] { "card_c" };
        CharacterGrowthService.ApplyFixedNode(gA, expandNode, null);
        Assert.IsTrue(ppA.UnlockedPoolCardIds.Contains("card_c"), "A 扩池应写入 A 的 Profession 池");
        Assert.IsFalse(ppB.UnlockedPoolCardIds.Contains("card_c"), "B 的 Profession 池不受 A 扩池影响");

        Assert.AreNotSame(ppA.UnlockedPoolCardIds, ppB.UnlockedPoolCardIds,
            "同 Profession 两人的 UnlockedPool 不得共享 List 引用");
    }

    [Test]
    public void PersonGrowth_DoesNotMutateProfessionDefinition()
    {
        // Test B3 — Person 成长（升级/扩池）不写回共享 ProfessionDefinition
        ProfessionDefinition def = ProfessionM4TestKit.NewHunterDefinition();
        object person = ProfessionM4TestKit.NewCharacter("char_001", "猎人", 30, 6);
        ProfessionProgress pp = ProfessionM4TestKit.NewProgress(def.ProfessionId, 1, 10,
            new List<string>(def.InitialUnlockedCards), 0);
        ProfessionM4TestKit.SetActiveProgress(person, pp);

        var g = (IPersonGrowth)person;
        CharacterGrowthService.TryLevelUpOnce(g, NewCurve(10, 15, 20), EmptyTable(), out _);
        var expandNode = NewNode(5, GrowthNodeType.UnlockedPoolExpand);
        expandNode.ExpandCardIds = new[] { "card_c", "card_a" };
        CharacterGrowthService.ApplyFixedNode(g, expandNode, null);

        CollectionAssert.AreEqual(new[] { "card_a", "card_b" }, def.InitialUnlockedCards,
            "Person 成长不得写回 ProfessionDefinition.InitialUnlockedCards");
        CollectionAssert.AreEqual(
            new[] { "card_a", "card_b", "card_c", "card_d" }, def.FullCardPool,
            "Person 成长不得修改 ProfessionDefinition.FullCardPool");
        Assert.AreNotSame(pp.UnlockedPoolCardIds, def.InitialUnlockedCards);
        Assert.AreNotSame(pp.UnlockedPoolCardIds, def.FullCardPool);
    }
}

