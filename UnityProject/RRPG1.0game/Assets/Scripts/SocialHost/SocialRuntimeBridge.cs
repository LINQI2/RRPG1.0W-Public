using System.Collections.Generic;
using System.Linq;
using RRPG.StaticConfig;
using RRPG.Social;

/// <summary>World host adapter. Queries/build never initialize persistent state implicitly.</summary>
public static class SocialRuntimeBridge
{
    private static SocialWorldState current;
    private static readonly IReadOnlyList<RelationalFactRecord> EmptyFacts =
        new List<RelationalFactRecord>().AsReadOnly();

    public static void EnsureInitialized()
    {
        if (current == null) current = new SocialWorldState();
    }

    public static DirectedRelationshipState QueryRelationship(string sourcePersonId, string targetPersonId)
    {
        return current != null ? current.Relationships.GetOrNeutral(sourcePersonId, targetPersonId)
            : new SocialWorldState().Relationships.GetOrNeutral(sourcePersonId, targetPersonId);
    }

    public static IReadOnlyList<RelationalFactRecord> QueryFacts()
        => current != null ? current.Facts.Facts : EmptyFacts;

    public static SocialCommitResult CommitRelationalFact(RelationalFactRecord fact)
    {
        EnsureInitialized();
        // Duplicate acknowledgement remains possible even if authoring is currently unavailable.
        try { return new SocialCommitService(current, new ConfiguredRelationshipInterpreter(BuildProductionRules())).Commit(fact); }
        catch (System.Exception) { return new SocialCommitService(current, null).Commit(fact); }
    }

    public static SocialRuleCatalog BuildProductionRules()
    {
        if (!StaticConfigRegistry.TryGetSocialConfig(out var db, out var diagnostic))
            throw new System.InvalidOperationException(diagnostic);
        return new SocialRuleCatalog(db.RelationalFactType.Select(f => f.FactType),
            db.RelationshipInterpretationRule.Select(r => new RelationshipInterpretationRule(r.RuleId, r.FactType, r.SourceRole, r.TargetRole, r.RelationshipChannel, r.Polarity, r.Strength)),
            db.SocialCalibration.Select(c => new KeyValuePair<string, float>(c.CalibrationKey, c.Value)));
    }

    public static SocialWorldSaveData BuildSaveData() => SocialWorldPersistence.Build(current);

    public static SocialRestoreResult Restore(SocialWorldSaveData data)
    {
        EnsureInitialized();
        return SocialWorldPersistence.Restore(current, data);
    }

    public static void ResetForNewGame() { Restore(null); }
    public static void ResetForTests() { Restore(null); }
}
