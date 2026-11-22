using System.IO;
using System.Linq;
using RRPG.ConfigCompiler.Core;
using Xunit;

namespace RRPG.ConfigCompiler.Tests
{
    /// <summary>
    /// M0 空领域状态（FDS §3.2 / §7；用户 M0 §13）：
    /// 还没有任何已迁移领域时，validate / build 都必须成功、给出明确状态，
    /// 并且**不得伪造**任何领域 JSON。
    /// </summary>
    public class EmptyDomainStatusTests
    {
        [Fact]
        public void NoWorkbook_BuildSucceeds_AndProducesNoDomainJson()
        {
            using (var root = new TestRoot())
            {
                var result = CompilerRunner.Run(root.Paths, CompilerRunner.CommandBuild, true);

                Assert.True(result.Success);
                Assert.Equal(0, result.ErrorCount);
                Assert.Empty(result.Workbooks);
                Assert.Contains(result.Diagnostics, d => d.Code == DiagnosticCodes.WbNoWorkbookFound);

                if (Directory.Exists(root.GeneratedDir))
                {
                    Assert.Empty(Directory.GetFiles(root.GeneratedDir, "*.json"));
                }

                // 报告仍会写到本地 .build-state（不是正式配置产物）
                Assert.True(File.Exists(root.BuildStateFile(OutputCommitter.ReportMarkdownName)));
            }
        }

        [Fact]
        public void NoWorkbook_ValidateSucceeds_AndIsReadOnly()
        {
            using (var root = new TestRoot())
            {
                var result = CompilerRunner.Run(root.Paths, CompilerRunner.CommandValidate, false);

                Assert.True(result.Success);
                Assert.Equal(0, result.ErrorCount);
                Assert.False(Directory.Exists(root.GeneratedDir), "validate 不得创建 Mirror 目录");
                Assert.False(Directory.Exists(root.Paths.BuildStateDir), "validate 不得写 BuildState");
                Assert.All(result.Outputs, o => Assert.Equal("not-written", o.Action));
            }
        }
    }
}
