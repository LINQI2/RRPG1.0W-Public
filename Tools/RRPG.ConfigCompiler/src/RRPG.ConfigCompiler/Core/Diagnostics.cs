using System.Collections.Generic;
using System.Linq;

namespace RRPG.ConfigCompiler.Core
{
    /// <summary>
    /// 诊断严重级别。
    /// Spike A 规则：任何 Error 都会阻断正式输出（Hard Error → 0 更新）。
    /// </summary>
    public enum Severity
    {
        Warning = 1,
        Error = 2
    }

    /// <summary>
    /// 诊断定位上下文（workbook / sheet / excel row）。
    /// 目的：让 BuildReport 能精确指出 workbook / sheet / row / column / rule。
    /// </summary>
    public sealed class DiagContext
    {
        public string Workbook { get; set; }
        public string Sheet { get; set; }
        public int Row { get; set; }

        public static DiagContext Of(string workbook, string sheet, int row)
        {
            return new DiagContext { Workbook = workbook, Sheet = sheet, Row = row };
        }
    }

    /// <summary>
    /// 单条编译诊断。Rule 仅在规则相关诊断上填写。
    /// </summary>
    public sealed class Diagnostic
    {
        public Severity Severity { get; set; }
        public string Code { get; set; }
        public string Workbook { get; set; }
        public string Sheet { get; set; }
        public int Row { get; set; }
        public string Column { get; set; }
        public string Rule { get; set; }
        public string Message { get; set; }

        /// <summary>确定性排序键（BuildReport 必须与物理扫描顺序无关）。</summary>
        public string SortKey
        {
            get
            {
                return string.Join("|", new[]
                {
                    Workbook ?? string.Empty,
                    Sheet ?? string.Empty,
                    Row.ToString("D9"),
                    Column ?? string.Empty,
                    Code ?? string.Empty,
                    Rule ?? string.Empty,
                    Message ?? string.Empty
                });
            }
        }

        public override string ToString()
        {
            string where = (Workbook ?? "-") + " / " + (Sheet ?? "-")
                + (Row > 0 ? " / row " + Row : string.Empty)
                + (string.IsNullOrEmpty(Column) ? string.Empty : " / col " + Column)
                + (string.IsNullOrEmpty(Rule) ? string.Empty : " / rule '" + Rule + "'");
            return "[" + (Severity == Severity.Error ? "ERROR" : "WARN") + "] " + Code
                + " @ " + where + " — " + Message;
        }
    }

    /// <summary>
    /// 诊断收集器。所有阶段共用；Error 数量决定 build 是否允许写正式输出。
    /// </summary>
    public sealed class DiagnosticBag
    {
        private readonly List<Diagnostic> _items = new List<Diagnostic>();

        public IReadOnlyList<Diagnostic> Items { get { return _items; } }

        public bool HasErrors { get { return _items.Any(d => d.Severity == Severity.Error); } }

        public int ErrorCount { get { return _items.Count(d => d.Severity == Severity.Error); } }

        public int WarningCount { get { return _items.Count(d => d.Severity == Severity.Warning); } }

        public void Add(Severity severity, string code, string message,
            DiagContext context = null, string column = null, string rule = null)
        {
            _items.Add(new Diagnostic
            {
                Severity = severity,
                Code = code,
                Message = message,
                Workbook = context != null ? context.Workbook : null,
                Sheet = context != null ? context.Sheet : null,
                Row = context != null ? context.Row : 0,
                Column = column,
                Rule = rule
            });
        }

        public void Error(string code, string message, DiagContext context = null,
            string column = null, string rule = null)
        {
            Add(Severity.Error, code, message, context, column, rule);
        }

        public void Warn(string code, string message, DiagContext context = null,
            string column = null, string rule = null)
        {
            Add(Severity.Warning, code, message, context, column, rule);
        }

        /// <summary>确定性视图：按 workbook / sheet / row / column / code / rule 排序。</summary>
        public IReadOnlyList<Diagnostic> Sorted()
        {
            return _items.OrderBy(d => d.SortKey, System.StringComparer.Ordinal).ToList();
        }
    }
}
