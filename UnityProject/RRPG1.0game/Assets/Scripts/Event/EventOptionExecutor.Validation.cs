using System;
using RRPG.StaticConfig;

namespace RRPG.Event
{
    /// <summary>EventOptionExecutor：前置检查（输入 / 依赖 / exactly-once / 选项存在性 / 可用性）。</summary>
    public static partial class EventOptionExecutor
    {
        /// <summary>返回 null = 前置条件全部满足。</summary>
        private static EventOptionExecutionResult CheckPreconditions(ResolvedEventInstance instance,
            EventStaticCatalog catalog, IEventWorldView world, EventWorldState worldState,
            ExpeditionEventSessionState sessionState, EventExecutionState executionState,
            IEventEffectGateway gateway, string optionId, EventExecutionTrace trace)
        {
            if (instance == null)
            {
                return Fail(trace, EventExecutionCode.InvalidRuntimeConfig, null, optionId,
                    "事件实例为 null：Option 执行必须以已解析实例为主体。");
            }

            if (catalog == null)
            {
                return Fail(trace, EventExecutionCode.InvalidRuntimeConfig, instance, optionId,
                    "静态事件配置不可用（未提供 EventStaticCatalog）。");
            }

            if (world == null || worldState == null || sessionState == null)
            {
                return Fail(trace, EventExecutionCode.InvalidRuntimeConfig, instance, optionId,
                    "World View / Event World State / Expedition Session State 不得为 null。");
            }

            if (executionState == null)
            {
                return Fail(trace, EventExecutionCode.InvalidRuntimeConfig, instance, optionId,
                    "缺少 EventExecutionState：没有它就无法保证 exactly-once（FDS §17）。");
            }

            if (gateway == null)
            {
                return Fail(trace, EventExecutionCode.InvalidRuntimeConfig, instance, optionId,
                    "缺少 IEventEffectGateway：外部产品域效果必须经网关表达"
                    + "（Event Core 不拥有 Village / Expedition / Battle Authority）。");
            }

            return null;
        }

        /// <summary>exactly-once 守卫（§17 / §18）：先于任何 mutation 判定。</summary>
        private static EventOptionExecutionResult CheckExactlyOnce(ResolvedEventInstance instance,
            EventExecutionState executionState, string optionId, EventExecutionTrace trace)
        {
            trace.Stage = "ExactlyOnce";
            if (!executionState.IsCompleted(instance.EventInstanceId)) return null;

            trace.Line("InstanceAlreadyResolved：" + instance.EventInstanceId + "（本次零 mutation）");
            return Fail(trace, EventExecutionCode.InstanceAlreadyResolved, instance, optionId,
                "事件实例已经成功结算过：" + instance.EventInstanceId
                + "。同一个 EventInstance 只能成功执行一个 Option 一次。");
        }

        private static bool TryResolveOption(ResolvedEventInstance instance, EventStaticCatalog catalog,
            string optionId, EventExecutionTrace trace, out EventOptionDefinition option,
            out EventOptionExecutionResult failure)
        {
            trace.Stage = "Option";
            failure = null;

            if (string.IsNullOrEmpty(optionId) || !catalog.TryGetOption(optionId, out option))
            {
                option = null;
                failure = Fail(trace, EventExecutionCode.OptionNotFound, instance, optionId,
                    "选项不存在：" + optionId + "。");
                return false;
            }

            if (!string.Equals(option.EventId, instance.EventId, StringComparison.Ordinal))
            {
                failure = Fail(trace, EventExecutionCode.InvalidRuntimeConfig, instance, optionId,
                    "选项 " + optionId + " 不属于事件 " + instance.EventId
                    + "（实际属于 " + option.EventId + "）。");
                return false;
            }

            return true;
        }

        /// <summary>
        /// Option 可用性（§4）：条件不成立时按 UnavailableMode → Hidden / Disabled；
        /// 未知模式或无法求值 → InvalidRuntimeConfig（绝不猜）。
        /// </summary>
        private static bool RequireAvailable(ResolvedEventInstance instance, string optionId,
            EventStaticCatalog catalog, IEventWorldView world, EventWorldState worldState,
            EventExecutionTrace trace, out EventOptionExecutionResult failure)
        {
            trace.Stage = "OptionCondition";
            failure = null;

            var availability = EventOptionEvaluator.Evaluate(instance, optionId, catalog, world, worldState);

            trace.ConditionOutcome = availability.Availability == EventOptionAvailability.Available
                ? EventConditionOutcome.Pass
                : (availability.Availability == EventOptionAvailability.Invalid
                    ? EventConditionOutcome.InvalidConfig
                    : EventConditionOutcome.Fail);
            trace.ConditionDetail = availability.ConditionDetail;
            trace.Line("OptionCondition = " + availability.Code
                + (string.IsNullOrEmpty(availability.ConditionDetail)
                    ? string.Empty
                    : "（" + availability.ConditionDetail + "）"));

            switch (availability.Availability)
            {
                case EventOptionAvailability.Available:
                    return true;

                case EventOptionAvailability.Hidden:
                    failure = Fail(trace, EventExecutionCode.OptionHidden, instance, optionId,
                        availability.Diagnostic);
                    return false;

                case EventOptionAvailability.Disabled:
                    failure = Fail(trace, EventExecutionCode.OptionDisabled, instance, optionId,
                        availability.Diagnostic);
                    return false;

                default:
                    failure = Fail(trace, EventExecutionCode.InvalidRuntimeConfig, instance, optionId,
                        availability.Diagnostic);
                    return false;
            }
        }
    }
}
