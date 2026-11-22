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
using UnityEngine.TestTools;

namespace RRPG.StaticConfig.Tests
{
    /// <summary>
    /// M3 Slice 1C：Enemy Presentation Runtime Resolver + Registry 领域入口。
    ///
    /// 断言的是**领域契约**：
    ///   R1 mon_001 解析成功（来自 Authority）；
    ///   R2 VisualAssetId 查询成功（Registry 领域入口 + Catalog）；
    ///   R3 MonsterId → Presentation → Sprite 链路闭合（全部绑定）；
    ///   R4 missing presentation → fallback sprite + 明确诊断（不静默）；
    ///   R5 missing visual asset / Sprite 不可用 → fallback sprite + 明确诊断；
    ///   R6 **fallback 只存在于 Resolver 内**（业务层不得判断，也未接线）；
    ///   R7 Resolver / Registry 无 Editor 依赖（无 AssetDatabase / UnityEditor / LoadAll / 场景搜索）；
    ///   R8 Registry 入口 lazy + 幂等 + 明确诊断（含 Root 缺失 / 库缺失分支）；
    ///   R9 WarnOnce：回退**每次都计数**，但日志只输出一次（不静默、不刷屏）。
    ///
    /// 只操作 Assets/Generated/StaticConfig（gitignored 生成根）与内存中的合成 ScriptableObject；
    /// 不触碰产品 Scene / Prefab / Battle / Expedition。
    /// </summary>
    public class EnemyPresentationResolverTests
    {
        private const string KnownMonsterId = "mon_001";
        private const string KnownVisualAssetId = "va_enemy_mon_001";

        private readonly List<ScriptableObject> _synthetics = new List<ScriptableObject>();

        [SetUp]
        public void SetUp()
        {
            StaticConfigRegistry.Reset();
            Assert.IsTrue(StaticConfigBuilder.Build(false).Success, "生成根必须先可构建");
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var item in _synthetics)
            {
                if (item != null) UnityEngine.Object.DestroyImmediate(item);
            }
            _synthetics.Clear();

            StaticConfigBuilder.Build(true);
            StaticConfigRegistry.Reset();
            LogAssert.ignoreFailingMessages = false;
        }

        // ---------------- R1：mon_001 解析成功 ----------------

        [Test]
        public void R1_Mon001_Resolve_Succeeds_FromAuthority()
        {
            var resolver = EnemyPresentationResolver.FromStaticConfig();

            Sprite sprite;
            string diagnostic;
            bool resolved = resolver.TryResolve(KnownMonsterId, out sprite, out diagnostic);

            Assert.IsTrue(resolved, "mon_001 必须从 Authority 解析成功：" + diagnostic);
            Assert.AreEqual(string.Empty, diagnostic, "正式路径的 diagnostic 必须为空");
            Assert.IsNotNull(sprite, "解析成功必须返回正式 Sprite");
            Assert.AreEqual(string.Empty, resolver.Diagnostic);

            // 与 Catalog 中的登记完全一致（Resolver 不得返回别的资源）
            VisualAssetDefinition asset;
            Assert.IsTrue(StaticConfigRegistry.VisualAssets.TryGetById(KnownVisualAssetId, out asset));
            Assert.AreSame(asset.Sprite, sprite, "Resolver 必须返回 Catalog 中登记的同一个 Sprite");

            // Resolve（显示安全）同样返回正式 Sprite
            Assert.AreSame(sprite, resolver.Resolve(KnownMonsterId));
            Assert.AreEqual(string.Empty, resolver.Diagnostic);
        }

        // ---------------- R2：VisualAssetId 查询 ----------------

        [Test]
        public void R2_VisualAssetId_Query_Succeeds_ViaRegistryEntries()
        {
            EnemyPresentationDatabase presentations;
            string presentationDiagnostic;
            Assert.IsTrue(StaticConfigRegistry.TryGetEnemyPresentation(out presentations, out presentationDiagnostic),
                presentationDiagnostic);
            Assert.AreEqual(string.Empty, presentationDiagnostic);

            VisualAssetCatalog catalog;
            string catalogDiagnostic;
            Assert.IsTrue(StaticConfigRegistry.TryGetVisualAssets(out catalog, out catalogDiagnostic), catalogDiagnostic);
            Assert.AreEqual(string.Empty, catalogDiagnostic);

            VisualAssetDefinition asset;
            Assert.IsTrue(catalog.TryGetById(KnownVisualAssetId, out asset), "已知 VisualAssetId 必须命中");
            Assert.IsNotNull(asset.Sprite);
            Assert.AreEqual("sprite_point_100", asset.ImportPreset);

            Assert.IsFalse(catalog.TryGetById("va_does_not_exist", out asset), "未知 VisualAssetId 必须不命中");
            Assert.IsNull(asset);

            // 绑定侧：MonsterId → VisualAssetId（enemy presentation 查询）
            EnemyPresentationBinding binding;
            Assert.IsTrue(presentations.TryGetByMonsterId(KnownMonsterId, out binding));
            Assert.AreEqual(KnownVisualAssetId, binding.VisualAssetId);
        }

        // ---------------- R3：链路闭合 ----------------

        [Test]
        public void R3_Chain_MonsterId_To_Sprite_IsClosed_ForAllBindings()
        {
            var resolver = EnemyPresentationResolver.FromStaticConfig();
            var presentations = StaticConfigRegistry.EnemyPresentation;
            var catalog = StaticConfigRegistry.VisualAssets;

            Assert.Greater(presentations.Count, 0, "当前 Mirror 至少有一条绑定");

            foreach (var candidate in presentations.All)
            {
                // 每一段都必须闭合：绑定 → 资源 → Sprite
                VisualAssetDefinition asset;
                Assert.IsTrue(catalog.TryGetById(candidate.VisualAssetId, out asset),
                    "VisualAssetId 未登记：" + candidate);

                Sprite sprite;
                string diagnostic;
                Assert.IsTrue(resolver.TryResolve(candidate.MonsterId, out sprite, out diagnostic),
                    "链路未闭合：" + candidate + " → " + diagnostic);
                Assert.AreSame(asset.Sprite, sprite);
                Assert.IsNotNull(sprite);
            }

            // 当前 Authority 的**全部**怪物都必须能解析（内容一致性；期望值来自 Canonical Mirror，
            // 不再写死「3 只 / mon_003_wolf」—— WP-Encounter-1A-R）。
            var authorityIds = StaticConfigTestAuthority.AuthorityMonsterIds();
            Assert.Greater(authorityIds.Count, 0, "当前 Authority 至少 1 只怪物（否则本用例无意义）");

            foreach (string monsterId in authorityIds)
            {
                Sprite sprite;
                string diagnostic;
                Assert.IsTrue(resolver.TryResolve(monsterId, out sprite, out diagnostic),
                    monsterId + " 必须解析成功：" + diagnostic);
                Assert.IsNotNull(sprite);
                Assert.IsFalse(EnemyPresentationResolver.WarnIssued, "正式路径不得产生回退警告");
            }
        }

        // ---------------- R4：missing presentation ----------------

        [Test]
        public void R4_MissingPresentation_ReturnsFallbackSprite_WithDiagnostic()
        {
            LogAssert.ignoreFailingMessages = true;   // 回退警告由 R9 断言（WarnIssued / WarnCount）

            // 4a：Authority 可用，但该 MonsterId 没有绑定
            var resolver = EnemyPresentationResolver.FromStaticConfig();
            Sprite sprite;
            string diagnostic;
            Assert.IsFalse(resolver.TryResolve("mon_does_not_exist", out sprite, out diagnostic));
            Assert.AreSame(EnemyPresentationResolver.FallbackSprite, sprite, "回退必须返回 fallback sprite（不为 null）");
            StringAssert.StartsWith(EnemyPresentationResolver.MissingPresentationDiagnosticPrefix, diagnostic);
            StringAssert.Contains("mon_does_not_exist", diagnostic);
            Assert.IsTrue(EnemyPresentationResolver.WarnIssued, "回退必须（一次）明确告警：不得静默");
            Assert.AreSame(EnemyPresentationResolver.FallbackSprite, resolver.Resolve("mon_does_not_exist"));

            // 4b：合成的空领域（合法空态）→ 同样是「没有绑定」回退
            var synthetic = EnemyPresentationResolver.Create(
                SyntheticPresentations(), StaticConfigRegistry.VisualAssets);

            Assert.IsFalse(synthetic.TryResolve(KnownMonsterId, out sprite, out diagnostic));
            Assert.AreSame(EnemyPresentationResolver.FallbackSprite, sprite);
            StringAssert.StartsWith(EnemyPresentationResolver.MissingPresentationDiagnosticPrefix, diagnostic);

            // 4c：monsterId 为空（调用方错误）→ 明确诊断 + fallback（不抛异常）
            Assert.IsFalse(synthetic.TryResolve(null, out sprite, out diagnostic));
            Assert.AreSame(EnemyPresentationResolver.FallbackSprite, sprite);
            Assert.AreEqual(EnemyPresentationResolver.NullMonsterIdDiagnostic, diagnostic);

            // 4d：领域整体不可用（Registry 未提供）→ 带回 Registry 诊断的明确回退
            var unavailable = EnemyPresentationResolver.Create(null, StaticConfigRegistry.VisualAssets);
            Assert.IsFalse(unavailable.TryResolve(KnownMonsterId, out sprite, out diagnostic));
            Assert.AreSame(EnemyPresentationResolver.FallbackSprite, sprite);
            StringAssert.StartsWith(EnemyPresentationResolver.RegistryUnavailableDiagnosticPrefix, diagnostic);
        }

        // ---------------- R5：missing visual asset / Sprite ----------------

        [Test]
        public void R5_MissingVisualAsset_OrMissingSprite_ReturnsFallback()
        {
            LogAssert.ignoreFailingMessages = true;

            Sprite sprite;
            string diagnostic;

            // 5a：绑定存在，但 VisualAssetId 不在 Catalog 中
            var resolver = EnemyPresentationResolver.Create(
                SyntheticPresentations((KnownMonsterId, "va_missing")), SyntheticCatalog());

            Assert.IsFalse(resolver.TryResolve(KnownMonsterId, out sprite, out diagnostic));
            Assert.AreSame(EnemyPresentationResolver.FallbackSprite, sprite);
            StringAssert.StartsWith(EnemyPresentationResolver.MissingVisualAssetDiagnosticPrefix, diagnostic);
            StringAssert.Contains("va_missing", diagnostic);

            // 5b：资源存在但 Sprite 不可用（生成根不完整）→ 同样不得静默
            var resolver2 = EnemyPresentationResolver.Create(
                SyntheticPresentations((KnownMonsterId, "va_no_sprite")),
                SyntheticCatalog(("va_no_sprite", null)));

            Assert.IsFalse(resolver2.TryResolve(KnownMonsterId, out sprite, out diagnostic));
            Assert.AreSame(EnemyPresentationResolver.FallbackSprite, sprite);
            StringAssert.StartsWith(EnemyPresentationResolver.MissingSpriteDiagnosticPrefix, diagnostic);
            StringAssert.Contains("va_no_sprite", diagnostic);

            // 5c：Catalog 整体不可用 → 明确回退
            var resolver3 = EnemyPresentationResolver.Create(
                SyntheticPresentations((KnownMonsterId, KnownVisualAssetId)), null);

            Assert.IsFalse(resolver3.TryResolve(KnownMonsterId, out sprite, out diagnostic));
            Assert.AreSame(EnemyPresentationResolver.FallbackSprite, sprite);
            StringAssert.StartsWith(EnemyPresentationResolver.RegistryUnavailableDiagnosticPrefix, diagnostic);
        }

        // ---------------- R6：fallback 只存在于 Resolver ----------------

        [Test]
        public void R6_FallbackLogic_ExistsOnlyInsideResolver()
        {
            string scriptsRoot = StaticConfigBuilder.Absolute("Assets/Scripts");
            string[] files = Directory.GetFiles(scriptsRoot, "*.cs", SearchOption.AllDirectories);

            Assert.Greater(files.Length, 0, "必须扫描到 Runtime 脚本（否则该 Gate 无意义）");

            // fallback 概念的**唯一允许出现处**：EnemyPresentationResolver.cs
            string[] fallbackMarkers =
            {
                "FallbackSprite", "FallbackColor", "FallbackWarningPrefix"
            };

            int scanned = 0;
            foreach (string file in files)
            {
                string name = Path.GetFileName(file);
                if (name == "EnemyPresentationResolver.cs") continue;

                scanned++;
                string code = ReadCode(file);

                foreach (string marker in fallbackMarkers)
                {
                    Assert.IsFalse(code.Contains(marker, StringComparison.Ordinal),
                        "fallback 逻辑只能存在于 EnemyPresentationResolver 内，但 " + name + " 引用了 " + marker);
                }
            }
            Assert.Greater(scanned, 0);

            // 1C 边界：业务层（Battle / UI）**尚未**接线到 Resolver —— 回退判断不得外泄到业务层。
            // （后续 Slice 若接入消费，本断言应随之更新为「只允许调用 Resolve，不得出现 fallback 标记」。）
            foreach (string name in new[] { "BattleUI.cs", "BattleSystem.cs" })
            {
                string[] hits = files.Where(f => Path.GetFileName(f) == name).ToArray();
                Assert.Greater(hits.Length, 0, "必须扫描到业务层文件：" + name);

                foreach (string file in hits)
                {
                    Assert.IsFalse(ReadCode(file).Contains("EnemyPresentationResolver", StringComparison.Ordinal),
                        "1C 不接入战斗表现，" + name + " 不得引用 Resolver");
                }
            }
        }

        // ---------------- R7：无 Editor 依赖 ----------------

        [Test]
        public void R7_ResolverAndRegistry_HaveNoEditorDependency()
        {
            string runtimeDir = StaticConfigBuilder.Absolute("Assets/Scripts/StaticConfig");
            string[] forbidden = { "AssetDatabase", "UnityEditor", "Resources.LoadAll", "FindObjectOfType" };

            string[] targets =
            {
                Path.Combine(runtimeDir, "EnemyPresentationResolver.cs"),
                Path.Combine(runtimeDir, "StaticConfigRegistry.cs")
            };

            foreach (string file in targets)
            {
                Assert.IsTrue(File.Exists(file), "必须存在：" + file);
                string code = ReadCode(file);

                foreach (string symbol in forbidden)
                {
                    Assert.IsFalse(code.Contains(symbol, StringComparison.Ordinal),
                        "Runtime Resolver / Registry 不得依赖 " + symbol + "：" + Path.GetFileName(file));
                }
            }
        }

        // ---------------- R8：Registry 入口 lazy / 幂等 / 明确诊断 ----------------

        [Test]
        public void R8_RegistryEntries_AreLazy_Idempotent_AndExplicit()
        {
            // 幂等：多次读取返回同一实例
            var first = StaticConfigRegistry.EnemyPresentation;
            var catalog = StaticConfigRegistry.VisualAssets;
            Assert.IsNotNull(first);
            Assert.IsNotNull(catalog);
            Assert.AreSame(first, StaticConfigRegistry.EnemyPresentation);
            Assert.AreSame(catalog, StaticConfigRegistry.VisualAssets);

            EnemyPresentationDatabase database;
            string diagnostic;
            Assert.IsTrue(StaticConfigRegistry.TryGetEnemyPresentation(out database, out diagnostic), diagnostic);
            Assert.AreSame(first, database);
            Assert.IsTrue(StaticConfigRegistry.TryGetVisualAssets(out catalog, out diagnostic), diagnostic);
            Assert.AreEqual(string.Empty, StaticConfigRegistry.EnemyPresentationDiagnostic);
            Assert.AreEqual(string.Empty, StaticConfigRegistry.VisualAssetDiagnostic);

            // lazy：Reset 后回到「未初始化」，首次访问才解析
            StaticConfigRegistry.Reset();
            Assert.IsFalse(StaticConfigRegistry.IsInitialized, "Reset 后必须回到未初始化（lazy 前提）");
            Assert.IsNotNull(StaticConfigRegistry.EnemyPresentation);
            Assert.IsTrue(StaticConfigRegistry.IsInitialized, "首次访问必须完成解析");

            // 明确诊断①：库缺失（另一个领域不应被连带失效）
            try
            {
                Assert.IsTrue(AssetDatabase.DeleteAsset(StaticConfigBuilder.EnemyPresentationDatabasePath));
                AssetDatabase.Refresh();
                StaticConfigRegistry.Reset();

                Assert.IsFalse(StaticConfigRegistry.TryGetEnemyPresentation(out database, out diagnostic));
                Assert.IsTrue(database == null, "库缺失时必须返回 null（Unity 语义：已销毁对象 == null）");
                Assert.AreEqual(StaticConfigRegistry.MissingEnemyPresentationDatabaseDiagnostic, diagnostic);
                Assert.IsNotNull(StaticConfigRegistry.VisualAssets, "VisualAsset 领域不应被连带失效");
                Assert.AreEqual(string.Empty, StaticConfigRegistry.VisualAssetDiagnostic);
            }
            finally
            {
                StaticConfigBuilder.Build(true);
                StaticConfigRegistry.Reset();
            }

            // 明确诊断②：Root 缺失 → 两个入口都给出 Root 诊断
            try
            {
                Assert.IsTrue(AssetDatabase.DeleteAsset(StaticConfigBuilder.RootAssetPath));
                AssetDatabase.Refresh();
                StaticConfigRegistry.Reset();

                Assert.IsFalse(StaticConfigRegistry.TryGetEnemyPresentation(out database, out diagnostic));
                Assert.AreEqual(StaticConfigRegistry.MissingRootDiagnostic, diagnostic);
                Assert.IsFalse(StaticConfigRegistry.TryGetVisualAssets(out catalog, out diagnostic));
                Assert.AreEqual(StaticConfigRegistry.MissingRootDiagnostic, diagnostic);
            }
            finally
            {
                StaticConfigBuilder.Build(true);
                StaticConfigRegistry.Reset();
            }
        }

        // ---------------- R9：WarnOnce ----------------

        [Test]
        public void R9_WarnOnce_LogsOnce_ButCountsEveryFallback()
        {
            LogAssert.ignoreFailingMessages = true;

            var resolver = EnemyPresentationResolver.FromStaticConfig();
            Assert.AreEqual(0, EnemyPresentationResolver.WarnCount);

            Sprite sprite;
            string diagnostic;
            Assert.IsFalse(resolver.TryResolve("mon_missing_a", out sprite, out diagnostic));
            Assert.AreEqual(1, EnemyPresentationResolver.WarnCount);
            Assert.IsTrue(EnemyPresentationResolver.WarnIssued, "第一次回退必须发生告警（不静默）");

            Assert.IsFalse(resolver.TryResolve("mon_missing_b", out sprite, out diagnostic));
            Assert.AreEqual(2, EnemyPresentationResolver.WarnCount, "每次回退都要计数");
            Assert.IsTrue(EnemyPresentationResolver.WarnIssued, "日志只输出一次（WarnOnce）");

            Assert.IsTrue(resolver.TryResolve(KnownMonsterId, out sprite, out diagnostic), diagnostic);
            Assert.AreEqual(2, EnemyPresentationResolver.WarnCount, "正式路径不得增加回退计数");

            EnemyPresentationResolver.ResetWarnOnce();
            Assert.IsFalse(EnemyPresentationResolver.WarnIssued);
            Assert.AreEqual(0, EnemyPresentationResolver.WarnCount);
        }

        // ---------------- 工具 ----------------

        private EnemyPresentationDatabase SyntheticPresentations(
            params (string monsterId, string visualAssetId)[] rows)
        {
            var database = ScriptableObject.CreateInstance<EnemyPresentationDatabase>();
            _synthetics.Add(database);

            var bindings = new List<EnemyPresentationBinding>();
            foreach (var row in rows)
            {
                bindings.Add(new EnemyPresentationBinding
                {
                    MonsterId = row.monsterId,
                    VisualAssetId = row.visualAssetId
                });
            }

            database.ApplyGeneratedContent(RRPGStaticConfigRoot.SupportedFormatVersion, "test-mirror-sha", bindings);
            return database;
        }

        private VisualAssetCatalog SyntheticCatalog(params (string visualAssetId, Sprite sprite)[] rows)
        {
            var catalog = ScriptableObject.CreateInstance<VisualAssetCatalog>();
            _synthetics.Add(catalog);

            var assets = new List<VisualAssetDefinition>();
            foreach (var row in rows)
            {
                assets.Add(new VisualAssetDefinition
                {
                    VisualAssetId = row.visualAssetId,
                    SourceFile = "Enemy/" + row.visualAssetId + ".png",
                    ImportPreset = "sprite_point_100",
                    SourceSha256 = "test-sha",
                    Sprite = row.sprite
                });
            }

            catalog.ApplyGeneratedContent(RRPGStaticConfigRoot.SupportedFormatVersion, "test-mirror-sha",
                "test-source-set-sha", assets);
            return catalog;
        }

        /// <summary>只扫**代码**：注释里出现「回退」等说明不算逻辑。</summary>
        private static string ReadCode(string file)
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
