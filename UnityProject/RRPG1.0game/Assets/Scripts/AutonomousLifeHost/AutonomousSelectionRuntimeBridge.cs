using System;
using System.Collections.Generic;
using System.Linq;
using RRPG.AutonomousActivity;
using RRPG.AutonomousSelection;
using RRPG.StaticConfig;

/// <summary>Product projection only. No Utility/arbitration algorithm or Person authority lives here.</summary>
public static class AutonomousSelectionConfigAdapter
{
    public static SelectionCatalog BuildCatalog(AutonomousLifeConfigDatabase db) => Build(db,false);
    public static SelectionCatalog BuildIntradayCatalog(AutonomousLifeConfigDatabase db) => Build(db,true);
    private static SelectionCatalog Build(AutonomousLifeConfigDatabase db,bool intraday)
    {
        if (db == null)
            throw new ArgumentNullException(nameof(db));
        AutonomousLifeConfigDatabase.ValidatePrototypeInjuryCalibration(db.AutonomousLifeCalibration);
        var bindings = new Dictionary<string, List<DesireBinding>>(StringComparer.Ordinal);
        foreach (var a in db.Activity)
            bindings.Add(a.ActivityType, new List<DesireBinding>());
        void add(string activity, ContributionFamily family, string key, string strength, int direction, string domain = null, string role = null) => bindings[activity].Add(new DesireBinding(family, key, (DesignerStrength)Enum.Parse(typeof(DesignerStrength), strength), direction, domain, role));
        foreach (var b in db.ActivityBehaviorBinding)
            add(b.ActivityType, ContributionFamily.Behavior, b.Key, b.Strength, b.Direction);
        foreach (var b in db.ActivityPreferenceBinding)
            add(b.ActivityType, ContributionFamily.Preference, b.Key, b.Strength, b.Direction);
        foreach (var b in db.ActivityRelationshipBinding)
            add(b.ActivityType, ContributionFamily.Relationship, b.Key, b.Strength, b.Direction);
        foreach (var b in db.ActivityContextBinding)
            add(b.ActivityType, ContributionFamily.Context, b.Key, b.Strength, b.Direction, b.SourceDomain, b.SubjectRole);
        foreach (var b in db.ActivityRepetition)
            add(b.ActivityType, ContributionFamily.Repetition, b.Key, b.Strength, b.Direction);
        return new SelectionCatalog(db.BehaviorChannel.Select(c => new BehaviorChannelDefinition(c.ChannelId, c.DisplayName, c.Description, c.NeutralValue, c.Min, c.Max)), db.Activity.Select(a => new ActivityDesireProfile(a.ActivityType, a.BaseDesire, bindings[a.ActivityType])), db.ActivityProvider.Select(p => new PrototypeProviderDefinition(p.ProviderId, p.ActivityType, p.PlaceId, p.RequiresTarget, p.RequiredContextKey, p.ContextDomain, intraday && p.RequiresTarget ? ActivityTargetParticipation.ReceptionAtArrival : ActivityTargetParticipation.Exclusive)), db.Place.Select(p => p.PlaceId), new SelectionCalibration(db.AutonomousLifeCalibration.Select(c => new KeyValuePair<string, double>(c.CalibrationKey, c.Value))));
    }

    public static DecisionPersonSnapshot Resolve(AutonomousLifeConfigDatabase db, SelectionCatalog catalog, string personId, string definitionId, PersonActivityState recent)
    {
        var behavior = db.PersonBehavior.Where(b => b.PersonDefinitionId == definitionId).ToList();
        var preferences = db.PersonPreference.Where(p => p.PersonDefinitionId == definitionId).ToList();
        foreach (var b in behavior)
        {
            var d = catalog.Channels.Single(c => c.ChannelId == b.ChannelId);
            if (double.IsNaN(b.Value) || double.IsInfinity(b.Value) || b.Value < d.Min || b.Value > d.Max)
                throw new ArgumentException("Invalid profile value");
        }

        return new DecisionPersonSnapshot(personId, definitionId, new ResolvedBehaviorProfile(behavior.Select(b => new KeyValuePair<string, double>(b.ChannelId, b.Value))), new SpecificPreferenceView(preferences.Select(p => new KeyValuePair<string, double>(p.PreferenceKey, p.Value))), recent.LastMajorParticipationDay, recent.LastInitiatedActivityDay, recent.LastInitiatedActivityType, behavior.Count == 0 && preferences.Count == 0 ? "MissingPrototypeProfile: " + (definitionId ?? "legacy") : "", recent.LastMissedActivityType, recent.LastMissedTargetPersonId, recent.LastMissedMinute);
    }
}

public static class AutonomousSelectionRuntimeBridge
{
    private static readonly AutonomousDecisionWorldState world = new AutonomousDecisionWorldState();
    public static DecisionRoundResult LastResult { get; private set; }
    internal static void ReportOpportunityFailure(DecisionDailyResult result,string diagnostic)
        => LastResult=new DecisionRoundResult(result,diagnostic);

    // Test context only: no HelpNeed world/save authority, production default is no fixture.
    public static Func<IEnumerable<ContextSignal>> ContextFixtureForTests;
    public static DecisionPlanner PlannerForTests;
    public static DecisionRoundResult RunDailyPhase(ActivityDayStamp currentDay, PlayerCollection source, bool phaseValid)
    {
        var window = ActivityRuntimeBridge.QueryCurrentWindow();
        if (source != null && phaseValid && currentDay.IsValid && window != null && window.DayStamp == currentDay
            && !AutonomousSocialFeedbackRuntimeBridge.DrainBarrier().Settled)
            return LastResult = new DecisionRoundResult(DecisionDailyResult.InvalidPhase, "SocialFeedbackUnsettled");
        LastResult = new DecisionCoordinator(world).Run(currentDay, source != null, phaseValid, ActivityRuntimeBridge.QueryCurrentWindow(), () => BuildSnapshot(source), PlannerForTests, new LifecycleGateway());
        return LastResult;
    }
    public static DecisionRoundResult RunOpportunity(ActivityDayStamp day,PlayerCollection source,long minute,IntradayActivitySchedule schedule)
    {
        var window=ActivityRuntimeBridge.QueryCurrentWindow();
        if(window==null || !window.IsIntraday || window.OpportunityMinute!=minute || window.DayStamp!=day)
            return LastResult=new DecisionRoundResult(DecisionDailyResult.StaleWindow);
        if(!AutonomousSocialFeedbackRuntimeBridge.DrainBarrier().Settled)
            return LastResult=new DecisionRoundResult(DecisionDailyResult.InvalidPhase,"SocialFeedbackUnsettled");
        return LastResult=new DecisionCoordinator(world).Run(day,source!=null,true,window,()=>BuildSnapshot(source),
            PlannerForTests,new ScheduledGateway(minute,schedule));
    }
    private sealed class ScheduledGateway : IActivityExecutionGateway
    {
        private readonly long minute; private readonly IntradayActivitySchedule schedule;
        public ScheduledGateway(long minute,IntradayActivitySchedule schedule) { this.minute=minute;this.schedule=schedule; }
        public ActivityResult Commit(string id,ActivityCandidate candidate)
        {
            long arrival=new WorldInstant(minute).AddMinutes(schedule.CommuteMinutes).Minute;
            long completion=new WorldInstant(arrival).AddMinutes(schedule.Duration(candidate.ActivityType)).Minute;
            string destination=candidate.TargetParticipation==ActivityTargetParticipation.ReceptionAtArrival ? VillageRuntimeBridge.ReceptionPolicy.KnownHome(candidate.TargetPersonId) : candidate.ActivityType=="Rest" ? VillageRuntimeBridge.ReceptionPolicy.KnownHome(candidate.ActorPersonId) : candidate.PlaceId;
            return ActivityRuntimeBridge.TryCommitScheduledAtPlace(id,candidate,minute,arrival,completion,destination);
        }
        public ActivityResult Begin(string id) => ActivityResult.IllegalTransition;
        public ActivityResult Complete(string id,out ActivityCompletionSnapshot completion) { completion=null; return ActivityResult.IllegalTransition; }
        public ActivityResult Fail(string id) => ActivityRuntimeBridge.TryFail(id);
    }

    private static DecisionSnapshot BuildSnapshot(PlayerCollection source)
    {
        if (!StaticConfigRegistry.TryGetAutonomousLifeConfig(out var db, out var diagnostic))
            throw new InvalidOperationException(diagnostic);
        var window = ActivityRuntimeBridge.QueryCurrentWindow();
        var catalog = window.IsIntraday ? AutonomousSelectionConfigAdapter.BuildIntradayCatalog(db) : AutonomousSelectionConfigAdapter.BuildCatalog(db);
        var ids = window.ActorEligiblePersonIds.Concat(window.ParticipantAvailablePersonIds).Distinct(StringComparer.Ordinal).OrderBy(id => id, StringComparer.Ordinal).ToList();
        if(window.IsIntraday) ids=ids.Concat(source.OwnedCharacters.Where(c=>c!=null && !c.IsDead).Select(c=>c.PersonId)).Concat(source.OwnedHeroes.Where(h=>h!=null).Select(h=>h.PersonId)).Where(id=>!string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.Ordinal).OrderBy(id=>id,StringComparer.Ordinal).ToList();
        var persons = new List<DecisionPersonSnapshot>();
        var context = new List<ContextSignal>();
        var relationships = new List<RelationshipSnapshot>();
        foreach (var id in ids)
        {
            var characters = source.OwnedCharacters.Where(c => c != null && c.PersonId == id).ToList();
            var heroes = source.OwnedHeroes.Where(h => h != null && h.PersonId == id).ToList();
            if (characters.Count + heroes.Count != 1)
                throw new ArgumentException("Missing/ambiguous source PersonId: " + id);
            var definition = characters.Count == 1 ? characters[0].PersonDefinitionId : null;
            var injury = characters.Count == 1 ? characters[0].Injury : heroes[0].Injury;
            persons.Add(AutonomousSelectionConfigAdapter.Resolve(db, catalog, id, definition, ActivityRuntimeBridge.QueryPersonActivityState(id)));
            double magnitude = 0;
            if (injury != InjuryStatus.Normal)
            {
                var key = injury == InjuryStatus.LightInjury ? "InjuryLight" : injury == InjuryStatus.HeavyInjury ? "InjuryHeavy" : "InjuryDisabled";
                magnitude = catalog.Calibration.Query(key);
            }

            context.Add(new ContextSignal("Target.InjurySeverity", "Person", "Target", id, magnitude));
        }

        if (ContextFixtureForTests != null)
            context.AddRange(ContextFixtureForTests());
        foreach (var actor in window.ActorEligiblePersonIds)
            foreach (var target in ids)
                if (actor != target)
                    relationships.Add(new RelationshipSnapshot(actor, target, SocialRuntimeBridge.QueryRelationship(actor, target).Affinity));
        return new DecisionSnapshot(window, persons, relationships, context, catalog, VillageRuntimeBridge.ReceptionPolicy.MissedCooldownMinutes);
    }

    private sealed class LifecycleGateway : IActivityExecutionGateway
    {
        public ActivityResult Commit(string id, ActivityCandidate candidate) => ActivityRuntimeBridge.TryCommitActivity(id, candidate);
        public ActivityResult Begin(string id) => ActivityRuntimeBridge.TryBeginExecution(id);
        public ActivityResult Complete(string id, out ActivityCompletionSnapshot completion) => AutonomousSocialFeedbackRuntimeBridge.TryComplete(id, out completion);
        public ActivityResult Fail(string id) => ActivityRuntimeBridge.TryFail(id);
    }

    public static AutonomousDecisionWorldSaveData BuildSaveData() => AutonomousDecisionWorldPersistence.Build(world);
    public static bool Restore(AutonomousDecisionWorldSaveData data)
    {
        if (!AutonomousDecisionWorldPersistence.Restore(world, data))
            return false;
        LastResult = null;
        return true;
    }

    public static void ResetForNewGame() => Restore(null);
    public static void ResetForTests()
    {
        Restore(null);
        PlannerForTests = null;
        ContextFixtureForTests = null;
    }
}
