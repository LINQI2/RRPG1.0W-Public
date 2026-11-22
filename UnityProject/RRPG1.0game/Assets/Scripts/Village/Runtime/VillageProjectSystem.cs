using System.Collections.Generic;

/// <summary>
/// VillageProjectSystem —— V2B Project Commitment 核心（Village.Runtime 纯逻辑）。
///
/// 职责（Village-GameSpec-V0.1 §2.5 / §4.5 / §4.6 / §4.12 V2B）：
///   - 保存具体 Participant Person（ParticipantPersonIds）与有限 Participant Capacity；
///   - 同一 Person 默认不能同时正式参与两个 Active Project；
///   - Active Project 是 Temporary Occupancy 来源：由外部占用聚合层（VillageRuntimeBridge）读取
///     ActiveParticipantPersonIds，暂停其 Primary Assignment 实际执行（本类不删除 Assignment）；
///   - 依赖 VillageTime 的 Day Tick 精确推进 Progress（按参与者 × 每日单位）；
///   - Progress 达到目标 → Completion 只结算一次 → 释放参与者（此后不再占用）。
///
/// 复用边界：
///   - 不建立第二套 Person Occupancy / Assignment 状态；只作为新的 Temporary Occupancy 来源接入现有关系；
///   - 不实现正式 Food / 经济 / Production 数值、Workload / Shift / Fatigue、
///     Player Cancellation Policy（Not Frozen）、正式建筑 / 项目内容链。
/// </summary>
public class VillageProjectSystem
{
    private readonly List<VillageProjectDef> _defs = new List<VillageProjectDef>();
    private readonly List<VillageProject> _activeProjects = new List<VillageProject>();
    private readonly List<VillageProject> _completedProjects = new List<VillageProject>();

    /// <summary>项目实例序列号；存档保留分配水位，旧档从现存项目后缀续用。</summary>
    private int _nextSequence;

    /// <summary>当前全部 Active Project（只读视图）。</summary>
    public IReadOnlyList<VillageProject> ActiveProjects => _activeProjects;

    /// <summary>当前已 Completed 的 Project 快照（仅运行期记录；不存档，不重新占用任何人）。</summary>
    public IReadOnlyList<VillageProject> CompletedProjects => _completedProjects;

    /// <summary>
    /// 以测试项目定义集合构造系统。定义属于内容/测试层（Test / Not Frozen），本类保存内部副本。
    /// </summary>
    public VillageProjectSystem(IReadOnlyList<VillageProjectDef> projectDefs)
    {
        if (projectDefs != null)
        {
            foreach (var def in projectDefs)
            {
                if (def == null || string.IsNullOrEmpty(def.ProjectDefId)) continue;
                _defs.Add(new VillageProjectDef
                {
                    ProjectDefId = def.ProjectDefId,
                    DisplayName = def.DisplayName,
                    ParticipantCapacity = def.ParticipantCapacity > 0 ? def.ParticipantCapacity : 0,
                    TestProgressPerParticipantPerDay = def.TestProgressPerParticipantPerDay > 0 ? def.TestProgressPerParticipantPerDay : 0,
                    TestProgressTarget = def.TestProgressTarget > 0 ? def.TestProgressTarget : 0
                });
            }
        }
    }

    /// <summary>
    /// 创建一个空 Active Project（随后用 TryAddParticipant 加入具体 Person）。
    /// projectId 由系统按「定义Id_序号」生成；projectDefId 必须已注册。
    /// </summary>
    public VillageProjectResult TryCreateProject(string projectDefId, out VillageProject created)
    {
        created = null;
        if (string.IsNullOrEmpty(projectDefId) || FindDef(projectDefId) == null)
            return VillageProjectResult.UnknownProjectDef;

        _nextSequence++;
        var project = new VillageProject
        {
            ProjectId = projectDefId + "_" + _nextSequence,
            ProjectDefId = projectDefId,
            State = VillageProjectState.Active
        };
        _activeProjects.Add(project);
        created = project;
        return VillageProjectResult.Success;
    }

    /// <summary>
    /// 尝试把具体 Person 加入 Active Project。
    /// 规则：Project 必须存在且 Active；PersonId 非空；目标 Project 未满员；
    ///      Person 不得已在其它 Active Project（同一 Person 禁双 Active Project）；
    ///      同一 Person 重复加入同一 Project 视为幂等成功（无变化）。
    /// </summary>
    public VillageProjectResult TryAddParticipant(string projectId, string personId)
    {
        if (string.IsNullOrEmpty(personId)) return VillageProjectResult.InvalidPersonId;

        var project = FindActiveProject(projectId);
        if (project == null) return VillageProjectResult.UnknownProject;

        var def = FindDef(project.ProjectDefId);
        if (def == null) return VillageProjectResult.UnknownProjectDef; // 防御：def 缺失的项目不应可继续操作

        if (project.ParticipantPersonIds.Contains(personId))
            return VillageProjectResult.Success; // 幂等：已在同一项目

        var activeOfPerson = GetActiveProjectOfPerson(personId);
        if (activeOfPerson != null && activeOfPerson.ProjectId != project.ProjectId)
            return VillageProjectResult.PersonAlreadyInActiveProject;

        if (project.ParticipantPersonIds.Count >= def.ParticipantCapacity)
            return VillageProjectResult.CapacityFull;

        project.ParticipantPersonIds.Add(personId);
        return VillageProjectResult.Success;
    }

    /// <summary>移除一个 Active Project（用于启动失败回滚 / 非法状态清理；返回是否确实移除）。</summary>
    public bool TryRemoveActiveProject(string projectId)
    {
        if (string.IsNullOrEmpty(projectId)) return false;
        int before = _activeProjects.Count;
        _activeProjects.RemoveAll(p => p.ProjectId == projectId);
        return _activeProjects.Count < before;
    }

    public VillageProjectResult TryRemoveParticipant(string projectId, string personId)
    {
        var project = FindActiveProject(projectId);
        if (project == null) return VillageProjectResult.UnknownProject;
        if (string.IsNullOrEmpty(personId)) return VillageProjectResult.InvalidPersonId;
        project.ParticipantPersonIds.Remove(personId);
        project.ExecutionPaused = project.ParticipantPersonIds.Count == 0;
        return VillageProjectResult.Success;
    }

    /// <summary>某 ProjectId 是否已 Completed（本次运行期记录；Completed 不存档）。</summary>
    public bool IsProjectCompleted(string projectId)
    {
        if (string.IsNullOrEmpty(projectId)) return false;
        foreach (var c in _completedProjects)
        {
            if (c.ProjectId == projectId) return true;
        }
        return false;
    }

    /// <summary>查询 Person 当前所在的 Active Project；无则 null。</summary>
    public VillageProject GetActiveProjectOfPerson(string personId)
    {
        if (string.IsNullOrEmpty(personId)) return null;
        foreach (var p in _activeProjects)
        {
            if (p.ParticipantPersonIds.Contains(personId)) return p;
        }
        return null;
    }

    /// <summary>Person 当前是否在某个 Active Project 中。</summary>
    public bool IsPersonInActiveProject(string personId)
    {
        return GetActiveProjectOfPerson(personId) != null;
    }

    /// <summary>按 Id 查询 Active Project；无则 null。</summary>
    public VillageProject GetActiveProject(string projectId)
    {
        return FindActiveProject(projectId);
    }

    /// <summary>当前全部 Active Project 参与者的 PersonId（去重；供 Temporary Occupancy 聚合）。</summary>
    public List<string> GetActiveParticipantPersonIds()
    {
        var result = new List<string>();
        foreach (var p in _activeProjects)
        {
            foreach (var id in p.ParticipantPersonIds)
            {
                if (string.IsNullOrEmpty(id)) continue;
                if (!result.Contains(id)) result.Add(id);
            }
        }
        return result;
    }

    /// <summary>
    /// Day Tick 推进所有 Active Project：
    ///   每名参与者产生 Def.TestProgressPerParticipantPerDay 单位进度；
    ///   达到 TestProgressTarget → 截断到目标、Completion 只结算一次（移入 Completed 快照）、
    ///   从 Active 释放（此后不再作为 Temporary Occupancy 占用）。
    /// 无参与者时不推进。
    /// </summary>
    public void OnDayTick()
    {
        if (_activeProjects.Count == 0) return;

        var snapshot = new List<VillageProject>(_activeProjects);
        foreach (var p in snapshot)
        {
            if (!p.IsActive) continue;
            var def = FindDef(p.ProjectDefId);
            if (def == null) continue; // 防御：def 缺失的项目不推进

            if (p.ParticipantPersonIds.Count > 0 && def.TestProgressPerParticipantPerDay > 0)
            {
                p.Progress += p.ParticipantPersonIds.Count * def.TestProgressPerParticipantPerDay;
            }

            if (def.TestProgressTarget > 0 && p.Progress >= def.TestProgressTarget)
            {
                p.Progress = def.TestProgressTarget; // 截断到目标，保证可精确断言
                p.State = VillageProjectState.Completed;
                _completedProjects.Add(p); // 完成快照（State 切换保证只结算一次）
            }
        }

        // 移除已完成，释放参与者占用
        _activeProjects.RemoveAll(p => p.IsCompleted);
    }

    public void OnWorkInterval(int minutes, IReadOnlyCollection<string> executingPersonIds)
    {
        var present = new HashSet<string>(executingPersonIds ?? new string[0]);
        foreach (var p in new List<VillageProject>(_activeProjects))
        {
            var def = FindDef(p.ProjectDefId);
            if (def == null) continue;
            int workers = 0;
            foreach (string id in p.ParticipantPersonIds) if (present.Contains(id)) workers++;
            p.ExecutionPaused = workers == 0;
            if (workers == 0 || minutes <= 0) continue;
            long numerator = checked(p.LaborRemainder + (long)workers * minutes * def.TestProgressPerParticipantPerDay);
            p.Progress = checked(p.Progress + (int)(numerator / 480));
            p.LaborRemainder = numerator % 480;
            if (def.TestProgressTarget > 0 && p.Progress >= def.TestProgressTarget)
            {
                p.Progress = def.TestProgressTarget; p.LaborRemainder = 0;
                p.State = VillageProjectState.Completed; _completedProjects.Add(p);
            }
        }
        _activeProjects.RemoveAll(p => p.IsCompleted);
    }

    /// <summary>存档快照：只保存 Active Project（Completed 不存档、不再占用）。</summary>
    public VillageProjectsSaveData ToSaveData()
    {
        var projects = new List<VillageProjectSaveData>();
        foreach (var p in _activeProjects)
        {
            projects.Add(new VillageProjectSaveData
            {
                ProjectId = p.ProjectId,
                ProjectDefId = p.ProjectDefId,
                ParticipantPersonIds = new List<string>(p.ParticipantPersonIds),
                Progress = p.Progress,
                LaborRemainder = p.LaborRemainder, ExecutionPaused = p.ExecutionPaused,
                State = (int)p.State
            });
        }
        return new VillageProjectsSaveData { LastAllocatedSequence = _nextSequence, ActiveProjects = projects };
    }

    /// <summary>
    /// 从存档恢复 Active Project。防御规则：
    ///   - null → 空系统（旧档兼容默认状态）；
    ///   - 未注册 Def / 空或重复 ProjectId → 丢弃该 Project；
    ///   - 空或非法 PersonId、Person 已占用（已恢复的其它 Active Project）→ 跳过该参与者；
    ///   - 零参与者项目保留进度并暂停；之后可继续招募；
    ///   - 若进度已达目标 → 视为已完成，不恢复为 Active（不占用）；
    ///   - 负数 / 非法进度 → 回退 0。
    /// </summary>
    public void Restore(VillageProjectsSaveData data)
    {
        _activeProjects.Clear();
        _completedProjects.Clear();
        _nextSequence = data != null && data.LastAllocatedSequence > 0 ? data.LastAllocatedSequence : 0;

        if (data == null || data.ActiveProjects == null) return;

        foreach (var sp in data.ActiveProjects)
        {
            if (sp == null || string.IsNullOrEmpty(sp.ProjectId)) continue;
            var def = FindDef(sp.ProjectDefId);
            if (def == null) continue;
            if (FindActiveProject(sp.ProjectId) != null) continue;

            var participants = new List<string>();
            if (sp.ParticipantPersonIds != null)
            {
                foreach (var id in sp.ParticipantPersonIds)
                {
                    if (string.IsNullOrEmpty(id)) continue;
                    if (participants.Contains(id)) continue;
                    if (GetActiveProjectOfPerson(id) != null) continue; // 同一 Person 禁双 Active Project
                    participants.Add(id);
                }
            }

            int progress = sp.Progress > 0 ? sp.Progress : 0;
            bool alreadyCompleted = def.TestProgressTarget > 0 && progress >= def.TestProgressTarget;

            if (alreadyCompleted)
            {
                _completedProjects.Add(new VillageProject
                {
                    ProjectId = sp.ProjectId,
                    ProjectDefId = sp.ProjectDefId,
                    ParticipantPersonIds = participants,
                    Progress = def.TestProgressTarget,
                    State = VillageProjectState.Completed
                });
                continue;
            }


            _activeProjects.Add(new VillageProject
            {
                ProjectId = sp.ProjectId,
                ProjectDefId = sp.ProjectDefId,
                ParticipantPersonIds = participants,
                Progress = progress,
                LaborRemainder = sp.LaborRemainder >= 0 && sp.LaborRemainder < 480 ? sp.LaborRemainder : 0,
                ExecutionPaused = sp.ExecutionPaused || participants.Count == 0,
                State = VillageProjectState.Active
            });
        }

        // 序列号恢复：取已有 ProjectId 后缀最大值（保守含空档），避免新建 ProjectId 冲突
        int maxSeq = _nextSequence;
        foreach (var p in System.Linq.Enumerable.Concat(_activeProjects, _completedProjects))
        {
            int idx = p.ProjectId.LastIndexOf('_');
            if (idx >= 0 && idx < p.ProjectId.Length - 1)
            {
                int v;
                if (int.TryParse(p.ProjectId.Substring(idx + 1), out v) && v > maxSeq) maxSeq = v;
            }
        }
        _nextSequence = maxSeq;
    }

    /// <summary>按 DefId 查询 Project 定义（UI / 视图层读取容量与进度规则用）；未注册返回 null。</summary>
    public VillageProjectDef GetProjectDef(string projectDefId)
    {
        return FindDef(projectDefId);
    }

    private VillageProjectDef FindDef(string projectDefId)
    {
        if (string.IsNullOrEmpty(projectDefId)) return null;
        foreach (var d in _defs)
        {
            if (d.ProjectDefId == projectDefId) return d;
        }
        return null;
    }

    private VillageProject FindActiveProject(string projectId)
    {
        if (string.IsNullOrEmpty(projectId)) return null;
        foreach (var p in _activeProjects)
        {
            if (p.ProjectId == projectId) return p;
        }
        return null;
    }
}

/// <summary>Project 操作结果。</summary>
public enum VillageProjectResult
{
    Success,
    /// <summary>ProjectDefId 未注册。</summary>
    UnknownProjectDef,
    /// <summary>ProjectId 不存在或已完成。</summary>
    UnknownProject,
    /// <summary>该 Person 已在另一个 Active Project（同一 Person 禁双 Active Project）。</summary>
    PersonAlreadyInActiveProject,
    /// <summary>目标 Project 参与者已满员。</summary>
    CapacityFull,
    /// <summary>PersonId 为空或非法。</summary>
    InvalidPersonId,
    /// <summary>调度层拒绝：Person 当前被其它来源（Expedition / Unavailable）占用（由桥接层返回）。</summary>
    PersonCurrentlyUnavailable,

    /// <summary>
    /// 调度层拒绝：Person 正处于玩家在 Recognition 事件中选择的恢复窗口（V3B Rest Gate，自选后果），
    /// 尚未到可再次安排日期（含 Until 当天；下月 1 日起恢复）。仅由桥接层在社会层返回。
    /// </summary>
    PersonInRestWindow
}
