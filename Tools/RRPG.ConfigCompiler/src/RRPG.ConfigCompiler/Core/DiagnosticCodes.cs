namespace RRPG.ConfigCompiler.Core
{
    /// <summary>
    /// 诊断代码（稳定机器码，供 BuildReport / CI / AI 消费）。
    /// 命名：前缀 = 阶段（WB / SCH / DAT / OUT / MIR）。
    /// </summary>
    public static class DiagnosticCodes
    {
        // ---- Workbook / 发现 / 包结构 ----
        public const string WbNoWorkbookFound = "WB001_NO_WORKBOOK_FOUND";
        public const string WbFileUnreadable = "WB002_FILE_UNREADABLE";
        public const string WbSheetEmpty = "WB003_SHEET_EMPTY";
        public const string WbSheetSkipped = "WB004_SHEET_SKIPPED";
        public const string WbOpenXmlInvalid = "WB005_OPENXML_INVALID";
        public const string WbDuplicateEntitySheet = "WB006_DUPLICATE_ENTITY_SHEET";
        public const string WbTempFileSkipped = "WB007_TEMP_FILE_SKIPPED";

        // ---- Schema / Rule DSL ----
        public const string SchMissingMachineField = "SCH001_MISSING_MACHINE_FIELD";
        public const string SchDuplicateMachineField = "SCH002_DUPLICATE_MACHINE_FIELD";
        public const string SchTypeRowEmpty = "SCH003_TYPE_ROW_EMPTY";
        public const string SchTypeUnknown = "SCH004_TYPE_UNKNOWN";
        public const string SchTypeMultiple = "SCH005_TYPE_MULTIPLE";
        public const string SchTypeTokenInRuleRow = "SCH006_TYPE_TOKEN_IN_RULE_ROW";
        public const string SchRuleUnknownToken = "SCH007_RULE_UNKNOWN_TOKEN";
        public const string SchRuleDuplicateToken = "SCH008_RULE_DUPLICATE_TOKEN";
        public const string SchRuleConflict = "SCH009_RULE_CONFLICT";
        public const string SchRuleOperatorWhitespace = "SCH010_RULE_OPERATOR_WHITESPACE";
        public const string SchRuleInvalidOperand = "SCH011_RULE_INVALID_OPERAND";
        public const string SchRuleNotApplicable = "SCH012_RULE_NOT_APPLICABLE";
        public const string SchRuleDefaultTypeMismatch = "SCH013_RULE_DEFAULT_TYPE_MISMATCH";
        public const string SchNullabilityUnspecified = "SCH014_NULLABILITY_UNSPECIFIED"; // v1 only：v2 默认必填，已不再产生
        public const string SchMultipleIdColumns = "SCH015_MULTIPLE_ID_COLUMNS";
        public const string SchNoKeyColumn = "SCH016_NO_KEY_COLUMN";
        public const string SchDataWithoutMachineField = "SCH017_DATA_WITHOUT_MACHINE_FIELD";
        public const string SchHeaderRowMissing = "SCH018_HEADER_ROW_MISSING";
        public const string SchDisplayHeaderMissing = "SCH019_DISPLAY_HEADER_MISSING";
        public const string SchRequiredTokenRemoved = "SCH020_REQUIRED_TOKEN_REMOVED";
        public const string SchUniqueTokenRemoved = "SCH021_UNIQUE_TOKEN_REMOVED";
        public const string SchLegacyDefaultArrow = "SCH022_LEGACY_DEFAULT_ARROW";
        public const string SchIdWithKey = "SCH023_ID_WITH_KEY";

        // allowed= 取值域
        public const string SchAllowedNotApplicable = "SCH024_ALLOWED_NOT_APPLICABLE";
        public const string SchAllowedDefaultNotMember = "SCH025_ALLOWED_DEFAULT_NOT_MEMBER";
        public const string SchAllowedEmptyMember = "SCH026_ALLOWED_EMPTY_MEMBER";

        // 列结构与列序（Producer UX）
        public const string SchRequiredFieldMissing = "SCH027_REQUIRED_MACHINE_FIELD_MISSING";
        public const string SchUnrecognizedColumn = "SCH028_UNRECOGNIZED_COLUMN";

        // ---- Data / Cross reference ----
        public const string DatRequiredEmpty = "DAT001_REQUIRED_EMPTY";
        public const string DatTypeMismatch = "DAT002_TYPE_MISMATCH";
        public const string DatMinViolation = "DAT003_MIN_VIOLATION";
        public const string DatMaxViolation = "DAT004_MAX_VIOLATION";
        public const string DatKeyDuplicate = "DAT005_KEY_DUPLICATE";
        public const string DatEmptyRowSkipped = "DAT006_EMPTY_ROW_SKIPPED";
        public const string DatIdEmpty = "DAT007_ID_EMPTY";
        public const string DatRefUnresolved = "DAT008_REF_UNRESOLVED";
        public const string DatRefUnknownSheet = "DAT009_REF_UNKNOWN_SHEET";
        public const string DatEnumUnenforced = "DAT010_ENUM_MEMBERSHIP_UNENFORCED"; // v1 only：v2 已从类型集删除 enum:
        public const string DatFormulaNotAllowed = "DAT011_FORMULA_NOT_ALLOWED";

        // ---- VisualAsset → SourceArt（Builder 输入；M1 领域落地时启用）----
        public const string DatAllowedViolation = "DAT012_ALLOWED_VIOLATION";
        public const string DatSourceArtMissing = "DAT013_SOURCE_ART_MISSING";
        public const string DatSourceArtPathEscape = "DAT014_SOURCE_ART_PATH_ESCAPE";
        public const string DatUnsupportedImportPreset = "DAT015_UNSUPPORTED_IMPORT_PRESET";
        public const string DatSourceArtExtension = "DAT016_SOURCE_ART_EXTENSION";
        public const string DatVisualAssetShape = "DAT017_VISUAL_ASSET_SHAPE";

        // ---- Encounter 领域（WP-Encounter-1A：遭遇生成静态配置基础）----
        // 说明：本组是「跨列 / 条件引用 / 领域取值域」的第二道防线。
        // 能由 Row 3 声明式表达的约束（类型、范围、单列 allowed=、普通 ref）仍由
        // DataCompiler / CrossReferenceValidator 负责，不在这里重复实现。
        public const string DatEncounterBudgetRange = "DAT018_ENCOUNTER_BUDGET_RANGE";
        public const string DatEncounterSlotCountRange = "DAT019_ENCOUNTER_SLOT_COUNT_RANGE";
        public const string DatEncounterSlotCountEmpty = "DAT020_ENCOUNTER_SLOT_COUNT_EMPTY";
        public const string DatEncounterCapacityExceeded = "DAT021_ENCOUNTER_CAPACITY_EXCEEDED";
        public const string DatEncounterSlotSelectionShape = "DAT022_ENCOUNTER_SLOT_SELECTION_SHAPE";
        public const string DatEncounterSlotPositionShape = "DAT023_ENCOUNTER_SLOT_POSITION_SHAPE";
        public const string DatEncounterSlotExactCount = "DAT024_ENCOUNTER_SLOT_EXACT_COUNT";
        public const string DatEncounterRuleClassification = "DAT025_ENCOUNTER_RULE_CLASSIFICATION";
        public const string DatEncounterRuleValue = "DAT026_ENCOUNTER_RULE_VALUE";
        public const string DatEncounterPoolDepthRange = "DAT027_ENCOUNTER_POOL_DEPTH_RANGE";
        public const string DatEncounterDomainShape = "DAT028_ENCOUNTER_DOMAIN_SHAPE";

        /// <summary>
        /// Encounter V0.1（协调层冻结）：Enabled = true 的遭遇必须至少有 1 个组合需求位；
        /// Enabled = false 允许暂时没有 Slot（制作人草稿）。
        /// </summary>
        public const string DatEncounterEnabledWithoutSlot = "DAT029_ENCOUNTER_ENABLED_WITHOUT_SLOT";

        // ---- Event 领域（WP-Event-1A：统一事件内容基础）----
        // 与 Encounter 同一分工：类型 / 范围 / 单列 allowed= / 普通 ref 由 DataCompiler 与
        // CrossReferenceValidator 负责；本组只做「跨列结构 / 条件引用形状 / 领域取值域第二道防线 / 域约束」。
        public const string DatEventDomainShape = "DAT030_EVENT_DOMAIN_SHAPE";
        public const string DatEventEnabledStructure = "DAT031_EVENT_ENABLED_STRUCTURE";
        public const string DatEventOptionWithoutOutcome = "DAT032_EVENT_OPTION_WITHOUT_OUTCOME";
        public const string DatEventRepeatCooldownShape = "DAT033_EVENT_REPEAT_COOLDOWN_SHAPE";
        public const string DatEventTriggerRule = "DAT034_EVENT_TRIGGER_RULE";
        public const string DatEventPresentationChannelMismatch = "DAT035_EVENT_PRESENTATION_CHANNEL_MISMATCH";
        public const string DatEventRoleShape = "DAT036_EVENT_ROLE_SHAPE";
        public const string DatEventRoleCrossEventRef = "DAT037_EVENT_ROLE_CROSS_EVENT_REF";
        public const string DatEventConditionShape = "DAT038_EVENT_CONDITION_SHAPE";
        public const string DatEventRoleRefUnresolved = "DAT039_EVENT_ROLE_REF_UNRESOLVED";
        public const string DatEventEffectShape = "DAT040_EVENT_EFFECT_SHAPE";
        public const string DatEventEffectOrder = "DAT041_EVENT_EFFECT_ORDER";
        public const string DatEventAssetRefUnresolved = "DAT042_EVENT_ASSET_REF_UNRESOLVED";
        public const string DatEventEffectDomainMismatch = "DAT043_EVENT_EFFECT_DOMAIN_MISMATCH";
        public const string DatEventTemplateSyntax = "DAT044_EVENT_TEMPLATE_SYNTAX";
        public const string DatEventOptionSortOrder = "DAT045_EVENT_OPTION_SORT_ORDER";

        /// <summary>
        /// WP-Event-1C：事件历史条件（EventNotSeen / EventSeenCountAtLeast）的 TargetKey 语义补强 ——
        /// TargetKey 必须指向真实存在的 EventId；EventSeenCountAtLeast 的 IntValue 必须 &gt;= 1。
        /// 依据：WP-Event-1 FDS §19.5（1B 冻结「统一查询世界长期事件历史」）。
        /// </summary>
        public const string DatEventConditionTargetRef = "DAT046_EVENT_CONDITION_TARGET_REF";

        /// <summary>
        /// WP-Event-1D §27：模板里的 `{X.DisplayName}` 是**真实 EventRole 引用**，
        /// X 必须是真实 RoleId 且属于同一个 Event（不得再写文档元变量名字面量 `{RoleId.DisplayName}`）。
        /// </summary>
        public const string DatEventTemplateRoleRef = "DAT047_EVENT_TEMPLATE_ROLE_REF";

        // ---- Canonical Mirror 契约（validate：只读比对，FDS §7.2 R4/R5）----
        public const string MirrorMissing = "MIR001_MIRROR_FILE_MISSING";
        public const string MirrorStale = "MIR002_MIRROR_STALE";
        public const string MirrorExtra = "MIR003_MIRROR_EXTRA_FILE";

        // ---- 输出 ----
        public const string OutVerifyFailed = "OUT001_VERIFY_FAILED";
        public const string OutCommitFailed = "OUT002_COMMIT_FAILED";
        public const string OutHardErrorZeroWrite = "OUT003_HARD_ERROR_ZERO_WRITE";
    }
}
