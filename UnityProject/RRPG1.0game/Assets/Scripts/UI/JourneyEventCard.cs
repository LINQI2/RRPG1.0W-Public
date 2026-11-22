using TMPro;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// JourneyEventOptionRole —— Option 的**表现路由角色**（不是规则 Authority）。
/// 适配器据此把玩家选择交回各自已有的 Current Rule Entry。
/// </summary>
public enum JourneyEventOptionRole
{
    ContinueContent = 0,   // 普通 / 占位 Encounter：确认后走既有 Compatibility Resolve 行为
    EnterBattle = 1,       // 战斗 Encounter：确认后走既有 Battle 产品入口
    ChooseBranch = 2,      // Branch：确认后走既有 Branch / Node Selection 入口

    /// <summary>正式 Event 的 Option（WP-Event-1D Gate D2）：点击后交回 Event Core 执行。</summary>
    EventOption = 3,

    /// <summary>正式 Event 结果卡的「继续」：点击后结算当前 Event Node（§15）。</summary>
    ContinueResult = 4
}

/// <summary>
/// JourneyEventCardOption —— 统一卡面的**唯一** Option 数据模型。
/// Label 必须为玩家可读非空文案；NodeId 只是表现层携带的引用，不构成规则判定。
/// </summary>
[Serializable]
public class JourneyEventCardOption
{
    public string Label;
    public string SubLabel;
    public string NodeId;
    public JourneyEventOptionRole Role;

    /// <summary>
    /// 正式 Event 的 OptionId（WP-Event-1D Gate D2）：仅表现层携带的引用
    ///（规则执行由产品 Adapter 用它与 Event Core 交互）；非 Event 卡为 null。
    /// </summary>
    public string OptionId;

    /// <summary>是否可点选（Event Core 的 Option availability：Disable → false；其余场景恒 true）。</summary>
    public bool Interactable = true;
}

/// <summary>
/// JourneyEventCardConfig —— 统一 Event / Encounter Card Presentation 的**唯一**配置模型。
///
/// 由适配器构造（Battle Encounter / 普通 Encounter / Branch 共用同一模型与同一 View）：
///   Node / Encounter / Branch → JourneyEventCardConfig → JourneyEventCardView → 玩家选择 Option
///     → Option 调用对应的 Current Rule Entry（由适配器负责，不在此模型内）
///
/// 边界：本模型不决定合法 Node、不修改 Progression State、不建立第二套 Event Gameplay Authority。
/// </summary>
[Serializable]
public class JourneyEventCardConfig
{
    public JourneyEncounterKind Kind = JourneyEncounterKind.Content;
    public Sprite Illustration;
    public string Title;
    public string Body;
    public List<JourneyEventCardOption> Options = new List<JourneyEventCardOption>();
}

/// <summary>
/// JourneyEventCardView —— **单一** Event / Encounter Card 表现宿主（Renderer）。
///
/// 结构（Scene 稳定宿主；Illustration / Title / Body / Options 四区）：
///   root（本对象 = EncounterOverlay）
///   ├─ DimBlocker（全屏模态阻断；可空）
///   └─ Panel
///      ├─ ImageHost（插图宿主，可替换）
///      ├─ TitleHost（标题）
///      ├─ BodyTextHost（正文）
///      └─ ChoiceRoot（Options 列表；按钮由本 Renderer 统一生成）
///
/// 只负责渲染 + 上报「玩家选择了第 i 个 Option」；规则行为一律由调用方适配器执行。
/// </summary>
public class JourneyEventCardView : MonoBehaviour
{
    public const string OptionButtonPrefix = "BtnOption_";

    [Header("Card Hosts（Battle / 普通 Encounter / Branch 共用同一套）")]
    public GameObject root;
    public GameObject dimBlocker;
    public Image illustrationHost;
    public TMP_Text titleHost;
    public TMP_Text bodyHost;
    public RectTransform optionRoot;

    [Header("Option 版式（Inspector 可调；未冻结）")]
    public Vector2 optionSize = new Vector2(560f, 62f);
    public float optionSpacing = 70f;
    public int optionFontSize = 24;
    public int optionSubFontSize = 17;

    private readonly List<GameObject> _optionButtons = new List<GameObject>();
    private Action<int> _onOptionChosen;

    /// <summary>卡面是否可见（只读；测试 / 诊断）。</summary>
    public bool IsVisible
    {
        get
        {
            var target = root != null ? root : gameObject;
            return target != null && target.activeSelf;
        }
    }

    /// <summary>当前渲染的 Option 数（只读；测试 / 诊断）。</summary>
    public int OptionCount => _optionButtons.Count;

    public JourneyEncounterKind RenderedKind { get; private set; } = JourneyEncounterKind.None;
    public string RenderedTitle => titleHost != null ? titleHost.text : string.Empty;
    public string RenderedBody => bodyHost != null ? bodyHost.text : string.Empty;

    private void Awake()
    {
        if (root == null) root = gameObject;
    }

    /// <summary>渲染统一卡面；Option 点击后只上报索引（规则入口由调用方负责）。</summary>
    public void Render(JourneyEventCardConfig config, Action<int> onOptionChosen)
    {
        if (config == null)
        {
            Debug.LogError("[JourneyEventCard] Render 收到空 Config：拒绝渲染（不得静默显示空卡面）。");
            return;
        }

        _onOptionChosen = onOptionChosen;
        RenderedKind = config.Kind;

        if (illustrationHost != null)
        {
            illustrationHost.sprite = config.Illustration;
            illustrationHost.enabled = config.Illustration != null;
            illustrationHost.raycastTarget = false;
        }
        if (titleHost != null) titleHost.text = string.IsNullOrEmpty(config.Title) ? "遭遇" : config.Title;
        if (bodyHost != null) bodyHost.text = config.Body ?? string.Empty;

        ClearOptions();

        var font = ResolveFont();
        if (optionRoot != null && config.Options != null)
        {
            float centerOffset = (config.Options.Count - 1) * 0.5f * optionSpacing;
            for (int i = 0; i < config.Options.Count; i++)
            {
                CreateOptionButton(config.Options[i], i, centerOffset, font);
            }
        }

        if (dimBlocker != null) dimBlocker.SetActive(true);
        var target = root != null ? root : gameObject;
        if (target != null) target.SetActive(true);
    }

    /// <summary>隐藏卡面（不保留回调）。</summary>
    public void Hide()
    {
        ClearOptions();
        if (dimBlocker != null) dimBlocker.SetActive(false);
        var target = root != null ? root : gameObject;
        if (target != null) target.SetActive(false);
    }

    /// <summary>取消：清空回调后隐藏（Return / Hide / Battle / 新 Cycle 使旧回调失效）。</summary>
    public void Cancel()
    {
        _onOptionChosen = null;
        RenderedKind = JourneyEncounterKind.None;
        Hide();
    }

    /// <summary>共享字体来源：优先标题宿主（Scene 已绑定字体），其次正文宿主。</summary>
    public TMP_FontAsset ResolveFont()
    {
        if (titleHost != null && titleHost.font != null) return titleHost.font;
        if (bodyHost != null && bodyHost.font != null) return bodyHost.font;
        return null;
    }

    private void CreateOptionButton(JourneyEventCardOption option, int index, float centerOffset, TMP_FontAsset font)
    {
        string label = option != null && !string.IsNullOrEmpty(option.Label)
            ? option.Label
            : ("选项 " + (index + 1));   // 防御：绝不留空白选项

        var go = new GameObject(OptionButtonPrefix + index, typeof(RectTransform));
        go.transform.SetParent(optionRoot, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = optionSize;
        rt.anchoredPosition = new Vector2(0f, index * optionSpacing - centerOffset);

        var img = go.AddComponent<Image>();
        img.color = new Color(0.22f, 0.34f, 0.5f, 1f);
        img.raycastTarget = true;
        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        // WP-Event-1D Gate D2：Event Core 的 Option availability（Disable）必须如实反映为不可点选。
        if (option != null) btn.interactable = option.Interactable;

        var labelRt = NewStretchRect("Label", rt);
        labelRt.anchoredPosition = new Vector2(0f, string.IsNullOrEmpty(option.SubLabel) ? 0f : 10f);
        labelRt.gameObject.AddComponent<CanvasRenderer>();
        var text = labelRt.gameObject.AddComponent<TextMeshProUGUI>();
        text.font = CurrentUIFontAuthority.Default;
        text.text = label;
        text.fontSize = optionFontSize;
        text.color = option != null && !option.Interactable ? new Color(1f, 1f, 1f, 0.45f) : Color.white;
        text.alignment = TextAlignmentOptions.Center;
        text.enableWordWrapping = true;
        text.overflowMode = TextOverflowModes.Overflow;
        text.raycastTarget = false;

        if (!string.IsNullOrEmpty(option.SubLabel))
        {
            var subRt = NewStretchRect("SubLabel", rt);
            subRt.anchoredPosition = new Vector2(0f, -16f);
            subRt.gameObject.AddComponent<CanvasRenderer>();
            var sub = subRt.gameObject.AddComponent<TextMeshProUGUI>();
            sub.font = CurrentUIFontAuthority.Default;
            sub.text = option.SubLabel;
            sub.fontSize = optionSubFontSize;
            sub.color = new Color(1f, 1f, 1f, 0.75f);
            sub.alignment = TextAlignmentOptions.Center;
            sub.raycastTarget = false;
        }

        int captured = index;
        btn.onClick.AddListener(() => Choose(captured));
        _optionButtons.Add(go);
    }

    private static RectTransform NewStretchRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        return rt;
    }

    /// <summary>玩家选择第 i 个 Option：隐藏卡面后恰好回调一次。</summary>
    private void Choose(int index)
    {
        var cb = _onOptionChosen;
        _onOptionChosen = null;
        Hide();
        if (cb != null) cb(index);
    }

    private void ClearOptions()
    {
        for (int i = 0; i < _optionButtons.Count; i++)
        {
            if (_optionButtons[i] != null) Destroy(_optionButtons[i]);
        }
        _optionButtons.Clear();
    }
}
