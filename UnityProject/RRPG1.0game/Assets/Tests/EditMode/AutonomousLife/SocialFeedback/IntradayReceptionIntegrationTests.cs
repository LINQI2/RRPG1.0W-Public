using System;
using System.Linq;
using NUnit.Framework;
using RRPG.AutonomousActivity;

namespace RRPG.Tests.SocialFeedback
{
    public partial class SocialFeedbackIntegrationTests
    {
        private object PrepareReception(out VillageTime clock)
        {
            var player=Player();AddPerson(player,"zhang","prototype.zhang_san");AddPerson(player,"li","prototype.li_si");AddPerson(player,"wang","prototype.wang_wu");
            Call("VillageRuntimeBridge","SetSimulationPersonSource",player);
            Call("VillageRuntimeBridge","ConfigureActivitySchedule",Activator.CreateInstance(TypeOf("IntradayActivitySchedule"),new object[]{new int[0],15,30,null}));
            clock=(VillageTime)Get("VillageRuntimeBridge","Time");AdvanceScheduled(clock,420);
            Assert.AreEqual(ActivityResult.Success,Call("ActivityRuntimeBridge","OpenOpportunity",new ActivityDailyWindow(new ActivityDayStamp(1,1,1),
                new[]{"zhang","li","wang"},new[]{"zhang","li","wang"},"reception:420",420)));
            return player;
        }
        private void CommitReception(string id,string actor="zhang") => Assert.AreEqual(ActivityResult.Success,
            Call("ActivityRuntimeBridge","TryCommitScheduledAtPlace",id,new ActivityCandidate(id,"VisitSick","test",actor,"wang","Home",null,
                ActivityTargetParticipation.ReceptionAtArrival),420L,435L,525L,"Home:wang"));
        [Test] public void Reception_RestTarget_TwoVisitors_PersonalEarlyDeparture_ExactlyOnceAcrossSaveLoad()
        {
            var player=PrepareReception(out var clock);CommitReception("visit:zhang");CommitReception("visit:li","li");
            Assert.AreEqual(ActivityResult.Success,Call("ActivityRuntimeBridge","TryCommitScheduledAtPlace","rest:wang",
                new ActivityCandidate("rest","Rest","test","wang",null,"Home"),420L,435L,495L,"Home:wang"));
            AdvanceScheduled(clock,465);Assert.IsEmpty(SocialSave().RelationalFacts);
            Assert.AreEqual(ActivityResult.Success,Call("VillageRuntimeBridge","TryLeaveIntradayActivity","zhang"));
            Assert.AreEqual(8,Affinity("wang","zhang"));Assert.AreEqual("visit:li",Call("ActivityRuntimeBridge","QueryClaim","li"));
            Assert.AreEqual("rest:wang",Call("ActivityRuntimeBridge","QueryClaim","wang"));
            Assert.IsTrue((bool)Call("SaveSystem","Save",player));var loaded=Call("SaveSystem","Load");Assert.IsNotNull(loaded);
            Call("VillageRuntimeBridge","SetSimulationPersonSource",loaded);AdvanceScheduled(clock,525);
            Assert.AreEqual(8,Affinity("wang","li"));Assert.AreEqual(8,Affinity("wang","zhang"));
            Assert.AreEqual(2,SocialSave().RelationalFacts.Length);Assert.AreEqual(2,SocialSave().RelationalFacts.Select(f=>f.SourceOccurrenceId).Distinct().Count());
            Assert.IsEmpty(FeedbackSave().Outbox);
        }
        [Test] public void Reception_TargetLeavesBeforeArrival_MissedWithoutFact_ActualPlaceAndCooldownPersist()
        {
            var player=PrepareReception(out var clock);CommitReception("missed");
            Call("VillageRuntimeBridge","SetExpeditionPersonIds",new object[]{new[]{"wang"}});AdvanceScheduled(clock,435);
            Assert.AreEqual(ActivityLifecycleState.Interrupted,((ActivityInstance)Call("ActivityRuntimeBridge","QueryActivityInstance","missed")).State);
            Assert.IsEmpty(SocialSave().RelationalFacts);Assert.IsNull(Call("ActivityRuntimeBridge","QueryClaim","zhang"));
            var state=(PersonActivityState)Call("ActivityRuntimeBridge","QueryPersonActivityState","zhang");
            Assert.AreEqual("Home:wang",state.PresencePlaceId);Assert.AreEqual("wang",state.LastMissedTargetPersonId);Assert.IsNull(state.LastMajorParticipationDay);
            Call("VillageRuntimeBridge","ClearExpeditionPersonIds");
            Assert.IsTrue((bool)Call("SaveSystem","Save",player));Assert.IsNotNull(Call("SaveSystem","Load"));
            Assert.AreEqual(435,((PersonActivityState)Call("ActivityRuntimeBridge","QueryPersonActivityState","zhang")).LastMissedMinute);
        }
        [Test] public void Reception_TooEarlyDeparture_DoesNotCreateSuccessfulResult()
        {
            PrepareReception(out var clock);CommitReception("early");AdvanceScheduled(clock,450);
            Assert.AreEqual(ActivityResult.Success,Call("VillageRuntimeBridge","TryLeaveIntradayActivity","zhang"));
            Assert.IsEmpty(SocialSave().RelationalFacts);Assert.IsNull(((PersonActivityState)Call("ActivityRuntimeBridge","QueryPersonActivityState","zhang")).LastMajorParticipationDay);
        }
        [Test] public void PresenceQuery_DoesNotCreateSaveRows_OffsiteTargetNotClaimed()
        {
            PrepareReception(out _);var before=((ActivityWorldSaveData)Call("ActivityRuntimeBridge","BuildSaveData")).PersonActivityStates.Length;
            Call("VillageRuntimeBridge","QueryIntradayPresence","wang");Assert.AreEqual(before,((ActivityWorldSaveData)Call("ActivityRuntimeBridge","BuildSaveData")).PersonActivityStates.Length);
            CommitReception("visit");Assert.IsNull(Call("ActivityRuntimeBridge","QueryClaim","wang"));
        }
        [TestCase(450,false)] [TestCase(465,true)]
        public void Reception_TargetLeavesDuringVisit_UsesActualDepartureMinute(long minute,bool qualified)
        {
            PrepareReception(out var clock);CommitReception("departure");AdvanceScheduled(clock,minute);
            Call("VillageRuntimeBridge","SetUnavailablePersonIds",new object[]{new[]{"wang"}});
            var instance=(ActivityInstance)Call("ActivityRuntimeBridge","QueryActivityInstance","departure");
            Assert.AreEqual(qualified ? ActivityLifecycleState.Completed : ActivityLifecycleState.Interrupted,instance.State);
            Assert.AreEqual(qualified ? 1 : 0,SocialSave().RelationalFacts.Length);
            Assert.AreEqual(qualified ? 8 : 0,Affinity("wang","zhang"));Assert.IsNull(Call("ActivityRuntimeBridge","QueryClaim","zhang"));
        }
        [Test] public void Reception_MissingSourceDuringTransit_PreservesCursorAndClaim_RebindResumesOriginalRequest()
        {
            var player=PrepareReception(out var clock);CommitReception("source:visit");
            Call("VillageRuntimeBridge","SetSimulationPersonSource",new object[]{null});
            var result=clock.AdvanceTo(new WorldInstant(435));Assert.AreEqual(WorldAdvanceOutcome.RetryableBoundaryFailure,result.Outcome);
            Assert.AreEqual(420,clock.CurrentInstant.Minute);Assert.AreEqual("source:visit",Call("ActivityRuntimeBridge","QueryClaim","zhang"));
            string request=clock.AdvanceSession.RequestId;Call("VillageRuntimeBridge","SetSimulationPersonSource",player);
            Assert.AreEqual(WorldAdvanceOutcome.Completed,clock.ResumeAdvance(request).Outcome);
            Assert.AreEqual(ActivityLifecycleState.Executing,((ActivityInstance)Call("ActivityRuntimeBridge","QueryActivityInstance","source:visit")).State);
        }
    }
}
