using System;
using System.Globalization;

namespace RRPG.Event
{
    /// <summary>条件解释器（数值段）：Village Resource 与 Expedition 状态阈值。</summary>
    public static partial class EventConditionEvaluator
    {
        internal static EventConditionOutcome EvaluateNumeric(EventConditionView condition,
            EventConditionEvaluation context, out string detail)
        {
            detail = string.Empty;
            string type = condition.ConditionType;

            // ---------------- Village Resource（只允许 Ingredients / Food） ----------------
            if (string.Equals(type, EventConditionTypes.VillageResourceAtLeast, StringComparison.Ordinal)
                || string.Equals(type, EventConditionTypes.VillageResourceAtMost, StringComparison.Ordinal))
            {
                if (string.IsNullOrEmpty(condition.TargetKey) || !condition.HasIntValue)
                {
                    detail = "缺少 TargetKey 或 IntValue。";
                    return EventConditionOutcome.InvalidConfig;
                }

                // WP-Event-1D Gate D1 §2（协调层裁决）：**World View 不可用时条件必须 Fail Closed**。
                // 不得把 `World == null` 解释成 `0` 参与比较 —— 那会让 `VillageResourceAtMost 100`
                // 在「读不到世界」时假成立（损坏配置 Fail Open）。
                if (context.World == null)
                {
                    detail = "IEventWorldView 不可用，无法求值 " + type
                        + "：村庄资源数值无从读取，条件视为**不成立**（Fail Closed，不按 0 猜测）。";
                    return EventConditionOutcome.Fail;
                }

                int actual;
                if (string.Equals(condition.TargetKey, EventResourceKeys.Ingredients, StringComparison.Ordinal))
                {
                    actual = context.World.Ingredients;
                }
                else if (string.Equals(condition.TargetKey, EventResourceKeys.Food, StringComparison.Ordinal))
                {
                    actual = context.World.Food;
                }
                else
                {
                    detail = "TargetKey 不是合法村庄资源：'" + condition.TargetKey + "'（只允许 "
                        + EventResourceKeys.Ingredients + " / " + EventResourceKeys.Food + "）。";
                    return EventConditionOutcome.InvalidConfig;
                }

                // WP-Event-1D Gate D2（与 Gate D1 §21.8 同一裁决的**对称**落地）：不可用值必须 Fail Closed。
                // 产品 Adapter 可能对「本域不拥有」的村庄资源返回负数哨兵（远征 WorldView 的
                // UnknownVillageResourceValue = -1）；若让它直接参与比较，`VillageResourceAtMost N`
                // 会得到 `-1 <= N = true` —— 那是**损坏配置 Fail Open**。合法村庄资源恒 >= 0。
                if (actual < 0)
                {
                    detail = "村庄资源 " + condition.TargetKey + " 在 World View 中不可用（actual="
                        + actual.ToString(CultureInfo.InvariantCulture)
                        + " < 0：该域不拥有此数据）：条件视为**不成立**（Fail Closed，绝不当作正常值比较）。";
                    return EventConditionOutcome.Fail;
                }

                bool atLeast = string.Equals(type, EventConditionTypes.VillageResourceAtLeast,
                    StringComparison.Ordinal);
                bool pass = atLeast ? actual >= condition.IntValue : actual <= condition.IntValue;

                detail = condition.TargetKey + " " + (atLeast ? "expected>=" : "expected<=")
                    + condition.IntValue.ToString(CultureInfo.InvariantCulture)
                    + " actual=" + actual.ToString(CultureInfo.InvariantCulture);
                return pass ? EventConditionOutcome.Pass : EventConditionOutcome.Fail;
            }

            // ---------------- Expedition ----------------
            if (IsExpeditionIntCondition(type))
            {
                if (!condition.HasIntValue)
                {
                    detail = "缺少 IntValue。";
                    return EventConditionOutcome.InvalidConfig;
                }

                // WP-Event-1D Gate D1 §2（协调层裁决）：**World View 不可用时条件必须 Fail Closed**。
                // 不得把 `World == null` 解释成 `0` 参与比较 —— 那会让 `ExpeditionDepthAtMost 100`
                // 之类条件在「读不到世界」时假成立（损坏配置 Fail Open）。
                if (context.World == null)
                {
                    detail = "IEventWorldView 不可用，无法求值 " + type
                        + "：远征数值无从读取，条件视为**不成立**（Fail Closed，不按 0 猜测）。";
                    return EventConditionOutcome.Fail;
                }

                int actual;
                bool atLeast;
                string label;

                if (string.Equals(type, EventConditionTypes.ExpeditionDepthAtLeast, StringComparison.Ordinal)
                    || string.Equals(type, EventConditionTypes.ExpeditionDepthAtMost, StringComparison.Ordinal))
                {
                    actual = context.World.ExpeditionDepth;
                    atLeast = string.Equals(type, EventConditionTypes.ExpeditionDepthAtLeast, StringComparison.Ordinal);
                    label = "ExpeditionDepth";
                }
                else if (string.Equals(type, EventConditionTypes.SupplyAtLeast, StringComparison.Ordinal)
                         || string.Equals(type, EventConditionTypes.SupplyAtMost, StringComparison.Ordinal))
                {
                    actual = context.World.ExpeditionSupply;
                    atLeast = string.Equals(type, EventConditionTypes.SupplyAtLeast, StringComparison.Ordinal);
                    label = "Supply";
                }
                else
                {
                    actual = context.World.ExpeditionPartySize;
                    atLeast = string.Equals(type, EventConditionTypes.PartySizeAtLeast, StringComparison.Ordinal);
                    label = "PartySize";
                }

                // WP-Event-1D Gate D1 §2：**不可用值必须 Fail Closed**。
                // 产品 Adapter 可能对「本域不拥有」的数值返回负数哨兵（例如 Village WorldView 的
                // UnknownExpeditionValue = -1）。若让它直接参与比较，`ExpeditionDepthAtMost 3` 会得到
                // `-1 <= 3 = true` —— 那是**损坏配置 Fail Open**（事件在不该出现的地方出现）。
                // 合法数值域恒为 >= 0（Depth / Supply / PartySize / NodeOrdinal），故在**唯一**解释器里集中判定；
                // 各 Adapter 不各自发明比较语义。未来新增 WorldView 数值字段同走此处。
                if (actual < 0)
                {
                    detail = label + " 在 World View 中不可用（actual="
                        + actual.ToString(CultureInfo.InvariantCulture)
                        + " < 0：该域不拥有此数据）：条件视为**不成立**（Fail Closed，绝不当作正常值比较）。";
                    return EventConditionOutcome.Fail;
                }

                bool pass = atLeast ? actual >= condition.IntValue : actual <= condition.IntValue;
                detail = label + " " + (atLeast ? "expected>=" : "expected<=")
                    + condition.IntValue.ToString(CultureInfo.InvariantCulture)
                    + " actual=" + actual.ToString(CultureInfo.InvariantCulture);
                return pass ? EventConditionOutcome.Pass : EventConditionOutcome.Fail;
            }

            detail = "未知 ConditionType：'" + type + "'（V0.1 允许："
                + EventConditionTypes.KnownListText() + "）。";
            return EventConditionOutcome.InvalidConfig;
        }

        private static bool IsExpeditionIntCondition(string type)
        {
            return string.Equals(type, EventConditionTypes.ExpeditionDepthAtLeast, StringComparison.Ordinal)
                || string.Equals(type, EventConditionTypes.ExpeditionDepthAtMost, StringComparison.Ordinal)
                || string.Equals(type, EventConditionTypes.SupplyAtLeast, StringComparison.Ordinal)
                || string.Equals(type, EventConditionTypes.SupplyAtMost, StringComparison.Ordinal)
                || string.Equals(type, EventConditionTypes.PartySizeAtLeast, StringComparison.Ordinal)
                || string.Equals(type, EventConditionTypes.PartySizeAtMost, StringComparison.Ordinal);
        }
    }
}
