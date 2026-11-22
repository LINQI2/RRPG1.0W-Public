using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using RRPG.AutonomousActivity;
using RRPG.AutonomousSelection;
using RRPG.Deterministic;
using static RRPG.Tests.AutonomousSelection.SelectionTestData;

namespace RRPG.Tests.AutonomousSelection
{
    public class SelectionMechanismTests
    {
        [TestCase(-.1, .5, 1)]
        [TestCase(0, 0, 1)]
        [TestCase(0, 1, 1)]
        [TestCase(1, .5, 0)]
        public void A_InvalidChannel(double min, double neutral, double max)
        {
            Assert.Throws<ArgumentException>(() => new BehaviorChannelDefinition("x", "X", "test", neutral, min, max));
        }

        [Test]
        public void A_MissingBehaviorAndPreferenceNeutralZeroSideEffect()
        {
            var b = new ResolvedBehaviorProfile();
            var p = new SpecificPreferenceView();
            Assert.AreEqual(.5, b.Query(Channel()));
            Assert.AreEqual(0, p.Query("leisure.drink"));
            Assert.AreEqual(0, b.Count);
            Assert.AreEqual(0, p.Count);
        }

        [Test]
        public void A_DefensiveProfiles()
        {
            var d = new Dictionary<string, double>
            {
                {
                    "social.sociability",
                    .8
                }
            };
            var b = new ResolvedBehaviorProfile(d);
            d["social.sociability"] = .2;
            Assert.AreEqual(.8, b.Query(Channel()));
        }

        [Test]
        public void A_DuplicateConfigRejected()
        {
            var c = Catalog();
            Assert.Throws<ArgumentException>(() => new SelectionCatalog(c.Channels.Concat(c.Channels), c.Activities, c.Providers, c.Places, c.Calibration));
        }

        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        [TestCase(1.1)]
        public void A_InvalidPreference(double v)
        {
            Assert.Throws<ArgumentException>(() => new SpecificPreferenceView(new Dictionary<string, double> { { "leisure.drink", v } }));
        }

        [Test]
        public void A_MissingCalibrationRejected()
        {
            Assert.Throws<ArgumentException>(() => new SelectionCalibration(new Dictionary<string, double>()));
        }

        [Test]
        public void B_ImmutableSnapshotCollections()
        {
            var ps = new List<DecisionPersonSnapshot>
            {
                Person("B"),
                Person()
            };
            var rs = new List<RelationshipSnapshot>
            {
                new RelationshipSnapshot("A", "B", 20)
            };
            var cs = new List<ContextSignal>
            {
                new ContextSignal("Target.InjurySeverity", "Person", "Target", "B", 1)
            };
            var s = Snapshot(persons: ps, relationships: rs, context: cs);
            ps.Clear();
            rs.Clear();
            cs.Clear();
            CollectionAssert.AreEqual(new[] { "A", "B" }, s.Persons.Select(p => p.PersonId));
            Assert.AreEqual(20, s.Affinity("A", "B"));
            Assert.AreEqual(1, s.Signal("Target.InjurySeverity", "Person", "Target", "B"));
            Assert.Throws<NotSupportedException>(() => ((IList<DecisionPersonSnapshot>)s.Persons).Clear());
            Assert.Throws<NotSupportedException>(() => ((IList<ContextSignal>)s.Context).Clear());
        }

        [Test]
        public void B_DuplicatePersonRejected()
        {
            Assert.Throws<ArgumentException>(() => Snapshot(persons: new[] { Person(), Person() }));
        }

        [Test]
        public void B_OneSnapshotAcrossProviderAndAcceptance()
        {
            var s = Snapshot();
            var seen = new List<DecisionSnapshot>();
            new DecisionPlanner(new Provider((x, a) =>
            {
                seen.Add(x);
                return new[]
                {
                    Candidate(a, "Rest", null, a)
                };
            })).Plan(s);
            Assert.IsTrue(seen.All(x => ReferenceEquals(s, x)));
        }

        [Test]
        public void C_AdditiveExactBreakdown()
        {
            var e = DesireEvaluator.Evaluate(Snapshot(relationships: new[] { new RelationshipSnapshot("A", "B", 20) }), Candidate());
            Assert.AreEqual(30, e.Breakdown.BaseDesire);
            Assert.AreEqual(5.85, e.Breakdown.Families["Behavior"], 1e-8);
            Assert.AreEqual(16.25, e.Breakdown.Families["Relationship"], 1e-8);
            Assert.AreEqual(52.1, e.Breakdown.Score, 1e-8);
        }

        [TestCase(DesignerStrength.Weak, .35)]
        [TestCase(DesignerStrength.Medium, .65)]
        [TestCase(DesignerStrength.Strong, 1)]
        public void C_Strength(DesignerStrength strength, double value)
        {
            Assert.AreEqual(value, Calibration().Strength(strength));
        }

        [Test]
        public void C_FamilyAggregatesThenClamps()
        {
            var basis = Catalog();
            var bindings = Enumerable.Repeat(new DesireBinding(ContributionFamily.Behavior, "social.sociability", DesignerStrength.Strong), 8);
            var c = new SelectionCatalog(basis.Channels, new[] { new ActivityDesireProfile("Talk", 30, bindings) }, new PrototypeProviderDefinition[0], basis.Places, basis.Calibration);
            var e = DesireEvaluator.Evaluate(Snapshot(c, persons: new[] { Person(social: 1), Person("B") }), Candidate());
            Assert.AreEqual(15, e.Breakdown.Families["Behavior"]);
        }

        [TestCase(-200, -25)]
        [TestCase(0, 0)]
        [TestCase(200, 25)]
        public void C_RelationshipNormalization(double affinity, double expected)
        {
            var b = Catalog();
            var c = new SelectionCatalog(b.Channels, new[] { new ActivityDesireProfile("Talk", 0, new[] { new DesireBinding(ContributionFamily.Relationship, "Affinity", DesignerStrength.Strong) }) }, new PrototypeProviderDefinition[0], b.Places, b.Calibration);
            var e = DesireEvaluator.Evaluate(Snapshot(c, relationships: new[] { new RelationshipSnapshot("A", "B", affinity) }), Candidate());
            Assert.AreEqual(expected, e.Breakdown.Families["Relationship"]);
        }

        [Test]
        public void C_ContextOverrideCausal()
        {
            var s = Snapshot(context: new[] { new ContextSignal("Target.InjurySeverity", "Person", "Target", "B", 1) });
            Assert.Greater(DesireEvaluator.Evaluate(s, Candidate(type: "VisitSick")).Breakdown.Score, DesireEvaluator.Evaluate(s, Candidate()).Breakdown.Score);
        }

        [Test]
        public void C_PersonDifferenceCausal()
        {
            var s = Snapshot();
            Assert.Greater(DesireEvaluator.Evaluate(s, Candidate()).Breakdown.Score, DesireEvaluator.Evaluate(s, Candidate("B", target: "A")).Breakdown.Score);
        }

        [Test]
        public void C_RelationshipDifferenceCausal()
        {
            Assert.Greater(DesireEvaluator.Evaluate(Snapshot(relationships: new[] { new RelationshipSnapshot("A", "B", 20) }), Candidate()).Breakdown.Score, DesireEvaluator.Evaluate(Snapshot(), Candidate()).Breakdown.Score);
        }

        [Test]
        public void C_DrinkRepetitionCausal()
        {
            var first = Snapshot(persons: new[] { Person("A", pref: 1), Person("B") });
            var next = Snapshot(persons: new[] { Person("A", pref: 1, initiated: Day(1), type: "Drink"), Person("B") });
            var a = DesireEvaluator.Evaluate(first, Candidate(type: "Drink", target: null));
            var b = DesireEvaluator.Evaluate(next, Candidate(type: "Drink", target: null));
            Assert.AreEqual(-25, b.Breakdown.Families["Repetition"]);
            Assert.AreEqual(25, a.Breakdown.Score - b.Breakdown.Score);
        }

        [TestCase(15, 1)]
        [TestCase(75, 5)]
        [TestCase(1000, 5)]
        public void C_WeightBoundaryAndSaturation(double score, double expected)
        {
            Assert.AreEqual(expected, DesireEvaluator.Weight(score, Calibration()), 1e-10);
        }

        [Test]
        public void C_WeightMonotonicFinite()
        {
            double previous = 0;
            for (int score = 15; score < 150; score++)
            {
                var w = DesireEvaluator.Weight(score, Calibration());
                Assert.GreaterOrEqual(w, previous);
                Assert.IsFalse(double.IsNaN(w) || double.IsInfinity(w));
                previous = w;
            }
        }

        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        public void C_NonFiniteRejected(double score)
        {
            Assert.Throws<ArgumentException>(() => DesireEvaluator.Weight(score, Calibration()));
        }

        [Test]
        public void C_ThresholdFiltersActivityOnly()
        {
            var s = Snapshot(Catalog(Calibration(new KeyValuePair<string, double>("Threshold", 1000))));
            var p = new DecisionPlanner().Plan(s);
            Assert.IsTrue(p.Trace.All(t => t.Order.Count == 1 && t.Order[0].IsNoMajor));
            Assert.IsTrue(p.Trace.SelectMany(t => t.Evaluations).All(e => !e.Plausible));
        }

        [Test]
        public void C_PossibilityContextRequirement()
        {
            var p = new PrototypeActivityProvider();
            Assert.IsFalse(p.Collect(Snapshot(), "A").Any(c => c.ActivityType == "VisitSick" || c.ActivityType == "Help"));
            Assert.IsTrue(p.Collect(Snapshot(context: new[] { new ContextSignal("Target.HelpNeedSeverity", "TestContext", "Target", "B", 1) }), "A").Any(c => c.ActivityType == "Help"));
        }

        [Test]
        public void D_SourceReorderAndSameInput()
        {
            var a = new DecisionPlanner().Plan(Snapshot());
            var b = new DecisionPlanner(new Provider((s, id) => new PrototypeActivityProvider().Collect(s, id).Reverse())).Plan(Snapshot(persons: new[] { Person("B", .2, .8), Person() }));
            CollectionAssert.AreEqual(a.Trace.SelectMany(t => t.Order).Select(o => o.RandomIdentity), b.Trace.SelectMany(t => t.Order).Select(o => o.RandomIdentity));
            CollectionAssert.AreEqual(a.Winners.Select(p => p.Identity), b.Winners.Select(p => p.Identity));
        }

        [Test]
        public void D_IndependentCandidateRandomAndDomain()
        {
            var s = Snapshot();
            var c = Candidate();
            var o = new SelectionOption(s.RoundIdentity, "A", c, 1);
            var extra = new SelectionOption(s.RoundIdentity, "A", Candidate(key: "unrelated"), 100);
            Assert.AreEqual(o.RandomValue, new SelectionOption(s.RoundIdentity, "A", c, 10).RandomValue);
            Assert.AreNotEqual(DeterministicRandom.StableHash64(o.RandomIdentity), new Proposal(s.RoundIdentity, c).Priority);
            Assert.Greater(o.RandomValue, 0);
            Assert.Less(o.RandomValue, 1);
        }

        [Test]
        public void D_CanonicalRoundCultureInvariant()
        {
            var old = System.Globalization.CultureInfo.CurrentCulture;
            try
            {
                System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("ar-SA");
                Assert.AreEqual("village.daily.major|1|1|2", Snapshot().RoundIdentity);
            }
            finally
            {
                System.Globalization.CultureInfo.CurrentCulture = old;
            }
        }

        [Test]
        public void E_UnavailableAndInvalidAcceptance()
        {
            var s = Snapshot(actors: new[] { "A" });
            var a = new MinimalAcceptance();
            Assert.AreEqual(AcceptanceReason.InvalidParticipant, a.Evaluate(s, new Proposal(s.RoundIdentity, Candidate(target: "missing"))).Reason);
            Assert.AreEqual(AcceptanceReason.InvalidParticipant, a.Evaluate(s, new Proposal(s.RoundIdentity, Candidate(target: "A"))).Reason);
        }

        [Test]
        public void E_CompletedParticipantRejects()
        {
            var s = Snapshot(persons: new[] { Person(), Person("B", participation: Day()) });
            Assert.AreEqual(AcceptanceReason.ParticipantUnavailable, new MinimalAcceptance().Evaluate(s, new Proposal(s.RoundIdentity, Candidate())).Reason);
        }

        [Test]
        public void E_DisjointCoexistOverlapUniqueOrderInvariant()
        {
            var a = new Proposal("round", Candidate(type: "Rest", target: null));
            var b = new Proposal("round", Candidate("B", "Rest", null, "B"));
            Assert.AreEqual(2, FairArbitration.Resolve(new[] { a, b }, null, 1).Count(r => r.Result == "Winner"));
            var x = new Proposal("round", Candidate("B", target: "A", key: "other"));
            var first = FairArbitration.Resolve(new[] { a, x }, null, 1);
            var second = FairArbitration.Resolve(new[] { x, a }, null, 1);
            Assert.AreEqual(1, first.Count(r => r.Result == "Winner"));
            CollectionAssert.AreEqual(first.Select(r => r.Proposal.Identity), second.Select(r => r.Proposal.Identity));
        }

        [Test]
        public void E_ArbitrationHasNoUtilitySurface()
        {
            Assert.IsFalse(typeof(Proposal).GetProperties().Any(p => p.PropertyType == typeof(CandidateEvaluationResult) || p.Name.Contains("Weight") || p.Name.Contains("Utility")));
            var a = new Proposal("r", Candidate());
            var b = new Proposal("r", Candidate("B", target: "A", key: "B"));
            Assert.AreEqual(FairArbitration.Resolve(new[] { a, b }, null, 1).First().Proposal.Identity, FairArbitration.Resolve(new[] { b, a }, null, 1).First().Proposal.Identity);
        }

        [Test]
        public void E_NoMajorDoesNotClaimAndCanParticipate()
        {
            var s = Snapshot(Catalog(Calibration(new KeyValuePair<string, double>("Threshold", 1000))));
            var world = new AutonomousDecisionWorldState();
            var gateway = new Gateway(s.Window);
            var r = new DecisionCoordinator(world).Run(s.Day, true, true, s.Window, () => s, new DecisionPlanner(), gateway);
            Assert.AreEqual(0, gateway.Calls);
            Assert.IsEmpty(ActivityWorldPersistence.Build(gateway.World).PersonActivityStates);
            Assert.AreEqual(DecisionDailyResult.Resolved, r.Result);
        }

        [Test]
        public void E_RejectionsMoveFixedPointerNoRerollBounded()
        {
            var s = Snapshot(Catalog(Calibration(new KeyValuePair<string, double>("NoMajorWeight", .000001))));
            var r = new DecisionPlanner(acceptance: new RejectAll()).Plan(s);
            Assert.IsEmpty(r.Winners);
            Assert.IsTrue(r.Trace.All(t => t.FinalResult == "NoMajor"));
            foreach (var t in r.Trace)
                CollectionAssert.AreEqual(t.Order.Where(o => !o.IsNoMajor).Select(o => o.StableKey), t.Proposals.Select(p => p.Proposal.Candidate.CandidateKey));
        }

        [Test]
        public void E_CausalContestedInjuredTargetLoserAdvancesToNoMajor()
        {
            var catalog = Catalog(Calibration(new KeyValuePair<string, double>("NoMajorWeight", .0000001)));
            var s = Snapshot(catalog, persons: new[] { Person(), Person("B"), Person("C") }, context: new[] { new ContextSignal("Target.InjurySeverity", "Person", "Target", "C", 1) }, actors: new[] { "A", "B" });
            var plan = new DecisionPlanner(new Provider((snapshot, id) => new[] { Candidate(id, "VisitSick", "C", id + ".visit") })).Plan(s);
            Assert.AreEqual(1, plan.Winners.Count);
            var loser = plan.Trace.Single(t => t.ActorPersonId != plan.Winners[0].Candidate.ActorPersonId);
            Assert.AreEqual("ConflictLoss", loser.Proposals.Single().Result);
            Assert.AreEqual("NoMajor", loser.FinalResult);
            Assert.AreEqual(loser.Order[0].StableKey, loser.Proposals[0].Proposal.Candidate.CandidateKey);
            Assert.IsTrue(loser.Order[1].IsNoMajor);
            var reversed = new DecisionPlanner(new Provider((snapshot, id) => new[] { Candidate(id, "VisitSick", "C", id + ".visit") })).Plan(Snapshot(catalog, persons: s.Persons.Reverse(), context: s.Context, actors: new[] { "B", "A" }));
            Assert.AreEqual(plan.Winners[0].Identity, reversed.Winners[0].Identity);
        }

        [Test]
        public void E_NoMajorActorCanAcceptOthers()
        {
            var s = Snapshot(Catalog(Calibration(new KeyValuePair<string, double>("NoMajorWeight", .0000001))));
            var plan = new DecisionPlanner(new Provider((x, id) => id == "A" ? new ActivityCandidate[0] : new[] { Candidate("B", target: "A", key: "B.talk") })).Plan(s);
            Assert.AreEqual("NoMajor", plan.Trace.Single(t => t.ActorPersonId == "A").FinalResult);
            Assert.AreEqual("B", plan.Winners.Single().Candidate.ActorPersonId);
            var gateway = new Gateway(s.Window);
            var result = new DecisionCoordinator(new AutonomousDecisionWorldState()).Run(s.Day, true, true, s.Window, () => s, new DecisionPlanner(new Provider((x, id) => id == "A" ? new ActivityCandidate[0] : new[] { Candidate("B", target: "A", key: "B.talk") })), gateway);
            Assert.AreEqual(s.Day, gateway.World.QueryPersonActivityState("A").LastMajorParticipationDay);
            Assert.IsNull(gateway.World.QueryPersonActivityState("A").LastInitiatedActivityDay);
            Assert.AreEqual(ActivityResult.Success, result.Executions.Single().Result);
        }

        [Test]
        public void B_UngovernedContextFailsClosed()
        {
            Assert.Throws<ArgumentException>(() => Snapshot(context: new[] { new ContextSignal("arbitrary", "Unknown", "Target", "A", 1) }));
        }

        [TestCase(ContributionFamily.Preference, 20)]
        [TestCase(ContributionFamily.Relationship, 25)]
        [TestCase(ContributionFamily.Context, 40)]
        [TestCase(ContributionFamily.Repetition, -25)]
        public void C_AllFamilyClamps(ContributionFamily family, double expected)
        {
            var basis = Catalog();
            var key = family == ContributionFamily.Preference ? "leisure.drink" : family == ContributionFamily.Relationship ? "Affinity" : family == ContributionFamily.Context ? "Target.InjurySeverity" : "LastInitiatedType";
            var binding = new DesireBinding(family, key, DesignerStrength.Strong, family == ContributionFamily.Repetition ? -1 : 1, family == ContributionFamily.Context ? "Person" : null, family == ContributionFamily.Context ? "Target" : null);
            var catalog = new SelectionCatalog(basis.Channels, new[] { new ActivityDesireProfile("Talk", 30, Enumerable.Repeat(binding, 10)) }, new PrototypeProviderDefinition[0], basis.Places, basis.Calibration);
            var s = Snapshot(catalog, persons: new[] { Person(pref: 1, initiated: Day(1), type: "Talk"), Person("B") }, relationships: new[] { new RelationshipSnapshot("A", "B", 20) }, context: new[] { new ContextSignal("Target.InjurySeverity", "Person", "Target", "B", 1) });
            Assert.AreEqual(expected, DesireEvaluator.Evaluate(s, Candidate()).Breakdown.Families[family.ToString()]);
        }

        private sealed class RejectAll : IParticipantAcceptance
        {
            public ParticipantAcceptanceResult Evaluate(DecisionSnapshot s, Proposal p) => new ParticipantAcceptanceResult(AcceptanceReason.HardReject);
        }

        [Test]
        public void F_NoMajorExactlyOnceAndRestore()
        {
            var s = Snapshot(Catalog(Calibration(new KeyValuePair<string, double>("Threshold", 1000))));
            var w = new AutonomousDecisionWorldState();
            var g = new Gateway(s.Window);
            var c = new DecisionCoordinator(w);
            Assert.AreEqual(DecisionDailyResult.Resolved, c.Run(s.Day, true, true, s.Window, () => s, null, g).Result);
            var save = AutonomousDecisionWorldPersistence.Build(w);
            var other = new AutonomousDecisionWorldState();
            AutonomousDecisionWorldPersistence.Restore(other, save);
            Assert.AreEqual(DecisionDailyResult.AlreadyResolved, new DecisionCoordinator(other).Run(s.Day, true, true, s.Window, () => throw new Exception(), null, g).Result);
        }

        [Test]
        public void F_ReplacementNullInvalidAndNoTransientSave()
        {
            var w = new AutonomousDecisionWorldState();
            AutonomousDecisionWorldPersistence.Restore(w, new AutonomousDecisionWorldSaveData { HasLastResolvedDay = true, LastResolvedYear = 1, LastResolvedMonth = 1, LastResolvedDay = 2 });
            Assert.IsTrue(w.AlreadyResolved(Day()));
            AutonomousDecisionWorldPersistence.Restore(w, null);
            Assert.IsFalse(w.AlreadyResolved(Day()));
            Assert.IsFalse(AutonomousDecisionWorldPersistence.Restore(w, new AutonomousDecisionWorldSaveData { HasLastResolvedDay = true }));
            CollectionAssert.AreEquivalent(new[]{"HasLastResolvedDay","LastResolvedYear","LastResolvedMonth","LastResolvedDay",
                "HasLastOpportunity","LastOpportunityMinute","LastOpportunityId"},typeof(AutonomousDecisionWorldSaveData).GetFields().Select(f=>f.Name));
        }

        [TestCase(0, 1, 1)]
        [TestCase(1, 13, 1)]
        [TestCase(1, 1, 32)]
        public void F_InvalidRestoreRetainsResolvedDay(int year, int month, int day)
        {
            var world = new AutonomousDecisionWorldState();
            var valid = new AutonomousDecisionWorldSaveData { HasLastResolvedDay = true, LastResolvedYear = 2, LastResolvedMonth = 3, LastResolvedDay = 4 };
            Assert.IsTrue(AutonomousDecisionWorldPersistence.Restore(world, valid));
            var invalid = new AutonomousDecisionWorldSaveData { HasLastResolvedDay = true, LastResolvedYear = year, LastResolvedMonth = month, LastResolvedDay = day };
            Assert.IsFalse(AutonomousDecisionWorldPersistence.IsValid(invalid));
            Assert.IsFalse(AutonomousDecisionWorldPersistence.Restore(world, invalid));
            Assert.AreEqual(new ActivityDayStamp(2, 3, 4), world.LastResolvedDay);
            Assert.IsTrue(world.AlreadyResolved(new ActivityDayStamp(2, 3, 4)));
        }

        [TestCase(false, true, DecisionDailyResult.MissingPersonSource)]
        [TestCase(true, false, DecisionDailyResult.InvalidPhase)]
        public void G_Guards(bool source, bool phase, DecisionDailyResult expected)
        {
            var s = Snapshot();
            Assert.AreEqual(expected, new DecisionCoordinator(new AutonomousDecisionWorldState()).Run(s.Day, source, phase, s.Window, () => throw new Exception(), null, new Gateway(s.Window)).Result);
        }

        [Test]
        public void G_StaleGuard()
        {
            var s = Snapshot();
            Assert.AreEqual(DecisionDailyResult.StaleWindow, new DecisionCoordinator(new AutonomousDecisionWorldState()).Run(Day(3), true, true, s.Window, () => s, null, new Gateway(s.Window)).Result);
        }

        [Test]
        public void G_PlanningExceptionZeroMutation()
        {
            var s = Snapshot();
            var w = new AutonomousDecisionWorldState();
            var g = new Gateway(s.Window);
            var r = new DecisionCoordinator(w).Run(s.Day, true, true, s.Window, () => s, new DecisionPlanner(new Provider((x, a) => throw new Exception("provider"))), g);
            Assert.AreEqual(DecisionDailyResult.PlanningFailure, r.Result);
            Assert.AreEqual(0, g.Calls);
            Assert.IsNull(w.LastResolvedDay);
        }

        [Test]
        public void G_ExecutionFailureExplicitNoReroll()
        {
            var s = Snapshot(Catalog(Calibration(new KeyValuePair<string, double>("NoMajorWeight", .000001))));
            var g = new Gateway(s.Window)
            {
                Reject = true
            };
            var w = new AutonomousDecisionWorldState();
            var r = new DecisionCoordinator(w).Run(s.Day, true, true, s.Window, () => s, null, g);
            Assert.IsTrue(r.Executions.All(e => e.Stage == "Commit" && e.Result == ActivityResult.ClaimConflict));
            Assert.AreEqual(r.Plan.Winners.Count, g.Calls);
            Assert.IsTrue(w.AlreadyResolved(s.Day));
        }

        [Test]
        public void H_PureModuleReferences()
        {
            var refs = typeof(DecisionPlanner).Assembly.GetReferencedAssemblies().Select(a => a.Name).ToArray();
            Assert.IsFalse(refs.Any(n => n.Contains("Unity") || n.Contains("Social") || n.Contains("Village") || n.Contains("Person") || n.Contains("Event")));
            Assert.Contains("AutonomousActivity.Runtime", refs);
            Assert.Contains("RRPG.Deterministic.Runtime", refs);
        }

        [Test]
        public void Experience_20DaysCausalTraceAndFsm()
        {
            var world = new AutonomousDecisionWorldState();
            var activity = new ActivityWorldState();
            var service = new ActivityLifecycleService(activity);
            var results = new List<string>();
            for (int day = 2; day <= 21; day++)
            {
                var ps = new[]
                {
                    Person("A", .8, .8),
                    Person("B", .5, .5),
                    Person("C", .8, .2, 1),
                    Person("D", .2, .8)
                }.Select(p =>
                {
                    var r = activity.QueryPersonActivityState(p.PersonId);
                    return new DecisionPersonSnapshot(p.PersonId, p.PersonDefinitionId, p.Behavior, p.Preferences, r.LastMajorParticipationDay, r.LastInitiatedActivityDay, r.LastInitiatedActivityType);
                }).ToArray();
                var s = Snapshot(persons: ps, context: day % 5 == 0 ? new[] { new ContextSignal("Target.InjurySeverity", "Person", "Target", "B", 1) } : null, day: day);
                service.OpenDailyWindow(s.Window);
                var gateway = new ExistingGateway(service);
                var r = new DecisionCoordinator(world).Run(s.Day, true, true, s.Window, () => s, null, gateway);
                Assert.AreEqual(DecisionDailyResult.Resolved, r.Result);
                foreach (var e in r.Executions)
                {
                    Assert.AreEqual(ActivityResult.Success, e.Result);
                    Assert.IsNotNull(e.Completion);
                    results.Add(e.Completion.ActivityType);
                }

                foreach (var t in r.Plan.Trace)
                {
                    Assert.IsNotEmpty(t.Order);
                    Assert.IsTrue(t.Evaluations.All(e => e.Breakdown != null));
                }

                Assert.IsTrue(ps.All(p => activity.QueryClaim(p.PersonId) == null));
            }

            Assert.Greater(results.Distinct().Count(), 1);
            TestContext.WriteLine("20-day completed activities: " + string.Join(", ", results.GroupBy(x => x).Select(g => g.Key + "=" + g.Count())));
        }

        private sealed class ExistingGateway : IActivityExecutionGateway
        {
            private readonly ActivityLifecycleService s;
            internal ExistingGateway(ActivityLifecycleService s)
            {
                this.s = s;
            }

            public ActivityResult Commit(string id, ActivityCandidate c) => s.TryCommitActivity(id, c);
            public ActivityResult Begin(string id) => s.TryBeginExecution(id);
            public ActivityResult Complete(string id, out ActivityCompletionSnapshot c) => s.TryComplete(id, out c);
            public ActivityResult Fail(string id) => s.TryFail(id);
        }
    }
}
