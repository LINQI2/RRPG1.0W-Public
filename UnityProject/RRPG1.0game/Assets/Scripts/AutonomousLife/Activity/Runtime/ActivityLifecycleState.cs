namespace RRPG.AutonomousActivity
{
    public enum ActivityLifecycleState { Committed, Executing, Completed, Failed, Interrupted }
    public enum ActivityResult
    {
        Success, Duplicate, WindowConflict, InvalidWindow, MissingWindow, InvalidCandidate,
        InvalidInstanceId, InstanceConflict, ActorIneligible, ParticipantUnavailable,
        ClaimConflict, AlreadyParticipated, MissingInstance, IllegalTransition
    }
}
