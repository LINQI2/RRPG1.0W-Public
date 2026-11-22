using System;
using System.Collections.Generic;
using System.Globalization;
using ClosedXML.Excel;

namespace RRPG.ConfigCompiler.Core
{
    /// <summary>
    /// Workbook → RawSheet 读取（ClosedXML）。
    ///
    /// 读取规则（确定性相关，均为本 Spike 的显式决策）：
    ///   - 单元格值统一转为字符串；文本两端空白被 Trim（空白视为空值）；
    ///   - 数值单元格：整数值 → 不带小数点的整数文本；非整数 → 往返格式（InvariantCulture）；
    ///   - 布尔单元格 → true / false；
    ///   - 公式单元格 → 不读取缓存值，直接判为 Hard Error（防止陈旧缓存值静默进入配置）；
    ///   - 末尾空行 / 空列不参与（避免 Excel 幽灵范围产生 phantom 列）。
    /// </summary>
    public static class WorkbookReader
    {
        public static List<RawSheet> Read(DiscoveredWorkbook workbook, DiagnosticBag bag)
        {
            var sheets = new List<RawSheet>();

            try
            {
                using (var wb = new XLWorkbook(workbook.FullPath))
                {
                    foreach (var ws in wb.Worksheets)
                    {
                        var raw = new RawSheet
                        {
                            WorkbookDisplayPath = workbook.DisplayPath,
                            SheetName = ws.Name
                        };

                        if (ShouldSkipSheet(ws.Name))
                        {
                            raw.SkippedForData = true;
                            raw.SkipReason = "Sheet 名以 _ / 00_ / __ / ~ 开头（导航或非数据面）";
                            bag.Warn(DiagnosticCodes.WbSheetSkipped,
                                "跳过非数据 Sheet：" + ws.Name + "（" + raw.SkipReason + "）",
                                DiagContext.Of(workbook.DisplayPath, ws.Name, 0));
                            sheets.Add(raw);
                            continue;
                        }

                        ReadSheet(ws, raw, bag);
                        sheets.Add(raw);
                    }
                }
            }
            catch (Exception ex)
            {
                bag.Error(DiagnosticCodes.WbFileUnreadable,
                    "无法读取工作簿：" + ex.GetType().Name + ": " + ex.Message,
                    DiagContext.Of(workbook.DisplayPath, null, 0));
            }

            return sheets;
        }

        /// <summary>非数据 Sheet 命名约定：_x / 00_x / __x / ~x。</summary>
        public static bool ShouldSkipSheet(string sheetName)
        {
            if (string.IsNullOrEmpty(sheetName)) return true;
            return sheetName.StartsWith("_", StringComparison.Ordinal)
                || sheetName.StartsWith("00_", StringComparison.Ordinal)
                || sheetName.StartsWith("__", StringComparison.Ordinal)
                || sheetName.StartsWith("~", StringComparison.Ordinal);
        }

        private static void ReadSheet(IXLWorksheet ws, RawSheet raw, DiagnosticBag bag)
        {
            var ctx = DiagContext.Of(raw.WorkbookDisplayPath, raw.SheetName, 0);

            int lastColumn = ws.LastColumnUsed() != null ? ws.LastColumnUsed().ColumnNumber() : 0;
            int lastRow = ws.LastRowUsed() != null ? ws.LastRowUsed().RowNumber() : 0;

            if (lastColumn <= 0 || lastRow < RawSheet.FirstDataRow)
            {
                raw.Empty = true;
                bag.Warn(DiagnosticCodes.WbSheetEmpty,
                    "Sheet 没有数据行（至少需要 Row 1/2/3/4 表头 + Row 5 起的数据）。", ctx);
                return;
            }

            raw.HasAnnotationColumn = IsAnnotationColumn(ws);

            for (int c = 1; c <= lastColumn; c++)
            {
                raw.Columns.Add(new ColumnRaw
                {
                    Index = c,
                    ColumnLetter = RuleParser.ColumnLetter(c),
                    MachineField = CellText(ws.Cell(RawSheet.MachineFieldRow, c)),
                    TypeRaw = CellText(ws.Cell(RawSheet.TypeRow, c)),
                    RuleRaw = CellText(ws.Cell(RawSheet.RuleRow, c)),
                    DisplayHeader = CellText(ws.Cell(RawSheet.DisplayHeaderRow, c))
                });
            }

            // T4.1：列角色判定必须在「行范围」之前 —— 否则说明列/备注列里的行备注会被当成数据行。
            ClassifyColumns(raw, ws, lastRow, ctx, bag);

            // 数据行范围：只按“导出的、有机器字段的列”判断，避免备注列与幽灵列拉长范围。
            int lastDataRow = 0;
            foreach (var col in raw.Columns)
            {
                if (col.NonExported) continue;
                if (string.IsNullOrEmpty(col.MachineField)) continue;
                for (int r = RawSheet.FirstDataRow; r <= lastRow; r++)
                {
                    if (CellText(ws.Cell(r, col.Index)).Length > 0)
                        lastDataRow = Math.Max(lastDataRow, r);
                }
            }

            for (int r = RawSheet.FirstDataRow; r <= lastDataRow; r++)
            {
                var row = new RowRaw { ExcelRow = r };
                bool any = false;

                foreach (var col in raw.Columns)
                {
                    var cell = ws.Cell(r, col.Index);
                    string text = CellText(cell);
                    row.Cells[col.Index] = text;

                    if (col.NonExported) continue;   // 说明列/备注列不参与「本行是否有数据」的判断

                    if (cell.HasFormula) row.FormulaColumns.Add(col.Index);
                    if (text.Length > 0) any = true;
                }

                if (!any)
                {
                    bag.Warn(DiagnosticCodes.DatEmptyRowSkipped,
                        "跳过空数据行（Row " + r + "）：不影响 Canonical 输出。",
                        DiagContext.Of(raw.WorkbookDisplayPath, raw.SheetName, r));
                    continue;
                }

                raw.Rows.Add(row);
            }

            foreach (var col in raw.Columns)
            {
                if (col.NonExported) continue;

                foreach (var row in raw.Rows)
                {
                    if (row.Get(col.Index).Length > 0) { col.HasData = true; break; }
                }

                if (string.IsNullOrEmpty(col.MachineField) && col.HasData)
                {
                    bag.Error(DiagnosticCodes.SchDataWithoutMachineField,
                        "列 " + col.ColumnLetter + " 有数据但 Row 1 没有机器字段名。"
                        + "（如果它只是备注：请在 Row 2 写 ignore；如果是字段：请补上机器字段名。）",
                        ctx, col.ColumnLetter);
                }
            }
        }

        /// <summary>
        /// T4.1：列角色判定（在 Schema 之前完成，保证「不导出列」不会影响任何语义）。
        ///
        /// 规则（确定性，与物理位置无关）：
        ///   1. **制作人说明列**：A1=机器字段 且 A2=数据类型 且 A3=规则 → 第 1 列整列非导出（A5+ 为行备注）；
        ///   2. **显式声明的非导出列**：Row 2 写 <c>ignore</c> / <c>note</c>（可放在任意位置，包括业务字段之间）；
        ///   3. 什么都没写（Row1 / Row4 空且无数据）→ 备用空列，静默跳过；
        ///   4. 其它情况（有中文表头或有数据，却没有机器字段、也没声明 ignore/note）→ Hard Error（无法识别的列）。
        /// </summary>
        public static bool IsIgnoredTypeToken(string typeRaw)
        {
            return string.Equals(typeRaw, "ignore", StringComparison.OrdinalIgnoreCase)
                || string.Equals(typeRaw, "note", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>第 1 列是否是「制作人说明列」（A1/A2/A3 三个标签同时命中才算）。</summary>
        public static bool IsAnnotationColumn(IXLWorksheet ws)
        {
            return string.Equals(CellText(ws.Cell(RawSheet.MachineFieldRow, 1)), RawSheet.AnnotationMachineFieldLabel, StringComparison.Ordinal)
                && string.Equals(CellText(ws.Cell(RawSheet.TypeRow, 1)), RawSheet.AnnotationTypeLabel, StringComparison.Ordinal)
                && string.Equals(CellText(ws.Cell(RawSheet.RuleRow, 1)), RawSheet.AnnotationRuleLabel, StringComparison.Ordinal);
        }

        private static void ClassifyColumns(RawSheet raw, IXLWorksheet ws, int lastRow, DiagContext ctx, DiagnosticBag bag)
        {
            foreach (var col in raw.Columns)
            {
                if (col.Index == 1 && raw.HasAnnotationColumn)
                {
                    col.NonExported = true;
                    col.NonExportedReason = "annotation-column";
                    continue;
                }

                if (IsIgnoredTypeToken(col.TypeRaw))
                {
                    col.NonExported = true;
                    col.NonExportedReason = "declared-" + col.TypeRaw.ToLowerInvariant();
                    continue;
                }

                if (!string.IsNullOrEmpty(col.MachineField)) continue;

                bool hasContent = col.DisplayHeader.Length > 0 || HasAnyValue(ws, col.Index, lastRow);
                if (!hasContent)
                {
                    col.NonExported = true;
                    col.NonExportedReason = "empty-spare";
                    continue;
                }

                bag.Error(DiagnosticCodes.SchUnrecognizedColumn,
                    "列 " + col.ColumnLetter + " 无法识别：它既没有机器字段名（Row 1），"
                    + "也没有被声明为不导出（Row 2 写 ignore 或 note）。"
                    + "请二选一：给它一个机器字段名，或把它声明成备注列。",
                    ctx, col.ColumnLetter);
            }
        }

        private static bool HasAnyValue(IXLWorksheet ws, int columnIndex, int lastRow)
        {
            for (int r = RawSheet.FirstDataRow; r <= lastRow; r++)
            {
                if (CellText(ws.Cell(r, columnIndex)).Length > 0) return true;
            }
            return false;
        }

        /// <summary>单元格 → 规范文本（Culture 无关）。</summary>
        public static string CellText(IXLCell cell)
        {
            if (cell == null) return string.Empty;
            if (cell.IsEmpty()) return string.Empty;

            switch (cell.DataType)
            {
                case XLDataType.Number:
                    double d = cell.GetDouble();
                    if (d == Math.Floor(d) && Math.Abs(d) < 1e15)
                        return ((long)d).ToString(CultureInfo.InvariantCulture);
                    return d.ToString("R", CultureInfo.InvariantCulture);

                case XLDataType.Boolean:
                    return cell.GetBoolean() ? "true" : "false";

                case XLDataType.DateTime:
                    // 配置列不允许日期（Spike A 无日期字段）；原样返回供类型校验报错。
                    return cell.GetDateTime().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);

                default:
                    return (cell.GetString() ?? string.Empty).Trim();
            }
        }
    }
}
