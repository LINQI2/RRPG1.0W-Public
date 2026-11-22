using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using RRPG.ConfigCompiler.Core;
using Xunit;

namespace RRPG.ConfigCompiler.Tests
{
    /// <summary>确定性：逐字节一致、culture 无关、行序稳定、LF / 无 BOM。</summary>
    public class DeterminismTests
    {
        [Fact]
        public void RepeatedBuild_ProducesByteIdenticalOutput_AndTouchesNothing()
        {
            using (var root = new TestRoot())
            {
                root.WriteWorkbook("RRPG_Test.xlsx", Fixtures.MinimalMonsterSheet());

                var first = CompilerRunner.Run(root.Paths, CompilerRunner.CommandBuild, true);
                Assert.True(first.Success);

                string path = root.GeneratedFile("Monster.json");
                byte[] bytes1 = File.ReadAllBytes(path);
                System.DateTime mtime1 = File.GetLastWriteTimeUtc(path);

                var second = CompilerRunner.Run(root.Paths, CompilerRunner.CommandBuild, true);
                Assert.True(second.Success);

                byte[] bytes2 = File.ReadAllBytes(path);
                System.DateTime mtime2 = File.GetLastWriteTimeUtc(path);

                Assert.Equal(bytes1, bytes2);
                Assert.Equal(mtime1, mtime2); // 未变化 → 不触碰（保持 mtime）

                var monster = second.Outputs.Single(o => o.Name == "Monster.json");
                Assert.Equal("unchanged", monster.Action);
            }
        }

        [Fact]
        public void CultureWithCommaDecimalSeparator_DoesNotChangeOutput()
        {
            using (var root = new TestRoot())
            {
                root.WriteWorkbook("RRPG_Test.xlsx",
                    Fixtures.ParentSheet(),
                    Fixtures.ChildSheet(new object[] { "P_A", 1, "子项" }));

                string baseline;
                var previous = CultureInfo.CurrentCulture;
                var previousThread = CultureInfo.DefaultThreadCurrentCulture;

                try
                {
                    CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
                    CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
                    var run = CompilerRunner.Run(root.Paths, CompilerRunner.CommandBuild, true);
                    Assert.True(run.Success);
                    baseline = root.ReadGenerated("FixtureParent.json");

                    // de-DE：小数分隔符为逗号 —— 输出必须完全一致
                    var german = new CultureInfo("de-DE");
                    CultureInfo.CurrentCulture = german;
                    CultureInfo.DefaultThreadCurrentCulture = german;

                    var run2 = CompilerRunner.Run(root.Paths, CompilerRunner.CommandBuild, true);
                    Assert.True(run2.Success);
                    string underGerman = root.ReadGenerated("FixtureParent.json");

                    Assert.Equal(baseline, underGerman);
                    Assert.Contains("\"Weight\": 0.25", underGerman); // 小数点必须是 '.'，不能变 ','
                }
                finally
                {
                    CultureInfo.CurrentCulture = previous;
                    CultureInfo.DefaultThreadCurrentCulture = previousThread;
                }
            }
        }

        [Fact]
        public void AddingRow_KeepsExistingRowsInPlace()
        {
            using (var root = new TestRoot())
            {
                root.WriteWorkbook("RRPG_Test.xlsx", Fixtures.MinimalMonsterSheet());
                var first = CompilerRunner.Run(root.Paths, CompilerRunner.CommandBuild, true);
                Assert.True(first.Success);
                string before = root.ReadGenerated("Monster.json");

                root.WriteWorkbook("RRPG_Test.xlsx", new TestSheet("Monster")
                    .Col("MonsterId", "id", "", "怪物ID")
                    .Col("Name", "string", "", "怪物名称")
                    .Col("MaxHP", "int", ">=1", "最大生命值")
                    .Row("mon_001", "史莱姆", 20)
                    .Row("mon_002", "骷髅弓手", 15)
                    .Row("mon_004", "新增测试怪", 33));

                var second = CompilerRunner.Run(root.Paths, CompilerRunner.CommandBuild, true);
                Assert.True(second.Success, string.Join(" | ", second.Diagnostics.Select(d => d.ToString())));
                string after = root.ReadGenerated("Monster.json");

                // 新增一行 = 局部插入，既有行内容不重排
                // （v2：行内键按**机器字段名**排序，所以断言不再依赖手写字段顺序）
                Assert.Contains("\"MaxHP\": 20,\n      \"MonsterId\": \"mon_001\",\n      \"Name\": \"史莱姆\"", after);
                Assert.Contains("\"MaxHP\": 15,\n      \"MonsterId\": \"mon_002\",\n      \"Name\": \"骷髅弓手\"", after);
                Assert.Contains("\"MonsterId\": \"mon_004\"", after);
                Assert.DoesNotContain("\"MonsterId\": \"mon_004\"", before);
            }
        }

        [Fact]
        public void Json_UsesLfOnly_AndHasNoBom()
        {
            using (var root = new TestRoot())
            {
                root.WriteWorkbook("RRPG_Test.xlsx", Fixtures.MinimalMonsterSheet());
                var result = CompilerRunner.Run(root.Paths, CompilerRunner.CommandBuild, true);
                Assert.True(result.Success);

                byte[] bytes = File.ReadAllBytes(root.GeneratedFile("Monster.json"));

                Assert.False(bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF, "不得写入 BOM");
                Assert.DoesNotContain((byte)0x0D, bytes); // 不得出现 CR
                Assert.Equal((byte)'\n', bytes[bytes.Length - 1]); // 末行为单个 LF
            }
        }
    }
}
