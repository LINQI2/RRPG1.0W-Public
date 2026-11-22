using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

/// <summary>
/// Initial Person &amp; Profession Bootstrap —— **长期冻结契约测试**（Long-term Regression Test）。
///
/// 只锁定新体系的核心不变量（不覆盖开发期边界矩阵 / 临时验证用例）：
///   1) PersonId ≠ PersonDefinitionId（配置身份与运行身份分离）；
///   2) New Game PersonDefinition → 开局 Active Profession Bootstrap（含配置化 Level/Exp/Trait/Pool/Starter + 深拷贝）；
///   3) Bootstrap 不写 FormationState、不产生 ProfessionFormed LifeHistory（开局已有职业 ≠ 一次人生 Formation）；
///   4) 真实产品 New Game 入口：开局人数 = Person.csv 配置行数、人名来自 Person.csv、全部有职业、卡→Person 显式绑定可解析。
/// </summary>
public class PersonBootstrapContractTests
{
    private string _savePath;

    [SetUp]
    public void SetUp()
    {
        PersonBootstrapTestKit.ResetBridge();
        _savePath = (string)PersonBootstrapTestKit.InvokeStatic(PersonBootstrapTestKit.SaveSystemType, "GetSavePath");
        DeleteSaveFile();
    }

    [TearDown]
    public void TearDown()
    {
        DeleteSaveFile();
        PersonBootstrapTestKit.ResetBridge();
    }

    private void DeleteSaveFile()
    {
        if (!string.IsNullOrEmpty(_savePath) && System.IO.File.Exists(_savePath)) System.IO.File.Delete(_savePath);
    }

    private static PersonDefinition DefinitionWithProfession(string id, string name)
    {
        var def = PersonBootstrapTestKit.NewDefinition(id, name);
        def.InitialProfession = new InitialProfessionConfig { ProfessionId = "prof_contract" };
        return def;
    }

    private static ProfessionDefinitionRegistry ContractRegistry()
    {
        return ProfessionDefinitionRegistry.Build(new[]
        {
            PersonBootstrapTestKit.NewProfessionDefinition(
                "prof_contract",
                new[] { "bcard_aim", "bcard_sword" },
                new[] { "bcard_aim", "bcard_sword", "bcard_heal" },
                new[] { new StarterSlotEntry("bcard_sword", 1) })
        });
    }

    [Test]
    public void Bootstrap_PersonId_Is_Not_PersonDefinitionId()
    {
        object person = PersonBootstrapTestKit.Bootstrap(DefinitionWithProfession("p_identity", "身份人物"),
            ContractRegistry());

        Assert.IsNotNull(person);
        string personId = (string)PersonBootstrapTestKit.Get(person, "PersonId");
        string personDefinitionId = (string)PersonBootstrapTestKit.Get(person, "PersonDefinitionId");
        Assert.IsFalse(string.IsNullOrEmpty(personId), "具体人物必须有独立 PersonId");
        Assert.AreNotEqual(personDefinitionId, personId, "PersonId 不得等于 PersonDefinitionId");

        object second = PersonBootstrapTestKit.Bootstrap(DefinitionWithProfession("p_identity", "身份人物"),
            ContractRegistry());
        Assert.AreNotEqual(personId, (string)PersonBootstrapTestKit.Get(second, "PersonId"),
            "同一 PersonDefinition 的两次创建必须得到不同 PersonId");
    }

    [Test]
    public void Bootstrap_Creates_Active_Profession_From_Definition_Config()
    {
        var definition = PersonBootstrapTestKit.NewDefinition("p_profession", "职业人物");
        definition.InitialProfession = new InitialProfessionConfig
        {
            ProfessionId = "prof_contract",
            InitialLevel = 2,
            InitialExp = 5,
            InitialTraitLevel = 3,
            HasUnlockedPoolOverride = true,
            InitialUnlockedPoolOverride = new List<string> { "bcard_heal" },
            HasStarterBuildOverride = true,
            InitialStarterBuildOverride = new List<StarterSlotEntry> { new StarterSlotEntry("bcard_heal", 2) }
        };

        var registry = ContractRegistry();
        object person = PersonBootstrapTestKit.Bootstrap(definition, registry);

        Assert.IsTrue(PersonBootstrapTestKit.HasActiveProfession(person), "固定初始人物开局必须有 Active Profession");
        var progress = PersonBootstrapTestKit.ProgressOf(person);
        Assert.AreEqual("prof_contract", progress.ProfessionId);
        Assert.AreEqual(2, progress.ProfessionLevel, "InitialLevel 为初始化快照");
        Assert.AreEqual(5, progress.ProfessionExp);
        Assert.AreEqual(3, progress.ProfessionTraitLevel);
        CollectionAssert.AreEqual(new[] { "bcard_heal" }, progress.UnlockedPoolCardIds,
            "override = 整值替换（不是 merge）");

        var starter = PersonBootstrapTestKit.StarterOf(person);
        Assert.AreEqual(1, starter.Count);
        Assert.AreEqual("bcard_heal", starter[0].CardId);
        Assert.AreEqual(2, starter[0].Proficiency);

        // 深拷贝：人物成长不得污染模板
        progress.UnlockedPoolCardIds.Add("bcard_rally");
        starter[0].Proficiency = 9;
        registry.TryGetById("prof_contract", out ProfessionDefinition template);
        CollectionAssert.DoesNotContain(template.InitialUnlockedCards, "bcard_rally");
        Assert.AreEqual(1, template.InitialStarterBuild[0].Proficiency);
        Assert.IsFalse(ReferenceEquals(progress.UnlockedPoolCardIds, template.InitialUnlockedCards));
        Assert.IsFalse(ReferenceEquals(starter[0], template.InitialStarterBuild[0]));

        var authority = PersonBootstrapTestKit.AuthorityOf(person);
        Assert.IsTrue(authority.IsAvailable);
        Assert.IsTrue(authority.IsProfessionGrowth);
    }

    [Test]
    public void Bootstrap_Writes_No_FormationState_And_No_ProfessionFormed_LifeHistory()
    {
        object person = PersonBootstrapTestKit.Bootstrap(DefinitionWithProfession("p_no_formation", "无形成事件"),
            ContractRegistry());

        object formationState = PersonBootstrapTestKit.Get(person, "FormationState");
        Assert.IsNotNull(formationState);
        Assert.IsNull(PersonBootstrapTestKit.Get(formationState, "FormationTargetProfessionId"),
            "开局已有职业不是 Formation：不得产生 Formation Target");
        Assert.IsFalse((bool)PersonBootstrapTestKit.Get(formationState, "FormationWindowOpen"));

        var events = (IEnumerable)PersonBootstrapTestKit.Get(
            PersonBootstrapTestKit.Get(person, "LifeHistory"), "Events");
        Assert.AreEqual(0, events.Cast<object>().Count(), "不得追加假的 ProfessionFormed LifeHistory");

        // InitialProfession == null：合法配置（未来随机无职业 Person → M6 Formation）
        object futurePerson = PersonBootstrapTestKit.Bootstrap(
            PersonBootstrapTestKit.NewDefinition("p_future", "未来无职业人物"), ContractRegistry());
        Assert.IsNotNull(futurePerson);
        Assert.IsFalse(PersonBootstrapTestKit.HasActiveProfession(futurePerson));
    }

    [Test]
    public void NewGame_ProductEntry_Creates_Configured_Roster_With_Initial_Profession()
    {
        var roster = PersonBootstrapTestKit.ProductInitialRoster();
        var registry = PersonBootstrapTestKit.ProductRegistry();
        Assert.IsNotEmpty(roster, "产品 Person.csv 必须已导入初始名册");
        Assert.IsNotNull(registry, "ProfessionDefinitionRegistry 必须已建立");

        object collection = PersonBootstrapTestKit.InvokeStatic(PersonBootstrapTestKit.SaveSystemType, "NewGame");
        Assert.IsNotNull(collection);

        var characters = PersonBootstrapTestKit.OwnedCharacters(collection);
        Assert.AreEqual(roster.Count, characters.Count, "开局人数必须等于 Person.csv 配置行数（不写死 5 人）");

        foreach (var definition in roster)
        {
            object person = null;
            foreach (var c in characters)
            {
                if (c != null && (string)PersonBootstrapTestKit.Get(c, "PersonDefinitionId") == definition.PersonDefinitionId)
                {
                    person = c;
                    break;
                }
            }
            Assert.IsNotNull(person, "未创建配置人物：" + definition.PersonDefinitionId);
            Assert.AreEqual(definition.DisplayName, (string)PersonBootstrapTestKit.Get(person, "DisplayName"),
                "人名必须来自 Person.csv");
            Assert.AreNotEqual(definition.PersonDefinitionId, (string)PersonBootstrapTestKit.Get(person, "PersonId"),
                "PersonId 不得等于 PersonDefinitionId");
            Assert.IsTrue(PersonBootstrapTestKit.HasActiveProfession(person),
                definition.PersonDefinitionId + " 开局必须有 Active Profession");

            var progress = PersonBootstrapTestKit.ProgressOf(person);
            Assert.AreEqual(definition.InitialProfession.ProfessionId, progress.ProfessionId);
            Assert.AreEqual(0, PersonBootstrapTestKit.AsPerson(person).UnlockedPoolCardIds.Count,
                "Bootstrap 人物不得被 GrowthDefaults Legacy 测试种子污染");
        }

        foreach (var card in PersonBootstrapTestKit.OwnedCards(collection))
        {
            string resolved = (string)PersonBootstrapTestKit.InvokeInstance(collection, "ResolvePersonIdForCard", card);
            Assert.IsFalse(string.IsNullOrEmpty(resolved), "初始角色卡必须显式绑定到具体人物（M1）");
        }
    }
}
