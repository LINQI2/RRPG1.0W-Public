using System;
using System.Collections.Generic;
using System.Linq;

namespace RRPG.ConfigCompiler.Core
{
    /// <summary>报告中的一个 Sheet 摘要。</summary>
    public sealed class ReportSheet
    {
        public string Name;
        public string Kind;        // DATA / SKIPPED / EMPTY
        public int RowCount;
        public int ColumnCount;
        public List<string> Key = new List<string>();
        public string SkipReason;
    }

    /// <summary>报告中的一个 Workbook 摘要。</summary>
    public sealed class ReportWorkbook
    {
        public string Path;                 // Authoring/RRPG_Monsters.xlsx
        public string PackageValidation;    // VALID / INVALID / ERROR
        public int PackageIssueCount;
        public List<ReportSheet> Sheets = new List<ReportSheet>();
    }

    /// <summary>一次输出文件的提交结果。</summary>
    public sealed class CommittedFile
    {
        /// <summary>所属根（Generated / .build-state），用于区分同名文件。</summary>
        public string Root;

        public string Name;
        public string Action;   // created / updated / unchanged / deleted / not-written
        public long Bytes;
        public string Sha256;

        public string FullName
        {
            get { return string.IsNullOrEmpty(Root) ? Name : Root + "/" + Name; }
        }
    }

    /// <summary>
    /// BuildReport 写出（机器可读 JSON + 人读 Markdown）。
    ///
    /// 确定性要求：报告不得包含时间戳、耗时、机器绝对路径；
    /// 诊断按 (workbook, sheet, row, column, code, rule) 确定性排序；输出按文件名排序。
    /// </summary>
    public static class BuildReport
    {
        public const string ReportVersion = "spike-a-build-report/1";

        public static string WriteJson(string command, CompilerPaths paths, bool success,
            IReadOnlyList<ReportWorkbook> workbooks, DiagnosticBag bag, IReadOnlyList<CommittedFile> outputs)
        {
            var w = new JsonWriterLite();

            w.BeginObject();
            w.Member("reportVersion", ReportVersion);
            w.Member("grammarVersion", RuleParser.GrammarVersion);
            w.Member("irVersion", CanonicalIrWriter.IrVersion);
            w.Member("command", command);
            w.Member("status", success ? "SUCCESS" : "FAILED");
            w.Member("mirrorDir", paths.GeneratedDirRelative);
            w.Member("irRoot", "Generated");
            w.Member("reportRoot", ".build-state");
            w.Member("hardErrorZeroWrite", !success);

            w.Name("summary");
            w.BeginObject();
            w.Member("workbookCount", workbooks.Count);
            w.Member("dataSheetCount", workbooks.Sum(x => x.Sheets.Count(s => s.Kind == "DATA")));
            w.Member("rowCount", workbooks.Sum(x => x.Sheets.Where(s => s.Kind == "DATA").Sum(s => s.RowCount)));
            w.Member("errorCount", bag.ErrorCount);
            w.Member("warningCount", bag.WarningCount);
            w.Member("outputCount", outputs.Count(o => o.Action != "deleted" && o.Action != "not-written"));
            w.EndObject();

            w.Name("workbooks");
            w.BeginArray();
            foreach (var wb in workbooks.OrderBy(x => x.Path, StringComparer.Ordinal))
            {
                w.BeginObject();
                w.Member("path", wb.Path);
                w.Member("openXmlValidation", wb.PackageValidation);
                w.Member("openXmlIssueCount", wb.PackageIssueCount);

                w.Name("sheets");
                w.BeginArray();
                foreach (var s in wb.Sheets.OrderBy(x => x.Name, StringComparer.Ordinal))
                {
                    w.BeginObject();
                    w.Member("name", s.Name);
                    w.Member("kind", s.Kind);
                    if (s.Kind == "DATA")
                    {
                        w.Member("rowCount", s.RowCount);
                        w.Member("columnCount", s.ColumnCount);
                        w.Name("key");
                        w.BeginArray();
                        foreach (var k in s.Key) w.Value(k);
                        w.EndArray();
                    }
                    else if (!string.IsNullOrEmpty(s.SkipReason))
                    {
                        w.Member("reason", s.SkipReason);
                    }
                    w.EndObject();
                }
                w.EndArray();
                w.EndObject();
            }
            w.EndArray();

            w.Name("diagnostics");
            w.BeginArray();
            foreach (var d in bag.Sorted())
            {
                w.BeginObject();
                w.Member("severity", d.Severity == Severity.Error ? "ERROR" : "WARNING");
                w.Member("code", d.Code);
                if (!string.IsNullOrEmpty(d.Workbook)) w.Member("workbook", d.Workbook);
                if (!string.IsNullOrEmpty(d.Sheet)) w.Member("sheet", d.Sheet);
                if (d.Row > 0) w.Member("row", d.Row);
                if (!string.IsNullOrEmpty(d.Column)) w.Member("column", d.Column);
                if (!string.IsNullOrEmpty(d.Rule)) w.Member("rule", d.Rule);
                w.Member("message", d.Message);
                w.EndObject();
            }
            w.EndArray();

            w.Name("outputs");
            w.BeginArray();
            foreach (var o in outputs.OrderBy(x => x.FullName, StringComparer.Ordinal))
            {
                w.BeginObject();
                w.Member("root", o.Root);
                w.Member("name", o.Name);
                w.Member("action", o.Action);
                w.Member("bytes", o.Bytes);
                if (!string.IsNullOrEmpty(o.Sha256)) w.Member("sha256", o.Sha256);
                w.EndObject();
            }
            w.EndArray();

            w.EndObject();
            return w.ToText();
        }
    }
}
