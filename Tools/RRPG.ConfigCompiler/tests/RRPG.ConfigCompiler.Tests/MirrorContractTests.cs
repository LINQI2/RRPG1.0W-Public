using System;
using System.IO;
using System.Linq;
using System.Text;
using RRPG.ConfigCompiler.Core;
using Xunit;

namespace RRPG.ConfigCompiler.Tests
{
    /// <summary>
    /// Canonical Mirror 契约（FDS §7.2 R1–R7 / 用户 M0 §7）：
    ///   - 首部 DO NOT EDIT 标识 + 输入指纹；
    ///   - UTF-8 无 BOM、LF、无尾随空白；
    ///   - 相同输入 → 逐字节相同（确定性）+ 第二次 build = unchanged；
    ///   - validate 只读（不写任何文件）；
    ///   - mirror-stale / mirror-missing / mirror-extra 可工作；
    ///   - Hard Error → Mirror 0 更新。
    /// </summary>
    public class MirrorContractTests
    {
        [Fact]
        public void Mirror_Header_HasDoNotEditAndFingerprint_AndIsUtf8NoBomWithLf()
        {
            using (var root = new TestRoot())
            {
                root.WriteWorkbook("RRPG_Sample.xlsx", SampleSheets.Sample());
                var build = CompilerRunner.Run(root.Paths, CompilerRunner.CommandBuild, true);
                Assert.True(build.Success, string.Join(" | ", build.Diagnostics.Select(d => d.ToString())));

                byte[] bytes = File.ReadAllBytes(root.GeneratedFile("Sample.json"));

                Assert.False(bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF,
                    "Mirror 必须是 UTF-8 无 BOM");
                Assert.DoesNotContain((byte)0x0D, bytes);   // LF only

                string text = Encoding.UTF8.GetString(bytes);
                Assert.Contains("\"_generated\"", text);
                Assert.Contains("\"doNotEdit\": true", text);
                Assert.Contains("\"tool\": \"RRPG.ConfigCompiler\"", text);
                Assert.Contains("\"sourceSha256\": \"", text);

                Assert.DoesNotContain("\t", text);
                foreach (string line in text.Split('\n'))
                {
                    Assert.Equal(line.TrimEnd(), line);
                }
            }
        }

        [Fact]
        public void Rebuild_SameInput_IsByteIdentical_AndReportsUnchanged()
        {
            using (var root = new TestRoot())
            {
                root.WriteWorkbook("RRPG_Sample.xlsx", SampleSheets.Sample());

                var first = CompilerRunner.Run(root.Paths, CompilerRunner.CommandBuild, true);
                Assert.True(first.Success);
                byte[] before = File.ReadAllBytes(root.GeneratedFile("Sample.json"));
                var mtimeBefore = File.GetLastWriteTimeUtc(root.GeneratedFile("Sample.json"));

                var second = CompilerRunner.Run(root.Paths, CompilerRunner.CommandBuild, true);
                Assert.True(second.Success);

                Assert.Equal(before, File.ReadAllBytes(root.GeneratedFile("Sample.json")));
                Assert.Equal(mtimeBefore, File.GetLastWriteTimeUtc(root.GeneratedFile("Sample.json")));
                Assert.Equal("unchanged", second.Outputs.Single(o => o.Name == "Sample.json").Action);
            }
        }

        [Fact]
        public void TamperedMirror_IsReportedAsStale_AndValidateDoesNotRepairIt()
        {
            using (var root = new TestRoot())
            {
                root.WriteWorkbook("RRPG_Sample.xlsx", SampleSheets.Sample());
                Assert.True(CompilerRunner.Run(root.Paths, CompilerRunner.CommandBuild, true).Success);

                string mirror = root.GeneratedFile("Sample.json");
                File.WriteAllText(mirror, File.ReadAllText(mirror).Replace("\"Amount\": 20", "\"Amount\": 999"), new UTF8Encoding(false));

                var result = CompilerRunner.Run(root.Paths, CompilerRunner.CommandValidate, false);

                Assert.False(result.Success);
                Assert.Contains(DiagnosticCodes.MirrorStale, result.Diagnostics.Select(d => d.Code));
                Assert.Contains("999", File.ReadAllText(mirror));   // validate 不得修 Mirror
            }
        }

        [Fact]
        public void DeletedMirrorFile_IsReportedAsMissing()
        {
            using (var root = new TestRoot())
            {
                root.WriteWorkbook("RRPG_Sample.xlsx", SampleSheets.Sample());
                Assert.True(CompilerRunner.Run(root.Paths, CompilerRunner.CommandBuild, true).Success);

                File.Delete(root.GeneratedFile("Sample.json"));

                var result = CompilerRunner.Run(root.Paths, CompilerRunner.CommandValidate, false);
                Assert.False(result.Success);
                Assert.Contains(DiagnosticCodes.MirrorMissing, result.Diagnostics.Select(d => d.Code));
            }
        }

        [Fact]
        public void ExtraJsonInMirrorRoot_IsReportedAsExtra_ButGovernanceReadmeIsAllowed()
        {
            using (var root = new TestRoot())
            {
                root.WriteWorkbook("RRPG_Sample.xlsx", SampleSheets.Sample());
                Assert.True(CompilerRunner.Run(root.Paths, CompilerRunner.CommandBuild, true).Success);

                // 治理文件（README）不参与比较
                File.WriteAllText(Path.Combine(root.GeneratedDir, "README.md"), "Mirror governance", new UTF8Encoding(false));
                Assert.True(CompilerRunner.Run(root.Paths, CompilerRunner.CommandValidate, false).Success);

                // 但手工塞进来的 JSON 必须被报出来（该目录由 Compiler 独占）
                File.WriteAllText(root.GeneratedFile("Extra.json"), "{}", new UTF8Encoding(false));

                var result = CompilerRunner.Run(root.Paths, CompilerRunner.CommandValidate, false);
                Assert.False(result.Success);
                Assert.Contains(DiagnosticCodes.MirrorExtra, result.Diagnostics.Select(d => d.Code));
            }
        }

        [Fact]
        public void ChangingAuthoringValue_MakesMirrorStale()
        {
            using (var root = new TestRoot())
            {
                root.WriteWorkbook("RRPG_Sample.xlsx", SampleSheets.Sample());
                Assert.True(CompilerRunner.Run(root.Paths, CompilerRunner.CommandBuild, true).Success);

                // 只改一个业务值（不重新 build）→ validate 必须报 mirror-stale
                root.WriteWorkbook("RRPG_Sample.xlsx", new TestSheet("Sample")
                    .Col("SampleId", "id", "", "样例ID")
                    .Col("Title", "string", "", "标题")
                    .Col("Amount", "int", ">=0", "数量")
                    .Row("sample_001", "甲", 24)
                    .Row("sample_002", "乙", 15)
                    .Row("sample_003", "丙", 24));

                var result = CompilerRunner.Run(root.Paths, CompilerRunner.CommandValidate, false);
                Assert.False(result.Success);
                Assert.Contains(DiagnosticCodes.MirrorStale, result.Diagnostics.Select(d => d.Code));
            }
        }

        [Fact]
        public void HardError_LeavesMirrorUntouched()
        {
            using (var root = new TestRoot())
            {
                root.WriteWorkbook("RRPG_Sample.xlsx", SampleSheets.Sample());
                Assert.True(CompilerRunner.Run(root.Paths, CompilerRunner.CommandBuild, true).Success);

                string mirror = root.GeneratedFile("Sample.json");
                byte[] before = File.ReadAllBytes(mirror);
                var mtimeBefore = File.GetLastWriteTimeUtc(mirror);

                // 制造 Hard Error（int 列填了非数字）
                root.WriteWorkbook("RRPG_Sample.xlsx", new TestSheet("Sample")
                    .Col("SampleId", "id", "", "样例ID")
                    .Col("Amount", "int", ">=0", "数量")
                    .Row("sample_001", "不是数字"));

                var failed = CompilerRunner.Run(root.Paths, CompilerRunner.CommandBuild, true);
                Assert.False(failed.Success);

                Assert.Equal(before, File.ReadAllBytes(mirror));
                Assert.Equal(mtimeBefore, File.GetLastWriteTimeUtc(mirror));
                Assert.All(failed.Outputs, o => Assert.True(
                    o.Name == OutputCommitter.ReportFileName || o.Name == OutputCommitter.ReportMarkdownName,
                    "Hard Error 时不得写其它输出：" + o.Name));
            }
        }
    }
}
