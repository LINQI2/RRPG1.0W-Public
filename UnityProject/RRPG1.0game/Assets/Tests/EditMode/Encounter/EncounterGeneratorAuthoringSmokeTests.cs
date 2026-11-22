using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using RRPG.Encounter;
using RRPG.StaticConfig;
using RRPG.StaticConfig.EditorTools;
using UnityEngine;

namespace RRPG.Encounter.Tests
{
    /// <summary>
    /// **当前正式配置冒烟测试**（WP-Encounter-1B §20.B）。
    ///
    /// 与契约测试的分工：这里**不写死内容**（不假设有 4 个遭遇 / 6 只怪 / 某个 ID 永远存在），
    /// 而是从当前 Authority 自动发现「启用的池 → 该池在某个深度下的合法条目 → 启用的遭遇」，
    /// 再用若干固定 Seed 尝试生成，并要求至少成功生成一份**合法 EnemyRoster**。
    ///
    /// 如果当前正式配置本身一个都生成不出来：这是**真实配置问题**，测试必须失败并报告，
    /// 不允许为了让测试变绿而放松生成规则。
    /// </summary>
    public class EncounterGeneratorAuthoringSmokeTests
    {
        private static readonly int[] Seeds = { 1, 20260925, 987654321 };

        [SetUp]
        public void SetUp()
        {
            Assert.IsTrue(StaticConfigBuilder.Build(false).Success, "前置：静态配置生成根必须可构建");
            StaticConfigRegistry.Reset();
        }

        [TearDown]
        public void TearDown()
        {
            StaticConfigRegistry.Reset();
        }

        [Test]
        public void CurrentAuthoring_EnabledPools_ProduceAtLeastOneValidRoster()
        {
            string diagnostic;
            var input = EncounterGenerationInput.FromStaticConfig(out diagnostic);
            Assert.IsNotNull(input, "当前正式静态配置必须可用：" + diagnostic);

            var generator = new EncounterGenerator(input);

            MonsterRegistry monsters;
            string monsterDiagnostic;
            Assert.IsTrue(StaticConfigRegistry.TryGetMonsters(out monsters, out monsterDiagnostic), monsterDiagnostic);

            var evidence = new StringBuilder();
            int attempts = 0;
            int successes = 0;
            int pools = 0;

            foreach (var pool in input.Pools)
            {
                if (pool == null || string.IsNullOrEmpty(pool.PoolId)) continue;
                if (!pool.Enabled)
                {
                    evidence.Append("池 ").Append(pool.PoolId).Append("：未启用（按规则不参与生成）\n");
                    continue;
                }

                pools++;
                var depths = DepthsOf(input, pool.PoolId);
                if (depths.Count == 0)
                {
                    evidence.Append("池 ").Append(pool.PoolId).Append("：没有任何 PoolEntry\n");
                    continue;
                }

                bool poolSucceeded = false;
                foreach (int depth in depths)
                {
                    foreach (int seed in Seeds)
                    {
                        attempts++;
                        var result = generator.GenerateFromPool(pool.PoolId, depth, seed);

                        if (!result.Success)
                        {
                            evidence.Append("池 ").Append(pool.PoolId).Append(" @depth=").Append(depth)
                                .Append(" seed=").Append(seed).Append(" → ").Append(result.Failure)
                                .Append("：").Append(OneLine(result.Diagnostic)).Append('\n');
                            continue;
                        }

                        successes++;
                        poolSucceeded = true;

                        foreach (var entry in result.Roster.Entries)
                        {
                            MonsterDefinition definition;
                            Assert.IsTrue(monsters.TryGetById(entry.MonsterId, out definition),
                                "生成出的 MonsterId 必须能在 Monster Registry 解析：" + entry.MonsterId);
                        }

                        EncounterRosterAssert.AssertValid(result, input);

                        var again = generator.GenerateFromPool(pool.PoolId, depth, seed);
                        Assert.IsTrue(again.Success, again.Diagnostic);
                        EncounterRosterAssert.AssertIdentical(result.Roster, again.Roster);

                        evidence.Append("池 ").Append(pool.PoolId).Append(" @depth=").Append(depth)
                            .Append(" seed=").Append(seed).Append(" → OK：").Append(Describe(result.Roster))
                            .Append('\n');
                        break;
                    }

                    if (poolSucceeded) break;
                }

                if (!poolSucceeded)
                {
                    evidence.Append("池 ").Append(pool.PoolId).Append("：所有深度 / Seed 组合都无法生成\n");
                }
            }

            Debug.Log("[Encounter 1B Smoke] 启用的池 = " + pools + "；尝试次数 = " + attempts
                + "；成功次数 = " + successes + "\n" + evidence);

            Assert.Greater(pools, 0, "当前正式配置至少应有 1 个启用的遭遇池（否则 1B 无输入）");
            Assert.Greater(successes, 0,
                "当前正式配置必须至少能生成一份合法 EnemyRoster（否则属真实配置问题，不得改绿）：\n" + evidence);
        }

        /// <summary>
        /// §18：在**真实配置**上，任何一次尝试都必须是「成功 + 完整阵容」或「明确失败 + 可操作诊断」。
        /// 不允许出现「失败但阵容非空 / 诊断为空 / 失败码为 None」的静默状态。
        /// 同时把每个池 / 深度 / Seed 的结果写进 Console，作为当前内容的实际生成证据。
        /// </summary>
        [Test]
        public void CurrentAuthoring_EveryPoolAttempt_IsExplicitSuccessOrExplicitFailure()
        {
            string diagnostic;
            var input = EncounterGenerationInput.FromStaticConfig(out diagnostic);
            Assert.IsNotNull(input, "当前正式静态配置必须可用：" + diagnostic);

            var generator = new EncounterGenerator(input);
            var evidence = new StringBuilder();
            int success = 0;
            int failure = 0;

            foreach (var pool in input.Pools)
            {
                if (pool == null || !pool.Enabled || string.IsNullOrEmpty(pool.PoolId)) continue;

                var depths = DepthsOf(input, pool.PoolId);
                if (depths.Count == 0) depths.Add(0);

                foreach (int depth in depths)
                {
                    foreach (int seed in Seeds)
                    {
                        var result = generator.GenerateFromPool(pool.PoolId, depth, seed);

                        if (result.Success)
                        {
                            success++;
                            Assert.IsNotNull(result.Roster, "成功必须带完整 EnemyRoster");
                            Assert.AreEqual(EncounterGenerationFailure.None, result.Failure);
                            evidence.Append("OK   ").Append(pool.PoolId).Append(" @").Append(depth)
                                .Append(" seed=").Append(seed).Append(" → ").Append(Describe(result.Roster)).Append('\n');
                            continue;
                        }

                        failure++;
                        Assert.IsNull(result.Roster, "失败时不得返回残缺阵容：" + pool.PoolId);
                        Assert.AreNotEqual(EncounterGenerationFailure.None, result.Failure,
                            "失败必须给出明确失败码：" + pool.PoolId);
                        Assert.IsNotEmpty(result.Diagnostic, "失败必须给出可操作诊断：" + pool.PoolId);

                        evidence.Append("FAIL ").Append(pool.PoolId).Append(" @").Append(depth)
                            .Append(" seed=").Append(seed).Append(" → ").Append(result.Failure)
                            .Append("：").Append(OneLine(result.Diagnostic)).Append('\n');
                    }
                }
            }

            Debug.Log("[Encounter 1B Authoring Attempts] 成功 = " + success + "；明确失败 = " + failure + "\n" + evidence);
            Assert.Greater(success + failure, 0, "至少应有一次尝试（否则当前配置没有可取的池 / 深度）");
        }

        /// <summary>该池在正式配置里出现过的合法深度（取条目边界与中点；边界 inclusive）。</summary>
        private static List<int> DepthsOf(EncounterGenerationInput input, string poolId)
        {
            var depths = new List<int>();
            foreach (var entry in input.PoolEntries)
            {
                if (entry == null || entry.PoolId != poolId) continue;
                if (entry.MaxDepth < entry.MinDepth) continue;

                AddUnique(depths, entry.MinDepth);
                AddUnique(depths, entry.MaxDepth);
                AddUnique(depths, (entry.MinDepth + entry.MaxDepth) / 2);
            }
            depths.Sort();
            return depths;
        }

        private static void AddUnique(List<int> values, int value)
        {
            if (!values.Contains(value)) values.Add(value);
        }

        private static string Describe(EnemyRoster roster)
        {
            var builder = new StringBuilder();
            builder.Append(roster.EncounterId).Append(" / ").Append(roster.Count).Append(" 只 / 威胁 ")
                .Append(roster.TotalThreat).Append(" / ");

            foreach (var entry in roster.Entries)
            {
                builder.Append(entry.MonsterId).Append('@').Append(entry.Row).Append('-').Append(entry.Column)
                    .Append(' ');
            }
            return builder.ToString();
        }

        private static string OneLine(string text)
        {
            return text == null ? string.Empty : text.Replace("\n", " ").Replace("\r", " ");
        }

    }
}
