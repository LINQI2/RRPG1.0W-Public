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
    /// **长期内容守卫**（WP-Encounter-1C §3 / C10）：
    /// 所有 `Encounter.Enabled = true` 的遭遇都必须至少存在**一个合法完整 EnemyRoster 解**。
    ///
    /// 为什么不写死数量 / ID：制作人会正常增删遭遇与怪物，快照型断言会把「正常内容变化」误判为回归。
    /// 本守卫只读**当前正式 Authority**（Encounter 静态配置 + Monster Registry），对每个启用遭遇
    /// 用固定测试 Seed 调用 `GenerateEncounter`；1B Generator 是完整求解（不是一次抽取），
    /// 因此「存在合法解 ⇒ 必然成功」—— 任一 Enabled 遭遇永久无解即 FAIL，并报告
    /// EncounterId / Failure Code / Diagnostic。
    ///
    /// Enabled = false（草稿）不要求可生成。
    /// </summary>
    public class EncounterContentSolvabilityGuardTests
    {
        /// <summary>固定测试 Seed（不含时间 / 随机，保证可复现）。</summary>
        private static readonly int[] FixedSeeds = { 1, 20260925 };

        [SetUp]
        public void SetUp()
        {
            StaticConfigRegistry.Reset();
            Assert.IsTrue(StaticConfigBuilder.Build(false).Success, "前置：静态配置生成根必须可构建");
        }

        [TearDown]
        public void TearDown()
        {
            StaticConfigRegistry.Reset();
        }

        [Test]
        public void AllEnabledEncounters_HaveAtLeastOneLegalRosterSolution()
        {
            string diagnostic;
            var input = EncounterGenerationInput.FromStaticConfig(out diagnostic);
            Assert.IsNotNull(input, "当前正式 Encounter / Monster 静态配置必须可用：" + diagnostic);

            var generator = new EncounterGenerator(input);
            var evidence = new StringBuilder();
            var failures = new List<string>();

            int enabled = 0;
            int disabled = 0;

            foreach (var encounter in input.Encounters)
            {
                if (encounter == null || string.IsNullOrEmpty(encounter.EncounterId)) continue;

                if (!encounter.Enabled)
                {
                    disabled++;
                    evidence.Append("SKIP  ").Append(encounter.EncounterId)
                        .Append("：Enabled=false（草稿，不要求可生成）\n");
                    continue;
                }

                enabled++;
                foreach (int seed in FixedSeeds)
                {
                    var result = generator.GenerateEncounter(encounter.EncounterId, seed);

                    if (!result.Success)
                    {
                        failures.Add("  EncounterId=" + encounter.EncounterId
                            + "  Seed=" + seed
                            + "  Failure=" + result.Failure
                            + "  Diagnostic=" + OneLine(result.Diagnostic));
                        continue;
                    }

                    AssertRosterIsLegal(encounter, result.Roster, seed);
                    evidence.Append("OK    ").Append(encounter.EncounterId)
                        .Append(" seed=").Append(seed)
                        .Append(" → ").Append(Describe(result.Roster)).Append('\n');
                }
            }

            Assert.Greater(enabled, 0, "当前正式配置至少应有 1 个 Enabled 遭遇（否则本守卫失去意义）");

            Debug.Log("[Encounter 1C Enabled 可解性守卫] Enabled=" + enabled
                + "；Disabled（草稿，跳过）=" + disabled + "\n" + evidence);

            if (failures.Count > 0)
            {
                Assert.Fail("以下 Enabled 遭遇在固定 Seed 下无法生成合法阵容（永久无解 = 内容或规则必须修）：\n"
                    + string.Join("\n", failures));
            }
        }

        /// <summary>轻量合法性复核（生成器已保证，这里防止「回归成残缺阵容」）。</summary>
        private static void AssertRosterIsLegal(EncounterDefinition encounter, EnemyRoster roster, int seed)
        {
            Assert.IsNotNull(roster, "成功必须带完整 EnemyRoster：" + encounter.EncounterId);
            Assert.Greater(roster.Count, 0, "阵容不得为空：" + encounter.EncounterId);
            Assert.LessOrEqual(roster.Count, EnemyBoardFormation.MaxEnemyUnits, "阵容不得超过 3×3 容量");

            Assert.GreaterOrEqual(roster.TotalThreat, encounter.ThreatBudgetMin,
                "总威胁不得低于最低预算：" + encounter.EncounterId);
            Assert.LessOrEqual(roster.TotalThreat, encounter.ThreatBudgetMax,
                "总威胁不得超过最高预算：" + encounter.EncounterId);

            var instanceIds = new List<string>();
            var cells = new List<string>();
            foreach (var entry in roster.Entries)
            {
                Assert.IsNotNull(entry, "阵容项不得为 null：" + encounter.EncounterId);
                Assert.IsNotEmpty(entry.EnemyInstanceId, "EnemyInstanceId 不得为空");
                Assert.IsNotEmpty(entry.MonsterId, "MonsterId 不得为空");

                instanceIds.Add(entry.EnemyInstanceId);
                cells.Add(entry.Row + ":" + entry.Column);
            }

            CollectionAssert.AllItemsAreUnique(instanceIds, "EnemyInstanceId 必须唯一（seed=" + seed + "）");
            CollectionAssert.AllItemsAreUnique(cells, "每个 (排, 列) 最多 1 只（seed=" + seed + "）");
        }

        private static string Describe(EnemyRoster roster)
        {
            var builder = new StringBuilder();
            builder.Append(roster.Count).Append(" 只 / 威胁 ").Append(roster.TotalThreat).Append(" / ");

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
