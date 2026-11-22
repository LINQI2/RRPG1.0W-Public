using System;
using System.Collections.Generic;
using NUnit.Framework;
using RRPG.Social;
namespace RRPG.Tests.AutonomousLife
{
    // Regression contracts: exactly-once effects and failure atomicity.
    public class SocialCommitServiceTests
    {
        [Test] public void B1_Success()
        {
            var w = new SocialWorldState(); SocialTestKit.Commit(w);
            Assert.AreEqual(1, w.Relationships.Count);
            Assert.AreEqual(10f, w.Relationships.GetOrNeutral("B", "A").Affinity);
        }
        [Test] public void B2_DuplicateNeverReinterprets()
        {
            var w = new SocialWorldState(); var interpreter = new TestInterpreter();
            var service = new SocialCommitService(w, interpreter);
            Assert.AreEqual(SocialCommitResult.Success, service.Commit(SocialTestKit.Fact()));
            Assert.AreEqual(SocialCommitResult.Duplicate, service.Commit(SocialTestKit.Fact()));
            Assert.AreEqual(1, interpreter.Calls);
            Assert.AreEqual(10f, w.Relationships.GetOrNeutral("B", "A").Affinity);
        }
        [Test] public void B3_InterpreterFailureAtomicity()
        {
            Reject(new TestInterpreter { Plan = (f, q) => throw new InvalidOperationException("test failure") },
                SocialCommitResult.InterpreterFailure);
        }
        [Test] public void B4_InvalidMutationAtomicityAfterValidProposal()
        {
            Reject(new TestInterpreter { Plan = (f, q) => new[]
            {
                new RelationshipMutation("A", "B", 1f), new RelationshipMutation("A", "A", 1f)
            } }, SocialCommitResult.InvalidMutation);
        }
        [Test] public void B5_MultiMutationAllCommitted()
        {
            var w = new SocialWorldState();
            var i = new TestInterpreter { Plan = (f, q) => new[]
            { new RelationshipMutation("A", "B", 2f), new RelationshipMutation("B", "A", 3f) } };
            Assert.AreEqual(SocialCommitResult.Success, new SocialCommitService(w, i).Commit(SocialTestKit.Fact()));
            Assert.AreEqual(2f, w.Relationships.GetOrNeutral("A", "B").Affinity);
            Assert.AreEqual(3f, w.Relationships.GetOrNeutral("B", "A").Affinity);
        }
        [Test] public void B6_DuplicatePairFailClosed()
        {
            Reject(new TestInterpreter { Plan = (f, q) => new[]
            { new RelationshipMutation("A", "B", 1f), new RelationshipMutation("A", "B", 2f) } },
                SocialCommitResult.DuplicatePairMutation);
        }
        [Test] public void LazyEnumerationFailureAtomicity()
        {
            Reject(new TestInterpreter { Plan = (f, q) => ThrowAfterFirst() }, SocialCommitResult.InterpreterFailure);
        }
        private static IEnumerable<RelationshipMutation> ThrowAfterFirst()
        {
            yield return new RelationshipMutation("A", "B", 1f);
            throw new InvalidOperationException("test iterator failure");
        }
        [TestCase(float.NaN)] [TestCase(float.PositiveInfinity)] [TestCase(float.NegativeInfinity)]
        public void NonFiniteDeltaRejected(float delta)
        {
            Reject(new TestInterpreter { Plan = (f, q) => new[] { new RelationshipMutation("A", "B", delta) } },
                SocialCommitResult.InvalidMutation);
        }
        [Test] public void FiniteDeltaOverflowRejected()
        {
            var w = new SocialWorldState();
            SocialWorldPersistence.Restore(w, new SocialWorldSaveData { Relationships = new[]
            { new DirectedRelationshipSaveData { SourcePersonId = "A", TargetPersonId = "B", Affinity = float.MaxValue } } });
            var i = new TestInterpreter { Plan = (f, q) => new[] { new RelationshipMutation("A", "B", float.MaxValue) } };
            Assert.AreEqual(SocialCommitResult.InvalidMutation, new SocialCommitService(w, i).Commit(SocialTestKit.Fact()));
            Assert.AreEqual(0, w.Facts.Facts.Count);
            Assert.AreEqual(float.MaxValue, w.Relationships.GetOrNeutral("A", "B").Affinity);
        }
        [Test] public void ReentrantCommitAndRestoreCannotMutateWhilePlanning()
        {
            var w = new SocialWorldState();
            var i = new TestInterpreter { Plan = (f, q) =>
            {
                Assert.AreEqual(SocialCommitResult.CommitInProgress, SocialTestKit.Service(w).Commit(SocialTestKit.Fact("nested")));
                Assert.Throws<InvalidOperationException>(() => SocialWorldPersistence.Restore(w, null));
                return new RelationshipMutation[0];
            } };
            Assert.AreEqual(SocialCommitResult.Success, new SocialCommitService(w, i).Commit(SocialTestKit.Fact()));
            Assert.AreEqual(1, w.Facts.Facts.Count);
        }
        [Test] public void ZeroMutationFactAllowedAndMissingInterpreterFailsClosed()
        {
            var w = new SocialWorldState();
            Assert.AreEqual(SocialCommitResult.InterpreterFailure,
                new SocialCommitService(w, null).Commit(SocialTestKit.Fact()));
            Assert.AreEqual(0, w.Facts.Facts.Count);
            var i = new TestInterpreter { Plan = (f, q) => new RelationshipMutation[0] };
            Assert.AreEqual(SocialCommitResult.Success, new SocialCommitService(w, i).Commit(SocialTestKit.Fact()));
            Assert.AreEqual(1, w.Facts.Facts.Count);
            Assert.AreEqual(0, w.Relationships.Count);
        }
        private static void Reject(TestInterpreter i, SocialCommitResult expected)
        {
            var w = new SocialWorldState(); SocialTestKit.Commit(w);
            Assert.AreEqual(expected, new SocialCommitService(w, i).Commit(SocialTestKit.Fact("rejected")));
            Assert.AreEqual(1, w.Facts.Facts.Count);
            Assert.AreEqual(1, w.Relationships.Count);
            Assert.AreEqual(10f, w.Relationships.GetOrNeutral("B", "A").Affinity);
        }
    }
}
