using System;
using System.Collections.Generic;
namespace RRPG.Social
{
    /// <summary>Canonical history. Neither collection nor its immutable records expose a public write path.</summary>
    public sealed class RelationalFactLedger
    {
        private readonly List<RelationalFactRecord> facts = new List<RelationalFactRecord>();
        private readonly Dictionary<string, RelationalFactRecord> occurrences =
            new Dictionary<string, RelationalFactRecord>(StringComparer.Ordinal);
        public IReadOnlyList<RelationalFactRecord> Facts { get; }
        internal RelationalFactLedger() { Facts = facts.AsReadOnly(); }
        public bool ContainsSourceOccurrence(string id) => id != null && occurrences.ContainsKey(id);
        internal bool TryGet(string id, out RelationalFactRecord fact)
        {
            fact = null;
            return id != null && occurrences.TryGetValue(id, out fact);
        }
        internal void Append(RelationalFactRecord fact)
        {
            occurrences.Add(fact.SourceOccurrenceId, fact);
            facts.Add(fact);
        }
        internal void Clear() { occurrences.Clear(); facts.Clear(); }
    }
}
