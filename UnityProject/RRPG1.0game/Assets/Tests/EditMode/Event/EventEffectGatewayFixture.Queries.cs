using System;

namespace RRPG.Event.Tests
{
    /// <summary>夹具的只读投影与纯查询（必须无副作用）。</summary>
    internal sealed partial class EventEffectGatewayFixture
    {
        public bool TryGetVillageResource(string resourceKey, out int amount)
        {
            return VillageResources.TryGetValue(resourceKey ?? string.Empty, out amount);
        }

        public bool TryGetExpeditionSupply(out int supply)
        {
            supply = ExpeditionSupply;
            return true;
        }

        public bool CanGrantRest(string personId, out string reason)
        {
            CanGrantRestCalls++;
            reason = string.Empty;

            if (FailCanGrantRest)
            {
                reason = "夹具：CanGrantRest 被配置为不可执行。";
                return false;
            }
            if (string.IsNullOrEmpty(personId))
            {
                reason = "人物为空。";
                return false;
            }
            return true;
        }

        public bool CanAdvanceExpeditionTime(int days, out string reason)
        {
            CanAdvanceExpeditionTimeCalls++;
            reason = string.Empty;

            if (days <= 0)
            {
                reason = "推进天数必须 > 0。";
                return false;
            }
            return true;
        }

        public bool CanStartBattle(string encounterPoolId, out string reason)
        {
            CanStartBattleCalls++;
            reason = string.Empty;

            if (string.IsNullOrEmpty(encounterPoolId))
            {
                reason = "EncounterPoolId 为空。";
                return false;
            }
            if (FailCanStartBattle)
            {
                reason = "夹具：CanStartBattle 被配置为不可执行（Preflight 失败）。";
                return false;
            }
            return true;
        }
    }
}
