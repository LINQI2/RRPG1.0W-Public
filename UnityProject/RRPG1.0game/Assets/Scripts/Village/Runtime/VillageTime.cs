using System;

/// <summary>Only world clock. All drivers delegate to one budgeted advance session.</summary>
public class VillageTime
{
    public const float LegacySecondsPerDay = 5f; // Legacy approximation, Test / Not Frozen.
    public const float DefaultSecondsPerDay = 24f * 60f;
    // Persisted identities: keep existing save codes; Slow's code is not its multiplier.
    public enum VillageTimeSpeed { Paused = 0, Normal = 1, Slow = 2, Double = 3, Fast = 4, Fast8 = 8, Fast12 = 12, Fast24 = 24 }
    public double SpeedMultiplier
    {
        get
        {
            switch (Speed)
            {
                case VillageTimeSpeed.Slow: return 0.5;
                case VillageTimeSpeed.Normal: return 1;
                case VillageTimeSpeed.Double: return 2;
                case VillageTimeSpeed.Fast: return 4;
                case VillageTimeSpeed.Fast8: return 8;
                case VillageTimeSpeed.Fast12: return 12;
                case VillageTimeSpeed.Fast24: return 24;
                default: return 0;
            }
        }
    }
    private float _secondsPerDay = DefaultSecondsPerDay;
    public float SecondsPerDay
    {
        get => _secondsPerDay;
        set
        {
            if (HasActiveAdvance) return;
            if (float.IsNaN(value) || float.IsInfinity(value) || value <= 0) throw new ArgumentOutOfRangeException(nameof(value));
            _secondsPerDay = value;
        }
    }
    public VillageTimeSpeed Speed { get; private set; } = VillageTimeSpeed.Normal;
    public WorldInstant CurrentInstant { get; private set; }
    public double DriverRemainder { get; private set; }
    public long LastCompletedDailyBoundaryMinute { get; private set; }
    public WorldAdvanceSession AdvanceSession { get; private set; }
    public bool HasActiveAdvance => AdvanceSession != null;
    public bool CanSaveOrdinarily => !HasActiveAdvance;
    public IWorldAdvanceSink AdvanceSink { get; set; }
    public int BoundaryBudget { get; set; } = 256;
    public string LastRestoreDiagnostic { get; private set; }
    private bool _executing;
    private long _requestSequence;
    public int Year { get { CurrentInstant.GetDate(out int y, out _, out _); return y; } }
    public int Month { get { CurrentInstant.GetDate(out _, out int m, out _); return m; } }
    public int Day { get { CurrentInstant.GetDate(out _, out _, out int d); return d; } }
    public int MinuteOfDay => CurrentInstant.MinuteOfDay;
    public float SecondsIntoDay => (float)((MinuteOfDay + DriverRemainder) * SecondsPerDay / 1440.0);
    public event Action<int> DayTicked;
    public event Action<int> MonthTicked;

    public void Update(float deltaSeconds)
    {
        if (HasActiveAdvance || Speed == VillageTimeSpeed.Paused || float.IsNaN(deltaSeconds)
            || float.IsInfinity(deltaSeconds) || deltaSeconds <= 0 || BoundaryBudget < 1) return;
        double minutes = deltaSeconds * SpeedMultiplier * 1440 / SecondsPerDay + DriverRemainder;
        if (double.IsInfinity(minutes) || minutes > WorldInstant.MaximumMinute - CurrentInstant.Minute) return;
        long whole = (long)Math.Floor(minutes);
        double remainder = minutes - whole;
        if (whole == 0) { DriverRemainder = remainder; return; }
        DriverRemainder = remainder;
        AdvanceTo(CurrentInstant.AddMinutes(whole), WorldAdvanceOrigin.Realtime);
    }
    public void TickDay()
    {
        if (HasActiveAdvance || CurrentInstant.Minute > WorldInstant.MaximumMinute - 1440) return;
        AdvanceTo(CurrentInstant.AddMinutes(1440));
    }
    public void JumpToNextMonth()
    {
        if (HasActiveAdvance) return;
        int year = Year, month = Month + 1;
        if (month == 13) { if (year == int.MaxValue) return; year++; month = 1; }
        AdvanceTo(WorldInstant.FromDate(year, month, 1), WorldAdvanceOrigin.Jump);
    }
    public void Pause() => SetSpeed(VillageTimeSpeed.Paused);
    public void CycleSpeed()
    {
        switch (Speed)
        {
            case VillageTimeSpeed.Slow: SetSpeed(VillageTimeSpeed.Normal); break;
            case VillageTimeSpeed.Normal: SetSpeed(VillageTimeSpeed.Double); break;
            case VillageTimeSpeed.Double: SetSpeed(VillageTimeSpeed.Fast); break;
            case VillageTimeSpeed.Fast: SetSpeed(VillageTimeSpeed.Fast8); break;
            case VillageTimeSpeed.Fast8: SetSpeed(VillageTimeSpeed.Fast12); break;
            case VillageTimeSpeed.Fast12: SetSpeed(VillageTimeSpeed.Fast24); break;
            case VillageTimeSpeed.Fast24: SetSpeed(VillageTimeSpeed.Slow); break;
            default: SetSpeed(VillageTimeSpeed.Normal); break;
        }
    }
    public void Resume() { if (Speed == VillageTimeSpeed.Paused) SetSpeed(VillageTimeSpeed.Normal); }
    public void SetSpeed(VillageTimeSpeed speed)
    {
        // Interaction display pause is orthogonal to the AdvanceSession lock. Even Resume/Fast
        // cannot advance a Waiting session; its original request still requires acknowledgement.
        if ((!HasActiveAdvance || AdvanceSession.State == WorldAdvanceState.WaitingForInteraction)
            && Enum.IsDefined(typeof(VillageTimeSpeed), speed)) Speed = speed;
    }

    public WorldAdvanceResult AdvanceTo(WorldInstant target, WorldAdvanceOrigin origin = WorldAdvanceOrigin.Direct,
        string requestId = null, int? budget = null)
    {
        if (_executing) return Result(WorldAdvanceOutcome.Reentrant, target, "Reentrant");
        if (AdvanceSession != null)
        {
            if (!string.Equals(requestId, AdvanceSession.RequestId, StringComparison.Ordinal))
                return Result(WorldAdvanceOutcome.Busy, target, "ActiveRequest");
            if (!target.Equals(AdvanceSession.OriginalTarget) || origin != AdvanceSession.Origin)
                return Result(WorldAdvanceOutcome.RequestConflict, target, "RequestConflict");
            return ResumeAdvance(requestId, budget);
        }
        if (target.Minute < CurrentInstant.Minute) return Result(WorldAdvanceOutcome.InvalidTarget, target, "CannotRewind");
        if ((budget ?? BoundaryBudget) < 1) return Result(WorldAdvanceOutcome.InvalidTarget, target, "InvalidBudget");
        if (string.IsNullOrEmpty(requestId)) requestId = "advance:" + checked(++_requestSequence);
        AdvanceSession = new WorldAdvanceSession(requestId, origin, CurrentInstant, target);
        return ResumeAdvance(requestId, budget);
    }
    public WorldAdvanceResult ResumeAdvance(string requestId, int? budget = null)
    {
        var session = AdvanceSession;
        if (session == null || !string.Equals(requestId, session.RequestId, StringComparison.Ordinal))
            return Result(WorldAdvanceOutcome.RequestConflict, CurrentInstant, "UnknownRequest");
        if (_executing) return Result(WorldAdvanceOutcome.Reentrant, session.OriginalTarget, "Reentrant");
        if (session.State == WorldAdvanceState.Failed)
            return Result(WorldAdvanceOutcome.PartialBoundaryFailure, session.OriginalTarget, session.Diagnostic);
        if (session.State == WorldAdvanceState.WaitingForInteraction)
            return Result(WorldAdvanceOutcome.WaitingForInteraction, session.OriginalTarget, session.Diagnostic);
        if (session.State == WorldAdvanceState.AwaitingFinalSave || session.State == WorldAdvanceState.MotionCompleted)
            return Result(WorldAdvanceOutcome.Completed, session.OriginalTarget);
        int remaining = budget ?? BoundaryBudget;
        if (remaining < 1) return Result(WorldAdvanceOutcome.InvalidTarget, session.OriginalTarget, "InvalidBudget");
        _executing = true;
        try
        {
            session.State = WorldAdvanceState.Running; session.Diagnostic = null;
            while (true)
            {
                if (CheckInteraction(session)) return Result(WorldAdvanceOutcome.WaitingForInteraction, session.OriginalTarget, session.Diagnostic);
                if (CurrentInstant.Equals(session.OriginalTarget))
                {
                    session.State = WorldAdvanceState.MotionCompleted;
                    var completed = Result(WorldAdvanceOutcome.Completed, session.OriginalTarget);
                    if (session.Origin != WorldAdvanceOrigin.Return) AdvanceSession = null;
                    return completed;
                }
                if (remaining-- == 0)
                {
                    session.State = WorldAdvanceState.Yielded;
                    return Result(WorldAdvanceOutcome.Yielded, session.OriginalTarget);
                }
                long nextMidnight = checked((CurrentInstant.Minute / 1440 + 1) * 1440);
                long next = Math.Min(nextMidnight, session.OriginalTarget.Minute);
                try
                {
                    WorldInstant? hostBoundary = AdvanceSink?.NextBoundary(CurrentInstant, session.OriginalTarget);
                    if (hostBoundary.HasValue)
                    {
                        if (hostBoundary.Value.Minute <= CurrentInstant.Minute || hostBoundary.Value.Minute > session.OriginalTarget.Minute)
                            throw new InvalidOperationException("InvalidHostBoundary");
                        next = Math.Min(next, hostBoundary.Value.Minute);
                    }
                    string diagnostic = AdvanceSink?.PreflightBoundary(new WorldInstant(next));
                    if (!string.IsNullOrEmpty(diagnostic)) throw new InvalidOperationException(diagnostic);
                }
                catch (Exception ex)
                {
                    session.State = WorldAdvanceState.Yielded; session.Diagnostic = ex.Message;
                    return Result(WorldAdvanceOutcome.RetryableBoundaryFailure, session.OriginalTarget, ex.Message);
                }
                WorldInstant previous = CurrentInstant;
                try
                {
                    CurrentInstant = new WorldInstant(next);
                    AdvanceSink?.CompleteInterval(previous, CurrentInstant);
                    if (next % 1440 == 0)
                    {
                        if (Month != GetMonth(previous)) MonthTicked?.Invoke(Month);
                        DayTicked?.Invoke(Day);
                    }
                    AdvanceSink?.CompleteBoundary(CurrentInstant);
                    session.LastCompletedInstant = CurrentInstant;
                    if (next % 1440 == 0) LastCompletedDailyBoundaryMinute = next;
                }
                catch (Exception ex)
                {
                    session.State = WorldAdvanceState.Failed; session.Diagnostic = ex.Message;
                    return Result(WorldAdvanceOutcome.PartialBoundaryFailure, session.OriginalTarget, ex.Message);
                }
            }
        }
        finally { _executing = false; }
    }
    private static int GetMonth(WorldInstant instant) { instant.GetDate(out _, out int m, out _); return m; }
    private bool CheckInteraction(WorldAdvanceSession session)
    {
        try
        {
            if (AdvanceSink == null || !AdvanceSink.TryGetBlockingInteraction(out string id, out string diagnostic)) return false;
            session.BlockingEventInstanceId = id; session.Diagnostic = diagnostic;
        }
        catch (Exception ex) { session.BlockingEventInstanceId = null; session.Diagnostic = "InteractionDiagnostic:" + ex.Message; }
        session.State = WorldAdvanceState.WaitingForInteraction;
        return true;
    }
    public bool AcknowledgeInteraction(string requestId, string eventInstanceId)
    {
        var s = AdvanceSession;
        if (s == null || s.State != WorldAdvanceState.WaitingForInteraction || string.IsNullOrEmpty(eventInstanceId)
            || s.RequestId != requestId || s.BlockingEventInstanceId != eventInstanceId) return false;
        s.BlockingEventInstanceId = null; s.Diagnostic = null; s.State = WorldAdvanceState.Yielded;
        return true;
    }
    public bool HoldForFinalSave(string requestId)
    {
        var s = AdvanceSession;
        if (s == null || s.RequestId != requestId || s.Origin != WorldAdvanceOrigin.Return
            || s.State != WorldAdvanceState.MotionCompleted) return false;
        s.State = WorldAdvanceState.AwaitingFinalSave; return true;
    }
    public bool CompleteFinalSave(string requestId)
    {
        var s = AdvanceSession;
        if (s == null || s.RequestId != requestId || s.State != WorldAdvanceState.AwaitingFinalSave) return false;
        AdvanceSession = null; return true;
    }
    private WorldAdvanceResult Result(WorldAdvanceOutcome outcome, WorldInstant target, string diagnostic = null)
        => new WorldAdvanceResult(outcome, AdvanceSession?.LastCompletedInstant ?? CurrentInstant, target, diagnostic);
    public static int DaysInMonth(int year, int month)
    {
        if (month < 1 || month > 12) return 30;
        if (month == 2) return IsLeapYear(year) ? 29 : 28;
        return month == 4 || month == 6 || month == 9 || month == 11 ? 30 : 31;
    }
    public static bool IsLeapYear(int year) => year % 4 == 0 && (year % 100 != 0 || year % 400 == 0);
    public string GetDateText() => Year + "年" + Month + "月" + Day + "日 " + MinuteOfDay / 60 + ":" + (MinuteOfDay % 60).ToString("00");
    public VillageTimeSaveData ToSaveData() => new VillageTimeSaveData
    {
        Year = Year, Month = Month, Day = Day, Speed = (int)Speed, SecondsIntoDay = SecondsIntoDay,
        IntradayTime = new VillageIntradayTimeSaveData { SchemaVersion = 1, CurrentWorldMinute = CurrentInstant.Minute,
            DriverRemainder = DriverRemainder, LastCompletedDailyBoundaryMinute = LastCompletedDailyBoundaryMinute }
    };
    public static bool TryPrepareRestore(VillageTimeSaveData data, bool hasIntradayBlock,
        out WorldInstant instant, out double remainder, out long marker, out string diagnostic)
    {
        instant = default; remainder = 0; marker = 0; diagnostic = null;
        if (data == null) return true;
        try
        {
            if (hasIntradayBlock)
            {
                var v = data.IntradayTime;
                if (v == null || v.SchemaVersion != 1 || double.IsNaN(v.DriverRemainder) || double.IsInfinity(v.DriverRemainder)
                    || v.DriverRemainder < 0 || v.DriverRemainder >= 1) throw new ArgumentException("InvalidIntradayTime");
                instant = new WorldInstant(v.CurrentWorldMinute); remainder = v.DriverRemainder;
                marker = v.LastCompletedDailyBoundaryMinute;
                if (marker < 0 || marker % 1440 != 0 || marker != instant.Minute / 1440 * 1440)
                    throw new ArgumentException("InvalidDailyBoundaryMarker");
                if (data.Year != GetYear(instant) || data.Month != GetMonth(instant) || data.Day != GetDay(instant))
                    throw new ArgumentException("InvalidLegacyDateProjection");
            }
            else
            {
                if (float.IsNaN(data.SecondsIntoDay) || float.IsInfinity(data.SecondsIntoDay)
                    || data.SecondsIntoDay < 0 || data.SecondsIntoDay >= LegacySecondsPerDay) throw new ArgumentException("InvalidLegacyTime");
                double minute = data.SecondsIntoDay * 1440.0 / LegacySecondsPerDay;
                instant = WorldInstant.FromDate(data.Year, data.Month, data.Day).AddMinutes((long)Math.Floor(minute));
                remainder = minute - Math.Floor(minute); marker = instant.Minute / 1440 * 1440;
                diagnostic = "LegacyTimeApproximation:SecondsPerDay=5(Test/NotFrozen)";
            }
            return true;
        }
        catch (Exception ex) { diagnostic = ex.Message; return false; }
    }
    private static int GetYear(WorldInstant instant) { instant.GetDate(out int y, out _, out _); return y; }
    private static int GetDay(WorldInstant instant) { instant.GetDate(out _, out _, out int d); return d; }
    public bool TryRestore(VillageTimeSaveData data, bool hasIntradayBlock)
    {
        if (HasActiveAdvance) return false;
        if (!TryPrepareRestore(data, hasIntradayBlock, out var instant, out var remainder, out var marker, out var diagnostic)) return false;
        CurrentInstant = instant; DriverRemainder = remainder; LastCompletedDailyBoundaryMinute = marker;
        Speed = data != null && Enum.IsDefined(typeof(VillageTimeSpeed), data.Speed) ? (VillageTimeSpeed)data.Speed : VillageTimeSpeed.Normal;
        LastRestoreDiagnostic = diagnostic; return true;
    }
    public void Restore(VillageTimeSaveData data) { TryRestore(data, data?.IntradayTime != null); }
    public void ResetForNewGame()
    {
        CurrentInstant = default; DriverRemainder = 0; LastCompletedDailyBoundaryMinute = 0;
        AdvanceSession = null; _executing = false; Speed = VillageTimeSpeed.Normal; LastRestoreDiagnostic = null;
    }
}
