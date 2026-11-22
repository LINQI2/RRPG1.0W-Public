namespace RRPG.Social
{
    /// <summary>World-social-domain-owned state, independent of Person/Scene hosts.</summary>
    public sealed class SocialWorldState
    {
        public RelationalFactLedger Facts { get; } = new RelationalFactLedger();
        public DirectedRelationshipRegistry Relationships { get; } = new DirectedRelationshipRegistry();
        internal object SyncRoot { get; } = new object();
        internal bool CommitInProgress { get; set; }
    }
}
