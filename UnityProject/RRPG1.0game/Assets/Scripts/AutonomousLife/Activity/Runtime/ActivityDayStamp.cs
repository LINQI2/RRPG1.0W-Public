using System;
namespace RRPG.AutonomousActivity
{
    /// <summary>Identity supplied by VillageTime; not a clock or calendar.</summary>
    public readonly struct ActivityDayStamp : IEquatable<ActivityDayStamp>
    {
        public int Year { get; }
        public int Month { get; }
        public int Day { get; }
        public bool IsValid => Year > 0 && Month >= 1 && Month <= 12 && Day >= 1
            && Day <= (Month==2 ? (Year%4==0 && (Year%100!=0 || Year%400==0) ? 29 : 28)
                : Month==4 || Month==6 || Month==9 || Month==11 ? 30 : 31);
        public ActivityDayStamp(int year, int month, int day) { Year = year; Month = month; Day = day; }
        public bool Equals(ActivityDayStamp other) => Year == other.Year && Month == other.Month && Day == other.Day;
        public override bool Equals(object obj) => obj is ActivityDayStamp other && Equals(other);
        public override int GetHashCode() => unchecked((Year * 397 ^ Month) * 397 ^ Day);
        public static bool operator ==(ActivityDayStamp a, ActivityDayStamp b) => a.Equals(b);
        public static bool operator !=(ActivityDayStamp a, ActivityDayStamp b) => !a.Equals(b);
    }
}
