using System.Collections.Generic;
using UnityEngine;

namespace RRPG.StaticConfig
{
    /// <summary>
    /// 生成容器：Monster 纯静态定义库（FDS §3.3 要求 Root 引用它）。
    ///
    /// M1（Shadow）：承载由 Mirror（<c>Development/Generated/StaticConfig/Monster.json</c>）
    /// 经 Unity Builder 生成的 MonsterDefinition 列表 —— **生成物**，不是人工 Authoring 对象。
    /// 产品 Gameplay 在 M1 完全不消费它。
    ///
    /// 注意：Unity 要求 ScriptableObject 与其资产脚本**同名文件**（本文件即该约定）。
    /// </summary>
    public sealed class MonsterConfigDatabase : ScriptableObject
    {
        [SerializeField] private string formatVersion = RRPGStaticConfigRoot.SupportedFormatVersion;
        [SerializeField] private string sourceMirrorSha256 = string.Empty;
        [SerializeField] private List<MonsterDefinition> monsters = new List<MonsterDefinition>();

        public string FormatVersion { get { return formatVersion; } }

        /// <summary>来源 Mirror 的语义指纹（_generated.sourceSha256）。</summary>
        public string SourceMirrorSha256 { get { return sourceMirrorSha256; } }

        public int Count { get { return monsters == null ? 0 : monsters.Count; } }

        public IReadOnlyList<MonsterDefinition> All
        {
            get { return monsters ?? (IReadOnlyList<MonsterDefinition>)new List<MonsterDefinition>(); }
        }

        /// <summary>Builder 唯一写入口（Editor-only 调用；Runtime 只读）。</summary>
        public void ApplyGeneratedContent(string format, string mirrorSha256, List<MonsterDefinition> definitions)
        {
            formatVersion = format;
            sourceMirrorSha256 = mirrorSha256 ?? string.Empty;
            monsters = definitions ?? new List<MonsterDefinition>();
        }

        /// <summary>Runtime 侧只读 Registry（Shadow；产品 Gameplay 不调用）。</summary>
        public MonsterRegistry CreateRegistry()
        {
            return new MonsterRegistry(All);
        }
    }
}

