using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// BattleV1DemoCardPool（测试人物-角色牌池映射）EditMode 测试
///
/// 覆盖 Current GameSpec R8.1/R20 语义：
///   角色牌绑定具体人物；只有本场实际参战人物拥有的角色牌进入共享牌库；
///   不在战场的医师/村长，其「治疗」「鼓舞」等角色牌不得进入本场；
///   Common 牌不绑定人物，始终进入。
/// </summary>
public class BattleV1DemoCardPoolTests
{
    private BattleCardData MakeTemplate(string id, BattleCardKind kind, int cost = 1)
    {
        var data = ScriptableObject.CreateInstance<BattleCardData>();
        data.CardId = id;
        data.DisplayName = id;
        data.CardKind = kind;
        data.Cost = cost;
        return data;
    }

    private List<BattleCardData> MakeDemoTemplates()
    {
        return new List<BattleCardData>
        {
            MakeTemplate("bcard_sword", BattleCardKind.Character),   // 猎人：挥砍
            MakeTemplate("bcard_aim", BattleCardKind.Character),     // 战士：瞄准射击
            MakeTemplate("bcard_heal", BattleCardKind.Character),    // 医师：治疗
            MakeTemplate("bcard_rally", BattleCardKind.Character)    // 村长：鼓舞
        };
    }

    [Test]
    public void BuildRoster_HunterAndWarrior_ExcludesMedicAndChiefCards()
    {
        var roster = BattleV1DemoCardPool.BuildRoster(
            new[] { "char_001", "char_002" }, MakeDemoTemplates());

        // 战士→瞄准射击、猎人→挥砍进入
        Assert.Contains(FindEntry(roster, "char_001", "bcard_aim"), roster);
        Assert.Contains(FindEntry(roster, "char_002", "bcard_sword"), roster);

        // 医师「治疗」、村长「鼓舞」不得进入（未参战）
        Assert.IsNull(FindEntry(roster, "char_003", "bcard_heal"), "未参战医师的治疗牌不应进入");
        Assert.IsNull(FindEntry(roster, "char_004", "bcard_rally"), "未参战村长的鼓舞牌不应进入");
        // 未参战人物不应以任何形式出现在 roster
        Assert.IsFalse(roster.Exists(e => e.PersonId == "char_003"), "未参战医师不应出现");
        Assert.IsFalse(roster.Exists(e => e.PersonId == "char_004"), "未参战村长不应出现");
    }

    [Test]
    public void BuildRoster_IncludesHeroOwnedCards()
    {
        var roster = BattleV1DemoCardPool.BuildRoster(
            new[] { "char_001", "char_002", "hero_001" }, MakeDemoTemplates());

        // 英雄（青鹿）→ 挥砍；英雄不得拥有医师的「治疗」
        Assert.Contains(FindEntry(roster, "hero_001", "bcard_sword"), roster);
        Assert.IsNull(FindEntry(roster, "hero_001", "bcard_heal"), "英雄不应拥有医师的治疗牌");
    }

    [Test]
    public void BuildRoster_UnknownPerson_GetsNoCards()
    {
        var roster = BattleV1DemoCardPool.BuildRoster(
            new[] { "char_999" }, MakeDemoTemplates());

        Assert.IsEmpty(roster);
    }

    [Test]
    public void BuildRoster_CommonAlwaysIncluded()
    {
        var templates = MakeDemoTemplates();
        templates.Add(MakeTemplate("bcard_common_demo", BattleCardKind.Common));

        var roster = BattleV1DemoCardPool.BuildRoster(
            new[] { "char_001", "char_002" }, templates);

        Assert.Contains(FindEntry(roster, null, "bcard_common_demo"), roster);
    }

    [Test]
    public void FullDeck_HunterAndWarrior_HasNoMedicOrChiefCard()
    {
        var roster = BattleV1DemoCardPool.BuildRoster(
            new[] { "char_001", "char_002" }, MakeDemoTemplates());
        var valid = SharedDeck.FilterValidCards(roster, new[] { "char_001", "char_002" });

        var deck = new SharedDeck();
        deck.BuildDeck(valid);
        deck.Reshuffle();

        // 整个牌库（抽/弃/手/耗 全集）不得包含医师治疗或村长鼓舞
        var allCardTemplates = new List<string>();
        foreach (var c in deck.DrawPile) allCardTemplates.Add(c.TemplateId);
        foreach (var c in deck.Hand) allCardTemplates.Add(c.TemplateId);
        foreach (var c in deck.DiscardPile) allCardTemplates.Add(c.TemplateId);
        foreach (var c in deck.ExhaustPile) allCardTemplates.Add(c.TemplateId);

        Assert.IsFalse(allCardTemplates.Contains("bcard_heal"), "牌库不应包含医师的治疗");
        Assert.IsFalse(allCardTemplates.Contains("bcard_rally"), "牌库不应包含村长的鼓舞");
        CollectionAssert.Contains(allCardTemplates, "bcard_sword");
        CollectionAssert.Contains(allCardTemplates, "bcard_aim");
    }

    private static BattlePersonCardEntry FindEntry(List<BattlePersonCardEntry> roster, string personId, string cardId)
    {
        return roster.Find(e => e.PersonId == personId && e.Template != null && e.Template.CardId == cardId);
    }
}
