using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using RRPG.StaticConfig;
using RRPG.StaticConfig.EditorTools;
using UnityEditor;
using UnityEngine;

namespace RRPG.StaticConfig.Tests
{
    /// <summary>
    /// M2 Slice 1 — Runtime Registry Entry 契约（FDS §13.3 R1–R5 / M2 Audit §7 Slice 1）。
    ///
    /// 覆盖：
    ///   R1 Root 存在 → StaticConfigRegistry.Monsters 返回**当前 Authority 的全部** MonsterDefinition；
    ///   R2 Root 缺失 → **明确失败**（M4：Legacy 回退已退役）：返回 false + registry = null + 可操作诊断；
    ///   R2b Root 恢复 → 正常路径自愈（同一 Builder 重建后立即恢复完整 Authority 集合）；
    ///   R3 重复初始化 / 重复读取 → 幂等且结果一致；
    ///   R4 Runtime 装配无 Editor 依赖；不得出现任何 Legacy 回退判断（M4 后回退链已不存在）；
    ///   R5 MonsterRegistry 查询 API（GetById / TryGetById + M1 别名 + 重复 ID 明确失败）。
    ///
    /// **WP-Encounter-1A-R**：期望值不再写死「3 只 / mon_003_wolf」，改为从当前 Canonical Mirror 派生
    /// （<see cref="StaticConfigTestAuthority"/>）。制作人正常增删怪物时，本文件只验证「Registry == Authority」，
    /// 不会因内容增长而误报回归；内容快照不再被当成规则。
    ///
    /// 本测试只操作 gitignored 生成根（Assets/Generated/StaticConfig）；不改产品 Scene / Prefab / SO，
    /// 不改任何 Authority 源；**不再读取任何 Legacy 数据**（M4 物理清理后 Legacy 已不存在）。
    /// </summary>
    public class MonsterRegistryEntryTests
    {
        /// <summary>当前正式 Monster 集合（顺序 = Canonical；来自 Canonical Mirror 的 rows）。</summary>
        private static List<string> AuthorityIds()
        {
            return StaticConfigTestAuthority.AuthorityMonsterIds();
        }

        [SetUp]
        public void SetUp()
        {
            Assert.IsTrue(StaticConfigBuilder.Build(false).Success, "前置：生成根必须可构建（M0/M1）");
            StaticConfigRegistry.Reset();
        }

        [TearDown]
        public void TearDown()
        {
            // 让后续测试从稳定的已生成状态开始
            StaticConfigBuilder.Build(true);
            StaticConfigRegistry.Reset();
        }

        // ---------------- R1：Root 存在 ----------------

        [Test]
        public void R1_RootPresent_MonstersReturnsCurrentAuthorityDefinitions()
        {
            var authorityIds = AuthorityIds();

            MonsterRegistry registry;
            string diagnostic;
            bool ok = StaticConfigRegistry.TryGetMonsters(out registry, out diagnostic);

            Assert.IsTrue(ok, diagnostic);
            Assert.IsNotNull(registry, "Root 可用时必须返回 Registry");
            Assert.IsEmpty(diagnostic, "正式路径不得产生诊断");

            Assert.Greater(authorityIds.Count, 0, "当前 Authority 至少 1 只怪物（否则本用例无意义）");
            Assert.AreEqual(authorityIds.Count, registry.Count, "Registry 条数必须等于当前 Authority 的怪物数");
            CollectionAssert.AreEquivalent(authorityIds, registry.All.Select(d => d.MonsterId).ToList(),
                "Registry 名单必须与当前 Authority（Canonical Mirror）一致");

            foreach (var definition in registry.All)
            {
                Assert.IsNotNull(definition);
                Assert.IsNotEmpty(definition.MonsterId);
                Assert.IsNotNull(definition.Name);
                Assert.IsNotNull(definition.Faction);
            }

            Assert.AreEqual(8, MonsterDefinition.FieldCount, "M2 不得扩充字段（8 字段契约）");

            // 只读入口必须与 TryGetMonsters 一致，且顺序稳定
            Assert.AreSame(registry, StaticConfigRegistry.Monsters, "Monsters 必须与 TryGetMonsters 返回同一实例");
            CollectionAssert.AreEqual(
                registry.All.Select(d => d.MonsterId).ToList(),
                StaticConfigRegistry.Monsters.All.Select(d => d.MonsterId).ToList(),
                "Registry.All 顺序必须稳定可复现（Canonical 顺序）");
        }

        // ---------------- R2：缺 Root（M4：明确失败，不回退 Legacy） ----------------

        [Test]
        public void R2_MissingRoot_FailsExplicitly_WithActionableDiagnostic()
        {
            Assert.IsTrue(StaticConfigBuilder.Build(true).Success);

            // 只删除 gitignored 生成根中的 Root 资产（Builder 可重建）
            AssetDatabase.DeleteAsset(StaticConfigBuilder.RootAssetPath);
            AssetDatabase.Refresh();
            StaticConfigRegistry.Reset();

            try
            {
                Assert.IsNull(RRPGStaticConfigRoot.Load(), "前置：Root 必须确实不可加载");

                MonsterRegistry registry;
                string diagnostic;
                bool ok = StaticConfigRegistry.TryGetMonsters(out registry, out diagnostic);

                Assert.IsFalse(ok, "M4：Root 缺失必须明确失败（Legacy 回退已退役）");
                Assert.IsNull(registry, "失败时不得返回任何 Registry（不得回退旧 Monster 数据）");
                Assert.IsNull(StaticConfigRegistry.Monsters, "只读入口必须与 TryGetMonsters 一致（null）");
                Assert.IsNotEmpty(diagnostic, "失败必须给出明确诊断（不得静默）");
                StringAssert.Contains("RRPG_一键导表.cmd", diagnostic, "诊断必须可操作（给出修复指引）");
                StringAssert.Contains("退役", diagnostic, "诊断必须说明 Legacy 回退已退役");
                Assert.AreEqual(diagnostic, StaticConfigRegistry.MonsterDiagnostic,
                    "诊断必须可从只读入口读到（同一份）");

                // 幂等：重复读取结果一致（不得缓存旧数据、不得变为成功）
                MonsterRegistry second;
                string secondDiagnostic;
                Assert.IsFalse(StaticConfigRegistry.TryGetMonsters(out second, out secondDiagnostic));
                Assert.IsNull(second);
                Assert.AreEqual(diagnostic, secondDiagnostic, "重复读取诊断必须稳定（幂等）");
            }
            finally
            {
                StaticConfigBuilder.Build(true);
                StaticConfigRegistry.Reset();
            }
        }

        /// <summary>
        /// R2b：Root 恢复后正常路径必须自愈（M4：失败不是终态 —— 修复生成根即可恢复，
        /// 由既有入口直接回到正常路径，无需重启产品流程）。
        /// </summary>
        [Test]
        public void R2b_AfterRootRestored_NormalPathRecovers()
        {
            Assert.IsTrue(StaticConfigBuilder.Build(true).Success);

            AssetDatabase.DeleteAsset(StaticConfigBuilder.RootAssetPath);
            AssetDatabase.Refresh();
            StaticConfigRegistry.Reset();

            MonsterRegistry failed;
            string failedDiagnostic;
            Assert.IsFalse(StaticConfigRegistry.TryGetMonsters(out failed, out failedDiagnostic),
                "前置：Root 缺失时必须是明确失败");

            // 恢复：重建生成根（同一 Builder，不引入任何新流程）
            var outcome = StaticConfigBuilder.Build(true);
            Assert.IsTrue(outcome.Success, outcome.Failure);
            StaticConfigRegistry.Reset();

            MonsterRegistry restored;
            string restoredDiagnostic;
            Assert.IsTrue(StaticConfigRegistry.TryGetMonsters(out restored, out restoredDiagnostic),
                restoredDiagnostic);
            Assert.IsNotNull(restored);
            Assert.IsEmpty(restoredDiagnostic, "恢复后正式路径不得产生诊断");

            var authorityIds = AuthorityIds();
            Assert.AreEqual(authorityIds.Count, restored.Count, "恢复后必须回到当前 Authority 的完整集合");
            CollectionAssert.AreEquivalent(authorityIds, restored.All.Select(d => d.MonsterId).ToList());
        }

        // ---------------- R3：重复初始化 / 重复读取 ----------------

        [Test]
        public void R3_RepeatedInitialization_IsIdempotent()
        {
            string d1, d2, d3;
            Assert.IsTrue(StaticConfigRegistry.TryInitialize(out d1), d1);
            Assert.IsTrue(StaticConfigRegistry.TryInitialize(out d2), d2);
            Assert.IsTrue(StaticConfigRegistry.TryInitialize(out d3), d3);

            MonsterRegistry r1, r2, r3;
            string md1, md2, md3;
            Assert.IsTrue(StaticConfigRegistry.TryGetMonsters(out r1, out md1), md1);
            Assert.IsTrue(StaticConfigRegistry.TryGetMonsters(out r2, out md2), md2);
            var viaProperty = StaticConfigRegistry.Monsters;
            Assert.IsTrue(StaticConfigRegistry.TryGetMonsters(out r3, out md3), md3);

            Assert.AreSame(r1, r2, "重复调用必须返回同一 Registry 实例");
            Assert.AreSame(r1, r3, "重复调用必须返回同一 Registry 实例");
            Assert.AreSame(r1, viaProperty, "Monsters 属性必须与 TryGetMonsters 返回同一实例");
            Assert.AreEqual(md1, md2, "诊断必须稳定");
            Assert.AreEqual(md1, md3, "诊断必须稳定");
            Assert.AreEqual(string.Empty, md1, "正式路径诊断必须为空");
            Assert.AreEqual(string.Empty, d1, "Root 初始化诊断必须为空");

            var authorityCount = AuthorityIds().Count;
            Assert.AreEqual(authorityCount, r1.Count, "Registry 条数必须等于当前 Authority 的怪物数");

            // Reset 后可重新解析：结果等价、不泄漏旧状态、不触发回退
            StaticConfigRegistry.Reset();
            MonsterRegistry afterReset;
            string mdAfterReset;
            Assert.IsTrue(StaticConfigRegistry.TryGetMonsters(out afterReset, out mdAfterReset), mdAfterReset);
            Assert.AreEqual(authorityCount, afterReset.Count, "Reset 后重新解析结果必须等价");
            Assert.AreEqual(string.Empty, mdAfterReset);
            // M4：整条 Legacy 回退链已退役，不再存在可被标记的回退状态（见 R2 明确失败契约）
        }

        // ---------------- R4：无 Editor 依赖 + 回退只允许在 StaticConfig 层 ----------------

        [Test]
        public void R4_RuntimeAssembly_HasNoEditorDependency_AndFallbackIsStaticConfigOnly()
        {
            string runtimeDir = StaticConfigBuilder.Absolute("Assets/Scripts/StaticConfig");
            var runtimeFiles = Directory.GetFiles(runtimeDir, "*.cs");
            Assert.GreaterOrEqual(runtimeFiles.Length, 8, "必须真的扫描到 Runtime 源文件（否则 R4 无意义）");

            foreach (string file in runtimeFiles)
            {
                string code = StripComments(file);
                Assert.IsFalse(code.Contains("UnityEditor"), "Runtime 不得引用 UnityEditor：" + file);
                Assert.IsFalse(code.Contains("AssetDatabase"), "Runtime 不得引用 AssetDatabase：" + file);
                Assert.IsFalse(code.Contains("FindObjectOfType"), "Runtime 不得做场景搜索：" + file);
            }

            string asmdef = File.ReadAllText(Path.Combine(runtimeDir, "StaticConfig.Runtime.asmdef"));
            Assert.IsTrue(asmdef.Contains("UnityEngine"), "Runtime asmdef 应引用 UnityEngine");
            Assert.IsFalse(asmdef.Contains("StaticConfig.Editor"), "Runtime asmdef 不得引用 Editor assembly");
            Assert.IsFalse(asmdef.Contains("UnityEditor"), "Runtime asmdef 不得引用 UnityEditor");

            // D-M2-1：回退判断只能存在于 StaticConfig 层 —— Battle / UI / Adventure 不得自行判断 fallback
            string scriptsRoot = StaticConfigBuilder.Absolute("Assets/Scripts");
            foreach (string file in Directory.GetFiles(scriptsRoot, "*.cs", SearchOption.AllDirectories))
            {
                if (file.Replace('\\', '/').Contains("/StaticConfig/")) continue;

                string code = StripComments(file);
                Assert.IsFalse(code.Contains("LegacyMonsterFallbackAdapter"),
                    "回退判断不得出现在 StaticConfig 层之外：" + file);
                Assert.IsFalse(code.Contains("UsingLegacyFallback"),
                    "回退状态判断不得出现在 StaticConfig 层之外：" + file);
            }

            Debug.Log("[M2 Slice 1 Gate] Runtime 无 Editor 依赖；Legacy 回退只存在于 StaticConfig 层");
        }

        // ---------------- R5：MonsterRegistry 查询 API 契约 ----------------

        [Test]
        public void R5_MonsterRegistry_QueryApi_Contract()
        {
            var registry = StaticConfigRegistry.Monsters;
            Assert.IsNotNull(registry, "前置：Registry 必须可用（诊断：" + StaticConfigRegistry.MonsterDiagnostic + "）");

            // 遍历**当前 Authority 的全部**怪物（不再写死 3 个 ID）
            foreach (string id in AuthorityIds())
            {
                MonsterDefinition byId, byAlias;
                Assert.IsTrue(registry.TryGetById(id, out byId), "TryGetById 必须命中：" + id);
                Assert.IsTrue(registry.TryGet(id, out byAlias), "TryGet（M1 别名）必须命中：" + id);
                Assert.AreSame(byId, byAlias, "TryGet 必须是 TryGetById 的同一实现：" + id);
                Assert.AreSame(registry.GetById(id), byId, "GetById 与 TryGetById 必须一致：" + id);
                Assert.AreSame(registry.Get(id), byId, "Get（M1 别名）必须一致：" + id);
                Assert.IsTrue(registry.Contains(id));
            }

            MonsterDefinition missing;
            Assert.IsFalse(registry.TryGetById("mon_does_not_exist", out missing), "未命中必须返回 false");
            Assert.IsNull(missing);
            Assert.IsNull(registry.GetById("mon_does_not_exist"), "未命中不得静默回退到其它怪物");
            StringAssert.Contains("不存在该 ID", registry.Diagnostic, "未命中必须留下明确诊断");
            Assert.IsFalse(registry.TryGetById(null, out missing));
            Assert.IsFalse(registry.TryGetById(string.Empty, out missing));

            // 重复 MonsterId → 构造期明确失败（不取首个）
            var duplicates = new List<MonsterDefinition>
            {
                new MonsterDefinition { MonsterId = "mon_dup" },
                new MonsterDefinition { MonsterId = "mon_dup" }
            };
            var ex = Assert.Throws<InvalidOperationException>(() => new MonsterRegistry(duplicates));
            StringAssert.Contains("重复 ID", ex.Message);
        }

        /// <summary>剥掉纯注释行后再做禁用符号检查（沿用既有 StaticConfig 测试口径）。</summary>
        private static string StripComments(string file)
        {
            var code = new StringBuilder();
            foreach (string line in File.ReadAllLines(file))
            {
                string trimmed = line.TrimStart();
                if (trimmed.StartsWith("//", StringComparison.Ordinal)) continue;
                if (trimmed.StartsWith("*", StringComparison.Ordinal)) continue;
                code.AppendLine(line);
            }
            return code.ToString();
        }
    }
}
