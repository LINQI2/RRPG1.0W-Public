using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

/// <summary>
/// M6（Profession Formation Foundation）EditMode 测试共享辅助。
/// 团结引擎限制：asmdef 测试程序集不能引用预定义程序集 Assembly-CSharp，因此
/// CharacterInstance / HeroInstance / PlayerCollection / SaveSystem / VillageRuntimeBridge /
/// TownUI 经反射访问；Profession.Runtime（ProfessionProgress / FormationState / FormationResolver /
/// FormationCommitter / PersonLifeHistory / ProfessionDefinition…）与 Growth.Runtime（StarterSlotEntry /
/// StarterDeckBuilder / GrowthAuthority…）与 Village.Runtime（VillageTime / VillageAssignmentSystem…）
/// 可强类型访问。
/// 内容全部为 Test / Not Frozen（profession_test_* / ws_test_* / trait_profession_test_*）。
/// </summary>
public static class ProfessionM6TestKit
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
        if (o == null) throw new ArgumentNullException(nameof(o), "Get target null");
        var f = o.GetType().GetField(name, F);
        if (f != null) return f.GetValue(o);
        var p = o.GetType().GetProperty(name, F);
        Assert.IsNotNull(p, "成员不存在：" + name + " @ " + o.GetType().FullName);
        return p.GetValue(o, null);
    }

    public static void Set(object o, string name, object value)
    {
        var f = o.GetType().GetField(name, F);
        if (f != null)
        {
            f.SetValue(o, value);
            return;
        }
        var p = o.GetType().GetProperty(name, F);
        Assert.IsNotNull(p, "字段/属性不存在：" + name);
        p.SetValue(o, value, null);
    }

    public static object GetStaticField(string asmTypeName, string fieldName)
    {
        var t = Asm(asmTypeName);
        var f = t.GetField(fieldName, F);
        Assert.IsNotNull(f, "静态字段不存在：" + fieldName + " @ " + asmTypeName);
        return f.GetValue(null);
    }

    public static void SetStaticField(string asmTypeName, string fieldName, object value)
    {
        var t = Asm(asmTypeName);
        var f = t.GetField(fieldName, F);
        Assert.IsNotNull(f, "静态字段不存在：" + fieldName + " @ " + asmTypeName);
        f.SetValue(null, value);
    }

    public static object InvokeStatic(Type type, string method, params object[] args)
    {
        var candidates = type.GetMethods(F).Where(m => m.Name == method).ToList();
        MethodInfo selected = Pick(candidates, args);
        Assert.IsNotNull(selected, "方法不存在或参数不匹配：" + method + " @ " + type.FullName);
        return selected.Invoke(null, args);
    }

    public static object Call(object o, string method, params object[] args)
    {
        var candidates = o.GetType().GetMethods(F).Where(m => m.Name == method).ToList();
        MethodInfo selected = Pick(candidates, args);
        Assert.IsNotNull(selected, "方法不存在或参数不匹配：" + method + " @ " + o.GetType().FullName);
        return selected.Invoke(o, args);
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

    // ==================== Assembly-CSharp 宿主（反射） ====================

    public static object NewPlayerCollection()
    {
        return NewAsm("PlayerCollection");
    }

    public static object NewCharacter(string characterId = "char_001", string displayName = "测试人物", int hp = 30, int atk = 6)
    {
        return NewAsm("CharacterInstance", characterId, displayName, hp, atk);
    }

    public static object NewHero(string heroId = "hero_001", string displayName = "青鹿", int hp = 35)
    {
        return NewAsm("HeroInstance", heroId, displayName, hp);
    }

    public static IList OwnedCharacters(object collection) => (IList)Get(collection, "OwnedCharacters");

    public static IList OwnedHeroes(object collection) => (IList)Get(collection, "OwnedHeroes");

    public static string PersonIdOf(object instance) => (string)Get(instance, "PersonId");

    // ==================== IFormationHost 访问 ====================

    /// <summary>真实普通 CharacterInstance 是 IFormationHost（Formation 宿主）。</summary>
    public static IFormationHost AsHost(object character)
    {
        var host = character as IFormationHost;
        Assert.IsNotNull(host, "CharacterInstance 应实现 IFormationHost");
        return host;
    }

    public static bool ImplementsIFormationHost(Type t) =>
        t != null && typeof(IFormationHost).IsAssignableFrom(t);

    // ==================== Profession 模板 / 注册表（Test / Not Frozen） ====================

    public static ProfessionDefinition NewDefinition(
        string professionId,
        IEnumerable<string> fullPool,
        IEnumerable<string> initialUnlocked,
        string professionTraitId,
        IEnumerable<StarterSlotEntry> initialStarterBuild)
    {
        return new ProfessionDefinition
        {
            ProfessionId = professionId,
            FullCardPool = fullPool == null ? new List<string>() : fullPool.ToList(),
            InitialUnlockedCards = initialUnlocked == null ? new List<string>() : initialUnlocked.ToList(),
            ProfessionTraitId = professionTraitId,
            InitialStarterBuild = initialStarterBuild == null
                ? new List<StarterSlotEntry>()
                : initialStarterBuild.ToList()
        };
    }

    public static ProfessionDefinitionRegistry NewRegistry(params ProfessionDefinition[] defs)
    {
        return ProfessionDefinitionRegistry.Build(defs);
    }

    /// <summary>农田（ws_test_01）→ profession_test_farmer 的主工作映射（Test / Not Frozen）。</summary>
    public static FormationTestWorkEvidenceMapping NewFarmerWorkMapping(float amountPerDay = 1f)
    {
        var mapping = new FormationTestWorkEvidenceMapping();
        mapping.Entries.Add(new FormationWorkEvidenceMappingEntry
        {
            WorkplaceId = "ws_test_01",
            ProfessionId = "profession_test_farmer",
            AmountPerExecutedDay = amountPerDay
        });
        return mapping;
    }

    /// <summary>快速形成用配置（Test / Not Frozen；确定性测试值，不冻结）。</summary>
    public static FormationTestConfig FastConfig()
    {
        return new FormationTestConfig
        {
            StrengthMax = 100f,
            QualificationThreshold = 10f,
            CandidateExitHysteresis = 2f,
            DominanceStrengthGap = 8f,
            StabilityGainPerConsistentDay = 1f,
            StabilityLossOnTargetChange = 6f,
            StabilityPassiveDecayPerDay = 0.05f,
            StabilityDecayPerAmbiguousDay = 0.2f,
            StabilityMax = 50f,
            FormationStabilityThreshold = 2f,
            GrowthSteepness = 0.3f,
            GrowthMidDay = 10f,
            DecayMaxFraction = 0.85f,
            DecaySteepness = 0.1f,
            DecayMidDay = 20f
        };
    }

    // ==================== Formation 数值辅助 ====================

    public static FormationEvidenceContribution Work(string professionId, float amount = 1f)
    {
        return new FormationEvidenceContribution(professionId, amount, FormationEvidenceSource.DailyWork);
    }

    /// <summary>连续运行 N 个 Day End（无贡献 = 仅 inactivity/decay；有贡献 = Work/其它 Evidence）。</summary>
    public static FormationDayReport RunDays(IFormationHost host, FormationTestConfig cfg,
        ProfessionDefinitionRegistry registry, int days, FormationDayContext start,
        Func<int, List<FormationEvidenceContribution>> contributionForDay)
    {
        FormationDayReport last = null;
        for (int i = 0; i < days; i++)
        {
            var day = new FormationDayContext(start.Year, start.Month, start.Day + i);
            last = FormationResolver.AdvanceDay(host, day,
                contributionForDay != null ? contributionForDay(i) : null,
                cfg, registry);
        }
        return last;
    }


    // ==================== SaveSystem / VillageRuntimeBridge（反射） ====================

    private static Type SaveSystemType => Asm("SaveSystem");
    private static Type BridgeType => Asm("VillageRuntimeBridge");

    public static void ResetBridge()
    {
        InvokeStatic(BridgeType, "ResetForTests");
    }

    public static void EnsureVillageInitialized()
    {
        InvokeStatic(BridgeType, "EnsureInitialized", new object[] { null });
    }

    public static void SetSimulationPersonSource(object collection)
    {
        InvokeStatic(BridgeType, "SetSimulationPersonSource", collection);
    }

    public static void SetUnavailablePersonIds(IEnumerable<string> personIds)
    {
        InvokeStatic(BridgeType, "SetUnavailablePersonIds", personIds);
    }

    public static VillageTime BridgeTime()
    {
        return (VillageTime)GetStaticField("VillageRuntimeBridge", "Time");
    }

    public static VillageAssignmentSystem BridgeAssignments()
    {
        return (VillageAssignmentSystem)GetStaticField("VillageRuntimeBridge", "Assignments");
    }

    public static void SetBridgeFormation(FormationRuntimeServices services)
    {
        SetStaticField("VillageRuntimeBridge", "Formation", services);
    }

    public static string SavePath()
    {
        return (string)InvokeStatic(SaveSystemType, "GetSavePath");
    }

    public static void DeleteSave()
    {
        string p = SavePath();
        if (p != null && File.Exists(p)) File.Delete(p);
    }

    public static bool Save(object collection)
    {
        return (bool)InvokeStatic(SaveSystemType, "Save", collection);
    }

    public static object Load()
    {
        return InvokeStatic(SaveSystemType, "Load");
    }

    /// <summary>真实 IPersonGrowth（反射宿主实现该接口；object → 接口向下转型在运行时成立）。</summary>
    public static IPersonGrowth AsGrowth(object instance)
    {
        return (IPersonGrowth)instance;
    }

    /// <summary>真实 IGrowthAuthority（经 GrowthAuthority 窄解析；形成后自然读取现有权威）。</summary>
    public static IGrowthAuthority ResolveAuth(object instance)
    {
        return GrowthAuthority.Resolve(AsGrowth(instance));
    }
}


