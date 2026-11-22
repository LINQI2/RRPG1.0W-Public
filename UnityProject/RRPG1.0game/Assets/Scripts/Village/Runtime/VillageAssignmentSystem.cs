using System;
using System.Collections.Generic;

/// <summary>
/// VillageAssignmentSystem —— V2A Assignment Core（Village.Runtime 纯逻辑）。
///
/// 职责（Village-GameSpec-V0.1 §4.2 / §4.4 / §4.7 / §4.12 V2A）：
///   - 承载「Person → Test Workplace」的 Primary Assignment（长期、持久关系，每 Person 最多一个）；
///   - 岗位 Capacity 有限，满员后拒绝继续分配；
///   - 按 Day Tick 结算：仅当 Person「存在 Primary Assignment + 对应岗位有效 + 未被 Temporary Occupancy
///     占用」时才实际执行，产生 Test Contribution 与 Profession Formation Source Hook (Test) 执行事实；
///   - Temporary Occupancy 只暂停实际执行，不删除 Assignment；
///   - Assignment 本身没有 Project 式 Progress：本类只有「长期职责记录 + 实际执行天数/贡献事实」，
///     没有完成度、完成条件或自动消失语义。
///
/// 数据所有权：
///   - 记录以 PersonId 为键，只表达 Village-specific 长期安排，不创建第二份 Person 长期真相源；
///   - 岗位定义（WorkplaceDef）由外部注入（当前为 Test 内容），满员占用由记录实时派生、不冗余存储。
///
/// 禁止（V2A 边界）：
///   - 不实现 V2B Project / Progress；不实现正式 Food / 经济 / Production 数值；
///   - Hook 事实只暴露执行事实来源，不实现 Profession Experience / 权重 / 候选 / 定型 / Trait / Card Pool。
/// </summary>
public class VillageAssignmentSystem
{
    private readonly List<VillageWorkplaceDef> _workplaces = new List<VillageWorkplaceDef>();
    private readonly List<VillageAssignmentRecord> _records = new List<VillageAssignmentRecord>();

    /// <summary>
    /// 【Test / Not Frozen】Village 级累计 Test Contribution（仅证明 Assignment 实际执行产生效果）。
    /// 每个实际执行的 Person × 岗位 TestContributionPerDay 累加；不是正式资源/经济数值。
    /// </summary>
    public int TotalTestContribution { get; private set; }

    /// <summary>当前全部 Primary Assignment 记录（只读视图；记录本体为运行时数据）。</summary>
    public IReadOnlyList<VillageAssignmentRecord> Records => _records;
    public IReadOnlyList<VillageWorkplaceDef> WorkplaceDefs => _workplaces.AsReadOnly();

    public bool TryRegisterWorkplace(VillageWorkplaceDef def)
    {
        if(def==null || string.IsNullOrEmpty(def.WorkplaceId) || def.Capacity<=0) return false;
        var existing=FindWorkplace(def.WorkplaceId);
        if(existing!=null) return existing.Capacity==def.Capacity && existing.TestContributionPerDay==def.TestContributionPerDay;
        _workplaces.Add(new VillageWorkplaceDef {WorkplaceId=def.WorkplaceId,DisplayName=def.DisplayName,
            Capacity=def.Capacity,TestContributionPerDay=def.TestContributionPerDay});
        return true;
    }

    /// <summary>
    /// 以岗位定义集合构造系统。
    /// 岗位定义属于内容/测试层（Test / Not Frozen），本类会保存内部副本以防外部修改。
    /// </summary>
    public VillageAssignmentSystem(IReadOnlyList<VillageWorkplaceDef> workplaces)
    {
        if (workplaces != null)
        {
            foreach (var def in workplaces)
            {
                if (def == null || string.IsNullOrEmpty(def.WorkplaceId)) continue;
                _workplaces.Add(new VillageWorkplaceDef
                {
                    WorkplaceId = def.WorkplaceId,
                    DisplayName = def.DisplayName,
                    Capacity = def.Capacity > 0 ? def.Capacity : 0,
                    TestContributionPerDay = def.TestContributionPerDay > 0 ? def.TestContributionPerDay : 0
                });
            }
        }
    }

    /// <summary>
    /// 尝试给 Person 指定一个 Primary Assignment。
    /// 规则：PersonId 非空；岗位必须已注册；Person 最多一个 Primary Assignment；
    ///      目标岗位当前占用数必须小于 Capacity。
    /// 同一 Person 对同一岗位重复调用视为幂等成功（无状态变化）。
    /// </summary>
    public VillageAssignmentResult TryAssign(string personId, string workplaceId)
    {
        if (string.IsNullOrEmpty(personId)) return VillageAssignmentResult.InvalidPersonId;
        if (string.IsNullOrEmpty(workplaceId) || FindWorkplace(workplaceId) == null)
            return VillageAssignmentResult.UnknownWorkplace;

        var existing = FindRecordByPerson(personId);
        if (existing != null)
        {
            return existing.WorkplaceId == workplaceId
                ? VillageAssignmentResult.Success
                : VillageAssignmentResult.AlreadyAssigned;
        }

        var def = FindWorkplace(workplaceId);
        if (GetOccupantCount(workplaceId) >= def.Capacity)
            return VillageAssignmentResult.CapacityFull;

        _records.Add(new VillageAssignmentRecord
        {
            PersonId = personId,
            WorkplaceId = workplaceId,
            TotalExecutedDays = 0
        });
        return VillageAssignmentResult.Success;
    }

    /// <summary>移除 Person 的 Primary Assignment（玩家主动修改）。移除后岗位占用释放。</summary>
    public bool RemoveAssignment(string personId)
    {
        var existing = FindRecordByPerson(personId);
        if (existing == null) return false;
        _records.Remove(existing);
        return true;
    }

    /// <summary>查询 Person 当前 Primary Assignment 记录；无则返回 null。</summary>
    public VillageAssignmentRecord GetRecord(string personId)
    {
        return FindRecordByPerson(personId);
    }

    /// <summary>查询 Person 当前岗位 Id；无则返回 null。</summary>
    public string GetWorkplaceId(string personId)
    {
        var r = FindRecordByPerson(personId);
        return r != null ? r.WorkplaceId : null;
    }

    /// <summary>查询岗位当前占用 Person 数。</summary>
    public int GetOccupantCount(string workplaceId)
    {
        if (string.IsNullOrEmpty(workplaceId)) return 0;
        int count = 0;
        foreach (var r in _records)
        {
            if (r.WorkplaceId == workplaceId) count++;
        }
        return count;
    }

    /// <summary>查询岗位当前绑定的 PersonId 列表。</summary>
    public List<string> GetOccupantPersonIds(string workplaceId)
    {
        var result = new List<string>();
        if (string.IsNullOrEmpty(workplaceId)) return result;
        foreach (var r in _records)
        {
            if (r.WorkplaceId == workplaceId) result.Add(r.PersonId);
        }
        return result;
    }

    /// <summary>岗位是否已满员（占用数 >= Capacity）。未注册岗位视为未满员（防御）。</summary>
    public bool IsWorkplaceFull(string workplaceId)
    {
        var def = FindWorkplace(workplaceId);
        return def != null && GetOccupantCount(workplaceId) >= def.Capacity;
    }

    /// <summary>查询岗位定义；未注册返回 null。</summary>
    public VillageWorkplaceDef GetWorkplaceDef(string workplaceId)
    {
        return FindWorkplace(workplaceId);
    }

    /// <summary>
    /// Day Tick 结算：对「有 Primary Assignment + 岗位有效 + 不在 Temporary Occupancy 集合」的 Person
    /// 实际执行一次 —— 累加执行天数（Hook 事实来源）与岗位 Test Contribution。
    /// temporaryOccupiedPersonIds：当前被 Expedition / Unavailable / Active Project 占用的 PersonId。
    /// 被占用 Person 的 Assignment 保留，只是本次不实际执行。
    ///
    /// M6（Stage 2）：本方法返回「当日实际执行 delta 事实」列表 —— 每名当天实际执行的 Person 一条。
    /// Formation 日结算只允许消费这个逐日 delta（不得每日读取累计 TotalExecutedDays 作为 delta）。
    /// Project / Temporary Occupancy 等未实际执行 Assignment 的日期不出现在返回值中（不产生 Work Evidence）。
    /// </summary>
    public List<VillageDailyExecutionFact> OnDayTick(IReadOnlyCollection<string> temporaryOccupiedPersonIds)
    {
        var dailyFacts = new List<VillageDailyExecutionFact>();
        var occupied = new HashSet<string>();
        if (temporaryOccupiedPersonIds != null)
        {
            foreach (var id in temporaryOccupiedPersonIds)
            {
                if (!string.IsNullOrEmpty(id)) occupied.Add(id);
            }
        }

        foreach (var r in _records)
        {
            if (occupied.Contains(r.PersonId)) continue;
            var def = FindWorkplace(r.WorkplaceId);
            if (def == null) continue; // 岗位无效 → 不执行（防御；正常不变量下不会出现）

            r.TotalExecutedDays++;
            r.WorkedMinutes = checked(r.WorkedMinutes + 480);
            TotalTestContribution += def.TestContributionPerDay;
            dailyFacts.Add(new VillageDailyExecutionFact
            {
                PersonId = r.PersonId,
                WorkplaceId = r.WorkplaceId
            });
        }
        return dailyFacts;
    }

    /// <summary>Consume actual interval evidence once at day end; no whole-day production.</summary>
    public void CommitLabor(IReadOnlyList<VillageDailyExecutionFact> facts)
    {
        if (facts == null) return;
        foreach (var fact in facts)
        {
            var record = FindRecordByPerson(fact.PersonId);
            var def = FindWorkplace(fact.WorkplaceId);
            if (record == null || def == null || fact.WorkedMinutes <= 0) continue;
            record.WorkedMinutes = checked(record.WorkedMinutes + fact.WorkedMinutes);
            record.TotalExecutedDays = checked((int)(record.WorkedMinutes / 480));
            long numerator = checked(record.ContributionRemainder + (long)fact.WorkedMinutes * def.TestContributionPerDay);
            TotalTestContribution = checked(TotalTestContribution + (int)(numerator / 480));
            record.ContributionRemainder = numerator % 480;
        }
    }

    /// <summary>
    /// Profession Formation Source Hook (Test) —— 只读执行事实来源。
    ///
    /// 仅当 Assignment 实际执行产生过事实（TotalExecutedDays &gt;= 1）才返回该 Person 的条目；
    /// 被 Temporary Occupancy 占用期间事实不增长，未执行则不出现新事实。
    /// 这只是未来 Profession Formation 可以读取的“执行事实来源”，
    /// 不实现 Experience Bar / 权重 / 候选算法 / 年龄阈值 / Profession 定型 / Trait / Card Pool
    /// （Village-GameSpec-V0.1 §4.7；字段与命名均 Test / Not Frozen）。
    /// </summary>
    public List<VillageExecutionFact> GetProfessionFormationSourceHookFacts()
    {
        var facts = new List<VillageExecutionFact>();
        foreach (var r in _records)
        {
            if (r.TotalExecutedDays <= 0) continue;
            facts.Add(new VillageExecutionFact
            {
                PersonId = r.PersonId,
                WorkplaceId = r.WorkplaceId,
                TotalExecutedDays = r.TotalExecutedDays
            });
        }
        return facts;
    }

    /// <summary>存档快照。</summary>
    public VillageAssignmentsSaveData ToSaveData()
    {
        var records = new List<VillageAssignmentSaveData>();
        foreach (var r in _records)
        {
            records.Add(new VillageAssignmentSaveData
            {
                PersonId = r.PersonId,
                WorkplaceId = r.WorkplaceId,
                TotalExecutedDays = r.TotalExecutedDays,
                WorkedMinutes = r.WorkedMinutes, ContributionRemainder = r.ContributionRemainder
            });
        }
        return new VillageAssignmentsSaveData
        {
            Records = records,
            TotalTestContribution = TotalTestContribution
        };
    }

    /// <summary>
    /// 从存档恢复。防御规则：
    ///   - null → 空系统（旧档兼容默认状态）；
    ///   - 空 PersonId / 未注册岗位（岗位定义已不存在）→ 丢弃该记录，避免 Person 被无法执行的安排锁定；
    ///   - 同一 Person 出现多条记录 → 只保留第一条（非法状态拒绝）；
    ///   - 负数累计值 → 回退 0；
    ///   - 任一条记录被丢弃（非法存档）→ Village 级累计 Test Contribution 一并清零，
    ///     避免保留无法归属到有效记录的贡献（聚合值无法反算到单条记录，Test / Not Frozen）。
    /// </summary>
    public void Restore(VillageAssignmentsSaveData data)
    {
        _records.Clear();
        TotalTestContribution = 0;
        if (data == null) return;

        bool droppedAny = false;
        if (data.Records != null)
        {
            foreach (var r in data.Records)
            {
                if (r == null || string.IsNullOrEmpty(r.PersonId)) { droppedAny = true; continue; }
                if (FindWorkplace(r.WorkplaceId) == null) { droppedAny = true; continue; } // 岗位定义缺失 → 不恢复该长期安排
                if (FindRecordByPerson(r.PersonId) != null) { droppedAny = true; continue; } // 同一 Person 多条 → 首条优先

                _records.Add(new VillageAssignmentRecord
                {
                    PersonId = r.PersonId,
                    WorkplaceId = r.WorkplaceId,
                    TotalExecutedDays = r.TotalExecutedDays > 0 ? r.TotalExecutedDays : 0
                    , WorkedMinutes = r.WorkedMinutes > 0 ? r.WorkedMinutes : Math.Max(0L,(long)r.TotalExecutedDays * 480),
                    ContributionRemainder = r.ContributionRemainder >= 0 && r.ContributionRemainder < 480 ? r.ContributionRemainder : 0
                });
            }
        }

        TotalTestContribution = (!droppedAny && data.TotalTestContribution > 0) ? data.TotalTestContribution : 0;
    }

    private VillageWorkplaceDef FindWorkplace(string workplaceId)
    {
        if (string.IsNullOrEmpty(workplaceId)) return null;
        foreach (var def in _workplaces)
        {
            if (def.WorkplaceId == workplaceId) return def;
        }
        return null;
    }

    private VillageAssignmentRecord FindRecordByPerson(string personId)
    {
        if (string.IsNullOrEmpty(personId)) return null;
        foreach (var r in _records)
        {
            if (r.PersonId == personId) return r;
        }
        return null;
    }
}

/// <summary>Primary Assignment 分配结果。</summary>
public enum VillageAssignmentResult
{
    Success,
    /// <summary>该 Person 已有 Primary Assignment（每 Person 最多一个；需先移除再改派）。</summary>
    AlreadyAssigned,
    /// <summary>目标岗位已满员，拒绝分配。</summary>
    CapacityFull,
    /// <summary>目标岗位未注册 / 不存在。</summary>
    UnknownWorkplace,
    /// <summary>PersonId 为空或非法。</summary>
    InvalidPersonId
}

/// <summary>
/// 运行时 Primary Assignment 记录（Person → Workplace；含执行事实计数）。
/// 字段全部为长期 Village 数据；TotalExecutedDays 是 Profession Formation Source Hook (Test) 事实，
/// Test / Not Frozen。
/// </summary>
public class VillageAssignmentRecord
{
    public long WorkedMinutes;
    public long ContributionRemainder;
    public string PersonId;
    public string WorkplaceId;

    /// <summary>实际执行累计天数（仅在实际执行当天 +1；Test / Not Frozen）。</summary>
    public int TotalExecutedDays;
}

/// <summary>Profession Formation Source Hook (Test) 返回的只读执行事实条目。</summary>
public class VillageExecutionFact
{
    public string PersonId;
    public string WorkplaceId;

    /// <summary>该 Person 在该岗位实际执行的累计天数（Test / Not Frozen）。</summary>
    public int TotalExecutedDays;
}

/// <summary>
/// M6（Stage 2）Assignment 每日 delta 事实：单次 Day Tick 中某 Person 实际执行了一次。
/// Formation 日结算只消费本 delta（PersonId + WorkplaceId），绝不读取累计 TotalExecutedDays 作为 delta；
/// Project / Temporary Occupancy 等未实际执行的日期不产生本事实。
/// </summary>
public class VillageDailyExecutionFact
{
    public int WorkedMinutes = 480;
    public string PersonId;
    public string WorkplaceId;
}
