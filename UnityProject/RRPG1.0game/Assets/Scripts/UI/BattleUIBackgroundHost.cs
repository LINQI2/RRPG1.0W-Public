using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// WP-D｜Battle Background / Row Visual Host（BattleUI partial）
///
/// - Background：Current Battle Canvas 内的可替换 Sprite 宿主（不参与 Raycast、不遮挡 UI）；
/// - Row：直接复用现有 6 个 Row Rect 与其下的 RowVisual 子对象，不重建第二套 Row 逻辑。
///
/// 两者都只做视觉绑定，不与 Gameplay Row / Board Authority 耦合。
/// </summary>
public partial class BattleUI : MonoBehaviour
{
    public const string RowVisualChildName = "RowVisual";

    [Header("WP-D Background / Row Visual Host（可替换宿主；占位不代表最终画风）")]
    [Tooltip("战斗背景视觉宿主（Battle Canvas 内；未来直接换 Sprite）")]
    public Image battleBackground;

    [Tooltip("战斗背景占位 Sprite（仅当宿主当前无 Sprite 时赋值）")]
    public Sprite battleBackgroundPlaceholder;

    [Tooltip("六排 Row 视觉是否按阵营着色")]
    public bool rowVisualTintEnabled = true;

    [Tooltip("我方排 RowVisual 颜色（占位）")]
    public Color playerRowVisualColor = new Color(0.30f, 0.45f, 0.90f, 0.10f);

    [Tooltip("敌方排 RowVisual 颜色（占位）")]
    public Color enemyRowVisualColor = new Color(0.90f, 0.35f, 0.30f, 0.10f);

    /// <summary>
    /// WP-D：场景 / Prefab 视觉宿主初始化（Start 时调用一次）。
    /// 只做准备动作与静态视觉绑定，不写任何 Gameplay 状态。
    /// </summary>
    public void ApplyVisualHostDefaults()
    {
        EnsureRowDataReady();
        RefreshBattleBackgroundHost();
        RefreshRowVisualHosts();
    }

    /// <summary>刷新背景宿主：占位赋值、关闭 Raycast、保持在最底层。</summary>
    public void RefreshBattleBackgroundHost()
    {
        if (battleBackground == null) return;

        if (battleBackground.sprite == null && battleBackgroundPlaceholder != null)
            battleBackground.sprite = battleBackgroundPlaceholder;

        // 背景不参与点击 / 目标选择（避免遮挡单位卡与 Row 拖拽交互）
        battleBackground.raycastTarget = false;

        var t = battleBackground.transform;
        if (t.parent != null && t.GetSiblingIndex() != 0) t.SetAsFirstSibling();
    }

    /// <summary>
    /// 刷新六排 Row 视觉：只着色现有 RowVisual 子对象（Row Rect 仍为 Authority 映射宿主）。
    /// 未创建 RowVisual 时保持原样，不在运行时新建第二套 Row 视觉。
    /// </summary>
    public void RefreshRowVisualHosts()
    {
        if (!rowVisualTintEnabled || _allRows == null) return;

        foreach (var row in _allRows)
        {
            if (row == null) continue;
            var visual = row.Find(RowVisualChildName)?.GetComponent<Image>();
            if (visual == null) continue;

            bool isPlayer = row == playerFrontRow || row == playerMiddleRow || row == playerBackRow;
            visual.color = isPlayer ? playerRowVisualColor : enemyRowVisualColor;
            visual.raycastTarget = false;
        }
    }
}
