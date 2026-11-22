using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// M1 §15.6 / §15.7：Run 内人物 HP 与 Run 牌归属隔离（同 CharacterId 双实例互不塌缩）。
/// </summary>
public class RunPersonIsolationTests
{
    [Test]
    public void TwoSameTemplateCharacters_KeepIndependentHp_InRunState()
    {
        object p1 = M1TestKit.NewCharacter("char_001", "战士甲", 30);
        object p2 = M1TestKit.NewCharacter("char_001", "战士乙", 30);
        string pid1 = M1TestKit.PersonIdOf(p1);
        string pid2 = M1TestKit.PersonIdOf(p2);
        Assert.AreNotEqual(pid1, pid2);

        var run = new RunState();
        run.GetOrCreatePerson(pid1, (string)M1TestKit.Get(p1, "DisplayName"), (int)M1TestKit.Get(p1, "MaxHP"));
        run.GetOrCreatePerson(pid2, (string)M1TestKit.Get(p2, "DisplayName"), (int)M1TestKit.Get(p2, "MaxHP"));

        run.GetPerson(pid1).CurrentHP = 8;
        run.GetPerson(pid2).CurrentHP = 3;

        Assert.AreEqual(8, run.GetPerson(pid1).CurrentHP);
        Assert.AreEqual(3, run.GetPerson(pid2).CurrentHP);
        Assert.AreNotEqual(run.GetPerson(pid1).CurrentHP, run.GetPerson(pid2).CurrentHP,
            "同模板双实例的 Run HP 必须独立");

        // 修改 P1 不影响 P2
        run.GetPerson(pid1).CurrentHP = 1;
        Assert.AreEqual(1, run.GetPerson(pid1).CurrentHP);
        Assert.AreEqual(3, run.GetPerson(pid2).CurrentHP, "修改 P1 不得影响 P2");
    }
}

/// <summary>
/// M1 §15.7：同模板两人物各自拥有同一 BattleCard Template 时，Run Card 归属通过 PersonId 隔离；
/// 失能/过滤只移除本人牌。
/// </summary>
public class RunCardOwnershipIsolationTests
{
    private static BattleCardData NewTemplate(string cardId)
    {
        var t = ScriptableObject.CreateInstance<BattleCardData>();
        t.CardId = cardId;
        t.DisplayName = cardId;
        t.CardKind = BattleCardKind.Character;
        return t;
    }

    [Test]
    public void SameTemplateOwnedByTwoPersons_IsDistinguishedByPersonId()
    {
        object p1 = M1TestKit.NewCharacter("char_001", "战士甲", 30);
        object p2 = M1TestKit.NewCharacter("char_001", "战士乙", 30);
        string pid1 = M1TestKit.PersonIdOf(p1);
        string pid2 = M1TestKit.PersonIdOf(p2);
        var template = NewTemplate("bcard_same");

        var run = new RunState();
        run.AddRunCard(pid1, template);
        run.AddRunCard(pid2, template);

        Assert.AreEqual(2, run.RunDeckEntries.Count, "同模板两人物各自拥有同一模板 = 两条独立条目");
        Assert.IsTrue(run.RunDeckEntries.Any(e => e.PersonId == pid1 && e.Template.CardId == "bcard_same"));
        Assert.IsTrue(run.RunDeckEntries.Any(e => e.PersonId == pid2 && e.Template.CardId == "bcard_same"));

        // 只部署 P1 时只出现 P1 的牌
        var onlyP1 = SharedDeck.FilterValidCards(run.GetRunDeckEntries(), new[] { pid1 });
        Assert.AreEqual(1, onlyP1.Count);
        Assert.AreEqual(pid1, onlyP1[0].PersonId);
        var onlyP2 = SharedDeck.FilterValidCards(run.GetRunDeckEntries(), new[] { pid2 });
        Assert.AreEqual(1, onlyP2.Count);
        Assert.AreEqual(pid2, onlyP2[0].PersonId);
    }

    [Test]
    public void RemovingDownedPersonsCards_DoesNotRemoveOtherPersonsCards()
    {
        object p1 = M1TestKit.NewCharacter("char_001", "战士甲", 30);
        object p2 = M1TestKit.NewCharacter("char_001", "战士乙", 30);
        string pid1 = M1TestKit.PersonIdOf(p1);
        string pid2 = M1TestKit.PersonIdOf(p2);
        var template = NewTemplate("bcard_same");

        var run = new RunState();
        run.AddRunCard(pid1, template);
        run.AddRunCard(pid2, template);

        var deck = new SharedDeck();
        deck.BuildDeck(run.GetRunDeckEntries());
        deck.Reshuffle();

        int removed = deck.RemovePersonCards(pid1);
        Assert.Greater(removed, 0, "P1 失能应移除 P1 的牌");

        var allCards = new List<BattleCard>();
        allCards.AddRange(deck.DrawPile);
        allCards.AddRange(deck.Hand);
        allCards.AddRange(deck.DiscardPile);
        allCards.AddRange(deck.ExhaustPile);
        Assert.IsFalse(allCards.Exists(c => c.OwnerPersonId == pid1), "P1 的牌应从全部牌区移除");
        Assert.IsTrue(allCards.Exists(c => c.OwnerPersonId == pid2), "P1 失能不得移除 P2 的牌");
    }
}
