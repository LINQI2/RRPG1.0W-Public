using NUnit.Framework;
using RRPG.AutonomousActivity;

namespace RRPG.Tests.AutonomousLife
{
    public class IntradayReceptionTests
    {
        private ActivityWorldState world;
        private ActivityLifecycleService service;
        private readonly ActivityDayStamp day=new ActivityDayStamp(1,1,1);
        private ActivityCandidate Visit(string actor="A") => new ActivityCandidate("visit:"+actor,"VisitSick","test",actor,"T","Home",new[]{"T"},ActivityTargetParticipation.ReceptionAtArrival);
        [SetUp] public void Setup()
        {
            world=new ActivityWorldState();service=new ActivityLifecycleService(world);
            service.OpenOpportunity(new ActivityDailyWindow(day,new[]{"A","B","T"},new[]{"A","B","T"},"op",420));
        }
        private void Commit(string id,string actor="A") => Assert.AreEqual(ActivityResult.Success,service.TryCommitScheduledAtPlace(id,Visit(actor),420,435,525,"Home:T"));
        [Test] public void ReceptionDoesNotPreclaimTarget_TwoVisitorsAndTargetRestCoexist()
        {
            Commit("a");Commit("b","B");Assert.IsNull(world.QueryClaim("T"));
            Assert.AreEqual(ActivityResult.Success,service.TryCommitScheduledAtPlace("rest",new ActivityCandidate("r","Rest","test","T",null,"Home"),420,435,495,"Home:T"));
            Assert.AreEqual("rest",world.QueryClaim("T"));Assert.AreEqual("a",world.QueryClaim("A"));
        }
        [Test] public void PersonalEarlyResult_ThresholdAndOnce_OtherVisitorClaimRetained()
        {
            Commit("a");Commit("b","B");service.TryBeginExecutionAt("a",435);service.TryBeginExecutionAt("b",435);
            Assert.AreEqual(ActivityResult.IllegalTransition,service.TryCompleteMemberResult("a",464,day,30,out _));
            Assert.AreEqual(ActivityResult.Success,service.TryCompleteMemberResult("a",465,day,30,out var result));
            Assert.AreEqual("Home:T",result.PlaceId);Assert.AreEqual("activity:a",world.QueryActivityInstance("a").MemberResultId);
            Assert.AreEqual(ActivityResult.Duplicate,service.TryCompleteMemberResult("a",465,day,30,out _));
            Assert.IsNull(world.QueryClaim("A"));Assert.AreEqual("b",world.QueryClaim("B"));
            Assert.IsNull(world.QueryPersonActivityState("T").LastInitiatedActivityType);
        }
        [Test] public void PresenceAndMissedVisit_RoundTrip_ReadDoesNotCreate_InvalidReplacementPreserves()
        {
            Assert.IsNull(world.QueryPersonActivityState("unknown").PresencePlaceId);Assert.AreEqual(0,world.PersonStateCount);
            service.RecordActualPresence("A","Home:T",435);service.RecordMissedVisit("A","VisitSick","T",435);
            var saved=ActivityWorldPersistence.Build(world);var restored=new ActivityWorldState();
            Assert.AreEqual(0,ActivityWorldPersistence.Restore(restored,saved));
            Assert.AreEqual("Home:T",restored.QueryPersonActivityState("A").PresencePlaceId);
            Assert.AreEqual(435,restored.QueryPersonActivityState("A").LastMissedMinute);
            Assert.IsNull(restored.QueryPersonActivityState("A").LastMajorParticipationDay);
            saved.PersonActivityStates[0].LastMissedTargetPersonId="A";
            Assert.AreEqual(-1,ActivityWorldPersistence.Restore(restored,saved));Assert.AreEqual("T",restored.QueryPersonActivityState("A").LastMissedTargetPersonId);
        }
        [Test] public void ActiveReceptionSave_StableMemberIdentity_ClaimRolesPreserved()
        {
            Commit("a");service.TryBeginExecutionAt("a",435);var copy=new ActivityWorldState();
            Assert.AreEqual(0,ActivityWorldPersistence.Restore(copy,ActivityWorldPersistence.Build(world)));
            Assert.AreEqual("activity:a",copy.QueryActivityInstance("a").MemberResultId);Assert.IsNull(copy.QueryClaim("T"));
            Assert.AreEqual("Home:T",copy.QueryActivityInstance("a").DestinationPlaceId);
        }
        [Test] public void LegacyImmediateReceptionCannotBypassArrival()
        { Assert.AreEqual(ActivityResult.InvalidCandidate,service.TryCommitActivity("bad",Visit()));Assert.AreEqual(0,world.InstanceCount); }
    }
}
