using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;

/// <summary>
/// V4 Gate 2 专项（Temporary Development Test；Closeout 默认删除）：
/// 非零 Expedition Elapsed → Supply 真实消耗 → Return Catch-up 使 Village 真实推进
/// → Return Person Consequence（长期 Injury + Character Expedition Experience）
/// → 会话内 exactly-once（含 Begin 重建守卫）→ Save / Load 保持一致。
///
/// 全部内容 Test / Not Frozen。
/// </summary>
public class VillageV4ReturnConsequenceTests
{
    [SetUp]
    public void SetUp()
    {
        VillageV4TestKit.ResetBridge();
        string p = VillageV4TestKit.GetSavePath();
        if (!string.IsNullOrEmpty(p) && File.Exists(p)) File.Delete(p);
    }

    [TearDown]
    public void TearDown()
    {
        VillageV4TestKit.ResetBridge();
        string p = VillageV4TestKit.GetSavePath();
        if (!string.IsNullOrEmpty(p) && File.Exists(p)) File.Delete(p);
    }

    private static IReadOnlyList<string> ExpeditionIds() =>
        (IReadOnlyList<string>)VillageV4TestKit.GetStatic(VillageV4TestKit.Bridge, "ExpeditionPersonIds");

    // ==================== 1｜Elapsed 策略：Battle/Elite/Boss = 1，其它 = 0 ====================

    [Test]
    public void ElapsedPolicy_BattleEliteBossAreOneDay_OtherLegacyNodesAreZero()
    {
        Assert.AreEqual(1, VillageV4TestKit.ResolveElapsedDays("Battle"));
        Assert.AreEqual(1, VillageV4TestKit.ResolveElapsedDays("Elite"));
        Assert.AreEqual(1, VillageV4TestKit.ResolveElapsedDays("Boss"));
        Assert.AreEqual(0, VillageV4TestKit.ResolveElapsedDays("Event"));
        Assert.AreEqual(0, VillageV4TestKit.ResolveElapsedDays("Rest"));
        Assert.AreEqual(0, VillageV4TestKit.ResolveElapsedDays("Shop"));
        Assert.AreEqual(0, VillageV4TestKit.ResolveElapsedDays("Recruit"));
    }

    // ==================== 2｜Supply：非零 elapsed 真实扣减 + 超出能力时 InsufficientSupply ====================

    [Test]
    public void Supply_NonZeroElapsed_IsReallyCharged_AndInsufficientSupplyBlocksEnter()
    {
        var graph = new ExpeditionGraph("n0", new List<ExpeditionNodeData>
        {
            new ExpeditionNodeData("n0", ExpeditionNodeKind.Battle, new[] { "n1" }, 1, 0, false),
            new ExpeditionNodeData("n1", ExpeditionNodeKind.Battle, new[] { "n2" }, 1, 0, false),
            new ExpeditionNodeData("n2", ExpeditionNodeKind.Boss, new string[0], 1, 0, true)
        });

        var resolver = new ExpeditionProgressionResolver();
        // party=2, carry=4, rate=1 → 每个 Battle Node 成本 = 2 × 1 × 1 = 2（恰好 2 个节点可负担）
        Assert.AreEqual(ExpeditionProgressionInitResult.Success,
            resolver.InitializeProgression(graph, 2, 4, 1));

        Assert.AreEqual(ExpeditionEnterResult.Success, resolver.TryEnterNode("n0"));
        Assert.AreEqual(ExpeditionResolveResult.Success,
            resolver.TryResolveCurrentNode(ExpeditionNodeOutcome.Success, out var first));
        Assert.AreEqual(1, first.NodeElapsedDaysSettled, "Battle Node 应结算 1 Test Day");
        Assert.AreEqual(2, first.BaseSupplyConsumption, "Supply 成本 = PartySize × elapsed × rate");
        Assert.AreEqual(2, first.CurrentCarryUnits, "Carry 应真实扣减");

        Assert.AreEqual(ExpeditionEnterResult.Success, resolver.TryEnterNode("n1"));
        Assert.AreEqual(ExpeditionResolveResult.Success,
            resolver.TryResolveCurrentNode(ExpeditionNodeOutcome.Success, out var second));
        Assert.AreEqual(2, second.ExpeditionElapsedDaysTotal, "elapsed 应累计");
        Assert.AreEqual(0, second.CurrentCarryUnits, "Carry 应耗尽");

        // 第 3 个战斗节点：Supply 不足 → 阻止进入（正确规则表现，不是 Bug；不自动补 Supply）
        Assert.AreEqual(ExpeditionEnterResult.InsufficientSupply, resolver.TryEnterNode("n2"),
            "Supply 不足必须阻止继续进入节点");
    }

    // ==================== 3｜Catch-up：非零 elapsed 真实推进 Village，且出征者保持 Away、不消费 Food ====================

    [Test]
    public void CatchUp_NonZeroElapsed_AdvancesVillage_KeepsAway_AndExcludesAwayFromFood()
    {
        var collection = VillageV4TestKit.NewPlayerCollection();
        var hero = VillageV4TestKit.AddHero(collection);            // 出征者（Away）
        VillageV4TestKit.AddCharacter(collection);                  // 在村消费者

        VillageV4TestKit.EnsureInitialized();
        VillageV4TestKit.BindPersonSource(collection);
        VillageV4TestKit.Resources().AddFood(10);
        VillageV4TestKit.SetExpedition(hero.PersonId);

        var time = VillageV4TestKit.BridgeTime();
        var expected = new VillageTime();
        expected.Restore(new VillageTimeSaveData { Year = time.Year,
            Month = time.Month,
            Day = time.Day,
            SecondsIntoDay = 0f, Speed = (int)VillageTime.VillageTimeSpeed.Normal });
        for (int i = 0; i < 3; i++) expected.TickDay();

        Assert.AreEqual(ExpeditionCatchUpResult.Completed, VillageV4TestKit.RunReturnSession(3));

        Assert.AreEqual(expected.Year, time.Year, "Catch-up 应按 elapsed 推进 Village 年份");
        Assert.AreEqual(expected.Month, time.Month, "Catch-up 应按 elapsed 推进 Village 月份");
        Assert.AreEqual(expected.Day, time.Day, "Catch-up 应按 elapsed 推进 Village 日期");
        Assert.AreEqual(3, VillageV4TestKit.ActiveRecord().DaysSimulated, "DaysSimulated 应等于真实 elapsed");

        CollectionAssert.Contains(ExpeditionIds(), hero.PersonId,
            "Catch-up 期间出征者必须保持 Away（Clear 尚未发生）");

        // 无 Farm/Kitchen 执行 → 无产出；3 天 × 1 名在村 Person × 1 = 3；出征者不消费
        Assert.AreEqual(7, VillageV4TestKit.Resources().Food,
            "出征者不得参与 Catch-up 期间的 Village Food demand");
    }

    // ==================== 4｜HP → Injury 映射（含 Defeat floor 只作用于受损者，OQ3） ====================

    [Test]
    public void HealthMapping_RatioAndDefeatFloor_AreTestRules()
    {
        Assert.AreEqual("Normal", VillageV4TestKit.ResolveInjuryName(30, 30, RunEndMode.SafeReturn));
        Assert.AreEqual("LightInjury", VillageV4TestKit.ResolveInjuryName(15, 30, RunEndMode.SafeReturn));
        Assert.AreEqual("HeavyInjury", VillageV4TestKit.ResolveInjuryName(7, 30, RunEndMode.SafeReturn));
        Assert.AreEqual("HeavyInjury", VillageV4TestKit.ResolveInjuryName(0, 30, RunEndMode.SafeReturn));

        // Defeat floor：只对最终 HP < MaxHP 的 Person 生效（满 HP 不因队伍 Defeat 自动受伤）
        Assert.AreEqual("Normal", VillageV4TestKit.ResolveInjuryName(30, 30, RunEndMode.Defeat));
        Assert.AreEqual("LightInjury", VillageV4TestKit.ResolveInjuryName(29, 30, RunEndMode.Defeat));
        Assert.AreEqual("HeavyInjury", VillageV4TestKit.ResolveInjuryName(5, 30, RunEndMode.Defeat));

        // ratio 恰好等于阈值 → 归入更重一级（Test 边界语义）
        Assert.AreEqual("LightInjury", VillageV4TestKit.ResolveInjuryName(15, 30, RunEndMode.Retreat));
        Assert.AreEqual("HeavyInjury", VillageV4TestKit.ResolveInjuryName(7, 30, RunEndMode.Retreat));

        // 非法 MaxHP → 不降级（Normal）
        Assert.AreEqual("Normal", VillageV4TestKit.ResolveInjuryName(5, 0, RunEndMode.Defeat));
    }

    // ==================== 5｜Return Commit：写长期 Injury + Character Experience；Hero 不写；exactly-once ====================

    [Test]
    public void ReturnCommit_WritesInjuryAndCharacterExperience_NotHeroLifeHistory_ExactlyOnce()
    {
        var collection = VillageV4TestKit.NewPlayerCollection();
        var hero = VillageV4TestKit.AddHero(collection);
        var character = VillageV4TestKit.AddCharacter(collection);

        VillageV4TestKit.EnsureInitialized();
        VillageV4TestKit.BindPersonSource(collection);
        VillageV4TestKit.SetExpedition(character.PersonId, hero.PersonId);

        var run = new RunState();
        run.GetOrCreatePerson(character.PersonId, "战士甲", 30);
        run.GetOrCreatePerson(hero.PersonId, "青鹿", 30);
        run.GetPerson(character.PersonId).CurrentHP = 5;   // ratio ≈ 0.17 → HeavyInjury
        run.GetPerson(hero.PersonId).CurrentHP = 20;       // ratio ≈ 0.67 → Normal（SafeReturn 无 floor）

        Assert.AreEqual(ExpeditionCatchUpResult.Completed, VillageV4TestKit.RunReturnSession(1));
        var record = VillageV4TestKit.ActiveRecord();

        object result = VillageV4TestKit.TryCommitReturnConsequence(
            record, run, RunEndMode.SafeReturn, VillageV4TestKit.BridgeTime(), collection);

        Assert.AreEqual(false, (bool)VillageV4TestKit.Get(result, "Rejected"), "Catch-up 完成后应允许提交");
        Assert.AreEqual(1, (int)VillageV4TestKit.Get(result, "HealthWritten"), "只应有受损者被写入");
        Assert.AreEqual(1, (int)VillageV4TestKit.Get(result, "ExperienceWritten"), "Character 应有 1 条远征经历");

        object characterObj = VillageV4TestKit.OwnedCharacters(collection)[0];
        object heroObj = VillageV4TestKit.OwnedHeroes(collection)[0];

        Assert.AreEqual("HeavyInjury", VillageV4TestKit.Get(characterObj, "Injury").ToString(),
            "Run HP 结果应写为长期 Injury");
        Assert.AreEqual("Normal", VillageV4TestKit.Get(heroObj, "Injury").ToString(),
            "未受损者不应被写入");

        var history = (PersonLifeHistory)VillageV4TestKit.Get(characterObj, "LifeHistory");
        Assert.AreEqual(1, history.CountExpeditionReturned(), "Character 应记录 1 条 ExpeditionReturned");
        Assert.AreEqual(0, (int)VillageV4TestKit.Get(history.Events[0], "Outcome"),
            "Outcome = SafeReturn（枚举序 0）");
        var returnTime = VillageV4TestKit.BridgeTime();
        Assert.AreEqual(returnTime.Year, (int)VillageV4TestKit.Get(history.Events[0], "Year"),
            "经历日期应为回到村庄当日（年）");
        Assert.AreEqual(returnTime.Month, (int)VillageV4TestKit.Get(history.Events[0], "Month"),
            "经历日期应为回到村庄当日（月）");
        Assert.AreEqual(returnTime.Day, (int)VillageV4TestKit.Get(history.Events[0], "Day"),
            "经历日期应为回到村庄当日（日）");
        Assert.IsNull(heroObj.GetType().GetProperty("LifeHistory"),
            "OQ1：Hero 不得新增 LifeHistory");

        // exactly-once：同一会话重复提交不再写入
        object again = VillageV4TestKit.TryCommitReturnConsequence(
            record, run, RunEndMode.SafeReturn, VillageV4TestKit.BridgeTime(), collection);
        Assert.AreEqual(0, (int)VillageV4TestKit.Get(again, "HealthWritten"));
        Assert.AreEqual(0, (int)VillageV4TestKit.Get(again, "ExperienceWritten"));
        Assert.AreEqual(1, history.CountExpeditionReturned(), "不得重复追加经历");
        Assert.AreEqual("HeavyInjury", VillageV4TestKit.Get(characterObj, "Injury").ToString(), "Injury 不得恶化/重复");
    }

    // ==================== 6｜冻结顺序：Catch-up 未完成前不得提交（失败不留半事务长期结果） ====================

    [Test]
    public void ReturnCommit_RejectedBeforeCatchUpComplete_ThenSucceedsAfter()
    {
        var collection = VillageV4TestKit.NewPlayerCollection();
        var character = VillageV4TestKit.AddCharacter(collection);

        VillageV4TestKit.EnsureInitialized();
        VillageV4TestKit.BindPersonSource(collection);
        VillageV4TestKit.SetExpedition(character.PersonId);

        var run = new RunState();
        run.GetOrCreatePerson(character.PersonId, "战士甲", 30);
        run.GetPerson(character.PersonId).CurrentHP = 5;

        VillageV4TestKit.SetElapsedDays(2);
        Assert.IsTrue(VillageV4TestKit.BeginReturn(2));
        Assert.IsTrue(VillageV4TestKit.MarkSettled());

        object rejected = VillageV4TestKit.TryCommitReturnConsequence(
            VillageV4TestKit.ActiveRecord(), run, RunEndMode.SafeReturn,
            VillageV4TestKit.BridgeTime(), collection);
        Assert.AreEqual(true, (bool)VillageV4TestKit.Get(rejected, "Rejected"),
            "Catch-up 未完成必须拒绝提交（避免半事务长期结果）");
        Assert.AreEqual(0, (int)VillageV4TestKit.Get(rejected, "HealthWritten"));

        object characterObj = VillageV4TestKit.OwnedCharacters(collection)[0];
        var history = (PersonLifeHistory)VillageV4TestKit.Get(characterObj, "LifeHistory");
        Assert.AreEqual("Normal", VillageV4TestKit.Get(characterObj, "Injury").ToString(),
            "拒绝时不得写入任何长期状态");
        Assert.AreEqual(0, history.CountExpeditionReturned());

        // Catch-up 成功后才允许提交（冻结顺序）
        Assert.AreEqual(ExpeditionCatchUpResult.Completed, VillageV4TestKit.RunCatchUp());
        object accepted = VillageV4TestKit.TryCommitReturnConsequence(
            VillageV4TestKit.ActiveRecord(), run, RunEndMode.SafeReturn,
            VillageV4TestKit.BridgeTime(), collection);
        Assert.AreEqual(false, (bool)VillageV4TestKit.Get(accepted, "Rejected"));
        Assert.AreEqual("HeavyInjury", VillageV4TestKit.Get(characterObj, "Injury").ToString());
        Assert.AreEqual(1, history.CountExpeditionReturned());
    }

    // ==================== 7｜Begin 重建守卫：防 Carry / Health / Experience 二次提交 ====================

    [Test]
    public void BeginExpeditionReturn_RejectsRebuildAfterCatchUpComplete_UntilDiscarded()
    {
        var collection = VillageV4TestKit.NewPlayerCollection();
        var hero = VillageV4TestKit.AddHero(collection);

        VillageV4TestKit.EnsureInitialized();
        VillageV4TestKit.BindPersonSource(collection);
        VillageV4TestKit.SetExpedition(hero.PersonId);

        Assert.AreEqual(ExpeditionCatchUpResult.Completed, VillageV4TestKit.RunReturnSession(2));

        Assert.IsFalse(VillageV4TestKit.BeginReturn(2),
            "存在已越过 Catch-up 且未 Completed 的会话记录时禁止重新 Begin"
            + "（否则 Carry 二次返还 / Health 二次提交 / Experience 二次追加）");

        VillageV4TestKit.DiscardReturnRecord();
        Assert.IsTrue(VillageV4TestKit.BeginReturn(2), "显式 Discard 后应可重新开始会话（文档化恢复路径）");
    }

    // ==================== 8｜Save / Load：Injury 与 Expedition Experience 持久化，SaveVersion 1 ====================

    [Test]
    public void SaveLoad_KeepsInjuryAndExpeditionExperience_SaveVersion1()
    {
        var collection = VillageV4TestKit.NewPlayerCollection();
        VillageV4TestKit.AddCharacter(collection); // char_001（真实模板，可用于 Load 恢复）
        object characterObj = VillageV4TestKit.OwnedCharacters(collection)[0];

        VillageV4TestKit.Set(characterObj, "Injury",
            System.Enum.Parse(VillageV4TestKit.Asm("InjuryStatus"), "HeavyInjury"));
        var history = (PersonLifeHistory)VillageV4TestKit.Get(characterObj, "LifeHistory");
        history.AppendExpeditionReturned(1, 3, 9, 2); // Defeat（枚举序 2）

        Assert.IsTrue(VillageV4TestKit.Save(collection), "Save 应成功");

        VillageV4TestKit.ResetBridge();
        object loaded = VillageV4TestKit.Load();
        Assert.IsNotNull(loaded, "存档应可加载");

        object loadedChar = VillageV4TestKit.OwnedCharacters(loaded)[0];
        Assert.AreEqual("HeavyInjury", VillageV4TestKit.Get(loadedChar, "Injury").ToString(),
            "长期 Injury 应通过既有 Injury 字段持久化（无新 Health DTO）");
        var loadedHistory = (PersonLifeHistory)VillageV4TestKit.Get(loadedChar, "LifeHistory");
        Assert.AreEqual(1, loadedHistory.CountExpeditionReturned(), "远征经历应持久化");
        Assert.AreEqual(2, (int)VillageV4TestKit.Get(loadedHistory.Events[0], "Outcome"),
            "Outcome 应持久化（Defeat=2）");

        string json = File.ReadAllText(VillageV4TestKit.GetSavePath());
        StringAssert.Contains("\"SaveVersion\": 1", json, "SaveVersion 必须保持 1");
    }
}
