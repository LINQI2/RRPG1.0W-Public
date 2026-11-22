using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using RRPG.AutonomousActivity;
using RRPG.AutonomousSelection;

namespace RRPG.Tests.AutonomousSelection
{
    public class IntradaySelectionTests
    {
        private sealed class ScheduledGateway : IActivityExecutionGateway
        {
            public int Commits;
            public ActivityResult Commit(string id,ActivityCandidate candidate){Commits++;return ActivityResult.Success;}
            public ActivityResult Begin(string id) => throw new Exception("Intraday must not begin immediately");
            public ActivityResult Complete(string id,out ActivityCompletionSnapshot snapshot) => throw new Exception("Intraday must not complete immediately");
            public ActivityResult Fail(string id) => throw new Exception("Unexpected fail");
        }
        private static DecisionSnapshot Snapshot(long minute,string id="op",bool reverse=false)
        {
            var persons=new[]{SelectionTestData.Person(),SelectionTestData.Person("B")};
            if(reverse) Array.Reverse(persons);
            return new DecisionSnapshot(new ActivityDailyWindow(SelectionTestData.Day(),persons.Select(p=>p.PersonId),
                persons.Select(p=>p.PersonId),id,minute),persons,null,null,SelectionTestData.Catalog());
        }
        [Test] public void NoMajor_OpportunityExactlyOnce_SurvivesReplacementSaveRestore()
        {
            var snapshot=Snapshot(1860);var state=new AutonomousDecisionWorldState();var gateway=new ScheduledGateway();
            var planner=new DecisionPlanner(new SelectionTestData.Provider((s,a)=>new ActivityCandidate[0]));
            var coordinator=new DecisionCoordinator(state);
            var result=coordinator.Run(snapshot.Day,true,true,snapshot.Window,()=>snapshot,planner,gateway);
            Assert.AreEqual(DecisionDailyResult.Resolved,result.Result);Assert.AreEqual(0,gateway.Commits);
            Assert.AreEqual(0,result.Executions.Count);Assert.IsTrue(result.Plan.Trace.All(t=>t.FinalResult=="NoMajor"));
            var restored=new AutonomousDecisionWorldState();Assert.IsTrue(AutonomousDecisionWorldPersistence.Restore(restored,AutonomousDecisionWorldPersistence.Build(state)));
            Assert.AreEqual(DecisionDailyResult.AlreadyResolved,new DecisionCoordinator(restored).Run(snapshot.Day,true,true,
                snapshot.Window,()=>throw new Exception("must not rebuild"),planner,gateway).Result);
        }
        [Test] public void DifferentOpportunitySameDay_IsNewDecision_ButNeverImmediateExecution()
        {
            var state=new AutonomousDecisionWorldState();var gateway=new ScheduledGateway();var coordinator=new DecisionCoordinator(state);
            foreach(long minute in new[]{1860L,2460L})
            {
                var snapshot=Snapshot(minute,"op:"+minute);
                var result=coordinator.Run(snapshot.Day,true,true,snapshot.Window,()=>snapshot,new DecisionPlanner(),gateway);
                Assert.AreEqual(DecisionDailyResult.Resolved,result.Result);
                Assert.IsTrue(result.Executions.All(e=>e.Stage=="Commit" && e.Completion==null && e.Result==ActivityResult.Success));
            }
            Assert.Greater(gateway.Commits,0);
        }
        [Test] public void SameInputReorderedSources_ProducesSameOpportunityOrderAndProposals()
        {
            var a=new DecisionPlanner().Plan(Snapshot(1860,"fixed"));
            var b=new DecisionPlanner().Plan(Snapshot(1860,"fixed",true));
            CollectionAssert.AreEqual(a.Trace.SelectMany(t=>t.Order).Select(o=>o.RandomIdentity),b.Trace.SelectMany(t=>t.Order).Select(o=>o.RandomIdentity));
            CollectionAssert.AreEqual(a.Winners.Select(p=>p.Identity),b.Winners.Select(p=>p.Identity));
        }
        [Test] public void InvalidOpportunityRestore_PreservesPreviousExactlyOnce()
        {
            var world=new AutonomousDecisionWorldState();
            Assert.IsTrue(AutonomousDecisionWorldPersistence.Restore(world,new AutonomousDecisionWorldSaveData{HasLastOpportunity=true,LastOpportunityMinute=1860,LastOpportunityId="op"}));
            Assert.IsFalse(AutonomousDecisionWorldPersistence.Restore(world,new AutonomousDecisionWorldSaveData{HasLastOpportunity=true,LastOpportunityMinute=-1,LastOpportunityId="bad"}));
            Assert.AreEqual(1860,world.LastOpportunityMinute);Assert.AreEqual("op",world.LastOpportunityId);
        }
    }
    public class IntradayActivitySaveIntegrationTests : SelectionIntegrationTestKit
    {
        private object player;
        private void Prepare(long minute=400)
        {
            player=Player();var person=AddCharacter(player,"A");
            Asm("CharacterInstance").GetField("CharacterId").SetValue(person,"char_001");
            Call(Village,"SetSimulationPersonSource",player);
            var schedule=Activator.CreateInstance(Asm("IntradayActivitySchedule"),new object[]{new int[0],15,30,null});
            Call(Village,"ConfigureActivitySchedule",schedule);Advance(minute);
            var window=new ActivityDailyWindow(new ActivityDayStamp(Time.Year,Time.Month,Time.Day),new[]{"A"},new[]{"A"},"test:"+minute,minute);
            Assert.AreEqual(ActivityResult.Success,Call(Host,"OpenOpportunity",window));
            Assert.AreEqual(ActivityResult.Success,Call(Host,"TryCommitScheduled","scheduled",SelectionTestData.Candidate("A","Rest",null),minute,minute+15,minute+75));
        }
        private void Advance(long minute)
        {
            var result=Time.AdvanceTo(new WorldInstant(minute));
            while(result.Outcome==WorldAdvanceOutcome.Yielded) result=Time.ResumeAdvance(Time.AdvanceSession.RequestId);
            Assert.AreEqual(WorldAdvanceOutcome.Completed,result.Outcome,result.Diagnostic);
        }
        [Test] public void RealSaveLoad_CommutingActivityRetainsPlanAndClaim_NoReplay_SaveVersionOne()
        {
            Prepare();Assert.IsTrue((bool)Call(Save,"Save",player));
            var loaded=Call(Save,"Load");Assert.IsNotNull(loaded);Assert.IsNull(Window);
            Assert.IsNotNull(Asm("PlayerCollection").GetMethod("GetCharacterByPersonId").Invoke(loaded,new object[]{"A"}));
            Assert.AreEqual("scheduled",Call(Host,"QueryClaim","A"));
            Assert.AreEqual(ActivityLifecycleState.Committed,((ActivityInstance)Call(Host,"QueryActivityInstance","scheduled")).State);
            Call(Village,"SetSimulationPersonSource",loaded);Advance(415);
            Assert.AreEqual(ActivityLifecycleState.Executing,((ActivityInstance)Call(Host,"QueryActivityInstance","scheduled")).State);
            Advance(475);Assert.AreEqual(ActivityLifecycleState.Completed,((ActivityInstance)Call(Host,"QueryActivityInstance","scheduled")).State);
            Assert.AreEqual(475,((PersonActivityState)Call(Host,"QueryPersonActivityState","A")).LastCompletionMinute);
            StringAssert.Contains("\"SaveVersion\": 1",File.ReadAllText(savePath));
        }
        [Test] public void InvalidActiveSave_PreflightRejectsBeforeClockOrActivityReplacement()
        {
            Prepare();Assert.IsTrue((bool)Call(Save,"Save",player));
            string json=File.ReadAllText(savePath);Assert.IsTrue(json.Contains("\"CompletionMinute\": 475"));
            File.WriteAllText(savePath,json.Replace("\"CompletionMinute\": 475","\"CompletionMinute\": 390"));
            Assert.IsNull(Call(Save,"Load"));Assert.AreEqual(400,Time.CurrentInstant.Minute);Assert.AreEqual("scheduled",Call(Host,"QueryClaim","A"));
        }
        [Test] public void ExplicitMissingClockSave_RejectsTimedActivityWithoutWritingOrDroppingIt()
        {
            Prepare();File.WriteAllText(savePath,"unchanged sentinel");
            Assert.IsFalse((bool)Call(Save,"SaveWithVillage",player,null));
            Assert.AreEqual("unchanged sentinel",File.ReadAllText(savePath));Assert.AreEqual("scheduled",Call(Host,"QueryClaim","A"));
        }
        [Test] public void CrossMidnight_ExecutingActivitySurvivesDailyAggregation_ActualCompletionDate()
        {
            Prepare(1400);Advance(1415);Assert.IsTrue((bool)Call(Save,"Save",player));
            var loaded=Call(Save,"Load");Assert.IsNotNull(loaded);Call(Village,"SetSimulationPersonSource",loaded);
            Advance(1440);Assert.AreEqual(ActivityLifecycleState.Executing,((ActivityInstance)Call(Host,"QueryActivityInstance","scheduled")).State);
            Advance(1475);Assert.AreEqual(new ActivityDayStamp(1,1,2),((PersonActivityState)Call(Host,"QueryPersonActivityState","A")).LastMajorParticipationDay);
        }
        [Test] public void MissingSourceAtOpportunity_PreservesOldWindowAndFailsClosed()
        {
            Prepare();Advance(475);var before=Window;Call(Village,"SetSimulationPersonSource",new object[]{null});Advance(505);
            Assert.AreSame(before,Window);Assert.AreEqual("MissingPersonSource",Get(Village,"IntradayActivityDiagnostic"));
        }
    }
}
