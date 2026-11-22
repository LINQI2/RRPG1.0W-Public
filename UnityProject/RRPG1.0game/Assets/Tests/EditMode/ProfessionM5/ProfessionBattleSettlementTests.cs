using System.Collections.Generic;
using NUnit.Framework;

/// <summary>
/// M5 Battle → Pending ProfessionExp 语义（反射调用 Assembly-CSharp BattleSettlement / CharacterInstance）：
///   S1  参战的 Profession Person 获得 Pending ProfessionExp；同队未上阵的 Profession Person = 0；
///   S2  Battle Win / Loss → 相同 Battle ProfessionExp；
///   S3  Run 中 Battle 只写 Pending，不直接修改长期 ProfessionExp；
///   S4  无 Profession 普通 Character 参战 → 不产生 Exp（不 fallback Legacy）；
///   S5  Hero 保持 M4 后 Legacy Run Growth：胜利才获得 Legacy Pending；不得产生 ProfessionExp。
/// </summary>
public class ProfessionBattleSettlementTests
{
    private const int TestBattleProfessionExp = 10; // 与 BattleSettlement.BattleProfessionExp 测试值一致（未冻结）

    private static object NewProfessionCharacter(string characterId, string displayName, int exp)
    {
        object character = ProfessionM5TestKit.NewCharacter(characterId, displayName, 30, 6);
        var pp = ProfessionM5TestKit.NewProgress("profession_test_hunter", 1, exp, new[] { "card_a" }, 0);
        ProfessionM5TestKit.SetActiveProgress(character, pp);
        return character;
    }

    private static object NewRunPersonList(params object[] characters)
    {
        var list = ProfessionM5TestKit.MakeList("CharacterInstance");
        foreach (var c in characters) ProfessionM5TestKit.AddTo(list, c);
        return list;
    }

    private static void SeedRunPersons(RunState run, params object[] instances)
    {
        foreach (var instance in instances)
        {
            string pid = ProfessionM5TestKit.PersonIdOf(instance);
            string display = (string)ProfessionM5TestKit.Get(instance, "DisplayName");
            int maxHp = (int)ProfessionM5TestKit.Get(instance, "MaxHP");
            run.GetOrCreatePerson(pid, display, maxHp);
        }
    }

    private static void Process(object settlement, bool isVictory, object allCharacters,
        List<string> deployedPersonIds, object hero, RunState run)
    {
        var method = settlement.GetType().GetMethod("ProcessSettlement");
        Assert.IsNotNull(method, "BattleSettlement.ProcessSettlement 未找到");
        method.Invoke(settlement, new object[] { isVictory, allCharacters, deployedPersonIds, hero, run });
    }

    private static object NewSettlement()
    {
        return System.Activator.CreateInstance(ProfessionM5TestKit.Asm("BattleSettlement"));
    }

    [Test]
    public void DeployedProfessionPerson_GainsPending_NonDeployedSameProfession_GetsZero()
    {
        // S1/S3 — A 参战 → Pending +10；同队同 Profession 的 B 未上阵 → 0；Battle 不写长期
        object a = NewProfessionCharacter("char_001", "战士", 0);
        object b = NewProfessionCharacter("char_002", "猎人", 0);
        string pidA = ProfessionM5TestKit.PersonIdOf(a);
        string pidB = ProfessionM5TestKit.PersonIdOf(b);

        var run = new RunState();
        SeedRunPersons(run, a, b);

        Process(NewSettlement(), true, NewRunPersonList(a, b), new List<string> { pidA }, null, run);

        Assert.AreEqual(TestBattleProfessionExp, run.GetPerson(pidA).PendingProfessionExp,
            "参战 Profession Person 应获得 Battle ProfessionExp");
        Assert.AreEqual(0, run.GetPerson(pidB).PendingProfessionExp, "随队未上阵的 Person 不得获得 Battle Exp");
        Assert.AreEqual(0, run.GetPerson(pidA).PendingGrowthExp, "Profession Person 不得走 Legacy 通道");
        Assert.AreEqual(0, ProfessionM5TestKit.ActiveProgress(a).ProfessionExp,
            "Battle 结束不得直接写长期 ProfessionExp（只累计 Pending）");
    }

    [Test]
    public void VictoryAndLoss_GiveSameBattleProfessionExp()
    {
        // S2 — Battle Win / Loss → 相同 Battle ProfessionExp（Current Rule：单场胜负经验相同）
        object aWin = NewProfessionCharacter("char_001", "战士", 0);
        object aLoss = NewProfessionCharacter("char_002", "猎人", 0);
        string pidWin = ProfessionM5TestKit.PersonIdOf(aWin);
        string pidLoss = ProfessionM5TestKit.PersonIdOf(aLoss);

        var runWin = new RunState();
        SeedRunPersons(runWin, aWin);
        Process(NewSettlement(), true, NewRunPersonList(aWin), new List<string> { pidWin }, null, runWin);

        var runLoss = new RunState();
        SeedRunPersons(runLoss, aLoss);
        Process(NewSettlement(), false, NewRunPersonList(aLoss), new List<string> { pidLoss }, null, runLoss);

        Assert.AreEqual(runWin.GetPerson(pidWin).PendingProfessionExp,
            runLoss.GetPerson(pidLoss).PendingProfessionExp, "胜负应获得相同的 Battle ProfessionExp");
        Assert.AreEqual(TestBattleProfessionExp, runLoss.GetPerson(pidLoss).PendingProfessionExp);
    }

    [Test]
    public void NoProfessionCharacter_Deployed_GainsNothing_NoLegacyFallback()
    {
        // S4 — 无 Profession 的普通 Character：不产生 ProfessionExp、不自动 Formation、不 fallback 到 Legacy Exp
        object c = ProfessionM5TestKit.NewCharacter("char_001", "普通队员", 30, 6);
        string pidC = ProfessionM5TestKit.PersonIdOf(c);
        Assert.IsNull(ProfessionM5TestKit.ActiveProgress(c), "测试人物应为无 Profession 状态");

        var run = new RunState();
        SeedRunPersons(run, c);
        Process(NewSettlement(), true, NewRunPersonList(c), new List<string> { pidC }, null, run);

        Assert.AreEqual(0, run.GetPerson(pidC).PendingProfessionExp, "无 Profession 不产生 PendingProfessionExp");
        Assert.AreEqual(0, run.GetPerson(pidC).PendingGrowthExp, "无 Profession 不 fallback 到 Legacy Exp");
    }

    [Test]
    public void Hero_KeepsLegacyRunGrowth_VictoryOnly_NoProfessionExp()
    {
        // S5 — Hero Legacy Run Growth：胜利 → Legacy Pending；失败 → 不获得；永远不产生 ProfessionExp
        object hero = ProfessionM5TestKit.NewHero("hero_001", "青鹿", 35);
        string heroPid = ProfessionM5TestKit.PersonIdOf(hero);

        var runWin = new RunState();
        SeedRunPersons(runWin, hero);
        Process(NewSettlement(), true, NewRunPersonList(), new List<string>(), hero, runWin);
        Assert.AreEqual(10, runWin.GetPerson(heroPid).PendingGrowthExp, "Hero 胜利 → Legacy Pending +BaseExp");
        Assert.AreEqual(0, runWin.GetPerson(heroPid).PendingProfessionExp, "Hero 不产生 ProfessionExp");

        var runLoss = new RunState();
        SeedRunPersons(runLoss, hero);
        Process(NewSettlement(), false, NewRunPersonList(), new List<string>(), hero, runLoss);
        Assert.AreEqual(0, runLoss.GetPerson(heroPid).PendingGrowthExp, "Hero 失败 → 不获得 Legacy 经验（M4 语义不变）");
        Assert.AreEqual(0, runLoss.GetPerson(heroPid).PendingProfessionExp, "Hero 失败也不产生 ProfessionExp");
    }
}
