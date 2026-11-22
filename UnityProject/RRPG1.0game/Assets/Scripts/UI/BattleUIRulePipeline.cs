using TMPro;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// BattleUI Gate 0 partial：配置化卡牌规则管线接线。
///
/// 负责：
///   - 为规则层提供真实 Battle Fact 视图（BattleRuleContextRuntime）并注入 BattleV1Runtime；
///   - 出牌合法性（支付 AP 前的 Primary Resolution / 手选目标校验）→ 支付 → 执行 → 刷新；
///   - 低成本 Debug 级目标表现：合法空格标记、《切配》自动目标预览、Move / Swap 后的卡片重排。
///
/// 不负责：
///   - 规则本身（BattleCardRuleResolver / BattleCardRuleExecutor）；
///   - 战斗/牌堆/空间状态（全部经既有 Authority 写入）。
/// </summary>
public partial class BattleUI : MonoBehaviour
{
    [Header("Gate 0 规则管线表现（临时低美术成本；Inspector 可调）")]
    [Tooltip("合法空格标记尺寸")]
    public Vector2 ruleCellMarkerSize = new Vector2(120f, 56f);

    [Tooltip("合法空格标记文字字号")]
    public int ruleCellMarkerFontSize = 18;

    [Tooltip("合法空格标记颜色")]
    public Color ruleCellMarkerColor = new Color(0.55f, 0.95f, 0.6f, 0.85f);

    [Tooltip("合法空格标记文字（测试者可识别当前需要选择的是什么目标）")]
    public string ruleCellMarkerText = "可选位置";

    private BattleRuleContextRuntime _ruleContext;
    private BattleSystem _ruleContextBattleSystem;
    private readonly List<GameObject> _ruleCellMarkers = new List<GameObject>();

    /// <summary>取得当前 Battle 的规则执行上下文（每场 Battle 的 BattleSystem 会重建，因此按需重建）</summary>
    private BattleRuleContextRuntime EnsureRuleContext()
    {
        if (_battleSystem == null || v1Runtime == null) return null;

        if (_ruleContext == null || !ReferenceEquals(_ruleContextBattleSystem, _battleSystem))
        {
            _ruleContext = new BattleRuleContextRuntime(_battleSystem, v1Runtime, GetPersonIdOfUnit);
            _ruleContextBattleSystem = _battleSystem;
        }

        return _ruleContext;
    }

    /// <summary>注入 Gate 0 规则管线所需的宿主接缝（每次初始化本场共享牌库后调用）</summary>
    private void InitGate0RulePipeline()
    {
        if (v1Runtime == null) return;

        _ruleContext = null;
        _ruleContextBattleSystem = null;
        v1Runtime.RuleContextProvider = EnsureRuleContext;
        v1Runtime.PresentationPeopleProvider = () => _playerData;
        v1Runtime.CardBadgeProvider = GetV1CardRuleBadge;
        v1Runtime.OnPlayerTurnStarted = OnPlayerTurnStartedGate0;
    }

    /// <summary>玩家回合开始：清除上一回合遗留的临时护盾与目标标记（Shield 不跨玩家回合残留）</summary>
    private void OnPlayerTurnStartedGate0()
    {
        if (_battleSystem != null) _battleSystem.ClearAllShields();
        ClearRuleCellMarkers();
    }

    /// <summary>
    /// 手牌附加提示：自动目标预览（《切配》当前会命中的牌）与手选目标提示（测试者可读）。
    /// </summary>
    private string GetV1CardRuleBadge(BattleCard card)
    {
        if (card == null) return null;

        var program = BattleCardRuleResolver.GetProgram(card);
        if (program == null || !program.IsExecutable) return null;

        var requirement = program.PlayRequirement;
        if (requirement != null && requirement.TargetSource != BattleRuleTargetSource.None)
        {
            var targets = BattleCardRuleResolver.PredictPrimaryTargets(card, EnsureRuleContext());
            if (targets.Count == 0) return "无合法目标";
            var names = new List<string>();
            foreach (var target in targets) names.Add(target.Describe());
            return "目标:" + string.Join(",", names.ToArray());
        }

        switch (program.ManualTargetKind)
        {
            case BattleRuleManualTargetKind.EnemyUnit: return "需选敌方";
            case BattleRuleManualTargetKind.AllyUnit: return "需选我方";
            case BattleRuleManualTargetKind.AllyCell: return "需选空格";
            default: return null;
        }
    }

    /// <summary>
    /// 显示当前待打出牌的全部合法空格（《补位》等以空格为目标的牌）。
    /// 合法性来源与出牌校验共用 BattleCardRuleResolver.GetLegalManualTargets。
    /// </summary>
    private void ShowRuleCellMarkers(BattleCard card)
    {
        ClearRuleCellMarkers();
        if (card == null) return;

        var targets = BattleCardRuleResolver.GetLegalManualTargets(card, EnsureRuleContext());
        foreach (var target in targets)
        {
            if (target.Kind != BattleRuleTargetKind.Cell) continue;
            CreateRuleCellMarker(card, target);
        }
    }

    private void CreateRuleCellMarker(BattleCard card, BattleRuleTargetRef cell)
    {
        var rowRect = GetRowRect(true, (BattleRow)cell.Row);
        if (rowRect == null) return;

        var go = new GameObject("RuleCellMarker_" + cell.Row + "_" + cell.Column,
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(rowRect, false);

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = ruleCellMarkerSize;
        rt.anchoredPosition = new Vector2(0, GetColumnOffset((BattleColumn)cell.Column));

        var image = go.GetComponent<Image>();
        if (image != null) image.color = ruleCellMarkerColor;

        var textGo = new GameObject("TMP_Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textGo.transform.SetParent(go.transform, false);
        var textRt = textGo.GetComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = Vector2.zero;
        textRt.offsetMax = Vector2.zero;

        var text = textGo.GetComponent<TMP_Text>();
        text.font = CurrentUIFontAuthority.Default;
        text.fontSize = ruleCellMarkerFontSize;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.black;
        text.raycastTarget = false;
        text.text = ruleCellMarkerText;

        var marker = go.AddComponent<BattleRuleCellMarker>();
        marker.labelText = text;
        marker.Bind(() => PlayV1CardAtCell(card, cell.Row, cell.Column));

        _ruleCellMarkers.Add(go);
    }

    /// <summary>清除全部临时空格标记（幂等）</summary>
    private void ClearRuleCellMarkers()
    {
        for (int i = 0; i < _ruleCellMarkers.Count; i++)
        {
            if (_ruleCellMarkers[i] == null) continue;
            if (Application.isPlaying) Destroy(_ruleCellMarkers[i]);
            else DestroyImmediate(_ruleCellMarkers[i]);
        }
        _ruleCellMarkers.Clear();
    }

    /// <summary>以“我方空格”为目标打出配置化牌</summary>
    private void PlayV1CardAtCell(BattleCard card, int row, int column)
    {
        if (card == null) return;
        PlayRuleV1Card(card, BattleRuleTargetRef.ForCell(row, column, null));
    }

    /// <summary>
    /// 配置化牌出牌流程（Gate 0）：
    ///   支付 AP 前完成合法性判断 → 绑定目标 → 支付 → 执行 → 去向 → 刷新。
    /// 数值在执行阶段读取（例如《收火》的“使用后 AP 为 0”必须在支付后判断）。
    /// </summary>
    private void PlayRuleV1Card(BattleCard card, BattleRuleTargetRef manualTarget)
    {
        if (_feedbackBusy) return;
        var context = EnsureRuleContext();
        if (context == null)
        {
            Log("规则执行上下文不可用，无法出牌：" + card.DisplayName);
            return;
        }

        // 1. 出牌合法性（Primary Resolution / 手选目标），必须在支付 AP 之前
        var legality = BattleCardRuleResolver.CheckPlayable(card, context, manualTarget);
        if (!legality.IsLegal)
        {
            Log(legality.Reason);
            return;
        }

        // 2. 伤害牌沿用既有统一攻击距离判定（R5.3.1 + RangeModifier），不新增第二套距离规则
        if (manualTarget.Kind == BattleRuleTargetKind.Unit)
        {
            var unitTarget = FindBattleUnit(manualTarget.UnitId);
            if (!CanV1CardReachTarget(card, unitTarget))
            {
                Log("目标超出攻击距离，无法打出 " + card.DisplayName);
                return;
            }
        }

        if (!v1Runtime.CanPlayCard(card))
        {
            Log("行动力不足或当前不可打出：" + card.DisplayName);
            return;
        }

        // 3. 绑定目标（支付之前）→ 支付共享行动力 → 执行
        var plan = BattleCardRuleResolver.BuildPlan(card, context, manualTarget);
        ExecuteCardWithFeedback(card, records =>
        {
            var resolution = BattleCardRuleExecutor.Execute(plan, context);
            AddRuleFeedback(card, resolution, records);
            Log(card.DisplayName + " 已使用 → " + resolution.Describe());
        });
    }

    private BattleUnit FindBattleUnit(string unitId)
    {
        if (_battleSystem == null || string.IsNullOrEmpty(unitId)) return null;
        var data = _battleSystem.GetBattleData();
        if (data == null) return null;

        var unit = data.PlayerUnits.Find(u => u != null && u.UnitId == unitId);
        if (unit != null) return unit;
        return data.EnemyUnits.Find(u => u != null && u.UnitId == unitId);
    }

    /// <summary>
    /// 战中 Move / Swap 后把单位卡对齐到逻辑位置（唯一空间 Authority = 单位所属 Board + unit.Row / Column）。
    /// </summary>
    private void RepositionAllUnitCards()
    {
        if (_battleSystem == null || _rowCards == null) return;
        var data = _battleSystem.GetBattleData();
        if (data == null) return;

        foreach (var unit in data.PlayerUnits) ReparentUnitCard(unit, true);
        foreach (var unit in data.EnemyUnits) ReparentUnitCard(unit, false);
        ApplyFormationPresentationToAllRows();

        // 保持战前布阵字典与逻辑位置一致（角色单位 UnitId 即角色卡 CardId）
        if (_pendingRows != null && _pendingCols != null)
        {
            foreach (var unit in data.PlayerUnits)
            {
                if (unit == null || string.IsNullOrEmpty(unit.UnitId)) continue;
                if (!_pendingRows.ContainsKey(unit.UnitId)) continue;
                _pendingRows[unit.UnitId] = unit.Row;
                _pendingCols[unit.UnitId] = unit.Column;
            }
        }
    }

    private void ReparentUnitCard(BattleUnit unit, bool isPlayer)
    {
        if (unit == null) return;

        var targetRow = GetRowRect(isPlayer, unit.Row);
        if (targetRow == null || !_rowCards.ContainsKey(targetRow)) return;

        // WP-Encounter-1C §8：双方 UI 卡名统一用本场身份 UnitId（敌方 = EnemyInstanceId）
        string cardName = "Card_" + unit.UnitId;

        GameObject card = null;
        foreach (var kv in _rowCards)
        {
            var found = kv.Value.Find(c => c != null && c.name == cardName);
            if (found == null) continue;

            if (kv.Key != targetRow) kv.Value.Remove(found);
            card = found;
            break;
        }

        if (card == null) return;

        if (card.transform.parent != targetRow)
        {
            card.transform.SetParent(targetRow, false);
            if (!_rowCards[targetRow].Contains(card)) _rowCards[targetRow].Add(card);
        }

        // 最终坐标统一由 ApplyFormationPresentation 对整排当前成员计算。
    }
}
