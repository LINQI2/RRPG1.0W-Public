using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

/// <summary>
/// M5（Run/Return Growth Semantic Migration）EditMode 测试共享辅助。
/// 团结引擎限制：asmdef 测试程序集不能引用预定义程序集 Assembly-CSharp，因此
/// CharacterInstance / HeroInstance / PlayerCollection / PlayerRunRewardSink / BattleSettlement /
/// AdventureManager / SaveSystem / VillageRuntimeBridge 经反射访问；
/// Growth.Runtime（IGrowthAuthority / GrowthAuthority / UnlockedPoolRewardProvider / IPersonGrowth）与
/// Profession.Runtime（ProfessionProgress）与 BattleStage1.Runtime（RunState）可强类型访问。
/// 全部内容为 Test / Not Frozen（profession_test_* 与 card_* / BattleProfessionExp 测试值）。
/// </summary>
public static class ProfessionM5TestKit
{
    public const BindingFlags F =
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

    // M2 Slice 2A：MonsterDefinition 位于 StaticConfig.Runtime（不是 Assembly-CSharp）→ 解析器加回退。
    public static Type Asm(string typeName) => Type.GetType(typeName + ", Assembly-CSharp")
        ?? Type.GetType("RRPG.StaticConfig." + typeName + ", StaticConfig.Runtime");

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

    /// <summary>按元素类型创建强类型 List&lt;T&gt;（用于反射方法参数，T 为 Assembly-CSharp 类型）。</summary>
    public static object MakeList(string elementTypeName)
    {
        var listType = typeof(List<>).MakeGenericType(Asm(elementTypeName));
        return Activator.CreateInstance(listType);
    }

    public static void AddTo(object list, object item)
    {
        list.GetType().GetMethod("Add", F).Invoke(list, new[] { item });
    }

    // ==================== Assembly-CSharp 宿主构造（反射） ====================

    public static object NewPlayerCollection()
    {
        return NewAsm("PlayerCollection");
    }

    public static object NewCharacter(string characterId = "char_001", string displayName = "测试人物",
        int hp = 30, int atk = 6)
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

    // ==================== Profession Progress（宿主挂载 / 读取） ====================

    public static ProfessionProgress NewProgress(string professionId, int level, int exp,
        IEnumerable<string> unlockedPool, int traitLevel)
    {
        return new ProfessionProgress
        {
            ProfessionId = professionId,
            ProfessionLevel = level,
            ProfessionExp = exp,
            UnlockedPoolCardIds = unlockedPool == null ? new List<string>() : unlockedPool.ToList(),
            ProfessionTraitLevel = traitLevel
        };
    }

    public static void SetActiveProgress(object character, ProfessionProgress progress)
    {
        Set(character, "ActiveProfessionProgress", progress);
    }

    public static ProfessionProgress ActiveProgress(object character)
    {
        return (ProfessionProgress)Get(character, "ActiveProfessionProgress");
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

    public static void DeleteSave()
    {
        string p = SavePath();
        if (p != null && File.Exists(p)) File.Delete(p);
    }

    public static string SavePath()
    {
        return (string)InvokeStatic(SaveSystemType, "GetSavePath");
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

    /// <summary>真实 IGrowthAuthority（经 GrowthAuthority 窄解析，验证 Return 写入权威 = Profession）。</summary>
    public static IGrowthAuthority ResolveAuth(object instance)
    {
        return GrowthAuthority.Resolve(AsGrowth(instance));
    }

    /// <summary>反射调用实例方法（按名称 + 参数运行时类型选择）。</summary>
    public static object Call(object o, string method, params object[] args)
    {
        var type = o.GetType();
        var candidates = type.GetMethods(F).Where(m => m.Name == method).ToList();
        MethodInfo selected = Pick(candidates, args);
        Assert.IsNotNull(selected, "方法不存在或参数不匹配：" + method + " @ " + type.FullName);
        return selected.Invoke(o, args);
    }

    /// <summary>反射读取属性/字段（供宿主属性如 AdventureManager.RunState）。</summary>
    public static object GetPropertyOrField(object o, string name)
    {
        var f = o.GetType().GetField(name, F);
        if (f != null) return f.GetValue(o);
        var p = o.GetType().GetProperty(name, F);
        Assert.IsNotNull(p, "属性/字段不存在：" + name + " @ " + o.GetType().FullName);
        return p.GetValue(o, null);
    }
}

