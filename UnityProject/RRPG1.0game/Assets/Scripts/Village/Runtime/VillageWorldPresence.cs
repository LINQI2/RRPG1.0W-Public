using System.Collections.Generic;

/// <summary>
/// Person 在当前 World 中的在场状态（Presence 派生结果；只从现有真相源派生，不建第二套 occupancy）。
/// </summary>
public enum VillagePresenceState
{
    /// <summary>离开村庄 / 暂不可用（远征、死亡、Unavailable 等），无在场 Place。</summary>
    Away,

    /// <summary>在村且无 Assignment / Active Project 占用 → 村庄公共区。</summary>
    Free,

    /// <summary>正在按 Primary Assignment 于对应 Workplace 执行（PlaceId = WorkplaceId）。</summary>
    Workplace,

    /// <summary>正投入 Active Project / 已在完成工地（PlaceId = site_&lt;ProjectId&gt;）。</summary>
    ProjectSite
}

/// <summary>Person Presence 派生快照（纯数据；PlaceId 仅在 Workplace / ProjectSite 状态下非空）。</summary>
public struct VillagePersonPresence
{
    public string PersonId;
    public VillagePresenceState State;
    public string PlaceId;
}

/// <summary>
/// VillageWorldPresence —— Presence 纯派生解析器（Village.Runtime）。
///
/// 输入全部来自既有真相源（Expedition / Unavailable 登记、Active Project、Primary Assignment），
/// 本类不做任何持久化，也不持有状态；World 表现层/桥接层只调用本解析得到「谁现在在哪」。
/// 优先级：Away（远征/不可用）→ ProjectSite（Temporary Occupancy，暂停 Assignment）→ Workplace（Primary Assignment）→ Free。
/// </summary>
public static class VillageWorldPresence
{
    public static VillagePersonPresence Resolve(
        string personId,
        IReadOnlyCollection<string> expeditionPersonIds,
        IReadOnlyCollection<string> unavailablePersonIds,
        string activeProjectId,
        string workplaceId)
    {
        var result = new VillagePersonPresence { PersonId = personId, State = VillagePresenceState.Free, PlaceId = null };

        if (Contains(expeditionPersonIds, personId) || Contains(unavailablePersonIds, personId))
        {
            result.State = VillagePresenceState.Away;
            return result;
        }

        if (!string.IsNullOrEmpty(activeProjectId))
        {
            result.State = VillagePresenceState.ProjectSite;
            result.PlaceId = VillageWorldRegistry.BuildProjectSitePlaceId(activeProjectId);
            return result;
        }

        if (!string.IsNullOrEmpty(workplaceId))
        {
            result.State = VillagePresenceState.Workplace;
            result.PlaceId = workplaceId;
            return result;
        }

        return result;
    }

    private static bool Contains(IReadOnlyCollection<string> ids, string personId)
    {
        if (ids == null || string.IsNullOrEmpty(personId)) return false;
        foreach (var id in ids)
        {
            if (id == personId) return true;
        }
        return false;
    }
}
