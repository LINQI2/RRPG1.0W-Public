using System.Collections.Generic;

/// <summary>
/// Person Availability —— 人物当前 Village 可用状态（Village.Runtime 纯逻辑）。
///
/// V0.1 最低语义（Village-GameSpec-V0.1 §2.3）：
///   - InVillage：在村庄、当前可安排；
///   - Expedition：正在远征（运行时从当前远征登记重建，不写入长期人物字段）；
///   - Unavailable：暂不可用（V0.1 仅覆盖「已死亡」；Assignment / Project 等占用由 Stage V2 接入）。
///
/// 本类只做「人物 → 可用状态」的纯逻辑映射，不创建第二套 Person 真相源；
/// 人物身份/成长/健康一律沿用既有长期人物真相源（PlayerCollection 中的 CharacterInstance / HeroInstance）。
/// </summary>
public enum VillageAvailability
{
    InVillage,
    Expedition,
    Unavailable
}

/// <summary>Village 对长期人物的只读视图输入（只取判可用所需字段，不复制长期数据本体）。</summary>
public class VillagePersonInput
{
    public string PersonId;
    public string DisplayName;

    /// <summary>是否已死亡（长期真相；角色可死亡，英雄恒 false）。</summary>
    public bool IsDead;
}

/// <summary>Village 对一名人物的可用状态视图。</summary>
public class VillagePersonView
{
    public string PersonId;
    public string DisplayName;
    public VillageAvailability Availability;
}

/// <summary>
/// 人物可用状态解析（纯逻辑）。
/// 规则：远征集合中的人物 → Expedition（绝不同时 InVillage）；已死亡 → Unavailable；否则 InVillage。
/// </summary>
public static class VillagePersonAvailability
{
    public static List<VillagePersonView> Resolve(
        IEnumerable<VillagePersonInput> persons,
        IReadOnlyCollection<string> expeditionPersonIds)
    {
        var result = new List<VillagePersonView>();
        if (persons == null) return result;

        var expedition = new HashSet<string>();
        if (expeditionPersonIds != null)
        {
            foreach (var id in expeditionPersonIds)
            {
                if (!string.IsNullOrEmpty(id)) expedition.Add(id);
            }
        }

        foreach (var p in persons)
        {
            if (p == null || string.IsNullOrEmpty(p.PersonId)) continue;

            var view = new VillagePersonView
            {
                PersonId = p.PersonId,
                DisplayName = string.IsNullOrEmpty(p.DisplayName) ? p.PersonId : p.DisplayName
            };

            if (expedition.Contains(p.PersonId))
                view.Availability = VillageAvailability.Expedition;
            else if (p.IsDead)
                view.Availability = VillageAvailability.Unavailable;
            else
                view.Availability = VillageAvailability.InVillage;

            result.Add(view);
        }

        return result;
    }
}
