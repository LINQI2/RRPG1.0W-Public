using TMPro;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using RRPG.Event;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

/// <summary>
/// 【Long-term Regression】Village Unified Event 全链端到端最小代表测试
///（真实 SampleScene + 真实 VillageSocialEventUI / VillageCommitmentUI）。
///
/// 迁移说明（WP-Event-1D Gate D1 · FDS §21.6.1「B 必须迁移」）：原 V3C 链走 Legacy `ResolveSocialResponse`
/// 的两按钮，现已迁移为 Unified Event 契约：
///   1) 真实调度制造 Case A 第 3 次事实 → 2) `EventRuntimeBridge.PendingVillageEvent` → 3) Event 配置驱动的
///   PauseOnOpen 行为 → 4) 真实点击配置驱动选项（GrantRest）→ 5) 承诺面板只读回看可见（恢复窗口来自正式
///   Rest Authority）→ 6) Save / Load → 7) 事件不回退、History 保留、Rest 后果仍影响后续 Project 调度。
/// 团结引擎限制：Assembly-CSharp 经反射；Village.Runtime / Event.Runtime 强类型。
/// </summary>
public class VillageV3CProductChainPlayTests
{
    private const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
    private const BindingFlags IF = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

    private static System.Type BridgeType => System.Type.GetType("VillageRuntimeBridge, Assembly-CSharp");
    private static System.Type SaveSystemType => System.Type.GetType("SaveSystem, Assembly-CSharp");
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
            var id = chars[0].GetType().GetField("PersonId").GetValue(chars[0]) as string;
            if (!string.IsNullOrEmpty(id)) return id;
        }
        var heroes = pc.GetType().GetField("OwnedHeroes").GetValue(pc) as System.Collections.IList;
        if (heroes != null && heroes.Count > 0)
        {
            var id = heroes[0].GetType().GetField("PersonId").GetValue(heroes[0]) as string;
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

    private static void StartAndComplete(string defId, string personId)
    {
        Assert.AreEqual(VillageProjectResult.Success,
            (VillageProjectResult)InvokeStatic(BridgeType, "TryStartProject", defId, new[] { personId }));
        TickDays(defId == "prj_test_02" ? 3 : 10);
    }

    /// <summary>真实调度制造 Case A 第 3 次 Temporary Commitment（post-add Recognition → Unified Event Pending）。</summary>
    private static void DraftThirdTimes(string personId)
    {
        StartAndComplete("prj_test_01", personId);
        StartAndComplete("prj_test_01", personId);
        Assert.AreEqual(VillageProjectResult.Success,
            (VillageProjectResult)InvokeStatic(BridgeType, "TryStartProject", "prj_test_01", new[] { personId }),
            "第 3 次真实抽调应成功（post-add）");
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

    private static EventWorldState WorldState()
    {
        return (EventWorldState)EventBridgeType.GetProperty("World", F).GetValue(null);
    }

    private static object FindEventUi()
    {
        var ui = FindInstance("VillageSocialEventUI");
        Assert.IsNotNull(ui, "场景应有 VillageSocialEventUI（Editor Authoring 构建）");
        return ui;
    }

    /// <summary>把容器下所有 TMP_Text 拼成一段，便于断言“玩家能看到什么”。</summary>
    private static string JoinTexts(RectTransform container)
    {
        var sb = new System.Text.StringBuilder();
        foreach (var t in container.GetComponentsInChildren<TMP_Text>(true))
        {
            if (t != null && !string.IsNullOrEmpty(t.text)) sb.Append(t.text).Append('\n');
        }
        return sb.ToString();
    }

    /// <summary>容器下是否存在指定名字的子对象（用于判定 UI 是否重画为「空历史」占位行）。</summary>
    private static bool HasChildNamed(RectTransform container, string name)
    {
        foreach (var t in container.GetComponentsInChildren<Transform>(true))
        {
            if (t != null && t.name == name) return true;
        }
        return false;
    }

    // ==================== 主链：PauseOnOpen + 历史回看 + Rest + Save/Load ====================

    [UnityTest]
    public IEnumerator ProductChain_EventPauseOnOpen_RestVisible_HistoryVisible_SaveLoad()
    {
        SceneManager.LoadScene("SampleScene");
        yield return null;
        yield return null;
        var town = EnterTown();
        yield return null;
        yield return null;

        // 1) 真实调度制造 Case A 第 3 次 Temporary Commitment 事实
        string pid = FirstPersonId(town);
        DraftThirdTimes(pid);

        // 2) Unified Event Pending 建立（UI 轮询后自动弹出）
        var pending = EventPending();
        Assert.IsNotNull(pending, "第 3 次真实加入后应建立 Unified Event Pending");
        Assert.AreEqual("evt_test_village_temp_draft", pending.EventId);
        Assert.IsFalse(LegacyLog().HasPendingResponse, "正式路径不得创建 Legacy Pending");

        // 3) PauseOnOpen（Event 配置：Timely + true）对 VillageTime 的行为
        var t = Time();
        var ui = FindEventUi();
        t.SetSpeed(VillageTime.VillageTimeSpeed.Fast);
        Assert.AreNotEqual(VillageTime.VillageTimeSpeed.Paused, t.Speed, "打开事件前 VillageTime 未暂停");

        yield return new WaitForSeconds(0.7f);
        var root = (GameObject)GetInstanceField(ui, "root");
        Assert.IsTrue(root.activeInHierarchy, "有 Event Pending 时事件 UI 应自动弹出");
        Assert.AreEqual(VillageTime.VillageTimeSpeed.Paused, t.Speed,
            "Event 配置 PauseOnOpen=true + Urgency=Timely → 打开时应暂停 VillageTime");

        // 4) 真实点击配置驱动的 GrantRest 选项（Option → Outcome → Effect Transaction）
        InvokeInstance(ui, "OnEventOptionClicked", "opt_test_village_grant_rest");
        Assert.IsNull(EventPending(), "结算后 Pending 清除");
        Assert.IsTrue((bool)InvokeStatic(BridgeType, "IsPersonInRestWindow", pid),
            "GrantRest 写入正式 Rest 权威");
        Assert.AreEqual(1, WorldState().History.OccurrenceCount("evt_test_village_temp_draft"),
            "必须记录 World Event History");
        Assert.AreEqual(VillageTime.VillageTimeSpeed.Paused, t.Speed,
            "结果文本仍展示（事件未关闭）→ 保持暂停");

        // 事件关闭后按打开前的 Speed 恢复
        InvokeInstance(ui, "OnCloseClicked");
        Assert.AreEqual(VillageTime.VillageTimeSpeed.Fast, t.Speed,
            "关闭事件后应恢复打开前的 Speed（不被强制改为 Normal）");

        // 冻结世界，保证后续断言的确定性（TickDay 不受 Speed 影响）
        t.SetSpeed(VillageTime.VillageTimeSpeed.Paused);

        // 5) 打开 Commitment 面板
        var commitmentUi = FindInstance("VillageCommitmentUI");
        Assert.IsNotNull(commitmentUi, "场景应有 VillageCommitmentUI");
        InvokeInstance(commitmentUi, "Show");
        yield return null;
        yield return null;

        var historyContainer = (RectTransform)GetInstanceField(commitmentUi, "socialHistoryListContainer");
        Assert.IsNotNull(historyContainer, "承诺面板应有只读回看容器（场景或运行时兜底）");

        // 6) 只读回看可见（恢复窗口来自**正式 Rest Authority**）
        string historyText = JoinTexts(historyContainer);
        StringAssert.Contains("恢复窗口", historyText, "应能看到 GrantRest 产生的恢复窗口");
        Assert.IsFalse(HasChildNamed(historyContainer, "NoSocialHistory"), "已有回看内容时不应显示空历史占位");

        // 7) Rest 状态可见（Person 行 + 选中详情）
        var personList = (RectTransform)GetInstanceField(commitmentUi, "personListContainer");
        Assert.IsTrue(JoinTexts(personList).Contains("恢复中"), "Person 行应主动显示恢复状态");
        InvokeInstance(commitmentUi, "OnPersonClicked", pid);
        var selectedInfo = (TMP_Text)GetInstanceField(commitmentUi, "selectedInfoText");
        Assert.IsTrue(selectedInfo.text.Contains("恢复中"), "选中详情应显示恢复状态");

        // 8) Save（真实产品路径：TownUI 持有的 PlayerCollection）→ 重启 Load
        Assert.IsTrue((bool)InvokeStatic(SaveSystemType, "Save", GetProperty(town, "PlayerData")),
            "真实保存路径应成功");
        ResetBridge();
        Assert.IsNotNull(InvokeStatic(SaveSystemType, "Load"), "应可加载刚保存的存档");

        // 9) Load 后回看仍成立（正式 Rest Authority 恢复；事件已结算不再回来）
        Assert.IsNull(EventPending(), "事件已结算 → Load 后不得让 Pending 回来");
        Assert.AreEqual(1, WorldState().History.OccurrenceCount("evt_test_village_temp_draft"),
            "Event History 必须保留");

        var loadedLog = (VillageSocialResponseLog)GetBridgeField("SocialResponse");
        var loadedRest = (VillagePersonRestState)GetBridgeField("PersonRest");
        Assert.IsNotNull(loadedRest, "正式 Rest Authority 必须随存档恢复");
        var names = new Dictionary<string, string> { { pid, pid } };
        var loadedRows = VillageCommitmentView.BuildRecentSocialHistoryRows(loadedLog, loadedRest, names, 8);
        Assert.AreEqual(1, loadedRows.Count, "Load 后应仍能合成恢复窗口行（正式 Rest Authority）");
        Assert.IsNotNull(loadedRows.Find(r => r.Kind == VillageCommitmentView.SocialHistoryKind.RestWindow));

        InvokeInstance(commitmentUi, "Show");
        yield return null;
        yield return null;
        var historyContainer2 = (RectTransform)GetInstanceField(commitmentUi, "socialHistoryListContainer");
        Assert.IsNotNull(historyContainer2);
        Assert.IsFalse(HasChildNamed(historyContainer2, "NoSocialHistory"),
            "Load 后面板重画不得退化为「空历史」");
        Assert.Greater(historyContainer2.childCount, 0, "Load 后回看区应有行");
        Assert.IsTrue(JoinTexts((RectTransform)GetInstanceField(commitmentUi, "personListContainer")).Contains("恢复中"),
            "Load 后 Person 行仍应显示恢复状态");

        // 10) Load 后已选后果仍影响后续 Project 调度
        Assert.IsTrue((bool)InvokeStatic(BridgeType, "IsPersonInRestWindow", pid), "Load 后 Rest Gate 保持");
        Assert.AreEqual(VillageProjectResult.PersonInRestWindow,
            (VillageProjectResult)InvokeStatic(BridgeType, "TryStartProject", "prj_test_01", new[] { pid }),
            "恢复期结束前仍不能安排新的临时工程（已选后果持续生效）");
    }
}
