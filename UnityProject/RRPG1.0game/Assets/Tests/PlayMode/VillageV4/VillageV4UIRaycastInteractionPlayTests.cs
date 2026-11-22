using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

/// <summary>
/// V4 Functional Repair｜**真实 Pointer / Raycast 交互验收**（Disposable Development Test / Test / Not Frozen）。
///
/// 目的（人工真实 PlayMode 发现的缺口）：
///   上一轮测试只在 `Button.onClick` / callback 级别验证，无法证明「玩家鼠标真的点得到」。
///   本轮改为：
///     1) 取按钮屏幕中心点 → `EventSystem.current.RaycastAll` → 断言 **top hit 属于预期 Button**；
///     2) 用 `ExecuteEvents` 真实派发 PointerClick（经 EventSystem 命中的 handler，而非 onClick.Invoke）；
///     3) 断言真实点击产生了 Progression / Retreat 的产品行为。
///   同时诊断并验证：Modal 关闭后不得留下不可见全屏 Raycast blocker。
/// </summary>
public class VillageV4UIRaycastInteractionPlayTests
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

    private static object StaticMember(string typeName, string name)
    {
        var t = GetType(typeName);
        var f = t.GetField(name, F);
        if (f != null) return f.GetValue(null);
        var p = t.GetProperty(name, F);
        Assert.IsNotNull(p, "静态成员不存在：" + typeName + "." + name);
        return p.GetValue(null, null);
    }

    // 注意：VillageRuntimeBridge / UI 均在 Assembly-CSharp（本测试程序集不引用）→ 统一用反射访问；
    // VillageTime / VillageResourceDayResult 等在 Village.Runtime，可直接引用类型。
    private static VillageTime BridgeTime() => (VillageTime)StaticMember("VillageRuntimeBridge", "Time");

    private static int VillageFood() =>
        (int)GetMember(StaticMember("VillageRuntimeBridge", "Resources"), "Food");

    private static void AddVillageFood(int amount) =>
        Call(StaticMember("VillageRuntimeBridge", "Resources"), "AddFood", amount);

    // ==================== 真实 Pointer / Raycast 诊断 ====================

    /// <summary>RectTransform 中心点对应的屏幕坐标（Overlay Canvas 用 null 相机）。</summary>
    private static Vector2 ScreenCenter(RectTransform rt)
    {
        var canvas = rt.GetComponentInParent<Canvas>();
        Camera cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? canvas.worldCamera : null;
        return RectTransformUtility.WorldToScreenPoint(cam, rt.TransformPoint(rt.rect.center));
    }

    private static List<RaycastResult> RaycastAllAt(Vector2 screenPoint)
    {
        var es = EventSystem.current;
        Assert.IsNotNull(es, "场景中必须存在 EventSystem（真实 Pointer 诊断前提）");
        var ped = new PointerEventData(es) { position = screenPoint };
        var results = new List<RaycastResult>();
        es.RaycastAll(ped, results);
        return results;
    }

    /// <summary>按顺序输出命中链（GameObject / Canvas / sortingOrder / raycastTarget / blocksRaycasts / interactable / activeInHierarchy）。</summary>
    private static string DumpHits(List<RaycastResult> hits)
    {
        if (hits == null || hits.Count == 0) return "（无任何 Raycast 命中）";
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < hits.Count; i++)
        {
            var go = hits[i].gameObject;
            if (go == null) { sb.AppendLine(i + ". <null>"); continue; }
            var canvas = go.GetComponentInParent<Canvas>();
            var graphic = go.GetComponent<Graphic>();
            var group = go.GetComponentInParent<CanvasGroup>();
            var btn = go.GetComponentInParent<Button>();
            sb.AppendLine(string.Format(
                "{0}. {1} depth={2} | Canvas={3} sortingOrder={4} | raycastTarget={5} | blocksRaycasts={6} | interactable={7} | activeInHierarchy={8}",
                i, go.name, hits[i].depth,
                canvas != null ? canvas.name : "null",
                canvas != null ? canvas.sortingOrder : -9999,
                graphic != null ? graphic.raycastTarget.ToString() : "n/a",
                group != null ? group.blocksRaycasts.ToString() : "n/a",
                btn != null ? btn.interactable.ToString() : "n/a",
                go.activeInHierarchy));
        }
        return sb.ToString();
    }

    private static void AssertTopHitIs(RectTransform target, string what, Vector2 point, List<RaycastResult> hits)
    {
        Assert.Greater(hits.Count, 0,
            what + "：屏幕点 " + point + " 处 RaycastAll 无任何命中（常见原因：该 Canvas 缺少 GraphicRaycaster）");
        var top = hits[0];
        bool belongs = top.gameObject == target.gameObject
            || top.gameObject.transform.IsChildOf(target.transform);
        Assert.IsTrue(belongs,
            what + "：top raycast hit 不是目标按钮（玩家鼠标点到的是别人）\n命中链：\n" + DumpHits(hits));
    }

    /// <summary>诊断输出：把真实命中链写入 Console（供报告/人工复核）。</summary>
    private static void LogHits(string what, Vector2 point, List<RaycastResult> hits)
    {
        Debug.Log("[Raycast 诊断] " + what + " @ " + point + " → 命中 " + hits.Count + " 个\n" + DumpHits(hits));
    }

    /// <summary>真实派发 PointerClick（经 EventSystem 命中的 handler，等价玩家鼠标点击）。</summary>
    private static GameObject RealPointerClick(Vector2 screenPoint, string what)
    {
        var es = EventSystem.current;
        Assert.IsNotNull(es, "EventSystem 必须存在");
        var ped = new PointerEventData(es)
        {
            position = screenPoint,
            button = PointerEventData.InputButton.Left,
            clickCount = 1
        };
        var hits = new List<RaycastResult>();
        es.RaycastAll(ped, hits);
        Assert.Greater(hits.Count, 0, what + "：点击位置没有任何可命中对象\n" + DumpHits(hits));

        var handler = ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject);
        Assert.IsNotNull(handler,
            what + "：top hit 及其父链没有任何 IPointerClickHandler（玩家点击不会触发任何按钮）\n命中链：\n" + DumpHits(hits));
        bool executed = ExecuteEvents.Execute(handler, ped, ExecuteEvents.pointerClickHandler);
        Assert.IsTrue(executed, what + "：PointerClick 未被 handler 处理（" + handler.name + "）");
        return handler;
    }

    /// <summary>真实 raycast 点击统一 Event Card 的第 index 个 Option（卡面不可见则跳过）。</summary>
    private static void RealClickEventOption(object journey, int index, string label, int attempt)
    {
        if (!(bool)GetMember(journey, "IsEventCardVisible")) return;
        GameObject optionGo = FindChildByName(((Component)journey).transform, "BtnOption_" + index);
        Assert.IsNotNull(optionGo, "统一卡面必须提供 Option 按钮：" + label);
        var optionRt = optionGo.GetComponent<RectTransform>();
        Vector2 p = ScreenCenter(optionRt);
        LogHits(label + "（第 " + (attempt + 1) + " 次）", p, RaycastAllAt(p));
        AssertTopHitIs(optionRt, label + "（第 " + (attempt + 1) + " 次）", p, RaycastAllAt(p));
        RealPointerClick(p, label);
    }

    private static GameObject FindChildByName(Transform parent, string name)
    {
        if (parent == null) return null;
        foreach (var t in parent.GetComponentsInChildren<Transform>(true))
            if (t.name == name) return t.gameObject;
        return null;
    }

    // ==================== 场景 / 产品入口 ====================

    private object _pc;

    private IEnumerator EnterTownFreshGame()
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
        Assert.IsNotNull(town, "TownUI 应存在（产品 Village 表面）");
        Assert.IsTrue(((GameObject)GetMember(town, "canvasRoot")).activeSelf, "应已进入 Town");

        _pc = GetMember(town, "PlayerData");
        Assert.IsNotNull(_pc, "TownUI.PlayerData 应存在");

        BridgeTime().SetSpeed(VillageTime.VillageTimeSpeed.Paused); // 确定性：手动按天推进
    }

    private static int CurrentCarry(object adventure)
    {
        object state = GetMember(adventure, "ProgressionState");
        object supply = GetMember(state, "Supply");
        return (int)GetMember(supply, "CurrentCarryUnits");
    }

    // ==================== 1｜人物详情：真实 raycast 打开 / 关闭；关闭后不得留下 blocker ====================

    [UnityTest]
    public IEnumerator Prep_PersonDetailPopup_RealRaycast_OpenClose_RestoresInteraction()
    {
        yield return EnterTownFreshGame();
        AddVillageFood(100);

        var expUI = FindInstance("ExpeditionUI");
        Assert.IsNotNull(expUI, "ExpeditionUI 应存在");
        Call(expUI, "ShowExpeditionFromTown", _pc);
        yield return null;
        yield return null;

        object panel = GetMember(expUI, "_identityPanel");
        Assert.IsNotNull(panel, "准备界面应存在人物详情面板实例");

        var canvas = ((Component)panel).GetComponentInChildren<Canvas>(true);
        Assert.IsNotNull(canvas, "人物详情面板应自建 Canvas");
        Assert.IsNotNull(canvas.GetComponent<GraphicRaycaster>(),
            "人物详情 Canvas 必须带 GraphicRaycaster（否则玩家点不到「人物详情」）");

        Assert.IsFalse((bool)GetMember(panel, "IsPopupOpen"), "Popup 默认必须关闭（不得默认显示大面积遮罩）");
        StringAssert.Contains("村庄食物：", (string)GetMember(panel, "SupplyText"),
            "准备界面必须可见 Village Food（只读，不复制资源状态）");

        // ---- 准备界面角色卡：真实 raycast 必须命中卡片按钮（准备界面未被遮挡） ----
        var charCards = (IDictionary)GetMember(expUI, "_charCards");
        Assert.Greater(charCards.Count, 0, "准备界面应有角色卡（产品选人表面）");
        GameObject firstCard = null;
        foreach (DictionaryEntry e in charCards) { firstCard = (GameObject)e.Value; break; }
        var cardRt = firstCard.GetComponent<RectTransform>();
        Vector2 cardPoint = ScreenCenter(cardRt);
        LogHits("准备界面角色卡", cardPoint, RaycastAllAt(cardPoint));
        AssertTopHitIs(cardRt, "准备界面角色卡", cardPoint, RaycastAllAt(cardPoint));

        // ---- 「人物详情」按钮：真实 raycast + 真实 PointerClick → Popup 打开 ----
        var detailBtn = (Button)GetMember(panel, "DetailButton");
        Assert.IsNotNull(detailBtn, "必须存在「人物详情」入口按钮");
        var detailRt = detailBtn.GetComponent<RectTransform>();
        Vector2 detailPoint = ScreenCenter(detailRt);
        LogHits("「人物详情」按钮", detailPoint, RaycastAllAt(detailPoint));
        AssertTopHitIs(detailRt, "「人物详情」按钮", detailPoint, RaycastAllAt(detailPoint));
        Assert.IsTrue(detailBtn.interactable, "「人物详情」按钮必须可交互");

        RealPointerClick(detailPoint, "「人物详情」按钮");
        yield return null;

        Assert.IsTrue((bool)GetMember(panel, "IsPopupOpen"), "真实点击后 Popup 必须打开");
        string detailText = (string)GetMember(panel, "DetailText");
        StringAssert.Contains("职责：", detailText, "Popup 必须显示当前职责");
        StringAssert.Contains("长期状态：", detailText, "Popup 必须显示 Injury / 长期状态");
        StringAssert.Contains("远征经历：", detailText, "Popup 必须显示 Expedition Experience / 最近 Outcome");
        Assert.IsTrue((bool)GetMember(panel, "IsDetailTextFontAvailable"),
            "详情文本必须有可用字体（否则文本肉眼不可见）");

        // ---- Popup 打开 = 真实 Modal：下层 UI 不得仍可点击 ----
        var blockedHits = RaycastAllAt(cardPoint);
        Assert.Greater(blockedHits.Count, 0, "Popup 打开时应命中 Modal 背景");
        Assert.IsNull(ExecuteEvents.GetEventHandler<IPointerClickHandler>(blockedHits[0].gameObject),
            "Popup 打开时下层 UI 不得仍可获得点击\n命中链：\n" + DumpHits(blockedHits));

        // ---- 「关闭」：真实点击 → Popup 关闭 → 其它 UI 恢复可点（无不可见 blocker） ----
        var closeBtn = (Button)GetMember(panel, "CloseButton");
        Assert.IsNotNull(closeBtn, "Popup 必须提供关闭入口");
        var closeRt = closeBtn.GetComponent<RectTransform>();
        Vector2 closePoint = ScreenCenter(closeRt);
        AssertTopHitIs(closeRt, "「关闭」按钮", closePoint, RaycastAllAt(closePoint));

        RealPointerClick(closePoint, "「关闭」按钮");
        yield return null;

        Assert.IsFalse((bool)GetMember(panel, "IsPopupOpen"), "真实点击「关闭」后 Popup 必须关闭");
        AssertTopHitIs(cardRt, "Popup 关闭后的角色卡", cardPoint, RaycastAllAt(cardPoint));
        AssertTopHitIs(detailRt, "Popup 关闭后的「人物详情」按钮", detailPoint, RaycastAllAt(detailPoint));

        CallStatic("VillageRuntimeBridge", "ResetForTests");
    }

    // ==================== 2｜Progression：真实 raycast 点击「继续前进 / 撤退」 ====================

    [UnityTest]
    public IEnumerator Progression_ContinueAndRetreat_RealRaycastClicks_Work()
    {
        yield return EnterTownFreshGame();
        AddVillageFood(100); // 让 Carry 足够（preflight 通过）

        var expUI = FindInstance("ExpeditionUI");
        Assert.IsNotNull(expUI, "ExpeditionUI 应存在");
        Call(expUI, "ShowExpeditionFromTown", _pc);
        yield return null;
        yield return null;

        object expSystem = GetMember(expUI, "_expedition");
        int selectedChars = ((IEnumerable)GetMember(expSystem, "SelectedCharacters")).Cast<object>().Count();
        Assert.GreaterOrEqual(selectedChars, 1, "准备界面应默认选中至少一张可出征角色卡");

        // ---- Confirm Expedition：真实 raycast + 真实点击 ----
        var confirmBtn = (Button)GetMember(expUI, "btnConfirm");
        Assert.IsNotNull(confirmBtn, "应存在 Confirm Expedition 按钮");
        Assert.IsTrue(confirmBtn.interactable, "Carry 足够时 Confirm Expedition 必须可用");
        var confirmRt = confirmBtn.GetComponent<RectTransform>();
        Vector2 confirmPoint = ScreenCenter(confirmRt);
        LogHits("Confirm Expedition 按钮", confirmPoint, RaycastAllAt(confirmPoint));
        AssertTopHitIs(confirmRt, "Confirm Expedition 按钮", confirmPoint, RaycastAllAt(confirmPoint));

        RealPointerClick(confirmPoint, "Confirm Expedition 按钮");
        yield return null;
        yield return null;

        object adventure = GetMember(expUI, "_currentAdventure");
        Assert.IsNotNull(adventure, "真实点击 Confirm 后必须绑定当前 Adventure（否则推进 / 撤退不可用）");

        var journey = FindInstance("ExpeditionJourneyView");
        Assert.IsNotNull(journey, "确认出征后必须存在 Journey 表现宿主");
        Assert.IsTrue((bool)GetMember(journey, "IsVisible"), "确认出征后 Journey 必须可见");
        var journeyCanvas = ((Component)journey).GetComponentInChildren<Canvas>(true);
        Assert.IsNotNull(journeyCanvas, "Journey Canvas 必须存在（稳定 Scene 宿主）");
        Assert.IsNotNull(journeyCanvas.GetComponent<GraphicRaycaster>(),
            "Journey Canvas 必须带 GraphicRaycaster（否则「继续前进 / 撤退」点不到）");

        StringAssert.Contains("补给：", (string)GetMember(journey, "SupplyDisplay"), "Journey Supply HUD 必须可见");
        int supplyBefore = CurrentCarry(adventure);
        Assert.Greater(supplyBefore, 0, "Carry 足够时 Supply 必须 > 0（0 Supply 会进入无动作会话）");

        // ---- 真实点击 Journey 推进控件 → 真实进入节点；elapsed 节点后 Supply 必须下降 ----
        bool advanced = false;
        for (int attempt = 0; attempt < 6; attempt++)
        {
            var nodes = new List<string>();
            foreach (var n in (IEnumerable)Call(adventure, "GetAvailableProgressionNodes"))
                nodes.Add((string)GetMember(n, "NodeId"));
            if (nodes.Count == 0) break; // 0 后继：路线终点（仅可撤退）

            // 统一 Event Card：旅行完成后出卡面；Branch / Battle 都用同一卡面
            string targetName = "BtnContinue";
            string label = "「继续前进」按钮";

            GameObject choiceGo = FindChildByName(((Component)journey).transform, targetName);
            if (choiceGo == null) break;

            var choiceRt = choiceGo.GetComponent<RectTransform>();
            Vector2 choicePoint = ScreenCenter(choiceRt);
            LogHits(label + "（第 " + (attempt + 1) + " 次）", choicePoint, RaycastAllAt(choicePoint));
            AssertTopHitIs(choiceRt, label + "（第 " + (attempt + 1) + " 次）",
                choicePoint, RaycastAllAt(choicePoint));
            var choiceBtn = choiceGo.GetComponent<Button>();
            Assert.IsTrue(choiceBtn != null && choiceBtn.interactable, label + "必须可用可点");

            string expectedNodeId = nodes[0];

            RealPointerClick(choicePoint, label);
            yield return null;
            yield return null;

            // 卡面 Option：必须真实点击（Branch → 第 1 条道路；Battle → 进入战斗）
            RealClickEventOption(journey, 0, "卡面 Option", attempt);
            // Battle 路线：再过一次战斗确认卡（同一卡面）
            if ((bool)GetMember(journey, "IsEventCardVisible"))
                RealClickEventOption(journey, 0, "卡面 Option（战斗确认）", attempt);

            yield return null;

            object state = GetMember(adventure, "ProgressionState");
            Assert.IsTrue((bool)Call(state, "IsNodeEntered", expectedNodeId),
                "真实点击" + label + "并确认统一卡面后必须真实进入节点：" + expectedNodeId);

            if (!(bool)GetMember(journey, "IsVisible"))
            {
                // Battle 类节点：按产品路径从 Battle 结果返回（走既有 Resolver 结算）
                Call(expUI, "OnReturnFromBattle", true);
                yield return null;
                yield return null;
                yield return null;
            }

            if (CurrentCarry(adventure) < supplyBefore) { advanced = true; break; }
        }

        Assert.IsTrue(advanced,
            "真实点击 Journey 推进控件并经过 elapsed 节点后，Supply 必须真实下降（当前 Supply 显示："
            + (string)GetMember(journey, "SupplyDisplay") + "）");

        // ---- 真实点击「撤退 / 返回领地」→ 必须回到 Town ----
        GameObject retreatGo = FindChildByName(((Component)journey).transform, "BtnReturn");
        Assert.IsNotNull(retreatGo, "Journey Retreat 按钮必须始终存在");
        var retreatBtn = retreatGo.GetComponent<Button>();
        Assert.IsTrue(retreatBtn.interactable, "Retreat 必须始终可点击（含 Supply 不足）");
        var retreatRt = retreatGo.GetComponent<RectTransform>();
        Vector2 retreatPoint = ScreenCenter(retreatRt);
        LogHits("「撤退 / 返回领地」按钮", retreatPoint, RaycastAllAt(retreatPoint));
        AssertTopHitIs(retreatRt, "「撤退 / 返回领地」按钮", retreatPoint, RaycastAllAt(retreatPoint));

        RealPointerClick(retreatPoint, "「撤退 / 返回领地」按钮");
        yield return null;
        yield return null;

        var town = FindInstance("TownUI");
        Assert.IsNotNull(town, "TownUI 应存在");
        Assert.IsTrue(((GameObject)GetMember(town, "canvasRoot")).activeSelf,
            "真实点击 Retreat 后必须回到 Town");
        object record = StaticMember("VillageRuntimeBridge", "ActiveReturnRecord");
        Assert.IsNotNull(record, "应存在 Return 会话记录");
        Assert.AreEqual("Completed", GetMember(record, "Phase").ToString(),
            "Retreat 必须走完既有 Return 事务（Settlement → Catch-up → Carry → Consequence → Clear → Final Save）");
        Assert.AreEqual(0,
            ((IEnumerable)StaticMember("VillageRuntimeBridge", "ExpeditionPersonIds")).Cast<object>().Count(),
            "Return 后 Expedition Occupancy 应清除");

        CallStatic("VillageRuntimeBridge", "ResetForTests");
    }

    // ==================== 3｜根因复刻：运行时自建 Canvas 缺少 GraphicRaycaster → 玩家永远点不到 ====================

    [UnityTest]
    public IEnumerator RootCauseProbe_RuntimeCanvasWithoutGraphicRaycaster_IsNeverHit()
    {
        yield return EnterTownFreshGame();

        // 复刻修复前的真实条件：运行时自建 Canvas 只 AddComponent<Canvas> + CanvasScaler（无 GraphicRaycaster）
        var probeCanvasGo = new GameObject("RootCauseProbeCanvas");
        var probeCanvas = probeCanvasGo.AddComponent<Canvas>();
        probeCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        probeCanvas.sortingOrder = 900;
        probeCanvasGo.AddComponent<CanvasScaler>();

        var probeBtnGo = new GameObject("RootCauseProbeButton", typeof(RectTransform));
        probeBtnGo.transform.SetParent(probeCanvasGo.transform, false);
        var probeRt = probeBtnGo.GetComponent<RectTransform>();
        probeRt.anchorMin = new Vector2(0.5f, 0.5f);
        probeRt.anchorMax = new Vector2(0.5f, 0.5f);
        probeRt.sizeDelta = new Vector2(480f, 96f);
        probeRt.anchoredPosition = Vector2.zero;
        var probeImg = probeBtnGo.AddComponent<Image>();
        probeImg.raycastTarget = true;
        probeBtnGo.AddComponent<Button>();
        Canvas.ForceUpdateCanvases();

        Vector2 probePoint = ScreenCenter(probeRt);
        var beforeHits = RaycastAllAt(probePoint);
        Debug.Log("[Raycast 根因复刻] 无 GraphicRaycaster 的运行时 Canvas（含 raycastTarget=true 的按钮）@ "
            + probePoint + " → 命中 " + beforeHits.Count + " 个\n" + DumpHits(beforeHits));
        Assert.IsFalse(beforeHits.Exists(h => h.gameObject == probeBtnGo
                || h.gameObject.transform.IsChildOf(probeCanvasGo.transform)),
            "根因复刻：缺少 GraphicRaycaster 时，EventSystem 完全不会命中该 Canvas 上的 Graphic"
            + "（玩家鼠标即使压在按钮上也无反应）\n命中链：\n" + DumpHits(beforeHits));

        // 对照探针：同一运行时条件，但 Canvas 创建时就带上 GraphicRaycaster（本轮修复）→ 立即可被真实命中
        var fixedCanvasGo = new GameObject("RootCauseProbeCanvasFixed");
        var fixedCanvas = fixedCanvasGo.AddComponent<Canvas>();
        fixedCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        fixedCanvas.sortingOrder = 901;
        fixedCanvasGo.AddComponent<CanvasScaler>();
        fixedCanvasGo.AddComponent<GraphicRaycaster>();

        var fixedBtnGo = new GameObject("RootCauseProbeButtonFixed", typeof(RectTransform));
        fixedBtnGo.transform.SetParent(fixedCanvasGo.transform, false);
        var fixedRt = fixedBtnGo.GetComponent<RectTransform>();
        fixedRt.anchorMin = new Vector2(0.5f, 0.5f);
        fixedRt.anchorMax = new Vector2(0.5f, 0.5f);
        fixedRt.sizeDelta = new Vector2(480f, 96f);
        fixedRt.anchoredPosition = Vector2.zero;
        var fixedImg = fixedBtnGo.AddComponent<Image>();
        fixedImg.raycastTarget = true;
        fixedBtnGo.AddComponent<Button>();
        Canvas.ForceUpdateCanvases();
        yield return null;

        Vector2 fixedPoint = ScreenCenter(fixedRt);
        var afterHits = RaycastAllAt(fixedPoint);
        Debug.Log("[Raycast 根因复刻] 带 GraphicRaycaster 的运行时 Canvas（对照）@ " + fixedPoint + " → 命中 "
            + afterHits.Count + " 个\n" + DumpHits(afterHits));
        AssertTopHitIs(fixedRt, "带 GraphicRaycaster 的探针按钮", fixedPoint, afterHits);
        Assert.IsNotNull(ExecuteEvents.GetEventHandler<IPointerClickHandler>(afterHits[0].gameObject),
            "带 GraphicRaycaster 的运行时按钮必须可获得 PointerClick handler（玩家可点）");

        UnityEngine.Object.Destroy(probeCanvasGo);
        UnityEngine.Object.Destroy(fixedCanvasGo);
        yield return null;
        CallStatic("VillageRuntimeBridge", "ResetForTests");
    }
}
