using System.Linq;
using RRPG.ConfigCompiler.Core;
using RRPG.ConfigCompiler.Xlsx;
using Xunit;

namespace RRPG.ConfigCompiler.Tests
{
    /// <summary>Open XML SDK 包结构校验 + 损坏工作簿的 Hard Error 行为。</summary>
    public class PackageValidationTests
    {
        [Fact]
        public void FixtureWorkbooks_AreValidOpenXmlPackages()
        {
            using (var root = new TestRoot())
            {
                string a = root.WriteWorkbook("RRPG_Sample.xlsx", SampleSheets.Sample());
                string b = root.WriteWorkbook("RRPG_Second.xlsx", SampleSheets.Second());

                var issues = OpenXmlPackageChecker.Validate(a);
                Assert.True(issues.Count == 0, a + " 结构校验失败：" + string.Join(" | ", issues.Take(5)));
                Assert.Empty(OpenXmlPackageChecker.Validate(b));
            }
        }

        [Fact]
        public void FixtureWorkbook_CompilesToThreeRows()
        {
            using (var root = new TestRoot())
            {
                root.WriteWorkbook("RRPG_Sample.xlsx", SampleSheets.Sample());

                var result = CompilerRunner.Run(root.Paths, CompilerRunner.CommandBuild, true);
                Assert.True(result.Success, string.Join(" | ", result.Diagnostics.Select(d => d.ToString())));

                var sheet = result.Workbooks
                    .SelectMany(w => w.Sheets)
                    .Single(s => s.Name == "Sample");

                Assert.Equal("DATA", sheet.Kind);
                Assert.Equal(3, sheet.RowCount);
                Assert.Equal(new[] { "SampleId" }, sheet.Key);
                Assert.Contains("\"Amount\": 20", root.ReadGenerated("Sample.json"));
                Assert.Contains("\"Title\": \"甲\"", root.ReadGenerated("Sample.json"));
            }
        }

        [Fact]
        public void BrokenWorkbook_IsHardError_AndWritesNoConfigOutput()
        {
            using (var root = new TestRoot())
            {
                root.WriteBrokenWorkbook("RRPG_Broken.xlsx");

                var result = CompilerRunner.Run(root.Paths, CompilerRunner.CommandBuild, true);

                Assert.False(result.Success);
                Assert.Contains(result.Diagnostics, d => d.Code == DiagnosticCodes.WbOpenXmlInvalid);
                Assert.Contains(result.Diagnostics, d => d.Code == DiagnosticCodes.WbFileUnreadable);
                Assert.False(System.IO.File.Exists(root.GeneratedFile("Monster.json")));
            }
        }

        [Fact]
        public void PackageCheck_OnScaffoldedWorkbook_ReturnsNoIssues()
        {
            using (var root = new TestRoot())
            {
                string path = root.WriteWorkbook("RRPG_Sample.xlsx", SampleSheets.Sample());
                Assert.Empty(OpenXmlPackageChecker.Validate(path));
            }
        }
    }
}
