using System;
using System.IO;

namespace RRPG.ConfigCompiler.Core
{
    /// <summary>
    /// 正式路径解析（FDS §3.1 / §6.1 Production V0.1）。
    ///
    /// 硬约束（确定性）：所有生成产物只允许出现 **仓库相对路径**，
    /// 绝不写入机器绝对路径（否则换机器 / 复制仓库会产生无意义 diff）。
    /// Report / Temp / BuildState 属于本地执行状态，**不进入** Canonical Mirror。
    /// </summary>
    public sealed class CompilerPaths
    {
        private const string AuthoringRelative = "Development/Config";
        private const string SourceAssetsRelative = "Development/SourceAssets";
        private const string MirrorRelative = "Development/Generated/StaticConfig";
        private const string CompilerRelative = "Tools/RRPG.ConfigCompiler";

        /// <summary>仓库根（绝对路径；仅本地使用，不进入任何生成物）。</summary>
        public string RepoRoot { get; private set; }

        /// <summary>Compiler 工程根（绝对路径）：Tools/RRPG.ConfigCompiler。</summary>
        public string CompilerRoot { get; private set; }

        /// <summary>仓库相对 Mirror 根（正斜杠）：Development/Generated/StaticConfig。</summary>
        public string GeneratedDirRelative { get; private set; }

        /// <summary>人工 Authoring 根（XLSX）：Development/Config。</summary>
        public string AuthoringDir { get { return Path.Combine(RepoRoot, "Development", "Config"); } }

        /// <summary>源资产根（美术 / 音频等）：Development/SourceAssets。Builder 的输入，不进 Unity Assets/。</summary>
        public string SourceAssetsDir { get { return Path.Combine(RepoRoot, "Development", "SourceAssets"); } }

        /// <summary>
        /// Canonical Mirror 输出目录（Git tracked、确定性、可重建；FDS §7）。
        /// 只有 Compiler 写它；人工不得编辑（DO NOT EDIT）。
        /// </summary>
        public string GeneratedDir { get { return Path.Combine(RepoRoot, "Development", "Generated", "StaticConfig"); } }

        /// <summary>最近一次执行状态目录（BuildReport；不是正式配置产物，本地忽略）。</summary>
        public string BuildStateDir { get { return Path.Combine(CompilerRoot, ".build-state"); } }

        /// <summary>Build Temp 目录（原子替换前的临时产物；本地忽略）。</summary>
        public string TempDir { get { return Path.Combine(CompilerRoot, ".build-tmp"); } }

        private CompilerPaths() { }

        /// <summary>
        /// 解析路径。<paramref name="explicitRepoRoot"/> 为 null 时从当前目录向上查找仓库根。
        /// </summary>
        public static CompilerPaths Resolve(string explicitRepoRoot = null)
        {
            string repoRoot = string.IsNullOrEmpty(explicitRepoRoot)
                ? FindRepoRoot()
                : Path.GetFullPath(explicitRepoRoot);

            return new CompilerPaths
            {
                RepoRoot = repoRoot,
                CompilerRoot = Path.Combine(repoRoot, "Tools", "RRPG.ConfigCompiler"),
                GeneratedDirRelative = MirrorRelative
            };
        }

        /// <summary>从当前目录向上查找仓库根（同时含 Development 与 UnityProject 两个目录）。</summary>
        public static string FindRepoRoot()
        {
            var dir = new DirectoryInfo(Environment.CurrentDirectory);
            while (dir != null)
            {
                if (Directory.Exists(Path.Combine(dir.FullName, "Development"))
                    && Directory.Exists(Path.Combine(dir.FullName, "UnityProject")))
                {
                    return dir.FullName;
                }
                dir = dir.Parent;
            }
            return Environment.CurrentDirectory;
        }

        /// <summary>计算 baseDir 下的相对路径；不在其下时回落为目录名（仍不泄露绝对路径）。</summary>
        public static string RelativeTo(string baseDir, string path)
        {
            string full = Path.GetFullPath(path);
            string b = Path.GetFullPath(baseDir);
            if (!b.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal))
                b += Path.DirectorySeparatorChar;

            if (full.StartsWith(b, StringComparison.OrdinalIgnoreCase))
                return ToForwardSlash(full.Substring(b.Length));
            return ToForwardSlash(Path.GetFileName(full));
        }

        public static string ToForwardSlash(string path)
        {
            return (path ?? string.Empty).Replace('\\', '/');
        }

        /// <summary>诊断用：把绝对路径变成稳定的仓库相对路径（绝不泄露机器路径）。</summary>
        public string Describe(string absolutePath)
        {
            return RelativeTo(RepoRoot, absolutePath);
        }
    }
}
