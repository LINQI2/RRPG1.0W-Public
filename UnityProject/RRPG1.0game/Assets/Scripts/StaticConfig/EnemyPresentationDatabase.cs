using System.Collections.Generic;
using UnityEngine;

namespace RRPG.StaticConfig
{
    /// <summary>
    /// 生成容器：敌方表现绑定库（EnemyPresentation 域；M3 Slice 1B 落地字段）。
    ///
    /// 承载由 Mirror（<c>Development/Generated/StaticConfig/EnemyPresentation.json</c>）
    /// 经 Unity Builder 生成的「MonsterId → VisualAssetId」列表 —— **生成物**，不是人工 Authoring 对象。
    ///
    /// 语义（与 MonsterRegistry 同一套约定）：
    ///   - <see cref="All"/> 顺序 = Mirror 的 Canonical 顺序（按 MonsterId 排序的产物顺序，可复现）；
    ///   - 查询未命中 → false（不抛、不猜、不静默回退到其它怪物）；
    ///   - 重复 MonsterId 不可能出现（Compiler 侧 DAT005 已拦），Builder 侧再校验一次。
    ///
    /// M3 Slice 1B 边界：**不接战斗显示**（没有 Gameplay 消费者）；视觉资源细节在
    /// <see cref="VisualAssetCatalog"/>（本类只持有 ID 引用，不持有 Sprite）。
    ///
    /// 注意：Unity 要求 ScriptableObject 与其资产脚本**同名文件**（本文件即该约定）。
    /// </summary>
    public sealed class EnemyPresentationDatabase : ScriptableObject
    {
        /// <summary>查询未命中时的明确诊断前缀（不静默回退）。</summary>
        public const string MissingMonsterDiagnosticPrefix = "Enemy Presentation 未登记该 MonsterId：";

        [SerializeField] private string formatVersion = RRPGStaticConfigRoot.SupportedFormatVersion;
        [SerializeField] private string sourceMirrorSha256 = string.Empty;
        [SerializeField] private List<EnemyPresentationBinding> bindings = new List<EnemyPresentationBinding>();

        public string FormatVersion { get { return formatVersion; } }

        /// <summary>来源 Mirror 的语义指纹（EnemyPresentation.json 的 <c>_generated.sourceSha256</c>）。</summary>
        public string SourceMirrorSha256 { get { return sourceMirrorSha256; } }

        public int Count { get { return bindings == null ? 0 : bindings.Count; } }

        /// <summary>稳定顺序的全部绑定（顺序 = Mirror 的 Canonical 顺序）。</summary>
        public IReadOnlyList<EnemyPresentationBinding> All
        {
            get { return bindings ?? (IReadOnlyList<EnemyPresentationBinding>)new List<EnemyPresentationBinding>(); }
        }

        /// <summary>Builder 唯一写入口（Editor-only 调用；Runtime 只读）。</summary>
        public void ApplyGeneratedContent(string format, string mirrorSha256, List<EnemyPresentationBinding> entries)
        {
            formatVersion = format;
            sourceMirrorSha256 = mirrorSha256 ?? string.Empty;
            bindings = entries ?? new List<EnemyPresentationBinding>();
        }

        /// <summary>按 MonsterId 命中绑定（未命中 → false）。</summary>
        public bool TryGetByMonsterId(string monsterId, out EnemyPresentationBinding binding)
        {
            binding = null;
            if (string.IsNullOrEmpty(monsterId) || bindings == null) return false;

            for (int i = 0; i < bindings.Count; i++)
            {
                var candidate = bindings[i];
                if (candidate != null && string.Equals(candidate.MonsterId, monsterId, System.StringComparison.Ordinal))
                {
                    binding = candidate;
                    return true;
                }
            }
            return false;
        }

        /// <summary>按 MonsterId 命中 VisualAssetId（未命中 → false）。只返回 ID，不解析 Sprite。</summary>
        public bool TryGetVisualAssetId(string monsterId, out string visualAssetId)
        {
            EnemyPresentationBinding binding;
            if (TryGetByMonsterId(monsterId, out binding))
            {
                visualAssetId = binding.VisualAssetId;
                return !string.IsNullOrEmpty(visualAssetId);
            }

            visualAssetId = null;
            return false;
        }

        /// <summary>未命中时的明确诊断（调用方应显式展示 / 记录，不得假装成功）。</summary>
        public static string BuildMissingDiagnostic(string monsterId)
        {
            return MissingMonsterDiagnosticPrefix + (string.IsNullOrEmpty(monsterId) ? "<null>" : monsterId);
        }
    }
}
