using System.Collections.Generic;
using NUnit.Framework;

/// <summary>
/// M5 Return 汇合点（AdventureManager.EndRun）+ PlayerRunRewardSink（真实兑现路径）测试：
///   S6  PlayerRunRewardSink.TryAddProfessionExp：只有“Person 存在 + Active Profession 有效 + 成功写入”
///       返回 true；Hero / 无 Profession 普通 Character / 找不到 Person / sink 无法执行返回 false
///       （不 fallback、不猜职业；Commit 依据 bool 决定是否清零）；
///   S7  有限远征完成（SafeReturn）→ Pending ProfessionExp 全量写入当前 Profession Growth；Exactly-Once；
///       不额外给予 Expedition Completion ProfessionExp；
///   S8  Expedition Failure（Defeat）→ ProfessionExp 仍全量保留；金币/建材/Legacy 仍按既有占位损失语义；
///   S9  无限远征主动撤退（Retreat）→ 同样全量保留 ProfessionExp；
///   S10 Save/Load：Return 兑现写回长期后 Save → Load，ProfessionExp 持久保留、不重复兑现
///       （当前 Active Run Return Settlement 的 Exactly-Once；不覆盖 Return 后旧 Checkpoint stale replay）；
///   S11 真实 sink：Person 找不到 → Commit 不清零 Pending（不被误消费）；
///   S12 真实 sink：Person 存在但无 Active Profession → Commit 不清零 Pending（不 fallback Legacy）。
/// </summary>
public class ProfessionRunReturnCommitTests
{
    private const int TestBattleProfessionExp = 10;

    [SetUp]
    public void SetUp()
    {
        ProfessionM5TestKit.DeleteSave();
        ProfessionM5TestKit.ResetBridge();
    }

    [TearDown]
    public void TearDown()
    {
        ProfessionM5TestKit.DeleteSave();
        ProfessionM5TestKit.ResetBridge();
    }

    private static object NewCollectionWithProfessionCharacter(out object character, out string personId)
    {
        object collection = ProfessionM5TestKit.NewPlayerCollection();
        ProfessionM5TestKit.Set(collection, "Gold", 100);
        character = ProfessionM5TestKit.NewCharacter("char_001", "测试人物", 30, 6);
        var pp = ProfessionM5TestKit.NewProgress("profession_test_hunter", 1, 0, new[] { "card_a" }, 0);
        ProfessionM5TestKit.SetActiveProgress(character, pp);
        ProfessionM5TestKit.OwnedCharacters(collection).Add(character);
        personId = ProfessionM5TestKit.PersonIdOf(character);
        return collection;
    }

    private static object NewAdventureManager(object collection, object hero)
    {
        object charCards = ProfessionM5TestKit.MakeList("CharacterCardData");
        object charDatas = ProfessionM5TestKit.MakeList("CharacterData");
        object skills = ProfessionM5TestKit.MakeList("SkillData");
        object monsters = ProfessionM5TestKit.MakeList("MonsterDefinition");

        var advType = ProfessionM5TestKit.Asm("AdventureManager");
        var ctor = advType.GetConstructor(new[]
        {
            ProfessionM5TestKit.Asm("PlayerCollection"),
            charCards.GetType(),
            ProfessionM5TestKit.Asm("HeroInstance"),
            charDatas.GetType(),
            skills.GetType(),
            monsters.GetType(),
            ProfessionM5TestKit.Asm("RunConfig")
        });
        Assert.IsNotNull(ctor, "AdventureManager 构造函数未找到");
        return ctor.Invoke(new object[] { collection, charCards, hero, charDatas, skills, monsters, null });
    }

    private static RunState RunStateOf(object adventure)
    {
        return (RunState)ProfessionM5TestKit.GetPropertyOrField(adventure, "RunState");
    }

    private static void EndRun(object adventure, RunEndMode mode)
    {
        ProfessionM5TestKit.Call(adventure, "EndRun", mode);
    }

    [Test]
    public void Sink_TryAddProfessionExp_AcksOnlyValidProfessionPerson()
    {
        // S6 — 真实 PlayerRunRewardSink.TryAddProfessionExp：
        //      只有 Profession Person 返回 true 并写入 ProfessionProgress.ProfessionExp；
        //      Hero / 无 Profession 普通 Character / 找不到 Person 返回 false（不 fallback、不猜职业）。
        object collection = ProfessionM5TestKit.NewPlayerCollection();
        ProfessionM5TestKit.Set(collection, "Gold", 100);

        object prof = ProfessionM5TestKit.NewCharacter("char_001", "职业者", 30, 6);
        ProfessionM5TestKit.Set(prof, "Exp", 5); // Legacy Exp = A
        ProfessionProgress pp = ProfessionM5TestKit.NewProgress("profession_test_hunter", 1, 0, new[] { "card_a" }, 0);
        ProfessionM5TestKit.SetActiveProgress(prof, pp);
        ProfessionM5TestKit.OwnedCharacters(collection).Add(prof);

        object none = ProfessionM5TestKit.NewCharacter("char_002", "无职业者", 30, 6);
        ProfessionM5TestKit.Set(none, "Exp", 7);
        ProfessionM5TestKit.OwnedCharacters(collection).Add(none);

        object hero = ProfessionM5TestKit.NewHero("hero_001", "青鹿", 35);
        ProfessionM5TestKit.Set(hero, "Exp", 9);
        ProfessionM5TestKit.OwnedHeroes(collection).Add(hero);

        string pidProf = ProfessionM5TestKit.PersonIdOf(prof);
        string pidNone = ProfessionM5TestKit.PersonIdOf(none);
        string pidHero = ProfessionM5TestKit.PersonIdOf(hero);

        var sink = (IRunRewardSink)ProfessionM5TestKit.NewAsm("PlayerRunRewardSink", collection);

        Assert.IsTrue(sink.TryAddProfessionExp(pidProf, 12), "有效 Profession Person 应返回 true");
        Assert.AreEqual(12, pp.ProfessionExp, "TryAddProfessionExp(true) 应写入该 Person 当前 ProfessionGrowth");
        Assert.AreEqual(5, ProfessionM5TestKit.Get(prof, "Exp"), "ProfessionExp 写入不得改 Legacy Exp");

        Assert.IsFalse(sink.TryAddProfessionExp(pidNone, 7), "无 Profession 普通 Character 应返回 false");
        Assert.IsFalse(sink.TryAddProfessionExp(pidHero, 4), "Hero 应返回 false（不进入 Profession 迁移）");
        Assert.IsFalse(sink.TryAddProfessionExp("missing_person", 3), "找不到 Person 应返回 false");
        Assert.IsNull(ProfessionM5TestKit.ActiveProgress(none), "无 Profession 不得被创建/猜测 Profession");
        Assert.AreEqual(7, ProfessionM5TestKit.Get(none, "Exp"), "无 Profession 不得 fallback 到 Legacy");
        Assert.AreEqual(9, ProfessionM5TestKit.Get(hero, "Exp"), "Hero 不进入 Profession 迁移");
        Assert.AreEqual(12, pp.ProfessionExp, "false 分支 no-op，ProfessionExp 保持不变");
    }

    [Test]
    public void Commit_MissingPerson_PendingNotCleared()
    {
        // S11（Correction：Commit Acknowledgement）— 真实 sink：Person 找不到 → TryAddProfessionExp=false，
        //      CommitPendingProfessionExp 不得清零 Pending（不被误消费）。
        object collection = ProfessionM5TestKit.NewPlayerCollection(); // 集合为空
        var sink = (IRunRewardSink)ProfessionM5TestKit.NewAsm("PlayerRunRewardSink", collection);

        var run = new RunState();
        run.AddPendingProfessionExp("missing_person", 10);

        int committed = run.CommitPendingProfessionExp(sink);
        Assert.AreEqual(0, committed, "找不到 Person → 无成功写入");
        Assert.AreEqual(10, run.GetPerson("missing_person").PendingProfessionExp,
            "找不到 Person → Pending 保留（Commit 不得乐观清零）");
    }

    [Test]
    public void Commit_NoProfessionPerson_PendingNotCleared()
    {
        // S12（Correction：Commit Acknowledgement）— 真实 sink：Person 存在但无 Active Profession →
        //      TryAddProfessionExp=false，Commit 不清零 Pending（不 fallback Legacy、不猜测职业）。
        object collection = ProfessionM5TestKit.NewPlayerCollection();
        object none = ProfessionM5TestKit.NewCharacter("char_001", "无职业者", 30, 6);
        ProfessionM5TestKit.Set(none, "Exp", 7);
        ProfessionM5TestKit.OwnedCharacters(collection).Add(none);
        string pidNone = ProfessionM5TestKit.PersonIdOf(none);

        var sink = (IRunRewardSink)ProfessionM5TestKit.NewAsm("PlayerRunRewardSink", collection);

        var run = new RunState();
        run.AddPendingProfessionExp(pidNone, 10);

        int committed = run.CommitPendingProfessionExp(sink);
        Assert.AreEqual(0, committed, "无 Active Profession → 无成功写入");
        Assert.AreEqual(10, run.GetPerson(pidNone).PendingProfessionExp,
            "无 Active Profession → Pending 保留（不得 fallback 到 Legacy）");
        Assert.AreEqual(7, ProfessionM5TestKit.Get(none, "Exp"), "Legacy Exp 不被误写");
    }

    [Test]
    public void SafeReturn_CommitsProfessionExp_ExactlyOnce_NoCompletionBonus()
    {
        // S7 — 有限远征完成（SafeReturn）：Pending ProfessionExp 全量写入；第二次 Return +0；无 Completion 加成
        object collection = NewCollectionWithProfessionCharacter(out object character, out string personId);
        object adventure = NewAdventureManager(collection, null);
        RunState run = RunStateOf(adventure);

        run.AddPendingGold(20);
        run.AddPendingProfessionExp(personId, TestBattleProfessionExp);

        EndRun(adventure, RunEndMode.SafeReturn);
        ProfessionProgress pp = ProfessionM5TestKit.ActiveProgress(character);
        Assert.AreEqual(TestBattleProfessionExp, pp.ProfessionExp, "SafeReturn 应把 Pending ProfessionExp 全量写入");
        Assert.AreEqual(120, (int)ProfessionM5TestKit.Get(collection, "Gold"), "SafeReturn 应兑现 PendingGold");
        Assert.IsFalse(run.HasPendingRewards, "Return 后 Pending 应清空");

        // 不额外给予 Expedition Completion ProfessionExp：写入量恰好等于 Pending 全量
        Assert.AreEqual(TestBattleProfessionExp, pp.ProfessionExp, "完成远征本身不额外发 ProfessionExp");

        // Exactly-Once：重复 Return 不再写长期
        EndRun(adventure, RunEndMode.SafeReturn);
        Assert.AreEqual(TestBattleProfessionExp, pp.ProfessionExp, "重复 Return 不得重复兑现（+0）");
    }

    [Test]
    public void Defeat_CommitsProfessionExp_KeepsOtherPendingLossSemantics()
    {
        // S8 — Expedition Failure（Defeat）：ProfessionExp 完整保留；金币/Legacy 其它 Pending 仍按既有占位损失语义
        object collection = NewCollectionWithProfessionCharacter(out object character, out string personId);
        object adventure = NewAdventureManager(collection, null);
        RunState run = RunStateOf(adventure);

        run.AddPendingGold(20);
        run.AddPendingProfessionExp(personId, TestBattleProfessionExp);
        run.AddPendingGrowthExp("hero_legacy", 10); // Legacy 通道 Pending（失败占位不兑现）

        EndRun(adventure, RunEndMode.Defeat);
        ProfessionProgress pp = ProfessionM5TestKit.ActiveProgress(character);
        Assert.AreEqual(TestBattleProfessionExp, pp.ProfessionExp, "Defeat 也必须完整保留已产生的 ProfessionExp");
        Assert.AreEqual(100, (int)ProfessionM5TestKit.Get(collection, "Gold"),
            "Failure 不得改变金币等其它 Pending 的既有损失语义（占位不兑现，金币不变）");
        Assert.IsFalse(run.HasPendingRewards, "Defeat 结算后 Pending 清空（Profession 已兑现，其它已丢弃）");

        // Exactly-Once：重复 Failure 结算不再写长期
        EndRun(adventure, RunEndMode.Defeat);
        Assert.AreEqual(TestBattleProfessionExp, pp.ProfessionExp, "重复 Failure 结算不得重复兑现（+0）");
    }

    [Test]
    public void Retreat_CommitsProfessionExp_ExactlyOnce()
    {
        // S9 — 无限远征主动撤退（Retreat = Normal Return）：Pending ProfessionExp 全量保留
        object collection = NewCollectionWithProfessionCharacter(out object character, out string personId);
        object adventure = NewAdventureManager(collection, null);
        RunState run = RunStateOf(adventure);

        run.AddPendingProfessionExp(personId, TestBattleProfessionExp);
        run.AddPendingGold(20);

        EndRun(adventure, RunEndMode.Retreat);
        ProfessionProgress pp = ProfessionM5TestKit.ActiveProgress(character);
        Assert.AreEqual(TestBattleProfessionExp, pp.ProfessionExp, "主动撤退也必须完整保留 ProfessionExp");
        Assert.AreEqual(100, (int)ProfessionM5TestKit.Get(collection, "Gold"),
            "主动撤退的金币损失比例未冻结，按既有占位语义不兑现（金币不变）");

        EndRun(adventure, RunEndMode.Retreat);
        Assert.AreEqual(TestBattleProfessionExp, pp.ProfessionExp, "重复 Retreat 不得重复兑现（+0）");
    }

    [Test]
    public void SaveLoad_AfterReturn_PersistsProfessionExp_NoDoubleCommit()
    {
        // S10 — Return 兑现写入长期 → EndRun(SafeReturn) Save → Load：ProfessionExp 持久保留且不重复兑现
        object collection = NewCollectionWithProfessionCharacter(out object character, out string personId);
        object adventure = NewAdventureManager(collection, null);
        RunState run = RunStateOf(adventure);
        run.AddPendingProfessionExp(personId, TestBattleProfessionExp);

        EndRun(adventure, RunEndMode.SafeReturn); // 内部已 Save
        ProfessionProgress pp = ProfessionM5TestKit.ActiveProgress(character);
        Assert.AreEqual(TestBattleProfessionExp, pp.ProfessionExp);

        object loaded = ProfessionM5TestKit.Load();
        Assert.IsNotNull(loaded, "Load 应成功");
        var loadedChars = ProfessionM5TestKit.OwnedCharacters(loaded);
        Assert.AreEqual(1, loadedChars.Count, "Save/Load 应还原该人物");
        object loadedCharacter = loadedChars[0];
        ProfessionProgress restored = ProfessionM5TestKit.ActiveProgress(loadedCharacter);
        Assert.IsNotNull(restored, "Load 后 ProfessionProgress 应存在");
        Assert.AreEqual(TestBattleProfessionExp, restored.ProfessionExp,
            "Save → Load 后 Return 兑现的 ProfessionExp 必须保留");
        Assert.AreEqual(1, restored.ProfessionLevel, "Save → Load 只保留经验写回，不改变等级");
        Assert.AreEqual(personId, ProfessionM5TestKit.PersonIdOf(loadedCharacter), "PersonId 跨 Save/Load 稳定");
    }
}
