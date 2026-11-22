using NUnit.Framework;
using RRPG.Event;

namespace RRPG.EventProduct.Tests
{
    /// <summary>Event World 存档：Outcome 稳定性与结构边界（D0 §4/§5/§9）。</summary>
    public class EventWorldPersistencePendingTests
    {
        private const string OptionId = EventWorldPersistenceFixtures.OptionId;

        [Test]
        public void PendingEvent_RoundTrip_KeepsOutcomeDeterministic()
        {
            var catalog = EventWorldPersistenceFixtures.BuildCatalog();
            var instance = EventWorldPersistenceFixtures.ResolveInstance(catalog, seed: 77);
            Assert.IsNotNull(instance);

            var saved = EventWorldPersistence.Build(new EventWorldState(), instance);
            var restored = EventWorldPersistence.FromSaveData(saved.PendingVillageEvent);

            var before = EventOptionExecutor.PreviewOutcome(instance, OptionId, catalog);
            var after = EventOptionExecutor.PreviewOutcome(restored, OptionId, catalog);

            Assert.IsNotNull(before);
            Assert.IsNotNull(after);
            Assert.AreEqual(before.OutcomeId, after.OutcomeId, "Save / Load 后 Outcome 必须完全一致（不重抽）");
        }

        [Test]
        public void Restore_NullBlock_IsEmptyDefault()
        {
            var world = new EventWorldState();
            world.SetFlag("legacy_flag");
            world.History.RecordOccurrence(EventWorldPersistenceFixtures.EventId, 10, "x");

            EventWorldPersistence.Restore(world, null);

            Assert.IsEmpty(world.Flags);
            Assert.AreEqual(0, world.History.OccurrenceCount(EventWorldPersistenceFixtures.EventId));
            Assert.IsNull(EventWorldPersistence.FromSaveData(null), "空 Pending → null");
        }

        [Test]
        public void FromSaveData_RejectsIncompletePending()
        {
            Assert.IsNull(EventWorldPersistence.FromSaveData(new PendingEventInstanceSaveData()));
            Assert.IsNull(EventWorldPersistence.FromSaveData(new PendingEventInstanceSaveData
            {
                EventInstanceId = "evtinst_x", EventId = "evt_x"
            }), "缺 TriggerType / TriggerKey → 不猜、不造");
        }

        [Test]
        public void SaveStructure_DoesNotPersistCompletedInstances_OrSessionHistory()
        {
            // §4：Completed Instance 不进入长期存档；§5：Expedition Session History 保持 Runtime Only。
            foreach (var field in typeof(EventWorldSaveData).GetFields())
            {
                StringAssert.DoesNotContain("Completed", field.Name);
                StringAssert.DoesNotContain("Session", field.Name);
                StringAssert.DoesNotContain("Expedition", field.Name);
            }

            foreach (var field in typeof(EventHistorySaveData).GetFields())
            {
                StringAssert.DoesNotContain("Completed", field.Name);
            }
        }
    }
}
