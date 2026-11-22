using System;
using System.Collections.Generic;
namespace RRPG.Social
{
    public enum SocialCommitResult
    {
        Success, Duplicate, SourceOccurrenceConflict, InvalidFact, InterpreterFailure,
        InvalidMutation, DuplicatePairMutation, CommitInProgress
    }
    /// <summary>Only gameplay write entry. All business failure points precede persistent mutation.</summary>
    public sealed class SocialCommitService
    {
        private readonly SocialWorldState world;
        private readonly IRelationshipInterpreter interpreter;
        public SocialCommitService(SocialWorldState world, IRelationshipInterpreter interpreter)
        {
            this.world = world ?? throw new ArgumentNullException(nameof(world));
            this.interpreter = interpreter;
        }
        public SocialCommitResult Commit(RelationalFactRecord fact)
        {
            lock (world.SyncRoot)
            {
                if (world.CommitInProgress) return SocialCommitResult.CommitInProgress;
                if (fact == null || !fact.IsValid) return SocialCommitResult.InvalidFact;
                if (world.Facts.TryGet(fact.SourceOccurrenceId, out var existing))
                    return existing.SamePayload(fact)
                        ? SocialCommitResult.Duplicate : SocialCommitResult.SourceOccurrenceConflict;
                if (interpreter == null) return SocialCommitResult.InterpreterFailure;
                world.CommitInProgress = true;
                try
                {
                    var plan = new List<DirectedRelationshipState>();
                    var pairs = new HashSet<Tuple<string, string>>();
                    var query = new ReadOnlyQuery(world.Relationships);
                    try
                    {
                        var mutations = interpreter.Interpret(fact, query);
                        if (mutations == null) return SocialCommitResult.InvalidMutation;
                        // Fully enumerate before commit: lazy enumerators may throw or call back.
                        foreach (var mutation in mutations)
                        {
                            if (mutation == null
                                || !DirectedRelationshipRegistry.ValidPair(mutation.SourcePersonId, mutation.TargetPersonId)
                                || !DirectedRelationshipRegistry.Finite(mutation.AffinityDelta))
                                return SocialCommitResult.InvalidMutation;
                            var pair = Tuple.Create(mutation.SourcePersonId, mutation.TargetPersonId);
                            if (!pairs.Add(pair)) return SocialCommitResult.DuplicatePairMutation;
                            float next = query.GetOrNeutral(pair.Item1, pair.Item2).Affinity + mutation.AffinityDelta;
                            if (!DirectedRelationshipRegistry.Finite(next)) return SocialCommitResult.InvalidMutation;
                            plan.Add(new DirectedRelationshipState(pair.Item1, pair.Item2, next));
                        }
                    }
                    catch (Exception) { return SocialCommitResult.InterpreterFailure; }
                    // No external callbacks or business validation beyond this boundary.
                    world.Facts.Append(fact);
                    foreach (var state in plan) world.Relationships.Apply(state);
                    return SocialCommitResult.Success;
                }
                finally { world.CommitInProgress = false; }
            }
        }
        private sealed class ReadOnlyQuery : IRelationshipQuery
        {
            private readonly DirectedRelationshipRegistry registry;
            internal ReadOnlyQuery(DirectedRelationshipRegistry registry) { this.registry = registry; }
            public int Count => registry.Count;
            public bool TryGet(string source, string target, out DirectedRelationshipState state)
                => registry.TryGet(source, target, out state);
            public DirectedRelationshipState GetOrNeutral(string source, string target)
                => registry.GetOrNeutral(source, target);
        }
    }
}
