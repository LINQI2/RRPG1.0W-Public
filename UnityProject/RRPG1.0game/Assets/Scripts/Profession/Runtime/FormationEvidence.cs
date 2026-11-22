using System.Collections.Generic;

/// <summary>
/// M6 标准化 Formation Evidence Input（§12.2 / §12.10）。
///
/// Formation Evidence Contribution = 「一天内 / 一次事件内」某 Person 对某 Profession 的
/// 标准化 Evidence 贡献。Day End 由 Formation Resolver 统一汇总当日 Contribution
/// （Evidence → Strength → Candidate → Target → Stability → Formation）。
/// Contribution 只是当日事实输入；本类型不承载任何长期状态。
/// </summary>
public sealed class FormationEvidenceContribution
{
    /// <summary>目标 ProfessionId（必须引用既有 ProfessionDefinition 的语义；未知条目保留原样，不投其它职业）。</summary>
    public string ProfessionId;

    /// <summary>当日 / 当次 Evidence 量（&gt;0；尺度与 Work/Event 映射相关，Test / Not Frozen）。</summary>
    public float Amount;

    /// <summary>Evidence 来源类别（M6 只接入 DailyWork；LifeActivity / Event / LifeDirection 为未来来源占位）。</summary>
    public FormationEvidenceSource Source;

    public FormationEvidenceContribution(string professionId, float amount, FormationEvidenceSource source)
    {
        ProfessionId = professionId;
        Amount = amount;
        Source = source;
    }
}

/// <summary>Formation Evidence 来源类别（不构成正式内容 / 正式数值）。</summary>
public enum FormationEvidenceSource
{
    /// <summary>真实发生的日常有效工作（M6 唯一接入源：Assignment 实际执行 Day Tick）。</summary>
    DailyWork = 0,

    /// <summary>日常生活 Activity（未来来源；M6 不实现）。</summary>
    LifeActivity = 1,

    /// <summary>随机 / 重大人生 Event（未来来源；M6 不实现）。</summary>
    RandomMajorEvent = 2,

    /// <summary>玩家参与的人生方向事件（未来来源；M6 不实现）。</summary>
    LifeDirection = 3
}

/// <summary>
/// 工作执行事实 → Formation Evidence 的映射适配器（Stage 2「test mapping adapter」）。
///
/// 只读映射：把「一个岗位的一次实际执行（WorkplaceId）」映射为对若干 Profession 的
/// Evidence Contribution（允许多对多，但必须存在可解释的主次关系 —— 由 Amount / 条目设计表达）。
/// 本接口供 Assembly-CSharp 桥（VillageRuntimeBridge）在 Day End 把 Assignment daily delta fact
/// 转成标准化 Contribution；映射内容必须可注入（Test / Not Frozen）。
/// </summary>
public interface IFormationWorkEvidenceMapper
{
    /// <summary>岗位实际执行一天 → 标准化 Contribution 列表（未知岗位返回空列表）。</summary>
    IReadOnlyList<FormationEvidenceContribution> MapWorkplaceDay(string workplaceId);
}

/// <summary>单条 Work → Profession 映射配置（Test / Not Frozen）。</summary>
public sealed class FormationWorkEvidenceMappingEntry
{
    public string WorkplaceId;
    public string ProfessionId;

    /// <summary>该岗位实际执行一天产生的 Evidence 量（Test / Not Frozen；主映射大、次映射小以表达主次关系）。</summary>
    public float AmountPerExecutedDay = 1f;
}

/// <summary>
/// FormationTestWorkEvidenceMapping —— 注入用 Work → Profession 测试映射容器。
/// 只承载 Test / Not Frozen 内容；Runtime Core 不内置正式映射。
/// 同一 WorkplaceId 允许多条（多对多：如主 Profession + 次要 Profession）。
/// </summary>
public sealed class FormationTestWorkEvidenceMapping : IFormationWorkEvidenceMapper
{
    public List<FormationWorkEvidenceMappingEntry> Entries = new List<FormationWorkEvidenceMappingEntry>();

    public IReadOnlyList<FormationEvidenceContribution> MapWorkplaceDay(string workplaceId)
    {
        var result = new List<FormationEvidenceContribution>();
        if (string.IsNullOrEmpty(workplaceId) || Entries == null) return result;
        for (int i = 0; i < Entries.Count; i++)
        {
            var e = Entries[i];
            if (e == null || e.WorkplaceId != workplaceId) continue;
            if (string.IsNullOrEmpty(e.ProfessionId) || e.AmountPerExecutedDay <= 0f) continue;
            result.Add(new FormationEvidenceContribution(e.ProfessionId, e.AmountPerExecutedDay,
                FormationEvidenceSource.DailyWork));
        }
        return result;
    }
}
