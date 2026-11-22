using System.Collections.Generic;

/// <summary>World Place 类别（候选命名；Test / Not Frozen）。Workplace 派生自岗位目录；ProjectSite 由 Project 完成点单向追加。</summary>
public enum VillagePlaceKind
{
    Workplace = 0,
    ProjectSite = 1,
    Construction = 2
}

/// <summary>运行时 Place 记录（Village.Runtime 纯数据）。Workplace 不落档；ProjectSite 追加式落档。</summary>
[System.Serializable]
public class VillagePlaceRecord
{
    public string PlaceId;
    public VillagePlaceKind Kind;
    public string DisplayName; // Test / Not Frozen 文案
    public string SourceRef;   // Workplace → WorkplaceId；ProjectSite → ProjectDefId
}

/// <summary>
/// VillageWorldRegistry —— 第一包最小 Place 注册表（纯逻辑）。
/// 职责（Village-GameSpec-V0.1 第 11 章附录）：Workplace 幂等派生登记；ProjectSite 只在完成点后单向追加；
/// Save 只存不可派生最小状态（ProjectSite），旧档缺失 → 空默认，不提升 SaveVersion。
/// 不负责 Grid/坐标/碰撞/导航/建筑系统/正式地图（均未冻结）。
/// </summary>
public class VillageWorldRegistry
{
    private readonly List<VillagePlaceRecord> _places = new List<VillagePlaceRecord>();

    public IReadOnlyList<VillagePlaceRecord> Places => _places;

    public static string BuildProjectSitePlaceId(string projectId) => "site_" + projectId;

    public bool ContainsPlace(string placeId) => FindPlace(placeId) != null;

    public VillagePlaceRecord GetPlace(string placeId) => FindPlace(placeId);
    public void EnsureConstructionPlace(string buildingId,string displayName)
    {
        string id="construction:"+buildingId;
        if(FindPlace(id)==null) _places.Add(new VillagePlaceRecord {PlaceId=id,Kind=VillagePlaceKind.Construction,
            DisplayName=displayName+" · 施工",SourceRef=buildingId});
    }
    public void RetireConstructionPlace(string buildingId) => _places.RemoveAll(p=>p.PlaceId=="construction:"+buildingId && p.Kind==VillagePlaceKind.Construction);

    public VillagePlaceRecord EnsureWorkplacePlace(string workplaceId, string displayName)
    {
        if (string.IsNullOrEmpty(workplaceId)) return null;
        var existing = FindPlace(workplaceId);
        if (existing != null) return existing;

        var record = new VillagePlaceRecord
        {
            PlaceId = workplaceId,
            Kind = VillagePlaceKind.Workplace,
            DisplayName = string.IsNullOrEmpty(displayName) ? workplaceId : displayName,
            SourceRef = workplaceId
        };
        _places.Add(record);
        return record;
    }

    public void EnsureWorkplacePlaces(IReadOnlyList<VillageWorkplaceDef> defs)
    {
        if (defs == null) return;
        foreach (var def in defs)
        {
            if (def == null) continue;
            EnsureWorkplacePlace(def.WorkplaceId, def.DisplayName);
        }
    }

    /// <summary>只在 Project 完成点调用：单向追加 ProjectSite（同 Project 幂等，不重复追加）。</summary>
    public VillagePlaceRecord AppendCompletedProjectSite(string projectId, string projectDefId, string displayName)
    {
        if (string.IsNullOrEmpty(projectId)) return null;
        string placeId = BuildProjectSitePlaceId(projectId);
        var existing = FindPlace(placeId);
        if (existing != null) return existing;

        var record = new VillagePlaceRecord
        {
            PlaceId = placeId,
            Kind = VillagePlaceKind.ProjectSite,
            DisplayName = string.IsNullOrEmpty(displayName) ? projectDefId : displayName,
            SourceRef = projectDefId
        };
        _places.Add(record);
        return record;
    }

    public int CountPlacesOfKind(VillagePlaceKind kind)
    {
        int count = 0;
        foreach (var p in _places)
        {
            if (p != null && p.Kind == kind) count++;
        }
        return count;
    }

    /// <summary>存档快照：只保存 ProjectSite（不可派生最小状态）；Workplace 派生不落档。</summary>
    public VillageWorldSaveData ToSaveData()
    {
        var data = new VillageWorldSaveData { Places = new List<VillageWorldPlaceSaveData>() };
        foreach (var p in _places)
        {
            if (p == null || p.Kind != VillagePlaceKind.ProjectSite) continue;
            data.Places.Add(new VillageWorldPlaceSaveData
            {
                Kind = (int)VillagePlaceKind.ProjectSite,
                PlaceId = p.PlaceId,
                DisplayName = p.DisplayName,
                SourceRef = p.SourceRef
            });
        }
        return data;
    }

    /// <summary>恢复 ProjectSite（旧档缺失/空区块 → 空默认，保留派生 Workplace）；防御：空/重复/未知 Kind 丢弃。</summary>
    public void Restore(VillageWorldSaveData data)
    {
        if (data == null || data.Places == null) return;
        foreach (var s in data.Places)
        {
            if (s == null || string.IsNullOrEmpty(s.PlaceId)) continue;
            if (ContainsPlace(s.PlaceId)) continue;
            if (s.Kind != (int)VillagePlaceKind.Workplace && s.Kind != (int)VillagePlaceKind.ProjectSite) continue;

            _places.Add(new VillagePlaceRecord
            {
                PlaceId = s.PlaceId,
                Kind = s.Kind == (int)VillagePlaceKind.ProjectSite ? VillagePlaceKind.ProjectSite : VillagePlaceKind.Workplace,
                DisplayName = string.IsNullOrEmpty(s.DisplayName) ? s.PlaceId : s.DisplayName,
                SourceRef = string.IsNullOrEmpty(s.SourceRef) ? s.PlaceId : s.SourceRef
            });
        }
    }

    public void Reset() => _places.Clear();

    private VillagePlaceRecord FindPlace(string placeId)
    {
        if (string.IsNullOrEmpty(placeId)) return null;
        foreach (var p in _places)
        {
            if (p != null && p.PlaceId == placeId) return p;
        }
        return null;
    }
}
