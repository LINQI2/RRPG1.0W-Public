using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

/// <summary>
/// Village V4（Gate 1｜Capability → Unlocked Pool）EditMode 测试共享辅助。
///
/// 团结引擎限制：asmdef 测试程序集不能引用预定义程序集 Assembly-CSharp，
/// 因此 PlayerCollection / CharacterInstance / HeroInstance / SaveSystem / VillageRuntimeBridge /
/// VillageCapabilityGrant 经反射访问；
/// Village.Runtime（VillageTime / VillageProjectSystem / VillageProjectResult / …）、
/// Growth.Runtime（IPersonGrowth / IGrowthAuthority / GrowthAuthority / CardGrowthResolver /
/// UnlockedPoolRewardProvider）、Profession.Runtime（ProfessionProgress）、
/// BattleStage1.Runtime（RunState / BattleCardData / BattleCardKind）可强类型访问。
///
/// 全部内容为 Test / Not Frozen（prj_test_* / bcard_* / char_* / hero_001 测试值）。
/// </summary>
public static class VillageV4TestKit
{
    public const BindingFlags F =
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

    public static Type Asm(string typeName) => Type.GetType(typeName + ", Assembly-CSharp");

    public static object NewAsm(string typeName, params object[] ctorArgs)
    {
        return Activator.CreateInstance(Asm(typeName), ctorArgs);
    }

    public static object Get(object o, string name)
    {
        var f = o.GetType().GetField(name, F);
        if (f != null) return f.GetValue(o);
        var p = o.GetType().GetProperty(name, F);
        Assert.IsNotNull(p, "成员不存在：" + name + " @ " + o.GetType().FullName);
        return p.GetValue(o, null);
    }

    public static void Set(object o, string name, object value)
    {
        var f = o.GetType().GetField(name, F);
        if (f != null) { f.SetValue(o, value); return; }
        var p = o.GetType().GetProperty(name, F);
        Assert.IsNotNull(p, "字段/属性不存在：" + name);
        p.SetValue(o, value, null);
    }

    public static object GetStatic(Type type, string name)
    {
        var f = type.GetField(name, F);
        if (f != null) return f.GetValue(null);
        var p = type.GetProperty(name, F);
        Assert.IsNotNull(p, "静态成员不存在：" + name + " @ " + type.FullName);
        return p.GetValue(null, null);
    }

    public static object InvokeStatic(Type type, string method, params object[] args)
    {
        var candidates = type.GetMethods(F).Where(m => m.Name == method).ToList();
        MethodInfo selected = Pick(candidates, args);
        Assert.IsNotNull(selected, "方法不存在或参数不匹配：" + method + " @ " + type.FullName);
        return selected.Invoke(null, args);
    }

    private static MethodInfo Pick(List<MethodInfo> candidates, object[] args)
    {
        foreach (var m in candidates)
        {
            var ps = m.GetParameters();
            if (ps.Length != (args == null ? 0 : args.Length)) continue;
            bool ok = true;
            for (int i = 0; i < ps.Length; i++)
            {
                if (args[i] == null) continue;
                if (!ps[i].ParameterType.IsInstanceOfType(args[i])) { ok = false; break; }
            }
            if (ok) return m;
        }
        return null;
    }

    // ==================== Bridge / Save ====================

    public static Type Bridge => Asm("VillageRuntimeBridge");
    public static Type SaveSystemType => Asm("SaveSystem");
    public static Type CapabilityGrantType => Asm("VillageCapabilityGrant");

    public static void ResetBridge() => InvokeStatic(Bridge, "ResetForTests");
    public static void EnsureInitialized() => InvokeStatic(Bridge, "EnsureInitialized", new object[] { null });

    public static void BindPersonSource(object collection) =>
        InvokeStatic(Bridge, "SetSimulationPersonSource", collection);

    public static VillageTime BridgeTime() => (VillageTime)GetStatic(Bridge, "Time");

    public static void TickDays(int days)
    {
        var t = BridgeTime();
        for (int i = 0; i < days; i++) t.TickDay();
    }

    public static VillageProjectResult StartProject(string defId, params string[] personIds) =>
        (VillageProjectResult)InvokeStatic(Bridge, "TryStartProject", defId, personIds);

    // ==================== Person / Collection 构造（Assembly-CSharp 反射） ====================

    public static object NewPlayerCollection() => NewAsm("PlayerCollection");

    public static IList OwnedHeroes(object collection) => (IList)Get(collection, "OwnedHeroes");
    public static IList OwnedCharacters(object collection) => (IList)Get(collection, "OwnedCharacters");

    /// <summary>创建并加入一个 Hero Person（真实模板 hero_001 用于 Save/Load 路径）。</summary>
    public static IPersonGrowth AddHero(object collection, string heroId = "hero_001", string name = "青鹿", int hp = 30)
    {
        object hero = NewAsm("HeroInstance", heroId, name, hp);
        OwnedHeroes(collection).Add(hero);
        return (IPersonGrowth)hero;
    }

    /// <summary>创建并加入一个普通 Character Person（默认无 Active Profession）。</summary>
    public static IPersonGrowth AddCharacter(object collection, string characterId = "char_001",
        string name = "战士甲", int hp = 30, int atk = 6)
    {
        object character = NewAsm("CharacterInstance", characterId, name, hp, atk);
        OwnedCharacters(collection).Add(character);
        return (IPersonGrowth)character;
    }

    /// <summary>给普通 Character 注入 Test Active Profession Progress（Person-owned；不使用 Formation 内容）。</summary>
    public static void SetActiveProfession(object characterInstance, string professionId, params string[] pool)
    {
        var progress = new ProfessionProgress
        {
            ProfessionId = professionId,
            ProfessionLevel = 1,
            ProfessionExp = 0,
            ProfessionTraitLevel = 1,
            UnlockedPoolCardIds = new List<string>(pool ?? new string[0])
        };
        Set(characterInstance, "ActiveProfessionProgress", progress);
    }

    /// <summary>向某 Person 的 **Legacy** 长期池播种（模拟产品初始 3 张池；直接字段，不经 Authority）。</summary>
    public static void SeedLegacyPool(IPersonGrowth person, params string[] cardIds)
    {
        person.UnlockedPoolCardIds = new List<string>(cardIds ?? new string[0]);
    }

    // ==================== Growth Authority / Pool ====================

    public static IGrowthAuthority AuthorityOf(IPersonGrowth person) => GrowthAuthority.Resolve(person);

    /// <summary>读取该 Person 的权威 Unlocked Pool（Hero → Legacy 池；有 Profession 的 Character → Profession 池）。</summary>
    public static List<string> AuthorityPool(IPersonGrowth person)
    {
        var auth = AuthorityOf(person);
        return auth != null ? auth.UnlockedPoolCardIds : null;
    }

    public static int CountOf(List<string> pool, string cardId) => pool == null ? 0 : pool.Count(id => id == cardId);

    // ==================== Card 模板 / Growth Offer / Run Reward ====================

    public static BattleCardData NewCharacterCard(string cardId)
    {
        var card = UnityEngine.ScriptableObject.CreateInstance<BattleCardData>();
        card.CardId = cardId;
        card.CardKind = BattleCardKind.Character;
        return card;
    }

    /// <summary>产品默认 4 张角色牌模板（bcard_aim / heal / rally / sword，Test 内容）。</summary>
    public static List<BattleCardData> DefaultTemplates()
    {
        return new List<BattleCardData>
        {
            NewCharacterCard("bcard_aim"),
            NewCharacterCard("bcard_heal"),
            NewCharacterCard("bcard_rally"),
            NewCharacterCard("bcard_sword")
        };
    }

    public static List<BattleCardData> BuildOffer(IPersonGrowth person, List<BattleCardData> templates) =>
        CardGrowthResolver.BuildOffer(person, templates);

    public static IReadOnlyList<RunRewardCandidate> BuildRunCandidates(
        RunState runState, IPersonGrowth person, List<BattleCardData> templates)
    {
        string personId = person.PersonId;
        var provider = new UnlockedPoolRewardProvider(pid => pid == personId ? person : null);
        return provider.BuildCandidates(runState, templates);
    }

    public static RunState NewRunWithPerson(IPersonGrowth person)
    {
        var run = new RunState();
        run.GetOrCreatePerson(person.PersonId, person.DisplayName, 30);
        return run;
    }

    // ==================== Gate 2：Return Transaction / Resource 便捷封装 ====================

    public static void SetExpedition(params string[] ids) =>
        InvokeStatic(Bridge, "SetExpeditionPersonIds", (object)ids);

    public static bool BeginReturn(int days) => (bool)InvokeStatic(Bridge, "BeginExpeditionReturn", days);

    public static bool MarkSettled() => (bool)InvokeStatic(Bridge, "MarkExpeditionReturnSettled");

    public static ExpeditionCatchUpResult RunCatchUp() =>
        (ExpeditionCatchUpResult)InvokeStatic(Bridge, "RunReturnCatchUp");

    public static void DiscardReturnRecord() => InvokeStatic(Bridge, "DiscardExpeditionReturnRecord");

    public static void CompleteSave(bool ok) => InvokeStatic(Bridge, "CompleteExpeditionReturnSave", ok);

    public static void SetElapsedDays(int days)
    {
        System.Func<int> provider = () => days;
        InvokeStatic(Bridge, "SetExpeditionElapsedDaysProvider", provider);
    }

    public static ExpeditionRecord ActiveRecord() => (ExpeditionRecord)GetStatic(Bridge, "ActiveReturnRecord");

    public static VillageResourceState Resources() => (VillageResourceState)GetStatic(Bridge, "Resources");

    public static string GetSavePath() => (string)InvokeStatic(SaveSystemType, "GetSavePath");

    public static bool Save(object collection) => (bool)InvokeStatic(SaveSystemType, "Save", collection);

    public static object Load() => InvokeStatic(SaveSystemType, "Load");

    /// <summary>Gate 2：Return Person Consequence 提交（Assembly-CSharp 反射）。</summary>
    public static object TryCommitReturnConsequence(ExpeditionRecord record, RunState runState,
        RunEndMode mode, VillageTime time, object personSource)
    {
        return InvokeStatic(Asm("VillageReturnPersonConsequence"), "TryCommit",
            record, runState, mode, time, personSource);
    }

    /// <summary>Gate 2：跑一次完整 Return 会话（Begin → MarkSettled → CatchUp）。</summary>
    public static ExpeditionCatchUpResult RunReturnSession(int elapsedDays)
    {
        SetElapsedDays(elapsedDays);
        Assert.IsTrue(BeginReturn(elapsedDays), "BeginExpeditionReturn 应成功");
        Assert.IsTrue(MarkSettled(), "MarkExpeditionReturnSettled 应成功");
        return RunCatchUp();
    }

    /// <summary>V4 Gate 2：纯 elapsed 策略（Assembly-CSharp 反射）。</summary>
    public static int ResolveElapsedDays(string nodeTypeName)
    {
        var nodeType = Enum.Parse(Asm("NodeType"), nodeTypeName);
        return (int)InvokeStatic(Asm("LegacyNodeElapsedDaysTestPolicy"), "Resolve", nodeType);
    }

    /// <summary>V4 Gate 2：Run HP → Injury 解析（Assembly-CSharp 反射）。</summary>
    public static string ResolveInjuryName(int currentHp, int maxHp, RunEndMode mode)
    {
        object value = InvokeStatic(Asm("VillageReturnPersonConsequence"), "ResolveInjury",
            currentHp, maxHp, mode);
        return value != null ? value.ToString() : "<null>";
    }

    // ==================== V4 Acceptance Prep：Person Identity 读取（Assembly-CSharp 反射） ====================

    /// <summary>远征选人卡片身份附加文本（职责 / 长期状态 / 远征经历）。</summary>
    public static string IdentitySuffix(object collection, string personId) =>
        (string)InvokeStatic(Asm("ExpeditionPersonIdentity"), "BuildCardSuffix", collection, personId);

    public static VillageAssignmentResult Assign(string personId, string workplaceId) =>
        (VillageAssignmentResult)InvokeStatic(Bridge, "TryAssignPerson", personId, workplaceId);

    public static void SetInjury(object characterOrHeroInstance, string injuryName) =>
        Set(characterOrHeroInstance, "Injury", System.Enum.Parse(Asm("InjuryStatus"), injuryName));

    public static void AppendExpeditionReturned(object characterInstance, int year, int month, int day, int outcome)
    {
        object history = Get(characterInstance, "LifeHistory");
        Assert.IsNotNull(history, "Character LifeHistory 应存在");
        history.GetType().GetMethod("AppendExpeditionReturned", F)
            .Invoke(history, new object[] { year, month, day, outcome });
    }

    /// <summary>Test 岗位显示名（Test / Not Frozen；供身份文本断言用，不硬编码文案）。</summary>
    public static string WorkplaceDisplayName(string workplaceId)
    {
        var defs = (System.Collections.IEnumerable)GetStatic(Bridge, "TestWorkplaceDefs");
        foreach (var def in defs)
        {
            if (def == null) continue;
            var idField = def.GetType().GetField("WorkplaceId", F);
            if (idField == null || (string)idField.GetValue(def) != workplaceId) continue;
            var nameField = def.GetType().GetField("DisplayName", F);
            return nameField != null ? (string)nameField.GetValue(def) : workplaceId;
        }
        return workplaceId;
    }
}
