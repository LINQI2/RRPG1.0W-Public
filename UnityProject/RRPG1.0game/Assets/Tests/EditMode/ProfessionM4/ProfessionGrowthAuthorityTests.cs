using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// M4-correction（Narrow Growth Authority Routing）专项 — 普通 Character（有 Active Profession）的
/// Growth 权威经 GrowthAuthority.Resolve → IGrowthAuthority 解析：
///   Test A1  升级只改 ProfessionLevel / ProfessionExp，不改 Legacy Level/Exp；
///   Test A2  UnlockedPoolExpand 只改 ProfessionProgress.UnlockedPoolCardIds；
///   Test A3  CardGrowth Offer 只读 ProfessionProgress.UnlockedPoolCardIds（不读 Legacy 池）；
///   Test A4  Starter 成长节点仍修改 Character 自己的 StarterSlots（Starter 保持 Character-owned）；
///   Test A5  IPersonGrowth 保持 Legacy 契约（不全局路由）；权威 Trait 数值 = ProfessionTraitLevel。
/// 真实宿主 CharacterInstance（Assembly-CSharp）经反射构造，Growth.Runtime 纯逻辑强类型调用。
/// </summary>
public class ProfessionGrowthAuthorityTests
{
    private static object NewCharacter()
    {
        return ProfessionM4TestKit.NewCharacter("char_001", "战士", 30, 6);
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

    private static BattleCardData NewCard(string cardId, BattleCardKind kind = BattleCardKind.Character)
    {
        var c = ScriptableObject.CreateInstance<BattleCardData>();
        c.CardId = cardId;
        c.DisplayName = cardId;
        c.CardKind = kind;
        return c;
    }

    [Test]
    public void CharacterWithActiveProfession_LevelUp_WritesProfessionLevelAndExp_NotLegacy()
    {
        // Test A1 — Growth 权威 = ActiveProfessionProgress
        object character = NewCharacter();
        ProfessionProgress pp = ProfessionM4TestKit.NewProgress("profession_test_hunter", 1, 10,
            new[] { "card_a", "card_b" }, 0);
        ProfessionM4TestKit.SetActiveProgress(character, pp);

        var g = (IPersonGrowth)character;
        IGrowthAuthority auth = GrowthAuthority.Resolve(g);
        Assert.IsNotNull(auth, "真实普通 Character 应可解析出 IGrowthAuthority");
        Assert.IsTrue(auth.IsAvailable, "有 Active Profession 的普通 Character Growth 应可用");
        Assert.IsTrue(auth.IsProfessionGrowth, "应为 Profession Growth");
        Assert.AreEqual(1, auth.Level, "权威 Level = ProfessionLevel");
        Assert.AreEqual(10, auth.Exp, "权威 Exp = ProfessionExp");
        // IPersonGrowth 共享长期契约保持 Legacy（不因 ActiveProfessionProgress 全局路由宿主）
        Assert.AreEqual(1, g.Level, "IPersonGrowth.Level 保持 Legacy（不被 Profession 路由）");
        Assert.AreEqual(0, g.Exp, "IPersonGrowth.Exp 保持 Legacy（不被 Profession 路由）");

        var curve = NewCurve(10, 15, 20);
        Assert.IsTrue(CharacterGrowthService.TryLevelUpOnce(g, curve, NewTable(), out _), "经验足够应可升级");
        Assert.AreEqual(2, pp.ProfessionLevel, "升级应写入 ProfessionLevel");
        Assert.AreEqual(0, pp.ProfessionExp, "升级应消耗 ProfessionExp");
        Assert.AreEqual(1, ProfessionM4TestKit.Get(character, "Level"),
            "M4-correction：普通 Character 升级不得改写 Legacy Level");
        Assert.AreEqual(0, ProfessionM4TestKit.Get(character, "Exp"),
            "M4-correction：普通 Character 升级不得改写 Legacy Exp");
    }

    [Test]
    public void CharacterUnlockedPoolExpand_OnlyChangesProfessionProgress()
    {
        // Test A2 — 扩池节点经路由只改 ProfessionProgress.UnlockedPoolCardIds
        object character = NewCharacter();
        ProfessionProgress pp = ProfessionM4TestKit.NewProgress("profession_test_hunter", 1, 0,
            new[] { "card_a", "card_b" }, 0);
        ProfessionM4TestKit.SetActiveProgress(character, pp);
        // Legacy 池独立保留（Run/Save 等一般长期语义沿用），但不得被 Growth 扩池改写
        ProfessionM4TestKit.Set(character, "UnlockedPoolCardIds", new List<string> { "legacy_x" });

        var node = NewNode(5, GrowthNodeType.UnlockedPoolExpand);
        node.ExpandCardIds = new[] { "card_b", "card_c", "card_a" };

        var result = CharacterGrowthService.ApplyFixedNode((IPersonGrowth)character, node, null);
        Assert.IsTrue(result.Applied, "扩池节点应可应用");

        CollectionAssert.AreEquivalent(new[] { "card_a", "card_b", "card_c" }, pp.UnlockedPoolCardIds,
            "扩池应写入 ProfessionProgress.UnlockedPoolCardIds（去重）");
        var legacyPool = (List<string>)ProfessionM4TestKit.Get(character, "UnlockedPoolCardIds");
        CollectionAssert.AreEqual(new[] { "legacy_x" }, legacyPool,
            "M4：扩池不得改写 Legacy UnlockedPool");
    }

    [Test]
    public void CardGrowthOffer_ReadsOnlyProfessionUnlockedPool_NotLegacyPool()
    {
        // Test A3 — CardGrowth Offer 只读 Profession UnlockedPool
        object character = NewCharacter();
        ProfessionProgress pp = ProfessionM4TestKit.NewProgress("profession_test_hunter", 1, 0,
            new[] { "prof_a", "prof_b" }, 0);
        ProfessionM4TestKit.SetActiveProgress(character, pp);
        // Legacy 池内容与 Profession 池不同：Offer 不得混入 Legacy 池
        ProfessionM4TestKit.Set(character, "UnlockedPoolCardIds",
            new List<string> { "legacy_x", "legacy_y", "prof_a" });

        var templates = new List<BattleCardData>
        {
            NewCard("prof_a"), NewCard("prof_b"), NewCard("legacy_x"), NewCard("legacy_y")
        };

        var offer = CardGrowthResolver.BuildOffer((IPersonGrowth)character, templates);
        var offeredIds = offer.Select(c => c.CardId).ToList();
        Assert.AreEqual(2, offer.Count, "Offer 应只来自 Profession 池的两张牌");
        CollectionAssert.AreEquivalent(new[] { "prof_a", "prof_b" }, offeredIds,
            "CardGrowth Offer 只读 ProfessionProgress.UnlockedPoolCardIds");
        Assert.IsFalse(offeredIds.Contains("legacy_x"), "Legacy 池不得进入 CardGrowth Offer");
        Assert.IsFalse(offeredIds.Contains("legacy_y"), "Legacy 池不得进入 CardGrowth Offer");
    }


    [Test]
    public void StarterSlotUnlock_StillModifiesCharacterStarter_ForProfessionCharacter()
    {
        // Test A4 — Starter 保持 Character-owned：Starter 成长节点修改 Character 自己的 StarterSlots
        object character = NewCharacter();
        ProfessionProgress pp = ProfessionM4TestKit.NewProgress("profession_test_hunter", 1, 10,
            new[] { "card_a", "card_b" }, 1);
        ProfessionM4TestKit.SetActiveProgress(character, pp);

        var g = (IPersonGrowth)character;
        Assert.AreEqual(0, g.StarterSlots.Count, "起始无 Starter 槽");

        var result = CharacterGrowthService.ApplyFixedNode(g,
            NewNode(3, GrowthNodeType.StarterSlotUnlock), null);
        Assert.IsTrue(result.Applied, "StarterSlotUnlock 节点应可应用");
        Assert.AreEqual(1, g.StarterSlots.Count, "Starter 槽应加在 Character 的 StarterSlots");
        Assert.IsTrue(g.StarterSlots[0].IsEmpty, "新槽应为空槽");
        Assert.AreEqual(1, pp.ProfessionLevel, "Starter 节点不改 ProfessionLevel");
        Assert.AreEqual(10, pp.ProfessionExp, "Starter 节点不改 ProfessionExp");
        CollectionAssert.AreEqual(new[] { "card_a", "card_b" }, pp.UnlockedPoolCardIds,
            "Starter 节点不改 Profession 池");
    }

    [Test]
    public void CharacterWithActiveProfession_IpGrowthStaysLegacy_AuthorityTraitLevelRoutedToProfession()
    {
        // A5 — 窄路由守卫：
        //   IPersonGrowth 共享契约保持 Legacy（不全局路由到 ProfessionProgress）；
        //   Growth Authority 的 TraitId 恒 null（不创建 Person-specific Trait），TraitLevel 落 ProfessionTraitLevel。
        object character = NewCharacter();
        ProfessionProgress pp = ProfessionM4TestKit.NewProgress("profession_test_hunter", 1, 0,
            new[] { "card_a" }, 2);
        ProfessionM4TestKit.SetActiveProgress(character, pp);

        var g = (IPersonGrowth)character;
        IGrowthAuthority auth = GrowthAuthority.Resolve(g);

        Assert.AreEqual(0, g.TraitLevel, "IPersonGrowth.TraitLevel 保持 Legacy（不因 Profession 路由）");
        Assert.IsNull(g.TraitId, "未赋值时 IPersonGrowth.TraitId 为 null（Legacy 字段默认）");
        Assert.IsNull(auth.TraitId, "Growth 权威不暴露 Person TraitId（Profession Growth 不创建 Person-specific Trait）");
        Assert.AreEqual(2, auth.TraitLevel, "权威 Trait 数值状态 = ProfessionTraitLevel");

        auth.TraitLevel = 3;
        Assert.AreEqual(3, pp.ProfessionTraitLevel, "TraitLevel 写应路由到 ProfessionTraitLevel");
        Assert.AreEqual(0, ProfessionM4TestKit.Get(character, "TraitLevel"),
            "Legacy TraitLevel 不得被 Profession Trait 写入");
    }
}
