using RRPG.Event;
using RRPG.StaticConfig;

/// <summary>
/// **EventPresentationTexts（Event 表现文本的「唯一产品入口」）** —— WP-Event-1D Gate D3 §7。
///
/// 为什么需要它：Village Modal 与 Expedition Journey 卡是**两个表面**，但必须共用**同一个**模板渲染器
/// 与**同一条** Fail-Closed 语义。若两个表面各自实现字符串替换，就会出现
/// 「同一个 Event 在两个表面文本不一致 / 一边把占位符显示给玩家」的静默分叉。
///
/// 硬约束：
///   - 只经唯一 Core 渲染器 <see cref="EventTemplateRenderer"/>
///     （支持 `{RoleId.DisplayName}` 与 `{Context.Key}`，不支持其它形状）；
///   - **Fail Closed**：渲染失败 → false / 空串 + **可操作诊断**，绝不把原始占位符或半渲染文本交给 UI；
///   - **只读**：不写任何状态、不执行 Option、不 Resolve Node、不产生 Outcome（表现层不得有副作用）；
///   - 本类**不**决定「渲染什么内容」（内容身份来自 `EventId → EventPresentation`），只负责「怎么渲染」。
/// </summary>
public static class EventPresentationTexts
{
    /// <summary>
    /// 渲染 Title + Body（任一失败即整体失败：不显示半成品卡面）。
    /// </summary>
    public static bool TryRenderTexts(ResolvedEventInstance instance, EventPresentationDefinition presentation,
        EventPersonDisplayNameResolver resolver, out string title, out string body, out string diagnostic)
    {
        title = string.Empty;
        body = string.Empty;
        diagnostic = string.Empty;

        if (instance == null)
        {
            diagnostic = "没有已解析的事件实例：无法渲染事件面（不得用空文本冒充成功）。";
            return false;
        }

        if (presentation == null)
        {
            diagnostic = "事件 " + instance.EventId + " 没有表现配置（EventPresentation）：无法渲染事件面。";
            return false;
        }

        var titleResult = EventTemplateRenderer.Render(presentation.TitleTemplate, instance, resolver,
            "EventPresentation.TitleTemplate");
        if (!titleResult.Success)
        {
            diagnostic = titleResult.Diagnostic;
            return false;
        }

        var bodyResult = EventTemplateRenderer.Render(presentation.BodyTemplate, instance, resolver,
            "EventPresentation.BodyTemplate");
        if (!bodyResult.Success)
        {
            diagnostic = bodyResult.Diagnostic;
            return false;
        }

        title = titleResult.Text ?? string.Empty;
        body = bodyResult.Text ?? string.Empty;
        return true;
    }

    /// <summary>
    /// 渲染单个模板（Option 文案 / SubLabel / Outcome 结果文本）。
    /// 失败 → 返回**空串** + 诊断（绝不返回原始占位符；调用方决定是否 Fail Closed 整张卡面）。
    /// </summary>
    public static string RenderTemplate(ResolvedEventInstance instance, string template,
        EventPersonDisplayNameResolver resolver, string label, out string diagnostic)
    {
        diagnostic = string.Empty;
        if (string.IsNullOrEmpty(template)) return string.Empty;

        var rendered = EventTemplateRenderer.Render(template, instance, resolver, label);
        if (rendered.Success) return rendered.Text;

        diagnostic = rendered.Diagnostic;
        return string.Empty;
    }
}
