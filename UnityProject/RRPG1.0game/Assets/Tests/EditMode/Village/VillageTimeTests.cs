using NUnit.Framework;

/// <summary>
/// VillageTime 纯逻辑测试（Village.Runtime 强类型）。
/// 覆盖：正常时间推进、Pause / Resume、Speed 倍率、Day / Month 边界、闰年、
/// JumpToNextMonth 完整执行中间 Tick（不漏 Tick）、State 随时间变化闭环、Save / Load 快照。
/// </summary>
public class VillageTimeTests
{
    [TestCase(VillageTime.VillageTimeSpeed.Normal, 1440f)]
    [TestCase(VillageTime.VillageTimeSpeed.Slow, 2880f)]
    [TestCase(VillageTime.VillageTimeSpeed.Fast, 360f)]
    [TestCase(VillageTime.VillageTimeSpeed.Double, 720f)]
    [TestCase(VillageTime.VillageTimeSpeed.Fast8, 180f)]
    [TestCase(VillageTime.VillageTimeSpeed.Fast12, 120f)]
    [TestCase(VillageTime.VillageTimeSpeed.Fast24, 60f)]
    public void ProductDefault_RealSecondsPerDay(VillageTime.VillageTimeSpeed speed, float seconds)
    {
        var time = new VillageTime();
        Assert.AreEqual(1440f, time.SecondsPerDay);
        time.SetSpeed(speed);
        time.Update(seconds - 1);
        Assert.AreEqual(1, time.Day);
        time.Update(1);
        Assert.AreEqual(2, time.Day);
        Assert.AreEqual(0, time.MinuteOfDay);
        Assert.AreEqual(0, time.DriverRemainder);
    }

    [Test]
    public void SlowSpeed_SaveRoundTrip_PreservesFractionalMinuteAndRate()
    {
        var time = new VillageTime();
        time.SetSpeed(VillageTime.VillageTimeSpeed.Slow);
        time.Update(1);
        Assert.AreEqual(0, time.MinuteOfDay);
        Assert.AreEqual(0.5, time.DriverRemainder);
        var restored = new VillageTime();
        Assert.IsTrue(restored.TryRestore(time.ToSaveData(), true));
        Assert.AreEqual(VillageTime.VillageTimeSpeed.Slow, restored.Speed);
        Assert.AreEqual(0.5, restored.SpeedMultiplier);
        restored.Update(1);
        Assert.AreEqual(1, restored.MinuteOfDay);
        Assert.AreEqual(0, restored.DriverRemainder);
        Assert.AreEqual(0, (int)VillageTime.VillageTimeSpeed.Paused);
        Assert.AreEqual(1, (int)VillageTime.VillageTimeSpeed.Normal);
        Assert.AreEqual(4, (int)VillageTime.VillageTimeSpeed.Fast);
    }

    [TestCase(VillageTime.VillageTimeSpeed.Double, 2d)]
    [TestCase(VillageTime.VillageTimeSpeed.Fast8, 8d)]
    [TestCase(VillageTime.VillageTimeSpeed.Fast12, 12d)]
    [TestCase(VillageTime.VillageTimeSpeed.Fast24, 24d)]
    public void AdditionalSpeed_SaveRoundTrip_PreservesRate(VillageTime.VillageTimeSpeed speed, double rate)
    {
        var time = new VillageTime(); time.SetSpeed(speed); time.Update(.125f);
        var restored = new VillageTime();
        Assert.IsTrue(restored.TryRestore(time.ToSaveData(), true));
        Assert.AreEqual(speed, restored.Speed); Assert.AreEqual(rate, restored.SpeedMultiplier);
        restored.Update(.875f);
        Assert.AreEqual((long)rate, restored.CurrentInstant.Minute);
        Assert.AreEqual(0, restored.DriverRemainder);
    }

    private static VillageTime NewTime()
    {
        return new VillageTime { SecondsPerDay = 5f };
    }

    // ==================== 正常推进 ====================

    [Test]
    public void Update_AccumulatesToDay_BeforeFullDay_NoTick()
    {
        var t = NewTime();
        t.Update(2.5f);
        Assert.AreEqual(1, t.Year);
        Assert.AreEqual(1, t.Month);
        Assert.AreEqual(1, t.Day, "未满一天不应推进日期");
    }

    [Test]
    public void Update_AfterFullDay_AdvancesOneDay()
    {
        var t = NewTime();
        t.Update(5f);
        Assert.AreEqual(2, t.Day);
        Assert.AreEqual(1, t.Month);
    }

    [Test]
    public void Update_MultipleDays_AdvancesMultipleDays()
    {
        var t = NewTime();
        t.Update(15f);
        Assert.AreEqual(4, t.Day, "15 秒 / 5 秒每天 = 3 天 → 1/4");
    }

    // ==================== Pause / Resume ====================

    [Test]
    public void Pause_StopsAdvance()
    {
        var t = NewTime();
        t.Pause();
        t.Update(100f);
        Assert.AreEqual(1, t.Day);
        Assert.AreEqual(VillageTime.VillageTimeSpeed.Paused, t.Speed);
    }

    [Test]
    public void Resume_FromPause_ContinuesAdvance()
    {
        var t = NewTime();
        t.Pause();
        t.Update(100f);
        t.Resume();
        t.Update(5f);
        Assert.AreEqual(2, t.Day, "Resume 后应继续推进");
    }

    [Test]
    public void Resume_WhenNotPaused_KeepsNormal()
    {
        var t = NewTime();
        t.SetSpeed(VillageTime.VillageTimeSpeed.Fast);
        t.Resume();
        Assert.AreEqual(VillageTime.VillageTimeSpeed.Fast, t.Speed, "非暂停状态 Resume 不应改速度");
    }

    // ==================== Speed ====================

    [Test]
    public void FastSpeed_AdvancesFourTimesPerSecond()
    {
        var t = NewTime();
        t.SetSpeed(VillageTime.VillageTimeSpeed.Fast);
        t.Update(1.25f); // 1.25 秒 × 4 = 5 秒 = 1 天
        Assert.AreEqual(2, t.Day, "Fast(4x) 下 1.25 秒应推进 1 天");
    }

    // ==================== Day / Month 边界 ====================

    [Test]
    public void MonthBoundary_RollsOver_FiresMonthTick()
    {
        var t = NewTime();
        var intradaySetup1 = t.ToSaveData();
        intradaySetup1.IntradayTime = null;
        intradaySetup1.Year = 1;
        intradaySetup1.Month = 1;
        intradaySetup1.Day = 31;
        t.Restore(intradaySetup1);


        int monthTicks = 0;
        int lastMonth = -1;
        t.MonthTicked += m => { monthTicks++; lastMonth = m; };

        t.TickDay();

        Assert.AreEqual(2, t.Month, "1/31 后应进入 2 月");
        Assert.AreEqual(1, t.Day);
        Assert.AreEqual(1, monthTicks);
        Assert.AreEqual(2, lastMonth);
    }

    [Test]
    public void YearBoundary_RollsOverYear()
    {
        var t = NewTime();
        var intradaySetup2 = t.ToSaveData();
        intradaySetup2.IntradayTime = null;
        intradaySetup2.Year = 1;
        intradaySetup2.Month = 12;
        intradaySetup2.Day = 31;
        t.Restore(intradaySetup2);


        t.TickDay();

        Assert.AreEqual(2, t.Year);
        Assert.AreEqual(1, t.Month);
        Assert.AreEqual(1, t.Day);
    }

    [Test]
    public void LeapYear_Feb29_ThenMar1()
    {
        // 2024 闰年：2 月 29 天
        Assert.AreEqual(29, VillageTime.DaysInMonth(2024, 2));
        Assert.AreEqual(28, VillageTime.DaysInMonth(2023, 2));
        Assert.AreEqual(29, VillageTime.DaysInMonth(2000, 2), "400 的倍数也是闰年");
        Assert.AreEqual(28, VillageTime.DaysInMonth(1900, 2), "100 的倍数非 400 倍数不是闰年");

        var t = NewTime();
        var intradaySetup3 = t.ToSaveData();
        intradaySetup3.IntradayTime = null;
        intradaySetup3.Year = 2024;
        intradaySetup3.Month = 2;
        intradaySetup3.Day = 28;
        t.Restore(intradaySetup3);

        t.TickDay();
        Assert.AreEqual(29, t.Day, "闰年 2/28 → 2/29");
        t.TickDay();
        Assert.AreEqual(3, t.Month, "闰年 2/29 → 3/1");
        Assert.AreEqual(1, t.Day);
    }

    // ==================== JumpToNextMonth（不漏 Tick） ====================

    [Test]
    public void JumpToNextMonth_8_15To9_1_ExecutesAllTicks()
    {
        var t = NewTime();
        var intradaySetup4 = t.ToSaveData();
        intradaySetup4.IntradayTime = null;
        intradaySetup4.Year = 1;
        intradaySetup4.Month = 8;
        intradaySetup4.Day = 15;
        t.Restore(intradaySetup4);


        int dayTicks = 0;
        int monthTicks = 0;
        t.DayTicked += d => dayTicks++;
        t.MonthTicked += m => monthTicks++;

        t.JumpToNextMonth();

        Assert.AreEqual(9, t.Month, "8/15 → 下个月应为 9 月");
        Assert.AreEqual(1, t.Day, "JumpToNextMonth 应落在下个月第 1 天");
        Assert.AreEqual(17, dayTicks, "必须完整执行 8/16..8/31 的 16 个 DayTick + 进入 9/1 的 1 个 DayTick");
        Assert.AreEqual(1, monthTicks, "进入 9 月应触发 1 次 MonthTick");
    }

    [Test]
    public void JumpToNextMonth_FromFirstOfMonth_SkipsFullCurrentMonth()
    {
        var t = NewTime();
        var intradaySetup5 = t.ToSaveData();
        intradaySetup5.IntradayTime = null;
        intradaySetup5.Year = 1;
        intradaySetup5.Month = 8;
        intradaySetup5.Day = 1;
        t.Restore(intradaySetup5);


        int dayTicks = 0;
        int monthTicks = 0;
        t.DayTicked += d => dayTicks++;
        t.MonthTicked += m => monthTicks++;

        t.JumpToNextMonth();

        Assert.AreEqual(9, t.Month);
        Assert.AreEqual(1, t.Day);
        Assert.AreEqual(31, dayTicks, "8/1 起跳应完整经过 8 月剩余 30 天 + 进入 9/1");
        Assert.AreEqual(1, monthTicks);
    }

    [Test]
    public void JumpToNextMonth_DecemberRollsYear()
    {
        var t = NewTime();
        var intradaySetup6 = t.ToSaveData();
        intradaySetup6.IntradayTime = null;
        intradaySetup6.Year = 1;
        intradaySetup6.Month = 12;
        intradaySetup6.Day = 30;
        t.Restore(intradaySetup6);


        int monthTicks = 0;
        t.MonthTicked += m => monthTicks++;

        t.JumpToNextMonth();

        Assert.AreEqual(2, t.Year, "12 月跳月应跨年");
        Assert.AreEqual(1, t.Month);
        Assert.AreEqual(1, t.Day);
        Assert.AreEqual(1, monthTicks, "12/30→12/31（无 MonthTick），12/31→1/1（1 次 MonthTick）；应总共 1 次 MonthTick");
    }

    // ==================== State 随时间变化闭环（Test / Not Frozen） ====================

    [Test]
    public void DayTick_DrivesVillageState_TestSurvivalValue()
    {
        var t = NewTime();
        var state = new VillageState();
        t.DayTicked += d => state.OnDayTick();

        t.Update(5f); // 1 天
        Assert.AreEqual(1, state.TestSurvivalValue);

        t.Update(10f); // 再 2 天
        Assert.AreEqual(3, state.TestSurvivalValue);

        t.JumpToNextMonth(); // 1/4 → 2/1：需要 28 个 DayTick（1 月 31 天，当前 day=4）
        Assert.AreEqual(31, state.TestSurvivalValue, "JumpToNextMonth 的每个 DayTick 都作用于 State");
    }

    // ==================== Save / Load 快照（DTO round-trip） ====================

    [Test]
    public void ToSaveData_Restore_RoundTrip()
    {
        var t = NewTime();
        var intradaySetup7 = t.ToSaveData();
        intradaySetup7.IntradayTime = null;
        intradaySetup7.Year = 2;
        intradaySetup7.Month = 3;
        intradaySetup7.Day = 10;
        intradaySetup7.Speed = (int)(VillageTime.VillageTimeSpeed.Fast);
        intradaySetup7.SecondsIntoDay = (2.5f) * VillageTime.LegacySecondsPerDay / t.SecondsPerDay;
        t.Restore(intradaySetup7);


        var state = new VillageState { TestSurvivalValue = 42 };

        var data = new VillageSaveData
        {
            Time = t.ToSaveData(),
            State = state.ToSaveData()
        };

        var t2 = new VillageTime();
        var s2 = new VillageState();
        t2.Restore(data.Time);
        s2.Restore(data.State);

        Assert.AreEqual(2, t2.Year);
        Assert.AreEqual(3, t2.Month);
        Assert.AreEqual(10, t2.Day);
        Assert.AreEqual(VillageTime.VillageTimeSpeed.Fast, t2.Speed);
        Assert.AreEqual(t.CurrentInstant, t2.CurrentInstant, "更改默认日长不应改变存档的世界时刻");
        Assert.AreEqual(t.DriverRemainder, t2.DriverRemainder);
        Assert.AreEqual(720f, t2.SecondsIntoDay, "默认24分钟一天，正午对应720现实秒");
        Assert.AreEqual(42, s2.TestSurvivalValue);
    }

    [Test]
    public void Restore_NullOrInvalid_FallsBackToSafeDefaults()
    {
        var t = NewTime();
        t.Restore(null);
        Assert.AreEqual(1, t.Year);
        Assert.AreEqual(1, t.Month);
        Assert.AreEqual(1, t.Day);

        t.Restore(new VillageTimeSaveData { Year = 0, Month = 99, Day = -5, Speed = 12345, SecondsIntoDay = -1f });
        Assert.AreEqual(1, t.Year, "非法年回退默认");
        Assert.AreEqual(1, t.Month, "非法月回退默认");
        Assert.AreEqual(1, t.Day, "非法日回退默认");
        Assert.AreEqual(VillageTime.VillageTimeSpeed.Normal, t.Speed, "非法速度回退 Normal");
        Assert.AreEqual(0f, t.SecondsIntoDay, "非法秒数回退 0");
    }
}

