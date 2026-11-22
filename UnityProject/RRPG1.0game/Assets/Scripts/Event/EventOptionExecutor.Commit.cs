using System;
using System.Globalization;

namespace RRPG.Event
{
    /// <summary>
    /// EventOptionExecutor：**Commit（唯一真实 mutation 点）** —— WP-Event-1C §10 / §11 / §12。
    ///
    /// 语义：
    ///   - 只在**整个序列的 Preflight 全部通过之后**调用；
    ///   - 顺序执行：Flag（Event Core 自己拥有）与外部效果都经各自的正式入口；
    ///   - 任一效果返回 false / 抛异常 → 立即停止，返回明确诊断
    ///     （**不**写 History、**不**标 Completed、**不**谎报成功）；
    ///   - 1C **不承诺**跨所有游戏系统的万能 rollback：产品 Gateway 必须在 1D 用能兑现
    ///     「Preflight 通过 ⇒ Commit 应成功」的 Authority 接口实现（§11）。
    /// </summary>
    public static partial class EventOptionExecutor
    {
        private static bool CommitAll(ExecutionContext ctx, EventEffectPlan plan, out int applied, out string error)
        {
            applied = 0;
            error = string.Empty;

            for (int i = 0; i < plan.Entries.Count; i++)
            {
                var entry = plan.Entries[i];

                string reason;
                bool ok;

                try
                {
                    ok = CommitOne(ctx, entry, out reason);
                }
                catch (Exception ex)
                {
                    ok = false;
                    reason = "Gateway 抛出异常（契约违规 / 程序错误）：" + ex.GetType().Name + "：" + ex.Message;
                }

                if (!ok)
                {
                    entry.Committed = false;
                    error = "效果 " + entry.Describe() + " 提交失败："
                        + (string.IsNullOrEmpty(reason) ? "Gateway 返回 false（未给出原因）。" : reason);
                    ctx.Trace.Line("Effect " + entry.Describe() + " : Commit FAILED（" + error + "）");
                    return false;
                }

                entry.Committed = true;
                applied++;
                ctx.Trace.Line("Effect " + entry.Describe() + " : Commit OK");
            }

            ctx.Trace.Line("Commit = Success（" + applied.ToString(CultureInfo.InvariantCulture) + " 条效果）");
            return true;
        }

        private static bool CommitOne(ExecutionContext ctx, EventEffectPlanEntry entry, out string reason)
        {
            reason = string.Empty;
            string type = entry.EffectType;

            // ---------------- Event Core 自己拥有：Flag（幂等） ----------------
            if (string.Equals(type, EventEffectTypes.SetFlag, StringComparison.Ordinal))
            {
                ctx.WorldState.SetFlag(entry.TargetKey);
                return true;
            }

            if (string.Equals(type, EventEffectTypes.ClearFlag, StringComparison.Ordinal))
            {
                ctx.WorldState.ClearFlag(entry.TargetKey);
                return true;
            }

            // ---------------- 外部产品域效果：经 Gateway ----------------
            if (string.Equals(type, EventEffectTypes.VillageResourceDelta, StringComparison.Ordinal))
            {
                return ctx.Gateway.ApplyVillageResourceDelta(entry.TargetKey, entry.IntValue, out reason);
            }

            if (string.Equals(type, EventEffectTypes.SupplyDelta, StringComparison.Ordinal))
            {
                return ctx.Gateway.ApplySupplyDelta(entry.IntValue, out reason);
            }

            if (string.Equals(type, EventEffectTypes.GrantPersonRest, StringComparison.Ordinal))
            {
                return ctx.Gateway.GrantRest(entry.TargetPersonId, out reason);
            }

            if (string.Equals(type, EventEffectTypes.AdvanceExpeditionTime, StringComparison.Ordinal))
            {
                return ctx.Gateway.AdvanceExpeditionTime(entry.IntValue, out reason);
            }

            if (string.Equals(type, EventEffectTypes.StartBattle, StringComparison.Ordinal))
            {
                return ctx.Gateway.StartBattle(entry.TargetKey, out reason);
            }

            reason = "未知 EffectType（计划构建本应拦截）：'" + type + "'。";
            return false;
        }
    }
}
