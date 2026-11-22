using System;
using RRPG.StaticConfig;

namespace RRPG.Event
{
    /// <summary>EventOptionExecutor：逐 EffectType 的形状校验（运行时第二道防线，§21）。</summary>
    public static partial class EventOptionExecutor
    {
        private static bool ValidateEffectShape(ExecutionContext ctx, EventEffectPlanEntry entry, int index, int count,
            out string error)
        {
            error = string.Empty;
            string type = entry.EffectType;

            // ---------------- Event Core 自己拥有：Flag（幂等；Commit 阶段才 mutation） ----------------
            if (EventEffectTypes.IsEventCoreOwned(type))
            {
                if (string.IsNullOrEmpty(entry.TargetKey))
                {
                    error = type + "（效果 " + entry.EffectId + "）必须填写 TargetKey（Flag 名）。";
                    return false;
                }
                return true;
            }

            // ---------------- VillageResourceDelta ----------------
            if (string.Equals(type, EventEffectTypes.VillageResourceDelta, StringComparison.Ordinal))
            {
                bool knownKey = string.Equals(entry.TargetKey, EventResourceKeys.Ingredients,
                                    StringComparison.Ordinal)
                                || string.Equals(entry.TargetKey, EventResourceKeys.Food,
                                    StringComparison.Ordinal);
                if (!knownKey)
                {
                    error = "VillageResourceDelta（效果 " + entry.EffectId
                        + "）的 TargetKey 必须是 Ingredients 或 Food（当前 '"
                        + (entry.TargetKey ?? "<null>") + "'）。";
                    return false;
                }
                if (!entry.HasIntValue || entry.IntValue == 0)
                {
                    error = "VillageResourceDelta（效果 " + entry.EffectId + "）必须填写非 0 的 IntValue。";
                    return false;
                }
                return true;
            }

            // ---------------- SupplyDelta ----------------
            if (string.Equals(type, EventEffectTypes.SupplyDelta, StringComparison.Ordinal))
            {
                if (!entry.HasIntValue || entry.IntValue == 0)
                {
                    error = "SupplyDelta（效果 " + entry.EffectId + "）必须填写非 0 的 IntValue。";
                    return false;
                }
                return true;
            }

            // ---------------- AdvanceExpeditionTime ----------------
            if (string.Equals(type, EventEffectTypes.AdvanceExpeditionTime, StringComparison.Ordinal))
            {
                if (!entry.HasIntValue || entry.IntValue <= 0)
                {
                    error = "AdvanceExpeditionTime（效果 " + entry.EffectId
                        + "）的 IntValue 必须 > 0（额外推进的远征天数）。";
                    return false;
                }
                return true;
            }

            // ---------------- GrantPersonRest（先经 TargetRoleId 解析到具体人物） ----------------
            if (string.Equals(type, EventEffectTypes.GrantPersonRest, StringComparison.Ordinal))
            {
                if (string.IsNullOrEmpty(entry.TargetRoleId))
                {
                    error = "GrantPersonRest（效果 " + entry.EffectId + "）必须填写 TargetRoleId。";
                    return false;
                }

                string personId;
                if (!ctx.Instance.TryGetBoundPerson(entry.TargetRoleId, out personId))
                {
                    error = "GrantPersonRest（效果 " + entry.EffectId + "）的 TargetRoleId "
                        + entry.TargetRoleId + " 不在本实例的角色绑定中。";
                    return false;
                }

                entry.TargetPersonId = personId;
                return true;
            }

            // ---------------- StartBattle（Terminal Effect） ----------------
            if (string.IsNullOrEmpty(entry.TargetKey))
            {
                error = "StartBattle（效果 " + entry.EffectId
                    + "）必须填写 TargetKey（EncounterPool.PoolId）。";
                return false;
            }

            if (index != count - 1)
            {
                error = "StartBattle（效果 " + entry.EffectId
                    + "）必须是该结果的最后一个效果：Terminal Effect 不可能有后续同结算效果。";
                return false;
            }

            return true;
        }
    }
}
