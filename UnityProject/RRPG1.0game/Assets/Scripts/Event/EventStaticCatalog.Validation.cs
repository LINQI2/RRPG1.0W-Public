using System;
using System.Collections.Generic;
using RRPG.StaticConfig;

namespace RRPG.Event
{
    /// <summary>
    /// EventStaticCatalog 的自洽性校验（WP-Event-1B）。
    ///
    /// 为什么在 Catalog 层校验（而不是只依赖 1A 的 Compiler）：运行时可能拿到**非编译器产生的**
    /// Projection（人工构造 / 未来 1D 适配器 bug / 版本错配）。1B 的硬约束是
    /// 「绝不静默接受未知枚举或悬空引用」：一旦发现 → Catalog 非法 → Resolver 返回
    /// `InvalidRuntimeConfig` 并给出可操作诊断。
    ///
    /// 校验顺序固定（先事件 → 触发 → 角色 → 角色条件 → 事件条件），因此错误列表顺序确定。
    /// </summary>
    public sealed partial class EventStaticCatalog
    {
        private void Validate()
        {
            ValidateEvents();
            ValidateTriggers();
            ValidateRoles();
            ValidateRoleConditions();
            ValidateConditions();
            ValidateOptionsTables();
        }

        private void ValidateEvents()
        {
            foreach (var eventRecord in _events)
            {
                if (eventRecord == null) continue;

                if (!EventRepeatPolicies.IsKnown(eventRecord.RepeatPolicy))
                {
                    _errors.Add("事件 " + eventRecord.EventId + " 的 RepeatPolicy 未知：'"
                        + eventRecord.RepeatPolicy + "'（允许：" + EventRepeatPolicies.KnownListText() + "）。");
                }

                if (!EventCooldownTypes.IsKnown(eventRecord.CooldownType))
                {
                    _errors.Add("事件 " + eventRecord.EventId + " 的 CooldownType 未知：'"
                        + eventRecord.CooldownType + "'（允许：" + EventCooldownTypes.KnownListText() + "）。");
                }

                if (eventRecord.CooldownValue < 0)
                {
                    _errors.Add("事件 " + eventRecord.EventId + " 的 CooldownValue 为负："
                        + eventRecord.CooldownValue + "。");
                }
            }
        }

        private void ValidateTriggers()
        {
            foreach (var eventRecord in _events)
            {
                if (eventRecord == null) continue;

                foreach (var trigger in Get(_triggersByEvent, eventRecord.EventId))
                {
                    if (!EventTriggerTypes.IsKnown(trigger.TriggerType))
                    {
                        _errors.Add("触发 " + trigger.TriggerId + " 的 TriggerType 未知：'"
                            + trigger.TriggerType + "'（允许：" + EventTriggerTypes.KnownListText() + "）。");
                    }
                }
            }
        }

        private void ValidateRoles()
        {
            foreach (var eventRecord in _events)
            {
                if (eventRecord == null) continue;

                foreach (var role in Get(_rolesByEvent, eventRecord.EventId))
                {
                    if (!EventSelectorTypes.IsKnown(role.SelectorType))
                    {
                        _errors.Add("角色 " + role.RoleId + " 的 SelectorType 未知：'"
                            + role.SelectorType + "'（允许：" + EventSelectorTypes.KnownListText() + "）。");
                    }
                }
            }
        }

        private void ValidateRoleConditions()
        {
            foreach (var pair in _roleConditionsByRole)
            {
                if (pair.Key == null || !HasRole(pair.Key))
                {
                    _errors.Add("角色筛选条件引用了不存在的 RoleId：" + (pair.Key ?? "<null>") + "。");
                }

                foreach (var condition in pair.Value)
                {
                    if (!EventConditionTypes.IsRoleConditionType(condition.ConditionType))
                    {
                        _errors.Add("角色条件 " + condition.ConditionId + " 的类型不允许出现在角色筛选表：'"
                            + condition.ConditionType + "'（允许："
                            + string.Join(" / ", EventConditionTypes.RoleConditionOnly) + "）。");
                    }
                }
            }
        }

        private void ValidateConditions()
        {
            foreach (var pair in _conditionsByEvent)
            {
                if (pair.Key == null || !_eventById.ContainsKey(pair.Key))
                {
                    _errors.Add("事件条件引用了不存在的 EventId：" + (pair.Key ?? "<null>") + "。");
                }

                foreach (var condition in pair.Value)
                {
                    if (!EventConditionTypes.IsKnown(condition.ConditionType))
                    {
                        _errors.Add("事件条件 " + condition.ConditionId + " 的 ConditionType 未知：'"
                            + condition.ConditionType + "'（允许：" + EventConditionTypes.KnownListText() + "）。");
                    }
                }
            }
        }

        private bool HasRole(string roleId)
        {
            if (string.IsNullOrEmpty(roleId)) return false;

            foreach (var pair in _rolesByEvent)
            {
                foreach (var role in pair.Value)
                {
                    if (string.Equals(role.RoleId, roleId, StringComparison.Ordinal)) return true;
                }
            }
            return false;
        }
    }
}
