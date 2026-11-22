using TMPro;
using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

/// <summary>
/// V4 Acceptance Prep｜真实产品路径验证（GUI/交互式 Editor PlayMode；Disposable Development Test）：
/// MainMenu → Town → ExpeditionUI（远征准备）真实渲染的角色卡 / 英雄卡上，
/// 必须能看到少量现有长期人物事实（职责 / 长期状态 / 远征经历），使制作人能够真实判断五问 #4。
/// 全部内容 Test / Not Frozen。
/// </summary>
public class VillageV4ExpeditionSelectionIdentityPlayTests
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
        var m = GetType(typeName).GetMethod(method, F);
        Assert.IsNotNull(m, "静态方法不存在：" + typeName + "." + method);
        return m.Invoke(null, args);
    }

    [UnityTest]
    public IEnumerator ExpeditionSelection_Cards_ShowPersonIdentity_OnRealTownPath()
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
        Assert.IsNotNull(pc, "TownUI.PlayerData 应存在");

        var expUI = FindInstance("ExpeditionUI");
        Assert.IsNotNull(expUI, "ExpeditionUI 应存在");
        Call(expUI, "ShowExpeditionFromTown", pc);
        yield return null;
        yield return null;

        // 可见身份表面：独立 Canvas（不再依赖会被裁切的小卡片）
        var identityPanel = FindInstance("ExpeditionIdentityPanel");
        Assert.IsNotNull(identityPanel, "远征准备界面应存在 ExpeditionIdentityPanel 可见身份表面");

        var panelCanvas = (Canvas)GetMember(identityPanel, "_canvas");
        Assert.IsNotNull(panelCanvas, "身份面板应有独立 Canvas");
        Assert.IsTrue(panelCanvas.enabled, "身份面板 Canvas 必须可见（enabled）");
        Assert.IsTrue(panelCanvas.gameObject.activeInHierarchy, "身份面板必须在层级中激活");
        Assert.IsFalse((bool)GetMember(identityPanel, "IsPopupOpen"),
            "身份 Popup 默认必须关闭（不得常驻大面积遮罩遮挡其它 UI）");

        // 出征前 Village Food / 预计 Carry / 第一步成本必须可见（只读；不复制资源状态）
        string supply = (string)GetMember(identityPanel, "SupplyText");
        StringAssert.Contains("村庄食物：", supply, "准备界面必须显示 Village Food");
        StringAssert.Contains("预计携带：", supply, "准备界面必须显示预计携带量");
        StringAssert.Contains("第一步预计成本：", supply, "准备界面必须显示第一步预计成本");

        // 点击「人物详情」→ 打开局部只读 Popup（真实入口按钮回调；真实指针行为见 Raycast 专项）
        var detailButton = (Button)GetMember(identityPanel, "DetailButton");
        Assert.IsNotNull(detailButton, "必须存在「人物详情」入口按钮");
        Assert.IsTrue(detailButton.interactable, "「人物详情」按钮必须可交互");
        detailButton.onClick.Invoke();
        yield return null;

        Assert.IsTrue((bool)GetMember(identityPanel, "IsPopupOpen"), "点击后 Popup 必须打开");
        var panelText = (TMP_Text)GetMember(identityPanel, "_detailText");
        Assert.IsNotNull(panelText, "Popup 应有详情 TMP_Text");
        Assert.AreEqual(TextOverflowModes.Overflow, panelText.overflowMode,
            "身份文本必须 Overflow（不得像卡片一样被 Truncate 裁切）");
        Assert.IsTrue(panelText.enableWordWrapping);
        Assert.IsTrue((bool)GetMember(identityPanel, "IsDetailTextFontAvailable"),
            "身份文本必须有可用字体（否则肉眼不可见）");

        string display = panelText.text;
        StringAssert.Contains("可查看的远征候选人", display, "应显示「全部可查看候选人」标题（只读 Viewer）");
        StringAssert.Contains("职责：", display, "应显示当前职责（复用既有 Assignment 权威）");
        StringAssert.Contains("长期状态：", display, "应显示长期状态（复用既有 Injury 字段）");
        StringAssert.Contains("· ", display, "应至少列出当前选中的一名具体人物");

        // 关闭 Popup → 不得留下不可见全屏 Raycast blocker
        ((Button)GetMember(identityPanel, "CloseButton")).onClick.Invoke();
        yield return null;
        Assert.IsFalse((bool)GetMember(identityPanel, "IsPopupOpen"), "关闭后 Popup 必须关闭");

        // 卡片保持最小摘要（身份不再塞进 160×190 / font 50 / Truncate 的小卡片）
        var cardPanel = (RectTransform)GetMember(expUI, "cardPanel");
        Assert.IsNotNull(cardPanel, "cardPanel 未绑定");
        int characterCardsChecked = 0;
        foreach (Transform child in cardPanel)
        {
            if (child == null || !child.name.StartsWith("CharCard_")) continue;
            var text = child.GetComponentInChildren<TMP_Text>(true);
            Assert.IsNotNull(text, "角色卡应有 TMP_Text：" + child.name);
            StringAssert.DoesNotContain("职责：", text.text, "卡片应保持最小摘要（身份由可见表面承载）");
            characterCardsChecked++;
        }
        Assert.GreaterOrEqual(characterCardsChecked, 1, "真实产品路径应至少渲染一张角色卡");

        CallStatic("VillageRuntimeBridge", "ResetForTests");
    }
}
