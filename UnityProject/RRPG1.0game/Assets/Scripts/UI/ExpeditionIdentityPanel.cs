using TMPro;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ExpeditionIdentityPanel —— **V4 Functional Repair / Functional Test / Not Frozen**：
/// 远征准备界面的「人物详情」入口 + 局部只读 Popup + 出征前 Village Food 可见性。
///
/// 修复要点（真实人工验收发现）：
///   - 旧实现把身份文本画在**常驻黑色半透明整块**上，且运行时自建 Canvas **缺少 GraphicRaycaster**
///     → 文字不可读、并造成遮挡/交互异常；
///   - 现在：默认只显示小型「人物详情」按钮与补给信息行；**Popup 默认关闭且 inactive**，
///     未打开时不拦截任何其它 UI Pointer；只有打开时才使用全屏 dim（真实 Modal）。
///
/// 边界：内容只读自现有 Authority（ExpeditionPersonIdentity / VillageRuntimeBridge.Resources），
/// 不新增状态 / Save / Authority / 规则；Hero 不新增 LifeHistory；视觉与布局不冻结。
/// </summary>
public class ExpeditionIdentityPanel : MonoBehaviour
{
    private Canvas _canvas;
    private Button _detailButton;
    private TMP_Text _supplyText;
    private GameObject _popupRoot;
    private TMP_Text _detailText;
    private Button _closeButton;
    private ScrollRect _scrollRect;
    private bool _built;

    private string _cachedDetail = string.Empty;

    /// <summary>Popup 是否打开（真实 Modal 判定）。</summary>
    public bool IsPopupOpen => _popupRoot != null && _popupRoot.activeSelf;

    public bool IsVisible => _canvas != null && _canvas.enabled;

    /// <summary>补给信息文本（村庄食物 / 预计携带 / 第一步预计成本；只读断言用）。</summary>
    public string SupplyText => _supplyText != null ? _supplyText.text : string.Empty;

    /// <summary>Popup 详情文本（只读断言用）。</summary>
    public string DetailText => _detailText != null ? _detailText.text : string.Empty;

    /// <summary>字体可用性（只读诊断：font == null 会导致文本不可见）。</summary>
    public bool IsDetailTextFontAvailable => _detailText != null && _detailText.font != null;

    public bool IsSupplyTextFontAvailable => _supplyText != null && _supplyText.font != null;

    private TMP_FontAsset _font;

    /// <summary>
    /// 防御：运行时自建 TMP_Text 若在字体尚未可用时创建，文本会完全不可见（人工验收可见性风险）。
    /// 这里在字体为空时重试获取（与既有 VillageWorldLayer 的字体重试一致），不改变任何数据/规则。
    /// </summary>
    private void Update()
    {
        if (_font != null) return;

        var font = FindExistingFont();
        if (font == null) return;

        _font = font;
        if (_supplyText != null) _supplyText.font = CurrentUIFontAuthority.Default;
        if (_detailText != null) _detailText.font = CurrentUIFontAuthority.Default;
    }

    public Button DetailButton => _detailButton;

    public Button CloseButton => _closeButton;

    public void Show()
    {
        EnsureBuilt();
        _canvas.enabled = true;
        ClosePopup(); // 默认关闭：未打开时不拦截任何 Pointer
    }

    public void Hide()
    {
        if (_canvas != null) _canvas.enabled = false;
        ClosePopup();
    }

    /// <summary>设置补给信息（只读展示；由 ExpeditionUI 用现有 Food / Carry policy 计算后传入）。</summary>
    public void SetSupplyText(string text)
    {
        EnsureBuilt();
        if (_supplyText != null) _supplyText.text = text;
    }

    /// <summary>
    /// 缓存「**全部可查看候选人**」详情（Popup 打开时显示；关闭时不显示、不拦截）。
    /// 只读：行集合由 ExpeditionUI 按准备界面候选集（全部 Hero + 全部角色卡候选）构造；
    /// 本方法不改变任何出征选择（Detail ≠ Selection）。
    /// </summary>
    public void SetCandidateRows(PlayerCollection collection, IList<ExpeditionPersonDetailRow> rows)
    {
        EnsureBuilt();
        _cachedDetail = ExpeditionPersonIdentity.BuildDetailText(collection, rows);
        _candidateCount = rows != null ? rows.Count : 0;
        ApplyDetailText();
    }

    /// <summary>详情当前覆盖的候选人数（只读断言用）。</summary>
    public int CandidateCount => _candidateCount;

    /// <summary>把缓存的详情文本写入可见 TMP_Text，并自适应内容高度（超出视口时可滚动）。</summary>
    private void ApplyDetailText()
    {
        if (_detailText == null) return;

        _detailText.text = _cachedDetail;

        var contentRt = _detailText.rectTransform;
        float viewportHeight = _scrollRect != null && _scrollRect.viewport != null
            ? _scrollRect.viewport.rect.height : 0f;
        float needed = Mathf.Max(_detailText.preferredHeight + 12f, viewportHeight);
        contentRt.sizeDelta = new Vector2(contentRt.sizeDelta.x, needed);
        contentRt.anchoredPosition = Vector2.zero;
        if (_scrollRect != null) _scrollRect.verticalNormalizedPosition = 1f;

        _detailLayoutDirty = false;
    }

    private int _candidateCount;
    private bool _detailLayoutDirty;

    /// <summary>布局完成后（Canvas 布局在 LateUpdate 之前）再校正一次内容高度。</summary>
    private void LateUpdate()
    {
        if (!_detailLayoutDirty) return;
        ApplyDetailText();
    }

    /// <summary>打开局部只读 Popup（真实 Modal：dim 打开时 blocksRaycasts）。不得改变出征选择。</summary>
    public void OpenPopup()
    {
        EnsureBuilt();
        _canvas.enabled = true;
        if (_popupRoot != null) _popupRoot.SetActive(true);
        if (_detailButton != null) _detailButton.gameObject.SetActive(false);
        _detailLayoutDirty = true;   // 打开后按真实布局校正内容高度
        ApplyDetailText();
    }

    /// <summary>关闭 Popup：Popup 根 inactive → 不再拦截任何 Pointer。</summary>
    public void ClosePopup()
    {
        if (_popupRoot != null) _popupRoot.SetActive(false);
        if (_detailButton != null) _detailButton.gameObject.SetActive(true);
    }

    // ==================== 构建（运行时自建；含 GraphicRaycaster，确保可真实点击） ====================

    private void EnsureBuilt()
    {
        if (_built) return;
        _built = true;

        var canvasGo = new GameObject("ExpeditionIdentityCanvas");
        canvasGo.transform.SetParent(transform, false);
        _canvas = canvasGo.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 320;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        // 关键修复：运行时自建 Canvas 必须带 GraphicRaycaster，否则 EventSystem 无法命中其上的按钮。
        canvasGo.AddComponent<GraphicRaycaster>();

        var root = canvasGo.GetComponent<RectTransform>();
        root.anchorMin = Vector2.zero;
        root.anchorMax = Vector2.one;
        root.offsetMin = Vector2.zero;
        root.offsetMax = Vector2.zero;

        var font = FindExistingFont();
        _font = font; // null 时由 Update 重试（TMP_Text 字体为空会完全不可见）

        // ---- 常驻区域（右下角）：补给信息 + 人物详情入口（不遮挡主要选区） ----
        var supplyGo = new GameObject("PrepSupply", typeof(RectTransform));
        supplyGo.transform.SetParent(root, false);
        supplyGo.AddComponent<CanvasRenderer>();
        _supplyText = supplyGo.AddComponent<TextMeshProUGUI>();
        _supplyText.font = CurrentUIFontAuthority.Default;
        _supplyText.fontSize = 22;
        _supplyText.color = Color.white;
        _supplyText.alignment = TextAlignmentOptions.TopLeft;
        _supplyText.enableWordWrapping = true;
        _supplyText.overflowMode = TextOverflowModes.Overflow;
        _supplyText.raycastTarget = false; // 文本永不拦截
        _supplyText.text = "村庄食物：—";
        var supplyRt = supplyGo.GetComponent<RectTransform>();
        supplyRt.anchorMin = new Vector2(1f, 0f);
        supplyRt.anchorMax = new Vector2(1f, 0f);
        supplyRt.pivot = new Vector2(1f, 0f);
        supplyRt.sizeDelta = new Vector2(460f, 130f);
        supplyRt.anchoredPosition = new Vector2(-24f, 96f);

        _detailButton = NewButton(root, "BtnPersonDetail", "人物详情", font,
            new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-24f, 24f), new Vector2(220f, 56f));
        _detailButton.onClick.AddListener(OpenPopup);

        BuildPopup(root, font);
    }

    private static TMP_FontAsset FindExistingFont()
    {
        var any = Object.FindObjectOfType<TMP_Text>(true);
        return any != null ? any.font : null;
    }

    /// <summary>Popup（默认 inactive；打开时为真实 Modal）。</summary>
    private void BuildPopup(RectTransform root, TMP_FontAsset font)
    {
        _popupRoot = new GameObject("DetailPopup", typeof(RectTransform));
        _popupRoot.transform.SetParent(root, false);
        var popupRt = _popupRoot.GetComponent<RectTransform>();
        popupRt.anchorMin = Vector2.zero;
        popupRt.anchorMax = Vector2.one;
        popupRt.offsetMin = Vector2.zero;
        popupRt.offsetMax = Vector2.zero;

        var dimGo = new GameObject("Dim", typeof(RectTransform));
        dimGo.transform.SetParent(_popupRoot.transform, false);
        var dim = dimGo.AddComponent<Image>();
        dim.color = new Color(0f, 0f, 0f, 0.82f);
        dim.raycastTarget = true; // 只在 Popup 打开时存在（真实 Modal 遮挡）
        var dimRt = dimGo.GetComponent<RectTransform>();
        dimRt.anchorMin = Vector2.zero;
        dimRt.anchorMax = Vector2.one;
        dimRt.offsetMin = Vector2.zero;
        dimRt.offsetMax = Vector2.zero;

        var panelGo = new GameObject("Panel", typeof(RectTransform));
        panelGo.transform.SetParent(_popupRoot.transform, false);
        var panelImg = panelGo.AddComponent<Image>();
        panelImg.color = new Color(0.06f, 0.07f, 0.09f, 1f);
        var panelRt = panelGo.GetComponent<RectTransform>();
        panelRt.anchorMin = new Vector2(0.5f, 0.5f);
        panelRt.anchorMax = new Vector2(0.5f, 0.5f);
        panelRt.sizeDelta = new Vector2(1200f, 900f); // 默认人口下全部候选人可见；更多人时可滚动
        panelRt.anchoredPosition = Vector2.zero;

        // ---- 简单纵向列表（ScrollRect + RectMask2D；超出视口时可滚动；Test / Not Frozen） ----
        var scrollGo = new GameObject("ScrollArea", typeof(RectTransform));
        scrollGo.transform.SetParent(panelRt, false);
        var scrollRt = scrollGo.GetComponent<RectTransform>();
        scrollRt.anchorMin = Vector2.zero;
        scrollRt.anchorMax = Vector2.one;
        scrollRt.offsetMin = new Vector2(20f, 92f);
        scrollRt.offsetMax = new Vector2(-20f, -20f);
        _scrollRect = scrollGo.AddComponent<ScrollRect>();
        _scrollRect.horizontal = false;
        _scrollRect.vertical = true;
        _scrollRect.movementType = ScrollRect.MovementType.Clamped;
        _scrollRect.scrollSensitivity = 30f;
        _scrollRect.inertia = false;

        var viewportGo = new GameObject("Viewport", typeof(RectTransform));
        viewportGo.transform.SetParent(scrollRt, false);
        var viewportRt = viewportGo.GetComponent<RectTransform>();
        viewportRt.anchorMin = Vector2.zero;
        viewportRt.anchorMax = Vector2.one;
        viewportRt.offsetMin = Vector2.zero;
        viewportRt.offsetMax = Vector2.zero;
        var viewportImg = viewportGo.AddComponent<Image>();
        viewportImg.color = new Color(0f, 0f, 0f, 0.001f);
        viewportImg.raycastTarget = true; // 供滚轮 / 拖拽接收（仅 Popup 打开时存在）
        viewportGo.AddComponent<RectMask2D>();

        var textGo = new GameObject("Content", typeof(RectTransform));
        textGo.transform.SetParent(viewportRt, false);
        textGo.AddComponent<CanvasRenderer>();
        _detailText = textGo.AddComponent<TextMeshProUGUI>();
        _detailText.font = CurrentUIFontAuthority.Default;
        _detailText.fontSize = 21;
        _detailText.color = Color.white;
        _detailText.alignment = TextAlignmentOptions.TopLeft;
        _detailText.enableWordWrapping = true;
        _detailText.overflowMode = TextOverflowModes.Overflow;
        _detailText.raycastTarget = false;
        _detailText.text = "=== 可查看的远征候选人（只读 / Test / Not Frozen）===";
        var textRt = textGo.GetComponent<RectTransform>();
        textRt.anchorMin = new Vector2(0f, 1f);
        textRt.anchorMax = new Vector2(1f, 1f);
        textRt.pivot = new Vector2(0.5f, 1f);
        textRt.sizeDelta = new Vector2(0f, 200f);
        textRt.anchoredPosition = Vector2.zero;

        _scrollRect.viewport = viewportRt;
        _scrollRect.content = textRt;

        _closeButton = NewButton(panelRt, "BtnCloseDetail", "关闭", font,
            new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 20f), new Vector2(220f, 52f));
        _closeButton.onClick.AddListener(ClosePopup);

        _popupRoot.SetActive(false); // 默认关闭
        _detailLayoutDirty = true;
    }

    private static Button NewButton(RectTransform parent, string name, string label, TMP_FontAsset font,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPos, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = new Vector2(anchorMin.x, anchorMin.y);
        rt.sizeDelta = size;
        rt.anchoredPosition = anchoredPos;

        var img = go.AddComponent<Image>();
        img.color = new Color(0.22f, 0.34f, 0.5f, 1f);
        img.raycastTarget = true;
        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.interactable = true;

        var textGo = new GameObject("Label", typeof(RectTransform));
        textGo.transform.SetParent(rt, false);
        textGo.AddComponent<CanvasRenderer>();
        var text = textGo.AddComponent<TextMeshProUGUI>();
        text.font = CurrentUIFontAuthority.Default;
        text.text = label;
        text.fontSize = 22;
        text.color = Color.white;
        text.alignment = TextAlignmentOptions.Center;
        text.enableWordWrapping = true;
        text.overflowMode = TextOverflowModes.Overflow;
        text.raycastTarget = false;
        var textRt = textGo.GetComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = Vector2.zero;
        textRt.offsetMax = Vector2.zero;
        return btn;
    }
}
