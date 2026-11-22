using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using RRPG.AutonomousActivity;
using RRPG.AutonomousSocialFeedback;
using RRPG.Social;
using RRPG.StaticConfig;

namespace RRPG.Tests.SocialFeedback
{
    public class SocialFeedbackDomainTests
    {
        private const string Visit = "relational.visited_sick_person";
        internal static RelationalFactRecord Fact(string id = "activity:visit", string type = Visit) => new RelationalFactRecord(id, type, "zhang", "wang", 1, 1, 2);
        internal static SocialRuleCatalog Rules(float medium = 8, string source = "Target", string target = "Actor", string polarity = "Positive")
            => new SocialRuleCatalog(new[] { Visit, "relational.helped_person" },
                new[] { new RelationshipInterpretationRule("visit", Visit, source, target, "Affinity", polarity, "Medium"), new RelationshipInterpretationRule("help", "relational.helped_person", source, target, "Affinity", polarity, "Medium") },
                new[] { new KeyValuePair<string, float>("AffinityDeltaWeak", 4), new KeyValuePair<string, float>("AffinityDeltaMedium", medium), new KeyValuePair<string, float>("AffinityDeltaStrong", 12) });
        internal static ActivityInstance Instance(string type, ActivityLifecycleState state = ActivityLifecycleState.Executing, string target = "wang")
        {
            var world = new ActivityWorldState(); var service = new ActivityLifecycleService(world);
            service.OpenDailyWindow(new ActivityDailyWindow(new ActivityDayStamp(1, 1, 2), new[] { "zhang" }, new[] { "zhang", "wang" }));
            Assert.AreEqual(ActivityResult.Success, service.TryCommitActivity("fixed", new ActivityCandidate("fixed", type, "prototype", "zhang", target)));
            if (state != ActivityLifecycleState.Committed) service.TryBeginExecution("fixed");
            if (state == ActivityLifecycleState.Failed) service.TryFail("fixed");
            if (state == ActivityLifecycleState.Interrupted) service.TryInterrupt("fixed");
            if (state == ActivityLifecycleState.Completed) service.TryComplete("fixed", out _);
            return world.QueryActivityInstance("fixed");
        }
        internal static ActivitySocialFactProjector Projector() => new ActivitySocialFactProjector(new[]
        { new KeyValuePair<string,string>("VisitSick", Visit), new KeyValuePair<string,string>("Help", "relational.helped_person") }, Rules());
        [TestCase("Rest")] [TestCase("Drink")] [TestCase("Talk")]
        public void A_LifestyleNoFeedback(string type) { var p = Projector().Prepare(Instance(type, target: null)); Assert.AreEqual(ProjectionResult.NoFeedback, p.Result); Assert.IsNull(p.Fact); }
        [TestCase("VisitSick", Visit)] [TestCase("Help", "relational.helped_person")]
        public void A_PersistentProjectionStableIdentity(string activity, string factType)
        { var i = Instance(activity); var a = Projector().Prepare(i); var b = Projector().Prepare(i); Assert.AreEqual(ProjectionResult.Prepared, a.Result); Assert.AreEqual("activity:fixed", a.Fact.SourceOccurrenceId); Assert.AreEqual(factType, a.Fact.FactType); Assert.AreEqual(a.Fact.SourceOccurrenceId, b.Fact.SourceOccurrenceId); Assert.AreEqual("zhang", a.Fact.ActorPersonId); Assert.AreEqual("wang", a.Fact.TargetPersonId); }
        [TestCase(ActivityLifecycleState.Committed)] [TestCase(ActivityLifecycleState.Failed)] [TestCase(ActivityLifecycleState.Interrupted)] [TestCase(ActivityLifecycleState.Completed)]
        public void A_NoProjectionOutsideExecuting(ActivityLifecycleState state) => Assert.AreEqual(ProjectionResult.InvalidProjection, Projector().Prepare(Instance("VisitSick", state)).Result);
        [Test] public void A_InvalidTargetFailsBeforeComplete() => Assert.AreEqual(ProjectionResult.InvalidProjection, Projector().Prepare(Instance("VisitSick", target: null)).Result);
        [Test] public void A_MissingMappingIsNoFeedback() => Assert.AreEqual(ProjectionResult.NoFeedback, Projector().Prepare(Instance("FutureActivity")).Result);
        [Test] public void A_MissingFactRuleFailsPreparationConfiguration() => Assert.Throws<ArgumentException>(() => new ActivitySocialFactProjector(new[] { new KeyValuePair<string,string>("VisitSick", "missing") }, Rules()));
        [Test] public void A_ProjectionIsImmutable() { var fact = Projector().Prepare(Instance("Help")).Fact; Assert.IsTrue(fact.GetType().GetProperties().All(p => p.SetMethod == null)); }
        [TestCase(Visit)] [TestCase("relational.helped_person")]
        public void B_TargetToActorOnlyAndExactlyOnce(string type)
        {
            var world = new SocialWorldState(); var service = new SocialCommitService(world, new ConfiguredRelationshipInterpreter(Rules()));
            Assert.AreEqual(SocialCommitResult.Success, service.Commit(Fact(type: type))); Assert.AreEqual(8, world.Relationships.GetOrNeutral("wang", "zhang").Affinity);
            Assert.AreEqual(0, world.Relationships.GetOrNeutral("zhang", "wang").Affinity); Assert.AreEqual(SocialCommitResult.Duplicate, service.Commit(Fact(type: type))); Assert.AreEqual(8, world.Relationships.GetOrNeutral("wang", "zhang").Affinity);
        }
        [Test] public void B_ConfiguredMagnitudeAndNegativeNoCurve() { var w = new SocialWorldState(); var s = new SocialCommitService(w, new ConfiguredRelationshipInterpreter(Rules(6, polarity: "Negative"))); s.Commit(Fact()); s.Commit(Fact("activity:second")); Assert.AreEqual(-12, w.Relationships.GetOrNeutral("wang", "zhang").Affinity); }
        [TestCase(0)] [TestCase(-1)] [TestCase(float.NaN)] [TestCase(float.PositiveInfinity)]
        public void B_InvalidCalibration(float value) => Assert.Throws<ArgumentException>(() => Rules(value));
        [Test] public void B_MissingCalibration() => Assert.Throws<ArgumentException>(() => new SocialRuleCatalog(new[] { Visit }, new RelationshipInterpretationRule[0], new KeyValuePair<string,float>[0]));
        [Test] public void B_DuplicateCalibration() => Assert.Throws<ArgumentException>(() => new SocialRuleCatalog(new[] { Visit }, new RelationshipInterpretationRule[0], new[] { new KeyValuePair<string,float>("AffinityDeltaWeak",4), new KeyValuePair<string,float>("AffinityDeltaWeak",4) }));
        [Test] public void B_InvalidRole() => Assert.Throws<ArgumentException>(() => Rules(source: "Unknown"));
        [Test] public void B_MultipleRulesOppositePairs() { var rules = new SocialRuleCatalog(new[] { Visit }, new[] { new RelationshipInterpretationRule("one",Visit,"Target","Actor","Affinity","Positive","Weak"),new RelationshipInterpretationRule("two",Visit,"Actor","Target","Affinity","Positive","Strong") }, new[] {new KeyValuePair<string,float>("AffinityDeltaWeak",4),new KeyValuePair<string,float>("AffinityDeltaMedium",8),new KeyValuePair<string,float>("AffinityDeltaStrong",12)}); var w=new SocialWorldState(); Assert.AreEqual(SocialCommitResult.Success,new SocialCommitService(w,new ConfiguredRelationshipInterpreter(rules)).Commit(Fact())); Assert.AreEqual(4,w.Relationships.GetOrNeutral("wang","zhang").Affinity); Assert.AreEqual(12,w.Relationships.GetOrNeutral("zhang","wang").Affinity); }
        [Test] public void C_EnqueueDuplicateConflictImmutableStableOrdering()
        { var w=new SocialFeedbackWorldState();Assert.AreEqual(FeedbackEnqueueResult.Success,w.Enqueue(Fact("activity:z")));Assert.AreEqual(FeedbackEnqueueResult.Duplicate,w.Enqueue(Fact("activity:z")));Assert.AreEqual(FeedbackEnqueueResult.Conflict,w.Enqueue(Fact("activity:z","relational.helped_person")));w.Enqueue(Fact("activity:a"));CollectionAssert.AreEqual(new[]{"activity:a","activity:z"},w.Entries.Select(e=>e.Fact.SourceOccurrenceId));Assert.AreEqual(2,w.Entries.Count);Assert.IsTrue(typeof(SocialFeedbackEntry).GetProperties().All(p=>p.SetMethod==null)); }
        [TestCase(SocialCommitResult.Success)] [TestCase(SocialCommitResult.Duplicate)]
        public void C_DeliveryAcknowledges(SocialCommitResult result) {var w=new SocialFeedbackWorldState();w.Enqueue(Fact());var trace=w.Drain(_=>result);Assert.IsTrue(trace.Settled);Assert.AreEqual(result,trace.Deliveries[0].Result);Assert.IsEmpty(w.Entries);}
        [Test] public void C_CommitInProgressRemainsPendingThenSettles() {var w=new SocialFeedbackWorldState();w.Enqueue(Fact());Assert.IsFalse(w.Drain(_=>SocialCommitResult.CommitInProgress).Settled);Assert.AreEqual(FeedbackStatus.Pending,w.Entries[0].Status);Assert.IsTrue(w.Drain(_=>SocialCommitResult.Success).Settled);}
        [TestCase(SocialCommitResult.SourceOccurrenceConflict)] [TestCase(SocialCommitResult.InvalidFact)] [TestCase(SocialCommitResult.InvalidMutation)] [TestCase(SocialCommitResult.DuplicatePairMutation)] [TestCase(SocialCommitResult.InterpreterFailure)]
        public void C_FaultedNeverAutomaticallyRetries(SocialCommitResult result) {var w=new SocialFeedbackWorldState();w.Enqueue(Fact());var trace=w.Drain(_=>result);Assert.IsFalse(trace.Settled);Assert.AreEqual(result.ToString(),w.Entries[0].FaultCode);int calls=0;w.Drain(_=>{calls++;return SocialCommitResult.Success;});Assert.AreEqual(0,calls);}
        [Test] public void C_DeliveryExceptionContained() {var w=new SocialFeedbackWorldState();w.Enqueue(Fact());Assert.DoesNotThrow(()=>w.Drain(_=>throw new Exception("test")));Assert.AreEqual(FeedbackStatus.Faulted,w.Entries[0].Status);}
        [Test] public void C_InvalidPayloadZeroMutation() {var w=new SocialFeedbackWorldState();Assert.AreEqual(FeedbackEnqueueResult.InvalidPayload,w.Enqueue(Fact("not_activity")));Assert.IsEmpty(w.Entries);}
        [Test] public void D_RoundTripReplacementAndDtoIsolation() {var w=new SocialFeedbackWorldState();w.Enqueue(Fact());w.Drain(_=>SocialCommitResult.InvalidFact);var save=SocialFeedbackWorldPersistence.Build(w);var other=new SocialFeedbackWorldState();other.Enqueue(Fact("activity:old"));Assert.IsTrue(SocialFeedbackWorldPersistence.Restore(other,save));Assert.AreEqual(1,other.Entries.Count);Assert.AreEqual(FeedbackStatus.Faulted,other.Entries[0].Status);save.Outbox[0].ActorPersonId="changed";Assert.AreEqual("zhang",other.Entries[0].Fact.ActorPersonId);Assert.AreEqual("zhang",w.Entries[0].Fact.ActorPersonId);}
        [Test] public void D_NullReplacementEmpty() {var w=new SocialFeedbackWorldState();w.Enqueue(Fact());Assert.IsTrue(SocialFeedbackWorldPersistence.Restore(w,null));Assert.IsEmpty(w.Entries);}
        [TestCase("Status")] [TestCase("Day")] [TestCase("Target")]
        public void D_InvalidRestorePreservesOld(string field) {var w=new SocialFeedbackWorldState();w.Enqueue(Fact());var save=SocialFeedbackWorldPersistence.Build(w);if(field=="Status")save.Outbox[0].Status="Delivered";if(field=="Day")save.Outbox[0].Day=99;if(field=="Target")save.Outbox[0].TargetPersonId="zhang";Assert.IsFalse(SocialFeedbackWorldPersistence.Restore(w,save));Assert.AreEqual("wang",w.Entries[0].Fact.TargetPersonId);Assert.AreEqual(2,w.Entries[0].Fact.Day);}
        [Test] public void D_DuplicateOccurrenceRejected() {var w=new SocialFeedbackWorldState();w.Enqueue(Fact());var save=SocialFeedbackWorldPersistence.Build(w);save.Outbox=new[]{save.Outbox[0],save.Outbox[0]};Assert.IsFalse(SocialFeedbackWorldPersistence.Restore(w,save));Assert.AreEqual(1,w.Entries.Count);}
        [Test] public void D_RestoreDoesNotDrain() {var w=new SocialFeedbackWorldState();w.Enqueue(Fact());var other=new SocialFeedbackWorldState();Assert.IsTrue(SocialFeedbackWorldPersistence.Restore(other,SocialFeedbackWorldPersistence.Build(w)));Assert.AreEqual(FeedbackStatus.Pending,other.Entries[0].Status);}
        [Test] public void B_StaticConfigRuntimeValidation() {Assert.Throws<ArgumentException>(()=>SocialConfigDatabase.ValidateContent(new[]{new RelationalFactTypeConfigRow{FactType=Visit}},new RelationshipInterpretationRuleConfigRow[0],new[]{new SocialCalibrationConfigRow{CalibrationKey="AffinityDeltaWeak",Value=4}}));}
        [Test] public void G_PureDependencyDirections()
        {
            var feedback=typeof(SocialFeedbackWorldState).Assembly.GetReferencedAssemblies().Select(a=>a.Name).ToArray();
            CollectionAssert.Contains(feedback,"Social.Runtime");CollectionAssert.Contains(feedback,"AutonomousActivity.Runtime");
            foreach(var n in feedback)Assert.IsFalse(n.Contains("UnityEngine")||n.Contains("Village")||n.Contains("Event")||n.Contains("Person"));
            Assert.IsFalse(typeof(SocialWorldState).Assembly.GetReferencedAssemblies().Any(a=>a.Name.Contains("AutonomousActivity")));
            Assert.IsFalse(typeof(ActivityWorldState).Assembly.GetReferencedAssemblies().Any(a=>a.Name.Contains("Social")));
        }
    }
}
