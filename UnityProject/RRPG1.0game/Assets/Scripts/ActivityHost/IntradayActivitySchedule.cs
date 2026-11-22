using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>Injectable prototype calibration, not a second clock or static-config pipeline.</summary>
public sealed class IntradayActivitySchedule
{
    public IReadOnlyList<int> OpportunityMinutes { get; }
    public int CommuteMinutes { get; }
    public int RepeatOpportunityDelay { get; }
    private readonly IReadOnlyDictionary<string,int> durations;
    public IntradayActivitySchedule(IEnumerable<int> opportunities=null,int commuteMinutes=15,int repeatDelay=30,
        IEnumerable<KeyValuePair<string,int>> activityDurations=null)
    {
        var points=(opportunities ?? new[]{420,1020,1200}).Distinct().OrderBy(x=>x).ToArray();
        if(points.Any(x=>x<0 || x>=1440) || commuteMinutes<1 || repeatDelay<1) throw new ArgumentException("Invalid activity timing");
        OpportunityMinutes=Array.AsReadOnly(points); CommuteMinutes=commuteMinutes; RepeatOpportunityDelay=repeatDelay;
        var values=new Dictionary<string,int>(StringComparer.Ordinal);
        foreach(var pair in activityDurations ?? new[]{new KeyValuePair<string,int>("Rest",60),
            new KeyValuePair<string,int>("Drink",60),new KeyValuePair<string,int>("Talk",60),
            new KeyValuePair<string,int>("VisitSick",90),new KeyValuePair<string,int>("Help",90)})
        {
            if(string.IsNullOrWhiteSpace(pair.Key) || pair.Value<1 || values.ContainsKey(pair.Key))
                throw new ArgumentException("Invalid activity duration");
            values.Add(pair.Key,pair.Value);
        }
        durations=new System.Collections.ObjectModel.ReadOnlyDictionary<string,int>(values);
    }
    public int Duration(string activityType) => durations.TryGetValue(activityType,out int value) ? value
        : throw new ArgumentException("Missing activity duration: "+activityType);
}
