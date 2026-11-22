using System;
using System.Collections.Generic;

/// <summary>
/// M2 Profession Template 查询能力：对"已通过合法性校验"的定义集合提供
/// 按 ProfessionId 稳定取得唯一 ProfessionDefinition 的能力。
///
/// 唯一性保证方式：
///   本类只能通过 Build / BuildFromCatalog 构造；构造时对全部定义执行严格校验。
///   若集合存在 duplicate ProfessionId 或任何非法定义，构造直接抛出
///   ProfessionDefinitionValidationException —— 绝不允许"取第一个然后继续"。
///   因此成功构造的 Registry 内部必然满足 ProfessionId 一对一。
///
/// 查询契约：
///   GetById(null/empty) → ArgumentException（显式失败）
///   GetById(不存在)     → KeyNotFoundException（显式失败）
///   TryGetById(不存在)  → false，out null
///
/// 不承载任何 Person 动态状态；Person 未来的动态 UnlockedPool（M3）只存在于人物自身。
/// </summary>
public sealed class ProfessionDefinitionRegistry
{
    private readonly IReadOnlyList<ProfessionDefinition> _definitions;
    private readonly Dictionary<string, ProfessionDefinition> _byId;

    private ProfessionDefinitionRegistry(
        IReadOnlyList<ProfessionDefinition> definitions,
        Dictionary<string, ProfessionDefinition> byId)
    {
        _definitions = definitions;
        _byId = byId;
    }

    /// <summary>从定义集合构造 Registry；非法（含 duplicate ProfessionId）时抛 ProfessionDefinitionValidationException。</summary>
    public static ProfessionDefinitionRegistry Build(IEnumerable<ProfessionDefinition> definitions)
    {
        if (definitions == null)
            throw new ArgumentNullException(nameof(definitions), "ProfessionDefinition 集合不能为 null");

        // 先物化再做全量校验：config 规模极小，物化成本可忽略。
        var materialized = new List<ProfessionDefinition>();
        foreach (ProfessionDefinition d in definitions)
            materialized.Add(d);

        if (!ProfessionDefinitionValidator.TryValidateAll(materialized, out List<string> errors))
            throw new ProfessionDefinitionValidationException(errors);

        var byId = new Dictionary<string, ProfessionDefinition>(materialized.Count);
        foreach (ProfessionDefinition d in materialized)
            byId[d.ProfessionId] = d;

        return new ProfessionDefinitionRegistry(materialized.AsReadOnly(), byId);
    }

    /// <summary>定义数量（同一 ProfessionId 只出现一次）。</summary>
    public int Count => _byId.Count;

    /// <summary>全部已校验定义（只读快照；模板共享，不承载任何 Person 动态状态）。</summary>
    public IReadOnlyList<ProfessionDefinition> AllDefinitions => _definitions;

    /// <summary>全部 ProfessionId（唯一）。</summary>
    public IReadOnlyCollection<string> ProfessionIds => _byId.Keys;

    /// <summary>按 ProfessionId 查询；成功返回 true。null/empty id 或不存在均返回 false（不抛异常）。</summary>
    public bool TryGetById(string professionId, out ProfessionDefinition definition)
    {
        if (string.IsNullOrEmpty(professionId))
        {
            definition = null;
            return false;
        }
        return _byId.TryGetValue(professionId, out definition);
    }

    /// <summary>
    /// 按 ProfessionId 查询；成功返回对应定义。
    /// null/empty ProfessionId → ArgumentException；不存在 → KeyNotFoundException。
    /// </summary>
    public ProfessionDefinition GetById(string professionId)
    {
        if (string.IsNullOrEmpty(professionId))
            throw new ArgumentException("ProfessionId 不能为 null/empty", nameof(professionId));

        if (!_byId.TryGetValue(professionId, out ProfessionDefinition definition))
            throw new KeyNotFoundException("不存在 ProfessionId：" + professionId);

        return definition;
    }
}
