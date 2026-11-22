using System.Collections.Generic;
using NUnit.Framework;

/// <summary>
/// M3 专项 — ProfessionProgress 数据容器本身的语义：
///   初始化后能正确持有五字段；
///   默认（new 后未赋值）UnlockedPool 非空空列表，便于后续 Person 独立成长；
///   同 ProfessionId 的两个 Progress 实例完全独立；
///   修改 Person Progress 不得触碰共享 ProfessionDefinition。
/// 纯数据层（不依赖 Assembly-CSharp 宿主）。
/// </summary>
public class ProfessionProgressDataTests
{
    [Test]
    public void InitializedProgress_HoldsAllFiveFields()
    {
        // Test 2（数据层）— 创建 ProfessionId=profession_test_hunter / Lv1 / Exp0 / [card_a, card_b] / TraitLv1
        ProfessionProgress progress = ProfessionM3TestKit.NewHunterProgress();

        Assert.AreEqual("profession_test_hunter", progress.ProfessionId);
        Assert.AreEqual(1, progress.ProfessionLevel);
        Assert.AreEqual(0, progress.ProfessionExp);
        CollectionAssert.AreEqual(new[] { "card_a", "card_b" }, progress.UnlockedPoolCardIds);
        Assert.AreEqual(1, progress.ProfessionTraitLevel);
    }

    [Test]
    public void FreshProgress_HasNonNullEmptyUnlockedPool()
    {
        // 未初始化的容器池必须是非 null 空列表（避免 null 语义歧义；成长可从此追加）
        var progress = new ProfessionProgress();

        Assert.IsNotNull(progress.UnlockedPoolCardIds, "UnlockedPoolCardIds 默认应为空列表而非 null");
        Assert.AreEqual(0, progress.UnlockedPoolCardIds.Count);
    }

    [Test]
    public void TwoProgressSameProfessionId_AreFullyIndependent()
    {
        // Test 3（数据层）— 修改 A 的 Level/Exp/UnlockedPool/TraitLevel 不得影响 B
        ProfessionProgress a = ProfessionM3TestKit.NewHunterProgress();
        ProfessionProgress b = ProfessionM3TestKit.NewHunterProgress();

        Assert.AreNotSame(a, b);
        Assert.AreNotSame(a.UnlockedPoolCardIds, b.UnlockedPoolCardIds,
            "两个 Person 的 UnlockedPool 不得共享同一 List 引用");

        a.ProfessionLevel = 5;
        a.ProfessionExp = 120;
        a.UnlockedPoolCardIds.Add("card_extra");
        a.UnlockedPoolCardIds.Remove("card_a");
        a.ProfessionTraitLevel = 3;

        // B 完全不受影响
        Assert.AreEqual(1, b.ProfessionLevel);
        Assert.AreEqual(0, b.ProfessionExp);
        CollectionAssert.AreEqual(new[] { "card_a", "card_b" }, b.UnlockedPoolCardIds);
        Assert.AreEqual(1, b.ProfessionTraitLevel);
    }

    [Test]
    public void MutatingProgressPool_DoesNotChangeSharedDefinition()
    {
        // Test 4 — 模板独立性：Person 的动态 UnlockedPool 不得污染 ProfessionDefinition.FullCardPool / InitialUnlockedCards
        ProfessionDefinition definition = ProfessionM3TestKit.NewHunterDefinition();
        ProfessionProgress progress = ProfessionM3TestKit.NewProgress(
            "profession_test_hunter", 1, 0,
            new List<string>(definition.InitialUnlockedCards), 1);

        // 初始：从模板克隆，不共享 List 引用
        CollectionAssert.AreEqual(definition.InitialUnlockedCards, progress.UnlockedPoolCardIds);
        Assert.AreNotSame(progress.UnlockedPoolCardIds, definition.InitialUnlockedCards);
        Assert.AreNotSame(progress.UnlockedPoolCardIds, definition.FullCardPool);

        // Person 成长：解锁新牌 / 移除旧牌
        progress.UnlockedPoolCardIds.Add("card_c");
        progress.UnlockedPoolCardIds.Remove("card_a");

        // 模板保持不变
        CollectionAssert.AreEqual(new[] { "card_a", "card_b" }, definition.InitialUnlockedCards,
            "Person 动态解锁变化不得写回 ProfessionDefinition.InitialUnlockedCards");
        CollectionAssert.AreEqual(
            new[] { "card_a", "card_b", "card_c", "card_d" }, definition.FullCardPool,
            "Person 动态解锁变化不得修改 ProfessionDefinition.FullCardPool");
    }
}
