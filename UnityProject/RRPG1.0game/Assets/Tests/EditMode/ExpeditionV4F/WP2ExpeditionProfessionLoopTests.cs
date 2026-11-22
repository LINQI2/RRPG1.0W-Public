using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;

/// <summary>
/// WP-2 Current（Expedition Facts → Persistent Person State）产品事务链验收测试（EditMode）。
///
/// 复用：V4FTestKit（V4F 产品 Return 冻结顺序）+ Profession.Runtime（ProfessionProgress 强类型）
///       + BattleSettlement.ProcessSettlement（与实际 BattleUI 战后结算同一事实入口）。
///
/// 驱动链（与当前产品 Authority 完全一致，不重构）：
///   Battle 事实（ProcessSettlement，实际参战 + 有 Active Profession 的普通 Character）
///   → RunState.PendingProfessionExp（按 PersonId）
///   → 产品 Return 事务（= ExpeditionUI.FinalizeReturn 冻结顺序，与 ExpeditionReturnOrderSaveTests T14 同一 seam）：
///       BeginExpeditionReturn → AdventureManager.SettleReturn（CommitPendingProfessionExp）
///       → MarkExpeditionReturnSettled → RunReturnCatchUp
///       → ClearExpeditionPersonIds → 唯一最终 Return Save → CompleteExpeditionReturnSave
///   → Save → Load
///
/// 断言：ProfessionExp 恰一次增加；Pending 成功 Commit 后被 ack / 清理；
///       重复 Return / 重复 Commit +0；Save/Load 后仍存在；Run→Battle→Settlement→Load 同一 PersonId；
///       普通 Character 不写 Legacy Character Exp；SafeReturn 与 Retreat 各覆盖一条。
///
/// 非 GUI 边界：本文件不实例化 ExpeditionUI / BattleUI（MonoBehaviour），产品 UI 方法
/// （OnReturnToTown / OnReturnFromBattle → FinalizeReturn）的 GUI 覆盖由
/// ExpeditionV4FReturnProductPlayTests（PlayMode）承担；两条 seam 相互补充。
/// </summary>
public class WP2ExpeditionProfessionLoopTests
{
    /// <summary>与 BattleSettlement.BattleProfessionExp 测试值一致（Test / Not Frozen）。</summary>
    private const int BattleProfessionExp = 10;

    [SetUp]
    public void SetUp()
    {
        V4FTestKit.ResetBridge();
        V4FTestKit.DeleteSave();
        V4FTestKit.EnsureInitialized();
        V4FTestKit.SetTime(1, 1, 1);
    }

    [TearDown]
    public void TearDown()
    {
        V4FTestKit.ResetBridge();
        V4FTestKit.DeleteSave();
    }

    // ==================== 构造与反射辅助 ====================

    private static ProfessionProgress NewProfessionProgress(string professionId, int level, int exp)
    {
        return new ProfessionProgress
        {
            ProfessionId = professionId,
            ProfessionLevel = level,
            ProfessionExp = exp,
            UnlockedPoolCardIds = new List<string> { "card_a" },
            ProfessionTraitLevel = 1
        };
    }

    /// <summary>建一个含 1 名“已形成 Active Profession”人物 + 显式绑定角色卡的 PlayerCollection。</summary>
    private static object NewCollectionWithProfessionCharacter(out object character, out string personId)
    {
        object collection = V4FTestKit.NewPlayerCollection();
        string cardId = "ccard_wp2_char_001_" + Guid.NewGuid().ToString("N").Substring(0, 8);
        V4FTestKit.AddCharacterWithCard(collection, "char_001", cardId, out character, out personId);
        V4FTestKit.Set(character, "ActiveProfessionProgress", NewProfessionProgress("profession_test_hunter", 1, 0));
        return collection;
    }

    /// <summary>按真实 PersonId 在（当前或 Load 后）集合中查找人物实例。</summary>
    private static object FindCharacterByPersonId(object collection, string personId)
    {
        foreach (var c in (IList)V4FTestKit.OwnedCharacters(collection))
        {
            if (c != null && (string)V4FTestKit.Get(c, "PersonId") == personId) return c;
        }
        return null;
    }

    /// <summary>用“该人物自己的卡”作为出征名单构造产品 AdventureManager（同构 Run 播种）。</summary>
    private static object NewAdventureManagerWithRoster(object collection, object card)
    {
        object charCards = V4FTestKit.MakeList("CharacterCardData");
        ((IList)charCards).Add(card);
        object charDatas = V4FTestKit.MakeList("CharacterData");
        object skills = V4FTestKit.MakeList("SkillData");
        object monsters = V4FTestKit.MakeList("MonsterDefinition");

        var advType = V4FTestKit.Asm("AdventureManager");
        var ctor = advType.GetConstructor(new[]
        {
            V4FTestKit.Asm("PlayerCollection"),
            charCards.GetType(),
            V4FTestKit.Asm("HeroInstance"),
            charDatas.GetType(),
            skills.GetType(),
            monsters.GetType(),
            V4FTestKit.Asm("RunConfig")
        });
        Assert.IsNotNull(ctor, "AdventureManager 构造函数未找到");
        return ctor.Invoke(new object[] { collection, charCards, null, charDatas, skills, monsters, null });
    }

    private static object NewSettlement()
    {
        return V4FTestKit.NewAsm("BattleSettlement");
    }

    private static void ProcessBattleVictory(object settlement, object allCharacters,
        List<string> deployedPersonIds, RunState run)
    {
        var method = settlement.GetType().GetMethod("ProcessSettlement", V4FTestKit.F);
        Assert.IsNotNull(method, "BattleSettlement.ProcessSettlement 未找到");
        method.Invoke(settlement, new object[] { true, allCharacters, deployedPersonIds, null, run });
    }

    private static ProfessionProgress ActiveProgressOf(object character)
    {
        return (ProfessionProgress)V4FTestKit.Get(character, "ActiveProfessionProgress");
    }

    // ==================== WP-2 测试 ====================

    [Test]
    public void WP2_SafeReturn_ProductChain_ProfessionExp_ExactlyOnce_NoLegacyWrite_PersistsAcrossLoad()
    {
        RunProductChain(RunEndMode.SafeReturn);
    }

    [Test]
    public void WP2_Retreat_ProductChain_ProfessionExp_FullyPreserved_NoLegacyWrite_PersistsAcrossLoad()
    {
        RunProductChain(RunEndMode.Retreat);
    }

    /// <summary>取集合卡册中模板 char_001 的 owned 卡（单人物测试集合，即该人物自己的卡）。</summary>
    private static object FindOwnedCardByTemplate(object collection, string templateId)
    {
        foreach (var c in (IList)V4FTestKit.OwnedCharacterCards(collection))
        {
            if (c != null && (string)V4FTestKit.Get(c, "CharacterId") == templateId) return c;
        }
        Assert.Fail("卡册中未找到模板 " + templateId + " 的 owned 卡");
        return null;
    }

    /// <summary>
    /// WP-2 产品事务链（SafeReturn / Retreat 共用）：Battle 事实 → Pending → 产品 Return 冻结顺序
    /// → Commit → 唯一最终 Save → Load。断言 Exactly-Once、Pending ack、Legacy 零写入、PersonId 稳定。
    /// </summary>
    private void RunProductChain(RunEndMode mode)
    {
        object collection = NewCollectionWithProfessionCharacter(out object character, out string personId);
        object card = FindOwnedCardByTemplate(collection, "char_001");

        // 前置：Active Profession 存在（exp 0）；Legacy Character Exp 为 0
        ProfessionProgress before = ActiveProgressOf(character);
        Assert.IsNotNull(before, "人物应已有 Active Profession");
        Assert.AreEqual(0, before.ProfessionExp);
        Assert.AreEqual(0, (int)V4FTestKit.Get(character, "Exp"), "前置 Legacy Character Exp 必须为 0");

        // 离村前基线 Save（用于“唯一最终 Return Save”的 hash 对比）
        Assert.IsTrue(V4FTestKit.Save(collection), "离村前 Save 应成功");
        string baseHash = V4FTestKit.SaveHash();

        // 产品出征登记 + 产品 AdventureManager（真实 PersonId 播种 Run）
        V4FTestKit.SetExpedition(new[] { personId });
        object adventure = NewAdventureManagerWithRoster(collection, card);
        var run = V4FTestKit.RunStateOf(adventure);
        Assert.IsTrue(run.Persons.ContainsKey(personId), "Run 播种必须含该真实 PersonId");
        Assert.AreEqual(1, run.Persons.Count);

        // 实际参加 Battle → 产生 PendingProfessionExp（真实事实入口 = BattleSettlement.ProcessSettlement）
        object allCharacters = V4FTestKit.MakeList("CharacterInstance");
        ((IList)allCharacters).Add(character);
        ProcessBattleVictory(NewSettlement(), allCharacters, new List<string> { personId }, run);

        Assert.AreEqual(BattleProfessionExp, run.GetPerson(personId).PendingProfessionExp,
            "实际参战的 Profession Person 应获得 Pending ProfessionExp");
        Assert.AreEqual(0, ActiveProgressOf(character).ProfessionExp, "Battle 只写 Pending，不写长期");
        Assert.AreEqual(0, (int)V4FTestKit.Get(character, "Exp"), "Battle 不得写 Legacy Character Exp");

        // ---- 产品 Return 冻结顺序（= ExpeditionUI.FinalizeReturn 同序 seam） ----
        Assert.IsTrue(V4FTestKit.BeginReturn(0), "BeginExpeditionReturn 应成功");
        bool saveIndication = (bool)V4FTestKit.Call(adventure, "SettleReturn", mode);
        Assert.IsTrue(saveIndication,
            mode + " 发生 ProfessionExp 长期写回 → 按旧 EndRun 规则应指示 Save（SettleReturn 自身不 Save）");
        Assert.AreEqual(baseHash, V4FTestKit.SaveHash(), "Settlement 之后不得发生文件 Save（最终 Return Save 唯一）");
        Assert.AreEqual(0, run.GetPerson(personId).PendingProfessionExp, "Commit 成功 ack → Pending 清理");
        Assert.AreEqual(BattleProfessionExp, ActiveProgressOf(character).ProfessionExp,
            "SettleReturn → CommitPendingProfessionExp → ProfessionProgress.ProfessionExp 恰一次增加");
        Assert.AreEqual(0, (int)V4FTestKit.Get(character, "Exp"), "Commit 不得 fallback 写 Legacy Character Exp");
        Assert.IsFalse((bool)V4FTestKit.Get(adventure, "IsActive"), "Settlement 后 Run 结束（IsActive=false）");
        Assert.IsFalse(run.HasPendingRewards, "Settlement 后无残留待结算");

        Assert.IsTrue(V4FTestKit.MarkSettled(), "Settlement 成功后置 ReturnSettled");
        var catchUp = V4FTestKit.RunCatchUp();
        Assert.AreEqual(ExpeditionCatchUpResult.AlreadyCaughtUp, catchUp, "elapsed=0 → Return Catch-up 空跑完成");
        Assert.AreEqual(baseHash, V4FTestKit.SaveHash(), "Catch-up 期间不得发生文件 Save");
        V4FTestKit.ClearExpedition();
        Assert.AreEqual(0, V4FTestKit.ExpeditionIds().Count, "Clear 后远征登记清空");
        Assert.AreEqual(baseHash, V4FTestKit.SaveHash(), "Clear Occupancy 不得触发 Save");

        // 唯一最终 Return Save
        Assert.IsTrue(V4FTestKit.Save(collection), "最终 Return Save 应成功");
        V4FTestKit.CompleteSave(true);
        var record = V4FTestKit.ActiveRecord();
        Assert.IsTrue(record.Completed, "最终 Return Save 成功后才置 Completed");
        Assert.AreEqual(ExpeditionReturnPhase.Completed, record.Phase);
        Assert.AreNotEqual(baseHash, V4FTestKit.SaveHash(), "最终 Return Save 必须改变存档内容");
        string finalHash = V4FTestKit.SaveHash();

        // 重复 Return / 重复 Commit：不得再次增加经验、不得产生第二次落盘
        Assert.IsFalse(V4FTestKit.BeginReturn(0), "Completed 记录 → 重复 BeginExpeditionReturn no-op");
        Assert.AreEqual(BattleProfessionExp, ActiveProgressOf(character).ProfessionExp, "重复 Return 不得再次增长");
        Assert.AreEqual(0, run.GetPerson(personId).PendingProfessionExp, "重复 Return 无第二次 Pending 入账");

        // 直接重放 SettleReturn（Commit 幂等）：+0，Legacy 仍零写入
        V4FTestKit.Call(adventure, "SettleReturn", mode);
        Assert.AreEqual(BattleProfessionExp, ActiveProgressOf(character).ProfessionExp, "重复 Commit 必须 +0");
        Assert.AreEqual(0, (int)V4FTestKit.Get(character, "Exp"), "重复 Commit 不得写 Legacy Character Exp");
        Assert.AreEqual(0, run.GetPerson(personId).PendingProfessionExp);
        Assert.AreEqual(finalHash, V4FTestKit.SaveHash(), "重复 Return / 重复 Commit 不产生第二次落盘");

        // Save → Load：同一 PersonId，ProfessionExp 稳定存在
        V4FTestKit.ResetBridge(); // 模拟重启（保留存档文件）
        object loaded = V4FTestKit.Load();
        Assert.IsNotNull(loaded, "Load 应成功");
        object loadedCharacter = FindCharacterByPersonId(loaded, personId);
        Assert.IsNotNull(loadedCharacter, "Load 后同一 PersonId 必须存在（同一人物）");
        ProfessionProgress restored = ActiveProgressOf(loadedCharacter);
        Assert.AreEqual(personId, (string)V4FTestKit.Get(loadedCharacter, "PersonId"), "PersonId 跨 Save/Load 稳定");
        Assert.AreEqual(BattleProfessionExp, restored.ProfessionExp, "Load 后 ProfessionExp 必须保留");
        Assert.AreEqual("profession_test_hunter", restored.ProfessionId);
        Assert.AreEqual(1, restored.ProfessionLevel);
        Assert.AreEqual(0, (int)V4FTestKit.Get(loadedCharacter, "Exp"), "Load 后 Legacy Character Exp 仍为 0");
    }
}

