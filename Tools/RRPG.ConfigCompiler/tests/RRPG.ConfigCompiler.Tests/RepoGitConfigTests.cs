using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RRPG.ConfigCompiler.Core;
using Xunit;

namespace RRPG.ConfigCompiler.Tests
{
    /// <summary>
    /// 仓库级 Git 配置契约（T1.1）。
    ///
    /// 这些断言保护的是"证据持久化能力"本身：
    ///   - 工具工程文件必须能入库（否则仓库里的工具缺件、不可构建）；
    ///   - XLSX 必须被当作二进制（不参与文本 normalization）；
    ///   - Spike Canonical JSON 必须显式 LF（跨 checkout 逐字节稳定）。
    ///
    /// 本测试只读仓库根的两个配置文件（不执行 git 命令、不修改任何文件）；
    /// 真实生效结果由 Evidence/GitIgnoreVerification.txt 与 GitAttributesVerification.txt 记录。
    /// </summary>
    public class RepoGitConfigTests
    {
        private static string RepoRoot()
        {
            return CompilerPaths.FindRepoRoot();
        }

        private static List<string> ReadEffectiveLines(string fileName)
        {
            string path = Path.Combine(RepoRoot(), fileName);
            Assert.True(File.Exists(path), "仓库根缺少文件：" + fileName);
            return File.ReadAllLines(path)
                .Select(l => l.Trim())
                .ToList();
        }

        [Fact]
        public void GitIgnore_KeepsGlobalCsprojIgnore_ButReIncludesToolsProjects()
        {
            var lines = ReadEffectiveLines(".gitignore");

            int globalIndex = lines.FindIndex(l => l == "*.csproj");
            int negationIndex = lines.FindIndex(l => l == "!Tools/**/*.csproj");

            Assert.True(globalIndex >= 0, ".gitignore 必须保留全局 *.csproj ignore（Unity/IDE 自动生成工程）");
            Assert.True(negationIndex >= 0, ".gitignore 必须包含 !Tools/**/*.csproj（仓库拥有的工具工程要入库）");
            Assert.True(negationIndex > globalIndex, "例外规则必须写在 *.csproj 之后");
        }

        [Fact]
        public void GitAttributes_MarksXlsxAsBinary()
        {
            var lines = ReadEffectiveLines(".gitattributes");
            Assert.Contains("*.xlsx binary", lines);
        }

        [Fact]
        public void GitAttributes_ForcesLfForProductionCanonicalMirror()
        {
            var lines = ReadEffectiveLines(".gitattributes");
            Assert.Contains("Development/Generated/StaticConfig/*.json text eol=lf", lines);
        }

        [Fact]
        public void GitAttributes_DoesNotChangeOtherJsonBehaviour()
        {
            var lines = ReadEffectiveLines(".gitattributes");

            // 全仓 JSON 仍保持既有规则（只有 *.json text，不含 eol），本轮不扩大改动面
            Assert.Contains("*.json text", lines);
            Assert.DoesNotContain("*.json text eol=lf", lines);
        }

        [Fact]
        public void GitIgnore_ExcludesGeneratedUnityArtifacts_ButKeepsCanonicalMirror()
        {
            var lines = ReadEffectiveLines(".gitignore");

            // Generated Unity Runtime Artifacts（不入 Git；FDS §8.2）——精确路径
            Assert.Contains("UnityProject/RRPG1.0game/Assets/Generated/StaticConfig/**", lines);

            // Compiler 本地构建状态（不是正式配置产物）
            Assert.Contains("Tools/RRPG.ConfigCompiler/.build-state/", lines);
            Assert.Contains("Tools/RRPG.ConfigCompiler/.build-tmp/", lines);

            // Canonical Mirror 必须可被 Git 追踪（不得被任何规则忽略）
            Assert.DoesNotContain(lines, l => l == "Development/Generated/"
                || l == "Development/Generated/**"
                || l == "Development/Generated/StaticConfig/**"
                || l == "Development/Generated/StaticConfig/");
        }
    }
}
