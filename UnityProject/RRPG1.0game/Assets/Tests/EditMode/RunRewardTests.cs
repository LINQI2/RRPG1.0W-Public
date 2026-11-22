using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Stage 6B Run 卡牌奖励纯逻辑测试（BattleStage1.Runtime 强类型）。
/// 覆盖：候选池生成（全人物扁平、不先随机人物）、同模板不同人物为不同候选、
/// 三选一内无完全相同实例、选择/Skip 对 Run Deck Copies 与奖励机会的副作用、连续多次奖励处理。
/// </summary>
public class RunRewardTests
{
    private static BattleCardData NewCard(string cardId, BattleCardKind kind = BattleCardKind.Character)
    {
        var c = ScriptableObject.CreateInstance<BattleCardData>();
        c.CardId = cardId;
        c.DisplayName = cardId;
        c.CardKind = kind;
        return c;
    }

    private static RunState NewRun()
    {
        var run = new RunState();
        run.GetOrCreatePerson("char_001", "战士", 30);
        run.GetOrCreatePerson("char_002", "猎人", 30);
        run.GetOrCreatePerson("hero_001", "青鹿", 30);
        return run;
    }

    private static List<BattleCardData> DemoPoolTemplates()
    {
        return new List<BattleCardData>
        {
            NewCard("bcard_aim"),
            NewCard("bcard_sword"),
            NewCard("bcard_heal"),
            NewCard("bcard_rally"),
            NewCard("bcard_common", BattleCardKind.Common)
        };
    }

    // ==================== 领悟 → 奖励机会 ====================

    [Test]
    public void AddInsightFromEncounter_CrossesThreshold_ProducesPendingCardReward()
    {
        var run = new RunState();
        run.InsightThreshold = 30;

        Assert.AreEqual(1, run.AddInsightFromEncounter(RunEncounterType.BattleVictory, 30),
            "BattleVictory 遭遇授予 30 领悟，跨过阈值 30 应得 1 次奖励机会");
        Assert.AreEqual(1, run.PendingCardRewardCount);
        Assert.AreEqual(0, run.CurrentInsight);
    }

    [Test]
    public void AddInsightFromEncounter_OverflowKeepsRemainder()
    {
        var run = new RunState();
        run.InsightThreshold = 30;

        Assert.AreEqual(1, run.AddInsightFromEncounter(RunEncounterType.BattleVictory, 40));
        Assert.AreEqual(10, run.CurrentInsight, "40-30=10 溢出保留");
        Assert.AreEqual(1, run.PendingCardRewardCount);
    }

    // ==================== 候选池生成 ====================

    [Test]
    public void Provider_BuildsCandidates_FromAllRunPersons_PlusCommon_NotPersonFirst()
    {
        var run = NewRun();
        var provider = new DemoCardPoolRewardProvider();

        var candidates = provider.BuildCandidates(run, DemoPoolTemplates());

        // 全人物扁平池：char_001(aim) + char_002(sword) + hero_001(sword) + 通用(common) = 4
        Assert.AreEqual(4, candidates.Count);
        Assert.IsTrue(candidates.Any(c => c.PersonId == "char_001" && c.Template.CardId == "bcard_aim"));
        Assert.IsTrue(candidates.Any(c => c.PersonId == "char_002" && c.Template.CardId == "bcard_sword"));
        Assert.IsTrue(candidates.Any(c => c.PersonId == "hero_001" && c.Template.CardId == "bcard_sword"));
        Assert.IsTrue(candidates.Any(c => c.IsCommon && c.Template.CardId == "bcard_common"));
    }

    [Test]
    public void Provider_DifferentPersonsSameTemplate_AreDistinctCandidates()
    {
        var run = NewRun();
        var provider = new DemoCardPoolRewardProvider();

        var candidates = provider.BuildCandidates(run, DemoPoolTemplates());
        var swordCandidates = candidates.Where(c => c.Template.CardId == "bcard_sword").ToList();

        Assert.AreEqual(2, swordCandidates.Count, "不同人物拥有同模板应产生两个不同候选");
        Assert.AreNotEqual(swordCandidates[0].IdentityKey, swordCandidates[1].IdentityKey,
            "不同人物的同模板候选 IdentityKey 不同");
    }

    // ==================== 抽取（去重 / 不先随机人物 / 同模板不同人物并存） ====================

    [Test]
    public void Picker_PickDistinct_NoIdenticalCandidatesInOffer()
    {
        var run = NewRun();
        var provider = new DemoCardPoolRewardProvider();
        var pool = provider.BuildCandidates(run, DemoPoolTemplates());

        // 抽取 4 次（全部候选）→ 无相同实例
        var pick = RunRewardPicker.PickDistinct(pool, 4, new System.Random(12345));
        Assert.AreEqual(4, pick.Count);
        var keys = pick.Select(c => c.IdentityKey).Distinct().ToList();
        Assert.AreEqual(4, keys.Count, "同一 offer 内不得出现完全相同的候选实例");
    }

    [Test]
    public void Picker_DifferentPersonsSameTemplate_BothCanAppear()
    {
        var run = NewRun();
        var provider = new DemoCardPoolRewardProvider();
        var pool = provider.BuildCandidates(run, DemoPoolTemplates());

        var pick = RunRewardPicker.PickDistinct(pool, 4, new System.Random(42));
        Assert.AreEqual(2, pick.Count(c => c.Template.CardId == "bcard_sword"),
            "猎人A·挥砍 与 英雄B·挥砍 应可同时成为候选");
    }

    [Test]
    public void Picker_CountLargerThanPool_ReturnsAllDistinct()
    {
        var run = NewRun();
        var provider = new DemoCardPoolRewardProvider();
        var pool = provider.BuildCandidates(run, DemoPoolTemplates());

        var pick = RunRewardPicker.PickDistinct(pool, 99, new System.Random(7));
        Assert.AreEqual(4, pick.Count, "抽取数超过候选池时返回全部去重候选");
    }

    [Test]
    public void Picker_DuplicatePoolInput_IsDeduped()
    {
        var run = NewRun();
        var provider = new DemoCardPoolRewardProvider();
        var pool = provider.BuildCandidates(run, DemoPoolTemplates());

        // 手动加入完全相同的候选（同人物同模板）后，抽取不得出现重复
        var dup = new List<RunRewardCandidate>(pool);
        dup.Add(new RunRewardCandidate { PersonId = "char_001", Template = NewCard("bcard_aim"), Weight = 1 });

        var pick = RunRewardPicker.PickDistinct(dup, 99, new System.Random(3));
        Assert.AreEqual(4, pick.Count, "完全相同的候选实例应被去重");
    }

    // ==================== 选择 / Skip 的副作用 ====================

    [Test]
    public void SelectCard_AddsToSpecificPersonDeckCopies_AndConsumesReward()
    {
        var run = NewRun();
        run.PendingCardRewardCount = 1;

        // 模拟三选一选中 char_002 的挥砍
        run.AddRunCard("char_002", NewCard("bcard_sword"));
        run.ConsumePendingCardReward();

        var entry = run.RunDeckEntries.First(e => e.PersonId == "char_002" && e.Template.CardId == "bcard_sword");
        Assert.AreEqual(1, entry.Copies, "选中后该人物 Run Deck 应新增 1 份");
        Assert.AreEqual(0, run.PendingCardRewardCount, "选择应消耗 1 次奖励机会");
    }

    [Test]
    public void Skip_ConsumesReward_WithoutAddingCard()
    {
        var run = NewRun();
        run.PendingCardRewardCount = 1;
        int deckBefore = run.RunDeckEntries.Count;

        run.ConsumePendingCardReward(); // Skip

        Assert.AreEqual(0, run.PendingCardRewardCount, "Skip 消耗本次机会");
        Assert.AreEqual(deckBefore, run.RunDeckEntries.Count, "Skip 不增加牌");
    }

    [Test]
    public void MultiReward_SequentialProcessing_ConsumesAll_KeepsInsight()
    {
        var run = NewRun();
        run.InsightThreshold = 30;
        run.CurrentInsight = 10;
        run.AddInsightFromEncounter(RunEncounterType.BattleVictory, 30); // 10+30=40 → +1 机会，余 10
        run.PendingCardRewardCount = 2; // 模拟累计 2 次

        // 连续处理 2 次：选择一张 + Skip 一张
        run.AddRunCard("char_001", NewCard("bcard_aim"));
        run.ConsumePendingCardReward();
        run.ConsumePendingCardReward(); // Skip

        Assert.AreEqual(0, run.PendingCardRewardCount, "连续 2 次机会应全部处理完");
        Assert.AreEqual(1, run.RunDeckEntries.Count, "只选择了一次 → 只增加 1 张");
        Assert.AreEqual(10, run.CurrentInsight, "处理奖励不消耗领悟余量");
    }

    [Test]
    public void ConsumePendingCardReward_NeverBelowZero()
    {
        var run = new RunState();
        run.PendingCardRewardCount = 0;
        run.ConsumePendingCardReward();
        run.ConsumePendingCardReward();
        Assert.AreEqual(0, run.PendingCardRewardCount);
    }
}

