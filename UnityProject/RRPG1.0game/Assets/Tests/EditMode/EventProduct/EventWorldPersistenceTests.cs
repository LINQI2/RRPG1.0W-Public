using NUnit.Framework;
using RRPG.Event;

namespace RRPG.EventProduct.Tests
{
    /// <summary>
    /// WP-Event-1D · Gate D0 §3/§9：Event World 存档往返（Flags / History / Pending 身份）。
    /// </summary>
    public class EventWorldPersistenceTests
    {
        private const string EventId = EventWorldPersistenceFixtures.EventId;

        [Test]
        public void FlagsAndHistory_RoundTrip_ExactValues()
        {
            var world = new EventWorldState();
            world.SetFlag("shrine_opened");
            world.SetFlag("draft_seen");
            world.History.RecordOccurrence(EventId, 100, "evtinst_a");
            world.History.RecordOccurrence(EventId, 140, "evtinst_b");
            world.History.RecordOccurrence("evt_other", 90, "evtinst_c");

            var saved = EventWorldPersistence.Build(world, null);
            Assert.IsNull(saved.PendingVillageEvent, "无 Pending 时不得写 Pending 区块");

            var loaded = new EventWorldState();
            loaded.SetFlag("should_be_cleared");
            EventWorldPersistence.Restore(loaded, saved);

            Assert.IsTrue(loaded.IsFlagSet("shrine_opened"));
            Assert.IsTrue(loaded.IsFlagSet("draft_seen"));
            Assert.IsFalse(loaded.IsFlagSet("should_be_cleared"), "恢复必须先清空再写回");

            Assert.AreEqual(2, loaded.History.OccurrenceCount(EventId), "计数必须精确写回（不是 +1）");
            Assert.AreEqual(1, loaded.History.OccurrenceCount("evt_other"));

            int lastDay;
            Assert.IsTrue(loaded.History.TryGetLastVillageDayOrdinal(EventId, out lastDay));
            Assert.AreEqual(140, lastDay, "最近发生日序必须写回");
        }

        [Test]
        public void PendingEvent_RoundTrip_KeepsIdentityBindingsAndContext()
        {
            var catalog = EventWorldPersistenceFixtures.BuildCatalog();
            var instance = EventWorldPersistenceFixtures.ResolveInstance(catalog, seed: 2026);
            Assert.IsNotNull(instance, "应能解析出实例");

            var saved = EventWorldPersistence.Build(new EventWorldState(), instance);
            Assert.IsNotNull(saved.PendingVillageEvent, "有 Pending 时必须落档");

            var restored = EventWorldPersistence.FromSaveData(saved.PendingVillageEvent);
            Assert.IsNotNull(restored);

            Assert.AreEqual(instance.EventInstanceId, restored.EventInstanceId);
            Assert.AreEqual(instance.EventId, restored.EventId);
            Assert.AreEqual(instance.TriggerType, restored.TriggerType);
            Assert.AreEqual(instance.TriggerKey, restored.TriggerKey);
            Assert.AreEqual(instance.ResolutionSeed, restored.ResolutionSeed);

            string originalPerson;
            string restoredPerson;
            Assert.IsTrue(instance.TryGetBoundPerson("role_d0", out originalPerson));
            Assert.IsTrue(restored.TryGetBoundPerson("role_d0", out restoredPerson));
            Assert.AreEqual(originalPerson, restoredPerson, "Requester 人物不得变化");
            Assert.AreEqual("p1", restoredPerson);

            string observer;
            Assert.IsTrue(restored.ContextSnapshot.TryGetPerson("Observer", out observer));
            Assert.AreEqual("p1", observer);
            Assert.AreEqual(instance.ContextSnapshot.ToText(), restored.ContextSnapshot.ToText(),
                "Context 快照必须逐条一致");
        }
    }
}
