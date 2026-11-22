using System;
using System.Globalization;

namespace RRPG.Event
{
    /// <summary>EventOptionExecutor：单个效果的 Preflight（数值型走 projected state，其余问 Gateway）。</summary>
    public static partial class EventOptionExecutor
    {
        private static ExecutionStep PreflightOne(ExecutionContext ctx, EventEffectPlanEntry entry,
            ProjectedState projected, out string detail)
        {
            detail = string.Empty;

            // Flag 由 Event Core 自己拥有且幂等 → 预计执行恒通过（真实 mutation 仍在 Commit）
            if (EventEffectTypes.IsEventCoreOwned(entry.EffectType)) return ExecutionStep.Ok;

            // ---------------- VillageResourceDelta ----------------
            if (string.Equals(entry.EffectType, EventEffectTypes.VillageResourceDelta, StringComparison.Ordinal))
            {
                int current;
                if (!projected.VillageResources.TryGetValue(entry.TargetKey, out current))
                {
                    if (!ctx.Gateway.TryGetVillageResource(entry.TargetKey, out current))
                    {
                        detail = "Gateway 无法读取村庄资源 '" + entry.TargetKey
                            + "'：契约违规（TryGetVillageResource 必须对该键返回 true）——"
                            + "没有可靠起点就无法做预计执行。";
                        return ExecutionStep.InvalidConfig;
                    }
                }

                return ProjectNumeric(entry, current, entry.IntValue, entry.TargetKey, target =>
                {
                    projected.VillageResources[entry.TargetKey] = target;
                });
            }

            // ---------------- SupplyDelta ----------------
            if (string.Equals(entry.EffectType, EventEffectTypes.SupplyDelta, StringComparison.Ordinal))
            {
                if (!projected.ExpeditionSupplyRead)
                {
                    if (!ctx.Gateway.TryGetExpeditionSupply(out projected.ExpeditionSupply))
                    {
                        detail = "Gateway 无法读取远征补给：契约违规（TryGetExpeditionSupply 必须返回 true）。";
                        return ExecutionStep.InvalidConfig;
                    }
                    projected.ExpeditionSupplyRead = true;
                }

                return ProjectNumeric(entry, projected.ExpeditionSupply, entry.IntValue, "Supplies", target =>
                {
                    projected.ExpeditionSupply = target;
                });
            }

            // ---------------- 其余外部效果：只问 Gateway「现在能不能执行」 ----------------
            bool can;
            string reason;

            if (string.Equals(entry.EffectType, EventEffectTypes.GrantPersonRest, StringComparison.Ordinal))
            {
                can = ctx.Gateway.CanGrantRest(entry.TargetPersonId, out reason);
            }
            else if (string.Equals(entry.EffectType, EventEffectTypes.AdvanceExpeditionTime,
                         StringComparison.Ordinal))
            {
                can = ctx.Gateway.CanAdvanceExpeditionTime(entry.IntValue, out reason);
            }
            else if (string.Equals(entry.EffectType, EventEffectTypes.StartBattle, StringComparison.Ordinal))
            {
                can = ctx.Gateway.CanStartBattle(entry.TargetKey, out reason);
            }
            else
            {
                detail = "未知 EffectType（计划构建本应拦截）：'" + entry.EffectType + "'。";
                return ExecutionStep.InvalidConfig;
            }

            if (can) return ExecutionStep.Ok;

            MarkPreflightFailed(entry,
                string.IsNullOrEmpty(reason) ? "Gateway 返回不可执行（未给出原因）。" : reason);
            return ExecutionStep.Failed;
        }

        /// <summary>数值投影：计算 projected after 并在变负时判定失败；否则更新投影并返回 Ok。</summary>
        private static ExecutionStep ProjectNumeric(EventEffectPlanEntry entry, int current, int delta, string label,
            Action<int> update)
        {
            int after = current + delta;
            entry.HasProjection = true;
            entry.ProjectedBefore = current;
            entry.ProjectedAfter = after;

            if (after < 0)
            {
                MarkPreflightFailed(entry, label + " 预计变为 " + after.ToString(CultureInfo.InvariantCulture)
                    + "（不允许 < 0；expected >= 0 actual projected = "
                    + after.ToString(CultureInfo.InvariantCulture) + "）：当前 "
                    + current.ToString(CultureInfo.InvariantCulture) + "，本效果 " + Signed(delta) + "。");
                return ExecutionStep.Failed;
            }

            update(after);
            return ExecutionStep.Ok;
        }
    }
}
