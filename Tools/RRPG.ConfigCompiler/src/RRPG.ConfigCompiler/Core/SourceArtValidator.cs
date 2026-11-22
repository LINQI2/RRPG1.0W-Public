using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace RRPG.ConfigCompiler.Core
{
    /// <summary>
    /// VisualAsset → 源资产校验 + AssetManifest 生成（M3 Slice 1A）。
    ///
    /// 职责（FDS §7.1）：
    ///   1. 形状（DAT017）：VisualAsset 表必须具备三列 VisualAssetId / SourceFile / ImportPreset；
    ///   2. 源文件事实（只读）：SourceFile 必须是相对 Development/SourceAssets 的 .png，
    ///      且文件真实存在（DAT014 路径越界 / DAT016 扩展名 / DAT013 文件缺失）；
    ///   3. ImportPreset 必须属于 <see cref="VisualImportPresets"/>（DAT015）；
    ///   4. 生成 AssetManifest.json：VisualAssetId → SourceFile + 源文件内容哈希（+ GeneratedAssetPath 契约）。
    ///
    /// 确定性硬约束（FDS §7.2 R3）：
    ///   - 条目不按 Excel 物理行序，而按 VisualAssetId（Ordinal）排序；
    ///   - 不写入时间戳 / 绝对路径 / 机器名；
    ///   - 只使用仓库相对路径（ManagedAssetPath 是 Builder 的**契约**，不是磁盘探测结果）。
    ///
    /// 无 VisualAsset Sheet → 返回 null（合法空态：没有该领域，输出集合与 M0/M1 完全一致）。
    /// </summary>
    public static class SourceArtValidator
    {
        public const string VisualAssetSheetName = "VisualAsset";

        /// <summary>生成资产的**契约**根（Builder 必须把源图生成为该路径下的 managed 资产；不进 Git）。</summary>
        public const string ManagedRootRelative = "Assets/Generated/StaticConfig/Assets";

        /// <summary>VisualAsset 三列契约（与 DAT017 的诊断文案一致）。</summary>
        public static readonly string[] RequiredColumns = { "VisualAssetId", "SourceFile", "ImportPreset" };

        /// <summary>M3 允许的源图扩展名。</summary>
        public static readonly string[] AllowedExtensions = { ".png" };

        private static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(false);

        public static string Validate(CompilerPaths paths, List<CompiledSheet> sheets, DiagnosticBag bag)
        {
            var sheet = sheets.FirstOrDefault(s => s.Spec.SheetName == VisualAssetSheetName);
            if (sheet == null) return null;

            var sheetCtx = DiagContext.Of(sheet.Spec.Workbook, sheet.Spec.SheetName, 0);

            // 1. 形状（DAT017）：缺列时明确失败；后续仍按可读字段尽力生成 manifest（不猜测列语义）。
            var missing = RequiredColumns
                .Where(c => sheet.Spec.Fields.All(f => !string.Equals(f.Field, c, StringComparison.Ordinal)))
                .ToList();
            if (missing.Count > 0)
            {
                bag.Error(DiagnosticCodes.DatVisualAssetShape,
                    "VisualAsset 表需要三列：" + string.Join(" / ", RequiredColumns)
                    + "；当前缺少：" + string.Join(", ", missing) + "。",
                    sheetCtx);
            }

            var entries = new List<ManifestEntry>();

            foreach (var row in sheet.Rows)
            {
                var rowCtx = DiagContext.Of(sheet.Spec.Workbook, sheet.Spec.SheetName, row.ExcelRow);

                string id = Text(row, "VisualAssetId");
                string sourceFile = Text(row, "SourceFile");
                string preset = Text(row, "ImportPreset");

                // 必填为空由 DAT001 / DAT007 覆盖：这里不重复报错，只跳过该行。
                if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(sourceFile) || string.IsNullOrEmpty(preset)) continue;

                string normalized = NormalizeRelativePath(sourceFile);
                if (normalized == null)
                {
                    bag.Error(DiagnosticCodes.DatSourceArtPathEscape,
                        "SourceFile 必须是相对 Development/SourceAssets 的路径（不得是绝对路径，也不得出现 ..）：'"
                        + sourceFile + "'。",
                        rowCtx, "SourceFile");
                    continue;
                }

                string extension = Path.GetExtension(normalized).ToLowerInvariant();
                if (!AllowedExtensions.Contains(extension))
                {
                    bag.Error(DiagnosticCodes.DatSourceArtExtension,
                        "SourceFile 扩展名不被支持（只允许 " + string.Join(" / ", AllowedExtensions) + "）：'"
                        + sourceFile + "'。",
                        rowCtx, "SourceFile");
                    continue;
                }

                if (!VisualImportPresets.IsSupported(preset))
                {
                    bag.Error(DiagnosticCodes.DatUnsupportedImportPreset,
                        "ImportPreset '" + preset + "' 不被支持；当前支持：" + VisualImportPresets.SupportedListText() + "。",
                        rowCtx, "ImportPreset");
                    continue;
                }

                string absolute = Path.Combine(paths.SourceAssetsDir, normalized.Replace('/', Path.DirectorySeparatorChar));
                if (!File.Exists(absolute))
                {
                    bag.Error(DiagnosticCodes.DatSourceArtMissing,
                        "源图不存在：Development/SourceAssets/" + normalized
                        + "（请把源图放进该目录，或修正 SourceFile 拼写）。",
                        rowCtx, "SourceFile");
                    continue;
                }

                byte[] bytes = File.ReadAllBytes(absolute);

                entries.Add(new ManifestEntry
                {
                    VisualAssetId = id,
                    SourceFile = normalized,
                    ImportPreset = preset,
                    SourceBytes = bytes.Length,
                    SourceSha256 = OutputCommitter.Sha256(bytes),
                    ManagedAssetPath = ManagedRootRelative + "/" + normalized
                });
            }

            return WriteManifest(entries);
        }

        private static string Text(CompiledRow row, string field)
        {
            var cell = row.Get(field);
            if (cell == null || cell.Value == null) return null;
            return cell.Value as string ?? cell.Value.ToString();
        }

        /// <summary>归一化为「相对 SourceAssets 的正斜杠路径」；越界（绝对 / .. / 空段）返回 null。</summary>
        private static string NormalizeRelativePath(string sourceFile)
        {
            if (string.IsNullOrWhiteSpace(sourceFile)) return null;

            string value = sourceFile.Trim().Replace('\\', '/');
            if (value.Length == 0) return null;
            if (value.StartsWith("/", StringComparison.Ordinal)) return null;
            if (value.Length >= 2 && value[1] == ':') return null;      // C:/...

            foreach (string segment in value.Split('/'))
            {
                if (segment.Length == 0) return null;
                if (segment == "." || segment == "..") return null;
            }

            return value;
        }

        /// <summary>
        /// 生成 AssetManifest.json（与 Canonical IrWriter 相同的 _generated 契约：无时间戳、无绝对路径）。
        /// </summary>
        private static string WriteManifest(List<ManifestEntry> entries)
        {
            var ordered = entries
                .OrderBy(e => e.VisualAssetId, StringComparer.Ordinal)
                .ThenBy(e => e.SourceFile, StringComparer.Ordinal)
                .ToList();

            string sourceSetSha256 = SourceSetSignature(ordered);

            var bodyWriter = new JsonWriterLite();
            bodyWriter.BeginObject();
            WriteBody(bodyWriter, ordered, sourceSetSha256);
            bodyWriter.EndObject();
            string body = bodyWriter.ToText();
            string bodySha256 = OutputCommitter.Sha256(Utf8NoBom.GetBytes(body));

            var w = new JsonWriterLite();
            w.BeginObject();
            w.Name("_generated");
            w.BeginObject();
            w.Name("doNotEdit");
            w.Value(true);
            w.Member("tool", CanonicalIrWriter.ToolName);
            w.Member("formatVersion", CanonicalIrWriter.IrVersion);
            w.Member("sourceSha256", bodySha256);
            w.Member("notice",
                "本文件由 RRPG ConfigCompiler 生成，禁止手工编辑；"
                + "它记录 SourceAssets 源文件的内容哈希（换图 = 必须重新 build）。");
            w.EndObject();

            WriteBody(w, ordered, sourceSetSha256);
            w.EndObject();
            return w.ToText();
        }

        private static void WriteBody(JsonWriterLite w, List<ManifestEntry> ordered, string sourceSetSha256)
        {
            w.Member("irVersion", CanonicalIrWriter.IrVersion);
            w.Member("managedRootRelative", ManagedRootRelative);
            w.Member("assetCount", ordered.Count);
            w.Member("sourceSetSha256", sourceSetSha256);

            w.Name("assets");
            w.BeginArray();
            foreach (var e in ordered)
            {
                w.BeginObject();
                w.Member("VisualAssetId", e.VisualAssetId);
                w.Member("SourceFile", e.SourceFile);
                w.Member("ImportPreset", e.ImportPreset);
                w.Member("SourceBytes", e.SourceBytes);
                w.Member("SourceSha256", e.SourceSha256);
                w.Member("ManagedAssetPath", e.ManagedAssetPath);
                w.EndObject();
            }
            w.EndArray();
        }

        /// <summary>整组源资产指纹（排序后拼接 → SHA256）：Builder 的内容签名输入。</summary>
        private static string SourceSetSignature(List<ManifestEntry> ordered)
        {
            var sb = new StringBuilder();
            foreach (var e in ordered)
            {
                sb.Append(e.VisualAssetId).Append('|')
                    .Append(e.SourceFile).Append('|')
                    .Append(e.ImportPreset).Append('|')
                    .Append(e.SourceSha256).Append('\n');
            }
            return OutputCommitter.Sha256(Utf8NoBom.GetBytes(sb.ToString()));
        }

        private sealed class ManifestEntry
        {
            public string VisualAssetId;
            public string SourceFile;
            public string ImportPreset;
            public int SourceBytes;
            public string SourceSha256;
            public string ManagedAssetPath;
        }
    }
}
