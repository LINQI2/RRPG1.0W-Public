/// <summary>
/// **VillageDayOrdinal（村庄绝对日序）** —— WP-Event-1D §7。
///
/// 为什么需要它：Event World History / RepeatPolicy / Cooldown 需要一个**单调、可比较、可存档**的
/// 「村庄第几天」数值，而 `VillageTime` 暴露的是 Gregorian（Year / Month / Day）三元组。
/// 本类只做一件事：把 Gregorian 三元组换算为从 **`1年1月1日`** 起的绝对日序（该日 = 1）。
///
/// 硬约束：
///   - **不发明日历**：月份天数 / 闰年规则完全委托 `VillageTime.DaysInMonth` / `VillageTime.IsLeapYear`；
///   - **不使用现实世界时间**：绝不读 `System.DateTime.Now` / `UnityEngine.Time`；
///   - 同日期永远同 Ordinal；次日严格 +1；跨月 / 跨年 +1；闰年 2/28 → 2/29 → 3/1 连续；
///   - 非法日期返回 0（不是「第 0 天」的合法语义，而是明确的无效哨兵，调用方必须判 0）。
///
/// 边界：本类不做 Save / Load（Ordinal 只在需要时由当前 VillageTime 重新换算），不持有状态。
/// </summary>
public static class VillageDayOrdinal
{
    /// <summary>`1年1月1日` 的日序。</summary>
    public const int FirstDayOrdinal = 1;

    /// <summary>无效日期的哨兵值（调用方必须显式判断）。</summary>
    public const int Invalid = 0;

    /// <summary>防御性年份上限（避免 int 溢出；正常 Village 时间远小于此值）。</summary>
    public const int MaxSupportedYear = 9999;

    /// <summary>日期是否合法（year ≥ 1、month 1..12、day 1..当月天数）。</summary>
    public static bool IsValidDate(int year, int month, int day)
    {
        if (year < 1 || year > MaxSupportedYear) return false;
        if (month < 1 || month > 12) return false;
        if (day < 1) return false;
        return day <= VillageTime.DaysInMonth(year, month);
    }

    /// <summary>
    /// Gregorian → 绝对日序（`1年1月1日` = 1）。非法日期 → <see cref="Invalid"/>（0）。
    /// </summary>
    public static int ToOrdinal(int year, int month, int day)
    {
        if (!IsValidDate(year, month, day)) return Invalid;

        long total = 0;
        for (int y = 1; y < year; y++)
        {
            total += VillageTime.IsLeapYear(y) ? 366 : 365;
        }
        for (int m = 1; m < month; m++)
        {
            total += VillageTime.DaysInMonth(year, m);
        }
        total += day - 1;
        total += FirstDayOrdinal;

        if (total <= 0 || total > int.MaxValue) return Invalid;
        return (int)total;
    }

    /// <summary>当前 VillageTime 的日序（time 为 null → <see cref="Invalid"/>）。</summary>
    public static int FromVillageTime(VillageTime time)
    {
        if (time == null) return Invalid;
        return ToOrdinal(time.Year, time.Month, time.Day);
    }
}
