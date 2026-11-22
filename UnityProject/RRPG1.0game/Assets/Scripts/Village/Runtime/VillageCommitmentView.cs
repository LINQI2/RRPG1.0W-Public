using System.Collections.Generic;

/// <summary>
/// VillageCommitmentView —— V2C Functional UI 的纯逻辑视图模型（Village.Runtime）。
///
/// 职责：把 Runtime 权威状态（Person Availability / Assignment / Project / Workplace Def / 执行事实）
/// 合成为「以具体 Person 为主体、可读」的行描述，供 UI 只做渲染；UI 不得自行计算 Occupancy / Progress。
/// 本类不持有可变状态：每次调用都从传入 Runtime 读取生成行快照，仅供当帧渲染。
/// 所有文案均为 Test / Not Frozen；不实现正式职业 / 正式生产数值。
/// </summary>
public static class VillageCommitmentView
{
    /// <summary>Person 承诺状态摘要（谁在做什么 / 谁被抽去哪 / 为什么原工作停了）。</summary>
    public enum PersonCommitmentStatus
    {
        Idle,            // 在村、无 Primary Assignment
        Working,         // 在村、有 Primary Assignment、未被占用 → 实际执行
        InProject,       // 投入 Active Project → 原 Assignment 暂停执行
        Expedition,      // 远征中 → 原 Assignment 暂停执行
        Unavailable      // 暂不可用（长期已死亡等）→ 原 Assignment 暂停执行
    }

    public class CommitmentPersonRow
    {
        public string PersonId;
        public string DisplayName;
        public VillageAvailability Availability;
        public PersonCommitmentStatus Status;
        public string WorkplaceDisplayName;    // Primary Assignment 岗位名（无则空）
        public string ActiveProjectDisplayName; // 所在 Active Project 名（无则空）
        public int TotalExecutedDays;          // Assignment 实际执行累计天数（Test）
        public string StatusText;              // 一行可读状态描述（中文，Test/Not Frozen）

        /// <summary>V3C（R2）：该 Person 当前是否处于 Rest Gate 恢复窗口内（由 Rest 权威规则派生）。</summary>
        public bool IsRestActive;

        /// <summary>V3C（R2）：恢复窗口可读文本（无则空串；Test / Not Frozen 文案）。</summary>
        public string RestText;
    }

    /// <summary>
    /// 合成 Person 承诺行。views 来自 VillagePersonAvailability.Resolve（真实长期人物只读映射）；
    /// assignments / projects 为运行系统（null 安全）。
    /// V3C（R2）：socialResponse + 当前年月日 为可选参数；传入时按 Rest 权威规则
    /// （VillageSocialResponseLog.IsPersonRestBlocked）填充 IsRestActive / RestText。
    /// 不传入（默认）时不产出任何 Rest 信息，既有调用方行为不变；本方法不复制 Rest 规则。
    /// </summary>
    public static List<CommitmentPersonRow> BuildPersonRows(
        IReadOnlyList<VillagePersonView> views,
        VillageAssignmentSystem assignments,
        VillageProjectSystem projects,
        VillageSocialResponseLog socialResponse = null,
        int currentYear = 0,
        int currentMonth = 0,
        int currentDay = 0)
    {
        return BuildPersonRowsCore(views, assignments, projects, socialResponse, null, currentYear, currentMonth,
            currentDay);
    }

    /// <summary>
    /// **WP-Event-1D Gate D1**：Rest 权威已从 Legacy `VillageSocialResponseLog` 独立为
    /// `VillagePersonRestState`（Unified Event 的 `GrantPersonRest` 只写它）。
    /// 本重载让只读展示与**正式 Rest 权威**保持一致（语义与 Rest Gate 相同：新权威优先，旧记录兼容）。
    /// </summary>
    public static List<CommitmentPersonRow> BuildPersonRows(
        IReadOnlyList<VillagePersonView> views,
        VillageAssignmentSystem assignments,
        VillageProjectSystem projects,
        VillagePersonRestState personRest,
        int currentYear,
        int currentMonth,
        int currentDay)
    {
        return BuildPersonRowsCore(views, assignments, projects, null, personRest, currentYear, currentMonth,
            currentDay);
    }

    private static List<CommitmentPersonRow> BuildPersonRowsCore(
        IReadOnlyList<VillagePersonView> views,
        VillageAssignmentSystem assignments,
        VillageProjectSystem projects,
        VillageSocialResponseLog socialResponse,
        VillagePersonRestState personRest,
        int currentYear,
        int currentMonth,
        int currentDay)
    {
        var rows = new List<CommitmentPersonRow>();
        if (views == null) return rows;

        foreach (var v in views)
        {
            if (v == null || string.IsNullOrEmpty(v.PersonId)) continue;

            var record = assignments != null ? assignments.GetRecord(v.PersonId) : null;
            var project = projects != null ? projects.GetActiveProjectOfPerson(v.PersonId) : null;

            string workplaceName = string.Empty;
            string projectName = string.Empty;
            var status = PersonCommitmentStatus.Idle;

            if (v.Availability == VillageAvailability.Unavailable)
            {
                status = PersonCommitmentStatus.Unavailable;
            }
            else if (v.Availability == VillageAvailability.Expedition)
            {
                status = PersonCommitmentStatus.Expedition;
            }
            else if (project != null)
            {
                status = PersonCommitmentStatus.InProject;
                projectName = ProjectDisplayName(projects, project);
            }
            else if (record != null)
            {
                status = PersonCommitmentStatus.Working;
                var def = assignments != null ? assignments.GetWorkplaceDef(record.WorkplaceId) : null;
                workplaceName = def != null ? def.DisplayName : record.WorkplaceId;
            }

            // V3C（R2）：Rest 可见性由 Rest 权威规则派生（不复制规则、不新增 Rest State）。
            // WP-Event-1D Gate D1：正式 Rest Authority（`VillagePersonRestState`）优先，Legacy `Rests` 兼容。
            // 仅在调用方提供合法当前日期时求值；否则保持「无 Rest 信息」（既有行为不变）。
            string restText = ResolveRestText(personRest, socialResponse, v.PersonId, currentYear, currentMonth,
                currentDay);
            bool restActive = !string.IsNullOrEmpty(restText);

            var row = new CommitmentPersonRow
            {
                PersonId = v.PersonId,
                DisplayName = string.IsNullOrEmpty(v.DisplayName) ? v.PersonId : v.DisplayName,
                Availability = v.Availability,
                Status = status,
                WorkplaceDisplayName = workplaceName,
                ActiveProjectDisplayName = projectName,
                TotalExecutedDays = record != null ? record.TotalExecutedDays : 0,
                StatusText = BuildStatusText(status, workplaceName, projectName,
                    record != null ? record.TotalExecutedDays : 0),
                IsRestActive = restActive,
                RestText = restText
            };
            rows.Add(row);
        }
        return rows;
    }

    /// <summary>
    /// Rest 可见性（只读展示）：**正式 Rest Authority 优先，Legacy `Rests` 兼容**
    /// （与 Rest Gate 判定同口径，UI / View 不复制 Rest 规则）。
    /// 仅在调用方给出合法当前日期时求值；否则返回空串。
    /// </summary>
    private static string ResolveRestText(VillagePersonRestState personRest, VillageSocialResponseLog socialResponse,
        string personId, int currentYear, int currentMonth, int currentDay)
    {
        if (string.IsNullOrEmpty(personId)) return string.Empty;
        if (currentYear < 1 || currentMonth < 1 || currentDay < 1) return string.Empty;

        if (personRest != null && personRest.IsRestBlocked(personId, currentYear, currentMonth, currentDay))
        {
            var rest = personRest.GetLatestRest(personId);
            if (rest != null) return FormatRestText(rest.UntilYear, rest.UntilMonth, rest.UntilDay);
            return "恢复中（恢复窗口内；Test / Not Frozen）";
        }

        if (socialResponse != null
            && socialResponse.IsPersonRestBlocked(personId, currentYear, currentMonth, currentDay))
        {
            var rest = socialResponse.GetLatestRest(personId);
            if (rest != null) return FormatRestText(rest.UntilYear, rest.UntilMonth, rest.UntilDay);
            return "恢复中（恢复窗口内；Test / Not Frozen）";
        }

        return string.Empty;
    }

    private static string FormatRestText(int year, int month, int day)
    {
        return "恢复中，至 " + FormatDate(year, month, day) + "（含当天仍拦截；Test / Not Frozen）";
    }

    private static string BuildStatusText(PersonCommitmentStatus status, string workplaceName,
        string projectName, int executedDays)
    {
        switch (status)
        {
            case PersonCommitmentStatus.Working:
                return "在岗执行中，已实际执行 " + executedDays + " 天（Test Contribution 每日累加）";
            case PersonCommitmentStatus.InProject:
                return "投入 Project「" + projectName + "」：原岗位暂停，完成后自动回来";
            case PersonCommitmentStatus.Expedition:
                return "远征中：原岗位暂停执行";
            case PersonCommitmentStatus.Unavailable:
                return "暂不可用：原岗位暂停执行";
            default:
                return "空闲：暂无 Primary Assignment（可在村庄被安排）";
        }
    }

    private static string ProjectDisplayName(VillageProjectSystem projects, VillageProject project)
    {
        var def = projects != null ? projects.GetProjectDef(project.ProjectDefId) : null;
        return def != null ? def.DisplayName : project.ProjectId;
    }

    // ==================== Test Workplace 行（Capacity / 满员可读） ====================

    public class WorkplaceRow
    {
        public string WorkplaceId;
        public string DisplayName;
        public int OccupantCount;
        public int Capacity;
        public bool IsFull;
        public string CapacityText;
        public string OccupantText;
    }

    public static List<WorkplaceRow> BuildWorkplaceRows(
        IReadOnlyList<VillageWorkplaceDef> defs,
        VillageAssignmentSystem assignments,
        IDictionary<string, string> personNames)
    {
        var rows = new List<WorkplaceRow>();
        if (defs == null) return rows;

        foreach (var def in defs)
        {
            if (def == null || string.IsNullOrEmpty(def.WorkplaceId)) continue;

            int count = assignments != null ? assignments.GetOccupantCount(def.WorkplaceId) : 0;
            var occupantIds = assignments != null ? assignments.GetOccupantPersonIds(def.WorkplaceId) : new List<string>();
            var names = new List<string>();
            foreach (var id in occupantIds)
            {
                string name;
                names.Add(personNames != null && personNames.TryGetValue(id, out name) && !string.IsNullOrEmpty(name)
                    ? name : id);
            }

            rows.Add(new WorkplaceRow
            {
                WorkplaceId = def.WorkplaceId,
                DisplayName = def.DisplayName,
                OccupantCount = count,
                Capacity = def.Capacity,
                IsFull = count >= def.Capacity,
                CapacityText = count + " / " + def.Capacity + (count >= def.Capacity ? "（已满）" : ""),
                OccupantText = names.Count > 0 ? string.Join("、", names) : "（无人占用）"
            });
        }
        return rows;
    }

    // ==================== Test Project 行（创建选项 / Active 进度） ====================

    public class ProjectDefRow
    {
        public string ProjectDefId;
        public string DisplayName;
        public int ParticipantCapacity;
        public string RuleText; // 如「容量 2；每参与者每日进度 1；需 10 完成」
    }

    public static List<ProjectDefRow> BuildProjectDefRows(IReadOnlyList<VillageProjectDef> defs)
    {
        var rows = new List<ProjectDefRow>();
        if (defs == null) return rows;
        foreach (var def in defs)
        {
            if (def == null || string.IsNullOrEmpty(def.ProjectDefId)) continue;
            rows.Add(new ProjectDefRow
            {
                ProjectDefId = def.ProjectDefId,
                DisplayName = def.DisplayName,
                ParticipantCapacity = def.ParticipantCapacity,
                RuleText = "容量 " + def.ParticipantCapacity +
                           "；每参与者每日进度 " + def.TestProgressPerParticipantPerDay +
                           "；共需 " + def.TestProgressTarget + " 完成"
            });
        }
        return rows;
    }

    public class ActiveProjectRow
    {
        public string ProjectId;
        public string DisplayName;
        public int Progress;
        public int Target;
        public int ParticipantCount;
        public int ParticipantCapacity;
        public int RemainingDays;          // 按当前参与者估算的剩余自然日（Test；>=0）
        public string ParticipantNamesText;
        public string ProgressText;        // 如 4 / 10
        public string SummaryText;         // 一行可读进度
    }

    /// <summary>
    /// 合成 Active Project 进度行。remainingDays 为估算（Test/Not Frozen，不构成新的工时模型）。
    /// </summary>
    public static List<ActiveProjectRow> BuildActiveProjectRows(
        VillageProjectSystem projects,
        IDictionary<string, string> personNames)
    {
        var rows = new List<ActiveProjectRow>();
        if (projects == null) return rows;

        foreach (var p in projects.ActiveProjects)
        {
            if (p == null) continue;
            var def = projects.GetProjectDef(p.ProjectDefId);
            if (def == null) continue;

            int namesCount = 0;
            var names = new List<string>();
            foreach (var id in p.ParticipantPersonIds)
            {
                string name;
                names.Add(personNames != null && personNames.TryGetValue(id, out name) && !string.IsNullOrEmpty(name)
                    ? name : id);
                namesCount++;
            }

            int daily = namesCount * def.TestProgressPerParticipantPerDay;
            int remaining = def.TestProgressTarget - p.Progress;
            int remainingDays = daily > 0 && remaining > 0
                ? (remaining + daily - 1) / daily
                : 0;

            rows.Add(new ActiveProjectRow
            {
                ProjectId = p.ProjectId,
                DisplayName = def.DisplayName,
                Progress = p.Progress,
                Target = def.TestProgressTarget,
                ParticipantCount = namesCount,
                ParticipantCapacity = def.ParticipantCapacity,
                RemainingDays = remainingDays,
                ParticipantNamesText = names.Count > 0 ? string.Join("、", names) : "（无参与者）",
                ProgressText = p.Progress + " / " + def.TestProgressTarget,
                SummaryText = def.DisplayName + "　" + p.Progress + " / " + def.TestProgressTarget +
                              "（预计还需 " + remainingDays + " 天）参与者：" +
                              (names.Count > 0 ? string.Join("、", names) : "（无）")
            });
        }
        return rows;
    }

    // ==================== V3C（R1）：社会回应历史只读合成 ====================
    // 只回看「已经真实发生过」的玩家选择与其后果；不新建 History System、不新增长期数据结构。
    // fired 是防重复技术事实（fired / 已触发记录），不得当作「人生历史」呈现 → 本区不读取 fired。

    /// <summary>社会回应历史行类型。</summary>
    public enum SocialHistoryKind
    {
        /// <summary>玩家对一次 Recognition 回应做出的选择（来源：ChoiceHistory）。</summary>
        Choice = 0,
        /// <summary>一次 Choice A 产生的恢复窗口（来源：RestUntil；日期 = 窗口截止日）。</summary>
        RestWindow = 1
    }

    public class SocialHistoryRow
    {
        public SocialHistoryKind Kind;
        public string PersonId;
        public string DisplayName;
        public int Year;      // Choice = 选择发生日；RestWindow = 恢复窗口截止日
        public int Month;
        public int Day;
        public long SortKey;  // Year*10000 + Month*100 + Day（降序 = 最近在前）
        public string DetailText;
    }

    /// <summary>
    /// 合成「最近的村民回应」只读历史行（R1）。
    ///
    /// 数据来源只使用现有长期事实：
    ///   - VillageSocialResponseLog.Choices（ChoiceHistory，玩家实际做出的选择）→ Kind=Choice；
    ///   - VillageSocialResponseLog.Rests（Choice A 产生的恢复窗口 RestUntil）→ Kind=RestWindow。
    /// 明确不读取 fired（fired 只是防重复技术事实）。
    ///
    /// 排序：SortKey 降序（最近在前）；同日期 Choice 行先于 RestWindow 行，之后按 PersonId / 文本保证确定性。
    /// RestWindow 行的日期是「窗口截止日」（Rest 记录本身不含创建日；本方法不做跨记录关联推断）。
    /// PersonId 找不到显示名时回退显示 PersonId；禁止按模板或其它信息猜 Person。
    /// </summary>
    public static List<SocialHistoryRow> BuildRecentSocialHistoryRows(
        VillageSocialResponseLog socialResponse,
        IDictionary<string, string> personNames,
        int maxRows)
    {
        return BuildRecentSocialHistoryRowsCore(socialResponse, null, personNames, maxRows);
    }

    /// <summary>
    /// **WP-Event-1D Gate D1**：Rest 权威独立后，恢复窗口行必须同时来自正式 Rest Authority
    /// （Unified Event 的 `GrantPersonRest` 只写 `VillagePersonRestState`；Legacy `Rests` 仅服务旧档兼容）。
    /// 同一 (Person, Until) 只出一行（不重复呈现同一恢复窗口）。
    /// </summary>
    public static List<SocialHistoryRow> BuildRecentSocialHistoryRows(
        VillageSocialResponseLog socialResponse,
        VillagePersonRestState personRest,
        IDictionary<string, string> personNames,
        int maxRows)
    {
        return BuildRecentSocialHistoryRowsCore(socialResponse, personRest, personNames, maxRows);
    }

    private static List<SocialHistoryRow> BuildRecentSocialHistoryRowsCore(
        VillageSocialResponseLog socialResponse,
        VillagePersonRestState personRest,
        IDictionary<string, string> personNames,
        int maxRows)
    {
        var rows = new List<SocialHistoryRow>();
        if (maxRows <= 0) return rows;
        if (socialResponse == null && personRest == null) return rows;

        if (socialResponse != null && socialResponse.Choices != null)
        {
            foreach (var c in socialResponse.Choices)
            {
                if (c == null || string.IsNullOrEmpty(c.PersonId)) continue;
                string name = ResolveDisplayName(c.PersonId, personNames);
                rows.Add(new SocialHistoryRow
                {
                    Kind = SocialHistoryKind.Choice,
                    PersonId = c.PersonId,
                    DisplayName = name,
                    Year = c.Year,
                    Month = c.Month,
                    Day = c.Day,
                    SortKey = SortKeyOf(c.Year, c.Month, c.Day),
                    DetailText = FormatDate(c.Year, c.Month, c.Day) + "　" + name + "　回应选择：" +
                                 ChoiceText(c.Choice)
                });
            }
        }

        var restKeys = new HashSet<string>();

        if (socialResponse != null && socialResponse.Rests != null)
        {
            foreach (var r in socialResponse.Rests)
            {
                if (r == null || string.IsNullOrEmpty(r.PersonId)) continue;
                if (!restKeys.Add(RestKey(r.PersonId, r.UntilYear, r.UntilMonth, r.UntilDay))) continue;
                rows.Add(BuildRestWindowRow(r.PersonId, r.UntilYear, r.UntilMonth, r.UntilDay, personNames));
            }
        }

        // WP-Event-1D Gate D1：正式 Rest Authority（Unified Event 的真实后果）也要出现在只读回看里。
        if (personRest != null && personRest.Rests != null)
        {
            foreach (var r in personRest.Rests)
            {
                if (r == null || string.IsNullOrEmpty(r.PersonId)) continue;
                if (!restKeys.Add(RestKey(r.PersonId, r.UntilYear, r.UntilMonth, r.UntilDay))) continue;
                rows.Add(BuildRestWindowRow(r.PersonId, r.UntilYear, r.UntilMonth, r.UntilDay, personNames));
            }
        }

        rows.Sort(CompareHistoryRowsDesc);
        if (rows.Count > maxRows) rows.RemoveRange(maxRows, rows.Count - maxRows);
        return rows;
    }

    private static string RestKey(string personId, int year, int month, int day)
    {
        return personId + "|" + year + "-" + month + "-" + day;
    }

    private static SocialHistoryRow BuildRestWindowRow(string personId, int year, int month, int day,
        IDictionary<string, string> personNames)
    {
        string name = ResolveDisplayName(personId, personNames);
        return new SocialHistoryRow
        {
            Kind = SocialHistoryKind.RestWindow,
            PersonId = personId,
            DisplayName = name,
            Year = year,
            Month = month,
            Day = day,
            SortKey = SortKeyOf(year, month, day),
            DetailText = name + "　恢复窗口：至 " + FormatDate(year, month, day) +
                         "（含当天仍拦截；Test / Not Frozen）"
        };
    }

    private static int CompareHistoryRowsDesc(SocialHistoryRow a, SocialHistoryRow b)
    {
        if (a == null && b == null) return 0;
        if (a == null) return 1;
        if (b == null) return -1;

        if (a.SortKey != b.SortKey) return a.SortKey > b.SortKey ? -1 : 1;
        if (a.Kind != b.Kind) return a.Kind < b.Kind ? -1 : 1;   // Choice 先于 RestWindow

        int byPerson = string.CompareOrdinal(a.PersonId ?? string.Empty, b.PersonId ?? string.Empty);
        if (byPerson != 0) return byPerson;
        return string.CompareOrdinal(a.DetailText ?? string.Empty, b.DetailText ?? string.Empty);
    }

    /// <summary>Choice 方向的可读文本（Test / Not Frozen 文案，中性治理表述）。</summary>
    private static string ChoiceText(VillageSocialResponseChoice choice)
    {
        switch (choice)
        {
            case VillageSocialResponseChoice.GrantRest: return "给了他一段明确的恢复窗口（休整至当月末）";
            case VillageSocialResponseChoice.ContinueUsing: return "当前仍需要他继续承担临时任务";
            default: return choice.ToString();
        }
    }

    /// <summary>PersonId → 显示名；找不到时回退 PersonId（禁止猜 Person）。</summary>
    private static string ResolveDisplayName(string personId, IDictionary<string, string> personNames)
    {
        if (string.IsNullOrEmpty(personId)) return string.Empty;
        string name;
        if (personNames != null && personNames.TryGetValue(personId, out name) && !string.IsNullOrEmpty(name))
        {
            return name;
        }
        return personId;
    }

    private static long SortKeyOf(int year, int month, int day)
    {
        return year * 10000L + month * 100L + day;
    }

    private static string FormatDate(int year, int month, int day)
    {
        return year + "年" + month + "月" + day + "日";
    }
}
