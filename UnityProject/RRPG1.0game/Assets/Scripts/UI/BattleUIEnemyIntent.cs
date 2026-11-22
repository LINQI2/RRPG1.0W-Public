using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// BattleUI Stage 5 partial：敌方卡上的 Intent / AttackRange 原位显示（Milestone C）
///
/// 约束：
///   - 复用现有敌方单位卡（UnitCard prefab 实例，挂载于敌方排容器），不重建第二套 HUD；
///   - Intent 文本 / AttackRange 文本以临时子 TMP_Text 占位，位置与字号由 Inspector 可调；
///   - 只显示粗粒度 Intent（进攻 / 防御 / 特殊），不公开具体目标与完整 AI 逻辑；
///   - 敌人失能后清空 Intent 显示（失能敌人不再生成/执行 Intent）。
/// </summary>
public partial class BattleUI : MonoBehaviour
{
    [Header("敌方 Intent 显示（Stage 5，原位扩展敌方卡，Inspector 可调）")]
    [Tooltip("Intent 文本在敌方卡上的本地偏移（相对卡中心；卡高约 237，默认置于卡顶上方）")]
    public Vector2 enemyIntentOffset = new Vector2(0f, 130f);

    [Tooltip("Intent 文本字号")]
    public int enemyIntentFontSize = 20;

    [Tooltip("Intent 文本颜色")]
    public Color enemyIntentColor = Color.yellow;

    [Tooltip("AttackRange 文本在敌方卡上的本地偏移（相对卡中心）")]
    public Vector2 enemyRangeOffset = new Vector2(0f, 100f);

    [Tooltip("AttackRange 文本字号")]
    public int enemyRangeFontSize = 14;

    /// <summary>
    /// 刷新敌方卡上的 Intent 与 AttackRange 显示。
    /// 由 RefreshFieldUI 在玩家回合开始（Intent 已锁定）与敌方阶段后调用。
    /// </summary>
    public void RefreshEnemyIntentUI()
    {
        var data = _battleSystem != null ? _battleSystem.GetBattleData() : null;
        if (data == null) return;

        foreach (var u in data.EnemyUnits)
        {
            var rr = GetRowRect(false, u.Row);
            if (rr == null || !_rowCards.ContainsKey(rr)) continue;

            // WP-Encounter-1C §8：敌方卡名 = 本场身份 UnitId（= EnemyInstanceId）
            var card = _rowCards[rr].Find(c => c != null && c.name == "Card_" + u.UnitId);
            if (card == null) continue;

            bool combatCapable = !u.IsDown && u.CurrentHP > 0;
            string intentText = "";
            if (combatCapable)
            {
                var intent = _battleSystem.GetEnemyIntent(u);
                intentText = intent != null ? EnemyIntentDisplayName(intent.ActionType) : "";
            }

            SetIntentPresentationHost(card, combatCapable ? _battleSystem.GetEnemyIntent(u) : null);
            SetEnemyCardText(card, "Text_Range", combatCapable ? "射程" + u.AttackRange : "",
                             enemyRangeOffset, enemyRangeFontSize, enemyIntentColor);
        }
    }

    private static string EnemyIntentDisplayName(EnemyActionType type)
    {
        switch (type)
        {
            case EnemyActionType.Attack: return "⚔ 进攻";
            case EnemyActionType.Defend: return "🛡 防御";
            default:                     return "✦ 特殊";
        }
    }

    private void SetEnemyCardText(GameObject card, string childName, string text,
                                  Vector2 offset, int fontSize, Color color)
    {
        var txt = GetOrCreateEnemyCardText(card, childName, offset, fontSize, color);
        if (txt != null) txt.text = text;
    }

    private TMP_Text GetOrCreateEnemyCardText(GameObject card, string childName,
                                          Vector2 offset, int fontSize, Color color)
    {
        // WP-D：优先使用 UnitCard Prefab 上稳定的 Anchor_Intent / Text_Intent 宿主
        //（宿主存在时，位置 / 字号 / 颜色由 Prefab / Inspector 决定，不再由代码坐标写死）
        if (childName == HostTextIntent)
        {
            var hostText = card.transform.Find(HostAnchorIntent + "/" + HostTextIntent)?.GetComponent<TMP_Text>();
            if (hostText != null) return hostText;
        }

        var tf = card.transform.Find(childName);
        var txt = tf != null ? tf.GetComponent<TMP_Text>() : null;
        if (txt != null) return txt;

        var go = new GameObject(childName, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        go.transform.SetParent(card.transform, false);

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = offset;

        txt = go.GetComponent<TMP_Text>();
        txt.font = CurrentUIFontAuthority.Default;
        txt.fontSize = fontSize;
        txt.alignment = TextAlignmentOptions.Center;
        txt.color = color;
        txt.raycastTarget = false;
        return txt;
    }
}
