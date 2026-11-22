using System;
using System.Collections.Generic;
using System.Linq;

namespace RRPG.ConfigCompiler.Core
{
    /// <summary>
    /// Event 领域校验（WP-Event-1A：统一事件内容基础 / Static Content Foundation）。
    ///
    /// 职责边界（与 Encounter 领域同一分工，刻意不重复既有机制）：
    ///   - 类型 / 单列范围（&gt;= &lt;=）/ 单列 allowed= / 普通 ref：由 <see cref="DataCompiler"/>
    ///     与 <see cref="CrossReferenceValidator"/> 负责，本类**不重复实现**；
    ///   - 本类只做三段既有机制表达不了的语义：
    ///       1. **领域结构完整性**（Enabled 事件必须有 Trigger / 恰好一条 Presentation / Option / Outcome）；
    ///       2. **跨列 + 条件引用形状**（RepeatPolicy↔Cooldown、Role Selector 形状、Condition / Effect 参数形状、
    ///          StartBattle 必须是同一 Outcome 下 Order 最大的效果、同 Outcome Order 唯一）；
    ///       3. **领域取值域第二道防线 + Domain 约束**（TriggerType / SelectorType / ConditionType /
    ///          PresentationChannel / RepeatPolicy / CooldownType / UnavailableMode / EffectType 未知值一律
    ///          Hard Error；Village / Expedition 的 Role / Channel / Cooldown / Effect 约束）。
    ///
    /// 为什么需要第 3 类：`allowed=` 写在 Row 3 是**声明式**的，制作人可能漏写或写歪；
    /// 这里用**作者字面输入**复核，给出领域自己的精确原因（绝不静默接受未知枚举）。
    ///
    /// 硬约束：**从不静默跳过**。缺列、缺值、引用不存在、未知分类值 → 明确 Hard Error +
    /// 定位到 Workbook / Sheet / Row / Column。
    ///
    /// 空态语义：Event 领域的十张表**全部不存在** = 该领域尚未建立（合法空态，不产生诊断）；
    /// 只存在其中一部分 = 领域不完整（DAT030，硬失败）。
    ///
    /// 边界（1A 不做）：不求解事件、不绑定人物、不判断条件、不执行效果、不渲染模板
    /// （模板只做最小结构检查，完整校验属 1B）。
    /// </summary>
    public static class EventValidator
    {
        // ---------------- 领域身份 ----------------

        public const string EventSheetName = "Event";
        public const string TriggerSheetName = "EventTrigger";
        public const string RoleSheetName = "EventRole";
        public const string RoleConditionSheetName = "EventRoleCondition";
        public const string ConditionSheetName = "EventCondition";
        public const string PresentationSheetName = "EventPresentation";
        public const string OptionSheetName = "EventOption";
        public const string OptionConditionSheetName = "EventOptionCondition";
        public const string OutcomeSheetName = "EventOutcome";
        public const string EffectSheetName = "EventEffect";

        /// <summary>Event 领域必须同时存在的十张表（缺任一 = 领域不完整）。</summary>
        public static readonly string[] DomainSheets =
        {
            EventSheetName, TriggerSheetName, RoleSheetName, RoleConditionSheetName, ConditionSheetName,
            PresentationSheetName, OptionSheetName, OptionConditionSheetName, OutcomeSheetName, EffectSheetName
        };

        // ---------------- Domain（产品 Context） ----------------

        public const string DomainVillage = "Village";
        public const string DomainExpedition = "Expedition";

        public static readonly string[] VillageTriggerTypes =
        {
            "VillageDaySettled", "VillageRecognitionRaised", "VillageProjectCompleted"
        };

        public static readonly string[] ExpeditionTriggerTypes =
        {
            "ExpeditionEventNodeReached", "ExpeditionBattleResolved", "ExpeditionReturned"
        };

        public const string ManualDebugTriggerType = "ManualDebug";

        // ---------------- 取值域（V0.1 冻结的最小词表） ----------------

        public static readonly string[] AllowedRepeatPolicies =
            { "Repeatable", "OncePerExpedition", "OncePerWorld" };

        public static readonly string[] AllowedCooldownTypes = { "None", "VillageDays", "ExpeditionNodes" };

        public static readonly string[] AllowedSelectorTypes =
            { "ContextPerson", "VillagePerson", "ExpeditionPartyMember", "FixedPerson" };

        public static readonly string[] AllowedRoleConditionTypes =
            { "PersonInVillage", "PersonOnExpedition", "PersonAvailable" };

        public static readonly string[] PersonConditionTypes =
            { "PersonExists", "PersonInVillage", "PersonOnExpedition", "PersonAvailable" };

        public static readonly string[] ContextStringConditionTypes = { "ContextStringEquals" };
        public static readonly string[] ContextIntConditionTypes = { "ContextIntAtLeast", "ContextIntAtMost" };
        public static readonly string[] FlagConditionTypes = { "FlagSet", "FlagNotSet" };
        public static readonly string[] VillageResourceConditionTypes =
            { "VillageResourceAtLeast", "VillageResourceAtMost" };
        public static readonly string[] IntOnlyConditionTypes =
        {
            "ExpeditionDepthAtLeast", "ExpeditionDepthAtMost", "SupplyAtLeast", "SupplyAtMost",
            "PartySizeAtLeast", "PartySizeAtMost"
        };

        public static readonly string[] AllowedConditionTypes = Concat(
            ContextStringConditionTypes, ContextIntConditionTypes, FlagConditionTypes,
            new[] { "EventNotSeen", "EventSeenCountAtLeast" },
            PersonConditionTypes, VillageResourceConditionTypes, IntOnlyConditionTypes);

        public static readonly string[] AllowedChannels = { "JourneyCard", "VillageModal" };
        public static readonly string[] AllowedUrgencies = { "Timely", "Ignorable" };
        public static readonly string[] AllowedUnavailableModes = { "Hide", "Disable" };

        public static readonly string[] AllowedEffectTypes =
        {
            "SetFlag", "ClearFlag", "VillageResourceDelta", "GrantPersonRest",
            "SupplyDelta", "AdvanceExpeditionTime", "StartBattle"
        };

        public static readonly string[] VillageOnlyEffectTypes = { "VillageResourceDelta", "GrantPersonRest" };

        public static readonly string[] ExpeditionOnlyEffectTypes =
            { "SupplyDelta", "AdvanceExpeditionTime", "StartBattle" };

        /// <summary>V0.1 合法 Village Resource TargetKey（禁止恢复旧 Wood / Stone / Gold Authority）。</summary>
        public static readonly string[] AllowedVillageResourceKeys = { "Ingredients", "Food" };

        private static string[] Concat(params string[][] parts)
        {
            var list = new List<string>();
            foreach (var part in parts) list.AddRange(part);
            return list.ToArray();
        }

        // ---------------- 入口 ----------------

        public static void Validate(List<CompiledSheet> sheets, DiagnosticBag bag)
        {
            var domain = new CompiledSheet[DomainSheets.Length];
            for (int i = 0; i < DomainSheets.Length; i++) domain[i] = Find(sheets, DomainSheets[i]);

            bool anyPresent = false;
            for (int i = 0; i < domain.Length; i++) if (domain[i] != null) anyPresent = true;
            if (!anyPresent) return;   // 领域尚未建立 = 合法空态

            var missing = new List<string>();
            for (int i = 0; i < domain.Length; i++) if (domain[i] == null) missing.Add(DomainSheets[i]);

            if (missing.Count > 0)
            {
                var anchor = FirstPresent(domain);
                bag.Error(DiagnosticCodes.DatEventDomainShape,
                    "Event 领域不完整：缺少 Sheet " + string.Join(" / ", missing)
                    + "。事件工作簿必须同时包含 " + string.Join(" / ", DomainSheets)
                    + "（缺表会让事件语义静默缺失：触发 / 角色 / 条件 / 表现 / 选项 / 结果 / 效果是同一份契约）。",
                    DiagContext.Of(anchor.Spec.Workbook, anchor.Spec.SheetName, 0));
                return;
            }

            var ctx = new Ctx
            {
                Sheets = sheets,
                Bag = bag,
                Event = domain[0],
                Trigger = domain[1],
                Role = domain[2],
                RoleCondition = domain[3],
                Condition = domain[4],
                Presentation = domain[5],
                Option = domain[6],
                OptionCondition = domain[7],
                Outcome = domain[8],
                Effect = domain[9]
            };

            ctx.EventIds = IdsOf(ctx.Event);
            ctx.RoleEventById = Relation(ctx.Role, "RoleId", "EventId");
            ctx.OptionEventById = Relation(ctx.Option, "OptionId", "EventId");
            ctx.OutcomeOptionById = Relation(ctx.Outcome, "OutcomeId", "OptionId");
            ctx.RoleIds = KeySet(ctx.RoleEventById);
            ctx.OptionIds = KeySet(ctx.OptionEventById);
            ctx.OutcomeIds = KeySet(ctx.OutcomeOptionById);

            ISet<string> poolIds = IdsOf(Find(sheets, EncounterValidator.EncounterPoolSheetName));
            ISet<string> visualAssetIds = IdsOf(Find(sheets, SourceArtValidator.VisualAssetSheetName));
            ctx.PoolIds = poolIds;
            ctx.VisualAssetIds = visualAssetIds;

            BuildCounts(ctx);

            ValidateTriggers(ctx);
            ValidateEvents(ctx);
            ValidateRoles(ctx);
            ValidateRoleConditions(ctx);
            ValidateConditions(ctx);
            ValidatePresentations(ctx);
            ValidateOptions(ctx);
            ValidateOptionConditions(ctx);
            ValidateEventHistoryTargets(ctx);
            ValidateOutcomes(ctx);
            ValidateEffects(ctx);
        }

        /// <summary>一次校验所需的全部索引（只在本次调用内有效）。</summary>
        private sealed class Ctx
        {
            public List<CompiledSheet> Sheets;
            public DiagnosticBag Bag;

            public CompiledSheet Event, Trigger, Role, RoleCondition, Condition, Presentation, Option,
                OptionCondition, Outcome, Effect;

            public ISet<string> EventIds = new HashSet<string>(StringComparer.Ordinal);
            public ISet<string> RoleIds = new HashSet<string>(StringComparer.Ordinal);
            public ISet<string> OptionIds = new HashSet<string>(StringComparer.Ordinal);
            public ISet<string> OutcomeIds = new HashSet<string>(StringComparer.Ordinal);
            public ISet<string> PoolIds;
            public ISet<string> VisualAssetIds;

            /// <summary>RoleId → EventId（用于「Role 必须属于同一个 Event」与条件性引用解析）。</summary>
            public Dictionary<string, string> RoleEventById = new Dictionary<string, string>(StringComparer.Ordinal);
            public Dictionary<string, string> OptionEventById = new Dictionary<string, string>(StringComparer.Ordinal);
            public Dictionary<string, string> OutcomeOptionById = new Dictionary<string, string>(StringComparer.Ordinal);

            /// <summary>EventId → 由非 ManualDebug Trigger 推导出的 Domain（空字符串 = 未推导出）。</summary>
            public Dictionary<string, string> DomainByEvent = new Dictionary<string, string>(StringComparer.Ordinal);
            public Dictionary<string, bool> MixedDomainByEvent = new Dictionary<string, bool>(StringComparer.Ordinal);
            public Dictionary<string, int> TriggerCountByEvent = new Dictionary<string, int>(StringComparer.Ordinal);
            public Dictionary<string, int> PresentationCountByEvent = new Dictionary<string, int>(StringComparer.Ordinal);
            public Dictionary<string, int> OptionCountByEvent = new Dictionary<string, int>(StringComparer.Ordinal);
            public Dictionary<string, int> OutcomeCountByOption = new Dictionary<string, int>(StringComparer.Ordinal);
            public Dictionary<string, int> SortOrderCountByEvent = new Dictionary<string, int>(StringComparer.Ordinal);
            public Dictionary<string, string> FirstEventOfRole = new Dictionary<string, string>(StringComparer.Ordinal);
        }

        // ---------------- 计数（用于 Enabled 结构完整性与 Option SortOrder 唯一性） ----------------

        private static void BuildCounts(Ctx ctx)
        {
            foreach (var row in ctx.Trigger.Rows)
            {
                string eventId = Text(row, "EventId");
                if (!string.IsNullOrEmpty(eventId)) Bump(ctx.TriggerCountByEvent, eventId);
            }

            foreach (var row in ctx.Presentation.Rows)
            {
                string eventId = Text(row, "EventId");
                if (!string.IsNullOrEmpty(eventId)) Bump(ctx.PresentationCountByEvent, eventId);
            }

            foreach (var row in ctx.Option.Rows)
            {
                string eventId = Text(row, "EventId");
                if (!string.IsNullOrEmpty(eventId)) Bump(ctx.OptionCountByEvent, eventId);

                long? order = Int(row, "SortOrder");
                if (!string.IsNullOrEmpty(eventId) && order != null)
                {
                    Bump(ctx.SortOrderCountByEvent, eventId + "\u001f" + order.Value.ToString(System.Globalization.CultureInfo.InvariantCulture));
                }
            }

            foreach (var row in ctx.Outcome.Rows)
            {
                string optionId = Text(row, "OptionId");
                if (!string.IsNullOrEmpty(optionId)) Bump(ctx.OutcomeCountByOption, optionId);
            }
        }

        private static void Bump(Dictionary<string, int> map, string key)
        {
            int count;
            map.TryGetValue(key, out count);
            map[key] = count + 1;
        }

        // ---------------- Trigger / Domain 推导 ----------------

        private static void ValidateTriggers(Ctx ctx)
        {
            foreach (var row in ctx.Trigger.Rows)
            {
                string eventId = Text(row, "EventId");
                string triggerType = Text(row, "TriggerType");

                if (triggerType != null && !IsKnownTriggerType(triggerType))
                {
                    ctx.Bag.Error(DiagnosticCodes.DatEventTriggerRule,
                        "TriggerType 未知取值 '" + triggerType + "'（允许："
                        + string.Join(" / ", KnownTriggerTypes()) + "）。未知触发时机不得静默进入 Mirror。",
                        Ctx_(row), "TriggerType");
                    continue;
                }

                if (string.IsNullOrEmpty(eventId) || string.IsNullOrEmpty(triggerType)) continue;

                string rowDomain = DomainOfTrigger(triggerType);
                if (rowDomain == null) continue;   // ManualDebug 不参与 Domain 推导

                string existing;
                if (!ctx.DomainByEvent.TryGetValue(eventId, out existing))
                {
                    ctx.DomainByEvent[eventId] = rowDomain;
                }
                else if (!string.Equals(existing, rowDomain, StringComparison.Ordinal))
                {
                    ctx.MixedDomainByEvent[eventId] = true;
                }
            }

            foreach (var pair in ctx.MixedDomainByEvent.OrderBy(p => p.Key, StringComparer.Ordinal))
            {
                if (!pair.Value) continue;

                var row = FirstRowWithEvent(ctx.Trigger, pair.Key);
                ctx.Bag.Error(DiagnosticCodes.DatEventTriggerRule,
                    "事件 " + pair.Key + " 同时拥有 Village 与 Expedition 的正式触发时机："
                    + "V0.1 不支持同一个正式事件同时既是村庄事件又是远征事件。"
                    + "统一的是 Event Core，不代表一条事件定义必须跨两个产品 Context 使用。",
                    Ctx_(row), "TriggerType");
            }
        }

        private static string DomainOfTrigger(string triggerType)
        {
            if (Contains(VillageTriggerTypes, triggerType)) return DomainVillage;
            if (Contains(ExpeditionTriggerTypes, triggerType)) return DomainExpedition;
            return null;
        }

        private static bool IsKnownTriggerType(string triggerType)
        {
            return Contains(VillageTriggerTypes, triggerType)
                || Contains(ExpeditionTriggerTypes, triggerType)
                || string.Equals(triggerType, ManualDebugTriggerType, StringComparison.Ordinal);
        }

        private static string[] KnownTriggerTypes()
        {
            var list = new List<string>();
            list.AddRange(VillageTriggerTypes);
            list.AddRange(ExpeditionTriggerTypes);
            list.Add(ManualDebugTriggerType);
            return list.ToArray();
        }

        /// <summary>事件当前推导出的 Domain；未推导出（只有 ManualDebug 或没有触发）= null。</summary>
        private static string DomainOf(Ctx ctx, string eventId)
        {
            string domain;
            if (string.IsNullOrEmpty(eventId)) return null;
            return ctx.DomainByEvent.TryGetValue(eventId, out domain) ? domain : null;
        }

        // ---------------- Event 主表：Repeat / Cooldown + Enabled 结构 ----------------

        private static void ValidateEvents(Ctx ctx)
        {
            foreach (var row in ctx.Event.Rows)
            {
                string eventId = Text(row, "EventId");
                string repeat = Text(row, "RepeatPolicy");
                string cooldownType = Text(row, "CooldownType");
                long? cooldownValue = Int(row, "CooldownValue");
                bool? enabled = Bool(row, "Enabled");

                if (repeat != null && !Contains(AllowedRepeatPolicies, repeat))
                {
                    ctx.Bag.Error(DiagnosticCodes.DatEventRepeatCooldownShape,
                        "RepeatPolicy 未知取值 '" + repeat + "'（允许："
                        + string.Join(" / ", AllowedRepeatPolicies) + "）。",
                        Ctx_(row), "RepeatPolicy");
                }

                if (cooldownType != null && !Contains(AllowedCooldownTypes, cooldownType))
                {
                    ctx.Bag.Error(DiagnosticCodes.DatEventRepeatCooldownShape,
                        "CooldownType 未知取值 '" + cooldownType + "'（允许："
                        + string.Join(" / ", AllowedCooldownTypes) + "）。",
                        Ctx_(row), "CooldownType");
                }

                // 冷却形状：None ⇒ 0；非 None ⇒ > 0
                if (cooldownType != null && cooldownValue != null)
                {
                    bool isNone = string.Equals(cooldownType, "None", StringComparison.Ordinal);
                    if (isNone && cooldownValue.Value != 0)
                    {
                        ctx.Bag.Error(DiagnosticCodes.DatEventRepeatCooldownShape,
                            "CooldownType = None 时 CooldownValue 必须为 0（当前 " + cooldownValue.Value
                            + "）：无冷却却写了冷却值会误导后续 Runtime。",
                            Ctx_(row), "CooldownValue", "None=>CooldownValue=0");
                    }
                    else if (!isNone && cooldownValue.Value <= 0)
                    {
                        ctx.Bag.Error(DiagnosticCodes.DatEventRepeatCooldownShape,
                            "CooldownType = " + cooldownType + " 时 CooldownValue 必须大于 0（当前 "
                            + cooldownValue.Value + "）。",
                            Ctx_(row), "CooldownValue", cooldownType + "=>CooldownValue>0");
                    }
                }

                // Once 策略已经限制一次，不再叠加无意义 Cooldown
                bool isOnce = string.Equals(repeat, "OncePerWorld", StringComparison.Ordinal)
                    || string.Equals(repeat, "OncePerExpedition", StringComparison.Ordinal);
                if (isOnce && cooldownType != null && !string.Equals(cooldownType, "None", StringComparison.Ordinal))
                {
                    ctx.Bag.Error(DiagnosticCodes.DatEventRepeatCooldownShape,
                        "RepeatPolicy = " + repeat + " 已经限制只能发生一次，不能再叠加 CooldownType = "
                        + cooldownType + "（请改为 None）。",
                        Ctx_(row), "CooldownType", repeat + "=>CooldownType=None");
                }

                // Domain 与冷却类型：Village 只用 None / VillageDays；Expedition 只用 None / ExpeditionNodes
                string domain = DomainOf(ctx, eventId);
                if (domain != null && cooldownType != null && !string.Equals(cooldownType, "None", StringComparison.Ordinal))
                {
                    string expected = domain == DomainVillage ? "VillageDays" : "ExpeditionNodes";
                    if (!string.Equals(cooldownType, expected, StringComparison.Ordinal))
                    {
                        ctx.Bag.Error(DiagnosticCodes.DatEventRepeatCooldownShape,
                            "事件 " + eventId + " 的 Domain = " + domain + "，因此 CooldownType 只允许 None / "
                            + expected + "（当前 " + cooldownType + "）。",
                            Ctx_(row), "CooldownType", domain + "=>" + expected);
                    }
                }

                if (enabled == null || !enabled.Value) continue;
                ValidateEnabledStructure(ctx, row, eventId);
            }
        }

        private static void ValidateEnabledStructure(Ctx ctx, CompiledRow row, string eventId)
        {
            if (CountOf(ctx.TriggerCountByEvent, eventId) == 0)
            {
                ctx.Bag.Error(DiagnosticCodes.DatEventEnabledStructure,
                    "启用的事件 " + eventId + " 没有任何 EventTrigger：它永远不可能被触发。"
                    + "请至少写一条触发时机，或把 Enabled 改为 FALSE 当作草稿。",
                    Ctx_(row), "Enabled", "Enabled=>Trigger>=1");
            }

            int presentationCount = CountOf(ctx.PresentationCountByEvent, eventId);
            if (presentationCount == 0)
            {
                ctx.Bag.Error(DiagnosticCodes.DatEventEnabledStructure,
                    "启用的事件 " + eventId + " 没有 EventPresentation：它无法被展示。"
                    + "第一版要求一个事件必须且只能有一条表现。",
                    Ctx_(row), "Enabled", "Enabled=>Presentation=1");
            }
            else if (presentationCount > 1)
            {
                ctx.Bag.Error(DiagnosticCodes.DatEventEnabledStructure,
                    "启用的事件 " + eventId + " 有 " + presentationCount + " 条 EventPresentation："
                    + "第一版要求一个事件必须且只能有一条表现（EventPresentation 的身份就是 EventId）。",
                    Ctx_(row), "Enabled", "Enabled=>Presentation=1");
            }

            if (CountOf(ctx.OptionCountByEvent, eventId) == 0)
            {
                ctx.Bag.Error(DiagnosticCodes.DatEventEnabledStructure,
                    "启用的事件 " + eventId + " 没有任何 EventOption：玩家无法做出任何选择。",
                    Ctx_(row), "Enabled", "Enabled=>Option>=1");
            }
        }

        private static int CountOf(Dictionary<string, int> map, string key)
        {
            int count;
            if (string.IsNullOrEmpty(key)) return 0;
            return map.TryGetValue(key, out count) ? count : 0;
        }

        // ---------------- EventRole：Selector 形状 + Domain 约束 ----------------

        private static void ValidateRoles(Ctx ctx)
        {
            foreach (var row in ctx.Role.Rows)
            {
                string roleId = Text(row, "RoleId");
                string eventId = Text(row, "EventId");
                string selector = Text(row, "SelectorType");
                string sourceKey = Text(row, "SourceKey");
                string fixedPersonId = Text(row, "FixedPersonId");

                if (selector != null && !Contains(AllowedSelectorTypes, selector))
                {
                    ctx.Bag.Error(DiagnosticCodes.DatEventRoleShape,
                        "SelectorType 未知取值 '" + selector + "'（允许："
                        + string.Join(" / ", AllowedSelectorTypes) + "）。",
                        Ctx_(row), "SelectorType");
                    continue;
                }

                bool hasSourceKey = !string.IsNullOrEmpty(sourceKey);
                bool hasFixedPerson = !string.IsNullOrEmpty(fixedPersonId);

                switch (selector)
                {
                    case "ContextPerson":
                        if (!hasSourceKey)
                        {
                            ctx.Bag.Error(DiagnosticCodes.DatEventRoleShape,
                                "SelectorType = ContextPerson 必须填写 SourceKey（触发上下文提供的来源键）。",
                                Ctx_(row), "SourceKey", "ContextPerson=>SourceKey!");
                        }
                        if (hasFixedPerson)
                        {
                            ctx.Bag.Error(DiagnosticCodes.DatEventRoleShape,
                                "SelectorType = ContextPerson 不得填写 FixedPersonId（人物来自触发上下文）。",
                                Ctx_(row), "FixedPersonId");
                        }
                        break;

                    case "VillagePerson":
                        RequireEmpty(ctx, row, "SourceKey", hasSourceKey, selector);
                        RequireEmpty(ctx, row, "FixedPersonId", hasFixedPerson, selector);
                        RequireDomain(ctx, row, DomainVillage, selector);
                        break;

                    case "ExpeditionPartyMember":
                        RequireEmpty(ctx, row, "SourceKey", hasSourceKey, selector);
                        RequireEmpty(ctx, row, "FixedPersonId", hasFixedPerson, selector);
                        RequireDomain(ctx, row, DomainExpedition, selector);
                        break;

                    case "FixedPerson":
                        if (!hasFixedPerson)
                        {
                            ctx.Bag.Error(DiagnosticCodes.DatEventRoleShape,
                                "SelectorType = FixedPerson 必须填写 FixedPersonId（固定故事NPC）。",
                                Ctx_(row), "FixedPersonId", "FixedPerson=>FixedPersonId!");
                        }
                        if (hasSourceKey)
                        {
                            ctx.Bag.Error(DiagnosticCodes.DatEventRoleShape,
                                "SelectorType = FixedPerson 不得填写 SourceKey。",
                                Ctx_(row), "SourceKey");
                        }
                        break;
                }

                if (!string.IsNullOrEmpty(roleId) && !string.IsNullOrEmpty(eventId))
                {
                    ctx.FirstEventOfRole[roleId] = eventId;
                }
            }
        }

        private static void RequireEmpty(Ctx ctx, CompiledRow row, string column, bool hasValue, string selector)
        {
            if (!hasValue) return;
            ctx.Bag.Error(DiagnosticCodes.DatEventRoleShape,
                "SelectorType = " + selector + " 不得填写 " + column + "。",
                Ctx_(row), column);
        }

        private static void RequireDomain(Ctx ctx, CompiledRow row, string required, string selector)
        {
            string eventId = Text(row, "EventId");
            string domain = DomainOf(ctx, eventId);
            if (domain == null) return;   // ManualDebug-only 等未推导出 Domain 的情况不做域约束

            if (!string.Equals(domain, required, StringComparison.Ordinal))
            {
                ctx.Bag.Error(DiagnosticCodes.DatEventRoleShape,
                    "SelectorType = " + selector + " 只允许出现在 " + required + " 事件里，"
                    + "但事件 " + eventId + " 的 Domain = " + domain + "。",
                    Ctx_(row), "SelectorType", selector + "=>" + required);
            }
        }

        // ---------------- 条件形状（EventRoleCondition / EventCondition / EventOptionCondition 共用） ----------------

        /// <summary>
        /// 条件参数形状 + 取值域第二道防线。返回 false = 已报错（调用方不必继续对该行做更深的语义判断）。
        /// </summary>
        private static bool ValidateConditionShape(Ctx ctx, CompiledRow row, string[] allowedTypes,
            string conditionType, string subjectRoleId, string targetKey, long? intValue, string stringValue,
            bool hasSubjectRoleColumn)
        {
            if (conditionType == null) return false;   // 必填为空已由 DAT001 报出

            if (!Contains(allowedTypes, conditionType))
            {
                ctx.Bag.Error(DiagnosticCodes.DatEventConditionShape,
                    "ConditionType 未知取值 '" + conditionType + "'（本表允许："
                    + string.Join(" / ", allowedTypes) + "）。不得静默接受未知条件类型。",
                    Ctx_(row), "ConditionType");
                return false;
            }

            bool hasTarget = !string.IsNullOrEmpty(targetKey);
            bool hasInt = intValue != null;
            bool hasString = !string.IsNullOrEmpty(stringValue);
            bool hasSubject = !string.IsNullOrEmpty(subjectRoleId);

            if (Contains(PersonConditionTypes, conditionType))
            {
                if (hasSubjectRoleColumn && !hasSubject)
                {
                    ctx.Bag.Error(DiagnosticCodes.DatEventConditionShape,
                        conditionType + " 必须填写 SubjectRoleId。",
                        Ctx_(row), "SubjectRoleId", conditionType + "=>SubjectRoleId!");
                }
                RequireNoTarget(ctx, row, conditionType, hasTarget);
                RequireNoInt(ctx, row, conditionType, hasInt);
                RequireNoString(ctx, row, conditionType, hasString);
                return true;
            }

            if (hasSubjectRoleColumn && hasSubject)
            {
                ctx.Bag.Error(DiagnosticCodes.DatEventConditionShape,
                    conditionType + " 不针对人物，因此不得填写 SubjectRoleId。",
                    Ctx_(row), "SubjectRoleId");
            }

            if (Contains(ContextStringConditionTypes, conditionType))
            {
                RequireTarget(ctx, row, conditionType, hasTarget);
                if (!hasString)
                {
                    ctx.Bag.Error(DiagnosticCodes.DatEventConditionShape,
                        conditionType + " 必须填写 StringValue。",
                        Ctx_(row), "StringValue", conditionType + "=>StringValue!");
                }
                RequireNoInt(ctx, row, conditionType, hasInt);
                return true;
            }

            if (Contains(ContextIntConditionTypes, conditionType))
            {
                RequireTarget(ctx, row, conditionType, hasTarget);
                if (!hasInt)
                {
                    ctx.Bag.Error(DiagnosticCodes.DatEventConditionShape,
                        conditionType + " 必须填写 IntValue。",
                        Ctx_(row), "IntValue", conditionType + "=>IntValue!");
                }
                RequireNoString(ctx, row, conditionType, hasString);
                return true;
            }

            if (Contains(VillageResourceConditionTypes, conditionType))
            {
                if (!hasTarget)
                {
                    ctx.Bag.Error(DiagnosticCodes.DatEventConditionShape,
                        conditionType + " 必须填写 TargetKey（村庄资源名）。",
                        Ctx_(row), "TargetKey", conditionType + "=>TargetKey!");
                }
                else if (!Contains(AllowedVillageResourceKeys, targetKey))
                {
                    ctx.Bag.Error(DiagnosticCodes.DatEventConditionShape,
                        "TargetKey '" + targetKey + "' 不是合法的村庄资源（V0.1 只允许："
                        + string.Join(" / ", AllowedVillageResourceKeys) + "）。"
                        + "不得恢复旧 Wood / Stone / Gold Authority。",
                        Ctx_(row), "TargetKey", "TargetKey∈{Ingredients,Food}");
                }

                if (!hasInt)
                {
                    ctx.Bag.Error(DiagnosticCodes.DatEventConditionShape,
                        conditionType + " 必须填写 IntValue（资源阈值）。",
                        Ctx_(row), "IntValue", conditionType + "=>IntValue!");
                }
                RequireNoString(ctx, row, conditionType, hasString);
                return true;
            }

            if (Contains(IntOnlyConditionTypes, conditionType))
            {
                if (hasTarget)
                {
                    ctx.Bag.Error(DiagnosticCodes.DatEventConditionShape,
                        conditionType + " 不使用 TargetKey（当前值 '" + targetKey + "'）："
                        + "远征状态阈值只由 IntValue 表达。",
                        Ctx_(row), "TargetKey");
                }
                if (!hasInt)
                {
                    ctx.Bag.Error(DiagnosticCodes.DatEventConditionShape,
                        conditionType + " 必须填写 IntValue（阈值）。",
                        Ctx_(row), "IntValue", conditionType + "=>IntValue!");
                }
                RequireNoString(ctx, row, conditionType, hasString);
                return true;
            }

            // Flag / Event history：TargetKey 必填
            RequireTarget(ctx, row, conditionType, hasTarget);

            if (string.Equals(conditionType, "EventSeenCountAtLeast", StringComparison.Ordinal))
            {
                if (!hasInt)
                {
                    ctx.Bag.Error(DiagnosticCodes.DatEventConditionShape,
                        "EventSeenCountAtLeast 必须填写 IntValue（至少见过多少次）。",
                        Ctx_(row), "IntValue", conditionType + "=>IntValue!");
                }
            }
            else
            {
                RequireNoInt(ctx, row, conditionType, hasInt);
            }

            RequireNoString(ctx, row, conditionType, hasString);
            return true;
        }

        private static void RequireTarget(Ctx ctx, CompiledRow row, string type, bool hasTarget)
        {
            if (hasTarget) return;
            ctx.Bag.Error(DiagnosticCodes.DatEventConditionShape,
                type + " 必须填写 TargetKey。", Ctx_(row), "TargetKey", type + "=>TargetKey!");
        }

        private static void RequireNoTarget(Ctx ctx, CompiledRow row, string type, bool hasTarget)
        {
            if (!hasTarget) return;
            ctx.Bag.Error(DiagnosticCodes.DatEventConditionShape,
                type + " 不使用 TargetKey（人物状态条件不需要目标键）。", Ctx_(row), "TargetKey");
        }

        private static void RequireNoInt(Ctx ctx, CompiledRow row, string type, bool hasInt)
        {
            if (!hasInt) return;
            ctx.Bag.Error(DiagnosticCodes.DatEventConditionShape,
                type + " 不使用 IntValue。", Ctx_(row), "IntValue");
        }

        private static void RequireNoString(Ctx ctx, CompiledRow row, string type, bool hasString)
        {
            if (!hasString) return;
            ctx.Bag.Error(DiagnosticCodes.DatEventConditionShape,
                type + " 不使用 StringValue。", Ctx_(row), "StringValue");
        }

        // ---------------- 条件表：形状 + 引用解析 + 同 Event 约束 ----------------

        private static void ValidateRoleConditions(Ctx ctx)
        {
            foreach (var row in ctx.RoleCondition.Rows)
            {
                bool shapeOk = ValidateConditionShape(ctx, row,
                    AllowedRoleConditionTypes, Text(row, "ConditionType"),
                    null, Text(row, "TargetKey"), Int(row, "IntValue"), Text(row, "StringValue"),
                    false);

                if (shapeOk && Contains(PersonConditionTypes, Text(row, "ConditionType")))
                {
                    // 角色筛选条件天然以该 Role 的人物为对象；除三个 Person* 之外的类型不允许出现在本表。
                    continue;
                }
            }
        }

        private static void ValidateConditions(Ctx ctx)
        {
            foreach (var row in ctx.Condition.Rows)
            {
                ValidateConditionShape(ctx, row,
                    AllowedConditionTypes, Text(row, "ConditionType"),
                    Text(row, "SubjectRoleId"), Text(row, "TargetKey"), Int(row, "IntValue"),
                    Text(row, "StringValue"), true);

                CheckRoleRef(ctx, row, "SubjectRoleId", Text(row, "SubjectRoleId"), Text(row, "EventId"));
            }
        }

        private static void ValidateOptionConditions(Ctx ctx)
        {
            foreach (var row in ctx.OptionCondition.Rows)
            {
                ValidateConditionShape(ctx, row,
                    AllowedConditionTypes, Text(row, "ConditionType"),
                    Text(row, "SubjectRoleId"), Text(row, "TargetKey"), Int(row, "IntValue"),
                    Text(row, "StringValue"), true);

                string optionId = Text(row, "OptionId");
                string ownerEventId = null;
                if (!string.IsNullOrEmpty(optionId)) ctx.OptionEventById.TryGetValue(optionId, out ownerEventId);

                CheckRoleRef(ctx, row, "SubjectRoleId", Text(row, "SubjectRoleId"), ownerEventId);
            }
        }

        /// <summary>
        /// **WP-Event-1C 编译期补强**：事件历史条件的 TargetKey 语义。
        ///
        /// 冻结语义（WP-Event-1 FDS §19.5，1B 起权威）：
        ///   - `EventNotSeen` / `EventSeenCountAtLeast` 的 `TargetKey` = **目标 EventId**，
        ///     查询的是**世界长期事件历史**（World Event History）；
        ///   - 因此 TargetKey 必须指向真实存在的 `Event.EventId`，否则该条件永远不可能成立
        ///     （静默永不触发 = 内容缺陷，必须在编译期拦住）；
        ///   - `EventSeenCountAtLeast` 的 `IntValue` 必须 &gt;= 1（「至少见过 0 次」与「从未见过」是同一条语义，
        ///     应写 `EventNotSeen`，避免同一件事有两种写法）。
        ///
        /// 覆盖两张共用同一套 ConditionType 词表的表：`EventCondition`（事件成立条件）与
        /// `EventOptionCondition`（选项可用条件）。
        ///
        /// 边界：本规则**不新增 Schema 列**，只补强既有字段（TargetKey / IntValue）的语义。
        /// </summary>
        private static void ValidateEventHistoryTargets(Ctx ctx)
        {
            CheckEventHistoryTargets(ctx, ctx.Condition.Rows);
            CheckEventHistoryTargets(ctx, ctx.OptionCondition.Rows);
        }

        private static void CheckEventHistoryTargets(Ctx ctx, List<CompiledRow> rows)
        {
            foreach (var row in rows)
            {
                string conditionType = Text(row, "ConditionType");
                bool notSeen = string.Equals(conditionType, "EventNotSeen", StringComparison.Ordinal);
                bool seenCount = string.Equals(conditionType, "EventSeenCountAtLeast", StringComparison.Ordinal);
                if (!notSeen && !seenCount) continue;

                string target = Text(row, "TargetKey");
                if (!string.IsNullOrEmpty(target) && !ctx.EventIds.Contains(target))
                {
                    ctx.Bag.Error(DiagnosticCodes.DatEventConditionTargetRef,
                        conditionType + " 的 TargetKey 必须是**真实存在的事件 ID**（当前 '" + target
                        + "' 不是任何 Event.EventId）：该条件查询世界长期事件历史，"
                        + "指向不存在的事件只会让内容静默永不触发。",
                        Ctx_(row), "TargetKey");
                }

                if (!seenCount) continue;

                long? value = Int(row, "IntValue");
                if (value.HasValue && value.Value < 1)
                {
                    ctx.Bag.Error(DiagnosticCodes.DatEventConditionTargetRef,
                        "EventSeenCountAtLeast 的 IntValue 必须 >= 1（当前 "
                        + value.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)
                        + "）：0 次与「从未见过」语义相同，请改用 EventNotSeen。",
                        Ctx_(row), "IntValue");
                }
            }
        }

        /// <summary>
        /// SubjectRoleId / TargetRoleId 的解析与「同一个 Event」约束：
        ///   - 引用不存在 → DAT039；
        ///   - 引用的 Role 属于别的 Event → DAT037（不能引用别的 Event 的 Role）。
        /// </summary>
        private static void CheckRoleRef(Ctx ctx, CompiledRow row, string column, string roleId, string ownerEventId)
        {
            if (string.IsNullOrEmpty(roleId)) return;

            if (!ctx.RoleIds.Contains(roleId))
            {
                ctx.Bag.Error(DiagnosticCodes.DatEventRoleRefUnresolved,
                    "引用的 RoleId '" + roleId + "' 在 EventRole 表中不存在。",
                    Ctx_(row), column, "RoleId∈EventRole");
                return;
            }

            string roleEvent;
            if (!ctx.RoleEventById.TryGetValue(roleId, out roleEvent)) return;
            if (string.IsNullOrEmpty(ownerEventId) || string.IsNullOrEmpty(roleEvent)) return;

            if (!string.Equals(roleEvent, ownerEventId, StringComparison.Ordinal))
            {
                ctx.Bag.Error(DiagnosticCodes.DatEventRoleCrossEventRef,
                    "RoleId '" + roleId + "' 属于事件 " + roleEvent + "，但本行属于事件 " + ownerEventId
                    + "：事件角色不能跨事件引用（必须属于同一个 Event）。",
                    Ctx_(row), column, "RoleEvent=OwnerEvent");
            }
        }

        // ---------------- 表现表：Channel / Domain / 插图引用 / 模板 ----------------

        private static void ValidatePresentations(Ctx ctx)
        {
            foreach (var row in ctx.Presentation.Rows)
            {
                string eventId = Text(row, "EventId");
                string channel = Text(row, "PresentationChannel");
                string urgency = Text(row, "Urgency");

                if (channel != null && !Contains(AllowedChannels, channel))
                {
                    ctx.Bag.Error(DiagnosticCodes.DatEventPresentationChannelMismatch,
                        "PresentationChannel 未知取值 '" + channel + "'（允许："
                        + string.Join(" / ", AllowedChannels) + "）。",
                        Ctx_(row), "PresentationChannel");
                }

                if (urgency != null && !Contains(AllowedUrgencies, urgency))
                {
                    ctx.Bag.Error(DiagnosticCodes.DatEventPresentationChannelMismatch,
                        "Urgency 未知取值 '" + urgency + "'（允许："
                        + string.Join(" / ", AllowedUrgencies) + "）。",
                        Ctx_(row), "Urgency");
                }

                string domain = DomainOf(ctx, eventId);
                if (domain != null && channel != null && Contains(AllowedChannels, channel))
                {
                    string expected = domain == DomainVillage ? "VillageModal" : "JourneyCard";
                    if (!string.Equals(channel, expected, StringComparison.Ordinal))
                    {
                        ctx.Bag.Error(DiagnosticCodes.DatEventPresentationChannelMismatch,
                            "事件 " + eventId + " 的 Domain = " + domain + "，因此 PresentationChannel 必须是 "
                            + expected + "（当前 " + channel + "）。",
                            Ctx_(row), "PresentationChannel", domain + "=>" + expected);
                    }
                }

                string illustration = Text(row, "IllustrationAssetId");
                if (!string.IsNullOrEmpty(illustration)
                    && (ctx.VisualAssetIds == null || !ctx.VisualAssetIds.Contains(illustration)))
                {
                    ctx.Bag.Error(DiagnosticCodes.DatEventAssetRefUnresolved,
                        "IllustrationAssetId '" + illustration + "' 在共享 VisualAsset 表中不存在"
                        + "（人工 Authority：Development/Config/RRPG_VisualAssets.xlsx）。"
                        + "插图必须引用共享视觉资源；没有正式 Event PNG 时请留空。",
                        Ctx_(row), "IllustrationAssetId", "IllustrationAssetId∈VisualAsset");
                }

                string titleTemplate = Text(row, "TitleTemplate");
                string bodyTemplate = Text(row, "BodyTemplate");
                CheckTemplate(ctx, row, "TitleTemplate", titleTemplate);
                CheckTemplate(ctx, row, "BodyTemplate", bodyTemplate);
                CheckTemplateRoleRefs(ctx, row, "TitleTemplate", titleTemplate, eventId);
                CheckTemplateRoleRefs(ctx, row, "BodyTemplate", bodyTemplate, eventId);
            }
        }

        /// <summary>
        /// 文本模板最小结构检查（1A 不实现 Renderer；完整键校验属 1B）：
        /// 大括号必须成对且不嵌套、内部不得为空、只能出现 [A-Za-z0-9_.]、不得出现表达式 / 脚本痕迹。
        /// </summary>
        private static void CheckTemplate(Ctx ctx, CompiledRow row, string column, string template)
        {
            if (string.IsNullOrEmpty(template)) return;

            string lower = template.ToLowerInvariant();
            if (lower.Contains("if(") || lower.Contains("if (") || lower.Contains("function")
                || lower.Contains("return ") || template.Contains("=>") || template.Contains("$("))
            {
                ctx.Bag.Error(DiagnosticCodes.DatEventTemplateSyntax,
                    "模板里出现了表达式 / 脚本痕迹（V0.1 只允许 {RoleId.DisplayName} 与 {Context.Key} 两种占位，"
                    + "不允许 if / 函数调用 / C# / Lua / 任意表达式脚本）。",
                    Ctx_(row), column);
                return;
            }

            bool inBrace = false;

            for (int i = 0; i < template.Length; i++)
            {
                char c = template[i];

                if (c == '{')
                {
                    if (inBrace)
                    {
                        ctx.Bag.Error(DiagnosticCodes.DatEventTemplateSyntax,
                            "模板占位符不得嵌套（发现第二个 '{'）。", Ctx_(row), column);
                        return;
                    }
                    inBrace = true;
                    continue;
                }

                if (c == '}')
                {
                    if (!inBrace)
                    {
                        ctx.Bag.Error(DiagnosticCodes.DatEventTemplateSyntax,
                            "模板出现没有对应 '{' 的 '}'：大括号必须成对。", Ctx_(row), column);
                        return;
                    }
                    inBrace = false;
                    continue;
                }

                if (!inBrace) continue;

                bool allowed = (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z')
                    || (c >= '0' && c <= '9') || c == '_' || c == '.';

                if (!allowed)
                {
                    ctx.Bag.Error(DiagnosticCodes.DatEventTemplateSyntax,
                        "模板占位符里出现非法字符 '" + c + "'（只允许 {RoleId.DisplayName} / {Context.Key} 这种形状）。",
                        Ctx_(row), column);
                    return;
                }
            }

            if (inBrace)
            {
                ctx.Bag.Error(DiagnosticCodes.DatEventTemplateSyntax,
                    "模板的 '{' 没有闭合：大括号必须成对。", Ctx_(row), column);
            }

            if (template.Contains("{}"))
            {
                ctx.Bag.Error(DiagnosticCodes.DatEventTemplateSyntax,
                    "模板里出现空的占位符 {}：必须写明键（例如 {Context.Key}）。", Ctx_(row), column);
            }
        }

        // ---------------- 模板 Role 引用硬校验（WP-Event-1D §27） ----------------

        private const string TemplateRoleSuffix = ".DisplayName";
        private const string TemplateContextPrefix = "Context.";

        /// <summary>
        /// **模板 Role 引用硬校验**：模板里所有 `{X.DisplayName}` 的 X 必须
        /// 1) 是真实 `EventRole.RoleId`；2) 属于该模板所属的同一个 Event。
        ///
        /// 为什么必须硬错误：Runtime 侧 `EventTemplateRenderer` 对查不到的 Role 是 **Fail Closed**
        /// （不渲染卡面）；若允许 `{RoleId.DisplayName}` 这种**文档元变量名字面量**留在内容里，
        /// 产品会静默不显示事件，只有运行期才暴露。
        ///
        /// `{Context.X}` 本轮**不**要求静态证明 Key 存在（Key 由 Trigger Adapter 提供），
        /// 只做语法检查（见 `CheckTemplate`），缺失由 Runtime Renderer Fail Closed。
        /// </summary>
        private static void CheckTemplateRoleRefs(Ctx ctx, CompiledRow row, string column, string template,
            string eventId)
        {
            if (string.IsNullOrEmpty(template)) return;

            // 行本身无法归属到某个 Event（EventId 缺失 / ref 未解析）→ 由各自结构诊断负责，不重复报错。
            if (string.IsNullOrEmpty(eventId)) return;

            int index = 0;
            while (index < template.Length)
            {
                if (template[index] != '{')
                {
                    index++;
                    continue;
                }

                int close = template.IndexOf('}', index + 1);
                if (close < 0) return;   // 未闭合 → 语法诊断已覆盖

                string body = template.Substring(index + 1, close - index - 1).Trim();
                index = close + 1;

                if (body.Length == 0) continue;
                if (body.IndexOf('{') >= 0 || body.IndexOf('}') >= 0) continue;
                if (body.StartsWith(TemplateContextPrefix, StringComparison.Ordinal)) continue;
                if (!body.EndsWith(TemplateRoleSuffix, StringComparison.Ordinal)) continue;

                string roleId = body.Substring(0, body.Length - TemplateRoleSuffix.Length);
                if (roleId.Length == 0) continue;

                string ownerEventId;
                if (!ctx.RoleEventById.TryGetValue(roleId, out ownerEventId))
                {
                    string example = ExampleRoleOfEvent(ctx, eventId);
                    ctx.Bag.Error(DiagnosticCodes.DatEventTemplateRoleRef,
                        "模板占位符 {" + roleId + ".DisplayName} 引用了**不存在的** EventRole '" + roleId
                        + "'（事件 " + eventId + " 没有这个 Role）。"
                        + "模板里的 RoleId 必须是真实 EventRole.RoleId，不是文档里的示例名字"
                        + "（{<RoleId>.DisplayName} 只是语法说明）。"
                        + (example != null ? "请改用本事件真实 Role，例如 {" + example + ".DisplayName}。" : ""),
                        Ctx_(row), column, "TemplateRole∈EventRole(same Event)");
                    continue;
                }

                if (!string.Equals(ownerEventId, eventId, StringComparison.Ordinal))
                {
                    ctx.Bag.Error(DiagnosticCodes.DatEventTemplateRoleRef,
                        "模板占位符 {" + roleId + ".DisplayName} 引用的 Role '" + roleId + "' 属于事件 '"
                        + ownerEventId + "'，不属于本模板所属事件 '" + eventId + "'。"
                        + "Role 引用必须与本 Event 的 EventRole 一致。",
                        Ctx_(row), column, "TemplateRole.EventId==Template.EventId");
                }
            }
        }

        /// <summary>取该事件的一个真实 RoleId 作为可操作示例（无则 null）。</summary>
        private static string ExampleRoleOfEvent(Ctx ctx, string eventId)
        {
            foreach (var pair in ctx.RoleEventById)
            {
                if (string.Equals(pair.Value, eventId, StringComparison.Ordinal)) return pair.Key;
            }
            return null;
        }

        // ---------------- 选项表 / 结果表 ----------------

        private static void ValidateOptions(Ctx ctx)
        {
            var firstRowByKey = new Dictionary<string, int>(StringComparer.Ordinal);

            foreach (var row in ctx.Option.Rows)
            {
                string eventId = Text(row, "EventId");
                string optionId = Text(row, "OptionId");
                string unavailable = Text(row, "UnavailableMode");

                if (unavailable != null && !Contains(AllowedUnavailableModes, unavailable))
                {
                    ctx.Bag.Error(DiagnosticCodes.DatEventOptionSortOrder,
                        "UnavailableMode 未知取值 '" + unavailable + "'（允许："
                        + string.Join(" / ", AllowedUnavailableModes) + "）。",
                        Ctx_(row), "UnavailableMode");
                }

                long? sortOrder = Int(row, "SortOrder");
                if (!string.IsNullOrEmpty(eventId) && sortOrder != null)
                {
                    string key = eventId + "\u001f" + sortOrder.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
                    int firstRow;
                    if (firstRowByKey.TryGetValue(key, out firstRow))
                    {
                        ctx.Bag.Error(DiagnosticCodes.DatEventOptionSortOrder,
                            "事件 " + eventId + " 的选项 SortOrder = " + sortOrder.Value + " 重复"
                            + "（已出现在 Row " + firstRow + "，本行 Row " + row.ExcelRow + "）："
                            + "同一事件的选项顺序必须唯一，否则玩家看到的顺序不确定。",
                            Ctx_(row), "SortOrder", "SortOrder unique per Event");
                    }
                    else
                    {
                        firstRowByKey[key] = row.ExcelRow;
                    }
                }

                string labelTemplate = Text(row, "LabelTemplate");
                string subLabelTemplate = Text(row, "SubLabelTemplate");
                CheckTemplate(ctx, row, "LabelTemplate", labelTemplate);
                CheckTemplate(ctx, row, "SubLabelTemplate", subLabelTemplate);
                CheckTemplateRoleRefs(ctx, row, "LabelTemplate", labelTemplate, eventId);
                CheckTemplateRoleRefs(ctx, row, "SubLabelTemplate", subLabelTemplate, eventId);
            }
        }

        private static void ValidateOutcomes(Ctx ctx)
        {
            foreach (var optionRow in ctx.Option.Rows)
            {
                string optionId = Text(optionRow, "OptionId");
                if (string.IsNullOrEmpty(optionId)) continue;

                if (CountOf(ctx.OutcomeCountByOption, optionId) == 0)
                {
                    ctx.Bag.Error(DiagnosticCodes.DatEventOptionWithoutOutcome,
                        "选项 " + optionId + " 没有任何 EventOutcome：玩家的选择不会有任何结果。"
                        + "所有选项都必须经过 Outcome（即使只是记录玩家选择）。",
                        Ctx_(optionRow), "OptionId", "Option=>Outcome>=1");
                }
            }

            foreach (var row in ctx.Outcome.Rows)
            {
                string resultTemplate = Text(row, "ResultTextTemplate");
                CheckTemplate(ctx, row, "ResultTextTemplate", resultTemplate);

                string outcomeOptionId = Text(row, "OptionId");
                string outcomeEventId = null;
                if (!string.IsNullOrEmpty(outcomeOptionId))
                {
                    ctx.OptionEventById.TryGetValue(outcomeOptionId, out outcomeEventId);
                }
                CheckTemplateRoleRefs(ctx, row, "ResultTextTemplate", resultTemplate, outcomeEventId);
            }
        }

        // ---------------- 效果表：类型 / 参数形状 / 顺序 / 域约束 / 引用 ----------------

        private static void ValidateEffects(Ctx ctx)
        {
            var byOutcome = new Dictionary<string, List<CompiledRow>>(StringComparer.Ordinal);

            foreach (var row in ctx.Effect.Rows)
            {
                string outcomeId = Text(row, "OutcomeId");
                string effectType = Text(row, "EffectType");
                string targetRoleId = Text(row, "TargetRoleId");
                string targetKey = Text(row, "TargetKey");
                long? intValue = Int(row, "IntValue");
                string stringValue = Text(row, "StringValue");

                if (!string.IsNullOrEmpty(outcomeId))
                {
                    List<CompiledRow> list;
                    if (!byOutcome.TryGetValue(outcomeId, out list))
                    {
                        list = new List<CompiledRow>();
                        byOutcome[outcomeId] = list;
                    }
                    list.Add(row);
                }

                if (effectType == null) continue;   // 必填为空已由 DAT001 报出

                if (!Contains(AllowedEffectTypes, effectType))
                {
                    ctx.Bag.Error(DiagnosticCodes.DatEventEffectShape,
                        "EffectType 未知取值 '" + effectType + "'（V0.1 允许："
                        + string.Join(" / ", AllowedEffectTypes) + "）。"
                        + "QueueEvent 等未来效果不在 V0.1 词表内，不得静默进入 Mirror。",
                        Ctx_(row), "EffectType");
                    continue;
                }

                CheckEffectShape(ctx, row, effectType, targetRoleId, targetKey, intValue, stringValue);

                string eventId = EventIdOfOutcome(ctx, outcomeId);
                string domain = DomainOf(ctx, eventId);

                if (domain == DomainVillage && Contains(ExpeditionOnlyEffectTypes, effectType))
                {
                    ctx.Bag.Error(DiagnosticCodes.DatEventEffectDomainMismatch,
                        "Village 事件 " + eventId + " 不得使用 Expedition 专属效果 " + effectType + "。",
                        Ctx_(row), "EffectType", DomainVillage + " 不允许 " + effectType);
                }
                else if (domain == DomainExpedition && Contains(VillageOnlyEffectTypes, effectType))
                {
                    ctx.Bag.Error(DiagnosticCodes.DatEventEffectDomainMismatch,
                        "Expedition 事件 " + eventId + " 不得使用 Village 专属效果 " + effectType + "。",
                        Ctx_(row), "EffectType", DomainExpedition + " 不允许 " + effectType);
                }

                CheckRoleRef(ctx, row, "TargetRoleId", targetRoleId, eventId);
            }

            foreach (var pair in byOutcome)
            {
                ValidateOutcomeEffectOrder(ctx, pair.Key, pair.Value);
            }
        }

        /// <summary>按 EffectType 校验参数形状：该填的必须填、不该有的必须空。</summary>
        private static void CheckEffectShape(Ctx ctx, CompiledRow row, string effectType, string targetRoleId,
            string targetKey, long? intValue, string stringValue)
        {
            bool hasRole = !string.IsNullOrEmpty(targetRoleId);
            bool hasKey = !string.IsNullOrEmpty(targetKey);
            bool hasInt = intValue != null;
            bool hasString = !string.IsNullOrEmpty(stringValue);

            switch (effectType)
            {
                case "SetFlag":
                case "ClearFlag":
                    if (!hasKey) Error(ctx, row, effectType, "必须填写 TargetKey（要设置 / 清除的 Flag 名）。", "TargetKey");
                    if (hasRole) Error(ctx, row, effectType, "不使用 TargetRoleId。", "TargetRoleId");
                    if (hasInt) Error(ctx, row, effectType, "不使用 IntValue。", "IntValue");
                    if (hasString) Error(ctx, row, effectType, "不使用 StringValue。", "StringValue");
                    break;

                case "VillageResourceDelta":
                    if (!hasKey)
                    {
                        Error(ctx, row, effectType, "必须填写 TargetKey（村庄资源名）。", "TargetKey");
                    }
                    else if (!Contains(AllowedVillageResourceKeys, targetKey))
                    {
                        ctx.Bag.Error(DiagnosticCodes.DatEventEffectShape,
                            "TargetKey '" + targetKey + "' 不是合法的村庄资源（V0.1 只允许："
                            + string.Join(" / ", AllowedVillageResourceKeys) + "）。"
                            + "不得恢复旧 Wood / Stone / Gold Authority。",
                            Ctx_(row), "TargetKey", "TargetKey∈{Ingredients,Food}");
                    }
                    if (!hasInt || intValue.Value == 0)
                    {
                        Error(ctx, row, effectType, "必须填写非 0 的 IntValue（增减量，可为负）。", "IntValue");
                    }
                    if (hasRole) Error(ctx, row, effectType, "不使用 TargetRoleId。", "TargetRoleId");
                    if (hasString) Error(ctx, row, effectType, "不使用 StringValue。", "StringValue");
                    break;

                case "GrantPersonRest":
                    if (!hasRole) Error(ctx, row, effectType, "必须填写 TargetRoleId（谁获得恢复）。", "TargetRoleId");
                    if (hasKey) Error(ctx, row, effectType, "不使用 TargetKey。", "TargetKey");
                    if (hasInt) Error(ctx, row, effectType,
                        "不使用 IntValue（具体 Rest 窗口服从当前 Village Rule，不在事件表另造第二套时间参数）。", "IntValue");
                    if (hasString) Error(ctx, row, effectType, "不使用 StringValue。", "StringValue");
                    break;

                case "SupplyDelta":
                    if (!hasInt || intValue.Value == 0)
                    {
                        Error(ctx, row, effectType, "必须填写非 0 的 IntValue（补给增减量）。", "IntValue");
                    }
                    if (hasRole) Error(ctx, row, effectType, "不使用 TargetRoleId。", "TargetRoleId");
                    if (hasKey) Error(ctx, row, effectType, "不使用 TargetKey。", "TargetKey");
                    if (hasString) Error(ctx, row, effectType, "不使用 StringValue。", "StringValue");
                    break;

                case "AdvanceExpeditionTime":
                    if (!hasInt || intValue.Value <= 0)
                    {
                        Error(ctx, row, effectType, "必须填写大于 0 的 IntValue（推进量）。", "IntValue");
                    }
                    if (hasRole) Error(ctx, row, effectType, "不使用 TargetRoleId。", "TargetRoleId");
                    if (hasKey) Error(ctx, row, effectType, "不使用 TargetKey。", "TargetKey");
                    if (hasString) Error(ctx, row, effectType, "不使用 StringValue。", "StringValue");
                    break;

                case "StartBattle":
                    if (!hasKey)
                    {
                        Error(ctx, row, effectType, "必须填写 TargetKey（EncounterPool.PoolId）。", "TargetKey");
                    }
                    else if (ctx.PoolIds == null || !ctx.PoolIds.Contains(targetKey))
                    {
                        ctx.Bag.Error(DiagnosticCodes.DatEventAssetRefUnresolved,
                            "StartBattle 的 TargetKey '" + targetKey + "' 在 EncounterPool.PoolId 中不存在"
                            + "（遭遇池 Authority：Development/Config/RRPG_Encounters.xlsx）。"
                            + "StartBattle 只能引用现有遭遇池，不得在事件表自带编队。",
                            Ctx_(row), "TargetKey", "TargetKey∈EncounterPool");
                    }
                    if (hasRole) Error(ctx, row, effectType, "不使用 TargetRoleId。", "TargetRoleId");
                    if (hasInt) Error(ctx, row, effectType, "不使用 IntValue。", "IntValue");
                    if (hasString) Error(ctx, row, effectType, "不使用 StringValue。", "StringValue");
                    break;
            }
        }

        private static void Error(Ctx ctx, CompiledRow row, string effectType, string reason, string column)
        {
            ctx.Bag.Error(DiagnosticCodes.DatEventEffectShape, effectType + " " + reason, Ctx_(row), column);
        }

        /// <summary>
        /// 同一 Outcome 内的效果顺序规则：
        ///   - Order 必须唯一；
        ///   - 最多一个 StartBattle；
        ///   - StartBattle 必须是该 Outcome 下 Order 最大的效果（其它效果先结算，最后才进入战斗）。
        /// </summary>
        private static void ValidateOutcomeEffectOrder(Ctx ctx, string outcomeId, List<CompiledRow> rows)
        {
            var firstRowByOrder = new Dictionary<long, int>();
            var orderByRow = new Dictionary<CompiledRow, long>();

            int startBattleCount = 0;
            long maxOrder = long.MinValue;
            CompiledRow startBattleRow = null;

            foreach (var row in rows)
            {
                long? order = Int(row, "Order");
                if (order != null)
                {
                    int firstRow;
                    if (firstRowByOrder.TryGetValue(order.Value, out firstRow))
                    {
                        ctx.Bag.Error(DiagnosticCodes.DatEventEffectOrder,
                            "结果 " + outcomeId + " 内 Order = " + order.Value + " 重复"
                            + "（已出现在 Row " + firstRow + "，本行 Row " + row.ExcelRow + "）："
                            + "同一结果的执行顺序必须唯一，否则执行次序不确定。",
                            Ctx_(row), "Order", "Order unique per Outcome");
                    }
                    else
                    {
                        firstRowByOrder[order.Value] = row.ExcelRow;
                    }

                    orderByRow[row] = order.Value;
                    if (order.Value > maxOrder) maxOrder = order.Value;
                }

                if (string.Equals(Text(row, "EffectType"), "StartBattle", StringComparison.Ordinal))
                {
                    startBattleCount++;
                    startBattleRow = row;
                }
            }

            if (startBattleCount > 1)
            {
                ctx.Bag.Error(DiagnosticCodes.DatEventEffectOrder,
                    "结果 " + outcomeId + " 有 " + startBattleCount + " 个 StartBattle："
                    + "一个结果最多只能开始一场战斗。",
                    Ctx_(startBattleRow), "EffectType", "StartBattle<=1 per Outcome");
            }

            if (startBattleRow != null && startBattleCount == 1)
            {
                long order;
                if (orderByRow.TryGetValue(startBattleRow, out order) && order != maxOrder)
                {
                    ctx.Bag.Error(DiagnosticCodes.DatEventEffectOrder,
                        "StartBattle 必须是同一结果（" + outcomeId + "）下 Order 最大的效果（当前 Order = "
                        + order + "，最大 Order = " + maxOrder + "）："
                        + "战斗必须最后开始，其它效果先完成结算。",
                        Ctx_(startBattleRow), "Order", "StartBattle=>Order=max");
                }
            }
        }

        private static string EventIdOfOutcome(Ctx ctx, string outcomeId)
        {
            if (string.IsNullOrEmpty(outcomeId)) return null;

            string optionId;
            if (!ctx.OutcomeOptionById.TryGetValue(outcomeId, out optionId)) return null;
            if (string.IsNullOrEmpty(optionId)) return null;

            string eventId;
            return ctx.OptionEventById.TryGetValue(optionId, out eventId) ? eventId : null;
        }

        // ---------------- 索引 / 取值工具 ----------------

        private static CompiledSheet Find(List<CompiledSheet> sheets, string sheetName)
        {
            for (int i = 0; i < sheets.Count; i++)
            {
                if (string.Equals(sheets[i].Spec.SheetName, sheetName, StringComparison.Ordinal)) return sheets[i];
            }
            return null;
        }

        private static CompiledSheet FirstPresent(CompiledSheet[] sheets)
        {
            for (int i = 0; i < sheets.Length; i++) if (sheets[i] != null) return sheets[i];
            return null;
        }

        /// <summary>目标 Sheet 的 id 列取值集合；Sheet 不存在（或无 id 列）时返回 null（调用方必须显式处理）。</summary>
        private static ISet<string> IdsOf(CompiledSheet sheet)
        {
            if (sheet == null) return null;

            FieldSpec idField = null;
            for (int i = 0; i < sheet.Spec.Fields.Count; i++)
            {
                if (sheet.Spec.Fields[i].Kind == FieldKind.Id) { idField = sheet.Spec.Fields[i]; break; }
            }
            if (idField == null) return null;

            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var row in sheet.Rows)
            {
                var cell = row.Get(idField.Field);
                if (cell != null && cell.Value is string s && s.Length > 0) ids.Add(s);
            }
            return ids;
        }

        /// <summary>id 列 → 关系列 的映射（用于「Role 属于哪个 Event」这类同域约束）。</summary>
        private static Dictionary<string, string> Relation(CompiledSheet sheet, string idField, string relationField)
        {
            var map = new Dictionary<string, string>(StringComparer.Ordinal);
            if (sheet == null) return map;

            foreach (var row in sheet.Rows)
            {
                string id = Text(row, idField);
                if (string.IsNullOrEmpty(id)) continue;

                map[id] = Text(row, relationField);
            }
            return map;
        }

        private static ISet<string> KeySet(Dictionary<string, string> map)
        {
            var set = new HashSet<string>(StringComparer.Ordinal);
            foreach (var pair in map) set.Add(pair.Key);
            return set;
        }

        private static CompiledRow FirstRowWithEvent(CompiledSheet sheet, string eventId)
        {
            foreach (var row in sheet.Rows)
            {
                if (string.Equals(Text(row, "EventId"), eventId, StringComparison.Ordinal)) return row;
            }
            return sheet.Rows.Count > 0 ? sheet.Rows[0] : null;
        }

        private static DiagContext Ctx_(CompiledRow row)
        {
            if (row == null || row.Sheet == null) return DiagContext.Of(null, null, 0);
            return DiagContext.Of(row.Sheet.Workbook, row.Sheet.SheetName, row.ExcelRow);
        }

        private static string Text(CompiledRow row, string field)
        {
            var cell = row.Get(field);
            if (cell == null) return null;
            if (cell.Value is string s) return s;

            // 兜底：使用**作者字面输入**。
            // 当 Row 3 的 allowed=（DAT012）或必填检查（DAT001）已经报错时，cell.Value 会变成 null；
            // 领域层仍必须用作者实际写的文本给出自己的精确原因（绝不静默接受未知枚举）。
            return string.IsNullOrEmpty(cell.Raw) ? null : cell.Raw;
        }

        private static long? Int(CompiledRow row, string field)
        {
            var cell = row.Get(field);
            if (cell == null || cell.Value == null) return null;
            if (cell.Value is long l) return l;
            return null;
        }

        private static bool? Bool(CompiledRow row, string field)
        {
            var cell = row.Get(field);
            if (cell == null || cell.Value == null) return null;
            if (cell.Value is bool b) return b;
            return null;
        }

        private static bool Contains(string[] allowed, string value)
        {
            if (value == null) return false;
            for (int i = 0; i < allowed.Length; i++)
            {
                if (string.Equals(allowed[i], value, StringComparison.Ordinal)) return true;
            }
            return false;
        }














    }
}

