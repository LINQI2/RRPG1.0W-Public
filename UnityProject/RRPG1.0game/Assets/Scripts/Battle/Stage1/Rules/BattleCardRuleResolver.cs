using System.Collections.Generic;

/// <summary>
/// Battle V0.1 Gate 0 战斗牌规则解析器（纯逻辑，不依赖 UnityEngine / Assembly-CSharp）。
///
/// 负责：
///   - 读取牌上的规则程序，判断当前是否可打出（Primary Resolution 合法性、手选目标合法性）；
///   - 在支付 AP 之前一次绑定各步骤目标，产出可执行计划（BattleCardRulePlan）；
///   - 为 UI 提供“合法手选目标集合”与“自动目标预览”（与合法性判断共用同一来源）。
///
/// 不负责：
///   - 任何具体卡牌的名称 / ID 判断（只有配置驱动）；
///   - 状态写入（执行由 BattleCardRuleExecutor + IBattleRuleContext 宿主完成）。
/// </summary>
public static class BattleCardRuleResolver
{
    /// <summary>读取牌上的规则程序（未迁移的牌返回 null）</summary>
    public static BattleCardRuleProgram GetProgram(BattleCard card)
    {
        return card != null ? card.RuleProgram : null;
    }

    /// <summary>该牌是否已迁移到配置化规则管线</summary>
    public static bool HasRuleProgram(BattleCard card)
    {
        var program = GetProgram(card);
        return program != null && program.IsExecutable;
    }

    /// <summary>该牌需要的手选目标类别（None = 自动目标 / 只作用于牌主）</summary>
    public static BattleRuleManualTargetKind GetManualTargetKind(BattleCard card)
    {
        var program = GetProgram(card);
        if (program == null || !program.IsExecutable) return BattleRuleManualTargetKind.None;
        return program.ManualTargetKind;
    }

    /// <summary>该牌是否需要玩家手选目标</summary>
    public static bool RequiresManualTarget(BattleCard card)
    {
        return GetManualTargetKind(card) != BattleRuleManualTargetKind.None;
    }

    /// <summary>
    /// 出牌合法性检查（必须在支付 AP 之前调用）。
    /// 未迁移的 Legacy 牌返回 Legal（沿用既有 Legacy 校验路径，不在本类重复实现）。
    /// </summary>
    public static BattleCardRuleLegality CheckPlayable(BattleCard card, IBattleRuleContext context,
                                                       BattleRuleTargetRef manualTarget = default(BattleRuleTargetRef))
    {
        var result = new BattleCardRuleLegality { IsLegal = true, Kind = BattleCardRuleLegalityKind.Legal };
        if (!HasRuleProgram(card)) return result;

        if (context == null)
        {
            result.IsLegal = false;
            result.Kind = BattleCardRuleLegalityKind.PrimaryResolutionUnavailable;
            result.Reason = "缺少规则执行上下文（无法读取 Battle Fact）";
            return result;
        }

        var program = card.RuleProgram;
        var legalManualTargets = GetLegalManualTargets(card, context);

        if (program.RequiresManualTarget)
        {
            if (legalManualTargets.Count == 0)
            {
                result.IsLegal = false;
                result.Kind = BattleCardRuleLegalityKind.NoLegalManualTarget;
                result.Reason = "当前没有合法目标，无法打出：" + card.DisplayName;
                return result;
            }

            if (manualTarget.IsValid && !BattleRuleTargeting.ContainsSame(legalManualTargets, manualTarget))
            {
                result.IsLegal = false;
                result.Kind = BattleCardRuleLegalityKind.IllegalManualTarget;
                result.Reason = "所选目标非法：" + manualTarget.Describe();
                return result;
            }
        }

        var requirement = program.PlayRequirement;
        if (requirement != null && requirement.TargetSource != BattleRuleTargetSource.None)
        {
            var primary = BattleRuleTargeting.ResolveTargets(requirement.TargetSource, requirement.Select,
                                                             card, context, manualTarget);
            result.PrimaryTargets = primary;

            int minCount = requirement.MinCount > 0 ? requirement.MinCount : 1;
            if (primary.Count < minCount)
            {
                result.IsLegal = false;
                result.Kind = BattleCardRuleLegalityKind.PrimaryResolutionUnavailable;
                result.Reason = "主要结算没有合法结果，无法打出：" + card.DisplayName;
                return result;
            }
        }
        else if (manualTarget.IsValid)
        {
            result.PrimaryTargets = new List<BattleRuleTargetRef> { manualTarget };
        }

        return result;
    }

    /// <summary>
    /// 预测本牌的 Primary Resolution 目标（供 UI 在打出前展示，例如《切配》当前自动目标）。
    /// </summary>
    public static List<BattleRuleTargetRef> PredictPrimaryTargets(BattleCard card, IBattleRuleContext context)
    {
        var legality = CheckPlayable(card, context);
        return legality.PrimaryTargets ?? new List<BattleRuleTargetRef>();
    }

    /// <summary>
    /// 该牌当前的全部合法手选目标（UI 高亮 / 点击校验与合法性检查共用同一来源）。
    /// </summary>
    public static List<BattleRuleTargetRef> GetLegalManualTargets(BattleCard card, IBattleRuleContext context)
    {
        var targets = new List<BattleRuleTargetRef>();
        if (!HasRuleProgram(card) || context == null) return targets;

        var kind = card.RuleProgram.ManualTargetKind;
        var requirement = card.RuleProgram.PlayRequirement;
        var requirementSource = requirement != null ? requirement.TargetSource : BattleRuleTargetSource.None;

        switch (kind)
        {
            case BattleRuleManualTargetKind.EnemyUnit:
                foreach (var enemy in context.GetEnemies())
                {
                    if (enemy == null || !enemy.IsCombatCapable) continue;
                    targets.Add(BattleRuleTargetRef.ForUnit(enemy));
                }
                break;

            case BattleRuleManualTargetKind.AllyUnit:
            {
                string ownerUnitId;
                bool hasOwner = context.TryGetOwnerUnitId(card, out ownerUnitId);
                // 换岗类（前置来源 = 除牌主外的我方单位）必须排除牌主自身
                bool excludeOwner = requirementSource == BattleRuleTargetSource.OtherAllyUnits;
                foreach (var ally in context.GetAllies())
                {
                    if (ally == null || !ally.IsCombatCapable) continue;
                    if (excludeOwner && hasOwner && ally.UnitId == ownerUnitId) continue;
                    targets.Add(BattleRuleTargetRef.ForUnit(ally));
                }
                break;
            }

            case BattleRuleManualTargetKind.AllyCell:
                foreach (var cell in context.GetAlliedCells())
                {
                    if (cell == null || cell.Occupied) continue;
                    targets.Add(BattleRuleTargetRef.ForCell(cell.Row, cell.Column, null));
                }
                break;
        }

        return targets;
    }

    /// <summary>
    /// 绑定目标并产出一份可执行计划（目标绑定发生在支付 AP 之前；数值在执行时读取当前 Fact）。
    /// </summary>
    public static BattleCardRulePlan BuildPlan(BattleCard card, IBattleRuleContext context,
                                               BattleRuleTargetRef manualTarget = default(BattleRuleTargetRef))
    {
        var plan = new BattleCardRulePlan
        {
            Card = card,
            Program = GetProgram(card),
            ManualTarget = manualTarget
        };

        if (!HasRuleProgram(card) || context == null) return plan;

        var requirement = plan.Program.PlayRequirement;
        if (requirement != null && requirement.TargetSource != BattleRuleTargetSource.None)
            plan.PrimaryTargets = BattleRuleTargeting.ResolveTargets(requirement.TargetSource, requirement.Select,
                                                                     card, context, manualTarget);
        else if (manualTarget.IsValid)
            plan.PrimaryTargets = new List<BattleRuleTargetRef> { manualTarget };

        foreach (var step in plan.Program.Steps)
        {
            if (step == null) continue;
            plan.Steps.Add(new BattleRuleBoundStep
            {
                Definition = step,
                Targets = BattleRuleTargeting.ResolveTargets(step.TargetSource, step.Select, card, context, manualTarget)
            });
        }

        return plan;
    }
}
