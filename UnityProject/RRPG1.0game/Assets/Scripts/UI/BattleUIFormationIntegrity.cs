using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Battle Formation &amp; Active Roster 一致性守卫（BattleUI partial）。
///
/// 必须保持的不变量：
///   A. 战前准备阶段：_rowCards ↔ _pendingRows ↔ _pendingCols 成员一致；
///      每排 ≤ 3；同排列唯一；总数 ≤ 6；不为非法第 4 人创建视觉槽；
///   B. 开战边界：OnStartBattle 在 _battleStarted = true / 隐藏准备 UI 之前做全量预检；
///   C. 部署阶段：DeployCharacter / DeployHero 的返回值是完整性断言，
///      任一失败 = 安全中止本次开战并回到战前准备（不缩减阵容、不留 UI ghost、不建错误牌库）；
///   D. Active Battle Roster：共享牌库人物集合 = 真实成功部署的 BattleData.PlayerUnits（见 InitV1Runtime）。
///
/// 明确不改：Battle GameSpec / R4.1 六人上限 / Player-Enemy 双 3×3 Board / Left-Center-Right /
///          WP-D 1-2-3 人表现 / Move / Swap / Range / Adjacent / Shared AP / Card 产品规则。
/// </summary>
public partial class BattleUI : MonoBehaviour
{
    // ==================== 排 / 成员工具 ====================

    /// <summary>该 RectTransform 是否为我方 Board 的排（战前布阵只允许落到我方排）。</summary>
    public bool IsPlayerRow(RectTransform row)
    {
        return row != null && (row == playerFrontRow || row == playerMiddleRow || row == playerBackRow);
    }

    /// <summary>
    /// 该排当前的实际上阵单位卡数量（不含敌方预览卡；可排除某个成员，用于「移动而非新增」判定）。
    /// </summary>
    public int CountRowMembers(RectTransform row, string excludingMemberId = null)
    {
        if (row == null || _rowCards == null || !_rowCards.ContainsKey(row)) return 0;

        int count = 0;
        foreach (var card in _rowCards[row])
        {
            if (card == null || card.name.StartsWith("EnemyPrep")) continue;
            if (!string.IsNullOrEmpty(excludingMemberId) && card.name == "Card_" + excludingMemberId) continue;
            count++;
        }
        return count;
    }

    /// <summary>
    /// 战前布阵唯一放行判定：成员能否进入目标排（失败不产生任何写入）。
    /// Current GameSpec R4.1（总数 ≤ 6）+ R5.0（一排 ≤ 3，不存在第 4 列）。
    /// </summary>
    public bool TryValidateRowPlacement(string memberId, RectTransform targetRow, out string reason)
    {
        reason = null;

        if (!IsPlayerRow(targetRow))
        {
            reason = "只能上阵到我方战场排";
            return false;
        }

        bool alreadyPlaced = _pendingRows != null && !string.IsNullOrEmpty(memberId) && _pendingRows.ContainsKey(memberId);
        int totalMembers = _pendingRows != null ? _pendingRows.Count : 0;
        int unitsInTargetRow = CountRowMembers(targetRow, memberId);

        if (BattleFormationPlan.CanPlace(totalMembers, alreadyPlaced, unitsInTargetRow)) return true;

        reason = unitsInTargetRow >= BattleFormationPlan.MaxUnitsPerRow
            ? "该排已满（每排最多 " + BattleFormationPlan.MaxUnitsPerRow + " 名成员）"
            : "最多上阵 " + BattleFormationPlan.MaxTeamUnits + " 名成员（英雄占用正常上阵位置）";
        return false;
    }

    /// <summary>某排的实际上阵单位卡（不含敌方预览卡、不含空对象）。</summary>
    private List<GameObject> PlayerCardsInRow(RectTransform row)
    {
        var result = new List<GameObject>();
        if (row == null || _rowCards == null || !_rowCards.ContainsKey(row)) return result;

        foreach (var card in _rowCards[row])
        {
            if (card == null || card.name.StartsWith("EnemyPrep")) continue;
            result.Add(card);
        }
        return result;
    }

    // ==================== 逻辑列写入（唯一入口） ====================

    /// <summary>
    /// 排内顺序 → 逻辑列的唯一写入入口（战前布阵 / 拖拽落点 / 拖拽恢复共用）。
    /// 只写前 MaxUnitsPerRow 个槽位；多余成员不获得任何列写入，也不会创建第 4 个视觉槽。
    /// 每写一列都同时同步 Player Board 占位（UI 不得绕过 Board Occupancy 守卫）。
    /// </summary>
    private void AssignRowColumns(RectTransform row, List<GameObject> orderedCards)
    {
        if (row == null || orderedCards == null) return;

        BattleRow bRow = RectToBattleRow(row);
        int slotCount = System.Math.Min(orderedCards.Count, BattleFormationPlan.MaxUnitsPerRow);

        for (int slot = 0; slot < slotCount; slot++)
        {
            var card = orderedCards[slot];
            if (card == null) continue;

            string cid = GetCardId(card.name);
            if (string.IsNullOrEmpty(cid)) continue;

            BattleColumn col = IdxToCol(BattleFormationPlan.ColumnIndexForSlot(slot, slotCount));
            _pendingCols[cid] = col;

            // WP-D：视觉位置统一由 Formation Presentation 计算（本方法只负责逻辑列分配）
            SyncPlayerBoardOccupancy(cid, bRow, col);
        }

        // WP-D：战前布阵结束统一走 Formation Presentation（与开战 / Move / Swap 同一入口）
        ApplyFormationPresentation(row);
    }

    /// <summary>
    /// 把「UI 预备列」同步到 Player Board：Board 占位与 unit.Row / unit.Column 必须同时一致。
    /// 目标格被仍具战斗能力的单位占用时拒绝写入并保持原状（不产生 Board / 坐标脱节）。
    /// 身份路径沿用现有规则：角色单位 UnitId = CardId；英雄单位 UnitId = SourceId = HeroId（不新增第二套身份 Authority）。
    /// </summary>
    private void SyncPlayerBoardOccupancy(string memberId, BattleRow row, BattleColumn col)
    {
        if (_battleSystem == null) return;
        var data = _battleSystem.GetBattleData();
        if (data == null || data.PlayerUnits == null) return;

        var unit = data.PlayerUnits.Find(u => u != null && u.UnitId == memberId);
        if (unit == null) unit = data.PlayerUnits.Find(u => u != null && u.SourceId == memberId);
        if (unit == null) return;

        if (data.PlayerField.Contains(unit))
        {
            if (unit.Row == row && unit.Column == col) return;
            if (!data.PlayerField.TryMoveUnit(unit, row, col))
                Debug.LogWarning("[BattleUI] 列同步被 Board 占位守卫拒绝（保持原占位）：" + memberId + " → " + row + "/" + col);
            return;
        }

        if (data.PlayerField.CanDeploy(row, col)) data.PlayerField.Deploy(unit, row, col);
        else Debug.LogWarning("[BattleUI] 列同步被 Board 占位守卫拒绝（目标格已被占用）：" + memberId + " → " + row + "/" + col);
    }

    // ==================== 战前准备 → 开战边界 ====================

    /// <summary>拖拽开始时的源排成员顺序（含被拖拽成员），用于拒绝移动时原子恢复原 Column / Pending 状态。</summary>
    private List<string> _dragSourceOrder;

    /// <summary>
    /// 开战前的最终预检。失败时调用方必须保持战前准备阶段：
    /// 不置 _battleStarted、不隐藏准备 UI、不部分启动战斗。
    /// </summary>
    public bool TryValidatePendingFormation(out string reason)
    {
        reason = null;
        EnsureRowDataReady();

        if (_pendingRows == null || _pendingCols == null || _rowCards == null)
        {
            reason = "阵型数据缺失";
            return false;
        }

        // 成员集合一致：每个 pending member 恰有一个 Row / 一个 Column；不得存在无排成员
        foreach (var kv in _pendingCols)
        {
            if (!_pendingRows.ContainsKey(kv.Key)) { reason = "存在没有排信息的预备成员：" + kv.Key; return false; }
        }

        var ids = new List<string>(_pendingRows.Keys);
        ids.Sort(string.CompareOrdinal);

        var rows = new int[ids.Count];
        var cols = new int[ids.Count];
        for (int i = 0; i < ids.Count; i++)
        {
            string id = ids[i];
            if (!_pendingCols.ContainsKey(id)) { reason = "成员缺少列信息：" + id; return false; }
            rows[i] = (int)_pendingRows[id];
            cols[i] = (int)_pendingCols[id];
        }

        // 非空 / 总数 ≤ 6 / 每排 ≤ 3 / 同排列唯一 / 行列合法
        string snapshotReason = BattleFormationPlan.ValidateSnapshot(rows, cols);
        if (snapshotReason != null) { reason = snapshotReason; return false; }

        // _rowCards ↔ _pendingRows ↔ _pendingCols 成员一致（每排视觉卡与 Pending 成员一一对应，且不得重复）
        for (int r = 0; r < BattleFormationPlan.RowCount; r++)
        {
            var rowRect = GetRowRect(true, (BattleRow)r);
            var cards = PlayerCardsInRow(rowRect);

            int pendingInRow = 0;
            for (int i = 0; i < ids.Count; i++) if (rows[i] == r) pendingInRow++;

            if (cards.Count != pendingInRow)
            {
                reason = "战前布阵视觉与上阵成员不一致（" + (BattleRow)r + " 排）";
                return false;
            }

            var seen = new HashSet<string>();
            foreach (var card in cards)
            {
                string cid = GetCardId(card.name);
                if (string.IsNullOrEmpty(cid) || !_pendingRows.ContainsKey(cid) || (int)_pendingRows[cid] != r)
                {
                    reason = "战场存在无法归属的上阵卡：" + card.name;
                    return false;
                }
                if (!seen.Add(cid)) { reason = "同一排出现重复成员卡：" + cid; return false; }
            }
        }

        return true;
    }

    // ==================== 已上阵单位卡跨排移动 ====================

    /// <summary>
    /// 已上阵单位卡跨排移动的唯一写入入口（拖拽落点与回归测试都走这里）。
    /// 目标排满 / 目标非法 → 失败零写入，并原子恢复源排（卡不丢失、不产生重复 Column）。
    /// </summary>
    public bool TryReassignUnitCardToRow(string cardId, RectTransform targetRow, float dropScreenY)
    {
        if (string.IsNullOrEmpty(cardId) || _pendingRows == null || _pendingCols == null)
        {
            RestoreDraggedCardToSourceRow(cardId);
            return false;
        }

        bool alreadyPlaced = _pendingRows.ContainsKey(cardId);
        int unitsInTargetRow = CountRowMembers(targetRow, cardId);

        if (!IsPlayerRow(targetRow)
            || !BattleFormationPlan.CanPlace(_pendingRows.Count, alreadyPlaced, unitsInTargetRow))
        {
            Log("该排已满（每排最多 " + BattleFormationPlan.MaxUnitsPerRow + " 名成员），已恢复到原位置");
            RestoreDraggedCardToSourceRow(cardId);
            return false;
        }

        _pendingRows[cardId] = RectToBattleRow(targetRow);

        if (_dragCard == null)
        {
            // 没有拖拽上下文：只校正 Pending 记录，不移动任何 UI（防御纵深）
            ReassignColumns(targetRow, dropScreenY);
            return true;
        }

        _dragCard.transform.SetParent(targetRow, false);
        var rt = _dragCard.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.name = "Card_" + cardId;
        if (!_rowCards[targetRow].Contains(_dragCard)) _rowCards[targetRow].Add(_dragCard);

        ReassignColumns(targetRow, dropScreenY);

        // 如果拖到了不同排，恢复源排卡牌的位置与列（清除预览偏移，避免残影）
        if (targetRow != _dragSourceRow && _dragSourceRow != null && _rowCards.ContainsKey(_dragSourceRow))
            ReassignColumns(_dragSourceRow, dropScreenY);

        AddUnitCardDrag(_dragCard, cardId);
        ApplyFormationPresentationToAllRows();
        return true;
    }

    /// <summary>
    /// 原子恢复：把拖拽中的卡放回源排，并按拖拽开始时的源排顺序重算列与 Pending 成员。
    /// 目标排完全不受影响（拒绝路径零写入）。
    /// </summary>
    private void RestoreDraggedCardToSourceRow(string cardId)
    {
        var card = _dragCard;
        _dragCard = null;
        if (card == null) return;

        var sourceRow = _dragSourceRow;
        if (sourceRow == null || !IsPlayerRow(sourceRow) || _rowCards == null || !_rowCards.ContainsKey(sourceRow))
        {
            // 没有可恢复的源排：退回手牌（与「丢出战场排外」语义一致）
            Destroy(card);
            if (_pendingRows != null && !string.IsNullOrEmpty(cardId)) _pendingRows.Remove(cardId);
            if (_pendingCols != null && !string.IsNullOrEmpty(cardId)) _pendingCols.Remove(cardId);
            return;
        }

        card.transform.SetParent(sourceRow, false);
        var rt = card.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        card.name = "Card_" + cardId;
        if (!_rowCards[sourceRow].Contains(card)) _rowCards[sourceRow].Add(card);
        _pendingRows[cardId] = RectToBattleRow(sourceRow);

        // 按拖拽开始时的源排顺序恢复（原 Column / Pending 状态）
        var ordered = new List<GameObject>();
        if (_dragSourceOrder != null)
        {
            foreach (var id in _dragSourceOrder)
            {
                var go = _rowCards[sourceRow].Find(c => c != null && c.name == "Card_" + id);
                if (go != null && !ordered.Contains(go)) ordered.Add(go);
            }
        }
        foreach (var c in _rowCards[sourceRow])
            if (c != null && !ordered.Contains(c)) ordered.Add(c);

        AssignRowColumns(sourceRow, ordered);
        ApplyFormationPresentationToAllRows();
    }

    // ==================== 部署完整性中止 ====================

    /// <summary>
    /// 部署阶段完整性错误：安全中止本次开战，回到战前准备阶段。
    /// 不缩减阵容继续、不留 UI ghost、不建立错误共享牌库。
    /// </summary>
    private void AbortBattleStart(string reason)
    {
        // 完整性错误必须同时进入 Console（只写 infoText 会让"从未发生"的错误无法诊断）
        Debug.LogWarning("[BattleUI] AbortBattleStart（部署完整性错误）：" + reason);

        _battleStarted = false;     // 回到战前准备：RunBattle 循环会再次等待玩家点击开始战斗
        _battleSystem = null;       // 丢弃本次半成品战斗（BattleData / PlayerUnits / Board 全部作废）

        if (btnStartBattle != null) btnStartBattle.gameObject.SetActive(true);
        if (btnEndTurn != null) btnEndTurn.gameObject.SetActive(false);

        RestorePrepHandVisibility();
        ApplyFormationPresentationToAllRows();
        Log(reason + "；已中止本次开战并回到战前准备。");
    }

    /// <summary>
    /// 恢复战前准备表现：未上阵成员的准备卡重新可见；已上阵成员的准备卡保持隐藏（不制造重复 UI）。
    /// </summary>
    private void RestorePrepHandVisibility()
    {
        var host = PrepCardHost;
        if (host == null) return;

        foreach (Transform child in host)
        {
            if (child == null) continue;
            string id = PrepMemberIdOfCardObject(child.name);
            if (id == null) continue;

            bool deployed = _pendingRows != null && _pendingRows.ContainsKey(id);
            child.gameObject.SetActive(!deployed);
        }
    }

    /// <summary>准备卡对象名（PrepCard_&lt;id&gt; / HeroPrepCard_&lt;id&gt;）→ 成员 id；非准备卡返回 null。</summary>
    public static string PrepMemberIdOfCardObject(string objectName)
    {
        if (string.IsNullOrEmpty(objectName)) return null;
        const string heroPrefix = "HeroPrepCard_";
        const string charPrefix = "PrepCard_";
        if (objectName.StartsWith(heroPrefix)) return objectName.Substring(heroPrefix.Length);
        if (objectName.StartsWith(charPrefix)) return objectName.Substring(charPrefix.Length);
        return null;
    }

    // ==================== Active Battle Roster ====================

    /// <summary>
    /// 本场实际参战名单（Active Battle Roster）→ 人物身份列表。
    /// 唯一事实来源 = 真实成功部署的 BattleData.PlayerUnits（Current GameSpec R8.1），
    /// 不再从 _pendingRows 推断「已参战」；身份映射继续走 GetPersonIdOfUnit 单一入口。
    /// </summary>
    private List<string> BuildActiveRosterPersonIds()
    {
        var deployed = new List<string>();
        var data = _battleSystem != null ? _battleSystem.GetBattleData() : null;
        if (data == null || data.PlayerUnits == null)
        {
            Debug.LogWarning("[BattleUI] InitV1Runtime：BattleData 缺失，本场共享牌库人物集合为空");
            return deployed;
        }

        foreach (var unit in data.PlayerUnits)
        {
            if (unit == null || unit.IsDown) continue;

            string pid = GetPersonIdOfUnit(unit);
            if (string.IsNullOrEmpty(pid))
            {
                // Foundation Identity Cleanup：unresolved 不制造临时 Person truth，也不进入本场共享牌库
                Debug.LogWarning("[BattleUI] InitV1Runtime：本场上阵单位 " + unit.UnitId
                    + " 无法解析人物身份，其角色牌不进入本场共享牌库。");
                continue;
            }
            if (!deployed.Contains(pid)) deployed.Add(pid);
        }
        return deployed;
    }
}
