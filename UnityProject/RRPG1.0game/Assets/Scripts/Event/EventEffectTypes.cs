using System;

namespace RRPG.Event
{
    /// <summary>
    /// Event Effect 词表（WP-Event-1A / 1C 冻结）。
    ///
    /// 三类效果（分工是硬边界）：
    ///   - **Event Core 自己拥有**：`SetFlag` / `ClearFlag`（世界事件标记，属 Event World State）；
    ///   - **外部产品域效果**：`VillageResourceDelta` / `GrantPersonRest` / `SupplyDelta` /
    ///     `AdvanceExpeditionTime` —— 它们改的是别的领域 Authority，只能经
    ///     <see cref="IEventEffectGateway"/> 表达（1C 不直接依赖产品 Authority，见 §9）；
    ///   - **Terminal Effect**：`StartBattle`（1C 只表达「能不能开始 / 请开始」，不打开 Battle UI、
    ///     不创建 ActiveEncounter、不 Enter Node —— 真生命周期属 1D）。
    ///
    /// `QueueEvent` **不做**（1A 起就未纳入 V0.1 词表）。
    /// </summary>
    public static class EventEffectTypes
    {
        public const string SetFlag = "SetFlag";
        public const string ClearFlag = "ClearFlag";
        public const string VillageResourceDelta = "VillageResourceDelta";
        public const string GrantPersonRest = "GrantPersonRest";
        public const string SupplyDelta = "SupplyDelta";
        public const string AdvanceExpeditionTime = "AdvanceExpeditionTime";
        public const string StartBattle = "StartBattle";

        public static readonly string[] All =
        {
            SetFlag, ClearFlag, VillageResourceDelta, GrantPersonRest, SupplyDelta, AdvanceExpeditionTime,
            StartBattle
        };

        public static bool IsKnown(string effectType)
        {
            if (string.IsNullOrEmpty(effectType)) return false;
            for (int i = 0; i < All.Length; i++)
            {
                if (string.Equals(All[i], effectType, StringComparison.Ordinal)) return true;
            }
            return false;
        }

        /// <summary>是否由 Event Core 自己拥有其状态（Flag 类；其 mutation 仍发生在 Commit 阶段）。</summary>
        public static bool IsEventCoreOwned(string effectType)
        {
            return string.Equals(effectType, SetFlag, StringComparison.Ordinal)
                || string.Equals(effectType, ClearFlag, StringComparison.Ordinal);
        }

        /// <summary>是否 Terminal Effect（唯一：StartBattle；必须是 Outcome 的最后一个效果）。</summary>
        public static bool IsTerminal(string effectType)
        {
            return string.Equals(effectType, StartBattle, StringComparison.Ordinal);
        }

        /// <summary>是否是数值型增量效果（VillageResourceDelta / SupplyDelta）。</summary>
        public static bool IsNumericDelta(string effectType)
        {
            return string.Equals(effectType, VillageResourceDelta, StringComparison.Ordinal)
                || string.Equals(effectType, SupplyDelta, StringComparison.Ordinal);
        }

        public static string KnownListText()
        {
            return string.Join(" / ", All);
        }
    }
}
