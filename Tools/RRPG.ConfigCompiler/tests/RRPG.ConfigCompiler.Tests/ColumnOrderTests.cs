using System.Collections.Generic;
using System.IO;
using System.Linq;
using RRPG.ConfigCompiler.Core;
using Xunit;

namespace RRPG.ConfigCompiler.Tests
{
    /// <summary>
    /// **列结构 / 列序无关** 契约（Producer UX 的核心保证）。
    ///
    /// 保证：制作人可以把列换位置、中间插列、插备注列、改中文表头 ——
    /// Canonical 输出**逐字节不变**；只有「改了字段名 / 少了必需字段 / 多了未声明列」才是错误。
    /// </summary>
    public class ColumnOrderTests
    {
        /// <summary>同一份数据、指定物理列顺序，编译一次并返回该 Sheet 的 Canonical 文本。</summary>
        private static string CompileWithColumnOrder(int[] columnOrder)
        {
            using (var root = new TestRoot())
            {
                var sheet = new TestSheet("Monster")
                    .Col("MonsterId", "id", "", "怪物ID")
                    .Col("Name", "string", "", "怪物名称")
                    .Col("MaxHP", "int", ">=1", "最大生命值")
                    .Row("mon_001", "史莱姆", 20)
                    .Row("mon_002", "骷髅弓手", 15);

                root.WriteWorkbook("RRPG_Test.xlsx", ReorderColumns(sheet, columnOrder));

                var result = CompilerRunner.Run(root.Paths, CompilerRunner.CommandBuild, true);
                Assert.True(result.Success, string.Join(" | ", result.Diagnostics.Select(d => d.ToString())));
                return root.ReadGenerated("Monster.json");
            }
        }

        /// <summary>按给定顺序重排物理列（不改变字段语义）。</summary>
        private static TestSheet ReorderColumns(TestSheet original, int[] order)
        {
            var reordered = new TestSheet(original.Name);
            foreach (var index in order) reordered.Columns.Add(original.Columns[index]);

            foreach (var row in original.Rows)
            {
                var values = new object[order.Length];
                for (int i = 0; i < order.Length; i++) values[i] = row[order[i]];
                reordered.Row(values);
            }

            return reordered;
        }

        [Fact]
        public void ReorderingColumns_ProducesByteIdenticalCanonicalJson()
        {
            string baseline = CompileWithColumnOrder(new[] { 0, 1, 2 });
            string reordered = CompileWithColumnOrder(new[] { 2, 0, 1 });
            string rotated = CompileWithColumnOrder(new[] { 1, 2, 0 });

            Assert.Equal(baseline, reordered);
            Assert.Equal(baseline, rotated);
        }

        [Fact]
        public void ChangingChineseHeader_ProducesByteIdenticalCanonicalJson()
        {
            using (var root = new TestRoot())
            {
                root.WriteWorkbook("RRPG_Test.xlsx", new TestSheet("Monster")
                    .Col("MonsterId", "id", "", "怪物ID")
                    .Col("Name", "string", "", "怪物名称")
                    .Col("MaxHP", "int", ">=1", "最大生命值")
                    .Row("mon_001", "史莱姆", 20));

                var first = CompilerRunner.Run(root.Paths, CompilerRunner.CommandBuild, true);
                Assert.True(first.Success);
                string before = root.ReadGenerated("Monster.json");

                // 只改中文表头（制作人每天都在干的事）
                root.WriteWorkbook("RRPG_Test.xlsx", new TestSheet("Monster")
                    .Col("MonsterId", "id", "", "怪物编号（随便改都行）")
                    .Col("Name", "string", "", "名字")
                    .Col("MaxHP", "int", ">=1", "最大血量（说明文字）")
                    .Row("mon_001", "史莱姆", 20));

                var second = CompilerRunner.Run(root.Paths, CompilerRunner.CommandBuild, true);
                Assert.True(second.Success, string.Join(" | ", second.Diagnostics.Select(d => d.ToString())));

                Assert.Equal(before, root.ReadGenerated("Monster.json"));
            }
        }

        [Fact]
        public void AnnotationColumn_IsIgnoredEntirely()
        {
            using (var root = new TestRoot())
            {
                var sheet = new TestSheet("Monster")
                    .Col("MonsterId", "id", "", "怪物ID")
                    .Col("MaxHP", "int", ">=1", "最大生命值")
                    .Row("mon_001", 20);

                // 旧布局（A 列就是业务字段）
                root.WriteWorkbook("RRPG_Test.xlsx", sheet);
                var legacy = CompilerRunner.Run(root.Paths, CompilerRunner.CommandBuild, true);
                Assert.True(legacy.Success, string.Join(" | ", legacy.Diagnostics.Select(d => d.ToString())));
                string before = root.ReadGenerated("Monster.json");

                // 新布局（A 列 = 说明栏 + 行备注）
                TestSupport.WriteWorkbookWithAnnotationColumn(root, "RRPG_Test.xlsx", sheet, new[] { "这一行是行备注，不进配置" });
                var withAnnotation = CompilerRunner.Run(root.Paths, CompilerRunner.CommandBuild, true);
                Assert.True(withAnnotation.Success, string.Join(" | ", withAnnotation.Diagnostics.Select(d => d.ToString())));

                Assert.Equal(before, root.ReadGenerated("Monster.json"));
            }
        }

        [Fact]
        public void DeclaredNoteColumn_BetweenFields_IsIgnored()
        {
            using (var root = new TestRoot())
            {
                root.WriteWorkbook("RRPG_Test.xlsx", new TestSheet("Monster")
                    .Col("MonsterId", "id", "", "怪物ID")
                    .Col("MaxHP", "int", ">=1", "最大生命值")
                    .Row("mon_001", 20));

                var first = CompilerRunner.Run(root.Paths, CompilerRunner.CommandBuild, true);
                Assert.True(first.Success);
                string before = root.ReadGenerated("Monster.json");

                // 在两个业务字段之间插入一列备注列：Row 2 写 ignore
                root.WriteWorkbook("RRPG_Test.xlsx", new TestSheet("Monster")
                    .Col("MonsterId", "id", "", "怪物ID")
                    .Col("Notes", "ignore", "", "给人看的备注")
                    .Col("MaxHP", "int", ">=1", "最大生命值")
                    .Row("mon_001", "这句话不会进配置", 20));

                var second = CompilerRunner.Run(root.Paths, CompilerRunner.CommandBuild, true);
                Assert.True(second.Success, string.Join(" | ", second.Diagnostics.Select(d => d.ToString())));

                Assert.Equal(before, root.ReadGenerated("Monster.json"));
            }
        }

        [Fact]
        public void InsertingBusinessField_AtDifferentPositions_KeepsSemantics()
        {
            using (var root = new TestRoot())
            {
                root.WriteWorkbook("RRPG_Test.xlsx", new TestSheet("Monster")
                    .Col("MonsterId", "id", "", "怪物ID")
                    .Col("MaxHP", "int", ">=1", "最大生命值")
                    .Col("Faction", "string", "default=enemy", "阵营")
                    .Row("mon_001", 20, "enemy"));

                var tail = CompilerRunner.Run(root.Paths, CompilerRunner.CommandBuild, true);
                Assert.True(tail.Success, string.Join(" | ", tail.Diagnostics.Select(d => d.ToString())));
                string tailJson = root.ReadGenerated("Monster.json");

                root.WriteWorkbook("RRPG_Test.xlsx", new TestSheet("Monster")
                    .Col("MonsterId", "id", "", "怪物ID")
                    .Col("Faction", "string", "default=enemy", "阵营")
                    .Col("MaxHP", "int", ">=1", "最大生命值")
                    .Row("mon_001", "enemy", 20));

                var middle = CompilerRunner.Run(root.Paths, CompilerRunner.CommandBuild, true);
                Assert.True(middle.Success, string.Join(" | ", middle.Diagnostics.Select(d => d.ToString())));

                Assert.Equal(tailJson, root.ReadGenerated("Monster.json"));
            }
        }

        [Fact]
        public void DuplicateMachineField_IsHardError()
        {
            using (var root = new TestRoot())
            {
                root.WriteWorkbook("RRPG_Test.xlsx", new TestSheet("Monster")
                    .Col("MonsterId", "id", "", "怪物ID")
                    .Col("MonsterId", "string", "", "重复的字段名")
                    .Row("mon_001", "x"));

                var result = CompilerRunner.Run(root.Paths, CompilerRunner.CommandBuild, true);
                Assert.False(result.Success);
                Assert.Contains(DiagnosticCodes.SchDuplicateMachineField, TestSupport.ErrorCodes(result));
            }
        }

        [Fact]
        public void MissingIdentityColumn_IsHardError()
        {
            using (var root = new TestRoot())
            {
                // 身份列（id）缺失 / 被挪进不导出的备注列 → Sheet 没有身份 → Hard Error。
                // （M0 无正式领域，因此「必需字段」= 身份列，由 SchemaCompiler 统一保证。）
                root.WriteWorkbook("RRPG_Test.xlsx", new TestSheet("Sample")
                    .Col("Title", "string", "", "标题")
                    .Col("Amount", "int", ">=0", "数量")
                    .Row("甲", 20));

                var result = CompilerRunner.Run(root.Paths, CompilerRunner.CommandBuild, true);
                Assert.False(result.Success);
                Assert.Contains(DiagnosticCodes.SchNoKeyColumn, TestSupport.ErrorCodes(result));
            }
        }

        [Fact]
        public void UnrecognizedColumn_IsHardError()
        {
            using (var root = new TestRoot())
            {
                // 有中文表头、有数据，但没有机器字段名，也没声明 ignore
                root.WriteWorkbook("RRPG_Test.xlsx", new TestSheet("Monster")
                    .Col("MonsterId", "id", "", "怪物ID")
                    .Col("", "string", "", "我随手加的一列")
                    .Row("mon_001", "x"));

                var result = CompilerRunner.Run(root.Paths, CompilerRunner.CommandBuild, true);
                Assert.False(result.Success);

                var codes = TestSupport.ErrorCodes(result);
                Assert.True(codes.Contains(DiagnosticCodes.SchUnrecognizedColumn)
                    || codes.Contains(DiagnosticCodes.SchDataWithoutMachineField),
                    string.Join(" | ", result.Diagnostics.Select(d => d.ToString())));
            }
        }
    }
}
