using System;
using System.Globalization;
using RRPG.StaticConfig;

namespace RRPG.Event
{
    /// <summary>EventResolver：RepeatPolicy / Cooldown 预筛选（WP-Event-1B §15 / §16）。</summary>
    public static partial class EventResolver
    {
        /// <summary>RepeatPolicy（§15）：Repeatable 直接过；Once* 查对应历史。</summary>
        private static bool RepeatAllows(ResolveContext ctx, EventDefinition eventRecord, out string reason)
        {
            reason = string.Empty;
            string eventId = eventRecord.EventId;

            if (string.Equals(eventRecord.RepeatPolicy, EventRepeatPolicies.OncePerWorld, StringComparison.Ordinal))
            {
                int count = ctx.WorldState.History.OccurrenceCount(eventId);
                if (count > 0)
                {
                    reason = "OncePerWorld：世界长期历史已发生 "
                        + count.ToString(CultureInfo.InvariantCulture) + " 次。";
                    return false;
                }
                return true;
            }

            if (string.Equals(eventRecord.RepeatPolicy, EventRepeatPolicies.OncePerExpedition,
                    StringComparison.Ordinal))
            {
                int count = ctx.SessionState.OccurrenceCount(eventId);
                if (count > 0)
                {
                    reason = "OncePerExpedition：本次远征历史已发生 "
                        + count.ToString(CultureInfo.InvariantCulture) + " 次。";
                    return false;
                }
                return true;
            }

            return true;   // Repeatable（Catalog 已保证取值合法）
        }

        /// <summary>
        /// Cooldown（§16）：只用「序数差」判定，绝不用现实时间 / Unity Time。
        /// 边界冻结为 `currentOrdinal - lastOrdinal &gt;= CooldownValue` 才 Ready。
        /// 从未发生过 → 不构成冷却限制（Ready）。
        /// </summary>
        private static bool CooldownAllows(ResolveContext ctx, EventDefinition eventRecord, out string reason)
        {
            reason = string.Empty;

            if (string.Equals(eventRecord.CooldownType, EventCooldownTypes.None, StringComparison.Ordinal))
            {
                return true;
            }

            int required = eventRecord.CooldownValue;

            if (string.Equals(eventRecord.CooldownType, EventCooldownTypes.VillageDays, StringComparison.Ordinal))
            {
                int last;
                if (!ctx.WorldState.History.TryGetLastVillageDayOrdinal(eventRecord.EventId, out last)) return true;

                int current = ctx.World.VillageDayOrdinal;
                int delta = current - last;
                if (delta < required)
                {
                    reason = "VillageDays 冷却未满足：" + delta.ToString(CultureInfo.InvariantCulture)
                        + "/" + required.ToString(CultureInfo.InvariantCulture)
                        + " 天（当前村庄日序 " + current.ToString(CultureInfo.InvariantCulture)
                        + "，最近发生 " + last.ToString(CultureInfo.InvariantCulture) + "）。";
                    return false;
                }
                return true;
            }

            if (string.Equals(eventRecord.CooldownType, EventCooldownTypes.ExpeditionNodes,
                    StringComparison.Ordinal))
            {
                int last;
                if (!ctx.SessionState.TryGetLastNodeOrdinal(eventRecord.EventId, out last)) return true;

                int current = ctx.World.ExpeditionNodeOrdinal;
                int delta = current - last;
                if (delta < required)
                {
                    reason = "ExpeditionNodes 冷却未满足：" + delta.ToString(CultureInfo.InvariantCulture)
                        + "/" + required.ToString(CultureInfo.InvariantCulture)
                        + " 个节点（当前节点序 " + current.ToString(CultureInfo.InvariantCulture)
                        + "，最近发生 " + last.ToString(CultureInfo.InvariantCulture) + "）。";
                    return false;
                }
                return true;
            }

            return true;   // Catalog 已保证 CooldownType 合法
        }
    }
}
