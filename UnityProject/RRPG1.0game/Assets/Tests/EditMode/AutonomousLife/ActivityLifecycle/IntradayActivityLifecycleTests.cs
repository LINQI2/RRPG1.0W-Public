using System;
using NUnit.Framework;
using RRPG.AutonomousActivity;

namespace RRPG.Tests.AutonomousLife
{
    public class IntradayActivityLifecycleTests
    {
        private ActivityWorldState world;
        private ActivityLifecycleService service;
        private static readonly ActivityDayStamp Day=new ActivityDayStamp(1,1,1);
        private static ActivityCandidate Candidate(string actor="A",string target=null)
            => new ActivityCandidate("test:"+actor,"Talk","test",actor,target,"Home");
        private void Open(long minute=420,string identity="op:420")
            => Assert.AreEqual(ActivityResult.Success,service.OpenOpportunity(new ActivityDailyWindow(Day,new[]{"A","B"},new[]{"A","B"},identity,minute)));
        private void Commit(string id="one",ActivityCandidate candidate=null,long now=420,long arrival=435,long completion=495)
            => Assert.AreEqual(ActivityResult.Success,service.TryCommitScheduled(id,candidate ?? Candidate(),now,arrival,completion));
        [SetUp] public void Setup(){world=new ActivityWorldState();service=new ActivityLifecycleService(world);Open();}
        [Test] public void ScheduledCommit_IsNotExecutionOrCompletion_ClockGuarded()
        {
            Commit();Assert.AreEqual(ActivityLifecycleState.Committed,world.QueryActivityInstance("one").State);
            Assert.AreEqual(ActivityResult.IllegalTransition,service.TryBeginExecution("one"));
            Assert.AreEqual(ActivityResult.IllegalTransition,service.TryBeginExecutionAt("one",434));
            Assert.AreEqual(ActivityResult.Success,service.TryBeginExecutionAt("one",435));
            Assert.AreEqual(ActivityResult.IllegalTransition,service.TryComplete("one",out _));
            Assert.AreEqual(ActivityResult.IllegalTransition,service.TryCompleteAt("one",494,Day,out _));
            Assert.AreEqual(0,world.PersonStateCount);
            Assert.AreEqual(ActivityResult.Success,service.TryCompleteAt("one",495,Day,out var snapshot));
            Assert.AreEqual("one",snapshot.ActivityInstanceId);Assert.IsNull(world.QueryClaim("A"));
            Assert.AreEqual(495,world.QueryPersonActivityState("A").LastCompletionMinute);
            Assert.AreEqual(ActivityResult.Duplicate,service.TryCompleteAt("one",495,Day,out _));
        }
        [Test] public void OpportunityRefresh_PreservesActiveInstancesAndClaims_IncludingAcrossMidnight()
        {
            Commit(completion:1600);Open(1200,"op:1200");
            Assert.AreEqual("one",world.QueryClaim("A"));Assert.IsNotNull(world.QueryActivityInstance("one"));
            Assert.AreEqual(ActivityResult.Success,service.OpenOpportunity(new ActivityDailyWindow(new ActivityDayStamp(1,1,2),
                new[]{"B"},new[]{"B"},"next",1441)));Assert.AreEqual("one",world.QueryClaim("A"));
        }
        [Test] public void SameOpportunity_DuplicateOrConflict_NoRefresh()
        {
            var before=world.QueryCurrentWindow();
            Assert.AreEqual(ActivityResult.Duplicate,service.OpenOpportunity(new ActivityDailyWindow(Day,new[]{"B","A","A"},new[]{"B","A"},"op:420",420)));
            Assert.AreEqual(ActivityResult.WindowConflict,service.OpenOpportunity(new ActivityDailyWindow(Day,new[]{"A"},new[]{"A"},"op:420",420)));
            Assert.AreSame(before,world.QueryCurrentWindow());
        }
        [Test] public void ClaimConflict_DoesNotCreateOrPartiallyClaim()
        {
            Commit();Assert.AreEqual(ActivityResult.ClaimConflict,service.TryCommitScheduled("two",Candidate("B","A"),420,435,495));
            Assert.AreEqual(1,world.InstanceCount);Assert.IsNull(world.QueryClaim("B"));
        }
        [Test] public void WrongCommitTime_FailsBeforeMutation()
        {
            Assert.AreEqual(ActivityResult.InvalidWindow,service.TryCommitScheduled("bad",Candidate(),421,435,495));Assert.AreEqual(0,world.InstanceCount);
        }
        [Test] public void CompletedPerson_CanTakeDifferentLaterOpportunity_NotSameRound()
        {
            Commit();service.TryBeginExecutionAt("one",435);service.TryCompleteAt("one",495,Day,out _);
            Open(525,"op:525");Commit("two",now:525,arrival:540,completion:600);Assert.AreEqual("two",world.QueryClaim("A"));
        }
        [Test] public void ActualCompletionDayAndParticipantRecency_AreDistinctFromCommittedDay()
        {
            Commit(candidate:Candidate("A","B"),completion:1600);service.TryBeginExecutionAt("one",435);
            var next=new ActivityDayStamp(1,1,2);service.TryCompleteAt("one",1600,next,out var snapshot);
            Assert.AreEqual(next,snapshot.DayStamp);Assert.AreEqual(next,world.QueryPersonActivityState("B").LastMajorParticipationDay);
            Assert.IsNull(world.QueryPersonActivityState("B").LastInitiatedActivityType);
            Assert.AreEqual("Talk",world.QueryPersonActivityState("A").LastInitiatedActivityType);
        }
        [Test] public void ReplacementRestore_RebuildsClaimsWithoutWindowOrExecutionReplay()
        {
            Commit();service.TryBeginExecutionAt("one",435);var data=ActivityWorldPersistence.Build(world);
            var restored=new ActivityWorldState();Assert.AreEqual(0,ActivityWorldPersistence.Restore(restored,data));
            Assert.IsNull(restored.QueryCurrentWindow());Assert.AreEqual("one",restored.QueryClaim("A"));
            Assert.AreEqual(ActivityLifecycleState.Executing,restored.QueryActivityInstance("one").State);
            Assert.AreEqual(0,restored.PersonStateCount);data.ActiveInstances[0].ActivityType="mutated";
            Assert.AreEqual("Talk",restored.QueryActivityInstance("one").Candidate.ActivityType);
        }
        [Test] public void InvalidRestore_PreservesOldWorldAndClaim()
        {
            Commit();var data=ActivityWorldPersistence.Build(world);data.ActiveInstances[0].CompletionMinute=434;
            Assert.AreEqual(-1,ActivityWorldPersistence.Restore(world,data));Assert.AreEqual("one",world.QueryClaim("A"));Assert.AreEqual(1,world.InstanceCount);
        }
        [Test] public void DuplicateClaimRestore_FailsWholeReplacement()
        {
            Commit();var data=ActivityWorldPersistence.Build(world);var first=data.ActiveInstances[0];
            data.ActiveInstances=new[]{first,new ActiveActivitySaveData {ActivityInstanceId="two",OpportunityId=first.OpportunityId,
                CandidateKey="second",ActivityType="Rest",ActorPersonId="A",Year=1,Month=1,Day=1,State=first.State,
                CommittedMinute=420,ArrivalMinute=435,CompletionMinute=500}};
            Assert.IsFalse(ActivityWorldPersistence.IsValid(data));Assert.AreEqual(-1,ActivityWorldPersistence.Restore(world,data));
            Assert.AreEqual("one",world.QueryClaim("A"));
        }
        [Test] public void Interrupt_ReleasesClaimWithoutConsumptionOrSuccess()
        {
            Commit();Assert.AreEqual(ActivityResult.Success,service.TryInterrupt("one"));Assert.IsNull(world.QueryClaim("A"));
            Assert.AreEqual(0,world.PersonStateCount);Assert.IsEmpty(ActivityWorldPersistence.Build(world).ActiveInstances);
            Assert.AreEqual(ActivityResult.IllegalTransition,service.TryBeginExecutionAt("one",435));
        }
        [Test] public void LegacyNullRestore_IsEmptyReplacement_NoScheduledDataLostThroughLegacyOpen()
        {
            Commit();Assert.AreEqual(ActivityResult.WindowConflict,service.OpenDailyWindow(new ActivityDailyWindow(new ActivityDayStamp(1,1,2),new[]{"A"},new[]{"A"})));
            Assert.AreEqual(0,ActivityWorldPersistence.Restore(world,null));Assert.AreEqual(0,world.InstanceCount);Assert.IsNull(world.QueryClaim("A"));
        }
    }
}
