using System;

namespace RRPG.StaticConfig
{
    /// <summary>
    /// 一条「怪物 → 视觉资源」绑定（EnemyPresentation 域的 Runtime 只读记录）。
    ///
    /// M3 Slice 1B 边界：它是**生成物**（Mirror <c>EnemyPresentation.json</c> → Builder → EnemyPresentationDatabase），
    /// 不是人工 Authoring 对象；本 Slice **不接战斗显示**（没有任何 Gameplay 消费者）。
    ///
    /// 身份 = <see cref="MonsterId"/>（Composite Key；Canonical 输出按它排序）。
    /// </summary>
    [Serializable]
    public sealed class EnemyPresentationBinding
    {
        public string MonsterId;
        public string VisualAssetId;

        /// <summary>字段数常量（与 Mirror 的 2 列契约一致；用于校验）。</summary>
        public const int FieldCount = 2;

        public override string ToString()
        {
            return MonsterId + " → " + VisualAssetId;
        }
    }
}
