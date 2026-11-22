using System.Collections.Generic;
using System.IO;
using NUnit.Framework;

/// <summary>
/// V4 Acceptance Prep 专项（Temporary Development Test；Closeout 默认删除）：
/// 远征选人卡片的最小「Person 身份信息」只读组合（职责 / 长期状态 / 远征经历）。
/// 覆盖：职责（在岗 / 空闲 / 投入工程）、Injury 文案、Character 远征经历摘要与最近 Outcome、
/// Hero 不显示远征经历、未知 Person 不输出信息。
/// 全部内容 Test / Not Frozen。
/// </summary>
public class VillageV4ExpeditionIdentityTests
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

    [Test]
    public void Identity_ShowsDuty_Injury_AndExpeditionSummary()
    {
        var collection = VillageV4TestKit.NewPlayerCollection();
        object characterInstance = VillageV4TestKit.NewAsm("CharacterInstance", "char_001", "战士甲", 30, 6);
        var character = (IPersonGrowth)characterInstance;
        VillageV4TestKit.OwnedCharacters(collection).Add(characterInstance);

        VillageV4TestKit.EnsureInitialized();
        VillageV4TestKit.BindPersonSource(collection);

        Assert.AreEqual(VillageAssignmentResult.Success,
            VillageV4TestKit.Assign(character.PersonId, "ws_test_01"), "前置：应可授予 Primary Assignment");
        VillageV4TestKit.SetInjury(characterInstance, "HeavyInjury");
        VillageV4TestKit.AppendExpeditionReturned(characterInstance, 2026, 1, 5, 1); // Retreat
        VillageV4TestKit.AppendExpeditionReturned(characterInstance, 2026, 2, 9, 2); // Defeat（最近）

        string suffix = VillageV4TestKit.IdentitySuffix(collection, character.PersonId);

        StringAssert.Contains("职责：" + VillageV4TestKit.WorkplaceDisplayName("ws_test_01"), suffix,
            "应显示当前 Village 职责（复用既有 Assignment 权威）");
        StringAssert.Contains("长期状态：重伤", suffix, "应显示既有 Injury 的长期状态");
        StringAssert.Contains("远征经历：2 次（最近：失败撤出）", suffix,
            "应显示 Character 远征经历次数与最近一次 Return Outcome");
    }

    [Test]
    public void Identity_IdlePerson_ShowsIdleDuty_AndNoExpeditionRecord()
    {
        var collection = VillageV4TestKit.NewPlayerCollection();
        var character = VillageV4TestKit.AddCharacter(collection);

        VillageV4TestKit.EnsureInitialized();
        VillageV4TestKit.BindPersonSource(collection);

        string suffix = VillageV4TestKit.IdentitySuffix(collection, character.PersonId);

        StringAssert.Contains("职责：空闲", suffix, "无 Primary Assignment 应显示空闲");
        StringAssert.Contains("长期状态：正常", suffix, "默认 Injury 为 Normal");
        StringAssert.Contains("远征经历：无", suffix, "未参加过远征应有明确「无」");
    }

    [Test]
    public void Identity_ProjectParticipant_ShowsProjectDuty()
    {
        var collection = VillageV4TestKit.NewPlayerCollection();
        var character = VillageV4TestKit.AddCharacter(collection);

        VillageV4TestKit.EnsureInitialized();
        VillageV4TestKit.BindPersonSource(collection);
        Assert.AreEqual(VillageProjectResult.Success,
            VillageV4TestKit.StartProject("prj_test_02", character.PersonId), "前置：应可投入 Test Project");

        string suffix = VillageV4TestKit.IdentitySuffix(collection, character.PersonId);
        StringAssert.Contains("职责：投入工程：", suffix,
            "投入 Active Project 时应显示工程职责（Temporary Occupancy 由既有权威派生）");
    }

    [Test]
    public void Identity_Hero_ShowsInjury_ButNoExpeditionLine()
    {
        var collection = VillageV4TestKit.NewPlayerCollection();
        object heroInstance = VillageV4TestKit.NewAsm("HeroInstance", "hero_001", "青鹿", 30);
        var hero = (IPersonGrowth)heroInstance;
        VillageV4TestKit.OwnedHeroes(collection).Add(heroInstance);

        VillageV4TestKit.EnsureInitialized();
        VillageV4TestKit.BindPersonSource(collection);
        VillageV4TestKit.SetInjury(heroInstance, "LightInjury");

        string suffix = VillageV4TestKit.IdentitySuffix(collection, hero.PersonId);

        StringAssert.Contains("长期状态：轻伤", suffix, "Hero 应显示既有 Injury");
        StringAssert.DoesNotContain("远征经历", suffix,
            "OQ1：Hero 无 LifeHistory → 不得显示远征经历行（也不得为此扩展 Hero LifeHistory）");
        Assert.IsNull(heroInstance.GetType().GetProperty("LifeHistory"), "Hero 仍不得拥有 LifeHistory");
    }

    [Test]
    public void Identity_UnknownPerson_ReturnsEmpty()
    {
        var collection = VillageV4TestKit.NewPlayerCollection();
        VillageV4TestKit.EnsureInitialized();
        VillageV4TestKit.BindPersonSource(collection);

        Assert.AreEqual(string.Empty,
            VillageV4TestKit.IdentitySuffix(collection, "person_does_not_exist"),
            "未知 PersonId 不得猜测人物信息");
    }
}
