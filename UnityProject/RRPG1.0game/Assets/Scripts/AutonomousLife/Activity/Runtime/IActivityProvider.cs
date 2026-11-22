using System.Collections.Generic;
namespace RRPG.AutonomousActivity
{
    /// <summary>Read-only offering contract; WP-AL-2 has no production providers.</summary>
    public interface IActivityProvider
    {
        IEnumerable<ActivityCandidate> CollectCandidates(ActivityDailyWindow window, string actorPersonId);
    }
}
