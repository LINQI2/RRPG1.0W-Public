using RRPG.AutonomousActivity;
/// <summary>World adapter; no selection, clock, social or presentation.</summary>
public static class ActivityRuntimeBridge
{
    /// <summary>Read-only post-completion observer bridge; subscriber failures cannot change domain results.</summary>
    public static event System.Action<ActivityCompletionSnapshot> CompletedOccurrence;
    private static ActivityResult ObserveCompletion(ActivityResult result,ActivityCompletionSnapshot snapshot)
    {
        if (result==ActivityResult.Success && snapshot!=null && CompletedOccurrence!=null)
            foreach (System.Action<ActivityCompletionSnapshot> observer in CompletedOccurrence.GetInvocationList())
                try { observer(snapshot); } catch (System.Exception error) { UnityEngine.Debug.LogWarning("[Activity observer] "+error.Message); }
        return result;
    }
    private static ActivityWorldState current;
    private static ActivityLifecycleService service;
    public static void EnsureInitialized()
    { if (current == null) { current = new ActivityWorldState(); service = new ActivityLifecycleService(current); } }
    public static ActivityDailyWindow QueryCurrentWindow() => current?.QueryCurrentWindow();
    public static PersonActivityState QueryPersonActivityState(string id)
        => current != null ? current.QueryPersonActivityState(id) : new ActivityWorldState().QueryPersonActivityState(id);
    public static ActivityInstance QueryActivityInstance(string id) => current?.QueryActivityInstance(id);
    public static string QueryClaim(string id) => current?.QueryClaim(id);
    public static ActivityResult OpenDailyWindow(ActivityDailyWindow window) { EnsureInitialized(); return service.OpenDailyWindow(window); }
    public static ActivityResult OpenOpportunity(ActivityDailyWindow window) { EnsureInitialized(); return service.OpenOpportunity(window); }
    public static System.Collections.Generic.IReadOnlyList<ActivityInstance> QueryActiveInstances()
    { EnsureInitialized(); return current.QueryActiveInstances(); }
    public static System.Collections.Generic.IReadOnlyList<PersonActivityState> QueryPersonStates()
    { EnsureInitialized(); return current.QueryPersonStates(); }
    public static ActivityResult TryCommitScheduled(string id, ActivityCandidate candidate,long now,long arrival,long completion)
    { EnsureInitialized(); return service.TryCommitScheduled(id,candidate,now,arrival,completion); }
    public static ActivityResult TryCommitScheduledAtPlace(string id,ActivityCandidate candidate,long now,long arrival,long completion,string place)
    { EnsureInitialized();return service.TryCommitScheduledAtPlace(id,candidate,now,arrival,completion,place); }
    public static bool RecordActualPresence(string personId,string place,long minute)
    { EnsureInitialized();return service.RecordActualPresence(personId,place,minute); }
    public static bool RecordMissedVisit(string personId,string type,string target,long minute)
    { EnsureInitialized();return service.RecordMissedVisit(personId,type,target,minute); }
    public static ActivityResult TryCompleteMemberResult(string id,long minute,ActivityDayStamp day,int minimum,out ActivityCompletionSnapshot completion)
    { EnsureInitialized();return ObserveCompletion(service.TryCompleteMemberResult(id,minute,day,minimum,out completion),completion); }
    public static ActivityResult TryBeginExecutionAt(string id,long minute) { EnsureInitialized(); return service.TryBeginExecutionAt(id,minute); }
    public static ActivityResult TryCompleteAt(string id,long minute,ActivityDayStamp day,out ActivityCompletionSnapshot snapshot)
    { EnsureInitialized(); return ObserveCompletion(service.TryCompleteAt(id,minute,day,out snapshot),snapshot); }
    public static ActivityResult TryCommitActivity(string id, ActivityCandidate candidate) { EnsureInitialized(); return service.TryCommitActivity(id, candidate); }
    public static ActivityResult TryBeginExecution(string id) { EnsureInitialized(); return service.TryBeginExecution(id); }
    public static ActivityResult TryComplete(string id, out ActivityCompletionSnapshot snapshot) { EnsureInitialized(); return ObserveCompletion(service.TryComplete(id, out snapshot),snapshot); }
    public static ActivityResult TryFail(string id) { EnsureInitialized(); return service.TryFail(id); }
    public static ActivityResult TryInterrupt(string id) { EnsureInitialized(); return service.TryInterrupt(id); }
    public static ActivityWorldSaveData BuildSaveData() => ActivityWorldPersistence.Build(current);
    public static int Restore(ActivityWorldSaveData data) { EnsureInitialized(); return ActivityWorldPersistence.Restore(current, data); }
    public static void ResetForNewGame() => Restore(null);
    public static void ResetForTests() => Restore(null);
}
