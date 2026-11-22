using System;
using System.Collections.Generic;
using RRPG.StaticConfig;

namespace RRPG.Event
{
    /// <summary>
    /// EventStaticCatalog：**Option / Outcome / Effect 索引**（WP-Event-1C 新增）。
    ///
    /// 为什么放在同一个 Catalog：1C 的 Option 求值 → Outcome 选择 → Effect 计划都要求
    /// 「同一个静态配置快照」，分开索引会让 Runtime 读到「一半新一半旧」的跨表不一致。
    ///
    /// 规范排序（确定性前提）：
    ///   - Options 按 (SortOrder, OptionId)；
    ///   - OptionConditions 按 ConditionId；
    ///   - Outcomes 按 OutcomeId；
    ///   - Effects 按 Order。
    /// </summary>
    public sealed partial class EventStaticCatalog
    {
        private readonly List<EventOptionDefinition> _options = new List<EventOptionDefinition>();
        private readonly Dictionary<string, EventOptionDefinition> _optionById =
            new Dictionary<string, EventOptionDefinition>(StringComparer.Ordinal);
        private readonly Dictionary<string, List<EventOptionDefinition>> _optionsByEvent =
            new Dictionary<string, List<EventOptionDefinition>>(StringComparer.Ordinal);
        private readonly Dictionary<string, List<EventOptionConditionDefinition>> _optionConditionsByOption =
            new Dictionary<string, List<EventOptionConditionDefinition>>(StringComparer.Ordinal);
        private readonly List<EventOutcomeDefinition> _outcomes = new List<EventOutcomeDefinition>();
        private readonly Dictionary<string, EventOutcomeDefinition> _outcomeById =
            new Dictionary<string, EventOutcomeDefinition>(StringComparer.Ordinal);
        private readonly Dictionary<string, List<EventOutcomeDefinition>> _outcomesByOption =
            new Dictionary<string, List<EventOutcomeDefinition>>(StringComparer.Ordinal);
        private readonly Dictionary<string, List<EventEffectDefinition>> _effectsByOutcome =
            new Dictionary<string, List<EventEffectDefinition>>(StringComparer.Ordinal);

        /// <summary>全部结果（OutcomeId 规范排序）。</summary>
        public IReadOnlyList<EventOutcomeDefinition> Outcomes { get { return _outcomes; } }

        public bool TryGetOutcome(string outcomeId, out EventOutcomeDefinition definition)
        {
            definition = null;
            if (string.IsNullOrEmpty(outcomeId)) return false;
            return _outcomeById.TryGetValue(outcomeId, out definition);
        }

        /// <summary>全部选项（按 (SortOrder, OptionId) 规范排序）。</summary>
        public IReadOnlyList<EventOptionDefinition> Options { get { return _options; } }

        public bool TryGetOption(string optionId, out EventOptionDefinition definition)
        {
            definition = null;
            if (string.IsNullOrEmpty(optionId)) return false;
            return _optionById.TryGetValue(optionId, out definition);
        }

        /// <summary>某事件的选项（(SortOrder, OptionId) 升序）—— 用于 UI 顺序与「第一个可用选项」等判定。</summary>
        public IReadOnlyList<EventOptionDefinition> GetOptions(string eventId)
        {
            return Get(_optionsByEvent, eventId);
        }

        /// <summary>某选项的可用条件（ConditionId 升序）。</summary>
        public IReadOnlyList<EventOptionConditionDefinition> GetOptionConditions(string optionId)
        {
            return Get(_optionConditionsByOption, optionId);
        }

        /// <summary>某选项的结果（OutcomeId 升序；抽取时由 <see cref="EventOutcomeSelector"/> 再按权重排列）。</summary>
        public IReadOnlyList<EventOutcomeDefinition> GetOutcomes(string optionId)
        {
            return Get(_outcomesByOption, optionId);
        }

        /// <summary>某结果的 Effects（Order 升序）—— 执行顺序即配置顺序。</summary>
        public IReadOnlyList<EventEffectDefinition> GetEffects(string outcomeId)
        {
            return Get(_effectsByOutcome, outcomeId);
        }
    }
}
