using System;

namespace RRPG.Encounter
{
    /// <summary>
    /// **EncounterDepth 当前规则**（WP-Encounter-1C §4；V0.1 Compatibility / Current Rule）。
    ///
    /// 规则：`depth = max(0, 已进入 Node 数量 - 1)` —— 表示**当前远征实际选择路线的推进步数**：
    ///   第一节点 = 0 / 第二节点 = 1 / 第三节点 = 2 …
    ///
    /// 语义边界（重要）：
    ///   - 所有 Node 类型都计入深度（Battle / Elite / Boss / Event / Rest / 其它），**不是**「战斗场数」；
    ///   - 不读 Legacy Map 坐标（PosY）、不读 UI 坐标、不新建 Save / Person / RunState 字段；
    ///   - 未来「无限远征」的正式 Depth 模型仍 **Not Frozen**（本规则只覆盖 V0.1 当前兼容语义）。
    /// </summary>
    public static class EncounterDepthRule
    {
        /// <summary>按「当前 Progression 已进入的 Node 数量」求本场 EncounterDepth。</summary>
        public static int DepthFor(int enteredNodeCount)
        {
            if (enteredNodeCount <= 0) return 0;
            return enteredNodeCount - 1;
        }
    }

    /// <summary>
    /// Battle 类节点的最小分派类别（Encounter 域视角，不引用产品层 ExpeditionNodeKind，避免双向依赖）。
    /// 产品适配层负责把 ExpeditionNodeKind 映射到本枚举。
    /// </summary>
    public enum EncounterNodeCategory
    {
        /// <summary>普通战斗节点。</summary>
        Normal = 0,

        /// <summary>精英战斗节点。</summary>
        Elite = 1,

        /// <summary>Boss 战斗节点。</summary>
        Boss = 2
    }

    /// <summary>
    /// **V0.1 Compatibility / Test Pool Selector**（WP-Encounter-1C §7）。
    ///
    /// ⚠️ 这不是长期设计：正式「Node Content → EncounterPool」关系（Biome / Region / 节点内容表）
    /// 尚未建立，本轮只用最小兼容映射让产品链能跑起来：
    ///   - 普通战斗 → <see cref="NormalPoolId"/>
    ///   - 精英 / Boss → <see cref="DeepPoolId"/>
    ///
    /// 边界：`pool_test_*` 是 **Test / Not Frozen** 测试池 ID，不得被当作正式产品内容 ID；
    /// 未来建立正式映射时，本类（连同这两个常量）应整体退役，而不是被扩展成第二套内容 Authority。
    /// </summary>
    public static class EncounterPoolSelector
    {
        /// <summary>【V0.1 Compatibility】普通战斗使用的测试池。</summary>
        public const string NormalPoolId = "pool_test_general";

        /// <summary>【V0.1 Compatibility】精英 / Boss 使用的测试池。</summary>
        public const string DeepPoolId = "pool_test_deep";

        /// <summary>按节点类别取池 ID（唯一入口；不读内容配置、不猜）。</summary>
        public static string PoolIdFor(EncounterNodeCategory category)
        {
            return category == EncounterNodeCategory.Normal ? NormalPoolId : DeepPoolId;
        }
    }

    /// <summary>
    /// **本场 EncounterSeed 的确定性派生**（WP-Encounter-1C §5）。
    ///
    /// 产品层每次新远征只生成**一次** SessionSeed（Runtime Only，不进 Save）；
    /// 单场 Seed 必须由 (SessionSeed, CurrentNodeId, EncounterDepth) 稳定派生：
    ///   - 同一远征会话 + 同一 CurrentNode → **同一个 EncounterSeed**（Prep 重开 / Battle 开始 / UI 重建都不重抽）；
    ///   - 不同 Node（或不同会话）→ 不同 Seed。
    ///
    /// 实现：FNV-1a 稳定哈希 + SplitMix64 finalizer（与 <see cref="DeterministicRandom"/> 同一套确定性原语族），
    /// **不使用**时间 / Guid / 引擎全局随机 —— 本文件仍受 1B 源码级守卫约束。
    /// </summary>
    public static class EncounterSeedDerivation
    {
        private const ulong GoldenGamma = 0x9E3779B97F4A7C15UL;

        /// <summary>派生本场 EncounterSeed（同输入必得同输出；跨进程稳定）。</summary>
        public static int DeriveSeed(int sessionSeed, string nodeId, int depth)
        {
            ulong hash = DeterministicRandom.StableHash64("rrpg-encounter-seed/1");
            hash = Mix(hash, DeterministicRandom.StableHash64(nodeId ?? string.Empty));
            hash = Mix(hash, (ulong)(uint)sessionSeed);
            hash = Mix(hash, (ulong)(uint)depth);
            return unchecked((int)Finalize(hash));
        }

        private static ulong Mix(ulong accumulator, ulong value)
        {
            return Finalize(accumulator ^ (value + GoldenGamma + (accumulator << 6) + (accumulator >> 2)));
        }

        private static ulong Finalize(ulong x)
        {
            x ^= x >> 30;
            x *= 0xBF58476D1CE4E5B9UL;
            x ^= x >> 27;
            x *= 0x94D049BB133111EBUL;
            x ^= x >> 31;
            return x;
        }
    }
}
