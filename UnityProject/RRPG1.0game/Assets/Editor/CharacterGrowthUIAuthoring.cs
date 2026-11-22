using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 人物成长正式 UI Authoring（V0.1 人工验收修复）。
///
/// 用途：
///   1. 把 CharacterGrowthUI 的正式宿主（Root / Canvas / Panel / Modal Blocker / 标题 / 关闭 /
///      列表容器 / 详情容器 / 升级按钮区域）持久化为 Prefab（Edit Mode / Prefab Mode 可编辑）；
///   2. 在 SampleScene.scene 中放入场景固定宿主（Prefab 实例）；
///   3. 通过 Unity Editor API（EditorSceneManager.SaveScene）持久化 .scene（MCP scene-save 仅支持 .unity）。
///
/// 约定：运行时只生成动态内容（人物列表项 / 详情文本 / 三选一卡牌 / 流程按钮）；
///       正式宿主与布局在 Edit Mode 可见、可调。场景 diff 可审计。
/// </summary>
public static class CharacterGrowthUIAuthoring
{
    private const string PrefabPath = "Assets/Prefabs/UI/CharacterGrowthUI.prefab";

    [MenuItem("RRPG/人物成长/01 创建正式 UI Prefab 并接入场景")]
    public static void CreatePrefabAndAddToScene()
    {
        var prefab = CreateFormalPrefab();
        AddSceneHostToSampleScene(prefab);
    }

    /// <summary>
    /// 把 CharacterGrowthUI 宿主（Prefab 实例）显式加入 SampleScene 并保存。
    /// 通过场景路径定位，避免测试运行器活动场景为临时场景时 host 丢失。
    /// </summary>
    public static void AddSceneHostToSampleScene(GameObject prefab = null)
    {
        if (prefab == null)
        {
            prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null) prefab = CreateFormalPrefab();
        }

        var scene = UnityEngine.SceneManagement.SceneManager.GetSceneByPath("Assets/Scenes/SampleScene.scene");
        if (!scene.IsValid())
        {
            Debug.LogError("[CharacterGrowthUIAuthoring] 未找到 SampleScene.scene，无法加入宿主");
            return;
        }
        UnityEngine.SceneManagement.SceneManager.SetActiveScene(scene);

        // 场景内已有宿主则复用，不重复创建
        bool exists = false;
        foreach (var root in scene.GetRootGameObjects())
        {
            // 宿主可位于整理容器中；按组件识别并包含隐藏对象，避免再次创建。
            if (root != null && root.GetComponentInChildren<CharacterGrowthUI>(true) != null)
            { exists = true; break; }
        }
        if (exists)
        {
            Debug.Log("[CharacterGrowthUIAuthoring] 场景已存在 CharacterGrowthUI 宿主，复用现有实例");
        }
        else
        {
            PrefabUtility.InstantiatePrefab(prefab, scene);
            Debug.Log("[CharacterGrowthUIAuthoring] 已把 CharacterGrowthUI Prefab 实例加入 SampleScene");
        }

        // 通过 Unity Editor API 持久化 .scene（保持场景 diff 可审计）。
        // 注意：不在测试/批处理上下文中调用 AssetDatabase.Refresh()（会触发域重载导致运行挂起）；
        // PrefabUtility + EditorSceneManager + SaveAssets 已足以持久化。
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("[CharacterGrowthUIAuthoring] 完成：Prefab=" + PrefabPath + "，SampleScene 已保存");
    }

    public static GameObject CreateFormalPrefab()
    {
        var root = new GameObject("CharacterGrowthUI");
        var comp = root.AddComponent<CharacterGrowthUI>();

        // ---------- Canvas（模态前景 sortingOrder 高于 Town） ----------
        var canvasRt = CreateUiRect("GrowthCanvas", root.transform,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, comp.canvasReferenceResolution);
        var canvas = canvasRt.gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = comp.canvasSortingOrder;
        var scaler = canvasRt.gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = comp.canvasReferenceResolution;
        canvasRt.gameObject.AddComponent<GraphicRaycaster>();
        canvasRt.gameObject.SetActive(false); // 初始隐藏，Open() 时显示

        // ---------- Modal Blocker：全屏遮罩（压暗 + 拦截底层 Raycast） ----------
        var maskRt = CreateUiRect("Mask_Dim", canvasRt,
            Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        var maskImg = maskRt.gameObject.AddComponent<Image>();
        maskImg.color = comp.backgroundDim;
        maskImg.raycastTarget = true;

        // ---------- Panel / Content ----------
        var contentRt = CreateUiRect("Content", canvasRt,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, comp.panelSize);
        CreateImage("Panel_Bg", contentRt, new Color(0.12f, 0.12f, 0.14f, 0.96f),
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, comp.panelSize, true);

        CreateText("Title", contentRt, "人物成长", 40, Color.white,
            comp.titlePosition, new Vector2(500, 60), TextAlignmentOptions.Left);
        var btnClose = CreateButton("BtnClose", contentRt, "关闭", comp.closeButtonPosition, new Vector2(140, 60),
            new Color(0.3f, 0.3f, 0.35f, 1f));
        btnClose.onClick.RemoveAllListeners();
        btnClose.onClick.AddListener(comp.Hide); // Prefab 自包含：关闭按钮绑定 Hide

        // ---------- 人物列表容器 ----------
        var listRt = CreateUiRect("ListRoot", contentRt,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), comp.listStartPosition,
            new Vector2(comp.listCardSize.x + 40, comp.detailSize.y + 40));
        CreateText("ListTitle", listRt, "人物列表", 24, Color.white,
            new Vector2(0, comp.listCardSize.y * 0.5f + 30), new Vector2(300, 40), TextAlignmentOptions.Left);

        // ---------- 人物详情容器 ----------
        var detailRt = CreateUiRect("DetailRoot", contentRt,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), comp.detailPosition, comp.detailSize);
        CreateText("DetailTitle", detailRt, "人物详情", 24, Color.white,
            new Vector2(0, comp.detailSize.y * 0.5f - 30), new Vector2(300, 40), TextAlignmentOptions.Left);

        var infoText = CreateText("InfoText", detailRt, "", 16, new Color(1f, 0.9f, 0.4f, 1f),
            new Vector2(0, -260), new Vector2(comp.detailSize.x - 80, 120), TextAlignmentOptions.Left);

        var levelUpBtn = CreateButton("BtnLevelUp", detailRt, "升级", new Vector2(0, -170),
            new Vector2(220, 70), new Color(0.3f, 0.5f, 0.3f, 1f));

        // ---------- 绑定组件字段 ----------
        comp.canvasRoot = canvasRt.gameObject;
        comp.maskDim = maskRt.gameObject;
        comp.listRoot = listRt;
        comp.detailRoot = detailRt;
        comp.infoText = infoText;
        comp.levelUpButton = levelUpBtn;

        var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        return prefab;
    }

    // ==================== UI 构建辅助 ====================

    private static RectTransform CreateUiRect(string name, Transform parent,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPos, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = anchoredPos;
        return rt;
    }

    private static Image CreateImage(string name, RectTransform parent, Color color,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPos, Vector2 size, bool raycastTarget)
    {
        var rt = CreateUiRect(name, parent, anchorMin, anchorMax, anchoredPos, size);
        var img = rt.gameObject.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = raycastTarget;
        return img;
    }

    private static TMP_Text CreateText(string name, RectTransform parent, string content, int fontSize, Color color,
        Vector2 pos, Vector2 size, TextAlignmentOptions alignment)
    {
        var rt = CreateUiRect(name, parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, size);
        var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        t.text = content;
        t.font = GetLegacyFont();
        t.fontSize = fontSize;
        t.color = color;
        t.alignment = alignment;
        t.enableWordWrapping = true;
        t.overflowMode = TextOverflowModes.Overflow;
        return t;
    }

    private static Button CreateButton(string name, RectTransform parent, string label,
        Vector2 pos, Vector2 size, Color color)
    {
        var rt = CreateUiRect(name, parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, size);
        var img = rt.gameObject.AddComponent<Image>();
        img.color = color;
        var btn = rt.gameObject.AddComponent<Button>();
        btn.targetGraphic = img;

        var labelRt = CreateUiRect("Label", rt, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        var t = labelRt.gameObject.AddComponent<TextMeshProUGUI>();
        t.text = label;
        t.font = GetLegacyFont();
        t.fontSize = 20;
        t.color = Color.white;
        t.alignment = TextAlignmentOptions.Center;
        t.enableWordWrapping = true;
        t.overflowMode = TextOverflowModes.Overflow;
        return btn;
    }

    private static TMP_FontAsset GetLegacyFont()
    {
        var font = CurrentUIFontAuthority.Default;
        if (font == null) font = CurrentUIFontAuthority.Default;
        return font;
    }
}

