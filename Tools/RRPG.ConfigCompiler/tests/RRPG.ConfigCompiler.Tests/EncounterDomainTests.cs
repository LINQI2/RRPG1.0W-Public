using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RRPG.ConfigCompiler.Core;
using Xunit;

namespace RRPG.ConfigCompiler.Tests
{
    /// <summary>
    /// WP-Encounter-1A：Encounter 领域（遭遇生成静态配置基础）。
    ///
    /// 断言的是**领域契约**而不是实现细节：
    ///   - Encounter 工作簿被现有发现 / 解析 / Schema / Data / Cross-ref 流程自动接入（不新增第二条链）；
    ///   - 7 张表各自产出确定性的 Canonical Mirror（同输入同字节、重复 build = unchanged）；
    ///   - 跨列 / 条件引用 / 领域取值域违规 = Hard Error + 定位 + **0 更新**。
    ///
    /// 注意：本类只覆盖「既有机制能表达 / 本技术检查点明确要求」的校验；
    /// 遭遇求解（Threat Budget 求解、最终阵型求解、随机 Seed）属 1B / 1C，不在本轮。
    /// </summary>
    public class EncounterDomainTests
    {
        private static readonly string[] MirrorFiles =
        {
            "Encounter.json", "EncounterSlot.json", "EncounterSlotRule.json",
            "EncounterPool.json", "EncounterPoolEntry.json",
            "MonsterEncounterProfile.json", "MonsterRole.json"
        };

        // ---------------- 合法：7 张表 → 确定性 Mirror ----------------

        [Fact]
        public void Domain_ProducesMirrorFiles_ForAllSevenSheets()
        {
            using (var root = new TestRoot())
            {
                WriteAuthoredWorkbooks(root);
                var run = CompilerRunner.Run(root.Paths, CompilerRunner.CommandBuild, true);

                Assert.True(run.Success, string.Join(" | ", run.Diagnostics.Select(d => d.ToString())));

                foreach (string file in MirrorFiles)
                {
                    Assert.True(File.Exists(root.GeneratedFile(file)), "缺 Mirror 文件：" + file);
                }

                string encounter = root.ReadGenerated("Encounter.json");
                Assert.Contains("\"rowCount\": 2", encounter);
                Assert.Contains("\"EncounterId\": \"enc_test_basic\"", encounter);
                Assert.Contains("\"ThreatBudgetMin\": 3", encounter);

                string slot = root.ReadGenerated("EncounterSlot.json");
                Assert.Contains("\"SelectionMode\": \"FixedMonster\"", slot);
                Assert.Contains("\"FixedMonsterId\": \"mon_001\"", slot);
                Assert.Contains("\"PositionMode\": \"Exact\"", slot);
                Assert.Contains("\"Row\": \"Front\"", slot);
                Assert.Contains("\"Column\": \"Center\"", slot);

                string rule = root.ReadGenerated("EncounterSlotRule.json");
                Assert.Contains("\"Dimension\": \"MonsterId\"", rule);
                Assert.Contains("\"Value\": \"mon_002\"", rule);

                Assert.Contains("\"Tier\": \"Normal\"", root.ReadGenerated("MonsterEncounterProfile.json"));

                // 复合身份（MonsterRole：同一怪多行）必须完整保留
                string role = root.ReadGenerated("MonsterRole.json");
                Assert.Contains("\"rowCount\": 3", role);
                Assert.Contains("\"Role\": \"Tank\"", role);
                Assert.Contains("\"Role\": \"Melee\"", role);

                // Mirror 不得泄露机器绝对路径
                Assert.DoesNotContain("D:/", encounter);
                Assert.DoesNotContain("D:/", slot);
            }
        }

        [Fact]
        public void Domain_RepeatedBuild_IsByteIdentical_AndUntouched()
        {
            using (var root = new TestRoot())
            {
                WriteAuthoredWorkbooks(root);

                var first = CompilerRunner.Run(root.Paths, CompilerRunner.CommandBuild, true);
                Assert.True(first.Success, string.Join(" | ", first.Diagnostics.Select(d => d.ToString())));

                string path = root.GeneratedFile("EncounterSlot.json");
                byte[] bytes1 = File.ReadAllBytes(path);
                System.DateTime mtime1 = File.GetLastWriteTimeUtc(path);

                var second = CompilerRunner.Run(root.Paths, CompilerRunner.CommandBuild, true);
                Assert.True(second.Success);

                Assert.Equal(bytes1, File.ReadAllBytes(path));
                Assert.Equal(mtime1, File.GetLastWriteTimeUtc(path));
                Assert.Equal("unchanged", second.Outputs.Single(o => o.Name == "EncounterSlot.json").Action);

                var validate = CompilerRunner.Run(root.Paths, CompilerRunner.CommandValidate, false);
                Assert.True(validate.Success, string.Join(" | ", validate.Diagnostics.Select(d => d.ToString())));
            }
        }

        [Fact]
        public void Domain_Absent_IsLegalEmptyState_AndProducesNoEncounterMirror()
        {
            using (var root = new TestRoot())
            {
                root.WriteWorkbook("RRPG_Monsters.xlsx", Fixtures.MinimalMonsterSheet());

                var run = CompilerRunner.Run(root.Paths, CompilerRunner.CommandBuild, true);

                Assert.True(run.Success, string.Join(" | ", run.Diagnostics.Select(d => d.ToString())));
                foreach (string file in MirrorFiles)
                {
                    Assert.False(File.Exists(root.GeneratedFile(file)), "领域不存在时不得产出：" + file);
                }
            }
        }

        // ---------------- 合法：三种站位模式都能表达（不含任何求解器） ----------------

        [Fact]
        public void Slot_CanExpressAnyRowExact_WithoutAnyFormationSolver()
        {
            using (var root = new TestRoot())
            {
                WriteAuthoredWorkbooks(root,
                    Fixtures.EncounterSheet(
                        new object[] { "enc_test_basic", "测试遭遇·基础", true, 3, 9, true }),
                    Fixtures.EncounterSlotSheet(
                        new object[] { "slot_any", "enc_test_basic", "Query", "", 0, 3, "Any", "", "" },
                        new object[] { "slot_row", "enc_test_basic", "Query", "", 1, 3, "Row", "Middle", "" },
                        new object[] { "slot_exact_l", "enc_test_basic", "FixedMonster", "mon_001", 1, 1, "Exact", "Front", "Left" },
                        new object[] { "slot_exact_c", "enc_test_basic", "FixedMonster", "mon_001", 1, 1, "Exact", "Front", "Center" },
                        new object[] { "slot_exact_r", "enc_test_basic", "FixedMonster", "mon_001", 1, 1, "Exact", "Front", "Right" }),
                    Fixtures.EncounterSlotRuleSheet(
                        new object[] { "slot_any", "Require", "Role", "Melee" },
                        new object[] { "slot_row", "Exclude", "Role", "Melee" },
                        new object[] { "slot_exact_c", "Require", "MonsterId", "mon_001" }),
                    Fixtures.EncounterPoolEntrySheet(
                        new object[] { "pool_test_general", "enc_test_basic", 100, 0, 3 }));

                var run = CompilerRunner.Run(root.Paths, CompilerRunner.CommandBuild, true);

                // ΣCountMax = 3 + 3 + 1 + 1 + 1 = 9：恰好等于敌方战场物理容量（3 排 × 3 列）
                Assert.True(run.Success, string.Join(" | ", run.Diagnostics.Select(d => d.ToString())));

                string slot = root.ReadGenerated("EncounterSlot.json");
                Assert.Contains("\"PositionMode\": \"Any\"", slot);
                Assert.Contains("\"PositionMode\": \"Row\"", slot);
                Assert.Contains("\"PositionMode\": \"Exact\"", slot);
                Assert.Contains("\"Column\": \"Left\"", slot);
                Assert.Contains("\"Column\": \"Right\"", slot);
            }
        }

        // ---------------- 非法：身份 / 引用 / 领域形状 ----------------


        [Fact]
        public void DuplicateEncounterId_IsHardError()
        {
            using (var root = new TestRoot())
            {
                var result = BuildDomain(root, Fixtures.EncounterSheet(
                    new object[] { "enc_test_basic", "测试遭遇·基础", true, 3, 9, true },
                    new object[] { "enc_test_basic", "重复身份", true, 1, 2, true }));

                AssertHasError(result, DiagnosticCodes.DatKeyDuplicate);
            }
        }

        [Fact]
        public void Slot_ReferencingMissingEncounter_IsHardError()
        {
            using (var root = new TestRoot())
            {
                var result = BuildDomain(root, Fixtures.EncounterSlotSheet(
                    new object[] { "slot_orphan", "enc_does_not_exist", "Query", "", 1, 1, "Any", "", "" }));

                AssertHasError(result, DiagnosticCodes.DatRefUnresolved);
            }
        }

        [Fact]
        public void FixedMonster_ReferencingMissingMonster_IsHardError()
        {
            using (var root = new TestRoot())
            {
                var result = BuildDomain(root, Fixtures.EncounterSlotSheet(
                    new object[] { "slot_bad_fixed", "enc_test_basic", "FixedMonster", "mon_999", 1, 1, "Exact", "Front", "Center" }));

                AssertHasError(result, DiagnosticCodes.DatEncounterSlotSelectionShape);
            }
        }

        [Fact]
        public void Query_WithFixedMonsterId_IsHardError()
        {
            using (var root = new TestRoot())
            {
                var result = BuildDomain(root, Fixtures.EncounterSlotSheet(
                    new object[] { "slot_query_with_fixed", "enc_test_basic", "Query", "mon_001", 1, 1, "Any", "", "" }));

                AssertHasError(result, DiagnosticCodes.DatEncounterSlotSelectionShape);
            }
        }

        [Fact]
        public void FixedMonster_WithoutFixedMonsterId_IsHardError()
        {
            using (var root = new TestRoot())
            {
                var result = BuildDomain(root, Fixtures.EncounterSlotSheet(
                    new object[] { "slot_fixed_empty", "enc_test_basic", "FixedMonster", "", 1, 1, "Any", "", "" }));

                AssertHasError(result, DiagnosticCodes.DatEncounterSlotSelectionShape);
            }
        }

        [Fact]
        public void MonsterEncounterProfile_ReferencingMissingMonster_IsHardError()
        {
            using (var root = new TestRoot())
            {
                var result = BuildDomain(root, Fixtures.MonsterEncounterProfileSheet(
                    new object[] { "mon_999", 1, "Normal", 10 }));

                AssertHasError(result, DiagnosticCodes.DatRefUnresolved);
            }
        }

        [Fact]
        public void MonsterRole_ReferencingMissingMonster_IsHardError()
        {
            using (var root = new TestRoot())
            {
                var result = BuildDomain(root, Fixtures.MonsterRoleSheet(
                    new object[] { "mon_999", "Melee" }));

                AssertHasError(result, DiagnosticCodes.DatRefUnresolved);
            }
        }

        [Fact]
        public void SlotRule_ReferencingMissingSlot_IsHardError()
        {
            using (var root = new TestRoot())
            {
                var result = BuildDomain(root, Fixtures.EncounterSlotRuleSheet(
                    new object[] { "slot_does_not_exist", "Require", "Role", "Melee" }));

                AssertHasError(result, DiagnosticCodes.DatRefUnresolved);
            }
        }

        [Fact]
        public void PoolEntry_ReferencingMissingPoolOrEncounter_IsHardError()
        {
            using (var root = new TestRoot())
            {
                var result = BuildDomain(root, Fixtures.EncounterPoolEntrySheet(
                    new object[] { "pool_test_general", "enc_does_not_exist", 100, 0, 3 }));

                AssertHasError(result, DiagnosticCodes.DatRefUnresolved);
            }
        }

        [Fact]
        public void EncounterWorkbook_WithoutAllDomainSheets_IsHardError()
        {
            using (var root = new TestRoot())
            {
                root.WriteWorkbook("RRPG_Monsters.xlsx", Fixtures.MinimalMonsterSheet());
                root.WriteWorkbook("RRPG_Encounters.xlsx", Fixtures.EncounterSheet(
                    new object[] { "enc_test_basic", "测试遭遇·基础", true, 3, 9, true }));

                var result = CompilerRunner.Run(root.Paths, CompilerRunner.CommandBuild, true);

                AssertHasError(result, DiagnosticCodes.DatEncounterDomainShape);
            }
        }

        // ---------------- 非法：数量 / 站位 / 容量 ----------------

        [Fact]
        public void Exact_MissingRowOrColumn_IsHardError()
        {
            using (var root = new TestRoot())
            {
                var result = BuildDomain(root, Fixtures.EncounterSlotSheet(
                    new object[] { "slot_exact_bad", "enc_test_basic", "FixedMonster", "mon_001", 1, 1, "Exact", "", "" }));

                AssertHasError(result, DiagnosticCodes.DatEncounterSlotPositionShape);
            }
        }

        [Fact]
        public void Exact_WithCountNotOne_IsHardError()
        {
            using (var root = new TestRoot())
            {
                var result = BuildDomain(root, Fixtures.EncounterSlotSheet(
                    new object[] { "slot_exact_many", "enc_test_basic", "FixedMonster", "mon_001", 1, 2, "Exact", "Front", "Center" }));

                AssertHasError(result, DiagnosticCodes.DatEncounterSlotExactCount);
            }
        }

        [Fact]
        public void Any_WithRowOrColumn_IsHardError()
        {
            using (var root = new TestRoot())
            {
                var result = BuildDomain(root, Fixtures.EncounterSlotSheet(
                    new object[] { "slot_any_with_row", "enc_test_basic", "Query", "", 1, 1, "Any", "Front", "" }));

                AssertHasError(result, DiagnosticCodes.DatEncounterSlotPositionShape);
            }
        }

        [Fact]
        public void Row_WithoutRowOrWithColumn_IsHardError()
        {
            using (var root = new TestRoot())
            {
                var result = BuildDomain(root, Fixtures.EncounterSlotSheet(
                    new object[] { "slot_row_no_row", "enc_test_basic", "Query", "", 1, 1, "Row", "", "" },
                    new object[] { "slot_row_with_col", "enc_test_basic", "Query", "", 1, 1, "Row", "Front", "Left" }));

                AssertHasError(result, DiagnosticCodes.DatEncounterSlotPositionShape);
            }
        }

        [Fact]
        public void Slot_WithZeroMinAndZeroMax_IsHardError()
        {
            using (var root = new TestRoot())
            {
                var result = BuildDomain(root, Fixtures.EncounterSlotSheet(
                    new object[] { "slot_dead", "enc_test_basic", "Query", "", 0, 0, "Any", "", "" }));

                AssertHasError(result, DiagnosticCodes.DatEncounterSlotCountEmpty);
            }
        }

        [Fact]
        public void Slot_WithMaxBelowMin_IsHardError()
        {
            using (var root = new TestRoot())
            {
                var result = BuildDomain(root, Fixtures.EncounterSlotSheet(
                    new object[] { "slot_inverted", "enc_test_basic", "Query", "", 3, 1, "Any", "", "" }));

                AssertHasError(result, DiagnosticCodes.DatEncounterSlotCountRange);
            }
        }

        [Fact]
        public void SlotCountMaxSum_OverEnemyBoardCapacity_IsHardError()
        {
            using (var root = new TestRoot())
            {
                var result = BuildDomain(root, Fixtures.EncounterSlotSheet(
                    new object[] { "slot_a", "enc_test_basic", "Query", "", 0, 5, "Any", "", "" },
                    new object[] { "slot_b", "enc_test_basic", "Query", "", 0, 5, "Any", "", "" }));

                AssertHasError(result, DiagnosticCodes.DatEncounterCapacityExceeded);
            }
        }

        // ---------------- 非法：分类值域 / 数值区间 / 硬错误的 0 更新 ----------------

        [Fact]
        public void UnknownSelectionMode_IsHardError_AndAlsoCaughtByDeclarativeAllowed()
        {
            using (var root = new TestRoot())
            {
                var result = BuildDomain(root, Fixtures.EncounterSlotSheet(
                    new object[] { "slot_bad_mode", "enc_test_basic", "Random", "", 1, 1, "Any", "", "" }));

                AssertHasError(result, DiagnosticCodes.DatEncounterSlotSelectionShape);
                // Row 3 的 allowed= 是同一事实的声明式人读面：两者都必须报出，不得静默接受。
                Assert.Contains(result.Diagnostics, d => d.Code == DiagnosticCodes.DatAllowedViolation);
            }
        }

        [Fact]
        public void UnknownSelectionMode_IsCaught_EvenWithoutDeclarativeAllowed()
        {
            using (var root = new TestRoot())
            {
                // 刻意**不写** Row 3 的 allowed=（模拟「XLSX 声明与 Compiler 取值域不同步」）。
                var slot = new TestSheet("EncounterSlot")
                    .Col("SlotId", "id", "", "组合需求位ID")
                    .Col("EncounterId", "ref:Encounter", "", "所属遭遇模板ID")
                    .Col("SelectionMode", "string", "", "选怪方式")
                    .Col("FixedMonsterId", "string", "optional", "固定怪物ID")
                    .Col("CountMin", "int", ">=0", "最少数量")
                    .Col("CountMax", "int", ">=0", "最多数量")
                    .Col("PositionMode", "string", "", "站位模式")
                    .Col("Row", "string", "optional", "指定排")
                    .Col("Column", "string", "optional", "指定列")
                    .Row("slot_bad_mode", "enc_test_basic", "Random", "", 1, 1, "Any", "", "");

                var result = BuildDomain(root, slot);

                AssertHasError(result, DiagnosticCodes.DatEncounterSlotSelectionShape);
            }
        }

        [Fact]
        public void UnknownPositionMode_AndUnknownRow_AreHardErrors()
        {
            using (var root = new TestRoot())
            {
                var result = BuildDomain(root, Fixtures.EncounterSlotSheet(
                    new object[] { "slot_bad_pos", "enc_test_basic", "Query", "", 1, 1, "Diagonally", "", "" },
                    new object[] { "slot_bad_row", "enc_test_basic", "Query", "", 1, 1, "Row", "Sideways", "" }));

                AssertHasError(result, DiagnosticCodes.DatEncounterSlotPositionShape);
            }
        }

        [Fact]
        public void UnknownRuleMode_AndUnknownDimension_AreHardErrors()
        {
            using (var root = new TestRoot())
            {
                var result = BuildDomain(root, Fixtures.EncounterSlotRuleSheet(
                    new object[] { "slot_basic_front", "Sometimes", "Role", "Melee" },
                    new object[] { "slot_basic_back", "Require", "Species", "undead" }));

                AssertHasError(result, DiagnosticCodes.DatEncounterRuleClassification);
            }
        }

        [Fact]
        public void RuleValue_NotInDimensionDomain_IsHardError()
        {
            using (var root = new TestRoot())
            {
                var result = BuildDomain(root, Fixtures.EncounterSlotRuleSheet(
                    new object[] { "slot_basic_front", "Require", "Role", "Wizard" },
                    new object[] { "slot_basic_back", "Require", "Tier", "Legendary" },
                    new object[] { "slot_boss_fixed", "Require", "MonsterId", "mon_999" }));

                AssertHasError(result, DiagnosticCodes.DatEncounterRuleValue);
            }
        }

        [Fact]
        public void ThreatBudgetMinOverMax_IsHardError()
        {
            using (var root = new TestRoot())
            {
                var result = BuildDomain(root, Fixtures.EncounterSheet(
                    new object[] { "enc_test_basic", "测试遭遇·基础", true, 12, 4, true }));

                AssertHasError(result, DiagnosticCodes.DatEncounterBudgetRange);
            }
        }

        [Fact]
        public void PoolEntry_WeightBelowOne_AndDepthInverted_AreHardErrors()
        {
            using (var root = new TestRoot())
            {
                var result = BuildDomain(root, Fixtures.EncounterPoolEntrySheet(
                    new object[] { "pool_test_general", "enc_test_basic", 0, 0, 3 },
                    new object[] { "pool_test_general", "enc_test_boss", 10, 7, 2 }));

                AssertHasError(result, DiagnosticCodes.DatMinViolation);
                Assert.Contains(result.Diagnostics, d => d.Code == DiagnosticCodes.DatEncounterPoolDepthRange);
            }
        }

        [Fact]
        public void HardError_UpdatesZeroMirrorFiles()
        {
            using (var root = new TestRoot())
            {
                WriteAuthoredWorkbooks(root);
                var good = CompilerRunner.Run(root.Paths, CompilerRunner.CommandBuild, true);
                Assert.True(good.Success, string.Join(" | ", good.Diagnostics.Select(d => d.ToString())));

                var before = MirrorFiles.ToDictionary(f => f, f => File.ReadAllBytes(root.GeneratedFile(f)));

                // 引入一个 Hard Error（Query 却填写 FixedMonsterId），同时 Encounter 表本身的内容也变了
                WriteAuthoredWorkbooks(root,
                    Fixtures.EncounterSheet(new object[] { "enc_test_basic", "改坏了", true, 3, 9, true }),
                    Fixtures.EncounterSlotSheet(
                        new object[] { "slot_basic_front", "enc_test_basic", "Query", "mon_001", 1, 3, "Row", "Front", "" }));

                var bad = CompilerRunner.Run(root.Paths, CompilerRunner.CommandBuild, true);

                Assert.False(bad.Success);
                AssertHasError(bad, DiagnosticCodes.DatEncounterSlotSelectionShape);

                foreach (var pair in before)
                {
                    Assert.Equal(pair.Value, File.ReadAllBytes(root.GeneratedFile(pair.Key)));
                }
            }
        }

        // ---------------- Encounter V0.1 规则：Enabled 必须至少有一个 Slot ----------------

        /// <summary>
        /// Enabled = true 的遭遇必须有 ≥1 个组合需求位；否则一旦被抽中就是一场无法生成的战斗。
        /// 定位：Encounter 工作簿 / Encounter 表 / 该行 / Enabled 列。
        /// </summary>
        [Fact]
        public void EnabledEncounter_WithoutSlot_IsHardError()
        {
            using (var root = new TestRoot())
            {
                var result = BuildDomain(root,
                    Fixtures.EncounterSheet(
                        new object[] { "enc_ok", "有位的启用遭遇", true, 3, 9, true },
                        new object[] { "enc_draft", "启用但没有位的遭遇", true, 3, 9, true }),
                    Fixtures.EncounterSlotSheet(
                        new object[] { "slot_ok", "enc_ok", "Query", "", 1, 2, "Any", "", "" }),
                    Fixtures.EncounterSlotRuleSheet(
                        new object[] { "slot_ok", "Require", "Role", "Melee" }),
                    Fixtures.EncounterPoolEntrySheet(
                        new object[] { "pool_test_general", "enc_ok", 100, 0, 3 }));

                AssertHasError(result, DiagnosticCodes.DatEncounterEnabledWithoutSlot);

                var diagnostic = result.Diagnostics.First(d => d.Code == DiagnosticCodes.DatEncounterEnabledWithoutSlot);
                Assert.Contains("enc_draft", diagnostic.Message, StringComparison.Ordinal);
                Assert.DoesNotContain("enc_ok", diagnostic.Message, StringComparison.Ordinal);
            }
        }

        /// <summary>Enabled = false 允许暂时没有 Slot（制作人草稿），不得报错。</summary>
        [Fact]
        public void DisabledEncounter_WithoutSlot_IsAllowed()
        {
            using (var root = new TestRoot())
            {
                WriteAuthoredWorkbooks(root,
                    Fixtures.EncounterSheet(
                        new object[] { "enc_ok", "有位的启用遭遇", true, 3, 9, true },
                        new object[] { "enc_draft", "未启用的草稿", false, 3, 9, true }),
                    Fixtures.EncounterSlotSheet(
                        new object[] { "slot_ok", "enc_ok", "Query", "", 1, 2, "Any", "", "" }),
                    Fixtures.EncounterSlotRuleSheet(
                        new object[] { "slot_ok", "Require", "Role", "Melee" }),
                    Fixtures.EncounterPoolEntrySheet(
                        new object[] { "pool_test_general", "enc_ok", 100, 0, 3 }));

                var run = CompilerRunner.Run(root.Paths, CompilerRunner.CommandBuild, true);

                Assert.True(run.Success, string.Join(" | ", run.Diagnostics.Select(d => d.ToString())));
                Assert.DoesNotContain(run.Diagnostics,
                    d => d.Code == DiagnosticCodes.DatEncounterEnabledWithoutSlot);

                // 草稿遭遇仍然进入 Mirror（Enabled=false 是内容事实，不是「不存在」）
                string encounter = root.ReadGenerated("Encounter.json");
                Assert.Contains("\"EncounterId\": \"enc_draft\"", encounter);
                Assert.Contains("\"Enabled\": false", encounter);
            }
        }

        // ---------------- 工具 ----------------

        /// <summary>按真实布局写两本工作簿：Monster 工作簿（Monster + 2 张 Encounter 子表）与 Encounter 工作簿。</summary>
        private static void WriteAuthoredWorkbooks(TestRoot root, params TestSheet[] overrides)
        {
            var sheets = Fixtures.EncounterDomain(overrides).ToList();

            var monsterSide = new List<TestSheet> { Fixtures.MinimalMonsterSheet() };
            monsterSide.AddRange(sheets.Where(s => !EncounterValidator.DomainSheets.Contains(s.Name)));

            var encounterSide = sheets.Where(s => EncounterValidator.DomainSheets.Contains(s.Name)).ToList();

            root.WriteWorkbook("RRPG_Monsters.xlsx", monsterSide.ToArray());
            if (encounterSide.Count > 0) root.WriteWorkbook("RRPG_Encounters.xlsx", encounterSide.ToArray());
        }

        private static CompileResult BuildDomain(TestRoot root, params TestSheet[] overrides)
        {
            WriteAuthoredWorkbooks(root, overrides);
            return CompilerRunner.Run(root.Paths, CompilerRunner.CommandBuild, true);
        }

        /// <summary>断言：Build 失败 + 指定诊断码存在 + 该诊断带 Workbook / Sheet / Row / 原因（不得无声失败）。</summary>
        private static void AssertHasError(CompileResult result, string code)
        {
            Assert.False(result.Success,
                "必须 Hard Error（实际诊断：" + string.Join(" | ", result.Diagnostics.Select(d => d.ToString())) + "）");

            var diagnostic = result.Diagnostics.FirstOrDefault(d => d.Code == code);
            Assert.True(diagnostic != null,
                "缺诊断 " + code + "（实际：" + string.Join(" | ", result.Diagnostics.Select(d => d.Code)) + "）");

            Assert.False(string.IsNullOrEmpty(diagnostic.Workbook), "诊断必须定位 Workbook：" + diagnostic);
            Assert.False(string.IsNullOrEmpty(diagnostic.Sheet), "诊断必须定位 Sheet：" + diagnostic);
            Assert.False(string.IsNullOrEmpty(diagnostic.Message), "诊断必须有原因：" + diagnostic);
            if (code != DiagnosticCodes.DatEncounterDomainShape)
            {
                Assert.True(diagnostic.Row > 0, "诊断必须定位数据行：" + diagnostic);
            }
        }




    }
}
