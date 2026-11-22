using TMPro;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using RRPG.Event;
using RRPG.StaticConfig;

/// <summary>
/// VillageSocialEventUI —— Village 事件弹窗（Viewer + Controller）。
///
/// **WP-Event-1D Gate D1 起为双模式组件**（Scene Host 与类名保持不变）：
///   1. **New Event Mode（正式）**：读 `EventRuntimeBridge.PendingVillageEvent`；
///      标题 / 正文 / 选项全部来自 Event 静态配置（唯一 `EventTemplateRenderer` 渲染）；
///      玩家点击 = `EventOptionEvaluator` → `EventOptionExecutor`（OptionCondition 重检 → Outcome → Effect Transaction）；
///   2. **Legacy Compatibility Mode（兼容）**：只在**没有**新 Event Pending 时处理无法迁移的旧档
///      Legacy Pending（§40：正式 Recognition 不再创建 Legacy Pending）。
///
/// 职责：
///   - 轮询 VillageRuntimeBridge 是否有待处理 Recognition 回应（V3B 至多一条）并弹出；
///   - 展示“谁在回应 + 他观察到的客观长期事实（自述计数，中性文本）”；
///   - 提供两个合法回应：Choice A（恢复窗口）/ Choice B（继续承担）；不提供 Ignore / Deadline（V3B 边界）。
///   - 解析后展示可理解的结果，玩家点“知道了”关闭。
///
/// V3C 增量（均 Test / Not Frozen）：
///   - Urgency 最小语义：按 PatternKey 经 VillageTestSocialPatterns.ResolveUrgency 暴露
///     「需要及时处理 / 可忽略」并在界面标注；本组件不建 Event 队列 / Deadline / 惩罚；
///   - PauseOnOpen 可配置：pauseOnOpen（Inspector 可调）→ 打开事件时暂停 VillageTime，
///     关闭 / pending 消失后按打开前的 Speed 恢复；可忽略事件恒不强制暂停；
///     仍不驱动时间推进（推进只由 VillageTimeDriver + TownUI gating 负责）。
///
/// 本组件不携带任何持久状态；文本与数值均为 Test / Not Frozen。
/// </summary>
public class VillageSocialEventUI : MonoBehaviour
{
    [Header("Root / 结构（Editor Authoring 绑定；未绑定时按名称解析）")]
    public GameObject root;                  // PanelRoot（子 Canvas sortingOrder=211 + 全屏遮罩）
    public TMP_Text titleText;
    public TMP_Text bodyText;
    public Button grantRestButton;           // Choice A
    public Button continueUsingButton;       // Choice B
    public Button closeButton;               // 解析结果展示后关闭

    [Header("V3C PauseOnOpen（Test / Not Frozen；Inspector 可配置）")]
    [Tooltip("true = 需要及时处理的事件打开时暂停 VillageTime，关闭后恢复原 Speed；" +
             "可忽略（Ignorable）事件恒不强制暂停。")]
    public bool pauseOnOpen = VillageTestSocialPatterns.DefaultPauseOnOpen;

    private bool _isOpen;
    private bool _resultMode;
    private bool _initialized;
    private Transform _advanceOriginalParent;
    private int _advanceOriginalSibling;
    private bool _advanceMounted;
    private Vector2 _mountAnchorMin, _mountAnchorMax, _mountPivot, _mountSize;
    private Vector3 _mountPosition, _mountScale;
    private Quaternion _mountRotation;
    private Canvas _advanceCanvas;
    private RenderMode _advanceOriginalRenderMode;
    private bool _advanceOriginalOverrideSorting;
    private int _advanceOriginalSortingOrder;
    private string _advanceInstanceId;
    private float _nextPollTime;
    private bool _pausedByOpen;
    private VillageTime.VillageTimeSpeed _speedBeforePause = VillageTime.VillageTimeSpeed.Normal;
    private const float PollInterval = 0.5f;

    // ==================== WP-Event-1D Gate D1：Unified Event 产品模式（§13/§14） ====================

    /// <summary>true = 当前展示的是 Unified Event（PendingVillageEvent）；false = Legacy 兼容模式。</summary>
    private bool _eventMode;

    /// <summary>当前展示的 Event 实例（Event 模式；Legacy 模式为 null）。</summary>
    private ResolvedEventInstance _pendingEventInstance;

    /// <summary>当前事件的表现配置（Event 模式；来自 `EventPresentation`）。</summary>
    private EventPresentationDefinition _eventPresentation;

    /// <summary>运行时按配置生成的选项按钮（Legacy 两按钮只作视觉模板；返回结果前销毁）。</summary>
    private readonly List<GameObject> _dynamicOptionButtons = new List<GameObject>();

    /// <summary>最近一次生成的选项视图（只读证据，钳制「选项来自配置」）。</summary>
    public IReadOnlyList<VillageEventOptionView> CurrentOptionViews { get { return _currentOptionViews; } }

    private List<VillageEventOptionView> _currentOptionViews = new List<VillageEventOptionView>();

    /// <summary>当前展示的 Legacy Pending（Legacy 兼容模式；避免每帧重画）。</summary>
    private VillagePendingResponse _legacyPendingShown;

    private void Start()
    {
        InitializeOnce();
    }

    private void InitializeOnce()
    {
        if (_initialized) return;
        _initialized = true;
        ResolveBindings();
        BindButtons();
        SetVisible(false);
    }

    public void ShowAdvancePending(string instanceId)
    {
        if (string.IsNullOrEmpty(instanceId)) return;
        InitializeOnce();
        if (_advanceInstanceId == instanceId && _isOpen && gameObject.activeInHierarchy) return;
        if (!_advanceMounted)
        {
            _advanceOriginalParent = transform.parent;
            _advanceOriginalSibling = transform.GetSiblingIndex();
            var rect = transform as RectTransform;
            if (rect != null)
            {
                _mountAnchorMin=rect.anchorMin; _mountAnchorMax=rect.anchorMax; _mountPivot=rect.pivot;
                _mountSize=rect.sizeDelta; _mountPosition=rect.anchoredPosition3D;
                _mountScale=rect.localScale; _mountRotation=rect.localRotation;
            }
            var host = GameObject.Find("SceneUIHosts");
            if (host == null) return; // Fail closed; never activate/initialize the entire Town.
            transform.SetParent(host.transform, false);
            // A nested Canvas inherits its original root. Once detached for Return waiting,
            // explicitly make the existing event Canvas a screen overlay, then restore it.
            _advanceCanvas = root != null ? root.GetComponent<Canvas>() : null;
            if (_advanceCanvas != null)
            {
                _advanceOriginalRenderMode = _advanceCanvas.renderMode;
                _advanceOriginalOverrideSorting = _advanceCanvas.overrideSorting;
                _advanceOriginalSortingOrder = _advanceCanvas.sortingOrder;
                _advanceCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                _advanceCanvas.overrideSorting = true;
                _advanceCanvas.sortingOrder = 211;
            }
            _advanceMounted = true;
        }
        _advanceInstanceId = instanceId;
        gameObject.SetActive(true);
        var pending = EventRuntimeBridge.PendingVillageEvent;
        if (pending != null && pending.EventInstanceId == instanceId) ShowEventPending(pending);
        else
        {
            var legacy = VillageRuntimeBridge.GetPendingSocialResponse();
            if (VillageEventFlow.LegacyAdvanceInstanceId(legacy) == instanceId) ShowPending(legacy);
        }
    }

    private void RestoreAdvanceMount()
    {
        if (!_advanceMounted) return;
        transform.SetParent(_advanceOriginalParent, false);
        transform.SetSiblingIndex(_advanceOriginalSibling);
        if (_advanceCanvas != null)
        {
            _advanceCanvas.renderMode = _advanceOriginalRenderMode;
            _advanceCanvas.overrideSorting = _advanceOriginalOverrideSorting;
            _advanceCanvas.sortingOrder = _advanceOriginalSortingOrder;
            _advanceCanvas = null;
        }
        var rect = transform as RectTransform;
        if (rect != null)
        {
            rect.anchorMin=_mountAnchorMin; rect.anchorMax=_mountAnchorMax; rect.pivot=_mountPivot;
            rect.sizeDelta=_mountSize; rect.anchoredPosition3D=_mountPosition;
            rect.localScale=_mountScale; rect.localRotation=_mountRotation;
        }
        _advanceMounted = false; _advanceInstanceId = null;
    }

    private void Update()
    {
        if (_resultMode) return; // 结果 / 诊断文本等待玩家关闭
        if (Time.unscaledTime < _nextPollTime) return;
        _nextPollTime = Time.unscaledTime + PollInterval;

        // ① 新 Event 产品路径优先（唯一正式来源；WP-Event-1D §13）
        var instance = EventRuntimeBridge.PendingVillageEvent;
        if (instance != null)
        {
            if (!_isOpen || !_eventMode || _pendingEventInstance == null
                || !string.Equals(_pendingEventInstance.EventInstanceId, instance.EventInstanceId,
                    StringComparison.Ordinal))
            {
                ShowEventPending(instance);
            }
            return;
        }

        // ② 没有新 Event Pending 时才允许 Legacy 兼容模式
        //（只服务无法迁移的旧档 Pending；正式 Recognition 不再创建 Legacy Pending，§40）
        var legacy = VillageRuntimeBridge.GetPendingSocialResponse();
        if (legacy != null)
        {
            if (!_isOpen || !_eventMode || !ReferenceEquals(_legacyPendingShown, legacy))
            {
                ShowPending(legacy);
            }
            return;
        }

        if (_isOpen) SetVisible(false);
    }

    /// <summary>展示一条待处理回应（**Legacy 兼容模式**；只服务无法迁移的旧档 Pending）。</summary>
    public void ShowPending(VillagePendingResponse pending)
    {
        if (pending == null) return;
        _eventMode = false;
        _pendingEventInstance = null;
        _eventPresentation = null;
        _legacyPendingShown = pending;
        ClearDynamicOptionButtons();
        _resultMode = false;
        SetChoiceButtonsVisible(true);
        SetCloseButtonVisible(false);

        // V3C：Urgency 最小语义由 Pattern 目录唯一决定（本组件不硬编码分类）。
        var urgency = VillageTestSocialPatterns.ResolveUrgency(pending.PatternKey);

        if (titleText != null)
            titleText.text = "村民自述（V3B Social Response / Test / Not Frozen）　【" + UrgencyLabel(urgency) + "】";

        if (bodyText != null)
        {
            string name = PersonDisplayName(pending.PersonId);
            int count = 0;
            if (VillageRuntimeBridge.Time != null)
            {
                count = VillageRuntimeBridge.CountTemporaryCommitmentsInYear(pending.PersonId, VillageRuntimeBridge.Time.Year);
            }
            bodyText.text = "<b>" + name + "</b> 自述：\n“今年我已经第 " + count +
                            " 次被从原岗位抽去临时工程。”\n\n（这次的工程已按你的安排开始；下面的选择只影响之后的安排。）\n" +
                            UrgencyHint(urgency);
        }

        // V3C：PauseOnOpen（可忽略事件恒不强制暂停）。
        if (VillageTestSocialPatterns.ShouldPauseOnOpen(pauseOnOpen, urgency))
        {
            PauseForOpen();
        }

        SetVisible(true);
    }

    // ==================== Unified Event 产品模式（WP-Event-1D Gate D1 §13/§14） ====================

    /// <summary>
    /// 展示一条新的 Unified Event Pending：
    ///   - 标题 / 正文来自 `EventPresentation` 配置（唯一 `EventTemplateRenderer` 渲染，不硬编码产品文案）；
    ///   - 选项数量 / 文案 / 可用性来自 `EventOption` + `EventOptionCondition`（不硬编码两个产品选项）；
    ///   - 渲染失败 → **Fail Closed**（不显示半渲染占位符，改为可操作诊断）。
    /// </summary>
    public void ShowEventPending(ResolvedEventInstance instance)
    {
        InitializeOnce();
        if (instance == null) return;
        var advance = VillageRuntimeBridge.Time?.AdvanceSession;
        if (advance != null && advance.State == WorldAdvanceState.WaitingForInteraction
            && advance.BlockingEventInstanceId == instance.EventInstanceId)
            _advanceInstanceId = instance.EventInstanceId;

        _eventMode = true;
        _legacyPendingShown = null;
        _pendingEventInstance = instance;
        _resultMode = false;

        ClearDynamicOptionButtons();
        SetChoiceButtonsVisible(false);   // Legacy 两按钮在 Event 模式不参与（只作视觉模板）
        SetCloseButtonVisible(false);

        EventPresentationDefinition presentation;
        VillageEventStaticConfigAdapter.TryGetPresentation(instance.EventId, out presentation);
        _eventPresentation = presentation;

        string title;
        string body;
        string diagnostic;
        if (VillageEventFlow.TryRenderPendingTexts(instance, ResolveEventPersonName, out title, out body,
                out diagnostic))
        {
            if (titleText != null) titleText.text = title;
            if (bodyText != null) bodyText.text = body;
        }
        else
        {
            // Fail Closed：绝不把原始占位符 / 半渲染文本显示给玩家。
            if (titleText != null) titleText.text = "事件无法显示（内容配置问题）";
            if (bodyText != null) bodyText.text = diagnostic;
        }

        BuildOptionButtons(instance);

        if (presentation != null
            && VillageTestSocialPatterns.ShouldPauseOnOpen(presentation.PauseOnOpen,
                ParseUrgency(presentation.Urgency)))
        {
            PauseForOpen();
        }

        SetVisible(true);
    }

    /// <summary>按配置生成选项按钮（Legacy 按钮只作视觉模板；数量 / 文案 / 可用性全部来自配置）。</summary>
    private void BuildOptionButtons(ResolvedEventInstance instance)
    {
        string diagnostic;
        _currentOptionViews = VillageEventFlow.BuildOptionViews(instance, ResolveEventPersonName, out diagnostic);

        if (_currentOptionViews.Count == 0)
        {
            if (bodyText != null) bodyText.text = bodyText.text + "\n\n（当前没有可显示的处理选项：" + diagnostic + "）";
            SetCloseButtonVisible(true);
            _resultMode = true;   // 无选项 = 明确的死胡同：显示诊断并允许关闭
            return;
        }

        var template = grantRestButton != null ? grantRestButton : continueUsingButton;
        if (template == null || template.transform.parent == null)
        {
            if (bodyText != null) bodyText.text = bodyText.text + "\n\n（场景缺少选项按钮模板：无法渲染事件选项）";
            SetCloseButtonVisible(true);
            _resultMode = true;
            return;
        }

        for (int i = 0; i < _currentOptionViews.Count; i++)
        {
            var view = _currentOptionViews[i];
            var go = Instantiate(template.gameObject, template.transform.parent);
            go.name = "EventOption_" + view.OptionId;
            go.SetActive(true);

            var label = go.GetComponentInChildren<TMP_Text>(true);
            if (label != null)
            {
                label.text = string.IsNullOrEmpty(view.SubLabel) ? view.Label : view.Label + "\n" + view.SubLabel;
            }

            var button = go.GetComponent<Button>();
            if (button != null)
            {
                button.interactable = view.IsSelectable;
                button.onClick.RemoveAllListeners();
                string optionId = view.OptionId;
                button.onClick.AddListener(() => OnEventOptionClicked(optionId));
            }

            _dynamicOptionButtons.Add(go);
        }
    }

    /// <summary>销毁运行时生成的选项按钮（结果 / 诊断模式与关闭时调用）。</summary>
    private void ClearDynamicOptionButtons()
    {
        for (int i = 0; i < _dynamicOptionButtons.Count; i++)
        {
            if (_dynamicOptionButtons[i] != null) Destroy(_dynamicOptionButtons[i]);
        }
        _dynamicOptionButtons.Clear();
        _currentOptionViews = new List<VillageEventOptionView>();
    }

    /// <summary>
    /// **真实点击 Event Option**（Event 模式）：OptionCondition 重检 → Outcome → Effect Transaction。
    ///
    /// 语义（Fail Closed）：
    ///   - 成功 → Pending 清除 + 结果文案（`ResultTextTemplate` 或通用结果行）；
    ///   - 失败 → **Pending 保留** + 可操作诊断（不谎报成功、不静默丢弃玩家的这次选择）。
    /// </summary>
    public void OnEventOptionClicked(string optionId)
    {
        if (_resultMode) return;
        if (string.IsNullOrEmpty(optionId)) return;

        var instance = _pendingEventInstance != null
            ? _pendingEventInstance
            : EventRuntimeBridge.PendingVillageEvent;
        if (instance == null) return;
        if (EventRuntimeBridge.PendingVillageEvent?.EventInstanceId != instance.EventInstanceId) return;
        if (_advanceInstanceId != null && _advanceInstanceId != instance.EventInstanceId) return;

        string optionLabel = FindOptionLabel(optionId);

        EventOptionExecutionResult result;
        bool success = VillageEventFlow.TryExecutePendingOption(optionId, out result);

        _resultMode = true;
        ClearDynamicOptionButtons();
        SetChoiceButtonsVisible(false);
        SetCloseButtonVisible(true);

        if (!success)
        {
            if (bodyText != null)
            {
                string reason = result != null && !string.IsNullOrEmpty(result.Diagnostic)
                    ? result.Diagnostic
                    : VillageEventFlow.LastDiagnostic;
                bodyText.text = "本次选择没有被执行（事件仍待处理）：\n" + reason;
            }
            return;
        }

        _pendingEventInstance = null;
        if (bodyText != null)
        {
            bodyText.text = VillageEventFlow.RenderResultText(instance, result, optionLabel,
                ResolveEventPersonName);
        }
    }

    /// <summary>取该选项在本次视图中的渲染文案（结果行引用它；取不到时退回 OptionId）。</summary>
    private string FindOptionLabel(string optionId)
    {
        for (int i = 0; i < _currentOptionViews.Count; i++)
        {
            if (_currentOptionViews[i] == null) continue;
            if (string.Equals(_currentOptionViews[i].OptionId, optionId, StringComparison.Ordinal))
            {
                return _currentOptionViews[i].Label ?? string.Empty;
            }
        }
        return optionId;
    }

    /// <summary>配置 Urgency → 最小语义（未知取值 → Timely 安全默认，不静默降级为「可忽略」）。</summary>
    private static VillageSocialUrgency ParseUrgency(string urgency)
    {
        return string.Equals(urgency, "Ignorable", StringComparison.Ordinal)
            ? VillageSocialUrgency.Ignorable
            : VillageSocialUrgency.Timely;
    }

    /// <summary>事件模板的 Person 显示名解析（产品侧提供；解析不到 → false，禁止用 RoleName / PersonId 冒充）。</summary>
    private bool ResolveEventPersonName(string personId, out string displayName)
    {
        displayName = null;
        if (string.IsNullOrEmpty(personId)) return false;

        var town = FindObjectOfType<TownUI>(true);
        if (town != null && town.PlayerData != null)
        {
            var person = town.PlayerData.FindPersonByPersonId(personId);
            if (person != null && !string.IsNullOrEmpty(person.DisplayName))
            {
                displayName = person.DisplayName;
                return true;
            }
        }

        return VillageEventFlow.TryResolvePersonDisplayName(personId, out displayName);
    }

    /// <summary>Choice A：给明确恢复窗口（至本月最后一天；含当天仍拦截，下月 1 日起恢复）。</summary>
    public void OnGrantRestClicked()
    {
        var pending = VillageRuntimeBridge.GetPendingSocialResponse();
        if (_resultMode || pending == null || !ReferenceEquals(_legacyPendingShown, pending)) return;
        if (_advanceInstanceId != null && _advanceInstanceId != VillageEventFlow.LegacyAdvanceInstanceId(pending)) return;
        string personId = pending.PersonId;

        if (!VillageRuntimeBridge.ResolveSocialResponse(VillageSocialResponseChoice.GrantRest)) return;

        _resultMode = true;
        SetChoiceButtonsVisible(false);
        SetCloseButtonVisible(true);
        if (bodyText != null)
        {
            bodyText.text = "已记录你的选择：给他一段明确的恢复窗口。\n" +
                            VillageRuntimeBridge.GetPersonRestDescription(personId) +
                            "\n（当前进行的工程不受影响，正常继续。）";
        }
    }

    /// <summary>Choice B：当前仍需要他继续承担（不产生任何惩罚或隐藏负值）。</summary>
    public void OnContinueUsingClicked()
    {
        var pending = VillageRuntimeBridge.GetPendingSocialResponse();
        if (_resultMode || pending == null || !ReferenceEquals(_legacyPendingShown, pending)) return;
        if (_advanceInstanceId != null && _advanceInstanceId != VillageEventFlow.LegacyAdvanceInstanceId(pending)) return;
        if (!VillageRuntimeBridge.ResolveSocialResponse(VillageSocialResponseChoice.ContinueUsing)) return;

        _resultMode = true;
        SetChoiceButtonsVisible(false);
        SetCloseButtonVisible(true);
        if (bodyText != null)
        {
            bodyText.text = "已记录你的选择：目前仍需要他继续承担临时任务。\n后续安排不受影响，也不会因此产生任何惩罚。";
        }
    }

    /// <summary>结果展示后关闭（第一版无 Ignore：关闭只发生在已解析 / 已给出诊断之后）。</summary>
    public void OnCloseClicked()
    {
        // Clearing Pending or disabling this object is not an acknowledgement.
        // Restore the display speed while the session is still Waiting; Yield remains locked.
        RestoreAfterClose();
        bool resume = _resultMode && _advanceInstanceId != null
            && VillageRuntimeBridge.CloseHandledAdvanceInteraction(_advanceInstanceId);
        _resultMode = false;
        ClearDynamicOptionButtons();
        SetVisible(false);
        if (resume) RestoreAdvanceMount(); // The existing driver resumes on its next frame.
    }

    private void SetVisible(bool visible)
    {
        _isOpen = visible;
        if (root != null) root.SetActive(visible);

        // V3C：关闭 / pending 消失时，按打开前的 Speed 恢复（含「打开前本就是 Paused」的情况，不误恢复）。
        if (!visible) RestoreAfterClose();
    }

    /// <summary>组件被禁用 / 销毁时的安全网：不得把 VillageTime 永久留在本组件造成的暂停状态。</summary>
    private void OnDisable()
    {
        _resultMode = false;
        RestoreAfterClose();
    }

    // ==================== V3C：PauseOnOpen 生命周期（Test / Not Frozen） ====================

    /// <summary>
    /// 打开事件时暂停 VillageTime：记录打开前的 Speed，再 Pause。
    /// 若打开前本就 Paused，则不重复暂停，并在关闭时原样恢复（不误 Resume）。
    /// </summary>
    private void PauseForOpen()
    {
        if (_pausedByOpen) return;
        var time = VillageRuntimeBridge.Time;
        if (time == null) return;

        _speedBeforePause = time.Speed;
        _pausedByOpen = true;
        if (time.Speed != VillageTime.VillageTimeSpeed.Paused)
        {
            time.Pause();
        }
    }

    /// <summary>关闭 / 解决后恢复打开前的 Speed（未由本组件暂停时无副作用）。</summary>
    private void RestoreAfterClose()
    {
        if (!_pausedByOpen) return;
        _pausedByOpen = false;

        var time = VillageRuntimeBridge.Time;
        if (time == null) return;
        time.SetSpeed(_speedBeforePause);
    }

    /// <summary>Urgency 可读标签（Test / Not Frozen 文案；仅两种最小区分）。</summary>
    private static string UrgencyLabel(VillageSocialUrgency urgency)
    {
        return VillageSocialResponseLog.IsIgnorable(urgency) ? "可忽略（非强制处理）" : "需要及时处理";
    }

    /// <summary>Urgency 说明行（Test / Not Frozen 文案；不承诺任何 Deadline / 惩罚 / 忽略按钮）。</summary>
    private static string UrgencyHint(VillageSocialUrgency urgency)
    {
        if (VillageSocialResponseLog.IsIgnorable(urgency))
        {
            return "（紧迫性：可忽略——不强制立刻处理；V3 不提供忽略按钮，也不计任何惩罚。）";
        }
        return "（紧迫性：需要及时处理——他正在等你给出方向。）";
    }

    private void SetChoiceButtonsVisible(bool visible)
    {
        if (grantRestButton != null) grantRestButton.gameObject.SetActive(visible);
        if (continueUsingButton != null) continueUsingButton.gameObject.SetActive(visible);
    }

    private void SetCloseButtonVisible(bool visible)
    {
        if (closeButton != null) closeButton.gameObject.SetActive(visible);
    }

    private void BindButtons()
    {
        if (grantRestButton != null)
        {
            grantRestButton.onClick.RemoveAllListeners();
            grantRestButton.onClick.AddListener(OnGrantRestClicked);
        }
        if (continueUsingButton != null)
        {
            continueUsingButton.onClick.RemoveAllListeners();
            continueUsingButton.onClick.AddListener(OnContinueUsingClicked);
        }
        if (closeButton != null)
        {
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(OnCloseClicked);
        }
    }

    private void ResolveBindings()
    {
        if (root == null)
        {
            var t = FindDeep("PanelRoot");
            if (t != null) root = t.gameObject;
        }
        if (titleText == null) titleText = FindText("TitleText");
        if (bodyText == null) bodyText = FindText("BodyText");
        if (grantRestButton == null) grantRestButton = FindButton("BtnGrantRest");
        if (continueUsingButton == null) continueUsingButton = FindButton("BtnContinueUsing");
        if (closeButton == null) closeButton = FindButton("BtnClose");
    }

    private Transform FindDeep(string name)
    {
        if (transform.name == name) return transform;
        foreach (var t in GetComponentsInChildren<Transform>(true))
        {
            if (t.name == name) return t;
        }
        return null;
    }

    private TMP_Text FindText(string name)
    {
        var t = FindDeep(name);
        return t != null ? t.GetComponent<TMP_Text>() : null;
    }

    private Button FindButton(string name)
    {
        var t = FindDeep(name);
        return t != null ? t.GetComponent<Button>() : null;
    }

    private string PersonDisplayName(string personId)
    {
        if (string.IsNullOrEmpty(personId)) return personId;

        var town = FindObjectOfType<TownUI>(true);
        if (town != null && town.PlayerData != null)
        {
            // M1：Social 事件的 PersonId 是真实人物身份，须按 PersonId 反查显示名（不再按模板匹配）
            var person = town.PlayerData.FindPersonByPersonId(personId);
            if (person != null && !string.IsNullOrEmpty(person.DisplayName)) return person.DisplayName;
        }
        return personId;
    }
}
