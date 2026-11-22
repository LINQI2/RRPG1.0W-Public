using System.Linq;
using RRPG.ConfigCompiler.Core;
using Xunit;

namespace RRPG.ConfigCompiler.Tests
{
    /// <summary>default / optional / 公式拒绝。</summary>
    public class DataDefaultingTests
    {
        [Fact]
        public void Default_FillsEmptyCell_AndIsVisibleInIr()
        {
            using (var root = new TestRoot())
            {
                root.WriteWorkbook("RRPG_Test.xlsx", new TestSheet("Monster")
                    .Col("MonsterId", "id", "")
                    .Col("Faction", "string", "default=enemy")
                    .Row("mon_001", null)
                    .Row("mon_002", "player"));

                var result = DataValidationTests.Build(root);
                Assert.True(result.Success, string.Join(" | ", result.Diagnostics.Select(d => d.ToString())));

                string json = root.ReadGenerated("Monster.json");
                Assert.Contains("\"Faction\": \"enemy\"", json);
                Assert.Contains("\"Faction\": \"player\"", json);
            }
        }

        [Fact]
        public void OptionalEmpty_BecomesNullInIr()
        {
            using (var root = new TestRoot())
            {
                root.WriteWorkbook("RRPG_Test.xlsx", new TestSheet("Monster")
                    .Col("MonsterId", "id", "")
                    .Col("Note", "string", "optional")
                    .Row("mon_001", null)
                    .Row("mon_002", "备注"));

                var result = DataValidationTests.Build(root);
                Assert.True(result.Success, string.Join(" | ", result.Diagnostics.Select(d => d.ToString())));

                string json = root.ReadGenerated("Monster.json");
                Assert.Contains("\"Note\": null", json);
                Assert.Contains("\"Note\": \"备注\"", json);
            }
        }

        [Fact]
        public void FormulaCell_IsHardError()
        {
            using (var root = new TestRoot())
            {
                root.WriteWorkbookWithFormula("RRPG_Test.xlsx");

                var result = DataValidationTests.Build(root);
                Assert.False(result.Success);
                Assert.Contains(result.Diagnostics, d => d.Code == DiagnosticCodes.DatFormulaNotAllowed);
            }
        }
    }
}
