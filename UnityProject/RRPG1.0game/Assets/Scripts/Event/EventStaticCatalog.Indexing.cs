using System;
using System.Collections.Generic;
using RRPG.StaticConfig;

namespace RRPG.Event
{
    /// <summary>
    /// EventStaticCatalog 的索引构建（WP-Event-1B）。
    ///
    /// 全部索引在**构造时一次**完成，并保证规范排序，因此输入列表的物理顺序不影响任何结果
    /// （确定性前提，§17）。重复身份在这里就报错（不进入索引）。
    /// </summary>
    public sealed partial class EventStaticCatalog
    {
        private void BuildEvents(IReadOnlyList<EventDefinition> events)
        {
            if (events != null)
            {
                foreach (var record in events)
                {
                    if (record != null) _events.Add(record);
                }
            }

            _events.Sort((a, b) => string.CompareOrdinal(a.EventId ?? string.Empty, b.EventId ?? string.Empty));

            foreach (var record in _events)
            {
                string eventId = record.EventId ?? string.Empty;
                if (eventId.Length == 0)
                {
                    _errors.Add("Event 行缺少 EventId（无法作为事件身份）。");
                    continue;
                }

                if (_eventById.ContainsKey(eventId))
                {
                    _errors.Add("事件身份重复：EventId = " + eventId + "（事件身份必须唯一）。");
                    continue;
                }
                _eventById.Add(eventId, record);
            }
        }

        private void BuildTriggers(IReadOnlyList<EventTriggerDefinition> triggers)
        {
            var seenIds = new HashSet<string>(StringComparer.Ordinal);

            if (triggers != null)
            {
                foreach (var record in triggers)
                {
                    if (record == null) continue;

                    string triggerId = record.TriggerId ?? string.Empty;
                    if (triggerId.Length == 0)
                    {
                        _errors.Add("EventTrigger 行缺少 TriggerId。");
                        continue;
                    }
                    if (!seenIds.Add(triggerId))
                    {
                        _errors.Add("触发配置身份重复：TriggerId = " + triggerId + "。");
                        continue;
                    }

                    Add(_triggersByEvent, record.EventId, record);
                }
            }

            SortAll(_triggersByEvent, (a, b) => string.CompareOrdinal(a.TriggerId, b.TriggerId));
        }

        private void BuildRoles(IReadOnlyList<EventRoleDefinition> roles)
        {
            var seenIds = new HashSet<string>(StringComparer.Ordinal);

            if (roles != null)
            {
                foreach (var record in roles)
                {
                    if (record == null) continue;

                    string roleId = record.RoleId ?? string.Empty;
                    if (roleId.Length == 0)
                    {
                        _errors.Add("EventRole 行缺少 RoleId。");
                        continue;
                    }
                    if (!seenIds.Add(roleId))
                    {
                        _errors.Add("事件角色身份重复：RoleId = " + roleId + "（全局唯一）。");
                        continue;
                    }

                    Add(_rolesByEvent, record.EventId, record);
                }
            }

            SortAll(_rolesByEvent, (a, b) => string.CompareOrdinal(a.RoleId, b.RoleId));
        }

        private void BuildRoleConditions(IReadOnlyList<EventRoleConditionDefinition> roleConditions)
        {
            var seenIds = new HashSet<string>(StringComparer.Ordinal);

            if (roleConditions != null)
            {
                foreach (var record in roleConditions)
                {
                    if (record == null) continue;

                    string conditionId = record.ConditionId ?? string.Empty;
                    if (conditionId.Length == 0)
                    {
                        _errors.Add("EventRoleCondition 行缺少 ConditionId。");
                        continue;
                    }
                    if (!seenIds.Add(conditionId))
                    {
                        _errors.Add("角色条件身份重复：ConditionId = " + conditionId + "。");
                        continue;
                    }

                    Add(_roleConditionsByRole, record.RoleId, record);
                }
            }

            SortAll(_roleConditionsByRole, (a, b) => string.CompareOrdinal(a.ConditionId, b.ConditionId));
        }

        private void BuildConditions(IReadOnlyList<EventConditionDefinition> conditions)
        {
            var seenIds = new HashSet<string>(StringComparer.Ordinal);

            if (conditions != null)
            {
                foreach (var record in conditions)
                {
                    if (record == null) continue;

                    string conditionId = record.ConditionId ?? string.Empty;
                    if (conditionId.Length == 0)
                    {
                        _errors.Add("EventCondition 行缺少 ConditionId。");
                        continue;
                    }
                    if (!seenIds.Add(conditionId))
                    {
                        _errors.Add("事件条件身份重复：ConditionId = " + conditionId + "。");
                        continue;
                    }

                    Add(_conditionsByEvent, record.EventId, record);
                }
            }

            SortAll(_conditionsByEvent, (a, b) =>
            {
                int byGroup = a.GroupId.CompareTo(b.GroupId);
                return byGroup != 0 ? byGroup : string.CompareOrdinal(a.ConditionId, b.ConditionId);
            });
        }
    }
}
