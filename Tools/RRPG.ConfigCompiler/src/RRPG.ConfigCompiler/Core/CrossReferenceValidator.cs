using System;
using System.Collections.Generic;
using System.Linq;

namespace RRPG.ConfigCompiler.Core
{
    /// <summary>
    /// 跨 Sheet 校验 + 确定性排序。
    ///
    /// 硬约束：
    ///   - 同一实体名（Sheet 名）只能出现一次（跨 Workbook 也唯一）→ 否则 WB006；
    ///   - key（unique 列组合）在同一 Sheet 内唯一 → 否则 DAT005；
    ///   - ref:X 的目标 Sheet 必须存在 → 否则 DAT009；
    ///   - ref 值必须能在目标 Sheet 的 id 列解析到 → 否则 DAT008；
    ///   - 输出顺序 = 按 key 列升序（与物理行顺序无关）→ 保证「子表行序变化不改变 Canonical 输出」。
    /// </summary>
    public static class CrossReferenceValidator
    {
        public static void ValidateAndSort(List<CompiledSheet> sheets, DiagnosticBag bag)
        {
            DetectDuplicateEntities(sheets, bag);

            var byName = new Dictionary<string, CompiledSheet>(StringComparer.Ordinal);
            foreach (var s in sheets)
            {
                if (!byName.ContainsKey(s.Spec.SheetName)) byName[s.Spec.SheetName] = s;
            }

            foreach (var sheet in sheets)
            {
                CheckKeyDuplicates(sheet, bag);
                CheckRefs(sheet, byName, bag);

                // T4.1：**列序无关** —— 排序键按机器字段名有序化。
                // 否则「把两列换个位置」会改变复合 key 的比较顺序，进而改变 Canonical 行顺序。
                sheet.Spec.KeyFields = sheet.Spec.KeyFields
                    .OrderBy(f => f.Field, StringComparer.Ordinal)
                    .ToList();

                SortByKey(sheet);
            }
        }

        private static void DetectDuplicateEntities(List<CompiledSheet> sheets, DiagnosticBag bag)
        {
            foreach (var group in sheets.GroupBy(s => s.Spec.SheetName, StringComparer.Ordinal))
            {
                if (group.Count() <= 1) continue;

                bag.Error(DiagnosticCodes.WbDuplicateEntitySheet,
                    "实体名 '" + group.Key + "' 在多个 Sheet / Workbook 中重复："
                    + string.Join(", ", group.Select(g => g.Spec.Workbook)),
                    DiagContext.Of(null, group.Key, 0));
            }
        }

        private static void CheckKeyDuplicates(CompiledSheet sheet, DiagnosticBag bag)
        {
            // v3：每个 Sheet 只有一套身份（id 或 key 列组合），它就是唯一性判定的对象。
            var set = sheet.Spec.KeyFields;
            if (set.Count == 0) return; // SCH016 已报错

            string label = sheet.Spec.Identity == SheetIdentityKind.PrimaryKey
                ? "id 身份重复"
                : "复合身份重复";

            var seen = new Dictionary<string, int>(StringComparer.Ordinal);

            foreach (var row in sheet.Rows)
            {
                string key = KeyText(row, set, out bool complete);
                if (!complete) continue; // 空 key 已由 required / id 诊断覆盖

                if (seen.TryGetValue(key, out int firstRow))
                {
                    bag.Error(DiagnosticCodes.DatKeyDuplicate,
                        label + "（" + string.Join("+", set.Select(f => f.Field)) + "）：["
                        + KeyDisplay(row, set) + "] 已出现在 Row " + firstRow
                        + "，本行 Row " + row.ExcelRow + "。",
                        DiagContext.Of(sheet.Spec.Workbook, sheet.Spec.SheetName, row.ExcelRow));
                }
                else
                {
                    seen.Add(key, row.ExcelRow);
                }
            }
        }

        private static void CheckRefs(CompiledSheet sheet, Dictionary<string, CompiledSheet> byName, DiagnosticBag bag)
        {
            foreach (var refField in sheet.Spec.RefFields)
            {
                string target = refField.TypeArgument;
                var ctxSheet = DiagContext.Of(sheet.Spec.Workbook, sheet.Spec.SheetName, 0);

                if (string.IsNullOrEmpty(target) || !byName.ContainsKey(target))
                {
                    bag.Error(DiagnosticCodes.DatRefUnknownSheet,
                        "ref:" + target + " 指向的 Sheet 不存在（跨 Workbook 也必须存在）。",
                        ctxSheet, refField.Field, "ref:" + target);
                    continue;
                }

                var targetSheet = byName[target];
                var idField = targetSheet.Spec.Fields.FirstOrDefault(f => f.Kind == FieldKind.Id);
                if (idField == null)
                {
                    bag.Error(DiagnosticCodes.DatRefUnknownSheet,
                        "ref:" + target + " 的目标 Sheet 没有 id 列，无法解析引用。",
                        ctxSheet, refField.Field, "ref:" + target);
                    continue;
                }

                var ids = new HashSet<string>(StringComparer.Ordinal);
                foreach (var row in targetSheet.Rows)
                {
                    var cell = row.Get(idField.Field);
                    if (cell != null && cell.Value is string s && s.Length > 0) ids.Add(s);
                }

                foreach (var row in sheet.Rows)
                {
                    var cell = row.Get(refField.Field);
                    if (cell == null || cell.Value == null) continue;

                    string value = cell.Value as string;
                    if (string.IsNullOrEmpty(value)) continue;

                    if (!ids.Contains(value))
                    {
                        bag.Error(DiagnosticCodes.DatRefUnresolved,
                            "引用值 '" + value + "' 在 " + target + "." + idField.Field + " 中不存在。",
                            DiagContext.Of(sheet.Spec.Workbook, sheet.Spec.SheetName, row.ExcelRow),
                            refField.Field, "ref:" + target);
                    }
                }
            }
        }

        /// <summary>按 key 列升序排序（LINQ OrderBy 为稳定排序 → 结果确定）。</summary>
        private static void SortByKey(CompiledSheet sheet)
        {
            var keyFields = sheet.Spec.KeyFields;
            if (keyFields.Count == 0) return;

            sheet.Rows = sheet.Rows
                .OrderBy(r => r, Comparer<CompiledRow>.Create((a, b) => CompareRows(a, b, keyFields)))
                .ToList();
        }

        private static int CompareRows(CompiledRow a, CompiledRow b, List<FieldSpec> keyFields)
        {
            foreach (var field in keyFields)
            {
                int cmp = CompareValues(a.Get(field.Field)?.Value, b.Get(field.Field)?.Value);
                if (cmp != 0) return cmp;
            }
            return 0;
        }

        private static int CompareValues(object x, object y)
        {
            if (x == null && y == null) return 0;
            if (x == null) return -1;
            if (y == null) return 1;

            if (x is long lx && y is long ly) return lx.CompareTo(ly);
            if (x is double dx && y is double dy) return dx.CompareTo(dy);
            if (x is bool bx && y is bool by) return bx.CompareTo(by);

            return string.CompareOrdinal(x.ToString(), y.ToString());
        }

        private static string KeyText(CompiledRow row, List<FieldSpec> keyFields, out bool complete)
        {
            complete = true;
            var parts = new List<string>();

            foreach (var f in keyFields)
            {
                var v = row.Get(f.Field)?.Value;
                if (v == null) { complete = false; return string.Empty; }
                parts.Add(v is string s
                    ? s
                    : Convert.ToString(v, System.Globalization.CultureInfo.InvariantCulture));
            }

            return string.Join("\u001f", parts);
        }

        private static string KeyDisplay(CompiledRow row, List<FieldSpec> keyFields)
        {
            var parts = new List<string>();
            foreach (var f in keyFields)
            {
                var v = row.Get(f.Field)?.Value;
                parts.Add(f.Field + "=" + (v == null
                    ? "<null>"
                    : Convert.ToString(v, System.Globalization.CultureInfo.InvariantCulture)));
            }
            return string.Join(", ", parts);
        }
    }
}
