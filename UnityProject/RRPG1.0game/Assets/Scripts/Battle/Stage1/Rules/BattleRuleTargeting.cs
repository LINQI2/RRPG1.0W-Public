using System.Collections.Generic;

/// <summary>
/// 战斗牌规则的目标解析（Query + Selector）与集合工具（纯逻辑）。
///
/// 负责：
///   - 把 BattleRuleTargetSource / BattleRuleSelect 解析为已绑定目标集合；
///   - 提供目标等价判断，供“所选目标是否合法”校验使用。
///
/// 不负责：
///   - 合法性判断与计划编排（见 BattleCardRuleResolver）；
///   - 数值计算与状态写入（见 BattleCardRuleExecutor）。
/// </summary>
public static class BattleRuleTargeting
{
    /// <summary>
    /// 目标集合解析。所有来源均读取当前 Battle Fact（IBattleRuleContext），不缓存第二套战场真相。
    /// </summary>
    public static List<BattleRuleTargetRef> ResolveTargets(BattleRuleTargetSource source, BattleRuleSelect select,
                                                           BattleCard card, IBattleRuleContext context,
                                                           BattleRuleTargetRef manualTarget)
    {
        var targets = new List<BattleRuleTargetRef>();
        if (context == null) return targets;

        switch (source)
        {
            case BattleRuleTargetSource.ManualTarget:
                if (manualTarget.IsValid) targets.Add(manualTarget);
                break;

            case BattleRuleTargetSource.CardOwner:
            {
                string ownerUnitId;
                BattleRuleUnitFacts owner;
                if (context.TryGetOwnerUnitId(card, out ownerUnitId) && context.TryGetUnit(ownerUnitId, out owner))
                    targets.Add(BattleRuleTargetRef.ForUnit(owner));
                break;
            }

            case BattleRuleTargetSource.ManualTargetAdjacentAllies:
                if (manualTarget.Kind == BattleRuleTargetKind.Unit)
                {
                    var adjacent = context.GetAdjacentAllies(manualTarget.UnitId);
                    if (adjacent != null)
                    {
                        foreach (var ally in adjacent)
                            targets.Add(BattleRuleTargetRef.ForUnit(ally));
                    }
                }
                break;

            case BattleRuleTargetSource.HandOthers:
            {
                var candidates = new List<BattleRuleTargetRef>();
                var hand = context.GetHandCards();
                if (hand != null)
                {
                    foreach (var handCard in hand)
                    {
                        if (handCard == null || ReferenceEquals(handCard, card)) continue;
                        candidates.Add(BattleRuleTargetRef.ForCard(handCard));
                    }
                }
                targets = ApplySelect(candidates, select);
                break;
            }

            case BattleRuleTargetSource.EmptyAllyCells:
                foreach (var cell in context.GetAlliedCells())
                {
                    if (cell == null || cell.Occupied) continue;
                    targets.Add(BattleRuleTargetRef.ForCell(cell.Row, cell.Column, null));
                }
                targets = ApplySelect(targets, select);
                break;

            case BattleRuleTargetSource.OtherAllyUnits:
            {
                string ownerUnitId;
                bool hasOwner = context.TryGetOwnerUnitId(card, out ownerUnitId);
                foreach (var ally in context.GetAllies())
                {
                    if (ally == null || !ally.IsCombatCapable) continue;
                    if (hasOwner && ally.UnitId == ownerUnitId) continue;
                    targets.Add(BattleRuleTargetRef.ForUnit(ally));
                }
                targets = ApplySelect(targets, select);
                break;
            }
        }

        return targets;
    }

    /// <summary>
    /// Selector：按配置归约目标集合。
    /// MaxByActualCostLeftmost 取 Actual Cost 最高的一张；并列时保留手牌顺序中最左者（确定性、非随机）。
    /// </summary>
    public static List<BattleRuleTargetRef> ApplySelect(List<BattleRuleTargetRef> candidates, BattleRuleSelect select)
    {
        if (select != BattleRuleSelect.MaxByActualCostLeftmost) return candidates;

        var result = new List<BattleRuleTargetRef>();
        var best = BattleRuleTargetRef.None();
        int bestCost = int.MinValue;
        foreach (var candidate in candidates)
        {
            if (candidate.Kind != BattleRuleTargetKind.HandCard || candidate.Card == null) continue;
            int cost = candidate.Card.ActualCost;
            // 严格大于：并列时保留先出现（最左）的一张
            if (!best.IsValid || cost > bestCost)
            {
                best = candidate;
                bestCost = cost;
            }
        }

        if (best.IsValid) result.Add(best);
        return result;
    }

    /// <summary>目标集合中是否存在等价目标</summary>
    public static bool ContainsSame(List<BattleRuleTargetRef> targets, BattleRuleTargetRef target)
    {
        if (targets == null) return false;
        foreach (var candidate in targets)
        {
            if (candidate.SameAs(target)) return true;
        }
        return false;
    }
}
