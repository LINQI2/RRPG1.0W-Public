using System.Collections.Generic;
using NUnit.Framework;

namespace RRPG.EventProduct.Tests
{
    /// <summary>
    /// WP-Event-1D · Gate D0 §7/§8/§9：**人物恢复期权威**（正式 Rest Authority）与旧档迁移。
    ///
    /// 保护的是契约而不是实现：
    ///   - 授予 → 拦截（含 Until 当天）→ 下月 1 日恢复；
    ///   - 只追加、绝不缩短（多次授予取最晚 Until）；
    ///   - Save / Restore 往返稳定；
    ///   - 旧档 `VillageSocialResponseSaveData.Rests` 幂等迁移，不猜 Person、不缩短。
    /// </summary>
    public class VillagePersonRestStateTests
    {
        private const string Person = "person_rest_001";

        [Test]
        public void Grant_RestBlocksUntilMonthEnd_AndRecoversNextMonth()
        {
            var rest = new VillagePersonRestState();

            Assert.IsFalse(rest.IsRestBlocked(Person, 2, 3, 15), "未授予恢复期时不应被拦截");

            Assert.IsTrue(rest.GrantRestUntil(Person, 2, 3, 31), "合法日期应授予成功");
            Assert.IsTrue(rest.IsRestBlocked(Person, 2, 3, 1), "授予后当月应被拦截");
            Assert.IsTrue(rest.IsRestBlocked(Person, 2, 3, 31), "含 Until 当天仍应被拦截");
            Assert.IsFalse(rest.IsRestBlocked(Person, 2, 4, 1), "下月 1 日起应恢复");
            Assert.IsFalse(rest.IsRestBlocked("person_other", 2, 3, 15), "不能影响其它人物");
        }

        [Test]
        public void Grant_RejectsInvalidInput_AndNeverShrinks()
        {
            var rest = new VillagePersonRestState();

            Assert.IsFalse(rest.GrantRestUntil(null, 2, 3, 31));
            Assert.IsFalse(rest.GrantRestUntil(Person, 0, 3, 31), "非法年份必须拒绝（不猜）");
            Assert.IsFalse(rest.GrantRestUntil(Person, 2, 13, 1), "非法月份必须拒绝");
            Assert.IsFalse(rest.GrantRestUntil(Person, 2, 2, 30), "非法日期必须拒绝（2 月没有 30 日）");
            Assert.IsFalse(rest.HasRest(Person), "非法输入不得写入任何记录");

            // 先短后长：查询必须取最晚 Until（不缩短 = 不被早期记录覆盖）
            Assert.IsTrue(rest.GrantRestUntil(Person, 2, 3, 10));
            Assert.IsTrue(rest.GrantRestUntil(Person, 2, 5, 10));

            var latest = rest.GetLatestRest(Person);
            Assert.IsNotNull(latest);
            Assert.AreEqual(5, latest.UntilMonth);
            Assert.IsTrue(rest.IsRestBlocked(Person, 2, 4, 15), "更晚的授予必须继续生效");
        }

        [Test]
        public void Save_Restore_RoundTrip()
        {
            var rest = new VillagePersonRestState();
            rest.GrantRestUntil(Person, 3, 7, 31);
            rest.GrantRestUntil("person_rest_002", 3, 7, 15);

            var saved = rest.ToSaveData();
            Assert.AreEqual(2, saved.Count);

            var loaded = new VillagePersonRestState();
            loaded.Restore(saved);

            Assert.IsTrue(loaded.IsRestBlocked(Person, 3, 7, 31));
            Assert.IsFalse(loaded.IsRestBlocked(Person, 3, 8, 1));
            Assert.AreEqual(2, loaded.Rests.Count);
        }

        [Test]
        public void Restore_NullOrGarbage_IsSafe()
        {
            var rest = new VillagePersonRestState();
            rest.GrantRestUntil(Person, 3, 7, 31);

            rest.Restore(null);
            Assert.AreEqual(0, rest.Rests.Count, "Restore(null) → 空状态");

            rest.Restore(new List<VillageRestSaveData>
            {
                null,
                new VillageRestSaveData { PersonId = null, UntilYear = 3, UntilMonth = 7, UntilDay = 31 },
                new VillageRestSaveData { PersonId = Person, UntilYear = 3, UntilMonth = 99, UntilDay = 1 },
                new VillageRestSaveData { PersonId = Person, UntilYear = 3, UntilMonth = 7, UntilDay = 31 }
            });

            Assert.AreEqual(1, rest.Rests.Count, "非法记录丢弃，只保留唯一合法记录");
            Assert.IsTrue(rest.IsRestBlocked(Person, 3, 7, 31));
        }

        // ---------------- §8 旧档迁移 ----------------

        [Test]
        public void MigrateFromLegacy_IsIdempotent_AndDoesNotGuessPerson()
        {
            var rest = new VillagePersonRestState();

            var legacy = new List<VillageRestSaveData>
            {
                new VillageRestSaveData { PersonId = Person, UntilYear = 4, UntilMonth = 6, UntilDay = 30 },
                new VillageRestSaveData { PersonId = null, UntilYear = 4, UntilMonth = 6, UntilDay = 30 },
                new VillageRestSaveData { PersonId = "person_legacy_002", UntilYear = 4, UntilMonth = 6, UntilDay = 10 }
            };

            Assert.AreEqual(2, rest.MigrateFromLegacy(legacy), "只迁移可识别 Person 的记录");
            Assert.AreEqual(0, rest.MigrateFromLegacy(legacy), "重复迁移必须幂等（0 条）");
            Assert.AreEqual(2, rest.Rests.Count, "不得产生重复记录");
            Assert.IsTrue(rest.IsRestBlocked(Person, 4, 6, 30), "旧档恢复期必须继续生效");
        }

        [Test]
        public void MigrateFromLegacy_DoesNotShrinkExistingLongerRest()
        {
            var rest = new VillagePersonRestState();
            rest.GrantRestUntil(Person, 4, 8, 31);   // 新的更长恢复期

            // 旧档里有一条更早的（同人物）→ 迁移不得缩短
            int migrated = rest.MigrateFromLegacy(new List<VillageRestSaveData>
            {
                new VillageRestSaveData { PersonId = Person, UntilYear = 4, UntilMonth = 6, UntilDay = 30 }
            });

            Assert.AreEqual(1, migrated);
            var latest = rest.GetLatestRest(Person);
            Assert.AreEqual(8, latest.UntilMonth, "迁移不得缩短已有恢复期（查询仍取最晚 Until）");
            Assert.IsTrue(rest.IsRestBlocked(Person, 4, 7, 15));
        }

        [Test]
        public void Rest_Boundary_IsInclusiveOnUntilDay()
        {
            // 2 月闰年边界：2024-02-29 合法；2023-02-29 非法
            var rest = new VillagePersonRestState();
            Assert.IsTrue(rest.GrantRestUntil(Person, 2024, 2, 29), "闰年 2 月 29 日合法");
            Assert.IsFalse(rest.GrantRestUntil("person_leap", 2023, 2, 29), "平年 2 月 29 日非法");
            Assert.IsTrue(rest.IsRestBlocked(Person, 2024, 2, 29));
            Assert.IsFalse(rest.IsRestBlocked(Person, 2024, 3, 1));
        }
    }
}
