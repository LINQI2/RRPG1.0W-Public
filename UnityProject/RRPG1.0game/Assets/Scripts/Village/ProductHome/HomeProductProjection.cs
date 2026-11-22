using System;
using System.Collections.Generic;
using System.Linq;
using RRPG.AutonomousActivity;

public enum HomeSurface { World, Person, Management, Cards, History }
public enum HomeAttentionIntent { PersonGrowth, PendingEvent }

/// <summary>Transient navigation, never controls simulation gating or the clock.</summary>
public sealed class HomeProductRouter
{
    public HomeSurface Surface { get; private set; }
    public bool BuildAvailable => true;
    public void Open(HomeSurface surface) { Surface=surface; }
    public void Close() { Surface=HomeSurface.World; }
}

public sealed class HomeAttentionItem
{
    public string SubjectId { get; }
    public string Subject { get; }
    public string Type { get; }
    public string Message { get; }
    public string Target { get; }
    public HomeAttentionIntent Intent { get; }
    public HomeAttentionItem(string id,string subject,string type,string message,string target,HomeAttentionIntent intent)
    { SubjectId=id;Subject=subject;Type=type;Message=message;Target=target;Intent=intent; }
}

/// <summary>Only existing domain action queries; no forecasts, thresholds, history or UI red dots.</summary>
public static class HomeAttentionProjection
{
    public static IReadOnlyList<HomeAttentionItem> Read()
    {
        var source=VillageRuntimeBridge.SimulationPersonSource;
        var result=new List<HomeAttentionItem>();
        if (source==null) return result.AsReadOnly();
        var persons=new List<IPersonGrowth>();
        if (source.OwnedHeroes!=null) persons.AddRange(source.OwnedHeroes.Where(p=>p!=null));
        if (source.OwnedCharacters!=null) persons.AddRange(source.OwnedCharacters.Where(p=>p!=null && !p.IsDead));
        foreach (var p in persons.OrderBy(p=>p.PersonId,StringComparer.Ordinal))
            if (CharacterGrowthService.CanLevelUp(p,GrowthConfigProvider.Curve))
                result.Add(new HomeAttentionItem(p.PersonId,p.DisplayName,"GrowthOpportunity","可进行职业成长",p.PersonId,HomeAttentionIntent.PersonGrowth));
        var pending=EventRuntimeBridge.PendingVillageEvent;
        if (pending!=null) result.Add(new HomeAttentionItem(pending.EventInstanceId,"村庄事件","PendingEvent","有待处理的村庄事件",pending.EventInstanceId,HomeAttentionIntent.PendingEvent));
        return result.AsReadOnly();
    }
}

public sealed class HomeFeedEntry
{
    public string OccurrenceId { get; }
    public string SubjectId { get; }
    public string Message { get; }
    public HomeFeedEntry(string occurrence,string subject,string message)
    { OccurrenceId=occurrence;SubjectId=subject;Message=message; }
}

/// <summary>Bounded session observer. Never reads or writes any persistent history.</summary>
public sealed class HomeVillageFeed : IDisposable
{
    public const int Capacity=3;
    private readonly List<HomeFeedEntry> _recent=new List<HomeFeedEntry>();
    private readonly HashSet<string> _seen=new HashSet<string>(StringComparer.Ordinal);
    public IReadOnlyList<HomeFeedEntry> Recent => _recent.AsReadOnly();
    public HomeVillageFeed() { ActivityRuntimeBridge.CompletedOccurrence+=OnActivity; }
    public void Observe(HomeFeedEntry entry)
    {
        if (entry==null || string.IsNullOrEmpty(entry.OccurrenceId) || !_seen.Add(entry.OccurrenceId)) return;
        _recent.Insert(0,entry);
        if (_recent.Count>Capacity) _recent.RemoveAt(_recent.Count-1);
    }
    private void OnActivity(ActivityCompletionSnapshot occurrence)
    {
        var source=VillageRuntimeBridge.SimulationPersonSource;
        var person=source?.GetCharacterByPersonId(occurrence.ActorPersonId);
        string name=person?.DisplayName ?? occurrence.ActorPersonId;
        string action=occurrence.ActivityType;
        switch(action) { case "Talk":action="交谈";break;case "Rest":action="休息";break;case "Drink":action="小酌";break;case "Help":action="帮忙";break;case "VisitSick":action="探望";break; }
        Observe(new HomeFeedEntry("activity:"+occurrence.ActivityInstanceId,occurrence.ActorPersonId,name+"结束了"+action));
    }
    public void ResetSession() { _recent.Clear();_seen.Clear(); }
    public void Dispose() { ActivityRuntimeBridge.CompletedOccurrence-=OnActivity;ResetSession(); }
}
