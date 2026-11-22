using System.IO;
using System.Reflection;
using NUnit.Framework;

/// <summary>
/// Stage V1 Save / Load 集成测试。
/// 通过反射访问 Assembly-CSharp（团结引擎 asmdef 不能引用预定义程序集）：
///   - 旧档（无 Village 区块）必须可正常加载，Village 以兼容默认状态初始化；
///   - 带 Village 区块的存档 Save → Load 后 VillageTime / VillageState 一致；
///   - VillageRuntimeBridge 未初始化时保存不产生 Village 区块（向后兼容）。
/// </summary>
public class VillageSaveCompatibilityTests
{
    private const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;

    private static System.Type SaveSystemType => System.Type.GetType("SaveSystem, Assembly-CSharp");
    private static System.Type BridgeType => System.Type.GetType("VillageRuntimeBridge, Assembly-CSharp");

    [SetUp]
    public void ResetBridge()
    {
        InvokeStatic(BridgeType, "ResetForTests");
    }

    [TearDown]
    public void Cleanup()
    {
        InvokeStatic(BridgeType, "ResetForTests");
        DeleteSave();
    }

    private static string SavePath() => (string)InvokeStatic(SaveSystemType, "GetSavePath");

    private static void DeleteSave()
    {
        string path = SavePath();
        if (path != null && File.Exists(path)) File.Delete(path);
    }

    private static object InvokeStatic(System.Type type, string method, params object[] args)
    {
        var methods = System.Linq.Enumerable.Where(
            type.GetMethods(F), m => m.Name == method);

        System.Reflection.MethodInfo selected = null;
        foreach (var m in methods)
        {
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

    private static object GetStaticField(System.Type type, string name)
    {
        var f = type.GetField(name, F);
        Assert.IsNotNull(f, "字段不存在：" + name + " @ " + type.FullName);
        return f.GetValue(null);
    }

    private static object NewPlayerCollection()
    {
        return System.Activator.CreateInstance(System.Type.GetType("PlayerCollection, Assembly-CSharp"));
    }

    [Test]
    public void LegacySave_WithoutVillage_LoadsWithDefaultVillage()
    {
        // 手工构造旧版存档 JSON（不含 Village 字段；SaveVersion=1 与 CurrentSaveVersion 一致）
        string json = "{\"SaveVersion\":1,\"GameVersion\":\"0.2.3\",\"SavedAtUnixTime\":0," +
                      "\"Player\":{\"Gold\":100,\"Heroes\":[],\"Characters\":[],\"OwnedCharacterCardIds\":[],\"OwnedItemCardIds\":[]}}";
        File.WriteAllText(SavePath(), json);

        object collection = InvokeStatic(SaveSystemType, "Load");

        Assert.IsNotNull(collection, "旧档必须可加载，不得因缺少 Village 区块失败");
        Assert.AreEqual(100, GetFieldValue(collection, "Gold"), "旧档玩家数据应正常恢复");

        var time = (VillageTime)GetStaticField(BridgeType, "Time");
        var state = (VillageState)GetStaticField(BridgeType, "State");
        Assert.IsNotNull(time, "旧档应初始化默认 VillageTime");
        Assert.IsNotNull(state, "旧档应初始化默认 VillageState");
        Assert.AreEqual(1, time.Year, "旧档缺 Village → 默认年 1");
        Assert.AreEqual(1, time.Month, "旧档缺 Village → 默认月 1");
        Assert.AreEqual(1, time.Day, "旧档缺 Village → 默认日 1");
        Assert.AreEqual(0, state.TestSurvivalValue, "旧档缺 Village → 默认 Test State 0");
    }

    [Test]
    public void SaveWithVillage_RoundTripThroughSaveSystem()
    {
        // 初始化 bridge 并推进到已知状态
        InvokeStatic(BridgeType, "EnsureInitialized", new object[] { null });
        var time = (VillageTime)GetStaticField(BridgeType, "Time");
        var state = (VillageState)GetStaticField(BridgeType, "State");
        var intradaySetup1 = time.ToSaveData();
        intradaySetup1.IntradayTime = null;
        intradaySetup1.Year = 2;
        intradaySetup1.Month = 3;
        intradaySetup1.Day = 10;
        intradaySetup1.Speed = (int)(VillageTime.VillageTimeSpeed.Fast);
        intradaySetup1.SecondsIntoDay = (1.5f) * VillageTime.LegacySecondsPerDay / time.SecondsPerDay;
        time.Restore(intradaySetup1);

        state.TestSurvivalValue = 7;

        object collection = NewPlayerCollection();
        Assert.IsTrue((bool)InvokeStatic(SaveSystemType, "Save", collection), "带 Village 的存档应保存成功");

        // 重置运行时单例，模拟重启后 Load
        InvokeStatic(BridgeType, "ResetForTests");
        object loaded = InvokeStatic(SaveSystemType, "Load");
        Assert.IsNotNull(loaded, "存档应可加载");

        var t2 = (VillageTime)GetStaticField(BridgeType, "Time");
        var s2 = (VillageState)GetStaticField(BridgeType, "State");
        Assert.IsNotNull(t2);
        Assert.IsNotNull(s2);
        Assert.AreEqual(2, t2.Year);
        Assert.AreEqual(3, t2.Month);
        Assert.AreEqual(10, t2.Day);
        Assert.AreEqual(VillageTime.VillageTimeSpeed.Fast, t2.Speed);
        Assert.AreEqual(1.5f, t2.SecondsIntoDay);
        Assert.AreEqual(7, s2.TestSurvivalValue);
    }

    [Test]
    public void Save_WhenBridgeNotExplicitlyInitialized_CreatesDefaultVillageAndLoads()
    {
        // bridge 未显式初始化时 Save(collection) 不应报错；SaveSystem.Save 会自动以默认状态
        // 初始化 Village（保证 MainMenuUI.NewGame 等早期保存入口也携带 Village 区块），随后可正常加载。
        object collection = NewPlayerCollection();
        Assert.IsTrue((bool)InvokeStatic(SaveSystemType, "Save", collection), "未初始化 bridge 时保存不应失败");

        object loaded = InvokeStatic(SaveSystemType, "Load");
        Assert.IsNotNull(loaded, "带默认 Village 区块的存档仍可加载");

        var time = (VillageTime)GetStaticField(BridgeType, "Time");
        var state = (VillageState)GetStaticField(BridgeType, "State");
        Assert.IsNotNull(time, "Save 应自动初始化默认 VillageTime");
        Assert.AreEqual(1, time.Year);
        Assert.AreEqual(1, time.Month);
        Assert.AreEqual(1, time.Day);
        Assert.AreEqual(0, state.TestSurvivalValue, "默认 Test State 为 0");
    }

    private static int GetFieldValue(object o, string name)
    {
        var f = o.GetType().GetField(name, BindingFlags.Public | BindingFlags.Instance);
        Assert.IsNotNull(f, "字段不存在：" + name);
        return (int)f.GetValue(o);
    }
}
