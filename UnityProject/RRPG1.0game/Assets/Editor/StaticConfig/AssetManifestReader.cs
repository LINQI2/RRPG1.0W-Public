using System;
using System.Collections.Generic;
using System.Text;

namespace RRPG.StaticConfig.EditorTools
{
    /// <summary>一条 AssetManifest 条目（SourceAsset → managed 资产的**契约**）。</summary>
    internal sealed class AssetManifestEntry
    {
        public string VisualAssetId;
        public string SourceFile;        // 相对 Development/SourceAssets（正斜杠）
        public string ImportPreset;
        public int SourceBytes;
        public string SourceSha256;      // 源图内容哈希（换图即变化）
        public string ManagedAssetPath;  // Assets/Generated/StaticConfig/Assets/<SourceFile>

        public override string ToString()
        {
            return VisualAssetId + " ← " + SourceFile + " → " + ManagedAssetPath;
        }
    }

    /// <summary>
    /// 从 Canonical Mirror 的 <c>AssetManifest.json</c> 读出「源图 → managed 资产」契约（Editor-only；M3 Slice 1B）。
    ///
    /// 这是 Builder 生成 managed Sprite 的**唯一输入**：SourceFile 决定从哪里读、
    /// ManagedAssetPath 决定写到哪里（Compiler 侧已冻结该契约，Builder 不得自行推导 / 漂移）。
    ///
    /// 校验（任一失败 = Hard Error，Builder 必须**在写入前**停）：
    ///   _generated.doNotEdit = true / formatVersion 匹配 / managedRootRelative 等于契约值 /
    ///   assetCount 与条目数一致 / 每条 6 字段齐全且类型正确 /
    ///   ManagedAssetPath == managedRootRelative + "/" + SourceFile / SourceSha256 为 64 位十六进制 /
    ///   ImportPreset 受支持 / SourceFile 是相对 .png 路径 / VisualAssetId 不得重复。
    /// </summary>
    internal static class AssetManifestReader
    {
        public const string FileName = "AssetManifest.json";

        /// <summary>managed 资产根的契约值（与 Compiler 的 ManagedRootRelative 一致）。</summary>
        public const string ExpectedManagedRoot = "Assets/Generated/StaticConfig/Assets";

        public static readonly string[] ExpectedFields =
        {
            "VisualAssetId", "SourceFile", "ImportPreset", "SourceBytes", "SourceSha256", "ManagedAssetPath"
        };

        public sealed class Payload
        {
            public string FormatVersion;
            public string SourceSha256;      // Manifest 正文的语义指纹
            public string SourceSetSha256;   // 整组源资产指纹
            public List<AssetManifestEntry> Assets = new List<AssetManifestEntry>();
        }

        public static bool IsSha256Hex(string value)
        {
            if (string.IsNullOrEmpty(value) || value.Length != 64) return false;

            foreach (char c in value)
            {
                bool hex = (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f');
                if (!hex) return false;
            }
            return true;
        }

        public static Payload Read(string json, out string failure)
        {
            failure = string.Empty;

            string formatVersion;
            string sourceSha256;
            if (!MirrorJsonText.TryReadHeader(json, FileName, out formatVersion, out sourceSha256, out failure))
                return null;

            string managedRoot = MirrorJsonText.ExtractString(json, "\"managedRootRelative\": ");
            if (!string.Equals(managedRoot, ExpectedManagedRoot, StringComparison.Ordinal))
            {
                failure = FileName + " managedRootRelative 与 Builder 契约不一致：期望 '"
                    + ExpectedManagedRoot + "'，实际 '" + managedRoot + "'。";
                return null;
            }

            string sourceSetSha256 = MirrorJsonText.ExtractString(json, "\"sourceSetSha256\": ") ?? string.Empty;
            if (string.IsNullOrEmpty(sourceSetSha256))
            {
                failure = FileName + " 缺少 sourceSetSha256（无法做换图判定）。";
                return null;
            }

            int declaredAssetCount;
            List<Dictionary<string, string>> rows;
            if (!MirrorJsonText.TryReadRows(json, FileName, "assetCount", "assets", out declaredAssetCount, out rows,
                    out failure)) return null;

            var payload = new Payload
            {
                FormatVersion = formatVersion,
                SourceSha256 = sourceSha256,
                SourceSetSha256 = sourceSetSha256
            };

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
                if (!VisualAssetMirrorReader.IsRelativePngPath(sourceFile, out pathFailure))
                {
                    failure = FileName + " 第 " + rowNumber + " 行（" + visualAssetId + "）" + pathFailure;
                    return null;
                }

                string importPreset = MirrorJsonText.Unquote(rows[i], "ImportPreset");
                if (!VisualAssetMirrorReader.IsSupportedPreset(importPreset))
                {
                    failure = FileName + " 第 " + rowNumber + " 行（" + visualAssetId + "）ImportPreset 不受支持：'"
                        + importPreset + "'。";
                    return null;
                }

                int sourceBytes;
                if (!MirrorJsonText.TryInt(rows[i], "SourceBytes", out sourceBytes) || sourceBytes <= 0)
                {
                    failure = FileName + " 第 " + rowNumber + " 行（" + visualAssetId + "）SourceBytes 非法。";
                    return null;
                }

                string entrySha = MirrorJsonText.Unquote(rows[i], "SourceSha256");
                if (!IsSha256Hex(entrySha))
                {
                    failure = FileName + " 第 " + rowNumber + " 行（" + visualAssetId + "）SourceSha256 不是 64 位十六进制。";
                    return null;
                }

                string managed = MirrorJsonText.Unquote(rows[i], "ManagedAssetPath");
                string expectedManaged = ExpectedManagedRoot + "/" + sourceFile;
                if (!string.Equals(managed, expectedManaged, StringComparison.Ordinal))
                {
                    failure = FileName + " 第 " + rowNumber + " 行（" + visualAssetId
                        + "）ManagedAssetPath 违反契约：期望 '" + expectedManaged + "'，实际 '" + managed + "'。";
                    return null;
                }

                payload.Assets.Add(new AssetManifestEntry
                {
                    VisualAssetId = visualAssetId,
                    SourceFile = sourceFile,
                    ImportPreset = importPreset,
                    SourceBytes = sourceBytes,
                    SourceSha256 = entrySha,
                    ManagedAssetPath = managed
                });
            }

            return payload;
        }
    }
}
