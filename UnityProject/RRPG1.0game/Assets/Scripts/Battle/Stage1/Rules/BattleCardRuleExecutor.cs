using System.Collections.Generic;

/// <summary>
/// Battle V0.1 Gate 0 战斗牌规则执行器（纯逻辑）。
///
/// 负责：
///   - 按已绑定的计划顺序执行步骤：计算数值（读取当前 Battle Fact）→ 调用 IBattleRuleContext 的写入接缝；
///   - 区分“Operation 对目标不适用”（Not Applicable）与“目标非法”（不进入执行）；
///   - 记录每步结果，供 UI 日志与测试断言。
///
/// 不负责：
///   - 目标选择与合法性判断（BattleCardRuleResolver）；
///   - 战斗状态本身（全部写入经宿主转发到既有 Battle Authority）。
///
/// 顺序约定：本执行器在支付 AP 之后调用，因此依赖“共享行动力”的条件（如 AP 归零）读取的是支付后的真实状态。
/// </summary>
public static class BattleCardRuleExecutor
{
    /// <summary>执行一份已绑定计划。</summary>
    public static BattleCardRuleResolutionResult Execute(BattleCardRulePlan plan, IBattleRuleContext context)
    {
        var result = new BattleCardRuleResolutionResult();
        if (plan == null || context == null) return result;

        foreach (var boundStep in plan.Steps)
        {
            if (boundStep == null || boundStep.Definition == null) continue;
            var step = boundStep.Definition;

            // Draw 无目标：直接按数值抽牌
            if (step.Operation == BattleRuleOperation.Draw)
            {
                int drawValue = ResolveValue(step.ValueSource, step.ValueParam, plan.Card, context);
                bool drawn = context.DrawCards(drawValue);
                result.Outcomes.Add(new BattleRuleStepOutcome
                {
                    Operation = step.Operation,
                    Kind = drawn ? BattleRuleStepOutcomeKind.Applied : BattleRuleStepOutcomeKind.NotApplicable,
                    Value = drawValue,
                    Target = "共享牌库",
                    Note = drawn ? string.Empty : "抽牌未产生任何牌"
                });
                continue;
            }

            if (boundStep.Targets == null || boundStep.Targets.Count == 0)
            {
                result.Outcomes.Add(new BattleRuleStepOutcome
                {
                    Operation = step.Operation,
                    Kind = BattleRuleStepOutcomeKind.NotApplicable,
                    Target = "无合法目标",
                    Note = "本步骤无合法目标（不影响整张牌结算）"
                });
                continue;
            }

            foreach (var target in boundStep.Targets)
            {
                int value = ResolveValue(step.ValueSource, step.ValueParam, plan.Card, context);
                bool bonus = step.BonusCondition != BattleRuleCondition.None
                             && IsConditionSatisfied(step.BonusCondition, step.BonusConditionParam, plan.Card, context, target);
                if (bonus) value += ResolveValue(step.BonusValueSource, step.BonusValueParam, plan.Card, context);

                var outcome = new BattleRuleStepOutcome
                {
                    TargetRef = target,
                    Operation = step.Operation,
                    Value = value,
                    Target = target.Describe(),
                    BonusApplied = bonus
                };

                var before = BattleRuleFactCapture.Read(context);
                ExecuteOnTarget(step, plan.Card, target, value, context, outcome);
                outcome.FactChanges = BattleRuleFactCapture.Changes(before, context);
                result.Outcomes.Add(outcome);
            }
        }

        return result;
    }

    private static void ExecuteOnTarget(BattleCardRuleStep step, BattleCard card, BattleRuleTargetRef target,
                                        int value, IBattleRuleContext context, BattleRuleStepOutcome outcome)
    {
        switch (step.Operation)
        {
            case BattleRuleOperation.Damage:
                outcome.Kind = context.ApplyDamage(target.UnitId, value)
                    ? BattleRuleStepOutcomeKind.Applied
                    : BattleRuleStepOutcomeKind.NotApplicable;
                if (outcome.Kind == BattleRuleStepOutcomeKind.NotApplicable) outcome.Note = "目标当前不可受伤";
                break;

            case BattleRuleOperation.Heal:
                outcome.Kind = context.ApplyHeal(target.UnitId, value)
                    ? BattleRuleStepOutcomeKind.Applied
                    : BattleRuleStepOutcomeKind.NotApplicable;
                if (outcome.Kind == BattleRuleStepOutcomeKind.NotApplicable) outcome.Note = "目标当前不可治疗";
                break;

            case BattleRuleOperation.Shield:
                outcome.Kind = context.ApplyShield(target.UnitId, value)
                    ? BattleRuleStepOutcomeKind.Applied
                    : BattleRuleStepOutcomeKind.NotApplicable;
                if (outcome.Kind == BattleRuleStepOutcomeKind.NotApplicable) outcome.Note = "目标当前不可获得护盾";
                break;

            case BattleRuleOperation.ModifyCost:
                if (target.Card == null)
                {
                    outcome.Kind = BattleRuleStepOutcomeKind.NotApplicable;
                    outcome.Note = "目标不是手牌";
                }
                else
                {
                    outcome.Kind = context.ApplyCostModifier(target.Card, value)
                        ? BattleRuleStepOutcomeKind.Applied
                        : BattleRuleStepOutcomeKind.NotApplicable;
                }
                break;

            case BattleRuleOperation.ModifyDamage:
                if (target.Card == null)
                {
                    outcome.Kind = BattleRuleStepOutcomeKind.NotApplicable;
                    outcome.Note = "目标不是手牌";
                }
                else if (!target.Card.HasDamage)
                {
                    // Gate 0 Development Spec §4.3：目标没有 Damage 时仍合法，Damage 修改自然不适用
                    outcome.Kind = BattleRuleStepOutcomeKind.NotApplicable;
                    outcome.Note = "目标牌没有 Damage（不适用）";
                }
                else
                {
                    outcome.Kind = context.ApplyDamageModifier(target.Card, value)
                        ? BattleRuleStepOutcomeKind.Applied
                        : BattleRuleStepOutcomeKind.NotApplicable;
                }
                break;

            case BattleRuleOperation.Move:
                outcome.Kind = context.TryMoveOwnerTo(card, target.Row, target.Column)
                    ? BattleRuleStepOutcomeKind.Applied
                    : BattleRuleStepOutcomeKind.Failed;
                if (outcome.Kind == BattleRuleStepOutcomeKind.Failed) outcome.Note = "移动非法（零写入）";
                break;

            case BattleRuleOperation.Swap:
                outcome.Kind = context.TrySwapOwnerWith(card, target.UnitId)
                    ? BattleRuleStepOutcomeKind.Applied
                    : BattleRuleStepOutcomeKind.Failed;
                if (outcome.Kind == BattleRuleStepOutcomeKind.Failed) outcome.Note = "交换非法（零写入）";
                break;

            default:
                outcome.Kind = BattleRuleStepOutcomeKind.NotApplicable;
                outcome.Note = "未实现的操作";
                break;
        }
    }

    /// <summary>数值来源解析（Fixed / CasterAttack）。</summary>
    public static int ResolveValue(BattleRuleValueSource source, int param, BattleCard card, IBattleRuleContext context)
    {
        return BattleRuleValueResolver.Resolve(source, param, card, context);
    }

    /// <summary>
    /// Bonus 条件判断（Predicate）。
    /// 目标相关条件按目标计算；全局条件（独狼 / 共享行动力归零）按当前 Battle Fact 计算。
    /// </summary>
    public static bool IsConditionSatisfied(BattleRuleCondition condition, int param, BattleCard card,
                                            IBattleRuleContext context, BattleRuleTargetRef target)
    {
        if (condition == BattleRuleCondition.None) return false;
        if (context == null) return false;

        switch (condition)
        {
            case BattleRuleCondition.TargetHpPercentAtOrBelow:
            {
                if (target.Kind != BattleRuleTargetKind.Unit) return false;
                BattleRuleUnitFacts unit;
                if (!context.TryGetUnit(target.UnitId, out unit) || unit == null) return false;
                if (unit.MaxHP <= 0) return false;
                return unit.CurrentHP * 100 <= unit.MaxHP * param;
            }

            case BattleRuleCondition.EnemyIntentIsDefend:
            {
                if (target.Kind != BattleRuleTargetKind.Unit) return false;
                BattleRuleUnitFacts unit;
                if (!context.TryGetUnit(target.UnitId, out unit) || unit == null) return false;
                return unit.IntentIsDefend;
            }

            case BattleRuleCondition.OwnerIsAloneInRow:
                return context.OwnerIsAloneInRow(card);

            case BattleRuleCondition.SharedActionPointsIsZero:
                return context.SharedActionPointsCurrent == 0;

            default:
                return false;
        }
    }
}
