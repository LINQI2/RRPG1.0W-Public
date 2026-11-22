using System;
using System.Collections.Generic;
using RRPG.StaticConfig;

namespace RRPG.Event
{
    /// <summary>
    /// EventStaticCatalog —— Resolver 需要的**纯逻辑静态配置视图**（WP-Event-1B）。
    ///
    /// 为什么不是直接用 `EventConfigDatabase`：
    ///   - `EventConfigDatabase` 是 `ScriptableObject`（UnityEngine 类型），而 Event.Runtime 是
    ///     `noEngineReferences = true` 的纯逻辑程序集 —— 一旦引用它就会把引擎依赖带进来；
    ///   - 因此 Resolver 只吃本类：由**适配器**把已生成的静态配置（1A 的 `EventConfigDatabase`
    ///     或测试 Fixture）投影成这 5 张只读表的规范索引。产品适配器属 **1D**；
    ///     1B 测试用 Fixture + EditMode 里的 Authority 投影。
    ///
    /// 规范排序（确定性前提，§17）：
    ///   - Events 按 EventId Ordinal 升序；
    ///   - Triggers 按 TriggerId；Roles 按 RoleId；RoleConditions 按 ConditionId；
    ///   - Conditions 按 (GroupId, ConditionId)。
    ///   索引在**构造时**一次性完成，因此输入列表顺序不影响任何结果。
    ///
    /// 校验（构造时一次性收集，任一错误 → <see cref="IsValid"/>=false）：
    /// 见 `EventStaticCatalog.Validation.cs`。Resolver 遇到非法 Catalog 直接返回
    /// <see cref="EventResolutionCode.InvalidRuntimeConfig"/>，不静默继续。
    /// </summary>
    public sealed partial class EventStaticCatalog
    {
        private readonly List<EventDefinition> _events = new List<EventDefinition>();
        private readonly Dictionary<string, EventDefinition> _eventById =
            new Dictionary<string, EventDefinition>(StringComparer.Ordinal);
        private readonly Dictionary<string, List<EventTriggerDefinition>> _triggersByEvent =
            new Dictionary<string, List<EventTriggerDefinition>>(StringComparer.Ordinal);
        private readonly Dictionary<string, List<EventRoleDefinition>> _rolesByEvent =
            new Dictionary<string, List<EventRoleDefinition>>(StringComparer.Ordinal);
        private readonly Dictionary<string, List<EventRoleConditionDefinition>> _roleConditionsByRole =
            new Dictionary<string, List<EventRoleConditionDefinition>>(StringComparer.Ordinal);
        private readonly Dictionary<string, List<EventConditionDefinition>> _conditionsByEvent =
            new Dictionary<string, List<EventConditionDefinition>>(StringComparer.Ordinal);
        private readonly Dictionary<string, List<EventDefinition>> _enabledByTriggerType =
            new Dictionary<string, List<EventDefinition>>(StringComparer.Ordinal);
        private readonly List<string> _errors = new List<string>();

        public EventStaticCatalog(
            IReadOnlyList<EventDefinition> events,
            IReadOnlyList<EventTriggerDefinition> triggers,
            IReadOnlyList<EventRoleDefinition> roles,
            IReadOnlyList<EventRoleConditionDefinition> roleConditions,
            IReadOnlyList<EventConditionDefinition> conditions)
            : this(events, triggers, roles, roleConditions, conditions, null, null, null, null)
        {
        }

        /// <summary>
        /// 完整构造（WP-Event-1C）：额外承载 Option / OptionCondition / Outcome / Effect 四张表。
        /// 1B 只需要前五张表，因此保留了等价的 5 参数重载（1B 测试与夹具零改动）。
        /// </summary>
        public EventStaticCatalog(
            IReadOnlyList<EventDefinition> events,
            IReadOnlyList<EventTriggerDefinition> triggers,
            IReadOnlyList<EventRoleDefinition> roles,
            IReadOnlyList<EventRoleConditionDefinition> roleConditions,
            IReadOnlyList<EventConditionDefinition> conditions,
            IReadOnlyList<EventOptionDefinition> options,
            IReadOnlyList<EventOptionConditionDefinition> optionConditions,
            IReadOnlyList<EventOutcomeDefinition> outcomes,
            IReadOnlyList<EventEffectDefinition> effects)
        {
            BuildEvents(events);
            BuildTriggers(triggers);
            BuildRoles(roles);
            BuildRoleConditions(roleConditions);
            BuildConditions(conditions);

            BuildOptions(options);
            BuildOptionConditions(optionConditions);
            BuildOutcomes(outcomes);
            BuildEffects(effects);

            BuildEnabledTriggerIndex();
            Validate();
        }

        /// <summary>配置是否自洽（false = 不可用于求解，Resolver 会返回 InvalidRuntimeConfig）。</summary>
        public bool IsValid { get { return _errors.Count == 0; } }

        /// <summary>全部配置错误（可操作中文；顺序 = 发现顺序）。</summary>
        public IReadOnlyList<string> Errors { get { return _errors; } }

        /// <summary>全部事件（EventId 规范升序）。</summary>
        public IReadOnlyList<EventDefinition> Events { get { return _events; } }

        public bool TryGetEvent(string eventId, out EventDefinition definition)
        {
            definition = null;
            if (string.IsNullOrEmpty(eventId)) return false;
            return _eventById.TryGetValue(eventId, out definition);
        }

        /// <summary>某事件的触发（TriggerId 升序）。</summary>
        public IReadOnlyList<EventTriggerDefinition> GetTriggers(string eventId)
        {
            return Get(_triggersByEvent, eventId);
        }

        /// <summary>某事件的角色（RoleId 升序）。</summary>
        public IReadOnlyList<EventRoleDefinition> GetRoles(string eventId)
        {
            return Get(_rolesByEvent, eventId);
        }

        /// <summary>某角色的筛选条件（ConditionId 升序）。</summary>
        public IReadOnlyList<EventRoleConditionDefinition> GetRoleConditions(string roleId)
        {
            return Get(_roleConditionsByRole, roleId);
        }

        /// <summary>某事件的成立条件（(GroupId, ConditionId) 升序）。</summary>
        public IReadOnlyList<EventConditionDefinition> GetConditions(string eventId)
        {
            return Get(_conditionsByEvent, eventId);
        }

        /// <summary>
        /// 监听指定 TriggerType 的**启用**事件（EventId 升序）。
        /// 同一个事件即使配置了多条同类型触发，也只会出现一次（§7：一个 Event 一张票）。
        /// </summary>
        public IReadOnlyList<EventDefinition> GetEnabledEventsByTrigger(string triggerType)
        {
            return Get(_enabledByTriggerType, triggerType);
        }

        /// <summary>该事件是否至少有一条指定 TriggerType 的触发（不区分启用）。</summary>
        public bool HasTriggerType(string eventId, string triggerType)
        {
            var list = Get(_triggersByEvent, eventId);
            for (int i = 0; i < list.Count; i++)
            {
                if (string.Equals(list[i].TriggerType, triggerType, StringComparison.Ordinal)) return true;
            }
            return false;
        }

        private static IReadOnlyList<T> Get<T>(Dictionary<string, List<T>> index, string key)
        {
            if (string.IsNullOrEmpty(key)) return new List<T>();

            List<T> list;
            return index.TryGetValue(key, out list) ? (IReadOnlyList<T>)list : new List<T>();
        }
    }
}
