using System.Collections.Generic;
using System.Linq;

/// <summary>Composes existing queries only. Does not initialize, synchronize or write any authority.</summary>
public static class ProductHomeReadAdapter
{
    public static ProductHomeSnapshot Read()
    {
        var people=new List<HomePersonSnapshot>();
        var places=new List<HomePlaceSnapshot>();
        var diagnostics=new List<string>();
        var source=VillageRuntimeBridge.SimulationPersonSource;
        if (source==null || VillageRuntimeBridge.Time==null || VillageRuntimeBridge.IntradayWork==null)
            return new ProductHomeSnapshot(people,places,new[]{"Village source not ready"});
        foreach (var person in VillagePersonAvailability.Resolve(VillageRuntimeBridge.BuildPersonInputs(source),VillageRuntimeBridge.ExpeditionPersonIds))
        {
            var actual=VillageRuntimeBridge.QueryIntradayPresence(person.PersonId);
            var assignment=VillageRuntimeBridge.Assignments?.GetWorkplaceId(person.PersonId);
            var def=VillageRuntimeBridge.Assignments?.GetWorkplaceDef(assignment);
            var character=source.GetCharacterByPersonId(person.PersonId);
            string profession=character==null ? "—" : character.ActiveProfessionProgress?.ProfessionId ?? "未形成职业";
            string occupancy=VillageRuntimeBridge.GetPersonOccupancyReason(person.PersonId);
            bool away=person.Availability!=VillageAvailability.InVillage || actual.State=="离村 / 不可用";
            people.Add(new HomePersonSnapshot(person.PersonId,person.DisplayName,profession,actual.State,actual.PlaceId,
                actual.DestinationPlaceId,assignment,def?.DisplayName ?? assignment ?? "无职责",person.Availability,
                occupancy,actual.State,away));
        }
        var registry=VillageRuntimeBridge.World;
        if (registry!=null)
            foreach (var place in registry.Places)
            {
                var def=place.Kind==VillagePlaceKind.Workplace ? VillageRuntimeBridge.Assignments?.GetWorkplaceDef(place.PlaceId) : null;
                var building=place.Kind==VillagePlaceKind.Construction ? VillageRuntimeBridge.Buildings.Find(place.SourceRef) : null;
                var construction=building==null ? null : VillageRuntimeBridge.Projects?.GetActiveProject(building.ConstructionProjectId);
                places.Add(new HomePlaceSnapshot(place.PlaceId,place.DisplayName,place.Kind.ToString(),place.SourceRef,
                    def==null ? (place.Kind==VillagePlaceKind.Construction ? 2 : (int?)null) : def.Capacity,
                    def==null ? construction?.ParticipantPersonIds ?? new List<string>() : VillageRuntimeBridge.Assignments.GetOccupantPersonIds(place.PlaceId),
                    people.Where(p=>!p.Away && p.CurrentPlaceId==place.PlaceId).Select(p=>p.PersonId),
                    place.Kind==VillagePlaceKind.ProjectSite ? "已完成痕迹" : place.Kind==VillagePlaceKind.Construction ? "施工中" : "岗位",false));
            }
        // These are snapshots of referenced facts, not additions to VillageWorldRegistry.
        var referenced=people.SelectMany(p=>new[]{p.CurrentPlaceId,p.DestinationPlaceId,VillageRuntimeBridge.ReceptionPolicy.KnownHome(p.PersonId)})
            .Concat(VillageRuntimeBridge.Projects==null ? new string[0] : VillageRuntimeBridge.Projects.ActiveProjects.Select(p=>VillageRuntimeBridge.ProjectDestination(p.ProjectId)))
            .Where(id=>!string.IsNullOrEmpty(id)).Distinct().OrderBy(id=>id,System.StringComparer.Ordinal);
        foreach (string id in referenced)
        {
            if (places.Any(p=>p.PlaceId==id)) continue;
            var project=VillageRuntimeBridge.Projects?.GetActiveProject(id);
            var def=project==null ? null : VillageRuntimeBridge.Projects.GetProjectDef(project.ProjectDefId);
            bool home=id.StartsWith("Home:",System.StringComparison.Ordinal);
            string name=def?.DisplayName ?? (home ? "住所 · "+(people.FirstOrDefault(p=>"Home:"+p.PersonId==id)?.DisplayName ?? id) : id);
            places.Add(new HomePlaceSnapshot(id,name,project!=null ? "ActiveProject" : home ? "Home" : "ActivityPlace",
                "Actual Presence / Destination",def==null ? (int?)null : def.ParticipantCapacity,
                project==null ? new List<string>() : project.ParticipantPersonIds,
                people.Where(p=>!p.Away && p.CurrentPlaceId==id).Select(p=>p.PersonId),"只读地点投影",true));
            diagnostics.Add("Unregistered presentation anchor: "+id);
        }
        var buildings=new List<HomeBuildingSnapshot>();
        if(RRPG.StaticConfig.StaticConfigRegistry.TryGetBuildings(out var definitions,out var buildingDiagnostic))
            foreach(var b in VillageRuntimeBridge.Buildings.All)
            {
                if(!definitions.TryGetById(b.BuildingDefinitionId,out var d)) {diagnostics.Add("Unknown BuildingDefinitionId: "+b.BuildingDefinitionId);continue;}
                buildings.Add(new HomeBuildingSnapshot(b.InstanceId,b.BuildingDefinitionId,d.DisplayName,b.WorldX,b.WorldY,d.FootprintWidth,d.FootprintDepth,b.LifecycleState));
            }
        else diagnostics.Add(buildingDiagnostic);
        return new ProductHomeSnapshot(people,places,diagnostics,buildings);
    }
}
