using System;

namespace RRPG.Event
{
    /// <summary>
    /// Resolver 的可选求解参数（WP-Event-1B §11：最大搜索必须有 Anti-Hang 保护）。
    ///
    /// 语义：
    ///   - <see cref="MaxSearchNodes"/> 是**整个一次 Resolve 调用**内允许评估的
    ///     「(Role, 候选 Person) 组合」次数上限；超过即返回
    ///     <see cref="EventResolutionCode.SearchLimitExhausted"/>（**不是** NoLegalRoleBinding）；
    ///   - 默认值刻意宽松（正常内容不可能接近），防止把真实内容误判为超限；
    ///   - 不提供「最大尝试 Event 数」：Event 集合来自配置，永远全量评估完毕（保证 Trace 完整）。
    /// </summary>
    public sealed class EventResolverOptions
    {
        /// <summary>默认搜索上限（宽松；只防卡死，不做内容节流）。</summary>
        public const int DefaultMaxSearchNodes = 20000;

        public int MaxSearchNodes { get; private set; }

        public EventResolverOptions(int maxSearchNodes = DefaultMaxSearchNodes)
        {
            MaxSearchNodes = maxSearchNodes > 0 ? maxSearchNodes : DefaultMaxSearchNodes;
        }

        /// <summary>默认参数（每次调用都可复用；本类不可变）。</summary>
        public static EventResolverOptions Default { get { return new EventResolverOptions(); } }
    }
}
