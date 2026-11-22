using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using RRPG.ConfigCompiler.Core;

namespace RRPG.ConfigCompiler.M1ShadowSync
{
    /// <summary>
    /// M1 迁移专用入口（MIGRATION ONLY · M1 CLOSED）。
    ///
    /// **M2 Monster Definition Authority Cutover 之后本工具已冻结 sync 写入口**（决策 D-M2-2 / FDS §10.6）：
    ///   - `sync`      **拒绝执行**（非 0 退出）：CSV → XLSX 同步不再被允许，防止 Frozen CSV 覆盖新 Authority；
    ///   - `equivalence` 只读等价（A: CSV↔XLSX、B: XLSX↔Mirror）；保留为迁移证据查询；
    ///   - `status`      只读打印 Shadow 链状态。
    ///
    /// 退出码：0 成功；1 = **拒绝 / Hard Error**；2 用法错误；3 环境错误。本工具不访问网络。
    /// 工具**保留**（不删除）；M1 迁移期结束后再行退役，不属于 M4 物理清理清单。
    /// </summary>
    public static class Program
    {
        private const int ExitOk = 0;
        private const int ExitHardError = 1;
        private const int ExitUsage = 2;
        private const int ExitEnvironment = 3;

        public static int Main(string[] args)
        {
            try { Console.OutputEncoding = new UTF8Encoding(false); }
            catch { }

            try
            {
                string verb = args.Length == 0 ? string.Empty : args[0].ToLowerInvariant();
                var paths = CompilerPaths.Resolve(ReadOption(args, "--root"));

                switch (verb)
                {
                    case "sync": return RunSync(paths);
                    case "equivalence": return RunEquivalence(paths);
                    case "status": return RunStatus(paths);
                    default:
                        PrintHelp();
                        return args.Length == 0 ? ExitUsage : ExitOk;
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("环境错误：" + ex.GetType().Name + ": " + ex.Message);
                return ExitEnvironment;
            }
        }

        private static string LegacyCsvPath(CompilerPaths paths)
        {
            return Path.Combine(paths.RepoRoot, "Development", "Excel", "Monster.csv");
        }

        /// <summary>
        /// **M2 FROZEN — M1 CLOSED**（D-M2-2 / FDS §10.4 / §10.6）：
        /// Monster Definition Authority 已转移到 `Development/Config/RRPG_Monsters.xlsx`，
        /// 因此本工具不再拥有任何产品内容写入权：`sync`（CSV → XLSX）**必须拒绝执行**。
        ///
        /// 保留 sync 动词是为了给出**明确拒绝**与可操作指引（而不是静默成功或静默无动作）。
        /// 原 M1 同步实现（MonsterCsv → ShadowWorkbookWriter）保留在仓库与 Git 历史中，不再有执行路径。
        /// </summary>
        private static int RunSync(CompilerPaths paths)
        {
            Console.Error.WriteLine("M1 ShadowSync is frozen after M2 Monster Definition Authority Cutover.");
            Console.Error.WriteLine("CSV -> XLSX synchronization is no longer allowed.");
            Console.Error.WriteLine();
            Console.Error.WriteLine("MIGRATION ONLY / M1 CLOSED");
            Console.Error.WriteLine("  frozen at : " + CompilerPaths.RelativeTo(paths.RepoRoot, LegacyCsvPath(paths))
                + "（Frozen Legacy Source，只读参考）");
            Console.Error.WriteLine("  authority : Development/Config/RRPG_Monsters.xlsx（Current Monster Definition Authoring Authority）");
            Console.Error.WriteLine("  下一步    : 内容变更请改 XLSX，然后运行 RRPG_一键导表.cmd 重建 Mirror / 生成根。");
            return ExitHardError;
        }

        private static int RunEquivalence(CompilerPaths paths)
        {
            Console.WriteLine("M1 EQUIVALENCE（A: CSV↔XLSX　B: XLSX↔Mirror）");

            var legacy = MonsterCsv.Read(LegacyCsvPath(paths));
            Console.WriteLine("[A] Legacy Monster.csv： " + legacy.Count + " 条（UTF-8 BOM 已处理）");

            var result = CompilerRunner.Run(paths, CompilerRunner.CommandValidate, false);

            var shadowSheet = result.CompiledSheets.FirstOrDefault(s => s.Spec.SheetName == "Monster");
            if (shadowSheet == null)
            {
                Console.Error.WriteLine("Hard Error：Shadow 链没有编译出 Monster Sheet。");
                PrintDiagnostics(result);
                return ExitHardError;
            }

            Console.WriteLine("[B] XLSX → Canonical validate = " + (result.Success ? "PASS" : "FAIL")
                + "（errors=" + result.ErrorCount + " warnings=" + result.WarningCount + "）");
            if (!result.Success)
            {
                PrintDiagnostics(result);
                return ExitHardError;
            }

            var shadowRows = shadowSheet.Rows
                .Select(r => r.Cells.ToDictionary(c => c.Field.Field, c => c.Value, StringComparer.Ordinal))
                .ToList();

            var failures = new List<string>();
            CompareCsvToShadow(legacy, shadowRows, failures);

            if (failures.Count > 0)
            {
                Console.Error.WriteLine("Hard Error：等价失败 " + failures.Count + " 项：");
                foreach (string f in failures) Console.Error.WriteLine("  - " + f);
                return ExitHardError;
            }

            Console.WriteLine("[B] Mirror 逐字节一致： validate 已证明 Authoring == Mirror（FDS §7.2 R5）");
            Console.WriteLine("等价结论：3 × 8 逐条逐字段一致（CSV ↔ XLSX ↔ Mirror）");
            Console.WriteLine("结果：PASS");
            return ExitOk;
        }

        /// <summary>逐条逐字段比较（ID 集合 / 条数 / 8 字段；Missing / Extra / Duplicate / 值不符 均为失败）。</summary>
        private static void CompareCsvToShadow(List<MonsterRow> legacy,
            List<Dictionary<string, object>> shadowRows, List<string> failures)
        {
            if (shadowRows.Count != legacy.Count)
            {
                failures.Add("条数不一致：CSV=" + legacy.Count + "，Shadow=" + shadowRows.Count);
            }

            var shadowById = new Dictionary<string, Dictionary<string, object>>(StringComparer.Ordinal);
            foreach (var row in shadowRows)
            {
                string id = Str(row, "MonsterId");
                if (shadowById.ContainsKey(id)) failures.Add("Shadow 出现重复 ID：" + id);
                else shadowById[id] = row;
            }

            foreach (var row in legacy)
            {
                Dictionary<string, object> shadow;
                if (!shadowById.TryGetValue(row.MonsterId, out shadow))
                {
                    failures.Add("Shadow 缺少 MonsterId：" + row.MonsterId);
                    continue;
                }

                Check(failures, row.MonsterId, "Name", row.Name, Str(shadow, "Name"));
                Check(failures, row.MonsterId, "MaxHP", row.MaxHP, Int(shadow, "MaxHP"));
                Check(failures, row.MonsterId, "Attack", row.Attack, Int(shadow, "Attack"));
                Check(failures, row.MonsterId, "AttackRange", row.AttackRange, Int(shadow, "AttackRange"));
                Check(failures, row.MonsterId, "AttackCount", row.AttackCount, Int(shadow, "AttackCount"));
                Check(failures, row.MonsterId, "ActionPoints", row.ActionPoints, Int(shadow, "ActionPoints"));
                Check(failures, row.MonsterId, "Faction", row.Faction, Str(shadow, "Faction"));
            }

            foreach (var id in shadowById.Keys)
            {
                if (!legacy.Any(r => r.MonsterId == id)) failures.Add("Shadow 多出 MonsterId：" + id);
            }
        }

        private static void Check(List<string> failures, string id, string field, object expected, object actual)
        {
            if (expected is int && actual is int && (int)expected == (int)actual) return;
            if (expected is string && actual is string
                && string.Equals((string)expected, (string)actual, StringComparison.Ordinal)) return;

            failures.Add(id + "." + field + "：CSV=" + Fmt(expected) + "，Shadow=" + Fmt(actual));
        }

        private static string Fmt(object value)
        {
            return value == null ? "<null>" : "'" + value + "'";
        }

        private static string Str(Dictionary<string, object> row, string field)
        {
            object value;
            if (!row.TryGetValue(field, out value) || value == null) return null;
            return value.ToString();
        }

        private static int Int(Dictionary<string, object> row, string field)
        {
            object value;
            if (!row.TryGetValue(field, out value) || value == null) return int.MinValue;
            if (value is long l) return (int)l;
            if (value is int i) return i;

            int parsed;
            return int.TryParse(value.ToString(), out parsed) ? parsed : int.MinValue;
        }

        private static void PrintDiagnostics(CompileResult result)
        {
            foreach (var d in result.Diagnostics) Console.Error.WriteLine("  " + d);
        }

        private static int RunStatus(CompilerPaths paths)
        {
            Console.WriteLine("M1 SHADOW 状态（只读 · MIGRATION ONLY / M1 CLOSED）");
            Console.WriteLine("  当前 Authority    : Development/Config/RRPG_Monsters.xlsx（Current Monster Definition Authoring Authority）");
            Console.WriteLine("  sync              : 已冻结（拒绝执行；CSV → XLSX 不再允许）");
            Console.WriteLine("  frozen legacy CSV : " + File.Exists(LegacyCsvPath(paths))
                + "（Development/Excel/Monster.csv = Frozen Legacy Source，只读参考）");
            Console.WriteLine("  shadow xlsx       : " + File.Exists(Path.Combine(paths.AuthoringDir, "RRPG_Monsters.xlsx")));
            Console.WriteLine("  presentation xlsx : " + File.Exists(Path.Combine(paths.AuthoringDir, "RRPG_EnemyPresentation.xlsx")));
            Console.WriteLine("  mirror dir        : " + paths.GeneratedDirRelative);
            Console.WriteLine("结果：PASS");
            return ExitOk;
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
            Console.WriteLine("rrpg-m1-shadow（MIGRATION ONLY · M1 CLOSED —— frozen after M2 Authority Cutover）");
            Console.WriteLine();
            Console.WriteLine("  sync        已冻结：拒绝执行（CSV -> XLSX synchronization is no longer allowed）");
            Console.WriteLine("  equivalence A/B 层只读等价（CSV↔XLSX、XLSX↔Mirror）；C 层由 Unity EditMode Gate 负责");
            Console.WriteLine("  status      只读打印 Shadow 链状态");
            Console.WriteLine();
            Console.WriteLine("M2 Authority Cutover 之后本工具不得再拥有产品内容写入权（D-M2-2；FDS §10.4-A / §10.6）。");
            Console.WriteLine("内容变更：改 Development/Config/RRPG_Monsters.xlsx → 运行 RRPG_一键导表.cmd。");
        }
    }
}
