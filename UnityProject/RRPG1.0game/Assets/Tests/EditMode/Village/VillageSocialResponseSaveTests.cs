using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;

/// <summary>
/// V3B Recognition → Response → New Decision 集成测试（真实 SaveSystem / VillageRuntimeBridge 反射）。
/// 覆盖：post-add 求值（当前工程不回滚）→ Choice A/B → Rest Gate 未来调度差异（含月末边界）
///      → fired 防重复 → Save/Load 四场景 → 旧档空默认。
/// </summary>
public class VillageSocialResponseSaveTests
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

    private static VillageSocialResponseLog GetResponse()
    {
        return (VillageSocialResponseLog)GetStaticField(BridgeType, "SocialResponse");
    }

    private static VillageProjectResult StartProject(string defId, string[] participants)
    {
        return (VillageProjectResult)InvokeStatic(BridgeType, "TryStartProject", defId, participants);
    }

    private static void TickDays(int days)
    {
        var time = GetTime();
        for (int i = 0; i < days; i++) time.TickDay();
    }

    private static bool Resolve(bool grantRest)
    {
        var choice = grantRest
            ? VillageSocialResponseChoice.GrantRest
            : VillageSocialResponseChoice.ContinueUsing;
        return (bool)InvokeStatic(BridgeType, "ResolveSocialResponse", choice);
    }

    private static object NewPlayerCollection()
    {
        return System.Activator.CreateInstance(System.Type.GetType("PlayerCollection, Assembly-CSharp"));
    }

    /// <summary>完成一个项目所需 Tick 数（Test def 常量：prj_test_01=10@1/day；prj_test_02=6@2/day）。</summary>
    private static int CompletionTicks(string defId)
    {
        return defId == "prj_test_02" ? 3 : 10;
    }

    /// <summary>启动一个项目并推进到完成释放（真实调度动作；第 N 次由此产生 V3A 事实）。</summary>
    private static void StartAndComplete(string defId, string personId)
    {
        Assert.AreEqual(VillageProjectResult.Success, StartProject(defId, new[] { personId }),
            "加入新 Project 应成功：" + defId);
        TickDays(CompletionTicks(defId));
        Assert.AreEqual(0, GetProjects().ActiveProjects.Count, "项目应完成并释放参与者");
    }

    // ==================== B 类已迁移（WP-Event-1D Gate D1 · FDS §21.6.1） ====================
    //
    // 原「第 3 次抽调 → Legacy Pending → ResolveSocialResponse」的 3 条**产品路径**用例
    //（Choice A / Choice B / Save-Load mid-session）已按「B 必须迁移」迁移为 **Event Product Contract**：
    // 真实入口 + Unified Event Pending + Option → Outcome → Effect + exactly-once + Save/Load：
    //   `Assets/Tests/EditMode/EventProduct/VillageEventFlowContractTests.cs`（V1 / V5 / V6 / V7 / V12 / V13）。
    //
    // 本文件自此只保护 **Legacy 兼容面**：旧档 Save 收敛、Legacy 防御性恢复、Legacy API 仍可用于兼容解析。

    [Test]
    public void LegacyPending_CompatPath_StillResolvesAndWritesLegacyHistory()
    {
        EnsureInitialized();
        const string pid = "char_001";

        // 兼容面：Legacy Pending 仍可由 Legacy API 解析（旧档 Pending 无法映射到 Event 时的兜底路径）。
        var log = GetResponse();
        Assert.AreEqual(VillageResponseOpenResult.Created,
            log.OpenBuildupResponse(VillageTestSocialPatterns.BuildupDraftPatternKey, pid, 1,
                VillageTestSocialPatterns.BuildupDraftThreshold, VillageTestSocialPatterns.BuildupDraftThreshold));
        Assert.IsTrue(log.HasPendingResponse);

        Assert.IsTrue(Resolve(true), "Legacy API 必须仍能解析 Legacy Pending（兼容面保留）");
        Assert.IsFalse(log.HasPendingResponse);
        Assert.AreEqual(1, log.Choices.Count);
        Assert.IsNotNull(log.GetLatestRest(pid));
        Assert.IsTrue((bool)InvokeStatic(BridgeType, "IsPersonInRestWindow", pid));
    }

    [Test]
    public void LegacySave_WithoutVillage_LoadsEmptySocialResponse()
    {
        string json = "{\"SaveVersion\":1,\"GameVersion\":\"0.2.3\",\"SavedAtUnixTime\":0," +
                      "\"Player\":{\"Gold\":100,\"Heroes\":[],\"Characters\":[],\"OwnedCharacterCardIds\":[],\"OwnedItemCardIds\":[]}}";
        string path = (string)InvokeStatic(SaveSystemType, "GetSavePath");
        File.WriteAllText(path, json);

        Assert.IsNotNull(InvokeStatic(SaveSystemType, "Load"), "旧档必须可加载");
        var response = GetResponse();
        Assert.IsNotNull(response, "旧档应以空 SocialResponse 默认初始化");
        Assert.AreEqual(0, response.Fired.Count);
        Assert.AreEqual(0, response.Choices.Count);
        Assert.AreEqual(0, response.Rests.Count);
        Assert.IsFalse(response.HasPendingResponse);
        Assert.IsFalse((bool)InvokeStatic(BridgeType, "IsPersonInRestWindow", "char_001"));
    }

    [Test]
    public void EnsureInitialized_MixedSocialResponse_DefensiveFilter()
    {
        var data = new VillageSaveData
        {
            SocialResponse = new VillageSocialResponseSaveData
            {
                Fired = new List<VillageSocialFiredSaveData>
                {
                    new VillageSocialFiredSaveData { PatternKey = VillageTestSocialPatterns.BuildupDraftPatternKey, PersonId = "char_ok", Year = 1 },
                    new VillageSocialFiredSaveData { PatternKey = "", PersonId = "char_bad", Year = 1 }
                },
                Choices = new List<VillageResponseChoiceSaveData>
                {
                    new VillageResponseChoiceSaveData { PatternKey = VillageTestSocialPatterns.BuildupDraftPatternKey, PersonId = "char_ok", Choice = 0, Year = 1, Month = 1, Day = 1 },
                    new VillageResponseChoiceSaveData { PatternKey = VillageTestSocialPatterns.BuildupDraftPatternKey, PersonId = "char_ok", Choice = 42, Year = 1, Month = 1, Day = 1 }
                },
                Rests = new List<VillageRestSaveData>
                {
                    new VillageRestSaveData { PersonId = "char_ok", UntilYear = 1, UntilMonth = 1, UntilDay = 31 },
                    new VillageRestSaveData { PersonId = "char_bad", UntilYear = 1, UntilMonth = 2, UntilDay = 30 }
                }
            }
        };
        InvokeStatic(BridgeType, "EnsureInitialized", data);

        var response = GetResponse();
        Assert.AreEqual(1, response.Fired.Count, "空 PatternKey fired 应丢弃");
        Assert.AreEqual(1, response.Choices.Count, "未知 Choice 枚举序应丢弃");
        Assert.AreEqual(1, response.Rests.Count, "非法日期 Rest 应丢弃");
        Assert.AreEqual("char_ok", response.Rests[0].PersonId);
    }
}
