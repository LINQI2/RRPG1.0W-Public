using System;
using System.Collections.Generic;
using System.Linq;

namespace RRPG.ConfigCompiler.Core
{
    /// <summary>
    /// Sheet 级 Schema 编译：把 RawSheet 的 Row 1..4 变成 SheetSpec，并检查 Sheet 级约束。
    ///
    /// Sheet 级硬约束：
    ///   - 机器字段（Row 1）在同 Sheet 内唯一；
    ///   - 同一 Sheet 最多一个 id 列；
    ///   - 必须存在 key（unique 列；否则退化为唯一 id 列），否则无法做确定性排序与重复检测。
    /// </summary>
    public static class SchemaCompiler
    {
        public static SheetSpec Compile(RawSheet raw, DiagnosticBag bag)
        {
            var ctx = DiagContext.Of(raw.WorkbookDisplayPath, raw.SheetName, 0);
            var spec = new SheetSpec
            {
                SheetName = raw.SheetName,
                Workbook = raw.WorkbookDisplayPath
            };

            var seenFields = new Dictionary<string, ColumnRaw>(StringComparer.OrdinalIgnoreCase);
            var bagForSheet = new DiagBag(bag, ctx);

            foreach (var col in raw.Columns)
            {
                if (col.NonExported) continue;   // T4.1：制作人说明列 / 声明的备注列完全不参与编译
                if (string.IsNullOrEmpty(col.MachineField)) continue; // 无机器字段的列已在读取阶段处理

                if (seenFields.TryGetValue(col.MachineField, out var first))
                {
                    bag.Error(DiagnosticCodes.SchDuplicateMachineField,
                        "机器字段 '" + col.MachineField + "' 重复：已出现在列 " + first.ColumnLetter
                        + "，本列 " + col.ColumnLetter + "。",
                        ctx, col.ColumnLetter);
                    continue;
                }
                seenFields.Add(col.MachineField, col);

                var field = RuleParser.Parse(
                    col.TypeRaw, col.RuleRaw, col.Index,
                    RawSheet.TypeRow, RawSheet.RuleRow, bagForSheet, col.MachineField);

                if (string.IsNullOrEmpty(col.DisplayHeader))
                {
                    bag.Warn(DiagnosticCodes.SchDisplayHeaderMissing,
                        "Row " + RawSheet.DisplayHeaderRow + "（中文 Header）为空：不影响编译，但影响制作人可读性。",
                        ctx, col.MachineField);
                }
                field.DisplayHeader = col.DisplayHeader;

                spec.Fields.Add(field);
            }

            BuildKeys(spec, bag, ctx);
            CheckRequiredFields(spec, bag, ctx);
            return spec;
        }

        /// <summary>
        /// T4.1：已知实体的必需机器字段必须存在（缺失 = Hard Error）。
        /// 这是「允许插列/换列」的必要配套：否则字段被挪进备注列时会静默消失。
        /// </summary>
        private static void CheckRequiredFields(SheetSpec spec, DiagnosticBag bag, DiagContext ctx)
        {
            string[] required;
            if (!EntityRequirements.TryGetRequired(spec.SheetName, out required)) return;

            foreach (var field in required)
            {
                if (spec.Fields.Any(f => string.Equals(f.Field, field, StringComparison.Ordinal))) continue;

                bag.Error(DiagnosticCodes.SchRequiredFieldMissing,
                    "实体 " + spec.SheetName + " 缺少必需字段 '" + field + "'。"
                    + "常见原因：字段被改名 / 被删除 / 被放进了不导出的备注列。",
                    ctx, field);
            }
        }

        /// <summary>
        /// v3 身份（唯一一套语义）：
        ///   - 有 id 列 → PrimaryKey：id 天然 required/唯一，作为身份与排序键；
        ///     此时其它列**不得**再写 key（避免两种身份声明叠加）；
        ///   - 无 id 列 → 显式 key 列组成 CompositeKey：component 各自允许重复，整体必须唯一，
        ///     并按声明顺序作为 Canonical 输出排序键；
        ///   - 两者都没有 → Missing（Hard Error：无法确定身份与确定性顺序）。
        /// </summary>
        private static void BuildKeys(SheetSpec spec, DiagnosticBag bag, DiagContext ctx)
        {
            var idFields = spec.Fields.Where(f => f.IsPrimaryKey).ToList();
            if (idFields.Count > 1)
            {
                bag.Error(DiagnosticCodes.SchMultipleIdColumns,
                    "同一 Sheet 出现 " + idFields.Count + " 个 id 列（"
                    + string.Join(", ", idFields.Select(f => f.Field)) + "）：id 列必须唯一。",
                    ctx);
            }

            var keyColumns = spec.Fields.Where(f => f.IsCompositeKeyComponent).ToList();

            if (idFields.Count == 1)
            {
                spec.Identity = SheetIdentityKind.PrimaryKey;
                spec.KeyFields.Add(idFields[0]);

                foreach (var col in keyColumns)
                {
                    bag.Error(DiagnosticCodes.SchIdWithKey,
                        "本 Sheet 已有 id 列（" + idFields[0].Field + "）：不得再用 key 声明第二个身份。"
                        + "（" + col.Field + " 的 key 请移除；如确实需要复合身份，请移除 id 列。）",
                        ctx, col.Field, "key");
                }
            }
            else if (keyColumns.Count > 0)
            {
                spec.Identity = SheetIdentityKind.CompositeKey;
                foreach (var col in keyColumns) spec.KeyFields.Add(col);
            }
            else
            {
                spec.Identity = SheetIdentityKind.Missing;
                bag.Error(DiagnosticCodes.SchNoKeyColumn,
                    "Sheet 没有身份：请声明一个 id 列（单列主键），或至少一个 key 列"
                    + "（key = 复合身份 component；身份是唯一性与确定性排序的前提）。",
                    ctx);
            }

            foreach (var f in spec.Fields.Where(x => x.Kind == FieldKind.Ref))
            {
                spec.RefFields.Add(f);
            }
        }
    }
}
