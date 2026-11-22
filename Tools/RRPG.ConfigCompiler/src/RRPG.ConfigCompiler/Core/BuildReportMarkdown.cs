using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RRPG.ConfigCompiler.Core
{
    /// <summary>人读版 BuildReport（Markdown）。与 JSON 报告同样禁止时间戳 / 机器路径。</summary>
    public static class BuildReportMarkdown
    {
        public static string Write(string command, CompilerPaths paths, bool success,
            IReadOnlyList<ReportWorkbook> workbooks, DiagnosticBag bag, IReadOnlyList<CommittedFile> outputs)
        {
            var sb = new StringBuilder();

            sb.Append("# RRPG Config Compiler — Spike A BuildReport\n\n");
            sb.Append("> 定位：SPIKE CANDIDATE / 实验产物。不是 Current Architecture，也不是最终工具链裁决。\n");
            sb.Append("> grammarVersion：").Append(RuleParser.GrammarVersion)
              .Append(" ｜ irVersion：").Append(CanonicalIrWriter.IrVersion).Append('\n').Append('\n');

            sb.Append("| 项目 | 值 |\n|---|---|\n");
            sb.Append("| command | ").Append(command).Append(" |\n");
            sb.Append("| status | ").Append(success ? "SUCCESS" : "FAILED").Append(" |\n");
            sb.Append("| mirrorDir | ").Append(paths.GeneratedDirRelative).Append(" |\n");
            sb.Append("| workbooks | ").Append(workbooks.Count).Append(" |\n");
            sb.Append("| data sheets | ").Append(workbooks.Sum(w => w.Sheets.Count(s => s.Kind == "DATA"))).Append(" |\n");
            sb.Append("| rows | ").Append(workbooks.Sum(w => w.Sheets.Where(s => s.Kind == "DATA").Sum(s => s.RowCount))).Append(" |\n");
            sb.Append("| errors | ").Append(bag.ErrorCount).Append(" |\n");
            sb.Append("| warnings | ").Append(bag.WarningCount).Append(" |\n");
            sb.Append("| hardErrorZeroWrite | ").Append(success ? "false" : "true").Append(" |\n\n");

            sb.Append("## Workbooks\n\n");
            sb.Append("| workbook | OpenXML | sheet | kind | rows | key |\n|---|---|---|---|---|---|\n");
            foreach (var wb in workbooks.OrderBy(w => w.Path, StringComparer.Ordinal))
            {
                foreach (var s in wb.Sheets.OrderBy(s => s.Name, StringComparer.Ordinal))
                {
                    sb.Append("| ").Append(wb.Path)
                      .Append(" | ").Append(wb.PackageValidation)
                      .Append(" | ").Append(s.Name)
                      .Append(" | ").Append(s.Kind)
                      .Append(" | ").Append(s.Kind == "DATA" ? s.RowCount.ToString() : "-")
                      .Append(" | ").Append(s.Key.Count > 0 ? string.Join("+", s.Key) : "-")
                      .Append(" |\n");
                }
            }

            sb.Append("\n## Diagnostics\n\n");
            var diags = bag.Sorted();
            if (diags.Count == 0)
            {
                sb.Append("（无）\n");
            }
            else
            {
                sb.Append("| severity | code | workbook | sheet | row | column | rule | message |\n");
                sb.Append("|---|---|---|---|---|---|---|---|\n");
                foreach (var d in diags)
                {
                    sb.Append("| ").Append(d.Severity == Severity.Error ? "ERROR" : "WARNING")
                      .Append(" | ").Append(d.Code)
                      .Append(" | ").Append(d.Workbook ?? "-")
                      .Append(" | ").Append(d.Sheet ?? "-")
                      .Append(" | ").Append(d.Row > 0 ? d.Row.ToString() : "-")
                      .Append(" | ").Append(d.Column ?? "-")
                      .Append(" | ").Append(d.Rule ?? "-")
                      .Append(" | ").Append(Escape(d.Message))
                      .Append(" |\n");
                }
            }

            sb.Append("\n## Outputs\n\n");
            if (outputs.Count == 0)
            {
                sb.Append("（本次未写出任何输出——Hard Error 时 Canonical IR 0 更新）\n");
            }
            else
            {
                sb.Append("| file | action | bytes | sha256 |\n|---|---|---|---|\n");
                foreach (var o in outputs.OrderBy(o => o.FullName, StringComparer.Ordinal))
                {
                    sb.Append("| ").Append(o.FullName)
                      .Append(" | ").Append(o.Action)
                      .Append(" | ").Append(o.Bytes)
                      .Append(" | ").Append(string.IsNullOrEmpty(o.Sha256) ? "-" : o.Sha256)
                      .Append(" |\n");
                }
            }

            return sb.ToString();
        }

        private static string Escape(string text)
        {
            return (text ?? string.Empty).Replace("|", "\\|").Replace("\n", " ");
        }
    }
}
