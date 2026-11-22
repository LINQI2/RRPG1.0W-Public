using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Battle V0.1 Stage 5：敌人行动决策器（纯逻辑，不依赖 UnityEngine）
///
/// 决策规则（Milestone C 最小框架）：
///   1. 过滤：不满足 Condition 的规则不可选；不可用规则（OncePerBattle 已用等）不可选；
///   2. 优先级：取可选项中 Priority 最高的一组；
///   3. 权重：在该组内按 Weight 加权随机选出 Action。
///
/// 输出为锁定的 EnemyIntent（ActionType + TargetRule；不包含具体目标）。
/// 若无可选规则返回 null，由宿主回退到普通攻击。
/// </summary>
public static class EnemyActionPlanner
{
    /// <summary>
    /// 选择下一次行动。
    /// </summary>
    /// <param name="rules">该敌人的行为规则列表（内容层配置）</param>
    /// <param name="conditionMet">条件判定委托：某规则的条件当前是否满足</param>
    /// <param name="available">可用性委托：某规则当前是否可用（OncePerBattle 已用等）；可为 null</param>
    /// <param name="rng">随机源（可注入种子使测试确定化；null 时退化为首个）</param>
    public static EnemyIntent Pick(IReadOnlyList<EnemyActionRule> rules,
                                   Func<EnemyActionRule, bool> conditionMet,
                                   Func<EnemyActionRule, bool> available,
                                   System.Random rng)
    {
        if (rules == null || rules.Count == 0) return null;

        var eligible = new List<EnemyActionRule>();
        foreach (var rule in rules)
        {
            if (rule == null) continue;
            if (available != null && !available(rule)) continue;
            if (conditionMet != null && !conditionMet(rule)) continue;
            eligible.Add(rule);
        }
        if (eligible.Count == 0) return null;

        // 最高优先级组
        int maxPriority = eligible.Max(r => r.Priority);
        var top = eligible.Where(r => r.Priority == maxPriority).ToList();

        var selected = WeightedPick(top, rng);
        if (selected == null) return null;

        return new EnemyIntent
        {
            ActionType = selected.ActionType,
            TargetRule = selected.TargetRule,
            SourceRule = selected,
            DamageMultiplier = Math.Max(1, selected.SpecialDamageMultiplier)
        };
    }

    private static EnemyActionRule WeightedPick(List<EnemyActionRule> rules, System.Random rng)
    {
        int total = rules.Sum(r => Math.Max(1, r.Weight));
        int roll = rng != null ? rng.Next(total) : 0;

        foreach (var rule in rules)
        {
            roll -= Math.Max(1, rule.Weight);
            if (roll < 0) return rule;
        }
        return rules[rules.Count - 1];
    }
}
