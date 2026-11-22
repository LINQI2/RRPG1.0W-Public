using System;
using System.Collections.Generic;
using System.Globalization;

namespace RRPG.Event
{
    /// <summary>
    /// 条件解释器（世界事实段）：World Flag / Event History / Person。
    ///
    /// 语义硬约束：
    ///   - Flag* → <see cref="EventWorldState"/> 的世界 Flag；
    ///   - `EventNotSeen` / `EventSeenCountAtLeast` → **World Event History**（世界长期历史）；
    ///     V0.1 不让同一个 ConditionType 根据 Context 偷换语义（FDS §19.5）；
    ///   - Person* → 经 `SubjectRoleId` 找到已完成绑定的具体人物，再查只读 World View。
    /// </summary>
    public static partial class EventConditionEvaluator
    {
        internal static EventConditionOutcome EvaluateWorld(EventConditionView condition,
            EventConditionEvaluation context, out string detail)
        {
            detail = string.Empty;
            string type = condition.ConditionType;

            if (string.Equals(type, EventConditionTypes.FlagSet, StringComparison.Ordinal)
                || string.Equals(type, EventConditionTypes.FlagNotSet, StringComparison.Ordinal))
            {
                if (string.IsNullOrEmpty(condition.TargetKey))
                {
                    detail = "缺少 TargetKey（Flag 名）。";
                    return EventConditionOutcome.InvalidConfig;
                }

                bool set = context.WorldState != null && context.WorldState.IsFlagSet(condition.TargetKey);
                bool wantSet = string.Equals(type, EventConditionTypes.FlagSet, StringComparison.Ordinal);

                detail = "flag[" + condition.TargetKey + "] " + (wantSet ? "expected=set" : "expected=unset")
                    + " actual=" + (set ? "set" : "unset");
                return set == wantSet ? EventConditionOutcome.Pass : EventConditionOutcome.Fail;
            }

            if (string.Equals(type, EventConditionTypes.EventNotSeen, StringComparison.Ordinal))
            {
                if (string.IsNullOrEmpty(condition.TargetKey))
                {
                    detail = "缺少 TargetKey（目标 EventId）。";
                    return EventConditionOutcome.InvalidConfig;
                }

                int count = OccurrenceCount(context, condition.TargetKey);
                detail = "world history[" + condition.TargetKey + "] expected=0 actual="
                    + count.ToString(CultureInfo.InvariantCulture);
                return count == 0 ? EventConditionOutcome.Pass : EventConditionOutcome.Fail;
            }

            if (string.Equals(type, EventConditionTypes.EventSeenCountAtLeast, StringComparison.Ordinal))
            {
                if (string.IsNullOrEmpty(condition.TargetKey) || !condition.HasIntValue)
                {
                    detail = "缺少 TargetKey 或 IntValue。";
                    return EventConditionOutcome.InvalidConfig;
                }

                int count = OccurrenceCount(context, condition.TargetKey);
                detail = "world history[" + condition.TargetKey + "] expected>="
                    + condition.IntValue.ToString(CultureInfo.InvariantCulture)
                    + " actual=" + count.ToString(CultureInfo.InvariantCulture);
                return count >= condition.IntValue ? EventConditionOutcome.Pass : EventConditionOutcome.Fail;
            }

            // Person*（配置层已保证类型属于人物集合）
            string personId;
            if (!TryResolveSubjectPerson(condition, context.Bindings, out personId, out detail))
            {
                return EventConditionOutcome.InvalidConfig;
            }

            bool ok;
            if (string.Equals(type, EventConditionTypes.PersonExists, StringComparison.Ordinal))
                ok = context.World != null && context.World.PersonExists(personId);
            else if (string.Equals(type, EventConditionTypes.PersonInVillage, StringComparison.Ordinal))
                ok = context.World != null && context.World.PersonInVillage(personId);
            else if (string.Equals(type, EventConditionTypes.PersonOnExpedition, StringComparison.Ordinal))
                ok = context.World != null && context.World.PersonOnExpedition(personId);
            else
                ok = context.World != null && context.World.PersonAvailable(personId);

            detail = type + "(" + personId + ")";
            return ok ? EventConditionOutcome.Pass : EventConditionOutcome.Fail;
        }

        private static int OccurrenceCount(EventConditionEvaluation context, string eventId)
        {
            if (context.WorldState == null) return 0;
            return context.WorldState.History.OccurrenceCount(eventId);
        }

        /// <summary>经 SubjectRoleId 找到已经完成绑定的人物（人物类条件的前提）。</summary>
        internal static bool TryResolveSubjectPerson(EventConditionView condition,
            IReadOnlyList<EventRoleBinding> bindings, out string personId, out string detail)
        {
            personId = null;
            detail = string.Empty;

            if (string.IsNullOrEmpty(condition.SubjectRoleId))
            {
                detail = condition.ConditionType + " 缺少 SubjectRoleId（人物类条件必须指定事件角色）。";
                return false;
            }

            if (bindings == null)
            {
                detail = "当前实例没有角色绑定，无法解析 " + condition.SubjectRoleId + "。";
                return false;
            }

            for (int i = 0; i < bindings.Count; i++)
            {
                if (string.Equals(bindings[i].RoleId, condition.SubjectRoleId, StringComparison.Ordinal))
                {
                    personId = bindings[i].PersonId;
                    if (string.IsNullOrEmpty(personId))
                    {
                        detail = "角色 " + condition.SubjectRoleId + " 的绑定缺少人物。";
                        return false;
                    }
                    return true;
                }
            }

            detail = "SubjectRoleId 未在该事件的角色绑定中：" + condition.SubjectRoleId + "。";
            return false;
        }
    }
}
