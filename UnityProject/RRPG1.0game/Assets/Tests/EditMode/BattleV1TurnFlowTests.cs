using NUnit.Framework;
using UnityEngine;

/// <summary>
/// BattleV1TurnFlow（玩家回合生命周期）EditMode 测试
///
/// 覆盖 Battle GameSpec V0.1 Stage 2 / Milestone A 要求：
///   玩家回合开始恢复共享 AP / 抽牌 / 打牌费用支付 / 普通牌进 Discard / Exhaust 进 Exhaust /
///   回合结束普通手牌弃置 / Retain 保留 / 下一玩家回合刷新 AP 与抽牌 /
///   抽牌堆耗尽时洗回弃牌堆继续抽 / 行动力不足不可合法使用。
/// </summary>
public class BattleV1TurnFlowTests
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

    private BattlePersonCardEntry MakeEntry(string templateId, BattleCardKind kind,
                                            int cost = 1, bool retain = false, bool exhaust = false,
                                            int copies = 1)
    {
        var template = MakeTemplate(templateId, kind, cost, retain, exhaust);
        string personId = kind == BattleCardKind.Common ? null : "person_001";
        return new BattlePersonCardEntry(template, personId, copies);
    }

    private BattleV1TurnFlow Setup(BattlePersonCardEntry[] entries, int maxAP = 3)
    {
        var deck = new SharedDeck();
        deck.BuildDeck(entries);
        deck.Reshuffle();
        var ap = new SharedActionPointPool(maxAP);
        return new BattleV1TurnFlow(deck, ap);
    }

    // ---------- 玩家回合开始 ----------

    [Test]
    public void BeginPlayerTurn_EntersPlayerTurn_AndRestoresSharedAP()
    {
        var flow = Setup(new[] { MakeEntry("c_sword", BattleCardKind.Character, copies: 5) }, maxAP: 3);

        flow.BeginPlayerTurn(1);
        flow.PayCost(flow.Deck.Hand[0]); // 消耗 AP
        int spent = flow.ActionPoints.Current;

        // 下一玩家回合：AP 恢复至上限
        flow.BeginPlayerTurn(1);

        Assert.AreEqual(BattleV1TurnFlow.PhaseType.PlayerTurn, flow.Phase);
        Assert.AreEqual(flow.ActionPoints.Max, flow.ActionPoints.Current);
        Assert.Less(spent, flow.ActionPoints.Current); // 消耗后 < 恢复后
    }

    [Test]
    public void BeginPlayerTurn_DrawsRequestedCount()
    {
        var flow = Setup(new[] { MakeEntry("c_sword", BattleCardKind.Character, copies: 6) });

        int drawn = flow.BeginPlayerTurn(3);

        Assert.AreEqual(3, drawn);
        Assert.AreEqual(3, flow.Deck.HandCount);
    }

    [Test]
    public void TurnNumber_IncrementsEachPlayerTurn()
    {
        var flow = Setup(new[] { MakeEntry("c_sword", BattleCardKind.Character, copies: 6) });

        flow.BeginPlayerTurn(2);
        flow.EndPlayerTurn();
        flow.BeginPlayerTurn(2);

        Assert.AreEqual(2, flow.TurnNumber);
    }

    // ---------- 出牌合法性 ----------

    [Test]
    public void CanPlay_InsufficientAP_ReturnsFalse()
    {
        var flow = Setup(new[] { MakeEntry("c_cost2", BattleCardKind.Character, cost: 2) }, maxAP: 1);
        flow.BeginPlayerTurn(1);

        var card = flow.Deck.Hand[0];
        Assert.IsFalse(flow.CanPlay(card)); // AP(1) < 费用(2)，不可合法使用
    }

    [Test]
    public void CanPlay_SufficientAP_ReturnsTrue()
    {
        var flow = Setup(new[] { MakeEntry("c_cost1", BattleCardKind.Character, cost: 1) }, maxAP: 3);
        flow.BeginPlayerTurn(1);

        Assert.IsTrue(flow.CanPlay(flow.Deck.Hand[0]));
    }

    [Test]
    public void CanPlay_OutsidePlayerTurn_ReturnsFalse()
    {
        var flow = Setup(new[] { MakeEntry("c_sword", BattleCardKind.Character, copies: 3) }, maxAP: 3);
        flow.BeginPlayerTurn(1);
        var card = flow.Deck.Hand[0];
        flow.EndPlayerTurn(); // 进入 EnemyTurn

        Assert.IsFalse(flow.CanPlay(card)); // 敌方阶段不可出牌
    }

    [Test]
    public void PayCost_DeductsSharedAP()
    {
        var flow = Setup(new[] { MakeEntry("c_cost1", BattleCardKind.Character, cost: 1) }, maxAP: 3);
        flow.BeginPlayerTurn(1);

        bool ok = flow.PayCost(flow.Deck.Hand[0]);

        Assert.IsTrue(ok);
        Assert.AreEqual(2, flow.ActionPoints.Current);
    }

    // ---------- 牌去向 ----------

    [Test]
    public void ResolvePlayedCard_Normal_GoesToDiscard()
    {
        var flow = Setup(new[] { MakeEntry("c_sword", BattleCardKind.Character, copies: 3) });
        flow.BeginPlayerTurn(1);
        var card = flow.Deck.Hand[0];

        bool ok = flow.ResolvePlayedCard(card);

        Assert.IsTrue(ok);
        Assert.AreEqual(1, flow.Deck.DiscardPileCount);
        Assert.AreEqual(0, flow.Deck.ExhaustPileCount);
        Assert.IsFalse(flow.Deck.Hand.Contains(card));
    }

    [Test]
    public void ResolvePlayedCard_Exhaust_GoesToExhaust()
    {
        var flow = Setup(new[] { MakeEntry("c_bomb", BattleCardKind.Character, exhaust: true, copies: 2) });
        flow.BeginPlayerTurn(1);
        var card = flow.Deck.Hand[0];

        bool ok = flow.ResolvePlayedCard(card);

        Assert.IsTrue(ok);
        Assert.AreEqual(0, flow.Deck.DiscardPileCount);
        Assert.AreEqual(1, flow.Deck.ExhaustPileCount);
        Assert.IsFalse(flow.Deck.Hand.Contains(card));
    }

    // ---------- 回合结束弃牌 ----------

    [Test]
    public void EndPlayerTurn_DiscardsNormal_KeepsRetain()
    {
        var flow = Setup(new[]
        {
            MakeEntry("c_normal", BattleCardKind.Character, copies: 2),
            MakeEntry("c_retain", BattleCardKind.Character, retain: true, copies: 1)
        });
        flow.BeginPlayerTurn(3); // 抽满 3 张（2 普通 + 1 滞留）

        int discarded = flow.EndPlayerTurn();

        // 普通牌全部弃置，Retain 牌保留在手牌
        Assert.AreEqual(2, discarded);
        Assert.AreEqual(2, flow.Deck.DiscardPileCount);
        Assert.AreEqual(1, flow.Deck.HandCount);
        Assert.AreEqual("c_retain", flow.Deck.Hand[0].TemplateId);
        Assert.AreEqual(BattleV1TurnFlow.PhaseType.EnemyTurn, flow.Phase);
    }

    [Test]
    public void EndPlayerTurn_NoRetainCards_DiscardsAll()
    {
        var flow = Setup(new[] { MakeEntry("c_sword", BattleCardKind.Character, copies: 3) });
        flow.BeginPlayerTurn(3);

        int discarded = flow.EndPlayerTurn();

        Assert.AreEqual(3, discarded);
        Assert.AreEqual(0, flow.Deck.HandCount);
        Assert.AreEqual(3, flow.Deck.DiscardPileCount);
    }

    // ---------- 循环与洗回 ----------

    [Test]
    public void NextPlayerTurn_RestoresAP_AndDrawsAgain()
    {
        var flow = Setup(new[] { MakeEntry("c_sword", BattleCardKind.Character, copies: 8) }, maxAP: 3);
        flow.BeginPlayerTurn(3);
        flow.PayCost(flow.Deck.Hand[0]);          // AP 3→2
        flow.EndPlayerTurn();                      // 手牌弃置
        flow.BeginPlayerTurn(3);                   // 下一玩家回合

        Assert.AreEqual(3, flow.ActionPoints.Current); // AP 恢复
        Assert.AreEqual(3, flow.Deck.HandCount);       // 重新抽牌
        Assert.AreEqual(2, flow.TurnNumber);
    }

    [Test]
    public void Draw_WhenDrawPileExhausted_ReshufflesDiscardAndContinues()
    {
        // 小牌库：2 张牌。抽光后打掉/弃掉进 Discard，再抽时洗回弃牌堆继续抽。
        var flow = Setup(new[] { MakeEntry("c_sword", BattleCardKind.Character, copies: 2) }, maxAP: 3);

        flow.BeginPlayerTurn(2);      // 抽 2 张，抽牌堆耗尽
        Assert.AreEqual(0, flow.Deck.DrawPileCount);

        flow.PayCost(flow.Deck.Hand[0]);
        flow.ResolvePlayedCard(flow.Deck.Hand[0]); // 打 1 张进 Discard
        flow.EndPlayerTurn();                       // 剩余 1 张弃入 Discard → Discard=2
        Assert.AreEqual(2, flow.Deck.DiscardPileCount);

        int drawn = flow.BeginPlayerTurn(2);        // 抽牌堆空 → 洗回弃牌堆 → 抽 2 张

        Assert.AreEqual(2, drawn);
        Assert.AreEqual(2, flow.Deck.HandCount);
        Assert.AreEqual(0, flow.Deck.DiscardPileCount); // 弃牌堆已洗回
    }
}

