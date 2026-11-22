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
/// V4 Functional Closure 专项（Disposable Development Test）：
/// 验证 Current Node-based Progression 的**可见**产品表现：
///   补给可见 → 合法后继只呈现当次选择（不显示完整 4×3 拓扑）→ 进入节点 → Supply 真实减少；
///   Supply 不足时点击「继续前进」必须明确显示「补给不足，无法继续前进」；
///   Supply 不足状态下 Retreat 仍可用并能走既有 Return 事务回到 Town。
/// 全部内容 Test / Not Frozen。
/// </summary>
public class VillageV4ProgressionClosurePlayTests
{
    private const BindingFlags F =
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

    private static Type GetType(string name) => Type.GetType(name + ", Assembly-CSharp");

    private static object FindInstance(string typeName)
    {
        var t = GetType(typeName);
        if (t == null) return null;
        foreach (var o in UnityEngine.Object.FindObjectsOfType(t, true))
            if (o != null) return o;
        return null;
    }

    private static object GetMember(object o, string name)
    {
        var f = o.GetType().GetField(name, F);
        if (f != null) return f.GetValue(o);
        var p = o.GetType().GetProperty(name, F);
        Assert.IsNotNull(p, "成员不存在：" + name + " @ " + o.GetType().FullName);
        return p.GetValue(o, null);
    }

    private static object GetStaticMember(string typeName, string name)
    {
        var t = GetType(typeName);
        var f = t.GetField(name, F);
        if (f != null) return f.GetValue(null);
        var p = t.GetProperty(name, F);
        Assert.IsNotNull(p, "静态成员不存在：" + typeName + "." + name);
        return p.GetValue(null, null);
    }

    private static void SetStaticField(Type type, string name, object value)
    {
        var f = type.GetField(name, F);
        Assert.IsNotNull(f, "静态字段不存在：" + name);
        f.SetValue(null, value);
    }

    private static object Call(object o, string method, params object[] args)
    {
        var m = o.GetType().GetMethod(method, F);
        Assert.IsNotNull(m, "方法不存在：" + method + " @ " + o.GetType().FullName);
        return m.Invoke(o, args);
    }

    private static object CallStatic(string typeName, string method, params object[] args)
    {
        var t = GetType(typeName);
        var candidates = t.GetMethods(F).Where(m => m.Name == method).ToList();
        foreach (var m in candidates)
        {
            var ps = m.GetParameters();
            if (ps.Length != (args == null ? 0 : args.Length)) continue;
            bool ok = true;
            for (int i = 0; i < ps.Length; i++)
            {
                if (args[i] == null) continue;
                if (!ps[i].ParameterType.IsInstanceOfType(args[i])) { ok = false; break; }
            }
            if (ok) return m.Invoke(null, args);
        }
        Assert.Fail("静态方法不存在或参数不匹配：" + typeName + "." + method);
        return null;
    }

    private static IEnumerator EnterTownAndExpedition(Action<object, object> afterReady)
    {
        CallStatic("VillageRuntimeBridge", "ResetForTests");
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

        var mm = FindInstance("MainMenuUI");
        Assert.IsNotNull(mm, "MainMenuUI 应存在");
        Call(mm, "OnContinue");
        yield return null;
        yield return null;

        var town = FindInstance("TownUI");
        Assert.IsNotNull(town, "TownUI 应存在");
        object pc = GetMember(town, "PlayerData");

        var expUI = FindInstance("ExpeditionUI");
        Assert.IsNotNull(expUI, "ExpeditionUI 应存在");
        Call(expUI, "ShowExpeditionFromTown", pc);
        yield return null;
        yield return null;

        // 前置：Village Food（Departure 会真实转移为 Carry；NewGame 默认 0 → 需显式补充）
        AddVillageFood(100);

        afterReady(pc, expUI);
        yield return null;
        yield return null;
    }

    private static void AddVillageFood(int amount)
    {
        object resources = GetStaticMember("VillageRuntimeBridge", "Resources");
        Assert.IsNotNull(resources, "Village Resources 应已初始化");
        Call(resources, "AddFood", amount);
    }

    private static void SelectFirstCharacterCard(object pc, object expUI)
    {
        object expedition = GetMember(expUI, "_expedition");
        object firstCard = null;
        foreach (var c in (System.Collections.IEnumerable)GetMember(pc, "OwnedCharacterCards"))
        {
            firstCard = c;
            break;
        }
        Assert.IsNotNull(firstCard, "需要至少一张 owned 角色卡");
        Call(expedition, "AddCharacterCard", firstCard);
    }

    // ==================== WP-E：Current 玩家表面 = Journey（Debug Progression Panel 已退出产品路径） ====================

    /// <summary>Current 玩家表现宿主（真实产品路径）。</summary>
    private static object JourneyView() => FindInstance("ExpeditionJourneyView");

    /// <summary>当次合法后继数量（0 / 1 / N）。</summary>
    private static int AvailableCount(object adventure)
    {
        int count = 0;
        foreach (var _ in (IEnumerable)Call(adventure, "GetAvailableProgressionNodes")) count++;
        return count;
    }

    /// <summary>N=1：真实产品「继续前进」的表现入口（View 的 SkipTravel，等价于点击该按钮）。</summary>
    private static void JourneySkipTravel(object journey) => Call(journey, "SkipTravel");

    /// <summary>统一 Event Card / Option（Battle、普通 Encounter、Branch 共用同一卡面）。</summary>
    private static bool EventCardVisible(object journey) => (bool)GetMember(journey, "IsEventCardVisible");

    private static int EventCardOptionCount(object journey) => (int)GetMember(journey, "EventCardOptionCount");

    private static GameObject EventOption(object journey, int index)
    {
        var card = GetMember(journey, "eventCard");
        if (card == null) return null;
        return FindChildByName(((Component)card).transform, "BtnOption_" + index);
    }

    private static bool ConfirmEventCardIfVisible(object journey, int index = 0)
    {
        if (!EventCardVisible(journey)) return false;
        var go = EventOption(journey, index);
        if (go == null) return false;
        var btn = go.GetComponent<Button>();
        if (btn == null) return false;
        btn.onClick.Invoke();
        return true;
    }

    private static Button JourneyRetreatButton(object journey) => (Button)GetMember(journey, "btnReturn");

    private static GameObject FindChildByName(Transform parent, string name)
    {
        foreach (Transform child in parent)
        {
            if (child.name == name) return child.gameObject;
            var nested = FindChildByName(child, name);
            if (nested != null) return nested;
        }
        return null;
    }

    /// <summary>当次合法后继中「有 elapsed 成本」的索引（Battle / Elite / Boss）；无则 0。</summary>
    private static int PickExpensiveIndex(object adventure)
    {
        int i = 0;
        foreach (var n in (IEnumerable)Call(adventure, "GetAvailableProgressionNodes"))
        {
            string kind = GetMember(n, "NodeKind").ToString();
            if (kind == "Battle" || kind == "Elite" || kind == "Boss") return i;
            i++;
        }
        return 0;
    }

    /// <summary>当次合法后继中「有 elapsed 成本」的节点 Id（Battle / Elite / Boss）；无则 null。</summary>
    private static string ExpensiveNodeId(object adventure)
    {
        foreach (var n in (IEnumerable)Call(adventure, "GetAvailableProgressionNodes"))
        {
            string kind = GetMember(n, "NodeKind").ToString();
            if (kind == "Battle" || kind == "Elite" || kind == "Boss")
                return (string)GetMember(n, "NodeId");
        }
        return null;
    }

    private static string CurrentNodeOf(object adventure) =>
        (string)GetMember(GetMember(adventure, "ProgressionState"), "CurrentNodeId");

    private static string FirstAvailableNodeId(object adventure)
    {
        foreach (var n in (System.Collections.IEnumerable)Call(adventure, "GetAvailableProgressionNodes"))
            return (string)GetMember(n, "NodeId");
        return null;
    }

    /// <summary>
    /// 状态驱动推进（统一 Event Card）：跳到卡面 → 点选当次 Option（Branch 选第 branchIndex 条道路；
    /// Battle 再确认），直到进入目标 Node 或达到帧上限。拒绝（补给不足）时目标不会被进入，由调用方断言原因。
    /// </summary>
    private static IEnumerator AdvanceStep(object adventure, object journey, int branchIndex)
    {
        string target = FirstAvailableNodeId(adventure);
        if (target == null) yield break;

        // WP-Event-1D Gate D2：正式 Event 节点是两步推进（Option → 结果卡「继续」）→ 必须等到已结算；
        // 战斗类节点仍只要求已进入（由 Battle 结果结算）。
        for (int i = 0; i < 40 && !AdvanceComplete(adventure, target); i++)
        {
            if ((bool)GetMember(journey, "IsTraveling"))
            {
                JourneySkipTravel(journey);
                yield return null;
                continue;
            }

            if (EventCardVisible(journey))
            {
                int count = EventCardOptionCount(journey);
                int pick = PickCardOption(journey, count, branchIndex);
                var go = EventOption(journey, pick);
                if (go != null)
                {
                    var btn = go.GetComponent<Button>();
                    if (btn != null) btn.onClick.Invoke();
                }
                yield return null;
                continue;
            }

            yield return null;
        }
    }

    /// <summary>
    /// D2：正式 Event 卡优先选择**确定性非战斗** Option（「离开」= 仅额外 1 天），
    /// 避免 Legacy 推进用例意外进入 Event 内战斗（`StartBattle`）；其余卡面沿用原索引策略。
    /// </summary>
    private static int PickCardOption(object journey, int optionCount, int preferredIndex)
    {
        if (optionCount <= 0) return 0;

        int leave = FindOptionIndexByLabel(journey, "离开");
        if (leave >= 0) return leave;

        return optionCount > 1 ? Mathf.Min(preferredIndex, optionCount - 1) : 0;
    }

    private static int FindOptionIndexByLabel(object journey, string contains)
    {
        int count = EventCardOptionCount(journey);
        for (int i = 0; i < count; i++)
        {
            var go = EventOption(journey, i);
            if (go == null) continue;

            var label = go.transform.Find("Label");
            var text = label != null ? label.GetComponent<TMP_Text>() : null;
            if (text != null && !string.IsNullOrEmpty(text.text) && text.text.Contains(contains)) return i;
        }
        return -1;
    }

    /// <summary>D2 完成判据：非战斗（正式 Event）节点必须已结算；战斗类节点只要求已进入。</summary>
    private static bool AdvanceComplete(object adventure, string target)
    {
        if (CurrentNodeOf(adventure) != target) return false;

        var state = GetMember(adventure, "ProgressionState");
        if ((bool)Call(state, "IsNodeResolved", target)) return true;

        string kind = NodeKindViaGraph(adventure, target);
        return kind == "Battle" || kind == "Elite" || kind == "Boss";
    }

    /// <summary>从 Progression Graph 读取 NodeKind（已进入后不再出现在 available 列表）。</summary>
    private static string NodeKindViaGraph(object adventure, string nodeId)
    {
        var resolver = GetMember(adventure, "Progression");
        var graph = GetMember(GetMember(resolver, "State"), "Graph");
        var tryGetNode = graph.GetType().GetMethod("TryGetNode");
        if (tryGetNode == null) return null;

        var args = new object[] { nodeId, null };
        if (!(bool)tryGetNode.Invoke(graph, args) || args[1] == null) return null;

        return GetMember(args[1], "NodeKind").ToString();
    }

    /// <summary>当次第 index 个合法后继的 NodeKind（用于 elapsed / Supply 断言）。</summary>
    private static string AvailableKind(object adventure, int index)
    {
        int i = 0;
        foreach (var n in (IEnumerable)Call(adventure, "GetAvailableProgressionNodes"))
        {
            if (i == index) return GetMember(n, "NodeKind").ToString();
            i++;
        }
        return null;
    }

    // ==================== 1｜Supply 可见 + 只呈现当次合法选择（无 Legacy 完整地图）+ 消耗可见 ====================

    [UnityTest]
    public IEnumerator Progression_ShowsSupply_AndLegalChoices_WithoutLegacyMap()
    {
        yield return EnterTownAndExpedition((pc, expUI) =>
        {
            SelectFirstCharacterCard(pc, expUI);
            Call(expUI, "OnConfirmExpedition"); // 真实产品确认入口
        });

        var expUI2 = FindInstance("ExpeditionUI");
        var journey = JourneyView();
        Assert.IsNotNull(journey, "远征开始后必须存在 Journey 表现宿主（ExpeditionJourneyView）");
        Assert.IsTrue((bool)GetMember(journey, "IsVisible"), "确认出征后 Journey 必须可见");
        Assert.IsFalse(((GameObject)GetMember(expUI2, "gameObject")).activeSelf,
            "ExpeditionUI 在远征期间隐藏（其 infoText / btnReturn 不可作为玩家表面）");
        var legacyPanel = FindInstance("ExpeditionProgressionPanel");
        if (legacyPanel != null)
            Assert.IsFalse((bool)GetMember(legacyPanel, "IsVisible"), "Debug 推进面板不得与 Journey 同屏");

        var mapUI = FindInstance("MapUI");
        if (mapUI != null)
        {
            var mapCanvas = (GameObject)GetMember(mapUI, "canvasRoot");
            if (mapCanvas != null)
                Assert.IsFalse(mapCanvas.activeSelf, "Legacy MapCanvas 不得作为当前推进主表现（不再展示完整 4×3）");
        }

        StringAssert.Contains("补给：", (string)GetMember(journey, "SupplyDisplay"),
            "Journey HUD 必须显示当前 Supply");

        var adventure = GetMember(expUI2, "_currentAdventure");
        Assert.IsNotNull(adventure, "应存在进行中的 AdventureManager");

        // WP-E：Prep 选中的具体 Person 原样成为 Journey 队伍（每个准确 PersonId 一个 Member View）
        int expectedMembers = ((System.Collections.IEnumerable)Call(adventure, "GetExpeditionPersonIds"))
            .Cast<object>().Count();
        Assert.AreEqual(expectedMembers, (int)GetMember(journey, "PartyMemberCount"),
            "Journey Party 必须显示全部实际远征成员（不得少显示人物）");

        var available = ((System.Collections.IEnumerable)Call(adventure, "GetAvailableProgressionNodes"))
            .Cast<object>().Select(n => (string)GetMember(n, "NodeId")).ToList();

        // 统一 Event Card：N>1 时先跳到 Branch 卡面，Option 数必须等于当次合法后继数（且文案非空）
        if (available.Count > 1)
        {
            JourneySkipTravel(journey);
            yield return null;
            Assert.IsTrue(EventCardVisible(journey), "N>1 时必须显示统一 Branch 卡面");
            Assert.AreEqual(available.Count, EventCardOptionCount(journey),
                "Branch 卡面 Option 数必须等于当次合法后继数（不得来自其它来源）");
            for (int i = 0; i < available.Count; i++)
            {
                var opt = EventOption(journey, i);
                Assert.IsNotNull(opt, "Branch Option " + i + " 必须存在（不得空白）");
                var labelText = opt.GetComponentInChildren<TMP_Text>(true);
                Assert.IsNotNull(labelText, "Branch Option " + i + " 必须有文字");
                Assert.IsFalse(string.IsNullOrWhiteSpace(labelText.text), "Branch Option 文字不得为空白");
            }
        }

        if (available.Count > 0)
        {
            string firstKind = AvailableKind(adventure, 0);
            var supplyOf = GetMember(GetMember(adventure, "ProgressionState"), "Supply");
            int carryBefore = (int)GetMember(supplyOf, "CurrentCarryUnits");

            Assert.IsNotNull(firstKind, "前置：应能读取当次合法后继类型");
            yield return AdvanceStep(adventure, journey, 0);
            yield return null;
            yield return null;

            var state = GetMember(adventure, "ProgressionState");
            string currentId = (string)GetMember(state, "CurrentNodeId");
            if (string.IsNullOrEmpty(currentId))
            {
                int carryNow = (int)GetMember(GetMember(state, "Supply"), "CurrentCarryUnits");
                Assert.Fail("推进后必须进入合法后继节点：" + available[0]
                    + "；Journey 消息=" + (string)GetMember(journey, "MessageDisplay") + " / carry=" + carryNow);
            }
            Assert.AreEqual(available[0], currentId, "推进后必须进入该合法后继节点");

            if (!(bool)GetMember(journey, "IsVisible"))
            {
                // Battle 节点：以 Resolver 直推等价 outcome（Battle 内容由 V4 核心 E2E 覆盖）
                var resolver = GetMember(adventure, "Progression");
                var outcomeType = Type.GetType("ExpeditionNodeOutcome, ExpeditionProgression.Runtime");
                resolver.GetType().GetMethod("TryResolveCurrentNode", new[] { outcomeType })
                    .Invoke(resolver, new object[] { Enum.Parse(outcomeType, "Success") });

                Call(expUI2, "OnReturnFromBattle", true);
                yield return null;
                yield return null;
                yield return null;
                Assert.IsTrue((bool)GetMember(journey, "IsVisible"), "战斗返回后 Journey 必须重新可见");
            }

            int carryAfter = (int)GetMember(GetMember(GetMember(adventure, "ProgressionState"), "Supply"), "CurrentCarryUnits");
            if (firstKind == "Battle" || firstKind == "Elite" || firstKind == "Boss")
            {
                Assert.Less(carryAfter, carryBefore, "elapsed 节点应真实消耗 Supply");
                StringAssert.Contains("补给：" + carryAfter, (string)GetMember(journey, "SupplyDisplay"),
                    "Supply 必须在 Journey 表面上可见地反映消耗");
            }
            else
            {
                Assert.AreEqual(carryBefore, carryAfter, "0 elapsed 占位节点不得消耗 Supply");
            }
        }

        CallStatic("VillageRuntimeBridge", "ResetForTests");
    }

    /// <summary>读当前 Carry（只读；不写回）。</summary>
    private static int Carry(object adventure)
    {
        object state = GetMember(adventure, "ProgressionState");
        object supply = GetMember(state, "Supply");
        return (int)GetMember(supply, "CurrentCarryUnits");
    }


    // ==================== 1B｜Carry 不足：产品入口必须明确拒绝（不得进入无动作会话） ====================

    [UnityTest]
    public IEnumerator Prep_InsufficientCarry_BlocksConfirm_WithVisibleMessage()
    {
        yield return EnterTownAndExpedition((pc, expUI) =>
        {
            // 必须在 helper 的 ResetForTests 之后设置（否则会被重置回产品 Test 值 2）
            SetStaticField(GetType("VillageRuntimeBridge"), "ResourceCarryPerPerson", 0);
            SelectFirstCharacterCard(pc, expUI);
            Call(expUI, "OnConfirmExpedition"); // 真实产品入口
        });

        var expUI2 = FindInstance("ExpeditionUI");
        Assert.IsNotNull(expUI2, "ExpeditionUI 应存在");

        Assert.IsNull(GetMember(expUI2, "_currentAdventure"),
            "V4 Functional Repair：Carry 无法承担第一步成本时不得创建远征会话（必须阻止 Confirm）");

        var identityPanel = GetMember(expUI2, "_identityPanel");
        Assert.IsNotNull(identityPanel, "准备界面应存在补给可见性表面");
        string supplyText = (string)GetMember(identityPanel, "SupplyText");
        StringAssert.Contains("当前补给不足以开始远征", supplyText,
            "必须在准备阶段明确提示当前补给不足以开始远征");
        StringAssert.Contains("第一步预计成本：", supplyText, "必须显示第一步预计成本");

        var confirmBtn = (Button)GetMember(expUI2, "btnConfirm");
        Assert.IsFalse(confirmBtn.interactable,
            "Carry 不足时 Confirm Expedition 必须不可用（不提供无提示的失败入口）");
        Assert.IsTrue(((GameObject)GetMember(expUI2, "gameObject")).activeSelf,
            "被拒绝后应留在准备界面（不隐藏 ExpeditionUI）");

        SetStaticField(GetType("VillageRuntimeBridge"), "ResourceCarryPerPerson", 2);
        CallStatic("VillageRuntimeBridge", "ResetForTests");
    }

    // ==================== 2｜Supply 不足：可见拒绝原因 + Retreat 仍可用并回到 Town ====================

    [UnityTest]
    public IEnumerator Progression_InsufficientSupply_ShowsVisibleReason_AndRetreatReturnsToTown()
    {
        yield return EnterTownAndExpedition((pc, expUI) =>
        {
            // 必须在 helper 的 ResetForTests 之后设置（否则会被重置回产品 Test 值 2）。
            // party 2（英雄 + 1 角色）× carryPerPerson 1 = Carry 2 = 第一个 elapsed 节点成本
            //（elapsed 1 × rate 1 × 2 人），从而可以真实出发、并在推进后被真实耗尽。
            SetStaticField(GetType("VillageRuntimeBridge"), "ResourceCarryPerPerson", 1);
            AddVillageFood(10); // 真实 Village Food → 真实 Carry（不伪造 Carry / 不绕过 Resource Authority）
            SelectFirstCharacterCard(pc, expUI);
            Call(expUI, "OnConfirmExpedition");
        });

        var expUI2 = FindInstance("ExpeditionUI");
        var journey = JourneyView();
        Assert.IsNotNull(journey, "远征期间必须存在 Journey 表现宿主");

        var adventure = GetMember(expUI2, "_currentAdventure");
        Assert.IsNotNull(adventure, "Carry 足以承担第一步时必须能真实出征");
        Assert.AreEqual(2, Carry(adventure), "前置：Carry 应为 2（party 2 × 1）");
        StringAssert.Contains("补给：2", (string)GetMember(journey, "SupplyDisplay"),
            "Supply 必须在 Journey 表面可见");

        // 真实推进直到 Supply 被 elapsed 节点耗尽（优先选 Battle/Elite/Boss：其 elapsed > 0 才会扣 Carry；
        // 纯占位节点 0 elapsed 会被产品立即结算且不扣 Supply）
        for (int i = 0; i < 8 && Carry(adventure) > 0; i++)
        {
            if (AvailableCount(adventure) == 0) break;
            yield return AdvanceStep(adventure, journey, PickExpensiveIndex(adventure));
            if (!(bool)GetMember(journey, "IsVisible"))
            {
                Call(expUI2, "OnReturnFromBattle", true); // 沿用既有 Resolver 结算
                yield return null;
                yield return null;
                yield return null;
            }
        }
        Assert.AreEqual(0, Carry(adventure), "经过 elapsed 节点后 Supply 应被真实耗尽");

        // 推进需要 elapsed 成本的节点 → 必须明确显示 InsufficientSupply（不再「没有反应」）
        // WP-Event-1D-R：拒绝日志是**产品的既定 LogError**（ExpeditionUI.HandleNodeClick 的战斗节点分支）。
        // 因此先按真实 Supply 规则（纯查询 ValidateEnterNode）确认该节点确实不可进入，再**精确声明期望**，
        // 然后单次走同一条真实产品入口（一次调用 = 一条日志；不使用全局 ignoreFailingMessages，也不吞其它 Error）。
        string denialTarget = ExpensiveNodeId(adventure);
        Assert.IsNotNull(denialTarget, "Supply 耗尽后应仍存在需要 elapsed 成本的合法后继（战斗 / 精英 / Boss）");
        Assert.Greater(AvailableCount(adventure), 0, "Supply 耗尽后仍应有合法后继可尝试");

        var battleUIForDenial = FindInstance("BattleUI");
        Assert.IsNotNull(battleUIForDenial, "战斗节点入口需要 BattleUI 宿主");

        var resolverForDenial = GetMember(adventure, "Progression");
        Assert.AreEqual("InsufficientSupply", Call(resolverForDenial, "ValidateEnterNode", denialTarget).ToString(),
            "前置：该节点必须因 Supply 不足而不可进入（纯查询，未进入节点）");

        string nodeBeforeDenial = CurrentNodeOf(adventure);
        LogAssert.Expect(LogType.Error,
            new System.Text.RegularExpressions.Regex("拒绝进入战斗节点.*InsufficientSupply"));
        Call(expUI2, "HandleNodeClick", adventure, denialTarget, battleUIForDenial);
        yield return null;
        yield return null;

        Assert.AreEqual(nodeBeforeDenial, CurrentNodeOf(adventure), "被拒绝的节点不得进入（零 mutation）");
        Assert.AreEqual(0, Carry(adventure), "Supply 不足时不得被扣减或自动补足");

        string denialMsg = (string)GetMember(journey, "MessageDisplay");
        Assert.IsTrue(denialMsg.Contains("InsufficientSupply"),
            "拒绝原因必须写入 Journey Message Host（玩家可见表面）；msg=" + denialMsg);

        // Supply 不足状态下 Retreat 仍可用 → 走既有 Return 事务回到 Town（不得软锁死）
        var retreat = JourneyRetreatButton(journey);
        Assert.IsNotNull(retreat, "Retreat 按钮必须存在");
        Assert.IsTrue(retreat.interactable, "Retreat 按钮必须可用");
        retreat.onClick.Invoke();
        yield return null;
        yield return null;

        var town = FindInstance("TownUI");
        Assert.IsNotNull(town, "TownUI 应存在");
        Assert.IsTrue(((GameObject)GetMember(town, "canvasRoot")).activeSelf, "Retreat 后应回到 Town");

        object record = GetStaticMember("VillageRuntimeBridge", "ActiveReturnRecord");
        Assert.IsNotNull(record, "应存在 Return 会话记录");
        Assert.AreEqual("Completed", GetMember(record, "Phase").ToString(),
            "Retreat 必须走完既有 Return 事务（Settlement → Catch-up → Carry → Consequence → Clear → Final Save）");
        var expeditionIds = (System.Collections.IEnumerable)GetStaticMember("VillageRuntimeBridge", "ExpeditionPersonIds");
        Assert.AreEqual(0, expeditionIds.Cast<object>().Count(), "Return 后 Expedition Occupancy 应清除");

        SetStaticField(GetType("VillageRuntimeBridge"), "ResourceCarryPerPerson", 2);
        CallStatic("VillageRuntimeBridge", "ResetForTests");
    }
}
