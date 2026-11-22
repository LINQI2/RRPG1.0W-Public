using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using RRPG.AutonomousActivity;
namespace RRPG.Tests.ActivityLifecycle
{
    public class ActivityLifecycleTests
    {
        private ActivityWorldState world;
        private ActivityLifecycleService service;
        internal static ActivityDayStamp Day(int day = 2) => new ActivityDayStamp(1, 1, day);
        internal static ActivityCandidate Candidate(string actor = "A", string target = null)
            => new ActivityCandidate("test_key", "test_talk", "test_provider", actor, target, "test_place",
                target == null ? null : new[] { target });
        [SetUp] public void Setup()
        {
            world = new ActivityWorldState(); service = new ActivityLifecycleService(world);
            service.OpenDailyWindow(new ActivityDailyWindow(Day(), new[] { "A", "B", "C" }, new[] { "A", "B", "C" }));
        }
        private void Commit(string id = "fixed_1", string actor = "A", string target = "B")
            => Assert.AreEqual(ActivityResult.Success, service.TryCommitActivity(id, Candidate(actor, target)));
        private ActivityCompletionSnapshot Complete(string id = "fixed_1")
        {
            Assert.AreEqual(ActivityResult.Success, service.TryBeginExecution(id));
            Assert.AreEqual(ActivityResult.Success, service.TryComplete(id, out var result)); return result;
        }
        [Test] public void A1_WindowOpen() { Assert.AreEqual(Day(), world.QueryCurrentWindow().DayStamp); }
        [Test] public void A2_IdenticalWindowNoOpPreservesClaims()
        {
            Commit(); var before = world.QueryCurrentWindow();
            Assert.AreEqual(ActivityResult.Duplicate, service.OpenDailyWindow(new ActivityDailyWindow(Day(), new[] { "C", "B", "A", "A" }, new[] { "C", "A", "B" })));
            Assert.AreSame(before, world.QueryCurrentWindow()); Assert.AreEqual("fixed_1", world.QueryClaim("B"));
        }
        [TestCase(true)] [TestCase(false)]
        public void A3_SameDayChangedSnapshotFailsClosed(bool changeActors)
        {
            Commit(); var before = world.QueryCurrentWindow();
            Assert.AreEqual(ActivityResult.WindowConflict, service.OpenDailyWindow(new ActivityDailyWindow(Day(),
                changeActors ? new[] { "A" } : new[] { "A", "B", "C" }, changeActors ? new[] { "A", "B", "C" } : new[] { "A" })));
            Assert.AreSame(before, world.QueryCurrentWindow()); Assert.AreEqual(2, world.Claims.Count);
        }
        [Test] public void A4_NewDayCancelsInflightWithoutSuccessfulEffects()
        {
            Commit(); service.TryBeginExecution("fixed_1");
            service.OpenDailyWindow(new ActivityDailyWindow(Day(3), new[] { "A" }, new[] { "A" }));
            Assert.AreEqual(0, world.Claims.Count); Assert.AreEqual(0, world.InstanceCount); Assert.AreEqual(0, world.PersonStateCount);
        }
        [Test] public void A5_WindowOrdinalDefensiveCopyAndReadonly()
        {
            var ids = new List<string> { "b", null, "A", " ", "", "A" };
            var window = new ActivityDailyWindow(Day(), ids, ids); ids.Clear();
            CollectionAssert.AreEqual(new[] { "A", "b" }, window.ActorEligiblePersonIds);
            Assert.Throws<NotSupportedException>(() => ((IList<string>)window.ActorEligiblePersonIds)[0] = "X");
            Assert.Throws<NotSupportedException>(() => ((IList<string>)window.ParticipantAvailablePersonIds).Add("X"));
        }
        [Test] public void A6_CandidateDefensiveReadonlyDistinctActorExcluded()
        {
            var ids = new List<string> { "A", "B", "B", null };
            var candidate = new ActivityCandidate("k", "t", "p", "A", exclusiveParticipants: ids); ids.Clear();
            CollectionAssert.AreEqual(new[] { "B" }, candidate.ExclusiveParticipantPersonIds);
            Assert.Throws<NotSupportedException>(() => ((IList<string>)candidate.ExclusiveParticipantPersonIds).Clear());
            Assert.AreEqual(0, world.PersonStateCount); Assert.AreEqual(0, world.Claims.Count);
        }
        [Test] public void A7_ActorAndParticipantSurfacesIndependent()
        {
            service.OpenDailyWindow(new ActivityDailyWindow(Day(3), new[] { "A" }, new[] { "B" }));
            Assert.AreEqual(ActivityResult.ActorIneligible, service.TryCommitActivity("b", Candidate("B")));
            Commit(target: "B"); Assert.AreEqual(2, world.Claims.Count);
        }
        [Test] public void A8_CommitAcquiresAllClaimsAndStableIdentity()
        {
            Commit(); Assert.AreEqual("fixed_1", world.QueryClaim("A")); Assert.AreEqual("fixed_1", world.QueryClaim("B"));
            Assert.AreEqual(ActivityLifecycleState.Committed, world.QueryActivityInstance("fixed_1").State);
        }
        [Test] public void A9_ClaimConflictCannotCreateInstance()
        {
            Commit(); Assert.AreEqual(ActivityResult.ClaimConflict, service.TryCommitActivity("fixed_2", Candidate("C", "B")));
            Assert.IsNull(world.QueryActivityInstance("fixed_2")); Assert.IsNull(world.QueryClaim("C"));
        }
        [Test] public void A10_UnavailableParticipantNoHalfClaim()
        {
            Assert.AreEqual(ActivityResult.ParticipantUnavailable, service.TryCommitActivity("bad", Candidate("A", "Z")));
            Assert.AreEqual(0, world.Claims.Count); Assert.AreEqual(0, world.InstanceCount);
        }
        [Test] public void A11_InstanceIdConflictAndNoGeneratedIdentity()
        {
            Commit(); Assert.AreEqual(ActivityResult.InstanceConflict, service.TryCommitActivity("fixed_1", Candidate("C")));
            Assert.AreEqual(ActivityResult.InvalidInstanceId, service.TryCommitActivity("", Candidate("C")));
            Assert.AreEqual(1, world.InstanceCount);
        }
        [Test] public void A12_FsmSnapshotDoesNotMutateBehindQuery()
        {
            Commit(); var before = world.QueryActivityInstance("fixed_1"); var result = Complete();
            Assert.AreEqual(ActivityLifecycleState.Committed, before.State);
            Assert.AreEqual(ActivityLifecycleState.Completed, world.QueryActivityInstance("fixed_1").State);
            Assert.AreEqual("fixed_1", result.ActivityInstanceId); Assert.AreEqual(Day(), result.DayStamp);
            Assert.AreEqual("B", result.TargetPersonId); Assert.AreEqual("test_place", result.PlaceId);
            Assert.Throws<NotSupportedException>(() => ((IList<string>)result.ParticipantPersonIds).Clear());
        }
        [Test] public void A13_CompleteRequiresExecutingAndTerminalCannotChange()
        {
            Commit(); Assert.AreEqual(ActivityResult.IllegalTransition, service.TryComplete("fixed_1", out var result)); Assert.IsNull(result);
            Complete(); Assert.AreEqual(ActivityResult.IllegalTransition, service.TryFail("fixed_1"));
            Assert.AreEqual(ActivityResult.IllegalTransition, service.TryInterrupt("fixed_1"));
            Assert.AreEqual(ActivityResult.IllegalTransition, service.TryBeginExecution("fixed_1"));
        }
        [TestCase(false, false)] [TestCase(false, true)] [TestCase(true, false)] [TestCase(true, true)]
        public void A14_A15_FailureOrInterruptReleasesWithoutSuccess(bool interrupt, bool executing)
        {
            Commit(); if (executing) service.TryBeginExecution("fixed_1");
            Assert.AreEqual(ActivityResult.Success, interrupt ? service.TryInterrupt("fixed_1") : service.TryFail("fixed_1"));
            Assert.AreEqual(0, world.Claims.Count); Assert.AreEqual(0, world.PersonStateCount);
            Assert.AreEqual(ActivityResult.IllegalTransition, service.TryComplete("fixed_1", out var result)); Assert.IsNull(result);
            Assert.AreEqual(ActivityResult.Success, service.TryCommitActivity("retry", Candidate("A", "B")));
        }
        [Test] public void A16_CompletionConsumesEveryParticipant()
        {
            Commit(); Complete(); Assert.AreEqual(0, world.Claims.Count);
            Assert.AreEqual(Day(), world.QueryPersonActivityState("A").LastMajorParticipationDay);
            Assert.AreEqual(Day(), world.QueryPersonActivityState("B").LastMajorParticipationDay);
            Assert.AreEqual(ActivityResult.AlreadyParticipated, service.TryCommitActivity("second", Candidate("C", "B")));
            Assert.IsNull(world.QueryClaim("C"));
        }
        [Test] public void A17_DuplicateCompleteNoOpNoSecondSnapshot()
        {
            Commit(); Complete(); var state = world.QueryPersonActivityState("A");
            Assert.AreEqual(ActivityResult.Duplicate, service.TryComplete("fixed_1", out var result));
            Assert.IsNull(result); Assert.AreSame(state, world.QueryPersonActivityState("A"));
        }
        [Test] public void A18_ActorInitiatedRecencyDoesNotOverwriteParticipantRecency()
        {
            Commit("old", "B", null); Complete("old");
            service.OpenDailyWindow(new ActivityDailyWindow(Day(3), new[] { "A" }, new[] { "B" }));
            Commit(); Complete();
            Assert.AreEqual(Day(3), world.QueryPersonActivityState("A").LastInitiatedActivityDay);
            Assert.AreEqual(Day(), world.QueryPersonActivityState("B").LastInitiatedActivityDay);
            Assert.AreEqual(Day(3), world.QueryPersonActivityState("B").LastMajorParticipationDay);
        }
        [TestCase("", "t", "A")] [TestCase("k", "", "A")] [TestCase("k", "t", "")]
        public void InvalidCandidateFailsBeforeMutation(string key, string type, string actor)
        {
            Assert.AreEqual(ActivityResult.InvalidCandidate, service.TryCommitActivity("bad", new ActivityCandidate(key, type, "p", actor)));
            Assert.AreEqual(0, world.Claims.Count); Assert.AreEqual(0, world.InstanceCount);
        }
        [Test] public void InvalidAndMissingWindowFailClosed()
        {
            var before = world.QueryCurrentWindow();
            Assert.AreEqual(ActivityResult.InvalidWindow, service.OpenDailyWindow(new ActivityDailyWindow(default, null, null)));
            Assert.AreSame(before, world.QueryCurrentWindow());
            ActivityWorldPersistence.Restore(world, null);
            Assert.AreEqual(ActivityResult.MissingWindow, service.TryCommitActivity("x", Candidate()));
            Assert.AreEqual(ActivityResult.MissingInstance, service.TryBeginExecution("x"));
        }
        [Test] public void TargetOmittedFromExclusiveStillClaimsAndConsumesTarget()
        {
            var c = new ActivityCandidate("k", "t", "p", "A", "B");
            Assert.AreEqual(ActivityResult.Success, service.TryCommitActivity("target", c));
            Assert.AreEqual("target", world.QueryClaim("B")); Complete("target");
            Assert.AreEqual(Day(), world.QueryPersonActivityState("B").LastMajorParticipationDay);
        }
    }
}
