using System;
using System.Linq;
using NUnit.Framework;
using RRPG.AutonomousActivity;
namespace RRPG.Tests.ActivityLifecycle
{
    public class ActivityPersistenceTests
    {
        private ActivityWorldState world;
        private ActivityLifecycleService service;
        private ActivityDayStamp day => ActivityLifecycleTests.Day();
        private void Open() => service.OpenDailyWindow(new ActivityDailyWindow(day, new[] { "A", "B", "Z" }, new[] { "A", "B", "Z" }));
        private void Complete(string id, string actor)
        { service.TryCommitActivity(id, ActivityLifecycleTests.Candidate(actor)); service.TryBeginExecution(id); service.TryComplete(id, out _); }
        [SetUp] public void Setup() { world = new ActivityWorldState(); service = new ActivityLifecycleService(world); Open(); Complete("one", "A"); }
        [Test] public void B1_DeepRoundTrip()
        {
            var data = ActivityWorldPersistence.Build(world); var copy = new ActivityWorldState();
            ActivityWorldPersistence.Restore(copy, data); data.PersonActivityStates[0].PersonId = "X";
            Assert.AreEqual(day, copy.QueryPersonActivityState("A").LastMajorParticipationDay);
            Assert.AreEqual("test_talk", copy.QueryPersonActivityState("A").LastInitiatedActivityType);
        }
        [Test] public void B2_Replacement()
        {
            var other = new ActivityWorldState(); ActivityWorldPersistence.Restore(other, ActivityWorldPersistence.Build(world));
            var otherService = new ActivityLifecycleService(other); otherService.OpenDailyWindow(new ActivityDailyWindow(day, new[] { "B" }, new[] { "B" }));
            otherService.TryCommitActivity("b", ActivityLifecycleTests.Candidate("B")); otherService.TryBeginExecution("b"); otherService.TryComplete("b", out _);
            var data = ActivityWorldPersistence.Build(other); data.PersonActivityStates = data.PersonActivityStates.Where(p => p.PersonId == "B").ToArray();
            ActivityWorldPersistence.Restore(world, data);
            Assert.IsNull(world.QueryPersonActivityState("A").LastMajorParticipationDay); Assert.AreEqual(1, world.PersonStateCount);
        }
        [Test] public void B3_NullRestore() { ActivityWorldPersistence.Restore(world, null); Assert.AreEqual(0, world.PersonStateCount); Assert.IsNull(world.QueryCurrentWindow()); }
        [TestCase(false)] [TestCase(true)]
        public void B4_InflightNotPersisted(bool executing)
        {
            service.TryCommitActivity("inflight", ActivityLifecycleTests.Candidate("B")); if(executing) service.TryBeginExecution("inflight");
            var data = ActivityWorldPersistence.Build(world);
            Assert.AreEqual(1, data.PersonActivityStates.Length);
            Assert.IsEmpty(data.ActiveInstances,"旧每日兼容模式的瞬态实例仍不保存；日内 scheduled 实例由新增专项验证");
            CollectionAssert.AreEquivalent(new[] { "PersonActivityStates","IntradaySchemaVersion","ActiveInstances" }, typeof(ActivityWorldSaveData).GetFields().Select(f => f.Name));
        }
        [Test] public void B5_RestoreClearsClaimsAndInstances()
        {
            service.TryCommitActivity("inflight", ActivityLifecycleTests.Candidate("B"));
            ActivityWorldPersistence.Restore(world, ActivityWorldPersistence.Build(world));
            Assert.AreEqual(0, world.Claims.Count); Assert.AreEqual(0, world.InstanceCount);
        }
        [Test] public void B6_StableOrdinalOrdering() { Complete("z", "Z"); Complete("b", "B"); CollectionAssert.AreEqual(new[] { "A", "B", "Z" }, ActivityWorldPersistence.Build(world).PersonActivityStates.Select(p => p.PersonId)); }
        [Test] public void B7_InvalidAndDuplicateFilteringUnknownIdentityPreserved()
        {
            var valid = ActivityWorldPersistence.Build(world).PersonActivityStates[0]; valid.PersonId = "inactive";
            int filtered = ActivityWorldPersistence.Restore(world, new ActivityWorldSaveData { PersonActivityStates = new[] {
                null, new PersonActivitySaveData {PersonId="bad", LastParticipationYear=1,LastParticipationMonth=13,LastParticipationDay=2},
                new PersonActivitySaveData {PersonId=" "}, valid, valid } });
            Assert.AreEqual(4, filtered); Assert.AreEqual(1, world.PersonStateCount);
            Assert.AreEqual(day, world.QueryPersonActivityState("inactive").LastMajorParticipationDay);
        }
        [Test] public void B8_EmptyLegacyBlock() { ActivityWorldPersistence.Restore(world, new ActivityWorldSaveData()); Assert.AreEqual(0, world.PersonStateCount); }
        [Test] public void B9_MissingReadDoesNotCreate() { var state = world.QueryPersonActivityState("missing"); Assert.IsNull(state.LastMajorParticipationDay); Assert.AreEqual(1, world.PersonStateCount); }
        [Test] public void B10_RestoreDoesNotReopenOrReplay()
        {
            ActivityWorldPersistence.Restore(world, ActivityWorldPersistence.Build(world)); Assert.IsNull(world.QueryCurrentWindow());
            Open(); Assert.AreEqual(ActivityResult.AlreadyParticipated, service.TryCommitActivity("second", ActivityLifecycleTests.Candidate("A")));
            Assert.AreEqual(0, world.Claims.Count);
        }
        [Test] public void ParticipantWithoutInitiatedStateSurvivesRestore()
        {
            service.TryCommitActivity("talk", ActivityLifecycleTests.Candidate("B","Z")); service.TryBeginExecution("talk"); service.TryComplete("talk", out _);
            ActivityWorldPersistence.Restore(world, ActivityWorldPersistence.Build(world));
            Assert.IsNull(world.QueryPersonActivityState("Z").LastInitiatedActivityDay);
            Assert.AreEqual(day, world.QueryPersonActivityState("Z").LastMajorParticipationDay);
        }
    }
}
