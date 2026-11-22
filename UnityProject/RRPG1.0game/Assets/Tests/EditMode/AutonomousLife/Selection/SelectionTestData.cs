using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using RRPG.AutonomousActivity;
using RRPG.AutonomousSelection;

namespace RRPG.Tests.AutonomousSelection
{
    internal static class SelectionTestData
    {
        internal static ActivityDayStamp Day(int d = 2) => new ActivityDayStamp(1, 1, d);
        internal static SelectionCalibration Calibration(params KeyValuePair<string, double>[] overrides)
        {
            var d = new Dictionary<string, double>
            {
                {
                    "BehaviorBudget",
                    15
                },
                {
                    "PreferenceBudget",
                    20
                },
                {
                    "RelationshipBudget",
                    25
                },
                {
                    "ContextBudget",
                    40
                },
                {
                    "RepetitionBudget",
                    25
                },
                {
                    "Weak",
                    .35
                },
                {
                    "Medium",
                    .65
                },
                {
                    "Strong",
                    1
                },
                {
                    "Threshold",
                    15
                },
                {
                    "WeightSpan",
                    60
                },
                {
                    "WeightGain",
                    4
                },
                {
                    "WeightPower",
                    1.5
                },
                {
                    "NoMajorWeight",
                    .75
                },
                {
                    "RelationshipReferenceMagnitude",
                    20
                }
            };
            foreach (var p in overrides)
                d[p.Key] = p.Value;
            return new SelectionCalibration(d);
        }

        internal static BehaviorChannelDefinition Channel(string key = "social.sociability") => new BehaviorChannelDefinition(key, key, "Prototype channel", .5, 0, 1);
        internal static SelectionCatalog Catalog(SelectionCalibration c = null)
        {
            var medium = DesignerStrength.Medium;
            var weak = DesignerStrength.Weak;
            var strong = DesignerStrength.Strong;
            DesireBinding B(ContributionFamily f, string key, DesignerStrength s, int dir = 1, string domain = null, string role = null) => new DesireBinding(f, key, s, dir, domain, role);
            return new SelectionCatalog(new[] { Channel(), Channel("social.supportiveness") }, new[] { new ActivityDesireProfile("Rest", 22, new[] { B(ContributionFamily.Repetition, "LastInitiatedType", weak, -1) }), new ActivityDesireProfile("Drink", 28, new[] { B(ContributionFamily.Behavior, "social.sociability", weak), B(ContributionFamily.Preference, "leisure.drink", strong), B(ContributionFamily.Repetition, "LastInitiatedType", strong, -1) }), new ActivityDesireProfile("Talk", 30, new[] { B(ContributionFamily.Behavior, "social.sociability", medium), B(ContributionFamily.Relationship, "Affinity", medium), B(ContributionFamily.Repetition, "LastInitiatedType", medium, -1) }), new ActivityDesireProfile("VisitSick", 18, new[] { B(ContributionFamily.Behavior, "social.supportiveness", medium), B(ContributionFamily.Relationship, "Affinity", weak), B(ContributionFamily.Context, "Target.InjurySeverity", strong, 1, "Person", "Target"), B(ContributionFamily.Repetition, "LastInitiatedType", weak, -1) }), new ActivityDesireProfile("Help", 18, new[] { B(ContributionFamily.Behavior, "social.supportiveness", medium), B(ContributionFamily.Relationship, "Affinity", medium), B(ContributionFamily.Context, "Target.HelpNeedSeverity", strong, 1, "TestContext", "Target") }) }, new[] { new PrototypeProviderDefinition("rest", "Rest", "Home", false), new PrototypeProviderDefinition("drink", "Drink", "Tavern", false), new PrototypeProviderDefinition("talk", "Talk", "Tavern", true), new PrototypeProviderDefinition("visit", "VisitSick", "Home", true, "Target.InjurySeverity", "Person"), new PrototypeProviderDefinition("help", "Help", "Workshop", true, "Target.HelpNeedSeverity", "TestContext") }, new[] { "Home", "Tavern", "Workshop" }, c ?? Calibration());
        }

        internal static DecisionPersonSnapshot Person(string id = "A", double social = .8, double support = .8, double pref = 0, ActivityDayStamp? participation = null, ActivityDayStamp? initiated = null, string type = null) => new DecisionPersonSnapshot(id, "prototype." + id, new ResolvedBehaviorProfile(new Dictionary<string, double> { { "social.sociability", social }, { "social.supportiveness", support } }), new SpecificPreferenceView(new Dictionary<string, double> { { "leisure.drink", pref } }), participation, initiated, type);
        internal static DecisionSnapshot Snapshot(SelectionCatalog catalog = null, IEnumerable<DecisionPersonSnapshot> persons = null, IEnumerable<RelationshipSnapshot> relationships = null, IEnumerable<ContextSignal> context = null, int day = 2, IEnumerable<string> actors = null)
        {
            var ps = (persons ?? new[]
            {
                Person(),
                Person("B", .2, .8)
            }

            ).ToList();
            return new DecisionSnapshot(new ActivityDailyWindow(Day(day), actors ?? ps.Select(p => p.PersonId), ps.Select(p => p.PersonId)), ps, relationships, context, catalog ?? Catalog());
        }

        internal static ActivityCandidate Candidate(string actor = "A", string type = "Talk", string target = "B", string key = "candidate") => new ActivityCandidate(key, type, "test", actor, target, "Tavern");
        internal sealed class Provider : IDecisionCandidateProvider
        {
            private readonly Func<DecisionSnapshot, string, IEnumerable<ActivityCandidate>> collect;
            internal Provider(Func<DecisionSnapshot, string, IEnumerable<ActivityCandidate>> collect)
            {
                this.collect = collect;
            }

            public IEnumerable<ActivityCandidate> Collect(DecisionSnapshot s, string a) => collect(s, a);
        }

        internal sealed class Gateway : IActivityExecutionGateway
        {
            internal readonly ActivityWorldState World = new ActivityWorldState();
            internal readonly ActivityLifecycleService Service;
            internal int Calls;
            internal bool Reject;
            internal Gateway(ActivityDailyWindow w)
            {
                Service = new ActivityLifecycleService(World);
                Service.OpenDailyWindow(w);
            }

            public ActivityResult Commit(string id, ActivityCandidate c)
            {
                Calls++;
                return Reject ? ActivityResult.ClaimConflict : Service.TryCommitActivity(id, c);
            }

            public ActivityResult Begin(string id) => Service.TryBeginExecution(id);
            public ActivityResult Complete(string id, out ActivityCompletionSnapshot c) => Service.TryComplete(id, out c);
            public ActivityResult Fail(string id) => Service.TryFail(id);
        }
    }
}
