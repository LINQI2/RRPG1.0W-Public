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
    /// M0 Builder / Runtime / Root 契约（FDS §10 / §11 / §12 / §13.2 / §13.3）。
    ///
    /// 所有测试只操作 Assets/Generated/StaticConfig（gitignored 的生成根）与
    /// Development/Generated/StaticConfig（Compiler 独占的 Mirror）；不触碰产品 Scene / Prefab / SO。
    /// </summary>
    public class StaticConfigBuilderTests
    {
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

        [Test]
        public void B1_Build_CreatesGeneratedRoot_AndExactPathLoadWorks()
        {
            var outcome = StaticConfigBuilder.Build(true);
            Assert.IsTrue(outcome.Success, outcome.Failure);

            Assert.IsTrue(File.Exists(StaticConfigBuilder.Absolute(StaticConfigBuilder.RootAssetPath)), "Root asset 未生成");
            Assert.IsTrue(File.Exists(StaticConfigBuilder.Absolute(StaticConfigBuilder.MonsterDatabasePath)));
            Assert.IsTrue(File.Exists(StaticConfigBuilder.Absolute(StaticConfigBuilder.EnemyPresentationDatabasePath)));
            Assert.IsTrue(File.Exists(StaticConfigBuilder.Absolute(StaticConfigBuilder.VisualAssetCatalogPath)));

            // exact single-root Load（不是 LoadAll 扫描模型）
            var loaded = RRPGStaticConfigRoot.Load();
            Assert.IsNotNull(loaded, "Resources.Load<RRPGStaticConfigRoot>(\"RRPGStaticConfigRoot\") 必须命中");
            Assert.AreEqual(RRPGStaticConfigRoot.SupportedFormatVersion, loaded.FormatVersion);
            Assert.IsNotEmpty(loaded.ContentSignature);
        }

        [Test]
        public void B2_SecondBuild_IsNoOp_AndDoesNotTouchArtifacts()
        {
            Assert.IsTrue(StaticConfigBuilder.Build(true).Success);

            string rootFile = StaticConfigBuilder.Absolute(StaticConfigBuilder.RootAssetPath);
            var mtime = File.GetLastWriteTimeUtc(rootFile);
            var signature = RRPGStaticConfigRoot.Load().ContentSignature;

            var second = StaticConfigBuilder.Build(false);

            Assert.IsTrue(second.Success, second.Failure);
            Assert.IsTrue(second.NoOp, "相同输入必须走 no-op 路径");
            Assert.AreEqual(mtime, File.GetLastWriteTimeUtc(rootFile), "no-op 不得触碰产物（mtime 必须不变）");
            Assert.AreEqual(signature, RRPGStaticConfigRoot.Load().ContentSignature);
        }

        [Test]
        public void B5_DeleteGeneratedRoot_ThenRebuild_RestoresLoadableRoot()
        {
            Assert.IsTrue(StaticConfigBuilder.Build(true).Success);

            var outcome = StaticConfigBuilder.RebuildFromZero();
            Assert.IsTrue(outcome.Success, outcome.Failure);
            Assert.IsTrue(File.Exists(StaticConfigBuilder.Absolute(StaticConfigBuilder.RootAssetPath)));

            StaticConfigRegistry.Reset();
            string diagnostic;
            Assert.IsTrue(StaticConfigRegistry.TryInitialize(out diagnostic), diagnostic);
            Assert.IsNotNull(RRPGStaticConfigRoot.Load(), "从零重建后必须仍能通过 exact path 加载");
        }

        [Test]
        public void B_OwnershipSafety_BuilderNeverDeletesOutsideManagedRoot()
        {
            Assert.IsTrue(StaticConfigBuilder.Build(true).Success);

            // 根外的兄弟文件（Assets/Generated/ 下，但**不在** Managed Root 之内）
            string sibling = StaticConfigBuilder.Absolute("Assets/Generated/StaticConfigSibling.tmp");
            File.WriteAllText(sibling, "decoy", new UTF8Encoding(false));

            try
            {
                // 根内的陈旧产物必须被回收
                string stale = StaticConfigBuilder.Absolute(StaticConfigBuilder.ResourcesDir + "/Stale.tmp");
                File.WriteAllText(stale, "stale", new UTF8Encoding(false));

                var outcome = StaticConfigBuilder.Build(true);

                Assert.IsTrue(outcome.Success, outcome.Failure);
                Assert.IsFalse(File.Exists(stale), "Managed Root 内的陈旧产物必须被回收");
                Assert.IsTrue(File.Exists(sibling), "Builder 绝不允许删出 Managed Root 之外");
                Assert.IsTrue(outcome.Actions.Any(a => a.StartsWith("deleted(stale):")), "必须记录 stale 回收动作");
            }
            finally
            {
                if (File.Exists(sibling)) File.Delete(sibling);
            }
        }

        [Test]
        public void B_InvalidMirrorJson_FailsWithoutPartialFormalOutput()
        {
            Assert.IsTrue(StaticConfigBuilder.Build(true).Success);

            string rootFile = StaticConfigBuilder.Absolute(StaticConfigBuilder.RootAssetPath);
            byte[] before = File.ReadAllBytes(rootFile);
            string signatureBefore = RRPGStaticConfigRoot.Load().ContentSignature;

            // 非法 Mirror（缺少 _generated.doNotEdit）→ 必须在写入前失败
            string broken = Path.Combine(StaticConfigBuilder.RepoRoot(),
                StaticConfigBuilder.MirrorRelativeDir.Replace('/', Path.DirectorySeparatorChar), "Broken.json");
            File.WriteAllText(broken, "{ \"oops\": ", new UTF8Encoding(false));

            try
            {
                var outcome = StaticConfigBuilder.Build(true);

                Assert.IsFalse(outcome.Success, "非法输入必须失败");
                Assert.IsNotEmpty(outcome.Failure);
                Assert.AreEqual(before, File.ReadAllBytes(rootFile), "失败时不得部分写入正式产物");
                Assert.AreEqual(signatureBefore, RRPGStaticConfigRoot.Load().ContentSignature);
            }
            finally
            {
                if (File.Exists(broken)) File.Delete(broken);
                AssetDatabase.Refresh();
            }
        }

        [Test]
        public void R_MissingRoot_ProducesExplicitDiagnostic()
        {
            Assert.IsTrue(StaticConfigBuilder.Build(true).Success);

            AssetDatabase.DeleteAsset(StaticConfigBuilder.RootAssetPath);
            AssetDatabase.Refresh();
            StaticConfigRegistry.Reset();

            try
            {
                string diagnostic;
                bool ok = StaticConfigRegistry.TryInitialize(out diagnostic);

                Assert.IsFalse(ok, "Root 缺失时必须返回 false（不得假装成功）");
                Assert.IsNotEmpty(diagnostic, "Root 缺失必须给出明确诊断");
                Assert.AreEqual(StaticConfigRegistry.MissingRootDiagnostic, diagnostic);
            }
            finally
            {
                StaticConfigBuilder.Build(true);
                StaticConfigRegistry.Reset();
            }
        }

        [Test]
        public void R_Registry_IsIdempotent_AndDeterministic()
        {
            Assert.IsTrue(StaticConfigBuilder.Build(true).Success);
            StaticConfigRegistry.Reset();

            string d1, d2, d3;
            Assert.IsTrue(StaticConfigRegistry.TryInitialize(out d1), d1);
            var first = StaticConfigRegistry.Root;

            Assert.IsTrue(StaticConfigRegistry.TryInitialize(out d2), d2);
            Assert.IsTrue(StaticConfigRegistry.TryInitialize(out d3), d3);

            Assert.AreSame(first, StaticConfigRegistry.Root, "重复初始化必须幂等（返回同一实例）");
            Assert.AreEqual(string.Empty, d1);
            Assert.AreEqual(string.Empty, d2);
            Assert.AreEqual(string.Empty, d3);
        }

        [Test]
        public void R_RuntimeAssembly_HasNoEditorDependency()
        {
            string runtimeDir = StaticConfigBuilder.Absolute("Assets/Scripts/StaticConfig");

            foreach (string file in Directory.GetFiles(runtimeDir, "*.cs"))
            {
                // 只扫**代码**：注释里出现「禁止使用 AssetDatabase」这类说明不算依赖。
                var code = new StringBuilder();
                foreach (string line in File.ReadAllLines(file))
                {
                    string trimmed = line.TrimStart();
                    if (trimmed.StartsWith("//", System.StringComparison.Ordinal)) continue;
                    if (trimmed.StartsWith("*", System.StringComparison.Ordinal)) continue;
                    code.AppendLine(line);
                }

                string text = code.ToString();
                Assert.IsFalse(text.Contains("AssetDatabase"), "Runtime 不得引用 AssetDatabase：" + file);
                Assert.IsFalse(text.Contains("UnityEditor"), "Runtime 不得引用 UnityEditor：" + file);
                Assert.IsFalse(text.Contains("Resources.LoadAll"), "Runtime 不得使用 Resources.LoadAll：" + file);
                Assert.IsFalse(text.Contains("FindObjectOfType"), "Runtime 不得使用场景搜索：" + file);
            }

            string asmdef = File.ReadAllText(Path.Combine(runtimeDir, "StaticConfig.Runtime.asmdef"));
            Assert.IsFalse(asmdef.Contains("Editor"), "Runtime asmdef 不得引用 Editor assembly");
        }

        [Test]
        public void Gate_NoSceneOrPrefab_ReferencesGeneratedAssets()
        {
            Assert.IsTrue(StaticConfigBuilder.Build(true).Success);

            string rootGuid = AssetDatabase.AssetPathToGUID(StaticConfigBuilder.RootAssetPath);
            string monstersGuid = AssetDatabase.AssetPathToGUID(StaticConfigBuilder.MonsterDatabasePath);
            Assert.IsNotEmpty(rootGuid);
            Assert.IsNotEmpty(monstersGuid);

            string assetsRoot = Application.dataPath;
            var targets = Directory.GetFiles(assetsRoot, "*.unity", SearchOption.AllDirectories)
                .Concat(Directory.GetFiles(assetsRoot, "*.prefab", SearchOption.AllDirectories))
                .ToList();

            Assert.Greater(targets.Count, 0, "必须真的扫描到 Scene / Prefab（否则该 Gate 无意义）");

            foreach (string file in targets)
            {
                string text = File.ReadAllText(file);
                Assert.IsFalse(text.Contains(rootGuid), "Scene/Prefab 不得直接引用 Generated Root GUID：" + file);
                Assert.IsFalse(text.Contains(monstersGuid), "Scene/Prefab 不得直接引用 per-domain 生成资产 GUID：" + file);
            }
        }
    }
}
