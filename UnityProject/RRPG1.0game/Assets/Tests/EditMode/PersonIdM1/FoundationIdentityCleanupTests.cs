using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Foundation Identity Cleanup 专项 EditMode 测试（M1 底座之上，Identity Cleanup Gate）。
///
/// 锁定规则（真实产品 Person truth 路径只允许 PersonId）：
///   A. Card→Person 只能由显式绑定解析；同模板两个 Person 或多个模板候选都不允许推断；
///     即使模板候选唯一、但没有显式 card binding，仍不得推断（null + warning）。
///   B. Save 旧档：Person 缺 PersonId 时生成独立新 PersonId；旧卡无 owner binding 不唯一模板猜绑；
///     已存在 PersonId Save/Load 稳定保持。
///   C. Run 播种 / 远征登记 / Village 输入 / Battle 结算：缺真实 PersonId 一律 fail/skip + 告警，
///     不把 CharacterId / HeroId 作为 Person truth；纯 Demo（PlayerData==null）临时模板 key 仍可运行。
///   D. BattleUI 产品 Run owner 解析失败 → 不制造临时 Person truth（GetPersonIdOfUnit 返回 null / 部署跳过）。
/// </summary>
public class FoundationIdentityCleanupTests
{
    private static readonly Type BridgeType = M1TestKit.Asm("VillageRuntimeBridge");
    private static readonly Type SaveSystemType = M1TestKit.Asm("SaveSystem");

    [SetUp]
    public void SetUp()
    {
        string p = null;
        try
        {
            p = (string)M1TestKit.InvokeStatic(SaveSystemType, "GetSavePath");
            if (p != null && File.Exists(p)) File.Delete(p);
        }
        catch
        {
            // 忽略清理异常：单测隔离优先
        }
        M1TestKit.InvokeStatic(BridgeType, "ResetForTests");
    }

    [TearDown]
    public void TearDown()
    {
        string p = null;
        try
        {
            p = (string)M1TestKit.InvokeStatic(SaveSystemType, "GetSavePath");
            if (p != null && File.Exists(p)) File.Delete(p);
        }
        catch
        {
            // 忽略清理异常
        }
        M1TestKit.InvokeStatic(BridgeType, "ResetForTests");
    }

    // ==================== 基础设施 ====================

    private static object MakeList(string elementType)
    {
        var t = typeof(List<>).MakeGenericType(M1TestKit.Asm(elementType));
        return Activator.CreateInstance(t);
    }

    private static void AddTo(object list, object item)
    {
        list.GetType().GetMethod("Add", M1TestKit.F).Invoke(list, new[] { item });
    }

    /// <summary>给 PlayerCollection 添加一个真实角色实例 + 角色卡 + 显式绑定；返回该卡。</summary>
    private static object AddBoundCharacter(object collection, string templateId, string cardId, out string personId)
    {
        object character = M1TestKit.NewCharacter(templateId, templateId, 30);
        personId = M1TestKit.PersonIdOf(character);
        object card = M1TestKit.NewCharacterCard(cardId, templateId);
        M1TestKit.OwnedCharacters(collection).Add(character);
        M1TestKit.OwnedCharacterCards(collection).Add(card);
        M1TestKit.InvokeInstance(collection, "BindCharacterCardOwner", cardId, personId);
        return card;
    }

    /// <summary>为角色挂载一个 Active Profession 进度（反射；仅测试种子，不属于 Formation 流程）。</summary>
    private static void SetActiveProfessionSeed(object character)
    {
        var ppType = Type.GetType("ProfessionProgress, Profession.Runtime");
        Assert.IsNotNull(ppType, "Profession.Runtime 应已加载");
        object pp = Activator.CreateInstance(ppType);
        M1TestKit.Set(pp, "ProfessionId", "profession_test_identity");
        M1TestKit.Set(pp, "ProfessionLevel", 1);
        M1TestKit.Set(pp, "ProfessionExp", 0);
        M1TestKit.Set(pp, "ProfessionTraitLevel", 0);
        M1TestKit.Set(pp, "UnlockedPoolCardIds", new List<string>());
        M1TestKit.Set(character, "ActiveProfessionProgress", pp);
    }

    /// <summary>反射构造 AdventureManager（产品 / Demo 共用）。playerData 可传 null（Demo）。</summary>
    private static object NewAdventureManager(object playerData, object playerChars, object hero, out RunState runState)
    {
        var allChars = MakeList("CharacterData");
        var allSkills = MakeList("SkillData");
        var allMonsters = MakeList("MonsterDefinition");

        var advType = M1TestKit.Asm("AdventureManager");
        var ctor = advType.GetConstructor(new[]
        {
            M1TestKit.Asm("PlayerCollection"),
            playerChars.GetType(),
            M1TestKit.Asm("HeroInstance"),
            allChars.GetType(),
            allSkills.GetType(),
            allMonsters.GetType(),
            M1TestKit.Asm("RunConfig")
        });
        Assert.IsNotNull(ctor, "AdventureManager 构造函数未找到");
        object adventure = ctor.Invoke(new object[]
        {
            playerData, playerChars, hero, allChars, allSkills, allMonsters, null
        });
        runState = (RunState)adventure.GetType().GetProperty("RunState", M1TestKit.F).GetValue(adventure);
        return adventure;
    }

    private static void SetExpedition(IEnumerable<string> ids)
    {
        M1TestKit.InvokeStatic(BridgeType, "SetExpeditionPersonIds", ids);
    }

    private static List<string> ExpeditionPersonIds()
    {
        return new List<string>((IReadOnlyList<string>)M1TestKit.InvokeStatic(BridgeType, "get_ExpeditionPersonIds"));
    }

    private static object NewSettlement()
    {
        return Activator.CreateInstance(M1TestKit.Asm("BattleSettlement"));
    }

    private static void ProcessSettlement(object settlement, bool isVictory, object allCharacters,
        List<string> deployedPersonIds, object hero, RunState run)
    {
        settlement.GetType().GetMethod("ProcessSettlement", M1TestKit.F)
            .Invoke(settlement, new object[] { isVictory, allCharacters, deployedPersonIds, hero, run });
    }

    private static object MakeBattleUnit(string unitId, string sourceId)
    {
        var unitType = Enum.Parse(M1TestKit.Asm("UnitType"), "Character");
        return Activator.CreateInstance(M1TestKit.Asm("BattleUnit"),
            new object[] { unitId, sourceId, unitType, 30, 6, 1, 1, 1, "player" });
    }

    // ==================== A. Card Ownership ====================

    [Test]
    public void SameCharacterId_TwoPersons_NoBinding_ResolveIsNull_NoTemplateInference()
    {
        object collection = M1TestKit.NewPlayerCollection();
        object p1 = M1TestKit.NewCharacter("char_001", "战士甲", 30);
        object p2 = M1TestKit.NewCharacter("char_001", "战士乙", 30);
        M1TestKit.OwnedCharacters(collection).Add(p1);
        M1TestKit.OwnedCharacters(collection).Add(p2);
        object card = M1TestKit.NewCharacterCard("ccard_two_persons", "char_001");
        M1TestKit.OwnedCharacterCards(collection).Add(card);

        object owner = M1TestKit.InvokeInstance(collection, "GetCharacterInstanceForCard", card);
        Assert.IsNull(owner, "同 CharacterId 两个 Person 且无绑定 → 不得推断 owner");
        string resolved = (string)M1TestKit.InvokeInstance(collection, "ResolvePersonIdForCard", card);
        Assert.IsNull(resolved, "同 CharacterId 两个 Person 且无绑定 → ResolvePersonIdForCard 必须返回 null");
        var exported = (IList)M1TestKit.InvokeInstance(collection, "BuildCharacterCardOwnerSaveData");
        Assert.AreEqual(0, exported.Count, "未绑定卡不得被悄悄绑定/导出");
    }

    [Test]
    public void SingleCandidate_NoExplicitBinding_StillNotInferred()
    {
        object collection = M1TestKit.NewPlayerCollection();
        object p1 = M1TestKit.NewCharacter("char_001", "唯一战士", 30);
        M1TestKit.OwnedCharacters(collection).Add(p1);
        object card = M1TestKit.NewCharacterCard("ccard_single_unbound", "char_001");
        M1TestKit.OwnedCharacterCards(collection).Add(card);

        // 只有一个 CharacterId 候选但无 card binding → 仍不得推断 owner。
        object owner = M1TestKit.InvokeInstance(collection, "GetCharacterInstanceForCard", card);
        Assert.IsNull(owner, "唯一模板候选且无显式绑定 → 仍不得推断 owner");
        string resolved = (string)M1TestKit.InvokeInstance(collection, "ResolvePersonIdForCard", card);
        Assert.IsNull(resolved, "唯一模板候选且无显式绑定 → ResolvePersonIdForCard 必须返回 null");
        var exported = (IList)M1TestKit.InvokeInstance(collection, "BuildCharacterCardOwnerSaveData");
        Assert.AreEqual(0, exported.Count, "唯一候选推断被禁止，绑定不得被自动创建/导出");
    }

    [Test]
    public void ExplicitCardBinding_ResolvesCorrectPerson()
    {
        object collection = M1TestKit.NewPlayerCollection();
        object p1 = M1TestKit.NewCharacter("char_001", "战士甲", 30);
        object p2 = M1TestKit.NewCharacter("char_001", "战士乙", 30);
        M1TestKit.OwnedCharacters(collection).Add(p1);
        M1TestKit.OwnedCharacters(collection).Add(p2);
        object card = M1TestKit.NewCharacterCard("ccard_bound_to_p2", "char_001");
        M1TestKit.OwnedCharacterCards(collection).Add(card);
        M1TestKit.InvokeInstance(collection, "BindCharacterCardOwner",
            M1TestKit.Get(card, "CardId"), M1TestKit.PersonIdOf(p2));

        object owner = M1TestKit.InvokeInstance(collection, "GetCharacterInstanceForCard", card);
        Assert.AreSame(p2, owner, "显式 card binding → 必须解析到绑定的具体 Person");
        Assert.AreNotSame(p1, owner, "显式绑定不得命中同模板的另一 Person");
    }

    [Test]
    public void Rebuild_DoesNotAutoBindEvenForSingleCandidate()
    {
        object collection = M1TestKit.NewPlayerCollection();
        object p1 = M1TestKit.NewCharacter("char_001", "唯一战士", 30);
        M1TestKit.OwnedCharacters(collection).Add(p1);
        object card = M1TestKit.NewCharacterCard("ccard_old_single", "char_001");
        M1TestKit.OwnedCharacterCards(collection).Add(card);

        M1TestKit.InvokeInstance(collection, "RebuildCharacterCardOwnersFromCardOrder");

        object owner = M1TestKit.InvokeInstance(collection, "GetCharacterInstanceForCard", card);
        Assert.IsNull(owner, "旧档重建不得对唯一候选自动补绑 Card owner");
        var exported = (IList)M1TestKit.InvokeInstance(collection, "BuildCharacterCardOwnerSaveData");
        Assert.AreEqual(0, exported.Count, "Rebuild 不得悄悄建立/导出绑定");
    }

    // ==================== B. Save Boundary ====================

    [Test]
    public void OldSave_PersonWithoutPersonId_GetsNewPersonId_ButUnboundCard_StaysUnresolved()
    {
        // 单实例 + 单卡、无任何 owner binding 的旧档：
        //   - 实例生成独立新 PersonId（≠ CharacterId），Save→Load 稳定；
        //   - 卡不唯一模板猜绑，保持 unresolved；重复 Load 后仍 unresolved。
        string json = "{\"SaveVersion\":1,\"GameVersion\":\"0.2.3\",\"SavedAtUnixTime\":0,"
                    + "\"Player\":{\"Gold\":100,\"Heroes\":[],\"Characters\":["
                    + "{\"CharacterId\":\"char_001\",\"Level\":1,\"Exp\":0,\"CurrentAttack\":6,"
                    + "\"Injury\":0,\"IsDead\":false,\"MaxHP\":30}],"
                    + "\"OwnedCharacterCardIds\":[\"ccard_zz_old_char_001_single\"],\"OwnedItemCardIds\":[]}}";
        string path = (string)M1TestKit.InvokeStatic(SaveSystemType, "GetSavePath");
        File.WriteAllText(path, json);

        object loaded1 = M1TestKit.InvokeStatic(SaveSystemType, "Load");
        Assert.IsNotNull(loaded1, "旧档必须可加载");
        Assert.AreEqual(1, M1TestKit.OwnedCharacters(loaded1).Count);
        string pid1 = M1TestKit.PersonIdOf(M1TestKit.OwnedCharacters(loaded1)[0]);
        Assert.IsFalse(string.IsNullOrEmpty(pid1), "旧档实例 Load 后应自动生成 PersonId");
        Assert.AreNotEqual(pid1, "char_001", "不能用 CharacterId 充当 PersonId");

        Assert.AreEqual(1, M1TestKit.OwnedCharacterCards(loaded1).Count);
        object card = M1TestKit.OwnedCharacterCards(loaded1)[0];
        object owner1 = M1TestKit.InvokeInstance(loaded1, "GetCharacterInstanceForCard", card);
        Assert.IsNull(owner1, "旧档单卡无 owner binding → 不唯一模板猜绑（unresolved）");

        Assert.IsTrue((bool)M1TestKit.InvokeStatic(SaveSystemType, "Save", loaded1), "迁移后第一次保存应成功");
        object loaded2 = M1TestKit.InvokeStatic(SaveSystemType, "Load");
        Assert.IsNotNull(loaded2);
        Assert.AreEqual(1, M1TestKit.OwnedCharacters(loaded2).Count);
        Assert.AreEqual(pid1, M1TestKit.PersonIdOf(M1TestKit.OwnedCharacters(loaded2)[0]),
            "生成的 PersonId 再次 Load 必须保持稳定");
        Assert.AreEqual(1, M1TestKit.OwnedCharacterCards(loaded2).Count);
        object owner2 = M1TestKit.InvokeInstance(loaded2, "GetCharacterInstanceForCard",
            M1TestKit.OwnedCharacterCards(loaded2)[0]);
        Assert.IsNull(owner2, "再次 Save→Load 后卡仍不得被模板猜绑");
    }

    [Test]
    public void ExistingPersonId_SaveLoad_StaysStable_WithExplicitBinding()
    {
        object collection = M1TestKit.NewPlayerCollection();
        AddBoundCharacter(collection, "char_001", "ccard_zz_char_001_stable", out string pidBefore);
        Assert.IsFalse(string.IsNullOrEmpty(pidBefore));

        Assert.IsTrue((bool)M1TestKit.InvokeStatic(SaveSystemType, "Save", collection));
        object loaded = M1TestKit.InvokeStatic(SaveSystemType, "Load");
        Assert.IsNotNull(loaded);
        string pidAfter = M1TestKit.PersonIdOf(M1TestKit.OwnedCharacters(loaded)[0]);
        Assert.AreEqual(pidBefore, pidAfter, "已存在 PersonId Save/Load 必须稳定保持");
        object owner = M1TestKit.InvokeInstance(loaded, "GetCharacterInstanceForCard",
            M1TestKit.OwnedCharacterCards(loaded)[0]);
        Assert.IsNotNull(owner, "显式绑定应随 Save/Load 保留");
        Assert.AreEqual(pidAfter, M1TestKit.PersonIdOf(owner), "显式卡绑定应还原到同一 Person");
    }

    // ==================== C. AdventureManager / Run Seed / Village ====================

    [Test]
    public void ProductRun_UnboundCard_SingleCandidate_NotSeeded_NotRegistered()
    {
        object collection = M1TestKit.NewPlayerCollection();
        object p1 = M1TestKit.NewCharacter("char_001", "唯一战士", 30);
        M1TestKit.OwnedCharacters(collection).Add(p1);
        object card = M1TestKit.NewCharacterCard("ccard_unbound_run", "char_001");
        M1TestKit.OwnedCharacterCards(collection).Add(card);
        var playerChars = MakeList("CharacterCardData");
        AddTo(playerChars, card);

        object adventure = NewAdventureManager(collection, playerChars, null, out RunState runState);
        string pid1 = M1TestKit.PersonIdOf(p1);

        Assert.AreEqual(0, runState.Persons.Count,
            "产品 Run：无显式绑定的卡不得进入 Run Person truth");
        Assert.IsFalse(runState.Persons.ContainsKey(pid1), "不能因为模板候选唯一就推断并播种 Person");
        Assert.IsFalse(runState.Persons.ContainsKey("char_001"), "不得以模板 CharacterId 作为 Run Person key");

        var expeditionIds = (IList<string>)adventure.GetType()
            .GetMethod("GetExpeditionPersonIds", M1TestKit.F).Invoke(adventure, null);
        Assert.AreEqual(0, expeditionIds.Count, "产品 Run：unresolved 卡不得登记为远征 Person");
    }

    [Test]
    public void ProductRun_HeroMissingPersonId_NotSeededWithHeroId()
    {
        object collection = M1TestKit.NewPlayerCollection();
        object hero = M1TestKit.NewHero();
        M1TestKit.Set(hero, "PersonId", "");
        M1TestKit.OwnedHeroes(collection).Add(hero);
        var playerChars = MakeList("CharacterCardData");

        object adventure = NewAdventureManager(collection, playerChars, hero, out RunState runState);
        Assert.AreEqual(0, runState.Persons.Count,
            "产品 Run：Hero 缺真实 PersonId 时不得用 HeroId 播种 Run Person");
        Assert.IsFalse(runState.Persons.ContainsKey("hero_001"), "不得以模板 HeroId 作为 Run Person key");

        var expeditionIds = (IList<string>)adventure.GetType()
            .GetMethod("GetExpeditionPersonIds", M1TestKit.F).Invoke(adventure, null);
        CollectionAssert.DoesNotContain(expeditionIds, "hero_001",
            "产品 Run：Hero 缺 PersonId 时不得把模板 HeroId 登记为远征占用");
    }

    [Test]
    public void DemoRun_NullPlayerData_StillUsesTemporaryTemplateKeys()
    {
        // 纯 Demo/Test（PlayerData == null）链路：仍可运行临时模板 key（不进入长期 truth）。
        object card = M1TestKit.NewCharacterCard("ccard_demo", "char_001");
        var playerChars = MakeList("CharacterCardData");
        AddTo(playerChars, card);
        object hero = M1TestKit.NewHero();
        M1TestKit.Set(hero, "PersonId", "");

        object adventure = NewAdventureManager(null, playerChars, hero, out RunState runState);
        Assert.IsTrue(runState.Persons.ContainsKey("char_001"),
            "Demo Run：角色卡缺 PlayerData 时允许模板 CharacterId 作为本 Run 临时标识");
        Assert.IsTrue(runState.Persons.ContainsKey("hero_001"),
            "Demo Run：Hero 缺 PersonId 且无 PlayerData 时允许模板 HeroId 作为本 Run 临时标识");

        var expeditionIds = (IList<string>)adventure.GetType()
            .GetMethod("GetExpeditionPersonIds", M1TestKit.F).Invoke(adventure, null);
        CollectionAssert.Contains(expeditionIds, "char_001", "Demo 链路允许临时模板 key 登记（仅运行时）");
    }

    [Test]
    public void VillageInput_EmptyPersonId_IsSkipped_NotTemplated()
    {
        object collection = M1TestKit.NewPlayerCollection();
        object badCharacter = M1TestKit.NewCharacter("char_bad", "无身份者", 30);
        M1TestKit.Set(badCharacter, "PersonId", "");
        M1TestKit.OwnedCharacters(collection).Add(badCharacter);

        object goodCharacter = M1TestKit.NewCharacter("char_good", "有身份者", 30);
        M1TestKit.OwnedCharacters(collection).Add(goodCharacter);
        string goodPid = M1TestKit.PersonIdOf(goodCharacter);

        var inputs = (List<VillagePersonInput>)M1TestKit.InvokeStatic(
            BridgeType, "BuildPersonInputs", collection);
        Assert.IsFalse(inputs.Any(i => i.PersonId == "char_bad"),
            "Village 输入：空 PersonId 不得回退为模板 CharacterId");
        Assert.IsTrue(inputs.Any(i => i.PersonId == goodPid), "真实 Person 应进入 Village 输入");
    }

    [Test]
    public void ExpeditionOccupancy_EmptyHeroPersonId_DoesNotUseTemplateHeroId()
    {
        // 场景：legacy Village 登记存在模板 key "hero_001"（历史污染）。
        // 新规则：选中英雄缺真实 PersonId 时跳过 occupancy 检查，不得把模板 HeroId 当 Person。
        M1TestKit.InvokeStatic(BridgeType, "ResetForTests");
        M1TestKit.InvokeStatic(BridgeType, "EnsureInitialized", new object[] { null });
        object collection = M1TestKit.NewPlayerCollection();
        object card = AddBoundCharacter(collection, "char_001", "card_hero_exp_guard", out string pid);
        object hero = M1TestKit.NewHero();
        M1TestKit.Set(hero, "PersonId", "");
        M1TestKit.OwnedHeroes(collection).Add(hero);

        SetExpedition(new List<string> { "hero_001" }); // 模板 key 历史污染（旧实现会误判占用）

        var expType = M1TestKit.Asm("ExpeditionSystem");
        object exp = Activator.CreateInstance(expType, new object[] { collection });
        var selected = (IList)exp.GetType().GetProperty("SelectedCharacters", M1TestKit.F).GetValue(exp, null);
        selected.Add(card);
        exp.GetType().GetMethod("SelectHero", M1TestKit.F)
            .Invoke(exp, new object[] { hero, null });

        var confirm = exp.GetType().GetMethod("ConfirmExpedition", M1TestKit.F);
        var args = new object[2];
        bool ok = (bool)confirm.Invoke(exp, args);
        Assert.IsTrue(ok,
            "空 PersonId 英雄不得因模板 HeroId 历史污染被误判占用；Confirm 应成功（char 已绑定且空闲）");
        CollectionAssert.DoesNotContain(ExpeditionPersonIds(), pid, "失败 Confirm 不得新增登记");
    }

    // ==================== D. Battle Settlement / BattleUI ====================

    [Test]
    public void BattleProduct_MissingCharacterPersonId_WritesNoRunPending_NoTemplatePerson()
    {
        object character = M1TestKit.NewCharacter("char_001", "战士", 30);
        M1TestKit.Set(character, "PersonId", "");      // 非法身份（产品路径缺失）
        SetActiveProfessionSeed(character);            // 使 Profession Pending 路径可达
        var list = MakeList("CharacterInstance");
        AddTo(list, character);

        var run = new RunState();
        object settlement = NewSettlement();
        // 传旧式模板 CharacterId 部署列表（模拟历史污染调用方）：新规则不得创建模板 key 的 Run Person。
        ProcessSettlement(settlement, true, list, new List<string> { "char_001" }, null, run);

        Assert.AreEqual(0, run.Persons.Count,
            "Battle 产品路径：PersonId 缺失时不得以模板 CharacterId 写入 Run Pending / 创建 Person");
        var results = (IList)settlement.GetType().GetProperty("CharacterResults", M1TestKit.F).GetValue(settlement, null);
        Assert.AreEqual(1, results.Count);
        var result = results[0];
        Assert.AreEqual("char_001", (string)M1TestKit.Get(result, "CharacterId"));
        Assert.IsTrue(string.IsNullOrEmpty((string)M1TestKit.Get(result, "PersonId")),
            "CharacterResult.PersonId 应为空（unresolved），不得回退模板 CharacterId");
        Assert.AreEqual(0, (int)M1TestKit.Get(result, "ExpGained"),
            "空 PersonId 不应计入 Pending ProfessionExp");
    }

    [Test]
    public void BattleProduct_HeroMissingPersonId_WritesNoLegacyPending_NoHeroIdPerson()
    {
        object hero = M1TestKit.NewHero();
        M1TestKit.Set(hero, "PersonId", "");

        var run = new RunState();
        object settlement = NewSettlement();
        ProcessSettlement(settlement, true, MakeList("CharacterInstance"),
            new List<string>(), hero, run);

        Assert.AreEqual(0, run.Persons.Count,
            "Battle 产品路径：Hero 缺 PersonId 时不得用 HeroId 写 Run PendingGrowthExp / 创建 Person");
        Assert.IsFalse(run.Persons.ContainsKey("hero_001"), "不得创建模板 HeroId 的 Run Person");
    }

    [Test]
    public void BattleUI_ProductRun_UnresolvedOwner_ReturnsNull_NoTempPersonTruth()
    {
        // BattleUI.GetPersonIdOfUnit 是 DoSettlement / WriteBackRunHp / SyncDownedPersonCards 的统一映射点。
        // 产品 Run（_playerData != null && _runState != null）owner 解析失败 → 必须返回 null，
        // 不得把模板 SourceId 或本场临时实例 PersonId 当作 Run 人物身份。
        object collection = M1TestKit.NewPlayerCollection();
        object p1 = M1TestKit.NewCharacter("char_001", "战士", 30);
        M1TestKit.OwnedCharacters(collection).Add(p1);
        object card = M1TestKit.NewCharacterCard("ccard_ui_unbound", "char_001");
        M1TestKit.OwnedCharacterCards(collection).Add(card);
        var cardsList = MakeList("CharacterCardData");
        AddTo(cardsList, card);

        var uiType = M1TestKit.Asm("BattleUI");
        Assert.IsNotNull(uiType, "BattleUI 应存在");
        var go = new GameObject("_IdentityCleanupBattleUITest");
        try
        {
            object ui = go.AddComponent(uiType);
            const BindingFlags f = M1TestKit.F;
            ui.GetType().GetField("_runState", f).SetValue(ui, new RunState());
            ui.GetType().GetField("_playerData", f).SetValue(ui, collection);
            ui.GetType().GetField("_allCharCards", f).SetValue(ui, cardsList);

            object unit = MakeBattleUnit("ccard_ui_unbound", "char_001");
            string pid = (string)ui.GetType().GetMethod("GetPersonIdOfUnit", f).Invoke(ui, new object[] { unit });
            Assert.IsNull(pid,
                "产品 Run owner 解析失败 → 返回 null，不制造临时 Person truth（不用模板 SourceId）");

            // 控制组：显式绑定后应解析到该具体 Person。
            M1TestKit.InvokeInstance(collection, "BindCharacterCardOwner", "ccard_ui_unbound",
                M1TestKit.PersonIdOf(p1));
            string bound = (string)ui.GetType().GetMethod("GetPersonIdOfUnit", f).Invoke(ui, new object[] { unit });
            Assert.AreEqual(M1TestKit.PersonIdOf(p1), bound, "显式绑定 → 应解析到真实 PersonId");

            // Demo（无 PlayerData）Run：仍返回临时模板 key（不进入长期 truth）。
            ui.GetType().GetField("_playerData", f).SetValue(ui, null);
            string demo = (string)ui.GetType().GetMethod("GetPersonIdOfUnit", f).Invoke(ui, new object[] { unit });
            Assert.AreEqual("char_001", demo, "Demo Run（无 PlayerData）可返回临时模板 key");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(go);
        }
    }
}
