using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// PersonDefinition authoring 容器（ScriptableObject）。
///
/// 职责：把一组 PersonDefinition 作为可序列化/可配置资产组织在一起，并提供
///   - 初始名册解析（InitialRosterOrder &gt; 0，按升序；代码不得写死初始人数或固定名单）；
///   - 按 PersonDefinitionId 查询。
///
/// 唯一数据来源：Development/Excel/Person.csv → ExcelImporter → 本 Catalog 资产
/// （沿用既有 CSV → SO 管线，不新建第二套配置系统；本类不含任何运行时状态）。
/// </summary>
[CreateAssetMenu(fileName = "NewPersonDefinitionCatalog", menuName = "RRPG/人物/人物定义目录")]
public class PersonDefinitionCatalog : ScriptableObject
{
    [Tooltip("固定初始人物定义列表（同一 PersonDefinitionId 只允许出现一次；初始名册顺序不允许重复）")]
    public List<PersonDefinition> Definitions = new List<PersonDefinition>();

    /// <summary>
    /// 解析 New Game 初始名册：InitialRosterOrder &gt; 0 的定义，按 InitialRosterOrder 升序；
    /// 顺序相同（非法配置）时按 PersonDefinitionId 兜底稳定排序，不吞掉非法状态（由 Validator/Importer 报错）。
    /// </summary>
    public List<PersonDefinition> BuildInitialRoster()
    {
        return BuildInitialRoster(Definitions);
    }

    /// <summary>静态版初始名册解析（供 DataManager 合并多 Catalog 后统一排序使用）。</summary>
    public static List<PersonDefinition> BuildInitialRoster(IEnumerable<PersonDefinition> definitions)
    {
        var roster = new List<PersonDefinition>();
        if (definitions == null) return roster;

        foreach (var d in definitions)
        {
            if (d == null || !d.IsInInitialRoster) continue;
            roster.Add(d);
        }

        roster.Sort((a, b) =>
        {
            int byOrder = a.InitialRosterOrder.CompareTo(b.InitialRosterOrder);
            if (byOrder != 0) return byOrder;
            return string.CompareOrdinal(a.PersonDefinitionId ?? string.Empty, b.PersonDefinitionId ?? string.Empty);
        });
        return roster;
    }

    /// <summary>按 PersonDefinitionId 查询（不存在返回 null；不猜、不取首个近似项）。</summary>
    public PersonDefinition FindById(string personDefinitionId)
    {
        if (string.IsNullOrEmpty(personDefinitionId) || Definitions == null) return null;
        foreach (var d in Definitions)
        {
            if (d != null && d.PersonDefinitionId == personDefinitionId) return d;
        }
        return null;
    }
}
