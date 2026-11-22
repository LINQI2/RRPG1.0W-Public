using System.IO;
using System.Linq;
using RRPG.ConfigCompiler.Core;

namespace RRPG.ConfigCompiler.Tests
{
    /// <summary>
    /// 测试共用工具（只存在于测试工程；FDS §4.1 P12：实验驱动只作为**测试辅助**进入正式测试目录，
    /// 不得成为产品工具）。
    /// </summary>
    internal static class TestSupport
    {
        /// <summary>在测试根的源资产目录下写一张占位图（编译期只校验存在性 / 扩展名 / 路径安全）。</summary>
        public static string WriteSourceArt(TestRoot root, string relativePath, byte[] bytes = null)
        {
            string full = Path.Combine(root.Paths.SourceAssetsDir,
                relativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(full));
            File.WriteAllBytes(full, bytes ?? new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3, 4 });
            return full;
        }

        /// <summary>
        /// M0 中性测试实体：用于验证 <c>allowed=</c> 取值域。
        /// 刻意不绑定任何正式领域（M0 不建立领域）。
        /// </summary>
        public static TestSheet AllowedValueSheet(params object[][] rows)
        {
            var sheet = new TestSheet("SampleTool")
                .Col("ToolId", "id", "", "工具ID")
                .Col("SourceFile", "string", "", "源文件")
                .Col("ImportPreset", "string", "allowed=UISprite", "导入预设");

            foreach (var row in rows) sheet.Row(row);
            return sheet;
        }

        public static TestSheet MonsterSheet()
        {
            return Fixtures.MinimalMonsterSheet();
        }

        public static string[] ErrorCodes(CompileResult result)
        {
            return result.Diagnostics
                .Where(d => d.Severity == Severity.Error)
                .Select(d => d.Code)
                .ToArray();
        }

        /// <summary>
        /// 按「说明列布局」写工作簿 —— A 列是制作人说明列（非导出），业务字段从 B 列开始。
        /// 用途：验证说明列 / 行备注完全不影响 Canonical 输出。
        /// </summary>
        public static void WriteWorkbookWithAnnotationColumn(TestRoot root, string fileName, TestSheet sheet, string[] rowNotes)
        {
            Directory.CreateDirectory(root.Paths.AuthoringDir);
            string path = Path.Combine(root.Paths.AuthoringDir, fileName);

            using (var wb = new ClosedXML.Excel.XLWorkbook())
            {
                var ws = wb.AddWorksheet(sheet.Name);

                ws.Cell(1, 1).Value = RawSheet.AnnotationMachineFieldLabel;
                ws.Cell(2, 1).Value = RawSheet.AnnotationTypeLabel;
                ws.Cell(3, 1).Value = RawSheet.AnnotationRuleLabel;
                ws.Cell(4, 1).Value = RawSheet.AnnotationHeaderLabel;

                for (int i = 0; i < sheet.Columns.Count; i++)
                {
                    int c = i + 2;   // 业务字段从 B 列开始
                    ws.Cell(1, c).Value = sheet.Columns[i].Machine;
                    ws.Cell(2, c).Value = sheet.Columns[i].Type;
                    ws.Cell(3, c).Value = sheet.Columns[i].Rule;
                    ws.Cell(4, c).Value = sheet.Columns[i].Header;
                }

                for (int r = 0; r < sheet.Rows.Count; r++)
                {
                    var row = sheet.Rows[r];
                    for (int c = 0; c < row.Length && c < sheet.Columns.Count; c++)
                    {
                        SetTestCell(ws.Cell(5 + r, c + 2), row[c]);
                    }

                    if (rowNotes != null && r < rowNotes.Length && !string.IsNullOrEmpty(rowNotes[r]))
                    {
                        ws.Cell(5 + r, 1).Value = rowNotes[r];
                    }
                }

                wb.SaveAs(path);
            }
        }

        private static void SetTestCell(ClosedXML.Excel.IXLCell cell, object value)
        {
            if (value == null) return;

            if (value is string s) cell.Value = s;
            else if (value is int i) cell.Value = i;
            else if (value is long l) cell.Value = l;
            else if (value is double d) cell.Value = d;
            else if (value is bool b) cell.Value = b;
            else cell.Value = value.ToString();
        }
    }
}
