using System;
using System.Collections.Generic;
using RRPG.StaticConfig;

namespace RRPG.StaticConfig.EditorTools
{
    /// <summary>
    /// 从 Canonical Mirror 的 <c>EnemyPresentation.json</c> 读出绑定（Editor-only；M3 Slice 1B）。
    ///
    /// 校验（任一失败 = Hard Error，Builder 必须**在写入前**停）：
    ///   _generated.doNotEdit = true / formatVersion 匹配 / source.sheet = EnemyPresentation /
    ///   列恰好为 MonsterId + VisualAssetId / rowCount 与行数一致 /
    ///   每行两列非空 / 不得出现重复 MonsterId。
    /// </summary>
    internal static class EnemyPresentationMirrorReader
    {
        public const string SheetName = "EnemyPresentation";
        public const string FileName = SheetName + ".json";

        public static readonly string[] ExpectedFields = { "MonsterId", "VisualAssetId" };

        public sealed class Payload
        {
            public string FormatVersion;
            public string SourceSha256;
            public List<EnemyPresentationBinding> Bindings = new List<EnemyPresentationBinding>();
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

                string monsterId = MirrorJsonText.Unquote(rows[i], "MonsterId");
                if (string.IsNullOrEmpty(monsterId))
                {
                    failure = FileName + " 第 " + rowNumber + " 行 MonsterId 为空。";
                    return null;
                }

                string visualAssetId = MirrorJsonText.Unquote(rows[i], "VisualAssetId");
                if (string.IsNullOrEmpty(visualAssetId))
                {
                    failure = FileName + " 第 " + rowNumber + " 行（" + monsterId + "）VisualAssetId 为空。";
                    return null;
                }

                if (!seen.Add(monsterId))
                {
                    failure = FileName + " 出现重复 MonsterId：" + monsterId;
                    return null;
                }

                payload.Bindings.Add(new EnemyPresentationBinding
                {
                    MonsterId = monsterId,
                    VisualAssetId = visualAssetId
                });
            }

            return payload;
        }
    }
}
