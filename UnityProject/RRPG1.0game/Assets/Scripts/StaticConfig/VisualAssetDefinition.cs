using System;
using UnityEngine;

namespace RRPG.StaticConfig
{
    /// <summary>
    /// 一条视觉资源定义（VisualAsset 域的 Runtime 只读记录）。
    ///
    /// M3 Slice 1B 边界：
    ///   - 它是**生成物**（Mirror <c>VisualAsset.json</c> + <c>AssetManifest.json</c> → Builder → VisualAssetCatalog）；
    ///   - <see cref="Sprite"/> 是 Builder 由 <c>Development/SourceAssets/&lt;SourceFile&gt;</c> 生成的 **managed 资产**
    ///     （落在 <c>Assets/Generated/StaticConfig/Assets/</c>，不进 Git）；
    ///   - <see cref="SourceFile"/> 只作诊断 / 溯源（Runtime 不读磁盘、不知道仓库路径）；
    ///   - 本 Slice **不接战斗显示**（没有任何 Gameplay 消费者）。
    /// </summary>
    [Serializable]
    public sealed class VisualAssetDefinition
    {
        /// <summary>身份（Canonical 输出按它排序）。</summary>
        public string VisualAssetId;

        /// <summary>源图路径（相对 <c>Development/SourceAssets</c>；仅供诊断 / 溯源）。</summary>
        public string SourceFile;

        /// <summary>导入预设名（M3 只支持 <c>sprite_point_100</c>）。</summary>
        public string ImportPreset;

        /// <summary>源图内容的 sha256（来自 AssetManifest；用于「换图 = 必须重新 build」的判定与诊断）。</summary>
        public string SourceSha256;

        /// <summary>Builder 生成的 Sprite（null = 生成根不完整，属失败态，不得静默使用）。</summary>
        public Sprite Sprite;

        /// <summary>字段数常量（与 Mirror 的 3 列契约一致；用于校验）。</summary>
        public const int FieldCount = 3;

        public override string ToString()
        {
            return VisualAssetId + "(" + SourceFile + ", " + ImportPreset + ", sprite="
                + (Sprite == null ? "<null>" : Sprite.name) + ")";
        }
    }
}
