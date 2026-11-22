using System;
using System.Collections.Generic;
namespace RRPG.Social
{
    public interface IRelationshipQuery
    {
        int Count { get; }
        bool TryGet(string sourcePersonId, string targetPersonId, out DirectedRelationshipState state);
        DirectedRelationshipState GetOrNeutral(string sourcePersonId, string targetPersonId);
    }
    public sealed class DirectedRelationshipRegistry : IRelationshipQuery
    {
        private readonly Dictionary<Tuple<string, string>, DirectedRelationshipState> records =
            new Dictionary<Tuple<string, string>, DirectedRelationshipState>();
        public int Count => records.Count;
        internal IEnumerable<DirectedRelationshipState> Records => records.Values;
        internal DirectedRelationshipRegistry() { }
        public bool TryGet(string sourcePersonId, string targetPersonId, out DirectedRelationshipState state)
        {
            state = null;
            return ValidPair(sourcePersonId, targetPersonId)
                && records.TryGetValue(Tuple.Create(sourcePersonId, targetPersonId), out state);
        }
        public DirectedRelationshipState GetOrNeutral(string sourcePersonId, string targetPersonId)
            => TryGet(sourcePersonId, targetPersonId, out var state) ? state
                : new DirectedRelationshipState(sourcePersonId, targetPersonId, 0f);
        internal static bool ValidPair(string source, string target)
            => !string.IsNullOrWhiteSpace(source) && !string.IsNullOrWhiteSpace(target)
                && !string.Equals(source, target, StringComparison.Ordinal);
        internal static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        internal void RestoreRecord(DirectedRelationshipState state)
            => records.Add(Tuple.Create(state.SourcePersonId, state.TargetPersonId), state);
        internal void Apply(DirectedRelationshipState state)
            => records[Tuple.Create(state.SourcePersonId, state.TargetPersonId)] = state;
        internal void Clear() { records.Clear(); }
    }
}
