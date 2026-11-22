using System.Collections.Generic;
using System.Linq;
using RRPG.ConfigCompiler.Core;
using Xunit;

namespace RRPG.ConfigCompiler.Tests
{
    /// <summary>1:N 子表 / 父引用 / 复合 key / order 冲突 / 跨 Workbook 实体唯一性。</summary>
    public class CrossReferenceTests
    {
        private static CompileResult Build(TestRoot root)
        {
            return CompilerRunner.Run(root.Paths, CompilerRunner.CommandBuild, true);
        }

        [Fact]
        public void ChildWithMissingParent_IsHardError()
        {
            using (var root = new TestRoot())
            {
                root.WriteWorkbook("RRPG_Test.xlsx",
                    Fixtures.ParentSheet(),
                    Fixtures.ChildSheet(new object[] { "P_Z", 1, "孤儿行" }));

                var result = Build(root);
                Assert.False(result.Success);
                Assert.Contains(result.Diagnostics, d => d.Code == DiagnosticCodes.DatRefUnresolved);
            }
        }

        [Fact]
        public void RefToUnknownSheet_IsHardError()
        {
            using (var root = new TestRoot())
            {
                root.WriteWorkbook("RRPG_Test.xlsx",
                    new TestSheet("FixtureChild")
                        .Col("ParentId", "ref:NoSuchSheet", "key")
                        .Col("Seq", "int", "key")
                        .Row("X", 1));

                var result = Build(root);
                Assert.False(result.Success);
                Assert.Contains(result.Diagnostics, d => d.Code == DiagnosticCodes.DatRefUnknownSheet);
            }
        }

        [Fact]
        public void DuplicateCompositeKey_IsHardError()
        {
            using (var root = new TestRoot())
            {
                // 同一父项同一 Seq = order 冲突（复合 key = ParentId + Seq）
                root.WriteWorkbook("RRPG_Test.xlsx",
                    Fixtures.ParentSheet(),
                    Fixtures.ChildSheet(
                        new object[] { "P_A", 1, "第一条" },
                        new object[] { "P_A", 1, "重复 order" }));

                var result = Build(root);
                Assert.False(result.Success);
                Assert.Contains(result.Diagnostics, d => d.Code == DiagnosticCodes.DatKeyDuplicate);
            }
        }

        [Fact]
        public void ChildPhysicalRowOrder_DoesNotAffectCanonicalOutput()
        {
            using (var root = new TestRoot())
            {
                root.WriteWorkbook("RRPG_Test.xlsx",
                    Fixtures.ParentSheet(),
                    Fixtures.ChildSheet(Fixtures.DefaultChildRows()));

                var first = Build(root);
                Assert.True(first.Success, string.Join(" | ", first.Diagnostics.Select(d => d.ToString())));
                string json1 = root.ReadGenerated("FixtureChild.json");

                // 物理行序完全不同（倒序）
                var reversed = Fixtures.DefaultChildRows().Reverse().ToArray();
                root.WriteWorkbook("RRPG_Test.xlsx", Fixtures.ParentSheet(), Fixtures.ChildSheet(reversed));

                var second = Build(root);
                Assert.True(second.Success);
                string json2 = root.ReadGenerated("FixtureChild.json");

                Assert.Equal(json1, json2);

                // 规范顺序 = P_A/1, P_A/2, P_B/1, P_B/2
                int a1 = json1.IndexOf("子项A-1", System.StringComparison.Ordinal);
                int a2 = json1.IndexOf("子项A-2", System.StringComparison.Ordinal);
                int b1 = json1.IndexOf("子项B-1", System.StringComparison.Ordinal);
                int b2 = json1.IndexOf("子项B-2", System.StringComparison.Ordinal);
                Assert.True(a1 >= 0 && a2 > a1 && b1 > a2 && b2 > b1, "子表必须按 (ParentId, Seq) 规范排序");
            }
        }

        [Fact]
        public void SameEntityNameInTwoWorkbooks_IsHardError()
        {
            using (var root = new TestRoot())
            {
                root.WriteWorkbook("a_first.xlsx", Fixtures.MinimalMonsterSheet());
                root.WriteWorkbook("b_second.xlsx", Fixtures.MinimalMonsterSheet());

                var result = Build(root);
                Assert.False(result.Success);
                Assert.Contains(result.Diagnostics, d => d.Code == DiagnosticCodes.WbDuplicateEntitySheet);
            }
        }

        // ==================== v3：复合身份（key）语义 ====================

        [Fact]
        public void KeyComponent_MayRepeat_WithinSameParent()
        {
            using (var root = new TestRoot())
            {
                root.WriteWorkbook("RRPG_Test.xlsx",
                    Fixtures.ParentSheet(),
                    Fixtures.ChildSheet(
                        new object[] { "P_A", 1, "子项A-1" },
                        new object[] { "P_A", 2, "子项A-2" }));

                var result = Build(root);
                Assert.True(result.Success, string.Join(" | ", result.Diagnostics.Select(d => d.ToString())));

                string json = root.ReadGenerated("FixtureChild.json");
                Assert.Contains("\"ParentId\": \"P_A\"", json);
                Assert.Contains("\"Seq\": 1", json);
                Assert.Contains("\"Seq\": 2", json);
            }
        }

        [Fact]
        public void KeyComponent_MayRepeat_AcrossParents()
        {
            using (var root = new TestRoot())
            {
                root.WriteWorkbook("RRPG_Test.xlsx",
                    Fixtures.ParentSheet(),
                    Fixtures.ChildSheet(
                        new object[] { "P_A", 1, "子项A-1" },
                        new object[] { "P_B", 1, "子项B-1" }));

                var result = Build(root);
                Assert.True(result.Success, string.Join(" | ", result.Diagnostics.Select(d => d.ToString())));

                string json = root.ReadGenerated("FixtureChild.json");
                Assert.Contains("子项A-1", json);
                Assert.Contains("子项B-1", json);
            }
        }

        [Fact]
        public void CanonicalOrdering_UsesCompositeKeyAsDeclared()
        {
            using (var root = new TestRoot())
            {
                root.WriteWorkbook("RRPG_Test.xlsx",
                    Fixtures.ParentSheet(),
                    Fixtures.ChildSheet(Fixtures.DefaultChildRows()));

                var result = Build(root);
                Assert.True(result.Success, string.Join(" | ", result.Diagnostics.Select(d => d.ToString())));

                // 报告中的身份列 = 声明顺序的 key 列
                var sheet = result.Workbooks.SelectMany(w => w.Sheets).Single(s => s.Name == "FixtureChild");
                Assert.Equal(new[] { "ParentId", "Seq" }, sheet.Key);

                // IR 中的 key 数组与 row 顺序一致
                string json = root.ReadGenerated("FixtureChild.json");
                int a1 = json.IndexOf("子项A-1", System.StringComparison.Ordinal);
                int a2 = json.IndexOf("子项A-2", System.StringComparison.Ordinal);
                int b1 = json.IndexOf("子项B-1", System.StringComparison.Ordinal);
                int b2 = json.IndexOf("子项B-2", System.StringComparison.Ordinal);
                Assert.True(a1 >= 0 && a2 > a1 && b1 > a2 && b2 > b1,
                    "必须按 (ParentId, Seq) 升序输出");
            }
        }

        [Fact]
        public void KeyToken_OnSheetThatAlsoHasId_IsHardError()
        {
            using (var root = new TestRoot())
            {
                root.WriteWorkbook("RRPG_Test.xlsx", new TestSheet("Mixed")
                    .Col("Id", "id", "")
                    .Col("Sub", "string", "key")
                    .Row("x1", "s1"));

                var result = Build(root);
                Assert.False(result.Success);
                Assert.Contains(result.Diagnostics, d => d.Code == DiagnosticCodes.SchIdWithKey);
            }
        }
    }
}
