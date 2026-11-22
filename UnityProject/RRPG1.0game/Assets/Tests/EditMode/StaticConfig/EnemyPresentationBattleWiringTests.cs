using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using RRPG.StaticConfig;
using RRPG.StaticConfig.EditorTools;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace RRPG.StaticConfig.Tests
{
    /// <summary>
    /// M3 Slice 1D-1：BattleUI 的敌方身体视觉接线到 Enemy Presentation Authority。
    ///
    /// 覆盖（EditMode）：
    ///   W1 BattleUI 暴露 Prep / Battle 共用的 Resolver 与敌方唯一入口；
    ///   W2 运行时代码里 enemyBodyPlaceholder 已物理删除（M4）+ 旧布尔入口已删除；
    ///   W3 Resolver 返回正式 Sprite（与 Catalog 同一实例）；
    ///   W4 三只怪物 → 三个不同 VisualAssetId → 三个不同 Sprite（资产路径也不同）；
    ///   W5 缺配置 → fallback + WarnOnce（fallback 只在 Resolver 内）；
    ///   W6 BattleUI.ApplyEnemyBodyVisual 的行为（合成卡：写入 Resolver 返回的 Sprite；未登记 → fallback）。
    ///
    /// BattleUI 位于预定义程序集 Assembly-CSharp（测试程序集不编译期引用），因此用反射访问。
    /// </summary>
    public class EnemyPresentationBattleWiringTests
    {
        private const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic
            | BindingFlags.Instance | BindingFlags.Static;

        private const string KnownMonsterId = "mon_001";

        private static Type BattleUIType()
        {
            return Type.GetType("BattleUI, Assembly-CSharp");
        }

        [SetUp]
        public void SetUp()
        {
            StaticConfigRegistry.Reset();
            Assert.IsTrue(StaticConfigBuilder.Build(false).Success, "生成根必须先可构建");
        }

        [TearDown]
        public void TearDown()
        {
            StaticConfigBuilder.Build(true);
            StaticConfigRegistry.Reset();
            LogAssert.ignoreFailingMessages = false;
        }

        // ---------------- W1：BattleUI 暴露共用 Resolver + 敌方唯一入口 ----------------

        [Test]
        public void W1_BattleUI_ExposesSharedResolverAndEnemyEntry()
        {
            var type = BattleUIType();
            Assert.IsNotNull(type, "BattleUI 未找到（Assembly-CSharp）");

            var property = type.GetProperty("PresentationResolver", F);
            Assert.IsNotNull(property, "BattleUI 必须暴露 PresentationResolver（Prep 与 Battle 共用实例）");
            Assert.AreEqual(typeof(EnemyPresentationResolver), property.PropertyType,
                "PresentationResolver 必须是 Enemy Presentation Authority 的 Resolver");

            var method = type.GetMethod("ApplyEnemyBodyVisual", F);
            Assert.IsNotNull(method, "BattleUI 必须提供敌方身体视觉唯一入口 ApplyEnemyBodyVisual(card, monsterId)");

            var parameters = method.GetParameters();
            Assert.AreEqual(2, parameters.Length, "ApplyEnemyBodyVisual 只接受 (card, monsterId)");
            Assert.AreEqual(typeof(GameObject), parameters[0].ParameterType);
            Assert.AreEqual(typeof(string), parameters[1].ParameterType, "第二参数必须是 MonsterId");
        }

        // ---------------- W2：旧 enemyBodyPlaceholder 已物理删除（M4 / M3-R2） ----------------

        [Test]
        public void W2_LegacyEnemyPlaceholder_IsPhysicallyRemoved()
        {
            // M4：字段已物理删除 → 运行时代码里不得再出现该标识符（不再是「只剩声明」）。
            string scriptsRoot = StaticConfigBuilder.Absolute("Assets/Scripts");
            string[] files = Directory.GetFiles(scriptsRoot, "*.cs", SearchOption.AllDirectories);
            Assert.Greater(files.Length, 0);

            var offenders = new StringBuilder();
            int offenderFiles = 0;
            foreach (string file in files)
            {
                if (CountOccurrences(ReadCode(file), "enemyBodyPlaceholder") == 0) continue;

                offenderFiles++;
                if (offenders.Length > 0) offenders.Append(", ");
                offenders.Append(Path.GetFileName(file));
            }

            Assert.AreEqual(0, offenderFiles,
                "M4：运行时代码不得再出现 enemyBodyPlaceholder（字段已物理删除），实际=" + offenders);

            // 反射层：BattleUI 不得再声明该字段（Scene 序列化条目已随之清除）
            var type = BattleUIType();
            Assert.IsNotNull(type);
            Assert.IsNull(type.GetField("enemyBodyPlaceholder", F),
                "M4：BattleUI.enemyBodyPlaceholder 字段必须已删除");

            // 旧「按阵营取占位」入口（读 enemyBodyPlaceholder 的那条路径）必须已删除，
            // 否则它会成为第二条敌方取图路径。
            Assert.IsNull(type.GetMethod("ApplyUnitBodyVisual", F),
                "旧 ApplyUnitBodyVisual(card, isPlayer) 必须已删除（敌方取图只允许经 Resolver）");
        }

        // ---------------- W3：Resolver 返回正式 Sprite ----------------

        [Test]
        public void W3_Resolver_ReturnsFormalSprite_FromCatalog()
        {
            var resolver = EnemyPresentationResolver.FromStaticConfig();

            Sprite sprite;
            string diagnostic;
            Assert.IsTrue(resolver.TryResolve(KnownMonsterId, out sprite, out diagnostic), diagnostic);
            Assert.AreEqual(string.Empty, diagnostic, "正式路径不得有诊断");
            Assert.IsFalse(EnemyPresentationResolver.WarnIssued, "正式路径不得触发回退告警");

            VisualAssetDefinition asset;
            Assert.IsTrue(StaticConfigRegistry.VisualAssets.TryGetById("va_enemy_mon_001", out asset));
            Assert.AreSame(asset.Sprite, sprite, "Resolver 必须返回 Catalog 中登记的 Sprite");
            Assert.AreSame(sprite, resolver.Resolve(KnownMonsterId));
        }

        // ---------------- W4：一怪一图（不同 MonsterId → 不同 Sprite） ----------------

        [Test]
        public void W4_CurrentMonsters_ResolveToDistinctFormalSprites()
        {
            var resolver = EnemyPresentationResolver.FromStaticConfig();
            var presentations = StaticConfigRegistry.EnemyPresentation;
            var catalog = StaticConfigRegistry.VisualAssets;

            // WP-Encounter-1A-R：期望值来自当前 Mirror 的绑定条数，而不是写死「3 只怪物」。
            // 制作人正常增加怪物（第 7 / 20 / 100 只）时，本用例只要求「一怪一图」这条契约继续成立。
            int boundCount = StaticConfigTestAuthority.MirrorRowCount("EnemyPresentation");
            Assert.Greater(boundCount, 0, "当前 Mirror 至少应有 1 条绑定（否则本用例无意义）");
            Assert.AreEqual(boundCount, presentations.Count, "绑定条数必须等于 Canonical Mirror 的行数");

            var spritesByMonster = new Dictionary<string, Sprite>(StringComparer.Ordinal);
            var assetIds = new HashSet<string>(StringComparer.Ordinal);
            var assetPaths = new HashSet<string>(StringComparer.Ordinal);

            foreach (var binding in presentations.All)
            {
                Sprite sprite;
                string diagnostic;
                Assert.IsTrue(resolver.TryResolve(binding.MonsterId, out sprite, out diagnostic),
                    binding + " → " + diagnostic);
                Assert.IsNotNull(sprite);
                Assert.IsNotNull(catalog);

                spritesByMonster[binding.MonsterId] = sprite;
                assetIds.Add(binding.VisualAssetId);

                string path = AssetDatabase.GetAssetPath(sprite);
                Assert.IsNotEmpty(path, "Sprite 必须来自生成资产（有资产路径）：" + binding);
                Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<Sprite>(path),
                    "生成资产路径必须可加载为 Sprite：" + path);
                assetPaths.Add(path);
            }

            // 核心契约（与内容数量无关）：每个 MonsterId 只有一个 Sprite；不同怪 → 不同 VisualAssetId
            // → 不同生成资产路径 → 不同 Sprite 实例（「一怪一图」）。
            Assert.AreEqual(presentations.Count, spritesByMonster.Count, "每个绑定都必须恰好对应一个 MonsterId");
            Assert.AreEqual(presentations.Count, assetIds.Count, "每只怪物必须各自绑定不同的 VisualAssetId");
            Assert.AreEqual(presentations.Count, assetPaths.Count, "每只怪物必须来自不同的生成资产（一怪一图）");

            var distinctSprites = new HashSet<Sprite>(spritesByMonster.Values);
            Assert.AreEqual(presentations.Count, distinctSprites.Count, "每只怪物必须得到不同的 Sprite 实例");

            // 刻意**不**断言「临时素材尺寸两两不同」：那是当前临时素材包的内容特征，不是系统契约
            //（正式美术可能使用统一尺寸）；「一怪一图」已由上面的 assetIds / assetPaths / Sprite 实例三重互异守护。
        }

        // ---------------- W5：fallback + WarnOnce（只在 Resolver 内） ----------------

        [Test]
        public void W5_MissingPresentation_FallsBackInsideResolverOnly()
        {
            LogAssert.ignoreFailingMessages = true;   // 回退告警由 WarnIssued / WarnCount 断言

            EnemyPresentationResolver.ResetWarnOnce();
            var resolver = EnemyPresentationResolver.FromStaticConfig();

            Sprite sprite;
            string diagnostic;
            Assert.IsFalse(resolver.TryResolve("mon_not_registered", out sprite, out diagnostic));
            Assert.AreSame(EnemyPresentationResolver.FallbackSprite, sprite, "回退必须给出 fallback sprite");
            StringAssert.StartsWith(EnemyPresentationResolver.MissingPresentationDiagnosticPrefix, diagnostic);
            Assert.IsTrue(EnemyPresentationResolver.WarnIssued, "回退必须产生 WarnOnce（不静默）");
            Assert.AreEqual(1, EnemyPresentationResolver.WarnCount);

            // Resolve（BattleUI 使用的入口）永不返回 null
            Assert.AreSame(EnemyPresentationResolver.FallbackSprite, resolver.Resolve("mon_not_registered"));
            Assert.AreEqual(2, EnemyPresentationResolver.WarnCount, "每次回退都计数（日志仍只一次）");
        }

        // ---------------- W6：BattleUI 敌方入口的行为 ----------------

        [Test]
        public void W6_BattleUI_ApplyEnemyBodyVisual_WritesResolvedSprite()
        {
            LogAssert.ignoreFailingMessages = true;   // 未登记分支会触发 Resolver 的回退告警

            var type = BattleUIType();
            Assert.IsNotNull(type, "BattleUI 未找到");

            GameObject host = null;
            GameObject card = null;

            try
            {
                host = new GameObject("BattleUI_M3WiringTestHost");
                var battleUI = host.AddComponent(type);
                Assert.IsNotNull(battleUI, "无法创建 BattleUI 组件");

                // 只验证表现写入，不跑 BattleUI.Start（其产品依赖在测试里不完整）→ 立即禁用组件。
                ((Behaviour)battleUI).enabled = false;

                // 合成 UnitCard：Anchor_Visual/Body_Visual（与产品 prefab 同结构；不改 Scene / Prefab）
                card = new GameObject("Card_m3_wiring_test");
                var anchor = new GameObject("Anchor_Visual");
                anchor.transform.SetParent(card.transform, false);
                var body = new GameObject("Body_Visual", typeof(Image));
                body.transform.SetParent(anchor.transform, false);

                var resolverProperty = type.GetProperty("PresentationResolver", F);
                var resolver = resolverProperty.GetValue(battleUI, null);
                Assert.IsNotNull(resolver, "BattleUI.PresentationResolver 不可用");
                Assert.AreSame(resolver, resolverProperty.GetValue(battleUI, null),
                    "Prep 与 Battle 必须共用同一 Resolver 实例");

                var method = type.GetMethod("ApplyEnemyBodyVisual", F);
                var resolverResolve = resolver.GetType().GetMethod("Resolve", F);
                var image = body.GetComponent<Image>();

                // 已登记 MonsterId → 正式 Sprite
                Assert.IsTrue((bool)method.Invoke(battleUI, new object[] { card, KnownMonsterId }));
                var expected = (Sprite)resolverResolve.Invoke(resolver, new object[] { KnownMonsterId });
                Assert.AreSame(expected, image.sprite, "BattleUI 必须写入 Resolver 返回的正式 Sprite");
                Assert.IsFalse(image.raycastTarget, "视觉节点不得拦截 raycast（Root Button 保持唯一交互）");

                // 未登记 MonsterId → Resolver 的 fallback（BattleUI 不判断、只显示）
                Assert.IsTrue((bool)method.Invoke(battleUI, new object[] { card, "mon_not_registered" }));
                Assert.AreSame(EnemyPresentationResolver.FallbackSprite, image.sprite,
                    "缺失配置时必须显示 Resolver 的 fallback sprite（BattleUI 不得自行处理）");

                // card 为空 → 安全返回 false（不抛）
                Assert.IsFalse((bool)method.Invoke(battleUI, new object[] { null, KnownMonsterId }));
            }
            finally
            {
                if (card != null) UnityEngine.Object.DestroyImmediate(card);
                if (host != null) UnityEngine.Object.DestroyImmediate(host);
                AssetDatabase.Refresh();
            }
        }

        // ---------------- 工具 ----------------

        /// <summary>只扫**代码**：注释里提到旧字段名不算读取。</summary>
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
    }
}
