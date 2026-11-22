namespace RRPG.Social
{
    /// <summary>Interpreter proposal; Affinity is a V0.1 Test / Not Frozen channel.</summary>
    public sealed class RelationshipMutation
    {
        public string SourcePersonId { get; }
        public string TargetPersonId { get; }
        public float AffinityDelta { get; }
        public RelationshipMutation(string sourcePersonId, string targetPersonId, float affinityDelta)
        {
            SourcePersonId = sourcePersonId; TargetPersonId = targetPersonId; AffinityDelta = affinityDelta;
        }
    }
}
