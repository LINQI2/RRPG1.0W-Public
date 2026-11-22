using System;

namespace RRPG.Event.Tests
{
    /// <summary>夹具的正式提交入口（只在 Commit 阶段被调用；失败/异常可注入）。</summary>
    internal sealed partial class EventEffectGatewayFixture
    {
        public bool ApplyVillageResourceDelta(string resourceKey, int delta, out string reason)
        {
            ApplyVillageResourceDeltaCalls++;
            reason = string.Empty;

            if (ThrowOnApplyVillageResourceDelta)
            {
                throw new InvalidOperationException("夹具：ApplyVillageResourceDelta 抛出异常。");
            }

            int current;
            if (!VillageResources.TryGetValue(resourceKey ?? string.Empty, out current))
            {
                reason = "未知村庄资源：" + resourceKey;
                return false;
            }

            int after = current + delta;
            if (after < 0)
            {
                reason = "夹具：不允许把 " + resourceKey + " 扣成负数。";
                return false;
            }

            VillageResources[resourceKey] = after;
            return true;
        }

        public bool ApplySupplyDelta(int delta, out string reason)
        {
            ApplySupplyDeltaCalls++;
            reason = string.Empty;

            if (FailApplySupplyDelta)
            {
                reason = "夹具：ApplySupplyDelta 被配置为失败。";
                return false;
            }

            int after = ExpeditionSupply + delta;
            if (after < 0)
            {
                reason = "夹具：不允许把 Supply 扣成负数。";
                return false;
            }

            ExpeditionSupply = after;
            return true;
        }

        public bool GrantRest(string personId, out string reason)
        {
            GrantRestCalls++;
            reason = string.Empty;

            if (FailGrantRest)
            {
                reason = "夹具：GrantRest 被配置为失败。";
                return false;
            }

            RestGrants.Add(personId);
            return true;
        }

        public bool AdvanceExpeditionTime(int days, out string reason)
        {
            AdvanceExpeditionTimeCalls++;
            reason = string.Empty;
            AdvancedDays.Add(days);
            return true;
        }

        public bool StartBattle(string encounterPoolId, out string reason)
        {
            StartBattleCalls++;
            reason = string.Empty;

            if (ThrowOnStartBattle)
            {
                throw new InvalidOperationException("夹具：StartBattle 抛出异常。");
            }

            if (FailStartBattle)
            {
                reason = "夹具：StartBattle 被配置为失败。";
                return false;
            }

            BattleStarts.Add(encounterPoolId);
            return true;
        }
    }
}
