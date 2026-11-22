using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// M4-correction（Narrow Growth Authority Routing）— Trait 语义与 Hero 零迁移：
///   Test C1  普通 Character（有 Active Profession）的 TraitUpgrade 只对齐 ProfessionTraitLevel，
///            不再按 PersonId → TraitCatalog 默认 Trait 创建 Person-specific Trait（不建 TraitId/正式定义）；
///   Test C2  Hero 仍用 Legacy Growth（Level/Exp/UnlockedPool/Trait），无行为回归、无 Profession 结构。
/// </summary>
public class ProfessionGrowthTraitAndHeroTests
{
    private static object NewCharacter()
    {
        return ProfessionM4TestKit.NewCharacter("char_001", "战士", 30, 6);
    }

    private static GrowthTableConfig EmptyTable()
    {
        var t = ScriptableObject.CreateInstance<GrowthTableConfig>();
        t.TableId = "test";
        t.Nodes = new List<GrowthNodeConfig>();
        return t;
    }

    private static GrowthCurveConfig NewCurve()
    {
        var c = ScriptableObject.CreateInstance<GrowthCurveConfig>();
        c.MaxLevel = 10;
        c.ProficiencyCap = 5;
        c.ExpRequiredToLevel = new[] { 10, 15, 20, 25, 30 };
        return c;
    }

    private static GrowthNodeConfig TraitUpgradeNode()
    {
        var n = ScriptableObject.CreateInstance<GrowthNodeConfig>();
        n.Level = 4;
        n.NodeType = GrowthNodeType.TraitUpgrade;
        return n;
    }

    private static TraitCatalog CatalogWithPersonDefault(string personId, TraitData trait)
    {
        var catalog = ScriptableObject.CreateInstance<TraitCatalog>();
        catalog.DefaultTraits = new List<TraitCatalog.PersonTraitEntry>
        {
            new TraitCatalog.PersonTraitEntry { PersonId = personId, Trait = trait }
        };
        return catalog;
    }

    private static TraitData NewTrait(string traitId)
    {
        var t = ScriptableObject.CreateInstance<TraitData>();
        t.TraitId = traitId;
        t.DisplayName = traitId;
        t.MaxLevel = 3;
        return t;
    }

    [Test]
    public void TraitUpgrade_OnProfessionCharacter_AlignsProfessionTraitLevel_NoPersonTrait()
    {
        // Test C1 — TraitUpgrade 对新 Profession 语义只做数值对齐（ProfessionTraitLevel）：
        // 即使 TraitCatalog 存在该 PersonId 的默认 Trait、节点也带 Trait 引用，都不得创建 Person-specific Trait，
        // 也不得触碰 Character 的 Legacy TraitId/TraitLevel（IPersonGrowth 共享契约保持 Legacy）。
        object character = NewCharacter();
        string personId = ProfessionM4TestKit.PersonIdOf(character);
        // Legacy 长期字段预设一个旧值（M4 不得因 Profession Growth 改动/删除/覆盖）
        ProfessionM4TestKit.Set(character, "TraitId", "trait_legacy_x");
        ProfessionM4TestKit.Set(character, "TraitLevel", 5);

        ProfessionProgress pp = ProfessionM4TestKit.NewProgress("profession_test_hunter", 1, 0,
            new[] { "card_a" }, 0);
        ProfessionM4TestKit.SetActiveProgress(character, pp);
        // Legacy 兜底诱惑：目录里有该 PersonId 的默认 Trait
        TraitCatalog catalog = CatalogWithPersonDefault(personId, NewTrait("trait_" + personId));
        // 节点也可以带一个 Trait 引用（同样不得落到 Person TraitId）
        var node = TraitUpgradeNode();
        node.Trait = NewTrait("trait_from_node");

        var g = (IPersonGrowth)character;
        IGrowthAuthority auth = GrowthAuthority.Resolve(g);
        Assert.IsNull(auth.TraitId, "Growth 权威不暴露 Person TraitId（Profession Growth 无 Person Trait 语义）");
        Assert.AreEqual("trait_legacy_x", g.TraitId, "IPersonGrowth.TraitId 保持 Legacy（不被 Profession 全局路由）");
        Assert.AreEqual(5, g.TraitLevel, "IPersonGrowth.TraitLevel 保持 Legacy（不被 Profession 全局路由）");

        var r1 = CharacterGrowthService.ApplyFixedNode(g, node, catalog);
        Assert.IsTrue(r1.Applied, "有 Active Profession 的 Character TraitUpgrade 应可应用");
        Assert.AreEqual(1, pp.ProfessionTraitLevel, "职业特性强化数值应写入 ProfessionTraitLevel");
        Assert.AreEqual("trait_legacy_x", ProfessionM4TestKit.Get(character, "TraitId"),
            "不得把目录/节点 Trait 写入 Character Legacy TraitId");
        Assert.AreEqual(5, ProfessionM4TestKit.Get(character, "TraitLevel"),
            "Legacy TraitLevel 不得被新 Profession 语义改写");
        Assert.AreEqual(5, g.TraitLevel, "IPersonGrowth（Legacy）视角 TraitLevel 不受 Profession 强化影响");

        var r2 = CharacterGrowthService.ApplyFixedNode(g, TraitUpgradeNode(), catalog);
        Assert.IsTrue(r2.Applied);
        Assert.AreEqual(2, pp.ProfessionTraitLevel, "再次强化应继续 +1");
        Assert.AreEqual("trait_legacy_x", ProfessionM4TestKit.Get(character, "TraitId"),
            "强化全程不得创建/覆盖 Person Trait");
        Assert.AreEqual(5, ProfessionM4TestKit.Get(character, "TraitLevel"));
    }

    [Test]
    public void HeroGrowth_Unchanged_LegacyAuthority_NoProfessionStructure()
    {
        // Test C2 — Hero 仍使用旧 Growth（Legacy Level/Exp 权威），无 Profession 结构。
        object hero = ProfessionM4TestKit.NewHero("hero_001", "青鹿", 35);
        ProfessionM4TestKit.Set(hero, "Exp", 10);

        var g = (IPersonGrowth)hero;
        IGrowthAuthority auth = GrowthAuthority.Resolve(g);
        Assert.IsNotNull(auth, "Hero 应解析出 Legacy 权威");
        Assert.IsTrue(auth.IsAvailable, "Hero Growth 恒可用");
        Assert.IsFalse(auth.IsProfessionGrowth, "Hero 不是 Profession Growth");
        Assert.IsNull(hero.GetType().GetProperty("ActiveProfessionProgress"),
            "HeroInstance 不应新增 ActiveProfessionProgress（Hero 零迁移）");

        var curve = NewCurve();
        Assert.IsTrue(CharacterGrowthService.TryLevelUpOnce(g, curve, EmptyTable(), out _));
        Assert.AreEqual(2, ProfessionM4TestKit.Get(hero, "Level"), "Hero 升级仍写 Legacy Level");
        Assert.AreEqual(0, ProfessionM4TestKit.Get(hero, "Exp"), "Hero 升级消耗 Legacy Exp");

        // Hero Legacy Trait 路径保持原样：TraitUpgrade 仍强化 Legacy TraitId/TraitLevel
        object hero2 = ProfessionM4TestKit.NewHero("hero_001", "青鹿", 35);
        ProfessionM4TestKit.Set(hero2, "TraitId", "trait_hero_001");
        ProfessionM4TestKit.Set(hero2, "TraitLevel", 0);
        var traitNode = TraitUpgradeNode();
        traitNode.Trait = NewTrait("trait_hero_001");
        var result = CharacterGrowthService.ApplyFixedNode((IPersonGrowth)hero2, traitNode, null);
        Assert.IsTrue(result.Applied, "Hero 的 TraitUpgrade 应沿用 Legacy 语义");
        Assert.AreEqual(1, ProfessionM4TestKit.Get(hero2, "TraitLevel"), "Hero Trait 强化写 Legacy TraitLevel");
        Assert.AreEqual("trait_hero_001", ProfessionM4TestKit.Get(hero2, "TraitId"));
    }
}
