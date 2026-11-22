using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using RRPG.ConfigCompiler.Core;
using RRPG.ConfigCompiler.Xlsx;

namespace RRPG.ConfigCompiler
{
    /// <summary>
    /// rrpg-config CLI（Production V0.1；FDS §4.1 / §5.2）。
    ///
    /// 正式入口（制作人 / AI）：
    ///   rrpg-config validate   只读检查（不写任何文件；含 Canonical Mirror 比对，FDS §7.2 R4/R5）
    ///   rrpg-config build      校验通过才写 Canonical Mirror（Hard Error → Mirror 0 更新）
    ///
    /// 诊断入口：
    ///   rrpg-config package-check &lt;file.xlsx&gt;   单文件 Open XML 结构校验
    ///
    /// 退出码：0 = 成功；1 = Hard Error；2 = 用法错误；3 = 环境 / 未知错误。
    /// 网络边界（FDS §5.2）：正常 validate / build **不访问网络**；
    /// 只有首次 dotnet restore 在 NuGet 缓存缺失时可能访问网络。
    ///
    /// M0 说明：还没有任何已迁移领域（Development/Config 下无 .xlsx）是**合法状态**，
    /// validate / build 都会给出明确的制作人状态并且退出码为 0。
    /// </summary>
    public static class Program
    {
        private const int ExitOk = 0;
        private const int ExitHardError = 1;
        private const int ExitUsage = 2;
        private const int ExitEnvironment = 3;

        public static int Main(string[] args)
        {
            // 全进程 culture 兜底；真正的确定性来自所有解析 / 格式化显式使用 InvariantCulture。
            CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;

            try { Console.OutputEncoding = new UTF8Encoding(false); }
            catch { /* 某些宿主不允许改编码：忽略，报告文件仍是 UTF-8 */ }

            try
            {
                return Run(args);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("环境错误：" + ex.GetType().Name + ": " + ex.Message);
                return ExitEnvironment;
            }
        }

        private static int Run(string[] args)
        {
            if (args.Length == 0 || IsHelp(args[0]))
            {
                PrintHelp();
                return args.Length == 0 ? ExitUsage : ExitOk;
            }

            string verb = args[0].ToLowerInvariant();
            string root = ReadOption(args, "--root");

            switch (verb)
            {
                case "validate":
                    return RunCompile(CompilerRunner.CommandValidate, root, false);

                case "build":
                    return RunCompile(CompilerRunner.CommandBuild, root, true);

                case "package-check":
                    return RunPackageCheck(args);

                default:
                    Console.Error.WriteLine("未知命令：" + verb);
                    PrintHelp();
                    return ExitUsage;
            }
        }

        private static int RunCompile(string command, string root, bool writeOutputs)
        {
            var paths = CompilerPaths.Resolve(root);

            Console.WriteLine("RRPG ConfigCompiler（Production V0.1 · M2 + M3 Slice 1A）— " + command);
            Console.WriteLine("  repoRoot   : " + paths.RepoRoot);
            Console.WriteLine("  authoring  : " + CompilerPaths.ToForwardSlash(paths.AuthoringDir));
            Console.WriteLine("  mirror     : " + paths.GeneratedDirRelative);
            Console.WriteLine("  sourceArt  : " + CompilerPaths.ToForwardSlash(paths.SourceAssetsDir));
            Console.WriteLine();

            var result = CompilerRunner.Run(paths, command, writeOutputs);

            foreach (var wb in result.Workbooks)
            {
                Console.WriteLine("workbook : " + wb.Path + "   openXml=" + wb.PackageValidation
                    + (wb.PackageIssueCount > 0 ? "(" + wb.PackageIssueCount + " issue)" : string.Empty));

                foreach (var sheet in wb.Sheets)
                {
                    var line = new StringBuilder("  sheet ").Append(sheet.Name).Append(" [").Append(sheet.Kind).Append("]");
                    if (sheet.RowCount > 0) line.Append(" rows=").Append(sheet.RowCount);
                    if (sheet.ColumnCount > 0) line.Append(" cols=").Append(sheet.ColumnCount);
                    if (sheet.Key != null && sheet.Key.Count > 0) line.Append(" key=").Append(string.Join("+", sheet.Key));
                    if (!string.IsNullOrEmpty(sheet.SkipReason)) line.Append(" reason=").Append(sheet.SkipReason);
                    Console.WriteLine(line.ToString());
                }
            }

            if (result.Diagnostics.Count > 0)
            {
                Console.WriteLine();
                Console.WriteLine("诊断（" + result.Diagnostics.Count + "）：");
                foreach (var d in result.Diagnostics) Console.WriteLine("  " + d);
            }

            if (result.Outputs.Count > 0)
            {
                Console.WriteLine();
                Console.WriteLine("输出动作：");
                foreach (var o in result.Outputs)
                {
                    Console.WriteLine("  " + o.Action.PadRight(10) + " " + o.Root + "/" + o.Name + "  bytes=" + o.Bytes);
                }
            }

            if (result.Workbooks.Count == 0)
            {
                Console.WriteLine();
                Console.WriteLine("Production Toolchain Ready");
                Console.WriteLine("当前尚无已迁移配置领域。");
                Console.WriteLine("把制作人工作簿（.xlsx）放进 Development/Config/ 后再次运行本次命令。");
            }
            else
            {
                // M2（Monster Definition Authority）：XLSX 已是正式人工 Authority；务必让制作人看到
                // 「改 XLSX 才生效」与「Monster.csv 已冻结」，避免继续改 Legacy CSV。
                Console.WriteLine();
                Console.WriteLine("M2 AUTHORITY：本目录的 XLSX = Current Monster Definition Authoring Authority（内容变更只改这里）。");
                Console.WriteLine("Frozen Legacy：Development/Excel/Monster.csv = Frozen Legacy Source"
                    + "（不再被读取；CSV → XLSX 同步已关闭）。");

                // M3 Slice 1A（Enemy Presentation）：视觉事实只登记在 XLSX；源图放在 SourceAssets，
                // 内容哈希进 Canonical Mirror（换图 = 必须重新 build），不得手改 Mirror 或 Mirror 之外的 JSON。
                Console.WriteLine("M3 AUTHORITY：RRPG_EnemyPresentation.xlsx = Enemy Presentation Authoring Authority"
                    + "（只写 MonsterId → VisualAssetId）；共享视觉资源 Authority 已独立到 "
                    + "RRPG_VisualAssets.xlsx（VisualAsset 表，唯一一份）；源图只放 Development/SourceAssets，"
                    + "AssetManifest.json 记录源图内容哈希（换图 = 必须重新 build）。");
                Console.WriteLine("M3 边界：Compiler 侧接线（Mirror）已冻结；Sprite 生成由 Unity Builder 负责（Slice 1B）；"
                    + "Runtime Resolver 已就位（Slice 1C）；战斗表现接线自 Slice 1D 起。");

                // WP-Event-1A（统一事件内容基础）：事件内容只登记在 RRPG_Events.xlsx；本体只做静态读取，不接产品。
                Console.WriteLine("EVENT AUTHORITY：RRPG_Events.xlsx = Event Content Authoring Authority"
                    + "（Event / EventTrigger / EventRole / EventRoleCondition / EventCondition / EventPresentation /"
                    + " EventOption / EventOptionCondition / EventOutcome / EventEffect）。");
                Console.WriteLine("EVENT 边界（WP-Event-1A）：本轮只建立静态内容 + 只读读取入口；"
                    + "不求解事件、不绑定人物、不判断条件、不执行效果（属后续 1B / 1C / 1D）。");
            }

            Console.WriteLine();
            Console.WriteLine("结果：" + (result.Success ? "PASS" : "FAIL")
                + "   errors=" + result.ErrorCount + "   warnings=" + result.WarningCount);

            if (writeOutputs)
            {
                Console.WriteLine("报告：" + CompilerPaths.ToForwardSlash(Path.Combine(paths.BuildStateDir, OutputCommitter.ReportMarkdownName))
                    + "（本地状态，不进 Git）");
            }
            else
            {
                Console.WriteLine("Mirror 未写入（validate 只读；FDS §7.2 R5）。");
            }

            return result.Success ? ExitOk : ExitHardError;
        }

        private static int RunPackageCheck(string[] args)
        {
            if (args.Length < 2)
            {
                Console.Error.WriteLine("用法：rrpg-config package-check <path-to-xlsx>");
                return ExitUsage;
            }

            string path = Path.GetFullPath(args[1]);
            if (!File.Exists(path))
            {
                Console.Error.WriteLine("文件不存在：" + path);
                return ExitUsage;
            }

            var issues = OpenXmlPackageChecker.Validate(path);
            Console.WriteLine("Open XML 结构校验：" + path);
            if (issues.Count == 0)
            {
                Console.WriteLine("  VALID（0 issue）");
                return ExitOk;
            }

            Console.WriteLine("  INVALID（" + issues.Count + " issue）");
            foreach (var issue in issues.Take(40)) Console.WriteLine("    - " + issue);
            return ExitHardError;
        }

        private static bool IsHelp(string arg)
        {
            return arg == "-h" || arg == "--help" || arg == "help" || arg == "/?";
        }

        private static string ReadOption(string[] args, string name)
        {
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase)) return args[i + 1];
            }
            return null;
        }

        private static void PrintHelp()
        {
            Console.WriteLine("RRPG ConfigCompiler（Production V0.1 · M2）");
            Console.WriteLine();
            Console.WriteLine("Authority：Development/Config/*.xlsx = 人工 Authority（内容变更只改这里）；");
            Console.WriteLine("           Development/Excel/Monster.csv = Frozen Legacy Source（M2 起不再读取）。");
            Console.WriteLine();
            Console.WriteLine("  rrpg-config validate [--root <repoRoot>]   只读校验（不写任何文件；含 Mirror 比对）");
            Console.WriteLine("  rrpg-config build    [--root <repoRoot>]   校验通过才写 Canonical Mirror");
            Console.WriteLine("  rrpg-config package-check <file.xlsx>      单文件 Open XML 结构校验");
            Console.WriteLine();
            Console.WriteLine("默认 repoRoot = 从当前目录向上查找（同时含 Development 与 UnityProject 的目录）。");
            Console.WriteLine("人工 Authoring 根 = Development/Config（XLSX 是唯一人工 Authority）。");
            Console.WriteLine("Canonical Mirror  = Development/Generated/StaticConfig（DO NOT EDIT，进 Git）。");
            Console.WriteLine("正常 validate / build 不访问网络；首次 dotnet restore 可能需要 NuGet 网络。");
        }
    }
}
