using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Stage 6A RunState 纯逻辑测试（BattleStage1.Runtime 强类型）。
/// 覆盖：领悟阈值、战后恢复、Pending Rewards 兑现/丢弃、Run 退出策略、Run Deck 跨战保留。
/// </summary>
public class RunStateTests
{
    private class FakeSink : IRunRewardSink
    {
        public int Gold;
        public int Material;
        public readonly Dictionary<string, int> ExpByPerson = new Dictionary<string, int>();
        public readonly Dictionary<string, int> ProfessionExpByPerson = new Dictionary<string, int>();

        public void AddGold(int amount) => Gold += amount;
        public void AddBuildingMaterial(int amount) => Material += amount;
        public void AddGrowthExp(string personId, int amount)
        {
            ExpByPerson.TryGetValue(personId, out int cur);
            ExpByPerson[personId] = cur + amount;
        }

        public bool TryAddProfessionExp(string personId, int amount)
        {
            ProfessionExpByPerson.TryGetValue(personId, out int cur);
            ProfessionExpByPerson[personId] = cur + amount;
            return true;
        }
    }
    private static BattleCardData NewCard(string cardId)
    {
        var c = ScriptableObject.CreateInstance<BattleCardData>();
        c.CardId = cardId;
        c.DisplayName = cardId;
        c.CardKind = BattleCardKind.Character;
        return c;
    }

    // ==================== 领悟 ====================

    [Test]
    public void AddInsight_80Then150_Threshold100_Gives2Chances_Remainder30()
    {
        var run = new RunState();
        run.InsightThreshold = 100;

        Assert.AreEqual(0, run.AddInsight(80), "80 不应触发奖励机会");
        Assert.AreEqual(80, run.CurrentInsight);

        int chances = run.AddInsight(150);
        Assert.AreEqual(2, chances, "80+150=230 应跨 2 个阈值");
        Assert.AreEqual(2, run.PendingCardRewardCount);
        Assert.AreEqual(30, run.CurrentInsight, "触发只扣阈值不清零：230 - 100*2 = 30");
    }

    [Test]
    public void AddInsight_SingleCallCrossesMultipleThresholds()
    {
        var run = new RunState();
        run.InsightThreshold = 100;

        Assert.AreEqual(2, run.AddInsight(250), "250 一次跨 2 个阈值");
        Assert.AreEqual(50, run.CurrentInsight);
        Assert.AreEqual(2, run.PendingCardRewardCount);

        Assert.AreEqual(3, run.AddInsight(300), "50+300=350 再跨 3 个阈值");
        Assert.AreEqual(50, run.CurrentInsight, "350 - 100*3 = 50");
        Assert.AreEqual(5, run.PendingCardRewardCount);
    }

    [Test]
    public void AddInsight_ThresholdZero_DoesNotInfiniteLoop()
    {
        var run = new RunState();
        run.InsightThreshold = 0;

        Assert.AreEqual(0, run.AddInsight(500), "阈值为 0 不应触发且不循环");
        Assert.AreEqual(500, run.CurrentInsight);
        Assert.AreEqual(0, run.PendingCardRewardCount);
    }

    // ==================== 战后恢复 / HP 跨战 ====================

    [Test]
    public void ApplyBattleEndHp_RecoveryPercent_NotFullHp()
    {
        var run = new RunState();
        run.PostBattleRecoveryPercent = 0.05f;
        run.PostBattleRecoveryMin = 1;

        run.ApplyBattleEndHp("char_001", 40, 100);
        Assert.AreEqual(45, run.GetPersonCurrentHp("char_001", -1), "40 + ceil(100*5%)=45");
        Assert.AreNotEqual(100, run.GetPersonCurrentHp("char_001", -1), "战后不自动回满");
    }

    [Test]
    public void ApplyBattleEndHp_ZeroHp_RecoversToPositive()
    {
        var run = new RunState();
        run.PostBattleRecoveryPercent = 0.05f;
        run.PostBattleRecoveryMin = 1;

        run.ApplyBattleEndHp("char_001", 0, 100);
        Assert.AreEqual(5, run.GetPersonCurrentHp("char_001", -1), "0HP 战后应可恢复");
    }

    [Test]
    public void ApplyBattleEndHp_NoRecovery_WhenPercentZero()
    {
        var run = new RunState();
        run.PostBattleRecoveryPercent = 0f;

        run.ApplyBattleEndHp("char_001", 40, 100);
        Assert.AreEqual(40, run.GetPersonCurrentHp("char_001", -1), "恢复比例 0 时不恢复");
    }

    [Test]
    public void ApplyBattleEndHp_DoesNotExceedMaxHp()
    {
        var run = new RunState();
        run.PostBattleRecoveryPercent = 0.5f;

        run.ApplyBattleEndHp("char_001", 90, 100);
        Assert.AreEqual(100, run.GetPersonCurrentHp("char_001", -1), "恢复不超过 MaxHP");
    }

    [Test]
    public void NextBattle_StartsFromRunCurrentHp_NotFull()
    {
        var run = new RunState();
        run.PostBattleRecoveryPercent = 0.05f;
        run.PostBattleRecoveryMin = 1;
        run.GetOrCreatePerson("char_001", "战士", 100);

        // 第一战结束：40 → 恢复 5 → 45
        run.ApplyBattleEndHp("char_001", 40, 100);

        // 第二战初始化：从 Run CurrentHP（45），而非 MaxHP
        Assert.AreEqual(45, run.GetPersonCurrentHp("char_001", 100));
        Assert.AreNotEqual(100, run.GetPersonCurrentHp("char_001", 100));
    }

    // ==================== Pending Rewards ====================

    [Test]
    public void CommitPendingRewards_FullRatio_WritesAllAndResets()
    {
        var run = new RunState();
        run.AddPendingGold(20);
        run.AddPendingBuildingMaterial(5);
        run.AddPendingGrowthExp("char_001", 10);
        run.AddPendingGrowthExp("hero_001", 10);

        var sink = new FakeSink();
        run.CommitPendingRewards(sink, 0f);

        Assert.AreEqual(20, sink.Gold);
        Assert.AreEqual(5, sink.Material);
        Assert.AreEqual(20, sink.ExpByPerson["char_001"] + sink.ExpByPerson["hero_001"]);
        Assert.IsFalse(run.HasPendingRewards, "兑现后 Pending 应清零");
        Assert.AreEqual(0, run.PendingGold);
    }

    [Test]
    public void CommitPendingRewards_WithLossRatio_KeepsPortion()
    {
        var run = new RunState();
        run.AddPendingGold(20);

        var sink = new FakeSink();
        run.CommitPendingRewards(sink, 0.25f);
        Assert.AreEqual(15, sink.Gold, "20 - 20*25% = 15");
    }

    [Test]
    public void DiscardPendingRewards_ZeroesAll()
    {
        var run = new RunState();
        run.AddPendingGold(20);
        run.AddPendingGrowthExp("char_001", 10);

        run.DiscardPendingRewards();
        Assert.IsFalse(run.HasPendingRewards);
        Assert.AreEqual(0, run.GetPerson("char_001").PendingGrowthExp);
    }

    // ==================== Run 退出策略 ====================

    [Test]
    public void ExitPolicy_SafeReturnZero_RetreatDefeatNotConfigured()
    {
        var policy = new RunExitPolicy();
        Assert.AreEqual(0f, policy.GetLossRatio(RunEndMode.SafeReturn), "安全返程无损失");
        Assert.AreEqual(RunExitPolicy.NotConfigured, policy.GetLossRatio(RunEndMode.Retreat), "主动返程损失未冻结");
        Assert.AreEqual(RunExitPolicy.NotConfigured, policy.GetLossRatio(RunEndMode.Defeat), "全灭损失未冻结");
    }

    // ==================== Run Deck ====================

    [Test]
    public void AddRunCard_SamePersonSameTemplate_IncrementsCopies()
    {
        var run = new RunState();
        var card = NewCard("bcard_sword");

        run.AddRunCard("char_001", card);
        run.AddRunCard("char_001", card);

        Assert.AreEqual(1, run.RunDeckEntries.Count);
        Assert.AreEqual(2, run.RunDeckEntries[0].Copies, "同人物同模板应累加份数");
    }

    [Test]
    public void RunDeck_NewCardRetained_AcrossSharedDeckRebuilds()
    {
        var run = new RunState();
        run.AddRunCard("char_001", NewCard("bcard_sword"));
        var newCard = NewCard("bcard_reward_test");
        run.AddRunCard("char_001", newCard); // 第一战获得新牌

        // 第二战：每战重新创建 SharedDeck，但 Run Deck 不重置
        var deck2 = new SharedDeck();
        deck2.BuildDeck(run.GetRunDeckEntries());
        Assert.IsTrue(deck2.DrawPile.Exists(c => c.TemplateId == "bcard_reward_test"), "新牌应进入第二战牌库");

        // 第三战：再次构筑，Run Deck 条目数不变（未被清除）
        var deck3 = new SharedDeck();
        deck3.BuildDeck(run.GetRunDeckEntries());
        Assert.IsTrue(deck3.DrawPile.Exists(c => c.TemplateId == "bcard_reward_test"));
        Assert.AreEqual(2, run.RunDeckEntries.Count, "Run Deck 不应被 Battle 重建清除");
    }

    [Test]
    public void SeedRunDeckFromPool_SeedsOnlyGivenPersons()
    {
        var run = new RunState();
        var templates = new List<BattleCardData>
        {
            NewCard("bcard_aim"),
            NewCard("bcard_sword"),
            NewCard("bcard_heal"),
            NewCard("bcard_rally")
        };

        run.SeedRunDeckFromPool(new List<string> { "char_001", "hero_001" }, templates);

        Assert.AreEqual(2, run.RunDeckEntries.Count, "只为给定人物播种其映射牌");
        Assert.AreEqual("char_001", run.RunDeckEntries[0].PersonId);
        Assert.AreEqual("hero_001", run.RunDeckEntries[1].PersonId);
        Assert.AreEqual(1, run.RunDeckEntries[0].Copies);
    }

    [Test]
    public void GetRunDeckEntries_ReturnsSnapshot_DoesNotAliasInternal()
    {
        var run = new RunState();
        run.AddRunCard("char_001", NewCard("bcard_sword"));

        var snapshot = run.GetRunDeckEntries();
        snapshot.Clear();

        Assert.AreEqual(1, run.RunDeckEntries.Count, "快照修改不得影响内部 Run Deck");
    }
}

