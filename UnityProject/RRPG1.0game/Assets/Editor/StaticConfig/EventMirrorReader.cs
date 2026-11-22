using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using RRPG.StaticConfig;

namespace RRPG.StaticConfig.EditorTools
{
    /// <summary>
    /// 从 Canonical Mirror 读出 Event 领域（WP-Event-1A）的 10 张表（Editor-only）。
    ///
    /// 读取的文件：Event.json / EventTrigger.json / EventRole.json / EventRoleCondition.json /
    /// EventCondition.json / EventPresentation.json / EventOption.json / EventOptionCondition.json /
    /// EventOutcome.json / EventEffect.json。
    ///
    /// 校验边界（**与 Compiler 的分工，刻意不重复**）：
    ///   - Compiler 是**领域语义 Authority**：类型 / 范围 / allowed= / 跨列结构 / Domain 约束 /
    ///     条件与效果参数形状 / 顺序规则 / 条件性引用（Role、EncounterPool、VisualAsset）/ 模板结构，
    ///     全部在 XLSX → Mirror 阶段硬失败；
    ///   - 本类只做 Builder 必须自己保证的**自洽性**（第二道防线，全部发生在任何写入之前）：
    ///       1. 结构：10 个文件必须**同时存在或同时不存在**；每个文件的列集合必须精确匹配；rowCount 与行数一致；
    ///       2. 身份：每个文件内主键不得重复；身份列与关系列不得为空；
    ///       3. 引用完整性：Trigger/Role/Condition/Option/OptionCondition/Presentation →
    ///          Event / Role / Option / Outcome 必须能在同一份 Mirror 里解析到；
    ///       4. 可选参数的存在性（Mirror 的裸 null ↔ HasIntValue）。
    ///   枚举取值域**不在这里重复实现**：1A 的 Builder 不按取值分支（只做静态装载），
    ///   若未来 Builder 开始按枚举分支，必须同时把取值域带过来（避免第二份词表漂移）。
    ///
    /// 缺失语义：10 个文件都不存在 = 该领域尚未迁移（**合法空态**，不生成库、不报错）；
    /// 只存在一部分 = Mirror 不完整 = Hard Error（Builder 必须在写入前停下）。
    /// </summary>
    internal static class EventMirrorReader
    {
        public const string EventSheet = "Event";
        public const string EventTriggerSheet = "EventTrigger";
        public const string EventRoleSheet = "EventRole";
        public const string EventRoleConditionSheet = "EventRoleCondition";
        public const string EventConditionSheet = "EventCondition";
        public const string EventPresentationSheet = "EventPresentation";
        public const string EventOptionSheet = "EventOption";
        public const string EventOptionConditionSheet = "EventOptionCondition";
        public const string EventOutcomeSheet = "EventOutcome";
        public const string EventEffectSheet = "EventEffect";

        public static readonly string[] SheetNames =
        {
            EventSheet, EventTriggerSheet, EventRoleSheet, EventRoleConditionSheet, EventConditionSheet,
            EventPresentationSheet, EventOptionSheet, EventOptionConditionSheet, EventOutcomeSheet, EventEffectSheet
        };

        public static readonly string[] EventFields =
            { "EventId", "DisplayName", "Enabled", "Weight", "RepeatPolicy", "CooldownType", "CooldownValue" };

        public static readonly string[] TriggerFields = { "TriggerId", "EventId", "TriggerType" };

        public static readonly string[] RoleFields =
            { "RoleId", "EventId", "RoleName", "SelectorType", "SourceKey", "FixedPersonId" };

        public static readonly string[] RoleConditionFields =
            { "ConditionId", "RoleId", "ConditionType", "TargetKey", "IntValue", "StringValue" };

        public static readonly string[] ConditionFields =
            { "ConditionId", "EventId", "GroupId", "ConditionType", "SubjectRoleId", "TargetKey", "IntValue", "StringValue" };

        public static readonly string[] PresentationFields =
            { "EventId", "PresentationChannel", "TitleTemplate", "BodyTemplate", "IllustrationAssetId", "Urgency", "PauseOnOpen" };

        public static readonly string[] OptionFields =
            { "OptionId", "EventId", "SortOrder", "LabelTemplate", "SubLabelTemplate", "UnavailableMode" };

        public static readonly string[] OptionConditionFields =
            { "ConditionId", "OptionId", "GroupId", "ConditionType", "SubjectRoleId", "TargetKey", "IntValue", "StringValue" };

        public static readonly string[] OutcomeFields = { "OutcomeId", "OptionId", "Weight", "ResultTextTemplate" };

        public static readonly string[] EffectFields =
            { "EffectId", "OutcomeId", "Order", "EffectType", "TargetRoleId", "TargetKey", "IntValue", "StringValue" };

        private static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(false);

        /// <summary>10 张表的读取结果（Builder 内部输入模型）。</summary>
        public sealed class Payload
        {
            public bool DomainAbsent;

            public string EventSha256 = string.Empty;

            public List<EventDefinition> Events = new List<EventDefinition>();
            public List<EventTriggerDefinition> Triggers = new List<EventTriggerDefinition>();
            public List<EventRoleDefinition> Roles = new List<EventRoleDefinition>();
            public List<EventRoleConditionDefinition> RoleConditions = new List<EventRoleConditionDefinition>();
            public List<EventConditionDefinition> Conditions = new List<EventConditionDefinition>();
            public List<EventPresentationDefinition> Presentations = new List<EventPresentationDefinition>();
            public List<EventOptionDefinition> Options = new List<EventOptionDefinition>();
            public List<EventOptionConditionDefinition> OptionConditions = new List<EventOptionConditionDefinition>();
            public List<EventOutcomeDefinition> Outcomes = new List<EventOutcomeDefinition>();
            public List<EventEffectDefinition> Effects = new List<EventEffectDefinition>();

            public int TotalCount
            {
                get
                {
                    return Events.Count + Triggers.Count + Roles.Count + RoleConditions.Count + Conditions.Count
                        + Presentations.Count + Options.Count + OptionConditions.Count + Outcomes.Count + Effects.Count;
                }
            }
        }

        /// <summary>一张 Mirror 表的原始内容（列集合已校验；rowCount 已与行数一致）。</summary>
        private sealed class RawTable
        {
            public string Sheet;
            public string SourceSha256;
            public List<Dictionary<string, string>> Rows = new List<Dictionary<string, string>>();
        }

        /// <summary>
        /// 读取 Event 领域十张表。
        /// 返回 null = 输入非法（<paramref name="failure"/> 给出可操作原因，Builder 必须在写入前停下）。
        /// </summary>
        public static Payload Read(string mirrorDir, string mirrorRelative, out string failure)
        {
            failure = string.Empty;

            var payload = new Payload();
            var present = new List<string>();
            var missing = new List<string>();

            foreach (string sheet in SheetNames)
            {
                if (File.Exists(Path.Combine(mirrorDir, sheet + ".json"))) present.Add(sheet);
                else missing.Add(sheet + ".json");
            }

            if (present.Count == 0)
            {
                payload.DomainAbsent = true;
                return payload;   // 领域尚未迁移 = 合法空态
            }

            if (missing.Count > 0)
            {
                failure = "Event Mirror 不完整：缺少 " + string.Join(" / ", missing)
                    + "（Event 领域的十张表必须同时存在——缺表会让事件语义静默缺失）。";
                return null;
            }

            if (!ReadEvents(mirrorDir, mirrorRelative, payload, out failure)) return null;
            if (!ReadTriggers(mirrorDir, mirrorRelative, payload, out failure)) return null;
            if (!ReadRoles(mirrorDir, mirrorRelative, payload, out failure)) return null;
            if (!ReadRoleConditions(mirrorDir, mirrorRelative, payload, out failure)) return null;
            if (!ReadConditions(mirrorDir, mirrorRelative, payload, out failure)) return null;
            if (!ReadPresentations(mirrorDir, mirrorRelative, payload, out failure)) return null;
            if (!ReadOptions(mirrorDir, mirrorRelative, payload, out failure)) return null;
            if (!ReadOptionConditions(mirrorDir, mirrorRelative, payload, out failure)) return null;
            if (!ReadOutcomes(mirrorDir, mirrorRelative, payload, out failure)) return null;
            if (!ReadEffects(mirrorDir, mirrorRelative, payload, out failure)) return null;

            if (!CrossCheckReferences(payload, out failure)) return null;
            return payload;
        }

        /// <summary>读取单张表：存在性 / 生成标识 / sheet 名 / 列集合 / rowCount 一致性。</summary>
        private static bool ReadTable(string mirrorDir, string mirrorRelative, string sheet, string[] expectedFields,
            out RawTable table, out string failure)
        {
            table = null;
            string fileName = sheet + ".json";
            string path = Path.Combine(mirrorDir, fileName);

            if (!File.Exists(path))
            {
                failure = Where(mirrorRelative, sheet, "Mirror 文件不存在：" + fileName);
                return false;
            }

            string json = File.ReadAllText(path, Utf8NoBom);

            string formatVersion;
            string sourceSha256;
            if (!MirrorJsonText.TryReadHeader(json, fileName, out formatVersion, out sourceSha256, out failure))
            {
                failure = Where(mirrorRelative, sheet, failure);
                return false;
            }
            if (!MirrorJsonText.TryReadSheet(json, fileName, sheet, out failure)
                || !MirrorJsonText.TryReadColumns(json, fileName, expectedFields, out failure))
            {
                failure = Where(mirrorRelative, sheet, failure);
                return false;
            }

            int declaredRowCount;
            List<Dictionary<string, string>> rows;
            if (!MirrorJsonText.TryReadRows(json, fileName, "rowCount", "rows", out declaredRowCount, out rows,
                    out failure))
            {
                failure = Where(mirrorRelative, sheet, failure);
                return false;
            }

            table = new RawTable { Sheet = sheet, SourceSha256 = sourceSha256, Rows = rows };
            return true;
        }

        private static string Where(string mirrorRelative, string sheet, string reason)
        {
            return reason + "（目录：" + mirrorRelative + " / 表：" + sheet + "）";
        }

        private static string Cell(IReadOnlyDictionary<string, string> cells, string field)
        {
            return MirrorJsonText.Unquote(cells, field);
        }

        private static bool Fail(string sheet, int rowNumber, string reason, out string failure)
        {
            failure = sheet + " 第 " + rowNumber + " 行：" + reason;
            return false;
        }

        // ---------------- 逐表映射（含 Builder 自洽性校验） ----------------

        private static bool ReadEvents(string mirrorDir, string mirrorRelative, Payload payload, out string failure)
        {
            failure = string.Empty;

            RawTable table;
            if (!ReadTable(mirrorDir, mirrorRelative, EventSheet, EventFields, out table, out failure)) return false;
            payload.EventSha256 = table.SourceSha256 ?? string.Empty;

            var seen = new HashSet<string>(StringComparer.Ordinal);

            for (int i = 0; i < table.Rows.Count; i++)
            {
                int rowNumber = i + 1;
                var cells = table.Rows[i];

                string eventId = Cell(cells, "EventId");
                if (string.IsNullOrEmpty(eventId)) return Fail(EventSheet, rowNumber, "EventId 为空。", out failure);
                if (!seen.Add(eventId))
                    return Fail(EventSheet, rowNumber, "重复 EventId：" + eventId, out failure);

                string displayName = Cell(cells, "DisplayName");
                if (string.IsNullOrEmpty(displayName))
                    return Fail(EventSheet, rowNumber, "DisplayName 为空。", out failure);

                bool enabled;
                if (!MirrorJsonText.TryBool(cells, "Enabled", out enabled))
                    return Fail(EventSheet, rowNumber, "Enabled 不是 bool。", out failure);

                int weight, cooldownValue;
                if (!MirrorJsonText.TryInt(cells, "Weight", out weight))
                    return Fail(EventSheet, rowNumber, "Weight 不是整数。", out failure);
                if (!MirrorJsonText.TryInt(cells, "CooldownValue", out cooldownValue))
                    return Fail(EventSheet, rowNumber, "CooldownValue 不是整数。", out failure);

                string repeatPolicy = Cell(cells, "RepeatPolicy");
                string cooldownType = Cell(cells, "CooldownType");
                if (string.IsNullOrEmpty(repeatPolicy))
                    return Fail(EventSheet, rowNumber, "RepeatPolicy 为空。", out failure);
                if (string.IsNullOrEmpty(cooldownType))
                    return Fail(EventSheet, rowNumber, "CooldownType 为空。", out failure);

                payload.Events.Add(new EventDefinition
                {
                    EventId = eventId,
                    DisplayName = displayName,
                    Enabled = enabled,
                    Weight = weight,
                    RepeatPolicy = repeatPolicy,
                    CooldownType = cooldownType,
                    CooldownValue = cooldownValue
                });
            }

            return true;
        }

        private static bool ReadTriggers(string mirrorDir, string mirrorRelative, Payload payload, out string failure)
        {
            failure = string.Empty;

            RawTable table;
            if (!ReadTable(mirrorDir, mirrorRelative, EventTriggerSheet, TriggerFields, out table, out failure))
                return false;

            var seen = new HashSet<string>(StringComparer.Ordinal);

            for (int i = 0; i < table.Rows.Count; i++)
            {
                int rowNumber = i + 1;
                var cells = table.Rows[i];

                string triggerId = Cell(cells, "TriggerId");
                string eventId = Cell(cells, "EventId");
                string triggerType = Cell(cells, "TriggerType");

                if (string.IsNullOrEmpty(triggerId))
                    return Fail(EventTriggerSheet, rowNumber, "TriggerId 为空。", out failure);
                if (!seen.Add(triggerId))
                    return Fail(EventTriggerSheet, rowNumber, "重复 TriggerId：" + triggerId, out failure);
                if (string.IsNullOrEmpty(eventId))
                    return Fail(EventTriggerSheet, rowNumber, "EventId 为空。", out failure);
                if (string.IsNullOrEmpty(triggerType))
                    return Fail(EventTriggerSheet, rowNumber, "TriggerType 为空。", out failure);

                payload.Triggers.Add(new EventTriggerDefinition
                {
                    TriggerId = triggerId,
                    EventId = eventId,
                    TriggerType = triggerType
                });
            }

            return true;
        }
        private static bool ReadRoles(string mirrorDir, string mirrorRelative, Payload payload, out string failure)
        {
            failure = string.Empty;

            RawTable table;
            if (!ReadTable(mirrorDir, mirrorRelative, EventRoleSheet, RoleFields, out table, out failure))
                return false;

            var seen = new HashSet<string>(StringComparer.Ordinal);

            for (int i = 0; i < table.Rows.Count; i++)
            {
                int rowNumber = i + 1;
                var cells = table.Rows[i];

                string roleId = Cell(cells, "RoleId");
                string eventId = Cell(cells, "EventId");
                string selectorType = Cell(cells, "SelectorType");

                if (string.IsNullOrEmpty(roleId))
                    return Fail(EventRoleSheet, rowNumber, "RoleId 为空。", out failure);
                if (!seen.Add(roleId))
                    return Fail(EventRoleSheet, rowNumber, "重复 RoleId：" + roleId, out failure);
                if (string.IsNullOrEmpty(eventId))
                    return Fail(EventRoleSheet, rowNumber, "EventId 为空。", out failure);
                if (string.IsNullOrEmpty(selectorType))
                    return Fail(EventRoleSheet, rowNumber, "SelectorType 为空。", out failure);

                payload.Roles.Add(new EventRoleDefinition
                {
                    RoleId = roleId,
                    EventId = eventId,
                    RoleName = Cell(cells, "RoleName") ?? string.Empty,
                    SelectorType = selectorType,
                    SourceKey = Cell(cells, "SourceKey"),
                    FixedPersonId = Cell(cells, "FixedPersonId")
                });
            }

            return true;
        }

        private static bool ReadRoleConditions(string mirrorDir, string mirrorRelative, Payload payload,
            out string failure)
        {
            failure = string.Empty;

            RawTable table;
            if (!ReadTable(mirrorDir, mirrorRelative, EventRoleConditionSheet, RoleConditionFields, out table,
                    out failure)) return false;

            var seen = new HashSet<string>(StringComparer.Ordinal);

            for (int i = 0; i < table.Rows.Count; i++)
            {
                int rowNumber = i + 1;
                var cells = table.Rows[i];

                string conditionId = Cell(cells, "ConditionId");
                string roleId = Cell(cells, "RoleId");
                string conditionType = Cell(cells, "ConditionType");

                if (string.IsNullOrEmpty(conditionId))
                    return Fail(EventRoleConditionSheet, rowNumber, "ConditionId 为空。", out failure);
                if (!seen.Add(conditionId))
                    return Fail(EventRoleConditionSheet, rowNumber, "重复 ConditionId：" + conditionId, out failure);
                if (string.IsNullOrEmpty(roleId))
                    return Fail(EventRoleConditionSheet, rowNumber, "RoleId 为空。", out failure);
                if (string.IsNullOrEmpty(conditionType))
                    return Fail(EventRoleConditionSheet, rowNumber, "ConditionType 为空。", out failure);

                int intValue;
                bool hasInt = MirrorJsonText.TryInt(cells, "IntValue", out intValue);

                payload.RoleConditions.Add(new EventRoleConditionDefinition
                {
                    ConditionId = conditionId,
                    RoleId = roleId,
                    ConditionType = conditionType,
                    TargetKey = Cell(cells, "TargetKey"),
                    HasIntValue = hasInt,
                    IntValue = hasInt ? intValue : 0,
                    StringValue = Cell(cells, "StringValue")
                });
            }

            return true;
        }
        private static bool ReadConditions(string mirrorDir, string mirrorRelative, Payload payload, out string failure)
        {
            failure = string.Empty;

            RawTable table;
            if (!ReadTable(mirrorDir, mirrorRelative, EventConditionSheet, ConditionFields, out table, out failure))
                return false;

            var seen = new HashSet<string>(StringComparer.Ordinal);

            for (int i = 0; i < table.Rows.Count; i++)
            {
                int rowNumber = i + 1;
                var cells = table.Rows[i];

                string conditionId = Cell(cells, "ConditionId");
                string eventId = Cell(cells, "EventId");
                string conditionType = Cell(cells, "ConditionType");

                if (string.IsNullOrEmpty(conditionId))
                    return Fail(EventConditionSheet, rowNumber, "ConditionId 为空。", out failure);
                if (!seen.Add(conditionId))
                    return Fail(EventConditionSheet, rowNumber, "重复 ConditionId：" + conditionId, out failure);
                if (string.IsNullOrEmpty(eventId))
                    return Fail(EventConditionSheet, rowNumber, "EventId 为空。", out failure);
                if (string.IsNullOrEmpty(conditionType))
                    return Fail(EventConditionSheet, rowNumber, "ConditionType 为空。", out failure);

                int groupId;
                if (!MirrorJsonText.TryInt(cells, "GroupId", out groupId))
                    return Fail(EventConditionSheet, rowNumber, "GroupId 不是整数。", out failure);

                int intValue;
                bool hasInt = MirrorJsonText.TryInt(cells, "IntValue", out intValue);

                payload.Conditions.Add(new EventConditionDefinition
                {
                    ConditionId = conditionId,
                    EventId = eventId,
                    GroupId = groupId,
                    ConditionType = conditionType,
                    SubjectRoleId = Cell(cells, "SubjectRoleId"),
                    TargetKey = Cell(cells, "TargetKey"),
                    HasIntValue = hasInt,
                    IntValue = hasInt ? intValue : 0,
                    StringValue = Cell(cells, "StringValue")
                });
            }

            return true;
        }

        private static bool ReadPresentations(string mirrorDir, string mirrorRelative, Payload payload,
            out string failure)
        {
            failure = string.Empty;

            RawTable table;
            if (!ReadTable(mirrorDir, mirrorRelative, EventPresentationSheet, PresentationFields, out table,
                    out failure)) return false;

            var seen = new HashSet<string>(StringComparer.Ordinal);

            for (int i = 0; i < table.Rows.Count; i++)
            {
                int rowNumber = i + 1;
                var cells = table.Rows[i];

                string eventId = Cell(cells, "EventId");
                string channel = Cell(cells, "PresentationChannel");
                string title = Cell(cells, "TitleTemplate");
                string body = Cell(cells, "BodyTemplate");
                string urgency = Cell(cells, "Urgency");

                if (string.IsNullOrEmpty(eventId))
                    return Fail(EventPresentationSheet, rowNumber, "EventId 为空。", out failure);
                if (!seen.Add(eventId))
                    return Fail(EventPresentationSheet, rowNumber,
                        "重复 EventId：" + eventId + "（一个事件只能有一条表现）。", out failure);
                if (string.IsNullOrEmpty(channel))
                    return Fail(EventPresentationSheet, rowNumber, "PresentationChannel 为空。", out failure);
                if (string.IsNullOrEmpty(title))
                    return Fail(EventPresentationSheet, rowNumber, "TitleTemplate 为空。", out failure);
                if (string.IsNullOrEmpty(body))
                    return Fail(EventPresentationSheet, rowNumber, "BodyTemplate 为空。", out failure);
                if (string.IsNullOrEmpty(urgency))
                    return Fail(EventPresentationSheet, rowNumber, "Urgency 为空。", out failure);

                bool pauseOnOpen;
                if (!MirrorJsonText.TryBool(cells, "PauseOnOpen", out pauseOnOpen))
                    return Fail(EventPresentationSheet, rowNumber, "PauseOnOpen 不是 bool。", out failure);

                payload.Presentations.Add(new EventPresentationDefinition
                {
                    EventId = eventId,
                    PresentationChannel = channel,
                    TitleTemplate = title,
                    BodyTemplate = body,
                    IllustrationAssetId = Cell(cells, "IllustrationAssetId"),
                    Urgency = urgency,
                    PauseOnOpen = pauseOnOpen
                });
            }

            return true;
        }
        private static bool ReadOptions(string mirrorDir, string mirrorRelative, Payload payload, out string failure)
        {
            failure = string.Empty;

            RawTable table;
            if (!ReadTable(mirrorDir, mirrorRelative, EventOptionSheet, OptionFields, out table, out failure))
                return false;

            var seen = new HashSet<string>(StringComparer.Ordinal);

            for (int i = 0; i < table.Rows.Count; i++)
            {
                int rowNumber = i + 1;
                var cells = table.Rows[i];

                string optionId = Cell(cells, "OptionId");
                string eventId = Cell(cells, "EventId");
                string label = Cell(cells, "LabelTemplate");
                string unavailableMode = Cell(cells, "UnavailableMode");

                if (string.IsNullOrEmpty(optionId))
                    return Fail(EventOptionSheet, rowNumber, "OptionId 为空。", out failure);
                if (!seen.Add(optionId))
                    return Fail(EventOptionSheet, rowNumber, "重复 OptionId：" + optionId, out failure);
                if (string.IsNullOrEmpty(eventId))
                    return Fail(EventOptionSheet, rowNumber, "EventId 为空。", out failure);
                if (string.IsNullOrEmpty(label))
                    return Fail(EventOptionSheet, rowNumber, "LabelTemplate 为空。", out failure);
                if (string.IsNullOrEmpty(unavailableMode))
                    return Fail(EventOptionSheet, rowNumber, "UnavailableMode 为空。", out failure);

                int sortOrder;
                if (!MirrorJsonText.TryInt(cells, "SortOrder", out sortOrder))
                    return Fail(EventOptionSheet, rowNumber, "SortOrder 不是整数。", out failure);

                payload.Options.Add(new EventOptionDefinition
                {
                    OptionId = optionId,
                    EventId = eventId,
                    SortOrder = sortOrder,
                    LabelTemplate = label,
                    SubLabelTemplate = Cell(cells, "SubLabelTemplate"),
                    UnavailableMode = unavailableMode
                });
            }

            return true;
        }

        private static bool ReadOptionConditions(string mirrorDir, string mirrorRelative, Payload payload,
            out string failure)
        {
            failure = string.Empty;

            RawTable table;
            if (!ReadTable(mirrorDir, mirrorRelative, EventOptionConditionSheet, OptionConditionFields, out table,
                    out failure)) return false;

            var seen = new HashSet<string>(StringComparer.Ordinal);

            for (int i = 0; i < table.Rows.Count; i++)
            {
                int rowNumber = i + 1;
                var cells = table.Rows[i];

                string conditionId = Cell(cells, "ConditionId");
                string optionId = Cell(cells, "OptionId");
                string conditionType = Cell(cells, "ConditionType");

                if (string.IsNullOrEmpty(conditionId))
                    return Fail(EventOptionConditionSheet, rowNumber, "ConditionId 为空。", out failure);
                if (!seen.Add(conditionId))
                    return Fail(EventOptionConditionSheet, rowNumber, "重复 ConditionId：" + conditionId, out failure);
                if (string.IsNullOrEmpty(optionId))
                    return Fail(EventOptionConditionSheet, rowNumber, "OptionId 为空。", out failure);
                if (string.IsNullOrEmpty(conditionType))
                    return Fail(EventOptionConditionSheet, rowNumber, "ConditionType 为空。", out failure);

                int groupId;
                if (!MirrorJsonText.TryInt(cells, "GroupId", out groupId))
                    return Fail(EventOptionConditionSheet, rowNumber, "GroupId 不是整数。", out failure);

                int intValue;
                bool hasInt = MirrorJsonText.TryInt(cells, "IntValue", out intValue);

                payload.OptionConditions.Add(new EventOptionConditionDefinition
                {
                    ConditionId = conditionId,
                    OptionId = optionId,
                    GroupId = groupId,
                    ConditionType = conditionType,
                    SubjectRoleId = Cell(cells, "SubjectRoleId"),
                    TargetKey = Cell(cells, "TargetKey"),
                    HasIntValue = hasInt,
                    IntValue = hasInt ? intValue : 0,
                    StringValue = Cell(cells, "StringValue")
                });
            }

            return true;
        }
        private static bool ReadOutcomes(string mirrorDir, string mirrorRelative, Payload payload, out string failure)
        {
            failure = string.Empty;

            RawTable table;
            if (!ReadTable(mirrorDir, mirrorRelative, EventOutcomeSheet, OutcomeFields, out table, out failure))
                return false;

            var seen = new HashSet<string>(StringComparer.Ordinal);

            for (int i = 0; i < table.Rows.Count; i++)
            {
                int rowNumber = i + 1;
                var cells = table.Rows[i];

                string outcomeId = Cell(cells, "OutcomeId");
                string optionId = Cell(cells, "OptionId");

                if (string.IsNullOrEmpty(outcomeId))
                    return Fail(EventOutcomeSheet, rowNumber, "OutcomeId 为空。", out failure);
                if (!seen.Add(outcomeId))
                    return Fail(EventOutcomeSheet, rowNumber, "重复 OutcomeId：" + outcomeId, out failure);
                if (string.IsNullOrEmpty(optionId))
                    return Fail(EventOutcomeSheet, rowNumber, "OptionId 为空。", out failure);

                int weight;
                if (!MirrorJsonText.TryInt(cells, "Weight", out weight))
                    return Fail(EventOutcomeSheet, rowNumber, "Weight 不是整数。", out failure);

                payload.Outcomes.Add(new EventOutcomeDefinition
                {
                    OutcomeId = outcomeId,
                    OptionId = optionId,
                    Weight = weight,
                    ResultTextTemplate = Cell(cells, "ResultTextTemplate")
                });
            }

            return true;
        }

        private static bool ReadEffects(string mirrorDir, string mirrorRelative, Payload payload, out string failure)
        {
            failure = string.Empty;

            RawTable table;
            if (!ReadTable(mirrorDir, mirrorRelative, EventEffectSheet, EffectFields, out table, out failure))
                return false;

            var seen = new HashSet<string>(StringComparer.Ordinal);

            for (int i = 0; i < table.Rows.Count; i++)
            {
                int rowNumber = i + 1;
                var cells = table.Rows[i];

                string effectId = Cell(cells, "EffectId");
                string outcomeId = Cell(cells, "OutcomeId");
                string effectType = Cell(cells, "EffectType");

                if (string.IsNullOrEmpty(effectId))
                    return Fail(EventEffectSheet, rowNumber, "EffectId 为空。", out failure);
                if (!seen.Add(effectId))
                    return Fail(EventEffectSheet, rowNumber, "重复 EffectId：" + effectId, out failure);
                if (string.IsNullOrEmpty(outcomeId))
                    return Fail(EventEffectSheet, rowNumber, "OutcomeId 为空。", out failure);
                if (string.IsNullOrEmpty(effectType))
                    return Fail(EventEffectSheet, rowNumber, "EffectType 为空。", out failure);

                int order;
                if (!MirrorJsonText.TryInt(cells, "Order", out order))
                    return Fail(EventEffectSheet, rowNumber, "Order 不是整数。", out failure);

                int intValue;
                bool hasInt = MirrorJsonText.TryInt(cells, "IntValue", out intValue);

                payload.Effects.Add(new EventEffectDefinition
                {
                    EffectId = effectId,
                    OutcomeId = outcomeId,
                    Order = order,
                    EffectType = effectType,
                    TargetRoleId = Cell(cells, "TargetRoleId"),
                    TargetKey = Cell(cells, "TargetKey"),
                    HasIntValue = hasInt,
                    IntValue = hasInt ? intValue : 0,
                    StringValue = Cell(cells, "StringValue")
                });
            }

            return true;
        }
        /// <summary>
        /// 引用完整性（第二道防线）：所有跨表关系必须能在**同一份 Mirror** 内解析。
        /// 任何悬空引用 = Hard Error（否则 Runtime 会读到指向不存在对象的事件）。
        /// </summary>
        private static bool CrossCheckReferences(Payload payload, out string failure)
        {
            failure = string.Empty;

            var eventIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var e in payload.Events) eventIds.Add(e.EventId);

            var roleIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var r in payload.Roles) roleIds.Add(r.RoleId);

            var optionIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var o in payload.Options) optionIds.Add(o.OptionId);

            var outcomeIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var o in payload.Outcomes) outcomeIds.Add(o.OutcomeId);

            foreach (var t in payload.Triggers)
            {
                if (!eventIds.Contains(t.EventId))
                    return Fail(EventTriggerSheet, 0, "引用了不存在的 EventId：" + t.EventId, out failure);
            }

            foreach (var r in payload.Roles)
            {
                if (!eventIds.Contains(r.EventId))
                    return Fail(EventRoleSheet, 0, "引用了不存在的 EventId：" + r.EventId, out failure);
            }

            foreach (var c in payload.RoleConditions)
            {
                if (!roleIds.Contains(c.RoleId))
                    return Fail(EventRoleConditionSheet, 0, "引用了不存在的 RoleId：" + c.RoleId, out failure);
            }

            foreach (var c in payload.Conditions)
            {
                if (!eventIds.Contains(c.EventId))
                    return Fail(EventConditionSheet, 0, "引用了不存在的 EventId：" + c.EventId, out failure);
                if (!string.IsNullOrEmpty(c.SubjectRoleId) && !roleIds.Contains(c.SubjectRoleId))
                    return Fail(EventConditionSheet, 0, "引用了不存在的 RoleId：" + c.SubjectRoleId, out failure);
            }

            foreach (var p in payload.Presentations)
            {
                if (!eventIds.Contains(p.EventId))
                    return Fail(EventPresentationSheet, 0, "引用了不存在的 EventId：" + p.EventId, out failure);
            }

            foreach (var o in payload.Options)
            {
                if (!eventIds.Contains(o.EventId))
                    return Fail(EventOptionSheet, 0, "引用了不存在的 EventId：" + o.EventId, out failure);
            }

            foreach (var c in payload.OptionConditions)
            {
                if (!optionIds.Contains(c.OptionId))
                    return Fail(EventOptionConditionSheet, 0, "引用了不存在的 OptionId：" + c.OptionId, out failure);
                if (!string.IsNullOrEmpty(c.SubjectRoleId) && !roleIds.Contains(c.SubjectRoleId))
                    return Fail(EventOptionConditionSheet, 0, "引用了不存在的 RoleId：" + c.SubjectRoleId, out failure);
            }

            foreach (var o in payload.Outcomes)
            {
                if (!optionIds.Contains(o.OptionId))
                    return Fail(EventOutcomeSheet, 0, "引用了不存在的 OptionId：" + o.OptionId, out failure);
            }

            foreach (var e in payload.Effects)
            {
                if (!outcomeIds.Contains(e.OutcomeId))
                    return Fail(EventEffectSheet, 0, "引用了不存在的 OutcomeId：" + e.OutcomeId, out failure);
                if (!string.IsNullOrEmpty(e.TargetRoleId) && !roleIds.Contains(e.TargetRoleId))
                    return Fail(EventEffectSheet, 0, "引用了不存在的 RoleId：" + e.TargetRoleId, out failure);
            }

            return true;
        }
    }
}
