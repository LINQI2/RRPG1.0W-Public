using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// 人物长期成长 × Run 集成测试。
///
/// 纯逻辑（Growth.Runtime / BattleStage1.Runtime 直接引用）：
///   - Run Deck 从长期 Starter 生成（每槽 1 张副本、同模板多槽、通用牌）；
///   - UnlockedPoolRewardProvider 从长期 Unlocked Pool 读取 Run 奖励候选（含扩展新牌）。
///
/// Assembly-CSharp 接线（经反射，测试程序集不可直接引用 Assembly-CSharp）：
///   - SafeReturn 兑现 PendingGrowthExp 到人物长期经验（不自动升级）；
///   - AdventureManager 创建 Run 时从长期 Starter 生成 Run Deck；
///   - 真实 Character 升级（M4：挂载 ProfessionProgress 后经 ProfessionLevel/ProfessionExp 路由）只改变
///     Growth 数值，不改变 MaxHP/CurrentAttack，也不改 Legacy Level。
/// </summary>
public class GrowthRunIntegrationTests
{
    private const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

    private static BattleCardData NewCard(string cardId, BattleCardKind kind = BattleCardKind.Character)
    {
        var c = ScriptableObject.CreateInstance<BattleCardData>();
        c.CardId = cardId;
        c.DisplayName = cardId;
        c.CardKind = kind;
        return c;
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
        t.Nodes = new List<GrowthNodeConfig>();
        return t;
    }

    // M2 Slice 2A：MonsterDefinition 位于 StaticConfig.Runtime（不是 Assembly-CSharp）→ 解析器加回退。
    private static System.Type Asm(string typeName) => System.Type.GetType(typeName + ", Assembly-CSharp")
        ?? System.Type.GetType("RRPG.StaticConfig." + typeName + ", StaticConfig.Runtime");

    private static object NewAsm(string typeName, params object[] ctorArgs)
    {
        return System.Activator.CreateInstance(Asm(typeName), ctorArgs);
    }

    private static object GetField(object o, string name)
    {
        var f = o.GetType().GetField(name, F);
        if (f != null) return f.GetValue(o);
        var p = o.GetType().GetProperty(name, F);
        return p.GetValue(o, null);
    }

    private static void SetField(object o, string name, object value)
    {
        o.GetType().GetField(name, F).SetValue(o, value);
    }

    private static object MakeList(string elementTypeName)
    {
        var listType = typeof(List<>).MakeGenericType(Asm(elementTypeName));
        return System.Activator.CreateInstance(listType);
    }

    private static void AddTo(object list, object item)
    {
        list.GetType().GetMethod("Add", F).Invoke(list, new[] { item });
    }

    // ==================== Starter → Run Deck（纯逻辑） ====================

    [Test]
    public void StarterDeckBuilder_EachSlotOneCopy_OwnerPerson()
    {
        var person = new TestPerson("char_001", "战士");
        person.StarterSlots.Add(new StarterSlotEntry("bcard_aim", 1));
        person.StarterSlots.Add(new StarterSlotEntry("bcard_sword", 1));
        var templates = new List<BattleCardData> { NewCard("bcard_aim"), NewCard("bcard_sword") };

        var entries = StarterDeckBuilder.BuildStarterEntries(new IPersonGrowth[] { person }, templates);
        Assert.AreEqual(2, entries.Count);
        Assert.IsTrue(entries.All(e => e.PersonId == "char_001"));
        Assert.IsTrue(entries.All(e => e.Copies == 1), "每个 Starter 槽 1 张副本");
    }

    [Test]
    public void StarterDeckBuilder_SameCardTwoSlots_TwoCopies()
    {
        var person = new TestPerson("char_001", "战士");
        person.StarterSlots.Add(new StarterSlotEntry("bcard_sword", 1));
        person.StarterSlots.Add(new StarterSlotEntry("bcard_sword", 1));
        var templates = new List<BattleCardData> { NewCard("bcard_sword") };

        var entries = StarterDeckBuilder.BuildStarterEntries(new IPersonGrowth[] { person }, templates);
        Assert.AreEqual(2, entries.Count, "同模板两个槽 → 两个条目各 1 份（共 2 份）");
    }

    [Test]
    public void StarterDeckBuilder_CommonCards_Included()
    {
        var person = new TestPerson("char_001", "战士");
        person.StarterSlots.Add(new StarterSlotEntry("bcard_aim", 1));
        var templates = new List<BattleCardData>
        {
            NewCard("bcard_aim"),
            NewCard("bcard_common", BattleCardKind.Common)
        };

        var entries = StarterDeckBuilder.BuildStarterEntries(new IPersonGrowth[] { person }, templates);
        Assert.AreEqual(2, entries.Count);
        Assert.IsTrue(entries.Any(e => string.IsNullOrEmpty(e.PersonId) && e.Template.CardId == "bcard_common"),
            "通用牌应进入 Run 牌组");
    }

    [Test]
    public void LevelUp_NextRunDeck_UsesNewStarter()
    {
        // 人物：Starter 初始 = [bcard_aim]
        var person = new TestPerson("char_001", "战士");
        person.StarterSlots.Add(new StarterSlotEntry("bcard_aim", 1));
        person.Exp = 10;
        var curve = NewCurve();

        // 玩家点击升级（消耗经验 + 等级+1）
        Assert.IsTrue(CharacterGrowthService.TryLevelUpOnce(person, curve, EmptyTable(), out _));

        // CardGrowth 选定牌 → 替换 Starter 槽 0
        var replace = CardGrowthResolver.ApplyToStarter(person, "bcard_sword", StarterCardStrategy.ReplaceSlot, 0, curve);
        Assert.IsTrue(replace.Applied);

        // 下一次远征：Run Deck 从新 Starter 生成 → 真实读取替换后的牌
        var entries = StarterDeckBuilder.BuildStarterEntries(new IPersonGrowth[] { person },
            new List<BattleCardData> { NewCard("bcard_aim"), NewCard("bcard_sword") });
        Assert.AreEqual(1, entries.Count);
        Assert.AreEqual("bcard_sword", entries[0].Template.CardId, "下次远征 Run Deck 真实使用新 Starter");
        Assert.AreEqual("char_001", entries[0].PersonId);
    }

    [Test]
    public void RunInsideCopies_DoNotWriteBack_LongTermStarter()
    {
        var person = new TestPerson("char_001", "战士");
        person.StarterSlots.Add(new StarterSlotEntry("bcard_aim", 1));
        person.UnlockedPoolCardIds.AddRange(new[] { "bcard_aim", "bcard_sword" });

        // Run 1：Run Deck 从 Starter 生成；Run 内获得两张同模板牌（长期不写回）
        var run1 = new RunState();
        run1.AddRunDeckEntries(StarterDeckBuilder.BuildStarterEntries(new IPersonGrowth[] { person },
            new List<BattleCardData> { NewCard("bcard_aim"), NewCard("bcard_sword") }));
        run1.AddRunCard("char_001", NewCard("bcard_sword"));
        run1.AddRunCard("char_001", NewCard("bcard_sword"));

        Assert.AreEqual("bcard_aim", person.StarterSlots[0].CardId, "Run 内新增 Copies 不回写长期 Starter");

        // Run 2：Run Deck 仍从长期 Starter 生成
        var entries2 = StarterDeckBuilder.BuildStarterEntries(new IPersonGrowth[] { person },
            new List<BattleCardData> { NewCard("bcard_aim"), NewCard("bcard_sword") });
        Assert.AreEqual(1, entries2.Count);
        Assert.AreEqual("bcard_aim", entries2[0].Template.CardId);
    }

    // ==================== Unlocked Pool → Run 奖励（纯逻辑） ====================

    [Test]
    public void UnlockedPoolRewardProvider_ReadsLongTermPool_AndIncludesExpanded()
    {
        var character = new TestPerson("char_001", "战士");
        character.UnlockedPoolCardIds.AddRange(new[] { "bcard_aim", "bcard_heal" }); // 含扩展新牌

        var run = new RunState();
        run.GetOrCreatePerson("char_001", "战士", 30);

        var provider = new UnlockedPoolRewardProvider(id => id == "char_001" ? character : null);
        var candidates = provider.BuildCandidates(run, new List<BattleCardData>
        {
            NewCard("bcard_aim"), NewCard("bcard_heal"), NewCard("bcard_rally")
        });

        Assert.IsTrue(candidates.Any(c => c.PersonId == "char_001" && c.Template.CardId == "bcard_aim"));
        Assert.IsTrue(candidates.Any(c => c.PersonId == "char_001" && c.Template.CardId == "bcard_heal"),
            "池扩展新牌应进入 Run 奖励候选");
        Assert.IsFalse(candidates.Any(c => c.Template.CardId == "bcard_rally"), "未解锁牌不应出现");
    }

    [Test]
    public void UnlockedPoolRewardProvider_NullLookup_FallsBackToDemoPool()
    {
        var run = new RunState();
        run.GetOrCreatePerson("char_001", "战士", 30);
        var provider = new UnlockedPoolRewardProvider(null);
        var candidates = provider.BuildCandidates(run, new List<BattleCardData> { NewCard("bcard_aim") });
        Assert.IsTrue(candidates.Count > 0, "缺失长期数据时回退临时内容种子");
    }

    // ==================== Assembly-CSharp 接线（反射） ====================

    [Test]
    public void SafeReturn_CommitsExpToLongTerm_NoAutoLevel()
    {
        var pc = NewAsm("PlayerCollection");
        var character = NewAsm("CharacterInstance", "char_001", "战士", 30, 6);
        var hero = NewAsm("HeroInstance", "hero_001", "青鹿", 35);
        ((System.Collections.IList)GetField(pc, "OwnedCharacters")).Add(character);
        ((System.Collections.IList)GetField(pc, "OwnedHeroes")).Add(hero);

        // M1：PendingGrowthExp 按真实 PersonId 归属（不再用模板 CharacterId / HeroId）
        string charPid = (string)GetField(character, "PersonId");
        string heroPid = (string)GetField(hero, "PersonId");

        var run = new RunState();
        run.AddPendingGold(20);
        run.AddPendingGrowthExp(charPid, 10);
        run.AddPendingGrowthExp(heroPid, 10);

        var sink = NewAsm("PlayerRunRewardSink", pc);
        run.CommitPendingRewards((IRunRewardSink)sink, 0f);

        Assert.AreEqual(10, GetField(character, "Exp"), "SafeReturn 后 PendingGrowthExp 进入人物长期经验");
        Assert.AreEqual(10, GetField(hero, "Exp"));
        Assert.AreEqual(1, GetField(character, "Level"), "经验入账后不自动升级");
        Assert.AreEqual(20, GetField(pc, "Gold"));
    }

    [Test]
    public void AdventureManager_SeedsRunDeck_FromStarter()
    {
        var pc = NewAsm("PlayerCollection");
        var character = NewAsm("CharacterInstance", "char_001", "战士", 30, 6);
        var hero = NewAsm("HeroInstance", "hero_001", "青鹿", 35);

        // 设置非默认 Starter（bcard_sword）
        var slots = (System.Collections.IList)GetField(character, "StarterSlots");
        slots.Add(new StarterSlotEntry("bcard_sword", 1));
        ((System.Collections.IList)GetField(pc, "OwnedCharacters")).Add(character);
        ((System.Collections.IList)GetField(pc, "OwnedHeroes")).Add(hero);

        // 角色卡（Assembly-CSharp 类型经反射创建；必须走 ScriptableObject.CreateInstance）
        var charCard = typeof(ScriptableObject).GetMethod("CreateInstance", new[] { typeof(System.Type) })
            .Invoke(null, new object[] { Asm("CharacterCardData") });
        SetField(charCard, "CardId", "ccard_test");
        SetField(charCard, "CharacterId", "char_001");
        // Foundation Identity Cleanup：产品路径卡→Person 必须显式绑定；不再允许“唯一模板候选”自动推断。
        pc.GetType().GetMethod("BindCharacterCardOwner", F)
            .Invoke(pc, new object[] { "ccard_test", GetField(character, "PersonId") });
        var playerChars = MakeList("CharacterCardData");
        AddTo(playerChars, charCard);

        var dm = Asm("DataManager").GetProperty("Instance", BindingFlags.Public | BindingFlags.Static).GetValue(null);
        var allChars = dm.GetType().GetProperty("AllCharacters").GetValue(dm);
        var allSkills = dm.GetType().GetProperty("AllSkills").GetValue(dm);
        // M2 Slice 2A：AdventureManager 只接受 MonsterDefinition（本用例不依赖敌人内容 → 空列表即可）
        var allMonsters = MakeList("MonsterDefinition");

        var adventureType = Asm("AdventureManager");
        var ctor = adventureType.GetConstructor(new[]
        {
            Asm("PlayerCollection"),
            playerChars.GetType(),
            Asm("HeroInstance"),
            allChars.GetType(),
            allSkills.GetType(),
            allMonsters.GetType(),
            Asm("RunConfig")
        });
        Assert.IsNotNull(ctor, "未找到 AdventureManager 构造函数");
        var adventure = ctor.Invoke(new object[]
        {
            pc, playerChars, hero, allChars, allSkills, allMonsters, null
        });

        var runState = (RunState)adventure.GetType().GetProperty("RunState", F).GetValue(adventure);
        // M1：Run Deck 归属使用人物实例的真实 PersonId（不再是模板 char_001）
        string personId = (string)GetField(character, "PersonId");
        Assert.IsFalse(string.IsNullOrEmpty(personId), "人物实例应拥有 PersonId");
        Assert.IsTrue(runState.RunDeckEntries.Any(e => e.PersonId == personId && e.Template.CardId == "bcard_sword"),
            "Run Deck 应从人物长期 Starter 生成（非 DemoCardPool），并归属该实例 PersonId");
    }

    [Test]
    public void LevelUp_DoesNotChangeBaseStats_OnRealCharacter()
    {
        var character = NewAsm("CharacterInstance", "char_001", "战士", 30, 6);
        // M4：普通 Character 的 Growth 权威 = ActiveProfessionProgress。真实宿主参与升级前必须挂载
        // ProfessionProgress（数据级测试种子；Formation 属未来 M 阶段，测试不实现 Formation 流程）。
        var ppType = System.Type.GetType("ProfessionProgress, Profession.Runtime");
        Assert.IsNotNull(ppType, "ProfessionProgress（Profession.Runtime）应存在");
        var pp = System.Activator.CreateInstance(ppType);
        SetField(pp, "ProfessionId", "profession_test_hunter");
        SetField(pp, "ProfessionLevel", 1);
        SetField(pp, "ProfessionExp", 10); // 测试曲线 [10,15,...]：Lv1→Lv2 需 10
        SetField(pp, "ProfessionTraitLevel", 0);
        SetField(pp, "UnlockedPoolCardIds", new List<string>());
        character.GetType().GetProperty("ActiveProfessionProgress", F).SetValue(character, pp);

        int hpBefore = (int)GetField(character, "MaxHP");
        int atkBefore = (int)GetField(character, "CurrentAttack");

        // 真实 CharacterInstance 作为 IPersonGrowth 参与升级（经 ProfessionProgress 路由）
        var curve = NewCurve();
        Assert.IsTrue(CharacterGrowthService.TryLevelUpOnce((IPersonGrowth)character, curve, EmptyTable(), out _));
        Assert.AreEqual(2, GetField(pp, "ProfessionLevel"), "普通 Character 升级应写入 ProfessionLevel");
        Assert.AreEqual(0, GetField(pp, "ProfessionExp"), "升级应消耗 ProfessionExp");
        Assert.AreEqual(1, GetField(character, "Level"), "M4：普通 Character 升级不得修改 Legacy Level");
        Assert.AreEqual(hpBefore, GetField(character, "MaxHP"), "等级不增加 HP");
        Assert.AreEqual(atkBefore, GetField(character, "CurrentAttack"), "等级不增加 Attack");
    }
}


