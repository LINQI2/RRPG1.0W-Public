using System;
using System.Linq;
using NUnit.Framework;
using RRPG.AutonomousActivity;

namespace RRPG.Tests.SocialFeedback
{
    public partial class SocialFeedbackIntegrationTests
    {
        private object PrepareScheduledVisit(out VillageTime clock)
        {
            var player=Player();AddPerson(player,"zhang","prototype.zhang_san");AddPerson(player,"wang","prototype.wang_wu","HeavyInjury");
            Call("VillageRuntimeBridge","SetSimulationPersonSource",player);
            Call("VillageRuntimeBridge","ConfigureActivitySchedule",Activator.CreateInstance(TypeOf("IntradayActivitySchedule"),new object[]{new int[0],15,30,null}));
            clock=(VillageTime)Get("VillageRuntimeBridge","Time");AdvanceScheduled(clock,1400);
            Assert.AreEqual(ActivityResult.Success,Call("ActivityRuntimeBridge","OpenOpportunity",new ActivityDailyWindow(new ActivityDayStamp(1,1,1),
                new[]{"zhang"},new[]{"zhang","wang"},"visit:op",1400)));
            Assert.AreEqual(ActivityResult.Success,Call("ActivityRuntimeBridge","TryCommitScheduled","cross:visit",
                new ActivityCandidate("visit","VisitSick","test","zhang","wang","Home"),1400L,1415L,1505L));
            return player;
        }
        private static void AdvanceScheduled(VillageTime clock,long minute)
        {
            var result=clock.AdvanceTo(new WorldInstant(minute));
            while(result.Outcome==WorldAdvanceOutcome.Yielded) result=clock.ResumeAdvance(clock.AdvanceSession.RequestId);
            Assert.AreEqual(WorldAdvanceOutcome.Completed,result.Outcome,result.Diagnostic);
        }
        [Test] public void Intraday_ActualCompletionDate_SaveLoad_ExactlyOnceFeedback()
        {
            var player=PrepareScheduledVisit(out var clock);AdvanceScheduled(clock,1420);
            Assert.IsEmpty(SocialSave().RelationalFacts);Assert.AreEqual(0,Affinity("wang","zhang"));
            Assert.IsTrue((bool)Call("SaveSystem","Save",player));var loaded=Call("SaveSystem","Load");Assert.IsNotNull(loaded);
            Call("VillageRuntimeBridge","SetSimulationPersonSource",loaded);AdvanceScheduled(clock,1505);
            Assert.AreEqual(8,Affinity("wang","zhang"));Assert.AreEqual(0,Affinity("zhang","wang"));
            Assert.AreEqual(1,SocialSave().RelationalFacts.Length);Assert.AreEqual(2,SocialSave().RelationalFacts[0].Day);
            var args=new object[]{"cross:visit",1505L,new ActivityDayStamp(1,1,2),null};
            Assert.AreEqual(ActivityResult.Duplicate,TypeOf("AutonomousSocialFeedbackRuntimeBridge").GetMethod("TryCompleteAt",Flags).Invoke(null,args));
            Assert.AreEqual(8,Affinity("wang","zhang"));Assert.IsEmpty(FeedbackSave().Outbox);
        }
        [Test] public void Intraday_CompletionCannotForgeFutureClock_NoPreparationOrFact()
        {
            PrepareScheduledVisit(out var clock);AdvanceScheduled(clock,1415);
            var args=new object[]{"cross:visit",1505L,new ActivityDayStamp(1,1,2),null};
            Assert.AreEqual(ActivityResult.IllegalTransition,TypeOf("AutonomousSocialFeedbackRuntimeBridge").GetMethod("TryCompleteAt",Flags).Invoke(null,args));
            Assert.AreEqual(ActivityLifecycleState.Executing,((ActivityInstance)Call("ActivityRuntimeBridge","QueryActivityInstance","cross:visit")).State);
            Assert.IsEmpty(SocialSave().RelationalFacts);Assert.IsEmpty(FeedbackSave().Outbox);
        }
        [Test] public void Intraday_CompletedDeliveryFaultRemainsCompleted_StopsNextOpportunity()
        {
            PrepareScheduledVisit(out var clock);
            Call("SocialRuntimeBridge","CommitRelationalFact",SocialFeedbackDomainTests.Fact("activity:cross:visit","relational.helped_person"));
            AdvanceScheduled(clock,1505);
            Assert.AreEqual(ActivityLifecycleState.Completed,((ActivityInstance)Call("ActivityRuntimeBridge","QueryActivityInstance","cross:visit")).State);
            Assert.AreEqual("Faulted",FeedbackSave().Outbox.Single().Status);var before=Window;
            AdvanceScheduled(clock,1535);Assert.AreSame(before,Window);
            Assert.AreEqual("SocialFeedbackUnsettled",Get("VillageRuntimeBridge","IntradayActivityDiagnostic"));
        }
    }
}
