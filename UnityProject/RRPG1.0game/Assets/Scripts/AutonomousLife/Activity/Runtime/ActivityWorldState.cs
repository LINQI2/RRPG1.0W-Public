using System;
using System.Collections.Generic;
namespace RRPG.AutonomousActivity
{
    public sealed class ActivityWorldState
    {
        internal readonly Dictionary<string, PersonActivityState> Persons = new Dictionary<string, PersonActivityState>(StringComparer.Ordinal);
        internal readonly Dictionary<string, ActivityInstance> Instances = new Dictionary<string, ActivityInstance>(StringComparer.Ordinal);
        internal ActivityDailyWindow Window;
        public ActivityClaimRegistry Claims { get; } = new ActivityClaimRegistry();
        public int PersonStateCount => Persons.Count;
        public int InstanceCount => Instances.Count;
        public ActivityDailyWindow QueryCurrentWindow() => Window;
        public PersonActivityState QueryPersonActivityState(string id)
            => id != null && Persons.TryGetValue(id, out var state) ? state : new PersonActivityState(id);
        public ActivityInstance QueryActivityInstance(string id)
            => id != null && Instances.TryGetValue(id, out var instance) ? instance : null;
        public string QueryClaim(string personId) => Claims.QueryClaim(personId);
        public IReadOnlyList<ActivityInstance> QueryActiveInstances()
        {
            var rows = new List<ActivityInstance>();
            foreach (var instance in Instances.Values)
                if (instance.State == ActivityLifecycleState.Committed || instance.State == ActivityLifecycleState.Executing)
                    rows.Add(instance);
            rows.Sort((a,b) => StringComparer.Ordinal.Compare(a.ActivityInstanceId,b.ActivityInstanceId));
            return rows.AsReadOnly();
        }
        public IReadOnlyList<PersonActivityState> QueryPersonStates()
        {
            var rows = new List<PersonActivityState>(Persons.Values);
            rows.Sort((a,b) => StringComparer.Ordinal.Compare(a.PersonId,b.PersonId));
            return rows.AsReadOnly();
        }
        internal void ClearTransient() { Window = null; Claims.Clear(); Instances.Clear(); }
    }
}
