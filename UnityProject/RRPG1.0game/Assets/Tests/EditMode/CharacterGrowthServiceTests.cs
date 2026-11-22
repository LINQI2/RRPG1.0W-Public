using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// 人物长期成长服务纯逻辑测试。
/// 覆盖：经验满后不自动升级、主动逐级升级、多级逐级领取、等级不增加基础属性、
///      数据驱动等级→节点、固定型节点应用（StarterSlotUnlock / UnlockedPoolExpand / TraitUpgrade）。
/// </summary>
public class CharacterGrowthServiceTests
{
    private static TestPerson NewPerson(string personId = "char_001", string name = "战士")
    {
        return new TestPerson(personId, name);
    }

    private static GrowthCurveConfig NewCurve(params int[] expPerLevel)
    {
        var c = ScriptableObject.CreateInstance<GrowthCurveConfig>();
        c.MaxLevel = expPerLevel.Length + 1;
        c.ProficiencyCap = 5;
        c.ExpRequiredToLevel = expPerLevel;
        return c;
    }

    private static GrowthTableConfig NewTable(params GrowthNodeConfig[] nodes)
    {
        var t = ScriptableObject.CreateInstance<GrowthTableConfig>();
        t.TableId = "test";
        t.Nodes = new List<GrowthNodeConfig>(nodes);
        return t;
    }

    private static GrowthNodeConfig NewNode(int level, GrowthNodeType type)
    {
        var n = ScriptableObject.CreateInstance<GrowthNodeConfig>();
        n.Level = level;
        n.NodeType = type;
        return n;
    }

    [Test]
    public void ExpAdded_DoesNotAutoLevel()
    {
        var person = NewPerson();
        person.Exp = 50; // 足够升多级

        Assert.AreEqual(1, person.Level, "经验满后不得自动升级");
        Assert.IsTrue(CharacterGrowthService.CanLevelUp(person, NewCurve(10, 15, 20)), "经验足够应显示可升级");
    }

    [Test]
    public void CanLevelUp_RequiresEnoughExp_AndNotMaxLevel()
    {
        var person = NewPerson();
        person.Exp = 9;
        var curve = NewCurve(10, 15, 20);
        Assert.IsFalse(CharacterGrowthService.CanLevelUp(person, curve), "经验不足不可升级");

        person.Exp = 10;
        Assert.IsTrue(CharacterGrowthService.CanLevelUp(person, curve));

        person.Level = curve.MaxLevel;
        Assert.IsFalse(CharacterGrowthService.CanLevelUp(person, curve), "达最高等级不可升级");
    }

    [Test]
    public void LevelUpOnce_ConsumesExp_LevelsUp_ReturnsNodeForNewLevel()
    {
        var person = NewPerson();
        person.Exp = 30;
        var curve = NewCurve(10, 15, 20);
        var table = NewTable(NewNode(2, GrowthNodeType.CardGrowth));

        Assert.IsTrue(CharacterGrowthService.TryLevelUpOnce(person, curve, table, out var node));
        Assert.AreEqual(2, person.Level);
        Assert.AreEqual(20, person.Exp, "30 - 10 = 20");
        Assert.IsNotNull(node, "升到新等级应返回该级成长节点");
        Assert.AreEqual(2, node.Level);
        Assert.AreEqual(GrowthNodeType.CardGrowth, node.NodeType);
    }

    [Test]
    public void LevelUpOnce_NotEnoughExp_NoChange()
    {
        var person = NewPerson();
        person.Exp = 5;
        var curve = NewCurve(10, 15, 20);

        Assert.IsFalse(CharacterGrowthService.TryLevelUpOnce(person, curve, NewTable(), out _));
        Assert.AreEqual(1, person.Level);
        Assert.AreEqual(5, person.Exp, "不可升级时不得消耗经验");
    }

    [Test]
    public void MultiLevel_SequentialLevelUp_OneLevelPerClick()
    {
        var person = NewPerson();
        person.Exp = 40; // 10 + 15 = 25 → 可升 2 级；25 + 20 = 45 > 40 → 第 3 级不够
        var curve = NewCurve(10, 15, 20);

        Assert.IsTrue(CharacterGrowthService.TryLevelUpOnce(person, curve, NewTable(), out _));
        Assert.AreEqual(2, person.Level);
        Assert.AreEqual(30, person.Exp);

        Assert.IsTrue(CharacterGrowthService.TryLevelUpOnce(person, curve, NewTable(), out _));
        Assert.AreEqual(3, person.Level);
        Assert.AreEqual(15, person.Exp, "40-10-15=15");

        Assert.IsFalse(CharacterGrowthService.TryLevelUpOnce(person, curve, NewTable(), out _));
        Assert.AreEqual(3, person.Level, "经验不足时停留在当前等级");
    }

    [Test]
    public void NodeLookup_DataDriven_NoHardcodedLevels()
    {
        var table = NewTable(NewNode(2, GrowthNodeType.CardGrowth), NewNode(7, GrowthNodeType.StarterSlotUnlock));
        Assert.AreEqual(GrowthNodeType.CardGrowth, CharacterGrowthService.GetNodeForLevel(table, 2).NodeType);
        Assert.AreEqual(GrowthNodeType.StarterSlotUnlock, CharacterGrowthService.GetNodeForLevel(table, 7).NodeType);
        Assert.IsNull(CharacterGrowthService.GetNodeForLevel(table, 3), "未配置的等级应无节点");
    }

    [Test]
    public void LevelUp_TouchesOnlyLevelAndExp()
    {
        // 服务只应修改 Level / Exp；Starter / Unlocked Pool / Trait 由节点处理，等级本身不产生数值成长。
        var person = NewPerson();
        person.Exp = 30;
        var curve = NewCurve(10, 15, 20);
        var table = NewTable(NewNode(2, GrowthNodeType.CardGrowth));
        int slotsBefore = person.StarterSlots.Count;
        int poolBefore = person.UnlockedPoolCardIds.Count;

        CharacterGrowthService.TryLevelUpOnce(person, curve, table, out _);
        Assert.AreEqual(2, person.Level);
        Assert.AreEqual(20, person.Exp);
        Assert.AreEqual(slotsBefore, person.StarterSlots.Count, "等级本身不改变 Starter");
        Assert.AreEqual(poolBefore, person.UnlockedPoolCardIds.Count, "等级本身不改变 Unlocked Pool");
        Assert.AreEqual(0, person.TraitLevel, "等级本身不改变 Trait");
    }

    [Test]
    public void ApplyFixedNode_StarterSlotUnlock_AddsEmptySlot()
    {
        var person = NewPerson();
        GrowthDefaults.EnsureSeeded(person); // 初始 1 槽
        int before = person.StarterSlots.Count;

        var result = CharacterGrowthService.ApplyFixedNode(person, NewNode(3, GrowthNodeType.StarterSlotUnlock), null);
        Assert.IsTrue(result.Applied);
        Assert.AreEqual(before + 1, person.StarterSlots.Count, "StarterSlotUnlock 应新增 1 槽");
        Assert.IsTrue(person.StarterSlots[before].IsEmpty, "新槽应为空槽");
    }

    [Test]
    public void ApplyFixedNode_UnlockedPoolExpand_AddsDeduped()
    {
        var person = NewPerson();
        person.UnlockedPoolCardIds.AddRange(new[] { "bcard_aim", "bcard_sword" });
        var node = NewNode(5, GrowthNodeType.UnlockedPoolExpand);
        node.ExpandCardIds = new[] { "bcard_sword", "bcard_heal" };

        var result = CharacterGrowthService.ApplyFixedNode(person, node, null);
        Assert.IsTrue(result.Applied);
        Assert.IsTrue(person.UnlockedPoolCardIds.Contains("bcard_heal"), "新牌应加入 Unlocked Pool");
        Assert.AreEqual(3, person.UnlockedPoolCardIds.Count, "重复牌不得重复加入");
    }

    [Test]
    public void ApplyFixedNode_TraitUpgrade_StrengthensSameTrait_NotNew()
    {
        var person = NewPerson();
        person.TraitId = "trait_char_001";
        person.TraitLevel = 0;
        var trait = ScriptableObject.CreateInstance<TraitData>();
        trait.TraitId = "trait_char_001";
        trait.MaxLevel = 3;
        var node = NewNode(4, GrowthNodeType.TraitUpgrade);
        node.Trait = trait;

        var r1 = CharacterGrowthService.ApplyFixedNode(person, node, null);
        Assert.IsTrue(r1.Applied);
        Assert.AreEqual(1, person.TraitLevel);

        var r2 = CharacterGrowthService.ApplyFixedNode(person, node, null);
        Assert.IsTrue(r2.Applied);
        Assert.AreEqual(2, person.TraitLevel);
        Assert.AreEqual("trait_char_001", person.TraitId, "TraitUpgrade 强化原 Trait，不得替换为其他 Trait");
    }

    [Test]
    public void ApplyFixedNode_TraitUpgrade_UsesCatalogDefault_WhenNoTraitAssigned()
    {
        var person = NewPerson();
        var trait = ScriptableObject.CreateInstance<TraitData>();
        trait.TraitId = "trait_char_001";
        trait.MaxLevel = 3;
        var catalog = ScriptableObject.CreateInstance<TraitCatalog>();
        catalog.DefaultTraits = new List<TraitCatalog.PersonTraitEntry>
        {
            new TraitCatalog.PersonTraitEntry { PersonId = "char_001", Trait = trait }
        };

        var node = NewNode(4, GrowthNodeType.TraitUpgrade);
        var result = CharacterGrowthService.ApplyFixedNode(person, node, catalog);
        Assert.IsTrue(result.Applied);
        Assert.AreEqual("trait_char_001", person.TraitId, "未分配 Trait 时从目录取默认 Trait");
        Assert.AreEqual(1, person.TraitLevel);
    }
}

