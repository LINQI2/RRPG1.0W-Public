using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// M1 PersonId EditMode 测试共享辅助。
/// 团结引擎限制：asmdef 测试程序集不能引用预定义程序集 Assembly-CSharp，
/// 因此 PlayerCollection / CharacterInstance / HeroInstance / SaveSystem / VillageRuntimeBridge 经反射访问；
/// RunState / SharedDeck / Village.Runtime / Growth.Runtime 类型可强类型访问。
/// </summary>
public static class M1TestKit
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

    public static object NewPlayerCollection()
    {
        return NewAsm("PlayerCollection");
    }

    public static object NewCharacter(string characterId, string displayName = "战士", int hp = 30, int atk = 6)
    {
        return NewAsm("CharacterInstance", characterId, displayName, hp, atk);
    }

    public static object NewHero(string heroId = "hero_001", string displayName = "青鹿", int hp = 35)
    {
        return NewAsm("HeroInstance", heroId, displayName, hp);
    }

    public static object NewCharacterCard(string cardId, string characterId)
    {
        object card = typeof(ScriptableObject).GetMethod("CreateInstance", new[] { typeof(Type) })
            .Invoke(null, new object[] { Asm("CharacterCardData") });
        Set(card, "CardId", cardId);
        Set(card, "CharacterId", characterId);
        return card;
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

    public static string PersonIdOf(object instance)
    {
        string pid = (string)Get(instance, "PersonId");
        return pid;
    }

    public static IList OwnedCharacters(object collection) => (IList)Get(collection, "OwnedCharacters");

    public static IList OwnedHeroes(object collection) => (IList)Get(collection, "OwnedHeroes");

    public static IList OwnedCharacterCards(object collection) => (IList)Get(collection, "OwnedCharacterCards");

    public static object CharacterByPersonId(object collection, string personId)
    {
        return GetCharacter(collection, personId);
    }

    private static object GetCharacter(object collection, string personId)
    {
        foreach (var c in OwnedCharacters(collection))
        {
            if (c != null && PersonIdOf(c) == personId) return c;
        }
        return null;
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
                if (args[i] == null) continue;
                if (!ps[i].ParameterType.IsInstanceOfType(args[i])) { ok = false; break; }
            }
            if (ok) { selected = m; break; }
        }
        Assert.IsNotNull(selected, "方法不存在或参数不匹配：" + method + " @ " + type.FullName);
        return selected.Invoke(null, args);
    }

    public static object InvokeInstance(object o, string method, params object[] args)
    {
        MethodInfo m = o.GetType().GetMethod(method, F);
        Assert.IsNotNull(m, "方法不存在：" + method + " @ " + o.GetType().FullName);
        return m.Invoke(o, args);
    }
}
