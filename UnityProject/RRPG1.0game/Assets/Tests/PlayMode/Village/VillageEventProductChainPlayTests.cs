using TMPro;
using System;
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
/// WP-Event-1D · Gate D1 Phase B（§28/§30）：**Village Unified Event 真实产品链**（PlayMode）。
///
/// 三条链（全部走真实产品入口：MainMenuUI.OnContinue → TownUI → 真实调度 → 事件 UI）：
///   1. Recognition → Event → **GrantRest**（Rest 后果 + History + Legacy 不得被写）；
///   2. Recognition → Event → **无效果 Continue**（合法 no-effect 结算）；
///   3. **Exactly-once**（同一实例第二次执行被拒绝，零 mutation）。
///
/// 真实数据报告：每条链向 PlayMode 日志输出 `EVENT_CHAIN_REPORT ...`（PersonId / DisplayName /
/// EventId / Title / Body / OptionId / OutcomeId / History / Rest），供人工核对「玩家真实看到什么」。
/// 团结引擎限制：Assembly-CSharp（Bridge / UI）经反射；Village.Runtime / Event.Runtime 强类型。
/// </summary>
public class VillageEventProductChainPlayTests
{
    private const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
    private const BindingFlags IF = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

    private const string EventId = "evt_test_village_temp_draft";
    private const string OptionGrantRest = "opt_test_village_grant_rest";
    private const string OptionContinue = "opt_test_village_continue";
    private const string OptionButtonPrefix = "EventOption_";

    private static Type BridgeType { get { return Type.GetType("VillageRuntimeBridge, Assembly-CSharp"); } }
    private static Type EventBridgeType { get { return Type.GetType("EventRuntimeBridge, Assembly-CSharp"); } }
    private static Type FlowType { get { return Type.GetType("VillageEventFlow, Assembly-CSharp"); } }
    private static Type SaveSystemType { get { return Type.GetType("SaveSystem, Assembly-CSharp"); } }

    [SetUp]
    public void SetUp()
    {
        ResetWorld();
        DeleteSaveFile();
    }

    [TearDown]
    public void TearDown()
    {
        ResetWorld();
        DeleteSaveFile();
    }

    // ==================== helpers ====================

    private static void ResetWorld()
    {
        if (BridgeType != null) InvokeStatic(BridgeType, "ResetForTests");
        if (EventBridgeType != null) InvokeStatic(EventBridgeType, "ResetForTests");
        if (FlowType != null) InvokeStatic(FlowType, "ResetSessionState");
    }

    private static void DeleteSaveFile()
    {
        if (SaveSystemType == null) return;
        string path = (string)InvokeStatic(SaveSystemType, "GetSavePath");
        if (!string.IsNullOrEmpty(path) && System.IO.File.Exists(path)) System.IO.File.Delete(path);
    }

    private static object InvokeStatic(Type type, string method, params object[] args)
    {
        var selected = FindMethod(type, method, args, true);
        Assert.IsNotNull(selected, "静态方法不存在或参数不匹配：" + method + " @ " + type.FullName);
        return selected.Invoke(null, args);
    }

    private static object InvokeInstance(object target, string method, params object[] args)
    {
        var selected = FindMethod(target.GetType(), method, args, false);
        Assert.IsNotNull(selected, "实例方法不存在或参数不匹配：" + method + " @ " + target.GetType().FullName);
        return selected.Invoke(target, args);
    }

    private static MethodInfo FindMethod(Type type, string method, object[] args, bool isStatic)
    {
        var flags = BindingFlags.Public | BindingFlags.NonPublic
            | (isStatic ? BindingFlags.Static : BindingFlags.Instance);

        foreach (var candidate in type.GetMethods(flags))
        {
            if (candidate.Name != method) continue;
            var ps = candidate.GetParameters();
            if (ps.Length != (args == null ? 0 : args.Length)) continue;

            bool ok = true;
            for (int i = 0; i < ps.Length; i++)
            {
                if (args[i] == null) continue;
                if (ps[i].ParameterType.IsAssignableFrom(args[i].GetType())) continue;
                if (ps[i].ParameterType.IsByRef
                    && ps[i].ParameterType.GetElementType().IsAssignableFrom(args[i].GetType())) continue;
                ok = false;
                break;
            }
            if (ok) return candidate;
        }
        return null;
    }

    private static object GetBridgeField(string name)
    {
        var field = BridgeType.GetField(name, F);
        Assert.IsNotNull(field, "字段不存在：" + name);
        return field.GetValue(null);
    }

    private static object GetInstanceField(object instance, string name)
    {
        var field = instance.GetType().GetField(name, IF);
        Assert.IsNotNull(field, "实例字段不存在：" + name + " @ " + instance.GetType().Name);
        return field.GetValue(instance);
    }

    private static object GetProperty(object instance, string name)
    {
        var property = instance.GetType().GetProperty(name, IF);
        Assert.IsNotNull(property, "属性不存在：" + name + " @ " + instance.GetType().Name);
        return property.GetValue(instance, null);
    }

    private static object FindInstance(string typeName)
    {
        var type = Type.GetType(typeName + ", Assembly-CSharp");
        if (type == null) return null;
        foreach (var o in UnityEngine.Object.FindObjectsOfType(type, true))
        {
            if (o != null) return o;
        }
        return null;
    }

    /// <summary>进入真实领地（真实产品入口：Main Menu 前台 → OnContinue → EnterTown → TownUI.Initialize）。</summary>
    private static object EnterTown()
    {
        var town = FindInstance("TownUI");
        Assert.IsNotNull(town, "场景应有 TownUI");
        var menu = FindInstance("MainMenuUI");
        Assert.IsNotNull(menu, "场景应有 MainMenuUI");

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
        var collection = GetProperty(town, "PlayerData");
        Assert.IsNotNull(collection, "Town 应持有真实 PlayerCollection");

        var characters = collection.GetType().GetField("OwnedCharacters").GetValue(collection) as IList;
        if (characters != null && characters.Count > 0)
        {
            var id = characters[0].GetType().GetField("PersonId").GetValue(characters[0]) as string;
            if (!string.IsNullOrEmpty(id)) return id;
        }

        Assert.Fail("PlayerCollection 应至少包含一个长期 Person");
        return null;
    }

    private static string DisplayNameOf(object town, string personId)
    {
        var collection = GetProperty(town, "PlayerData");
        var characters = collection.GetType().GetField("OwnedCharacters").GetValue(collection) as IList;
        if (characters != null)
        {
            foreach (var c in characters)
            {
                var id = c.GetType().GetField("PersonId").GetValue(c) as string;
                if (id != personId) continue;
                return c.GetType().GetField("DisplayName").GetValue(c) as string;
            }
        }
        Assert.Fail("找不到人物显示名：" + personId);
        return null;
    }

    private static VillageTime Time()
    {
        return (VillageTime)GetBridgeField("Time");
    }

    private static void TickDays(int days)
    {
        var time = Time();
        for (int i = 0; i < days; i++) time.TickDay();
    }

    private static VillageProjectResult StartProject(string defId, string personId)
    {
        return (VillageProjectResult)InvokeStatic(BridgeType, "TryStartProject", defId, new[] { personId });
    }

    private static void StartAndComplete(string defId, string personId)
    {
        Assert.AreEqual(VillageProjectResult.Success, StartProject(defId, personId), "加入新 Project 应成功：" + defId);
        TickDays(defId == "prj_test_02" ? 3 : 10);
        Assert.AreEqual(0, ((VillageProjectSystem)GetBridgeField("Projects")).ActiveProjects.Count,
            "Project 应已完成并释放参与者");
    }

    /// <summary>真实调度制造 Case A 第 3 次 Temporary Commitment（post-add Recognition）。</summary>
    private static void DraftThirdTime(string personId)
    {
        StartAndComplete("prj_test_01", personId);
        StartAndComplete("prj_test_01", personId);
        Assert.AreEqual(VillageProjectResult.Success, StartProject("prj_test_01", personId),
            "第 3 次真实抽调应成功");
        Assert.AreEqual(1, ((VillageProjectSystem)GetBridgeField("Projects")).ActiveProjects.Count,
            "post-add：当前工程继续（不回滚）");
    }

    private static object FindEventUi()
    {
        var ui = FindInstance("VillageSocialEventUI");
        Assert.IsNotNull(ui, "场景应有 VillageSocialEventUI（Editor Authoring 宿主）");
        return ui;
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

    private static bool IsRestBlocked(string personId)
    {
        return (bool)InvokeStatic(BridgeType, "IsPersonInRestWindow", personId);
    }

    private sealed class OptionButtonRow
    {
        public string OptionId;
        public string Label;
        public bool Interactable;
    }

    /// <summary>真实渲染出来的选项按钮（运行时按配置生成；名称 = `EventOption_&lt;OptionId&gt;`）。</summary>
    private static List<OptionButtonRow> OptionButtons(GameObject root)
    {
        var rows = new List<OptionButtonRow>();
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
        {
            if (t == null || !t.name.StartsWith(OptionButtonPrefix, StringComparison.Ordinal)) continue;

            var label = t.GetComponentInChildren<TMP_Text>(true);
            var button = t.GetComponent<Button>();
            rows.Add(new OptionButtonRow
            {
                OptionId = t.name.Substring(OptionButtonPrefix.Length),
                Label = label != null ? label.text : string.Empty,
                Interactable = button == null || button.interactable
            });
        }
        return rows;
    }

    private static void Report(string chain, string personId, string displayName, string eventId, string title,
        string body, string optionId, string outcomeId, int historyCount, bool restActive)
    {
        Debug.Log("EVENT_CHAIN_REPORT [" + chain + "] PersonId=" + personId
            + " | DisplayName=" + displayName
            + " | EventId=" + eventId
            + " | Title=" + title
            + " | Body=" + body
            + " | OptionId=" + optionId
            + " | OutcomeId=" + outcomeId
            + " | History=" + historyCount
            + " | RestActive=" + restActive);
    }

    // ==================== 链 1：Recognition → Event → GrantRest ====================

    [UnityTest]
    public IEnumerator EventChain1_Recognition_GrantRest_PendingRestAndHistory()
    {
        SceneManager.LoadScene("SampleScene");
        yield return null;
        yield return null;
        var town = EnterTown();
        yield return null;
        yield return null;

        string pid = FirstPersonId(town);
        string displayName = DisplayNameOf(town, pid);

        // ① 真实调度：第 3 次抽调（post-add Recognition → Unified Event Pending）
        DraftThirdTime(pid);
        var pending = EventPending();
        Assert.IsNotNull(pending, "第 3 次真实抽调应建立 Unified Event Pending");
        Assert.AreEqual(EventId, pending.EventId);
        Assert.IsFalse(LegacyLog().HasPendingResponse, "正式路径不得创建 Legacy Pending");
        Assert.AreEqual(0, LegacyLog().Fired.Count, "正式路径不得写 Legacy fired");

        // ② 事件 UI 自动弹出（真实轮询）+ 文案来自 Event 配置
        yield return new WaitForSeconds(0.7f);
        var ui = FindEventUi();
        var root = (GameObject)GetInstanceField(ui, "root");
        Assert.IsTrue(root.activeInHierarchy, "有 Event Pending 时事件 UI 应自动弹出");

        string title = ((TMP_Text)GetInstanceField(ui, "titleText")).text;
        string body = ((TMP_Text)GetInstanceField(ui, "bodyText")).text;
        Assert.AreEqual("村庄里有人私下提起最近的安排", title, "标题必须来自 EventPresentation 配置");
        Assert.AreEqual(displayName + " 已经连续多次承担临时任务，想谈谈接下来的节奏。", body,
            "正文必须由配置模板 + 真实人物显示名渲染");

        // ③ 选项来自配置（不是硬编码两个产品选项）
        var buttons = OptionButtons(root);
        Assert.AreEqual(2, buttons.Count, "应有两个配置驱动选项");
        Assert.AreEqual(OptionGrantRest, buttons[0].OptionId);
        Assert.AreEqual("先给他一段恢复时间", buttons[0].Label);
        Assert.IsTrue(buttons[0].Interactable);
        Assert.AreEqual(OptionContinue, buttons[1].OptionId);
        Assert.AreEqual("继续让他承担", buttons[1].Label);

        // ④ 真实点击 GrantRest 选项 → Option → Outcome → Effect Transaction
        InvokeInstance(ui, "OnEventOptionClicked", OptionGrantRest);

        Assert.IsNull(EventPending(), "成功结算后 Pending 必须清除");
        Assert.IsTrue(IsRestBlocked(pid), "GrantPersonRest 必须真实写入正式 Rest Authority");
        Assert.AreEqual(0, LegacyLog().Rests.Count, "新路径不得写 Legacy Rests");
        Assert.AreEqual(1, WorldState().History.OccurrenceCount(EventId), "必须记录 World Event History");

        string resultBody = ((TMP_Text)GetInstanceField(ui, "bodyText")).text;
        StringAssert.Contains("已记录你的选择", resultBody, "结果模式应显示可理解的结果");

        Report("Chain1-GrantRest", pid, displayName, EventId, title, body, OptionGrantRest,
            "out_test_village_grant_rest", WorldState().History.OccurrenceCount(EventId), IsRestBlocked(pid));

        // ⑤ 真实按钮关闭 → 面板收起
        InvokeInstance(ui, "OnCloseClicked");
        yield return null;
        Assert.IsFalse(root.activeInHierarchy, "关闭后事件面板应收起");
    }

    // ==================== 链 2：Recognition → Event → 无效果 Continue ====================

    [UnityTest]
    public IEnumerator EventChain2_Recognition_NoEffectContinue_RecordsHistoryWithoutConsequence()
    {
        SceneManager.LoadScene("SampleScene");
        yield return null;
        yield return null;
        var town = EnterTown();
        yield return null;
        yield return null;

        string pid = FirstPersonId(town);
        string displayName = DisplayNameOf(town, pid);

        DraftThirdTime(pid);
        Assert.IsNotNull(EventPending(), "第 3 次真实抽调应建立 Event Pending");

        yield return new WaitForSeconds(0.7f);
        var ui = FindEventUi();
        var root = (GameObject)GetInstanceField(ui, "root");
        Assert.IsTrue(root.activeInHierarchy, "事件 UI 应自动弹出");

        string body = ((TMP_Text)GetInstanceField(ui, "bodyText")).text;

        // 真实点击「继续让他承担」（该选项未配置任何效果 → 合法的 no-effect 结算）
        InvokeInstance(ui, "OnEventOptionClicked", OptionContinue);

        Assert.IsNull(EventPending(), "成功结算后 Pending 必须清除");
        Assert.IsFalse(IsRestBlocked(pid), "no-effect 选项不得产生 Rest");
        Assert.AreEqual(0, LegacyLog().Rests.Count, "Legacy 侧不得被写入");
        Assert.AreEqual(1, WorldState().History.OccurrenceCount(EventId), "必须记录 World Event History");

        string resultBody = ((TMP_Text)GetInstanceField(ui, "bodyText")).text;
        StringAssert.Contains("已记录你的选择", resultBody);
        StringAssert.Contains("本次应用效果 0 条", resultBody, "no-effect 结算必须如实反映「没有世界变化」");

        Report("Chain2-NoEffectContinue", pid, displayName, EventId, "", body, OptionContinue,
            "out_test_village_continue", WorldState().History.OccurrenceCount(EventId), IsRestBlocked(pid));
    }

    // ==================== 链 3：Exactly-once ====================

    [UnityTest]
    public IEnumerator EventChain3_ExactlyOnce_SecondExecutionRejectedWithoutMutation()
    {
        SceneManager.LoadScene("SampleScene");
        yield return null;
        yield return null;
        var town = EnterTown();
        yield return null;
        yield return null;

        string pid = FirstPersonId(town);
        DraftThirdTime(pid);

        var instance = EventPending();
        Assert.IsNotNull(instance, "第 3 次真实抽调应建立 Event Pending");

        yield return new WaitForSeconds(0.7f);
        var ui = FindEventUi();
        InvokeInstance(ui, "OnEventOptionClicked", OptionContinue);

        Assert.IsNull(EventPending(), "第一次结算应清除 Pending");
        int historyAfterFirst = WorldState().History.OccurrenceCount(EventId);
        Assert.AreEqual(1, historyAfterFirst);

        // 绕过 UI 对**同一实例**第二次执行 → exactly-once 拒绝（零 mutation）
        object[] args = { instance, OptionGrantRest, null };
        bool ok = (bool)InvokeStatic(FlowType, "TryExecuteOption", args);
        var result = (EventOptionExecutionResult)args[2];

        Assert.IsFalse(ok, "同一实例第二次执行必须被拒绝");
        Assert.AreEqual(EventExecutionCode.InstanceAlreadyResolved, result.Code);
        Assert.AreEqual(historyAfterFirst, WorldState().History.OccurrenceCount(EventId), "History 不得重复计数");
        Assert.IsFalse(IsRestBlocked(pid), "被拒绝的执行不得产生任何 mutation");

        // UI 侧再次点击也不得建立新事件 / 新结算
        InvokeInstance(ui, "OnEventOptionClicked", OptionContinue);
        Assert.IsNull(EventPending());
        Assert.AreEqual(historyAfterFirst, WorldState().History.OccurrenceCount(EventId));

        Report("Chain3-ExactlyOnce", pid, DisplayNameOf(town, pid), EventId, "", "", OptionContinue,
            result.OutcomeId, historyAfterFirst, IsRestBlocked(pid));
    }
}