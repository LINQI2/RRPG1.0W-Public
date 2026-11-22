using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

/// <summary>
/// Initial Person &amp; Profession Bootstrap — D. V4 Capability 依赖链（Disposable Development Test）。
///
/// 覆盖（不改 VillageCapabilityGrant / GrowthAuthority）：
///   - 开局 Profession 使 GrowthAuthority.IsAvailable = true → Capability 可授予；
///   - bcard_rally 开局不在 InitialUnlockedCards，Project Capability 授予后新增且 exactly once；
///   - Character Growth Offer 与 Run Reward Candidate 真实读取 Profession Pool；
///   - Save → Load 后 Capability 结果（池扩大）仍存在。
/// </summary>
public class PersonBootstrapCapabilityTests
{
    private const string RallyCard = "bcard_rally";
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

    private static List<string> GrowthOfferIds(IPersonGrowth person)
        => CardGrowthResolver.BuildOffer(person, PersonBootstrapTestKit.AllBattleCardTemplates())
            .Select(c => c.CardId).ToList();

    private static List<string> RunRewardIds(IPersonGrowth person)
    {
        var run = new RunState();
        run.GetOrCreatePerson(person.PersonId, person.DisplayName, 30);
        var provider = new UnlockedPoolRewardProvider(pid => pid == person.PersonId ? person : null);
        return provider.BuildCandidates(run, PersonBootstrapTestKit.AllBattleCardTemplates())
            .Where(c => c.Template != null).Select(c => c.Template.CardId).ToList();
    }

    private object BuildProductLikeCollection(out string personId, out object person)
    {
        var registry = ProfessionDefinitionRegistry.Build(new[]
        {
            PersonBootstrapTestKit.NewProfessionDefinition(
                "prof_capability",
                new[] { "bcard_aim", "bcard_sword" },
                new[] { "bcard_aim", "bcard_sword", RallyCard },
                new[] { new StarterSlotEntry("bcard_sword", 1) })
        });

        var definition = PersonBootstrapTestKit.NewDefinition("p_capability", "能力链人物");
        definition.InitialProfession = new InitialProfessionConfig { ProfessionId = "prof_capability" };

        person = PersonBootstrapTestKit.Bootstrap(definition, registry);
        personId = (string)PersonBootstrapTestKit.Get(person, "PersonId");

        object collection = PersonBootstrapTestKit.NewCollection();
        PersonBootstrapTestKit.OwnedCharacters(collection).Add(person);
        object card = PersonBootstrapTestKit.NewCard("pcard_capability", definition.CombatDefinitionId);
        PersonBootstrapTestKit.OwnedCards(collection).Add(card);
        PersonBootstrapTestKit.InvokeInstance(collection, "BindCharacterCardOwner", "pcard_capability", personId);
        PersonBootstrapTestKit.SetPersonSource(collection);
        return collection;
    }

    [Test]
    public void Capability_ExpandsProfessionPool_ExactlyOnce_And_FeedsGrowthAndReward()
    {
        object collection = BuildProductLikeCollection(out string personId, out object person);
        var growth = PersonBootstrapTestKit.AsPerson(person);
        var pool = PersonBootstrapTestKit.ProgressOf(person).UnlockedPoolCardIds;

        Assert.IsTrue(PersonBootstrapTestKit.AuthorityOf(person).IsAvailable,
            "开局 Profession 必须让 Growth Authority 可用（Capability 前置）");
        Assert.IsFalse(pool.Contains(RallyCard), "前置：bcard_rally 开局必须 locked");
        Assert.IsFalse(GrowthOfferIds(growth).Contains(RallyCard), "前置：Growth Offer 不应出现未解锁卡");

        Assert.AreEqual("Granted", PersonBootstrapTestKit.GrantCapability(personId, RallyCard),
            "Project Capability 必须真正扩大 Profession Pool");
        Assert.AreEqual("AlreadyPresent", PersonBootstrapTestKit.GrantCapability(personId, RallyCard),
            "重复授予不得重复加入同一 CardId（exactly once）");
        Assert.AreEqual(1, pool.Count(id => id == RallyCard), "Pool 中 bcard_rally 必须恰好一次");
        Assert.AreEqual(RallyCard, pool[0], "授予卡位于可观测位置（Test / Not Frozen 观测策略）");

        Assert.Contains(RallyCard, GrowthOfferIds(growth), "Character Growth Offer 必须读取到新卡");
        Assert.Contains(RallyCard, RunRewardIds(growth), "Run Reward Candidate 必须读取到新卡");

        // Save → Load 后 Capability 结果仍在（池是 ProfessionProgress 的一部分）
        Assert.IsTrue((bool)PersonBootstrapTestKit.InvokeStatic(PersonBootstrapTestKit.SaveSystemType, "Save", collection));
        object loaded = PersonBootstrapTestKit.InvokeStatic(PersonBootstrapTestKit.SaveSystemType, "Load");
        object restored = PersonBootstrapTestKit.InvokeInstance(loaded, "GetCharacterByPersonId", personId);
        Assert.IsNotNull(restored);
        CollectionAssert.Contains(PersonBootstrapTestKit.ProgressOf(restored).UnlockedPoolCardIds, RallyCard,
            "Save → Load 后 Capability 结果必须仍存在");
    }
}
