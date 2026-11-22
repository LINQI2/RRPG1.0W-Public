using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

/// <summary>
/// Initial Person &amp; Profession Bootstrap — Save / Load（Disposable Development Test）。
///
/// 覆盖：
///   - Save → Load 保持 PersonDefinitionId provenance 与 Profession 五字段（含已成长进度）；
///   - Load 绝不重新执行 Bootstrap（不覆盖已成长 Profession，也不给旧档人物凭空补职业）；
///   - SaveVersion 不提升（既有存档字段继续可用）。
/// </summary>
public class PersonBootstrapSaveTests
{
    private ProfessionDefinitionRegistry _registry;
    private string _savePath;

    [SetUp]
    public void SetUp()
    {
        PersonBootstrapTestKit.ResetBridge();
        _registry = ProfessionDefinitionRegistry.Build(new[]
        {
            PersonBootstrapTestKit.NewProfessionDefinition(
                "prof_save_test", new[] { "bcard_aim", "bcard_sword" },
                new[] { "bcard_aim", "bcard_sword" },
                new[] { new StarterSlotEntry("bcard_sword", 1) })
        });
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
        if (!string.IsNullOrEmpty(_savePath) && System.IO.File.Exists(_savePath))
            System.IO.File.Delete(_savePath);
    }

    private PersonDefinition DefinitionWithProfession(string id, string name)
    {
        var def = PersonBootstrapTestKit.NewDefinition(id, name);
        def.InitialProfession = new InitialProfessionConfig { ProfessionId = "prof_save_test" };
        return def;
    }

    private object BuildCollectionWithPerson(PersonDefinition definition, string cardId, out string personId)
    {
        object collection = PersonBootstrapTestKit.NewCollection();
        object person = PersonBootstrapTestKit.Bootstrap(definition, _registry);
        personId = (string)PersonBootstrapTestKit.Get(person, "PersonId");
        PersonBootstrapTestKit.OwnedCharacters(collection).Add(person);

        object card = PersonBootstrapTestKit.NewCard(cardId, definition.CombatDefinitionId);
        PersonBootstrapTestKit.OwnedCards(collection).Add(card);
        PersonBootstrapTestKit.InvokeInstance(collection, "BindCharacterCardOwner", cardId, personId);
        return collection;
    }

    [Test]
    public void SaveLoad_KeepsPersonDefinitionProvenance_AndProfessionProgress()
    {
        object collection = BuildCollectionWithPerson(DefinitionWithProfession("p_save", "存档人物"), "pcard_save", out string personId);

        // 模拟真实成长：等级 / 经验 / 池扩大
        object person = PersonBootstrapTestKit.OwnedCharacters(collection)[0];
        var progress = PersonBootstrapTestKit.ProgressOf(person);
        progress.ProfessionLevel = 4;
        progress.ProfessionExp = 12;
        progress.ProfessionTraitLevel = 3;
        progress.UnlockedPoolCardIds.Add("bcard_heal");

        Assert.IsTrue((bool)PersonBootstrapTestKit.InvokeStatic(PersonBootstrapTestKit.SaveSystemType, "Save", collection));

        object loaded = PersonBootstrapTestKit.InvokeStatic(PersonBootstrapTestKit.SaveSystemType, "Load");
        Assert.IsNotNull(loaded, "存档必须可加载");

        object restored = PersonBootstrapTestKit.InvokeInstance(loaded, "GetCharacterByPersonId", personId);
        Assert.IsNotNull(restored, "必须能按 PersonId 找回同一具体人物");

        Assert.AreEqual("p_save", (string)PersonBootstrapTestKit.Get(restored, "PersonDefinitionId"),
            "PersonDefinitionId（配置 provenance）必须随档恢复");
        Assert.AreEqual("存档人物", (string)PersonBootstrapTestKit.Get(restored, "DisplayName"),
            "人物显示名必须由 PersonDefinition 恢复（不是战斗定义名）");

        var restoredProgress = PersonBootstrapTestKit.ProgressOf(restored);
        Assert.IsNotNull(restoredProgress, "有 Active Profession 的人物 Load 后必须仍有 Profession");
        Assert.AreEqual("prof_save_test", restoredProgress.ProfessionId);
        Assert.AreEqual(4, restoredProgress.ProfessionLevel, "已成长等级不得被 Load 重置");
        Assert.AreEqual(12, restoredProgress.ProfessionExp);
        Assert.AreEqual(3, restoredProgress.ProfessionTraitLevel);
        CollectionAssert.Contains(restoredProgress.UnlockedPoolCardIds, "bcard_heal");

        Assert.IsTrue(PersonBootstrapTestKit.AuthorityOf(restored).IsAvailable);
    }

    [Test]
    public void Load_DoesNotBootstrap_LegacyPersonStaysWithoutProfession()
    {
        // Legacy 人物：无 PersonDefinitionId、无 Active Profession（等价迁移前旧档人物）
        object collection = PersonBootstrapTestKit.NewCollection();
        object legacy = PersonBootstrapTestKit.NewAsm("CharacterInstance", "char_001", "旧档人物", 30, 6);
        string personId = (string)PersonBootstrapTestKit.Get(legacy, "PersonId");
        PersonBootstrapTestKit.OwnedCharacters(collection).Add(legacy);
        object card = PersonBootstrapTestKit.NewCard("pcard_legacy", "char_001");
        PersonBootstrapTestKit.OwnedCards(collection).Add(card);
        PersonBootstrapTestKit.InvokeInstance(collection, "BindCharacterCardOwner", "pcard_legacy", personId);

        Assert.IsTrue((bool)PersonBootstrapTestKit.InvokeStatic(PersonBootstrapTestKit.SaveSystemType, "Save", collection));
        object loaded = PersonBootstrapTestKit.InvokeStatic(PersonBootstrapTestKit.SaveSystemType, "Load");

        object restored = PersonBootstrapTestKit.InvokeInstance(loaded, "GetCharacterByPersonId", personId);
        Assert.IsNotNull(restored);
        Assert.IsFalse(PersonBootstrapTestKit.HasActiveProfession(restored),
            "Load 绝不重新执行 Bootstrap：旧档人物不得被凭空补上职业");
        Assert.IsTrue(string.IsNullOrEmpty((string)PersonBootstrapTestKit.Get(restored, "PersonDefinitionId")),
            "旧档缺失 provenance 时保持为空（不猜人物定义）");
    }
}
