using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using RRPG.StaticConfig;
using RRPG.StaticConfig.EditorTools;

namespace RRPG.StaticConfig.Tests
{
    /// <summary>
    /// WP-Event-1A：Event 领域（统一事件内容基础）在 Unity 生成链上的契约。
    ///
    /// 覆盖：
    ///   - Canonical Mirror → EventConfigDatabase 生成（十张表的条数与 Mirror 一致）；
    ///   - content-signature no-op（相同输入不触碰产物）；
    ///   - StaticConfigRegistry 只读入口（lazy / 幂等 / 明确诊断 / 身份查找）；
    ///   - 共享 VisualAsset Authority 迁移后，EnemyPresentation 绑定仍能在同一份 Catalog 中解析到 Sprite
    ///     （这就是 1A 要求保护的「MonsterId → EnemyPresentation → VisualAsset → Sprite」）。
    ///
    /// 断言一律从 **Canonical Mirror 派生期望值**（与 StaticConfigTestAuthority 同一原则）：
    /// 制作人正常增删事件内容不应该让长期契约测试变红。
    /// </summary>
    public class EventConfigBuilderTests
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

        // ---------------- Mirror 期望值（内容无关） ----------------

        /// <summary>从 Mirror 的 rows 段取出某身份字段的全部取值（内容数量变化时自动跟随）。</summary>
        private static List<string> IdsFromMirror(string mirrorFile, string idField)
        {
            string json = StaticConfigTestAuthority.ReadMirror(mirrorFile);

            int rowsAt = json.IndexOf("\"rows\": [", StringComparison.Ordinal);
            Assert.Greater(rowsAt, -1, mirrorFile + " 必须包含 rows 数组");
            string rows = json.Substring(rowsAt);

            var ids = new List<string>();
            string key = "\"" + idField + "\": \"";

            int at = 0;
            while (true)
            {
                at = rows.IndexOf(key, at, StringComparison.Ordinal);
                if (at < 0) break;

                int start = at + key.Length;
                int close = rows.IndexOf('"', start);
                Assert.Greater(close, start, mirrorFile + " 的 " + idField + " 字面量必须闭合");

                ids.Add(rows.Substring(start, close - start));
                at = close;
            }

            Assert.Greater(ids.Count, 0, mirrorFile + " 必须至少有一条 " + idField);
            return ids;
        }

        private static string DatabasePath()
        {
            return StaticConfigBuilder.Absolute(StaticConfigBuilder.EventDatabasePath);
        }

        // ---------------- 生成链 ----------------

        [Test]
        public void E1_Build_GeneratesEventDatabase_MatchingMirrorCounts()
        {
            var outcome = StaticConfigBuilder.Build(true);
            Assert.IsTrue(outcome.Success, outcome.Failure);

            Assert.IsTrue(File.Exists(DatabasePath()),
                "EventConfigDatabase 未生成：" + StaticConfigBuilder.EventDatabasePath);

            var root = RRPGStaticConfigRoot.Load();
            Assert.IsNotNull(root, "生成 Root 必须可加载");
            Assert.IsNotNull(root.EventDatabase, "Root 必须挂上 EventConfigDatabase");

            var database = root.EventDatabase;
            Assert.AreEqual(StaticConfigTestAuthority.MirrorRowCount("Event"), database.EventCount);
            Assert.AreEqual(StaticConfigTestAuthority.MirrorRowCount("EventTrigger"), database.TriggerCount);
            Assert.AreEqual(StaticConfigTestAuthority.MirrorRowCount("EventRole"), database.RoleCount);
            Assert.AreEqual(StaticConfigTestAuthority.MirrorRowCount("EventRoleCondition"), database.RoleConditionCount);
            Assert.AreEqual(StaticConfigTestAuthority.MirrorRowCount("EventCondition"), database.ConditionCount);
            Assert.AreEqual(StaticConfigTestAuthority.MirrorRowCount("EventPresentation"), database.PresentationCount);
            Assert.AreEqual(StaticConfigTestAuthority.MirrorRowCount("EventOption"), database.OptionCount);
            Assert.AreEqual(StaticConfigTestAuthority.MirrorRowCount("EventOptionCondition"), database.OptionConditionCount);
            Assert.AreEqual(StaticConfigTestAuthority.MirrorRowCount("EventOutcome"), database.OutcomeCount);
            Assert.AreEqual(StaticConfigTestAuthority.MirrorRowCount("EventEffect"), database.EffectCount);

            Assert.IsNotEmpty(database.SourceMirrorSha256, "Event 库必须记录来源 Mirror 指纹");
            Assert.AreEqual(RRPGStaticConfigRoot.SupportedFormatVersion, database.FormatVersion);
        }

        [Test]
        public void E2_SecondBuild_IsNoOp_AndDoesNotTouchEventDatabase()
        {
            Assert.IsTrue(StaticConfigBuilder.Build(true).Success);

            var mtime = File.GetLastWriteTimeUtc(DatabasePath());

            var second = StaticConfigBuilder.Build(false);
            Assert.IsTrue(second.Success, second.Failure);
            Assert.IsTrue(second.NoOp, "相同输入必须走 no-op 路径");
            Assert.AreEqual(mtime, File.GetLastWriteTimeUtc(DatabasePath()), "no-op 不得触碰 Event 生成库");
        }
        // ---------------- Registry 只读入口 ----------------

        [Test]
        public void E3_Registry_ProvidesReadOnlyEventConfig()
        {
            Assert.IsTrue(StaticConfigBuilder.Build(false).Success);

            EventConfigDatabase database;
            string diagnostic;
            Assert.IsTrue(StaticConfigRegistry.TryGetEventConfig(out database, out diagnostic), diagnostic);
            Assert.IsEmpty(diagnostic, "正式且非空时诊断必须为空");
            Assert.Greater(database.TotalCount, 0);

            // 身份查找可用（这是「只读访问」，不是事件求解）
            foreach (string eventId in IdsFromMirror("Event.json", "EventId"))
            {
                EventDefinition found;
                Assert.IsTrue(database.TryGetEvent(eventId, out found), "必须能按 EventId 命中：" + eventId);
                Assert.AreEqual(eventId, found.EventId);
            }

            EventDefinition missing;
            Assert.IsFalse(database.TryGetEvent("evt_does_not_exist", out missing), "未命中必须返回 false");

            // 幂等：重复读取必须是同一个实例（lazy + 缓存，不重复解析）
            Assert.AreSame(database, StaticConfigRegistry.EventConfig);
        }

        [Test]
        public void E4_EventPresentation_IllustrationReferences_ResolveToSharedCatalog()
        {
            Assert.IsTrue(StaticConfigBuilder.Build(false).Success);

            var database = StaticConfigRegistry.EventConfig;
            Assert.IsNotNull(database);

            var catalog = StaticConfigRegistry.VisualAssets;
            Assert.IsNotNull(catalog, StaticConfigRegistry.VisualAssetDiagnostic);

            foreach (var presentation in database.Presentations)
            {
                // 插图是可选的（当前没有正式 Event PNG 时允许为空）；非空则必须命中共享 VisualAsset。
                if (string.IsNullOrEmpty(presentation.IllustrationAssetId)) continue;

                Assert.IsTrue(catalog.Contains(presentation.IllustrationAssetId),
                    "事件插图引用了不存在的 VisualAsset：" + presentation.IllustrationAssetId);
            }
        }

        // ---------------- Enemy Regression（VisualAsset Authority 迁移后） ----------------

        [Test]
        public void E5_EnemyPresentation_StillResolvesThroughSharedVisualAssetCatalog()
        {
            Assert.IsTrue(StaticConfigBuilder.Build(false).Success);

            var catalog = StaticConfigRegistry.VisualAssets;
            Assert.IsNotNull(catalog, StaticConfigRegistry.VisualAssetDiagnostic);
            Assert.Greater(catalog.Count, 0, "共享 VisualAsset 不得为空（迁移后仍必须可用）");

            // 1) 迁移无损：Mirror 里的每个 VisualAssetId 都能在同一份 Catalog 中命中
            foreach (string visualAssetId in IdsFromMirror("VisualAsset.json", "VisualAssetId"))
            {
                Assert.IsTrue(catalog.Contains(visualAssetId),
                    "VisualAsset 缺失（Authority 迁移后必须无损）：" + visualAssetId);
            }

            var presentations = StaticConfigRegistry.EnemyPresentation;
            Assert.IsNotNull(presentations, StaticConfigRegistry.EnemyPresentationDiagnostic);

            // 2) 链路仍成立：MonsterId → EnemyPresentation → VisualAsset → Sprite
            foreach (string monsterId in IdsFromMirror("EnemyPresentation.json", "MonsterId"))
            {
                EnemyPresentationBinding binding;
                Assert.IsTrue(presentations.TryGetByMonsterId(monsterId, out binding),
                    "EnemyPresentation 必须仍能解析：" + monsterId);
                Assert.IsFalse(string.IsNullOrEmpty(binding.VisualAssetId));

                VisualAssetDefinition asset;
                Assert.IsTrue(catalog.TryGetById(binding.VisualAssetId, out asset),
                    "绑定引用的 VisualAsset 必须存在于共享 Catalog：" + binding.VisualAssetId);
                Assert.IsNotNull(asset.Sprite, "共享 Catalog 里的 Sprite 必须真实存在：" + binding.VisualAssetId);
            }
        }
    }
}
