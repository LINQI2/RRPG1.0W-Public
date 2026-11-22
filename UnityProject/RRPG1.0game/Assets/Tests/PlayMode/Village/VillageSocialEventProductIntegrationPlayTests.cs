using System.Collections;
using System.Reflection;
using NUnit.Framework;
using RRPG.Event;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

/// <summary>
/// **Village Event 产品路径集成测试（Unified Event Core 版）** —— WP-Event-1D Gate D1 §28。
///
/// 迁移说明（FDS §21.6.1「B 必须迁移」）：原 V3B「Legacy Pending + 两个硬编码按钮」的断言已迁移为
/// **Unified Event 契约**：真实调度制造第 3 次 Temporary Commitment → `EventRuntimeBridge.PendingVillageEvent`
/// → 事件 UI 从配置渲染标题 / 正文 / 选项 → 真实点击配置驱动选项 → Option → Outcome → Effect Transaction
///（GrantRest 写正式 Rest Authority；Continue 无效果）→ Rest Gate 的后续调度差异 + exactly-once。
/// 真实入口：MainMenuUI.OnContinue → TownUI。团结引擎限制：Assembly-CSharp 经反射；Village / Event Runtime 强类型。
/// </summary>
public class VillageSocialEventProductIntegrationPlayTests
{
    private const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
    private const BindingFlags IF = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

    private static System.Type BridgeType => System.Type.GetType("VillageRuntimeBridge, Assembly-CSharp");
    private static System.Type SaveSystemType => System.Type.GetType("SaveSystem, Assembly-CSharp");
    private static System.Type EventUiType => System.Type.GetType("VillageSocialEventUI, Assembly-CSharp");
    private static System.Type EventBridgeType => System.Type.GetType("EventRuntimeBridge, Assembly-CSharp");

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

    // ==================== helpers（反射 / 强类型混合） ====================

    private static void ResetBridge()
    {
        if (BridgeType != null) InvokeStatic(BridgeType, "ResetForTests");
        if (EventBridgeType != null) InvokeStatic(EventBridgeType, "ResetForTests");
        var flowType = System.Type.GetType("VillageEventFlow, Assembly-CSharp");
        if (flowType != null) InvokeStatic(flowType, "ResetSessionState");
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
        Assert.IsNotNull(selected, "方法不存在或参数不匹配：" + method + " @ " + type.FullName);
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

    private static object InvokeInstance(object instance, string method, params object[] args)
    {
        var m = instance.GetType().GetMethod(method, IF);
        Assert.IsNotNull(m, "实例方法不存在：" + method + " @ " + instance.GetType().Name);
        return m.Invoke(instance, args);
    }

    private static object GetInstanceField(object instance, string name)
    {
        var f = instance.GetType().GetField(name, IF);
        Assert.IsNotNull(f, "实例字段不存在：" + name);
        return f.GetValue(instance);
    }

    private static object GetProperty(object instance, string name)
    {
        var p = instance.GetType().GetProperty(name, IF);
        Assert.IsNotNull(p, "属性不存在：" + name);
        return p.GetValue(instance, null);
    }

    private static object EnterTown()
    {
        var town = FindInstance("TownUI");
        Assert.IsNotNull(town, "场景应有 TownUI");
        var menu = FindInstance("MainMenuUI");
        Assert.IsNotNull(menu, "场景应有 MainMenuUI");

        // 真实产品入口：Main Menu 前台 → OnContinue → EnterTown → TownUI.Initialize → SetSimulationActive(true)
        if (!(bool)InvokeStatic(SaveSystemType, "HasSave"))
        {
            var fresh = InvokeStatic(SaveSystemType, "NewGame");
            Assert.IsNotNull(fresh, "进入领地前置：应能创建新档数据");
            Assert.IsTrue((bool)InvokeStatic(SaveSystemType, "Save", fresh), "进入领地前置：新档应能写入真实存档");
        }
        InvokeInstance(menu, "OnContinue");
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

    private static VillageTime Time()
    {
        return (VillageTime)GetBridgeField("Time");
    }

    private static void TickDays(int days)
    {
        var t = Time();
        for (int i = 0; i < days; i++) t.TickDay();
    }

    private static object StartProject(string defId, string[] participants)
    {
        return InvokeStatic(BridgeType, "TryStartProject", defId, participants);
    }

    private static void StartAndComplete(string defId, string personId)
    {
        Assert.AreEqual(VillageProjectResult.Success, StartProject(defId, new[] { personId }));
        TickDays(defId == "prj_test_02" ? 3 : 10);
        Assert.AreEqual(0, ((VillageProjectSystem)GetBridgeField("Projects")).ActiveProjects.Count);
    }

    /// <summary>真实调度制造第 3 次 Temporary Commitment（post-add Recognition → Unified Event Pending）。</summary>
    private static void DraftThirdTimes(string personId)
    {
        StartAndComplete("prj_test_01", personId);
        StartAndComplete("prj_test_01", personId);
        Assert.AreEqual(VillageProjectResult.Success,
            (VillageProjectResult)InvokeStatic(BridgeType, "TryStartProject", "prj_test_01", new[] { personId }),
            "第 3 次真实抽调应成功");
    }

    private static ResolvedEventInstance EventPending()
    {
        var property = EventBridgeType.GetProperty("PendingVillageEvent", F);
        Assert.IsNotNull(property, "EventRuntimeBridge.PendingVillageEvent 属性不存在");
        return (ResolvedEventInstance)property.GetValue(null);
    }

    private static VillageSocialResponseLog LegacyLog()
    {
        return (VillageSocialResponseLog)GetBridgeField("SocialResponse");
    }

    private static object FindEventUi()
    {
        var ui = FindInstance("VillageSocialEventUI");
        Assert.IsNotNull(ui, "场景应有 VillageSocialEventUI（Editor Authoring 构建）");
        return ui;
    }

    [UnityTest]
    public IEnumerator EventUI_RealTown_GrantRestOption_RestBlocksFutureDraft_UntilNextMonth()
    {
        SceneManager.LoadScene("SampleScene");
        yield return null;
        yield return null;
        var town = EnterTown();
        yield return null;
        yield return null;

        string pid = FirstPersonId(town);
        DraftThirdTimes(pid);

        var pending = EventPending();
        Assert.IsNotNull(pending, "第 3 次真实加入后应建立 Unified Event Pending");
        Assert.AreEqual("evt_test_village_temp_draft", pending.EventId);
        Assert.IsFalse(LegacyLog().HasPendingResponse, "正式路径不得创建 Legacy Pending");

        // 事件 UI 自动弹出（真实轮询）
        yield return new WaitForSeconds(0.7f);
        var ui = FindEventUi();
        var root = (GameObject)GetInstanceField(ui, "root");
        Assert.IsTrue(root.activeInHierarchy, "有 Event Pending 时事件 UI 应自动弹出");

        // 真实点击配置驱动的 GrantRest 选项
        InvokeInstance(ui, "OnEventOptionClicked", "opt_test_village_grant_rest");
        Assert.IsNull(EventPending(), "结算后 Pending 清除");
        Assert.IsTrue((bool)InvokeStatic(BridgeType, "IsPersonInRestWindow", pid), "GrantRest 写入正式 Rest 权威");
        Assert.AreEqual(1, ((VillageProjectSystem)GetBridgeField("Projects")).ActiveProjects.Count,
            "post-add：当前工程继续（不回滚）");

        // 完成当前工程 → 月末当天仍被 Rest Gate 拦截新 Project
        InvokeInstance(ui, "OnCloseClicked");
        InvokeStatic(BridgeType, "PumpWorldAdvance");
        TickDays(10);
        Assert.AreEqual(0, ((VillageProjectSystem)GetBridgeField("Projects")).ActiveProjects.Count);
        Assert.AreEqual(31, Time().Day, "应处于本月最后一天");
        Assert.IsTrue((bool)InvokeStatic(BridgeType, "IsPersonInRestWindow", pid), "月末当天仍拦截");
        Assert.AreEqual(VillageProjectResult.PersonInRestWindow,
            (VillageProjectResult)InvokeStatic(BridgeType, "TryStartProject", "prj_test_01", new[] { pid }));

        // 下月 1 日恢复
        TickDays(1);
        Assert.IsFalse((bool)InvokeStatic(BridgeType, "IsPersonInRestWindow", pid), "下月 1 日恢复");
        Assert.AreEqual(VillageProjectResult.Success,
            (VillageProjectResult)InvokeStatic(BridgeType, "TryStartProject", "prj_test_01", new[] { pid }),
            "恢复后真实调度可再次加入");
    }

    [UnityTest]
    public IEnumerator EventUI_RealTown_ContinueOption_NoRest_FutureDraftAllowed()
    {
        SceneManager.LoadScene("SampleScene");
        yield return null;
        yield return null;
        var town = EnterTown();
        yield return null;
        yield return null;

        string pid = FirstPersonId(town);
        DraftThirdTimes(pid);
        Assert.IsNotNull(EventPending());

        yield return new WaitForSeconds(0.7f);
        var ui = FindEventUi();
        var root = (GameObject)GetInstanceField(ui, "root");
        Assert.IsTrue(root.activeInHierarchy, "事件 UI 应弹出（Continue 场景）");

        // 真实点击「继续让他承担」（无效果选项）
        InvokeInstance(ui, "OnEventOptionClicked", "opt_test_village_continue");
        Assert.IsNull(EventPending());
        Assert.IsFalse((bool)InvokeStatic(BridgeType, "IsPersonInRestWindow", pid), "Continue 不产生 Rest Gate");
        Assert.AreEqual(0, LegacyLog().Rests.Count, "新路径不得写 Legacy Rests");
        Assert.AreEqual(1, ((VillageProjectSystem)GetBridgeField("Projects")).ActiveProjects.Count,
            "当前工程继续（不回滚）");

        // 完成后可继续加入（无隐藏处罚）
        InvokeInstance(ui, "OnCloseClicked");
        InvokeStatic(BridgeType, "PumpWorldAdvance");
        TickDays(10);
        Assert.AreEqual(0, ((VillageProjectSystem)GetBridgeField("Projects")).ActiveProjects.Count);
        Assert.AreEqual(VillageProjectResult.Success,
            (VillageProjectResult)InvokeStatic(BridgeType, "TryStartProject", "prj_test_01", new[] { pid }),
            "Continue 后仍可正常加入新 Project");
        Assert.IsNull(EventPending(), "已结算实例不得再次弹出（exactly-once）");
    }
}
