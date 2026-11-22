using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using RRPG.AutonomousActivity;
namespace RRPG.Tests.ActivityLifecycle
{
    public class ActivityVillageIntegrationTests : ActivityIntegrationTestKit
    {
        private object Bind(bool dead = false)
        {
            var player = Player(); AddCharacter(player,"A",dead); AddCharacter(player,"B");
            Call(Village,"SetSimulationPersonSource",player); return player;
        }
        private void Eligible(string id, bool expected) { Assert.IsNotNull(Window); Assert.AreEqual(expected, Window.ActorEligiblePersonIds.Contains(id)); Assert.AreEqual(expected, Window.ParticipantAvailablePersonIds.Contains(id)); }
        [Test] public void C1_PrimaryAssignmentDoesNotBlock()
        {
            Bind(); var assignments = (VillageAssignmentSystem)Get(Village,"Assignments");
            Assert.AreEqual(VillageAssignmentResult.Success, assignments.TryAssign("A", "ws_test_01"));
            Time.TickDay(); Eligible("A",true); Assert.IsNotNull(assignments.GetRecord("A"));
        }
        [Test] public void C2_ExpeditionBlocks() { Bind(); Call(Village,"SetExpeditionPersonIds",new object[]{new[]{"A"}}); Time.TickDay(); Eligible("A",false); Eligible("B",true); }
        [TestCase(false)] [TestCase(true)]
        public void C3_UnavailableOrDeadBlocks(bool dead)
        {
            Bind(dead); if(!dead) Call(Village,"SetUnavailablePersonIds",new object[]{new[]{"A"}});
            Time.TickDay(); Eligible("A",false); Eligible("B",true);
        }
        private VillageProjectSystem Project(int target)
        {
            var projects = new VillageProjectSystem(new[] { new VillageProjectDef {
                ProjectDefId="test_project",ParticipantCapacity=1,TestProgressPerParticipantPerDay=1,TestProgressTarget=target } });
            Set(Village,"Projects",projects);
            Assert.AreEqual(VillageProjectResult.Success,projects.TryCreateProject("test_project",out var project));
            Assert.AreEqual(VillageProjectResult.Success,projects.TryAddParticipant(project.ProjectId,"A")); return projects;
        }
        [Test] public void C4_ProjectCommitmentDoesNotBlockOffHours() { Bind(); Project(10); Time.TickDay(); Eligible("A",true); }
        [Test] public void C5_CompletionDoesNotBlockOffHours() { Bind(); var projects=Project(1); Time.TickDay(); Assert.AreEqual(1,projects.CompletedProjects.Count); Eligible("A",true); }
        [Test] public void C6_NextDayRecoversAndAssignmentReturns()
        {
            Bind(); var assignments=(VillageAssignmentSystem)Get(Village,"Assignments"); assignments.TryAssign("A","ws_test_01"); Project(1);
            Time.TickDay(); Eligible("A",true); Time.TickDay(); Eligible("A",true); Assert.IsNotNull(assignments.GetRecord("A"));
        }
        [Test] public void C7_RecoveryWindowDoesNotBlock()
        {
            Bind(); Assert.IsTrue((bool)Call(Village,"GrantPersonRestUntilMonthEnd","A"));
            Time.TickDay(); Eligible("A",true);
        }
        [TestCase(false)] [TestCase(true)]
        public void C8_DirectAndLiveUpdateUseSamePhase(bool update)
        { Bind(); if(update) Time.Update(Time.SecondsPerDay); else Time.TickDay();
            Assert.IsTrue(Window.IsIntraday);Assert.AreEqual(1,Window.DayStamp.Day);
            Assert.Less(Window.OpportunityMinute,Time.CurrentInstant.Minute); }
        [Test] public void C9_JumpMonthRunsEveryDailyPhase()
        {
            Bind(); int observed=0; Time.DayTicked += _ => { observed++; Assert.IsTrue(Window.IsIntraday);
                Assert.Less(Window.OpportunityMinute,Time.CurrentInstant.Minute); };
            Time.JumpToNextMonth();while(Time.HasActiveAdvance) Assert.AreNotEqual(WorldAdvanceOutcome.PartialBoundaryFailure,Time.ResumeAdvance(Time.AdvanceSession.RequestId).Outcome);
            Assert.AreEqual(31,observed); Assert.AreEqual(1,Window.DayStamp.Month); Assert.AreEqual(31,Window.DayStamp.Day);
        }
        [Test] public void C10_C11_C12_ReturnCatchupUsesSameSourceAndPhaseAwayBlockedHomeEligible()
        {
            var player=Bind(); Call(Village,"SetExpeditionPersonIds",new object[]{new[]{"A"}});
            Assert.IsTrue((bool)Call(Village,"BeginExpeditionReturn",3)); Assert.IsTrue((bool)Call(Village,"MarkExpeditionReturnSettled"));
            int observed=0; Time.DayTicked += _ => { observed++; Eligible("A",false); Eligible("B",true); Assert.AreSame(player,Get(Village,"SimulationPersonSource")); Assert.AreEqual(Time.Day-1,Window.DayStamp.Day); };
            Assert.AreEqual(ExpeditionCatchUpResult.Completed,Call(Village,"RunReturnCatchUp")); Assert.AreEqual(3,observed);
        }
        [Test] public void C13_MissingSourceFailsClosedPreservesOldWindow()
        {
            Bind(); Time.TickDay(); var before=Window; Call(Village,"SetSimulationPersonSource",new object[]{null});
            Assert.DoesNotThrow(()=>Time.TickDay()); Assert.AreSame(before,Window);
            Assert.AreEqual("MissingPersonSource",Get(Village,"LastActivityDailyPhaseResult").ToString());
        }
        [Test] public void C14_BoundEmptyCollectionOpensRealEmptyWindow()
        {
            Call(Village,"SetSimulationPersonSource",Player()); Time.TickDay();
            Assert.IsNotNull(Window); Assert.IsEmpty(Window.ActorEligiblePersonIds); Assert.IsEmpty(Window.ParticipantAvailablePersonIds);
        }
    }
}
