using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// WP-E｜Journey 表现宿主显示生命周期契约测试（EditMode）。
///
/// 冻结的是**表现宿主生命周期**（不涉任何游戏规则 / Progression / Battle / Return / Save）：
///   1. 初始化即 Hidden：Scene 中 Canvas 的 serialized enabled 不得决定启动可见性
///      （否则 Scene 里 Root Active + Canvas enabled 会在启动时覆盖主界面）；
///   2. SetVisible(true) 对「Root 被意外置为 Inactive」必须最小防御恢复 Active 后显示
///      （只启用 Canvas 组件不会渲染 → 症状 = 蓝屏 / Journey 不显示）；
///   3. SetVisible(false) 只关 Canvas 组件，Root 保持 Active（稳定 Scene Host，
///      隐藏不得依赖永久关闭 Root）；
///   4. IsVisible 必须同时要求「已请求显示 + Canvas enabled + Host ActiveInHierarchy」。
///
/// 说明：asmdef 测试程序集不能引用预定义程序集 Assembly-CSharp，
/// 运行时类型（ExpeditionJourneyView / Canvas）通过反射访问（与本目录既有测试一致）。
/// </summary>
public class JourneyHostLifecycleTests
{
    private const BindingFlags F =
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

    private static Type ViewType
    {
        get
        {
            var t = Type.GetType("ExpeditionJourneyView, Assembly-CSharp");
            Assert.IsNotNull(t, "未找到 Assembly-CSharp 类型：ExpeditionJourneyView");
            return t;
        }
    }

    private GameObject _root;
    private Canvas _canvas;
    private Component _view;

    [SetUp]
    public void SetUp()
    {
        // 裸宿主：只有 Root + Canvas + View（不新建第二套 Canvas / Journey Host）。
        _root = new GameObject("JourneyHostLifecycleTestRoot", typeof(RectTransform), typeof(Canvas));
        _canvas = _root.GetComponent<Canvas>();
        _view = _root.AddComponent(ViewType);
    }

    [TearDown]
    public void TearDown()
    {
        if (_root != null) UnityEngine.Object.DestroyImmediate(_root);
        _root = null;
        _canvas = null;
        _view = null;
    }

    // ==================== 反射辅助 ====================

    private void InvokeAwake() => ViewType.GetMethod("Awake", F).Invoke(_view, null);

    private void CallSetVisible(bool visible) =>
        ViewType.GetMethod("SetVisible", F).Invoke(_view, new object[] { visible });

    private bool IsVisible => (bool)ViewType.GetProperty("IsVisible", F).GetValue(_view, null);
    private bool IsHostActive => (bool)ViewType.GetProperty("IsHostActive", F).GetValue(_view, null);
    private GameObject HostRoot => (GameObject)ViewType.GetProperty("HostRoot", F).GetValue(_view, null);

    // ==================== 契约 ====================

    [Test]
    public void Awake_InitializesHidden_EvenWhenCanvasIsSerializedEnabled()
    {
        _canvas.enabled = true;             // 模拟 Scene 里被手工打开的 Canvas

        InvokeAwake();

        Assert.IsFalse(_canvas.enabled,
            "初始化后 Canvas 必须关闭：启动 / 非 Journey 阶段不得依赖 Scene serialized enabled 决定可见性");
        Assert.IsFalse(IsVisible, "初始化后 Journey 不得可见（否则启动即覆盖主界面）");
        Assert.IsTrue(_root.activeSelf, "初始化不得关闭 Root：ExpeditionJourneyRoot 是稳定 Scene Host");
    }

    [Test]
    public void SetVisible_True_RecoversHostThatWasAccidentallyInactive()
    {
        InvokeAwake();
        _root.SetActive(false);             // 模拟「Scene / 手工把 ExpeditionJourneyRoot 设为 Inactive」

        CallSetVisible(true);

        Assert.IsTrue(_root.activeSelf,
            "SetVisible(true) 必须最小防御恢复 Host Active（否则只启用 Canvas 组件 → 蓝屏 / 不渲染）");
        Assert.IsTrue(IsHostActive, "恢复后 Host 必须 ActiveInHierarchy");
        Assert.IsTrue(_canvas.enabled, "恢复后 Canvas 必须启用");
        Assert.IsTrue(IsVisible, "恢复后 Journey 必须真正可见");
    }

    [Test]
    public void SetVisible_False_HidesWithoutDeactivatingHost()
    {
        InvokeAwake();
        CallSetVisible(true);
        Assert.IsTrue(IsVisible, "前置：SetVisible(true) 后必须可见");

        CallSetVisible(false);

        Assert.IsFalse(_canvas.enabled, "SetVisible(false) 必须关闭 Canvas（隐藏）");
        Assert.IsFalse(IsVisible, "SetVisible(false) 后不得声称可见");
        Assert.IsTrue(_root.activeSelf,
            "SetVisible(false) 不得关闭 Root：隐藏不得依赖永久关闭 Root（保持稳定宿主）");
    }

    [Test]
    public void VisibilityRoundTrip_ConfirmThenBattleThenReturn()
    {
        InvokeAwake();

        // Confirm Expedition → Show
        CallSetVisible(true);
        Assert.IsTrue(IsVisible, "Confirm Expedition 后 Journey 必须可见");

        // Battle 前 → Hide（HideForEncounter 语义：CancelPresentation + SetVisible(false)）
        ViewType.GetMethod("HideForEncounter", F).Invoke(_view, null);
        Assert.IsFalse(IsVisible, "Battle 期间 Journey 必须隐藏 / 冻结");
        Assert.IsTrue(_root.activeSelf, "Battle 期间 Root 保持 Active（Host 稳定性）");

        // Battle 返回 → ShowAfterEncounter → Show
        CallSetVisible(true);
        Assert.IsTrue(IsVisible, "Battle 返回后 Journey 必须恢复");

        // Return / Retreat → Hide
        CallSetVisible(false);
        Assert.IsFalse(IsVisible, "Return 后 Journey 必须隐藏");
        Assert.IsTrue(_root.activeSelf, "Return 后 Root 仍保持 Active");
    }

    [Test]
    public void HostRoot_ResolvesCanvasGameObject_WhenFieldNotBound()
    {
        InvokeAwake();

        Assert.AreSame(_root, HostRoot,
            "HostRoot 必须解析为 Canvas 所在 GameObject（未绑定时自动解析，不新建第二套宿主）");
    }
}
