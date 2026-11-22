using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// SharedDeck（共享牌库）EditMode 测试
///
/// 覆盖 Battle GameSpec V0.1 Stage 1 要求：
///   本场有效牌过滤 / 共享Deck / Draw / Hand / Discard / Reshuffle / Exhaust / Retain。
/// </summary>
public class SharedDeckTests
{
    private BattleCardData MakeTemplate(string id, BattleCardKind kind, int cost = 1,
                                        bool retain = false, bool exhaust = false)
    {
        var data = ScriptableObject.CreateInstance<BattleCardData>();
        data.CardId = id;
        data.DisplayName = id;
        data.CardKind = kind;
        data.Cost = cost;
        data.Retain = retain;
        data.IsExhaust = exhaust;
        return data;
    }

    private BattlePersonCardEntry MakeEntry(string templateId, BattleCardKind kind, string personId,
                                            int copies = 1, bool retain = false, bool exhaust = false)
    {
        var template = MakeTemplate(templateId, kind, 1, retain, exhaust);
        return new BattlePersonCardEntry(template, personId, copies);
    }

    // ---------- 本场有效牌过滤 ----------

    [Test]
    public void FilterValidCards_CharacterOfDeployed_IsIncluded()
    {
        var entry = MakeEntry("c_sword", BattleCardKind.Character, "person_001");

        var result = SharedDeck.FilterValidCards(new[] { entry }, new[] { "person_001" });

        Assert.AreEqual(1, result.Count);
        Assert.AreSame(entry, result[0]);
    }

    [Test]
    public void FilterValidCards_CharacterNotDeployed_IsExcluded()
    {
        var entry = MakeEntry("c_sword", BattleCardKind.Character, "person_001");

        var result = SharedDeck.FilterValidCards(new[] { entry }, new[] { "person_002" });

        Assert.IsEmpty(result);
    }

    [Test]
    public void FilterValidCards_Common_AlwaysIncluded()
    {
        var entry = MakeEntry("c_rally", BattleCardKind.Common, null);

        var result = SharedDeck.FilterValidCards(new[] { entry }, new string[0]);

        Assert.AreEqual(1, result.Count);
    }

    [Test]
    public void FilterValidCards_Mixed_OnlyDeployedCharacterAndCommonKept()
    {
        var deployed = MakeEntry("c_sword", BattleCardKind.Character, "person_001");
        var notDeployed = MakeEntry("c_heal", BattleCardKind.Character, "person_003");
        var common = MakeEntry("c_rally", BattleCardKind.Common, null);

        var result = SharedDeck.FilterValidCards(
            new[] { deployed, notDeployed, common },
            new[] { "person_001" });

        Assert.AreEqual(2, result.Count);
        Assert.Contains(deployed, result);
        Assert.Contains(common, result);
        CollectionAssert.DoesNotContain(result, notDeployed);
    }

    /// <summary>
    /// 规格修正测试：两个共享同一角色模板但人物实例不同，
    /// 人物A的牌不得因为人物B上阵而进入牌库。
    /// </summary>
    [Test]
    public void SameTemplateDifferentPersons_OnlyDeployedPersonsCardEntersDeck()
    {
        var template = MakeTemplate("c_sword", BattleCardKind.Character);

        var personA = new BattlePersonCardEntry(template, "person_A", 1);
        var personB = new BattlePersonCardEntry(template, "person_B", 1);

        // 只有 person_A 上阵
        var valid = SharedDeck.FilterValidCards(new[] { personA, personB }, new[] { "person_A" });
        Assert.AreEqual(1, valid.Count);
        Assert.AreEqual("person_A", valid[0].PersonId);

        // 构筑后牌库只含 person_A 的牌，person_B 的牌不得进入
        var deck = new SharedDeck();
        deck.BuildDeck(valid);
        deck.Reshuffle();

        Assert.AreEqual(1, deck.DrawPileCount);
        Assert.AreEqual("person_A", deck.DrawPile[0].OwnerPersonId);
        Assert.AreEqual("c_sword", deck.DrawPile[0].TemplateId);

        // 反向：只有 person_B 上阵时，进入牌库的必须是 person_B 的牌
        var validB = SharedDeck.FilterValidCards(new[] { personA, personB }, new[] { "person_B" });
        var deckB = new SharedDeck();
        deckB.BuildDeck(validB);
        Assert.AreEqual(1, deckB.DrawPileCount);
        Assert.AreEqual("person_B", deckB.DrawPile[0].OwnerPersonId);
    }

    // ---------- 构筑 ----------

    [Test]
    public void BuildDeck_InstantiatesCopies_IntoDrawPile()
    {
        var deck = new SharedDeck();
        var entry = MakeEntry("c_sword", BattleCardKind.Character, "person_001", copies: 3);

        deck.BuildDeck(new[] { entry });

        Assert.AreEqual(3, deck.DrawPileCount);
        Assert.AreEqual(0, deck.HandCount);
        Assert.AreEqual(0, deck.DiscardPileCount);
        Assert.AreEqual(0, deck.ExhaustPileCount);
        Assert.AreEqual("c_sword_person_001_0", deck.DrawPile[0].CardId);
        Assert.AreEqual("c_sword_person_001_2", deck.DrawPile[2].CardId);
        Assert.AreEqual("person_001", deck.DrawPile[0].OwnerPersonId);
    }

    // ---------- Draw ----------

    [Test]
    public void Draw_MovesCardsToHand()
    {
        var deck = new SharedDeck();
        var entry = MakeEntry("c_sword", BattleCardKind.Character, "person_001", copies: 3);
        deck.BuildDeck(new[] { entry });

        var drawn = deck.Draw(2);

        Assert.AreEqual(2, drawn.Count);
        Assert.AreEqual(2, deck.HandCount);
        Assert.AreEqual(1, deck.DrawPileCount);
    }

    [Test]
    public void Draw_WhenDrawPileEmpty_ReshufflesDiscardBack()
    {
        var deck = new SharedDeck();
        var entry = MakeEntry("c_sword", BattleCardKind.Character, "person_001", copies: 2);
        deck.BuildDeck(new[] { entry });
        deck.Reshuffle();

        // 抽空抽牌堆 → 全部弃牌
        var first = deck.Draw(2);
        Assert.AreEqual(2, deck.HandCount);

        deck.Discard(first[0]);
        deck.Discard(first[1]);
        Assert.AreEqual(0, deck.DrawPileCount);
        Assert.AreEqual(2, deck.DiscardPileCount);

        // 再次抽牌应触发弃牌堆洗回
        var redrawn = deck.Draw(2);

        Assert.AreEqual(2, redrawn.Count);
        Assert.AreEqual(0, deck.DrawPileCount);
        Assert.AreEqual(0, deck.DiscardPileCount);
        Assert.AreEqual(2, deck.HandCount);
    }

    /// <summary>
    /// 规格修正测试：抽牌请求执行到一半 DrawPile 耗尽时，
    /// 应立即将 Discard 洗回并继续完成本次抽牌。
    /// </summary>
    [Test]
    public void Draw_WhenDrawPileExhaustsMidDraw_ReshufflesDiscardAndContinues()
    {
        var deck = new SharedDeck();
        var entry = MakeEntry("c_sword", BattleCardKind.Character, "person_001", copies: 3);
        deck.BuildDeck(new[] { entry });

        // 先抽 1 张并弃掉，制造 Discard=1, DrawPile=2
        var first = deck.Draw(1)[0];
        deck.Discard(first);
        Assert.AreEqual(2, deck.DrawPileCount);
        Assert.AreEqual(1, deck.DiscardPileCount);

        // 请求抽 3 张：第 1、2 张来自 DrawPile（耗尽）→ 第 3 张应洗回 Discard 并继续完成
        var drawn = deck.Draw(3);

        Assert.AreEqual(3, drawn.Count, "中途 DrawPile 耗尽后必须洗回 Discard 并完成全部请求");
        Assert.AreEqual(3, deck.HandCount);
        Assert.AreEqual(0, deck.DrawPileCount);
        Assert.AreEqual(0, deck.DiscardPileCount);
    }

    [Test]
    public void Draw_WhenBothPilesEmpty_ReturnsEmpty()
    {
        var deck = new SharedDeck();

        var drawn = deck.Draw(3);

        Assert.IsEmpty(drawn);
        Assert.AreEqual(0, deck.HandCount);
    }

    // ---------- Discard / Exhaust / Retain ----------

    [Test]
    public void Discard_MovesHandToDiscard()
    {
        var deck = new SharedDeck();
        var entry = MakeEntry("c_sword", BattleCardKind.Character, "person_001");
        deck.BuildDeck(new[] { entry });
        var drawn = deck.Draw(1)[0];

        Assert.IsTrue(deck.Discard(drawn));
        Assert.AreEqual(1, deck.DiscardPileCount);
        Assert.AreEqual(0, deck.HandCount);

        // 不在手牌的牌不能弃
        Assert.IsFalse(deck.Discard(drawn));
    }

    [Test]
    public void Exhaust_MovesHandToExhaust()
    {
        var deck = new SharedDeck();
        var entry = MakeEntry("c_bomb", BattleCardKind.Common, null, exhaust: true);
        deck.BuildDeck(new[] { entry });
        var drawn = deck.Draw(1)[0];

        Assert.IsTrue(deck.Exhaust(drawn));
        Assert.AreEqual(1, deck.ExhaustPileCount);
        Assert.AreEqual(0, deck.HandCount);
    }

    [Test]
    public void Retain_MarksCardAndKeepsInHand()
    {
        var deck = new SharedDeck();
        var entry = MakeEntry("c_retain", BattleCardKind.Character, "person_001", retain: true);
        deck.BuildDeck(new[] { entry });
        var drawn = deck.Draw(1)[0];

        Assert.IsTrue(deck.Retain(drawn));
        Assert.IsTrue(drawn.Retain);
        Assert.AreEqual(1, deck.HandCount);
    }

    [Test]
    public void GetNonRetainedHand_ExcludesRetainedCards()
    {
        var deck = new SharedDeck();
        var retainEntry = MakeEntry("c_retain", BattleCardKind.Character, "person_001", retain: true);
        var normalEntry = MakeEntry("c_normal", BattleCardKind.Character, "person_001");
        deck.BuildDeck(new[] { retainEntry, normalEntry });
        deck.Draw(2);

        var nonRetained = deck.GetNonRetainedHand();

        Assert.AreEqual(1, nonRetained.Count);
        Assert.AreEqual("c_normal", nonRetained[0].TemplateId);
    }

    // ---------- 打出牌去向 ----------

    [Test]
    public void ResolvePlayedCard_ExhaustCard_GoesToExhaust()
    {
        var deck = new SharedDeck();
        var entry = MakeEntry("c_bomb", BattleCardKind.Common, null, exhaust: true);
        deck.BuildDeck(new[] { entry });
        var drawn = deck.Draw(1)[0];

        Assert.IsTrue(deck.ResolvePlayedCard(drawn));
        Assert.AreEqual(1, deck.ExhaustPileCount);
        Assert.AreEqual(0, deck.DiscardPileCount);
        Assert.AreEqual(0, deck.HandCount);
    }

    [Test]
    public void ResolvePlayedCard_NormalCard_GoesToDiscard()
    {
        var deck = new SharedDeck();
        var entry = MakeEntry("c_sword", BattleCardKind.Character, "person_001");
        deck.BuildDeck(new[] { entry });
        var drawn = deck.Draw(1)[0];

        Assert.IsTrue(deck.ResolvePlayedCard(drawn));
        Assert.AreEqual(0, deck.ExhaustPileCount);
        Assert.AreEqual(1, deck.DiscardPileCount);
        Assert.AreEqual(0, deck.HandCount);
    }

    // ---------- Reshuffle ----------

    [Test]
    public void Reshuffle_RandomizesDrawPileOrder()
    {
        // 注入固定种子使洗牌结果确定
        var deck = new SharedDeck(new System.Random(42));
        var entry = MakeEntry("c_sword", BattleCardKind.Character, "person_001", copies: 10);
        deck.BuildDeck(new[] { entry });

        deck.Reshuffle();

        // 数量不变
        Assert.AreEqual(10, deck.DrawPileCount);

        // 全部实例仍在（仅验证成员关系，不验证具体顺序）
        var ids = new List<string>();
        foreach (var c in deck.DrawPile) ids.Add(c.TemplateId);
        Assert.AreEqual(10, ids.FindAll(id => id == "c_sword").Count);
    }

    [Test]
    public void BuildDeck_ThenReshuffle_TotalCardCountPreserved()
    {
        var deck = new SharedDeck();
        var a = MakeEntry("a", BattleCardKind.Character, "person_001", copies: 2);
        var b = MakeEntry("b", BattleCardKind.Common, null, copies: 3);
        deck.BuildDeck(new[] { a, b });
        deck.Reshuffle();

        int total = deck.DrawPileCount + deck.HandCount + deck.DiscardPileCount + deck.ExhaustPileCount;
        Assert.AreEqual(5, total);
    }
}

