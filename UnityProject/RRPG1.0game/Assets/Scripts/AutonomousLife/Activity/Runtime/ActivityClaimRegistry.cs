using System;
using System.Collections.Generic;
namespace RRPG.AutonomousActivity
{
    public sealed class ActivityClaimRegistry
    {
        private readonly Dictionary<string, string> claims = new Dictionary<string, string>(StringComparer.Ordinal);
        public int Count => claims.Count;
        public string QueryClaim(string personId)
            => personId != null && claims.TryGetValue(personId, out var id) ? id : null;
        internal void Acquire(IEnumerable<string> people, string id) { foreach (var p in people) claims.Add(p, id); }
        internal void Release(IEnumerable<string> people) { foreach (var p in people) claims.Remove(p); }
        internal void Clear() => claims.Clear();
    }
}
