using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;

/// <summary>
/// V3A Social Fact 记录点 + Save/Load 集成测试（真实 SaveSystem / VillageRuntimeBridge 反射）：
///   - 事实只记录在「真实成功的 Project Temporary Commitment 动作」之后
///     （TryStartProject / TryAddProjectParticipant），且不复制 Assignment / Project 当前状态；
///   - 整批失败回滚 → 不记录任何事实；
///   - 同一动作幂等：重复加入 / Save→Load 后再加入都不重复产生事实；
///   - Save → Load 后年度事实与计数一致；
///   - 旧档（无 Village / 无 Social 区块）以空账本兼容加载；
///   - 带非法 Social 记录的存档经 Bridge Restore 防御性过滤。
/// 团结引擎限制：Bridge / SaveSystem（Assembly-CSharp）经反射；Village.Runtime（含 ledger）强类型。
/// </summary>
public class VillageSocialFactSaveTests
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

    // ==================== helpers（反射 / 强类型混合） ====================

    private static void EnsureInitialized()
    {
        InvokeStatic(BridgeType, "EnsureInitialized", new object[] { null });
        IntradayVillageTestSource.BindDefaults();
    }

    private static void DeleteSave()
    {
        string path = (string)InvokeStatic(SaveSystemType, "GetSavePath");
        if (path != null && File.Exists(path)) File.Delete(path);
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
                if (args[i] == null) continue;
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

    private static VillageProjectSystem GetProjects()
    {
        return (VillageProjectSystem)GetStaticField(BridgeType, "Projects");
    }

    private static VillageTime GetTime()
    {
        return (VillageTime)GetStaticField(BridgeType, "Time");
    }

    private static VillageSocialFactLedger GetLedger()
    {
        return (VillageSocialFactLedger)GetStaticField(BridgeType, "SocialFacts");
    }

    private static VillageProjectResult StartProject(string defId, string[] participants)
    {
        return (VillageProjectResult)InvokeStatic(BridgeType, "TryStartProject", defId, participants);
    }

    private static VillageProjectResult AddParticipant(string projectId, string personId)
    {
        return (VillageProjectResult)InvokeStatic(BridgeType, "TryAddProjectParticipant", projectId, personId);
    }

    private static void TickDays(int days)
    {
        var time = GetTime();
        for (int i = 0; i < days; i++) time.TickDay();
    }

    private static object NewPlayerCollection()
    {
        return IntradayVillageTestSource.Current ?? IntradayVillageTestSource.Create();
    }

    // ==================== 记录点：真实成功动作后才记录 ====================

    [Test]
    public void StartProject_Success_RecordsOneFactPerParticipant()
    {
        EnsureInitialized();
        var time = GetTime();
        Assert.AreEqual(VillageProjectResult.Success, StartProject("prj_test_01", new[] { "char_001" }));

        var ledger = GetLedger();
        Assert.IsNotNull(ledger, "EnsureInitialized 后 SocialFacts 账本必须存在");
        Assert.AreEqual(1, ledger.Facts.Count, "一个成功加入应产生一条 Temporary Commitment 事实");
        Assert.AreEqual("char_001", ledger.Facts[0].PersonId);
        Assert.AreEqual("prj_test_01", ledger.Facts[0].ProjectDefId);
        Assert.AreEqual(time.Year, ledger.Facts[0].Year);
        Assert.AreEqual(time.Month, ledger.Facts[0].Month);
        Assert.AreEqual(time.Day, ledger.Facts[0].Day);
        Assert.AreEqual(1, ledger.CountTemporaryCommitmentsInYear("char_001", 1));
        Assert.AreEqual(1, VillageRuntimeBridgeStaticCount("char_001", 1), "Bridge 只读查询应返回一致计数");
    }

    [Test]
    public void AddParticipant_AfterCreate_RecordsFactForAddedPersonOnly()
    {
        EnsureInitialized();
        Assert.AreEqual(VillageProjectResult.Success, StartProject("prj_test_01", new string[0]));
        string pid = GetProjects().ActiveProjects[0].ProjectId;
        Assert.IsFalse(string.IsNullOrEmpty(pid));

        Assert.AreEqual(VillageProjectResult.Success, AddParticipant(pid, "char_002"));
        var ledger = GetLedger();
        Assert.AreEqual(1, ledger.Facts.Count, "只记录真实加入的 Person");
        Assert.AreEqual("char_002", ledger.Facts[0].PersonId);
        Assert.AreEqual(0, ledger.CountTemporaryCommitmentsInYear("char_001", 1), "未被加入的人不得产生事实");
    }

    [Test]
    public void FailedBatch_Rollback_RecordsNoFacts_AndNoActiveProject()
    {
        EnsureInitialized();
        // char_busy 在远征 → 整批失败并回滚；char_ok 即使可加入也不得留下任何事实
        InvokeStatic(BridgeType, "SetExpeditionPersonIds", (object)new[] { "char_busy" });
        Assert.AreEqual(VillageProjectResult.PersonCurrentlyUnavailable,
            StartProject("prj_test_01", new[] { "char_ok", "char_busy" }));

        var ledger = GetLedger();
        Assert.AreEqual(0, ledger.Facts.Count, "整体失败回滚后不得记录任何 Person 的事实");
        Assert.AreEqual(0, GetProjects().ActiveProjects.Count, "回滚后不得残留 Active Project");
    }

    private static int VillageRuntimeBridgeStaticCount(string personId, int year)
    {
        return (int)InvokeStatic(BridgeType, "CountTemporaryCommitmentsInYear", personId, year);
    }

    [Test]
    public void ReAddSamePersonSameProject_IsIdempotent_NoDuplicateFact()
    {
        EnsureInitialized();
        Assert.AreEqual(VillageProjectResult.Success, StartProject("prj_test_01", new[] { "char_001" }));
        string pid = GetProjects().ActiveProjects[0].ProjectId;

        // Projects 层幂等成功（已在该 Project），Bridge 层不得再次产生事实
        Assert.AreEqual(VillageProjectResult.Success, AddParticipant(pid, "char_001"));
        var ledger = GetLedger();
        Assert.AreEqual(1, ledger.Facts.Count, "同一 Person 对同一 Project 重复加入不得产生第二条事实");
    }

    [Test]
    public void SecondProject_AfterFirstCompletes_SameYearCountsTwo()
    {
        EnsureInitialized();
        Assert.AreEqual(VillageProjectResult.Success, StartProject("prj_test_01", new[] { "char_001" }));
        TickDays(10); // prj_test_01 target=10 / 1 参与者 → 完成并释放
        Assert.AreEqual(0, GetProjects().ActiveProjects.Count);

        Assert.AreEqual(VillageProjectResult.Success, StartProject("prj_test_02", new[] { "char_001" }));
        var ledger = GetLedger();
        Assert.AreEqual(2, ledger.Facts.Count);
        Assert.AreEqual(2, ledger.CountTemporaryCommitmentsInYear("char_001", 1));
        Assert.AreEqual(2, VillageRuntimeBridgeStaticCount("char_001", 1));
    }

    // ==================== Save / Load ====================

    [Test]
    public void SaveLoad_RoundTrip_PreservesFactsAndCounts_NoDuplicateAfterLoad()
    {
        EnsureInitialized();
        Assert.AreEqual(VillageProjectResult.Success, StartProject("prj_test_01", new[] { "char_001" }));
        TickDays(10); // 完成第一个
        Assert.AreEqual(VillageProjectResult.Success, StartProject("prj_test_02", new[] { "char_001" }));
        TickDays(2); // 第二个进行中（Active 可存档）
        Assert.AreEqual(2, GetLedger().Facts.Count);

        object collection = NewPlayerCollection();
        Assert.IsTrue((bool)InvokeStatic(SaveSystemType, "Save", collection), "真实 Save 路径应携带 Social 区块并成功");
        InvokeStatic(BridgeType, "ResetForTests");
        object loaded = InvokeStatic(SaveSystemType, "Load");
        if (loaded != null) IntradayVillageTestSource.Bind(loaded);
        Assert.IsNotNull(loaded, "带 Social 区块的存档必须可加载");

        var ledger = GetLedger();
        Assert.IsNotNull(ledger);
        Assert.AreEqual(2, ledger.Facts.Count, "Save→Load 后事实必须保持");
        Assert.AreEqual(2, ledger.CountTemporaryCommitmentsInYear("char_001", 1));
        Assert.AreEqual(2, VillageRuntimeBridgeStaticCount("char_001", 1));

        // 加载后再把已在 Active Project 的同一 Person 重复加入 → 仍幂等，不产生第 3 条事实
        string pid = GetProjects().ActiveProjects[0].ProjectId;
        Assert.IsFalse(string.IsNullOrEmpty(pid));
        Assert.AreEqual(VillageProjectResult.Success, AddParticipant(pid, "char_001"));
        Assert.AreEqual(2, ledger.Facts.Count, "中途 Save/Load 后重复加入不得再次产生事实");
    }

    [Test]
    public void LegacySave_WithoutVillage_LoadsEmptyLedger()
    {
        string json = "{\"SaveVersion\":1,\"GameVersion\":\"0.2.3\",\"SavedAtUnixTime\":0," +
                      "\"Player\":{\"Gold\":100,\"Heroes\":[],\"Characters\":[],\"OwnedCharacterCardIds\":[],\"OwnedItemCardIds\":[]}}";
        string path = (string)InvokeStatic(SaveSystemType, "GetSavePath");
        File.WriteAllText(path, json);

        object collection = InvokeStatic(SaveSystemType, "Load");
        Assert.IsNotNull(collection, "旧档（无 Village / 无 Social）必须可加载");

        var ledger = GetLedger();
        Assert.IsNotNull(ledger, "旧档加载后应以空账本默认初始化");
        Assert.AreEqual(0, ledger.Facts.Count);
        Assert.AreEqual(0, ledger.CountTemporaryCommitmentsInYear("char_001", 1));
    }

    [Test]
    public void Restore_MixedSocialRecords_DefensiveFilterThroughBridge()
    {
        // 直接构造带 Village.Social 的存档数据，经真实 Bridge.EnsureInitialized 恢复路径验证防御过滤
        var data = new VillageSaveData
        {
            Social = new VillageSocialSaveData
            {
                Facts = new List<VillageSocialFactSaveData>
                {
                    new VillageSocialFactSaveData { Type = 0, PersonId = "char_good", ProjectDefId = "d", ProjectId = "ok1", Year = 1, Month = 1, Day = 1 },
                    new VillageSocialFactSaveData { Type = 999, PersonId = "char_bad_type", ProjectDefId = "d", ProjectId = "bad", Year = 1, Month = 1, Day = 1 },
                    new VillageSocialFactSaveData { Type = 0, PersonId = "", ProjectDefId = "d", ProjectId = "bad2", Year = 1, Month = 1, Day = 1 }
                }
            }
        };
        InvokeStatic(BridgeType, "EnsureInitialized", data);

        var ledger = GetLedger();
        Assert.AreEqual(1, ledger.Facts.Count, "非法 Type / 空 PersonId 记录必须被防御性丢弃");
        Assert.AreEqual(1, ledger.CountTemporaryCommitmentsInYear("char_good", 1));
    }
}
