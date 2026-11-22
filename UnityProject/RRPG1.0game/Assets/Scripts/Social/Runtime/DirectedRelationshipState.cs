namespace RRPG.Social
{
    /// <summary>Immutable query snapshot. Affinity is a V0.1 Test / Not Frozen channel.</summary>
    public sealed class DirectedRelationshipState
    {
        public string SourcePersonId { get; }
        public string TargetPersonId { get; }
        public float Affinity { get; }
        internal DirectedRelationshipState(string sourcePersonId, string targetPersonId, float affinity)
        {
            SourcePersonId = sourcePersonId; TargetPersonId = targetPersonId; Affinity = affinity;
        }
    }
}
