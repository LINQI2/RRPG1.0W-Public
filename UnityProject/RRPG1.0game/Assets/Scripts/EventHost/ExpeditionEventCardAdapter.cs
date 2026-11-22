using System;
using System.Collections.Generic;
using RRPG.Event;
using RRPG.StaticConfig;
using UnityEngine;

/// <summary>
/// **ExpeditionEventCardAdapter（正式 Event → Journey 统一卡面配置）** —— WP-Event-1D Gate D2 §16 / §17。
///
/// 职责（且仅此）：把**已解析的正式事件**（`ResolvedEventInstance` + Event Config）
/// 投影成 <see cref="JourneyEventCardConfig">（唯一卡面模型）：
///   - Title / Body / Option 文案：`EventPresentation` + Event Config，经**唯一** `EventTemplateRenderer`；
///   - Option availability：`EventOptionEvaluator`（点击此刻的世界事实；Hide → 不出现，Disable → 显示但不可选）；
///   - Result 文本：Outcome.ResultTextTemplate 的渲染结果（来自 Active Event 状态，不重抽）；
///   - 插图：`EventPresentation.IllustrationAssetId` → 共享 VisualAsset Resolver；未配置 / 解析不到 →
///     类别级 fallback（**不再**用 NodeId → JourneyEventPresentationCatalog 决定事件身份 / 内容）。
///
/// Fail Closed：模板渲染失败 / Option 条件非法 / 配置缺失 → 返回 false + 可操作诊断，
/// **不**渲染一张「看起来能用」的半成品卡面，也绝不把原始占位符显示给玩家。
/// </summary>
public static class ExpeditionEventCardAdapter
{
    /// <summary>
    /// 构造「事件选项卡」：玩家在此选择 Option。
    /// <paramref name="categoryFallback"/> 由 View 提供（正式素材缺失时的类别级占位图）。
    /// </summary>
    public static bool TryBuildOptionCard(AdventureManager adventure, Sprite categoryFallback,
        out JourneyEventCardConfig config, out string diagnostic)
    {
        config = null;
        diagnostic = string.Empty;

        ActiveExpeditionEventState active;
        if (!ExpeditionEventFlow.TryGetActiveEvent(adventure, null, out active, out diagnostic)) return false;

        EventStaticCatalog catalog;
        string catalogDiagnostic;
        if (!VillageEventStaticConfigAdapter.TryBuild(out catalog, out catalogDiagnostic))
        {
            diagnostic = catalogDiagnostic;
            return false;
        }

        EventPresentationDefinition presentation;
        if (!VillageEventStaticConfigAdapter.TryGetPresentation(active.Instance.EventId, out presentation)
            || presentation == null)
        {
            diagnostic = "事件 " + active.Instance.EventId
                + " 在当前静态配置中没有表现配置（EventPresentation）：无法渲染正式事件卡。";
            return false;
        }

        string title;
        string body;
        if (!TryRenderTexts(adventure, active.Instance, presentation, out title, out body, out diagnostic))
            return false;

        var world = new ExpeditionEventWorldView(adventure, active.NodeId);
        EventRuntimeBridge.EnsureInitialized();

        var options = new List<JourneyEventCardOption>();
        var definitions = catalog.GetOptions(active.Instance.EventId);
        for (int i = 0; i < definitions.Count; i++)
        {
            var definition = definitions[i];
            if (definition == null) continue;

            var availability = EventOptionEvaluator.Evaluate(active.Instance, definition.OptionId, catalog,
                world, EventRuntimeBridge.World);

            if (availability.Availability == EventOptionAvailability.Hidden) continue;

            if (availability.Availability == EventOptionAvailability.Invalid)
            {
                diagnostic = "选项 " + definition.OptionId + " 的可用性无法求值（配置非法，Fail Closed）："
                    + availability.Diagnostic;
                return false;
            }

            string label;
            string labelDiagnostic;
            label = EventPresentationTexts.RenderTemplate(active.Instance, definition.LabelTemplate,
                ExpeditionEventFlow.PersonDisplayNameResolver(adventure), "Option.LabelTemplate",
                out labelDiagnostic);
            if (!string.IsNullOrEmpty(definition.LabelTemplate) && !string.IsNullOrEmpty(labelDiagnostic))
            {
                // D3 §8：Option 文案渲染失败 → Fail Closed（不显示占位符、不自动选择第一个选项）。
                diagnostic = "事件 " + active.Instance.EventId + " 的选项 " + definition.OptionId
                    + " 文案渲染失败（Fail Closed，不显示占位符）：" + labelDiagnostic;
                return false;
            }

            // SubLabel 是可选展示项：渲染失败 → 省略该行（不显示原始占位符，也不影响选项本体）。
            string subLabel = RenderTemplate(adventure, active.Instance, definition.SubLabelTemplate,
                "Option.SubLabelTemplate");

            if (availability.Availability == EventOptionAvailability.Disabled)
            {
                subLabel = string.IsNullOrEmpty(subLabel) ? "（当前不可选）" : subLabel + " · 当前不可选";
            }

            bool selectable = availability.Availability == EventOptionAvailability.Available;

            // WP-Event-1 Producer Acceptance Repair（Issue C）：点击前只读可行性预测 ——
            // 若确定性 Outcome 已经确定、且 Preflight 能证明该 Option 当前**必然**无法执行（例如结算补给不足），
            // 直接把 Option 置灰 + 显示短原因（不再让玩家靠「点击后报错」才发现）。
            // 只读：不 Commit / 不写 History / 不改 Outcome / 不改 RNG；条件变化后重新构造卡面即可恢复可用。
            if (selectable)
            {
                EventOptionFeasibilityPreview preview;
                if (ExpeditionEventFlow.TryPreviewOptionFeasibility(adventure, definition.OptionId, out preview)
                    && preview.Applicable && !preview.Feasible)
                {
                    selectable = false;
                    subLabel = preview.PlayerReason;
                }
            }

            options.Add(new JourneyEventCardOption
            {
                Label = label,
                SubLabel = subLabel,
                OptionId = definition.OptionId,
                Role = JourneyEventOptionRole.EventOption,
                Interactable = selectable
            });
        }

        if (options.Count == 0)
        {
            diagnostic = "事件 " + active.Instance.EventId
                + " 的当前可用选项为空（全部 Hide / 无 Option）：拒绝渲染无法推进的卡面。";
            return false;
        }

        config = new JourneyEventCardConfig
        {
            Kind = JourneyEncounterKind.Event,
            Illustration = ResolveEventIllustration(presentation, categoryFallback),
            Title = title,
            Body = body,
            Options = options
        };
        return true;
    }

    /// <summary>
    /// 构造「结果卡」：Option 已成功执行、无 StartBattle 时的玩家可见结果（§15）。
    /// 文本来自 Active Event 状态中已冻结的 Outcome 渲染结果（**不重抽** Outcome）；
    /// 玩家点「继续」后才结算当前 Node。
    /// </summary>
    public static bool TryBuildResultCard(AdventureManager adventure, Sprite categoryFallback,
        out JourneyEventCardConfig config, out string diagnostic)
    {
        config = null;
        diagnostic = string.Empty;

        ActiveExpeditionEventState active;
        if (!ExpeditionEventFlow.TryGetActiveEvent(adventure, null, out active, out diagnostic)) return false;

        if (active.Phase != ExpeditionEventPhase.AwaitingResultContinue)
        {
            diagnostic = "活动事件阶段为 " + active.Phase + "：当前没有待确认的事件结果。";
            return false;
        }

        EventPresentationDefinition presentation;
        if (!VillageEventStaticConfigAdapter.TryGetPresentation(active.Instance.EventId, out presentation)
            || presentation == null)
        {
            diagnostic = "事件 " + active.Instance.EventId + " 缺少表现配置：无法渲染结果卡。";
            return false;
        }

        string title;
        string ignoredBody;
        if (!TryRenderTexts(adventure, active.Instance, presentation, out title, out ignoredBody, out diagnostic))
            return false;

        // 结果正文：优先 Outcome.ResultTextTemplate 的渲染结果；为空时回到事件正文（玩家仍有上下文）。
        string body = !string.IsNullOrEmpty(active.LastResultText) ? active.LastResultText : ignoredBody;

        config = new JourneyEventCardConfig
        {
            Kind = JourneyEncounterKind.Event,
            Illustration = ResolveEventIllustration(presentation, categoryFallback),
            Title = title,
            Body = body,
            Options = new List<JourneyEventCardOption>
            {
                new JourneyEventCardOption
                {
                    Label = "继续",
                    SubLabel = null,
                    OptionId = null,
                    Role = JourneyEventOptionRole.ContinueResult,
                    Interactable = true
                }
            }
        };
        return true;
    }

    // ==================== 内部：文本 / 插图 ====================

    private static bool TryRenderTexts(AdventureManager adventure, ResolvedEventInstance instance,
        EventPresentationDefinition presentation, out string title, out string body, out string diagnostic)
    {
        // WP-Event-1D Gate D3 §7：与 Village Modal 共用**同一个**产品表现文本入口（内部唯一 Core 渲染器）。
        return EventPresentationTexts.TryRenderTexts(instance, presentation,
            ExpeditionEventFlow.PersonDisplayNameResolver(adventure), out title, out body, out diagnostic);
    }

    private static string RenderTemplate(AdventureManager adventure, ResolvedEventInstance instance,
        string template, string label)
    {
        string ignored;
        return EventPresentationTexts.RenderTemplate(instance, template,
            ExpeditionEventFlow.PersonDisplayNameResolver(adventure), label, out ignored);
    }

    // ==================== 插图（§3 / §4：唯一正式链） ====================

    /// <summary>最近一次插图解析的可操作诊断（成功 / 有意不配置时为空串）。</summary>
    public static string LastIllustrationDiagnostic { get; private set; }

    /// <summary>最近一次插图解析的来源（`visualasset:&lt;id&gt;` / `unconfigured` / `category:&lt;fallback&gt;`）。</summary>
    public static string LastIllustrationSource { get; private set; }

    /// <summary>
    /// **正式 Event 插图的唯一产品入口**（D3 §3）：
    ///
    /// <code>
    /// EventId → EventPresentation.IllustrationAssetId → 共享 VisualAsset Authority → Sprite
    /// </code>
    ///
    /// 硬约束（D3 §2 / §4）：
    ///   - **绝不**使用 `NodeId → JourneyEventPresentationCatalog` 决定事件插图 / 身份；
    ///   - `IllustrationAssetId` 为空 = **有意不配置插图**（Schema `optional`）→ 返回类别级占位图，
    ///     诊断为空、来源 = `unconfigured`（这不是错误）；
    ///   - `IllustrationAssetId` 非空但**无法解析**（VisualAsset 权威不可用 / Id 不存在 / Sprite 未生成）
    ///     → **明确诊断** + 返回类别级占位图，**绝不**悄悄换成别的图片（不换 NodeId 图、不取「第一张事件图」）。
    /// </summary>
    public static Sprite ResolveEventIllustration(EventPresentationDefinition presentation, Sprite categoryFallback)
    {
        LastIllustrationDiagnostic = string.Empty;
        LastIllustrationSource = "unconfigured";

        if (presentation == null || string.IsNullOrEmpty(presentation.IllustrationAssetId))
        {
            // 有意不配置插图（合法状态）：UI 用类别级占位图；不产生诊断噪音。
            LastIllustrationSource = categoryFallback != null ? "category:unconfigured" : "none:unconfigured";
            return categoryFallback;
        }

        string visualAssetId = presentation.IllustrationAssetId;

        VisualAssetCatalog catalog;
        string catalogDiagnostic;
        if (!StaticConfigRegistry.TryGetVisualAssets(out catalog, out catalogDiagnostic) || catalog == null)
        {
            LastIllustrationDiagnostic = "Event " + presentation.EventId + " 声明了 IllustrationAssetId="
                + visualAssetId + "，但共享 VisualAsset 权威不可用"
                + (string.IsNullOrEmpty(catalogDiagnostic) ? string.Empty : "（" + catalogDiagnostic + "）")
                + "：本次不显示该插图（**不得**静默换成 NodeId 图或其它事件图），请重新运行 RRPG_一键导表.cmd。";
            LastIllustrationSource = categoryFallback != null ? "category:authority-unavailable" : "none:authority-unavailable";
            return categoryFallback;
        }

        VisualAssetDefinition asset;
        if (!catalog.TryGetById(visualAssetId, out asset) || asset == null)
        {
            LastIllustrationDiagnostic = "Event " + presentation.EventId + " 声明的 IllustrationAssetId="
                + visualAssetId + " 在共享 VisualAsset 表中不存在"
                + "（" + VisualAssetCatalog.MissingIdDiagnosticPrefix + visualAssetId + "）："
                + "本次不显示该插图，**不得**静默换成其它图；请在 RRPG_VisualAssets.xlsx 补该行并重新导表。";
            LastIllustrationSource = categoryFallback != null ? "category:unresolved" : "none:unresolved";
            return categoryFallback;
        }

        if (asset.Sprite == null)
        {
            LastIllustrationDiagnostic = "Event " + presentation.EventId + " 的 IllustrationAssetId="
                + visualAssetId + " 已登记，但 managed Sprite 未生成（SourceFile="
                + (asset.SourceFile ?? "<null>") + "）：本次不显示该插图，**不得**静默换成其它图；"
                + "请确认源图存在并重新运行 RRPG_一键导表.cmd。";
            LastIllustrationSource = categoryFallback != null ? "category:sprite-missing" : "none:sprite-missing";
            return categoryFallback;
        }

        LastIllustrationSource = "visualasset:" + visualAssetId;
        return asset.Sprite;
    }
}

