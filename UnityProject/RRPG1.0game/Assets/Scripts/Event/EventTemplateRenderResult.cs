using System;
using System.Collections.Generic;
using System.Text;

namespace RRPG.Event
{
    /// <summary>
    /// 模板渲染失败原因（WP-Event-1D §10/§11；V0.1 最小集合）。
    ///
    /// 产品语义：**Fail Closed** —— 渲染失败时**不得**把原始占位符（或空串）显示给玩家，
    /// 必须由上层给出明确诊断（例如不渲染这张卡面）。
    /// </summary>
    public enum EventTemplateRenderFailure
    {
        None = 0,

        /// <summary>占位符语法损坏（`{` 未闭合 / 嵌套 `{` / 空占位符）。</summary>
        MalformedPlaceholder = 1,

        /// <summary>占位符形状不受支持（只允许 `{RoleId.DisplayName}` 与 `{Context.Key}`）。</summary>
        UnsupportedPlaceholder = 2,

        /// <summary>RoleId 不在该实例的角色绑定中。</summary>
        RoleNotFound = 3,

        /// <summary>Role 存在但未绑定到具体 PersonId。</summary>
        RoleNotBound = 4,

        /// <summary>Person 显示名无法从正式长期身份数据解析（§12）。</summary>
        PersonNameUnresolved = 5,

        /// <summary>Context Key 不存在（String / Int / Person 三类都查过）。</summary>
        ContextKeyMissing = 6
    }

    /// <summary>事件模板渲染结果（成功 = 可显示文本；失败 = 明确原因 + 定位）。</summary>
    public sealed class EventTemplateRenderResult
    {
        public bool Success { get; internal set; }

        /// <summary>成功时的渲染文本；失败时为已构造的部分文本（**不得用于显示**）。</summary>
        public string Text { get; internal set; }

        public EventTemplateRenderFailure Failure { get; internal set; }

        /// <summary>可操作中文诊断。</summary>
        public string Diagnostic { get; internal set; }

        /// <summary>出错的原始占位符（如 `{Requester.DisplayName}`；无则 null）。</summary>
        public string FailedPlaceholder { get; internal set; }

        /// <summary>占位符在模板中的起始下标（-1 = 不适用）。</summary>
        public int FailureOffset { get; internal set; }

        internal static EventTemplateRenderResult Ok(string text)
        {
            return new EventTemplateRenderResult
            {
                Success = true,
                Text = text ?? string.Empty,
                Failure = EventTemplateRenderFailure.None,
                Diagnostic = string.Empty,
                FailedPlaceholder = null,
                FailureOffset = -1
            };
        }

        internal static EventTemplateRenderResult Fail(EventTemplateRenderFailure failure, string placeholder,
            int offset, string diagnostic)
        {
            return new EventTemplateRenderResult
            {
                Success = false,
                Text = null,
                Failure = failure,
                Diagnostic = diagnostic ?? string.Empty,
                FailedPlaceholder = placeholder,
                FailureOffset = offset
            };
        }

        public override string ToString()
        {
            return Success ? "OK: " + Text : "FAILED " + Failure + "：" + Diagnostic;
        }
    }

    /// <summary>
    /// Person 显示名解析委托（WP-Event-1D §12）：**必须**来自具体 Person 的正式长期身份数据。
    ///
    /// 由产品层（Village / Person Authority）提供；Event Core 保持纯逻辑，不知道 PlayerCollection。
    /// 解析不到 → 返回 false（**不得**用 RoleName / PersonId / Character template 冒充）。
    /// </summary>
    public delegate bool EventPersonDisplayNameResolver(string personId, out string displayName);
}
