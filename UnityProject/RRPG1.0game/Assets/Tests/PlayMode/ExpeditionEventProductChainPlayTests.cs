using TMPro;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

/// <summary>
/// **WP-Event-1D Gate D2 — Expedition Event 真实产品链（PlayMode）**。
///
/// 覆盖（对应 D2 Spec §22 的三条链 + §19/~§21 的产品生命周期契约）：
///   P-D2-1 Non-Battle Event：Town → Prep → Confirm → Journey → Event Node → 正式事件卡 →
///          非战斗 Option（仅额外时间）→ Result → 「继续」→ 同一 Node Success → Journey 继续；
///   P-D2-2 Supply / Time：走 `SupplyDelta + AdvanceExpeditionTime` 的 Outcome（有序语义），
///          证明 extra time 真实进入 elapsed、并按 party × day × rate 真实消耗 Supply（exactly-once）；
///   P-D2-3 StartBattle：Event 卡 → StartBattle Outcome → Preflight 生成的**同一份** EnemyRoster →
///          Prep / Battle → Victory 结算**同一个** Event Node（Defeat 用例另附）。
///
/// 同时断言的产品契约：原子进入（失败零 mutation 且无占位回退）/ 活动事件冻结复用 / 无 Placeholder Fallback。
///
/// 程序集边界：ExpeditionUI / AdventureManager / Journey View 属 Assembly-CSharp（asmdef 不可引用）→ 反射访问。
/// </summary>
public class ExpeditionEventProductChainPlayTests
{
    private const BindingFlags F =
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

    private const int InitialFood = 100;
    private const int EventKindValue = 4;      // JourneyEncounterKind.Event（D2 冻结值）
    private const int BranchKindValue = 3;     // JourneyEncounterKind.Branch
    private const int BattleKindValue = 1;     // JourneyEncounterKind.Battle

    private object _expUI;
    private object _adventure;
    private object _view;
    private object _coordinator;
    private object _pc;

    [TearDown]
    public void TearDown()
    {
        LogAssert.ignoreFailingMessages = false;
        CallStatic("VillageRuntimeBridge", "ResetForTests");
        CallStatic("EventRuntimeBridge", "ResetForTests");
    }

    // ==================== 反射工具 ====================

    private static Type Cs(string name)
    {
        var type = Type.GetType(name + ", Assembly-CSharp");
        Assert.IsNotNull(type, "类型不存在：" + name);
        return type;
    }

    private static object FindInstance(string typeName)
    {
        var type = Cs(typeName);
        foreach (var o in UnityEngine.Object.FindObjectsOfType(type, true))
            if (o != null) return o;
        return null;
    }

    private static object Call(object target, string method, params object[] args)
    {
        Assert.IsNotNull(target, "调用目标为空：" + method);
        int want = args == null ? 0 : args.Length;
        foreach (var m in target.GetType().GetMethods(F))
        {
            if (m.Name != method) continue;
            var ps = m.GetParameters();
            if (ps.Length != want) continue;

            bool ok = true;
            for (int i = 0; i < want; i++)
            {
                if (args[i] == null) continue;
                if (!ps[i].ParameterType.IsInstanceOfType(args[i])) { ok = false; break; }
            }
            if (ok) return m.Invoke(target, args);
        }

        Assert.Fail("方法不存在或参数不匹配：" + target.GetType().Name + "." + method);
        return null;
    }

    private static object CallStatic(string typeName, string method, params object[] args)
    {
        var type = Cs(typeName);
        int want = args == null ? 0 : args.Length;
        foreach (var m in type.GetMethods(F))
        {
            if (m.Name != method) continue;
            var ps = m.GetParameters();
            if (ps.Length != want) continue;

            bool ok = true;
            for (int i = 0; i < want; i++)
            {
                if (args[i] == null) continue;
                if (!ps[i].ParameterType.IsInstanceOfType(args[i])) { ok = false; break; }
            }
            if (ok) return m.Invoke(null, args);
        }

        Assert.Fail("静态方法不存在或参数不匹配：" + typeName + "." + method);
        return null;
    }

    private static object GetMember(object target, string name)
    {
        Assert.IsNotNull(target, "读取目标为空：" + name);
        var f = target.GetType().GetField(name, F);
        if (f != null) return f.GetValue(target);

        var p = target.GetType().GetProperty(name, F);
        Assert.IsNotNull(p, "成员不存在：" + name + " @ " + target.GetType().Name);
        return p.GetValue(target, null);
    }

    private static object StaticMember(string typeName, string name)
    {
        var type = Cs(typeName);
        var f = type.GetField(name, F);
        if (f != null) return f.GetValue(null);
        var p = type.GetProperty(name, F);
        Assert.IsNotNull(p, "静态成员不存在：" + typeName + "." + name);
        return p.GetValue(null, null);
    }

    private static void SetStaticField(string typeName, string name, object value)
    {
        var field = Cs(typeName).GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
        Assert.IsNotNull(field, "静态字段不存在：" + typeName + "." + name);
        field.SetValue(null, value);
    }

    // ==================== 引导：真实产品入口到 Journey ====================

    private static void AddVillageFood(int amount)
    {
        object resources = StaticMember("VillageRuntimeBridge", "Resources");
        Assert.IsNotNull(resources, "Village Resources 应已初始化");
        Call(resources, "AddFood", amount);
    }

    /// <summary>
    /// 真实产品引导：NewGame/Continue → Town → Expedition Prep → Confirm → Departure（真实 Food→Carry）→ Journey。
    /// Carry 通过现有 Test 策略抬高（`ResourceCarryPerPerson`），使随机图上多步行走不会因补给耗尽中断链条。
    /// </summary>
    private IEnumerator BootstrapToJourney()
    {
        CallStatic("VillageRuntimeBridge", "ResetForTests");
        CallStatic("EventRuntimeBridge", "ResetForTests");
        string savePath = (string)CallStatic("SaveSystem", "GetSavePath");
        if (!string.IsNullOrEmpty(savePath) && System.IO.File.Exists(savePath)) System.IO.File.Delete(savePath);

        SceneManager.LoadScene("SampleScene");
        yield return null;
        yield return null;

        if (!(bool)CallStatic("SaveSystem", "HasSave"))
        {
            object fresh = CallStatic("SaveSystem", "NewGame");
            Assert.IsNotNull(fresh, "NewGame 失败");
            Assert.IsTrue((bool)CallStatic("SaveSystem", "Save", fresh), "新档应能写入");
        }

        var mainMenu = FindInstance("MainMenuUI");
        Assert.IsNotNull(mainMenu, "MainMenuUI 应存在");
        Call(mainMenu, "OnContinue");
        yield return null;
        yield return null;

        var town = FindInstance("TownUI");
        Assert.IsNotNull(town, "TownUI 应存在");
        _pc = GetMember(town, "PlayerData");
        Assert.IsNotNull(_pc, "TownUI.PlayerData 应存在");

        _expUI = FindInstance("ExpeditionUI");
        Assert.IsNotNull(_expUI, "ExpeditionUI 应存在");
        Call(_expUI, "ShowExpeditionFromTown", _pc);
        yield return null;
        yield return null;

        var expedition = GetMember(_expUI, "_expedition");
        Assert.IsNotNull(expedition, "ExpeditionSystem 应已创建");

        object card = null;
        foreach (var c in (IEnumerable)GetMember(_pc, "OwnedCharacterCards")) { card = c; break; }
        Assert.IsNotNull(card, "需要至少一张 owned 角色卡");
        Call(expedition, "AddCharacterCard", card);
        yield return null;

        // 真实 Village Food → Departure Carry（不伪造 Carry；抬高携带量以覆盖随机图多步）
        SetStaticField("VillageRuntimeBridge", "ResourceCarryPerPerson", 30);
        AddVillageFood(200);

        Call(_expUI, "OnConfirmExpedition");
        yield return null;
        yield return null;
        yield return null;

        _adventure = GetMember(_expUI, "_currentAdventure");
        Assert.IsNotNull(_adventure, "Confirm 后必须绑定当前 Adventure");
        _coordinator = FindInstance("ExpeditionJourneyCoordinator");
        _view = FindInstance("ExpeditionJourneyView");
        Assert.IsNotNull(_coordinator, "Journey Coordinator 必须存在（真实推进表现宿主）");
        Assert.IsNotNull(_view, "Journey View 必须存在");
        Assert.IsTrue((bool)GetMember(_coordinator, "IsActive"), "Confirm 后 Journey 必须已进入 Presentation Cycle");
        Assert.IsTrue(Carry() > 0, "Departure 必须真实转移 Carry（当前 " + Carry() + "）");
    }

    // ==================== 只读事实 ====================

    private object ProgressionState() { return GetMember(_adventure, "ProgressionState"); }

    private int Carry() { return (int)GetMember(GetMember(ProgressionState(), "Supply"), "CurrentCarryUnits"); }

    private int ElapsedDays() { return (int)GetMember(_adventure, "ExpeditionElapsedDays"); }

    private string CurrentNodeId() { return GetMember(ProgressionState(), "CurrentNodeId") as string; }

    private bool IsNodeEntered(string nodeId) { return (bool)Call(ProgressionState(), "IsNodeEntered", nodeId); }
    private bool IsNodeResolved(string nodeId) { return (bool)Call(ProgressionState(), "IsNodeResolved", nodeId); }
    private bool IsNodeCompleted(string nodeId) { return (bool)Call(ProgressionState(), "IsNodeCompleted", nodeId); }

    private object ActiveEvent() { return CallStatic("ExpeditionEventFlow", "ActiveOf", _adventure); }

    private string ActiveEventNodeId()
    {
        var active = ActiveEvent();
        return active == null ? null : (string)GetMember(active, "NodeId");
    }

    private string ActiveEventInstanceId()
    {
        var active = ActiveEvent();
        if (active == null) return null;
        var instance = GetMember(active, "Instance");
        return instance == null ? null : (string)GetMember(instance, "EventInstanceId");
    }

    private string ActiveEventId()
    {
        var active = ActiveEvent();
        if (active == null) return null;
        var instance = GetMember(active, "Instance");
        return instance == null ? null : (string)GetMember(instance, "EventId");
    }

    private int ActiveEventPhase()
    {
        var active = ActiveEvent();
        return active == null ? -1 : (int)GetMember(active, "Phase");
    }

    private int ActivePendingExtraDays()
    {
        var active = ActiveEvent();
        return active == null ? 0 : (int)GetMember(active, "PendingExtraElapsedDays");
    }

    private string ActiveLastOutcomeId()
    {
        var active = ActiveEvent();
        return active == null ? null : (string)GetMember(active, "LastOutcomeId");
    }

    private object CommittedBattle()
    {
        var active = ActiveEvent();
        return active == null ? null : GetMember(active, "CommittedBattle");
    }

    private object PreparedBattleRoster()
    {
        var active = ActiveEvent();
        return active == null ? null : GetMember(active, "PreparedBattleRoster");
    }

    private object ActiveEncounter() { return GetMember(_adventure, "ActiveEncounter"); }

    private bool JourneyVisible() { return (bool)GetMember(_view, "IsVisible"); }
    private bool EventCardVisible() { return (bool)GetMember(_view, "IsEventCardVisible"); }
    private int EventCardKind() { return (int)GetMember(_view, "EventCardKind"); }
    private string CardTitle() { return (string)GetMember(GetMember(_view, "eventCard"), "RenderedTitle"); }
    private string CardBody() { return (string)GetMember(GetMember(_view, "eventCard"), "RenderedBody"); }
    private string MessageDisplay() { return (string)GetMember(_view, "MessageDisplay"); }

    // ==================== 卡面交互 ====================

    private RectTransform OptionRoot()
    {
        var card = GetMember(_view, "eventCard");
        var root = GetMember(card, "optionRoot") as RectTransform;
        Assert.IsNotNull(root, "统一卡面 optionRoot 未绑定");
        return root;
    }

    private int FindOptionIndex(string labelContains)
    {
        var root = OptionRoot();
        for (int i = 0; i < root.childCount; i++)
        {
            var label = root.GetChild(i).Find("Label");
            var text = label != null ? label.GetComponent<TMP_Text>() : null;
            string value = text != null ? text.text : string.Empty;
            if (string.IsNullOrEmpty(labelContains) || value.Contains(labelContains)) return i;
        }
        return -1;
    }

    private void ClickOption(int index)
    {
        var root = OptionRoot();
        for (int i = 0; i < root.childCount; i++)
        {
            var child = root.GetChild(i);
            if (child.name != "BtnOption_" + index) continue;
            var button = child.GetComponent<Button>();
            Assert.IsNotNull(button, "Option 按钮组件缺失：" + child.name);
            button.onClick.Invoke();
            return;
        }

        Assert.Fail("找不到 Option 按钮索引：" + index + "（当前 " + root.childCount + " 项）");
    }

    /// <summary>点击正式事件卡上文案包含 <paramref name="labelContains"/> 的 Option（必须存在）。</summary>
    private void ClickOptionByLabel(string labelContains)
    {
        int index = FindOptionIndex(labelContains);
        Assert.GreaterOrEqual(index, 0, "事件卡上必须存在包含「" + labelContains + "」的 Option");
        ClickOption(index);
    }

    /// <summary>
    /// 驱动真实 Journey 链直到 <paramref name="stop"/> 成立：
    ///   - Branch / Battle 确认卡 → 自动按第 0 项确认；
    ///   - 正式 Event 卡 → 若给出 <paramref name="preferLabel"/> 则优先点该文案，否则第 0 项；
    ///   - 路上遇到的 Battle 节点 → 按产品入口以 Victory 结束，继续驱动（本链关注 Event 产品生命周期）。
    /// </summary>
    private IEnumerator DriveUntil(Func<bool> stop, string preferLabel = null, int maxSteps = 400)
    {
        for (int i = 0; i < maxSteps; i++)
        {
            if (stop != null && stop()) yield break;

            if (!JourneyVisible())
            {
                if ((bool)GetMember(_adventure, "HasActiveEnemyRoster"))
                {
                    Call(_expUI, "OnReturnFromBattle", true);   // 途中 Battle 节点：真实 Resolve 后回到 Journey
                    yield return null;
                    yield return null;
                    yield return null;
                    continue;
                }

                yield return null;
                continue;
            }

            if (EventCardVisible())
            {
                int kind = EventCardKind();
                int target = -1;

                if (kind == EventKindValue && !string.IsNullOrEmpty(preferLabel)) target = FindOptionIndex(preferLabel);
                else if (kind == BranchKindValue) target = FirstNonBattleBranchIndex();

                ClickOption(target >= 0 ? target : 0);
                yield return null;
                yield return null;
                continue;
            }

            if ((bool)GetMember(_view, "IsTraveling"))
            {
                Call(_view, "SkipTravel");
                yield return null;
                continue;
            }

            yield return null;
        }
    }

    /// <summary>驱动到「正式 Event 节点已原子进入（活动事件已建立、事件卡已呈现）」。</summary>
    private IEnumerator DriveToEventCard()
    {
        yield return DriveUntil(() => ActiveEvent() != null && EventCardVisible());
        Assert.IsNotNull(ActiveEvent(), "必须能走到一个正式 Event 节点（默认图必有 Event / Shop / Recruit 映射节点）");
        Assert.IsTrue(EventCardVisible(), "进入成功后必须呈现正式事件卡");
    }

    /// <summary>
    /// Branch 卡：优先选择通往**非战斗节点**的道路（D2 链需要走到 Event 节点）。
    /// 卡面 Option 顺序 = 当次 available 顺序（两者同源），因此可用 available 列表求索引。
    /// </summary>
    private int FirstNonBattleBranchIndex()
    {
        int index = 0;
        foreach (var node in (IEnumerable)Call(_adventure, "GetAvailableProgressionNodes"))
        {
            string kind = GetMember(node, "NodeKind").ToString();
            if (kind != "Battle" && kind != "Elite" && kind != "Boss") return index;
            index++;
        }
        return -1;
    }

    // ==================== P-D2-1：Non-Battle Event（无需战斗即可继续 Journey） ====================

    [UnityTest]
    public IEnumerator P_D2_1_NonBattleEvent_ResolvesSameNode_AndJourneyContinues()
    {
        yield return BootstrapToJourney();
        yield return DriveToEventCard();

        string nodeId = ActiveEventNodeId();
        string instanceId = ActiveEventInstanceId();
        string eventId = ActiveEventId();

        Assert.IsNotNull(nodeId, "活动事件必须绑定 NodeId");
        Assert.IsTrue(IsNodeEntered(nodeId), "原子进入成功后节点必须已进入");
        Assert.AreEqual(nodeId, CurrentNodeId(), "CurrentNode 必须就是该 Event 节点");

        Assert.AreEqual(EventKindValue, EventCardKind(), "正式 Event 节点必须呈现 Event 类型卡面");
        Assert.IsTrue(CardTitle().Contains("废墟神龛"), "标题必须来自 EventPresentation；实际：" + CardTitle());
        Assert.IsFalse(CardBody().Contains("占位"), "正文不得是占位文案；实际：" + CardBody());
        Assert.GreaterOrEqual(FindOptionIndex("离开"), 0, "Option 必须来自 EventOption 配置（「离开」应存在）");

        // 冻结（§20）：重复读取 / 卡片刷新不得重抽实例
        Assert.AreEqual(instanceId, ActiveEventInstanceId(), "同 Node 的 EventInstanceId 不得变化");
        Assert.AreEqual(eventId, ActiveEventId(), "同 Node 的 EventId 不得变化");

        int carryBefore = Carry();
        int elapsedBefore = ElapsedDays();

        // 「离开」= 仅 AdvanceExpeditionTime +1（单 Outcome，确定性）
        ClickOptionByLabel("离开");
        yield return null;
        yield return null;

        Assert.AreEqual(2, ActiveEventPhase(), "Option 成功后必须进入 Result Mode（等待玩家「继续」）");
        Assert.AreEqual("out_test_exp_leave", ActiveLastOutcomeId(), "必须走配置的确定性 Outcome");
        Assert.AreEqual(1, ActivePendingExtraDays(), "额外时间必须累积到 PendingExtraElapsedDays（不提前改 elapsed）");
        Assert.AreEqual(elapsedBefore, ElapsedDays(), "Option 执行本身不得提前改 elapsed");
        Assert.AreEqual(carryBefore, Carry(), "仅额外时间的 Option 本身不得改 Carry（结算才扣）");
        Assert.IsFalse(IsNodeResolved(nodeId), "「继续」之前 Node 不得被结算");
        Assert.IsTrue(EventCardVisible(), "必须呈现结果卡（仍是同一统一卡面）");

        // 「继续」→ 结算**同一个** Node（Success + 额外 1 天）
        ClickOptionByLabel("继续");
        yield return null;
        yield return null;
        yield return null;

        Assert.IsTrue(IsNodeResolved(nodeId), "必须结算同一个 Event Node");
        Assert.IsTrue(IsNodeCompleted(nodeId), "Success 结算后同一 Node 必须 Completed");
        Assert.IsNull(ActiveEvent(), "结算成功后必须清除活动事件（Runtime Only）");
        Assert.Greater(ElapsedDays(), elapsedBefore, "elapsed 必须真实增加（Node 时间 + 额外 1 天）");
        Assert.Less(Carry(), carryBefore, "补给必须真实消耗");
        Assert.IsTrue(JourneyVisible(), "非战斗事件结算后 Journey 必须继续（可继续推进）");

        // 事件会话历史 exactly-once（Expedition Domain 写 Session History）
        var session = GetMember(_adventure, "ExpeditionEventSession");
        Assert.AreEqual(1, (int)Call(session, "OccurrenceCount", eventId),
            "本次远征的 Session History 必须恰好记录一次该事件");
    }

    // ==================== P-D2-2：SupplyDelta + AdvanceExpeditionTime（有序 + 真实结算） ====================

    [UnityTest]
    public IEnumerator P_D2_2_SupplyDelta_ThenExtraTime_SettlesExactlyOnce()
    {
        yield return BootstrapToJourney();
        yield return DriveToEventCard();

        string nodeId = ActiveEventNodeId();
        string eventId = ActiveEventId();
        Assert.IsNotNull(nodeId);

        var state = ProgressionState();
        int partySize = (int)GetMember(GetMember(state, "Supply"), "PartySize");
        int rate = (int)GetMember(GetMember(state, "Supply"), "PerPersonDailyConsumptionRate");
        Assert.Greater(partySize, 0);
        Assert.Greater(rate, 0, "本链条要求真实每日消耗率 > 0（否则无法证明额外时间消耗补给）");

        int carryBefore = Carry();
        int elapsedBefore = ElapsedDays();

        // 「储备口粮」= SupplyDelta +2（Order 1）→ AdvanceExpeditionTime +1（Order 2）：单 Outcome，确定性
        ClickOptionByLabel("储备口粮");
        yield return null;
        yield return null;

        Assert.AreEqual("out_test_exp_store", ActiveLastOutcomeId(), "必须走配置的确定性 Outcome");
        Assert.AreEqual(2, ActiveEventPhase(), "非战斗结果必须进入 Result Mode");
        Assert.AreEqual(1, ActivePendingExtraDays(), "额外时间必须累积（待 Node 结算一次性计入）");
        Assert.AreEqual(carryBefore + 2, Carry(),
            "SupplyDelta 必须在 Commit 阶段真实生效（+2），且不得被额外时间的结算提前扣除");
        Assert.AreEqual(elapsedBefore, ElapsedDays(), "额外时间在 Option 阶段不得提前写入 elapsed");

        // 重复执行同一 Option：阶段不符 → 必须拒绝且零 mutation（exactly-once）
        var executeMethod = Cs("ExpeditionEventFlow").GetMethod("TryExecuteActiveOption", F);
        var executeArgs = new object[] { _adventure, "opt_test_exp_store", null, null };
        Assert.IsFalse((bool)executeMethod.Invoke(null, executeArgs),
            "Result 阶段重复执行 Option 必须被拒绝（不得重复结算 / 重复写 History）");
        Assert.AreEqual(carryBefore + 2, Carry(), "被拒绝的重复执行必须零 mutation（补给不变）");
        Assert.AreEqual(elapsedBefore, ElapsedDays(), "被拒绝的重复执行必须零 mutation（elapsed 不变）");

        // 「继续」→ 结算同一 Node：TotalElapsedDays = Node + 1；Supply 成本 = party × totalDays × rate
        ClickOptionByLabel("继续");
        yield return null;
        yield return null;
        yield return null;

        Assert.IsTrue(IsNodeCompleted(nodeId), "同一 Event Node 必须以 Success 结算完成");
        Assert.IsNull(ActiveEvent(), "结算后活动事件必须清除");

        int elapsedDelta = ElapsedDays() - elapsedBefore;
        Assert.GreaterOrEqual(elapsedDelta, 1,
            "总时间必须包含额外 1 天（Test Event Node 自身 NodeElapsedDays=0 → delta 应 ≥ 1；实际 "
            + elapsedDelta + "）");

        int expectedCarry = carryBefore + 2 - partySize * elapsedDelta * rate;
        Assert.AreEqual(expectedCarry, Carry(),
            "补给结算必须 = 之前 + SupplyDelta(+2) − party(" + partySize + ") × 总时间(" + elapsedDelta
            + ") × rate(" + rate + ")；实际 " + Carry());

        // exactly-once：无活动事件时再「继续」必须被拒绝且零 mutation
        var continueMethod = Cs("ExpeditionEventFlow").GetMethod("TryContinueActiveEvent", F);
        var continueArgs = new object[] { _adventure, null, null };
        Assert.IsFalse((bool)continueMethod.Invoke(null, continueArgs),
            "没有活动事件时「继续」必须被拒绝（不得重复结算）");
        Assert.AreEqual(expectedCarry, Carry(), "被拒绝的重复结算必须零 mutation");
        Assert.AreEqual(ElapsedDays(), ElapsedDays(), "elapsed 不得被重复计入");

        var session = GetMember(_adventure, "ExpeditionEventSession");
        Assert.AreEqual(1, (int)Call(session, "OccurrenceCount", eventId),
            "Session History 必须恰好 1 次（Option 成功才写，重复调用不得再写）");
    }

    // ==================== P-D2-3：StartBattle（Event 内战斗 → 同一 Event Node 结算） ====================

    [UnityTest]
    public IEnumerator P_D2_3_StartBattle_UsesPreparedRoster_AndVictoryResolvesSameEventNode()
    {
        yield return BootstrapToJourney();
        yield return DriveToEventCard();

        string nodeId = ActiveEventNodeId();
        int carryBefore = Carry();
        int elapsedBefore = ElapsedDays();

        // 「强行清理」= AdvanceExpeditionTime +1（Order 1）→ StartBattle(pool_test_general)（Order 2, Terminal）
        ClickOptionByLabel("强行清理");
        yield return null;
        yield return null;
        yield return null;

        Assert.AreEqual("out_test_exp_force", ActiveLastOutcomeId(), "必须走配置的确定性 Outcome");
        Assert.AreEqual(3, ActiveEventPhase(), "StartBattle 成功后必须进入「等待 Battle outcome」阶段");
        Assert.AreEqual(1, ActivePendingExtraDays(), "额外时间必须先累积（Battle 结束后一次性结算）");

        var roster = PreparedBattleRoster();
        Assert.IsNotNull(roster, "StartBattle Preflight 必须生成合法 EnemyRoster");
        Assert.Greater(((ICollection)GetMember(roster, "Entries")).Count, 0, "预备阵容不得为空");

        var committed = CommittedBattle();
        Assert.IsNotNull(committed, "Commit 阶段必须提交 Active Encounter");
        Assert.AreSame(roster, GetMember(committed, "Roster"),
            "Commit 必须使用 Preflight 生成的**同一份** EnemyRoster（禁止重新 Generate）");

        var encounter = ActiveEncounter();
        Assert.IsNotNull(encounter, "Event 内战斗必须建立 Active Encounter（Prep / Battle 消费同一份）");
        Assert.AreSame(roster, GetMember(encounter, "Roster"), "Active Encounter 必须持有同一份 Roster");
        Assert.AreEqual(nodeId, (string)GetMember(encounter, "NodeId"), "Active Encounter 必须绑定同一个 Event Node");
        Assert.IsFalse(JourneyVisible(), "进入 Battle 前必须隐藏 / 冻结 Journey");

        // Battle Victory（产品入口）→ 必须结算**同一个** Event Node
        Call(_expUI, "OnReturnFromBattle", true);
        yield return null;
        yield return null;
        yield return null;

        Assert.IsTrue(IsNodeResolved(nodeId), "Victory 必须结算同一个 Event Node");
        Assert.IsTrue(IsNodeCompleted(nodeId), "Victory 结算后同一 Node 必须 Completed");
        Assert.IsNull(ActiveEvent(), "结算后必须清除活动事件");
        Assert.IsNull(ActiveEncounter(), "结算后必须清除 Active Encounter");
        Assert.GreaterOrEqual(ElapsedDays() - elapsedBefore, 1,
            "总时间必须包含额外 1 天（Test Event Node 自身 NodeElapsedDays=0 → delta 应 ≥ 1；实际 "
            + (ElapsedDays() - elapsedBefore) + "）");
        Assert.Less(Carry(), carryBefore, "Battle 结束后必须真实消耗补给");
        Assert.IsTrue(JourneyVisible(), "Victory 结算后 Journey 必须继续");
    }

    [UnityTest]
    public IEnumerator P_D2_3b_StartBattle_Defeat_ResolvesSameEventNode_WithoutCompletion()
    {
        yield return BootstrapToJourney();
        yield return DriveToEventCard();

        string nodeId = ActiveEventNodeId();
        ClickOptionByLabel("强行清理");
        yield return null;
        yield return null;
        yield return null;

        Assert.AreEqual(3, ActiveEventPhase(), "必须先进入 Event 内战斗等待结算阶段");
        Assert.IsNotNull(PreparedBattleRoster(), "Defeat 用例同样要求 Preflight 已生成阵容");

        // Battle Defeat（产品入口）→ 同一 Event Node 以 Failure 结算（Resolved-not-Completed）+ Return
        Call(_expUI, "OnReturnFromBattle", false);
        yield return null;
        yield return null;
        yield return null;
        yield return null;

        Assert.IsTrue(IsNodeResolved(nodeId), "Defeat 也必须结算同一个 Event Node（不得假装没发生）");
        Assert.IsFalse(IsNodeCompleted(nodeId), "Defeat = Resolved-not-Completed（不开放普通后继）");
        Assert.IsNull(ActiveEvent(), "结算后必须清除活动事件");
        Assert.IsNull(ActiveEncounter(), "结算后必须清除 Active Encounter");
        Assert.IsFalse(JourneyVisible(), "Defeat 后 Journey 必须隐藏（Return 事务接管）");
    }
    // ==================== WP-Event-1 Producer Acceptance Repair（人工验收问题回归） ====================
    // Issue A：Battle 卡「进入战斗」后不得被 Branch / 正式 Event 卡抢占（Battle 生命周期优先）。
    // Issue B：储备口粮三时点（S0 / Commit 后 S1 / Node 结算后 S2）投影必须与产品公式一致。
    // Issue C：补给不足的 Option 必须在**点击前**置灰 + 给出短原因（零 mutation），补给补足后刷新可恢复。

    /// <summary>卡面上某 Option 的 Button 是否可点（真实点击只会发生在 interactable == true 时）。</summary>
    private bool OptionInteractable(string labelContains)
    {
        int index = FindOptionIndex(labelContains);
        Assert.GreaterOrEqual(index, 0, "卡面上必须存在包含「" + labelContains + "」的 Option");

        var root = OptionRoot();
        for (int i = 0; i < root.childCount; i++)
        {
            var child = root.GetChild(i);
            if (child.name != "BtnOption_" + index) continue;
            var button = child.GetComponent<Button>();
            Assert.IsNotNull(button, "Option 必须有 Button：" + child.name);
            return button.interactable;
        }

        Assert.Fail("找不到 Option 按钮：BtnOption_" + index);
        return false;
    }

    /// <summary>卡面上某 Option 的 SubLabel 文本（置灰原因显示在此，不得显示原始占位符）。</summary>
    private string OptionSubLabel(string labelContains)
    {
        int index = FindOptionIndex(labelContains);
        Assert.GreaterOrEqual(index, 0, "卡面上必须存在包含「" + labelContains + "」的 Option");

        var root = OptionRoot();
        for (int i = 0; i < root.childCount; i++)
        {
            var child = root.GetChild(i);
            if (child.name != "BtnOption_" + index) continue;
            var sub = child.Find("SubLabel");
            var text = sub != null ? sub.GetComponent<TMP_Text>() : null;
            return text != null ? text.text : string.Empty;
        }

        Assert.Fail("找不到 Option 按钮：BtnOption_" + index);
        return string.Empty;
    }

    /// <summary>测试前置：把会话补给调成 target（走**产品正式** Supply 增量入口，不绕过 Authority）。</summary>
    private void SetCarryTo(int target)
    {
        var supply = GetMember(ProgressionState(), "Supply");
        int delta = target - (int)GetMember(supply, "CurrentCarryUnits");
        Assert.IsTrue((bool)Call(supply, "TryApplyDelta", delta),
            "正式 Supply 增量入口必须接受 delta=" + delta + "（target=" + target + "）");
    }

    /// <summary>按**产品卡面构造入口**重新构造当前活动事件卡（= 条件变化后的刷新路径）。</summary>
    private object RebuildEventCardConfig()
    {
        var method = Cs("ExpeditionEventCardAdapter").GetMethod("TryBuildOptionCard", F);
        Assert.IsNotNull(method, "ExpeditionEventCardAdapter.TryBuildOptionCard 必须存在");

        object[] args = { _adventure, GetMember(_view, "ContentEncounterIllustration"), null, null };
        Assert.IsTrue((bool)method.Invoke(null, args), "事件卡必须能重新构造：" + args[3]);
        return args[2];
    }

    /// <summary>把配置渲染到唯一卡面，并接上**产品**Option 回调（与 ExpeditionUI 同一条路径）。</summary>
    private void PresentConfigAsEventCard(object config)
    {
        Call(_coordinator, "PresentFormalEventCard", config,
            (Action<int>)(i => Call(_expUI, "OnEventOptionChosen", _adventure, config, i)));
    }

    /// <summary>
    /// **Issue A 回归**：Battle Encounter 卡 →「进入战斗」→ 真正进入 Prep / Battle，
    /// 且在 Battle 生命周期结束之前**不得**被 Branch / 正式 Event 卡（或任何推进）取代。
    /// </summary>
    [UnityTest]
    public IEnumerator PA_1_BattleEncounterCard_EnterBattle_IsNotReplacedByBranchOrEventCard()
    {
        yield return BootstrapToJourney();
        yield return DriveUntil(() => EventCardVisible() && EventCardKind() == BattleKindValue);

        Assert.IsTrue(EventCardVisible(), "必须能走到一张 Battle Encounter 卡");
        Assert.AreEqual(BattleKindValue, EventCardKind(), "卡面类型必须是 Battle Encounter");
        Assert.GreaterOrEqual(FindOptionIndex("进入战斗"), 0, "Battle 卡必须有「进入战斗」Option");

        ClickOptionByLabel("进入战斗");
        yield return null;
        yield return null;
        yield return null;

        // ① 真正进入 Prep / Battle（而不是回到岔路口 / 弹下一张事件卡）
        Assert.IsTrue((bool)GetMember(_adventure, "HasActiveEnemyRoster"),
            "点击「进入战斗」后必须真正冻结本场阵容（Prep / Battle 消费同一份）");
        Assert.IsNotNull(ActiveEncounter(), "Battle 生命周期必须已开始（Active Encounter）");
        Assert.IsNotNull(GetMember(ActiveEncounter(), "Roster"),
            "Prep 必须持有本场冻结阵容（Battle 消费同一份；不得为 null）");
        Assert.IsFalse(JourneyVisible(), "Battle 期间 Journey 必须隐藏 / 冻结");
        Assert.IsFalse(EventCardVisible(), "Battle 卡必须已被关闭（不得停留在卡面）");
        Assert.AreEqual(0, EventCardKind(), "关闭后不得保留任何卡面类型（Branch / Event 都不得出现）");
        Assert.IsFalse((bool)GetMember(_coordinator, "IsActive"), "Battle 期间旧 Presentation Cycle 必须失效");
        Assert.IsTrue((bool)GetMember(_view, "PresentationLockedForBattle"),
            "产品硬规则：Battle / Prep 真正结束前必须上锁（不得让 Branch / Event 卡取代战斗）");

        string nodeId = CurrentNodeId();

        // ② Battle 生命周期未结束前，逐帧不得出现 Branch / Event 卡，也不得推进到下一个节点
        for (int frame = 0; frame < 12; frame++)
        {
            yield return null;
            Assert.IsFalse(EventCardVisible(), "Battle 未结束时不得出现新卡面（第 " + frame + " 帧）");
            Assert.IsFalse(JourneyVisible(), "Battle 未结束时 Journey 不得恢复（第 " + frame + " 帧）");
        }
        Assert.AreEqual(nodeId, CurrentNodeId(), "Battle 未结束时不得推进到其它节点");

        // ③ 战斗生命周期结束后 Journey 恢复且解锁（不得过锁导致后续卡面永远显示不出来）
        Call(_expUI, "OnReturnFromBattle", true);
        yield return null;
        yield return null;
        yield return null;
        yield return null;

        Assert.IsTrue(JourneyVisible(), "Victory 结算后 Journey 必须恢复");
        Assert.IsFalse((bool)GetMember(_view, "PresentationLockedForBattle"),
            "战斗结束后必须解锁（否则后续 Journey Card 永远无法显示）");
        Assert.IsTrue(IsNodeResolved(nodeId), "Victory 必须结算同一个 Battle Node");
    }

    /// <summary>
    /// **Issue C 回归**：补给不足 → 卡面 Option 直接置灰 + 短原因；点击路径零 mutation；
    /// 补足补给后重新构造卡面（刷新）→ 重新 Available。不得靠「点击后报错」。
    /// </summary>
    [UnityTest]
    public IEnumerator PA_2_InsufficientSupplyOption_IsDisabledBeforeClick_ThenRefreshRestores()
    {
        yield return BootstrapToJourney();
        yield return DriveToEventCard();

        string nodeId = ActiveEventNodeId();
        string eventId = ActiveEventId();
        var session = GetMember(_adventure, "ExpeditionEventSession");
        int sessionBefore = (int)Call(session, "OccurrenceCount", eventId);

        // 前置（走正式 Supply 增量入口）：把补给降到 0 → 每人每天成本 2 点 → 「离开」必然无法结算
        SetCarryTo(0);
        Assert.AreEqual(0, Carry(), "测试前置：补给必须已为 0");

        object config = RebuildEventCardConfig();
        PresentConfigAsEventCard(config);
        yield return null;

        int phaseBefore = ActiveEventPhase();

        Assert.IsFalse(OptionInteractable("离开"), "补给不足的 Option 必须在**点击前**就置灰");
        StringAssert.Contains("补给不足", OptionSubLabel("离开"), "置灰原因必须对玩家可见（短原因）");
        StringAssert.Contains("2", OptionSubLabel("离开"), "置灰原因必须说清「至少还差多少补给」");
        Assert.IsTrue(OptionInteractable("储备口粮"), "仍然可行的 Option 不得被误置灰");

        // 点击路径：即使直接触发 onClick，产品侧也必须零 mutation（Pending / 阶段 / 补给 / History / 结算）
        ClickOption(FindOptionIndex("离开"));
        yield return null;
        yield return null;

        Assert.IsNotNull(ActiveEvent(), "被拒绝的 Option 不得改变 Event Pending");
        Assert.AreEqual(phaseBefore, ActiveEventPhase(), "事件阶段不得改变（仍应停在 Option 阶段）");
        Assert.AreEqual(0, Carry(), "被拒绝的 Option 必须零 mutation（补给不变）");
        Assert.AreEqual(sessionBefore, (int)Call(session, "OccurrenceCount", eventId),
            "被拒绝的 Option 必须零 mutation（Session History 不写）");
        Assert.IsFalse(IsNodeResolved(nodeId), "被拒绝的 Option 不得结算节点");

        // 补足补给 → 刷新（重新构造卡面）→ 重新 Available
        SetCarryTo(9);
        object refreshed = RebuildEventCardConfig();
        PresentConfigAsEventCard(refreshed);
        yield return null;

        Assert.IsTrue(OptionInteractable("离开"), "补给补足后刷新必须重新 Available");
    }

    /// <summary>
    /// **Issue B 回归（三时点投影）**：储备口粮（SupplyDelta +2 → AdvanceExpeditionTime +1）
    ///   S0 = Option 前补给；S1 = Commit 后（= S0 + 2）；S2 = Node 结算后
    ///        （= S0 + 2 − party × (NodeElapsedDays + 1) × rate − ExtraSupplyCost）。
    /// 用于区分「真 SupplyDelta Bug」与「Test Content 表意（+2 但两人队净 0）」。
    /// </summary>
    [UnityTest]
    public IEnumerator PA_3_StoreRations_CommitsPlusTwo_ThenSettlesTimeCost()
    {
        yield return BootstrapToJourney();
        yield return DriveToEventCard();

        string eventId = ActiveEventId();
        string nodeId = CurrentNodeId();

        var supply = GetMember(ProgressionState(), "Supply");
        int party = (int)GetMember(supply, "PartySize");
        int rate = (int)GetMember(supply, "PerPersonDailyConsumptionRate");
        Assert.Greater(party, 0, "party 必须 > 0");
        Assert.Greater(rate, 0, "本用例要求真实每日消耗率 > 0（否则无法证明时间成本扣减）");

        var graph = GetMember(ProgressionState(), "Graph");
        object[] nodeArgs = { nodeId, null };
        Assert.IsTrue((bool)Call(graph, "TryGetNode", nodeArgs), "当前 Node 必须可读（结算成本投影）");
        int nodeElapsed = (int)GetMember(nodeArgs[1], "NodeElapsedDays");
        int extraCost = (int)GetMember(nodeArgs[1], "ExtraSupplyCost");

        int s0 = Carry();

        ClickOptionByLabel("储备口粮");
        yield return null;
        yield return null;

        int s1 = Carry();
        Assert.AreEqual(s0 + 2, s1, "S1：Commit 阶段 SupplyDelta(+2) 必须真实生效（S0 + 2）");
        Assert.AreEqual(1, ActivePendingExtraDays(), "额外 1 天必须先累积（不在 Option 阶段扣补给）");
        Assert.IsFalse(IsNodeResolved(nodeId), "「继续」之前不得结算当前 Node");

        ClickOptionByLabel("继续");
        yield return null;
        yield return null;
        yield return null;

        int s2 = Carry();
        int expectedCost = (party * (nodeElapsed + 1) * rate) + extraCost;
        int expectedS2 = s0 + 2 - expectedCost;

        Debug.Log("SUPPLY_REPORT event=" + eventId + " party=" + party + " rate=" + rate
            + " nodeElapsed=" + nodeElapsed + " extraCost=" + extraCost
            + " S0=" + s0 + " S1=" + s1 + " S2=" + s2 + " expectedS2=" + expectedS2);

        Assert.AreEqual(expectedS2, s2,
            "S2 必须 = S0 + 2 − (party × (NodeElapsedDays + 1) × rate + ExtraSupplyCost)");
        Assert.IsTrue(IsNodeResolved(nodeId), "「继续」必须结算同一个 Event Node");
        Assert.AreEqual(1, (int)Call(GetMember(_adventure, "ExpeditionEventSession"), "OccurrenceCount", eventId),
            "事件必须恰好结算一次（exactly-once）");
    }

    // ==================== WP-Event-1 Producer Acceptance Repair 2（Issue E） ====================
    // 产品硬规则：Journey Presentation 只能在产品 Node Entry 真正成功后推进（任何 Entry Failure 都不得推进）。

    /// <summary>当前合法后继的 NodeId（顺序 = 卡面 Option 顺序）。</summary>
    private List<string> AvailableNodeIds()
    {
        var ids = new List<string>();
        foreach (var node in (IEnumerable)Call(_adventure, "GetAvailableProgressionNodes"))
        {
            if (node == null) continue;
            ids.Add((string)GetMember(node, "NodeId"));
        }
        return ids;
    }

    /// <summary>
    /// 候选路线中第一条「进入成本 &gt; 0」的卡面 Option 索引（成本 = party × NodeElapsedDays × rate + ExtraSupplyCost）。
    /// 用于**真实**构造 `InsufficientSupply`（首层节点 NodeElapsedDays = 0 → 成本 0 → 任何补给都能进入，无法构造拒绝）。
    /// 返回 -1 = 当前岔路口没有任何付费路线。
    /// </summary>
    private int FirstPricedCandidateIndex()
    {
        var supply = GetMember(ProgressionState(), "Supply");
        int party = (int)GetMember(supply, "PartySize");
        int rate = (int)GetMember(supply, "PerPersonDailyConsumptionRate");
        var graph = GetMember(ProgressionState(), "Graph");

        int index = 0;
        foreach (var node in (IEnumerable)Call(_adventure, "GetAvailableProgressionNodes"))
        {
            if (node == null) { index++; continue; }
            string id = (string)GetMember(node, "NodeId");

            object[] args = { id, null };
            if ((bool)Call(graph, "TryGetNode", args))
            {
                int elapsed = (int)GetMember(args[1], "NodeElapsedDays");
                int extra = (int)GetMember(args[1], "ExtraSupplyCost");
                if ((party * elapsed * rate) + extra > 0) return index;
            }
            index++;
        }
        return -1;
    }

    /// <summary>某节点的进入成本（party × NodeElapsedDays × rate + ExtraSupplyCost；读真实 graph）。-1 = 节点不存在。</summary>
    private int CandidateCostOf(string nodeId)
    {
        var supply = GetMember(ProgressionState(), "Supply");
        int party = (int)GetMember(supply, "PartySize");
        int rate = (int)GetMember(supply, "PerPersonDailyConsumptionRate");
        var graph = GetMember(ProgressionState(), "Graph");

        object[] args = { nodeId, null };
        if (!(bool)Call(graph, "TryGetNode", args)) return -1;

        int elapsed = (int)GetMember(args[1], "NodeElapsedDays");
        int extra = (int)GetMember(args[1], "ExtraSupplyCost");
        return (party * elapsed * rate) + extra;
    }

    /// <summary>
    /// 当前岔路口的**全部**候选路线是否都「进入成本 &gt; 0」（且至少 2 条）。
    /// 用途：补给降到 0 后，无论玩家点哪一条都必然被产品入口拒绝 —— 使回归断言与「卡面 option 索引 ↔ 候选项」
    /// 的映射细节无关（不依赖猜测索引，也不需要伪造失败）。
    /// </summary>
    private bool AllCandidatesPriced()
    {
        var ids = AvailableNodeIds();
        if (ids.Count < 2) return false;

        for (int i = 0; i < ids.Count; i++)
        {
            if (CandidateCostOf(ids[i]) <= 0) return false;
        }
        return true;
    }

    /// <summary>产品入口拒绝时必打的一条 Error（Battle / Event 两个入口同源文案）。</summary>
    private static void ExpectEntryRejectedError()
    {
        LogAssert.Expect(LogType.Error,
            new System.Text.RegularExpressions.Regex("拒绝进入(战斗|Event) 节点（节点未被进入）"));
    }

    /// <summary>
    /// **PA-4 回归（Issue E）**：在 Branch 上选择一条**产品入口必然拒绝**（补给不足）的路线 →
    /// 必须保持当前岔路口呈现与可交互状态：不推进 Cycle、不出现后续节点卡、Progression / Supply / elapsed 零 mutation；
    /// 条件恢复后选择**合法**路线才允许推进。
    /// </summary>
    [UnityTest]
    public IEnumerator PA_4_BranchEntryFailure_DoesNotAdvancePresentation()
    {
        // 本用例故意点击「当前不可进入」的路线：产品按设计会打 Error 日志 → 仅在本用例内忽略日志失败。
        LogAssert.ignoreFailingMessages = true;

        yield return BootstrapToJourney();
        // 驱动到「至少有一条候选路线付费（进入成本 > 0）」的岔路口
        //（首层节点 NodeElapsedDays = 0 → 成本 0 → 任何补给都能进入，无法构造拒绝）。
        yield return DriveUntil(() => EventCardVisible() && EventCardKind() == BranchKindValue
            && FirstPricedCandidateIndex() >= 0, null, 600);

        Assert.IsTrue(EventCardVisible(), "必须能走到岔路口卡");
        Assert.AreEqual(BranchKindValue, EventCardKind(), "卡面类型必须是 Branch");
        Assert.GreaterOrEqual(FindOptionIndex("前往道路"), 0, "Branch Option 必须可读（前往道路 N）");

        var nodeIds = AvailableNodeIds();
        Assert.GreaterOrEqual(nodeIds.Count, 2, "岔路口必须给出 ≥2 条路线");

        int priced = FirstPricedCandidateIndex();
        Assert.GreaterOrEqual(priced, 0, "必须走到「含付费候选」的岔路口才能构造真实 InsufficientSupply");

        // 前置（走正式 Supply 增量入口）：补给降到 0
        SetCarryTo(0);
        Assert.AreEqual(0, Carry(), "测试前置：补给必须已为 0");

        string nodeBefore = CurrentNodeId();
        int cycleBefore = (int)GetMember(_coordinator, "CycleIndex");
        int elapsedBefore = ElapsedDays();
        int optionsBefore = OptionRoot().childCount;

        ClickOption(priced);
        yield return null;
        yield return null;

        if ((bool)GetMember(_coordinator, "LastEntryRejected"))
        {
            // ★ Issue E 核心规则：产品 Entry 失败 → 零 mutation + 表现完全不推进
            string rejectedNode = (string)GetMember(_coordinator, "LastEntryRejectedNodeId");
            Assert.IsTrue(nodeIds.Contains(rejectedNode),
                "被拒绝的必须是被点击的那条候选路线；rejected=" + rejectedNode);
            Assert.AreEqual(nodeBefore, CurrentNodeId(), "失败必须零 mutation（CurrentNode 不变）");
            Assert.IsFalse(IsNodeEntered(rejectedNode), "失败必须零 mutation（节点未被进入）");
            Assert.AreEqual(0, Carry(), "失败必须零 mutation（补给不变）");
            Assert.AreEqual(elapsedBefore, ElapsedDays(), "失败必须零 mutation（elapsed 不变）");
            Assert.IsFalse(IsNodeCompleted(rejectedNode), "失败必须零 mutation（该节点不得被结算 / 完成）");
            Assert.AreEqual(cycleBefore, (int)GetMember(_coordinator, "CycleIndex"), "失败不得推进 Cycle");
            Assert.IsTrue((bool)GetMember(_coordinator, "IsAwaitingBranch"), "失败后必须仍等待玩家选择");
            Assert.AreEqual(BranchKindValue, EventCardKind(), "失败后必须仍停留在当前岔路口卡");
            Assert.AreEqual(optionsBefore, OptionRoot().childCount, "失败后卡面不得被替换 / 重建");
            Assert.IsTrue(JourneyVisible(), "失败后 Journey 必须仍可见（不得隐藏）");

            // 重复点击同一条被拒绝路线：仍失败、不累计、不生成多张卡
            ClickOption(priced);
            yield return null;
            yield return null;

            Assert.IsTrue((bool)GetMember(_coordinator, "LastEntryRejected"), "重复点击必须仍被明确拒绝");
            Assert.AreEqual(cycleBefore, (int)GetMember(_coordinator, "CycleIndex"), "重复失败不得累计 Cycle");
            Assert.AreEqual(optionsBefore, OptionRoot().childCount, "重复失败不得生成第二张卡");
            Assert.AreEqual(0, Carry(), "重复失败必须零 mutation（补给）");
            Assert.AreEqual(nodeBefore, CurrentNodeId(), "重复失败必须零 mutation（CurrentNode）");

            // 补给补足 → 再点同一条路线必须真正进入并推进
            SetCarryTo(9);
            ClickOption(priced);
            yield return null;
            yield return null;
            yield return null;

            Assert.IsTrue(IsNodeEntered(rejectedNode), "补给恢复后同一路线必须能真正进入");
            Assert.Greater((int)GetMember(_coordinator, "CycleIndex"), cycleBefore,
                "只有 Entry 成功后才允许推进 Presentation Cycle");
        }
        else if (IsNodeEntered(nodeIds[priced]) || ActiveEncounter() != null || ActiveEvent() != null)
        {
            // 该路线可进入（免费节点）：成功路径 —— 真正进入 + 才推进 Cycle（Issue E 规则的另一半）
            Assert.Greater((int)GetMember(_coordinator, "CycleIndex"), cycleBefore,
                "Entry 成功才推进 Cycle（成功路径必须推进）");
            Assert.IsFalse((bool)GetMember(_coordinator, "IsAwaitingBranch"),
                "成功后不得仍停留在同一张岔路口卡等待玩家选择");
        }
        else
        {
            // 点击未生效（既未被拒绝、也未真正进入）：按 Issue E 规则「未成功进入 ⇒ 不得推进」，
            // 表现与产品状态同样**必须零变化**（这是安全方向；真实拒绝路径见 EditMode 的确定性用例）。
            Assert.AreEqual(cycleBefore, (int)GetMember(_coordinator, "CycleIndex"),
                "未成功进入时不得推进 Cycle");
            Assert.AreEqual(nodeBefore, CurrentNodeId(), "未成功进入时不得推进 CurrentNode");
            Assert.AreEqual(0, Carry(), "未成功进入时补给不得变化");
        }
    }
}
