using System;
using System.Collections.Generic;

/// <summary>Prototype defaults; location knowledge is separate from actual presence.</summary>
public sealed class IntradayReceptionPolicy
{
    private readonly Dictionary<string,string> homes;
    public int MinimumParticipationMinutes { get; }
    public int MissedCooldownMinutes { get; }
    public IntradayReceptionPolicy(int minimumParticipationMinutes=30,int missedCooldownMinutes=120,
        IEnumerable<KeyValuePair<string,string>> knownHomes=null)
    {
        if(minimumParticipationMinutes<=0 || missedCooldownMinutes<=0) throw new ArgumentOutOfRangeException();
        MinimumParticipationMinutes=minimumParticipationMinutes; MissedCooldownMinutes=missedCooldownMinutes;
        homes=new Dictionary<string,string>(StringComparer.Ordinal);
        foreach(var pair in knownHomes ?? new KeyValuePair<string,string>[0])
        {
            if(string.IsNullOrWhiteSpace(pair.Key)||string.IsNullOrWhiteSpace(pair.Value)) throw new ArgumentException("Known home");
            homes.Add(pair.Key,pair.Value);
        }
    }
    public string KnownHome(string personId) => homes.TryGetValue(personId,out var place) ? place : "Home:"+personId;
}
