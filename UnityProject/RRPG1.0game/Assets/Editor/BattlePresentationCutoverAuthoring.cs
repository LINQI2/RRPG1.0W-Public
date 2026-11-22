using TMPro;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Battle Presentation Cutover / Layout V0.1 —— 阶段A：表现宿主接线 + 白模开发标签清理
///
/// 本工具只做两件**表现层**接线，不改任何玩法：
///   1. 在正式 Battle Canvas（SampleScene → BattleController → BattleCanvas）内补齐玩家可读 HUD 宿主：
///      `HUD_TurnText` / `HUD_ActionPointText` / `HUD_DrawPileText` / `HUD_DiscardPileText`，
///      并把 `BattleV1Runtime.txtSharedActionPoint` 绑定到 `HUD_ActionPointText`
///      （该字段此前为 fileID:0 未绑定 → 共享行动力在真实玩家路径上没有任何显示宿主）；
///   2. 隐藏 6 排上的 `Text_Left` / `Text_Center` / `Text_Right` 开发标签。
///      Authority：WP-D Spec §-1 制作人确认第 5 条「Left / Center / Right 只属于内部开发逻辑，
///      不得作为玩家层可见三格/三列标签」+ §22「Logic Column 未暴露为常驻玩家格子」。
///
/// 明确不做（避免越权与覆盖用户视觉）：
///   - 不移动 / 不缩放任何**已存在**对象（Battle Scene & UI Binding Spec §4、§32：用户手调 Transform 不得被无条件重置）；
///   - 不修改 Row / Column Authority、Formation Presentation 间距、Shared Deck / AP、Hand 排列、BattleSystem；
///   - 不修改按钮颜色 / 字体 / 素材（最终颜色与构图层属于用户视觉验收范围）；
///   - 不创建第二套 Battle Canvas / HUD Authority：全部宿主都在同一个 BattleCanvas 内。
///
/// 新建 HUD 宿主使用**首次创建的合理临时位置**（Binding Spec §4 允许），
/// 之后的位置 / 字号 / 颜色 / 文案都由用户在 Unity Inspector 调整，本工具重复执行不会覆盖已有宿主。
/// </summary>
public static class BattlePresentationCutoverAuthoring
{
    private const string ScenePath = "Assets/Scenes/SampleScene.scene";

    // ---- HUD 宿主对象名（稳定身份；后续替换素材 / 调参都按名字找）----
    public const string HostTurn = "HUD_TurnText";
    public const string HostActionPoint = "HUD_ActionPointText";
    public const string HostDrawPile = "HUD_DrawPileText";
    public const string HostDiscardPile = "HUD_DiscardPileText";

    // ---- 首次创建临时位置（相对 BattleCanvas 中心；用户在 Inspector 自行调整）----
    private static readonly Vector2 TurnPos = new Vector2(-800f, 492f);
    private static readonly Vector2 ActionPointPos = new Vector2(-800f, 436f);
    private static readonly Vector2 DrawPilePos = new Vector2(800f, 492f);
    private static readonly Vector2 DiscardPilePos = new Vector2(800f, 436f);
    private static readonly Vector2 HudSize = new Vector2(380f, 56f);

    /// <summary>开发期列标签子对象名（Left / Center / Right 只属内部逻辑，不作为玩家可见标签）。</summary>
    private static readonly string[] DevColumnLabelNames = { "Text_Left", "Text_Center", "Text_Right" };

    [MenuItem("RRPG/Battle Presentation/01 应用表现宿主与开发标签清理（Layout V0.1 阶段A）")]
    public static void ApplyPresentationHosts()
    {
        var report = new List<string>();
        try
        {
            EnsureSceneOpen();

            var battleUI = Object.FindFirstObjectByType<BattleUI>(FindObjectsInactive.Include);
            if (battleUI == null)
            {
                Debug.LogError("[BattlePresentationCutover] 未找到 BattleUI（SampleScene → BattleController），未做任何修改");
                return;
            }

            var canvas = battleUI.GetComponentInChildren<Canvas>(true);
            if (canvas == null)
            {
                Debug.LogError("[BattlePresentationCutover] BattleUI 下未找到 BattleCanvas，未做任何修改");
                return;
            }

            var runtime = battleUI.GetComponent<BattleV1Runtime>();
            var presenter = battleUI.GetComponent<BattleHudPresenter>();
            // Resolve every existing host before creating or rebinding anything.
            // Current HUDs may be nested or have a producer-authored name.
            var existingTurn = FindExistingTextHost(canvas.transform, HostTurn, presenter != null ? presenter.turnHost : null);
            var existingAp = FindExistingTextHost(canvas.transform, HostActionPoint, runtime != null ? runtime.txtSharedActionPoint : null);
            var existingDraw = FindExistingTextHost(canvas.transform, HostDrawPile, presenter != null ? presenter.drawPileHost : null);
            var existingDiscard = FindExistingTextHost(canvas.transform, HostDiscardPile, presenter != null ? presenter.discardPileHost : null);
            var font = ResolveReferenceFont(canvas, report);

            // 1) HUD 宿主（存在即复用，不覆盖用户已调好的位置 / 字号 / 颜色）
            var turnHost = EnsureTextHost(canvas.transform, HostTurn, "回合 1", font,
                TurnPos, HudSize, 34, TextAlignmentOptions.Left, report, existingTurn);
            var apHost = EnsureTextHost(canvas.transform, HostActionPoint, "行动力 3/3", font,
                ActionPointPos, HudSize, 34, TextAlignmentOptions.Left, report, existingAp);
            var drawHost = EnsureTextHost(canvas.transform, HostDrawPile, "抽牌堆 0", font,
                DrawPilePos, HudSize, 28, TextAlignmentOptions.Right, report, existingDraw);
            var discardHost = EnsureTextHost(canvas.transform, HostDiscardPile, "弃牌堆 0", font,
                DiscardPilePos, HudSize, 28, TextAlignmentOptions.Right, report, existingDiscard);

            // 2) 共享行动力宿主绑定（BattleV1Runtime.RefreshCounters() 的既有写入目标）
            if (runtime == null)
            {
                report.Add("· BattleV1Runtime 缺失：跳过行动力宿主绑定");
            }
            else if (runtime.txtSharedActionPoint != apHost)
            {
                runtime.txtSharedActionPoint = apHost;
                EditorUtility.SetDirty(runtime);
                report.Add("· BattleV1Runtime.txtSharedActionPoint → " + HostActionPoint + "（原为未绑定）");
            }
            else
            {
                report.Add("· BattleV1Runtime.txtSharedActionPoint 已绑定 " + HostActionPoint + "（保持）");
            }

            // 3) 只读 HUD Presenter（回合 / 抽牌堆 / 弃牌堆）；不构成第二套 Authority
            if (presenter == null)
            {
                presenter = battleUI.gameObject.AddComponent<BattleHudPresenter>();
                report.Add("· 新增组件 BattleHudPresenter（只读 BattleV1Runtime 事实）");
            }
            presenter.v1Runtime = runtime;
            presenter.turnHost = turnHost;
            presenter.drawPileHost = drawHost;
            presenter.discardPileHost = discardHost;
            EditorUtility.SetDirty(presenter);

            // 4) 隐藏开发期 Left / Center / Right 标签（只关显示，不删除对象、不改 Gameplay）
            int hidden = HideDevColumnLabels(battleUI, report);
            report.Add("· 已隐藏开发期列标签：" + hidden + " 个（Text_Left / Text_Center / Text_Right）");

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[BattlePresentationCutover] 完成：\n" + string.Join("\n", report));
        }
        catch (System.Exception e)
        {
            Debug.LogError("[BattlePresentationCutover] 执行失败：" + e);
            throw;
        }
    }

    /// <summary>batchmode 入口：-executeMethod BattlePresentationCutoverAuthoring.BatchApplyPresentationHosts</summary>
    public static void BatchApplyPresentationHosts()
    {
        try
        {
            ApplyPresentationHosts();
            EditorApplication.Exit(0);
        }
        catch (System.Exception)
        {
            EditorApplication.Exit(1);
        }
    }

    /// <summary>确认目标场景处于打开状态（batchmode 与 Editor 菜单共用入口）。</summary>
    private static void EnsureSceneOpen()
    {
        var active = EditorSceneManager.GetActiveScene();
        if (active.IsValid() && active.path == ScenePath) return;
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
    }

    /// <summary>
    /// 取一个场景内已存在的参考字体：新建 TMP_Text 宿主必须有 TMP_FontAsset，否则文字不渲染。
    /// 优先复用同 Canvas 下已有 TMP_Text 的字体（不新增字体资产、不改用户字体设置）。
    /// </summary>
    private static TMP_FontAsset ResolveReferenceFont(Canvas canvas, List<string> report)
    {
        var texts = canvas.GetComponentsInChildren<TMP_Text>(true);
        foreach (var t in texts)
        {
            if (t != null && t.font != null)
            {
                report.Add("· HUD 宿主字体复用自：" + t.gameObject.name);
                return t.font;
            }
        }

        // 场景内没有任何已绑定字体的 TMP_Text 时，退回引擎内置字体（保证 HUD 文案可见）
        var builtin = CurrentUIFontAuthority.Default;
        if (builtin != null)
        {
            report.Add("· 场景内无已绑定字体：HUD 宿主使用引擎内置 LegacyRuntime.ttf（可在 Inspector 更换）");
            return builtin;
        }

        report.Add("· 警告：未找到任何可用字体（HUD 文案可能不渲染，请在 Inspector 指定 TMP_FontAsset）");
        return null;
    }

    /// <summary>
    /// 按名字获得 HUD TMP_Text 宿主：已存在则**原样复用**（不覆盖用户已调整的位置 / 字号 / 颜色 / 文案），
    /// 不存在才按首次创建的临时位置新建。
    /// </summary>
    private static TMP_Text FindExistingTextHost(Transform canvasRoot, string hostName, TMP_Text boundHost)
    {
        if (boundHost != null && !boundHost.transform.IsChildOf(canvasRoot))
            throw new System.InvalidOperationException("HUD binding is outside the Battle Canvas: " + hostName);

        TMP_Text existing = null;
        foreach (var child in canvasRoot.GetComponentsInChildren<Transform>(true))
        {
            if (child == canvasRoot || child.name != hostName) continue;
            var text = child.GetComponent<TMP_Text>();
            if (text == null)
                throw new System.InvalidOperationException("HUD name is occupied without TMP_Text: " + hostName);
            if (existing != null)
                throw new System.InvalidOperationException("Multiple HUD hosts found: " + hostName);
            existing = text;
        }
        if (boundHost != null && existing != null && boundHost != existing)
            throw new System.InvalidOperationException("HUD binding conflicts with the named host: " + hostName);
        return boundHost != null ? boundHost : existing;
    }

    private static TMP_Text EnsureTextHost(Transform canvasRoot, string hostName, string defaultText, TMP_FontAsset font,
        Vector2 anchoredPos, Vector2 size, int fontSize, TextAlignmentOptions alignment, List<string> report,
        TMP_Text existing)
    {
        if (existing != null)
        {
            report.Add("· 宿主已存在，保持用户设置：" + hostName);
            return existing;
        }

        var go = new GameObject(hostName, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        go.transform.SetParent(canvasRoot, false);

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = anchoredPos;

        var text = go.GetComponent<TMP_Text>();
        text.text = defaultText;
        text.font = font;
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = new Color(0.93f, 0.94f, 0.97f, 1f);   // 深色占位背景上的可读浅色
        text.raycastTarget = false;                        // HUD 不拦截点击 / 拖拽
        text.enableWordWrapping = false;
        text.overflowMode = TextOverflowModes.Overflow;

        report.Add("· 新建 HUD 宿主（临时位置，可自由调整）：" + hostName);
        return text;
    }

    /// <summary>
    /// 隐藏 6 排上的开发期列标签（Text_Left / Text_Center / Text_Right）。
    /// 只 SetActive(false)：不删除对象、不改层级、不影响任何 Gameplay 读取（这些对象无代码消费者）。
    /// </summary>
    private static int HideDevColumnLabels(BattleUI battleUI, List<string> report)
    {
        int count = 0;
        var rows = new[]
        {
            battleUI.playerFrontRow, battleUI.playerMiddleRow, battleUI.playerBackRow,
            battleUI.enemyFrontRow, battleUI.enemyMiddleRow, battleUI.enemyBackRow
        };

        foreach (var row in rows)
        {
            if (row == null) continue;
            foreach (var labelName in DevColumnLabelNames)
            {
                var label = row.Find(labelName);
                if (label == null || !label.gameObject.activeSelf) continue;
                label.gameObject.SetActive(false);
                count++;
            }
        }

        report.Add("· 依据：WP-D Spec §-1 第 5 条（Left / Center / Right 不得作为玩家层可见标签）");
        return count;
    }
}
