using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>Transient read projection. Contains no Unity objects, simulation references or save state.</summary>
public sealed class ProductHomeSnapshot
{
    public IReadOnlyList<HomePersonSnapshot> Persons { get; }
    public IReadOnlyList<HomePlaceSnapshot> Places { get; }
    public IReadOnlyList<string> Diagnostics { get; }
    public IReadOnlyList<HomeBuildingSnapshot> Buildings { get; }
    public ProductHomeSnapshot(IEnumerable<HomePersonSnapshot> persons, IEnumerable<HomePlaceSnapshot> places, IEnumerable<string> diagnostics, IEnumerable<HomeBuildingSnapshot> buildings=null)
    {
        Persons = Array.AsReadOnly(persons.ToArray());
        Places = Array.AsReadOnly(places.ToArray());
        Diagnostics = Array.AsReadOnly(diagnostics.ToArray());
        Buildings=Array.AsReadOnly((buildings ?? Enumerable.Empty<HomeBuildingSnapshot>()).ToArray());
    }
    public HomePersonSnapshot Person(string id) => Persons.FirstOrDefault(p => p.PersonId == id);
    public HomePlaceSnapshot Place(string id) => Places.FirstOrDefault(p => p.PlaceId == id);
    public HomeBuildingSnapshot Building(string id) => Buildings.FirstOrDefault(b=>b.InstanceId==id);
}

public sealed class HomeBuildingSnapshot
{
    public string InstanceId { get; }
    public string DefinitionId { get; }
    public string DisplayName { get; }
    public float WorldX { get; }
    public float WorldY { get; }
    public int Width { get; }
    public int Depth { get; }
    public BuildingLifecycleState LifecycleState { get; }
    public HomeBuildingSnapshot(string id,string definition,string name,float x,float y,int width,int depth,BuildingLifecycleState state)
    {InstanceId=id;DefinitionId=definition;DisplayName=name;WorldX=x;WorldY=y;Width=width;Depth=depth;LifecycleState=state;}
}

public sealed class HomePersonSnapshot
{
    public string PersonId { get; }
    public string DisplayName { get; }
    public string Profession { get; }
    public string SimulationPresence { get; }
    public string CurrentPlaceId { get; }
    public string DestinationPlaceId { get; }
    public string AssignmentId { get; }
    public string AssignmentSummary { get; }
    public VillageAvailability Availability { get; }
    public string OccupancySummary { get; }
    public string ActivitySummary { get; }
    public string PresentationIntent { get; }
    public bool Away { get; }
    public bool Traveling => !Away && string.IsNullOrEmpty(CurrentPlaceId) && !string.IsNullOrEmpty(DestinationPlaceId);
    public HomePersonSnapshot(string id, string name, string profession, string presence, string place, string destination,
        string assignmentId, string assignmentSummary, VillageAvailability availability, string occupancy, string activity, bool away)
    {
        PersonId=id; DisplayName=name; Profession=profession; SimulationPresence=presence;
        CurrentPlaceId=place; DestinationPlaceId=destination; AssignmentId=assignmentId;
        AssignmentSummary=assignmentSummary; Availability=availability; OccupancySummary=occupancy;
        ActivitySummary=activity; Away=away;
        PresentationIntent=away ? "away" : Traveling ? "traveling" : "onsite";
    }
}

public sealed class HomePlaceSnapshot
{
    public string PlaceId { get; }
    public string DisplayName { get; }
    public string Kind { get; }
    public string Source { get; }
    public int? Capacity { get; }
    public int Occupancy => AssignedPersonIds.Count;
    public IReadOnlyList<string> AssignedPersonIds { get; }
    public IReadOnlyList<string> OccupantPersonIds { get; }
    public string Status { get; }
    public bool IsFallback { get; }
    public HomePlaceSnapshot(string id, string name, string kind, string source, int? capacity,
        IEnumerable<string> assigned, IEnumerable<string> occupants, string status, bool fallback)
    {
        PlaceId=id; DisplayName=name; Kind=kind; Source=source; Capacity=capacity;
        AssignedPersonIds=Array.AsReadOnly(assigned.ToArray()); OccupantPersonIds=Array.AsReadOnly(occupants.ToArray());
        Status=status; IsFallback=fallback;
    }
}

public enum HomeSelectionKind { None, Person, Place, Building }

/// <summary>Shared by world clicks and future lists. Never owns domain state.</summary>
public sealed class HomeSelectionContext
{
    public HomeSelectionKind Kind { get; private set; }
    public string Id { get; private set; }
    public event Action Changed;
    public void SelectPerson(string id) => Select(HomeSelectionKind.Person,id);
    public void SelectPlace(string id) => Select(HomeSelectionKind.Place,id);
    public void SelectBuilding(string id) => Select(HomeSelectionKind.Building,id);
    public void Clear() => Select(HomeSelectionKind.None,null);
    private void Select(HomeSelectionKind kind,string id)
    {
        if (string.IsNullOrEmpty(id)) { kind=HomeSelectionKind.None; id=null; }
        if (Kind==kind && Id==id) return;
        Kind=kind; Id=id; Changed?.Invoke();
    }
}
