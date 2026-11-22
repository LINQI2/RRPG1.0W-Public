using System;
using System.Collections.Generic;
using RRPG.StaticConfig;

namespace RRPG.Event
{
    /// <summary>EventCondition 的求值结果（三态）。</summary>
    public enum EventConditionOutcome
    {
        Pass = 0,
        Fail = 1,

        /// <summary>
        /// 配置在编译期理论上已被禁止的形态（未知 ConditionType / 缺参数 / 悬空 SubjectRoleId）：
        /// 绝不默认 true / false 后继续，而是让整次求值以 InvalidRuntimeConfig 失败。
        /// </summary>
        InvalidConfig = 2
    }

    /// <summary>
    /// Context 读取契约：让「事件成立条件」与「选项可用条件」共用同一套解释器，
    /// 同时允许 Context 来源不同（1B 用请求的 `EventContext`；1C 用实例冻结的 `EventContextSnapshot`）。
    /// </summary>
    public interface IEventContextReader
    {
        bool TryGetString(string key, out string value);
        bool TryGetInt(string key, out int value);
        bool TryGetPerson(string key, out string personId);
    }

    /// <summary>
    /// 一次条件求值的输入（**只读**）：Context 来源 + 当前 World View + World State + 已完成角色绑定。
    ///
    /// 语义边界（1C §3）：
    ///   - Context* 条件读取 **Context 来源**（事件实例冻结的「为什么触发」）；
    ///   - 其它条件（资源 / 状态 / 历史 / 人物）读取**此刻**的 World View / State
    ///     —— 玩家「现在能不能满足」永远看当下，而不是事件创建时的快照。
    /// </summary>
    public sealed class EventConditionEvaluation
    {
        private static readonly IReadOnlyList<EventRoleBinding> Empty =
            new List<EventRoleBinding>();

        public IEventContextReader Context;
        public IEventWorldView World;
        public EventWorldState WorldState;

        /// <summary>已完成的角色绑定（人物类条件用 SubjectRoleId 在此解析）。</summary>
        public IReadOnlyList<EventRoleBinding> Bindings = Empty;

        public static EventConditionEvaluation Of(IEventContextReader context, IEventWorldView world,
            EventWorldState worldState, IReadOnlyList<EventRoleBinding> bindings = null)
        {
            return new EventConditionEvaluation
            {
                Context = context,
                World = world,
                WorldState = worldState,
                Bindings = bindings ?? Empty
            };
        }
    }

    /// <summary>
    /// 条件形状归一：把 `EventConditionDefinition`（事件成立条件）与
    /// `EventOptionConditionDefinition`（选项可用条件）投影成同一形状，
    /// 从而**只存在一套条件解释器**（1C §5：避免复制第二套语义）。
    /// </summary>
    public sealed class EventConditionView
    {
        public string ConditionId;
        public int GroupId;
        public string ConditionType;
        public string SubjectRoleId;
        public string TargetKey;
        public bool HasIntValue;
        public int IntValue;
        public string StringValue;

        public static EventConditionView FromEventCondition(EventConditionDefinition definition)
        {
            if (definition == null) return null;
            return new EventConditionView
            {
                ConditionId = definition.ConditionId,
                GroupId = definition.GroupId,
                ConditionType = definition.ConditionType,
                SubjectRoleId = definition.SubjectRoleId,
                TargetKey = definition.TargetKey,
                HasIntValue = definition.HasIntValue,
                IntValue = definition.IntValue,
                StringValue = definition.StringValue
            };
        }

        public static EventConditionView FromOptionCondition(EventOptionConditionDefinition definition)
        {
            if (definition == null) return null;
            return new EventConditionView
            {
                ConditionId = definition.ConditionId,
                GroupId = definition.GroupId,
                ConditionType = definition.ConditionType,
                SubjectRoleId = definition.SubjectRoleId,
                TargetKey = definition.TargetKey,
                HasIntValue = definition.HasIntValue,
                IntValue = definition.IntValue,
                StringValue = definition.StringValue
            };
        }

        public static List<EventConditionView> FromEventConditions(
            IReadOnlyList<EventConditionDefinition> definitions)
        {
            var views = new List<EventConditionView>();
            if (definitions == null) return views;

            for (int i = 0; i < definitions.Count; i++)
            {
                var view = FromEventCondition(definitions[i]);
                if (view != null) views.Add(view);
            }
            return views;
        }

        public static List<EventConditionView> FromOptionConditions(
            IReadOnlyList<EventOptionConditionDefinition> definitions)
        {
            var views = new List<EventConditionView>();
            if (definitions == null) return views;

            for (int i = 0; i < definitions.Count; i++)
            {
                var view = FromOptionCondition(definitions[i]);
                if (view != null) views.Add(view);
            }
            return views;
        }

        public override string ToString()
        {
            return (ConditionId ?? "<no-id>") + " group=" + GroupId + " " + ConditionType;
        }
    }
}
