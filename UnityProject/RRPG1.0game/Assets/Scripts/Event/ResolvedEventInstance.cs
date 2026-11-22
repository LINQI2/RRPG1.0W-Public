using System;
using System.Collections.Generic;

namespace RRPG.Event
{
    /// <summary>
    /// 一条**已完成**的角色绑定（Role → Person）—— 只存在于已解析事件实例与 Trace 内。
    ///
    /// 硬约束：同一个 <see cref="ResolvedEventInstance"/> 内不同 Role 默认必须绑定不同 PersonId
    /// （WP-Event-1B §10）。若故事确实需要同一人，应改为引用同一个 RoleId，而不是放宽唯一性。
    /// </summary>
    public sealed class EventRoleBinding
    {
        /// <summary>事件角色（`EventRole.RoleId`）。</summary>
        public string RoleId { get; private set; }

        /// <summary>角色中文名（来自配置；仅供 Trace / 未来表现层参考，不参与规则判定）。</summary>
        public string RoleName { get; private set; }

        /// <summary>人物选择方式（ContextPerson / VillagePerson / ExpeditionPartyMember / FixedPerson）。</summary>
        public string SelectorType { get; private set; }

        /// <summary>被绑定的人物 Id。</summary>
        public string PersonId { get; private set; }

        /// <summary>ContextPerson 使用的 SourceKey（其它方式为 null；仅诊断用）。</summary>
        public string SourceKey { get; private set; }

        internal EventRoleBinding(string roleId, string roleName, string selectorType, string personId,
            string sourceKey)
        {
            RoleId = roleId;
            RoleName = roleName;
            SelectorType = selectorType;
            PersonId = personId;
            SourceKey = sourceKey;
        }

        public override string ToString()
        {
            return RoleId + "(" + SelectorType + ") → " + PersonId;
        }
    }

    /// <summary>
    /// **ResolvedEventInstance（已解析事件实例）** —— WP-Event-1B §19 / §20 / §21。
    ///
    /// 它只冻结「事件为什么能出现、出现的是谁」：
    ///   - 身份：EventInstanceId / EventId / TriggerType / TriggerKey / ResolutionSeed；
    ///   - 人物：RoleBindings；
    ///   - 上下文：ContextSnapshot（本次为什么触发）。
    ///
    /// **刻意不复制**：Title / Body / Options / Conditions / Effects（仍通过 `EventId → EventConfig` 取得）、
    /// 当前 Village 全状态、当前 Expedition 全状态（动态状态属 1C / Adapter 在需要时重新读取）。
    ///
    /// EventInstanceId 规则（§20）：
    ///   - 非 Guid；同输入 / 同 Seed 稳定；**不等于** EventId；同一个 TriggerKey 在相同输入下重复解析
    ///     得到同一个 Id；不同事件（或不同 Seed）得到不同 Id。
    /// </summary>
    public sealed class ResolvedEventInstance
    {
        public string EventInstanceId { get; private set; }
        public string EventId { get; private set; }
        public string TriggerType { get; private set; }
        public string TriggerKey { get; private set; }
        public int ResolutionSeed { get; private set; }

        private readonly List<EventRoleBinding> _roleBindings;

        public EventContextSnapshot ContextSnapshot { get; private set; }

        internal ResolvedEventInstance(string eventInstanceId, string eventId, string triggerType,
            string triggerKey, int resolutionSeed, List<EventRoleBinding> roleBindings,
            EventContextSnapshot contextSnapshot)
        {
            EventInstanceId = eventInstanceId;
            EventId = eventId;
            TriggerType = triggerType;
            TriggerKey = triggerKey;
            ResolutionSeed = resolutionSeed;
            _roleBindings = roleBindings ?? new List<EventRoleBinding>();
            ContextSnapshot = contextSnapshot;
        }

        /// <summary>角色绑定（顺序 = RoleId 规范升序；与解析输入顺序无关）。</summary>
        public IReadOnlyList<EventRoleBinding> RoleBindings { get { return _roleBindings; } }

        /// <summary>按 RoleId 取已绑定人物（未命中 → false）。</summary>
        public bool TryGetBoundPerson(string roleId, out string personId)
        {
            personId = null;
            if (string.IsNullOrEmpty(roleId)) return false;

            for (int i = 0; i < _roleBindings.Count; i++)
            {
                if (string.Equals(_roleBindings[i].RoleId, roleId, StringComparison.Ordinal))
                {
                    personId = _roleBindings[i].PersonId;
                    return true;
                }
            }
            return false;
        }

        public override string ToString()
        {
            return EventInstanceId + " [" + EventId + "] " + TriggerType
                + " roles=" + _roleBindings.Count + " context=" + ContextSnapshot;
        }
    }
}
