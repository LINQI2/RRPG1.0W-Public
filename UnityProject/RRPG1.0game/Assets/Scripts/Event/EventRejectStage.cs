using System;
using System.Collections.Generic;

namespace RRPG.Event
{
    /// <summary>事件在解析流水线中被拒绝的阶段（WP-Event-1B §24）。</summary>
    public enum EventRejectStage
    {
        None = 0,

        /// <summary>该事件没有任何 TriggerType 等于本次请求的启用触发（未进入候选）。</summary>
        Trigger = 1,

        /// <summary>RepeatPolicy（OncePerWorld / OncePerExpedition）已判定发生。</summary>
        Repeat = 2,

        /// <summary>Cooldown（VillageDays / ExpeditionNodes）未满足。</summary>
        Cooldown = 3,

        /// <summary>无法找到合法 Role Binding（候选为空 / 条件不满足 / 人物唯一性冲突）。</summary>
        RoleBinding = 4,

        /// <summary>EventCondition 未成立。</summary>
        Condition = 5,

        /// <summary>被搜索上限截断（与「无解」不同）。</summary>
        SearchLimit = 6
    }

    /// <summary>单个 Event 的解析追踪（结构化，不靠 Console 刷屏）。</summary>
    public sealed class EventResolutionTraceEntry
    {
        public string EventId { get; internal set; }

        /// <summary>本次请求的 TriggerType 是否命中了该事件的启用触发。</summary>
        public bool TriggerMatched { get; internal set; }

        /// <summary>Repeat 预筛选是否通过（未命中触发时为 false）。</summary>
        public bool RepeatPassed { get; internal set; }

        /// <summary>Cooldown 预筛选是否通过。</summary>
        public bool CooldownPassed { get; internal set; }

        /// <summary>是否求出合法 Role Binding。</summary>
        public bool RoleBindingPassed { get; internal set; }

        /// <summary>EventCondition 是否成立。</summary>
        public bool ConditionPassed { get; internal set; }

        /// <summary>是否成为合法候选（进入最终权重抽取）。</summary>
        public bool IsCandidate { get; internal set; }

        /// <summary>配置权重（只有成为候选时才有意义）。</summary>
        public int Weight { get; internal set; }

        /// <summary>被拒阶段（<see cref="EventRejectStage.None"/> = 未被拒）。</summary>
        public EventRejectStage RejectStage { get; internal set; }

        /// <summary>可操作中文拒绝原因（成功候选为空字符串）。</summary>
        public string RejectReason { get; internal set; }

        /// <summary>本次为该事件评估过的 (Role, 候选 Person) 组合次数（证明「求解而非抽一次」）。</summary>
        public int SearchNodes { get; internal set; }

        /// <summary>求出的角色绑定（成功时非空；顺序 = RoleId 规范升序）。</summary>
        public IReadOnlyList<EventRoleBinding> RoleBindings { get; internal set; }

        public override string ToString()
        {
            if (IsCandidate)
            {
                return EventId + " OK Candidate Weight=" + Weight
                    + " roles=" + RoleBindings.Count + " nodes=" + SearchNodes;
            }

            return EventId + " x " + RejectStage + ": " + RejectReason;
        }
    }
}
