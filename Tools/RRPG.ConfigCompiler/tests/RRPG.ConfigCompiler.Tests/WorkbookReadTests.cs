using System.Collections.Generic;
using System.Linq;
using RRPG.ConfigCompiler.Core;
using Xunit;

namespace RRPG.ConfigCompiler.Tests
{
    /// <summary>Sheet 识别 + 四行表头解析 + 中文读取 + 空行 / 空白处理。</summary>
    public class WorkbookReadTests
    {
        internal static RawSheet ReadSingle(TestRoot root, string fileName = "RRPG_Test.xlsx")
        {
            var bag = new DiagnosticBag();
            var wb = WorkbookDiscovery.Discover(new[]
            {
                new KeyValuePair<string, string>("Authoring", root.AuthoringDir)
            }, bag).Single();

            return WorkbookReader.Read(wb, bag).Single();
        }

        [Fact]
        public void Read_SkipsNavSheet_AndIdentifiesDataSheet()
        {
            using (var root = new TestRoot())
            {
                root.WriteWorkbook("RRPG_Test.xlsx",
                    new TestSheet("00_导航").Col("说明", "string", "optional").Row("导航面"),
                    new TestSheet("_hidden").Col("X", "string", "optional").Row("x"),
                    Fixtures.MinimalMonsterSheet());

                var bag = new DiagnosticBag();
                var wb = WorkbookDiscovery.Discover(new[]
                {
                    new KeyValuePair<string, string>("Authoring", root.AuthoringDir)
                }, bag).Single();

                var sheets = WorkbookReader.Read(wb, bag);
                Assert.Equal(3, sheets.Count);
                Assert.True(sheets.Single(s => s.SheetName == "00_导航").SkippedForData);
                Assert.True(sheets.Single(s => s.SheetName == "_hidden").SkippedForData);

                var data = sheets.Single(s => s.SheetName == "Monster");
                Assert.False(data.SkippedForData);
                Assert.Equal(2, data.Rows.Count);
            }
        }

        [Fact]
        public void Read_ParsesFourHeaderRows_WithChinese()
        {
            using (var root = new TestRoot())
            {
                root.WriteWorkbook("RRPG_Test.xlsx", Fixtures.MinimalMonsterSheet());
                var sheet = ReadSingle(root);

                Assert.Equal("MonsterId", sheet.Columns[0].MachineField);
                Assert.Equal("id", sheet.Columns[0].TypeRaw);
                Assert.Equal("", sheet.Columns[0].RuleRaw);   // v2：id 天然唯一/必填，Row 3 可留空
                Assert.Equal("怪物ID", sheet.Columns[0].DisplayHeader);
                Assert.Equal(">=1", sheet.Columns[2].RuleRaw);

                Assert.Equal("史莱姆", sheet.Rows[0].Get(2));
                Assert.Equal("骷髅弓手", sheet.Rows[1].Get(2));
                Assert.Equal("20", sheet.Rows[0].Get(3));
            }
        }

        [Fact]
        public void Read_EmptyMiddleRow_IsSkippedWithWarning()
        {
            using (var root = new TestRoot())
            {
                root.WriteWorkbook("RRPG_Test.xlsx", new TestSheet("Monster")
                    .Col("MonsterId", "id", "")
                    .Col("MaxHP", "int", "")
                    .Row("mon_001", 20)
                    .Row(null, null)
                    .Row("mon_003", 30));

                var bag = new DiagnosticBag();
                var wb = WorkbookDiscovery.Discover(new[]
                {
                    new KeyValuePair<string, string>("Authoring", root.AuthoringDir)
                }, bag).Single();
                var read = WorkbookReader.Read(wb, bag).Single();

                Assert.Equal(2, read.Rows.Count);
                Assert.Contains(bag.Items, d => d.Code == DiagnosticCodes.DatEmptyRowSkipped);
            }
        }

        [Fact]
        public void Read_TextWhitespace_IsTrimmed()
        {
            using (var root = new TestRoot())
            {
                root.WriteWorkbook("RRPG_Test.xlsx", new TestSheet("Monster")
                    .Col("MonsterId", "id", "unique")
                    .Row("  mon_001  "));

                var sheet = ReadSingle(root);
                Assert.Equal("mon_001", sheet.Rows[0].Get(1));
            }
        }

        [Fact]
        public void Read_NumericCell_NormalizesToIntegerText()
        {
            using (var root = new TestRoot())
            {
                root.WriteWorkbook("RRPG_Test.xlsx", new TestSheet("Monster")
                    .Col("MonsterId", "id", "unique")
                    .Col("MaxHP", "int", "required")
                    .Row("mon_001", 20.0));

                var sheet = ReadSingle(root);
                Assert.Equal("20", sheet.Rows[0].Get(2));
            }
        }
    }
}
