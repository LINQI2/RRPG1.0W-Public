using System;

namespace RRPG.Event
{
    /// <summary>
    /// EventResolutionRequest —— Resolver 的正式输入（WP-Event-1B §3）。
    ///
    /// 四件输入：TriggerType + TriggerKey + ResolutionSeed + EventContext。
    /// 它**只**描述「这一次为什么可能触发事件」；世界状态与历史概不在其中
    /// （分别经 <see cref="IEventWorldView"/> / <see cref="EventWorldState"/> /
    /// <see cref="ExpeditionEventSessionState"/> 以只读方式提供）。
    ///
    /// 硬约束：
    ///   - `TriggerKey` 是本次触发的**稳定身份**（例如
    ///     `recognition:TemporaryDraftBuildup:person_031:year_2`）：同一个 TriggerKey
    ///     在相同输入下必须解析出相同的 <see cref="ResolvedEventInstance.EventInstanceId"/>；
    ///   - 不含时间 / Guid / 随机源：随机性只来自 `ResolutionSeed`。
    /// </summary>
    public sealed class EventResolutionRequest
    {
        /// <summary>触发类型（取值来自 <see cref="EventTriggerTypes"/>）。</summary>
        public string TriggerType { get; private set; }

        /// <summary>本次触发的稳定身份（不得为空）。</summary>
        public string TriggerKey { get; private set; }

        /// <summary>解析随机种子（唯一随机来源）。</summary>
        public int ResolutionSeed { get; private set; }

        /// <summary>本次为什么触发的事件上下文（可为 null = 空上下文）。</summary>
        public EventContext Context { get; private set; }

        public EventResolutionRequest(string triggerType, string triggerKey, int resolutionSeed,
            EventContext context = null)
        {
            TriggerType = triggerType;
            TriggerKey = triggerKey;
            ResolutionSeed = resolutionSeed;
            Context = context;
        }

        /// <summary>空上下文时的安全访问（不创建第二套上下文语义）。</summary>
        public EventContext ContextOrEmpty { get { return Context ?? new EventContext(); } }

        public override string ToString()
        {
            return TriggerType + " key=" + (TriggerKey ?? "<null>") + " seed=" + ResolutionSeed;
        }
    }
}
