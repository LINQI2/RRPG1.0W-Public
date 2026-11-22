using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// VillageSocialEventUIAuthoring —— V3B 最小 Event UI 的 Editor 构建工具（仅 Editor 使用）。
/// Build()：在 TownCanvas 下构建/重建 VillageSocialEventUI 宿主（独立子 Canvas sortingOrder=211，
/// 高于 Commitment 210），并把引用序列化到 VillageSocialEventUI 组件。幂等：先移除旧结构再重建。
/// 只修改 SampleScene.scene；不手改 YAML、不改其它 Scene / Prefab。
/// </summary>
public static class VillageSocialEventUIAuthoring
{
    private const string ScenePath = "Assets/Scenes/SampleScene.scene";

    /// <summary>在真实 Town 宿主（TownCanvas）下构建 V3B 事件 UI 宿主并保存场景。</summary>
    public static void Build()
    {
        if (!OpenSampleScene()) { EditorApplication.Exit(1); return; }

        var townCanvasGo = GameObject.Find("TownCanvas");
        if (townCanvasGo == null)
        {
            Debug.LogError("[VillageSocialEventUIAuthoring] 未找到 TownCanvas，无法集成。");
            EditorApplication.Exit(1);
            return;
        }
        var canvas = townCanvasGo.GetComponent<RectTransform>();

        // 幂等：移除旧结构
        var oldHost = canvas.Find("VillageSocialEventUI");
        if (oldHost != null) Object.DestroyImmediate(oldHost.gameObject);

        var host = CreateObject("VillageSocialEventUI", canvas);
        Stretch(host.GetComponent<RectTransform>());
        var ui = host.AddComponent<VillageSocialEventUI>();

        // Modal 层：PanelRoot 子 Canvas sortingOrder=211 + GraphicRaycaster + 全屏遮罩
        var panelRoot = CreateObject("PanelRoot", host.transform);
        Stretch(panelRoot.GetComponent<RectTransform>());
        var panelCanvas = panelRoot.AddComponent<Canvas>();
        panelCanvas.overrideSorting = true;
        panelCanvas.sortingOrder = 211;
        panelRoot.AddComponent<GraphicRaycaster>();
        var rootBg = panelRoot.AddComponent<Image>();
        rootBg.color = new Color(0.03f, 0.04f, 0.07f, 0.88f);
        rootBg.raycastTarget = true;

        // 中央面板
        var panel = CreateObject("PanelBox", panelRoot.transform);
        var panelRt = panel.GetComponent<RectTransform>();
        panelRt.anchorMin = new Vector2(0.5f, 0.5f);
        panelRt.anchorMax = new Vector2(0.5f, 0.5f);
        panelRt.pivot = new Vector2(0.5f, 0.5f);
        panelRt.anchoredPosition = Vector2.zero;
        panelRt.sizeDelta = new Vector2(860f, 470f);
        var panelImage = panel.AddComponent<Image>();
        panelImage.color = new Color(0.13f, 0.17f, 0.23f, 1f);
        panelImage.raycastTarget = true;
        var panelLayout = panel.AddComponent<VerticalLayoutGroup>();
        panelLayout.spacing = 8f;
        panelLayout.padding = new RectOffset(18, 18, 18, 18);
        panelLayout.childControlWidth = true;
        panelLayout.childControlHeight = false;
        panelLayout.childForceExpandWidth = true;
        panelLayout.childForceExpandHeight = false;

        // 内容
        var titleText = CreateText(panel.transform, "TitleText", "村民自述（V3B Social Response / Test / Not Frozen）", 24, 44);
        var bodyText = CreateText(panel.transform, "BodyText", "", 18, 170);
        var grantBtn = CreateButton(panel.transform, "BtnGrantRest",
            "Choice A：给他一段明确的恢复窗口（休整至本月最后一天；期间不再安排新的临时工程）");
        var continueBtn = CreateButton(panel.transform, "BtnContinueUsing",
            "Choice B：目前仍需要他继续承担临时任务");
        var closeBtn = CreateButton(panel.transform, "BtnClose", "知道了");

        // 绑定引用
        ui.root = panelRoot;
        ui.titleText = titleText;
        ui.bodyText = bodyText;
        ui.grantRestButton = grantBtn;
        ui.continueUsingButton = continueBtn;
        ui.closeButton = closeBtn;

        // 初始状态：整个 PanelRoot 在运行时 Start 隐藏；Edit Mode 保留可见可调；BtnClose 初始隐藏
        closeBtn.gameObject.SetActive(false);

        EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        Debug.Log("[VillageSocialEventUIAuthoring] Build 完成：VillageSocialEventUI 宿主已写入 SampleScene。");
        EditorApplication.Exit(0);
    }

    private static bool OpenSampleScene()
    {
        if (!System.IO.File.Exists(ScenePath))
        {
            Debug.LogError("[VillageSocialEventUIAuthoring] Scene not found: " + ScenePath);
            return false;
        }
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        return true;
    }

    // ==================== 构建辅助 ====================

    private static GameObject CreateObject(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    private static void Stretch(RectTransform rt, float inset = 0f)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(inset, inset);
        rt.offsetMax = new Vector2(-inset, -inset);
    }

    private static TMP_Text CreateText(Transform parent, string name, string text, int fontSize, float height)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 0.5f);
        rt.anchorMax = new Vector2(1f, 0.5f);
        rt.sizeDelta = new Vector2(0f, height);

        var textComp = go.GetComponent<TMP_Text>();
        textComp.text = text;
        textComp.fontSize = fontSize;
        textComp.alignment = TextAlignmentOptions.Left;
        textComp.color = Color.white;
        textComp.enableWordWrapping = true;
        textComp.overflowMode = TextOverflowModes.Overflow;
        textComp.raycastTarget = false;
        textComp.font = FindAnyFont();
        textComp.enableAutoSizing = false;

        var layout = go.GetComponent<LayoutElement>();
        layout.minHeight = height;
        layout.preferredHeight = height;
        return textComp;
    }

    private static Button CreateButton(Transform parent, string name, string label)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image),
            typeof(Button), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 0.5f);
        rt.anchorMax = new Vector2(1f, 0.5f);
        rt.sizeDelta = new Vector2(0f, 46f);

        var image = go.GetComponent<Image>();
        image.color = new Color(0.12f, 0.3f, 0.5f, 1f);
        image.raycastTarget = true;

        var button = go.GetComponent<Button>();
        button.targetGraphic = image;

        var labelGo = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        labelGo.transform.SetParent(go.transform, false);
        var labelRt = labelGo.GetComponent<RectTransform>();
        labelRt.anchorMin = Vector2.zero;
        labelRt.anchorMax = Vector2.one;
        labelRt.offsetMin = Vector2.zero;
        labelRt.offsetMax = Vector2.zero;
        var labelText = labelGo.GetComponent<TMP_Text>();
        labelText.text = label;
        labelText.fontSize = 15;
        labelText.alignment = TextAlignmentOptions.Left;
        labelText.color = Color.white;
        labelText.enableWordWrapping = true;
        labelText.overflowMode = TextOverflowModes.Overflow;
        labelText.raycastTarget = false;
        labelText.font = FindAnyFont();

        var layout = go.GetComponent<LayoutElement>();
        layout.minHeight = 46f;
        layout.preferredHeight = 46f;
        return button;
    }

    private static TMP_FontAsset FindAnyFont()
    {
        var texts = Object.FindObjectsOfType<TMP_Text>(true);
        foreach (var t in texts)
        {
            if (t != null && t.font != null) return t.font;
        }
        return null;
    }
}
