using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

/// <summary>
/// V2A Assignment × Save/Load 集成测试。
/// 通过真实 SaveSystem / VillageRuntimeBridge（Assembly-CSharp，反射）验证：
///   - Save → Load 后 Primary Assignment / Capacity 占用 / 执行事实保持一致并可继续累计；
///   - Expedition / Unavailable(Temporary Occupancy) 运行时登记下 Assignment 暂停执行但不删除；
///   - 旧档（无 Village / 无 Assignments 区块）以空 Assignment 系统兼容加载。
/// Village.Runtime 类型可强类型访问；SaveSystem / Bridge 经反射。
/// </summary>
public class VillageAssignmentSaveTests
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

    // ==================== helpers（反射） ====================

    private static void DeleteSave()
    {
        string path = (string)InvokeStatic(SaveSystemType, "GetSavePath");
        if (path != null && File.Exists(path)) File.Delete(path);
    }

    private static void EnsureInitialized()
    {
        InvokeStatic(BridgeType, "EnsureInitialized", new object[] { null });
        IntradayVillageTestSource.BindDefaults();
    }

    private static object InvokeStatic(System.Type type, string method, params object[] args)
    {
        var methods = System.Linq.Enumerable.Where(type.GetMethods(F), m => m.Name == method);
        MethodInfo selected = null;
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

    private static object GetStaticProperty(System.Type type, string name)
    {
        var p = type.GetProperty(name, F);
        Assert.IsNotNull(p, "属性不存在：" + name + " @ " + type.FullName);
        return p.GetValue(null, null);
    }

    private static VillageAssignmentSystem GetAssignments()
    {
        return (VillageAssignmentSystem)GetStaticField(BridgeType, "Assignments");
    }

    private static VillageTime GetTime()
    {
        return (VillageTime)GetStaticField(BridgeType, "Time");
    }

    private static VillageAssignmentResult Assign(string personId, string workplaceId)
    {
        return (VillageAssignmentResult)InvokeStatic(BridgeType, "TryAssignPerson", personId, workplaceId);
    }

    private static void TickDays(int days)
    {
        var t = GetTime();
        for (int i = 0; i < days; i++) t.TickDay();
    }

    private static object NewPlayerCollection()
    {
        return IntradayVillageTestSource.Current ?? IntradayVillageTestSource.Create();
    }

    // ==================== Save / Load 集成 ====================

    [Test]
    public void SaveLoad_RoundTrip_PreservesAssignments_CapacityAndExecutionFacts()
    {
        EnsureInitialized();

        // 分配 2 人到容量 2 的默认测试岗位，第 3 人必须因满员被拒
        Assert.AreEqual(VillageAssignmentResult.Success, Assign("char_001", "ws_test_01"));
        Assert.AreEqual(VillageAssignmentResult.Success, Assign("char_002", "ws_test_01"));
        Assert.AreEqual(VillageAssignmentResult.CapacityFull, Assign("char_003", "ws_test_01"));
        TickDays(2); // 未占用 → 实际执行 2 天

        var system = GetAssignments();
        Assert.AreEqual(2, system.GetOccupantCount("ws_test_01"));
        Assert.AreEqual(2, system.GetRecord("char_001").TotalExecutedDays);
        Assert.AreEqual(4, system.TotalTestContribution);

        // 真实保存链路（SaveSystem.Save 自动携带 Village/Assignments 区块）
        object collection = NewPlayerCollection();
        Assert.IsTrue((bool)InvokeStatic(SaveSystemType, "Save", collection), "带 Assignments 的存档应保存成功");

        // 模拟重启后 Load
        InvokeStatic(BridgeType, "ResetForTests");
        object loaded = InvokeStatic(SaveSystemType, "Load");
        if (loaded != null) IntradayVillageTestSource.Bind(loaded);
        Assert.IsNotNull(loaded, "存档应可加载");

        var restored = GetAssignments();
        Assert.IsNotNull(restored, "Load 应初始化 Assignment 系统");

        // Primary Assignment 恢复
        Assert.AreEqual("ws_test_01", restored.GetWorkplaceId("char_001"));
        Assert.AreEqual("ws_test_01", restored.GetWorkplaceId("char_002"));
        Assert.IsNull(restored.GetWorkplaceId("char_003"));

        // Capacity 占用保持一致：岗位仍满员，非法分配继续被拒
        Assert.AreEqual(2, restored.GetOccupantCount("ws_test_01"));
        Assert.IsTrue(restored.IsWorkplaceFull("ws_test_01"));
        Assert.AreEqual(VillageAssignmentResult.CapacityFull, Assign("char_003", "ws_test_01"),
            "Load 后满员岗位必须继续拒绝分配");

        // 执行事实保持一致并继续累计
        Assert.AreEqual(2, restored.GetRecord("char_001").TotalExecutedDays);
        Assert.AreEqual(2, restored.GetRecord("char_002").TotalExecutedDays);
        Assert.AreEqual(4, restored.TotalTestContribution);
        TickDays(1);
        Assert.AreEqual(3, restored.GetRecord("char_001").TotalExecutedDays);
        Assert.AreEqual(6, restored.TotalTestContribution);
    }

    [Test]
    public void ExpeditionOccupancy_ThroughBridge_PausesExecution_ButKeepsAssignment()
    {
        EnsureInitialized();
        Assert.AreEqual(VillageAssignmentResult.Success, Assign("char_001", "ws_test_01"));

        // 运行时登记远征占用（V1 既有接线路径）
        InvokeStatic(BridgeType, "SetExpeditionPersonIds", (object)new[] { "char_001" });
        TickDays(3);

        var system = GetAssignments();
        Assert.IsNotNull(system.GetRecord("char_001"), "Expedition 占用不得删除 Primary Assignment");
        Assert.AreEqual(0, system.GetRecord("char_001").TotalExecutedDays);
        Assert.AreEqual(0, system.TotalTestContribution, "Expedition 下不得错误产生 Assignment Contribution");

        // 释放后自动恢复执行
        InvokeStatic(BridgeType, "SetExpeditionPersonIds", (object)new string[0]);
        TickDays(1);
        Assert.AreEqual(1, system.GetRecord("char_001").TotalExecutedDays);
        Assert.AreEqual(1, system.TotalTestContribution);
    }

    [Test]
    public void UnavailableOccupancy_SyncedFromDeadLongTermPerson_PausesExecution()
    {
        // M1：Temporary Occupancy / Assignment 一律按具体人物 PersonId（不再用 CharacterId 当 PersonId）
        var charType = System.Type.GetType("CharacterInstance, Assembly-CSharp");
        object character = System.Activator.CreateInstance(charType,
            new object[] { "char_dead_001", "死者", 10, 3 });
        charType.GetField("IsDead").SetValue(character, true);
        string deadPid = (string)charType.GetField("PersonId").GetValue(character);

        object pc = NewPlayerCollection();
        var owned = (IList)pc.GetType().GetField("OwnedCharacters").GetValue(pc);
        owned.Add(character);

        EnsureInitialized();
        Assert.AreEqual(VillageAssignmentResult.Success, Assign(deadPid, "ws_test_01"));

        // 构造含已死亡角色的长期 PlayerCollection，经 GetPersonAvailability 触发 Unavailable 单向同步
        IntradayVillageTestSource.Bind(pc);
        InvokeStatic(BridgeType, "GetPersonAvailability", pc);
        var unavailable = (IReadOnlyList<string>)GetStaticProperty(BridgeType, "UnavailablePersonIds");
        Assert.IsTrue(unavailable.Contains(deadPid), "已死亡长期人物应登记为 Unavailable（Temporary Occupancy）");

        TickDays(2);
        var system = GetAssignments();
        Assert.AreEqual(0, system.GetRecord(deadPid).TotalExecutedDays);
        Assert.AreEqual(0, system.TotalTestContribution, "Unavailable 占用下不得继续产生 Assignment Contribution");

        // Clearing a registration alone cannot resurrect a dead authoritative Person.
        charType.GetField("IsDead").SetValue(character, false);
        // 显式恢复存活后解除登记，重新劳动
        InvokeStatic(BridgeType, "SetUnavailablePersonIds", (object)new string[0]);
        TickDays(1);
        Assert.AreEqual(1, system.GetRecord(deadPid).TotalExecutedDays);
    }

    [Test]
    public void LegacySave_WithoutVillage_LoadsWithEmptyAssignments_AndCanAssign()
    {
        // 旧档 JSON：无 Village 区块（与 V1 兼容测试同构，SaveVersion=1）
        string json = "{\"SaveVersion\":1,\"GameVersion\":\"0.2.3\",\"SavedAtUnixTime\":0," +
                      "\"Player\":{\"Gold\":100,\"Heroes\":[],\"Characters\":[],\"OwnedCharacterCardIds\":[],\"OwnedItemCardIds\":[]}}";
        string path = (string)InvokeStatic(SaveSystemType, "GetSavePath");
        File.WriteAllText(path, json);

        object loaded = InvokeStatic(SaveSystemType, "Load");
        if (loaded != null) IntradayVillageTestSource.Bind(loaded);
        Assert.IsNotNull(loaded, "旧档必须可加载，不得因缺少 Village / Assignments 区块失败");

        var restored = GetAssignments();
        Assert.IsNotNull(restored, "旧档应初始化空 Assignment 系统");
        Assert.AreEqual(0, restored.Records.Count);
        Assert.AreEqual(0, restored.GetOccupantCount("ws_test_01"));

        // 旧档进入后可以正常进行 V2A 分配
        Assert.AreEqual(VillageAssignmentResult.Success, Assign("char_001", "ws_test_01"));
    }
}
