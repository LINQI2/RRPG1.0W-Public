using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// M4-correction 专项 — Save/Load 后 Growth 权威延续（窄路由）：
///   Test D1  有 Active Profession 的 Character：Save → Load 后 ProfessionProgress 仍是 Growth 权威
///            （GrowthAuthority 解析后升级继续改 ProfessionLevel/Exp，不改 Legacy）；
///   Test D2  无 Profession 的 Character：Save → Load 后仍无 Profession，Growth 仍不可执行（不自动 Formation）。
/// 本文件不改变任何 Save schema / restore 顺序（Legacy + Profession 独立保存），只验证行为不变。
/// </summary>
public class ProfessionGrowthSaveAuthorityTests
{
    [SetUp]
    public void SetUp()
    {
        ProfessionM4TestKit.DeleteSave();
        ProfessionM4TestKit.ResetBridge();
    }

    [TearDown]
    public void TearDown()
    {
        ProfessionM4TestKit.DeleteSave();
        ProfessionM4TestKit.ResetBridge();
    }

    private static GrowthCurveConfig NewCurve()
    {
        var c = ScriptableObject.CreateInstance<GrowthCurveConfig>();
        c.MaxLevel = 10;
        c.ProficiencyCap = 5;
        c.ExpRequiredToLevel = new[] { 10, 15, 20, 25, 30 };
        return c;
    }

    private static GrowthTableConfig EmptyTable()
    {
        var t = ScriptableObject.CreateInstance<GrowthTableConfig>();
        t.TableId = "test";
        t.Nodes = new List<GrowthNodeConfig>();
        return t;
    }

    private static object NewCollectionWithCharacter()
    {
        object collection = ProfessionM4TestKit.NewPlayerCollection();
        ProfessionM4TestKit.OwnedCharacters(collection)
            .Add(ProfessionM4TestKit.NewCharacter("char_001", "测试人物", 30, 6));
        return collection;
    }

    [Test]
    public void SaveLoad_RoundTrip_ProfessionProgressRemainsGrowthAuthority()
    {
        // Test D1 — 保存时已有 Active Profession；Load 后 ProfessionProgress 恢复，Growth 继续走 Profession 权威
        object collection = NewCollectionWithCharacter();
        object character = ProfessionM4TestKit.OwnedCharacters(collection)[0];
        ProfessionProgress pp = ProfessionM4TestKit.NewProgress("profession_test_hunter", 1, 10,
            new[] { "card_a", "card_b" }, 0);
        ProfessionM4TestKit.SetActiveProgress(character, pp);
        Assert.IsTrue(ProfessionM4TestKit.Save(collection), "Save 应成功");

        object loaded = ProfessionM4TestKit.Load();
        Assert.IsNotNull(loaded, "Load 应成功");
        object loadedCharacter = ProfessionM4TestKit.OwnedCharacters(loaded)[0];
        ProfessionProgress restored = ProfessionM4TestKit.ActiveProgress(loadedCharacter);
        Assert.IsNotNull(restored, "Load 后 ActiveProfessionProgress 应恢复");
        Assert.AreEqual("profession_test_hunter", restored.ProfessionId);
        Assert.AreEqual(1, restored.ProfessionLevel);
        Assert.AreEqual(10, restored.ProfessionExp);
        CollectionAssert.AreEqual(new[] { "card_a", "card_b" }, restored.UnlockedPoolCardIds);

        // Growth 权威延续：Load 后继续升级（经 GrowthAuthority 显式解析）
        var g = (IPersonGrowth)loadedCharacter;
        IGrowthAuthority auth = GrowthAuthority.Resolve(g);
        Assert.IsNotNull(auth, "Load 后应可解析 IGrowthAuthority");
        Assert.IsTrue(auth.IsAvailable, "Load 后仍可执行 Profession Growth");
        Assert.IsTrue(CharacterGrowthService.TryLevelUpOnce(g, NewCurve(), EmptyTable(), out _),
            "Load 后新 Progress 应继续是 Growth 权威");
        Assert.AreEqual(2, restored.ProfessionLevel, "Load 后升级应写入 ProfessionLevel");
        Assert.AreEqual(0, restored.ProfessionExp, "Load 后升级应消耗 ProfessionExp");
        Assert.AreEqual(1, ProfessionM4TestKit.Get(loadedCharacter, "Level"),
            "Load 后升级不得改写 Legacy Level");
    }

    [Test]
    public void SaveLoad_RoundTrip_NoProfessionCharacter_GrowthStillBlocked()
    {
        // Test D2 — 无 Profession 的 Character Save→Load 仍为 null，Growth 依旧不可执行（不自动 Formation / 不 fallback）
        object collection = NewCollectionWithCharacter();
        object character = ProfessionM4TestKit.OwnedCharacters(collection)[0];
        ProfessionM4TestKit.Set(character, "Level", 5);
        ProfessionM4TestKit.Set(character, "Exp", 120);
        Assert.IsNull(ProfessionM4TestKit.ActiveProgress(character));
        Assert.IsTrue(ProfessionM4TestKit.Save(collection), "Save 应成功");

        object loaded = ProfessionM4TestKit.Load();
        object loadedCharacter = ProfessionM4TestKit.OwnedCharacters(loaded)[0];
        Assert.IsNull(ProfessionM4TestKit.ActiveProgress(loadedCharacter),
            "旧/无职业人物 Load 后必须保持无 Profession（不自动 Formation）");
        IGrowthAuthority auth = GrowthAuthority.Resolve((IPersonGrowth)loadedCharacter);
        Assert.IsNotNull(auth);
        Assert.IsFalse(auth.IsAvailable,
            "Load 后无 Profession Growth 仍不可执行");
        Assert.AreEqual(5, ProfessionM4TestKit.Get(loadedCharacter, "Level"),
            "Legacy Level 原样保留（不删除 Legacy 字段）");
        Assert.AreEqual(120, ProfessionM4TestKit.Get(loadedCharacter, "Exp"),
            "Legacy Exp 原样保留（不删除 Legacy 字段）");

        Assert.IsFalse(CharacterGrowthService.TryLevelUpOnce(
            (IPersonGrowth)loadedCharacter, NewCurve(), EmptyTable(), out _),
            "Load 后无 Profession 仍不可升级（不 fallback Legacy Exp）");
    }

    [Test]
    public void SaveLoad_LegacyAndProfessionRemainIndependent_NoSchemaChange()
    {
        // Test D3 — Save 同时持久化 Legacy 与 Profession 两个独立区块；Load 后两者互不污染
        //（本轮未改动任何 Save schema / restore 顺序 —— 见本文件未触碰 SaveSystem/SaveData）。
        object collection = NewCollectionWithCharacter();
        object character = ProfessionM4TestKit.OwnedCharacters(collection)[0];
        ProfessionM4TestKit.Set(character, "Level", 4);
        ProfessionM4TestKit.Set(character, "Exp", 60);
        ProfessionM4TestKit.Set(character, "UnlockedPoolCardIds", new List<string> { "legacy_x" });
        ProfessionM4TestKit.Set(character, "TraitLevel", 2);

        ProfessionProgress pp = ProfessionM4TestKit.NewProgress("profession_test_hunter", 2, 7,
            new[] { "card_a", "card_b" }, 1);
        ProfessionM4TestKit.SetActiveProgress(character, pp);
        Assert.IsTrue(ProfessionM4TestKit.Save(collection), "Save 应成功");

        object loaded = ProfessionM4TestKit.Load();
        object loadedCharacter = ProfessionM4TestKit.OwnedCharacters(loaded)[0];

        // Legacy 区块原样保留（与 Profession 独立）
        Assert.AreEqual(4, ProfessionM4TestKit.Get(loadedCharacter, "Level"), "Legacy Level 原样恢复");
        Assert.AreEqual(60, ProfessionM4TestKit.Get(loadedCharacter, "Exp"), "Legacy Exp 原样恢复");
        CollectionAssert.AreEqual(new[] { "legacy_x" },
            (List<string>)ProfessionM4TestKit.Get(loadedCharacter, "UnlockedPoolCardIds"),
            "Legacy Pool 独立恢复（不被 Profession Pool 混入）");
        Assert.AreEqual(2, ProfessionM4TestKit.Get(loadedCharacter, "TraitLevel"));

        // Profession 区块原样恢复
        ProfessionProgress restored = ProfessionM4TestKit.ActiveProgress(loadedCharacter);
        Assert.IsNotNull(restored);
        Assert.AreEqual(2, restored.ProfessionLevel);
        Assert.AreEqual(7, restored.ProfessionExp);
        CollectionAssert.AreEqual(new[] { "card_a", "card_b" }, restored.UnlockedPoolCardIds);
        Assert.AreEqual(1, restored.ProfessionTraitLevel);

        // 权威视角与共享契约视角解耦：Growth Authority = Profession；IPersonGrowth = Legacy
        IGrowthAuthority auth = GrowthAuthority.Resolve((IPersonGrowth)loadedCharacter);
        Assert.AreEqual(2, auth.Level, "Growth 权威 = ProfessionLevel");
        Assert.AreEqual(4, ((IPersonGrowth)loadedCharacter).Level, "IPersonGrowth 共享契约 = Legacy Level");
    }
}

