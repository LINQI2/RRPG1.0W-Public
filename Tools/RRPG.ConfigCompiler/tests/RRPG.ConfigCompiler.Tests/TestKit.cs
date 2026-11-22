using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using ClosedXML.Excel;
using RRPG.ConfigCompiler.Core;

namespace RRPG.ConfigCompiler.Tests
{
    /// <summary>测试用的列定义（machine / type / rule / header）。</summary>
    public sealed class TestColumn
    {
        public string Machine;
        public string Type;
        public string Rule;
        public string Header;

        public TestColumn(string machine, string type, string rule, string header = null)
        {
            Machine = machine;
            Type = type;
            Rule = rule;
            Header = header ?? machine;
        }
    }

    /// <summary>测试用的 Sheet 定义。</summary>
    public sealed class TestSheet
    {
        public string Name;
        public List<TestColumn> Columns = new List<TestColumn>();
        public List<object[]> Rows = new List<object[]>();

        public TestSheet(string name) { Name = name; }

        public TestSheet Col(string machine, string type, string rule, string header = null)
        {
            Columns.Add(new TestColumn(machine, type, rule, header));
            return this;
        }

        public TestSheet Row(params object[] values)
        {
            Rows.Add(values);
            return this;
        }
    }

    /// <summary>
    /// 测试用一次性 Spike 根目录：在系统临时目录下创建，测试结束删除。
    /// （不污染仓库工作区，也不需要 Unity。）
    /// </summary>
    public sealed class TestRoot : IDisposable
    {
        public string Root { get; private set; }
        public CompilerPaths Paths { get; private set; }

        public TestRoot()
        {
            Root = Path.Combine(Path.GetTempPath(), "rrpg-spike-a-tests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Root);
            Paths = CompilerPaths.Resolve(Root);
        }

        public string AuthoringDir { get { return Paths.AuthoringDir; } }

        public string GeneratedDir { get { return Paths.GeneratedDir; } }

        /// <summary>写出一个测试工作簿；返回绝对路径。</summary>
        public string WriteWorkbook(string fileName, params TestSheet[] sheets)
        {
            Directory.CreateDirectory(Paths.AuthoringDir);
            string path = Path.Combine(Paths.AuthoringDir, fileName);

            using (var wb = new XLWorkbook())
            {
                foreach (var sheet in sheets)
                {
                    var ws = wb.AddWorksheet(sheet.Name);
                    for (int i = 0; i < sheet.Columns.Count; i++)
                    {
                        int c = i + 1;
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
                            SetValue(ws.Cell(5 + r, c + 1), row[c]);
                        }
                    }
                }

                wb.SaveAs(path);
            }

            return path;
        }

        /// <summary>写出带公式单元格的工作簿（公式必须被编译器拒绝）。</summary>
        public string WriteWorkbookWithFormula(string fileName)
        {
            Directory.CreateDirectory(Paths.AuthoringDir);
            string path = Path.Combine(Paths.AuthoringDir, fileName);

            using (var wb = new XLWorkbook())
            {
                var ws = wb.AddWorksheet("Monster");
                ws.Cell(1, 1).Value = "MonsterId";
                ws.Cell(1, 2).Value = "MaxHP";
                ws.Cell(2, 1).Value = "id";
                ws.Cell(2, 2).Value = "int";
                ws.Cell(3, 1).Value = "";
                ws.Cell(3, 2).Value = ">=1";
                ws.Cell(4, 1).Value = "怪物ID";
                ws.Cell(4, 2).Value = "最大生命值";
                ws.Cell(5, 1).Value = "mon_001";
                ws.Cell(5, 2).FormulaA1 = "10+10";

                wb.SaveAs(path);
            }

            return path;
        }

        /// <summary>写出一个损坏的“xlsx”（内容不是 OOXML 包），用于包校验负例。</summary>
        public string WriteBrokenWorkbook(string fileName)
        {
            Directory.CreateDirectory(Paths.AuthoringDir);
            string path = Path.Combine(Paths.AuthoringDir, fileName);
            File.WriteAllText(path, "this is not an xlsx package");
            return path;
        }

        /// <summary>在 Authoring 目录放一个 Excel 临时锁文件（~$ 前缀）。</summary>
        public string WriteTempLockFile(string fileName)
        {
            Directory.CreateDirectory(Paths.AuthoringDir);
            string path = Path.Combine(Paths.AuthoringDir, fileName);
            File.WriteAllText(path, "excel lock");
            return path;
        }

        public string GeneratedFile(string name)
        {
            return Path.Combine(Paths.GeneratedDir, name);
        }

        /// <summary>M3：源资产根（<c>Development/SourceAssets</c>）。</summary>
        public string SourceAssetsDir { get { return Paths.SourceAssetsDir; } }

        /// <summary>
        /// M3：写入一个测试源资产（路径相对 SourceAssets，正斜杠）。
        /// 注意：Compiler 只校验「存在 + 扩展名」，不校验 PNG 内容本身，所以测试用任意字节。
        /// </summary>
        public string WriteSourceAsset(string relativePath, byte[] bytes)
        {
            string path = Path.Combine(Paths.SourceAssetsDir,
                relativePath.Replace('/', Path.DirectorySeparatorChar));

            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, bytes);
            return path;
        }

        /// <summary>M3：写入测试源资产（内容 = <paramref name="content"/> 的 UTF-8 字节）。</summary>
        public string WriteSourceAsset(string relativePath, string content = "test-source-bytes")
        {
            return WriteSourceAsset(relativePath, new UTF8Encoding(false).GetBytes(content));
        }

        public string BuildStateFile(string name)
        {
            return Path.Combine(Paths.BuildStateDir, name);
        }

        public string ReadGenerated(string name)
        {
            return File.ReadAllText(GeneratedFile(name));
        }

        public string ReadBuildState(string name)
        {
            return File.ReadAllText(BuildStateFile(name));
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(Root)) Directory.Delete(Root, true);
            }
            catch { /* 清理失败不影响断言结果 */ }
        }

        private static void SetValue(IXLCell cell, object value)
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
