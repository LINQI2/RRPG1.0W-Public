using System;
using System.Collections.Generic;
using RRPG.AutonomousActivity;

namespace RRPG.AutonomousSelection
{
    [Serializable]
    public sealed class AutonomousDecisionWorldSaveData
    {
        public bool HasLastResolvedDay;
        public int LastResolvedYear;
        public int LastResolvedMonth;
        public int LastResolvedDay;
        public bool HasLastOpportunity;
        public long LastOpportunityMinute;
        public string LastOpportunityId;
    }

    public sealed class AutonomousDecisionWorldState
    {
        public ActivityDayStamp? LastResolvedDay { get; internal set; }
        public long? LastOpportunityMinute { get; internal set; }
        public string LastOpportunityId { get; internal set; }

        public bool AlreadyResolved(ActivityDayStamp day)
        {
            if (!LastResolvedDay.HasValue)
                return false;
            var last = LastResolvedDay.Value;
            return day.Year < last.Year || day.Year == last.Year && (day.Month < last.Month || day.Month == last.Month && day.Day <= last.Day);
        }
    }

    public static class AutonomousDecisionWorldPersistence
    {
        public static AutonomousDecisionWorldSaveData Build(AutonomousDecisionWorldState world)
        {
            var d = world?.LastResolvedDay;
            return new AutonomousDecisionWorldSaveData
            {
                HasLastResolvedDay = d.HasValue,
                LastResolvedYear = d?.Year ?? 0,
                LastResolvedMonth = d?.Month ?? 0,
                LastResolvedDay = d?.Day ?? 0,
                HasLastOpportunity=world?.LastOpportunityMinute.HasValue ?? false,
                LastOpportunityMinute=world?.LastOpportunityMinute ?? 0,
                LastOpportunityId=world?.LastOpportunityId
            };
        }

        public static bool Restore(AutonomousDecisionWorldState world, AutonomousDecisionWorldSaveData data)
        {
            if (world == null)
                throw new ArgumentNullException(nameof(world));
            if (!TryReadDay(data, out var day))
                return false;
            world.LastResolvedDay = day;
            world.LastOpportunityMinute=data != null && data.HasLastOpportunity ? (long?)data.LastOpportunityMinute : null;
            world.LastOpportunityId=data != null && data.HasLastOpportunity ? data.LastOpportunityId : null;
            return true;
        }

        // Preflight is read-only; Load can reject invalid data before restoring any world.
        public static bool IsValid(AutonomousDecisionWorldSaveData data) => TryReadDay(data, out _);

        private static bool TryReadDay(AutonomousDecisionWorldSaveData data, out ActivityDayStamp? day)
        {
            day = null;
            if (data != null && data.HasLastOpportunity
                && (data.LastOpportunityMinute<0 || string.IsNullOrWhiteSpace(data.LastOpportunityId))) return false;
            if (data == null || !data.HasLastResolvedDay)
                return true;
            var restored = new ActivityDayStamp(data.LastResolvedYear, data.LastResolvedMonth, data.LastResolvedDay);
            if (!restored.IsValid)
                return false;
            day = restored;
            return true;
        }
    }

    public interface IActivityExecutionGateway
    {
        ActivityResult Commit(string id, ActivityCandidate candidate);
        ActivityResult Begin(string id);
        ActivityResult Complete(string id, out ActivityCompletionSnapshot completion);
        ActivityResult Fail(string id);
    }

    public sealed class ActivityExecutionResult
    {
        public Proposal Proposal { get; }
        public string ActivityInstanceId { get; }
        public string Stage { get; }
        public ActivityResult Result { get; }
        public ActivityCompletionSnapshot Completion { get; }

        public ActivityExecutionResult(Proposal p, string id, string stage, ActivityResult result, ActivityCompletionSnapshot completion = null)
        {
            Proposal = p;
            ActivityInstanceId = id;
            Stage = stage;
            Result = result;
            Completion = completion;
        }
    }

    public enum DecisionDailyResult
    {
        Resolved,
        AlreadyResolved,
        MissingPersonSource,
        InvalidPhase,
        StaleWindow,
        PlanningFailure
    }

    public sealed class DecisionRoundResult
    {
        public DecisionDailyResult Result { get; }
        public string Diagnostic { get; }
        public DecisionPlan Plan { get; }
        public IReadOnlyList<ActivityExecutionResult> Executions { get; }

        public DecisionRoundResult(DecisionDailyResult result, string diagnostic = null, DecisionPlan plan = null, IEnumerable<ActivityExecutionResult> executions = null)
        {
            Result = result;
            Diagnostic = diagnostic ?? "";
            Plan = plan;
            Executions = Frozen.List(executions);
        }
    }

    public sealed class DecisionCoordinator
    {
        private readonly AutonomousDecisionWorldState world;
        public DecisionCoordinator(AutonomousDecisionWorldState world)
        {
            this.world = world ?? throw new ArgumentNullException(nameof(world));
        }

        public DecisionRoundResult Run(ActivityDayStamp currentDay, bool personSourceBound, bool phaseValid, ActivityDailyWindow window, Func<DecisionSnapshot> buildSnapshot, DecisionPlanner planner, IActivityExecutionGateway gateway)
        {
            if (!personSourceBound)
                return new DecisionRoundResult(DecisionDailyResult.MissingPersonSource);
            if (!phaseValid || !currentDay.IsValid)
                return new DecisionRoundResult(DecisionDailyResult.InvalidPhase);
            if (window == null || window.DayStamp != currentDay)
                return new DecisionRoundResult(DecisionDailyResult.StaleWindow);
            if (window.IsIntraday ? world.LastOpportunityMinute.HasValue
                && window.OpportunityMinute <= world.LastOpportunityMinute.Value : world.AlreadyResolved(currentDay))
                return new DecisionRoundResult(DecisionDailyResult.AlreadyResolved);
            DecisionPlan plan;
            try
            {
                if (gateway == null)
                    throw new ArgumentNullException(nameof(gateway));
                var snapshot = buildSnapshot();
                // Exact phase snapshot; equivalent-looking stale adapter output is not accepted.
                if (snapshot == null || !ReferenceEquals(snapshot.Window, window))
                    throw new ArgumentException("Snapshot/window mismatch");
                plan = (planner ?? new DecisionPlanner()).Plan(snapshot);
            }
            catch (Exception ex)
            {
                return new DecisionRoundResult(DecisionDailyResult.PlanningFailure, ex.GetType().Name + ": " + ex.Message);
            }

            var executions = new List<ActivityExecutionResult>();
            foreach (var proposal in plan.Winners)
            {
                var id = proposal.Identity + "|activity";
                var stage = "Commit";
                ActivityCompletionSnapshot completed = null;
                ActivityResult result;
                try
                {
                    result = gateway.Commit(id, proposal.Candidate);
                    if (result == ActivityResult.Success && !window.IsIntraday)
                    {
                        stage = "Begin";
                        result = gateway.Begin(id);
                        if (result == ActivityResult.Success)
                        {
                            stage = "Complete";
                            result = gateway.Complete(id, out completed);
                        }

                        if (result != ActivityResult.Success)
                            gateway.Fail(id);
                    }

                    executions.Add(new ActivityExecutionResult(proposal, id, stage, result, completed));
                }
                catch (Exception ex)
                {
                    // Never reroll or undo earlier successes. Best-effort terminal cleanup through the lifecycle.
                    try
                    {
                        gateway.Fail(id);
                    }
                    catch (Exception)
                    {
                    }

                    executions.Add(new ActivityExecutionResult(proposal, id, stage + ": " + ex.GetType().Name, ActivityResult.IllegalTransition));
                }
            }

            if (window.IsIntraday)
            { world.LastOpportunityMinute=window.OpportunityMinute; world.LastOpportunityId=window.OpportunityId; }
            world.LastResolvedDay = currentDay;
            return new DecisionRoundResult(DecisionDailyResult.Resolved, plan: plan, executions: executions);
        }
    }
}
