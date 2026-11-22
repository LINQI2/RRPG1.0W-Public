using System.Collections.Generic;
using NUnit.Framework;
using RRPG.Deterministic;

namespace RRPG.Event.Tests
{
    /// <summary>
    /// WP-Event-1B：**确定性随机源单一真相源守卫**。
    ///
    /// 背景：算法原本只存在于 `RRPG.Encounter.DeterministicRandom`；1B 把它抽到共享程序集
    /// `RRPG.Deterministic.Runtime`，Encounter 的类型改为其兼容子类。
    /// 本测试守住「两边不是两套实现」：同一 Seed 必须产生逐位相同的序列。
    /// </summary>
    public class DeterministicRandomSharedTests
    {
        [Test]
        public void EncounterWrapper_AndSharedImplementation_ProduceIdenticalSequences()
        {
            var shared = new RRPG.Deterministic.DeterministicRandom(12345);
            var encounter = new RRPG.Encounter.DeterministicRandom(12345);

            for (int i = 0; i < 128; i++)
            {
                Assert.AreEqual(shared.NextULong(), encounter.NextULong(), "第 " + i + " 个随机数必须一致");
            }

            var sharedIndex = new RRPG.Deterministic.DeterministicRandom(777);
            var encounterIndex = new RRPG.Encounter.DeterministicRandom(777);

            for (int i = 0; i < 64; i++)
            {
                Assert.AreEqual(sharedIndex.NextIndex(7), encounterIndex.NextIndex(7));
            }
        }

        [Test]
        public void DerivedStreams_AndPermutations_Match()
        {
            var shared = new RRPG.Deterministic.DeterministicRandom(99).Derive("role-order|evt|role");
            var encounter = new RRPG.Encounter.DeterministicRandom(99).Derive("role-order|evt|role");

            CollectionAssert.AreEqual(shared.Permutation(6), encounter.Permutation(6));

            var sharedWeights = new RRPG.Deterministic.DeterministicRandom(3);
            var encounterWeights = new RRPG.Encounter.DeterministicRandom(3);

            var items = new List<string> { "a", "b", "c", "d" };
            var weights = new List<int> { 10, 1, 5, 5 };

            CollectionAssert.AreEqual(
                sharedWeights.WeightedPermutation(items, weights),
                encounterWeights.WeightedPermutation(items, weights));
        }

        [Test]
        public void StableHash64_IsStable_AndShared()
        {
            ulong shared = RRPG.Deterministic.DeterministicRandom.StableHash64("evtinst|evt_a|1");
            ulong encounter = RRPG.Encounter.DeterministicRandom.StableHash64("evtinst|evt_a|1");

            Assert.AreEqual(shared, encounter);
            Assert.AreEqual(shared, RRPG.Deterministic.DeterministicRandom.StableHash64("evtinst|evt_a|1"),
                "同一个字符串必须得到同一个哈希（跨调用稳定）");
            Assert.AreNotEqual(shared, RRPG.Deterministic.DeterministicRandom.StableHash64("evtinst|evt_b|1"));
        }
    }
}
