using System;
using UnityEngine;

public static partial class VillageRuntimeBridge
{
    private static WorldInstant? _expeditionDeparture;
    private static long _returnSequence;
    private static Func<long> _elapsedMinutesProvider;
    private static bool _executingBlockingOption;
    private static string _successfullyHandledInteraction;
    private static bool _suppressTimeRestore;
    internal static void RestoreVillageWithoutTime(VillageSaveData save)
    {
        _suppressTimeRestore = true;
        _stagingBuildingLoad = true;
        try { EnsureInitialized(save); }
        finally { _suppressTimeRestore = false; _stagingBuildingLoad=false; }
    }
    internal static void AbortAdvanceAfterSuccessfulWorldRestore()
    {
        Time.ResetForNewGame();
        _returnRecord = null; _pendingReturnMode = null; _expeditionPersonIds.Clear();
        ResetIntradayHost();
    }
    public static void ResetTimeForNewGame()
    {
        EnsureInitialized(null);
        Time.ResetForNewGame(); IntradayWork.Restore(null); _returnRecord = null; _pendingReturnMode = null; ResetIntradayHost();
    }
    public static string WorldAdvanceDiagnostic { get; private set; }
    public static bool IsWorldWriteLocked => (Time != null && Time.HasActiveAdvance)
        || (_returnRecord != null && !_returnRecord.Completed);
    public static bool CanSaveWorld => !IsWorldWriteLocked && _expeditionPersonIds.Count == 0;
    internal static bool CanSaveReturnFinal => _returnRecord != null
        && _returnRecord.Phase == ExpeditionReturnPhase.OccupancyCleared && !_returnRecord.Completed
        && Time?.AdvanceSession != null && Time.AdvanceSession.RequestId == _returnRecord.SessionIdentity
        && Time.AdvanceSession.State == WorldAdvanceState.AwaitingFinalSave;
    public static bool GuardWorldWrite()
    {
        if (!IsWorldWriteLocked || _executingBlockingOption) return true;
        WorldAdvanceDiagnostic = "WorldBusy:" + (Time?.AdvanceSession?.State.ToString() ?? "ReturnSettlement");
        return false;
    }
    public static void SetExpeditionElapsedMinutesProvider(Func<long> provider) => _elapsedMinutesProvider = provider;
    public static long ResolveExpeditionElapsedMinutes()
    {
        try { return _elapsedMinutesProvider != null ? _elapsedMinutesProvider() : checked((long)ResolveExpeditionElapsedDays() * 1440); }
        catch (Exception ex) { WorldAdvanceDiagnostic = "InvalidElapsedMinutes:" + ex.Message; return -1; }
    }
    private static int CompletedReturnDays(long departure, long elapsed, string identity)
    {
        long cursor = departure;
        if (Time?.AdvanceSession != null && Time.AdvanceSession.RequestId == identity)
            cursor = Time.AdvanceSession.LastCompletedInstant.Minute;
        else if (_returnRecord != null && _returnRecord.SessionIdentity == identity
            && (_returnRecord.Completed || _returnRecord.Phase == ExpeditionReturnPhase.CatchUpComplete
                || _returnRecord.Phase == ExpeditionReturnPhase.OccupancyCleared)) cursor = checked(departure + elapsed);
        return checked((int)(cursor / 1440 - departure / 1440));
    }
    private static ExpeditionCatchUpResult RunIntradayReturnCatchUp()
    {
        if (_isCatchUpRunning) return ExpeditionCatchUpResult.RejectedReentrant;
        var record = _returnRecord;
        if (record == null || record.Completed) return ExpeditionCatchUpResult.AlreadyCompleted;
        if (record.Phase == ExpeditionReturnPhase.Failed) return ExpeditionCatchUpResult.RejectedFailedRecord;
        if (record.Phase == ExpeditionReturnPhase.CatchUpComplete || record.Phase == ExpeditionReturnPhase.OccupancyCleared)
            return ExpeditionCatchUpResult.AlreadyCaughtUp;
        if (record.Phase != ExpeditionReturnPhase.ReturnSettled) return ExpeditionCatchUpResult.InvalidPhase;
        if (HasProjectExpeditionOverlap()) return ExpeditionCatchUpResult.OverlapRejected;
        WorldInstant target;
        try { target = new WorldInstant(record.DepartureWorldMinute).AddMinutes(record.SettledElapsedMinutes); }
        catch (Exception ex) { WorldAdvanceDiagnostic = "InvalidReturnTimeline:" + ex.Message; return ExpeditionCatchUpResult.InvalidTimeline; }
        _isCatchUpRunning = true;
        try
        {
            var result = Time.AdvanceTo(target, WorldAdvanceOrigin.Return, record.SessionIdentity);
            WorldAdvanceDiagnostic = result.Diagnostic;
            if (result.Outcome == WorldAdvanceOutcome.Completed)
            { record.Phase = ExpeditionReturnPhase.CatchUpComplete; return record.SettledElapsedMinutes == 0 ? ExpeditionCatchUpResult.AlreadyCaughtUp : ExpeditionCatchUpResult.Completed; }
            if (result.Outcome == WorldAdvanceOutcome.PartialBoundaryFailure)
            { record.Phase = ExpeditionReturnPhase.Failed; Debug.LogError("Return Catch-up 在 Day 中途失败: " + result.Diagnostic); return ExpeditionCatchUpResult.PartialDayFailure; }
            if (result.Outcome == WorldAdvanceOutcome.WaitingForInteraction)
            {
                PresentAdvanceInteraction();
                return ExpeditionCatchUpResult.WaitingForInteraction;
            }
            if (result.Outcome == WorldAdvanceOutcome.Yielded) return ExpeditionCatchUpResult.Yielded;
            if (result.Outcome == WorldAdvanceOutcome.RetryableBoundaryFailure) return result.Diagnostic == "ProjectExpeditionOverlap" ? ExpeditionCatchUpResult.OverlapRejected : ExpeditionCatchUpResult.RetryableFailure;
            return ExpeditionCatchUpResult.InvalidTimeline;
        }
        finally { _isCatchUpRunning = false; }
    }
    public static bool TryBeginBlockingEventOption(string eventId)
    {
        var s = Time?.AdvanceSession;
        if (!IsWorldWriteLocked) return true;
        if (s == null || s.State != WorldAdvanceState.WaitingForInteraction || s.BlockingEventInstanceId != eventId) return false;
        _executingBlockingOption = true; return true;
    }
    public static void EndBlockingEventOption(string eventId, bool success)
    {
        if (success && Time?.AdvanceSession?.BlockingEventInstanceId == eventId) _successfullyHandledInteraction = eventId;
        _executingBlockingOption = false;
    }
    public static bool CloseHandledAdvanceInteraction(string eventId)
    {
        var s = Time?.AdvanceSession;
        if (s == null || _successfullyHandledInteraction != eventId) return false;
        bool ok = Time.AcknowledgeInteraction(s.RequestId, eventId);
        if (ok) _successfullyHandledInteraction = null;
        return ok;
    }
    public static void PresentAdvanceInteraction()
    {
        var s = Time?.AdvanceSession;
        if (s == null || s.State != WorldAdvanceState.WaitingForInteraction) return;
        var view = UnityEngine.Object.FindObjectOfType<VillageSocialEventUI>(true);
        if (view == null) { WorldAdvanceDiagnostic = "MissingVillageSocialEventUI"; return; }
        view.ShowAdvancePending(s.BlockingEventInstanceId);
    }
    /// <summary>Existing driver resumes one accepted request, never submits waiting wall-time delta.</summary>
    public static void PumpWorldAdvance()
    {
        var s = Time?.AdvanceSession;
        if (s == null) return;
        if (s.State == WorldAdvanceState.WaitingForInteraction) { PresentAdvanceInteraction(); return; }
        if (s.State != WorldAdvanceState.Yielded || !string.IsNullOrEmpty(s.Diagnostic)) return;
        if (s.Origin == WorldAdvanceOrigin.Return)
        {
            var expeditionUI = UnityEngine.Object.FindObjectOfType<ExpeditionUI>(true);
            if (expeditionUI != null && TryGetPendingReturnMode(out var mode)) expeditionUI.FinalizeReturn(mode);
            return;
        }
        var result = Time.ResumeAdvance(s.RequestId);
        WorldAdvanceDiagnostic = result.Diagnostic;
        if (result.Outcome == WorldAdvanceOutcome.WaitingForInteraction) PresentAdvanceInteraction();
    }
    private static void ResetIntradayHost()
    {
        _expeditionDeparture = null; _elapsedMinutesProvider = null; _returnSequence = 0;
        _executingBlockingOption = false; _successfullyHandledInteraction = null; WorldAdvanceDiagnostic = null;
        ReceptionPolicy=new IntradayReceptionPolicy(); ActivitySchedule=new IntradayActivitySchedule(); IntradayActivityDiagnostic=null;
    }
    private sealed class DailyAdvanceSink : IWorldAdvanceSink
    {
        public WorldInstant? NextBoundary(WorldInstant from, WorldInstant target)
        {
            var work=NextWorkBoundary(from,target);var activity=NextActivityBoundary(from,target);
            return !work.HasValue ? activity : !activity.HasValue ? work
                : work.Value.Minute<=activity.Value.Minute ? work : activity;
        }
        public string PreflightBoundary(WorldInstant boundary)
        {
            if(SimulationPersonSource==null && ActivityRuntimeBridge.QueryActiveInstances().Count>0)
            {
                LastActivityDailyPhaseResult=ActivityDailyPhaseResult.MissingPersonSource;
                IntradayActivityDiagnostic="MissingActivityPersonSource";
                AutonomousSelectionRuntimeBridge.ReportOpportunityFailure(RRPG.AutonomousSelection.DecisionDailyResult.MissingPersonSource,IntradayActivityDiagnostic);
                return IntradayActivityDiagnostic;
            }
            if (Time?.AdvanceSession?.Origin != WorldAdvanceOrigin.Return) return null;
            if (HasProjectExpeditionOverlap()) return "ProjectExpeditionOverlap";
            if (boundary.Minute % 1440 == 0) BeforeCatchUpDayTickTestHook?.Invoke(_returnRecord.DaysSimulated);
            if (HasProjectExpeditionOverlap()) return "ProjectExpeditionOverlap";
            return null;
        }
        public void CompleteInterval(WorldInstant from, WorldInstant to) => CompleteWorkInterval(from,to);
        public void CompleteBoundary(WorldInstant boundary)
        { CompleteActivityBoundary(boundary);CompleteWorkBoundary(boundary);OpenIntradayOpportunity(boundary); }
        public bool TryGetBlockingInteraction(out string eventInstanceId, out string diagnostic)
            => VillageEventFlow.TryGetAdvanceBlockingInteraction(out eventInstanceId, out diagnostic);
    }
}
