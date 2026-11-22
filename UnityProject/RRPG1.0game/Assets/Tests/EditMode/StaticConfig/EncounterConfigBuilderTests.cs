using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using RRPG.StaticConfig;
using RRPG.StaticConfig.EditorTools;
using UnityEditor;

namespace RRPG.StaticConfig.Tests
{
    /// <summary>
    /// WP-Encounter-1A：Encounter 领域（遭遇生成静态配置基础）的 Builder → Root → Registry 契约。
    ///
    /// 断言的是**领域契约**（Mirror → ScriptableObject → 只读入口），不是实现细节：
    ///   E1 JSON → ScriptableObject：7 张表的条数与 Mirror rowCount 一致；来源指纹来自 Mirror；
    ///   E2 Registry 入口：TryGetEncounterConfig / EncounterConfig / EncounterConfigDiagnostic 一致且幂等；
    ///   E3 相同输入 = no-op（不触碰任何产物）；
    ///   E4 RebuildFromZero 之后仍可读取；
    ///   E5 生成根缺该库 → **明确失败 + 可操作诊断**（不静默、不伪装成功），重建后自愈；
    ///   E6 自洽性：跨表引用在生成根内闭合；MonsterRole 允许一怪多行；MonsterId 与 Monster 域一致。
    ///
    /// 只操作 Assets/Generated/StaticConfig（gitignored 生成根）；不触碰产品 Scene / Prefab / 人工 Authority。
    /// 本轮**不**消费这些数据进入 Battle / Prep（属后续技术检查点 1B / 1C）。
    /// </summary>
    public class EncounterConfigBuilderTests
    {
        private static readonly string[] SheetNames =
        {
            "Encounter", "EncounterSlot", "EncounterSlotRule", "EncounterPool",
            "EncounterPoolEntry", "MonsterEncounterProfile", "MonsterRole"
        };

        [SetUp]
        public void SetUp()
        {
            StaticConfigRegistry.Reset();
        }

        [TearDown]
        public void TearDown()
        {
            StaticConfigBuilder.Build(false);
            StaticConfigRegistry.Reset();
        }

        // ---------------- E1：JSON → ScriptableObject ----------------

        [Test]
        public void E1_MirrorJson_BecomesEncounterDatabase()
        {
            var outcome = StaticConfigBuilder.Build(true);
            Assert.IsTrue(outcome.Success, outcome.Failure);

            Assert.IsTrue(File.Exists(StaticConfigBuilder.Absolute(StaticConfigBuilder.EncounterDatabasePath)),
                "Encounter 库必须被生成：" + StaticConfigBuilder.EncounterDatabasePath);

            var root = RRPGStaticConfigRoot.Load();
            Assert.IsNotNull(root, "生成根必须可加载（exact path）");

            var db = root.EncounterDatabase;
            Assert.IsNotNull(db, "Root 必须引用 EncounterConfigDatabase");
            Assert.AreEqual(RRPGStaticConfigRoot.SupportedFormatVersion, db.FormatVersion);
            Assert.IsNotEmpty(db.SourceMirrorSha256, "来源指纹必须来自 Mirror（Encounter.json）");
            Assert.IsNotEmpty(db.MonsterProfileSha256, "Monster 子表指纹必须来自 Mirror（MonsterEncounterProfile.json）");
            Assert.IsNotEmpty(db.MonsterRoleSha256, "Monster 子表指纹必须来自 Mirror（MonsterRole.json）");

            Assert.AreEqual(RowCount("Encounter"), db.EncounterCount, "遭遇模板条数必须等于 Mirror 行数");
            Assert.AreEqual(RowCount("EncounterSlot"), db.SlotCount, "组合需求位条数必须等于 Mirror 行数");
            Assert.AreEqual(RowCount("EncounterSlotRule"), db.SlotRuleCount, "筛选规则条数必须等于 Mirror 行数");
            Assert.AreEqual(RowCount("EncounterPool"), db.PoolCount, "遭遇池条数必须等于 Mirror 行数");
            Assert.AreEqual(RowCount("EncounterPoolEntry"), db.PoolEntryCount, "池条目条数必须等于 Mirror 行数");
            Assert.AreEqual(RowCount("MonsterEncounterProfile"), db.MonsterProfileCount, "怪物画像条数必须等于 Mirror 行数");
            Assert.AreEqual(RowCount("MonsterRole"), db.MonsterRoleCount, "怪物定位条数必须等于 Mirror 行数");

            Assert.Greater(db.TotalCount, 0, "当前 Mirror 至少应有一批 Encounter 记录");

            // 顺序 = Canonical 顺序（稳定、可复现）
            var encounterIds = db.Encounters.Select(e => e.EncounterId).ToList();
            CollectionAssert.AreEqual(
                encounterIds.OrderBy(x => x, StringComparer.Ordinal).ToList(), encounterIds,
                "遭遇模板顺序必须等于 Canonical（按 EncounterId 排序）顺序");
        }

        // ---------------- E2：Registry 入口 ----------------

        [Test]
        public void E2_RegistryEntry_IsAvailable_AndIdempotent()
        {
            Assert.IsTrue(StaticConfigBuilder.Build(false).Success, "前置：生成根必须可构建");
            StaticConfigRegistry.Reset();

            EncounterConfigDatabase db;
            string diagnostic;
            Assert.IsTrue(StaticConfigRegistry.TryGetEncounterConfig(out db, out diagnostic), diagnostic);
            Assert.IsNotNull(db, "Root 可用时必须返回 Encounter 库");
            Assert.IsEmpty(diagnostic, "正式路径不得产生诊断");

            Assert.AreSame(db, StaticConfigRegistry.EncounterConfig,
                "EncounterConfig 必须与 TryGetEncounterConfig 返回同一实例");
            Assert.IsEmpty(StaticConfigRegistry.EncounterConfigDiagnostic);

            // 幂等：重复读取不重复解析
            EncounterConfigDatabase again;
            string againDiagnostic;
            Assert.IsTrue(StaticConfigRegistry.TryGetEncounterConfig(out again, out againDiagnostic), againDiagnostic);
            Assert.AreSame(db, again, "重复读取必须幂等（同一实例）");

            // Root 也指向同一实例
            Assert.AreSame(db, RRPGStaticConfigRoot.Load().EncounterDatabase);
        }

        [Test]
        public void E2b_RegistryReset_ReResolvesDeterministically()
        {
            EncounterConfigDatabase before;
            string diagnostic;
            Assert.IsTrue(StaticConfigRegistry.TryGetEncounterConfig(out before, out diagnostic), diagnostic);
            int count = before.TotalCount;

            StaticConfigRegistry.Reset();

            EncounterConfigDatabase after;
            Assert.IsTrue(StaticConfigRegistry.TryGetEncounterConfig(out after, out diagnostic), diagnostic);
            Assert.AreEqual(count, after.TotalCount, "Reset 后重新解析必须得到相同条数");
        }

        // ---------------- E3：no-op ----------------

        [Test]
        public void E3_SecondBuild_IsNoOp_WithEncounterDatabase()
        {
            Assert.IsTrue(StaticConfigBuilder.Build(true).Success);

            string dbFile = StaticConfigBuilder.Absolute(StaticConfigBuilder.EncounterDatabasePath);
            var mtime = File.GetLastWriteTimeUtc(dbFile);

            var second = StaticConfigBuilder.Build(false);

            Assert.IsTrue(second.Success, second.Failure);
            Assert.IsTrue(second.NoOp, "相同输入必须走 no-op 路径");
            Assert.AreEqual(mtime, File.GetLastWriteTimeUtc(dbFile),
                "no-op 不得触碰 Encounter 库（mtime 必须不变）");
        }

        // ---------------- E4：从零重建 ----------------

        [Test]
        public void E4_RebuildFromZero_RestoresEncounterDatabase()
        {
            Assert.IsTrue(StaticConfigBuilder.Build(true).Success);

            var outcome = StaticConfigBuilder.RebuildFromZero();
            Assert.IsTrue(outcome.Success, outcome.Failure);

            StaticConfigRegistry.Reset();
            string diagnostic;
            EncounterConfigDatabase db;
            Assert.IsTrue(StaticConfigRegistry.TryGetEncounterConfig(out db, out diagnostic), diagnostic);
            Assert.Greater(db.TotalCount, 0, "从零重建后 Encounter 记录必须可恢复");
        }

        // ---------------- E5：缺库 → 明确失败 ----------------

        [Test]
        public void E5_MissingEncounterDatabase_FailsExplicitly_WithActionableDiagnostic()
        {
            Assert.IsTrue(StaticConfigBuilder.Build(true).Success);

            AssetDatabase.DeleteAsset(StaticConfigBuilder.EncounterDatabasePath);
            AssetDatabase.Refresh();
            StaticConfigRegistry.Reset();

            try
            {
                EncounterConfigDatabase db;
                string diagnostic;
                bool ok = StaticConfigRegistry.TryGetEncounterConfig(out db, out diagnostic);

                Assert.IsFalse(ok, "缺库时必须明确失败");
                Assert.IsNull(db);
                Assert.AreEqual(StaticConfigRegistry.MissingEncounterDatabaseDiagnostic, diagnostic,
                    "必须给出可操作的中文诊断（重新运行一键导表）");
            }
            finally
            {
                // 自愈：重建生成根后正式路径恢复
                Assert.IsTrue(StaticConfigBuilder.Build(true).Success);
                StaticConfigRegistry.Reset();
            }

            string healed;
            EncounterConfigDatabase restored;
            Assert.IsTrue(StaticConfigRegistry.TryGetEncounterConfig(out restored, out healed), healed);
            Assert.IsEmpty(healed);
        }

        // ---------------- E6：生成根内的自洽性（跨表 / 跨域） ----------------

        [Test]
        public void E6_GeneratedRoot_IsSelfConsistent_AndKeepsMultiRowRoles()
        {
            Assert.IsTrue(StaticConfigBuilder.Build(true).Success);

            var db = RRPGStaticConfigRoot.Load().EncounterDatabase;
            Assert.IsNotNull(db);

            var encounterIds = new HashSet<string>(db.Encounters.Select(e => e.EncounterId), StringComparer.Ordinal);
            var slotIds = new HashSet<string>(db.Slots.Select(s => s.SlotId), StringComparer.Ordinal);
            var poolIds = new HashSet<string>(db.Pools.Select(p => p.PoolId), StringComparer.Ordinal);

            foreach (var slot in db.Slots)
            {
                Assert.IsTrue(encounterIds.Contains(slot.EncounterId),
                    "每个组合需求位都必须引用存在的遭遇模板：" + slot);
                Assert.IsFalse(string.IsNullOrEmpty(slot.PositionMode), "PositionMode 不得为空：" + slot);

                // 取值以 XLSX 的 allowed= 声明为准（"FixedMonster" / "Query"）
                if (slot.SelectionMode == "FixedMonster")
                {
                    Assert.IsFalse(string.IsNullOrEmpty(slot.FixedMonsterId),
                        "FixedMonster 必须带 FixedMonsterId：" + slot);
                }
                else if (slot.SelectionMode == "Query")
                {
                    Assert.IsTrue(string.IsNullOrEmpty(slot.FixedMonsterId),
                        "Query 不得带 FixedMonsterId：" + slot);
                }
                else
                {
                    Assert.Fail("SelectionMode 出现未声明取值（应已被 Compiler 拦下）：" + slot);
                }
            }

            foreach (var rule in db.SlotRules)
            {
                Assert.IsTrue(slotIds.Contains(rule.SlotId), "每条筛选规则都必须引用存在的组合需求位：" + rule);
            }

            foreach (var entry in db.PoolEntries)
            {
                Assert.IsTrue(poolIds.Contains(entry.PoolId), "每个池条目都必须引用存在的遭遇池：" + entry);
                Assert.IsTrue(encounterIds.Contains(entry.EncounterId),
                    "每个池条目都必须引用存在的遭遇模板：" + entry);
            }

            // 复合身份不得重复
            Assert.AreEqual(db.MonsterRoleCount,
                db.MonsterRoles.Select(r => r.MonsterId + "|" + r.Role).Distinct(StringComparer.Ordinal).Count(),
                "MonsterRole 复合身份（MonsterId + Role）不得重复");

            // 一怪多行必须保留（同一只怪可以同时是 Melee + Tank）
            var rolesPerMonster = db.MonsterRoles.GroupBy(r => r.MonsterId).ToList();
            Assert.AreEqual(db.MonsterProfiles.Select(p => p.MonsterId).Distinct(StringComparer.Ordinal).Count(),
                rolesPerMonster.Count,
                "画像表与定位表覆盖的怪物集合必须一致");

            // 跨域：Encounter 引用的怪物必须真实存在于 Monster 域
            MonsterRegistry monsters;
            string diagnostic;
            Assert.IsTrue(StaticConfigRegistry.TryGetMonsters(out monsters, out diagnostic), diagnostic);

            foreach (var profile in db.MonsterProfiles)
            {
                Assert.IsTrue(monsters.Contains(profile.MonsterId),
                    "MonsterEncounterProfile 必须引用 Monster 域存在的怪物：" + profile.MonsterId);
            }

            foreach (var role in db.MonsterRoles)
            {
                Assert.IsTrue(monsters.Contains(role.MonsterId),
                    "MonsterRole 必须引用 Monster 域存在的怪物：" + role.MonsterId);
            }

            foreach (var slot in db.Slots)
            {
                if (string.IsNullOrEmpty(slot.FixedMonsterId)) continue;
                Assert.IsTrue(monsters.Contains(slot.FixedMonsterId),
                    "FixedMonsterId 必须引用 Monster 域存在的怪物：" + slot.FixedMonsterId);
            }
        }

        /// <summary>Round-trip：Encounter 库的字段数契约必须与 Mirror 列数一致（防止 Schema 漂移）。</summary>
        [Test]
        public void E7_DefinitionFieldCounts_MatchMirrorContracts()
        {
            Assert.AreEqual(6, EncounterDefinition.FieldCount);
            Assert.AreEqual(9, EncounterSlotDefinition.FieldCount);
            Assert.AreEqual(4, EncounterSlotRuleDefinition.FieldCount);
            Assert.AreEqual(3, EncounterPoolDefinition.FieldCount);
            Assert.AreEqual(5, EncounterPoolEntryDefinition.FieldCount);
            Assert.AreEqual(4, MonsterEncounterProfileDefinition.FieldCount);
            Assert.AreEqual(2, MonsterRoleDefinition.FieldCount);

            foreach (string sheet in SheetNames)
            {
                Assert.IsTrue(File.Exists(MirrorPath(sheet + ".json")), "Mirror 必须存在：" + sheet + ".json");
            }
        }

        // ---------------- 工具 ----------------

        private static string MirrorPath(string fileName)
        {
            return Path.Combine(StaticConfigBuilder.RepoRoot(),
                StaticConfigBuilder.MirrorRelativeDir.Replace('/', Path.DirectorySeparatorChar), fileName);
        }

        /// <summary>从 Mirror 读取 rowCount（与 Builder 消费的是同一份产物）。</summary>
        private static int RowCount(string sheetName)
        {
            string json = File.ReadAllText(MirrorPath(sheetName + ".json"), new UTF8Encoding(false));
            const string key = "\"rowCount\": ";

            int at = json.IndexOf(key, StringComparison.Ordinal);
            Assert.Greater(at, -1, sheetName + ".json 必须含 rowCount");

            int start = at + key.Length;
            int end = start;
            while (end < json.Length && char.IsDigit(json[end])) end++;

            Assert.Greater(end, start, sheetName + ".json 的 rowCount 必须是数字");
            return int.Parse(json.Substring(start, end - start), System.Globalization.CultureInfo.InvariantCulture);
        }


    }
}
