using NUnit.Framework;
using RRPG.Social;
namespace RRPG.Tests.AutonomousLife
{
    // Regression contracts: exact restoration, old-save compatibility and stable snapshots.
    public class SocialWorldPersistenceTests
    {
        [Test] public void C1_RoundTripBothDirectionsAndStableFactOrder()
        {
            var w = new SocialWorldState(); SocialTestKit.Commit(w);
            SocialTestKit.Service(w).Commit(SocialTestKit.Fact("second", "B", "A"));
            var dto = SocialWorldPersistence.Build(w);
            var restored = new SocialWorldState();
            SocialWorldPersistence.Restore(restored, dto);
            Assert.AreEqual("occurrence", restored.Facts.Facts[0].SourceOccurrenceId);
            Assert.AreEqual("second", restored.Facts.Facts[1].SourceOccurrenceId);
            Assert.AreEqual(10f, restored.Relationships.GetOrNeutral("A", "B").Affinity);
            Assert.AreEqual(10f, restored.Relationships.GetOrNeutral("B", "A").Affinity);
        }
        [Test] public void C2_RestoreReplaces()
        {
            var w = new SocialWorldState(); SocialTestKit.Commit(w);
            var other = new SocialWorldState();
            SocialTestKit.Service(other).Commit(SocialTestKit.Fact("new", "C", "D"));
            SocialWorldPersistence.Restore(w, SocialWorldPersistence.Build(other));
            Assert.IsFalse(w.Facts.ContainsSourceOccurrence("occurrence"));
            Assert.IsFalse(w.Relationships.TryGet("B", "A", out var ignored));
            Assert.AreEqual(1, w.Facts.Facts.Count);
            Assert.AreEqual(10f, w.Relationships.GetOrNeutral("D", "C").Affinity);
        }
        [Test] public void C3_RestoreNullClears()
        {
            var w = new SocialWorldState(); SocialTestKit.Commit(w);
            SocialWorldPersistence.Restore(w, null);
            Assert.AreEqual(0, w.Facts.Facts.Count); Assert.AreEqual(0, w.Relationships.Count);
        }
        [Test] public void C4_InvalidDataFilteringAndFirstWinsDiagnostics()
        {
            var w = new SocialWorldState(); SocialTestKit.Commit(w);
            var valid = SocialWorldPersistence.Build(w).RelationalFacts[0];
            var conflict = new RelationalFactSaveData
            { SourceOccurrenceId = "occurrence", FactType = "changed", ActorPersonId = "A", TargetPersonId = "B", Year = 1, Month = 1, Day = 1 };
            var dto = new SocialWorldSaveData
            {
                RelationalFacts = new[] { valid, valid, conflict, null,
                    new RelationalFactSaveData { SourceOccurrenceId = "bad" } },
                Relationships = new[]
                {
                    new DirectedRelationshipSaveData { SourcePersonId = "B", TargetPersonId = "A", Affinity = 7f },
                    new DirectedRelationshipSaveData { SourcePersonId = "B", TargetPersonId = "A", Affinity = 99f },
                    new DirectedRelationshipSaveData { SourcePersonId = "A", TargetPersonId = "A" },
                    new DirectedRelationshipSaveData { SourcePersonId = "A", TargetPersonId = "B", Affinity = float.NaN },
                    null
                }
            };
            var result = SocialWorldPersistence.Restore(w, dto);
            Assert.AreEqual(1, result.DuplicateFacts); Assert.AreEqual(1, result.ConflictingFacts);
            Assert.AreEqual(2, result.InvalidFacts); Assert.AreEqual(3, result.InvalidRelationships);
            Assert.AreEqual(1, result.DuplicateRelationships);
            Assert.AreEqual(1, w.Facts.Facts.Count); Assert.AreEqual(1, w.Relationships.Count);
            Assert.AreEqual(7f, w.Relationships.GetOrNeutral("B", "A").Affinity);
        }
        [Test] public void C5_RelationshipOrderingOrdinalAndRepeatable()
        {
            var w = new SocialWorldState();
            SocialWorldPersistence.Restore(w, new SocialWorldSaveData { Relationships = new[]
            {
                new DirectedRelationshipSaveData { SourcePersonId = "z", TargetPersonId = "a" },
                new DirectedRelationshipSaveData { SourcePersonId = "A", TargetPersonId = "z" },
                new DirectedRelationshipSaveData { SourcePersonId = "A", TargetPersonId = "a" }
            } });
            var first = SocialWorldPersistence.Build(w).Relationships;
            var second = SocialWorldPersistence.Build(w).Relationships;
            Assert.AreEqual("A", first[0].SourcePersonId); Assert.AreEqual("a", first[0].TargetPersonId);
            Assert.AreEqual("z", first[1].TargetPersonId); Assert.AreEqual("z", first[2].SourcePersonId);
            for (int i = 0; i < first.Length; i++)
            {
                Assert.AreEqual(first[i].SourcePersonId, second[i].SourcePersonId);
                Assert.AreEqual(first[i].TargetPersonId, second[i].TargetPersonId);
            }
        }
        [Test] public void C6_UnknownInactivePersonIdsPreserved()
        {
            var w = new SocialWorldState();
            SocialTestKit.Service(w).Commit(SocialTestKit.Fact("inactive", "gone", "unknown"));
            var restored = new SocialWorldState();
            SocialWorldPersistence.Restore(restored, SocialWorldPersistence.Build(w));
            Assert.AreEqual("gone", restored.Facts.Facts[0].ActorPersonId);
            Assert.AreEqual(10f, restored.Relationships.GetOrNeutral("unknown", "gone").Affinity);
        }
        [Test] public void C7_RestoreDoesNotReinterpretFacts()
        {
            var w = new SocialWorldState(); SocialTestKit.Commit(w);
            var data = SocialWorldPersistence.Build(w); data.Relationships[0].Affinity = 42f;
            var interpreter = new TestInterpreter { Plan = (f, q) => throw new System.Exception("Must not run") };
            var service = new SocialCommitService(w, interpreter);
            SocialWorldPersistence.Restore(w, data);
            Assert.AreEqual(42f, w.Relationships.GetOrNeutral("B", "A").Affinity);
            Assert.AreEqual(0, interpreter.Calls);
            Assert.AreEqual(SocialCommitResult.Duplicate, service.Commit(SocialTestKit.Fact()));
            Assert.AreEqual(0, interpreter.Calls);
        }
        [Test] public void BuildAndRestoreSnapshotsDoNotAliasDto()
        {
            var w = new SocialWorldState(); SocialTestKit.Commit(w);
            var dto = SocialWorldPersistence.Build(w);
            dto.RelationalFacts[0].FactType = "edited"; dto.Relationships[0].Affinity = 99f;
            Assert.AreEqual("test_support", w.Facts.Facts[0].FactType);
            Assert.AreEqual(10f, w.Relationships.GetOrNeutral("B", "A").Affinity);
            SocialWorldPersistence.Restore(w, dto);
            dto.RelationalFacts[0].FactType = "edited_again"; dto.Relationships[0].Affinity = 200f;
            Assert.AreEqual("edited", w.Facts.Facts[0].FactType);
            Assert.AreEqual(99f, w.Relationships.GetOrNeutral("B", "A").Affinity);
        }
    }
}
