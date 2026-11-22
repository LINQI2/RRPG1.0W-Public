using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

/// <summary>
/// WP-B Expedition Return Integrity｜PlayMode 真实产品 Return Integration（§14 Case A / B / C）。
///
/// 全部经真实产品入口（MainMenuUI.OnContinue → TownUI → ExpeditionUI.ShowExpeditionFromTown →
/// 真实 btnConfirm → ExpeditionUI.OnReturnToTown / FinalizeReturn），不使用 Mock Runner 代替产品入口。
///   Case A：正常成功 → Settlement → Catch-up → Carry → Consequence → Clear → Final Save → Completed，
///           且 caller 只在真正 Completed 后 Reset elapsed provider / 清 adventure / 恢复 Town；
///   Case B：注入 Between-Day 失败 → 产品再次触发从现有 DaysSimulated 继续 → 已完成 Day 不重复 → Completed；
///   Case C：最终 Save false（可靠 test seam）→ 保持 OccupancyCleared / 未 Completed → 再次触发只 retry Save → Completed；
///   另含 caller 契约：未完成事务不得执行完成态清理（不 Reset elapsed provider / 不清 adventure / 不进 Town）。
///
/// 团结引擎限制：Assembly-CSharp（TownUI / ExpeditionUI / AdventureManager / SaveSystem /
/// VillageRuntimeBridge / MainMenuUI / PlayerCollection）经反射访问；
/// Village.Runtime / BattleStage1.Runtime 类型可强类型访问。
/// </summary>
public partial class ExpeditionReturnIntegrityProductPlayTests
{
    private const BindingFlags F =
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

    private static Type GetType(string name) => System.Type.GetType(name + ", Assembly-CSharp");

    private static object FindInstance(string typeName)
    {
        var t = GetType(typeName);
        if (t == null) return null;
        foreach (var o in UnityEngine.Object.FindObjectsOfType(t, true))
        {
            if (o != null) return o;
        }
        return null;
    }

    private static object GetMember(object o, string name)
    {
        var f = o.GetType().GetField(name, F);
        if (f != null) return f.GetValue(o);
        var p = o.GetType().GetProperty(name, F);
        Assert.IsNotNull(p, "属性/字段不存在：" + name);
        return p.GetValue(o, null);
    }

    private static object GetField(object o, string name)
    {
        var f = o.GetType().GetField(name, F);
        Assert.IsNotNull(f, "字段不存在：" + name);
        return f.GetValue(o);
    }

    private static void SetField(object o, string name, object value)
    {
        var f = o.GetType().GetField(name, F);
        Assert.IsNotNull(f, "字段不存在：" + name);
        f.SetValue(o, value);
    }

    private static object Call(object o, string method, params object[] args)
    {
        var m = o.GetType().GetMethod(method, F);
        Assert.IsNotNull(m, "方法不存在：" + method);
        return m.Invoke(o, args);
    }

    private static object CallStatic(string typeName, string method, params object[] args)
    {
        var m = GetType(typeName).GetMethod(method, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
        Assert.IsNotNull(m, "静态方法不存在：" + typeName + "." + method);
        return m.Invoke(null, args);
    }

    private static object GetBridgeStatic(string name)
    {
        var t = GetType("VillageRuntimeBridge");
        var f = t.GetField(name, F);
        if (f != null) return f.GetValue(null);
        var p = t.GetProperty(name, F);
        Assert.IsNotNull(p, "桥成员不存在：" + name);
        return p.GetValue(null, null);
    }

    private static void SetBridgeStatic(string name, object value)
    {
        var t = GetType("VillageRuntimeBridge");
        var f = t.GetField(name, F);
        Assert.IsNotNull(f, "桥字段不存在：" + name);
        f.SetValue(null, value);
    }

    private static void SetBridgeDayHook(Action<int> hook) =>
        SetBridgeStatic("BeforeCatchUpDayTickTestHook", hook);

    private static void SetElapsedProvider(Func<int> provider) =>
        CallStatic("VillageRuntimeBridge", "SetExpeditionElapsedDaysProvider", provider);

    private static int ResolvedElapsedDays() =>
        (int)CallStatic("VillageRuntimeBridge", "ResolveExpeditionElapsedDays");

    private static void SetSaveFailure(Func<bool> hook) =>
        GetType("SaveSystem").GetField("ForceSaveFailureForTests", F).SetValue(null, hook);

    private static void ResetBridgeAndDeleteSave()
    {
        CallStatic("VillageRuntimeBridge", "ResetForTests");
        SetSaveFailure(null);
        string p = (string)CallStatic("SaveSystem", "GetSavePath");
        if (!string.IsNullOrEmpty(p) && System.IO.File.Exists(p)) System.IO.File.Delete(p);
    }

    private static IEnumerator Pump(IEnumerator inner)
    {
        while (inner.MoveNext()) yield return inner.Current;
    }

    private static object DataManagerInstance() =>
        GetType("DataManager").GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)
            .GetValue(null);

    private static object GetPropertyOrField(object o, string name)
    {
        var p = o.GetType().GetProperty(name, F);
        if (p != null) return p.GetValue(o, null);
        var f = o.GetType().GetField(name, F);
        Assert.IsNotNull(f, "成员不存在：" + name);
        return f.GetValue(o);
    }

    private static object NewAdventureFromExpedition(object collection, object expedition, object dataManager)
    {
        var advType = GetType("AdventureManager");
        var ctor = advType.GetConstructor(new[]
        {
            GetType("PlayerCollection"),
            GetMember(expedition, "SelectedCharacters").GetType(),
            GetType("HeroInstance"),
            GetPropertyOrField(dataManager, "AllCharacters").GetType(),
            GetPropertyOrField(dataManager, "AllSkills").GetType(),
            StaticMonsterTestData.Build().GetType(),
            GetType("RunConfig")
        });
        Assert.IsNotNull(ctor, "AdventureManager 构造函数未找到");
        return ctor.Invoke(new object[]
        {
            collection,
            GetMember(expedition, "SelectedCharacters"),
            GetMember(expedition, "SelectedHero"),
            GetPropertyOrField(dataManager, "AllCharacters"),
            GetPropertyOrField(dataManager, "AllSkills"),
            StaticMonsterTestData.Build(),
            null
        });
    }

    /// <summary>本测试会话对象（真实产品宿主引用）。</summary>
    private class ProductRun
    {
        public int? StartMinute;
        public object ExpUI;
        public object Town;
        public object PlayerData;
        public object Adventure;
        public GameObject TownGo;
        public GameObject ExpGo;
        public List<string> ExpectedIds = new List<string>();
    }

    /// <summary>
    /// 真实产品入口链：SampleScene → NewGame（无档时）→ MainMenuUI.OnContinue → TownUI →
    /// ExpeditionUI.ShowExpeditionFromTown → 真实 btnConfirm → 同构 AdventureManager 绑定为当前 Adventure。
    /// </summary>
    private static IEnumerator EnterProductRun(ProductRun r)
    {
        SceneManager.LoadScene("SampleScene");
        yield return null;
        yield return null;

        if (!(bool)CallStatic("SaveSystem", "HasSave"))
        {
            object fresh = CallStatic("SaveSystem", "NewGame");
            Assert.IsNotNull(fresh, "NewGame 失败");
            Assert.IsTrue((bool)CallStatic("SaveSystem", "Save", fresh), "新档应能写入");
        }

        var mm = FindInstance("MainMenuUI");
        Assert.IsNotNull(mm, "MainMenuUI 应存在");
        Call(mm, "OnContinue");
        yield return null;
        yield return null;

        r.Town = FindInstance("TownUI");
        Assert.IsNotNull(r.Town, "TownUI 应存在");
        r.TownGo = (GameObject)GetPropertyOrField(r.Town, "gameObject");
        r.PlayerData = GetMember(r.Town, "PlayerData");
        Assert.IsNotNull(r.PlayerData, "Town 应持有 PlayerCollection");

        r.ExpUI = FindInstance("ExpeditionUI");
        Assert.IsNotNull(r.ExpUI, "ExpeditionUI 应存在");
        r.ExpGo = (GameObject)GetPropertyOrField(r.ExpUI, "gameObject");

        ((Button)GetField(r.Town, "btnExpedition")).onClick.Invoke();
        yield return null;
        yield return null;

        var expedition = GetField(r.ExpUI, "_expedition");
        var selected = (IList)GetMember(expedition, "SelectedCharacters");
        Assert.Greater(selected.Count, 0, "远征准备应自动选中至少一名真实 Person");
        foreach (var card in selected)
        {
            string pid = (string)Call(r.PlayerData, "ResolvePersonIdForCard", card);
            if (!string.IsNullOrEmpty(pid)) r.ExpectedIds.Add(pid);
        }
        var selHero = GetMember(expedition, "SelectedHero");
        if (selHero != null)
        {
            string heroPid = (string)GetMember(selHero, "PersonId");
            if (!string.IsNullOrEmpty(heroPid)) r.ExpectedIds.Add(heroPid);
        }
        Assert.Greater(r.ExpectedIds.Count, 0, "出征名单应含至少一个真实 PersonId");

        // 真实 Carry preflight 需要真实 Village Food（不伪造 Carry）。
        object villageResources = GetBridgeStatic("Resources");
        Assert.IsNotNull(villageResources, "Village Resources 应已初始化");
        Call(villageResources, "AddFood", 100);

        if (r.StartMinute.HasValue)
        {
            var clock=(VillageTime)GetBridgeStatic("Time");
            var instant=WorldInstant.FromDate(1,1,1,r.StartMinute.Value);
            clock.Restore(new VillageTimeSaveData {Year=1,Month=1,Day=1,Speed=1,
                IntradayTime=new VillageIntradayTimeSaveData{SchemaVersion=1,CurrentWorldMinute=instant.Minute,LastCompletedDailyBoundaryMinute=0}});
        }
        var btnConfirm = (Button)GetField(r.ExpUI, "btnConfirm");
        Assert.IsNotNull(btnConfirm, "btnConfirm 应绑定");
        btnConfirm.onClick.Invoke();
        yield return null;
        yield return null;

        Assert.IsFalse(r.ExpGo.activeSelf, "确认后应进入远征（ExpeditionUI 隐藏）");
        Assert.AreEqual(r.ExpectedIds.Count,
            ((IReadOnlyList<string>)GetBridgeStatic("ExpeditionPersonIds")).Count,
            "Expedition Occupancy 登记应恰为真实 Person 名单");

        // 与产品同构的 Run（真实 PlayerData + DataManager 全量数据），并绑定为当前 Adventure。
        r.Adventure = NewAdventureFromExpedition(r.PlayerData, expedition, DataManagerInstance());
        Assert.IsNotNull(GetPropertyOrField(r.Adventure, "RunState"), "AdventureManager.RunState 应存在");
        SetField(r.ExpUI, "_currentAdventure", r.Adventure);
    }

    [SetUp]
    public void SetUp() => ResetBridgeAndDeleteSave();

    [TearDown]
    public void TearDown() => ResetBridgeAndDeleteSave();

    // ==================== Case A｜正常成功（含 Completed 后才 cleanup）====================

    [UnityTest]
    public IEnumerator CaseA_NormalReturn_ProductPath_CompletesThenCleansUp()
    {
        return Pump(CaseA());
    }

    private static IEnumerator CaseA()
    {
        var r = new ProductRun();
        yield return EnterProductRun(r);

        var time = (VillageTime)GetBridgeStatic("Time");
        var state = (VillageState)GetBridgeStatic("State");
        int dayBase = time.Day;
        int stateBase = state.TestSurvivalValue;
        object food = GetBridgeStatic("Resources");
        Assert.IsNotNull(food, "Village Resources 应已初始化");

        SetElapsedProvider(() => 2); // 真实 elapsed seam（Test / Not Frozen）：Return 需逐日补算 2 天
        yield return null;

        Call(r.ExpUI, "OnReturnToTown"); // 真实产品 Return 入口（Retreat）
        yield return null;
        yield return null;

        var record = (ExpeditionRecord)GetBridgeStatic("ActiveReturnRecord");
        Assert.IsNotNull(record, "Return 会话记录应存在");
        Assert.IsTrue(record.Completed, "正常成功：最终 Return Save 成功后 Completed=true");
        Assert.AreEqual(ExpeditionReturnPhase.Completed, record.Phase);
        Assert.AreEqual(2, record.DaysSimulated, "Catch-up 完整逐日 2 天");
        Assert.AreEqual(dayBase + 2, time.Day, "Village 时间恰好推进 2 天（不重复）");
        Assert.AreEqual(stateBase + 2, state.TestSurvivalValue, "逐日 Village 效果恰好 2 次");
        Assert.Greater((int)GetPropertyOrField(food, "Food"), 0, "剩余 Carry 已返还 Village Food");

        // Completed 后才允许的完成态清理。
        Assert.AreEqual(0, ((IReadOnlyList<string>)GetBridgeStatic("ExpeditionPersonIds")).Count,
            "Expedition Occupancy 已清除");
        Assert.IsNull(GetField(r.ExpUI, "_currentAdventure"), "Return Completed 后才清 adventure 引用");
        Assert.AreEqual(0, ResolvedElapsedDays(), "Return Completed 后才 Reset elapsed provider");
        Assert.IsTrue(r.TownGo.activeSelf, "Return 后 Town 恢复显示");
        Assert.IsTrue((bool)GetBridgeStatic("IsSimulationActive"), "Return 后 Village 模拟恢复");

        // 再次触发：Completed → no-op（不重复逐日、不重复 Save、不重复返还）。
        int dayAfter = time.Day;
        int foodAfter = (int)GetPropertyOrField(food, "Food");
        Call(r.ExpUI, "OnReturnToTown");
        yield return null;

        Assert.IsTrue(record.Completed);
        Assert.AreEqual(2, record.DaysSimulated);
        Assert.AreEqual(dayAfter, time.Day, "Completed 后重复触发不得再推进时间");
        Assert.AreEqual(foodAfter, (int)GetPropertyOrField(food, "Food"), "Completed 后不得重复返还 Carry");
    }

    // ==================== Case B｜可重试失败 → 从 DaysSimulated 继续 ====================

    [UnityTest]
    public IEnumerator CaseB_RetryableCatchUpFailure_ResumesFromDaysSimulated()
    {
        return Pump(CaseB());
    }

    private static IEnumerator CaseB()
    {
        var r = new ProductRun();
        yield return EnterProductRun(r);

        var time = (VillageTime)GetBridgeStatic("Time");
        var state = (VillageState)GetBridgeStatic("State");
        int dayBase = time.Day;
        int stateBase = state.TestSurvivalValue;

        SetElapsedProvider(() => 4);
        yield return null;

        bool threw = false;
        SetBridgeDayHook(day =>
        {
            if (day == 2 && !threw)
            {
                threw = true;
                throw new InvalidOperationException("WP-B Case B between-day failure");
            }
        });

        LogAssert.Expect(LogType.Error, new Regex("Return Catch-up 未完成"));
        Call(r.ExpUI, "OnReturnToTown"); // 第一次：Catch-up 第 3 天前失败
        yield return null;
        yield return null;

        var record = (ExpeditionRecord)GetBridgeStatic("ActiveReturnRecord");
        Assert.IsTrue(threw, "故障注入应触发");
        Assert.IsFalse(record.Completed, "可重试失败：Return 未 Completed");
        Assert.AreEqual(ExpeditionReturnPhase.ReturnSettled, record.Phase, "可同会话从原进度 resume");
        Assert.AreEqual(2, record.DaysSimulated, "已完成 2 天");
        Assert.AreEqual(dayBase + 2, time.Day, "已完整执行的 2 天不重复");
        Assert.AreEqual(stateBase + 2, state.TestSurvivalValue);

        // Return 未完成 → 产品 caller 不得执行完成态清理（恢复入口必须保留）。
        Assert.AreEqual(r.ExpectedIds.Count,
            ((IReadOnlyList<string>)GetBridgeStatic("ExpeditionPersonIds")).Count,
            "Return 未完成 → 保留 Expedition Occupancy");
        Assert.IsNotNull(GetField(r.ExpUI, "_currentAdventure"), "Return 未完成 → 不得清 adventure");
        Assert.AreEqual(4, ResolvedElapsedDays(), "Return 未完成 → 不得 Reset elapsed provider");
        Assert.IsTrue((bool)GetField(r.ExpUI, "_fromTown"),
            "Return 未完成 → 保留「返回领地」恢复入口（不进入 Town 完成态）");

        // 问题解除后再次触发产品入口：从 DaysSimulated 继续，只补剩余 2 天。
        SetBridgeDayHook(null);
        Call(r.ExpUI, "OnReturnToTown");
        yield return null;
        yield return null;

        Assert.IsTrue(record.Completed, "Resume 后最终 Completed");
        Assert.AreEqual(ExpeditionReturnPhase.Completed, record.Phase);
        Assert.AreEqual(4, record.DaysSimulated, "只补剩余 2 天（不重复已完成 Day）");
        Assert.AreEqual(dayBase + 4, time.Day, "Village 时间恰好推进 4 天");
        Assert.AreEqual(stateBase + 4, state.TestSurvivalValue, "逐日 Village 效果恰好 4 次");
        Assert.AreEqual(0, ((IReadOnlyList<string>)GetBridgeStatic("ExpeditionPersonIds")).Count,
            "Completed 后 Occupancy 清除");
        Assert.IsNull(GetField(r.ExpUI, "_currentAdventure"), "Completed 后清 adventure 引用");
        Assert.AreEqual(0, ResolvedElapsedDays(), "Completed 后 Reset elapsed provider");
    }

    // ==================== Case C｜Final Save failure → 只 retry Save ====================

    [UnityTest]
    public IEnumerator CaseC_FinalSaveFailure_RetryOnlySave()
    {
        return Pump(CaseC());
    }

    private static IEnumerator CaseC()
    {
        var r = new ProductRun();
        yield return EnterProductRun(r);

        var time = (VillageTime)GetBridgeStatic("Time");
        var state = (VillageState)GetBridgeStatic("State");
        object food = GetBridgeStatic("Resources");
        int dayBase = time.Day;
        int stateBase = state.TestSurvivalValue;

        SetElapsedProvider(() => 1);
        yield return null;

        // 可靠 test seam：最终 Return Save 强制失败（不写盘）。
        SetSaveFailure(() => true);

        LogAssert.Expect(LogType.Error, new Regex("最终 Return Save 失败"));
        Call(r.ExpUI, "OnReturnToTown");
        yield return null;
        yield return null;

        var record = (ExpeditionRecord)GetBridgeStatic("ActiveReturnRecord");
        Assert.IsNotNull(record);
        Assert.IsFalse(record.Completed, "Save false → Return 不得被宣称 Completed");
        Assert.AreEqual(ExpeditionReturnPhase.OccupancyCleared, record.Phase,
            "保持 OccupancyCleared（只可重试 Save）");
        Assert.AreEqual(1, record.DaysSimulated, "Catch-up 已完成 1 天");
        Assert.AreEqual(dayBase + 1, time.Day, "逐日推进只发生一次");
        Assert.AreEqual(stateBase + 1, state.TestSurvivalValue);
        Assert.AreEqual(0, ((IReadOnlyList<string>)GetBridgeStatic("ExpeditionPersonIds")).Count,
            "Occupancy 已在 Save 之前按顺序清除");

        int foodAfterCarry = (int)GetPropertyOrField(food, "Food");
        Assert.IsTrue(record.RemainingCarryReturned, "Carry 已返还（exactly-once）");
        Assert.IsTrue(record.HealthConsequenceCommitted, "Person Consequence 已提交（exactly-once）");
        Assert.IsTrue(record.ExperienceCommitted);

        // Return 未 Completed → 不得执行完成态清理。
        Assert.IsNotNull(GetField(r.ExpUI, "_currentAdventure"), "未 Completed → 不得清 adventure");
        Assert.AreEqual(1, ResolvedElapsedDays(), "未 Completed → 不得 Reset elapsed provider");

        // 再次触发：只 retry Final Save（不重结算 / 不重补算 / 不重复返还 Carry / 不重复 Consequence）。
        SetSaveFailure(null);
        Call(r.ExpUI, "OnReturnToTown");
        yield return null;
        yield return null;

        Assert.IsTrue(record.Completed, "Retry Save 成功 → Completed");
        Assert.AreEqual(ExpeditionReturnPhase.Completed, record.Phase);
        Assert.AreEqual(1, record.DaysSimulated, "只 retry Save：不得重跑 Catch-up");
        Assert.AreEqual(dayBase + 1, time.Day, "不得再次推进 Village 时间");
        Assert.AreEqual(stateBase + 1, state.TestSurvivalValue, "不得重复逐日效果");
        Assert.AreEqual(foodAfterCarry, (int)GetPropertyOrField(food, "Food"), "不得重复返还 Carry");

        // 只有真正 Completed 后才执行完成态清理。
        Assert.IsNull(GetField(r.ExpUI, "_currentAdventure"), "Completed 后才清 adventure 引用");
        Assert.AreEqual(0, ResolvedElapsedDays(), "Completed 后才 Reset elapsed provider");
    }
}
