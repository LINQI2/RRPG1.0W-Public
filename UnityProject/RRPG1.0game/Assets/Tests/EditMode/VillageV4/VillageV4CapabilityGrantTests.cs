using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;

/// <summary>
/// V4 Gate 1 专项（Temporary Development Test；Closeout 默认删除）：
/// Test Project Completion → Test Capability Grant → 具体 Person 权威 Unlocked Pool 扩大
/// → Character Growth Offer 与 Run Reward Candidate 共同读取 → Save / Load 后仍存在。
///
/// 覆盖 FDS Gate 1 验证项 A–G（exactly-once / 门禁 / Pool before-after / Growth Offer /
/// Run Reward / Save-Load / 无第二套 Capability·Pool Save Authority）。
///
/// 全部内容 Test / Not Frozen。
/// </summary>
public class VillageV4CapabilityGrantTests
{
    private const string TestProject = "prj_test_02";            // capacity 1 / 2 per day / target 6 → 3 天
    private const string NonCapabilityProject = "prj_test_01";   // 无 Capability 映射
    private const string GrantedCard = "bcard_rally";
    private const int TestProjectDays = 3;

    [SetUp]
    public void SetUp()
    {
        VillageV4TestKit.ResetBridge();
        DeleteSave();
    }

    [TearDown]
    public void TearDown()
    {
        VillageV4TestKit.ResetBridge();
        DeleteSave();
    }

    private static void DeleteSave()
    {
        string path = (string)VillageV4TestKit.InvokeStatic(VillageV4TestKit.SaveSystemType, "GetSavePath");
        if (!string.IsNullOrEmpty(path) && File.Exists(path)) File.Delete(path);
    }

    /// <summary>产品式 Hero 场景：Hero + 3 张初始池（sword/heal/aim），Temporary Occupancy 可控。</summary>
    private static IPersonGrowth NewHeroWithSeededPool(object collection)
    {
        var hero = VillageV4TestKit.AddHero(collection);
        VillageV4TestKit.SeedLegacyPool(hero, "bcard_sword", "bcard_heal", "bcard_aim");
        return hero;
    }

    // ==================== A：完成 exactly-once → 授予 exactly-once ====================

    [Test]
    public void Capability_ProjectCompletion_ExpandsAuthorityPool_WithGrantedCardAtHead()
    {
        var collection = VillageV4TestKit.NewPlayerCollection();
        var hero = NewHeroWithSeededPool(collection);
        List<string> pool = VillageV4TestKit.AuthorityPool(hero);

        Assert.AreEqual(3, pool.Count, "前置：Hero 权威池应为产品式 3 张");
        CollectionAssert.DoesNotContain(pool, GrantedCard, "前置：授予前不得含 Test Capability 卡");

        VillageV4TestKit.EnsureInitialized();
        VillageV4TestKit.BindPersonSource(collection);
        Assert.AreEqual(VillageProjectResult.Success,
            VillageV4TestKit.StartProject(TestProject, hero.PersonId), "Test Project 应可启动");

        VillageV4TestKit.TickDays(TestProjectDays);

        Assert.AreEqual(4, pool.Count, "完成后 Pool 应扩大 1 张（无第二套池）");
        Assert.AreEqual(GrantedCard, pool[0], "granted Card 应位于 index 0（Test observable strategy）");
        Assert.AreEqual(1, VillageV4TestKit.CountOf(pool, GrantedCard), "同 CardId 不得重复");
    }

    [Test]
    public void Capability_RepeatTicksAndSecondProject_DoNotDuplicateGrantedCard()
    {
        var collection = VillageV4TestKit.NewPlayerCollection();
        var hero = NewHeroWithSeededPool(collection);

        VillageV4TestKit.EnsureInitialized();
        VillageV4TestKit.BindPersonSource(collection);
        Assert.AreEqual(VillageProjectResult.Success, VillageV4TestKit.StartProject(TestProject, hero.PersonId));
        VillageV4TestKit.TickDays(TestProjectDays);

        List<string> pool = VillageV4TestKit.AuthorityPool(hero);
        int sizeAfterFirst = pool.Count;
        Assert.AreEqual(1, VillageV4TestKit.CountOf(pool, GrantedCard));

        // 完成后继续推进：完成快照增量不再包含该项目 → 不得重复授予
        VillageV4TestKit.TickDays(2);
        Assert.AreEqual(sizeAfterFirst, pool.Count, "完成后继续 Tick 不得再次授予");
        Assert.AreEqual(1, VillageV4TestKit.CountOf(pool, GrantedCard));

        // 第二次完成同一 Test Project（同一 Person）：contains 幂等，仍不重复
        Assert.AreEqual(VillageProjectResult.Success, VillageV4TestKit.StartProject(TestProject, hero.PersonId),
            "完成解除占用后同一 Person 应可再次投入");
        VillageV4TestKit.TickDays(TestProjectDays);
        Assert.AreEqual(sizeAfterFirst, pool.Count, "第二次完成不得重复加入同 CardId");
        Assert.AreEqual(1, VillageV4TestKit.CountOf(pool, GrantedCard));
        Assert.AreEqual(GrantedCard, pool[0], "重复授予仅确保可观测位置，不改变池规模");
    }

    // ==================== B：无可用 Growth Authority → 跳过且不污染任何池 ====================

    [Test]
    public void Capability_CharacterWithoutProfession_IsSkipped_AndNoPoolIsPolluted()
    {
        var collection = VillageV4TestKit.NewPlayerCollection();
        var character = VillageV4TestKit.AddCharacter(collection); // char_001，无 Active Profession

        Assert.IsFalse(VillageV4TestKit.AuthorityOf(character).IsAvailable,
            "前置：普通 Character 未形成 Profession → Growth Authority 不可用");

        VillageV4TestKit.EnsureInitialized();
        VillageV4TestKit.BindPersonSource(collection);
        Assert.AreEqual(VillageProjectResult.Success,
            VillageV4TestKit.StartProject(TestProject, character.PersonId));
        VillageV4TestKit.TickDays(TestProjectDays);

        List<string> authorityPool = VillageV4TestKit.AuthorityPool(character);
        Assert.AreEqual(0, authorityPool.Count,
            "不可用 Authority 的池（共享空池）绝不得被 Capability 写入污染");
        CollectionAssert.DoesNotContain(character.UnlockedPoolCardIds, GrantedCard,
            "禁止 fallback 写入 Legacy Character 池");
    }

    // ==================== C：Profession 分支只写 Profession 池（Legacy 池不动） ====================

    [Test]
    public void Capability_ProfessionCharacter_ExpandsProfessionPool_NotLegacyPool()
    {
        var collection = VillageV4TestKit.NewPlayerCollection();
        object characterInstance = VillageV4TestKit.NewAsm("CharacterInstance", "char_001", "战士甲", 30, 6);
        var character = (IPersonGrowth)characterInstance;
        VillageV4TestKit.OwnedCharacters(collection).Add(characterInstance);
        VillageV4TestKit.SetActiveProfession(characterInstance, "prof_test", "bcard_aim");
        VillageV4TestKit.SeedLegacyPool(character, "bcard_sword"); // Legacy 池故意与 Authority 池不同

        Assert.IsTrue(VillageV4TestKit.AuthorityOf(character).IsAvailable,
            "有 Active Profession → Growth Authority 可用");

        VillageV4TestKit.EnsureInitialized();
        VillageV4TestKit.BindPersonSource(collection);
        Assert.AreEqual(VillageProjectResult.Success,
            VillageV4TestKit.StartProject(TestProject, character.PersonId));
        VillageV4TestKit.TickDays(TestProjectDays);

        List<string> professionPool = VillageV4TestKit.AuthorityPool(character);
        Assert.AreEqual(2, professionPool.Count, "Profession 池应扩大 1 张");
        Assert.AreEqual(GrantedCard, professionPool[0]);
        CollectionAssert.DoesNotContain(character.UnlockedPoolCardIds, GrantedCard,
            "Legacy Character 池不得被 Capability 写入");
        CollectionAssert.Contains(character.UnlockedPoolCardIds, "bcard_sword", "Legacy 池应保持原样");
    }

    // ==================== D：Character Growth Offer 读取新增内容 ====================

    [Test]
    public void Capability_GrowthOffer_ReadsGrantedCard_WithoutChangingOfferCount()
    {
        var collection = VillageV4TestKit.NewPlayerCollection();
        var hero = NewHeroWithSeededPool(collection);
        var templates = VillageV4TestKit.DefaultTemplates();

        var before = VillageV4TestKit.BuildOffer(hero, templates).Select(c => c.CardId).ToList();
        Assert.AreEqual(3, before.Count, "前置：OfferCount=3，池为 3 时三选一为原池前 3 张");
        CollectionAssert.DoesNotContain(before, GrantedCard);

        VillageV4TestKit.EnsureInitialized();
        VillageV4TestKit.BindPersonSource(collection);
        VillageV4TestKit.StartProject(TestProject, hero.PersonId);
        VillageV4TestKit.TickDays(TestProjectDays);

        var after = VillageV4TestKit.BuildOffer(hero, templates).Select(c => c.CardId).ToList();
        Assert.AreEqual(3, after.Count, "OfferCount 未被修改");
        Assert.AreEqual(GrantedCard, after[0], "授予后三选一应确定性读取新 Capability 卡");
    }

    // ==================== E：Run Reward Candidate 读取同一新增内容 ====================

    [Test]
    public void Capability_RunRewardCandidate_ReadsSameGrantedCard_FromSamePool()
    {
        var collection = VillageV4TestKit.NewPlayerCollection();
        var hero = NewHeroWithSeededPool(collection);
        var templates = VillageV4TestKit.DefaultTemplates();
        var run = VillageV4TestKit.NewRunWithPerson(hero);

        var before = VillageV4TestKit.BuildRunCandidates(run, hero, templates)
            .Where(c => c.Template != null).Select(c => c.Template.CardId).ToList();
        CollectionAssert.DoesNotContain(before, GrantedCard, "前置：Run 候选不得含未授予的测试卡");

        VillageV4TestKit.EnsureInitialized();
        VillageV4TestKit.BindPersonSource(collection);
        VillageV4TestKit.StartProject(TestProject, hero.PersonId);
        VillageV4TestKit.TickDays(TestProjectDays);

        var after = VillageV4TestKit.BuildRunCandidates(run, hero, templates)
            .Where(c => c.Template != null).Select(c => c.Template.CardId).ToList();
        CollectionAssert.Contains(after, GrantedCard, "Run 奖励候选必须读取同一 Person 池的新增内容");
        Assert.AreEqual(VillageV4TestKit.CountOf(VillageV4TestKit.AuthorityPool(hero), GrantedCard),
            after.Count(id => id == GrantedCard), "不得复制两份解锁状态（候选仅来自同一池）");
    }

    // ==================== F/G：Save → Load 仍存在；无第二套 Capability Save Authority ====================

    [Test]
    public void Capability_SaveLoad_KeepsGrantedCard_SaveVersion1_NoCapabilitySaveBlock()
    {
        var collection = VillageV4TestKit.NewPlayerCollection();
        var hero = NewHeroWithSeededPool(collection);
        string heroPersonId = hero.PersonId;

        VillageV4TestKit.EnsureInitialized();
        VillageV4TestKit.BindPersonSource(collection);
        VillageV4TestKit.StartProject(TestProject, heroPersonId);
        VillageV4TestKit.TickDays(TestProjectDays);
        Assert.AreEqual(GrantedCard, VillageV4TestKit.AuthorityPool(hero)[0], "前置：授予已发生");

        Assert.IsTrue((bool)VillageV4TestKit.InvokeStatic(VillageV4TestKit.SaveSystemType, "Save", collection),
            "Save 应成功");

        string savePath = (string)VillageV4TestKit.InvokeStatic(VillageV4TestKit.SaveSystemType, "GetSavePath");
        string json = File.ReadAllText(savePath);
        StringAssert.Contains("\"SaveVersion\": 1", json, "SaveVersion 必须保持 1");
        StringAssert.Contains(GrantedCard, json, "Capability 结果应通过既有 Person Pool 持久化");
        StringAssert.DoesNotContain("capability", json.ToLowerInvariant(),
            "不得新增 Capability Save 区块 / 字段");

        VillageV4TestKit.ResetBridge(); // 模拟重启
        object loaded = VillageV4TestKit.InvokeStatic(VillageV4TestKit.SaveSystemType, "Load");
        Assert.IsNotNull(loaded, "存档应可加载");

        IPersonGrowth loadedHero = FindPerson(loaded, heroPersonId);
        Assert.IsNotNull(loadedHero, "Load 后应恢复同一 PersonId 的 Hero");
        List<string> pool = VillageV4TestKit.AuthorityPool(loadedHero);
        Assert.AreEqual(GrantedCard, pool[0], "Load 后同一 Person 的 Pool 仍含 Capability 结果");
        Assert.AreEqual(1, VillageV4TestKit.CountOf(pool, GrantedCard), "Load 后不得重复");
    }

    // ==================== 无映射 Project：不得授予任何东西 ====================

    [Test]
    public void Capability_ProjectWithoutMapping_DoesNotGrantAnything()
    {
        var collection = VillageV4TestKit.NewPlayerCollection();
        var hero = NewHeroWithSeededPool(collection);

        Assert.IsNull(VillageV4TestKit.InvokeStatic(VillageV4TestKit.CapabilityGrantType,
            "ResolveTestCapabilityCardId", NonCapabilityProject), "无映射应返回 null（不默认授予）");

        VillageV4TestKit.EnsureInitialized();
        VillageV4TestKit.BindPersonSource(collection);
        Assert.AreEqual(VillageProjectResult.Success,
            VillageV4TestKit.StartProject(NonCapabilityProject, hero.PersonId));
        VillageV4TestKit.TickDays(10); // prj_test_01：1/人/日，target 10

        List<string> pool = VillageV4TestKit.AuthorityPool(hero);
        Assert.AreEqual(3, pool.Count, "无 Capability 映射的 Project 完成不得改变任何 Pool");
        CollectionAssert.DoesNotContain(pool, GrantedCard);
    }

    private static IPersonGrowth FindPerson(object collection, string personId)
    {
        var m = collection.GetType().GetMethod("FindPersonByPersonId", VillageV4TestKit.F, null,
            new[] { typeof(string) }, null);
        Assert.IsNotNull(m, "PlayerCollection.FindPersonByPersonId 不存在");
        return (IPersonGrowth)m.Invoke(collection, new object[] { personId });
    }
}
