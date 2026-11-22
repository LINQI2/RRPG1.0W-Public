using TMPro;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Battle Presentation Cutover / Layout V0.1 —— A1 表现/布局落地工具（Editor-only，不进玩家路径）。
///
/// 分两段，互不覆盖：
///   00 Dump       —— 只读导出当前 Battle 场景 / UnitCard / BattleCard 表现层级事实（用于规划与证据）；
///   01 Apply A1   —— 按 Whitebox V1.2 落地 A1 表现退场与相对布局。
///
/// 权威边界（见 GameSpec/Battle/Battle-Scene-UI-Binding-V0.1.md §2 / §3 / §4 / §32）：
///   - 只改**表现**：Row / Column / Slot / HitTest / Drag-Drop / Hand 算法 / BattleSystem 一律不碰；
///   - 不改 6 个 Row RectTransform 的父级、对象名、ActiveSelf（HitTest / DropTarget 依赖它们）；
///   - 不改 UnitCard 根下的绑定契约对象名（Root / Button / Anchor_Visual / Body_Visual / Text_* / Anchor_*）；
///   - 不新建立第二套 Battle Canvas / HUD / Hand。
/// </summary>
public static class BattlePresentationLayoutAuthoring
{
    private const string ScenePath = "Assets/Scenes/SampleScene.scene";
    private const string UnitCardPrefabPath = "Assets/Prefabs/UI/UnitCard.prefab";
    private const string BattleCardPrefabPath = "Assets/Prefabs/UI/BattleCard.prefab";

    /// <summary>
    /// 证据 / 导出目录：优先环境变量 <c>RRPG_A1_EVIDENCE_DIR</c>；
    /// 默认落在工程 <c>screenshots/A1</c>（Git 忽略目录），不把本机绝对路径写死进仓库工具。
    /// </summary>
    private static string EvidenceDir()
    {
        string fromEnv = System.Environment.GetEnvironmentVariable("RRPG_A1_EVIDENCE_DIR");
        if (!string.IsNullOrEmpty(fromEnv)) return fromEnv;
        return Path.GetFullPath(Path.Combine(Application.dataPath, "..", "screenshots", "A1"));
    }

    [MenuItem("RRPG/Battle Presentation/00 导出当前 Battle 表现层级（只读）")]
    public static void DumpAll()
    {
        EnsureSceneOpen();
        Directory.CreateDirectory(EvidenceDir());

        var sb = new StringBuilder();
        sb.AppendLine("# Battle Presentation Hierarchy Dump");

        var battleUI = Object.FindFirstObjectByType<BattleUI>(FindObjectsInactive.Include);
        if (battleUI == null)
        {
            sb.AppendLine("ERROR: BattleUI not found in SampleScene");
            Write(Path.Combine(EvidenceDir(), "hierarchy.txt"), sb.ToString());
            return;
        }

        sb.AppendLine("BattleUI host: " + PathOf(battleUI.transform));
        sb.AppendLine();

        var canvas = battleUI.GetComponentInChildren<Canvas>(true);
        if (canvas != null)
        {
            sb.AppendLine("## 1. BattleCanvas subtree: " + PathOf(canvas.transform));
            AppendNode(sb, canvas.transform, 0);
            sb.AppendLine();
        }

        sb.AppendLine("## 2. BattleUI serialized object references");
        var so = new SerializedObject(battleUI);
        var prop = so.GetIterator();
        while (prop.NextVisible(true))
        {
            if (prop.propertyType != SerializedPropertyType.ObjectReference) continue;
            var target = prop.objectReferenceValue;
            string desc = "<null>";
            if (target != null)
            {
                var comp = target as Component;
                var go = target as GameObject;
                var t = comp != null ? comp.transform : (go != null ? go.transform : null);
                desc = target.GetType().Name + " \"" + target.name + "\""
                    + (t != null ? " @ " + PathOf(t) : " (非场景/预制体对象)");
            }
            sb.AppendLine("  " + prop.propertyPath + " = " + desc);
        }
        sb.AppendLine();

        sb.AppendLine("## 3. UnitCard.prefab");
        AppendPrefab(sb, UnitCardPrefabPath);
        sb.AppendLine();

        sb.AppendLine("## 4. BattleCard.prefab");
        AppendPrefab(sb, BattleCardPrefabPath);

        Write(Path.Combine(EvidenceDir(), "hierarchy.txt"), sb.ToString());
        Debug.Log("[BattlePresentationLayout] dump written: " + Path.Combine(EvidenceDir(), "hierarchy.txt"));
    }

    /// <summary>batchmode 入口：-executeMethod BattlePresentationLayoutAuthoring.BatchDumpAll</summary>
    public static void BatchDumpAll()
    {
        try { DumpAll(); EditorApplication.Exit(0); }
        catch (System.Exception e) { Debug.LogError(e); EditorApplication.Exit(1); }
    }

    // =====================================================================================
    // 01 Apply A1 —— Whitebox V1.2 相对结构还原（Scene 部分）
    // =====================================================================================

    // 下列参数是 1920x1080 参考分辨率下的**相对结构**取值，全部写进 Scene / Prefab 序列化，
    // 不写进运行时 C#：用户可在 Unity Inspector 直接微调（Binding Spec §3 / §4 / §32）。

    /// <summary>战场带：六排可视尺寸（宽 × 高）</summary>
    private static readonly Vector2 RowSize = new Vector2(190f, 640f);

    /// <summary>战场带核心 Y：上方留 HUD，下方留 Shared Hand</summary>
    private const float RowCenterY = 80f;

    /// <summary>两侧“前排”离屏幕中心的距离（中央留白 = Player / Enemy 对峙带）</summary>
    private const float RowFrontX = 195f;

    /// <summary>同侧相邻两排中心 X 间距（190 宽 + 10 间隙，排与排不重叠 → HitTest 无歧义）</summary>
    private const float RowSpacingX = 200f;

    /// <summary>HUD 文本宿主尺寸</summary>
    private static readonly Vector2 HudSize = new Vector2(380f, 60f);

    /// <summary>HUD 左右内缩（原 ±800 / 宽 380 会越界 30px，导致文案被屏幕边缘裁掉）</summary>
    private const float HudX = 750f;
    private const float HudTopY = 498f;
    private const float HudSecondY = 434f;

    /// <summary>开发期列标签（A0 已隐藏，本工具保持隐藏；Left / Center / Right 不是玩家可见标签）</summary>
    private static readonly string[] DevColumnLabelNames = { "Text_Left", "Text_Center", "Text_Right" };

    /// <summary>排名大字（Prototype Presentation，不属于最终设计）</summary>
    private static readonly string[] RowNameTexts = { "前排", "中排", "后排" };

    [MenuItem("RRPG/Battle Presentation/01 应用 A1 表现退场与布局（Whitebox V1.2 相对结构）")]
    public static void ApplyA1()
    {
        var report = new List<string>();
        try
        {
            EnsureSceneOpen();

            var battleUI = Object.FindFirstObjectByType<BattleUI>(FindObjectsInactive.Include);
            if (battleUI == null)
            {
                Debug.LogError("[BattlePresentationLayout] 未找到 BattleUI，未做任何修改");
                return;
            }

            var canvas = battleUI.GetComponentInChildren<Canvas>(true);
            if (canvas == null)
            {
                Debug.LogError("[BattlePresentationLayout] 未找到 BattleCanvas，未做任何修改");
                return;
            }
            var canvasT = canvas.transform;

            // ---------------- A1.1 Row Prototype Visual 退场 ----------------
            var rows = new[]
            {
                battleUI.playerFrontRow, battleUI.playerMiddleRow, battleUI.playerBackRow,
                battleUI.enemyFrontRow, battleUI.enemyMiddleRow, battleUI.enemyBackRow
            };
            int retired = 0;
            foreach (var row in rows) retired += RetireRowPrototypeVisual(row, report);
            report.Add("· A1.1 Row 原型视觉退场：底板 / RowVisual / 开发列标签 / 排名大字 共处理 " + retired + " 项");

            if (battleUI.rowVisualTintEnabled)
            {
                battleUI.rowVisualTintEnabled = false;
                EditorUtility.SetDirty(battleUI);
                report.Add("· A1.1 rowVisualTintEnabled → false（RowVisual 宿主与其颜色字段保留，可随时恢复）");
            }
            else
            {
                report.Add("· A1.1 rowVisualTintEnabled 已是 false（保持）");
            }

            // ---------------- A1.3 六排相对结构（Player 左 / Enemy 右，三排沿 X 展开） ----------------
            PlaceRow(battleUI.playerFrontRow, "playerFrontRow", -(RowFrontX + 0f * RowSpacingX), report);
            PlaceRow(battleUI.playerMiddleRow, "playerMiddleRow", -(RowFrontX + 1f * RowSpacingX), report);
            PlaceRow(battleUI.playerBackRow, "playerBackRow", -(RowFrontX + 2f * RowSpacingX), report);
            PlaceRow(battleUI.enemyFrontRow, "enemyFrontRow", +(RowFrontX + 0f * RowSpacingX), report);
            PlaceRow(battleUI.enemyMiddleRow, "enemyMiddleRow", +(RowFrontX + 1f * RowSpacingX), report);
            PlaceRow(battleUI.enemyBackRow, "enemyBackRow", +(RowFrontX + 2f * RowSpacingX), report);
            report.Add("· A1.3 六排：size=" + Fmt(RowSize) + " / centerY=" + RowCenterY
                + " / 前排 X=±" + RowFrontX + " / 同侧间距=" + RowSpacingX
                + "（Row GameObject、父级、对象名、ActiveSelf 均未改，HitTest / DragDrop 契约保持）");

            // ---------------- A1.4 HUD / Controls ----------------
            SetHudText(canvasT, BattlePresentationCutoverAuthoring.HostTurn, new Vector2(-HudX, HudTopY),
                HudSize, 36, TextAlignmentOptions.Left, report);
            SetHudText(canvasT, BattlePresentationCutoverAuthoring.HostActionPoint, new Vector2(-HudX, HudSecondY),
                HudSize, 42, TextAlignmentOptions.Left, report);
            SetHudText(canvasT, BattlePresentationCutoverAuthoring.HostDrawPile, new Vector2(HudX, HudTopY),
                HudSize, 30, TextAlignmentOptions.Right, report);
            SetHudText(canvasT, BattlePresentationCutoverAuthoring.HostDiscardPile, new Vector2(HudX, HudSecondY),
                HudSize, 30, TextAlignmentOptions.Right, report);

            // Shared Hand 容器：只扩大矩形、不移动中心 → V1 手牌世界位置保持（Prep 手牌对齐不被破坏）
            var handRt = battleUI.handContainer;
            if (handRt != null)
            {
                handRt.sizeDelta = new Vector2(-400f, -800f);
                EditorUtility.SetDirty(handRt);
                report.Add("· A1.4 handContainer(BattleActionHandRoot)：中心与锚点不变，矩形 → 1520x280（底部 Shared Hand 带）");
            }

            // 敌人“射程”运行文本（非宿主文本，位置来自 enemyRangeOffset）下移，避免与名称宿主重叠
            if (battleUI.enemyRangeOffset != new Vector2(0f, 104f))
            {
                battleUI.enemyRangeOffset = new Vector2(0f, 104f);
                EditorUtility.SetDirty(battleUI);
                report.Add("· A1.5 enemyRangeOffset → (0,104)（不再与 Text_Name / Anchor_Intent 重叠）");
            }

            // InfoText 仍是 Debug 文本（不升级为正式玩家 HUD）：缩进左侧留白，不再压住后排
            if (battleUI.infoText != null)
            {
                Place(battleUI.infoText.rectTransform, new Vector2(-830f, 140f), new Vector2(240f, 540f));
                if (battleUI.infoText.fontSize != 24)
                {
                    battleUI.infoText.fontSize = 24;
                    EditorUtility.SetDirty(battleUI.infoText);
                }
                report.Add("· A1.4 InfoText（Debug，保留）：anchored=(-830,140) size=240x540 fontSize=24");
            }

            // ---------------- A1.2 / A1.5 UnitCard.prefab ----------------
            ApplyUnitCardPresentation(report);

            SaveSceneState();

            // ---------------- 保存后校验：从磁盘重开场景，确认序列化结果与引用仍然可解析 ----------------
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var reopenUi = Object.FindFirstObjectByType<BattleUI>(FindObjectsInactive.Include);
            bool refsOk = reopenUi != null && reopenUi.unitCardPrefab != null && reopenUi.prepCardPrefab != null
                && reopenUi.handContainer != null && reopenUi.v1Runtime != null;
            report.Add("· 校验（重开场景读取序列化结果）：BattleUI 引用 unitCardPrefab / prepCardPrefab / handContainer / v1Runtime 可解析 = " + refsOk);
            if (!refsOk)
                throw new System.InvalidOperationException("保存后 BattleUI 关键引用失效（Prefab fileID / 场景引用被破坏）");

            Write(Path.Combine(EvidenceDir(), "a1-apply-report.txt"), string.Join("\n", report));
            Debug.Log("[BattlePresentationLayout] A1 完成：\n" + string.Join("\n", report));
        }
        catch (System.Exception e)
        {
            Debug.LogError("[BattlePresentationLayout] A1 执行失败：" + e);
            Write(Path.Combine(EvidenceDir(), "a1-apply-report.txt"),
                "FAILED\n" + string.Join("\n", report) + "\n" + e);
            throw;
        }
    }

    // =====================================================================================
    // A1 辅助：Row 原型视觉退场 / 排位 / HUD 布局 / 场景保存
    // =====================================================================================

    /// <summary>
    /// A1.1：只让「玩家可见的旧表现」退场，不动任何功能职责。
    /// 处理对象（全部保留 GameObject 与组件，仅 alpha=0 / SetActive(false)）：
    ///   - Row 自身 Image（灰 / 红大底板）；
    ///   - RowVisual 子对象 Image（WP-D 半透明阵营色块）；
    ///   - Text_Left / Text_Center / Text_Right（开发列标签，A0 已要求保持隐藏）；
    ///   - 「前排 / 中排 / 后排」永久大字。
    /// Row 的 RectTransform / 父级 / 对象名 / activeSelf / Image.raycastTarget 均不变。
    /// </summary>
    private static int RetireRowPrototypeVisual(RectTransform row, List<string> report)
    {
        if (row == null) return 0;
        int retired = 0;

        var plate = row.GetComponent<Image>();
        if (plate != null && plate.color.a > 0f)
        {
            var c = plate.color;
            plate.color = new Color(c.r, c.g, c.b, 0f);
            EditorUtility.SetDirty(plate);
            retired++;
        }

        var rowVisualTf = row.Find(BattleUI.RowVisualChildName);
        var rowVisual = rowVisualTf != null ? rowVisualTf.GetComponent<Image>() : null;
        if (rowVisual != null && rowVisual.color.a > 0f)
        {
            var c = rowVisual.color;
            rowVisual.color = new Color(c.r, c.g, c.b, 0f);
            EditorUtility.SetDirty(rowVisual);
            retired++;
        }

        foreach (var labelName in DevColumnLabelNames)
        {
            var label = row.Find(labelName);
            if (label != null && label.gameObject.activeSelf)
            {
                label.gameObject.SetActive(false);
                EditorUtility.SetDirty(label.gameObject);
                retired++;
            }
        }

        foreach (var text in row.GetComponentsInChildren<TMP_Text>(true))
        {
            string value = text.text.Replace("\r", string.Empty).Replace("\n", string.Empty).Trim();
            bool isRowName = false;
            foreach (var rowName in RowNameTexts)
            {
                if (value == rowName) { isRowName = true; break; }
            }
            if (!isRowName) continue;
            if (text.gameObject.activeSelf)
            {
                text.gameObject.SetActive(false);
                EditorUtility.SetDirty(text.gameObject);
                retired++;
            }
        }

        return retired;
    }

    /// <summary>按 A1.3 相对结构放置一排（只改 RectTransform 的锚点 / 尺寸 / 位置）。</summary>
    private static void PlaceRow(RectTransform row, string label, float centerX, List<string> report)
    {
        if (row == null)
        {
            report.Add("· 缺失 Row（跳过）：" + label);
            return;
        }
        Place(row, new Vector2(centerX, RowCenterY), RowSize);
    }

    /// <summary>统一的 RectTransform 表现放置（中心锚点 / 中心 pivot）。</summary>
    private static void Place(RectTransform rt, Vector2 anchored, Vector2 size)
    {
        if (rt == null) return;
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchored;
        rt.sizeDelta = size;
        EditorUtility.SetDirty(rt);
    }

    /// <summary>调整 A0 建好的 HUD TMP_Text 宿主位置 / 尺寸 / 字号 / 对齐（只动布局与字号，不改颜色与文案）。</summary>
    private static void SetHudText(Transform canvasRoot, string hostName, Vector2 anchored, Vector2 size,
        int fontSize, TextAlignmentOptions alignment, List<string> report)
    {
        var tf = canvasRoot.Find(hostName);
        if (tf == null)
        {
            report.Add("· 缺失 HUD 宿主（跳过；A0 未创建时由 A0 工具补齐）：" + hostName);
            return;
        }

        Place(tf as RectTransform, anchored, size);

        var text = tf.GetComponent<TMP_Text>();
        if (text != null)
        {
            text.fontSize = fontSize;
            text.alignment = alignment;
            EditorUtility.SetDirty(text);
        }

        report.Add("· A1.4 HUD：" + hostName + " anchored=" + Fmt(anchored) + " size=" + Fmt(size)
            + " fontSize=" + fontSize + " align=" + alignment);
    }

    private static void SaveSceneState()
    {
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    // =====================================================================================
    // A1.2 / A1.5：UnitCard.prefab 表现调整（不改绑定契约、不减节点、不改 Sprite）
    // =====================================================================================

    private static void ApplyUnitCardPresentation(List<string> report)
    {
        var root = PrefabUtility.LoadPrefabContents(UnitCardPrefabPath);
        if (root == null)
        {
            report.Add("· A1.2 UnitCard.prefab 加载失败（跳过 Prefab 段）");
            return;
        }

        try
        {
            // A1.2：根旧矩形卡片壳退出主视觉（alpha=0）。
            // 根对象 / Image / Button / UnitCardDebug / 全部子节点与对象名保持不变 → 点击、拖拽、绑定契约不受影响。
            var plate = root.GetComponent<Image>();
            if (plate != null && plate.color.a > 0f)
            {
                var c = plate.color;
                plate.color = new Color(c.r, c.g, c.b, 0f);
                report.Add("· A1.2 UnitCard 根底板 alpha " + c.a.ToString("F2")
                    + " → 0（根对象 / Image / Button / 绑定契约全部保留）");
            }

            // A1.5：实体化排布（名称 / Intent 在头肩上方，身体居中，HP / ATK / AP 在下，Shield / Status 在底）。
            LayoutChild(root.transform, "Anchor_Intent", new Vector2(0f, 142f), new Vector2(160f, 36f), -1);
            LayoutChild(root.transform, "Text_Name", new Vector2(0f, 76f), new Vector2(178f, 30f), 26);
            LayoutChild(root.transform, "Anchor_Visual", new Vector2(0f, -12f), new Vector2(140f, 148f), -1);
            LayoutChild(root.transform, "Anchor_Visual/Body_Visual", new Vector2(0f, 0f), new Vector2(124f, 144f), -1);
            LayoutChild(root.transform, "Text_HP", new Vector2(0f, -104f), new Vector2(178f, 28f), 24);
            LayoutChild(root.transform, "Text_ATK", new Vector2(-44f, -140f), new Vector2(84f, 24f), 22);
            LayoutChild(root.transform, "Text_AP", new Vector2(44f, -140f), new Vector2(84f, 24f), 20);
            LayoutChild(root.transform, "Anchor_Shield", new Vector2(0f, -170f), new Vector2(100f, 30f), -1);
            LayoutChild(root.transform, "Anchor_Status", new Vector2(0f, -196f), new Vector2(160f, 22f), -1);

            report.Add("· A1.5 UnitCard：Intent / Name / Anchor_Visual / Body_Visual / HP / ATK / AP / Shield / Status 已按实体化排布重排");

            PrefabUtility.SaveAsPrefabAsset(root, UnitCardPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        report.Add("· A1.2/A1.5 UnitCard.prefab 已保存（对象名、节点数量、Sprite 绑定、Button / UnitCardDebug 均未改）");
    }

    /// <summary>按相对路径放置 Prefab 子节点；fontSize &lt; 0 表示不改字号。</summary>
    private static void LayoutChild(Transform root, string relativePath, Vector2 anchored, Vector2 size, int fontSize)
    {
        var tf = root.Find(relativePath);
        if (tf == null) return;

        Place(tf as RectTransform, anchored, size);

        if (fontSize > 0)
        {
            var text = tf.GetComponent<TMP_Text>();
            if (text != null)
            {
                text.fontSize = fontSize;
                EditorUtility.SetDirty(text);
            }
        }
    }

    /// <summary>batchmode 入口：-executeMethod BattlePresentationLayoutAuthoring.BatchApplyA1</summary>
    public static void BatchApplyA1()
    {
        try { ApplyA1(); EditorApplication.Exit(0); }
        catch (System.Exception) { EditorApplication.Exit(1); }
    }

    private static void AppendPrefab(StringBuilder sb, string path)
    {
        var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (go == null) { sb.AppendLine("  <missing> " + path); return; }
        AppendNode(sb, go.transform, 0);
    }

    private static void AppendNode(StringBuilder sb, Transform t, int depth)
    {
        string indent = new string(' ', depth * 2);
        var rt = t as RectTransform;
        string extra = string.Empty;

        if (rt != null)
        {
            var c = new Vector3[4];
            rt.GetWorldCorners(c);
            extra += " anchored=" + Fmt(rt.anchoredPosition) + " size=" + Fmt(rt.sizeDelta)
                + " anchor=" + Fmt(rt.anchorMin) + ".." + Fmt(rt.anchorMax)
                + " pivot=" + Fmt(rt.pivot) + " scale=" + Fmt(rt.localScale)
                + " rect=[" + Fmt(c[0]) + ".." + Fmt(c[2]) + "]";
        }

        var img = t.GetComponent<Image>();
        if (img != null)
            extra += " [Image color=" + Fmt(img.color) + " enabled=" + img.enabled
                + " raycast=" + img.raycastTarget
                + " sprite=" + (img.sprite != null ? img.sprite.name : "<null>") + "]";

        var txt = t.GetComponent<TMP_Text>();
        if (txt != null)
            extra += " [TMP_Text \"" + txt.text + "\" size=" + txt.fontSize + " color=" + Fmt(txt.color)
                + " font=" + (txt.font != null ? txt.font.name : "<null>") + "]";

        var cg = t.GetComponent<CanvasGroup>();
        if (cg != null)
            extra += " [CanvasGroup alpha=" + cg.alpha + " interactable=" + cg.interactable
                + " blocks=" + cg.blocksRaycasts + "]";

        sb.AppendLine(indent + "- " + t.name + " active=" + t.gameObject.activeSelf + extra);

        for (int i = 0; i < t.childCount; i++) AppendNode(sb, t.GetChild(i), depth + 1);
    }

    private static string Fmt(Vector3 v) => "(" + v.x.ToString("F1") + "," + v.y.ToString("F1") + "," + v.z.ToString("F1") + ")";
    private static string Fmt(Vector2 v) => "(" + v.x.ToString("F1") + "," + v.y.ToString("F1") + ")";
    private static string Fmt(Color c) => "(" + c.r.ToString("F2") + "," + c.g.ToString("F2") + "," + c.b.ToString("F2") + ",a=" + c.a.ToString("F2") + ")";

    private static string PathOf(Transform t)
    {
        if (t == null) return "<null>";
        var parts = new List<string>();
        while (t != null) { parts.Add(t.name); t = t.parent; }
        parts.Reverse();
        return string.Join("/", parts);
    }

    private static void Write(string path, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllText(path, content, new UTF8Encoding(false));
    }

    private static void EnsureSceneOpen()
    {
        var active = EditorSceneManager.GetActiveScene();
        if (active.IsValid() && active.path == ScenePath) return;
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
    }
}
