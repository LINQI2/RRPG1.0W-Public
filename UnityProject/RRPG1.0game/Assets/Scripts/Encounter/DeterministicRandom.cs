namespace RRPG.Encounter
{
    /// <summary>
    /// WP-Encounter-1B：Encounter 生成使用的确定性随机源。
    ///
    /// **WP-Event-1B 起，算法本体只有一份**：
    /// <see cref="RRPG.Deterministic.DeterministicRandom"/>（共享纯逻辑程序集
    /// `RRPG.Deterministic.Runtime`）。本类保留自己的程序集身份与类型名，
    /// 只是它的一层**兼容子类**：Encounter 的调用点与测试无需任何改动，
    /// 同时杜绝「两套 RNG 实现逐渐不一致」。
    ///
    /// 行为完全等同父类（不重写任何成员）：SplitMix64 状态推进 + FNV-1a 稳定哈希 +
    /// 排列 / 区间排列 / 加权排列 / 子流派生。
    /// 由 `Assets/Tests/EditMode/Event/DeterministicRandomSharedTests.cs` 守住「同一实现」。
    /// </summary>
    public sealed class DeterministicRandom : RRPG.Deterministic.DeterministicRandom
    {
        /// <summary>用 int Seed 构造（内部先做一次混合，避免小种子产生相邻序列）。</summary>
        public DeterministicRandom(int seed)
            : base(seed)
        {
        }

        /// <summary>用 64 位状态构造（供派生独立子流）。</summary>
        public DeterministicRandom(ulong state)
            : base(state)
        {
        }
    }
}
