namespace RRPG.Event
{
    /// <summary>
    /// 执行阶段的可选参数（WP-Event-1C）。
    ///
    /// 只有一件：**一条 Outcome 允许的最大效果数**（Anti-Hang 防御）。
    /// 正常内容远小于默认值；超过即 <see cref="EventExecutionCode.InvalidRuntimeConfig"/>，
    /// 而不是无限循环或悄悄截断（截断会静默丢失内容，绝不允许）。
    /// </summary>
    public sealed class EventExecutionOptions
    {
        public const int DefaultMaxEffectsPerOutcome = 64;

        public int MaxEffectsPerOutcome { get; private set; }

        public EventExecutionOptions(int maxEffectsPerOutcome = DefaultMaxEffectsPerOutcome)
        {
            MaxEffectsPerOutcome = maxEffectsPerOutcome > 0 ? maxEffectsPerOutcome : DefaultMaxEffectsPerOutcome;
        }

        public static EventExecutionOptions Default { get { return new EventExecutionOptions(); } }
    }
}
