using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// M6 专项 — Formation Host（CharacterInstance）与现有 Growth 权威自然读取：
/// Hero 不拥有 FormationState / LifeHistory / IFormationHost；Person×Profession 状态独立；
/// 形成后 GrowthAuthority / StarterDeckBuilder 自然读取现有权威，不建第二套接线。
/// </summary>
public class ProfessionFormationHostTests
{
    [Test]
    public void Character_HasFormationHost_AndState_AndLifeHistory()
    {
        object character = ProfessionM6TestKit.NewCharacter("char_001", "农夫甲", 30, 6);
        var host = ProfessionM6TestKit.AsHost(character);
        Assert.IsFalse(string.IsNullOrEmpty(host.PersonId), "PersonId 必须存在");
        Assert.IsNotNull(host.FormationState, "普通 Character 恒持有 FormationState（空状态合法）");
        Assert.IsNotNull(host.LifeHistory, "普通 Character 恒持有 Life History（空记录合法）");
        Assert.IsFalse(host.HasActiveProfession, "新人物默认未形成 Profession");
        Assert.AreEqual(0, host.FormationState.Entries.Count);
        Assert.AreEqual(0, host.LifeHistory.Events.Count);
    }

    [Test]
    public void Hero_DoesNotImplementFormationHost_NoFormationState()
    {
        object hero = ProfessionM6TestKit.NewHero("hero_001", "青鹿", 35);
        var t = hero.GetType();
        Assert.IsFalse(ProfessionM6TestKit.ImplementsIFormationHost(t),
            "Hero 不得实现 IFormationHost（Hero Guard：Formation 只作用于普通 Character）");
        Assert.IsNull(t.GetProperty("FormationState"), "Hero 不得拥有 FormationState");
        Assert.IsNull(t.GetProperty("LifeHistory"), "Hero 不得拥有 Life History");
    }

    [Test]
    public void FormationState_IsPersonIndependent_SameTemplateTwoPersonsDoNotShare()
    {
        object a = ProfessionM6TestKit.NewCharacter("char_001", "人物甲", 30, 6);
        object b = ProfessionM6TestKit.NewCharacter("char_001", "人物乙", 30, 6);
        var hostA = ProfessionM6TestKit.AsHost(a);
        var hostB = ProfessionM6TestKit.AsHost(b);
        Assert.AreNotEqual(hostA.PersonId, hostB.PersonId, "同模板不同实例必须不同 PersonId");

        hostA.FormationState.GetOrAddEntry("profession_test_a").Strength = 55f;
        Assert.AreEqual(1, hostA.FormationState.Entries.Count);
        Assert.AreEqual(0, hostB.FormationState.Entries.Count,
            "Person A 的 Formation 状态不得影响同模板 Person B（Person×Profession 独立）");
        Assert.AreNotSame(hostA.FormationState, hostB.FormationState);
    }

    [Test]
    public void PostFormation_GrowthAuthorityReadsExistingAuthorityNaturally()
    {
        object character = ProfessionM6TestKit.NewCharacter("char_001", "农夫甲", 30, 6);
        var host = ProfessionM6TestKit.AsHost(character);
        var registry = ProfessionFormationCommitTests.FarmerRegistry();

        var entry = host.FormationState.GetOrAddEntry(ProfessionFormationCommitTests.Farmer);
        entry.Strength = 70f;
        entry.IsCandidate = true;
        entry.Stability = 8f;
        host.FormationState.FormationWindowOpen = true;
        host.FormationState.FormationTargetProfessionId = ProfessionFormationCommitTests.Farmer;
        var result = FormationCommitter.Commit(host, ProfessionFormationCommitTests.Farmer,
            new FormationDayContext(1, 1, 1), ProfessionFormationCommitTests.Fast(), registry);
        Assert.IsTrue(result.IsSuccess);

        // GrowthAuthority 窄解析自然读取现有权威（不建第二套 Growth 接线）。
        var auth = ProfessionM6TestKit.ResolveAuth(character);
        Assert.IsTrue(auth.IsAvailable, "形成后 Growth 可用");
        Assert.IsTrue(auth.IsProfessionGrowth, "形成后为 Profession Growth");
        Assert.AreEqual(1, auth.Level, "权威 Level = ProfessionProgress.ProfessionLevel（1）");
        Assert.AreEqual(0, auth.Exp);
        Assert.AreEqual(1, auth.TraitLevel, "权威 Trait 数值状态 = ProfessionTraitLevel（1）");
        Assert.AreEqual(2, auth.UnlockedPoolCardIds.Count, "权威池 = ProfessionProgress clone 模板 InitialUnlockedCards");
        Assert.AreEqual(2, auth.StarterSlots.Count, "权威 Starter = 原子替换后的模板 InitialStarterBuild");
        var cardIds = new List<string>();
        foreach (var s in auth.StarterSlots) cardIds.Add(s.CardId);
        CollectionAssert.AreEqual(new[] { "card_f1", "card_f3" }, cardIds);
    }

    [Test]
    public void PostFormation_StarterDeckBuilderReadsReplacedStarter()
    {
        object character = ProfessionM6TestKit.NewCharacter("char_001", "农夫甲", 30, 6);
        var host = ProfessionM6TestKit.AsHost(character);
        var registry = ProfessionFormationCommitTests.FarmerRegistry();
        var entry = host.FormationState.GetOrAddEntry(ProfessionFormationCommitTests.Farmer);
        entry.Strength = 70f;
        entry.IsCandidate = true;
        entry.Stability = 8f;
        host.FormationState.FormationWindowOpen = true;
        host.FormationState.FormationTargetProfessionId = ProfessionFormationCommitTests.Farmer;
        Assert.IsTrue(FormationCommitter.Commit(host, ProfessionFormationCommitTests.Farmer,
            new FormationDayContext(1, 1, 1), ProfessionFormationCommitTests.Fast(), registry).IsSuccess);

        // StarterDeckBuilder（既有 Run 播种权威）自然读取人物长期 Starter 槽。
        var templates = new List<BattleCardData>
        {
            NewCard("card_f1"), NewCard("card_f3"), NewCard("card_unused")
        };
        var entries = StarterDeckBuilder.BuildStarterEntries(
            new List<IPersonGrowth> { ProfessionM6TestKit.AsGrowth(character) }, templates);
        var deployed = entries.FindAll(e => e.PersonId == host.PersonId);
        Assert.AreEqual(2, deployed.Count, "Run Deck 应从替换后的 Starter 槽生成（每槽一张副本）");
        var ids = new List<string>();
        foreach (var e in deployed) ids.Add(e.Template.CardId);
        CollectionAssert.AreEquivalent(new[] { "card_f1", "card_f3" }, ids,
            "Run 播种自然读取 Formation 后原子替换的 Starter");
    }

    private static BattleCardData NewCard(string cardId)
    {
        var c = ScriptableObject.CreateInstance<BattleCardData>();
        c.CardId = cardId;
        c.DisplayName = cardId;
        c.CardKind = BattleCardKind.Character;
        return c;
    }
}
