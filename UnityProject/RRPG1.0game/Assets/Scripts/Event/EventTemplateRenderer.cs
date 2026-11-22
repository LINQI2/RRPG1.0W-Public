using System;
using System.Text;

namespace RRPG.Event
{
    /// <summary>
    /// **EventTemplateRenderer（最小事件模板渲染器）** —— WP-Event-1D §10/§11（由 D3 提前到 D1）。
    ///
    /// V0.1 只支持两种占位符（与 1A 的 `EventValidator` / `DAT044` 词表一致）：
    ///   - `{RoleId.DisplayName}`（例如 `{Requester.DisplayName}`）；
    ///   - `{Context.Key}`（例如 `{Context.ObservedCount}`）。
    ///
    /// **明确不支持**（出现即渲染失败）：if / 函数调用 / 数学表达式 / 嵌套占位符 / Lua / C# / 任意脚本。
    ///
    /// 失败语义（§11 / §12，Fail Closed）：见 <see cref="EventTemplateRenderFailure"/>；
    /// 绝不把原始占位符原样输出，也绝不用空串掩盖配置问题。
    /// </summary>
    public static partial class EventTemplateRenderer
    {
        /// <summary>
        /// 渲染模板。空 / null 模板 → 成功且文本为空（由调用方决定 UI fallback，§19）。
        /// </summary>
        /// <param name="template">模板文本（可为 null / 空）。</param>
        /// <param name="instance">已解析事件实例（角色绑定 + Context 快照）。</param>
        /// <param name="personNameResolver">Person 显示名解析（产品提供；null → 任何 Role 占位符都会失败）。</param>
        /// <param name="templateLabel">诊断用标签（如 "TitleTemplate"）。</param>
        public static EventTemplateRenderResult Render(string template, ResolvedEventInstance instance,
            EventPersonDisplayNameResolver personNameResolver, string templateLabel = null)
        {
            string label = string.IsNullOrEmpty(templateLabel) ? "Template" : templateLabel;

            if (string.IsNullOrEmpty(template)) return EventTemplateRenderResult.Ok(string.Empty);

            if (instance == null)
            {
                return EventTemplateRenderResult.Fail(EventTemplateRenderFailure.RoleNotFound, null, -1,
                    label + " 渲染失败：事件实例为 null（没有角色绑定与 Context 可读）。");
            }

            var builder = new StringBuilder(template.Length + 16);
            int index = 0;

            while (index < template.Length)
            {
                char current = template[index];

                if (current == '}')
                {
                    return EventTemplateRenderResult.Fail(EventTemplateRenderFailure.MalformedPlaceholder, null,
                        index, label + " 渲染失败：出现多余的 '}'（位置 " + index + "）。");
                }

                if (current != '{')
                {
                    builder.Append(current);
                    index++;
                    continue;
                }

                int close = template.IndexOf('}', index + 1);
                if (close < 0)
                {
                    return EventTemplateRenderResult.Fail(EventTemplateRenderFailure.MalformedPlaceholder,
                        template.Substring(index), index,
                        label + " 渲染失败：占位符 '{' 未闭合（位置 " + index + "）。");
                }

                string body = template.Substring(index + 1, close - index - 1);
                string placeholder = template.Substring(index, close - index + 1);
                string trimmed = body.Trim();

                if (trimmed.Length == 0)
                {
                    return EventTemplateRenderResult.Fail(EventTemplateRenderFailure.MalformedPlaceholder,
                        placeholder, index, label + " 渲染失败：空占位符 '{}'（位置 " + index + "）。");
                }

                if (trimmed.IndexOf('{') >= 0 || trimmed.IndexOf('}') >= 0)
                {
                    return EventTemplateRenderResult.Fail(EventTemplateRenderFailure.MalformedPlaceholder,
                        placeholder, index,
                        label + " 渲染失败：占位符内出现嵌套大括号（位置 " + index + "）。");
                }

                string value;
                string diagnostic;
                if (!TryResolvePlaceholder(trimmed, instance, personNameResolver, out value, out diagnostic))
                {
                    return EventTemplateRenderResult.Fail(ClassifyFailure(trimmed, diagnostic), placeholder, index,
                        label + " 渲染失败（" + placeholder + "）：" + diagnostic);
                }

                builder.Append(value);
                index = close + 1;
            }

            return EventTemplateRenderResult.Ok(builder.ToString());
        }
    }
}
