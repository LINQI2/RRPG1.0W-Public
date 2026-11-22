using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// M5 Run Card Reward Pool 显式迁移测试（真实宿主 + UnlockedPoolRewardProvider）：
///   R1  Profession Character → Run Card Reward 候选只读其当前 Profession Unlocked Pool；
///   R2  Hero → Run Card Reward 仍读 Legacy Hero Unlocked Pool；
///   R3  无 Profession 普通 Character → 仍读自身 Legacy Unlocked Pool（非 Profession 语义不变）；
///   R4  Profession Character 空 Profession Pool → 无角色候选（不 Demo 兜底、不 fallback Legacy）；
///   R5  Provider 找不到长期人物 → 才回退 DemoCardPool（临时内容种子）。
/// 经 GrowthAuthority 窄解析，不通过修改 IPersonGrowth 全局语义达成。
/// </summary>
public class ProfessionRewardPoolMigrationTests
{
    private static BattleCardData NewCard(string cardId, BattleCardKind kind = BattleCardKind.Character)
    {
        var c = ScriptableObject.CreateInstance<BattleCardData>();
        c.CardId = cardId;
        c.DisplayName = cardId;
        c.CardKind = kind;
        return c;
    }

    [Test]
    public void ProfessionCharacter_Candidates_UseProfessionUnlockedPool_NotLegacy()
    {
        // R1 — Profession Character 的 Run 卡牌奖励 = 该 Person 当前 Profession Unlocked Pool
        object character = ProfessionM5TestKit.NewCharacter("char_001", "职业者", 30, 6);
        string personId = ProfessionM5TestKit.PersonIdOf(character);
        ProfessionM5TestKit.Set(character, "UnlockedPoolCardIds", new List<string> { "b_legacy_x" });
        var pp = ProfessionM5TestKit.NewProgress("profession_test_hunter", 1, 0,
            new[] { "b_prof_x", "b_prof_y" }, 0);
        ProfessionM5TestKit.SetActiveProgress(character, pp);

        var run = new RunState();
        run.GetOrCreatePerson(personId, "职业者", 30);
        var provider = new UnlockedPoolRewardProvider(
            pid => pid == personId ? (IPersonGrowth)character : null);

        var candidates = provider.BuildCandidates(run, new List<BattleCardData>
        {
            NewCard("b_prof_x"), NewCard("b_prof_y"), NewCard("b_legacy_x"), NewCard("common", BattleCardKind.Common)
        });

        var ids = candidates.Where(c => c.PersonId == personId).Select(c => c.Template.CardId).ToList();
        CollectionAssert.Contains(ids, "b_prof_x", "Profession Unlocked Pool 内牌必须进入候选");
        CollectionAssert.Contains(ids, "b_prof_y", "Profession Unlocked Pool 内牌必须进入候选");
        CollectionAssert.DoesNotContain(ids, "b_legacy_x", "不得读 Legacy Unlocked Pool（不迁移 IPersonGrowth 全局语义）");
        Assert.IsTrue(candidates.Any(c => c.IsCommon && c.Template.CardId == "common"), "通用牌始终可奖励");
    }

    [Test]
    public void Hero_Candidates_StillUseLegacyHeroUnlockedPool()
    {
        // R2 — Hero Run Card Reward 仍读取 Legacy Hero Unlocked Pool（Hero 不进入 Profession 迁移）
        object hero = ProfessionM5TestKit.NewHero("hero_001", "青鹿", 35);
        string heroPid = ProfessionM5TestKit.PersonIdOf(hero);
        ProfessionM5TestKit.Set(hero, "UnlockedPoolCardIds", new List<string> { "b_hero_x" });

        var run = new RunState();
        run.GetOrCreatePerson(heroPid, "青鹿", 35);
        var provider = new UnlockedPoolRewardProvider(
            pid => pid == heroPid ? (IPersonGrowth)hero : null);

        var candidates = provider.BuildCandidates(run, new List<BattleCardData>
        {
            NewCard("b_hero_x"), NewCard("b_other")
        });

        Assert.IsTrue(candidates.Any(c => c.PersonId == heroPid && c.Template.CardId == "b_hero_x"),
            "Hero Run 奖励候选必须仍来自 Legacy Hero Unlocked Pool");
    }

    [Test]
    public void NoProfessionCharacter_Candidates_StillUseOwnLegacyUnlockedPool()
    {
        // R3 — 无 Profession 普通 Character：Run 奖励仍读自身 Legacy Unlocked Pool（不因 M5 受影响）
        object character = ProfessionM5TestKit.NewCharacter("char_001", "无职业者", 30, 6);
        string personId = ProfessionM5TestKit.PersonIdOf(character);
        Assert.IsNull(ProfessionM5TestKit.ActiveProgress(character), "前置：无 Profession");
        ProfessionM5TestKit.Set(character, "UnlockedPoolCardIds", new List<string> { "b_char_x" });

        var run = new RunState();
        run.GetOrCreatePerson(personId, "无职业者", 30);
        var provider = new UnlockedPoolRewardProvider(
            pid => pid == personId ? (IPersonGrowth)character : null);

        var candidates = provider.BuildCandidates(run, new List<BattleCardData> { NewCard("b_char_x") });
        Assert.IsTrue(candidates.Any(c => c.PersonId == personId && c.Template.CardId == "b_char_x"),
            "无 Profession 角色的 Run 奖励保持既有 Legacy 池语义");
    }

    [Test]
    public void ProfessionCharacter_EmptyProfessionPool_NoCandidates_NoDemoOrLegacyFallback()
    {
        // R4 — Profession Character 的 Profession Pool 为空 → 无该人物角色候选；
        //      不得 fallback 到 Legacy Pool、不得用 Demo 临时内容替代
        object character = ProfessionM5TestKit.NewCharacter("char_001", "职业者", 30, 6);
        string personId = ProfessionM5TestKit.PersonIdOf(character);
        ProfessionM5TestKit.Set(character, "UnlockedPoolCardIds", new List<string> { "b_legacy_x" });
        var pp = ProfessionM5TestKit.NewProgress("profession_test_hunter", 1, 0,
            new List<string>(), 0); // 空 Profession Pool
        ProfessionM5TestKit.SetActiveProgress(character, pp);

        var run = new RunState();
        run.GetOrCreatePerson(personId, "职业者", 30);
        var provider = new UnlockedPoolRewardProvider(
            pid => pid == personId ? (IPersonGrowth)character : null);

        var candidates = provider.BuildCandidates(run, new List<BattleCardData>
        {
            NewCard("b_legacy_x"), NewCard("b_prof_x"), NewCard("common", BattleCardKind.Common)
        });

        Assert.IsFalse(candidates.Any(c => c.PersonId == personId),
            "空 Profession Pool → 该 Person 不得产生角色候选（不 Demo 兜底、不 fallback Legacy）");
        Assert.IsTrue(candidates.Any(c => c.IsCommon), "通用牌不受人物池影响");
    }

    [Test]
    public void MissingLongTermPerson_StillFallsBackToDemoPool()
    {
        // R5 — personLookup 找不到长期人物（异常路径）才回退 DemoCardPool（临时内容种子，行为不变）
        var run = new RunState();
        run.GetOrCreatePerson("char_001", "战士", 30); // 模板键 person（Demo 映射可识别）
        var provider = new UnlockedPoolRewardProvider(null);
        var candidates = provider.BuildCandidates(run, new List<BattleCardData> { NewCard("bcard_aim") });
        Assert.IsTrue(candidates.Any(c => c.PersonId == "char_001" && c.Template.CardId == "bcard_aim"),
            "缺失长期人物时按既有语义回退 Demo 临时内容种子");
    }
}
