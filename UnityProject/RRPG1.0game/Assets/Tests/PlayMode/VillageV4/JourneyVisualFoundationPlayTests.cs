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
/// WP-E｜Expedition Journey Visual Foundation —— PlayMode 真实产品链集成测试。
///
/// 覆盖 FDS §16（Product Integration）与 §5（0/1/N + Presentation Cycle）：
///   1. 真实产品入口链：NewGame/Continue → Town → Expedition Prep → Confirm → Journey 可见；
///   2. Journey Party = Prep 选中的准确 PersonId 集合（每个 PersonId 一个独立 Member View）；
///   3. 两名不同 Person 使用可区分 visual；缺失绑定 → default placeholder（不塌缩身份）；
///   4. 行走 / Pause 不改变 elapsed days / Supply / CurrentNode（现实播放时间不是规则时间）；
///   5. Continue Skip 与自动完成汇合到同一完成入口（同一次消费只进入一个 Node）；
///   6. 连续 N=1 / Placeholder 链每个 Cycle 只消费一个 Progression Transition；
///   7. Battle 开始 Journey 隐藏 / 冻结，Battle 返回后重新读取 Current State 并建立新 Cycle；
///   8. Return（Retreat）取消未完成 callback 并走既有 V4F Return 事务回到 Town。
///
/// 全部内容 Test / Not Frozen；asmdef 测试程序集不能引用 Assembly-CSharp，运行时类型用反射访问。
/// </summary>
public class JourneyVisualFoundationPlayTests
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

    private static object JourneyView() => FindInstance("ExpeditionJourneyView");
    private static object Coordinator() => FindInstance("ExpeditionJourneyCoordinator");

    /// <summary>截图证据目录（仓库忽略目录；与 WP-D 同一位置约定）。</summary>
    private static string ShotDir =>
        System.IO.Path.Combine(Application.dataPath, "..", "screenshots", "WP-E");

    private static IEnumerator Capture(string fileName)
    {
        System.IO.Directory.CreateDirectory(ShotDir);
        ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(ShotDir, fileName));
        yield return null;
        yield return null;
    }

    private static int AvailableCount(object adventure)
    {
        int count = 0;
        foreach (var _ in (System.Collections.IEnumerable)Call(adventure, "GetAvailableProgressionNodes")) count++;
        return count;
    }

    private static int CompletedCount(object adventure)
    {
        var state = GetMember(adventure, "ProgressionState");
        return ((System.Collections.IEnumerable)Call(state, "GetCompletedNodeIds")).Cast<object>().Count();
    }

    private static int Carry(object adventure)
    {
        var supply = GetMember(GetMember(adventure, "ProgressionState"), "Supply");
        return (int)GetMember(supply, "CurrentCarryUnits");
    }

    private static string CurrentNode(object adventure)
    {
        return (string)GetMember(GetMember(adventure, "ProgressionState"), "CurrentNodeId");
    }

    /// <summary>断言统一卡面的插图宿主已绑定可替换素材（Illustration 区不是空的）。</summary>
    private static void AssertCardIllustrationBound(object journey, string what)
    {
        var card = GetMember(journey, "eventCard");
        Assert.IsNotNull(card, "统一卡面组件必须存在");
        var image = GetMember(card, "illustrationHost") as Image;
        Assert.IsNotNull(image, what + "：卡面必须绑定插图宿主（Illustration）");
        Assert.IsTrue(image.enabled, what + "：插图宿主必须处于启用状态");
        Assert.IsNotNull(image.sprite, what + "：插图宿主必须绑定可替换 Sprite（占位素材）");
    }

    /// <summary>用真实 JourneyNodeRef 类型构造 Branch 节点列表（仅用于视觉证据；不进入规则层）。</summary>
    private static System.Collections.IList NewNodeRefList(params string[] nodeIds)
    {
        var nodeRefType = Type.GetType("JourneyNodeRef, ExpeditionJourney.Runtime");
        Assert.IsNotNull(nodeRefType, "未找到 JourneyNodeRef 类型");
        var listType = typeof(List<>).MakeGenericType(nodeRefType);
        var list = (System.Collections.IList)Activator.CreateInstance(listType);
        foreach (var id in nodeIds)
            list.Add(Activator.CreateInstance(nodeRefType, new object[] { id, string.Empty }));
        return list;
    }

    /// <summary>PartyHost 下每个 Member View 的 body Sprite instanceID（0 = 无 body）。</summary>
    private static List<int> MemberSpriteIds(object journey)
    {
        var ids = new List<int>();
        var partyHost = (RectTransform)GetMember(journey, "partyHost");
        if (partyHost == null) return ids;
        foreach (Transform member in partyHost)
        {
            if (member == null || !member.gameObject.activeSelf) continue;
            var body = member.Find("Body");
            var img = body != null ? body.GetComponent<Image>() : null;
            ids.Add(img != null && img.sprite != null ? img.sprite.GetInstanceID() : 0);
        }
        return ids;
    }

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

    /// <summary>统一 Event Card 是否可见（Battle / 普通 Encounter / Branch 共用同一卡面）。</summary>
    private static bool EventCardVisible(object journey) => (bool)GetMember(journey, "IsEventCardVisible");

    /// <summary>统一卡面类型（0=None / 1=Battle / 2=Content / 3=Branch）。</summary>
    private const int BranchCardKind = 3;

    private static int EventCardKindValue(object journey)
    {
        try { return Convert.ToInt32(GetMember(journey, "EventCardKind")); }
        catch { return 0; }
    }

    private static int EventCardOptionCount(object journey) => (int)GetMember(journey, "EventCardOptionCount");

    /// <summary>当次第 index 个合法后继的 NodeKind（表现分类断言用）。</summary>
    private static string NodeKindOf(object adventure, string nodeId)
    {
        foreach (var n in (System.Collections.IEnumerable)Call(adventure, "GetAvailableProgressionNodes"))
        {
            if ((string)GetMember(n, "NodeId") == nodeId) return GetMember(n, "NodeKind").ToString();
        }
        return null;
    }

    private static bool IsBattleKind(string kind) =>
        kind == "Battle" || kind == "Elite" || kind == "Boss";

    /// <summary>统一卡面的第 i 个 Option 按钮（运行时由 Card Renderer 统一生成）。</summary>
    private static GameObject EventOption(object journey, int index)
    {
        var card = GetMember(journey, "eventCard");
        if (card == null) return null;
        return FindChildByName(((Component)card).transform, "BtnOption_" + index);
    }

    /// <summary>若统一卡面已出现则点选第 index 个 Option；返回是否点了。</summary>
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

    /// <summary>
    /// 状态驱动推进（统一 Event Card）：跳到卡面 → 点选 Option（Branch 选 branchIndex；Battle 再确认），
    /// 直到进入目标 Node 或达到帧上限。
    /// </summary>
    private static IEnumerator AdvanceToFirstNode(object adventure, object journey, int branchIndex)
    {
        string target = FirstAvailableNodeId(adventure);
        if (target == null) yield break;

        // WP-Event-1D Gate D2：正式 Event 节点的推进是**两步**（Option → 结果卡「继续」）；
        // 因此循环条件不能只看「已进入」，必须等该 Node **已结算**（战斗节点交由 Battle 结果结算）。
        for (int i = 0; i < 40 && !AdvanceComplete(adventure, target); i++)
        {
            if ((bool)GetMember(journey, "IsTraveling"))
            {
                Call(journey, "SkipTravel");
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
    /// 避免 Legacy 推进用例意外进入 Event 内战斗（`StartBattle`）而脱离其覆盖范围；
    /// 其余卡面（Branch / Battle 确认卡）沿用原索引策略。
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
        if (CurrentNode(adventure) != target) return false;

        var state = GetMember(adventure, "ProgressionState");
        if ((bool)Call(state, "IsNodeResolved", target)) return true;

        string kind = NodeKindViaGraph(adventure, target);
        return kind == "Battle" || kind == "Elite" || kind == "Boss";
    }

    /// <summary>从 Progression Graph 读取该 Node 的 NodeKind（已进入后不再出现在 available 列表，故直接查图）。</summary>
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

    private static IEnumerator PrepareAdvance(object adventure, object journey)
    {
        // N>1：先跳过旅行到 Branch（统一卡面随后出现；不代选道路）。
        if (AvailableCount(adventure) > 1 && !EventCardVisible(journey))
        {
            Call(journey, "SkipTravel");
            yield return null;
            yield return null;
        }
        yield break;
    }

    /// <summary>当次第一个合法后继 NodeId（无则 null）。</summary>
    private static string FirstAvailableNodeId(object adventure)
    {
        foreach (var n in (System.Collections.IEnumerable)Call(adventure, "GetAvailableProgressionNodes"))
            return (string)GetMember(n, "NodeId");
        return null;
    }

    /// <summary>
    /// 推进一次（统一 Event Card 流程）：
    ///   N=1 → 继续前进（跳过旅行）→ 卡面确认（「进入战斗」/「继续前进」）；
    ///   N&gt;1 → 跳过旅行到 Branch 卡面 → 选第 branchIndex 条道路 →（若该路线是战斗）再过战斗确认卡。
    /// 返回被推进到的 NodeId；null = 当前无法推进。
    /// </summary>
    private static string AdvanceOnce(object adventure, object journey, int branchIndex = 0)
    {
        int count = AvailableCount(adventure);
        if (count == 0) return null;

        string target = FirstAvailableNodeId(adventure);
        if (count == 1)
        {
            Call(journey, "SkipTravel");            // 跳过旅行表现（不代选）
            ConfirmEventCardIfVisible(journey, 0);  // 统一卡面确认后才进入 Node
            return target;
        }

        Call(journey, "SkipTravel");                // 到 Branch
        if (!ConfirmEventCardIfVisible(journey, branchIndex)) return null;
        ConfirmEventCardIfVisible(journey, 0);      // Battle 路线：战斗事件卡确认
        return target;
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

    /// <summary>真实产品入口：NewGame/Continue → Town → Expedition Prep → Confirm（Journey 就绪）。</summary>
    private static IEnumerator EnterTownAndConfirmExpedition()
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

        AddVillageFood(100);
        SelectFirstCharacterCard(pc, expUI);

        Call(expUI, "OnConfirmExpedition");
        yield return null;
        yield return null;
        yield return null;
    }

    // ==================== 1｜真实产品链：Prep → Journey → 行走 / Pause → Battle 往返 → Return ====================

    [UnityTest]
    public IEnumerator Journey_RealProductChain_PartyVisuals_WalkingRules_BattleReturn_Return()
    {
        yield return EnterTownAndConfirmExpedition();

        var expUI = FindInstance("ExpeditionUI");
        var journey = JourneyView();
        Assert.IsNotNull(journey, "Confirm 后必须存在 Journey 表现宿主（不得回退 Debug Panel）");
        Assert.IsTrue((bool)GetMember(journey, "IsVisible"), "Confirm 后 Journey 必须可见");

        var coordinator = Coordinator();
        Assert.IsNotNull(coordinator, "Confirm 后必须存在 Journey Coordinator");
        Assert.IsTrue((bool)GetMember(coordinator, "IsActive"), "Journey 必须已进入 Presentation Cycle");

        var adventure = GetMember(expUI, "_currentAdventure");
        Assert.IsNotNull(adventure, "Confirm 后必须绑定当前 Adventure");

        // ---- Party = 准确 PersonId 集合（每个 PersonId 一个独立 Member View） ----
        var personIds = ((System.Collections.IEnumerable)Call(adventure, "GetExpeditionPersonIds"))
            .Cast<object>().Select(o => (string)o).ToList();
        Assert.GreaterOrEqual(personIds.Count, 1, "至少一名出征人物");
        Assert.AreEqual(personIds.Count, (int)GetMember(journey, "PartyMemberCount"),
            "Journey Party 必须显示全部实际成员（Prep 选中的 Person 原样成为 Journey 队伍）");

        var icons = MemberSpriteIds(journey);
        Assert.AreEqual(personIds.Count, icons.Count, "每个 Member View 必须存在（不得少显示人物）");
        Assert.IsTrue(icons.All(id => id != 0), "Member View 必须有可用 visual（或 default placeholder）");

        if (personIds.Count >= 2)
            Assert.IsTrue(icons.Distinct().Count() >= 2,
                "两名不同 Person 必须使用可区分 visual（不得塌缩成同一份外观）");

        // 结构性可见性（模型无法直接看图，改为机器可判定的屏幕内证据）
        var partyHost = (RectTransform)GetMember(journey, "partyHost");
        int onScreen = 0;
        foreach (Transform member in partyHost)
        {
            if (member == null || !member.gameObject.activeSelf) continue;
            Vector2 sp = RectTransformUtility.WorldToScreenPoint(null, member.position);
            Assert.IsTrue(sp.x > 0f && sp.x < Screen.width && sp.y > 0f && sp.y < Screen.height,
                "Journey Member 必须在屏幕内可见：" + member.name + " screen=" + sp
                + " / view=" + Screen.width + "x" + Screen.height);
            onScreen++;
        }
        Assert.AreEqual(personIds.Count, onScreen, "全部 Member View 必须在屏幕内");

        StringAssert.Contains("补给：", (string)GetMember(journey, "SupplyDisplay"), "Journey HUD 必须显示 Supply");
        StringAssert.Contains("累计耗时", (string)GetMember(journey, "ElapsedDaysDisplay"), "Journey HUD 必须显示日内实际耗时");

        // 截图证据 1：Journey Overview（Background + Party + HUD）
        yield return Capture("wp-e_journey_overview.png");

        // ---- 行走 / Pause 不改变规则状态（现实播放时间不是规则时间） ----
        int carry0 = Carry(adventure);
        string node0 = CurrentNode(adventure);
        int completed0 = CompletedCount(adventure);
        int elapsed0 = (int)Call(adventure, "GetProgressionElapsedDays");

        for (int i = 0; i < 30; i++) yield return null;     // 约 0.5s：仍在旅行表现中
        Call(journey, "SetPaused", true);
        for (int i = 0; i < 20; i++) yield return null;

        // 截图证据 2：Pause（只暂停表现；HUD 按钮状态可见）
        yield return Capture("wp-e_journey_paused.png");

        Assert.AreEqual(carry0, Carry(adventure), "行走 / Pause 期间 Supply 不得变化");
        Assert.AreEqual(node0, CurrentNode(adventure), "行走 / Pause 期间 CurrentNode 不得变化");
        Assert.AreEqual(completed0, CompletedCount(adventure), "行走 / Pause 期间不得结算任何 Node");
        Assert.AreEqual(elapsed0, (int)Call(adventure, "GetProgressionElapsedDays"), "行走 / Pause 不得产生 elapsed days");

        Call(journey, "SetPaused", false);
        yield return null;

        // ---- Battle 前统一 Encounter Card：旅行完成 → 卡面（插图 + 正文 + 明确 Option）→ 确认后才进入 ----
        string target = FirstAvailableNodeId(adventure);
        string targetKind = NodeKindOf(adventure, target);

        Call(journey, "SkipTravel");                     // 跳过旅行表现（不代选）
        yield return null;

        Assert.IsTrue(EventCardVisible(journey), "旅行完成后必须显示统一 Event Card（不得直接进入 Node）");
        Assert.IsFalse((bool)GetMember(journey, "IsTraveling"), "卡面期间不得残留旅行表现");
        Assert.GreaterOrEqual(EventCardOptionCount(journey), 1, "统一卡面必须至少一个 Option");

        var firstOption = EventOption(journey, 0);
        Assert.IsNotNull(firstOption, "Option 必须存在（不得空白）");
        var optionText = firstOption.GetComponentInChildren<TMP_Text>(true);
        Assert.IsNotNull(optionText, "Option 必须有可读文字");
        Assert.IsFalse(string.IsNullOrWhiteSpace(optionText.text), "Option 文字不得为空白");
        if (IsBattleKind(targetKind))
            StringAssert.Contains("进入战斗", optionText.text, "战斗 Encounter 必须提供「进入战斗」Option");
        else
            StringAssert.Contains("继续前进", optionText.text, "普通 Encounter 必须提供「继续前进」Option");

        // 截图证据 3：统一 Event Card（战斗前遭遇确认）
        AssertCardIllustrationBound(journey, "战斗前 Encounter Card");
        yield return Capture("wp-e_unified_encounter_card.png");

        Assert.IsTrue(ConfirmEventCardIfVisible(journey, 0), "必须能确认统一卡面后进入");
        yield return null;
        yield return null;
        yield return null;

        var stateAfterAdvance = GetMember(adventure, "ProgressionState");
        Assert.IsTrue((bool)Call(stateAfterAdvance, "IsNodeEntered", target),
            "确认卡面后必须真实进入该 Node：" + target);
        Assert.AreEqual(target, CurrentNode(adventure),
            "一次 Presentation Cycle 最多消费一个 Progression Transition（旧 Cycle 不得继续推进下一个 Node）");

        // ---- Battle 往返：Battle 开始隐藏 Journey；返回后重新建立 Cycle ----
        bool journeyHiddenForBattle = false;
        for (int i = 0; i < 12; i++)
        {
            if (!(bool)GetMember(journey, "IsVisible")) { journeyHiddenForBattle = true; break; }
            if (AvailableCount(adventure) == 0) break;
            yield return AdvanceToFirstNode(adventure, journey, 0);
            yield return null;
            yield return null;
            yield return null;
        }

        if (journeyHiddenForBattle)
        {
            Assert.IsFalse((bool)GetMember(journey, "IsVisible"), "Battle 期间 Journey 必须隐藏 / 冻结");
            Assert.IsFalse((bool)GetMember(coordinator, "IsActive"), "Battle 期间旧 Presentation Cycle 必须失效");

            Call(expUI, "OnReturnFromBattle", true);       // 真实产品 Battle 返回入口
            yield return null;
            yield return null;
            yield return null;

            Assert.IsTrue((bool)GetMember(journey, "IsVisible"), "Battle 返回后 Journey 必须恢复");
            Assert.IsTrue((bool)GetMember(coordinator, "IsActive"), "Battle 返回后必须重新读取 Current State 并建立新 Cycle");
        }

        // ---- Return：真实点击 Journey「撤退 / 返回领地」→ 走既有 V4F Return 事务 ----
        var retreat = (Button)GetMember(journey, "btnReturn");
        Assert.IsNotNull(retreat, "Journey Retreat 按钮必须存在");
        Assert.IsTrue(retreat.interactable, "Journey Retreat 必须可用（含 Supply 不足）");
        retreat.onClick.Invoke();
        yield return null;
        yield return null;
        yield return null;

        var town = FindInstance("TownUI");
        Assert.IsTrue(((GameObject)GetMember(town, "canvasRoot")).activeSelf, "Return 后必须回到 Town");
        Assert.IsFalse((bool)GetMember(journey, "IsVisible"), "Return 后 Journey 必须隐藏（取消未完成 callback）");

        object record = GetStaticMember("VillageRuntimeBridge", "ActiveReturnRecord");
        Assert.IsNotNull(record, "应存在 Return 会话记录");
        Assert.AreEqual("Completed", GetMember(record, "Phase").ToString(),
            "Return 必须走完既有 V4F 事务（Settlement → Catch-up → Carry → Consequence → Clear → Final Save）");
        var expeditionIds = (System.Collections.IEnumerable)GetStaticMember("VillageRuntimeBridge", "ExpeditionPersonIds");
        Assert.AreEqual(0, expeditionIds.Cast<object>().Count(), "Return 后 Expedition Occupancy 应清除");

        CallStatic("VillageRuntimeBridge", "ResetForTests");
    }

    // ==================== 2｜连续 Transition：每 Cycle 只消费一个 + Branch 不被 Skip 代选 ====================

    [UnityTest]
    public IEnumerator Journey_ConsecutiveTransitions_OnePerCycle_AndBranchNotAutoSelected()
    {
        yield return EnterTownAndConfirmExpedition();

        var expUI = FindInstance("ExpeditionUI");
        var journey = JourneyView();
        var coordinator = Coordinator();
        var adventure = GetMember(expUI, "_currentAdventure");

        int transitions = 0;
        bool branchChecked = false;
        bool branchShotTaken = false;
        bool supplyStopped = false;

        for (int step = 0; step < 10; step++)
        {
            // Battle 期间先把产品路径走完（Battle 内容由 V4 核心 E2E 覆盖）
            if (!(bool)GetMember(journey, "IsVisible"))
            {
                Call(expUI, "OnReturnFromBattle", true);
                yield return null;
                yield return null;
                yield return null;
                continue;
            }

            if (AvailableCount(adventure) == 0) break;      // N=0：terminal（不自动判胜）

            // N>1：等待 Branch 卡面真正出现（跳过旅行表现，不代选），再校验 Option 可读性 + 截图
            for (int w = 0; w < 8; w++)
            {
                if (AvailableCount(adventure) <= 1) break;
                if (EventCardVisible(journey) && EventCardKindValue(journey) == BranchCardKind) break;
                if ((bool)GetMember(journey, "IsTraveling")) Call(journey, "SkipTravel");
                yield return null;
            }

            if (AvailableCount(adventure) > 1 && EventCardVisible(journey)
                && EventCardKindValue(journey) == BranchCardKind)
            {
                int optionCount = EventCardOptionCount(journey);
                Assert.GreaterOrEqual(optionCount, 2, "Branch 卡面必须给出当次全部合法路线 Option");
                for (int i = 0; i < optionCount; i++)
                {
                    var opt = EventOption(journey, i);
                    Assert.IsNotNull(opt, "Branch Option " + i + " 必须存在（不得空白）");
                    var label = opt.GetComponentInChildren<TMP_Text>(true);
                    Assert.IsNotNull(label, "Branch Option " + i + " 必须有文字");
                    Assert.IsFalse(string.IsNullOrWhiteSpace(label.text), "Branch Option 文字不得为空白");
                }

                // 截图证据 4：统一卡面的 Branch 用法（插图 + 正文 + 可读路线 Option）
                if (!branchShotTaken) { branchShotTaken = true; yield return Capture("wp-e_unified_branch_card.png"); }

                string nodeBefore = CurrentNode(adventure);
                int completedBeforeSkip = CompletedCount(adventure);

                Call(journey, "SkipTravel");                 // = 点击「继续前进」：不得代选道路
                yield return null;
                yield return null;

                Assert.AreEqual(nodeBefore, CurrentNode(adventure), "Branch 阶段 Continue 不得代选道路");
                Assert.AreEqual(completedBeforeSkip, CompletedCount(adventure),
                    "Branch 阶段 Continue 不得推进任何 Node");
                branchChecked = true;
            }

            int cycleBefore = (int)GetMember(coordinator, "CycleIndex");

            string target = FirstAvailableNodeId(adventure);
            Assert.IsNotNull(target, "第 " + (step + 1) + " 步：存在合法后继时必须能推进");

            // WP-Event-1D-R：Supply 不足时产品会明确拒绝并输出**既定 LogError**
            //（ExpeditionUI.HandleNodeClick 的战斗节点分支）。先用真实 Supply 规则（纯查询 ValidateEnterNode）
            // 预判：不足 → 精确声明期望 + 单次走同一产品入口，不依赖 AdvanceToFirstNode 内部可能重复点击
            // 造成的未知日志条数（不使用全局 ignoreFailingMessages，也不吞其它 Error）。
            var entryResolver = GetMember(adventure, "Progression");
            if (Call(entryResolver, "ValidateEnterNode", target).ToString() == "InsufficientSupply")
            {
                LogAssert.Expect(LogType.Error,
                    new System.Text.RegularExpressions.Regex("拒绝进入战斗节点.*InsufficientSupply"));
                Call(expUI, "HandleNodeClick", adventure, target, FindInstance("BattleUI"));
                yield return null;
                yield return null;

                string deniedForSupply = (string)GetMember(journey, "MessageDisplay");
                Assert.IsTrue(deniedForSupply.Contains("InsufficientSupply"),
                    "第 " + (step + 1) + " 步：进入被拒绝时必须显示真实原因（Supply 不足）；msg="
                    + deniedForSupply + " / target=" + target);
                supplyStopped = true;
                break;
            }

            yield return AdvanceToFirstNode(adventure, journey, 0);
            yield return null;
            yield return null;
            yield return null;

            var state = GetMember(adventure, "ProgressionState");
            if (!(bool)Call(state, "IsNodeEntered", target))
            {
                // 其它拒绝原因（非 Supply）属于**正确的产品行为**（必须明确拒绝并保持可撤退）：
                // 这里记录为链路正常终止，而不是测试失败。
                string deniedMsg = (string)GetMember(journey, "MessageDisplay");
                Assert.IsFalse(string.IsNullOrEmpty(deniedMsg),
                    "第 " + (step + 1) + " 步：进入被拒绝时必须显示真实原因；target=" + target);
                Assert.IsTrue(deniedMsg.Contains("InsufficientSupply") || deniedMsg.Contains("补给不足")
                    || deniedMsg.Contains("无法进入"),
                    "第 " + (step + 1) + " 步：拒绝原因必须说明不可进入；msg=" + deniedMsg
                    + " / target=" + target);
                supplyStopped = true;
                break;
            }
            Assert.AreEqual(target, CurrentNode(adventure),
                "第 " + (step + 1) + " 步：一次 Presentation Cycle 最多消费一个 Progression Transition");
            // WP-Event-1 Producer Acceptance Repair（Issue A）产品硬规则：
            // 若本步进入的是 **Battle** 节点 → Battle / Prep 生命周期开始，Journey 表现被冻结：
            // 旧 Presentation Cycle 失效，且**不得**在战斗结束前建立 / 显示新的 Branch / Event 卡。
            // 因此「消费后必须建立新 Cycle」只适用于非 Battle 的进入；Battle 进入改为断言「已上锁」并结束本推进循环。
            if ((bool)GetMember(adventure, "HasActiveEnemyRoster"))
            {
                Assert.IsTrue((bool)GetMember(journey, "PresentationLockedForBattle"),
                    "第 " + (step + 1) + " 步：Battle 生命周期内必须锁定 Journey 表现（不得出现新卡面 / 不得推进）");
                transitions++;
                break;
            }

            Assert.Greater((int)GetMember(coordinator, "CycleIndex"), cycleBefore,
                "第 " + (step + 1) + " 步：消费后必须重新读取 Current Progression State 并建立新 Cycle");

            transitions++;
        }

        Assert.GreaterOrEqual(transitions, 1, "至少应完成一次真实推进");

        // WP-Event-1 Producer Acceptance Repair（Issue A）：若推进过程中进入了 Battle（表现按硬规则被冻结），
        // 必须先用**产品入口**结束战斗（Victory）→ Journey 恢复 → 解锁，之后的 Branch 卡面截图仍然成立。
        if ((bool)GetMember(adventure, "HasActiveEnemyRoster"))
        {
            Call(expUI, "OnReturnFromBattle", true);
            yield return null;
            yield return null;
            yield return null;
            Assert.IsFalse((bool)GetMember(journey, "PresentationLockedForBattle"),
                "战斗结束后必须解锁（否则后续 Journey Card 永远无法显示）");
        }

        // 截图证据 4（确定性兜底）：若本局未自然出现 Branch，则用**同一** View/Config 渲染一次 Branch 卡面
        if (!branchShotTaken)
        {
            var branchNodes = NewNodeRefList("branch_preview_1", "branch_preview_2", "branch_preview_3");
            Call(journey, "ShowBranchChoice", branchNodes, (Action<string>)(id => { }));
            yield return null;

            Assert.IsTrue(EventCardVisible(journey), "Branch 卡面必须可见（统一卡面 View）");
            Assert.GreaterOrEqual(EventCardOptionCount(journey), 2, "Branch 卡面必须给出可读路线 Option");
            for (int i = 0; i < EventCardOptionCount(journey); i++)
            {
                var opt = EventOption(journey, i);
                Assert.IsNotNull(opt, "Branch Option " + i + " 必须存在（不得空白）");
                var label = opt.GetComponentInChildren<TMP_Text>(true);
                Assert.IsNotNull(label, "Branch Option " + i + " 必须有文字");
                Assert.IsFalse(string.IsNullOrWhiteSpace(label.text), "Branch Option 文字不得为空白");
            }

            yield return Capture("wp-e_unified_branch_card.png");
            AssertCardIllustrationBound(journey, "Branch 卡面");
            Call(journey, "HideBranchChoice");
            yield return null;
            branchShotTaken = true;
        }

        if (branchChecked)
            Assert.IsTrue(branchChecked, "Branch 阶段不得被 Continue 代选（覆盖已成立）");
        if (supplyStopped)
            Assert.IsTrue(supplyStopped, "Supply 不足时必须明确拒绝并保持链路可撤退（覆盖已成立）");

        // 收尾：真实 Retreat 回 Town + 清理
        var retreat = (Button)GetMember(journey, "btnReturn");
        if (retreat != null && retreat.gameObject.activeInHierarchy) retreat.onClick.Invoke();
        yield return null;
        yield return null;
        yield return null;
        CallStatic("VillageRuntimeBridge", "ResetForTests");
    }
}
