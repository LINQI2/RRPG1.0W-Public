using System;
using System.Collections.Generic;
using RRPG.AutonomousActivity;
using RRPG.Social;

namespace RRPG.AutonomousSocialFeedback
{
    public enum ProjectionResult { Prepared, NoFeedback, InvalidProjection }
    public sealed class PreparedSocialProjection
    {
        public ProjectionResult Result { get; }
        public RelationalFactRecord Fact { get; }
        public string Diagnostic { get; }
        internal PreparedSocialProjection(ProjectionResult result, RelationalFactRecord fact = null, string diagnostic = "")
        { Result = result; Fact = fact; Diagnostic = diagnostic; }
    }
    public sealed class ActivitySocialFactProjector
    {
        private readonly Dictionary<string, string> mapping = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly SocialRuleCatalog rules;
        public ActivitySocialFactProjector(IEnumerable<KeyValuePair<string, string>> projections, SocialRuleCatalog rules)
        {
            this.rules = rules ?? throw new ArgumentNullException(nameof(rules));
            foreach (var p in projections ?? throw new ArgumentNullException(nameof(projections)))
            {
                if (string.IsNullOrWhiteSpace(p.Key) || string.IsNullOrWhiteSpace(p.Value) || mapping.ContainsKey(p.Key) || !rules.CanInterpret(p.Value))
                    throw new ArgumentException("Invalid activity social mapping");
                mapping.Add(p.Key, p.Value);
            }
        }
        public PreparedSocialProjection Prepare(ActivityInstance instance, ActivityDayStamp? completionDay = null)
        {
            if (instance == null || instance.State != ActivityLifecycleState.Executing)
                return new PreparedSocialProjection(ProjectionResult.InvalidProjection, diagnostic: "ActivityNotExecuting");
            if (!mapping.TryGetValue(instance.Candidate.ActivityType, out var factType))
                return new PreparedSocialProjection(ProjectionResult.NoFeedback);
            var d = completionDay ?? instance.DayStamp;
            var fact = new RelationalFactRecord(instance.MemberResultId, factType,
                instance.Candidate.ActorPersonId, instance.Candidate.TargetPersonId, d.Year, d.Month, d.Day);
            return rules.CanInterpret(fact)
                ? new PreparedSocialProjection(ProjectionResult.Prepared, fact)
                : new PreparedSocialProjection(ProjectionResult.InvalidProjection, diagnostic: "InvalidFactOrRule");
        }
    }
}
