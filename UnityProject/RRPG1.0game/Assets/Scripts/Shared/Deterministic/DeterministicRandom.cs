using System.Collections.Generic;
using System.Globalization;

namespace RRPG.Deterministic
{
    /// <summary>
    /// **确定性随机源**（Seeded / Deterministic Random）—— 全仓**唯一**算法实现。
    ///
    /// 为什么自建：仓库中没有可复用的带种子随机设施；`UnityEngine.Random` 是全局状态
    /// （不可复现、不可控）；`System.Random` 的实现随运行时而变（跨版本不保证序列一致）。
    /// 本类只用纯整数运算（SplitMix64），保证「同 Seed + 同调用序列 → 同结果」，
    /// 且不依赖任何引擎状态、时间、Guid 或平台差异。
    ///
    /// 历史与边界（WP-Event-1B 抽取，2026-09-26）：
    ///   - 算法原本位于 `RRPG.Encounter.DeterministicRandom`（WP-Encounter-1B）。
    ///     Event Resolver 也需要**同一套**确定性随机，但架构硬约束 **不得** 让 Event 依赖
    ///     Encounter Gameplay 程序集；因此算法抽到本共享纯逻辑程序集（单一真相源），
    ///     `RRPG.Encounter.DeterministicRandom` 改成本类的**兼容子类**（Encounter 调用点零改动）。
    ///   - 本程序集 `RRPG.Deterministic.Runtime` 是纯逻辑（`noEngineReferences = true`），
    ///     不引用 UnityEngine / UI / Editor / SaveSystem。
    ///
    /// 明确禁止：`UnityEngine.Random`、时间、Guid、字典遍历顺序 —— 可复现性依赖这条约束。
    /// </summary>
    public class DeterministicRandom
    {
        private ulong _state;

        /// <summary>用 int Seed 构造（内部先做一次混合，避免小种子产生相邻序列）。</summary>
        public DeterministicRandom(int seed)
        {
            _state = Mix((ulong)(uint)seed * 0x9E3779B97F4A7C15UL);
        }

        /// <summary>用 64 位状态构造（供 <see cref="Derive"/> 派生独立子流）。</summary>
        public DeterministicRandom(ulong state)
        {
            _state = Mix(state);
        }

        /// <summary>SplitMix64 状态推进；返回下一个 64 位随机数。</summary>
        public ulong NextULong()
        {
            _state += 0x9E3779B97F4A7C15UL;
            ulong z = _state;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }

        public uint NextUInt()
        {
            return (uint)(NextULong() >> 32);
        }

        /// <summary>[0, maxExclusive) 的整数（maxExclusive &lt;= 0 时返回 0）。</summary>
        public int NextIndex(int maxExclusive)
        {
            if (maxExclusive <= 0) return 0;
            return (int)(NextUInt() % (uint)maxExclusive);
        }

        /// <summary>[minInclusive, maxInclusive] 的整数（区间反置时自动交换）。</summary>
        public int NextInclusive(int minInclusive, int maxInclusive)
        {
            if (minInclusive > maxInclusive)
            {
                int swap = minInclusive;
                minInclusive = maxInclusive;
                maxInclusive = swap;
            }
            long span = (long)maxInclusive - minInclusive + 1L;
            if (span <= 1L) return minInclusive;
            return minInclusive + (int)(NextULong() % (ulong)span);
        }

        /// <summary>
        /// 派生一条**独立子流**：让「遭遇抽取 / 数量选择 / 怪物选择 / 站位求解」各自拥有互不干扰的
        /// 随机序列（同一 Seed 下每段都稳定；某一段搜索深度变化不会污染另一段）。
        /// </summary>
        public DeterministicRandom Derive(string purpose)
        {
            return new DeterministicRandom(
                StableHash64(purpose + "#" + _state.ToString("x16", CultureInfo.InvariantCulture)));
        }

        /// <summary>0..count-1 的排列（Fisher-Yates；只由本流决定）。</summary>
        public List<int> Permutation(int count)
        {
            var order = new List<int>(count < 0 ? 0 : count);
            for (int i = 0; i < count; i++) order.Add(i);

            for (int i = order.Count - 1; i > 0; i--)
            {
                int j = NextIndex(i + 1);
                int tmp = order[i];
                order[i] = order[j];
                order[j] = tmp;
            }
            return order;
        }

        /// <summary>[minInclusive, maxInclusive] 值的排列（例如 Count 1..3 → 可能顺序 2,3,1）。</summary>
        public List<int> RangePermutation(int minInclusive, int maxInclusive)
        {
            if (minInclusive > maxInclusive)
            {
                int swap = minInclusive;
                minInclusive = maxInclusive;
                maxInclusive = swap;
            }

            var values = new List<int>();
            for (int v = minInclusive; v <= maxInclusive; v++) values.Add(v);

            for (int i = values.Count - 1; i > 0; i--)
            {
                int j = NextIndex(i + 1);
                int tmp = values[i];
                values[i] = values[j];
                values[j] = tmp;
            }
            return values;
        }

        /// <summary>
        /// 按权重做**无放回**加权抽取，得到一个「确定性候选顺序」（Weighted Permutation）。
        ///
        /// 语义（WP-Encounter-1B §6 / §11）：
        ///   - 权重越大越可能排在前面，但**所有候选都会被排进顺序里**：搜索可以从「随机最优候选」
        ///     继续退到其它候选，而不是「抽一次失败就整体失败」；
        ///   - 同权重时按输入顺序稳定取（不依赖字典遍历顺序）。
        /// </summary>
        public List<T> WeightedPermutation<T>(IReadOnlyList<T> items, IReadOnlyList<int> weights)
        {
            var result = new List<T>();
            if (items == null || items.Count == 0) return result;

            var remaining = new List<int>();
            for (int i = 0; i < items.Count; i++) remaining.Add(i);

            while (remaining.Count > 0)
            {
                long total = 0;
                for (int i = 0; i < remaining.Count; i++) total += WeightAt(weights, remaining[i]);

                int pick;
                if (total > 0)
                {
                    ulong roll = NextULong() % (ulong)total;
                    long acc = 0;
                    pick = remaining.Count - 1;
                    for (int i = 0; i < remaining.Count; i++)
                    {
                        acc += WeightAt(weights, remaining[i]);
                        if (roll < (ulong)acc) { pick = i; break; }
                    }
                }
                else
                {
                    pick = NextIndex(remaining.Count);   // 全 0 权重（配置非法）→ 退化为均匀，不静默丢候选
                }

                result.Add(items[remaining[pick]]);
                remaining.RemoveAt(pick);
            }

            return result;
        }

        /// <summary>
        /// 稳定 64 位字符串哈希（FNV-1a）：用于从 (Seed, EncounterId, SlotId …) 派生确定性子 Seed。
        /// 不使用 string.GetHashCode（其实现不保证跨运行 / 跨版本稳定）。
        /// </summary>
        public static ulong StableHash64(string text)
        {
            const ulong offset = 14695981039346656037UL;
            const ulong prime = 1099511628211UL;

            ulong hash = offset;
            if (text == null) return hash;

            for (int i = 0; i < text.Length; i++)
            {
                hash ^= text[i];
                hash *= prime;
            }
            return hash;
        }

        private static int WeightAt(IReadOnlyList<int> weights, int index)
        {
            if (weights == null || index >= weights.Count) return 1;
            int w = weights[index];
            return w > 0 ? w : 0;
        }

        private static ulong Mix(ulong z)
        {
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }
    }
}
