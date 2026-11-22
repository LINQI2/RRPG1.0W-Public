using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace RRPG.ConfigCompiler.Core
{
    /// <summary>一次 Compile / Validate 的结果（供 CLI 打印与测试断言）。</summary>
    public sealed class CompileResult
    {
        public bool Success;
        public string Command;
        public CompilerPaths Paths;
        public int ErrorCount;
        public int WarningCount;
        public List<Diagnostic> Diagnostics = new List<Diagnostic>();
        public List<ReportWorkbook> Workbooks = new List<ReportWorkbook>();
        public List<CompiledSheet> CompiledSheets = new List<CompiledSheet>();
        public List<CommittedFile> Outputs = new List<CommittedFile>();
        public string ReportJson;
        public string ReportMarkdown;

        /// <summary>
        /// T4 扩展：Asset Manifest 文本（VisualAssetId → GeneratedAssetPath + SourceFile 事实）。
        /// null = 语料中没有 VisualAsset 实体（T1/T2/T3 语料：输出集合不变）。
        /// </summary>
        public string AssetManifest;
    }

    /// <summary>
    /// 编译流水线编排：
    ///   Discover → Parse → Schema Validate → Data Validate → Cross-Reference Validate
    ///   → Build Temp → Verify → Atomic Replace
    ///
    /// 输出身份（T1.1）：
    ///   - Generated/     = Canonical IR（Git tracked，确定性机器镜像）
    ///   - .build-state/  = 最近一次执行状态（BuildReport；不是正式配置产物，本地忽略）
    ///
    /// hard error（任一 Error）→ Generated/ 0 更新；只允许更新 .build-state/ 的 FAILED 报告。
    /// </summary>
    public static class CompilerRunner
    {
        public const string CommandValidate = "validate";
        public const string CommandBuild = "build";

        /// <summary>Asset Manifest 文件名（M1 VisualAsset 领域落地时使用；FDS §7.1）。</summary>
        public const string AssetManifestFileName = "AssetManifest.json";

        /// <summary>
        /// Mirror 目录允许存在的治理文件（不是配置产物，删掉会破坏 Mirror 的可读治理面；
        /// FDS §7.1 + 用户 M0 §7：Mirror 允许「空目录 / 仅 README 类治理文件」）。
        /// </summary>
        public const string MirrorReadmeName = "README.md";

        public static CompileResult Run(CompilerPaths paths, string command, bool writeOutputs)
        {
            var bag = new DiagnosticBag();
            var result = new CompileResult { Command = command, Paths = paths };

            var roots = new List<KeyValuePair<string, string>>
            {
                // Production V0.1：唯一人工 Authoring 根 = Development/Config（FDS §3.1）。
                // 测试夹具不进这里：它们在测试工程的临时目录里生成（FDS §7）。
                new KeyValuePair<string, string>("Config", paths.AuthoringDir)
            };

            var workbooks = WorkbookDiscovery.Discover(roots, bag);

            foreach (var wb in workbooks)
            {
                var reportWb = new ReportWorkbook { Path = wb.DisplayPath };

                var issues = Xlsx.OpenXmlPackageChecker.Validate(wb.FullPath);
                reportWb.PackageValidation = issues.Count == 0 ? "VALID" : "INVALID";
                reportWb.PackageIssueCount = issues.Count;

                if (issues.Count > 0)
                {
                    bag.Error(DiagnosticCodes.WbOpenXmlInvalid,
                        "Open XML 结构校验失败（" + issues.Count + " 项）：" + string.Join(" | ", issues.Take(3)),
                        DiagContext.Of(wb.DisplayPath, null, 0));
                }

                foreach (var raw in WorkbookReader.Read(wb, bag))
                {
                    var rs = new ReportSheet { Name = raw.SheetName };

                    if (raw.SkippedForData)
                    {
                        rs.Kind = "SKIPPED";
                        rs.SkipReason = raw.SkipReason;
                    }
                    else if (raw.Empty)
                    {
                        rs.Kind = "EMPTY";
                    }
                    else
                    {
                        rs.Kind = "DATA";
                        var spec = SchemaCompiler.Compile(raw, bag);
                        var compiled = DataCompiler.Compile(raw, spec, bag);
                        rs.RowCount = compiled.Rows.Count;
                        rs.ColumnCount = spec.ColumnCount;
                        rs.Key = spec.KeyFields.Select(f => f.Field).ToList();
                        result.CompiledSheets.Add(compiled);
                    }

                    reportWb.Sheets.Add(rs);
                }

                result.Workbooks.Add(reportWb);
            }

            CrossReferenceValidator.ValidateAndSort(result.CompiledSheets, bag);

            // WP-Encounter-1A：Encounter 领域（遭遇生成静态配置基础）的跨列 / 条件引用 / 取值域校验。
            // 复用同一 bag / 同一诊断面：任何 Error → build 不写 Mirror（Hard Error → 0 更新）。
            EncounterValidator.Validate(result.CompiledSheets, bag);

            // WP-Event-1A：Event 领域（统一事件内容基础）的跨列 / 条件引用 / 领域规则校验。
            // 同一 bag / 同一诊断面：任何 Error → build 不写 Mirror（Hard Error → 0 更新）。
            EventValidator.Validate(result.CompiledSheets, bag);
            AutonomousLifeValidator.Validate(result.CompiledSheets, bag);
            SocialValidator.Validate(result.CompiledSheets, bag);
            BuildingFunctionValidator.Validate(result.CompiledSheets, bag);

            // Asset Manifest（M3 Slice 1A）：VisualAsset → SourceFile 内容哈希的 Builder 输入契约。
            // 没有 VisualAsset Sheet → null（合法空态：该领域不存在，输出集合与 M0/M1 完全一致）。
            result.AssetManifest = SourceArtValidator.Validate(paths, result.CompiledSheets, bag);

            // Canonical Mirror 内容（先在内存里算全，再由 build 提交；validate 只读比对）。
            var files = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var sheet in result.CompiledSheets)
            {
                files[CanonicalIrWriter.OutputName(sheet)] = CanonicalIrWriter.Write(sheet);
            }

            if (result.AssetManifest != null) files[AssetManifestFileName] = result.AssetManifest;

            // validate（writeOutputs = false）：证明 Authoring == Mirror，且**不写任何文件**（FDS §7.2 R4/R5）。
            if (!writeOutputs) MirrorCheck.Verify(paths, files, bag);

            result.Success = !bag.HasErrors;
            result.ErrorCount = bag.ErrorCount;
            result.WarningCount = bag.WarningCount;
            result.Diagnostics = bag.Sorted().ToList();

            var irCommitter = new OutputCommitter(paths.GeneratedDir, paths.TempDir, "Generated");
            var stateCommitter = new OutputCommitter(paths.BuildStateDir, paths.TempDir, ".build-state");

            if (!result.Success)
            {
                // Hard Error：Canonical IR（Generated/）0 更新。
                // 只允许更新 .build-state/ 下的 FAILED 报告（审计需要看见失败本身）。
                result.ReportJson = BuildReport.WriteJson(command, paths, false, result.Workbooks, bag,
                    new List<CommittedFile>());
                result.ReportMarkdown = BuildReportMarkdown.Write(command, paths, false, result.Workbooks, bag,
                    new List<CommittedFile>());

                if (writeOutputs)
                {
                    result.Outputs = stateCommitter.CommitSome(new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        { OutputCommitter.ReportFileName, result.ReportJson },
                        { OutputCommitter.ReportMarkdownName, result.ReportMarkdown }
                    });
                }

                return result;
            }

            // 第一段：提交 Canonical IR（原子替换），拿到真实动作。
            List<CommittedFile> irActions = writeOutputs
                ? irCommitter.CommitSome(files)
                : files.Select(kv => Describe("Generated", kv.Key, kv.Value, false)).ToList();

            // 第二段：报告记录真实动作（不是 "pending"），写入 .build-state/（不是 Generated/）。
            string md = BuildReportMarkdown.Write(command, paths, true, result.Workbooks, bag, irActions);
            string reportJson = BuildReport.WriteJson(command, paths, true, result.Workbooks, bag, irActions);

            result.ReportJson = reportJson;
            result.ReportMarkdown = md;

            if (writeOutputs)
            {
                var reportFiles = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    { OutputCommitter.ReportFileName, reportJson },
                    { OutputCommitter.ReportMarkdownName, md }
                };

                var reportActions = stateCommitter.CommitSome(reportFiles);

                var staleIr = irCommitter.DeleteStale(MirrorKeepSet(files));
                var staleState = stateCommitter.DeleteStale(new HashSet<string>(reportFiles.Keys, StringComparer.Ordinal));

                result.Outputs = irActions
                    .Concat(reportActions)
                    .Concat(staleIr)
                    .Concat(staleState)
                    .OrderBy(o => o.FullName, StringComparer.Ordinal)
                    .ToList();
            }
            else
            {
                result.Outputs = irActions;
            }

            return result;
        }

        /// <summary>
        /// Mirror 目录的保留集合 = 本轮产物 + 治理文件（README.md）。
        /// 治理文件不是配置产物：不得被 stale 回收删除（FDS §7.1 / 用户 M0 §7）。
        /// </summary>
        private static ISet<string> MirrorKeepSet(IDictionary<string, string> files)
        {
            var keep = new HashSet<string>(files.Keys, StringComparer.Ordinal);
            keep.Add(MirrorReadmeName);
            return keep;
        }

        private static CommittedFile Describe(string root, string name, string text, bool written)
        {
            byte[] bytes = new UTF8Encoding(false).GetBytes(text);
            return new CommittedFile
            {
                Root = root,
                Name = name,
                Action = written ? "pending" : "not-written",
                Bytes = bytes.Length,
                Sha256 = OutputCommitter.Sha256(bytes)
            };
        }
    }
}
