using System;
using System.Collections.Generic;
using System.Linq;

namespace RRPG.ConfigCompiler.Core
{
    /// <summary>
    /// Encounter 领域校验（WP-Encounter-1A：遭遇生成静态配置基础）。
    ///
    /// 职责边界（与既有分工一致）：
    ///   - 类型 / 单列范围（&gt;= &lt;=）/ 单列 allowed= / 普通 ref：由
    ///     <see cref="DataCompiler"/> 与 <see cref="CrossReferenceValidator"/> 负责，本类**不重复实现**；
    ///   - 本类只做三段既有机制表达不了的语义：
    ///       1. **跨列结构**（ThreatBudgetMin ≤ ThreatBudgetMax；CountMax ≥ CountMin；
    ///          MinDepth ≤ MaxDepth；同 Encounter 的 CountMax 总和 ≤ 9）；
    ///       2. **条件引用形状**（FixedMonster 必须有 FixedMonsterId 且必须存在；
    ///          Query 不得填 FixedMonsterId；Exact 必须 Row + Column 且 CountMin = CountMax = 1）；
    ///       3. **领域取值域第二道防线**（SelectionMode / PositionMode / Row / Column / RuleMode /
    ///          Dimension / Value-by-Dimension）。
    ///
    /// 为什么需要第 3 类：`allowed=` 写在 Row 3 是**声明式**的，制作人可能漏写或写歪；
    /// 而 <c>EncounterSlotRule.Value</c> 的合法值取决于同一行的 <c>Dimension</c>（一列对应三套取值域），
    /// 无法用单列 `allowed=` 表达 —— 这与 M3 的 <see cref="VisualImportPresets"/> 是同一模式：
    /// C# 常量表是 Authority，XLSX 的 `allowed=` 是同一份声明的人读面。
    ///
    /// 两层取值域的分工（实测行为，必须有意识保留）：
    ///   - 第 1 层：Row 3 的 `allowed=` → DAT012_ALLOWED_VIOLATION（声明式，cells 值被判为非法后不再进入数据）；
    ///   - 第 2 层：本类的常量表 → 用**作者字面输入**复核，给出领域自己的精确原因。
    ///     因此同一个非法值可能同时出现 DAT012 与一条领域诊断：这不是重复报错，
    ///     而是「声明不同步」与「语义非法」两件事各自被明确说出（绝不静默接受）。
    ///
    /// 硬约束：**从不静默跳过**。缺列、缺值、引用不存在、未知分类值 → 明确 Hard Error +
    /// 定位到 Workbook / Sheet / Row / Column。
    ///
    /// 空态语义：Encounter 领域的 5 张表**全部不存在** = 该领域尚未建立（合法空态，不产生诊断）；
    /// 只存在其中一部分 = 领域不完整（DAT028，硬失败）。
    /// </summary>
    public static class EncounterValidator
    {
        // ---------------- 领域身份 ----------------

        public const string EncounterSheetName = "Encounter";
        public const string EncounterSlotSheetName = "EncounterSlot";
        public const string EncounterSlotRuleSheetName = "EncounterSlotRule";
        public const string EncounterPoolSheetName = "EncounterPool";
        public const string EncounterPoolEntrySheetName = "EncounterPoolEntry";

        /// <summary>Monster 工作簿里的 Encounter 子表（人工 Authority 在 Monster 工作簿，遭遇生成只读消费）。</summary>
        public const string MonsterEncounterProfileSheetName = "MonsterEncounterProfile";
        public const string MonsterRoleSheetName = "MonsterRole";

        /// <summary>Encounter 工作簿必须同时存在的 5 张表（缺任一 = 领域不完整）。</summary>
        public static readonly string[] DomainSheets =
        {
            EncounterSheetName, EncounterSlotSheetName, EncounterSlotRuleSheetName,
            EncounterPoolSheetName, EncounterPoolEntrySheetName
        };

        /// <summary>敌方战场物理容量：3 Row × 3 Column = 9（Battle GameSpec R5.0 / R6.3）。</summary>
        public const int EnemyBoardCapacity = 9;

        // ---------------- 取值域（第一版冻结的最小集合） ----------------

        public static readonly string[] AllowedSelectionModes = { "FixedMonster", "Query" };
        public static readonly string[] AllowedPositionModes = { "Any", "Row", "Exact" };
        public static readonly string[] AllowedRows = { "Front", "Middle", "Back" };
        public static readonly string[] AllowedColumns = { "Left", "Center", "Right" };
        public static readonly string[] AllowedRuleModes = { "AnyOf", "Require", "Exclude" };
        public static readonly string[] AllowedDimensions = { "Role", "Tier", "MonsterId" };
        public static readonly string[] AllowedRoles = { "Melee", "Ranged", "Tank", "Support" };
        public static readonly string[] AllowedTiers = { "Normal", "Elite", "Boss" };

        // ---------------- 入口 ----------------

        public static void Validate(List<CompiledSheet> sheets, DiagnosticBag bag)
        {
            var domain = DomainSheets
                .Select(name => Find(sheets, name))
                .ToList();

            if (domain.All(s => s == null)) return;   // 领域不存在 = 合法空态

            var missing = DomainSheets
                .Where((name, i) => domain[i] == null)
                .ToList();
            if (missing.Count > 0)
            {
                var anchor = domain.First(s => s != null);
                bag.Error(DiagnosticCodes.DatEncounterDomainShape,
                    "Encounter 领域不完整：缺少 Sheet " + string.Join(" / ", missing)
                    + "。Encounter 工作簿必须同时包含 " + string.Join(" / ", DomainSheets)
                    + "（敌人数量的唯一来源是 EncounterSlot 的数量配置，缺表会让遭遇语义静默缺失）。",
                    DiagContext.Of(anchor.Spec.Workbook, anchor.Spec.SheetName, 0));
            }

            var monsterIds = IdsOf(Find(sheets, "Monster"));

            ValidateEncounter(domain[0], bag);
            ValidateEncounterSlot(domain[1], monsterIds, bag);
            ValidateEncounterSlotRule(domain[2], domain[1], monsterIds, bag);
            ValidateEncounterPoolEntry(domain[4], bag);
            ValidateSlotCapacity(domain[1], bag);
            ValidateEnabledEncountersHaveSlots(domain[0], domain[1], bag);
        }

        // ---------------- 逐表校验 ----------------

        /// <summary>Encounter：ThreatBudgetMin ≤ ThreatBudgetMax。</summary>
        private static void ValidateEncounter(CompiledSheet sheet, DiagnosticBag bag)
        {
            if (sheet == null) return;

            foreach (var row in sheet.Rows)
            {
                long? min = Int(row, "ThreatBudgetMin");
                long? max = Int(row, "ThreatBudgetMax");
                if (min == null || max == null) continue;   // 缺失 / 类型错误已由 DAT001 / DAT002 报出

                if (min.Value > max.Value)
                {
                    bag.Error(DiagnosticCodes.DatEncounterBudgetRange,
                        "ThreatBudgetMin（" + min.Value + "）大于 ThreatBudgetMax（" + max.Value + "）。"
                        + "威胁预算是区间：最低值不得高于最高值。"
                        + "（Threat 数值当前为 Test / Not Frozen，但区间合法性始终必须成立。）",
                        Ctx(row), "ThreatBudgetMin", "ThreatBudgetMin<=ThreatBudgetMax");
                }
            }
        }

        /// <summary>EncounterSlot：数量区间 + 选怪方式形状 + 站位形状（含精确站位数量约束）。</summary>
        private static void ValidateEncounterSlot(CompiledSheet sheet, ISet<string> monsterIds, DiagnosticBag bag)
        {
            if (sheet == null) return;

            foreach (var row in sheet.Rows)
            {
                long? countMin = Int(row, "CountMin");
                long? countMax = Int(row, "CountMax");

                if (countMin != null && countMax != null)
                {
                    if (countMax.Value < countMin.Value)
                    {
                        bag.Error(DiagnosticCodes.DatEncounterSlotCountRange,
                            "CountMax（" + countMax.Value + "）小于 CountMin（" + countMin.Value + "）。",
                            Ctx(row), "CountMax", "CountMax>=CountMin");
                    }
                    else if (countMin.Value == 0 && countMax.Value == 0)
                    {
                        bag.Error(DiagnosticCodes.DatEncounterSlotCountEmpty,
                            "CountMin 与 CountMax 不能同时为 0：该组合需求位永远不会产生敌人，"
                            + "等于一个静默失效的配置。请给出至少 1 个敌人的区间，或删除该行。",
                            Ctx(row), "CountMin", "CountMin=0&&CountMax=0");
                    }
                }

                ValidateSlotSelection(row, Text(row, "SelectionMode"), Text(row, "FixedMonsterId"),
                    monsterIds, bag);
                ValidateSlotPosition(row, Text(row, "PositionMode"), Text(row, "Row"),
                    Text(row, "Column"), countMin, countMax, bag);
            }
        }

        /// <summary>选怪方式形状：FixedMonster 必须给出且必须存在；Query 不得给出。</summary>
        private static void ValidateSlotSelection(CompiledRow row, string selectionMode, string fixedMonsterId,
            ISet<string> monsterIds, DiagnosticBag bag)
        {
            if (selectionMode == null) return;   // 必填为空已由 DAT001 报出

            if (!Contains(AllowedSelectionModes, selectionMode))
            {
                bag.Error(DiagnosticCodes.DatEncounterSlotSelectionShape,
                    "SelectionMode 未知取值 '" + selectionMode + "'（第一版允许："
                    + string.Join(" / ", AllowedSelectionModes) + "）。"
                    + "若是新的合法取值，请先在 Compiler 的取值域与 Row 3 的 allowed= 中同步声明。",
                    Ctx(row), "SelectionMode");
                return;
            }

            if (selectionMode == "FixedMonster")
            {
                if (string.IsNullOrEmpty(fixedMonsterId))
                {
                    bag.Error(DiagnosticCodes.DatEncounterSlotSelectionShape,
                        "SelectionMode = FixedMonster 时必须填写 FixedMonsterId（固定怪物ID）。"
                        + "若该位应由条件随机选怪，请改用 SelectionMode = Query。",
                        Ctx(row), "FixedMonsterId");
                }
                else if (monsterIds != null && !monsterIds.Contains(fixedMonsterId))
                {
                    bag.Error(DiagnosticCodes.DatEncounterSlotSelectionShape,
                        "FixedMonsterId '" + fixedMonsterId + "' 在 Monster.MonsterId 中不存在。"
                        + "固定怪物必须引用现有怪物；不得自动补造怪物。",
                        Ctx(row), "FixedMonsterId", "FixedMonsterId∈Monster");
                }
            }
            else if (!string.IsNullOrEmpty(fixedMonsterId))
            {
                bag.Error(DiagnosticCodes.DatEncounterSlotSelectionShape,
                    "SelectionMode = Query 时不得填写 FixedMonsterId（当前：'" + fixedMonsterId + "'）。"
                    + "条件随机与固定怪物是两种互斥语义：请二选一。",
                    Ctx(row), "FixedMonsterId");
            }
        }

        /// <summary>站位形状：Any 不得带指定值；Row 必须给排、不得给列；Exact 必须给排+列且数量恰为 1。</summary>
        private static void ValidateSlotPosition(CompiledRow row, string positionMode, string rowValue,
            string columnValue, long? countMin, long? countMax, DiagnosticBag bag)
        {
            if (string.IsNullOrEmpty(positionMode)) return;   // 必填为空已由 DAT001 报出

            if (!Contains(AllowedPositionModes, positionMode))
            {
                bag.Error(DiagnosticCodes.DatEncounterSlotPositionShape,
                    "PositionMode 未知取值 '" + positionMode + "'（第一版允许："
                    + string.Join(" / ", AllowedPositionModes) + "）。",
                    Ctx(row), "PositionMode");
                return;
            }

            CheckDomainValue(row, "Row", rowValue, AllowedRows, bag);
            CheckDomainValue(row, "Column", columnValue, AllowedColumns, bag);

            if (positionMode == "Any")
            {
                if (!string.IsNullOrEmpty(rowValue) || !string.IsNullOrEmpty(columnValue))
                {
                    bag.Error(DiagnosticCodes.DatEncounterSlotPositionShape,
                        "PositionMode = Any（任意排）时 Row / Column 必须留空（当前 Row='"
                        + Display(rowValue) + "'，Column='" + Display(columnValue) + "'）。"
                        + "任意站位不得携带会被忽略的指定值。",
                        Ctx(row), "Row");
                }
                return;
            }

            if (positionMode == "Row")
            {
                if (string.IsNullOrEmpty(rowValue))
                {
                    bag.Error(DiagnosticCodes.DatEncounterSlotPositionShape,
                        "PositionMode = Row（指定排）时必须填写 Row（"
                        + string.Join(" / ", AllowedRows) + "）。",
                        Ctx(row), "Row");
                }
                if (!string.IsNullOrEmpty(columnValue))
                {
                    bag.Error(DiagnosticCodes.DatEncounterSlotPositionShape,
                        "PositionMode = Row 时 Column 必须留空（当前：'" + columnValue + "'）："
                        + "指定排表示该排内任意列，不能再叠加一个列指定值。",
                        Ctx(row), "Column");
                }
                return;
            }

            // Exact
            if (string.IsNullOrEmpty(rowValue))
            {
                bag.Error(DiagnosticCodes.DatEncounterSlotPositionShape,
                    "PositionMode = Exact（精确格）时必须填写 Row（"
                    + string.Join(" / ", AllowedRows) + "）。",
                    Ctx(row), "Row");
            }
            if (string.IsNullOrEmpty(columnValue))
            {
                bag.Error(DiagnosticCodes.DatEncounterSlotPositionShape,
                    "PositionMode = Exact（精确格）时必须填写 Column（"
                    + string.Join(" / ", AllowedColumns) + "）。",
                    Ctx(row), "Column");
            }
            if (countMin != null && countMax != null && (countMin.Value != 1 || countMax.Value != 1))
            {
                bag.Error(DiagnosticCodes.DatEncounterSlotExactCount,
                    "PositionMode = Exact 必须 CountMin = CountMax = 1（当前 "
                    + countMin.Value + " ~ " + countMax.Value + "）："
                    + "一个精确格只能放一个敌人。若要表达多个位置，请拆成多行（每行一个精确格）。",
                    Ctx(row), "CountMin", "Exact=>CountMin=CountMax=1");
            }
        }

        /// <summary>EncounterSlotRule：RuleMode / Dimension 取值域 + Value 必须属于 Dimension 的取值域。</summary>
        private static void ValidateEncounterSlotRule(CompiledSheet sheet, CompiledSheet slotSheet,
            ISet<string> monsterIds, DiagnosticBag bag)
        {
            if (sheet == null) return;

            var slotIds = IdsOf(slotSheet);

            foreach (var row in sheet.Rows)
            {
                string ruleMode = Text(row, "RuleMode");
                if (ruleMode != null && !Contains(AllowedRuleModes, ruleMode))
                {
                    bag.Error(DiagnosticCodes.DatEncounterRuleClassification,
                        "RuleMode 未知取值 '" + ruleMode + "'（第一版允许："
                        + string.Join(" / ", AllowedRuleModes) + "）。",
                        Ctx(row), "RuleMode");
                }

                string slotId = Text(row, "SlotId");
                if (!string.IsNullOrEmpty(slotId) && slotIds != null && !slotIds.Contains(slotId))
                {
                    // SlotId 列类型是 ref:EncounterSlot，正常情况已由 DAT008 覆盖；
                    // 这里是第二道防线（防止目标 Sheet 存在但该行被静默跳过的边界）。
                    bag.Error(DiagnosticCodes.DatEncounterRuleValue,
                        "SlotId '" + slotId + "' 在 EncounterSlot.SlotId 中不存在。",
                        Ctx(row), "SlotId", "SlotId∈EncounterSlot");
                }

                string dimension = Text(row, "Dimension");
                if (dimension == null) continue;   // 必填为空已由 DAT001 报出

                if (!Contains(AllowedDimensions, dimension))
                {
                    bag.Error(DiagnosticCodes.DatEncounterRuleClassification,
                        "Dimension 未知取值 '" + dimension + "'（第一版只正式支持："
                        + string.Join(" / ", AllowedDimensions) + "）。"
                        + "Species / Allegiance / Biome / Curse / 世界时间 / 世界状态 / Encounter Modifier "
                        + "只是未来接口方向，不属于本技术检查点。",
                        Ctx(row), "Dimension");
                    continue;
                }

                string value = Text(row, "Value");
                if (value == null) continue;   // 必填为空已由 DAT001 报出

                string[] allowed = null;
                if (dimension == "Role") allowed = AllowedRoles;
                else if (dimension == "Tier") allowed = AllowedTiers;

                if (allowed != null && !Contains(allowed, value))
                {
                    bag.Error(DiagnosticCodes.DatEncounterRuleValue,
                        "Dimension = " + dimension + " 时 Value 必须是合法取值（"
                        + string.Join(" / ", allowed) + "），当前：'" + value + "'。",
                        Ctx(row), "Value", "Dimension=" + dimension);
                    continue;
                }

                if (dimension != "MonsterId") continue;

                if (monsterIds == null)
                {
                    bag.Error(DiagnosticCodes.DatEncounterRuleValue,
                        "Dimension = MonsterId 需要 Monster 表来解析 Value，但当前语料中没有 Monster 表："
                        + "无法校验该值，也不会静默接受它。",
                        Ctx(row), "Value", "Dimension=MonsterId");
                }
                else if (!monsterIds.Contains(value))
                {
                    bag.Error(DiagnosticCodes.DatEncounterRuleValue,
                        "Dimension = MonsterId 时 Value '" + value + "' 在 Monster.MonsterId 中不存在。",
                        Ctx(row), "Value", "Dimension=MonsterId");
                }
            }
        }

        /// <summary>EncounterPoolEntry：MinDepth ≤ MaxDepth（Weight &gt; 0 由 Row 3 的 &gt;=1 负责）。</summary>
        private static void ValidateEncounterPoolEntry(CompiledSheet sheet, DiagnosticBag bag)
        {
            if (sheet == null) return;

            foreach (var row in sheet.Rows)
            {
                long? min = Int(row, "MinDepth");
                long? max = Int(row, "MaxDepth");
                if (min == null || max == null) continue;   // 缺失 / 类型错误已由 DAT001 / DAT002 报出

                if (min.Value > max.Value)
                {
                    bag.Error(DiagnosticCodes.DatEncounterPoolDepthRange,
                        "MinDepth（" + min.Value + "）大于 MaxDepth（" + max.Value + "）："
                        + "深度区间非法，该条目永远不会被选中。",
                        Ctx(row), "MinDepth", "MinDepth<=MaxDepth");
                }
            }
        }

        /// <summary>
        /// Encounter V0.1（协调层冻结）：**Enabled = true 的遭遇必须至少有 1 个组合需求位**。
        ///
        /// 理由：`Enabled = true` 表示该遭遇会参与抽取；而敌人数量的唯一来源是 EncounterSlot，
        /// 所以「启用但没有任何 Slot」的遭遇一旦被抽中就是一场无法生成的战斗。
        /// `Enabled = false` 表示制作人草稿，允许暂时没有 Slot（不得报错）。
        ///
        /// 边界（刻意不做）：本规则只检查「有没有 Slot」，不检查 Slot 能否筛出怪、
        /// 也不做数量 / 威胁预算求解 —— 那些是 Generator（后续技术检查点）的职责。
        /// </summary>
        private static void ValidateEnabledEncountersHaveSlots(CompiledSheet encounterSheet,
            CompiledSheet slotSheet, DiagnosticBag bag)
        {
            if (encounterSheet == null) return;

            // EncounterSlot 整表缺失已由 DAT028（领域不完整）报出，这里不重复报同一件事。
            if (slotSheet == null) return;

            var encountersWithSlots = new HashSet<string>(StringComparer.Ordinal);
            foreach (var slotRow in slotSheet.Rows)
            {
                string encounterId = Text(slotRow, "EncounterId");
                if (!string.IsNullOrEmpty(encounterId)) encountersWithSlots.Add(encounterId);
            }

            foreach (var row in encounterSheet.Rows)
            {
                bool? enabled = Bool(row, "Enabled");
                if (enabled != true) continue;   // 未启用（或值非法，已由 DAT002 报出）→ 允许没有 Slot

                string encounterId = Text(row, "EncounterId");
                if (string.IsNullOrEmpty(encounterId)) continue;   // 身份为空已由 DAT007 报出
                if (encountersWithSlots.Contains(encounterId)) continue;

                bag.Error(DiagnosticCodes.DatEncounterEnabledWithoutSlot,
                    "遭遇 '" + encounterId + "' 的 Enabled = true，但在 EncounterSlot 里没有任何组合需求位。"
                    + "启用中的遭遇一旦被抽中就必须能生成敌人，而敌人数量的唯一来源是 EncounterSlot；"
                    + "请至少添加 1 个组合需求位，或把 Enabled 改为 false（草稿状态允许暂时没有 Slot）。",
                    Ctx(row), "Enabled", "Enabled=true=>≥1 EncounterSlot");
            }
        }

        /// <summary>
        /// 同一 Encounter 下所有 Slot 的 CountMax 总和不得超过敌方战场物理容量（9）。
        /// 每个 Encounter 只报一条（定位到该 Encounter 的第一条 Slot 行），避免同一事实刷屏。
        /// </summary>
        private static void ValidateSlotCapacity(CompiledSheet sheet, DiagnosticBag bag)
        {
            if (sheet == null) return;

            var groups = new List<KeyValuePair<string, List<CompiledRow>>>();
            var index = new Dictionary<string, List<CompiledRow>>(StringComparer.Ordinal);

            foreach (var row in sheet.Rows)
            {
                string encounterId = Text(row, "EncounterId");
                if (string.IsNullOrEmpty(encounterId)) continue;   // 空 / 未解析已由 DAT001 / DAT008 报出

                List<CompiledRow> bucket;
                if (!index.TryGetValue(encounterId, out bucket))
                {
                    bucket = new List<CompiledRow>();
                    index.Add(encounterId, bucket);
                    groups.Add(new KeyValuePair<string, List<CompiledRow>>(encounterId, bucket));
                }
                bucket.Add(row);
            }

            foreach (var group in groups)
            {
                long sum = 0;
                bool complete = true;

                foreach (var row in group.Value)
                {
                    long? countMax = Int(row, "CountMax");
                    if (countMax == null) { complete = false; continue; }
                    sum += countMax.Value;
                }

                if (!complete || sum <= EnemyBoardCapacity) continue;

                bag.Error(DiagnosticCodes.DatEncounterCapacityExceeded,
                    "Encounter '" + group.Key + "' 的所有 Slot 的 CountMax 合计为 " + sum
                    + "，超过敌方战场物理容量 " + EnemyBoardCapacity + "（3 排 × 3 列）。"
                    + "敌人数量的唯一来源是 EncounterSlot 的数量配置，请下调 CountMax 或拆分该遭遇。",
                    Ctx(group.Value[0]), "CountMax", "ΣCountMax<=" + EnemyBoardCapacity);
            }
        }

        // ---------------- 工具 ----------------

        private static void CheckDomainValue(CompiledRow row, string column, string value,
            string[] allowed, DiagnosticBag bag)
        {
            if (string.IsNullOrEmpty(value)) return;
            if (Contains(allowed, value)) return;

            bag.Error(DiagnosticCodes.DatEncounterSlotPositionShape,
                column + " 未知取值 '" + value + "'（允许：" + string.Join(" / ", allowed) + "）。",
                Ctx(row), column);
        }

        private static bool Contains(string[] allowed, string value)
        {
            for (int i = 0; i < allowed.Length; i++)
            {
                if (string.Equals(allowed[i], value, StringComparison.Ordinal)) return true;
            }
            return false;
        }

        private static CompiledSheet Find(List<CompiledSheet> sheets, string sheetName)
        {
            return sheets.FirstOrDefault(s => string.Equals(s.Spec.SheetName, sheetName, StringComparison.Ordinal));
        }

        /// <summary>目标 Sheet 的 id 列取值集合；Sheet 不存在（或无 id 列）时返回 null（调用方必须显式处理）。</summary>
        private static ISet<string> IdsOf(CompiledSheet sheet)
        {
            if (sheet == null) return null;

            var idField = sheet.Spec.Fields.FirstOrDefault(f => f.Kind == FieldKind.Id);
            if (idField == null) return null;

            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var row in sheet.Rows)
            {
                var cell = row.Get(idField.Field);
                if (cell != null && cell.Value is string s && s.Length > 0) ids.Add(s);
            }
            return ids;
        }

        private static DiagContext Ctx(CompiledRow row)
        {
            return DiagContext.Of(row.Sheet.Workbook, row.Sheet.SheetName, row.ExcelRow);
        }

        private static string Text(CompiledRow row, string field)
        {
            var cell = row.Get(field);
            if (cell == null) return null;
            if (cell.Value is string s) return s;

            // 兜底：使用**作者字面输入**。
            // 当 Row 3 的 allowed=（DAT012）或空值检查（DAT001）已经报错时，cell.Value 会变成 null；
            // 领域层仍必须用作者实际写的文本给出自己的精确原因 —— 否则会出现
            // 「PositionMode = Row 且作者填了 Row=Sideways，却报『必须填写 Row』」这类误导诊断。
            return string.IsNullOrEmpty(cell.Raw) ? null : cell.Raw;
        }

        private static long? Int(CompiledRow row, string field)
        {
            var cell = row.Get(field);
            if (cell == null || cell.Value == null) return null;
            if (cell.Value is long l) return l;
            return null;
        }

        /// <summary>bool 列取值；值非法（类型错误已由 DAT002 报出）时返回 null（调用方必须显式处理）。</summary>
        private static bool? Bool(CompiledRow row, string field)
        {
            var cell = row.Get(field);
            if (cell == null || cell.Value == null) return null;
            if (cell.Value is bool b) return b;
            return null;
        }

        private static string Display(string value)
        {
            return string.IsNullOrEmpty(value) ? "<空>" : value;
        }




    }
}
