using System;
using System.Collections.Generic;
using System.Globalization;
using RRPG.Deterministic;
using RRPG.StaticConfig;

namespace RRPG.Event
{
    /// <summary>
    /// **Outcome 确定性选择**（WP-Event-1C §6 / §24）。
    ///
    /// 核心规则（冻结）：
    ///   - 随机来源**只**派生自 `EventInstanceId + OptionId`（+ 固定 domain salt）；
    ///   - 因此：同一个 EventInstance 的同一个 Option，无论 UI 重开 / 重试 / Preview 多少次，
    ///     永远得到**同一个** Outcome（玩家不能靠反复开关事件卡重抽结果）；
    ///   - 不同 EventInstance 可以得到不同（但都合法）的 Outcome；
    ///   - 不使用 `UnityEngine.Random` / 时间 / Guid；候选先按 OutcomeId 规范排序再入随机。
    ///
    /// 本类**纯函数**：Preview 与 Execute 走同一条选择逻辑，绝不产生副作用（§7）。
    /// </summary>
    public static class EventOutcomeSelector
    {
        /// <summary>固定 domain salt：让 Outcome 序列与其它用途的随机流分开（不是可调参数）。</summary>
        public const string DomainSalt = "event-outcome";

        /// <summary>由 (EventInstanceId, OptionId) 稳定派生的随机种子。</summary>
        public static int SeedFor(string eventInstanceId, string optionId)
        {
            ulong hash = DeterministicRandom.StableHash64(
                DomainSalt + "\u001f" + (eventInstanceId ?? string.Empty) + "\u001f" + (optionId ?? string.Empty));
            return (int)(hash & 0x7FFFFFFFUL);
        }

        /// <summary>
        /// 在给定 Outcome 集合中确定性选中一个（按 Weight 无放回加权排列后的第一个）。
        /// 调用方必须传入**该 Option 的全部 Outcome**（本方法自己按 OutcomeId 规范排序）。
        /// </summary>
        public static EventOutcomeDefinition Select(string eventInstanceId, string optionId,
            IReadOnlyList<EventOutcomeDefinition> outcomes)
        {
            if (outcomes == null || outcomes.Count == 0) return null;

            var ordered = new List<EventOutcomeDefinition>();
            for (int i = 0; i < outcomes.Count; i++)
            {
                if (outcomes[i] != null) ordered.Add(outcomes[i]);
            }
            if (ordered.Count == 0) return null;

            ordered.Sort((a, b) => string.CompareOrdinal(a.OutcomeId ?? string.Empty, b.OutcomeId ?? string.Empty));

            var weights = new List<int>(ordered.Count);
            for (int i = 0; i < ordered.Count; i++)
            {
                weights.Add(ordered[i].Weight);
            }

            var rng = new DeterministicRandom(SeedFor(eventInstanceId, optionId));
            var permutation = rng.WeightedPermutation(ordered, weights);
            return permutation.Count == 0 ? null : permutation[0];
        }

        /// <summary>诊断用：选择依据文本（种子 + 权重表）。</summary>
        public static string DescribeSelection(string eventInstanceId, string optionId,
            IReadOnlyList<EventOutcomeDefinition> outcomes)
        {
            var selected = Select(eventInstanceId, optionId, outcomes);
            if (selected == null) return "(no outcome)";

            return selected.OutcomeId + " (weight " + selected.Weight.ToString(CultureInfo.InvariantCulture)
                + ", seed " + SeedFor(eventInstanceId, optionId).ToString(CultureInfo.InvariantCulture) + ")";
        }
    }
}
