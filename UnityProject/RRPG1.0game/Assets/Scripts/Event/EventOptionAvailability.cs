namespace RRPG.Event
{
    /// <summary>
    /// 选项可用性（WP-Event-1C §4）。**UI 只消费这个结果，不得自己重新实现条件判断。**
    /// </summary>
    public enum EventOptionAvailability
    {
        /// <summary>条件成立，玩家可以选择。</summary>
        Available = 0,

        /// <summary>条件不成立 + `UnavailableMode = Hide`：整条选项隐藏。</summary>
        Hidden = 1,

        /// <summary>条件不成立 + `UnavailableMode = Disable`：仍然显示，但不可选择。</summary>
        Disabled = 2,

        /// <summary>配置 / 运行时异常（未知 ConditionType、悬空 SubjectRoleId、未知 UnavailableMode）：显式失败，不猜。</summary>
        Invalid = 3
    }

    /// <summary>
    /// 选项可用性结果（含可诊断原因）。
    ///
    /// 边界：本类型只描述「现在能不能选」，**不**产生任何世界变化（§7）——查询可用性永远不 mutation。
    /// </summary>
    public sealed class EventOptionAvailabilityResult
    {
        public string EventInstanceId { get; private set; }
        public string EventId { get; private set; }
        public string OptionId { get; private set; }
        public EventOptionAvailability Availability { get; private set; }

        /// <summary>英文结果码文本（便于测试与日志对齐）。</summary>
        public string Code { get { return Availability.ToString(); } }

        /// <summary>可操作中文诊断（Available 时为空字符串）。</summary>
        public string Diagnostic { get; private set; }

        /// <summary>条件求值细节（失败时给出「组 N（AND）中的 cX：…」）。</summary>
        public string ConditionDetail { get; private set; }

        public bool IsSelectable { get { return Availability == EventOptionAvailability.Available; } }

        private EventOptionAvailabilityResult() { }

        internal static EventOptionAvailabilityResult Create(string eventInstanceId, string eventId, string optionId,
            EventOptionAvailability availability, string diagnostic, string conditionDetail)
        {
            return new EventOptionAvailabilityResult
            {
                EventInstanceId = eventInstanceId,
                EventId = eventId,
                OptionId = optionId,
                Availability = availability,
                Diagnostic = diagnostic ?? string.Empty,
                ConditionDetail = conditionDetail ?? string.Empty
            };
        }

        public override string ToString()
        {
            return OptionId + " = " + Availability
                + (string.IsNullOrEmpty(Diagnostic) ? string.Empty : "（" + Diagnostic + "）");
        }
    }
}
