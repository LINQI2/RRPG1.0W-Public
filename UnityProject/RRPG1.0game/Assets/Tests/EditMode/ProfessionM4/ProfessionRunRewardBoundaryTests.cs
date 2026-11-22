using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// M4/M5 迁移边界测试 —— Run/Reward/StarterDeck 产品组件在 M5 Run/Return Growth Semantic Migration 后：
///   Test E1  PlayerRunRewardSink.AddGrowthExp（Legacy 通道）保持 M4 语义（写 Legacy Character Exp）；
///   Test E2  UnlockedPoolRewardProvider（Run 卡牌奖励）：Profession Character 已迁移为读取
///            该 Person 当前 Profession Unlocked Pool（M5），不再观察 Legacy UnlockedPool；
///   Test E3  StarterDeckBuilder 仍读 Character-owned Starter（与 Profession 无关，M5 Starter 不迁移）。
/// </summary>
public class ProfessionRunRewardBoundaryTests
{
    private static object NewCharacter()
    {
        return ProfessionM4TestKit.NewCharacter("char_001", "战士", 30, 6);
    }

    private static BattleCardData NewCard(string cardId)
    {
        var c = ScriptableObject.CreateInstance<BattleCardData>();
        c.CardId = cardId;
        c.DisplayName = cardId;
        c.CardKind = BattleCardKind.Character;
        return c;
    }

    [Test]
    public void RunRewardSink_AddGrowthExp_WritesLegacyExp_NotProfessionExp()
    {
        // Test E1 — PlayerRunRewardSink.AddGrowthExp 是 Legacy 通道：即使带 Profession 也写 Legacy Exp
        //（M5 迁移后 Profession Character 的 Run/Return 经验走 TryAddProfessionExp 通道，
        //   见 ProfessionRunReturnCommitTests.Sink_TryAddProfessionExp_AcksOnlyValidProfessionPerson）。
        object collection = ProfessionM4TestKit.NewPlayerCollection();
        object character = NewCharacter();
        string personId = ProfessionM4TestKit.PersonIdOf(character);
        ProfessionM4TestKit.Set(character, "Exp", 5); // Legacy Exp = A

        ProfessionProgress pp = ProfessionM4TestKit.NewProgress("profession_test_hunter", 2, 7,
            new[] { "card_a" }, 1); // ProfessionExp = B
        ProfessionM4TestKit.SetActiveProgress(character, pp);
        ProfessionM4TestKit.OwnedCharacters(collection).Add(character);

        var sink = (IRunRewardSink)ProfessionM4TestKit.NewAsm("PlayerRunRewardSink", collection);
        sink.AddGrowthExp(personId, 3); // X = 3（Legacy 通道）

        Assert.AreEqual(8, ProfessionM4TestKit.Get(character, "Exp"),
            "AddGrowthExp（Legacy 通道）写 Legacy Character Exp（Legacy A + X）");
        Assert.AreEqual(7, pp.ProfessionExp,
            "AddGrowthExp（Legacy 通道）不得写入 ProfessionExp（M5：ProfessionExp 由 AddProfessionExp 通道写入）");
    }

    [Test]
    public void UnlockedPoolRewardProvider_MigratedToProfessionPool_ForProfessionCharacter()
    {
        // Test E2 — M5 迁移：带 Profession 的普通 Character：Run 卡牌奖励候选必须观察其 Profession Unlocked Pool，
        // 不再观察 Legacy UnlockedPool（不能通过修改 IPersonGrowth 全局语义达成，经 GrowthAuthority 窄解析）。
        object character = NewCharacter();
        string personId = ProfessionM4TestKit.PersonIdOf(character);
        ProfessionM4TestKit.Set(character, "UnlockedPoolCardIds",
            new List<string> { "b_legacy_a" }); // Legacy Pool = [A]
        ProfessionProgress pp = ProfessionM4TestKit.NewProgress("profession_test_hunter", 2, 0,
            new[] { "b_prof_x" }, 1); // Profession Pool = [B]
        ProfessionM4TestKit.SetActiveProgress(character, pp);

        var provider = new UnlockedPoolRewardProvider(
            pid => pid == personId ? (IPersonGrowth)character : null);

        var runState = new RunState();
        runState.GetOrCreatePerson(personId, "战士", 30);

        var templates = new List<BattleCardData> { NewCard("b_legacy_a"), NewCard("b_prof_x") };
        var candidates = provider.BuildCandidates(runState, templates);

        var ids = candidates
            .Where(c => c.PersonId == personId)
            .Select(c => c.Template.CardId)
            .ToList();
        CollectionAssert.Contains(ids, "b_prof_x",
            "M5：Profession Character 的 Run 奖励候选必须读取其 Profession Unlocked Pool");
        CollectionAssert.DoesNotContain(ids, "b_legacy_a",
            "M5：Profession Character 的 Run 奖励候选不得再读 Legacy UnlockedPool");
    }

    [Test]
    public void StarterDeckBuilder_StillReadsCharacterOwnedStarter_ForProfessionCharacter()
    {
        // Test E3 — 带 Profession 的普通 Character：Run Deck 初始条目仍来自 Character 自己的 StarterSlots
        //（M5 Starter 不迁移，StarterDeckBuilder 行为不变）。
        object character = NewCharacter();
        string personId = ProfessionM4TestKit.PersonIdOf(character);
        var g = (IPersonGrowth)character;
        g.StarterSlots.Add(new StarterSlotEntry("bcard_sword", 1));

        ProfessionProgress pp = ProfessionM4TestKit.NewProgress("profession_test_hunter", 2, 0,
            new[] { "card_a" }, 1);
        ProfessionM4TestKit.SetActiveProgress(character, pp);

        var entries = StarterDeckBuilder.BuildStarterEntries(
            new IPersonGrowth[] { g },
            new List<BattleCardData> { NewCard("bcard_sword"), NewCard("bcard_aim") });

        Assert.AreEqual(1, entries.Count(e => e.PersonId == personId), "StarterDeck 应只含人物 Starter 内容");
        var entry = entries.First(e => e.PersonId == personId);
        Assert.AreEqual("bcard_sword", entry.Template.CardId,
            "StarterDeckBuilder 仍读 Character-owned Starter（与 Profession 无关）");
    }
}
