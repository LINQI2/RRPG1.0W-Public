using System;
using System.Collections.Generic;
using RRPG.StaticConfig;

namespace RRPG.StaticConfig.EditorTools
{
    /// <summary>
    /// 从 Canonical Mirror 的 <c>VisualAsset.json</c> 读出视觉资源定义（Editor-only；M3 Slice 1B）。
    ///
    /// 校验（任一失败 = Hard Error，Builder 必须**在写入前**停）：
    ///   _generated.doNotEdit = true / formatVersion 匹配 / source.sheet = VisualAsset /
    ///   列恰好为 VisualAssetId + SourceFile + ImportPreset / rowCount 与行数一致 /
    ///   每行三列非空 / VisualAssetId 不得重复 / ImportPreset 必须受支持 /
    ///   SourceFile 必须是相对路径且为 .png。
    ///
    /// 注意：**源图是否存在**不在这里判定（那是 AssetManifest + 磁盘的职责，见 <see cref="AssetManifestReader"/>）。
    /// </summary>
    internal static class VisualAssetMirrorReader
    {
        public const string SheetName = "VisualAsset";
        public const string FileName = SheetName + ".json";

        /// <summary>唯一受支持的导入预设（Point / 100 PPU / Center）。</summary>
        public const string SpritePoint100Preset = "sprite_point_100";

        public static readonly string[] ExpectedFields = { "VisualAssetId", "SourceFile", "ImportPreset" };

        public sealed class Payload
        {
            public string FormatVersion;
            public string SourceSha256;
            public List<VisualAssetDefinition> Assets = new List<VisualAssetDefinition>();
        }

        public static bool IsSupportedPreset(string preset)
        {
            return string.Equals(preset, SpritePoint100Preset, StringComparison.Ordinal);
        }

        public static Payload Read(string json, out string failure)
        {
            failure = string.Empty;

            string formatVersion;
            string sourceSha256;
            if (!MirrorJsonText.TryReadHeader(json, FileName, out formatVersion, out sourceSha256, out failure))
                return null;
            if (!MirrorJsonText.TryReadSheet(json, FileName, SheetName, out failure)) return null;
            if (!MirrorJsonText.TryReadColumns(json, FileName, ExpectedFields, out failure)) return null;

            int declaredRowCount;
            List<Dictionary<string, string>> rows;
            if (!MirrorJsonText.TryReadRows(json, FileName, "rowCount", "rows", out declaredRowCount, out rows,
                    out failure)) return null;

            var payload = new Payload { FormatVersion = formatVersion, SourceSha256 = sourceSha256 };
            var seen = new HashSet<string>(StringComparer.Ordinal);

            for (int i = 0; i < rows.Count; i++)
            {
                int rowNumber = i + 1;

                string visualAssetId = MirrorJsonText.Unquote(rows[i], "VisualAssetId");
                if (string.IsNullOrEmpty(visualAssetId))
                {
                    failure = FileName + " 第 " + rowNumber + " 行 VisualAssetId 为空。";
                    return null;
                }

                if (!seen.Add(visualAssetId))
                {
                    failure = FileName + " 出现重复 VisualAssetId：" + visualAssetId;
                    return null;
                }

                string sourceFile = MirrorJsonText.Unquote(rows[i], "SourceFile");
                string pathFailure;
                if (!IsRelativePngPath(sourceFile, out pathFailure))
                {
                    failure = FileName + " 第 " + rowNumber + " 行（" + visualAssetId + "）" + pathFailure;
                    return null;
                }

                string importPreset = MirrorJsonText.Unquote(rows[i], "ImportPreset");
                if (!IsSupportedPreset(importPreset))
                {
                    failure = FileName + " 第 " + rowNumber + " 行（" + visualAssetId + "）ImportPreset 不受支持：'"
                        + importPreset + "'（当前只支持 " + SpritePoint100Preset + "）。";
                    return null;
                }

                payload.Assets.Add(new VisualAssetDefinition
                {
                    VisualAssetId = visualAssetId,
                    SourceFile = sourceFile,
                    ImportPreset = importPreset,
                    SourceSha256 = string.Empty,     // 由 AssetManifest 补齐（Builder 写入前填充）
                    Sprite = null                    // 由 Builder 在生成 managed 资产后填充
                });
            }

            return payload;
        }

        /// <summary>SourceFile 必须是「相对 SourceAssets、不含 .. / 绝对路径、扩展名为 .png」的正斜杠路径。</summary>
        public static bool IsRelativePngPath(string sourceFile, out string failure)
        {
            failure = string.Empty;

            if (string.IsNullOrEmpty(sourceFile))
            {
                failure = "SourceFile 为空。";
                return false;
            }

            string value = sourceFile.Replace('\\', '/');
            if (value.StartsWith("/", StringComparison.Ordinal) || (value.Length >= 2 && value[1] == ':'))
            {
                failure = "SourceFile 必须是相对 Development/SourceAssets 的路径（不得是绝对路径）：'" + sourceFile + "'。";
                return false;
            }

            foreach (string segment in value.Split('/'))
            {
                if (segment.Length == 0 || segment == "." || segment == "..")
                {
                    failure = "SourceFile 不得包含空段 / . / ..：'" + sourceFile + "'。";
                    return false;
                }
            }

            if (!value.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
            {
                failure = "SourceFile 必须是 .png：'" + sourceFile + "'。";
                return false;
            }

            return true;
        }
    }
}
