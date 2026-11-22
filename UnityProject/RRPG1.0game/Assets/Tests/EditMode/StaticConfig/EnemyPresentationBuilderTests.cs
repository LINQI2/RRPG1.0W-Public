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
    /// M3 Slice 1B：Unity Builder 对 Enemy Presentation 数据的消费。
    ///
    /// 断言的是**领域契约**（JSON → ScriptableObject → 查询 / managed Sprite / no-op），不是实现细节：
    ///   P1 JSON → ScriptableObject（条数与 Mirror 行数一致；来源指纹非空）；
    ///   P2 VisualAssetId 查询；
    ///   P3 EnemyPresentationId（MonsterId）查询 + 与 VisualAsset 的链接；
    ///   P4 Missing asset **明确失败**且不触碰生成根；
    ///   P5 源图 → managed Sprite：逐字节一致 + 预设生效 + 只落在 Managed Root 内；
    ///   P6 相同输入 = no-op（不触碰任何产物）；
    ///   P7 RebuildFromZero 后仍可查询（Sprite 可恢复）。
    ///
    /// 只操作 Assets/Generated/StaticConfig（gitignored 生成根）与 Development/Generated/StaticConfig
    /// （Compiler 独占的 Mirror；测试若做了修改必须在 finally 中还原）；不触碰产品 Scene / Prefab / SO。
    /// </summary>
    public class EnemyPresentationBuilderTests
    {
        private const string KnownMonsterId = "mon_001";
        private const string KnownVisualAssetId = "va_enemy_mon_001";
        private const string KnownSourceFile = "Enemy/enemy_mon_001.png";

        [SetUp]
        public void SetUp()
        {
            StaticConfigRegistry.Reset();
        }

        [TearDown]
        public void TearDown()
        {
            // 让后续测试从稳定的已生成状态开始
            StaticConfigBuilder.Build(false);
            StaticConfigRegistry.Reset();
        }

        // ---------------- P1：JSON → ScriptableObject ----------------

        [Test]
        public void P1_MirrorJson_BecomesScriptableObjects()
        {
            var outcome = StaticConfigBuilder.Build(true);
            Assert.IsTrue(outcome.Success, outcome.Failure);

            var root = RRPGStaticConfigRoot.Load();
            Assert.IsNotNull(root, "生成根必须可加载（exact path）");

            var presentations = root.EnemyPresentationDatabase;
            var catalog = root.VisualAssetCatalog;
            Assert.IsNotNull(presentations, "Root 必须引用 EnemyPresentationDatabase");
            Assert.IsNotNull(catalog, "Root 必须引用 VisualAssetCatalog");

            // 条数必须与 Mirror 的 Canonical 行数一致（JSON → SO 是逐行映射，不得丢行）
            Assert.AreEqual(CountOccurrences(ReadMirror("EnemyPresentation.json"), "\"MonsterId\": "),
                presentations.Count, "绑定条数必须等于 EnemyPresentation.json 的行数");
            Assert.AreEqual(CountOccurrences(ReadMirror("VisualAsset.json"), "\"VisualAssetId\": "),
                catalog.Count, "资源条数必须等于 VisualAsset.json 的行数");
            Assert.GreaterOrEqual(catalog.Count, 1, "当前 Mirror 至少登记了 1 个视觉资源");

            Assert.AreEqual(RRPGStaticConfigRoot.SupportedFormatVersion, presentations.FormatVersion);
            Assert.AreEqual(RRPGStaticConfigRoot.SupportedFormatVersion, catalog.FormatVersion);

            // 来源指纹必须来自 Mirror（而不是凭空生成）
            Assert.IsNotEmpty(presentations.SourceMirrorSha256);
            Assert.IsNotEmpty(catalog.SourceMirrorSha256);
            Assert.IsNotEmpty(catalog.SourceSetSha256, "AssetManifest 的整组源资产指纹必须写入 catalog");

            // 顺序 = Canonical 顺序（稳定、可复现）
            var bindingIds = presentations.All.Select(b => b.MonsterId).ToList();
            CollectionAssert.AreEqual(bindingIds.OrderBy(x => x, StringComparer.Ordinal).ToList(), bindingIds,
                "绑定顺序必须等于 Canonical（按 MonsterId 排序）顺序");
        }

        // ---------------- P2：VisualAssetId 查询 ----------------

        [Test]
        public void P2_VisualAssetId_Query_HitsAndMisses()
        {
            Assert.IsTrue(StaticConfigBuilder.Build(true).Success);
            var catalog = RRPGStaticConfigRoot.Load().VisualAssetCatalog;

            VisualAssetDefinition asset;
            Assert.IsTrue(catalog.TryGetById(KnownVisualAssetId, out asset), "已知 VisualAssetId 必须命中");
            Assert.IsNotNull(asset);
            Assert.AreEqual(KnownVisualAssetId, asset.VisualAssetId);
            Assert.AreEqual(KnownSourceFile, asset.SourceFile);
            Assert.AreEqual("sprite_point_100", asset.ImportPreset, "预设名必须与 XLSX/Mirror 完全一致（契约字符串）");
            Assert.IsNotNull(asset.Sprite, "命中项必须持有 managed Sprite（生成根不完整属失败态）");
            Assert.IsNotEmpty(asset.SourceSha256, "应记录源图内容哈希");

            VisualAssetDefinition missing;
            Assert.IsFalse(catalog.TryGetById("va_does_not_exist", out missing), "未知 ID 必须不命中");
            Assert.IsNull(missing);
            Assert.IsFalse(catalog.TryGetById(null, out missing));
            Assert.IsFalse(catalog.Contains("va_does_not_exist"));
            StringAssert.Contains("va_does_not_exist", VisualAssetCatalog.BuildMissingDiagnostic("va_does_not_exist"));
        }

        // ---------------- P3：EnemyPresentationId 查询 ----------------

        [Test]
        public void P3_EnemyPresentationId_Query_AndLinksToVisualAsset()
        {
            Assert.IsTrue(StaticConfigBuilder.Build(true).Success);

            var root = RRPGStaticConfigRoot.Load();
            var presentations = root.EnemyPresentationDatabase;
            var catalog = root.VisualAssetCatalog;

            EnemyPresentationBinding binding;
            Assert.IsTrue(presentations.TryGetByMonsterId(KnownMonsterId, out binding), "已知 MonsterId 必须命中");
            Assert.AreEqual(KnownMonsterId, binding.MonsterId);
            Assert.AreEqual(KnownVisualAssetId, binding.VisualAssetId);

            string visualAssetId;
            Assert.IsTrue(presentations.TryGetVisualAssetId(KnownMonsterId, out visualAssetId));
            Assert.AreEqual(KnownVisualAssetId, visualAssetId);

            // 完整链路：MonsterId → VisualAssetId → Sprite（Slice 1C 消费者所需的最小链路）
            VisualAssetDefinition asset;
            Assert.IsTrue(catalog.TryGetById(visualAssetId, out asset));
            Assert.IsNotNull(asset.Sprite, "绑定 → 资源 → Sprite 链路必须闭合");

            // 每个绑定都必须能解析到资源（不允许悬空引用）
            foreach (var candidate in presentations.All)
            {
                Assert.IsTrue(catalog.Contains(candidate.VisualAssetId),
                    "绑定引用了不存在的 VisualAssetId：" + candidate);
            }

            EnemyPresentationBinding missing;
            Assert.IsFalse(presentations.TryGetByMonsterId("mon_does_not_exist", out missing), "未知 MonsterId 必须不命中");
            Assert.IsNull(missing);
            string ignored;
            Assert.IsFalse(presentations.TryGetVisualAssetId("mon_does_not_exist", out ignored));
            StringAssert.Contains("mon_does_not_exist",
                EnemyPresentationDatabase.BuildMissingDiagnostic("mon_does_not_exist"));
        }

        // ---------------- P4：Missing asset 明确失败 ----------------

        [Test]
        public void P4_MissingAsset_FailsExplicitly_WithoutTouchingGeneratedRoot()
        {
            Assert.IsTrue(StaticConfigBuilder.Build(true).Success);

            string rootFile = StaticConfigBuilder.Absolute(StaticConfigBuilder.RootAssetPath);
            byte[] before = File.ReadAllBytes(rootFile);
            string signatureBefore = RRPGStaticConfigRoot.Load().ContentSignature;

            string manifestPath = MirrorPath("AssetManifest.json");
            string original = File.ReadAllText(manifestPath, new UTF8Encoding(false));

            // 构造「VisualAsset 表仍登记资源，但 AssetManifest 没有对应条目」的 Mirror 半损坏状态。
            // 该 Manifest 形状仍然合法（assetCount 与条目数自洽），只有交叉校验才能发现
            // → 精确覆盖 Missing asset 路径，而不是普通的解析失败。
            File.WriteAllText(manifestPath, StripManifestAssets(original), new UTF8Encoding(false));

            try
            {
                var outcome = StaticConfigBuilder.Build(true);

                Assert.IsFalse(outcome.Success, "Missing asset 必须明确失败");
                StringAssert.Contains("Missing asset", outcome.Failure);
                Assert.AreEqual(before, File.ReadAllBytes(rootFile), "失败时不得部分写入正式产物");
                Assert.AreEqual(signatureBefore, RRPGStaticConfigRoot.Load().ContentSignature);
            }
            finally
            {
                File.WriteAllText(manifestPath, original, new UTF8Encoding(false));
                AssetDatabase.Refresh();
            }
        }

        // ---------------- P5：源图 → managed Sprite ----------------

        [Test]
        public void P5_ManagedSprite_IsByteIdenticalCopy_WithPresetApplied()
        {
            Assert.IsTrue(StaticConfigBuilder.Build(true).Success);

            string managedPath = StaticConfigBuilder.ManagedAssetsDir + "/" + KnownSourceFile;
            string managedAbsolute = StaticConfigBuilder.Absolute(managedPath);
            Assert.IsTrue(File.Exists(managedAbsolute), "managed Sprite 必须生成：" + managedPath);

            // 只落在 Managed Root 之内（源资产绝不进 Unity 生成根之外的路径）
            StringAssert.StartsWith(StaticConfigBuilder.ManagedRoot, managedPath);

            // 逐字节一致（复制，不重编码）
            string sourceAbsolute = Path.Combine(StaticConfigBuilder.RepoRoot(),
                StaticConfigBuilder.SourceAssetsRelativeDir.Replace('/', Path.DirectorySeparatorChar),
                KnownSourceFile.Replace('/', Path.DirectorySeparatorChar));
            Assert.IsTrue(File.Exists(sourceAbsolute), "源图必须存在：" + sourceAbsolute);
            Assert.AreEqual(File.ReadAllBytes(sourceAbsolute), File.ReadAllBytes(managedAbsolute),
                "managed 资产必须是源图的逐字节副本");

            // 预设生效（sprite_point_100）
            var importer = AssetImporter.GetAtPath(managedPath) as TextureImporter;
            Assert.IsNotNull(importer, "managed 资产必须有 TextureImporter");
            Assert.AreEqual(TextureImporterType.Sprite, importer.textureType);
            Assert.AreEqual(SpriteImportMode.Single, importer.spriteImportMode);
            Assert.AreEqual(StaticConfigBuilder.SpritePresetPixelsPerUnit, importer.spritePixelsPerUnit);
            Assert.AreEqual(FilterMode.Point, importer.filterMode);
            Assert.AreEqual(TextureImporterCompression.Uncompressed, importer.textureCompression);
            Assert.IsFalse(importer.mipmapEnabled, "sprite_point_100 不得启用 mipmap");

            // Sprite 必须可加载（catalog 已持有它）
            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<Sprite>(managedPath));

            // catalog 记录的源图哈希必须等于源图真实哈希（换图判定依据）
            var catalog = RRPGStaticConfigRoot.Load().VisualAssetCatalog;
            VisualAssetDefinition asset;
            catalog.TryGetById(KnownVisualAssetId, out asset);
            Assert.AreEqual(Sha256Hex(File.ReadAllBytes(sourceAbsolute)), asset.SourceSha256);
        }

        // ---------------- P6：no-op ----------------

        [Test]
        public void P6_SecondBuild_IsNoOp_AndDoesNotTouchManagedSprite()
        {
            Assert.IsTrue(StaticConfigBuilder.Build(true).Success);

            string[] guarded =
            {
                StaticConfigBuilder.Absolute(StaticConfigBuilder.RootAssetPath),
                StaticConfigBuilder.Absolute(StaticConfigBuilder.EnemyPresentationDatabasePath),
                StaticConfigBuilder.Absolute(StaticConfigBuilder.VisualAssetCatalogPath),
                StaticConfigBuilder.Absolute(StaticConfigBuilder.ManagedAssetsDir + "/" + KnownSourceFile)
            };

            var before = guarded.Select(File.GetLastWriteTimeUtc).ToArray();
            var bytesBefore = guarded.Select(File.ReadAllBytes).ToArray();

            var second = StaticConfigBuilder.Build(false);

            Assert.IsTrue(second.Success, second.Failure);
            Assert.IsTrue(second.NoOp, "相同输入必须走 no-op 路径");
            for (int i = 0; i < guarded.Length; i++)
            {
                Assert.AreEqual(before[i], File.GetLastWriteTimeUtc(guarded[i]),
                    "no-op 不得触碰产物（mtime 必须不变）：" + guarded[i]);
                Assert.AreEqual(bytesBefore[i], File.ReadAllBytes(guarded[i]),
                    "no-op 不得改动产物内容：" + guarded[i]);
            }
        }

        // ---------------- P7：rebuild from zero ----------------

        [Test]
        public void P7_RebuildFromZero_RestoresSpritesAndQueries()
        {
            Assert.IsTrue(StaticConfigBuilder.Build(true).Success);

            var outcome = StaticConfigBuilder.RebuildFromZero();
            Assert.IsTrue(outcome.Success, outcome.Failure);

            StaticConfigRegistry.Reset();
            var root = RRPGStaticConfigRoot.Load();
            Assert.IsNotNull(root, "从零重建后必须仍能通过 exact path 加载");

            var catalog = root.VisualAssetCatalog;
            VisualAssetDefinition asset;
            Assert.IsTrue(catalog.TryGetById(KnownVisualAssetId, out asset));
            Assert.IsNotNull(asset.Sprite, "从零重建后 Sprite 必须可恢复");

            EnemyPresentationBinding binding;
            Assert.IsTrue(root.EnemyPresentationDatabase.TryGetByMonsterId(KnownMonsterId, out binding));
            Assert.AreEqual(KnownVisualAssetId, binding.VisualAssetId);
        }

        // ---------------- 工具 ----------------

        private static string MirrorPath(string fileName)
        {
            return Path.Combine(StaticConfigBuilder.RepoRoot(),
                StaticConfigBuilder.MirrorRelativeDir.Replace('/', Path.DirectorySeparatorChar), fileName);
        }

        private static string ReadMirror(string fileName)
        {
            return File.ReadAllText(MirrorPath(fileName), new UTF8Encoding(false));
        }

        private static int CountOccurrences(string text, string pattern)
        {
            int count = 0;
            int index = 0;
            while ((index = text.IndexOf(pattern, index, StringComparison.Ordinal)) >= 0)
            {
                count++;
                index += pattern.Length;
            }
            return count;
        }

        private static string Sha256Hex(byte[] bytes)
        {
            using (var sha = System.Security.Cryptography.SHA256.Create())
            {
                var hash = sha.ComputeHash(bytes);
                var sb = new StringBuilder(hash.Length * 2);
                foreach (byte b in hash) sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }

        /// <summary>把 AssetManifest 的 assets 数组清空、assetCount 归零（保持格式合法）。</summary>
        private static string StripManifestAssets(string json)
        {
            const string countKey = "\"assetCount\": ";
            int countAt = json.IndexOf(countKey, StringComparison.Ordinal);
            Assert.Greater(countAt, -1, "AssetManifest 必须含 assetCount");

            int digitsStart = countAt + countKey.Length;
            int digitsEnd = digitsStart;
            while (digitsEnd < json.Length && char.IsDigit(json[digitsEnd])) digitsEnd++;

            string withZeroCount = json.Substring(0, digitsStart) + "0" + json.Substring(digitsEnd);

            const string arrayKey = "\"assets\": [";
            int arrayAt = withZeroCount.IndexOf(arrayKey, StringComparison.Ordinal);
            Assert.Greater(arrayAt, -1, "AssetManifest 必须含 assets 数组");

            int arrayOpen = withZeroCount.IndexOf('[', arrayAt);
            int arrayClose = withZeroCount.LastIndexOf(']');
            Assert.Greater(arrayClose, arrayOpen, "assets 数组必须完整");

            return withZeroCount.Substring(0, arrayOpen + 1) + withZeroCount.Substring(arrayClose);
        }
    }
}
