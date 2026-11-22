using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

/// <summary>
/// Minimum Village World Integration WP1 —— Runtime Foundation / Technical Proof 测试
/// （真实 SampleScene + VillageWorldLayer Debug Visualization；Village.Runtime 强类型 + Assembly-CSharp 反射）。
///
/// 覆盖（WP1 技术验收范围）：
///   - Person Presence 正确派生 + Debug 视图只随权威 Simulation 显示（Main Menu 前台阶段不显示）；
///   - Runtime truth / Presence / Place 与 UI 面板生命周期解耦（sim 关停只停视图、状态不失效不重建）；
///   - Project → Persistent World Change 数据链 + 真实 Save / Reload 后痕迹仍在。
///
/// VillageWorldLayer 仅为 **Debug Visualization / Test / Not Frozen**，不代表产品 World；
/// 本测试不断言任何 Spatial Presence / Place Memory / 产品 World 体验。
/// </summary>
public class VillageWorldProductIntegrationPlayTests
{
    private const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
    private const BindingFlags IF = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

    private static System.Type BridgeType => System.Type.GetType("VillageRuntimeBridge, Assembly-CSharp");
    private static System.Type SaveSystemType => System.Type.GetType("SaveSystem, Assembly-CSharp");
    private static System.Type WorldLayerType => System.Type.GetType("VillageWorldLayer, Assembly-CSharp");
    private static System.Type TownUiType => System.Type.GetType("TownUI, Assembly-CSharp");

    [SetUp]
    public void Reset()
    {
        ResetBridge();
        DeleteSaveFile();
    }

    [TearDown]
    public void Cleanup()
    {
        ResetBridge();
        DeleteSaveFile();
    }

    private static void ResetBridge()
    {
        if (BridgeType != null) InvokeStatic(BridgeType, "ResetForTests");
    }

    private static void DeleteSaveFile()
    {
        if (SaveSystemType == null) return;
        string path = (string)InvokeStatic(SaveSystemType, "GetSavePath");
        if (!string.IsNullOrEmpty(path) && System.IO.File.Exists(path)) System.IO.File.Delete(path);
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
        Assert.IsNotNull(selected, "静态方法不存在或参数不匹配：" + method + " @ " + type.FullName);
        return selected.Invoke(null, args);
    }

    private static object InvokeInstance(object instance, string method, params object[] args)
    {
        var m = instance.GetType().GetMethod(method, IF);
        Assert.IsNotNull(m, "实例方法不存在：" + method + " @ " + instance.GetType().Name);
        return m.Invoke(instance, args);
    }

    private static object GetBridgeField(string name)
    {
        var f = BridgeType.GetField(name, F);
        Assert.IsNotNull(f, "Bridge 字段不存在：" + name);
        return f.GetValue(null);
    }

    private static object GetProperty(object instance, string name)
    {
        var p = instance.GetType().GetProperty(name, IF);
        Assert.IsNotNull(p, "属性不存在：" + name + " @ " + instance.GetType().Name);
        return p.GetValue(instance, null);
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

    private static object EnterTown()
    {
        var town = FindInstance("TownUI");
        Assert.IsNotNull(town, "场景应有 TownUI");
        var mm = FindInstance("MainMenuUI");
        Assert.IsNotNull(mm, "场景应有 MainMenuUI");

        // 进入领地必须走真实产品入口：Main Menu 前台（sim=false 门禁）→ 玩家点击主菜单按钮 →
        // MainMenuUI.EnterTown(collection) → TownUI.Initialize → SetSimulationActive(true)。
        // 不得再用 SetActive(true) 依赖修复前「TownUI.Start 提前把 sim 置 true、MainMenu 不再复位」的旧行为。
        if (!(bool)InvokeStatic(SaveSystemType, "HasSave"))
        {
            var fresh = InvokeStatic(SaveSystemType, "NewGame");
            Assert.IsNotNull(fresh, "进入领地前置：应能创建新档数据");
            Assert.IsTrue((bool)InvokeStatic(SaveSystemType, "Save", fresh), "进入领地前置：新档应能写入真实存档");
        }
        InvokeInstance(mm, "OnContinue"); // 产品路径：继续游戏 → 读档 → EnterTown → Initialize
        return FindInstance("TownUI");
    }

    private static string FirstPersonId(object town)
    {
        var pc = GetProperty(town, "PlayerData");
        Assert.IsNotNull(pc, "Town 应持有真实 PlayerCollection");
        var chars = pc.GetType().GetField("OwnedCharacters").GetValue(pc) as System.Collections.IList;
        if (chars != null && chars.Count > 0)
        {
            var first = chars[0];
            // M1：人物长期实例身份 = PersonId（不再把模板 CharacterId 当 PersonId）
            var id = first.GetType().GetField("PersonId").GetValue(first) as string;
            if (!string.IsNullOrEmpty(id)) return id;
        }
        var heroes = pc.GetType().GetField("OwnedHeroes").GetValue(pc) as System.Collections.IList;
        if (heroes != null && heroes.Count > 0)
        {
            var first = heroes[0];
            var id = first.GetType().GetField("PersonId").GetValue(first) as string;
            if (!string.IsNullOrEmpty(id)) return id;
        }
        Assert.Fail("PlayerCollection 应至少包含一个长期 Person");
        return null;
    }

    private static VillageTime VillageTime()
    {
        return (VillageTime)GetBridgeField("Time");
    }

    private static void TickDays(int days)
    {
        var t = VillageTime();
        for (int i = 0; i < days; i++) t.TickDay();
    }

    private static VillagePersonPresence PresenceOf(string personId)
    {
        return (VillagePersonPresence)InvokeStatic(BridgeType, "GetPersonPresence", personId);
    }

    [UnityTest]
    public IEnumerator Presence_DerivedAndWorldVisible_WithoutAnyManagementPanel_And_SurvivesSimToggle()
    {
        SceneManager.LoadScene("SampleScene");
        yield return null;
        yield return null;
        var town = EnterTown();
        yield return null;
        yield return null;

        string pid = FirstPersonId(town);
        Assert.AreEqual(VillageAssignmentResult.Success,
            InvokeStatic(BridgeType, "TryAssignPerson", pid, "ws_test_01"), "真实调度 API 指派 Primary Assignment");

        var presence = PresenceOf(pid);
        Assert.AreEqual(VillagePresenceState.Workplace, presence.State, "Presence 只从 Assignment 真相源派生");
        Assert.AreEqual("ws_test_01", presence.PlaceId);

        // Debug 视图只随权威 Simulation 显示（未打开任何管理面板）：用于开发观察 Presence 数据链
        yield return new WaitForSeconds(0.7f);
        var layer = FindInstance("VillageWorldLayer");
        Assert.IsNotNull(layer, "场景运行时应存在 VillageWorldLayer Debug 宿主（VillageTimeDriver 运行时自托管）");
        Assert.IsTrue((bool)GetProperty(layer, "CanvasVisible"), "Simulation 激活时 World 画布应可见");
        string summary = (string)GetProperty(layer, "MarkerSummary");
        Assert.IsTrue(!string.IsNullOrEmpty(summary), "World 应已绘制 Presence / Place Marker");
        Assert.IsTrue(summary.Contains(pid), "World 摘要应包含该具体 Person");
        Assert.IsTrue(summary.Contains("ws_test_01"), "World 摘要应包含其 Workplace Place");

        // UI/Sim gating 关停（等价 DeckViewer / Expedition Preparation 的 sim 关停路径）：
        // World 视图停绘，但 Runtime 状态与 Place 注册表不变、不销毁、不重建
        int placesBefore = ((VillageWorldRegistry)GetBridgeField("World")).Places.Count;
        InvokeStatic(BridgeType, "SetSimulationActive", false);
        yield return null;
        Assert.IsFalse((bool)GetProperty(layer, "CanvasVisible"), "sim 关停 → 只停视图");
        Assert.AreEqual(placesBefore, ((VillageWorldRegistry)GetBridgeField("World")).Places.Count, "Place truth 不因 UI 关停变化");
        Assert.AreEqual(VillagePresenceState.Workplace, PresenceOf(pid).State, "Presence truth 不因 UI 关停变化");

        // 返回领地（sim 恢复）→ 同一 Runtime 状态直接恢复显示，无重建失效
        InvokeStatic(BridgeType, "SetSimulationActive", true);
        yield return new WaitForSeconds(0.7f);
        Assert.IsTrue((bool)GetProperty(layer, "CanvasVisible"), "sim 恢复 → World 视图恢复");
        string summary2 = (string)GetProperty(layer, "MarkerSummary");
        Assert.IsTrue(summary2.Contains(pid) && summary2.Contains("ws_test_01"), "恢复后仍指向同一 Person / 同一 Place");
    }

    [UnityTest]
    public IEnumerator ProjectCompletion_WorldSiteTrace_PersistsAfterSaveReload_WorldRestored()
    {
        SceneManager.LoadScene("SampleScene");
        yield return null;
        yield return null;
        var town = EnterTown();
        yield return null;
        yield return null;

        string pid = FirstPersonId(town);
        object player = GetProperty(town, "PlayerData");

        Assert.AreEqual(VillageProjectResult.Success,
            InvokeStatic(BridgeType, "TryStartProject", "prj_test_02", new[] { pid }), "真实调度启动 Test Project");

        var projects = (VillageProjectSystem)GetBridgeField("Projects");
        Assert.AreEqual(1, projects.ActiveProjects.Count);
        string projectId = projects.ActiveProjects[0].ProjectId;

        TickDays(3); // prj_test_02：1 人 × 2/天 → 3 天达 target 6 完成
        Assert.AreEqual(0, projects.ActiveProjects.Count, "Project 完成并释放参与者");

        var world = (VillageWorldRegistry)GetBridgeField("World");
        string placeId = VillageWorldRegistry.BuildProjectSitePlaceId(projectId);
        Assert.IsTrue(world.ContainsPlace(placeId), "完成点应单向追加 ProjectSite 痕迹");

        var saveData = (VillageSaveData)InvokeStatic(BridgeType, "BuildSaveData");
        Assert.IsNotNull(saveData.World, "存档应含 World 区块");
        Assert.IsTrue(saveData.World.Places != null && saveData.World.Places.Count == 1, "只持久化 ProjectSite（Workplace 派生不落档）");
        Assert.AreEqual(placeId, saveData.World.Places[0].PlaceId);

        Assert.IsTrue((bool)InvokeStatic(SaveSystemType, "Save", player), "真实存档路径写入");

        // 全量重载：Reset 运行时 → 重新加载真实场景 → Town 从存档恢复 Village 区块
        ResetBridge();
        SceneManager.LoadScene("SampleScene");
        yield return null;
        yield return null;
        town = EnterTown();
        yield return null;
        yield return null;

        var world2 = (VillageWorldRegistry)GetBridgeField("World");
        Assert.IsTrue(world2.ContainsPlace(placeId), "Save / Reload 后 ProjectSite 痕迹仍在");
        Assert.AreEqual(0, ((VillageProjectSystem)GetBridgeField("Projects")).ActiveProjects.Count, "已完成 Project 不恢复为 Active");

        yield return new WaitForSeconds(0.7f);
        var layer = FindInstance("VillageWorldLayer");
        Assert.IsNotNull(layer, "重载后场景仍有 VillageWorldLayer 根宿主");
        Assert.IsTrue((bool)GetProperty(layer, "CanvasVisible"), "重载进入领地后 World 画布应可见");
        Assert.IsTrue(((string)GetProperty(layer, "MarkerSummary")).Contains(placeId), "World 应显示持久痕迹 Place");
    }

    [UnityTest]
    public IEnumerator MainMenuPhase_DebugViewHidden_UntilTownSimulationActive()
    {
        SceneManager.LoadScene("SampleScene");
        yield return null;
        yield return null;
        yield return null;

        // Main Menu 前台阶段：权威 Village Simulation 必须为 false（MainMenuUI 为外层产品门禁），
        // VillageWorldLayer Debug 视图不得显示（无论 TownUI.Start 是否先于 MainMenu.Start 执行过）
        Assert.IsFalse((bool)GetBridgeField("IsSimulationActive"), "Main Menu 阶段权威 sim 必须为 false");
        var layerAtMenu = FindInstance("VillageWorldLayer");
        bool hiddenAtMenu = layerAtMenu == null || !(bool)GetProperty(layerAtMenu, "CanvasVisible");
        Assert.IsTrue(hiddenAtMenu, "Main Menu 阶段 Village World Debug 视图不得显示");

        // 进入领地：TownUI.Initialize → 权威 sim=true → Debug 视图（仅开发观察）随之显示
        var town = EnterTown();
        yield return null;
        yield return null;
        Assert.IsTrue((bool)GetBridgeField("IsSimulationActive"), "进入领地后权威 sim=true");

        yield return new WaitForSeconds(0.7f);
        var layer = FindInstance("VillageWorldLayer");
        Assert.IsNotNull(layer, "进入领地后应存在 Debug 宿主");
        Assert.IsTrue((bool)GetProperty(layer, "CanvasVisible"), "进入领地后 Debug 视图显示");
    }
}
