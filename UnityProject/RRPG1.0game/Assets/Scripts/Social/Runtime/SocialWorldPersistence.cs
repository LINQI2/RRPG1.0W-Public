using System;
using System.Collections.Generic;
namespace RRPG.Social
{
    public sealed class SocialRestoreResult
    {
        public int InvalidFacts { get; internal set; }
        public int DuplicateFacts { get; internal set; }
        public int ConflictingFacts { get; internal set; }
        public int InvalidRelationships { get; internal set; }
        public int DuplicateRelationships { get; internal set; }
    }
    /// <summary>Deep snapshot and exact replacement restore; never runs an interpreter.</summary>
    public static class SocialWorldPersistence
    {
        public static SocialWorldSaveData Build(SocialWorldState world)
        {
            if (world == null) return new SocialWorldSaveData
            {
                RelationalFacts = new RelationalFactSaveData[0],
                Relationships = new DirectedRelationshipSaveData[0]
            };
            lock (world.SyncRoot)
            {
                var facts = new List<RelationalFactSaveData>();
                foreach (var fact in world.Facts.Facts)
                    facts.Add(new RelationalFactSaveData
                    {
                        SourceOccurrenceId = fact.SourceOccurrenceId, FactType = fact.FactType,
                        ActorPersonId = fact.ActorPersonId, TargetPersonId = fact.TargetPersonId,
                        Year = fact.Year, Month = fact.Month, Day = fact.Day
                    });
                var ordered = new List<DirectedRelationshipState>(world.Relationships.Records);
                ordered.Sort((a, b) =>
                {
                    int source = string.CompareOrdinal(a.SourcePersonId, b.SourcePersonId);
                    return source != 0 ? source : string.CompareOrdinal(a.TargetPersonId, b.TargetPersonId);
                });
                var relationships = new List<DirectedRelationshipSaveData>();
                foreach (var state in ordered)
                    relationships.Add(new DirectedRelationshipSaveData
                    {
                        SourcePersonId = state.SourcePersonId, TargetPersonId = state.TargetPersonId,
                        Affinity = state.Affinity
                    });
                return new SocialWorldSaveData
                {
                    RelationalFacts = facts.ToArray(), Relationships = relationships.ToArray()
                };
            }
        }
        public static SocialRestoreResult Restore(SocialWorldState world, SocialWorldSaveData data)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            lock (world.SyncRoot)
            {
                if (world.CommitInProgress)
                    throw new InvalidOperationException("Cannot restore social state during interpretation/commit.");
                world.Facts.Clear(); world.Relationships.Clear();
                var result = new SocialRestoreResult();
                if (data == null) return result;
                if (data.RelationalFacts != null)
                    foreach (var dto in data.RelationalFacts)
                    {
                        var fact = dto == null ? null : new RelationalFactRecord(dto.SourceOccurrenceId, dto.FactType,
                            dto.ActorPersonId, dto.TargetPersonId, dto.Year, dto.Month, dto.Day);
                        if (fact == null || !fact.IsValid) { result.InvalidFacts++; continue; }
                        if (world.Facts.TryGet(fact.SourceOccurrenceId, out var existing))
                        {
                            if (existing.SamePayload(fact)) result.DuplicateFacts++;
                            else result.ConflictingFacts++;
                            continue;
                        }
                        world.Facts.Append(fact);
                    }
                if (data.Relationships != null)
                    foreach (var dto in data.Relationships)
                    {
                        if (dto == null || !DirectedRelationshipRegistry.ValidPair(dto.SourcePersonId, dto.TargetPersonId)
                            || !DirectedRelationshipRegistry.Finite(dto.Affinity))
                        { result.InvalidRelationships++; continue; }
                        if (world.Relationships.TryGet(dto.SourcePersonId, dto.TargetPersonId, out var ignored))
                        { result.DuplicateRelationships++; continue; }
                        world.Relationships.RestoreRecord(
                            new DirectedRelationshipState(dto.SourcePersonId, dto.TargetPersonId, dto.Affinity));
                    }
                return result;
            }
        }
    }
}
