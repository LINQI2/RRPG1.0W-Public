using System;
using System.Collections.Generic;
using RRPG.Event;
using RRPG.StaticConfig;

/// <summary>
/// **VillageEventFlow（Village 统一事件产品流）** —— WP-Event-1D Gate D1（§4 / §13 / §14 / §21）。
///
/// 职责（且仅此）：把 Village 侧的**真实产品动作**接进 Unified Event Core：
/// <code>
/// SocialFactLedger（已发生事实）
///   → Recognition（计数达阈值）
///   → EventResolutionRequest（TriggerType / TriggerKey / ResolutionSeed / Context）
///   → EventResolver
///   → EventRuntimeBridge.PendingVillageEvent
///   → EventOptionEvaluator / EventOptionExecutor（Option → Outcome → Effect Transaction）
///   → VillageEventEffectGateway（真实 Authority 写入）
/// </code>
///
/// 硬约束：
///   - **不拥有**任何状态：世界状态属 <c>EventRuntimeBridge</c>，Village Authority 仍属 Village 域，
///     静态内容仍属 Static Config；本类只做「投影 + 编排 + 诊断」；
///   - **不再创建 Legacy Pending**（§40）：Village 正式 Recognition 路径绝不给
///     `VillageSocialResponseLog` 建立新的 pending / fired；Legacy 只保留旧档收敛能力；
///   - **确定性**：`TriggerKey` 由真实事实稳定派生，`ResolutionSeed` 由 TriggerKey 稳定派生
///     （不使用 Guid / 时间 / 随机数），因此同一事实重复求解得到同一 `EventInstanceId`；
///   - **Fail Closed**：配置不可用 / 条件不可求值 / 效果 preflight 失败时不建 pending、不谎报成功，
///     一律给出可操作中文诊断（`LastDiagnostic`）。
/// </summary>
public static class VillageEventFlow
{
    // ==================== Recognition Context 键（与 Event 内容侧冻结口径一致） ====================

    /// <summary>Village 正式触发时机（`RRPG_Events.xlsx / EventTrigger.TriggerType`）。</summary>
    public const string RecognitionTriggerType = EventTriggerTypes.VillageRecognitionRaised;

    /// <summary>Context：本次识别针对的人物（`ContextPerson` 角色的 SourceKey）。</summary>
    public const string RecognitionPersonKey = "RecognitionPerson";

    /// <summary>Context：识别类型（例如 `TemporaryDraftBuildup`）。</summary>
    public const string RecognitionTypeKey = "RecognitionType";

    /// <summary>Context：该识别观察到的年内次数。</summary>
    public const string ObservedCountKey = "ObservedCount";

    // ==================== Legacy 兼容映射（旧档 Pending 收敛；Test / Not Frozen） ====================

    /// <summary>Legacy PatternKey（旧 V3B `TemporaryDraftBuildup`）。</summary>
    public const string LegacyBuildupPatternKey = "TemporaryDraftBuildup";

    /// <summary>Legacy 已知 Pending 对应的正式 EventId（旧 Pending 是**已发生事实**，因此按映射直接重建）。</summary>
    public const string LegacyBuildupEventId = "evt_test_village_temp_draft";

    /// <summary>Legacy 已知 Pending 对应的 RoleId。</summary>
    public const string LegacyBuildupRoleId = "role_test_village_requester";

    private const string LegacyBuildupInstancePrefix = "evtinst_legacy|";

    // ==================== 运行时（Runtime Only；不进 Save） ====================

    private static ExpeditionEventSessionState _sessionState;

    /// <summary>最近一次编排的可操作诊断（成功后为空串；不刷 Console）。</summary>
    public static string LastDiagnostic { get; private set; }

    /// <summary>最近一次 Resolver 结果码文本（`ConfigUnavailable` / `NoCandidateEvent` / `Success` …）。</summary>
    public static string LastResolutionCode { get; private set; }

    /// <summary>
    /// Event 求解所需的**会话历史**输入（Resolver 契约要求非 null）。
    /// Village V0.1 事件不使用 `ExpeditionNodes` 冷却、也不写 Session History（会话历史不持久化），
    /// 本对象只保证契约成立；Load / 重开后重建。
    /// </summary>
    public static ExpeditionEventSessionState SessionState
    {
        get
        {
            if (_sessionState == null) _sessionState = new ExpeditionEventSessionState("village_event_session");
            return _sessionState;
        }
    }

    /// <summary>
    /// 重开世界 / Load 用：重建会话输入与诊断（**只影响 Runtime 输入**，不触碰 Event World 状态、不改存档）。
    /// 测试亦复用本入口（避免出现「只给测试用」的第二套重置路径）。
    /// </summary>
    public static void ResetSessionState()
    {
        _sessionState = null;
        LastDiagnostic = string.Empty;
        LastResolutionCode = string.Empty;
    }

    // ==================== 稳定派生（确定性；不得使用 Guid / 时间） ====================

    /// <summary>
    /// 本次 Recognition 的稳定身份：`village-recognition|&lt;RecognitionType&gt;|&lt;PersonId&gt;|year:&lt;Year&gt;`。
    /// 同一个事实（同一人物 / 同一识别类型 / 同一年）永远得到同一个键 → 同一个 EventInstanceId。
    /// </summary>
    public static string BuildRecognitionTriggerKey(string recognitionType, string personId, int year)
    {
        return "village-recognition|" + (recognitionType ?? string.Empty) + "|" + (personId ?? string.Empty)
            + "|year:" + year;
    }

    /// <summary>
    /// 由 TriggerKey 稳定派生 ResolutionSeed（FNV-1a 32 位；只取非负值便于诊断与存档可读）。
    /// 唯一随机来源仍是 Seed 本身 —— 不使用 `UnityEngine.Random` / 时间 / Guid。
    /// </summary>
    public static int DeriveResolutionSeed(string triggerKey)
    {
        if (string.IsNullOrEmpty(triggerKey)) return 0;

        unchecked
        {
            const uint offsetBasis = 2166136261u;
            const uint prime = 16777619u;

            uint hash = offsetBasis;
            for (int i = 0; i < triggerKey.Length; i++)
            {
                hash ^= triggerKey[i];
                hash *= prime;
            }
            return (int)(hash & 0x7FFFFFFFu);
        }
    }

    // ==================== 1) Recognition → Event Pending ====================

    /// <summary>
    /// **纯求解**（不写 Pending、不改任何状态）：把一次 Recognition 交给 Event Resolver。
    /// 返回 null = 静态配置不可用（环境问题，不是「没有事件」）；否则结果码见 `EventResolutionCode`。
    /// </summary>
    public static EventResolutionResult ResolveRecognition(string personId, string recognitionType,
        int observedCount, int year)
    {
        EventStaticCatalog catalog;
        string catalogDiagnostic;
        if (!VillageEventStaticConfigAdapter.TryBuild(out catalog, out catalogDiagnostic))
        {
            LastResolutionCode = EventResolutionCode.ConfigUnavailable.ToString();
            LastDiagnostic = catalogDiagnostic;
            return null;
        }

        EventRuntimeBridge.EnsureInitialized();

        string triggerKey = BuildRecognitionTriggerKey(recognitionType, personId, year);
        var context = new EventContext()
            .SetPerson(RecognitionPersonKey, personId)
            .SetString(RecognitionTypeKey, recognitionType)
            .SetInt(ObservedCountKey, observedCount);

        var request = new EventResolutionRequest(RecognitionTriggerType, triggerKey,
            DeriveResolutionSeed(triggerKey), context);

        var result = EventResolver.Resolve(request, catalog, new VillageEventWorldView(),
            EventRuntimeBridge.World, SessionState);

        LastResolutionCode = result.Code.ToString();
        LastDiagnostic = result.Diagnostic;
        return result;
    }

    /// <summary>
    /// **正式 Recognition 入口**（Village 产品路径唯一入口）：
    /// 求解成功 → 建立 `PendingVillageEvent`；求解「没有候选事件」→ 正常的 Gameplay 结果（不建任何 Pending）。
    ///
    /// **绝不**创建 Legacy Pending（§40）。
    /// </summary>
    public static bool RaiseRecognition(string personId, string recognitionType, int observedCount, int year,
        out string diagnostic)
    {
        diagnostic = string.Empty;

        if (string.IsNullOrEmpty(personId))
        {
            diagnostic = "Recognition 缺少人物 Id：无法建立事件。";
            LastDiagnostic = diagnostic;
            return false;
        }

        var result = ResolveRecognition(personId, recognitionType, observedCount, year);
        if (result == null)
        {
            diagnostic = LastDiagnostic;
            return false;
        }

        if (!result.Success)
        {
            diagnostic = result.Code + "：" + result.Diagnostic;
            LastDiagnostic = diagnostic;
            return false;
        }

        string pendingDiagnostic;
        if (!EventRuntimeBridge.SetPendingVillageEvent(result.Instance, out pendingDiagnostic))
        {
            diagnostic = pendingDiagnostic;
            LastDiagnostic = diagnostic;
            return false;
        }

        LastDiagnostic = string.Empty;
        return true;
    }

    /// <summary>便捷重载（诊断只落在 `LastDiagnostic`）。</summary>
    public static bool RaiseRecognition(string personId, string recognitionType, int observedCount, int year)
    {
        string ignored;
        return RaiseRecognition(personId, recognitionType, observedCount, year, out ignored);
    }

    // ==================== 2) Option / Outcome / Effect（真实执行） ====================

    /// <summary>
    /// **正式执行入口**（给定实例；供 UI 与契约测试使用）。
    /// 成功 = Preflight 通过 + Commit 成功 + History 已记录 + 实例已标记 Completed（exactly-once）。
    /// 失败 = 明确结果码 + 诊断；**零 mutation**（不写 History、不标 Completed、不清 Pending）。
    /// </summary>
    public static bool TryExecuteOption(ResolvedEventInstance instance, string optionId,
        out EventOptionExecutionResult result)
    {
        result = null;

        if (instance == null)
        {
            LastDiagnostic = "没有已解析的事件实例：无法执行选项。";
            return false;
        }

        EventStaticCatalog catalog;
        string catalogDiagnostic;
        if (!VillageEventStaticConfigAdapter.TryBuild(out catalog, out catalogDiagnostic))
        {
            LastDiagnostic = catalogDiagnostic;
            return false;
        }

        EventRuntimeBridge.EnsureInitialized();

        var outcome = EventOptionExecutor.Execute(instance, optionId, catalog, new VillageEventWorldView(),
            EventRuntimeBridge.World, SessionState, EventRuntimeBridge.Execution,
            new VillageEventEffectGateway());

        result = outcome;
        LastDiagnostic = outcome.Success ? string.Empty : outcome.Code + "：" + outcome.Diagnostic;
        return outcome.Success;
    }

    /// <summary>
    /// **待处理事件执行入口**（UI 用）：执行成功后清除 Pending；失败时 Pending **保留**（Fail Closed）。
    /// </summary>
    public static bool TryExecutePendingOption(string optionId, out EventOptionExecutionResult result)
    {
        result = null;

        var instance = EventRuntimeBridge.PendingVillageEvent;
        if (instance == null)
        {
            LastDiagnostic = "当前没有待处理的 Village Event。";
            return false;
        }

        if (!VillageRuntimeBridge.TryBeginBlockingEventOption(instance.EventInstanceId))
        { LastDiagnostic = "WorldBusy or stale blocking event"; return false; }
        bool handled = false;
        try
        {
            if (!TryExecuteOption(instance, optionId, out result)) return false;
            EventRuntimeBridge.ClearPendingVillageEvent();
            handled = true;
            return true;
        }
        finally { VillageRuntimeBridge.EndBlockingEventOption(instance.EventInstanceId, handled); }
    }

    public static string LegacyAdvanceInstanceId(VillagePendingResponse pending)
        => pending == null ? null : "legacy:" + pending.EventId + ":" + pending.PatternKey + ":" + pending.PersonId + ":" + pending.Year;

    /// <summary>Existing PauseOnOpen classification, without rerolling or mutating Pending.</summary>
    public static bool TryGetAdvanceBlockingInteraction(out string eventInstanceId, out string diagnostic)
    {
        diagnostic = null;
        var instance = EventRuntimeBridge.PendingVillageEvent;
        eventInstanceId = instance?.EventInstanceId;
        if (instance != null)
        {
            if (!VillageEventStaticConfigAdapter.TryGetPresentation(instance.EventId, out var presentation) || presentation == null)
            { diagnostic = "MissingEventPresentation:" + instance.EventId; return true; }
            var urgency = string.Equals(presentation.Urgency, "Ignorable", StringComparison.Ordinal)
                ? VillageSocialUrgency.Ignorable : VillageSocialUrgency.Timely;
            return VillageTestSocialPatterns.ShouldPauseOnOpen(presentation.PauseOnOpen, urgency);
        }
        var legacy = VillageRuntimeBridge.GetPendingSocialResponse();
        if (legacy == null) return false;
        eventInstanceId = LegacyAdvanceInstanceId(legacy);
        var legacyUrgency = VillageTestSocialPatterns.ResolveUrgency(legacy.PatternKey);
        if (legacyUrgency == VillageSocialUrgency.Ignorable) return false;
        var view = UnityEngine.Object.FindObjectOfType<VillageSocialEventUI>(true);
        if (view == null) { diagnostic = "MissingLegacyPauseConfiguration"; return true; }
        return VillageTestSocialPatterns.ShouldPauseOnOpen(view.pauseOnOpen, legacyUrgency);
    }

    /// <summary>
    /// **只算 Outcome**（不写任何状态）。与正式执行使用同一选择函数 → 预览结果 = 正式结果。
    /// </summary>
    public static EventOutcomeDefinition PreviewOutcome(ResolvedEventInstance instance, string optionId)
    {
        if (instance == null || string.IsNullOrEmpty(optionId)) return null;

        EventStaticCatalog catalog;
        string catalogDiagnostic;
        if (!VillageEventStaticConfigAdapter.TryBuild(out catalog, out catalogDiagnostic)) return null;

        return EventOptionExecutor.PreviewOutcome(instance, optionId, catalog);
    }

    // ==================== 3) 表现（模板渲染 → 只读视图） ====================

    /// <summary>
    /// **Person 显示名解析（默认实现）**：只查正式长期人物真相源（`PlayerCollection`），
    /// 找不到 → false（绝不用 RoleName / PersonId 冒充显示名）。
    /// </summary>
    public static bool TryResolvePersonDisplayName(string personId, out string displayName)
    {
        displayName = null;
        if (string.IsNullOrEmpty(personId)) return false;

        var source = VillageRuntimeBridge.SimulationPersonSource;
        if (source == null) return false;

        var person = source.FindPersonByPersonId(personId);
        if (person == null || string.IsNullOrEmpty(person.DisplayName)) return false;

        displayName = person.DisplayName;
        return true;
    }

    /// <summary>
    /// 渲染一张事件面的标题 / 正文（来自 `EventPresentation` 配置，经**唯一** `EventTemplateRenderer`）。
    /// Fail Closed：模板渲染失败 → false + 可操作诊断（**不**把占位符原样显示给玩家）。
    /// </summary>
    public static bool TryRenderPendingTexts(ResolvedEventInstance instance,
        EventPersonDisplayNameResolver resolver, out string title, out string body, out string diagnostic)
    {
        title = string.Empty;
        body = string.Empty;
        diagnostic = string.Empty;

        if (instance == null)
        {
            diagnostic = "没有已解析的事件实例：无法渲染事件面。";
            return false;
        }

        EventPresentationDefinition presentation;
        if (!VillageEventStaticConfigAdapter.TryGetPresentation(instance.EventId, out presentation))
        {
            diagnostic = "事件 " + instance.EventId + " 在当前静态配置中没有表现配置（EventPresentation）。";
            return false;
        }

        // WP-Event-1D Gate D3 §7：Village Modal 与 Expedition Journey 卡**共用同一个产品表现文本入口**
        //（内部只经唯一 EventTemplateRenderer）；两个表面不得各自实现字符串替换。
        return EventPresentationTexts.TryRenderTexts(instance, presentation,
            resolver ?? TryResolvePersonDisplayName, out title, out body, out diagnostic);
    }

    /// <summary>默认解析器重载。</summary>
    public static bool TryRenderPendingTexts(ResolvedEventInstance instance, out string title, out string body,
        out string diagnostic)
    {
        return TryRenderPendingTexts(instance, null, out title, out body, out diagnostic);
    }

    /// <summary>
    /// 构造该实例的**选项视图列表**（顺序 = 配置 `(SortOrder, OptionId)`；可用性来自 `EventOptionEvaluator`）。
    /// UI 只渲染这些行，不得自行判断条件。
    /// </summary>
    public static List<VillageEventOptionView> BuildOptionViews(ResolvedEventInstance instance,
        EventPersonDisplayNameResolver resolver, out string diagnostic)
    {
        diagnostic = string.Empty;
        var views = new List<VillageEventOptionView>();

        if (instance == null)
        {
            diagnostic = "没有已解析的事件实例：无法求值选项。";
            return views;
        }

        EventStaticCatalog catalog;
        string catalogDiagnostic;
        if (!VillageEventStaticConfigAdapter.TryBuild(out catalog, out catalogDiagnostic))
        {
            diagnostic = catalogDiagnostic;
            return views;
        }

        EventRuntimeBridge.EnsureInitialized();

        var world = new VillageEventWorldView();
        var nameResolver = resolver ?? TryResolvePersonDisplayName;
        var options = catalog.GetOptions(instance.EventId);

        if (options.Count == 0)
        {
            diagnostic = "事件 " + instance.EventId + " 没有配置任何选项（Option）。";
            return views;
        }

        for (int i = 0; i < options.Count; i++)
        {
            var option = options[i];
            if (option == null || string.IsNullOrEmpty(option.OptionId)) continue;

            var availability = EventOptionEvaluator.Evaluate(instance, option.OptionId, catalog, world,
                EventRuntimeBridge.World);

            // Hidden：整条隐藏（UI 不得显示）——不进视图列表
            if (availability.Availability == EventOptionAvailability.Hidden) continue;

            var view = new VillageEventOptionView
            {
                OptionId = option.OptionId,
                SortOrder = option.SortOrder,
                Availability = availability.Availability,
                IsSelectable = availability.IsSelectable,
                SubLabel = string.Empty,
                Diagnostic = availability.Diagnostic
            };

            var labelResult = EventTemplateRenderer.Render(option.LabelTemplate, instance, nameResolver,
                "OptionLabelTemplate");
            if (labelResult.Success)
            {
                view.Label = labelResult.Text ?? string.Empty;
            }
            else
            {
                // Fail Closed：不显示半渲染文案，也不静默隐藏配置问题。
                view.Label = "（选项文案无法渲染）";
                view.IsSelectable = false;
                view.Availability = EventOptionAvailability.Invalid;
                view.Diagnostic = labelResult.Diagnostic;
            }

            if (!string.IsNullOrEmpty(option.SubLabelTemplate))
            {
                var subResult = EventTemplateRenderer.Render(option.SubLabelTemplate, instance, nameResolver,
                    "OptionSubLabelTemplate");
                if (subResult.Success)
                {
                    view.SubLabel = subResult.Text ?? string.Empty;
                }
                else
                {
                    view.SubLabel = string.Empty;
                    view.IsSelectable = false;
                    view.Availability = EventOptionAvailability.Invalid;
                    view.Diagnostic = subResult.Diagnostic;
                }
            }

            views.Add(view);
        }

        if (views.Count == 0) diagnostic = "事件 " + instance.EventId + " 当前没有任何可见选项（全部 Hidden）。";
        return views;
    }

    /// <summary>默认解析器重载。</summary>
    public static List<VillageEventOptionView> BuildOptionViews(ResolvedEventInstance instance, out string diagnostic)
    {
        return BuildOptionViews(instance, null, out diagnostic);
    }

    /// <summary>
    /// **结果文案**：优先使用配置的 `EventOutcome.ResultTextTemplate`（经唯一渲染器）；
    /// 内容侧未配置 / 渲染失败 → 通用结果行（含 EventId / OutcomeId / 应用效果条数，可追溯）。
    /// </summary>
    public static string RenderResultText(ResolvedEventInstance instance, EventOptionExecutionResult result,
        string optionLabel, EventPersonDisplayNameResolver resolver)
    {
        string fallback = BuildGenericResultText(result, optionLabel);
        if (result == null) return fallback;

        EventStaticCatalog catalog;
        string catalogDiagnostic;
        if (!VillageEventStaticConfigAdapter.TryBuild(out catalog, out catalogDiagnostic) || catalog == null)
        {
            return fallback;
        }

        EventOutcomeDefinition outcome;
        if (!catalog.TryGetOutcome(result.OutcomeId, out outcome) || outcome == null) return fallback;
        if (string.IsNullOrEmpty(outcome.ResultTextTemplate)) return fallback;

        var rendered = EventTemplateRenderer.Render(outcome.ResultTextTemplate, instance,
            resolver ?? TryResolvePersonDisplayName, "ResultTextTemplate");

        if (!rendered.Success) return fallback + "\n（注意：结果文案渲染失败：" + rendered.Diagnostic + "）";
        return rendered.Text;
    }

    /// <summary>默认解析器重载。</summary>
    public static string RenderResultText(ResolvedEventInstance instance, EventOptionExecutionResult result,
        string optionLabel)
    {
        return RenderResultText(instance, result, optionLabel, null);
    }

    /// <summary>通用结果行（不含任何硬编码产品选项语义；只反映真实结算身份）。</summary>
    private static string BuildGenericResultText(EventOptionExecutionResult result, string optionLabel)
    {
        if (result == null) return string.Empty;

        string head = string.IsNullOrEmpty(optionLabel)
            ? "已记录你的选择。"
            : "已记录你的选择：" + optionLabel + "。";

        return head + "\n（事件 " + result.EventId + " / 结果 " + result.OutcomeId
            + "；本次应用效果 " + result.AppliedEffectCount + " 条。）";
    }

    // ==================== 4) Legacy Pending 收敛迁移（旧档既成事实；不重判） ====================

    /// <summary>
    /// 把**旧档**的 Legacy `VillagePendingResponse` 收敛为 Unified Event Pending（Gate D1 · B4）。
    ///
    /// 语义（冻结）：
    ///   - 旧 Pending 是**已经发生的事实** → **不**重跑 Resolver / Weight / EventCondition
    ///     （不按「年内计数是否达阈值」再判一次）；
    ///   - 已知 PatternKey（`TemporaryDraftBuildup`）→ 按映射直接重建实例；
    ///   - 未知 PatternKey / 内容缺失 → **保留** Legacy 兼容路径（不静默删除、不猜映射）；
    ///   - 已有新 Event Pending 时不迁移（V0.1 至多一条）。
    ///
    /// 返回 true = 本次真实完成一次迁移。
    /// </summary>
    public static bool TryMigrateLegacyPending(out string diagnostic)
    {
        diagnostic = string.Empty;

        if (EventRuntimeBridge.HasPendingVillageEvent)
        {
            diagnostic = "Skip：已存在新的 Event Pending，不迁移（V0.1 至多一条）。";
            return false;
        }

        var legacy = VillageRuntimeBridge.SocialResponse != null
            ? VillageRuntimeBridge.SocialResponse.PendingResponse
            : null;
        if (legacy == null)
        {
            diagnostic = "Skip：旧档没有 Legacy Pending。";
            return false;
        }

        if (!string.Equals(legacy.PatternKey, LegacyBuildupPatternKey, StringComparison.Ordinal))
        {
            diagnostic = "Unknown：Legacy Pending 的 PatternKey '" + legacy.PatternKey
                + "' 没有对应的 Event 内容 → 保留 Legacy 兼容路径（不静默删除）。";
            LastDiagnostic = diagnostic;
            return false;
        }

        if (string.IsNullOrEmpty(legacy.PersonId) || legacy.Year < 1)
        {
            diagnostic = "Invalid：Legacy Pending 缺少 PersonId / Year → 无法迁移（保留 Legacy 兼容路径）。";
            LastDiagnostic = diagnostic;
            return false;
        }

        EventRoleDefinition role;
        if (!VillageEventStaticConfigAdapter.TryGetRole(LegacyBuildupRoleId, out role) || role == null
            || string.IsNullOrEmpty(role.SelectorType))
        {
            diagnostic = "ContentMissing：当前静态配置中没有可用角色 " + LegacyBuildupRoleId + " → 不迁移。";
            LastDiagnostic = diagnostic;
            return false;
        }

        if (!VillageEventStaticConfigAdapter.EventExists(LegacyBuildupEventId))
        {
            diagnostic = "ContentMissing：当前静态配置中没有事件 " + LegacyBuildupEventId + " → 不迁移。";
            LastDiagnostic = diagnostic;
            return false;
        }

        // 映射一致性（内容被改坏 → Fail Closed，绝不拼一个自相矛盾的实例）
        if (!string.Equals(role.EventId, LegacyBuildupEventId, StringComparison.Ordinal))
        {
            diagnostic = "ContentMismatch：角色 " + role.RoleId + " 已不属于事件 " + LegacyBuildupEventId
                + "（实际 " + role.EventId + "）→ 不迁移。";
            LastDiagnostic = diagnostic;
            return false;
        }

        // 旧档只保留 Pattern / Person / Year：ObservedCount 取**当前可得的最佳事实**（账本年内计数），
        // 完全缺失时退回该 Pattern 的测试阈值。**不据此重判任何条件。**
        int observedCount = VillageRuntimeBridge.CountTemporaryCommitmentsInYear(legacy.PersonId, legacy.Year);
        if (observedCount < 1) observedCount = VillageTestSocialPatterns.BuildupDraftThreshold;

        string triggerKey = BuildRecognitionTriggerKey(LegacyBuildupPatternKey, legacy.PersonId, legacy.Year);

        var dto = new PendingEventInstanceSaveData
        {
            EventInstanceId = LegacyBuildupInstancePrefix + LegacyBuildupEventId + "|"
                + (string.IsNullOrEmpty(legacy.EventId)
                    ? legacy.PersonId + "@" + legacy.Year
                    : legacy.EventId),
            EventId = LegacyBuildupEventId,
            TriggerType = RecognitionTriggerType,
            TriggerKey = triggerKey,
            ResolutionSeed = DeriveResolutionSeed(triggerKey),
            RoleBindings = new List<RoleBindingSaveData>
            {
                new RoleBindingSaveData
                {
                    RoleId = role.RoleId,
                    RoleName = role.RoleName,
                    SelectorType = role.SelectorType,
                    PersonId = legacy.PersonId,
                    SourceKey = role.SourceKey
                }
            },
            ContextSnapshot = new List<ContextEntrySaveData>
            {
                new ContextEntrySaveData
                {
                    Kind = (int)EventContextEntryKind.Person,
                    Key = RecognitionPersonKey,
                    StringValue = legacy.PersonId
                },
                new ContextEntrySaveData
                {
                    Kind = (int)EventContextEntryKind.String,
                    Key = RecognitionTypeKey,
                    StringValue = legacy.PatternKey
                },
                new ContextEntrySaveData
                {
                    Kind = (int)EventContextEntryKind.Int,
                    Key = ObservedCountKey,
                    IntValue = observedCount
                }
            }
        };

        var instance = EventWorldPersistence.FromSaveData(dto);
        if (instance == null)
        {
            diagnostic = "Invalid：Legacy Pending 迁移实例构建失败（DTO 残缺）。";
            LastDiagnostic = diagnostic;
            return false;
        }

        string pendingDiagnostic;
        if (!EventRuntimeBridge.SetPendingVillageEvent(instance, out pendingDiagnostic))
        {
            diagnostic = pendingDiagnostic;
            LastDiagnostic = diagnostic;
            return false;
        }

        // 旧 Pending 已收敛为 Event Pending → 清除 Legacy Pending
        //（只清 pending：fired / ChoiceHistory / Rest 记录保持不动，不重写历史）。
        VillageRuntimeBridge.ClearLegacyPendingMigratedByEventCutover();

        LastDiagnostic = string.Empty;
        return true;
    }
}
