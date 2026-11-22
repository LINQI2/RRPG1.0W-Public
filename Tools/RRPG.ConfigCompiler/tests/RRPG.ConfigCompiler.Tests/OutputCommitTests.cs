using System.IO;
using System.Linq;
using RRPG.ConfigCompiler.Core;
using Xunit;

namespace RRPG.ConfigCompiler.Tests
{
    /// <summary>Atomic Replace 与 Hard Error 时「正式输出 0 更新」。</summary>
    public class OutputCommitTests
    {
        [Fact]
        public void ChangedInput_UpdatesOutput_AndStaleFilesAreDeleted()
        {
            using (var root = new TestRoot())
            {
                root.WriteWorkbook("RRPG_Test.xlsx", Fixtures.MinimalMonsterSheet());
                var first = CompilerRunner.Run(root.Paths, CompilerRunner.CommandBuild, true);
                Assert.True(first.Success);

                // 制造一个 stale 文件（Generated/ 由编译器独占）
                string stale = root.GeneratedFile("Stale.json");
                File.WriteAllText(stale, "stale");

                // 改变一个值 → 该输出必须 updated，其它不变
                root.WriteWorkbook("RRPG_Test.xlsx", new TestSheet("Monster")
                    .Col("MonsterId", "id", "", "怪物ID")
                    .Col("Name", "string", "", "怪物名称")
                    .Col("MaxHP", "int", ">=1", "最大生命值")
                    .Row("mon_001", "史莱姆", 24)
                    .Row("mon_002", "骷髅弓手", 15));

                var second = CompilerRunner.Run(root.Paths, CompilerRunner.CommandBuild, true);
                Assert.True(second.Success);

                Assert.Equal("updated", second.Outputs.Single(o => o.Name == "Monster.json").Action);

                // BuildReport.md 内嵌输出哈希 → 任一输出变化时报告也会更新（设计后果，已记录）
                Assert.Equal("updated", second.Outputs.Single(o => o.Name == "BuildReport.md").Action);

                // 未参与变化的输出（Stale.json 之外的独立检查见 DeterminismTests）
                Assert.Equal("deleted", second.Outputs.Single(o => o.Name == "Stale.json").Action);
                Assert.False(File.Exists(stale));
                Assert.Contains("\"MaxHP\": 24", root.ReadGenerated("Monster.json"));
            }
        }

        [Fact]
        public void HardError_LeavesCanonicalIrUntouched_AndOnlyUpdatesBuildState()
        {
            using (var root = new TestRoot())
            {
                root.WriteWorkbook("RRPG_Test.xlsx", Fixtures.MinimalMonsterSheet());
                var ok = CompilerRunner.Run(root.Paths, CompilerRunner.CommandBuild, true);
                Assert.True(ok.Success);

                string irPath = root.GeneratedFile("Monster.json");
                byte[] before = File.ReadAllBytes(irPath);
                var mtimeBefore = File.GetLastWriteTimeUtc(irPath);
                string statePath = root.BuildStateFile("BuildReport.json");
                byte[] reportBefore = File.ReadAllBytes(statePath);

                // 制造 Hard Error（非法 int）
                root.WriteWorkbook("RRPG_Test.xlsx", new TestSheet("Monster")
                    .Col("MonsterId", "id", "")
                    .Col("MaxHP", "int", "")
                    .Row("mon_001", "不是数字"));

                var failed = CompilerRunner.Run(root.Paths, CompilerRunner.CommandBuild, true);

                Assert.False(failed.Success);
                Assert.True(failed.ErrorCount > 0);

                // Canonical IR（tracked 产物）：0 更新（内容与 mtime 都不变）
                Assert.Equal(before, File.ReadAllBytes(irPath));
                Assert.Equal(mtimeBefore, File.GetLastWriteTimeUtc(irPath));

                // 失败只更新 .build-state/，否则失败不可审计
                Assert.NotEqual(reportBefore, File.ReadAllBytes(statePath));
                Assert.Contains("\"status\": \"FAILED\"", root.ReadBuildState("BuildReport.json"));
                Assert.Contains("\"hardErrorZeroWrite\": true", root.ReadBuildState("BuildReport.json"));

                // 本次被写的只有 .build-state/ 下的两个报告
                Assert.Equal(2, failed.Outputs.Count);
                Assert.All(failed.Outputs, o => Assert.True(
                    o.Name == OutputCommitter.ReportFileName || o.Name == OutputCommitter.ReportMarkdownName,
                    "失败时不得写其它输出：" + o.Name));
            }
        }

        [Fact]
        public void HardError_WhenNoGeneratedYet_DoesNotCreateCanonicalIr()
        {
            using (var root = new TestRoot())
            {
                root.WriteWorkbook("RRPG_Test.xlsx", new TestSheet("Monster")
                    .Col("MonsterId", "id", "")
                    .Col("MaxHP", "int", "")
                    .Row("mon_001", "保"));

                var failed = CompilerRunner.Run(root.Paths, CompilerRunner.CommandBuild, true);
                Assert.False(failed.Success);

                Assert.False(File.Exists(root.GeneratedFile("Monster.json")));
                Assert.False(File.Exists(root.GeneratedFile(OutputCommitter.ReportFileName)));
                Assert.False(File.Exists(root.GeneratedFile(OutputCommitter.ReportMarkdownName)));
                Assert.True(File.Exists(root.BuildStateFile(OutputCommitter.ReportFileName)));
                Assert.True(File.Exists(root.BuildStateFile(OutputCommitter.ReportMarkdownName)));
            }
        }

        [Fact]
        public void Success_WritesCanonicalIrToGenerated_AndReportsToBuildState()
        {
            using (var root = new TestRoot())
            {
                root.WriteWorkbook("RRPG_Test.xlsx", Fixtures.MinimalMonsterSheet());

                var result = CompilerRunner.Run(root.Paths, CompilerRunner.CommandBuild, true);
                Assert.True(result.Success);

                Assert.True(File.Exists(root.GeneratedFile("Monster.json")));
                Assert.False(File.Exists(root.GeneratedFile(OutputCommitter.ReportFileName)),
                    "BuildReport 不得写入 Generated/（Generated 只放 Canonical IR）");

                Assert.True(File.Exists(root.BuildStateFile(OutputCommitter.ReportFileName)));
                Assert.True(File.Exists(root.BuildStateFile(OutputCommitter.ReportMarkdownName)));

                // 报告记录的是真实提交结果（不是 pending）
                string report = root.ReadBuildState("BuildReport.json");
                Assert.Contains("\"action\": \"created\"", report);

                var irAction = result.Outputs.Single(o => o.Name == "Monster.json");
                Assert.Equal("created", irAction.Action);
            }
        }

        [Fact]
        public void ValidateCommand_WritesNothing()
        {
            using (var root = new TestRoot())
            {
                root.WriteWorkbook("RRPG_Test.xlsx", Fixtures.MinimalMonsterSheet());

                // 首次（Mirror 尚未生成）：validate 必须报 mirror-missing，且**不写任何文件**。
                var first = CompilerRunner.Run(root.Paths, CompilerRunner.CommandValidate, false);
                Assert.False(first.Success);
                Assert.Contains(DiagnosticCodes.MirrorMissing, first.Diagnostics.Select(d => d.Code));
                Assert.False(Directory.Exists(root.GeneratedDir), "validate 不得创建 Generated 目录");
                Assert.All(first.Outputs, o => Assert.Equal("not-written", o.Action));

                // build 之后 validate 必须能证明 Authoring == Mirror，且仍然 0 写入。
                var build = CompilerRunner.Run(root.Paths, CompilerRunner.CommandBuild, true);
                Assert.True(build.Success, string.Join(" | ", build.Diagnostics.Select(d => d.ToString())));
                byte[] after = File.ReadAllBytes(root.GeneratedFile("Monster.json"));

                var result = CompilerRunner.Run(root.Paths, CompilerRunner.CommandValidate, false);
                Assert.True(result.Success, string.Join(" | ", result.Diagnostics.Select(d => d.ToString())));
                Assert.All(result.Outputs, o => Assert.Equal("not-written", o.Action));
                Assert.Equal(after, File.ReadAllBytes(root.GeneratedFile("Monster.json")));
            }
        }
    }
}
