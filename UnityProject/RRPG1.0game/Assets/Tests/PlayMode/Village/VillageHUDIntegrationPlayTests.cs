using TMPro;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

/// <summary>
/// Stage V1 PlayMode 集成验证（Town Functional HUD + 时间驱动 gating + 真实保存路径 + Expedition 人物可用状态）。
///
/// 从真实场景验证：
///   1. VillageHUD 宿主在场景中（Edit Mode 可见可调）；
///   2. Village 时间由常驻 VillageTimeDriver 驱动，受 VillageRuntimeBridge.IsSimulationActive gating 控制
///      （Town 激活时推进；不依赖 HUD 是否启用）；
///   3. 真实产品保存路径（TownUI.OnReturnFromExpedition → SaveSystem.Save）捕获最新 VillageTime/State；
///   4. 登记远征人物后，该人物在 Village 中显示为 Expedition、绝不同时 InVillage。
/// 团结引擎限制：VillageRuntimeBridge / SaveSystem / TownUI（Assembly-CSharp）经反射访问；Village.Runtime 强类型。
/// </summary>
public class VillageHUDIntegrationPlayTests
{
    private const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;

    private static System.Type BridgeType => System.Type.GetType("VillageRuntimeBridge, Assembly-CSharp");
    private static System.Type SaveSystemType => System.Type.GetType("SaveSystem, Assembly-CSharp");

    private static object InvokeBridge(string method, params object[] args)
    {
        return InvokeStatic(BridgeType, method, args);
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
        Assert.IsNotNull(selected, "方法不存在：" + method + " @ " + type.FullName);
        return selected.Invoke(null, args);
    }

    private static object GetBridgeField(string name)
    {
        var f = BridgeType.GetField(name, F);
        Assert.IsNotNull(f, "字段不存在：" + name);
        return f.GetValue(null);
    }

    private static object FindInstance(string typeName)
    {
        var t = System.Type.GetType(typeName + ", Assembly-CSharp");
        if (t == null) return null;
        foreach (var o in Object.FindObjectsOfType(t, true))
        {
            if (o != null) return o;
        }
        return null;
    }

    private static object GetProperty(object o, string name)
    {
        var p = o.GetType().GetProperty(name,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(p, "属性不存在：" + name);
        return p.GetValue(o, null);
    }

    private static object InvokeInstance(object instance, string method, params object[] args)
    {
        var m = instance.GetType().GetMethod(method,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(m, "实例方法不存在：" + method + " @ " + instance.GetType().Name);
        return m.Invoke(instance, args);
    }

    /// <summary>
    /// 进入领地必须走真实产品入口：Main Menu 前台（sim=false 门禁）→ 玩家点击主菜单按钮 →
    /// MainMenuUI.EnterTown(collection) → TownUI.Initialize → SetSimulationActive(true)。
    /// 不得再用 SetActive(true) 依赖修复前「TownUI.Start 提前把 sim 置 true、MainMenu 不再复位」的旧行为。
    /// </summary>
    private static object EnterTownViaMainMenu()
    {
        var town = FindInstance("TownUI");
        Assert.IsNotNull(town, "场景应有 TownUI");
        var mm = FindInstance("MainMenuUI");
        Assert.IsNotNull(mm, "场景应有 MainMenuUI");

        if (!(bool)InvokeStatic(SaveSystemType, "HasSave"))
        {
            var fresh = InvokeStatic(SaveSystemType, "NewGame");
            Assert.IsNotNull(fresh, "进入领地前置：应能创建新档数据");
            Assert.IsTrue((bool)InvokeStatic(SaveSystemType, "Save", fresh), "进入领地前置：新档应能写入真实存档");
        }
        InvokeInstance(mm, "OnContinue"); // 产品路径：继续游戏 → 读档 → EnterTown → Initialize
        return FindInstance("TownUI");
    }

    [UnityTest]
    public IEnumerator VillageHUD_DrivesTime_AndExpeditionPersonNotAvailable()
    {
        VillageRuntimeBridge_ResetForTests();

        SceneManager.LoadScene("SampleScene");
        yield return null;
        yield return null;

        // 进入领地：走真实产品入口（MainMenuUI.OnContinue → TownUI.Initialize → sim=true）
        var town = EnterTownViaMainMenu();
        yield return null;
        yield return null;

        var hud = FindInstance("VillageHUD");
        Assert.IsNotNull(hud, "场景应有 VillageHUD 宿主（Edit Mode 可见可调）");

        var time = (VillageTime)GetBridgeField("Time");
        Assert.IsNotNull(time, "VillageHUD Start 应初始化 VillageTime");
        Assert.IsTrue((bool)GetBridgeField("IsSimulationActive"), "领地激活后 Village 模拟 gating 应为开启");

        float before = time.Day * 100000f + time.SecondsIntoDay;
        yield return new WaitForSeconds(0.3f);
        var timeAfter = (VillageTime)GetBridgeField("Time");
        float after = timeAfter.Day * 100000f + timeAfter.SecondsIntoDay;
        Assert.Greater(after, before, "领地激活（gating 开启）时 Village 时间应由 VillageTimeDriver 实时推进");

        // 关闭 gating 后时间应停止推进（显式 state gating，而非 HUD 隐藏）
        InvokeBridge("SetSimulationActive", false);
        float pausedBefore = ((VillageTime)GetBridgeField("Time")).Day * 100000f + ((VillageTime)GetBridgeField("Time")).SecondsIntoDay;
        yield return new WaitForSeconds(0.2f);
        float pausedAfter = ((VillageTime)GetBridgeField("Time")).Day * 100000f + ((VillageTime)GetBridgeField("Time")).SecondsIntoDay;
        Assert.AreEqual(pausedBefore, pausedAfter, "gating 关闭后时间不得推进");
        InvokeBridge("SetSimulationActive", true);

        // Expedition 人物不可在 Village 重复使用
        var pc = GetProperty(town, "PlayerData");
        Assert.IsNotNull(pc, "TownUI 应有 PlayerData");
        var inputs = (List<VillagePersonInput>)InvokeBridge("BuildPersonInputs", pc);
        Assert.Greater(inputs.Count, 0, "应有长期人物进入 Village");
        string pid = inputs[0].PersonId;

        InvokeBridge("SetExpeditionPersonIds", new object[] { new[] { pid } });
        var views = (List<VillagePersonView>)InvokeBridge("GetPersonAvailability", pc);
        var v = views.Find(x => x.PersonId == pid);
        Assert.IsNotNull(v, "人物应出现在可用状态列表");
        Assert.AreEqual(VillageAvailability.Expedition, v.Availability, "远征中人物应为 Expedition");
        Assert.IsFalse(views.Exists(x => x.PersonId == pid && x.Availability == VillageAvailability.InVillage),
            "远征中人物不得同时显示为在村可用");

        InvokeBridge("ClearExpeditionPersonIds");
    }

    /// <summary>
    /// 真实产品保存路径集成验证：
    /// 玩家在领地 → VillageTime/State 已变化 → 调用真实入口 TownUI.OnReturnFromExpedition
    /// （内部 RefreshDisplay + SaveSystem.Save(_playerData)）→ 重启 Load → VillageTime/State 一致。
    /// 覆盖「玩家正常保存 / 返回领地保存」这条真实调用链确实捕获最新 Village 状态。
    /// </summary>
    [UnityTest]
    public IEnumerator VillageSave_RealPath_TownReturn_CapturesLatestVillage()
    {
        VillageRuntimeBridge_ResetForTests();
        DeleteSaveFile();

        SceneManager.LoadScene("SampleScene");
        yield return null;
        yield return null;

        var town = FindInstance("TownUI");
        Assert.IsNotNull(town, "场景应有 TownUI");
        ((GameObject)GetProperty(town, "gameObject")).SetActive(true);
        yield return null;
        yield return null;

        // 设定已知 Village 状态（模拟时间已推进 / 状态已变化）
        var time = (VillageTime)GetBridgeField("Time");
        Assert.IsNotNull(time, "领地激活后应存在 VillageTime");
        var intradaySetup1 = time.ToSaveData();
        intradaySetup1.IntradayTime = null;
        intradaySetup1.Year = 1;
        intradaySetup1.Month = 3;
        intradaySetup1.Day = 10;
        intradaySetup1.Speed = (int)(VillageTime.VillageTimeSpeed.Normal);
        intradaySetup1.SecondsIntoDay = (1.5f) * VillageTime.LegacySecondsPerDay / time.SecondsPerDay;
        time.Restore(intradaySetup1);

        var state = (VillageState)GetBridgeField("State");
        state.TestSurvivalValue = 25;

        // 真实产品入口：TownUI.OnReturnFromExpedition → RefreshDisplay + SaveSystem.Save(_playerData)
        var m = town.GetType().GetMethod("OnReturnFromExpedition",
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(m, "OnReturnFromExpedition 应存在");
        m.Invoke(town, null);
        yield return null;

        // 模拟重启：清空运行时后 Load
        VillageRuntimeBridge_ResetForTests();
        object loaded = InvokeStatic(SaveSystemType, "Load");
        Assert.IsNotNull(loaded, "真实保存路径的存档应可加载");

        var t2 = (VillageTime)GetBridgeField("Time");
        var s2 = (VillageState)GetBridgeField("State");
        Assert.IsNotNull(t2, "Load 后应恢复 VillageTime");
        Assert.IsNotNull(s2, "Load 后应恢复 VillageState");
        Assert.AreEqual(1, t2.Year);
        Assert.AreEqual(3, t2.Month);
        Assert.AreEqual(10, t2.Day, "真实保存路径应捕获最新 Day");
        Assert.AreEqual(1.5f, t2.SecondsIntoDay, "真实保存路径应捕获最新 SecondsIntoDay");
        Assert.AreEqual(25, s2.TestSurvivalValue, "真实保存路径应捕获最新 Test State");
    }

    /// <summary>
    /// 人物状态动态行「几何可见性」验证：
    /// 防止「Hierarchy 里生成成功，但 Game View 完全看不到」的测试假阳性。
    /// 对每个 VillagePersonRow_* 断言：activeInHierarchy、带 CanvasRenderer（渲染必需）、
    /// 尺寸 &gt; 0、TMP_Text 非空且 alpha &gt; 0 且字体非空、可视 Rect 与 VillagePersonList 可视 Rect 相交。
    /// </summary>
    [UnityTest]
    public IEnumerator VillagePersonRows_AreVisibleInGameView()
    {
        VillageRuntimeBridge_ResetForTests();
        DeleteSaveFile();

        SceneManager.LoadScene("SampleScene");
        yield return null;
        yield return null;

        var town = FindInstance("TownUI");
        Assert.IsNotNull(town, "场景应有 TownUI");
        EnterTownViaMainMenu();
        yield return null;
        yield return null;
        // This test protects the debug/compatibility list, not the production Home HUD.
        InvokeInstance(FindInstance("HomeProductShell"), "ToggleCompatibility");
        yield return null; yield return null;

        var hud = FindInstance("VillageHUD");
        Assert.IsNotNull(hud, "场景应有 VillageHUD");
        var hudGo = (GameObject)GetProperty(hud, "gameObject");
        var list = hudGo.transform.Find("VillagePersonList");
        Assert.IsNotNull(list, "VillagePersonList 容器应存在");
        var containerRt = list as RectTransform;
        Assert.IsNotNull(containerRt, "VillagePersonList 应为 RectTransform");

        var rows = new List<GameObject>();
        foreach (Transform child in list)
        {
            if (child.name.StartsWith("VillagePersonRow_")) rows.Add(child.gameObject);
        }
        Assert.Greater(rows.Count, 0, "应生成人物行（防止空列表假阳性）");

        Rect containerRect = WorldRectOf(containerRt);
        Assert.IsTrue(containerRect.width > 0f && containerRect.height > 0f, "容器应有非零可视尺寸");

        foreach (var row in rows)
        {
            Assert.IsTrue(row.activeInHierarchy, "行应 activeInHierarchy：" + row.name);
            Assert.IsNotNull(row.GetComponent<CanvasRenderer>(),
                "行必须带 CanvasRenderer 才能被 Canvas 渲染：" + row.name);

            var rt = row.GetComponent<RectTransform>();
            Assert.IsNotNull(rt, "行应有 RectTransform：" + row.name);
            Assert.Greater(rt.sizeDelta.x, 0f, "行宽度应 > 0：" + row.name);
            Assert.Greater(rt.sizeDelta.y, 0f, "行高度应 > 0：" + row.name);

            var text = row.GetComponent<TMP_Text>();
            Assert.IsNotNull(text, "行应有 TMP_Text：" + row.name);
            Assert.IsFalse(string.IsNullOrEmpty(text.text), "行文本不应为空：" + row.name);
            Assert.Greater(text.color.a, 0f, "行文本 alpha 应 > 0：" + row.name);
            Assert.IsNotNull(text.font, "行文本应有字体（缺失则不可渲染）：" + row.name);

            Rect rowRect = WorldRectOf(rt);
            Assert.IsTrue(rowRect.Overlaps(containerRect),
                "行可视 Rect 应与容器可视 Rect 相交（行被放到容器外）：" + row.name);
        }
    }

    private static Rect WorldRectOf(RectTransform rt)
    {
        var corners = new Vector3[4];
        rt.GetWorldCorners(corners);
        float minX = Mathf.Min(corners[0].x, corners[1].x, corners[2].x, corners[3].x);
        float maxX = Mathf.Max(corners[0].x, corners[1].x, corners[2].x, corners[3].x);
        float minY = Mathf.Min(corners[0].y, corners[1].y, corners[2].y, corners[3].y);
        float maxY = Mathf.Max(corners[0].y, corners[1].y, corners[2].y, corners[3].y);
        return new Rect(minX, minY, maxX - minX, maxY - minY);
    }

    [TearDown]
    public void TearDown()
    {
        VillageRuntimeBridge_ResetForTests();
        DeleteSaveFile();
    }

    private static void DeleteSaveFile()
    {
        if (SaveSystemType == null) return;
        string path = (string)InvokeStatic(SaveSystemType, "GetSavePath");
        if (!string.IsNullOrEmpty(path) && System.IO.File.Exists(path))
            System.IO.File.Delete(path);
    }

    private static void VillageRuntimeBridge_ResetForTests()
    {
        if (BridgeType != null)
        {
            InvokeBridge("ResetForTests");
        }
    }
}
