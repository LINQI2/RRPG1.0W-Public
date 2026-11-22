using TMPro;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ExpeditionProgressionPanel —— **Legacy / Compatibility / Test seam（WP-E 起不再是 Current 产品表面）**。
///
/// WP-E（Expedition Journey Visual Foundation）后，Current 玩家表面是 `ExpeditionJourneyRoot`
/// （`ExpeditionJourneyCoordinator` + `ExpeditionJourneyView`）；本面板只在
/// Journey 宿主缺失（找不到 Scene 宿主，属异常环境）时作为 Compatibility fallback 显示，
/// 或作为既有 Test seam 供旧测试使用。**不得**恢复为与 Journey 同屏的正式玩家表面。
///
/// 历史职责（保留功能语义，未删除）：最小远征推进表现 —— 当前 Supply 可见；
/// N=1 只呈现「继续前进」；N&gt;1 只呈现当次合法 Branch；0 后继不自动判胜；
/// Enter 被拒绝时显示真实原因；Retreat / Return 始终可用。
///
/// 边界（硬约束）：
///   - 不持有任何状态 / 不持久化 / 不新增 Authority；
///   - 不复制 Supply 规则（只读 CurrentCarryUnits / InitialCarryUnits）；
///   - 不新建第二套 Return 逻辑（按钮只回调 ExpeditionUI 的产品方法）；
///   - 视觉与布局不冻结（运行时自建最小 UI，不改 Scene / Prefab / SO）。
/// </summary>
public class ExpeditionProgressionPanel : MonoBehaviour
{
    private Canvas _canvas;
    private TMP_Text _titleText;
    private TMP_Text _supplyText;
    private TMP_Text _messageText;
    private RectTransform _choiceRoot;
    private Button _retreatButton;
    private bool _built;

    private AdventureManager _adventure;
    private ExpeditionUI _owner;
    private readonly List<GameObject> _choiceButtons = new List<GameObject>();

    /// <summary>最近一次进入被拒绝的原因（可读文本；空 = 无）。</summary>
    public string LastDeniedText { get; private set; } = string.Empty;

    /// <summary>当前是否可见。</summary>
    public bool IsVisible => _canvas != null && _canvas.enabled;

    /// <summary>当前 Supply 文本（供产品测试/诊断只读）。</summary>
    public string SupplyDisplay => _supplyText != null ? _supplyText.text : string.Empty;

    public void Show(AdventureManager adventure, ExpeditionUI owner)
    {
        _adventure = adventure;
        _owner = owner;
        LastDeniedText = string.Empty;
        EnsureBuilt();
        _canvas.enabled = true;
        Refresh();
    }

    public void Hide()
    {
        if (_canvas != null) _canvas.enabled = false;
    }

    /// <summary>进入被拒绝：在可见表面显示真实原因（不再只写隐藏的 ExpeditionUI.infoText）。</summary>
    public void ShowDenied(ExpeditionEnterResult result, string describe)
    {
        EnsureBuilt();
        _canvas.enabled = true;
        LastDeniedText = describe;
        if (_messageText != null)
        {
            _messageText.text = result == ExpeditionEnterResult.InsufficientSupply
                ? "补给不足，无法继续前进（" + describe + "）"
                : "无法进入：" + describe;
        }
        RefreshSupplyAndChoices();
    }

    public void Refresh()
    {
        EnsureBuilt();
        LastDeniedText = string.Empty;
        if (_messageText != null) _messageText.text = string.Empty;
        RefreshSupplyAndChoices();
    }

    // ==================== 内部 ====================

    private void RefreshSupplyAndChoices()
    {
        var state = _adventure != null ? _adventure.ProgressionState : null;

        if (_supplyText != null)
        {
            if (state != null && state.Supply != null)
            {
                _supplyText.text = "补给：" + state.Supply.CurrentCarryUnits
                    + "（初始 " + state.Supply.InitialCarryUnits + "）";
            }
            else
            {
                _supplyText.text = "补给：—";
            }
        }

        if (_titleText != null)
        {
            string node = state != null && !string.IsNullOrEmpty(state.CurrentNodeId)
                ? state.CurrentNodeId : "（入口）";
            _titleText.text = "远征推进（Node-based Progression / Test / Not Frozen）　当前节点：" + node;
        }

        ClearChoiceButtons();
        if (_adventure == null) return;

        var available = _adventure.GetAvailableProgressionNodes();
        int count = available != null ? available.Count : 0;
        if (count == 0)
        {
            // 0 后继：不自动解释为胜利（交给 Progression / Return 既有规则）；仅提示玩家可撤退
            if (_messageText != null && string.IsNullOrEmpty(_messageText.text))
                _messageText.text = "当前路线已到终点：可选择「撤退 / 返回领地」。";
            return;
        }

        for (int i = 0; i < count; i++)
        {
            var node = available[i];
            string nodeId = node.NodeId;
            string label = count == 1
                ? "继续前进（" + node.NodeKind + "）"
                : ("分支 " + (i + 1) + "：" + node.NodeKind);

            var btn = NewButton(_choiceRoot, "BtnChoice_" + i, label, new Vector2(0f, 110f - i * 54f));
            btn.onClick.AddListener(() =>
            {
                if (_owner != null) _owner.OnProgressionNodeChosen(nodeId);
            });
            _choiceButtons.Add(btn.gameObject);
        }
    }

    private void EnsureBuilt()
    {
        if (_built) return;
        _built = true;

        var canvasGo = new GameObject("ExpeditionProgressionCanvas");
        canvasGo.transform.SetParent(transform, false);
        _canvas = canvasGo.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 300; // Battle 期间本面板隐藏；高于 Legacy MapCanvas 常规 UI
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        // 关键修复：运行时自建 Canvas 必须带 GraphicRaycaster，否则 EventSystem 无法命中其上的按钮
        //（人工验收发现「继续前进 / 撤退」点击无反应）。
        canvasGo.AddComponent<GraphicRaycaster>();

        var root = canvasGo.GetComponent<RectTransform>();
        root.anchorMin = new Vector2(0.5f, 0.5f);
        root.anchorMax = new Vector2(0.5f, 0.5f);
        root.sizeDelta = new Vector2(760f, 560f);
        root.anchoredPosition = Vector2.zero;

        var font = FindExistingFont();

        var bg = new GameObject("Background", typeof(RectTransform));
        bg.transform.SetParent(root, false);
        var bgImg = bg.AddComponent<Image>();
        bgImg.color = new Color(0f, 0f, 0f, 0.85f);
        bgImg.raycastTarget = false; // 面板背景永不拦截（只有按钮可点；不留下不可见 blocker）
        var bgRt = bg.GetComponent<RectTransform>();
        bgRt.anchorMin = Vector2.zero;
        bgRt.anchorMax = Vector2.one;
        bgRt.offsetMin = Vector2.zero;
        bgRt.offsetMax = Vector2.zero;

        _titleText = NewText(root, "Title", "远征推进", 24, new Vector2(0f, 240f), 720f, font);
        _supplyText = NewText(root, "Supply", "补给：—", 28, new Vector2(0f, 196f), 720f, font);
        _messageText = NewText(root, "Message", string.Empty, 22, new Vector2(0f, 152f), 720f, font);
        _messageText.color = new Color(1f, 0.82f, 0.4f);

        var choiceGo = new GameObject("Choices", typeof(RectTransform));
        choiceGo.transform.SetParent(root, false);
        _choiceRoot = choiceGo.GetComponent<RectTransform>();
        _choiceRoot.anchorMin = new Vector2(0.5f, 0.5f);
        _choiceRoot.anchorMax = new Vector2(0.5f, 0.5f);
        _choiceRoot.sizeDelta = new Vector2(720f, 300f);
        _choiceRoot.anchoredPosition = new Vector2(0f, 20f);

        _retreatButton = NewButton(root, "BtnRetreat", "撤退 / 返回领地", new Vector2(0f, -230f));
        _retreatButton.onClick.AddListener(() =>
        {
            if (_owner != null) _owner.RequestRetreatFromProgression();
        });
    }

    private Button NewButton(RectTransform parent, string name, string label, Vector2 pos)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(460f, 44f);
        rt.anchoredPosition = pos;

        var img = go.AddComponent<Image>();
        img.color = new Color(0.22f, 0.34f, 0.5f, 1f);
        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;

        var text = NewText(rt, "Label", label, 20, Vector2.zero, 440f, FindExistingFont(), true);
        if (text != null) text.alignment = TextAlignmentOptions.Center;
        return btn;
    }

    private static TMP_Text NewText(RectTransform parent, string name, string content, int fontSize,
        Vector2 pos, float width, TMP_FontAsset font, bool centered = false)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        go.AddComponent<CanvasRenderer>();
        var text = go.AddComponent<TextMeshProUGUI>();
        text.font = CurrentUIFontAuthority.Default;
        text.text = content;
        text.fontSize = fontSize;
        text.color = Color.white;
        text.alignment = centered ? TextAlignmentOptions.Center : TextAlignmentOptions.Left;
        text.enableWordWrapping = true;
        text.overflowMode = TextOverflowModes.Overflow;
        text.raycastTarget = false;
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(width, fontSize + 12f);
        rt.anchoredPosition = pos;
        return text;
    }

    private static TMP_FontAsset FindExistingFont()
    {
        var any = Object.FindObjectOfType<TMP_Text>(true);
        return any != null ? any.font : null;
    }

    private void ClearChoiceButtons()
    {
        for (int i = 0; i < _choiceButtons.Count; i++)
        {
            if (_choiceButtons[i] != null) Destroy(_choiceButtons[i]);
        }
        _choiceButtons.Clear();
    }
}
