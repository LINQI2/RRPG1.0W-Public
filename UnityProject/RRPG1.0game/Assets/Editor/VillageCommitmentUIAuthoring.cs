using TMPro;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// VillageCommitmentUIAuthoring —— V2C Functional UI 的 Editor 构建工具（仅 Editor 使用）。
/// Probe()：只读探测 SampleScene 中 Town 产品宿主层级（供 UI 集成定位，不写场景）。
/// Build()：在 TownCanvas 下构建/重建 VillageCommitmentPanel UI 骨架并保存场景（V2C）。
/// 说明：V2C UI 只作为 Viewer + Controller，不承担 Simulation Host。
/// </summary>
public static class VillageCommitmentUIAuthoring
{
    private const string ScenePath = "Assets/Scenes/SampleScene.scene";

    /// <summary>只读探测：打印 Town 产品宿主层级与 TownUI 引用，供集成决策。执行后退出编辑器。</summary>
    public static void Probe()
    {
        if (!OpenSampleScene()) { EditorApplication.Exit(1); return; }

        var sb = new StringBuilder();
        sb.AppendLine("=== VillageCommitmentUIAuthoring.Probe ===");

        // 场景根对象
        var rootObjs = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
        sb.AppendLine("SceneRoots: " + rootObjs.Length);
        foreach (var r in rootObjs)
        {
            sb.AppendLine("  ROOT " + r.name);
        }

        // 关键对象父链
        string[] targets = { "TownUI", "TownCanvas", "VillageHUD", "VillageTimeDriver", "MainMenuUI", "MainMenuCanvas" };
        foreach (var t in targets)
        {
            var go = GameObject.Find(t);
            if (go == null) { sb.AppendLine("MISSING: " + t); continue; }
            sb.AppendLine("FOUND " + t + " -> " + ParentPath(go.transform));
            sb.AppendLine("   components: " + ComponentSummary(go));
        }

        // TownUI 序列化按钮/文本引用
        var town = GameObject.Find("TownUI");
        if (town != null)
        {
            var townUi = town.GetComponent<TownUI>();
            if (townUi != null) sb.AppendLine(TownReflectionSummary(townUi));
        }

        // TownCanvas 的直接子对象
        var canvas = GameObject.Find("TownCanvas");
        if (canvas != null)
        {
            sb.AppendLine("TownCanvas children (" + canvas.transform.childCount + "):");
            for (int i = 0; i < canvas.transform.childCount; i++)
            {
                var c = canvas.transform.GetChild(i);
                sb.AppendLine("   [" + i + "] " + c.name + (c.gameObject.activeInHierarchy ? "" : " (inactive)") +
                              " comps=" + ComponentNames(c.gameObject));
            }
        }

        FileWriter_WriteProbe(sb.ToString());
        Debug.Log("[VillageCommitmentUIAuthoring] Probe done.");
        EditorApplication.Exit(0);
    }

    private static string ParentPath(Transform t)
    {
        var sb = new StringBuilder();
        while (t != null)
        {
            if (sb.Length > 0) sb.Insert(0, " / ");
            sb.Insert(0, t.name);
            t = t.parent;
        }
        return sb.ToString();
    }

    private static string ComponentSummary(GameObject go)
    {
        var comps = go.GetComponents<Component>();
        var sb = new StringBuilder();
        for (int i = 0; i < comps.Length; i++)
        {
            if (comps[i] == null) { sb.Append("[missing] "); continue; }
            sb.Append(comps[i].GetType().Name);
            if (i < comps.Length - 1) sb.Append(", ");
        }
        return sb.ToString();
    }

    private static string ComponentNames(GameObject go)
    {
        var comps = go.GetComponents<Component>();
        var sb = new StringBuilder();
        for (int i = 0; i < comps.Length; i++)
        {
            if (comps[i] == null) { sb.Append("[missing] "); continue; }
            sb.Append(comps[i].GetType().Name);
            if (i < comps.Length - 1) sb.Append(", ");
        }
        return sb.ToString();
    }

    private static string TownReflectionSummary(TownUI townUi)
    {
        var sb = new StringBuilder();
        var flags = System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance;
        foreach (var f in townUi.GetType().GetFields(flags))
        {
            var v = f.GetValue(townUi);
            string target = "null";
            if (v is Object)
            {
                var o = (Object)v;
                target = o != null ? o.name : "null";
            }
            else if (v != null)
            {
                target = v.ToString();
            }
            sb.AppendLine("TownUI." + f.Name + " (" + f.FieldType.Name + ") = " + target);
        }
        return sb.ToString();
    }

    private static bool OpenSampleScene()
    {
        if (!System.IO.File.Exists(ScenePath))
        {
            Debug.LogError("[VillageCommitmentUIAuthoring] Scene not found: " + ScenePath);
            return false;
        }
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        return true;
    }

    /// <summary>探测结果写到仓库外日志（避免写 Assets）。</summary>
    private static void FileWriter_WriteProbe(string content)
    {
        try
        {
            string path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "VillageCommitmentUI_Probe.txt");
            System.IO.File.WriteAllText(path, content);
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning("[VillageCommitmentUIAuthoring] 探测文件写入失败: " + ex.Message);
        }
    }

    // ==================== Build：V2C Functional UI 场景宿主 ====================

    /// <summary>
    /// 在真实 Town 产品宿主（TownUI/TownCanvas）下构建/重建 V2C Functional UI：
    ///   - 常驻入口按钮 BtnVillageCommitment（Town 主界面）；
    ///   - VillageCommitmentUI 宿主（Viewer + Controller；只读 Runtime，不影响 Simulation）。
    /// 幂等：存在旧结构先移除再重建。完成后保存 SampleScene。
    /// </summary>
    public static void Build()
    {
        if (!OpenSampleScene()) { EditorApplication.Exit(1); return; }

        var townGo = GameObject.Find("TownUI");
        var townCanvasGo = GameObject.Find("TownCanvas");
        if (townGo == null || townCanvasGo == null)
        {
            Debug.LogError("[VillageCommitmentUIAuthoring] 未找到 TownUI / TownCanvas，无法集成。");
            EditorApplication.Exit(1);
            return;
        }
        var canvas = townCanvasGo.GetComponent<RectTransform>();

        // 幂等：移除旧结构（若存在）
        var oldHost = canvas.Find("VillageCommitmentUI");
        if (oldHost != null) Object.DestroyImmediate(oldHost.gameObject);
        var oldBtn = canvas.Find("BtnVillageCommitment");
        if (oldBtn != null) Object.DestroyImmediate(oldBtn.gameObject);

        var host = CreateObject("VillageCommitmentUI", canvas);
        Stretch(host.GetComponent<RectTransform>());
        var ui = host.AddComponent<VillageCommitmentUI>();

        // 面板根（Edit Mode 保存为可见可调；运行时由 VillageCommitmentUI.Start 隐藏）
        var panelRoot = CreateObject("PanelRoot", host.transform);
        Stretch(panelRoot.GetComponent<RectTransform>());

        // Modal 层：子 Canvas 提高 sortingOrder，确保盖过 Town 底层并独立拦截 Raycast
        var panelCanvas = panelRoot.AddComponent<Canvas>();
        panelCanvas.overrideSorting = true;
        panelCanvas.sortingOrder = 210;
        panelRoot.AddComponent<GraphicRaycaster>();

        // PanelRoot 全屏背景（也拦截点击，保证底层 Town 不可交互）
        var rootBg = panelRoot.AddComponent<Image>();
        rootBg.color = new Color(0.03f, 0.04f, 0.07f, 0.88f);
        rootBg.raycastTarget = true;

        // Blocker（第一层全屏遮罩；Raycast 拦截兜底）
        var blocker = CreateObject("Blocker", panelRoot.transform);
        Stretch(blocker.GetComponent<RectTransform>());
        var blockerImage = blocker.AddComponent<Image>();
        blockerImage.color = new Color(0f, 0f, 0f, 0.45f);
        blockerImage.raycastTarget = true;

        // 滚动内容主体
        var scrollGo = CreateObject("ScrollView", panelRoot.transform);
        Stretch(scrollGo.GetComponent<RectTransform>(), 16f);
        var scrollRect = scrollGo.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.scrollSensitivity = 30f;

        var viewport = CreateObject("Viewport", scrollGo.transform);
        Stretch(viewport.GetComponent<RectTransform>());
        viewport.AddComponent<RectMask2D>();

        var content = CreateObject("Content", viewport.transform);
        var contentRt = content.GetComponent<RectTransform>();
        Stretch(contentRt);
        contentRt.pivot = new Vector2(0.5f, 1f);
        var contentLayout = content.AddComponent<VerticalLayoutGroup>();
        contentLayout.spacing = 6f;
        contentLayout.padding = new RectOffset(8, 8, 8, 8);
        contentLayout.childControlWidth = true;
        contentLayout.childControlHeight = false;
        contentLayout.childForceExpandWidth = true;
        contentLayout.childForceExpandHeight = false;
        var fitter = content.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scrollRect.viewport = viewport.GetComponent<RectTransform>();
        scrollRect.content = contentRt;

        // —— 静态元素（顺序即展示顺序；动态列表插入各 Section）——
        var titleText = CreateText(content.transform, "TitleText", "Village 承诺管理（V2C Functional UI / Test / Not Frozen）", 26, TextAlignmentOptions.Left);
        var timeText = CreateText(content.transform, "TimeText", "", 16, TextAlignmentOptions.Left);
        var contributionText = CreateText(content.transform, "VillageContributionText", "", 15, TextAlignmentOptions.Left);
        var selectedInfoText = CreateText(content.transform, "SelectedInfoText", "（点击 Person 查看承诺详情）", 15, TextAlignmentOptions.Left);
        var messageText = CreateText(content.transform, "MessageText", "", 15, TextAlignmentOptions.Left);

        CreateText(content.transform, "SectionPersonLabel", "① 谁在做什么（点击选择一位 Person）", 17, TextAlignmentOptions.Left);
        var personList = CreateSection(content.transform, "PersonList");
        CreateText(content.transform, "SectionWorkplaceLabel", "② Test Workplace / 岗位容量（点击「指派到此处」为选中 Person 指定 Primary Assignment）", 17, TextAlignmentOptions.Left);
        var workplaceList = CreateSection(content.transform, "WorkplaceList");
        CreateText(content.transform, "SectionDefLabel", "③ 选择要启动的 Test Project（点击行选中）", 17, TextAlignmentOptions.Left);
        var projectDefList = CreateSection(content.transform, "ProjectDefList");
        CreateText(content.transform, "SectionParticipantLabel", "④ 勾选 Project 参与者（在村 Person；抽出后原岗位暂停执行）", 17, TextAlignmentOptions.Left);
        var participantList = CreateSection(content.transform, "ParticipantList");
        CreateText(content.transform, "SectionActiveLabel", "⑤ 进行中的 Project / 进度（随时间推进；完成后自动解除占用）", 17, TextAlignmentOptions.Left);
        var activeProjectList = CreateSection(content.transform, "ActiveProjectList");
        CreateText(content.transform, "SectionSocialHistoryLabel", "⑥ 最近的村民回应（只读回看：选择记录 / 恢复窗口；Test / Not Frozen）", 17, TextAlignmentOptions.Left);
        var socialHistoryList = CreateSection(content.transform, "SocialHistoryList");

        // 按钮行（Close / Remove Assignment / Start Project；位于滚动内容末尾）
        var closeBtn = CreateButton(content.transform, "BtnClose", "关闭面板");
        var removeBtn = CreateButton(content.transform, "BtnRemoveAssignment", "移除选中 Person 的 Primary Assignment");
        var startBtn = CreateButton(content.transform, "BtnStartProject", "启动选中的 Test Project");

        // Town 主界面入口按钮（Edit Mode 持久化；TownCanvas 顶层，点击显示面板）
        var entryBtn = CreateButton(canvas, "BtnVillageCommitment", "承诺管理（V2C Test）");
        PositionTownEntryButton(canvas, entryBtn);

        // 绑定 VillageCommitmentUI 组件引用（随场景序列化，Edit Mode 可调）
        ui.root = panelRoot;
        ui.titleText = titleText;
        ui.timeText = timeText;
        ui.villageContributionText = contributionText;
        ui.selectedInfoText = selectedInfoText;
        ui.messageText = messageText;
        ui.closeButton = closeBtn;
        ui.removeAssignmentButton = removeBtn;
        ui.startProjectButton = startBtn;
        ui.entryButton = entryBtn;
        ui.personListContainer = personList;
        ui.workplaceListContainer = workplaceList;
        ui.projectDefListContainer = projectDefList;
        ui.participantListContainer = participantList;
        ui.activeProjectListContainer = activeProjectList;
        ui.socialHistoryListContainer = socialHistoryList;

        // 面板根默认隐藏；ScrollView 属于 PanelRoot（隐藏），Edit Mode 中编辑时可临时打开 PanelRoot
        EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        Debug.Log("[VillageCommitmentUIAuthoring] Build 完成：VillageCommitmentUI 宿主 + BtnVillageCommitment 已写入 SampleScene。");
        EditorApplication.Exit(0);
    }

    // ==================== Build 辅助 ====================

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

    private static void PositionTownEntryButton(RectTransform canvas, Button button)
    {
        // 放在 TownCanvas 右上角（主界面顶部工具位，不与左下/中心控件重叠）
        var rt = button.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(1f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(1f, 1f);
        rt.anchoredPosition = new Vector2(-30f, -90f);
        rt.sizeDelta = new Vector2(280f, 54f);
    }

    private static TMP_Text CreateText(Transform parent, string name, string text, int fontSize, TextAlignmentOptions align)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 0.5f);
        rt.anchorMax = new Vector2(1f, 0.5f);
        rt.sizeDelta = new Vector2(0f, 32f);

        var textComp = go.GetComponent<TMP_Text>();
        textComp.text = text;
        textComp.fontSize = fontSize;
        textComp.alignment = align;
        textComp.color = Color.white;
        textComp.enableWordWrapping = true;
        textComp.overflowMode = TextOverflowModes.Overflow;
        textComp.raycastTarget = false;
        textComp.font = FindAnyFont();
        textComp.enableAutoSizing = false;

        var layout = go.GetComponent<LayoutElement>();
        layout.minHeight = 32f;
        layout.preferredHeight = 32f;
        return textComp;
    }

    /// <summary>动态行列表段（浅色背景容器 + VerticalLayoutGroup + 自适应高度；运行时向其中插入行）。</summary>
    private static RectTransform CreateSection(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image),
            typeof(VerticalLayoutGroup), typeof(ContentSizeFitter), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 0.5f);
        rt.anchorMax = new Vector2(1f, 0.5f);
        rt.sizeDelta = new Vector2(0f, 0f);

        var image = go.GetComponent<Image>();
        image.color = new Color(0.16f, 0.2f, 0.26f, 0.55f);
        image.raycastTarget = false;

        var layout = go.GetComponent<VerticalLayoutGroup>();
        layout.spacing = 2f;
        layout.padding = new RectOffset(4, 4, 4, 4);
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        var fitter = go.GetComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        return rt;
    }

    private static Button CreateButton(Transform parent, string name, string label)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image),
            typeof(Button), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 0.5f);
        rt.anchorMax = new Vector2(1f, 0.5f);
        rt.sizeDelta = new Vector2(0f, 40f);

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
        labelText.fontSize = 16;
        labelText.alignment = TextAlignmentOptions.Center;
        labelText.color = Color.white;
        labelText.raycastTarget = false;
        labelText.font = FindAnyFont();

        var layout = go.GetComponent<LayoutElement>();
        layout.minHeight = 40f;
        layout.preferredHeight = 40f;
        return button;
    }

    private static TMP_FontAsset FindAnyFont()
    {
        // 遍历场景全部 TMP_Text，找到第一个 font 非空的有效字体引用（内置 LegacyRuntime.ttf 或资产字体）
        var texts = Object.FindObjectsOfType<TMP_Text>(true);
        foreach (var t in texts)
        {
            if (t != null && t.font != null) return t.font;
        }
        return null;
    }
}
