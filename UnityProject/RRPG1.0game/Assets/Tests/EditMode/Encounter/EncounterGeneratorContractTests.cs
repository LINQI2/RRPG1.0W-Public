using System.Collections.Generic;
using NUnit.Framework;
using RRPG.Encounter;

namespace RRPG.Encounter.Tests
{
    /// <summary>
    /// WP-Encounter-1B：**纯契约测试**（G1–G18）。全部使用 <see cref="EncounterFixture"/> 固定测试数据，
    /// 不依赖制作人当前 6 怪内容（因此内容正常增长不会让这些用例失败）。
    ///
    /// 观察方式：先建立夹具 → 用多个 Seed 反复生成 → 断言**硬性质**（范围 / 集合 / 唯一性 / 可复现性），
    /// 而不是「某个 Seed 必须抽到某个具体结果」。
    /// </summary>
    public class EncounterGeneratorContractTests
    {
        private const int SeedSpread = 12;

        // ---------------- G1：Seed 确定性 ----------------

        [Test]
        public void G1_SameSeed_SameConfig_ProducesIdenticalRoster()
        {
            var fixture = new EncounterFixture()
                .Encounter(1, 30, allowDuplicate: true)
                .Monster("fx_melee", 2, "Normal", 100, "Melee")
                .Monster("fx_range", 3, "Normal", 80, "Ranged")
                .Monster("fx_tank", 4, "Elite", 40, "Tank")
                .Slot("fx_slot", "Query", 1, 3, "Any");

            var input = fixture.Build();
            var generator = new EncounterGenerator(input);

            var first = generator.GenerateEncounter("fx_enc", 20260925);
            var second = generator.GenerateEncounter("fx_enc", 20260925);
            var third = new EncounterGenerator(input).GenerateEncounter("fx_enc", 20260925);

            EncounterRosterAssert.AssertValid(first, input);
            EncounterRosterAssert.AssertValid(second, input);
            EncounterRosterAssert.AssertValid(third, input);

            EncounterRosterAssert.AssertIdentical(first.Roster, second.Roster);
            EncounterRosterAssert.AssertIdentical(first.Roster, third.Roster);
        }

        [Test]
        public void G1b_DifferentSeed_MayDiffer_ButStaysValid()
        {
            var fixture = new EncounterFixture()
                .Encounter(1, 30, allowDuplicate: true)
                .Monster("fx_a", 2, "Normal", 100, "Melee")
                .Monster("fx_b", 3, "Normal", 100, "Ranged")
                .Monster("fx_c", 4, "Elite", 100, "Tank")
                .Slot("fx_slot", "Query", 1, 3, "Any");

            var input = fixture.Build();
            var generator = new EncounterGenerator(input);

            var signatures = new HashSet<string>();
            for (int seed = 1; seed <= SeedSpread; seed++)
            {
                var result = generator.GenerateEncounter("fx_enc", seed);
                EncounterRosterAssert.AssertValid(result, input);

                var builder = new System.Text.StringBuilder();
                foreach (var entry in result.Roster.Entries)
                {
                    builder.Append(entry.MonsterId).Append('@').Append(entry.Row).Append(entry.Column).Append(';');
                }
                signatures.Add(builder.ToString());
            }

            Assert.Greater(signatures.Count, 1, "不同 Seed 应能产生不同的合法结果（否则随机性没有真正参与）");
        }

        // ---------------- G2 / G3 / G4 / G5：筛选规则 ----------------

        /// <summary>G2：Query + Require Role=Ranged → 任何 Seed 都不会抽出纯近战。</summary>
        [Test]
        public void G2_QueryRole_NeverPicksNonMatchingMonster()
        {
            var fixture = new EncounterFixture()
                .Encounter(1, 9, allowDuplicate: true)
                .Monster("fx_melee", 1, "Normal", 100, "Melee")
                .Monster("fx_range", 1, "Normal", 100, "Ranged")
                .Slot("fx_slot", "Query", 1, 3, "Any")
                .Rule("fx_slot", "Require", "Role", "Ranged");

            var input = fixture.Build();
            var gen = new EncounterGenerator(input);

            for (int seed = 1; seed <= SeedSpread; seed++)
            {
                var result = gen.GenerateEncounter("fx_enc", seed);
                EncounterRosterAssert.AssertValid(result, input);

                CollectionAssert.AreEquivalent(new[] { "fx_range" },
                    EncounterRosterAssert.MonsterIdsOf(result.Roster),
                    "Require Role=Ranged 时只允许 fx_range（seed=" + seed + "）");
            }
        }

        /// <summary>G3-a：同 Dimension 多条 AnyOf = OR（Role=Melee 或 Tank；绝不出 Ranged）。</summary>
        [Test]
        public void G3a_AnyOf_SameDimension_IsOr()
        {
            var input = new EncounterFixture()
                .Encounter(1, 9, allowDuplicate: true)
                .Monster("fx_melee", 1, "Normal", 100, "Melee")
                .Monster("fx_tank", 1, "Normal", 100, "Tank")
                .Monster("fx_range", 1, "Normal", 100, "Ranged")
                .Slot("fx_slot", "Query", 1, 2, "Any")
                .Rule("fx_slot", "AnyOf", "Role", "Melee")
                .Rule("fx_slot", "AnyOf", "Role", "Tank")
                .Build();

            var allowed = new HashSet<string> { "fx_melee", "fx_tank" };

            for (int seed = 1; seed <= SeedSpread; seed++)
            {
                var result = new EncounterGenerator(input).GenerateEncounter("fx_enc", seed);
                EncounterRosterAssert.AssertValid(result, input);

                foreach (string monsterId in EncounterRosterAssert.MonsterIdsOf(result.Roster))
                {
                    Assert.IsTrue(allowed.Contains(monsterId), "AnyOf 只允许 Melee 或 Tank，实际：" + monsterId);
                }
            }
        }

        /// <summary>G3-b：同 Dimension 多条 Require = AND（必须同时具备 Melee 与 Tank）。</summary>
        [Test]
        public void G3b_Require_SameDimension_IsAnd()
        {
            var input = new EncounterFixture()
                .Encounter(1, 9, allowDuplicate: true)
                .Monster("fx_both", 1, "Normal", 100, "Melee", "Tank")
                .Monster("fx_melee", 1, "Normal", 100, "Melee")
                .Monster("fx_tank", 1, "Normal", 100, "Tank")
                .Slot("fx_slot", "Query", 1, 2, "Any")
                .Rule("fx_slot", "Require", "Role", "Melee")
                .Rule("fx_slot", "Require", "Role", "Tank")
                .Build();

            for (int seed = 1; seed <= SeedSpread; seed++)
            {
                var result = new EncounterGenerator(input).GenerateEncounter("fx_enc", seed);
                EncounterRosterAssert.AssertValid(result, input);

                CollectionAssert.AreEquivalent(new[] { "fx_both" },
                    EncounterRosterAssert.MonsterIdsOf(result.Roster),
                    "Require Melee + Tank 只允许同时具备两个定位的怪（seed=" + seed + "）");
            }
        }

        /// <summary>G3-c：Exclude 优先于其它规则（命中即排除）。</summary>
        [Test]
        public void G3c_Exclude_WinsOverOthers()
        {
            var input = new EncounterFixture()
                .Encounter(1, 9, allowDuplicate: true)
                .Monster("fx_melee", 1, "Normal", 100, "Melee")
                .Monster("fx_range", 1, "Normal", 100, "Ranged")
                .Slot("fx_slot", "Query", 1, 2, "Any")
                .Rule("fx_slot", "AnyOf", "Role", "Melee")
                .Rule("fx_slot", "AnyOf", "Role", "Ranged")
                .Rule("fx_slot", "Exclude", "Role", "Melee")
                .Build();

            for (int seed = 1; seed <= SeedSpread; seed++)
            {
                var result = new EncounterGenerator(input).GenerateEncounter("fx_enc", seed);
                EncounterRosterAssert.AssertValid(result, input);

                CollectionAssert.AreEquivalent(new[] { "fx_range" },
                    EncounterRosterAssert.MonsterIdsOf(result.Roster),
                    "Exclude Melee 必须压过 AnyOf（seed=" + seed + "）");
            }
        }

        /// <summary>G4：Tier 维度筛选（Normal / Elite / Boss）。</summary>
        [Test]
        public void G4_Tier_FiltersCorrectly()
        {
            var input = new EncounterFixture()
                .Encounter(1, 9, allowDuplicate: true)
                .Monster("fx_normal", 1, "Normal", 100, "Melee")
                .Monster("fx_elite", 1, "Elite", 100, "Melee")
                .Monster("fx_boss", 1, "Boss", 100, "Melee")
                .Slot("fx_slot", "Query", 1, 2, "Any")
                .Rule("fx_slot", "Require", "Tier", "Elite")
                .Build();

            for (int seed = 1; seed <= SeedSpread; seed++)
            {
                var result = new EncounterGenerator(input).GenerateEncounter("fx_enc", seed);
                EncounterRosterAssert.AssertValid(result, input);

                CollectionAssert.AreEquivalent(new[] { "fx_elite" },
                    EncounterRosterAssert.MonsterIdsOf(result.Roster), "Require Tier=Elite（seed=" + seed + "）");
            }
        }

        /// <summary>G5：MonsterId 维度可以精确包含（Require）与精确排除（Exclude）。</summary>
        [Test]
        public void G5_MonsterId_IncludeAndExclude()
        {
            var requireInput = new EncounterFixture()
                .Encounter(1, 9, allowDuplicate: true)
                .Monster("fx_pick", 1, "Normal", 100, "Melee")
                .Monster("fx_other", 1, "Normal", 100, "Melee")
                .Slot("fx_slot", "Query", 1, 2, "Any")
                .Rule("fx_slot", "Require", "MonsterId", "fx_pick")
                .Build();

            for (int seed = 1; seed <= 6; seed++)
            {
                var result = new EncounterGenerator(requireInput).GenerateEncounter("fx_enc", seed);
                EncounterRosterAssert.AssertValid(result, requireInput);
                CollectionAssert.AreEquivalent(new[] { "fx_pick" },
                    EncounterRosterAssert.MonsterIdsOf(result.Roster), "Require MonsterId=fx_pick");
            }

            var excludeInput = new EncounterFixture()
                .Encounter(1, 9, allowDuplicate: true)
                .Monster("fx_bad", 1, "Normal", 100, "Melee")
                .Monster("fx_good", 1, "Normal", 100, "Melee")
                .Slot("fx_slot", "Query", 1, 3, "Any")
                .Rule("fx_slot", "Exclude", "MonsterId", "fx_bad")
                .Build();

            for (int seed = 1; seed <= 6; seed++)
            {
                var result = new EncounterGenerator(excludeInput).GenerateEncounter("fx_enc", seed);
                EncounterRosterAssert.AssertValid(result, excludeInput);

                foreach (string monsterId in EncounterRosterAssert.MonsterIdsOf(result.Roster))
                {
                    Assert.AreNotEqual("fx_bad", monsterId, "Exclude MonsterId=fx_bad 不得出现");
                }
            }
        }

        /// <summary>§8.5：Query 位没有任何规则 = 当前所有合法候选都可用（不是错误）。</summary>
        [Test]
        public void G_QueryWithoutRules_AcceptsAllCandidates()
        {
            var input = new EncounterFixture()
                .Encounter(1, 9, allowDuplicate: true)
                .Monster("fx_a", 1, "Normal", 100, "Melee")
                .Monster("fx_b", 1, "Normal", 100, "Ranged")
                .Slot("fx_slot", "Query", 2, 2, "Any")
                .Build();

            for (int seed = 1; seed <= 6; seed++)
            {
                var result = new EncounterGenerator(input).GenerateEncounter("fx_enc", seed);
                EncounterRosterAssert.AssertValid(result, input);
                Assert.AreEqual(2, result.Roster.Count);
            }
        }

        /// <summary>§8：没有 EncounterProfile 的怪物不进入候选（它也不允许出现在结果里）。</summary>
        [Test]
        public void G_QueryWithoutProfile_IsNotACandidate()
        {
            var input = new EncounterFixture()
                .Encounter(1, 9, allowDuplicate: true)
                .Monster("fx_legal", 1, "Normal", 100, "Melee")
                .MonsterWithoutProfile("fx_no_profile")
                .Slot("fx_slot", "Query", 1, 3, "Any")
                .Build();

            for (int seed = 1; seed <= 6; seed++)
            {
                var result = new EncounterGenerator(input).GenerateEncounter("fx_enc", seed);
                EncounterRosterAssert.AssertValid(result, input);

                foreach (string monsterId in EncounterRosterAssert.MonsterIdsOf(result.Roster))
                {
                    Assert.AreNotEqual("fx_no_profile", monsterId, "没有 EncounterProfile 的怪不得成为候选");
                }
            }
        }

        // ---------------- G6 / G7：Duplicate ----------------

        /// <summary>G6：AllowDuplicateMonster = true → 同一 MonsterId 可以出现多次，但 InstanceId 必须不同。</summary>
        [Test]
        public void G6_DuplicateTrue_AllowsRepeats_WithUniqueInstanceIds()
        {
            var input = new EncounterFixture()
                .Encounter(6, 6, allowDuplicate: true)
                .Monster("fx_wolf", 2, "Normal", 100, "Melee")
                .Slot("fx_slot", "Query", 3, 3, "Any")
                .Build();

            var result = new EncounterGenerator(input).GenerateEncounter("fx_enc", 7);
            EncounterRosterAssert.AssertValid(result, input);

            Assert.AreEqual(3, result.Roster.Count);
            var instanceIds = new HashSet<string>();
            foreach (var entry in result.Roster.Entries)
            {
                Assert.AreEqual("fx_wolf", entry.MonsterId, "重复允许时三只都应是同一 MonsterId");
                Assert.IsTrue(instanceIds.Add(entry.EnemyInstanceId),
                    "同种怪多只时 EnemyInstanceId 必须唯一：" + entry.EnemyInstanceId);
                Assert.AreNotEqual(entry.MonsterId, entry.EnemyInstanceId,
                    "EnemyInstanceId 不得等于 MonsterId（实例身份 ≠ 模板身份）");
            }
        }

        /// <summary>G7：AllowDuplicateMonster = false → 整场（跨 Slot）不得出现重复 MonsterId。</summary>
        [Test]
        public void G7_DuplicateFalse_NoRepeatAcrossSlots()
        {
            // 两个位各 2 只、每只威胁 2 → 总威胁恒为 8
            var input = new EncounterFixture()
                .Encounter(8, 8, allowDuplicate: false)
                .Monster("fx_a", 2, "Normal", 100, "Melee")
                .Monster("fx_b", 2, "Normal", 100, "Ranged")
                .Monster("fx_c", 2, "Normal", 100, "Tank")
                .Monster("fx_d", 2, "Normal", 100, "Support")
                .Slot("fx_slot_1", "Query", 2, 2, "Any")
                .Slot("fx_slot_2", "Query", 2, 2, "Any")
                .Build();

            for (int seed = 1; seed <= SeedSpread; seed++)
            {
                var result = new EncounterGenerator(input).GenerateEncounter("fx_enc", seed);
                EncounterRosterAssert.AssertValid(result, input);
                EncounterRosterAssert.AssertNoDuplicateMonsterIds(result.Roster);
                Assert.AreEqual(4, result.Roster.Count, "两个位各 2 只（seed=" + seed + "）");
            }
        }

        /// <summary>G7b：Duplicate = false 且候选不够 → 明确失败（不放松规则）。</summary>
        [Test]
        public void G7b_DuplicateFalse_WhenCandidatesInsufficient_FailsExplicitly()
        {
            var input = new EncounterFixture()
                .Encounter(6, 6, allowDuplicate: false)
                .Monster("fx_a", 2, "Normal", 100, "Melee")
                .Monster("fx_b", 2, "Normal", 100, "Melee")
                .Slot("fx_slot", "Query", 3, 3, "Any")
                .Build();

            var result = new EncounterGenerator(input).GenerateEncounter("fx_enc", 3);

            Assert.IsFalse(result.Success, "只有 2 个候选却要 3 只且不允许重复 → 必须失败");
            Assert.IsNull(result.Roster, "失败时不得返回残缺阵容");
            Assert.AreEqual(EncounterGenerationFailure.DuplicateConflict, result.Failure,
                "应明确报告 Duplicate 约束无解，实际：" + result.Failure + " / " + result.Diagnostic);
        }

        // ---------------- G8 / G9：Threat 与 Count ----------------

        /// <summary>G8：有解时总威胁严格落在预算内；无解时明确失败。</summary>
        [Test]
        public void G8_ThreatBudget_Respected_AndUnsolvableFailsExplicitly()
        {
            var feasible = new EncounterFixture()
                .Encounter(5, 6, allowDuplicate: true)
                .Monster("fx_small", 1, "Normal", 100, "Melee")
                .Monster("fx_mid", 2, "Normal", 100, "Melee")
                .Monster("fx_big", 3, "Normal", 100, "Melee")
                .Slot("fx_slot", "Query", 1, 3, "Any")
                .Build();

            for (int seed = 1; seed <= SeedSpread; seed++)
            {
                var result = new EncounterGenerator(feasible).GenerateEncounter("fx_enc", seed);
                EncounterRosterAssert.AssertValid(result, feasible);   // 内部已断言预算区间
            }

            var impossible = new EncounterFixture()
                .Encounter(100, 200, allowDuplicate: true)
                .Monster("fx_only", 1, "Normal", 100, "Melee")
                .Slot("fx_slot", "Query", 1, 3, "Any")
                .Build();

            var failed = new EncounterGenerator(impossible).GenerateEncounter("fx_enc", 5);
            Assert.IsFalse(failed.Success, "预算 100~200 与最多 3 只 × 威胁 1 无解 → 必须失败");
            Assert.IsNull(failed.Roster);
            Assert.AreEqual(EncounterGenerationFailure.ThreatBudgetUnsolvable, failed.Failure,
                "应明确报告 Threat 预算无解，实际：" + failed.Failure + " / " + failed.Diagnostic);
        }

        /// <summary>
        /// G9：Count 范围属于**求解空间**（不是永远取 Min / Max）。
        /// 判据：所有结果的数量都必须落在 [Min,Max]；且不同 Seed 能观察到多于一种数量。
        /// </summary>
        [Test]
        public void G9_CountRange_IsPartOfSolutionSpace()
        {
            var input = new EncounterFixture()
                .Encounter(1, 30, allowDuplicate: true)
                .Monster("fx_only", 2, "Normal", 100, "Melee")
                .Slot("fx_slot", "Query", 1, 3, "Any")
                .Build();

            var counts = new HashSet<int>();
            for (int seed = 1; seed <= SeedSpread; seed++)
            {
                var result = new EncounterGenerator(input).GenerateEncounter("fx_enc", seed);
                EncounterRosterAssert.AssertValid(result, input);

                Assert.GreaterOrEqual(result.Roster.Count, 1, "数量必须 >= CountMin");
                Assert.LessOrEqual(result.Roster.Count, 3, "数量必须 <= CountMax");
                counts.Add(result.Roster.Count);
            }

            Assert.Greater(counts.Count, 1,
                "Count 必须真正参与求解（多个 Seed 应能观察到多个合法数量，实际只看到：" + string.Join(",", counts) + "）");
        }

        // ---------------- G10 / G11 / G12：站位 ----------------

        /// <summary>G10：Row 模式（两只远程指定 Back）→ 都在 Back，且列为 Left + Right。</summary>
        [Test]
        public void G10_RowMode_PlacesAllInRow_WithRuleLegalColumns()
        {
            var input = new EncounterFixture()
                .Encounter(2, 2, allowDuplicate: true)
                .Monster("fx_range", 1, "Normal", 100, "Ranged")
                .Slot("fx_back", "Query", 2, 2, "Row", "Back")
                .Build();

            var result = new EncounterGenerator(input).GenerateEncounter("fx_enc", 11);
            EncounterRosterAssert.AssertValid(result, input);

            var columns = new HashSet<EnemyRosterColumn>();
            foreach (var entry in result.Roster.Entries)
            {
                Assert.AreEqual(EnemyRosterRow.Back, entry.Row, "Row=Back 的两只都必须在前排…（应在 Back）");
                columns.Add(entry.Column);
            }

            CollectionAssert.AreEquivalent(
                new[] { EnemyRosterColumn.Left, EnemyRosterColumn.Right }, columns,
                "一排 2 人的合法列只有 Left + Right（不得使用 Center）");
        }

        /// <summary>G11：Any 模式 → 每排 ≤ 3、每排列结构合法（由共享不变量断言覆盖）。</summary>
        [Test]
        public void G11_AnyMode_ProducesLegalRowsAndColumns()
        {
            var input = new EncounterFixture()
                .Encounter(5, 5, allowDuplicate: true)
                .Monster("fx_any", 1, "Normal", 100, "Melee")
                .Slot("fx_any_slot", "Query", 5, 5, "Any")
                .Build();

            for (int seed = 1; seed <= SeedSpread; seed++)
            {
                var result = new EncounterGenerator(input).GenerateEncounter("fx_enc", seed);
                EncounterRosterAssert.AssertValid(result, input);

                // 5 只敌人分布在 3 排、每排 ≤ 3 → 必然存在某一排 > 1 只（证明列确实按「排内人数」重算）
                var perRow = new Dictionary<EnemyRosterRow, int>();
                foreach (var entry in result.Roster.Entries)
                {
                    int count;
                    perRow.TryGetValue(entry.Row, out count);
                    perRow[entry.Row] = count + 1;
                }

                bool hasMultiRow = false;
                foreach (var pair in perRow)
                {
                    if (pair.Value > 1) hasMultiRow = true;
                }
                Assert.IsTrue(hasMultiRow, "5 只敌人必须至少有一排 > 1 只（seed=" + seed + "）");
            }
        }

        /// <summary>
        /// G12：Exact 精确格必须结合**该排最终人数**判定：
        ///   1 人 → 只有 Center 合法；2 人 → 只有 Left + Right；3 人 → Left + Center + Right。
        /// </summary>
        [Test]
        public void G12_ExactMode_ValidatesAgainstFinalRowSize()
        {
            // 1 人 Center 合法
            AssertSuccess(ExactFixture(1, "Front|Center"), "1 人 Front Center");
            // 1 人 Left 无解（1 人必须 Center）
            AssertFails(ExactFixture(1, "Front|Left"), EncounterGenerationFailure.FormationUnsolvable,
                "1 人 Front Left");
            // 2 人 Left + Right 合法
            AssertSuccess(ExactFixture(2, "Front|Left", "Front|Right"), "2 人 Front Left+Right");
            // 2 人含 Center 无解
            AssertFails(ExactFixture(2, "Front|Left", "Front|Center"), EncounterGenerationFailure.FormationUnsolvable,
                "2 人 Front Left+Center");
            // 3 人 Left + Center + Right 合法
            AssertSuccess(ExactFixture(3, "Front|Left", "Front|Center", "Front|Right"), "3 人 Front L+C+R");
            // 非 Front 排同样规则（Back 1 人 → Center）
            AssertSuccess(ExactFixture(1, "Back|Center"), "1 人 Back Center");
        }

        private static EncounterFixture ExactFixture(int threat, params string[] rowColumnPairs)
        {
            var fixture = new EncounterFixture()
                .Encounter(threat, threat, allowDuplicate: true)
                .Monster("fx_fixed", 1, "Normal", 100, "Melee");

            for (int i = 0; i < rowColumnPairs.Length; i++)
            {
                string[] parts = rowColumnPairs[i].Split('|');
                fixture.Slot("fx_exact_" + i, "FixedMonster", 1, 1, "Exact", parts[0], parts[1], "fx_fixed");
            }
            return fixture;
        }

        private static void AssertSuccess(EncounterFixture fixture, string label)
        {
            var input = fixture.Build();
            var result = new EncounterGenerator(input).GenerateEncounter(fixture.EncounterId, 13);
            Assert.IsTrue(result.Success, label + " 必须成功，实际：" + result.Failure + " / " + result.Diagnostic);
            EncounterRosterAssert.AssertValid(result, input);
        }

        private static void AssertFails(EncounterFixture fixture, EncounterGenerationFailure expected, string label)
        {
            var result = new EncounterGenerator(fixture.Build()).GenerateEncounter(fixture.EncounterId, 13);
            Assert.IsFalse(result.Success, label + " 必须失败");
            Assert.IsNull(result.Roster, label + " 失败时不得返回残缺阵容");
            Assert.AreEqual(expected, result.Failure, label + " 的失败原因，实际：" + result.Failure
                + " / " + result.Diagnostic);
        }

        // ---------------- G13 / G14 / G18：规模上限、无解、InstanceId ----------------

        /// <summary>G13：9 只敌人（Front 3 + Middle 3 + Back 3）→ 9 个唯一 InstanceId + 9 个唯一格位。</summary>
        [Test]
        public void G13_NineEnemies_FillWholeBoard()
        {
            var input = new EncounterFixture()
                .Encounter(9, 9, allowDuplicate: true)
                .Monster("fx_any", 1, "Normal", 100, "Melee")
                .Slot("fx_front", "Query", 3, 3, "Row", "Front")
                .Slot("fx_middle", "Query", 3, 3, "Row", "Middle")
                .Slot("fx_back", "Query", 3, 3, "Row", "Back")
                .Build();

            var result = new EncounterGenerator(input).GenerateEncounter("fx_enc", 17);
            EncounterRosterAssert.AssertValid(result, input);

            Assert.AreEqual(EnemyBoardFormation.MaxEnemyUnits, result.Roster.Count);

            var instanceIds = new HashSet<string>();
            var positions = new HashSet<string>();
            foreach (var entry in result.Roster.Entries)
            {
                Assert.IsTrue(instanceIds.Add(entry.EnemyInstanceId), "InstanceId 必须唯一");
                Assert.IsTrue(positions.Add((int)entry.Row + ":" + (int)entry.Column), "格位必须唯一");
            }
            Assert.AreEqual(9, instanceIds.Count);
            Assert.AreEqual(9, positions.Count);
        }

        /// <summary>G14：无解遭遇 → 明确失败（不少生成 / 不忽略规则 / 不 fallback / 不残缺）。</summary>
        [Test]
        public void G14_InfeasibleEncounter_FailsExplicitly_WithoutPartialRoster()
        {
            var input = new EncounterFixture()
                .Encounter(1, 9, allowDuplicate: true)
                .Monster("fx_melee", 1, "Normal", 100, "Melee")
                .Slot("fx_slot", "Query", 1, 3, "Any")
                .Rule("fx_slot", "Require", "Role", "Support")   // 没有任何 Support 怪 → 该位被筛空
                .Build();

            var result = new EncounterGenerator(input).GenerateEncounter("fx_enc", 1);

            Assert.IsFalse(result.Success, "该位被筛空时必须失败");
            Assert.IsNull(result.Roster, "失败时不得返回残缺阵容");
            Assert.AreEqual(EncounterGenerationFailure.NoLegalMonster, result.Failure,
                "实际：" + result.Failure + " / " + result.Diagnostic);
            StringAssert.Contains("fx_slot", result.Diagnostic, "诊断必须定位到具体组合需求位");
        }

        /// <summary>G18：EnemyInstanceId 同 Seed 稳定、阵容内唯一、不等于 MonsterId、非 Guid。</summary>
        [Test]
        public void G18_EnemyInstanceId_IsStableUniqueAndNotAMonsterId()
        {
            var input = new EncounterFixture()
                .Encounter(6, 6, allowDuplicate: true)
                .Monster("fx_wolf", 2, "Normal", 100, "Melee")
                .Slot("fx_slot", "Query", 3, 3, "Any")
                .Build();

            var first = new EncounterGenerator(input).GenerateEncounter("fx_enc", 99);
            var second = new EncounterGenerator(input).GenerateEncounter("fx_enc", 99);
            EncounterRosterAssert.AssertValid(first, input);

            Assert.AreEqual(first.Roster.Count, second.Roster.Count);
            var seen = new HashSet<string>();
            for (int i = 0; i < first.Roster.Count; i++)
            {
                string a = first.Roster.Entries[i].EnemyInstanceId;
                string b = second.Roster.Entries[i].EnemyInstanceId;

                Assert.AreEqual(a, b, "同 Seed 重跑 InstanceId 必须稳定");
                Assert.IsTrue(seen.Add(a), "InstanceId 在阵容内必须唯一");
                Assert.AreNotEqual("fx_wolf", a, "InstanceId 不得等于 MonsterId");
                StringAssert.StartsWith(EncounterGenerator.EnemyInstanceIdPrefix, a, "InstanceId 必须使用稳定前缀");
                Assert.AreEqual(-1, a.IndexOf('-'), "InstanceId 不得是 Guid 形式");
            }
        }

        // ---------------- 额外契约：FixedMonster / 搜索上限 ----------------

        /// <summary>§9：FixedMonster 也必须通过该位规则（Fixed 不代表可以绕过其它 Encounter 规则）。</summary>
        [Test]
        public void G_FixedMonster_MustAlsoPassSlotRules()
        {
            var okInput = new EncounterFixture()
                .Encounter(1, 1, allowDuplicate: false)
                .Monster("fx_tank", 1, "Normal", 100, "Tank")
                .Slot("fx_slot", "FixedMonster", 1, 1, "Any", fixedMonsterId: "fx_tank")
                .Rule("fx_slot", "Require", "Role", "Tank")
                .Build();

            var ok = new EncounterGenerator(okInput).GenerateEncounter("fx_enc", 2);
            EncounterRosterAssert.AssertValid(ok, okInput);
            CollectionAssert.AreEquivalent(new[] { "fx_tank" }, EncounterRosterAssert.MonsterIdsOf(ok.Roster));

            var badInput = new EncounterFixture()
                .Encounter(1, 1, allowDuplicate: false)
                .Monster("fx_melee", 1, "Normal", 100, "Melee")
                .Slot("fx_slot", "FixedMonster", 1, 1, "Any", fixedMonsterId: "fx_melee")
                .Rule("fx_slot", "Require", "Role", "Tank")   // 固定怪不满足该规则
                .Build();

            var bad = new EncounterGenerator(badInput).GenerateEncounter("fx_enc", 2);
            Assert.IsFalse(bad.Success, "固定怪不满足该位规则时必须明确失败");
            Assert.AreEqual(EncounterGenerationFailure.NoLegalMonster, bad.Failure,
                "实际：" + bad.Failure + " / " + bad.Diagnostic);
        }

        /// <summary>§16：搜索上限耗尽必须与「无解」区分（不伪装成配置无解）。</summary>
        [Test]
        public void G_SearchLimit_ReportsExhausted_NotUnsolvable()
        {
            var input = new EncounterFixture()
                .Encounter(1, 30, allowDuplicate: true)
                .Monster("fx_a", 2, "Normal", 100, "Melee")
                .Monster("fx_b", 3, "Normal", 100, "Ranged")
                .Slot("fx_slot", "Query", 1, 3, "Any")
                .Build();

            // 极限节点预算：必然在找到解之前耗尽
            var starved = new EncounterGenerator(input, 1).GenerateEncounter("fx_enc", 1);

            Assert.IsFalse(starved.Success);
            Assert.IsNull(starved.Roster);
            Assert.AreEqual(EncounterGenerationFailure.SearchLimitExhausted, starved.Failure,
                "上限耗尽必须单独报告，实际：" + starved.Failure + " / " + starved.Diagnostic);
        }

        // ---------------- G15 / G16 / G17：遭遇池 ----------------

        /// <summary>G15：PoolEntry 深度过滤（边界 inclusive：MinDepth / MaxDepth 都算命中）。</summary>
        [Test]
        public void G15_PoolDepth_FilteringIsInclusive()
        {
            var input = new EncounterFixture()
                .Encounter(1, 1, allowDuplicate: true)
                .Monster("fx_a", 1, "Normal", 100, "Melee")
                .Slot("fx_slot", "Query", 1, 1, "Any")
                .Pool("fx_pool")
                .Entry("fx_pool", "fx_enc", 100, 2, 4)
                .Build();

            var gen = new EncounterGenerator(input);

            Assert.AreEqual(EncounterGenerationFailure.NoCandidateEncounter,
                gen.GenerateFromPool("fx_pool", 1, 5).Failure, "深度 1 < MinDepth 2 → 无候选");

            foreach (int depth in new[] { 2, 3, 4 })
            {
                var result = gen.GenerateFromPool("fx_pool", depth, 5);
                EncounterRosterAssert.AssertValid(result, input);
                Assert.AreEqual("fx_pool", result.Roster.PoolId, "从池生成必须记录来源 PoolId");
            }

            Assert.AreEqual(EncounterGenerationFailure.NoCandidateEncounter,
                gen.GenerateFromPool("fx_pool", 5, 5).Failure, "深度 5 > MaxDepth 4 → 无候选");
        }

        /// <summary>G16：Pool 不存在 / 未启用 / Encounter 未启用 都不得进入正常生成结果。</summary>
        [Test]
        public void G16_DisabledAndMissing_AreExcluded_WithExplicitFailure()
        {
            var input = new EncounterFixture()
                .Encounter(1, 1, allowDuplicate: true)
                .Monster("fx_a", 1, "Normal", 100, "Melee")
                .Slot("fx_slot", "Query", 1, 1, "Any")
                .Pool("fx_pool_disabled", enabled: false)
                .Entry("fx_pool_disabled", "fx_enc", 100, 0, 9)
                .Build();

            var gen = new EncounterGenerator(input);

            Assert.AreEqual(EncounterGenerationFailure.PoolNotFound,
                gen.GenerateFromPool("fx_pool_missing", 0, 1).Failure, "池不存在必须明确失败");
            Assert.AreEqual(EncounterGenerationFailure.PoolDisabled,
                gen.GenerateFromPool("fx_pool_disabled", 0, 1).Failure, "池未启用必须明确失败");

            var disabledEncounter = new EncounterFixture()
                .Encounter(1, 1, allowDuplicate: true, enabled: false)
                .Monster("fx_a", 1, "Normal", 100, "Melee")
                .Slot("fx_slot", "Query", 1, 1, "Any")
                .Pool("fx_pool")
                .Entry("fx_pool", "fx_enc", 100, 0, 9)
                .Build();

            var fromPool = new EncounterGenerator(disabledEncounter).GenerateFromPool("fx_pool", 0, 1);
            Assert.AreEqual(EncounterGenerationFailure.NoCandidateEncounter, fromPool.Failure,
                "未启用的 Encounter 必须被池过滤掉，实际：" + fromPool.Failure + " / " + fromPool.Diagnostic);
            StringAssert.Contains("未启用", fromPool.Diagnostic, "诊断必须说明被过滤的原因");

            var direct = new EncounterGenerator(disabledEncounter).GenerateEncounter("fx_enc", 1);
            Assert.AreEqual(EncounterGenerationFailure.EncounterDisabled, direct.Failure,
                "直接生成未启用遭遇必须明确失败");
        }

        /// <summary>
        /// G17：池内按权重顺序尝试；首个遭遇无解时继续下一个合法遭遇，
        /// 且**不放松**失败遭遇的规则（直接生成它仍然失败）。
        /// </summary>
        [Test]
        public void G17_PoolFallback_TriesNextEncounter_WithoutRelaxingRules()
        {
            var input = new EncounterFixture()
                .Encounter(999, 1000, allowDuplicate: true, encounterId: "fx_infeasible")
                .Monster("fx_a", 1, "Normal", 100, "Melee")
                .Slot("fx_slot_a", "Query", 1, 1, "Any")
                .OtherEncounter("fx_feasible", 1, 1, true, true, "fx_slot_b", 1, 1)
                .Pool("fx_pool")
                .Entry("fx_pool", "fx_infeasible", 1000, 0, 9)   // 权重最高 → 大概率先尝试
                .Entry("fx_pool", "fx_feasible", 1, 0, 9)
                .Build();

            var gen = new EncounterGenerator(input);

            for (int seed = 1; seed <= 6; seed++)
            {
                var result = gen.GenerateFromPool("fx_pool", 0, seed);
                EncounterRosterAssert.AssertValid(result, input);

                Assert.AreEqual("fx_feasible", result.Roster.EncounterId,
                    "必须退到池内下一个合法遭遇（seed=" + seed + "）");
                Assert.AreEqual("fx_pool", result.Roster.PoolId);
            }

            // 失败遭遇的规则没有被放宽：它自己依然无法生成
            var infeasible = gen.GenerateEncounter("fx_infeasible", 1);
            Assert.IsFalse(infeasible.Success, "池回退不得修改失败遭遇的规则");
            Assert.AreEqual(EncounterGenerationFailure.ThreatBudgetUnsolvable, infeasible.Failure);

            // 全部候选都不可生成时，池生成必须整体失败并逐个说明
            var allBad = new EncounterFixture()
                .Encounter(999, 1000, allowDuplicate: true)
                .Monster("fx_a", 1, "Normal", 100, "Melee")
                .Slot("fx_slot", "Query", 1, 1, "Any")
                .Pool("fx_pool")
                .Entry("fx_pool", "fx_enc", 100, 0, 9)
                .Build();

            var failed = new EncounterGenerator(allBad).GenerateFromPool("fx_pool", 0, 1);
            Assert.AreEqual(EncounterGenerationFailure.AllCandidatesFailed, failed.Failure,
                "全部候选失败必须整体失败，实际：" + failed.Failure);
            Assert.IsNull(failed.Roster);
            Assert.IsNotEmpty(failed.Attempts, "必须逐个列出候选遭遇的失败原因");
            StringAssert.Contains("fx_enc", failed.Diagnostic);
        }







    }
}
