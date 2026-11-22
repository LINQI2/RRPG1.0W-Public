using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace RRPG.ConfigCompiler.Core
{
    /// <summary>
    /// 输出提交：Build Temp → Verify → Atomic Replace。
    ///
    /// 语义（Spike A 的安全原则）：
    ///   - 所有内容先写入 .build-tmp/，回读并校验字节与哈希；
    ///   - 逐文件提交：内容完全相同的文件 **不触碰**（保持 mtime，避免无意义 diff 与 Unity 重导入）；
    ///   - 内容变化 → 写 <name>.tmp → File.Move(overwrite) 原子替换；
    ///   - Generated/ 由编译器独占：本次未产出的旧文件 → 删除（记录为 deleted）；
    ///   - Hard Error 路径使用 CommitReportOnly：只写报告，绝不触碰任何配置输出（正式输出 0 更新）。
    ///
    /// 已知限制（如实记录）：Windows 上目录级替换不是真正的原子操作，本实现采用
    /// 「逐文件先写临时文件再 Move」+ 失败即中止，属于务实近似，不是事务文件系统保证。
    /// </summary>
    public sealed class OutputCommitter
    {
        public const string ReportFileName = "BuildReport.json";
        public const string ReportMarkdownName = "BuildReport.md";

        private static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(false);

        private readonly string _generatedDir;
        private readonly string _tempDir;
        private readonly string _rootLabel;

        public OutputCommitter(string generatedDir, string tempDir, string rootLabel = "")
        {
            _generatedDir = generatedDir;
            _tempDir = tempDir;
            _rootLabel = rootLabel;
        }

        /// <summary>正式 build：提交全部输出 + 清理 stale。</summary>
        public List<CommittedFile> CommitAll(IDictionary<string, string> files)
        {
            var results = CommitSome(files);
            var keep = new HashSet<string>(files.Keys, StringComparer.Ordinal);
            results.AddRange(DeleteStale(keep));
            return results.OrderBy(r => r.Name, StringComparer.Ordinal).ToList();
        }

        /// <summary>
        /// 提交给定文件（Build Temp → Verify → 逐文件原子替换），不做 stale 清理。
        /// 用于「先提交配置输出 → 用真实动作生成报告 → 再提交报告」两段式。
        /// </summary>
        public List<CommittedFile> CommitSome(IDictionary<string, string> files)
        {
            PrepareTempDir(files);

            var results = new List<CommittedFile>();
            Directory.CreateDirectory(_generatedDir);

            foreach (var name in files.Keys.OrderBy(x => x, StringComparer.Ordinal))
            {
                results.Add(CommitOne(name, files[name]));
            }

            return results.OrderBy(r => r.Name, StringComparer.Ordinal).ToList();
        }

        /// <summary>删除 Generated/ 中不在 keep 集合里的旧文件（Generated 由编译器独占）。</summary>
        public List<CommittedFile> DeleteStale(ISet<string> keep)
        {
            var results = new List<CommittedFile>();
            if (!Directory.Exists(_generatedDir)) return results;

            foreach (var existing in Directory.EnumerateFiles(_generatedDir, "*", SearchOption.TopDirectoryOnly))
            {
                string name = Path.GetFileName(existing);
                if (name.EndsWith(".tmp", StringComparison.Ordinal)) continue;
                if (keep.Contains(name)) continue;

                File.Delete(existing);
                results.Add(new CommittedFile
                {
                    Root = _rootLabel,
                    Name = name,
                    Action = "deleted",
                    Bytes = 0,
                    Sha256 = string.Empty
                });
            }

            return results;
        }

        // ---------------- 内部 ----------------

        private void PrepareTempDir(IDictionary<string, string> files)
        {
            if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, true);
            Directory.CreateDirectory(_tempDir);

            foreach (var kv in files)
            {
                string tempPath = Path.Combine(_tempDir, kv.Key);
                File.WriteAllText(tempPath, kv.Value, Utf8NoBom);
            }

            // Verify：回读临时产物，确认字节与哈希可复现
            foreach (var kv in files)
            {
                string tempPath = Path.Combine(_tempDir, kv.Key);
                string readBack = File.ReadAllText(tempPath, Utf8NoBom);
                if (!string.Equals(readBack, kv.Value, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Temp 输出回读不一致：" + kv.Key);
                }
            }
        }

        private CommittedFile CommitOne(string name, string content)
        {
            byte[] bytes = Utf8NoBom.GetBytes(content);
            string target = Path.Combine(_generatedDir, name);
            string hash = Sha256(bytes);

            if (File.Exists(target))
            {
                byte[] existing = File.ReadAllBytes(target);
                if (BytesEqual(existing, bytes))
                {
                    return new CommittedFile
                    {
                        Root = _rootLabel, Name = name, Action = "unchanged", Bytes = bytes.Length, Sha256 = hash
                    };
                }

                string tmp = target + ".tmp";
                File.WriteAllBytes(tmp, bytes);
                File.Move(tmp, target, true);
                return new CommittedFile
                {
                    Root = _rootLabel, Name = name, Action = "updated", Bytes = bytes.Length, Sha256 = hash
                };
            }

            string tmpNew = target + ".tmp";
            File.WriteAllBytes(tmpNew, bytes);
            File.Move(tmpNew, target, true);
            return new CommittedFile
            {
                Root = _rootLabel, Name = name, Action = "created", Bytes = bytes.Length, Sha256 = hash
            };
        }

        private static bool BytesEqual(byte[] a, byte[] b)
        {
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
            return true;
        }

        public static string Sha256(byte[] bytes)
        {
            using (var sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(bytes);
                var sb = new StringBuilder(hash.Length * 2);
                foreach (byte b in hash) sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }
    }
}
