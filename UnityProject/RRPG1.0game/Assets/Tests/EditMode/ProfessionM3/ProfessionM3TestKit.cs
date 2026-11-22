using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

/// <summary>
/// M3 Ordinary Person Profession Progress EditMode 测试共享辅助。
/// 团结引擎限制：asmdef 测试程序集不能引用预定义程序集 Assembly-CSharp，
/// 因此 CharacterInstance / HeroInstance / PlayerCollection / SaveSystem / VillageRuntimeBridge 经反射访问；
/// Profession.Runtime（ProfessionProgress / ProfessionDefinition）类型可强类型访问。
/// 内容全部为 Test / Not Frozen（profession_test_* 与 card_*）。
/// </summary>
public static class ProfessionM3TestKit
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

    // ==================== Assembly-CSharp 宿主构造（反射） ====================

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

    public static string PersonIdOf(object instance)
    {
        string pid = (string)Get(instance, "PersonId");
        return pid;
    }

    // ==================== Profession Progress 访问（宿主上的 Person-owned 状态） ====================

    public static ProfessionProgress ActiveProgress(object character)
    {
        return (ProfessionProgress)Get(character, "ActiveProfessionProgress");
    }

    public static bool HasActiveProfession(object character)
    {
        return (bool)Get(character, "HasActiveProfession");
    }

    // ==================== ProfessionProgress / ProfessionDefinition 构造（Test / Not Frozen） ====================

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

    /// <summary>任务示例值：profession_test_hunter / Lv1 / Exp0 / [card_a, card_b] / TraitLv1</summary>
    public static ProfessionProgress NewHunterProgress()
    {
        return NewProgress("profession_test_hunter", 1, 0, new[] { "card_a", "card_b" }, 1);
    }

    public static ProfessionDefinition NewHunterDefinition()
    {
        return new ProfessionDefinition
        {
            ProfessionId = "profession_test_hunter",
            FullCardPool = new List<string> { "card_a", "card_b", "card_c", "card_d" },
            InitialUnlockedCards = new List<string> { "card_a", "card_b" }
        };
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

    public static object InvokeStatic(Type type, string method, params object[] args)
    {
        var candidates = type.GetMethods(F).Where(m => m.Name == method).ToList();
        MethodInfo selected = null;
        foreach (var m in candidates)
        {
            var ps = m.GetParameters();
            if (ps.Length != (args == null ? 0 : args.Length)) continue;
            bool ok = true;
            for (int i = 0; i < ps.Length; i++)
            {
                if (args[i] == null) continue; // null 兼容任意引用类型参数
                if (!ps[i].ParameterType.IsInstanceOfType(args[i])) { ok = false; break; }
            }
            if (ok) { selected = m; break; }
        }
        Assert.IsNotNull(selected, "方法不存在或参数不匹配：" + method + " @ " + type.FullName);
        return selected.Invoke(null, args);
    }
}
