using NUnit.Framework;

namespace RRPG.EventProduct.Tests
{
    /// <summary>
    /// WP-Event-1D · Gate D1 §7：**Village 绝对日序**（Event World History / Repeat / Cooldown 的日单位）。
    ///
    /// 保护的是与 `VillageTime` 的一致性：不发明日历、不使用现实世界时间；
    /// 同日期同序、次日 +1、跨月 / 跨年 +1、闰年 2/28 → 2/29 → 3/1 连续；非法日期明确返回 0。
    /// </summary>
    public class VillageDayOrdinalTests
    {
        [Test]
        public void ToOrdinal_FirstDayOfVillage_IsOne()
        {
            Assert.AreEqual(1, VillageDayOrdinal.ToOrdinal(1, 1, 1));
            Assert.AreEqual(2, VillageDayOrdinal.ToOrdinal(1, 1, 2), "次日严格 +1");
        }

        [Test]
        public void ToOrdinal_CrossMonth_And_CrossYear()
        {
            Assert.AreEqual(VillageDayOrdinal.ToOrdinal(1, 1, 31) + 1, VillageDayOrdinal.ToOrdinal(1, 2, 1),
                "跨月 +1");
            Assert.AreEqual(32, VillageDayOrdinal.ToOrdinal(1, 2, 1), "1 月 31 天 → 2 月 1 日 = 第 32 天");

            Assert.AreEqual(VillageDayOrdinal.ToOrdinal(1, 12, 31) + 1, VillageDayOrdinal.ToOrdinal(2, 1, 1),
                "跨年 +1");
            Assert.AreEqual(366, VillageDayOrdinal.ToOrdinal(2, 1, 1), "1 年非闰年 → 2 年 1 月 1 日 = 第 366 天");
        }

        [Test]
        public void ToOrdinal_LeapYear_FebruaryIsContinuous()
        {
            // 2024 闰年：2/28 → 2/29 → 3/1 连续（不得缺号、不得重复）
            int d28 = VillageDayOrdinal.ToOrdinal(2024, 2, 28);
            int d29 = VillageDayOrdinal.ToOrdinal(2024, 2, 29);
            int d31 = VillageDayOrdinal.ToOrdinal(2024, 3, 1);

            Assert.AreEqual(d28 + 1, d29);
            Assert.AreEqual(d29 + 1, d31);

            // 非闰年不存在 2/29；世纪年按 Gregorian（2100 非闰、2000 闰）
            Assert.AreEqual(VillageDayOrdinal.Invalid, VillageDayOrdinal.ToOrdinal(2023, 2, 29));
            Assert.AreEqual(VillageDayOrdinal.Invalid, VillageDayOrdinal.ToOrdinal(2100, 2, 29));
            Assert.AreEqual(VillageDayOrdinal.ToOrdinal(2000, 2, 28) + 1,
                VillageDayOrdinal.ToOrdinal(2000, 2, 29));
        }

        [Test]
        public void ToOrdinal_InvalidInput_IsZeroAndNeverGuessed()
        {
            Assert.AreEqual(VillageDayOrdinal.Invalid, VillageDayOrdinal.ToOrdinal(0, 1, 1));
            Assert.AreEqual(VillageDayOrdinal.Invalid, VillageDayOrdinal.ToOrdinal(1, 13, 1));
            Assert.AreEqual(VillageDayOrdinal.Invalid, VillageDayOrdinal.ToOrdinal(1, 1, 0));
            Assert.AreEqual(VillageDayOrdinal.Invalid, VillageDayOrdinal.ToOrdinal(1, 2, 30));
            Assert.AreEqual(VillageDayOrdinal.Invalid, VillageDayOrdinal.ToOrdinal(10000, 1, 1),
                "超出防御性年份上限 → 无效（不溢出）");

            Assert.IsFalse(VillageDayOrdinal.IsValidDate(2023, 2, 29));
            Assert.IsTrue(VillageDayOrdinal.IsValidDate(2024, 2, 29));
        }

        [Test]
        public void FromVillageTime_MatchesDirectConversion_AndIsMonotonicDayByDay()
        {
            var time = new VillageTime { SecondsPerDay = 1f };
        time.Restore(new VillageTimeSaveData { Year = 1, Month = 12, Day = 30, Speed = (int)VillageTime.VillageTimeSpeed.Normal });
            var intradaySetup1 = time.ToSaveData();
            intradaySetup1.IntradayTime = null;
            intradaySetup1.Speed = (int)(VillageTime.VillageTimeSpeed.Normal);
            time.Restore(intradaySetup1);


            Assert.AreEqual(VillageDayOrdinal.ToOrdinal(1, 12, 30), VillageDayOrdinal.FromVillageTime(time));

            int previous = VillageDayOrdinal.FromVillageTime(time);
            for (int i = 0; i < 3; i++)
            {
                time.Update(1f);
                int current = VillageDayOrdinal.FromVillageTime(time);
                Assert.AreEqual(previous + 1, current, "每过一天必须严格 +1（第 " + (i + 1) + " 天）");
                previous = current;
            }

            Assert.AreEqual(2, time.Year);
            Assert.AreEqual(1, time.Month);
            Assert.AreEqual(2, time.Day, "1年12月30日 + 3 天 = 2年1月2日（含跨月与跨年）");
        }

        [Test]
        public void FromVillageTime_NullTime_IsInvalid()
        {
            Assert.AreEqual(VillageDayOrdinal.Invalid, VillageDayOrdinal.FromVillageTime(null));
        }

        [Test]
        public void Ordinal_IsStrictlyIncreasingOverLongRun()
        {
            var time = new VillageTime { SecondsPerDay = 1f };
        time.Restore(new VillageTimeSaveData { Year = 1, Month = 1, Day = 1, Speed = (int)VillageTime.VillageTimeSpeed.Normal });
            var intradaySetup2 = time.ToSaveData();
            intradaySetup2.IntradayTime = null;
            intradaySetup2.Speed = (int)(VillageTime.VillageTimeSpeed.Normal);
            time.Restore(intradaySetup2);


            int previous = VillageDayOrdinal.FromVillageTime(time);
            Assert.AreEqual(1, previous);

            // 800 天覆盖 1 年 + 闰年边界（V0.1 起始年不冻结，这里只验证连续性）
            for (int i = 0; i < 800; i++)
            {
                time.Update(1f);
                int current = VillageDayOrdinal.FromVillageTime(time);
                Assert.AreEqual(previous + 1, current, "第 " + (i + 2) + " 天出现跳号 / 重复");
                previous = current;
            }

            Assert.AreEqual(801, previous);
        }
    }
}
