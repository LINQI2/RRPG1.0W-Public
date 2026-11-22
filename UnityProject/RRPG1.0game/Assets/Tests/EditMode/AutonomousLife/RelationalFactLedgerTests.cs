using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using RRPG.Social;

namespace RRPG.Tests.AutonomousLife
{
    // Regression contracts: identity, immutable reads and canonical occurrence ownership.
    public class RelationalFactLedgerTests
    {
        [Test] public void A1_MissingQueryNeutralAndNoSideEffect()
        {
            var world = new SocialWorldState();
            Assert.AreEqual(0f, world.Relationships.GetOrNeutral("A", "B").Affinity);
            Assert.IsFalse(world.Relationships.TryGet("A", "B", out var ignored));
            Assert.AreEqual(0, world.Relationships.Count);
        }
        [Test] public void A2_Directionality()
        {
            var world = new SocialWorldState();
            SocialTestKit.Commit(world);
            Assert.AreEqual(10f, world.Relationships.GetOrNeutral("B", "A").Affinity);
            Assert.AreEqual(0f, world.Relationships.GetOrNeutral("A", "B").Affinity);
        }
        [Test] public void A3_SelfPairRejected()
        {
            var world = new SocialWorldState();
            Assert.AreEqual(SocialCommitResult.InvalidFact,
                new SocialCommitService(world, new TestInterpreter()).Commit(SocialTestKit.Fact("self", "A", "A")));
            Assert.AreEqual(0, world.Facts.Facts.Count);
            Assert.AreEqual(0, world.Relationships.Count);
        }
        [Test] public void A4_FactAppend()
        {
            var world = new SocialWorldState(); SocialTestKit.Commit(world);
            Assert.AreEqual(1, world.Facts.Facts.Count);
            Assert.IsTrue(world.Facts.ContainsSourceOccurrence("occurrence"));
        }
        [Test] public void A5_ExactDuplicate()
        {
            var world = new SocialWorldState(); SocialTestKit.Commit(world);
            Assert.AreEqual(SocialCommitResult.Duplicate, SocialTestKit.Service(world).Commit(SocialTestKit.Fact()));
            Assert.AreEqual(1, world.Facts.Facts.Count);
        }
        [Test] public void A6_SourceIdentityConflict()
        {
            var world = new SocialWorldState(); SocialTestKit.Commit(world);
            var different = new RelationalFactRecord("occurrence", "test_visit", "A", "B", 1, 1, 1);
            Assert.AreEqual(SocialCommitResult.SourceOccurrenceConflict, SocialTestKit.Service(world).Commit(different));
            Assert.AreEqual("test_support", world.Facts.Facts[0].FactType);
            Assert.AreEqual(10f, world.Relationships.GetOrNeutral("B", "A").Affinity);
        }
        [Test] public void A7_DifferentOccurrencesSamePair()
        {
            var world = new SocialWorldState(); SocialTestKit.Commit(world);
            Assert.AreEqual(SocialCommitResult.Success, SocialTestKit.Service(world).Commit(SocialTestKit.Fact("second")));
            Assert.AreEqual(2, world.Facts.Facts.Count);
            Assert.AreEqual(20f, world.Relationships.GetOrNeutral("B", "A").Affinity);
        }
        [Test] public void A8_RuntimeFactAndCollectionImmutable()
        {
            var world = new SocialWorldState(); SocialTestKit.Commit(world);
            Assert.IsTrue(typeof(RelationalFactRecord).GetProperties().All(p => p.SetMethod == null));
            Assert.IsTrue(typeof(RelationalFactRecord).GetFields().All(f => f.IsInitOnly));
            var list = (IList)world.Facts.Facts;
            Assert.IsTrue(list.IsReadOnly);
            Assert.Throws<NotSupportedException>(() => list.Add(SocialTestKit.Fact("other")));
            Assert.IsFalse(typeof(RelationalFactLedger).GetMethods().Any(m => m.Name == "Append" || m.Name == "Clear"));
        }
        [Test] public void A9_RelationshipQueryImmutableAndOldSnapshotStable()
        {
            var world = new SocialWorldState(); SocialTestKit.Commit(world);
            var snapshot = world.Relationships.GetOrNeutral("B", "A");
            SocialTestKit.Service(world).Commit(SocialTestKit.Fact("second"));
            Assert.AreEqual(10f, snapshot.Affinity);
            Assert.AreEqual(20f, world.Relationships.GetOrNeutral("B", "A").Affinity);
            Assert.IsTrue(typeof(DirectedRelationshipState).GetProperties().All(p => p.SetMethod == null));
            Assert.IsFalse(typeof(DirectedRelationshipRegistry).GetMethods().Any(m => m.Name == "Apply"));
        }
        [Test] public void OrdinalIdentityDoesNotTrimOrFoldCase()
        {
            var world = new SocialWorldState();
            var service = SocialTestKit.Service(world);
            foreach (string id in new[] { "key", "KEY", " key" })
                Assert.AreEqual(SocialCommitResult.Success, service.Commit(SocialTestKit.Fact(id)));
            Assert.AreEqual(3, world.Facts.Facts.Count);
            SocialWorldPersistence.Restore(world, new SocialWorldSaveData
            {
                Relationships = new[] { new DirectedRelationshipSaveData
                { SourcePersonId = "a", TargetPersonId = "A", Affinity = 1f } }
            });
            Assert.AreEqual(1f, world.Relationships.GetOrNeutral("a", "A").Affinity);
            Assert.AreEqual(0f, world.Relationships.GetOrNeutral("A", "a").Affinity);
        }
        [Test] public void PureAssemblyHasNoUnityOrDomainReferences()
        {
            var names = typeof(SocialWorldState).Assembly.GetReferencedAssemblies().Select(a => a.Name).ToArray();
            Assert.IsFalse(names.Any(n => n.StartsWith("Unity") || n.StartsWith("Village")
                || n.StartsWith("Event") || n.StartsWith("Person") || n.StartsWith("Battle")));
        }
    }

    internal static class SocialTestKit
    {
        internal static RelationalFactRecord Fact(string id = "occurrence", string actor = "A", string target = "B")
            => new RelationalFactRecord(id, "test_support", actor, target, 1, 1, 1);
        internal static SocialCommitService Service(SocialWorldState world)
            => new SocialCommitService(world, new TestInterpreter());
        internal static void Commit(SocialWorldState world)
            => Assert.AreEqual(SocialCommitResult.Success, Service(world).Commit(Fact()));
    }

    internal sealed class TestInterpreter : IRelationshipInterpreter
    {
        internal int Calls;
        internal Func<RelationalFactRecord, IRelationshipQuery, IEnumerable<RelationshipMutation>> Plan;
        public IEnumerable<RelationshipMutation> Interpret(RelationalFactRecord fact, IRelationshipQuery query)
        {
            Calls++;
            Assert.IsFalse(query is DirectedRelationshipRegistry);
            if (Plan != null) return Plan(fact, query);
            // V0.1 Test / Not Frozen: this +10 rule exists only in tests.
            return fact.FactType == "test_support"
                ? new[] { new RelationshipMutation(fact.TargetPersonId, fact.ActorPersonId, 10f) }
                : new RelationshipMutation[0];
        }
    }
}
