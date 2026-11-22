using System;
using System.Collections.Generic;
using RRPG.StaticConfig;

namespace RRPG.Event
{
    /// <summary>EventStaticCatalog：Option / Outcome / Effect 索引构建（WP-Event-1C）。</summary>
    public sealed partial class EventStaticCatalog
    {
        private void BuildOptions(IReadOnlyList<EventOptionDefinition> options)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);

            if (options != null)
            {
                foreach (var record in options)
                {
                    if (record == null) continue;

                    string optionId = record.OptionId ?? string.Empty;
                    if (optionId.Length == 0)
                    {
                        _errors.Add("EventOption 行缺少 OptionId。");
                        continue;
                    }
                    if (!seen.Add(optionId))
                    {
                        _errors.Add("选项身份重复：OptionId = " + optionId + "。");
                        continue;
                    }

                    _options.Add(record);
                    _optionById.Add(optionId, record);
                    Add(_optionsByEvent, record.EventId, record);
                }
            }

            SortAll(_optionsByEvent, (a, b) =>
            {
                int byOrder = a.SortOrder.CompareTo(b.SortOrder);
                return byOrder != 0
                    ? byOrder
                    : string.CompareOrdinal(a.OptionId ?? string.Empty, b.OptionId ?? string.Empty);
            });

            _options.Sort((a, b) =>
            {
                int byOrder = a.SortOrder.CompareTo(b.SortOrder);
                return byOrder != 0
                    ? byOrder
                    : string.CompareOrdinal(a.OptionId ?? string.Empty, b.OptionId ?? string.Empty);
            });
        }

        private void BuildOptionConditions(IReadOnlyList<EventOptionConditionDefinition> conditions)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);

            if (conditions != null)
            {
                foreach (var record in conditions)
                {
                    if (record == null) continue;

                    string conditionId = record.ConditionId ?? string.Empty;
                    if (conditionId.Length == 0)
                    {
                        _errors.Add("EventOptionCondition 行缺少 ConditionId。");
                        continue;
                    }
                    if (!seen.Add(conditionId))
                    {
                        _errors.Add("选项条件身份重复：ConditionId = " + conditionId + "。");
                        continue;
                    }

                    Add(_optionConditionsByOption, record.OptionId, record);
                }
            }

            SortAll(_optionConditionsByOption, (a, b) => string.CompareOrdinal(a.ConditionId, b.ConditionId));
        }

        private void BuildOutcomes(IReadOnlyList<EventOutcomeDefinition> outcomes)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);

            if (outcomes != null)
            {
                foreach (var record in outcomes)
                {
                    if (record == null) continue;

                    string outcomeId = record.OutcomeId ?? string.Empty;
                    if (outcomeId.Length == 0)
                    {
                        _errors.Add("EventOutcome 行缺少 OutcomeId。");
                        continue;
                    }
                    if (!seen.Add(outcomeId))
                    {
                        _errors.Add("结果身份重复：OutcomeId = " + outcomeId + "。");
                        continue;
                    }

                    _outcomes.Add(record);
                    _outcomeById.Add(outcomeId, record);
                    Add(_outcomesByOption, record.OptionId, record);
                }
            }

            _outcomes.Sort((a, b) => string.CompareOrdinal(a.OutcomeId, b.OutcomeId));
            SortAll(_outcomesByOption, (a, b) => string.CompareOrdinal(a.OutcomeId, b.OutcomeId));
        }

        private void BuildEffects(IReadOnlyList<EventEffectDefinition> effects)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);

            if (effects != null)
            {
                foreach (var record in effects)
                {
                    if (record == null) continue;

                    string effectId = record.EffectId ?? string.Empty;
                    if (effectId.Length == 0)
                    {
                        _errors.Add("EventEffect 行缺少 EffectId。");
                        continue;
                    }
                    if (!seen.Add(effectId))
                    {
                        _errors.Add("效果身份重复：EffectId = " + effectId + "。");
                        continue;
                    }

                    Add(_effectsByOutcome, record.OutcomeId, record);
                }
            }

            SortAll(_effectsByOutcome, (a, b) => a.Order.CompareTo(b.Order));
        }
    }
}
