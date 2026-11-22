using System.Collections.Generic;
using UnityEngine;

namespace RRPG.StaticConfig
{
    /// <summary>
    /// 生成容器：视觉资源目录（VisualAsset 域；M3 Slice 1B 落地字段）。
    ///
    /// 承载由 Mirror（<c>VisualAsset.json</c> + <c>AssetManifest.json</c>）经 Unity Builder 生成的
    /// 视觉资源列表（含由 <c>Development/SourceAssets/&lt;SourceFile&gt;</c> 生成的 **managed Sprite**）——
    /// **生成物**，不是人工 Authoring 对象。
    ///
    /// 语义：
    ///   - <see cref="All"/> 顺序 = Mirror 的 Canonical 顺序（按 VisualAssetId 排序，可复现）；
    ///   - 查询未命中 → false（不抛、不猜、不静默返回别的资源）；
    ///   - <c>Sprite == null</c> 表示生成根不完整（失败态）：调用方必须显式处理，不得静默用空图。
    ///
    /// M3 Slice 1B 边界：**不接战斗显示**（没有 Gameplay 消费者）。
    ///
    /// 注意：Unity 要求 ScriptableObject 与其资产脚本**同名文件**（本文件即该约定）。
    /// </summary>
    public sealed class VisualAssetCatalog : ScriptableObject
    {
        /// <summary>查询未命中时的明确诊断前缀（不静默回退到其它资源）。</summary>
        public const string MissingIdDiagnosticPrefix = "VisualAsset 不存在该 VisualAssetId：";

        [SerializeField] private string formatVersion = RRPGStaticConfigRoot.SupportedFormatVersion;
        [SerializeField] private string sourceMirrorSha256 = string.Empty;
        [SerializeField] private string sourceSetSha256 = string.Empty;
        [SerializeField] private List<VisualAssetDefinition> assets = new List<VisualAssetDefinition>();

        public string FormatVersion { get { return formatVersion; } }

        /// <summary>来源 Mirror 的语义指纹（VisualAsset.json 的 <c>_generated.sourceSha256</c>）。</summary>
        public string SourceMirrorSha256 { get { return sourceMirrorSha256; } }

        /// <summary>整组源资产指纹（AssetManifest.json 的 <c>sourceSetSha256</c>；换图即变化）。</summary>
        public string SourceSetSha256 { get { return sourceSetSha256; } }

        public int Count { get { return assets == null ? 0 : assets.Count; } }

        /// <summary>稳定顺序的全部资源（顺序 = Mirror 的 Canonical 顺序）。</summary>
        public IReadOnlyList<VisualAssetDefinition> All
        {
            get { return assets ?? (IReadOnlyList<VisualAssetDefinition>)new List<VisualAssetDefinition>(); }
        }

        /// <summary>Builder 唯一写入口（Editor-only 调用；Runtime 只读）。</summary>
        public void ApplyGeneratedContent(string format, string mirrorSha256, string sourceSetHash,
            List<VisualAssetDefinition> entries)
        {
            formatVersion = format;
            sourceMirrorSha256 = mirrorSha256 ?? string.Empty;
            sourceSetSha256 = sourceSetHash ?? string.Empty;
            assets = entries ?? new List<VisualAssetDefinition>();
        }

        /// <summary>按 VisualAssetId 命中资源（未命中 → false）。</summary>
        public bool TryGetById(string visualAssetId, out VisualAssetDefinition asset)
        {
            asset = null;
            if (string.IsNullOrEmpty(visualAssetId) || assets == null) return false;

            for (int i = 0; i < assets.Count; i++)
            {
                var candidate = assets[i];
                if (candidate != null && string.Equals(candidate.VisualAssetId, visualAssetId, System.StringComparison.Ordinal))
                {
                    asset = candidate;
                    return true;
                }
            }
            return false;
        }

        public bool Contains(string visualAssetId)
        {
            VisualAssetDefinition ignored;
            return TryGetById(visualAssetId, out ignored);
        }

        /// <summary>未命中时的明确诊断（调用方应显式展示 / 记录，不得假装成功）。</summary>
        public static string BuildMissingDiagnostic(string visualAssetId)
        {
            return MissingIdDiagnosticPrefix + (string.IsNullOrEmpty(visualAssetId) ? "<null>" : visualAssetId);
        }
    }
}
