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
/// V4 Person Detail Final Repair｜**候选人详情只读 Viewer 专项验证**（Disposable Development Test / Test / Not Frozen）。
///
/// 覆盖（针对性）：
///   A. 准备界面有 N 个候选 Person → Detail Popup 覆盖对应 N 个候选（按既有合法过滤规则）；
///   B. 不能只显示当前 Selected Party；
///   C. Hero + 2 个以上 Character → 三者均可在详情中看到；
///   D. Character 显示 职责 / 长期状态 / 远征经历；Hero 不伪造 LifeHistory（无远征经历行）；
///   E. 真实 Pointer 打开 / 关闭成立，关闭后其它准备界面按钮仍可点击；
///   F. 查看详情前后 Party Selection 不变（Detail = Read Only）。
/// 不覆盖（已人工通过，本轮不改不重验）：Resource / Supply / Progression / Retreat / Return。
/// </summary>
public class VillageV4PersonDetailCandidatePlayTests
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
        foreach (var m in t.GetMethods(F).Where(m => m.Name == method))
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
        Assert.IsNotNull(es, "场景中必须存在 EventSystem");
        var ped = new PointerEventData(es) { position = screenPoint };
        var results = new List<RaycastResult>();
        es.RaycastAll(ped, results);
        return results;
    }

    private static string DumpHits(List<RaycastResult> hits)
    {
        if (hits == null || hits.Count == 0) return "（无任何 Raycast 命中）";
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < hits.Count; i++)
        {
            var go = hits[i].gameObject;
            var canvas = go != null ? go.GetComponentInParent<Canvas>() : null;
            var graphic = go != null ? go.GetComponent<Graphic>() : null;
            var btn = go != null ? go.GetComponentInParent<Button>() : null;
            sb.AppendLine(i + ". " + (go != null ? go.name : "<null>")
                + " | Canvas=" + (canvas != null ? canvas.name : "null")
                + " sortingOrder=" + (canvas != null ? canvas.sortingOrder : -9999)
                + " | raycastTarget=" + (graphic != null ? graphic.raycastTarget.ToString() : "n/a")
                + " | interactable=" + (btn != null ? btn.interactable.ToString() : "n/a")
                + " | active=" + (go != null && go.activeInHierarchy));
        }
        return sb.ToString();
    }

    private static void AssertTopHitIs(RectTransform target, string what, Vector2 point, List<RaycastResult> hits)
    {
        Assert.Greater(hits.Count, 0, what + "：RaycastAll 无命中\n" + DumpHits(hits));
        var top = hits[0];
        bool belongs = top.gameObject == target.gameObject || top.gameObject.transform.IsChildOf(target.transform);
        Assert.IsTrue(belongs, what + "：top raycast hit 不是目标\n" + DumpHits(hits));
    }

    private static void RealPointerClick(Vector2 screenPoint, string what)
    {
        var es = EventSystem.current;
        var ped = new PointerEventData(es)
        {
            position = screenPoint,
            button = PointerEventData.InputButton.Left,
            clickCount = 1
        };
        var hits = new List<RaycastResult>();
        es.RaycastAll(ped, hits);
        Assert.Greater(hits.Count, 0, what + "：点击位置没有可命中对象\n" + DumpHits(hits));
        var handler = ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject);
        Assert.IsNotNull(handler, what + "：top hit 父链没有 IPointerClickHandler\n" + DumpHits(hits));
        Assert.IsTrue(ExecuteEvents.Execute(handler, ped, ExecuteEvents.pointerClickHandler),
            what + "：PointerClick 未被处理");
    }

    // ==================== 场景入口 ====================

    private object _pc;

    private IEnumerator EnterPrep()
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
            Assert.IsTrue((bool)CallStatic("SaveSystem", "Save", fresh), "新档应能写入");
        }

        var mm = FindInstance("MainMenuUI");
        Assert.IsNotNull(mm, "MainMenuUI 应存在");
        Call(mm, "OnContinue");
        yield return null;
        yield return null;

        var town = FindInstance("TownUI");
        Assert.IsNotNull(town, "TownUI 应存在");
        _pc = GetMember(town, "PlayerData");
        Assert.IsNotNull(_pc, "PlayerData 应存在");

        var expUI = FindInstance("ExpeditionUI");
        Assert.IsNotNull(expUI, "ExpeditionUI 应存在");
        Call(expUI, "ShowExpeditionFromTown", _pc);
        yield return null;
        yield return null;
    }

    /// <summary>把详情文本切成以「· 」开头的候选块（每块 = 一个候选；标题行不计入）。</summary>
    private static List<string> ExtractRowBlocks(string detailText)
    {
        var blocks = new List<string>();
        System.Text.StringBuilder sb = null;
        foreach (var raw in detailText.Split('\n'))
        {
            string line = raw.TrimEnd('\r');
            if (line.StartsWith("· "))
            {
                if (sb != null) blocks.Add(sb.ToString());
                sb = new System.Text.StringBuilder();
                sb.Append(line);
            }
            else if (sb != null)
            {
                sb.Append('\n').Append(line);
            }
        }
        if (sb != null) blocks.Add(sb.ToString());
        return blocks;
    }

    /// <summary>读取当前远征选择的只读快照（用于 F：查看详情前后不变化）。</summary>
    private static string PartySnapshot(object expUI)
    {
        var expedition = GetMember(expUI, "_expedition");
        var chars = (IEnumerable)GetMember(expedition, "SelectedCharacters");
        var ids = new List<string>();
        foreach (var c in chars) ids.Add((string)GetMember(c, "CardId"));
        object hero = GetMember(expedition, "SelectedHero");
        string heroId = hero != null ? (string)GetMember(hero, "PersonId") : "none";
        ids.Sort(StringComparer.Ordinal);
        return heroId + "|" + string.Join(",", ids);
    }

    // ==================== 候选人详情（A–F） ====================

    [UnityTest]
    public IEnumerator PersonDetail_ShowsAllCandidates_IsReadOnly_AndRealPointerWorks()
    {
        yield return EnterPrep();

        var expUI = FindInstance("ExpeditionUI");
        var panel = GetMember(expUI, "_identityPanel");
        Assert.IsNotNull(panel, "准备界面应存在人物详情面板");
        Assert.IsFalse((bool)GetMember(panel, "IsPopupOpen"), "Popup 默认必须关闭");

        // ---- 候选集（准备界面同源：全部 Hero + 全部角色卡） ----
        var heroes = ((IEnumerable)GetMember(_pc, "OwnedHeroes")).Cast<object>().ToList();
        var cards = ((IEnumerable)GetMember(_pc, "OwnedCharacterCards")).Cast<object>().ToList();
        int expectedCandidates = heroes.Count + cards.Count;
        Assert.Greater(expectedCandidates, 0, "Fresh New Game 应存在候选人物");

        // A：详情覆盖候选数 = 准备界面候选数
        Assert.AreEqual(expectedCandidates, (int)GetMember(panel, "CandidateCount"),
            "Detail Popup 必须覆盖准备界面的全部候选（A）");

        // C 前提：≥2 名可解析 Card→Person 的 Character
        var resolvedPersonIds = new List<string>();
        foreach (var c in cards)
            resolvedPersonIds.Add((string)Call(_pc, "ResolvePersonIdForCard", c));
        Assert.GreaterOrEqual(resolvedPersonIds.Count(x => !string.IsNullOrEmpty(x)), 2,
            "Fresh New Game 应有 ≥2 名可解析显式 Card→Person 绑定的 Character 候选（C）");

        // C 前提：Hero 不新增 LifeHistory
        var heroObj = heroes.FirstOrDefault();
        Assert.IsNotNull(heroObj, "应有 Hero 候选");
        Assert.IsNull(heroObj.GetType().GetProperty("LifeHistory"), "Hero 不得新增 LifeHistory（OQ1）");

        var charCardsDict = (IDictionary)GetMember(expUI, "_charCards");
        Assert.Greater(charCardsDict.Count, 0, "准备界面应渲染角色卡");
        GameObject firstCard = null;
        foreach (DictionaryEntry e in charCardsDict) { firstCard = (GameObject)e.Value; break; }
        var cardRt = firstCard.GetComponent<RectTransform>();
        Vector2 cardPoint = ScreenCenter(cardRt);

        var detailBtn = (Button)GetMember(panel, "DetailButton");
        var detailRt = detailBtn.GetComponent<RectTransform>();
        Vector2 detailPoint = ScreenCenter(detailRt);
        AssertTopHitIs(detailRt, "「人物详情」按钮", detailPoint, RaycastAllAt(detailPoint));
        AssertTopHitIs(cardRt, "准备界面角色卡（详情前）", cardPoint, RaycastAllAt(cardPoint));

        // ---- E/F：真实 Pointer 打开；Selection 不变 ----
        string partyBefore = PartySnapshot(expUI);
        RealPointerClick(detailPoint, "「人物详情」按钮");
        yield return null;

        Assert.IsTrue((bool)GetMember(panel, "IsPopupOpen"), "真实点击后 Popup 必须打开（E）");
        Assert.AreEqual(partyBefore, PartySnapshot(expUI), "打开详情不得改变 Party Selection（F）");

        string detail = (string)GetMember(panel, "DetailText");
        Debug.Log("[Person Detail] 候选详情文本：\n" + detail);
        Assert.IsTrue((bool)GetMember(panel, "IsDetailTextFontAvailable"), "详情文本必须有可用字体");

        // ---- A/C：候选块数与逐候选可见性 ----
        var blocks = ExtractRowBlocks(detail);
        Assert.AreEqual(expectedCandidates, blocks.Count, "候选块数必须等于候选数（A）");
        Assert.GreaterOrEqual(blocks.Count(b => b.Contains("（英雄）")), 1, "详情必须包含 Hero 候选（C）");
        Assert.GreaterOrEqual(blocks.Count(b => !b.Contains("（英雄）")), 2, "详情必须包含 ≥2 个 Character 候选（C）");

        foreach (var h in heroes)
        {
            string name = (string)GetMember(h, "DisplayName");
            if (!string.IsNullOrEmpty(name)) StringAssert.Contains(name, detail, "Hero 候选必须可见（A/C）");
        }
        foreach (var c in cards)
        {
            string pid = (string)Call(_pc, "ResolvePersonIdForCard", c);
            if (string.IsNullOrEmpty(pid)) continue;
            var person = Call(_pc, "FindPersonByPersonId", pid);
            string personName = person != null ? (string)GetMember(person, "DisplayName") : null;
            if (!string.IsNullOrEmpty(personName))
                StringAssert.Contains(personName, detail, "Character 候选必须可见：" + personName);
        }

        // ---- B：必须包含“未选中”的候选（不是只显示 Selected Party） ----
        var selectedCardIds = new HashSet<string>();
        foreach (var c in ((IEnumerable)GetMember(GetMember(expUI, "_expedition"), "SelectedCharacters")).Cast<object>())
            selectedCardIds.Add((string)GetMember(c, "CardId"));

        string unselectedName = null;
        foreach (var c in cards)
        {
            if (selectedCardIds.Contains((string)GetMember(c, "CardId"))) continue;
            string pid = (string)Call(_pc, "ResolvePersonIdForCard", c);
            var person = string.IsNullOrEmpty(pid) ? null : Call(_pc, "FindPersonByPersonId", pid);
            string nm = person != null ? (string)GetMember(person, "DisplayName") : null;
            if (!string.IsNullOrEmpty(nm)) { unselectedName = nm; break; }
        }
        Assert.IsNotNull(unselectedName, "Fresh New Game 应存在未选中的候选（B 前提）");
        StringAssert.Contains(unselectedName, detail, "详情必须包含当前未选中的候选（B）");

        // ---- D：字段完整性（Character 三类；Hero 不含远征经历） ----
        foreach (var b in blocks)
        {
            if (b.Contains("无法读取长期人物信息")) continue; // 无显式 Card→Person 绑定的候选只给原因
            Assert.IsTrue(b.Contains("职责："), "每个候选必须显示职责（D）：\n" + b);
            Assert.IsTrue(b.Contains("长期状态："), "每个候选必须显示 Injury / 长期状态（D）：\n" + b);
            if (b.Contains("（英雄）"))
                Assert.IsFalse(b.Contains("远征经历："), "Hero 不得显示虚构 Expedition Experience（D）：\n" + b);
            else
                Assert.IsTrue(b.Contains("远征经历："),
                    "Character 必须显示 Expedition Experience 次数 / 最近 Outcome（D）：\n" + b);
        }

        // ---- E/F：真实 Pointer 关闭；Selection 不变；无残留 blocker ----
        var closeBtn = (Button)GetMember(panel, "CloseButton");
        var closeRt = closeBtn.GetComponent<RectTransform>();
        Vector2 closePoint = ScreenCenter(closeRt);
        AssertTopHitIs(closeRt, "「关闭」按钮", closePoint, RaycastAllAt(closePoint));

        RealPointerClick(closePoint, "「关闭」按钮");
        yield return null;

        Assert.IsFalse((bool)GetMember(panel, "IsPopupOpen"), "真实点击「关闭」后 Popup 必须关闭（E）");
        Assert.AreEqual(partyBefore, PartySnapshot(expUI), "关闭详情不得改变 Party Selection（F）");
        AssertTopHitIs(cardRt, "关闭后的角色卡", cardPoint, RaycastAllAt(cardPoint));
        AssertTopHitIs(detailRt, "关闭后的「人物详情」按钮", detailPoint, RaycastAllAt(detailPoint));

        CallStatic("VillageRuntimeBridge", "ResetForTests");
    }
}
