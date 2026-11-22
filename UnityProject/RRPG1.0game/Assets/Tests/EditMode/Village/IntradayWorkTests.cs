using System;
using System.Collections.Generic;
using NUnit.Framework;

public class IntradayWorkTests
{
    private sealed class Person : IFulfillmentHost
    { public string PersonId { get; set; } public bool Hungry { get; set; } public int ConsecutiveUnderfedDays { get; set; } }
    private static VillageResourceWorkplaceDef Farm => VillageTestResourceWorkplaces.CreateDefaults()[0];
    private static VillageResourceWorkplaceDef Kitchen => VillageTestResourceWorkplaces.CreateDefaults()[1];
    [Test] public void FarmPartialBatch_SurvivesStaffReplacement()
    {
        var w = new VillageIntradayWork(); var r = new VillageResourceState();
        w.ProcessBatch(r,Farm,20); Assert.AreEqual(0,r.Ingredients);
        w.ProcessBatch(r,Farm,20); Assert.AreEqual(1,r.Ingredients);
        w.ProcessBatch(r,Farm,40); Assert.AreEqual(2,r.Ingredients);
    }
    [Test] public void KitchenNoInput_NoAccumulatedFakeLabor()
    {
        var w = new VillageIntradayWork(); var r = new VillageResourceState();
        w.ProcessBatch(r,Kitchen,200); Assert.AreEqual(0,w.ToSaveData().Batches[0].LaborNumerator);
        r.AddIngredients(1); w.ProcessBatch(r,Kitchen,20);
        Assert.AreEqual(0,r.Food); Assert.AreEqual(0,r.Ingredients);
        w.ProcessBatch(r,Kitchen,20); Assert.AreEqual(1,r.Food);
    }
    [Test] public void KitchenInputPaid_SaveLoadDoesNotPayTwice()
    {
        var w = new VillageIntradayWork(); var r = new VillageResourceState(); r.AddIngredients(2);
        w.ProcessBatch(r,Kitchen,20); Assert.AreEqual(1,r.Ingredients);
        var restored = new VillageIntradayWork(); Assert.IsTrue(restored.Restore(w.ToSaveData()));
        restored.ProcessBatch(r,Kitchen,20); Assert.AreEqual(1,r.Ingredients); Assert.AreEqual(1,r.Food);
    }
    [Test] public void InvalidRestore_PreservesBatchAndLunch()
    {
        var w = new VillageIntradayWork(); var r = new VillageResourceState(); w.ProcessBatch(r,Farm,20);
        var dto=w.ToSaveData(); dto.Batches[0].LaborNumerator=480;
        Assert.IsFalse(w.Restore(dto)); Assert.AreEqual(240,w.ToSaveData().Batches[0].LaborNumerator);
        Assert.IsTrue(w.Restore(null)); Assert.IsEmpty(w.ToSaveData().Batches);
    }
    [TestCase(false)] [TestCase(true)] public void EqualShare_NoOrderedWinners_Once(bool reverse)
    {
        var a=new Person {PersonId="A"};var b=new Person {PersonId="B"}; var r=new VillageResourceState();r.AddFood(1);
        var w=new VillageIntradayWork();var people=new List<IFulfillmentHost>{a,b}; if(reverse)people.Reverse();
        w.Lunch(720,r,people,1,2,1);
        Assert.AreEqual(1,a.ConsecutiveUnderfedDays);Assert.AreEqual(1,b.ConsecutiveUnderfedDays);
        Assert.IsNull(w.Lunch(720,r,people,1,2,1));Assert.AreEqual(1,a.ConsecutiveUnderfedDays);
    }
    [Test] public void AbsentLunch_DoesNotWritePerson_ExpeditionEvidenceOnlyClearsAttendees()
    {
        var a=new Person {PersonId="A",ConsecutiveUnderfedDays=4,Hungry=true};var b=new Person {PersonId="B",ConsecutiveUnderfedDays=3,Hungry=true};
        var w=new VillageIntradayWork(); w.Lunch(720,new VillageResourceState(),new IFulfillmentHost[]{a},1,2,1);
        w.ApplyExpeditionNutrition("B",900,b,true);Assert.AreEqual(3,b.ConsecutiveUnderfedDays);
        w.ApplyExpeditionNutrition("A",900,a,true);Assert.AreEqual(0,a.ConsecutiveUnderfedDays);
    }
    [Test] public void MealInputsAvailableOnlyAtNoon_AfternoonNotReplayed()
    {
        var a=new Person{PersonId="A"};var w=new VillageIntradayWork();var r=new VillageResourceState();
        w.Lunch(720,r,new IFulfillmentHost[]{a},1,2,1);r.AddFood(10);
        Assert.IsNull(w.Lunch(720,r,new IFulfillmentHost[]{a},1,2,1));Assert.IsTrue(a.Hungry);Assert.AreEqual(10,r.Food);
    }
    [Test] public void ActualProjectLabor_AbsentReservedMemberPaused_ReturnResumes()
    {
        var p=new VillageProjectSystem(VillageTestProjects.CreateDefaults());p.TryCreateProject("prj_test_01",out var project);
        p.TryAddParticipant(project.ProjectId,"A");p.OnWorkInterval(480,new string[0]);
        Assert.IsTrue(project.ExecutionPaused);Assert.AreEqual(0,project.Progress);
        var restored=new VillageProjectSystem(VillageTestProjects.CreateDefaults());restored.Restore(p.ToSaveData());
        restored.OnWorkInterval(240,new[]{"A"});Assert.AreEqual(0,restored.ActiveProjects[0].Progress);
        restored.OnWorkInterval(240,new[]{"A"});Assert.AreEqual(1,restored.ActiveProjects[0].Progress);
    }
    [Test] public void EmptyProjectRestore_RetainsPausedCapacityCommitment()
    {
        var p=new VillageProjectSystem(VillageTestProjects.CreateDefaults());p.TryCreateProject("prj_test_01",out var project);
        var restored=new VillageProjectSystem(VillageTestProjects.CreateDefaults());restored.Restore(p.ToSaveData());
        Assert.AreEqual(1,restored.ActiveProjects.Count);Assert.IsTrue(restored.ActiveProjects[0].ExecutionPaused);
    }
    [Test] public void ProjectCompletion_OnlyOnceAndFractionSaved()
    {
        var defs=VillageTestProjects.CreateDefaults();defs[0].TestProgressTarget=1;
        var p=new VillageProjectSystem(defs);p.TryCreateProject("prj_test_01",out var project);p.TryAddParticipant(project.ProjectId,"A");
        p.OnWorkInterval(479,new[]{"A"});Assert.AreEqual(479,p.ToSaveData().ActiveProjects[0].LaborRemainder);
        p.OnWorkInterval(1,new[]{"A"});p.OnWorkInterval(480,new[]{"A"});Assert.AreEqual(1,p.CompletedProjects.Count);
    }
    [Test] public void Commute_DestinationChangeCostsTime_StableDestinationNoSecondJoin()
    {
        var w=new VillageIntradayWork();Assert.IsFalse(w.Arrived("A","Farm",465));Assert.IsFalse(w.Arrived("A","Farm",479));
        Assert.IsTrue(w.Arrived("A","Farm",480));Assert.IsFalse(w.Arrived("A","Project",480));Assert.IsTrue(w.Arrived("A","Project",495));
    }
    [Test] public void EvidenceSnapshotDetached_StableOrder_ConsumeOnce()
    {
        var w=new VillageIntradayWork();w.Work("B","Farm",20);w.Work("A","Farm",30);var copy=w.ToSaveData();copy.Evidence[0].Minutes=99;
        var delta=w.TakeDailyEvidence();Assert.AreEqual("A",delta[0].PersonId);Assert.AreEqual(30,delta[0].WorkedMinutes);Assert.IsEmpty(w.TakeDailyEvidence());
    }
    [Test] public void LunchBreak_WorkScheduleDoesNotTreatRestAsWholeDayBlocked()
    {
        var s=new VillageWorkSchedule();Assert.IsTrue(s.IsWorking(719));Assert.IsFalse(s.IsWorking(720));Assert.IsFalse(s.IsWorking(779));
        Assert.IsTrue(s.IsWorking(780));Assert.IsFalse(s.IsWorking(1020));
    }
}
