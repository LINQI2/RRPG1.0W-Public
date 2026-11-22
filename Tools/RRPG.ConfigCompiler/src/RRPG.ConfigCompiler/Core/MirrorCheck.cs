using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace RRPG.ConfigCompiler.Core
{
    /// <summary>
    /// Canonical Mirror 契约校验（FDS §7.2 R4 / R5）—— **只读，绝不写文件**。
    ///
    /// validate 阶段必须能证明 Authoring == Mirror：
    ///   - 重算 Canonical（不写盘），与磁盘 Mirror **逐字节**比较；
    ///   - Mirror 缺失 → MIR001；内容不一致（stale / 半更新 / 人工改动）→ MIR002；
    ///   - Mirror 目录出现本轮未产出的 .json → MIR003（Compiler 独占该目录）。
    ///
    /// 非 .json 文件（例如治理用 README.md）不参与比较。
    /// </summary>
    public static class MirrorCheck
    {
        private static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(false);

        public static void Verify(CompilerPaths paths, IDictionary<string, string> expected, DiagnosticBag bag)
        {
            string dir = paths.GeneratedDir;

            foreach (var kv in expected.OrderBy(k => k.Key, StringComparer.Ordinal))
            {
                string target = Path.Combine(dir, kv.Key);

                if (!File.Exists(target))
                {
                    bag.Error(DiagnosticCodes.MirrorMissing,
                        "Canonical Mirror 缺少文件 " + paths.GeneratedDirRelative + "/" + kv.Key
                        + "：请运行 build 重新生成（Mirror 是生成物，不得手工创建）。");
                    continue;
                }

                byte[] disk = File.ReadAllBytes(target);
                byte[] want = Utf8NoBom.GetBytes(kv.Value);

                if (!BytesEqual(disk, want))
                {
                    bag.Error(DiagnosticCodes.MirrorStale,
                        "Canonical Mirror 与当前 Authoring / SourceAssets 不一致（mirror-stale）："
                        + paths.GeneratedDirRelative + "/" + kv.Key
                        + " 磁盘 sha256=" + OutputCommitter.Sha256(disk)
                        + " 期望 sha256=" + OutputCommitter.Sha256(want)
                        + "。请运行 build（Mirror 不得人工编辑）。");
                }
            }

            if (!Directory.Exists(dir)) return;

            foreach (var file in Directory.EnumerateFiles(dir, "*.json", SearchOption.TopDirectoryOnly))
            {
                string name = Path.GetFileName(file);
                if (expected.ContainsKey(name)) continue;

                bag.Error(DiagnosticCodes.MirrorExtra,
                    "Canonical Mirror 存在本轮未产出的文件 " + paths.GeneratedDirRelative + "/" + name
                    + "：该目录由 Compiler 独占，请运行 build 收敛（不得手工放入 JSON）。");
            }
        }

        private static bool BytesEqual(byte[] a, byte[] b)
        {
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
            return true;
        }
    }
}
