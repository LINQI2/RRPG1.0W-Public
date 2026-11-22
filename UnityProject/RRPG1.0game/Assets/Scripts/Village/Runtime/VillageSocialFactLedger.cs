using System.Collections.Generic;

/// <summary>
/// VillageSocialFactType —— V3A 社会事实类型（Village.Runtime 纯逻辑；Test / Not Frozen）。
///
/// V3A 只记录一种客观社会事实：某 Person 被成功投入某个 Active Project（Temporary Commitment 动作）。
/// 类型字段用于约束查询只统计同一种事实，避免未来加入其它事实类型时被静默计入。
/// </summary>
public enum VillageSocialFactType
{
    /// <summary>Person 被投入 Active Project 的 Temporary Commitment 动作事实（Test / Not Frozen）。</summary>
    TemporaryCommitment = 0
}

/// <summary>
/// VillageSocialFact —— 一条已发生的客观社会事实（Village.Runtime 纯数据）。
///
/// 只记录「已发生动作 + 发生时间」，不写解释 / 评价 / 认识
/// （Experience ≠ Recognition ≠ Event，Village-GameSpec-V0.1 §5.2.3）。
/// 唯一性：同一 Person 对同一 Project 实例只可能成功加入一次，因此以 (PersonId, ProjectId) 为唯一键。
/// </summary>
public class VillageSocialFact
{
    public VillageSocialFactType Type;
    public string PersonId;
    public string ProjectDefId;
    public string ProjectId;
    public int Year;
    public int Month;
    public int Day;
}

/// <summary>VillageSocialFactLedger 记录结果。</summary>
public enum VillageSocialFactResult
{
    Success,
    /// <summary>同一 Person 对同一 Project 实例已存在事实（幂等，无新增）。</summary>
    Duplicate,
    /// <summary>PersonId 为空或非法。</summary>
    InvalidPersonId,
    /// <summary>ProjectDefId / ProjectId 为空或非法。</summary>
    InvalidProjectId,
    /// <summary>日期非法（Year/Month/Day 超出日历范围）。</summary>
    InvalidDate
}

/// <summary>
/// VillageSocialFactLedger —— V3A 客观社会事实账本（Village.Runtime 纯逻辑）。
///
/// 职责：
///   - 追加式保存 Temporary Commitment 动作事实（不修改、不删除、不复制 Assignment / Project 当前状态）；
///   - 提供按自然年的事实查询 CountTemporaryCommitmentsInYear；
///   - Save / Load（旧档缺失 → 空账本默认，不提升 SaveVersion）。
///
/// 边界：
///   - 本类不建立任何「第 N 次 = Recognition」的阈值 / 规则（阈值只允许存在于 Test / Not Frozen 测试）；
///   - 明确不是 Trend / Pattern / Observer / Recognition / Mood / Opinion / Satisfaction 等任何社会指标系统
///     （Village-GameSpec-V0.1 §5.2.7）；记录点由上层（VillageRuntimeBridge）只在真实成功的
///     Project Temporary Commitment 动作后调用。
/// </summary>
public class VillageSocialFactLedger
{
    private readonly List<VillageSocialFact> _facts = new List<VillageSocialFact>();

    /// <summary>全部已记录事实（只读视图）。</summary>
    public IReadOnlyList<VillageSocialFact> Facts => _facts;

    /// <summary>
    /// 记录一条 Temporary Commitment 客观事实。
    /// 幂等：同一 Person 对同一 Project 实例已存在事实时返回 Duplicate，不追加第二条。
    /// </summary>
    public VillageSocialFactResult RecordTemporaryCommitment(
        string personId, string projectDefId, string projectId, int year, int month, int day)
    {
        if (string.IsNullOrEmpty(personId)) return VillageSocialFactResult.InvalidPersonId;
        if (string.IsNullOrEmpty(projectDefId) || string.IsNullOrEmpty(projectId))
            return VillageSocialFactResult.InvalidProjectId;
        if (!IsValidDate(year, month, day)) return VillageSocialFactResult.InvalidDate;
        if (ContainsFact(personId, projectId)) return VillageSocialFactResult.Duplicate;

        _facts.Add(new VillageSocialFact
        {
            Type = VillageSocialFactType.TemporaryCommitment,
            PersonId = personId,
            ProjectDefId = projectDefId,
            ProjectId = projectId,
            Year = year,
            Month = month,
            Day = day
        });
        return VillageSocialFactResult.Success;
    }

    /// <summary>
    /// 查询某 Person 在指定自然年内被成功投入 Active Project（Temporary Commitment）的次数。
    /// 只做事实计数；本阶段不在 Runtime 判定任何阈值 / 形成 Recognition（消费方为 Test 或后续 Stage）。
    /// </summary>
    public int CountTemporaryCommitmentsInYear(string personId, int year)
    {
        if (string.IsNullOrEmpty(personId) || year < 1) return 0;

        int count = 0;
        foreach (var f in _facts)
        {
            if (f == null) continue;
            if (f.Type != VillageSocialFactType.TemporaryCommitment) continue;
            if (f.PersonId == personId && f.Year == year) count++;
        }
        return count;
    }

    /// <summary>存档快照（只保存事实本体；不含任何派生 / 指标状态）。</summary>
    public VillageSocialSaveData ToSaveData()
    {
        var facts = new List<VillageSocialFactSaveData>();
        foreach (var f in _facts)
        {
            if (f == null) continue;
            facts.Add(new VillageSocialFactSaveData
            {
                Type = (int)f.Type,
                PersonId = f.PersonId,
                ProjectDefId = f.ProjectDefId,
                ProjectId = f.ProjectId,
                Year = f.Year,
                Month = f.Month,
                Day = f.Day
            });
        }
        return new VillageSocialSaveData { Facts = facts };
    }

    /// <summary>
    /// 从存档恢复。防御规则：
    ///   - null / 空列表 → 空账本（旧档兼容默认状态）；
    ///   - 空 PersonId / ProjectDefId / ProjectId → 丢弃该条；
    ///   - 未知 Type 枚举序 → 丢弃该条；
    ///   - 非法日期 → 丢弃该条；
    ///   - 同一 (PersonId, ProjectId) 出现多条 → 只保留首条。
    /// </summary>
    public void Restore(VillageSocialSaveData data)
    {
        _facts.Clear();
        if (data == null || data.Facts == null) return;

        foreach (var f in data.Facts)
        {
            if (f == null) continue;
            if (string.IsNullOrEmpty(f.PersonId)
                || string.IsNullOrEmpty(f.ProjectDefId)
                || string.IsNullOrEmpty(f.ProjectId)) continue;
            if (!System.Enum.IsDefined(typeof(VillageSocialFactType), f.Type)) continue;
            if (!IsValidDate(f.Year, f.Month, f.Day)) continue;
            if (ContainsFact(f.PersonId, f.ProjectId)) continue;

            _facts.Add(new VillageSocialFact
            {
                Type = (VillageSocialFactType)f.Type,
                PersonId = f.PersonId,
                ProjectDefId = f.ProjectDefId,
                ProjectId = f.ProjectId,
                Year = f.Year,
                Month = f.Month,
                Day = f.Day
            });
        }
    }

    /// <summary>是否已存在该 Person 对该 Project 实例的 Temporary Commitment 事实。</summary>
    private bool ContainsFact(string personId, string projectId)
    {
        foreach (var f in _facts)
        {
            if (f == null) continue;
            if (f.Type != VillageSocialFactType.TemporaryCommitment) continue;
            if (f.PersonId == personId && f.ProjectId == projectId) return true;
        }
        return false;
    }

    private static bool IsValidDate(int year, int month, int day)
    {
        if (year < 1 || month < 1 || month > 12 || day < 1) return false;
        return day <= VillageTime.DaysInMonth(year, month);
    }
}
