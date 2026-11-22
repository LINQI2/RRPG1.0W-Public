using System;
using System.Collections.Generic;

namespace RRPG.ConfigCompiler.Core
{
    /// <summary>一个列在 Excel 中的原始内容（未做任何类型解释）。</summary>
    public sealed class ColumnRaw
    {
        public int Index;
        public string ColumnLetter;
        public string MachineField = string.Empty;   // Row 1
        public string TypeRaw = string.Empty;        // Row 2
        public string RuleRaw = string.Empty;        // Row 3
        public string DisplayHeader = string.Empty;  // Row 4
        public bool HasData;

        /// <summary>
        /// T4.1：该列是否**不导出**（制作人说明列 / 显式声明的备注列）。
        /// 语义：读取器仍会读它（便于诊断），但 Schema / Canonical 输出完全忽略它。
        /// </summary>
        public bool NonExported;

        /// <summary>不导出的原因（annotation-column / declared-ignore / empty-spare）。</summary>
        public string NonExportedReason;
    }

    /// <summary>一行数据（行号 + 列号 → 原始文本）。</summary>
    public sealed class RowRaw
    {
        public int ExcelRow;
        public Dictionary<int, string> Cells = new Dictionary<int, string>();
        public HashSet<int> FormulaColumns = new HashSet<int>();

        public string Get(int columnIndex)
        {
            string v;
            return Cells.TryGetValue(columnIndex, out v) ? v : string.Empty;
        }
    }

    /// <summary>
    /// 一个 Sheet 的原始读取结果（Row 1..4 固定为表头区）。
    ///
    /// T4.1：第 1 列（A）可以是「制作人说明列」（A1=机器字段 / A2=数据类型 / A3=规则 / A4=中文说明），
    /// 此时真正的业务字段从 B 列开始；A 列整个不参与编译。
    /// </summary>
    public sealed class RawSheet
    {
        public const int MachineFieldRow = 1;
        public const int TypeRow = 2;
        public const int RuleRow = 3;
        public const int DisplayHeaderRow = 4;
        public const int FirstDataRow = 5;

        /// <summary>制作人说明列的文字（新布局）；null = 旧布局（A 列是业务字段）。</summary>
        public const string AnnotationMachineFieldLabel = "机器字段";
        public const string AnnotationTypeLabel = "数据类型";
        public const string AnnotationRuleLabel = "规则";
        public const string AnnotationHeaderLabel = "中文说明";

        public string WorkbookDisplayPath;
        public string SheetName;
        public bool SkippedForData;
        public string SkipReason;
        public bool Empty;

        /// <summary>T4.1：本 Sheet 是否有「制作人说明列」（物理第 1 列，非导出）。</summary>
        public bool HasAnnotationColumn;

        public List<ColumnRaw> Columns = new List<ColumnRaw>();
        public List<RowRaw> Rows = new List<RowRaw>();
    }
}
