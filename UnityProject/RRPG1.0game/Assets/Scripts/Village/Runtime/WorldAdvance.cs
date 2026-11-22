using System;

/// <summary>Gregorian world minutes. A value, never an independent clock.</summary>
public readonly struct WorldInstant : IEquatable<WorldInstant>, IComparable<WorldInstant>
{
    public const long MinutesPerDay = 1440;
    public static readonly long MaximumMinute = checked(DaysBeforeYear(int.MaxValue) * MinutesPerDay +
        (VillageTime.IsLeapYear(int.MaxValue) ? 366L : 365L) * MinutesPerDay - 1);
    public long Minute { get; }
    public WorldInstant(long minute)
    {
        if (minute < 0 || minute > MaximumMinute) throw new ArgumentOutOfRangeException(nameof(minute));
        Minute = minute;
    }
    private static long DaysBeforeYear(int year)
    {
        long y = (long)year - 1;
        return checked(y * 365 + y / 4 - y / 100 + y / 400);
    }
    public static WorldInstant FromDate(int year, int month, int day, int minuteOfDay = 0)
    {
        if (year < 1 || month < 1 || month > 12 || day < 1 || day > VillageTime.DaysInMonth(year, month)
            || minuteOfDay < 0 || minuteOfDay >= MinutesPerDay) throw new ArgumentOutOfRangeException(nameof(day));
        long days = DaysBeforeYear(year) + day - 1;
        for (int m = 1; m < month; m++) days += VillageTime.DaysInMonth(year, m);
        return new WorldInstant(checked(days * MinutesPerDay + minuteOfDay));
    }
    public void GetDate(out int year, out int month, out int day)
    {
        long days = Minute / MinutesPerDay;
        long low = 1, high = int.MaxValue;
        while (low < high)
        {
            long mid = low + (high - low + 1) / 2;
            if (DaysBeforeYear((int)mid) <= days) low = mid; else high = mid - 1;
        }
        year = (int)low;
        int remaining = (int)(days - DaysBeforeYear(year));
        month = 1;
        while (remaining >= VillageTime.DaysInMonth(year, month)) remaining -= VillageTime.DaysInMonth(year, month++);
        day = remaining + 1;
    }
    public int MinuteOfDay => (int)(Minute % MinutesPerDay);
    public WorldInstant AddMinutes(long minutes) => new WorldInstant(checked(Minute + minutes));
    public bool Equals(WorldInstant other) => Minute == other.Minute;
    public override bool Equals(object obj) => obj is WorldInstant other && Equals(other);
    public override int GetHashCode() => Minute.GetHashCode();
    public int CompareTo(WorldInstant other) => Minute.CompareTo(other.Minute);
    public override string ToString()
    {
        GetDate(out int y, out int m, out int d);
        return y + "-" + m + "-" + d + " " + MinuteOfDay / 60 + ":" + (MinuteOfDay % 60).ToString("00");
    }
}

public enum WorldAdvanceOrigin { Direct, Realtime, Jump, Return }
public enum WorldAdvanceState { Running, Yielded, WaitingForInteraction, Failed, MotionCompleted, AwaitingFinalSave }
public enum WorldAdvanceOutcome
{
    Completed, Yielded, WaitingForInteraction, Busy, InvalidTarget, RequestConflict, Reentrant,
    RetryableBoundaryFailure, PartialBoundaryFailure
}

/// <summary>Read-only outside the clock. Return/UI never own a second cursor or target.</summary>
public sealed class WorldAdvanceSession
{
    public string RequestId { get; }
    public WorldAdvanceOrigin Origin { get; }
    public WorldInstant OriginalTarget { get; }
    public WorldInstant LastCompletedInstant { get; internal set; }
    public WorldAdvanceState State { get; internal set; }
    public string BlockingEventInstanceId { get; internal set; }
    public string Diagnostic { get; internal set; }
    internal WorldAdvanceSession(string id, WorldAdvanceOrigin origin, WorldInstant start, WorldInstant target)
    {
        RequestId = id; Origin = origin; OriginalTarget = target; LastCompletedInstant = start;
        State = WorldAdvanceState.Running;
    }
}

public sealed class WorldAdvanceResult
{
    public WorldAdvanceOutcome Outcome { get; }
    public WorldInstant LastCompletedInstant { get; }
    public WorldInstant TargetInstant { get; }
    public string Diagnostic { get; }
    public WorldAdvanceResult(WorldAdvanceOutcome outcome, WorldInstant cursor, WorldInstant target, string diagnostic = null)
    { Outcome = outcome; LastCompletedInstant = cursor; TargetInstant = target; Diagnostic = diagnostic; }
}

/// <summary>Small host boundary seam; no scheduler, provider scan or content language.</summary>
public interface IWorldAdvanceSink
{
    WorldInstant? NextBoundary(WorldInstant from, WorldInstant target);
    string PreflightBoundary(WorldInstant boundary);
    void CompleteInterval(WorldInstant from, WorldInstant to);
    void CompleteBoundary(WorldInstant boundary);
    bool TryGetBlockingInteraction(out string eventInstanceId, out string diagnostic);
}
