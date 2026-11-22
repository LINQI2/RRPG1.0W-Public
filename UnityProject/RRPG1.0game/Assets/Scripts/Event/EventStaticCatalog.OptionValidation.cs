using System;
using System.Collections.Generic;
using System.Globalization;
using RRPG.StaticConfig;

namespace RRPG.Event
{
    /// <summary>
    /// EventStaticCatalog：Option / Outcome / Effect 表的自洽性校验（WP-Event-1C）。
    ///
    /// 与 1B 的校验同一原则：运行时可能拿到**非编译器产生的**投影，
    /// 因此「未知枚举 / 悬空引用 / 重复顺序 / Terminal Effect 形状」必须显式失败
    /// （<see cref="EventResolutionCode.InvalidRuntimeConfig"/>），绝不静默继续。
    ///
    /// 变异：1A 的 Compiler 已在编译期保证这些；这里是运行时的**第二道防线**（§21）。
    /// </summary>
    public sealed partial class EventStaticCatalog
    {
        private void ValidateOptionsTables()
        {
            foreach (var option in _options)
            {
                if (option == null) continue;

                if (option.EventId == null || !_eventById.ContainsKey(option.EventId))
                {
                    _errors.Add("选项 " + option.OptionId + " 引用了不存在的事件：" + (option.EventId ?? "<null>") + "。");
                }

                if (!IsKnownUnavailableMode(option.UnavailableMode))
                {
                    _errors.Add("选项 " + option.OptionId + " 的 UnavailableMode 未知：'"
                        + option.UnavailableMode + "'（允许：Hide / Disable）。");
                }
            }

            foreach (var pair in _optionConditionsByOption)
            {
                if (pair.Key == null || !_optionById.ContainsKey(pair.Key))
                {
                    _errors.Add("选项条件引用了不存在的 OptionId：" + (pair.Key ?? "<null>") + "。");
                }

                foreach (var condition in pair.Value)
                {
                    if (!EventConditionTypes.IsKnown(condition.ConditionType))
                    {
                        _errors.Add("选项条件 " + condition.ConditionId + " 的 ConditionType 未知：'"
                            + condition.ConditionType + "'（允许：" + EventConditionTypes.KnownListText() + "）。");
                    }
                }
            }

            foreach (var pair in _outcomesByOption)
            {
                if (pair.Key == null || !_optionById.ContainsKey(pair.Key))
                {
                    _errors.Add("结果引用了不存在的 OptionId：" + (pair.Key ?? "<null>") + "。");
                }

                foreach (var outcome in pair.Value)
                {
                    if (outcome.Weight <= 0)
                    {
                        _errors.Add("结果 " + outcome.OutcomeId + " 的 Weight 必须 > 0（当前 "
                            + outcome.Weight.ToString(CultureInfo.InvariantCulture) + "）。");
                    }
                }
            }

            foreach (var pair in _effectsByOutcome)
            {
                if (pair.Key == null || !_outcomeById.ContainsKey(pair.Key))
                {
                    _errors.Add("效果引用了不存在的 OutcomeId：" + (pair.Key ?? "<null>") + "。");
                }

                ValidateEffectSequence(pair.Key, pair.Value);
            }
        }

        /// <summary>
        /// 同一 Outcome 内的效果序列：未知类型 / Order 重复 / StartBattle 必须是最后一个且最多一个。
        /// 列表已按 Order 规范排序，因此「最后一个」= 列表末尾。
        /// </summary>
        private void ValidateEffectSequence(string outcomeId, List<EventEffectDefinition> effects)
        {
            if (effects == null) return;

            var seenOrders = new HashSet<int>();
            int terminalCount = 0;

            for (int i = 0; i < effects.Count; i++)
            {
                var effect = effects[i];
                if (effect == null) continue;

                if (!EventEffectTypes.IsKnown(effect.EffectType))
                {
                    _errors.Add("效果 " + effect.EffectId + " 的 EffectType 未知：'" + effect.EffectType
                        + "'（允许：" + EventEffectTypes.KnownListText() + "）。");
                    continue;
                }

                if (!seenOrders.Add(effect.Order))
                {
                    _errors.Add("结果 " + outcomeId + " 内效果 Order 重复：" + effect.Order
                        .ToString(CultureInfo.InvariantCulture) + "（效果 " + effect.EffectId + "）。");
                }

                if (!EventEffectTypes.IsTerminal(effect.EffectType)) continue;

                terminalCount++;
                if (terminalCount > 1)
                {
                    _errors.Add("结果 " + outcomeId + " 内出现多个 StartBattle（效果 " + effect.EffectId
                        + "）：Terminal Effect 每个结果最多一个。");
                }

                if (i != effects.Count - 1)
                {
                    _errors.Add("结果 " + outcomeId + " 的 StartBattle（效果 " + effect.EffectId
                        + "）必须是最后一个效果（Order 最大）：它一旦开始，后续效果不可能再属于同一次结算。");
                }
            }
        }

        private static bool IsKnownUnavailableMode(string mode)
        {
            return string.Equals(mode, "Hide", StringComparison.Ordinal)
                || string.Equals(mode, "Disable", StringComparison.Ordinal);
        }
    }
}
