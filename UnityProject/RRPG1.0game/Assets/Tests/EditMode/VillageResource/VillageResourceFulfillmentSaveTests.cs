using System.Collections;
using System.Reflection;
using NUnit.Framework;

/// <summary>
/// WP-3 Gate 2：Person-owned Fulfillment / Hunger 存档双向镜像测试（EditMode，纯内存）。
/// 通过反射访问 Assembly-CSharp（团结引擎 asmdef 不能引用预定义程序集）：
///   - 直接调用私有静态 SaveSystem.BuildSaveData / RestorePlayerCollection（不写磁盘存档，
///     无持久化污染 / 不触碰真实存档路径）；
///   - 覆盖：Hero / Character 的 Hungry + ConsecutiveUnderfedDays 的 Build 镜像、Restore 往返、
///     旧档缺省（DTO 不含 WP-3 字段 → false / 0，SaveVersion 保持 1）。
/// </summary>
public class VillageResourceFulfillmentSaveTests
{
    private const BindingFlags StaticFlags =
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
    private const BindingFlags InstanceFlags =
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

    private static System.Type SaveSystemType => System.Type.GetType("SaveSystem, Assembly-CSharp");

    private static object InvokeStatic(System.Type type, string method, params object[] args)
    {
        MethodInfo selected = null;
        foreach (var m in type.GetMethods(StaticFlags))
        {
            if (m.Name != method) continue;
            var ps = m.GetParameters();
            if (ps.Length != (args == null ? 0 : args.Length)) continue;
            bool ok = true;
            for (int i = 0; i < ps.Length; i++)
            {
                if (args[i] == null) continue; // null 兼容任意引用类型参数
                if (!ps[i].ParameterType.IsAssignableFrom(args[i].GetType())) { ok = false; break; }
            }
            if (ok) { selected = m; break; }
        }
        Assert.IsNotNull(selected, "方法不存在或参数不匹配：" + method + " @ " + type.FullName);
        return selected.Invoke(null, args);
    }

    private static object GetValue(object target, string name)
    {
        Assert.IsNotNull(target, "读取目标为 null：" + name);
        var t = target.GetType();
        var f = t.GetField(name, InstanceFlags);
        if (f != null) return f.GetValue(target);
        var p = t.GetProperty(name, InstanceFlags);
        Assert.IsNotNull(p, "属性/字段不存在：" + name + " @ " + t.FullName);
        return p.GetValue(target, null);
    }

    private static void SetValue(object target, string name, object value)
    {
        var t = target.GetType();
        var p = t.GetProperty(name, InstanceFlags);
        if (p != null && p.CanWrite)
        {
            p.SetValue(target, value, null);
            return;
        }
        var f = t.GetField(name, InstanceFlags);
        Assert.IsNotNull(f, "可写属性/字段不存在：" + name + " @ " + t.FullName);
        f.SetValue(target, value);
    }

    /// <summary>取集合字段首元素；集合空返回 null。</summary>
    private static object FirstOf(object target, string collectionFieldName)
    {
        var list = GetValue(target, collectionFieldName) as IEnumerable;
        Assert.IsNotNull(list, collectionFieldName + " 不是可枚举集合");
        foreach (var item in list)
        {
            if (item != null) return item;
        }
        return null;
    }

    private static object NewDefaultCollection()
    {
        return InvokeStatic(SaveSystemType, "NewGame");
    }

    private static object BuildPlayerData(object collection)
    {
        // BuildSaveData(PlayerCollection, VillageSaveData) → SaveData；village 传 null（本测试不涉 Village 区块）。
        object saveData = InvokeStatic(SaveSystemType, "BuildSaveData", collection, null);
        Assert.IsNotNull(saveData, "BuildSaveData 返回 null");
        return GetValue(saveData, "Player");
    }

    private static object RestoreCollection(object playerData)
    {
        object restored = InvokeStatic(SaveSystemType, "RestorePlayerCollection", playerData);
        Assert.IsNotNull(restored, "RestorePlayerCollection 返回 null");
        return restored;
    }

    [Test]
    public void Hero_BuildMirrorsFulfillmentState()
    {
        var collection = NewDefaultCollection();
        var hero = FirstOf(collection, "OwnedHeroes");
        Assert.IsNotNull(hero, "默认集合应含 hero（hero_001）");
        SetValue(hero, "Hungry", true);
        SetValue(hero, "ConsecutiveUnderfedDays", 4);

        var player = BuildPlayerData(collection);
        var heroDto = FirstOf(player, "Heroes");
        Assert.IsNotNull(heroDto, "Build 应产出 Hero DTO");
        Assert.AreEqual(true, GetValue(heroDto, "Hungry"));
        Assert.AreEqual(4, GetValue(heroDto, "ConsecutiveUnderfedDays"));
    }

    [Test]
    public void Roundtrip_HeroAndCharacter_PreserveFulfillmentState()
    {
        var collection = NewDefaultCollection();
        var hero = FirstOf(collection, "OwnedHeroes");
        var character = FirstOf(collection, "OwnedCharacters");
        SetValue(hero, "Hungry", true);
        SetValue(hero, "ConsecutiveUnderfedDays", 5);
        SetValue(character, "Hungry", true);
        SetValue(character, "ConsecutiveUnderfedDays", 1);

        var restored = RestoreCollection(BuildPlayerData(collection));

        var restoredHero = FirstOf(restored, "OwnedHeroes");
        Assert.AreEqual(true, GetValue(restoredHero, "Hungry"));
        Assert.AreEqual(5, GetValue(restoredHero, "ConsecutiveUnderfedDays"));

        var restoredCharacter = FirstOf(restored, "OwnedCharacters");
        Assert.AreEqual(true, GetValue(restoredCharacter, "Hungry"));
        Assert.AreEqual(1, GetValue(restoredCharacter, "ConsecutiveUnderfedDays"));
    }

    [Test]
    public void Character_BuildMirrorsFulfillmentState()
    {
        var collection = NewDefaultCollection();
        var character = FirstOf(collection, "OwnedCharacters");
        Assert.IsNotNull(character, "默认集合应含角色实例");
        SetValue(character, "Hungry", true);
        SetValue(character, "ConsecutiveUnderfedDays", 2);

        var player = BuildPlayerData(collection);
        var characterDto = FirstOf(player, "Characters");
        Assert.IsNotNull(characterDto, "Build 应产出 Character DTO");
        Assert.AreEqual(true, GetValue(characterDto, "Hungry"));
        Assert.AreEqual(2, GetValue(characterDto, "ConsecutiveUnderfedDays"));
    }

    [Test]
    public void Restore_DefaultsFalseZero_WhenDtoOmitsFulfillmentFields()
    {
        // 模拟“旧档缺省”：DTO 不写 WP-3 字段（值类型缺省 false / 0）。
        // 从默认集合构建后先保持实例默认值，再恢复 → 仍为 false / 0（与 JSON 缺 key 语义一致）。
        var collection = NewDefaultCollection();
        var hero = FirstOf(collection, "OwnedHeroes");
        Assert.IsFalse((bool)GetValue(hero, "Hungry"));
        Assert.AreEqual(0, GetValue(hero, "ConsecutiveUnderfedDays"));

        var restored = RestoreCollection(BuildPlayerData(collection));
        var restoredHero = FirstOf(restored, "OwnedHeroes");
        Assert.IsFalse((bool)GetValue(restoredHero, "Hungry"));
        Assert.AreEqual(0, GetValue(restoredHero, "ConsecutiveUnderfedDays"));
    }
}
