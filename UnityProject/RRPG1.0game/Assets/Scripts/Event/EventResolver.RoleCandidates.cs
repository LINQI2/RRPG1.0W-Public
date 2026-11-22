using System;
using System.Collections.Generic;
using RRPG.Deterministic;
using RRPG.StaticConfig;

namespace RRPG.Event
{
    /// <summary>EventResolver：Role 候选人物与角色筛选条件（WP-Event-1B §8 / §9）。</summary>
    public static partial class EventResolver
    {
        /// <summary>每个 Role 的全部筛选条件 = AND（§9）；任一不满足即该候选人物不可用。</summary>
        private static bool RoleConditionsPass(ResolveContext ctx, EventRoleDefinition role, string personId,
            out string detail)
        {
            detail = string.Empty;

            var conditions = ctx.Catalog.GetRoleConditions(role.RoleId);
            for (int i = 0; i < conditions.Count; i++)
            {
                var condition = conditions[i];
                bool ok;

                if (string.Equals(condition.ConditionType, EventConditionTypes.PersonInVillage,
                        StringComparison.Ordinal))
                {
                    ok = ctx.World.PersonInVillage(personId);
                }
                else if (string.Equals(condition.ConditionType, EventConditionTypes.PersonOnExpedition,
                             StringComparison.Ordinal))
                {
                    ok = ctx.World.PersonOnExpedition(personId);
                }
                else if (string.Equals(condition.ConditionType, EventConditionTypes.PersonAvailable,
                             StringComparison.Ordinal))
                {
                    ok = ctx.World.PersonAvailable(personId);
                }
                else
                {
                    // Catalog 校验已阻止未知类型；这里显式拒绝而不是默默放过
                    detail = "角色条件类型不可判定：" + condition.ConditionType;
                    return false;
                }

                if (!ok)
                {
                    detail = condition.ConditionType + " 不满足（person " + personId + "）";
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// 生成某个 Role 的候选人物（规范排序 + 由 Seed 派生的确定性顺序）。
        /// 顺序只依赖 (Seed, EventId, RoleId)：其它 Role 的候选数量变化不会污染本 Role 的顺序。
        /// </summary>
        private static bool BuildCandidates(ResolveContext ctx, EventDefinition eventRecord, EventRoleDefinition role,
            out List<string> candidates, out string detail)
        {
            candidates = new List<string>();
            detail = string.Empty;

            if (string.Equals(role.SelectorType, EventSelectorTypes.ContextPerson, StringComparison.Ordinal))
            {
                if (string.IsNullOrEmpty(role.SourceKey))
                {
                    detail = "配置缺少 SourceKey（ContextPerson 必须声明来源键）。";
                    return false;
                }

                string personId;
                if (!ctx.Request.ContextOrEmpty.TryGetPerson(role.SourceKey, out personId)
                    || string.IsNullOrEmpty(personId))
                {
                    detail = "EventContext 缺少 person[" + role.SourceKey + "]。";
                    return false;
                }

                if (!ctx.World.PersonExists(personId))
                {
                    detail = "上下文人物在 World View 中不存在：" + personId + "。";
                    return false;
                }

                candidates.Add(personId);
                return true;
            }

            if (string.Equals(role.SelectorType, EventSelectorTypes.FixedPerson, StringComparison.Ordinal))
            {
                if (string.IsNullOrEmpty(role.FixedPersonId))
                {
                    detail = "配置缺少 FixedPersonId（FixedPerson 必须声明固定人物）。";
                    return false;
                }

                if (!ctx.World.PersonExists(role.FixedPersonId))
                {
                    detail = "固定人物不存在：" + role.FixedPersonId
                        + "（固定 ID 不代表允许不存在的 Person）。";
                    return false;
                }

                candidates.Add(role.FixedPersonId);
                return true;
            }

            if (string.Equals(role.SelectorType, EventSelectorTypes.VillagePerson, StringComparison.Ordinal))
            {
                AddPool(ctx, ctx.World.VillagePersonIds, candidates);
                if (candidates.Count == 0) detail = "村庄人物候选池为空。";
            }
            else if (string.Equals(role.SelectorType, EventSelectorTypes.ExpeditionPartyMember,
                         StringComparison.Ordinal))
            {
                AddPool(ctx, ctx.World.ExpeditionPartyPersonIds, candidates);
                if (candidates.Count == 0) detail = "远征队伍候选池为空。";
            }
            else
            {
                detail = "未知 SelectorType：" + role.SelectorType + "。";
                return false;
            }

            candidates.Sort(StringComparer.Ordinal);

            var rng = new DeterministicRandom(ctx.Request.ResolutionSeed)
                .Derive("role-order|" + eventRecord.EventId + "|" + role.RoleId);

            var permutation = rng.Permutation(candidates.Count);
            var shuffled = new List<string>(candidates.Count);
            for (int i = 0; i < permutation.Count; i++) shuffled.Add(candidates[permutation[i]]);

            candidates = shuffled;
            return true;
        }

        /// <summary>把候选池投影成「存在且去重」的人物列表（顺序无关；调用方随后规范排序）。</summary>
        private static void AddPool(ResolveContext ctx, IReadOnlyList<string> pool, List<string> target)
        {
            if (pool == null) return;

            for (int i = 0; i < pool.Count; i++)
            {
                string personId = pool[i];
                if (string.IsNullOrEmpty(personId)) continue;
                if (target.Contains(personId)) continue;
                if (!ctx.World.PersonExists(personId)) continue;

                target.Add(personId);
            }
        }
    }
}
