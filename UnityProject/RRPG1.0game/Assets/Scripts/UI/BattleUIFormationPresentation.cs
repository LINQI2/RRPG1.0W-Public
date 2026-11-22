using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// WP-D｜Formation Presentation 纯计算层（无 UnityEngine 依赖之外的状态，便于独立测试）。
///
/// 职责：只把「同一排内当前显示的单位数量 + 排内相对顺序」换算成画面偏移。
/// 不负责：BattleUnit.Row / Column（Gameplay Position Authority，仍由 BattleData / WP-A 双 Board 决定）。
///
/// 视觉规则（WP-D Spec §5.2）：
///   1 人 → 排内居中；
///   2 人 → 围绕排中心聚拢（内部逻辑仍是 Left + Right，但不再使用最外侧 Column Offset）；
///   3 人 → 展开，且保持 Left / Center / Right 顺序。
///
/// 所有间距数值都来自 Inspector 参数，本类不冻结任何“最佳数值”。
/// </summary>
public static class BattleFormationVisualLayout
{
    /// <summary>视觉上最多展示 3 个槽位（与内部 Left / Center / Right 对应，但不向玩家显示标签或永久格子）</summary>
    public const int MaxVisualSlots = 3;

    /// <summary>按单位数量得到可视槽位数（1 / 2 / 3）</summary>
    public static int SlotCount(int unitCount)
    {
        if (unitCount <= 1) return 1;
        if (unitCount == 2) return 2;
        return MaxVisualSlots;
    }

    /// <summary>
    /// 槽位视觉偏移。slotIndex 0 = 排内第一个（逻辑 Left 方向），依次递增。
    /// </summary>
    /// <param name="unitCount">该排当前显示的单位数量</param>
    /// <param name="slotIndex">排内相对顺序下标（0 起）</param>
    /// <param name="singleOffset">1 人排偏移</param>
    /// <param name="twoSpacing">2 人排视觉间距（聚拢）</param>
    /// <param name="threeSpacing">3 人排视觉间距（展开）</param>
    public static float GetOffset(int unitCount, int slotIndex,
                                  float singleOffset, float twoSpacing, float threeSpacing)
    {
        int count = SlotCount(unitCount);
        int idx = Mathf.Clamp(slotIndex, 0, count - 1);

        if (count == 1) return singleOffset;
        if (count == 2) return idx == 0 ? twoSpacing * 0.5f : -twoSpacing * 0.5f;
        if (idx == 0) return threeSpacing;
        if (idx == 2) return -threeSpacing;
        return 0f;
    }
}

/// <summary>
/// WP-D｜Formation Presentation（BattleUI partial）
///
/// 唯一视觉排布入口：战前 PlaceUnit / ReassignColumns、开战首次渲染、Gate 0 Move / Swap、
/// 单位移除后重排都走这里，避免「战前一套、战中另一套」。
/// </summary>
public partial class BattleUI : MonoBehaviour
{
    [Header("WP-D Formation Presentation（排内视觉排布；只影响画面，不改变 Row/Column Authority）")]
    [Tooltip("1 人排的排内视觉偏移（0 = 居中）")]
    public float singleUnitVisualOffset = 0f;

    [Tooltip("2 人排的视觉间距（两人排向中间聚拢；内部逻辑仍为 Left + Right）")]
    public float twoUnitVisualSpacing = 110f;

    [Tooltip("3 人排的视觉间距（三人排展开，保持 Left / Center / Right 顺序）")]
    public float threeUnitVisualSpacing = 180f;

    [Tooltip("排内整体视觉偏移（微调用；X 同时作为单位卡的横向偏移）")]
    public Vector2 rowLocalVisualOffset = Vector2.zero;

    /// <summary>按排内单位数量与相对顺序取视觉偏移（唯一的 Formation Presentation 计算入口）</summary>
    public float GetFormationSlotOffset(int unitCount, int slotIndex)
    {
        return BattleFormationVisualLayout.GetOffset(
            unitCount, slotIndex, singleUnitVisualOffset, twoUnitVisualSpacing, threeUnitVisualSpacing);
    }

    /// <summary>
    /// 目标标记 / 拖拽预览等表现所使用的「列视觉偏移」。
    /// 按 3 人排视觉网格换算，使表现层与 Formation Presentation 共用同一套视觉位置。
    /// </summary>
    public float GetFormationColumnOffset(BattleColumn col)
    {
        return GetFormationSlotOffset(BattleFormationVisualLayout.MaxVisualSlots, ColToIdx(col));
    }

    /// <summary>
    /// 按该排当前显示单位重新计算视觉排布（单一入口；不写任何逻辑 Row / Column）。
    /// </summary>
    public void ApplyFormationPresentation(RectTransform row)
    {
        if (row == null || _rowCards == null) return;
        if (!_rowCards.ContainsKey(row)) return;

        var ordered = GetFormationOrderedCards(row);
        if (ordered.Count == 0) return;

        int count = ordered.Count;
        for (int i = 0; i < count; i++)
        {
            var card = ordered[i];
            if (card == null) continue;
            var rt = card.GetComponent<RectTransform>();
            if (rt == null) continue;

            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(rowLocalVisualOffset.x, GetFormationSlotOffset(count, i) + rowLocalVisualOffset.y);
        }
    }

    /// <summary>六排全部按 Formation Presentation 重排（开战首次渲染 / 单位移除后使用）</summary>
    public void ApplyFormationPresentationToAllRows()
    {
        if (_allRows == null) return;
        foreach (var row in _allRows)
        {
            if (row == null) continue;
            ApplyFormationPresentation(row);
        }
    }

    /// <summary>
    /// 排内视觉顺序：优先按逻辑 Column（Left → Center → Right）与 Column Authority 一致；
    /// 战前布阵阶段（BattleUnit 尚未建立）回退 ReassignColumns 写入的预备列；
    /// 两者都不存在的卡保持原有相对顺序排在后面。
    /// </summary>
    private List<GameObject> GetFormationOrderedCards(RectTransform row)
    {
        var known = new List<KeyValuePair<int, GameObject>>();
        var unknown = new List<GameObject>();

        foreach (var card in _rowCards[row])
        {
            if (card == null || !card.activeSelf) continue;
            int idx = GetFormationColumnIndex(card);
            if (idx >= 0)
            {
                // 插入到首个更大列之前；相同列保留原有 UI 顺序。
                int insertAt = known.FindIndex(entry => entry.Key > idx);
                if (insertAt < 0) known.Add(new KeyValuePair<int, GameObject>(idx, card));
                else known.Insert(insertAt, new KeyValuePair<int, GameObject>(idx, card));
            }
            else unknown.Add(card);
        }

        var ordered = new List<GameObject>(known.Count + unknown.Count);
        for (int i = 0; i < known.Count; i++) ordered.Add(known[i].Value);
        ordered.AddRange(unknown);
        return ordered;
    }

    /// <summary>
    /// 取该卡当前逻辑列顺序：优先 BattleUnit.Column（战中 Authority），
    /// 战前布阵阶段回退 <c>_pendingCols</c>（ReassignColumns 写入的预备列）。
    /// 都不存在时返回 -1（不参与视觉排序，保持原相对顺序）。
    /// </summary>
    private int GetFormationColumnIndex(GameObject card)
    {
        var unit = FindUnitForCard(card);
        if (unit != null) return ColToIdx(unit.Column);

        string cid = GetCardId(card.name);
        if (!string.IsNullOrEmpty(cid) && _pendingCols != null && _pendingCols.ContainsKey(cid))
            return ColToIdx(_pendingCols[cid]);

        return -1;
    }

    /// <summary>
    /// 按 UI 卡名（Card_&lt;id&gt;）找到对应 BattleUnit。
    /// WP-Encounter-1C §8：卡名统一 = 本场身份 UnitId（玩家 CardId / 英雄 HeroId / 敌方 EnemyInstanceId）；
    /// 仅保留 SourceId 兼容回退（旧调用方 / 演示路径），产品路径不依赖它。
    /// </summary>
    public BattleUnit FindUnitForCard(GameObject card)
    {
        if (card == null) return null;
        var data = _battleSystem != null ? _battleSystem.GetBattleData() : null;
        if (data == null) return null;

        string id = GetCardId(card.name);
        if (string.IsNullOrEmpty(id)) return null;

        var unit = data.PlayerUnits.Find(u => u.UnitId == id);
        if (unit == null) unit = data.EnemyUnits.Find(u => u.UnitId == id);
        if (unit == null) unit = data.EnemyUnits.Find(u => u.SourceId == id);
        return unit;
    }
}
