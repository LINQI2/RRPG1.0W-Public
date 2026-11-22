using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace RRPG.ConfigCompiler.Core
{
    /// <summary>一个被发现的 Workbook（路径全部为仓库 / 扫描根相对路径）。</summary>
    public sealed class DiscoveredWorkbook
    {
        /// <summary>扫描根标签（Production V0.1 固定为 Config）。</summary>
        public string RootLabel;

        /// <summary>相对扫描根的文件路径（正斜杠）。</summary>
        public string RelativePath;

        /// <summary>绝对路径（仅本地读取用；不进入生成物）。</summary>
        public string FullPath;

        /// <summary>报告与 IR 中使用的稳定标识。</summary>
        public string DisplayPath { get { return RootLabel + "/" + RelativePath; } }
    }

    /// <summary>
    /// Workbook 发现：扫描配置的根目录，确定性排序，跳过 Excel 临时文件与隐藏文件。
    /// </summary>
    public static class WorkbookDiscovery
    {
        private const string XlsxExtension = ".xlsx";

        public static List<DiscoveredWorkbook> Discover(
            IEnumerable<KeyValuePair<string, string>> roots, DiagnosticBag bag)
        {
            var found = new List<DiscoveredWorkbook>();

            foreach (var root in roots)
            {
                string label = root.Key;
                string dir = root.Value;

                if (!Directory.Exists(dir))
                {
                    // 目录不存在是合法状态（M0 尚无已迁移领域），不产生错误。
                    continue;
                }

                foreach (var file in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
                {
                    string name = Path.GetFileName(file);

                    // Excel 打开工作簿时的锁文件（~$xxx.xlsx）：正常现象，跳过并记录。
                    if (name.StartsWith("~$", StringComparison.Ordinal))
                    {
                        bag.Warn(DiagnosticCodes.WbTempFileSkipped,
                            "跳过 Excel 临时锁文件：" + CompilerPaths.ToForwardSlash(file));
                        continue;
                    }

                    if (name.StartsWith(".", StringComparison.Ordinal)
                        || name.StartsWith("_", StringComparison.Ordinal))
                    {
                        continue; // 隐藏 / 显式忽略文件，静默跳过
                    }

                    if (!name.EndsWith(XlsxExtension, StringComparison.OrdinalIgnoreCase))
                    {
                        continue; // 只处理 .xlsx（CSV / 临时后缀不进本轮）
                    }

                    // 跳过隐藏目录（例如 .build-tmp）
                    string relToRoot = CompilerPaths.RelativeTo(dir, file);
                    if (relToRoot.Split('/').Any(seg => seg.StartsWith(".", StringComparison.Ordinal)))
                    {
                        continue;
                    }

                    found.Add(new DiscoveredWorkbook
                    {
                        RootLabel = label,
                        RelativePath = relToRoot,
                        FullPath = Path.GetFullPath(file)
                    });
                }
            }

            var ordered = found
                .OrderBy(w => w.DisplayPath, StringComparer.Ordinal)
                .ToList();

            if (ordered.Count == 0)
            {
                // M0（FDS §3.2 / §7）：还没有任何已迁移领域是**合法状态**，
                // 因此只记录 Warning（不阻断），由 CLI 打印明确的制作人状态。
                bag.Warn(DiagnosticCodes.WbNoWorkbookFound,
                    "未发现任何 .xlsx：当前尚无已迁移配置领域（M0 合法状态）。");
            }

            return ordered;
        }
    }
}
