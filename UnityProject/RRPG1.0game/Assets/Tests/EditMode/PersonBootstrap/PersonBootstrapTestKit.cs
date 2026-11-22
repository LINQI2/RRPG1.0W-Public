using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Initial Person &amp; Profession Bootstrap — EditMode 测试共享辅助（Disposable Development Test）。
///
/// 团结引擎限制：asmdef 测试程序集不能引用预定义程序集 Assembly-CSharp，
/// 因此 CharacterInstance / PlayerCollection / SaveSystem / PersonBootstrapper /
/// VillageRuntimeBridge / VillageCapabilityGrant / DataManager 经反射访问；
/// Person.Runtime（PersonDefinition / InitialProfessionConfig / PersonDefinitionCatalog / Validator）、
/// Profession.Runtime（ProfessionDefinition / Registry / ProfessionProgress）、
/// Growth.Runtime（IPersonGrowth / IGrowthAuthority / GrowthAuthority / CardGrowthResolver / UnlockedPoolRewardProvider）、
/// BattleStage1.Runtime（RunState / BattleCardData）可强类型访问。
///
/// 内容全部为 Test / Not Frozen。
/// </summary>
public static class PersonBootstrapTestKit
{
    public const BindingFlags F =
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

    public static Type Asm(string typeName) => Type.GetType(typeName + ", Assembly-CSharp");

    public static readonly Type BootstrapperType = Asm("PersonBootstrapper");
    public static readonly Type DataManagerType = Asm("DataManager");
    public static readonly Type BridgeType = Asm("VillageRuntimeBridge");
    public static readonly Type CapabilityType = Asm("VillageCapabilityGrant");
    public static readonly Type SaveSystemType = Asm("SaveSystem");

    public static object NewAsm(string typeName, params object[] ctorArgs)
        => Activator.CreateInstance(Asm(typeName), ctorArgs);

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

    public static object InvokeStatic(Type type, string method, params object[] args)
    {
        int count = args == null ? 0 : args.Length;
        foreach (var m in type.GetMethods(F).Where(m => m.Name == method && m.GetParameters().Length == count))
            return m.Invoke(null, args);
        Assert.Fail("静态方法不存在或参数数量不匹配：" + method + " @ " + type.FullName);
        return null;
    }

    public static object InvokeInstance(object o, string method, params object[] args)
    {
        int count = args == null ? 0 : args.Length;
        foreach (var m in o.GetType().GetMethods(F).Where(m => m.Name == method && m.GetParameters().Length == count))
            return m.Invoke(o, args);
        Assert.Fail("方法不存在或参数数量不匹配：" + method + " @ " + o.GetType().FullName);
        return null;
    }

    public static object GetStatic(Type type, string name)
    {
        var f = type.GetField(name, F);
        if (f != null) return f.GetValue(null);
        var p = type.GetProperty(name, F);
        Assert.IsNotNull(p, "静态成员不存在：" + name + " @ " + type.FullName);
        return p.GetValue(null, null);
    }

    public static void SetStatic(Type type, string name, object value)
    {
        var f = type.GetField(name, F);
        if (f != null) { f.SetValue(null, value); return; }
        var p = type.GetProperty(name, F);
        Assert.IsNotNull(p, "静态属性不存在：" + name);
        p.SetValue(null, value, null);
    }

    // ==================== DataManager / 产品配置 ====================

    public static object DataManagerInstance()
        => DataManagerType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static).GetValue(null);

    public static List<PersonDefinition> ProductPersonDefinitions()
        => ((IEnumerable)Get(DataManagerInstance(), "AllPersonDefinitions")).Cast<PersonDefinition>().ToList();

    public static List<PersonDefinition> ProductInitialRoster()
        => ((IEnumerable)Get(DataManagerInstance(), "InitialPersonRoster")).Cast<PersonDefinition>().ToList();

    public static List<ProfessionDefinition> ProductProfessions()
        => ((IEnumerable)Get(DataManagerInstance(), "AllProfessionDefinitions")).Cast<ProfessionDefinition>().ToList();

    public static ProfessionDefinitionRegistry ProductRegistry()
        => (ProfessionDefinitionRegistry)Get(DataManagerInstance(), "ProfessionRegistry");

    // ==================== Person / Bootstrap ====================

    public static object NewCollection() => NewAsm("PlayerCollection");

    public static IList OwnedCharacters(object collection) => (IList)Get(collection, "OwnedCharacters");

    public static IList OwnedCards(object collection) => (IList)Get(collection, "OwnedCharacterCards");

    public static object Bootstrap(PersonDefinition definition, ProfessionDefinitionRegistry registry)
        => InvokeStatic(BootstrapperType, "CreatePerson", definition, registry);

    public static IPersonGrowth AsPerson(object characterInstance) => (IPersonGrowth)characterInstance;

    public static IGrowthAuthority AuthorityOf(object characterInstance)
        => GrowthAuthority.Resolve((IPersonGrowth)characterInstance);

    public static ProfessionProgress ProgressOf(object characterInstance)
        => (ProfessionProgress)Get(characterInstance, "ActiveProfessionProgress");

    public static List<StarterSlotEntry> StarterOf(object characterInstance)
        => (List<StarterSlotEntry>)Get(characterInstance, "StarterSlots");

    public static bool HasActiveProfession(object characterInstance)
        => (bool)Get(characterInstance, "HasActiveProfession");

    public static object NewCard(string cardId, string combatId)
    {
        object card = ScriptableObject.CreateInstance(Asm("CharacterCardData"));
        Set(card, "CardId", cardId);
        Set(card, "CharacterId", combatId);
        return card;
    }

    // ==================== Village Bridge / Capability（V4 链） ====================

    public static void ResetBridge() => InvokeStatic(BridgeType, "ResetForTests");

    public static void SetPersonSource(object collection)
        => InvokeStatic(BridgeType, "SetSimulationPersonSource", collection);

    public static string GrantCapability(string personId, string cardId)
    {
        object result = InvokeStatic(CapabilityType, "TryGrantToPerson", personId, cardId);
        return result != null ? result.ToString() : "null";
    }

    // ==================== 测试内容构造（Test / Not Frozen） ====================

    public static PersonDefinition NewDefinition(string id, string displayName, int rosterOrder = 0,
        string combatDefinitionId = "char_001", string cardDefinitionId = null)
    {
        return new PersonDefinition
        {
            PersonDefinitionId = id,
            DisplayName = displayName,
            CombatDefinitionId = combatDefinitionId,
            CardDefinitionId = cardDefinitionId,
            InitialRosterOrder = rosterOrder
        };
    }

    public static ProfessionDefinition NewProfessionDefinition(string professionId,
        IEnumerable<string> initialUnlocked, IEnumerable<string> fullPool = null,
        IEnumerable<StarterSlotEntry> starterBuild = null)
    {
        var unlocked = initialUnlocked.ToList();
        var full = fullPool != null ? fullPool.ToList() : unlocked.ToList();
        return new ProfessionDefinition
        {
            ProfessionId = professionId,
            FullCardPool = full,
            InitialUnlockedCards = unlocked,
            InitialStarterBuild = starterBuild != null ? starterBuild.ToList() : new List<StarterSlotEntry>(),
            ProfessionTraitId = "trait_" + professionId
        };
    }

    public static List<BattleCardData> AllBattleCardTemplates()
    {
        var list = new List<BattleCardData>();
        foreach (var t in (IEnumerable)Get(DataManagerInstance(), "AllBattleCards"))
            if (t is BattleCardData card && !string.IsNullOrEmpty(card.CardId)) list.Add(card);
        return list;
    }
}
