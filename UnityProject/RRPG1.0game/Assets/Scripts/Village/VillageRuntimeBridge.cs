using System.Collections.Generic;
using UnityEngine;
using RRPG.AutonomousActivity;

/// <summary>
/// Village 运行时桥（Assembly-CSharp 层）。
///
/// 职责：
///   - 持有运行时唯一的 VillageTime / VillageState / VillageAssignmentSystem / VillageProjectSystem 实例
///     （V1/V2A/V2B 单例），以及 Stage V3A 客观社会事实账本 VillageSocialFactLedger；
///     并把 DayTicked 连接到 State 测试规则、Active Project 推进
///     与 Primary Assignment 执行结算；
///   - 从既有长期人物真相源（PlayerCollection）构建输入，调用纯逻辑 VillagePersonAvailability 解析可用状态；
///   - 生成 / 恢复 VillageSaveData（供 SaveSystem 读写；不创建第二套存档系统）；
///   - 登记当前 Temporary Occupancy 人物（Expedition / Unavailable / Active Project 参与者）PersonId
///     （运行时状态，只供 Village 执行/显示使用；不持久化、不写入长期人物字段）；
///   - V4F：承接一次 Expedition Return 的冻结顺序（Settlement → 保持 Occupancy → Return Catch-up 逐日
///     → Clear Occupancy → 最终 Return Save → Town），并持有跨调用 exactly-once 会话状态（ExpeditionRecord）。
///
/// 不负责：正式 Village 规则（全部为 Test / Not Frozen）、正式 UI、远征耗时语义（V1 不建模）。
/// </summary>
public static partial class VillageRuntimeBridge
{
    /// <summary>运行时唯一 Village 世界时间真相源实例。</summary>
    public static VillageTime Time;

    /// <summary>运行时唯一 Village 共同体状态实例。</summary>
    public static VillageState State;

    /// <summary>
    /// 运行时唯一 Primary Assignment 系统实例（Stage V2A）。
    /// 记录以 PersonId 为键的 Village-specific 长期安排，不创建第二份 Person 长期真相源。
    /// </summary>
    public static VillageAssignmentSystem Assignments;

    /// <summary>
    /// 运行时唯一 Test Project 系统实例（Stage V2B）。
    /// Active Project 是本桥 Temporary Occupancy 的新来源（参与者占用），
    /// 不建立第二套 Person Occupancy / Assignment 状态。
    /// </summary>
    public static VillageProjectSystem Projects;

    /// <summary>
    /// 运行时唯一客观社会事实账本实例（Stage V3A）。
    /// 只记录真实成功的 Temporary Commitment 动作事实（追加式），
    /// 不复制 / 不替代任何 Person / Assignment / Project 当前状态；
    /// 本账本不做 Recognition / 阈值判定（Village-GameSpec-V0.1 §5.2.7）。
    /// </summary>
    public static VillageSocialFactLedger SocialFacts;

    /// <summary>
    /// 运行时唯一 V3B Recognition → Response → New Decision 状态实例。
    /// 承载 fired / 单条 pending / ChoiceHistory / RestUntil；不承载 Recognition 数值
    /// （由调用方以 V3A ledger 计数 + VillageTestSocialPatterns 派生后调用）。
    /// </summary>
    public static VillageSocialResponseLog SocialResponse;

    /// <summary>
    /// **人物恢复期权威**（WP-Event-1D §7 正式抽离）：长期 Rest Gate 的唯一写读入口。
    ///
    /// 关系：
    ///   - 旧 `SocialResponse.Rests` 继续可读（Legacy Save Compatibility）；
    ///   - Load 时把旧档 Rests **幂等迁移**到本 Authority；
    ///   - Rest Gate 查询同时接受两者（OR），因此迁移不改变玩家可见行为；
    ///   - Unified Event 的 `GrantPersonRest` 正式 Adapter 只写本 Authority。
    /// </summary>
    public static VillagePersonRestState PersonRest;

    /// <summary>
    /// 运行时唯一最小 World Place 注册表实例（Minimum Village World Integration 第一包）。
    /// 只登记 Workplace（派生）与 ProjectSite（完成点单向追加）；不承载坐标 / 导航 / 建筑系统。
    /// </summary>
    public static VillageWorldRegistry World;

    /// <summary>
    /// M6 可注入 Formation Runtime Services（Stage 2 接线；默认 null = 未注入 → 不产生 Formation 行为）。
    /// 只承载 Test / Not Frozen 配置、唯一合法 Profession Registry 与 Work → Evidence 测试映射；
    /// 正式职业内容落地前产品路径不注入，不冻结任何 Formation 数值。
    /// </summary>
    public static FormationRuntimeServices Formation;

    /// <summary>
    /// 当前 Village 世界会话的长期人物真相源（场景会话级绑定；TownUI 等入口显式传入）。
    /// 只引用既有 PlayerCollection 的同一对象，不复制、不创建第二份长期 Person 数据；
    /// null = 未绑定（Day End Formation Resolver 跳过）。
    /// </summary>
    public static PlayerCollection SimulationPersonSource;

    /// <summary>当前远征人物 PersonId 列表（运行时登记，由 ExpeditionUI 维护；V1 不存档）。</summary>
    private static readonly List<string> _expeditionPersonIds = new List<string>();

    /// <summary>
    /// 当前暂不可用人物 PersonId 列表（运行时登记：从长期人物 IsDead 单向同步，不写长期字段、不存档）。
    /// 与 _expeditionPersonIds 同属 Temporary Occupancy 运行时输入；由 GetPersonAvailability 自动同步，
    /// 也允许测试显式登记。
    /// </summary>
    private static readonly List<string> _unavailablePersonIds = new List<string>();

    /// <summary>
    /// 默认岗位目录（Test / Not Frozen）：V2A 既有 Test 岗位 + WP-3 最小 Farm/Kitchen 岗位。
    /// Farm/Kitchen 复用既有 VillageWorkplaceDef / Assignment / Capacity 语义，不创建 Building System；
    /// WorkplaceId 与 VillageResourceWorkplaceDef（角色/数值）对齐，Capacity 仍为单一权威。
    /// </summary>
    private static readonly List<VillageWorkplaceDef> _testWorkplaces = MergeWorkplaceDefs(
        VillageTestWorkplaces.CreateDefaults(),
        VillageResourceCapacityWorkplaceDefs());

    /// <summary>WP-3（Gate 4 产品接线）：Farm/Kitchen 最小岗位 Capacity 定义（Test / Not Frozen）。</summary>
    private static List<VillageWorkplaceDef> VillageResourceCapacityWorkplaceDefs()
    {
        return new List<VillageWorkplaceDef>
        {
            new VillageWorkplaceDef
            {
                WorkplaceId = "ws_farm_01",
                DisplayName = "Farm（Test / Not Frozen）",
                Capacity = 2,
                TestContributionPerDay = 1
            },
            new VillageWorkplaceDef
            {
                WorkplaceId = "ws_kitchen_01",
                DisplayName = "Kitchen（Test / Not Frozen）",
                Capacity = 1,
                TestContributionPerDay = 1
            }
        };
    }

    /// <summary>V2B 测试项目目录（Test / Not Frozen；VillageTestProjects.CreateDefaults 的静态实例）。</summary>
    private static readonly List<VillageProjectDef> _testProjects = VillageTestProjects.CreateDefaults();

    // ==================== WP-3：Resource（Gate 3 接入真实 Day Tick；Test / Not Frozen） ====================

    /// <summary>
    /// 运行时唯一 Village 长期资源 Authority 实例。
    /// 只承载 Ingredients / Food（VillageResourceState）；本 Gate 不扩展 Save / Carry——
    /// Resources 随运行时创建与每日结算变化，存档/Expedition 接线留后续 Gate。
    /// </summary>
    public static VillageResourceState Resources;

    /// <summary>WP-3 资源岗位定义（仅表达 Role 与单位数值；Capacity 仍由既有 Workplace 权威持有，此处不复制）。</summary>
    private static readonly List<VillageResourceWorkplaceDef> _resourceWorkplaceProfiles =
        VillageTestResourceWorkplaces.CreateDefaults();
    // Derived lookup only; batch progress remains owned by IntradayWork and keyed by instance WorkplaceId.
    private static readonly List<VillageResourceWorkplaceDef> _resourceWorkplaceDefs =
        VillageTestResourceWorkplaces.CreateDefaults();

    /// <summary>【Test / Not Frozen】每名在村 Person 每日 Food 满足阈值（Fulfillment Threshold）。</summary>
    public static int ResourceFulfillmentThresholdPerPerson = 1;

    /// <summary>【Test / Not Frozen】Food 不足时每份需求的 Emergency Ingredients 量。</summary>
    public static int ResourceEmergencyIngredientsPerShare = 2;

    /// <summary>【Test / Not Frozen】Hungry 触发所需连续未满足天数（1 必须天然可用；默认 1）。</summary>
    public static int ResourceRequiredConsecutiveUnderfedDays = 1;

    /// <summary>
    /// 【Test / Not Frozen】Departure 每位成员默认携带 Food 数（最小 Test 策略；Food 不足按可带量转移）。
    /// V4 Gate 2（Decision D / OQ2）：产品链 Test 值 = 2（配合 Battle Node elapsed = 1 与消耗率 = 1，
    /// 使 2 个战斗节点可负担；超出后 `InsufficientSupply` 阻止继续进入节点属正确规则表现）。
    /// </summary>
    public static int ResourceCarryPerPerson = 2;

    /// <summary>
    /// 【Test / Not Frozen】远征每人每日 Supply 消耗率。
    /// V4 Gate 2（Decision D / OQ2）：产品链 Test 值 = 1（让 Supply 消耗真实发生）。
    /// 正式数值未冻结。
    /// </summary>
    public static int ResourcePerPersonDailyConsumptionRate = 1;

    private static bool _dayTickBound;

    /// <summary>
    /// Village 世界时间模拟是否激活（显式 runtime/state gating）。
    /// V1 规则：仅当领地界面激活时世界时间推进；离开领地（远征/卡册/主菜单）时暂停。
    /// 本状态由 TownUI 生命周期显式设置，不依赖任何 HUD 是否启用——HUD 只负责显示与操作。
    /// </summary>
    public static bool IsSimulationActive;

    /// <summary>显式设置 Village 时间模拟激活状态（由场景/UI 生命周期调用，非 HUD 驱动）。</summary>
    public static void SetSimulationActive(bool active)
    {
        IsSimulationActive = active;
    }

    public static IReadOnlyList<string> ExpeditionPersonIds => _expeditionPersonIds;

    /// <summary>登记当前远征人物（V1 运行时：从 AdventureManager.GetExpeditionPersonIds() 获取，不持久化）。</summary>
    public static void SetExpeditionPersonIds(IEnumerable<string> personIds)
    {
        if (!GuardWorldWrite()) return;
        EnsureInitialized(null);
        _expeditionDeparture = Time.CurrentInstant;
        // V4F：一次新的出征登记 = 一次新的 Home↔Expedition 会话开始。
        // 上一 Return 会话记录（Completed / Failed）随之失效；不携带任何跨会话的“完成”语义。
        _returnRecord = null;
        _pendingReturnMode = null; // WP-B：原始出口模式随会话记录同生命周期失效

        _expeditionPersonIds.Clear();
        if (personIds != null)
        {
            foreach (var id in personIds)
            {
                if (!string.IsNullOrEmpty(id)) _expeditionPersonIds.Add(id);
            }
        }
        ReconcileReceptionPresence();
    }

    /// <summary>
    /// 清空当前远征人物登记（V4F Return 顺序：Catch-up 全部完成之后调用）。
    /// 若存在进行中的 Return 会话且不在 Failed，则按冻结顺序把相位推进到 OccupancyCleared；
    /// 正在 Return Catch-up 期间调用会被拒绝（防重入：禁止补算中途清占用造成剩余天数占用快照缺员）。
    /// </summary>
    public static void ClearExpeditionPersonIds()
    {
        if (_returnRecord != null && !_returnRecord.Completed && _returnRecord.Phase != ExpeditionReturnPhase.CatchUpComplete)
        {
            if (_returnRecord.SettledElapsedMinutes != 0 || RunIntradayReturnCatchUp() != ExpeditionCatchUpResult.Completed) return;
        }
        if (_isCatchUpRunning)
        {
            Debug.LogWarning("[VillageRuntimeBridge] ClearExpeditionPersonIds 被拒绝：Return Catch-up 正在执行（防重入）。");
            return;
        }

        _expeditionPersonIds.Clear();
        RefreshWorkIntent();
        if (_returnRecord != null && !_returnRecord.Completed
            && _returnRecord.Phase != ExpeditionReturnPhase.Failed)
        {
            // 空跑（EffectiveElapsedDays=0）时 Catch-up 无逐日循环，Clear 前先把相位推进到 CatchUpComplete。
            if (_returnRecord.Phase == ExpeditionReturnPhase.ReturnSettled
                && _returnRecord.DaysSimulated >= _returnRecord.EffectiveElapsedDays)
            {
                _returnRecord.Phase = ExpeditionReturnPhase.CatchUpComplete;
            }
            if (_returnRecord.Phase == ExpeditionReturnPhase.CatchUpComplete)
            {
                _returnRecord.Phase = ExpeditionReturnPhase.OccupancyCleared;
                Time.HoldForFinalSave(_returnRecord.SessionIdentity);
            }
        }
    }

    /// <summary>当前暂不可用（长期已死亡）人物 PersonId（运行时登记，不存档）。</summary>
    public static IReadOnlyList<string> UnavailablePersonIds => _unavailablePersonIds;

    /// <summary>V2A 测试岗位目录（Test / Not Frozen）。</summary>
    public static IReadOnlyList<VillageWorkplaceDef> TestWorkplaceDefs => _testWorkplaces;

    /// <summary>V2B 测试项目目录（Test / Not Frozen）。</summary>
    public static IReadOnlyList<VillageProjectDef> TestProjectDefs => _testProjects;

    /// <summary>
    /// 登记当前暂不可用人物（V2A 运行时：从长期人物 IsDead 单向同步，不写长期字段、不存档）。
    /// GetPersonAvailability 每次解析时自动刷新，测试也可显式登记。
    /// </summary>
    public static void SetUnavailablePersonIds(IEnumerable<string> personIds)
    {
        _unavailablePersonIds.Clear();
        if (personIds != null)
        {
            foreach (var id in personIds)
            {
                if (!string.IsNullOrEmpty(id)) _unavailablePersonIds.Add(id);
            }
        }
        ReconcileReceptionPresence();
    }

    /// <summary>
    /// 当前全部 Temporary Occupancy 人物集合：
    ///   Expedition ∪ Unavailable ∪ Active Project 参与者（V2B 接入）。
    /// Primary Assignment 在执行时遇到该集合内 Person 会暂停实际执行，但不删除。
    /// </summary>
    public static List<string> GetTemporaryOccupiedPersonIds()
    {
        var result = new List<string>();
        result.AddRange(_expeditionPersonIds);
        foreach (var id in _unavailablePersonIds)
        {
            if (!result.Contains(id)) result.Add(id);
        }
        if (Projects != null)
        {
            foreach (var id in Projects.GetActiveParticipantPersonIds())
            {
                if (IsExecutingWork(id) && !result.Contains(id)) result.Add(id);
            }
        }
        return result;
    }

    // ==================== V4F：Expedition Return（Home↔Expedition Continuity Foundation） ====================

    /// <summary>
    /// 当前 Return 会话记录（V4F，会话级、不持久化）。
    /// 只承载「一次 Return 从 Settlement 到最终 Return Save」的跨调用 exactly-once 终态；
    /// Completed=true 仅在最终 Return Save 成功后由 CompleteExpeditionReturnSave(true) 置位。
    /// </summary>
    private static ExpeditionRecord _returnRecord;

    /// <summary>
    /// WP-B Return Integrity：本次**未完成** Return 会话的原始退出模式（RunEndMode）所有权。
    ///
    /// 与 `_returnRecord` / `_isCatchUpRunning` 同属同一次 Return 会话状态（同生命周期、不持久化、
    /// 不进 SaveData）——不新建第二套 Return Authority。
    ///   - 由 `BeginExpeditionReturn(effectiveElapsedDays, mode)` 写入；
    ///   - 由 `SetExpeditionPersonIds`（新出征会话）/ `DiscardExpeditionReturnRecord`（恢复路径）/
    ///     `ResetForTests` 清除；
    ///   - 用途：Settlement / Person Consequence 失败后的同会话 resume 必须沿用原始出口模式
    ///     （Defeat / SafeReturn 失败重试不得被通用 Retreat 入口改写）。
    /// </summary>
    private static RunEndMode? _pendingReturnMode;

    /// <summary>
    /// 读取未完成 Return 会话的原始退出模式。
    /// 返回 false = 无法解析（无未完成会话 / 会话由无模式语境的入口创建）→ 调用方必须 Fail Closed，
    /// 不得用其它出口模式改写原始 Return。
    /// </summary>
    public static bool TryGetPendingReturnMode(out RunEndMode mode)
    {
        if (_pendingReturnMode.HasValue)
        {
            mode = _pendingReturnMode.Value;
            return true;
        }
        mode = RunEndMode.SafeReturn;
        return false;
    }

    /// <summary>
    /// Return Catch-up 是否正在执行（进程内瞬态；只防 Reentrant / 并发，finally 复位，不承载任何“完成”语义）。
    /// “完成”唯一由 ExpeditionRecord.Completed 表达（Final Spec 修订 2 职责边界）。
    /// </summary>
    private static bool _isCatchUpRunning;

    /// <summary>elapsed-days 来源（Test / Not Frozen 公式注入点；默认恒 0 —— 无 Run 耗时公式时 Catch-up 为空跑）。</summary>
    private static System.Func<int> _elapsedDaysProvider = DefaultExpeditionElapsedDays;

    /// <summary>
    /// Catch-up 每日推进前的测试注入点（Between-Day 失败：抛异常 → 无部分写入、DaysSimulated 不变、
    /// 可从 DaysSimulated 安全续跑）。仅供契约测试制造 T12/T13 指定失败；ResetForTests 清空。
    /// </summary>
    public static System.Action<int> BeforeCatchUpDayTickTestHook;

    /// <summary>当前 Return 会话记录；null = 无进行中 Return 会话。</summary>
    public static ExpeditionRecord ActiveReturnRecord => _returnRecord;

    /// <summary>Return Catch-up 是否正在执行（读取守卫用；进程内瞬态）。</summary>
    public static bool IsReturnCatchUpRunning => _isCatchUpRunning;

    private static int DefaultExpeditionElapsedDays()
    {
        return 0;
    }

    /// <summary>设置 elapsed-days 提供器（Test / Not Frozen；null → 恢复默认恒 0）。</summary>
    public static void SetExpeditionElapsedDaysProvider(System.Func<int> provider)
    {
        _elapsedDaysProvider = provider ?? DefaultExpeditionElapsedDays;
        _elapsedMinutesProvider = null;
    }

    /// <summary>解析本次 Return 应逐日补算的有效天数（elapsed-days 正式公式未冻结，Test / Not Frozen）。</summary>
    public static int ResolveExpeditionElapsedDays()
    {
        System.Func<int> p = _elapsedDaysProvider ?? DefaultExpeditionElapsedDays;
        int value;
        try
        {
            value = p();
        }
        catch (System.Exception)
        {
            value = 0;
        }
        return value > 0 ? value : 0;
    }

    /// <summary>
    /// 开始一次 Return 会话（FinalizeReturn 入口第一步；Phase=Active）。
    ///
    /// 守卫（WP-B Return Integrity；两个 guard 语义不互换）：
    ///   - IsReturnCatchUpRunning → 拒绝（防重入）；
    ///   - 既有记录 Completed → 拒绝（重复 Return，跨调用 exactly-once 终态 no-op）；
    ///   - 既有记录 Failed → 拒绝（半完成记录不得被误重试；恢复 = Discard + 从最后成功 Save 重开）；
    ///   - 既有记录 Active / ReturnSettled / CatchUpComplete / OccupancyCleared → 拒绝
    ///     （**未完成 Return Record 不得重新 Begin**）：必须由 FinalizeReturn 按 Phase resume，
    ///      否则 DaysSimulated / RemainingCarryReturned / HealthConsequenceCommitted / ExperienceCommitted
    ///      会被重置 → 重复 Settlement / 重复逐日补算 / 重复 Carry 返还与 Consequence 提交。
    ///
    /// effectiveElapsedDays 为本次应逐日补算的有效天数（elapsed-days 公式 Test / Not Frozen）。
    /// mode 为本次 Return 的原始出口模式（SafeReturn / Retreat / Defeat），随会话保留用于 resume（不持久化）。
    /// </summary>
    public static bool BeginExpeditionReturn(int effectiveElapsedDays, RunEndMode mode)
    {
        return BeginExpeditionReturnMinutes(checked((long)System.Math.Max(0, effectiveElapsedDays) * 1440), mode);
    }

    public static bool BeginExpeditionReturnMinutes(long effectiveElapsedMinutes, RunEndMode mode)
    {
        if (effectiveElapsedMinutes < 0 || effectiveElapsedMinutes / 1440 > int.MaxValue) return false;
        if (_isCatchUpRunning)
        {
            Debug.LogWarning("[VillageRuntimeBridge] BeginExpeditionReturn 被拒绝：Return Catch-up 正在执行（防重入）。");
            return false;
        }
        if (_returnRecord != null)
        {
            if (_returnRecord.Completed)
            {
                Debug.LogWarning("[VillageRuntimeBridge] BeginExpeditionReturn 被拒绝：Return 会话已完成（Completed），重复 Return no-op。");
                return false;
            }
            if (_returnRecord.Phase == ExpeditionReturnPhase.Failed)
            {
                Debug.LogWarning("[VillageRuntimeBridge] BeginExpeditionReturn 被拒绝：存在 Failed 会话记录（禁止自动续跑；恢复 = Discard 并从最后成功 Save 重开）。");
                return false;
            }

            // WP-B（I-1）：从 Return 首次开始直到 Completed / Failed，不得创建第二个 Return Record。
            // 已有未完成记录（Active / ReturnSettled / CatchUpComplete / OccupancyCleared）必须 resume existing phase，
            // 不得 reset DaysSimulated / RemainingCarryReturned / HealthConsequenceCommitted / ExperienceCommitted
            // 等 exactly-once 标记（否则已完成阶段会被重复执行并产生重复副作用）。
            Debug.LogWarning("[VillageRuntimeBridge] BeginExpeditionReturn 被拒绝：存在未完成 Return 记录（phase="
                + _returnRecord.Phase + "）——未完成 Record 不得重新 Begin，必须按 Phase resume"
                + "（重复 Begin 会重建记录并重复 Settlement / 逐日补算 / Carry 返还 / Consequence 提交）。");
            return false;
        }

        EnsureInitialized(null);

        WorldInstant departure = _expeditionDeparture ?? Time.CurrentInstant;
        try { departure.AddMinutes(effectiveElapsedMinutes); } catch (System.Exception) { return false; }
        string identity = "return:" + checked(++_returnSequence);
        departure.GetDate(out int departureYear, out int departureMonth, out int departureDay);
        _pendingReturnMode = mode;
        _returnRecord = new ExpeditionRecord(departure.Minute, effectiveElapsedMinutes, identity,
            () => CompletedReturnDays(departure.Minute, effectiveElapsedMinutes, identity))
        {
            PersonIds = new List<string>(_expeditionPersonIds),
            DepartureYear = departureYear,
            DepartureMonth = departureMonth,
            DepartureDay = departureDay,
            EffectiveElapsedDays = (int)(effectiveElapsedMinutes / 1440),
            Phase = ExpeditionReturnPhase.Active,
            Completed = false
        };
        return true;
    }

    /// <summary>
    /// Test / Legacy 便捷重载（无出口模式语境的契约测试入口；等价于 SafeReturn）。
    /// Current 产品入口（ExpeditionUI.FinalizeReturn）一律使用带 RunEndMode 的重载，
    /// 以便 resume 时沿用原始出口模式（Defeat / SafeReturn 不被改写为 Retreat）。
    /// </summary>
    public static bool BeginExpeditionReturn(int effectiveElapsedDays)
    {
        return BeginExpeditionReturn(effectiveElapsedDays, RunEndMode.SafeReturn);
    }

    /// <summary>
    /// Settlement 成功后置位（Phase Active → ReturnSettled）。幂等：仅在 Active 时推进，
    /// 已 ReturnSettled 及之后的相位保持原状（可安全重复调用）。
    /// </summary>
    public static bool MarkExpeditionReturnSettled()
    {
        if (_returnRecord == null) return false;
        if (_returnRecord.Completed) return false;
        if (_returnRecord.Phase == ExpeditionReturnPhase.Failed) return false;
        if (_returnRecord.Phase == ExpeditionReturnPhase.Active)
        {
            _returnRecord.Phase = ExpeditionReturnPhase.ReturnSettled;
        }
        return _returnRecord.Phase == ExpeditionReturnPhase.ReturnSettled
            || _returnRecord.Phase == ExpeditionReturnPhase.CatchUpComplete
            || _returnRecord.Phase == ExpeditionReturnPhase.OccupancyCleared;
    }

    /// <summary>
    /// Return Catch-up 唯一逐日推进器（V4F 冻结：只走 Time.TickDay，不跳日、不用 Update/Jump）。
    ///
    /// 前置：存在 Return 会话且 Phase==ReturnSettled（Settlement 必须先成功）。
    /// 逐日：每日 TickDay 前先做互斥重叠检查（C-P1：远征者绝不在 Active Project 参与者集合）；
    ///       TickDay 完整返回后 DaysSimulated++；中途异常 → 当日不计入、记录转 Failed（PartialDayFailure）。
    /// 完成后 Phase → CatchUpComplete。
    /// </summary>
    public static ExpeditionCatchUpResult RunReturnCatchUp()
    {
        return RunIntradayReturnCatchUp();
    }

    /// <summary>
    /// C-P1 互斥重叠检查：Catch-up 任一日的“占用快照”语义要求远征者绝不在 Active Project 参与者集合。
    /// 若 Restore / 异常数据出现该重叠，立即在逐日推进前拒绝（不推进、不产生 Project 进度 / World 痕迹）。
    /// </summary>
    // A project membership reserves capacity; absent members never execute in the work adapter.
    private static bool HasProjectExpeditionOverlap() => false;

    /// <summary>
    /// 最终 Return Save 结果回报（Clear Occupancy 之后、进入 Town 时由 Return 链路调用）。
    /// 成功（saveSucceeded=true）→ Phase=Completed、Completed=true（跨调用 exactly-once 终态）；
    /// 失败 → 保持 OccupancyCleared、Completed=false（可只重试 Save，不重结算 / 不重补算）。
    /// </summary>
    public static void CompleteExpeditionReturnSave(bool saveSucceeded)
    {
        if (_returnRecord == null || _returnRecord.Completed) return;
        if (_returnRecord.Phase != ExpeditionReturnPhase.OccupancyCleared)
        {
            Debug.LogWarning("[VillageRuntimeBridge] CompleteExpeditionReturnSave 忽略：Phase=" + _returnRecord.Phase
                + "（必须在 OccupancyCleared 之后）。");
            return;
        }
        if (saveSucceeded)
        {
            _returnRecord.Phase = ExpeditionReturnPhase.Completed;
            _returnRecord.Completed = true;
            Time.CompleteFinalSave(_returnRecord.SessionIdentity);
        }
        else
        {
            // WP-B：Save false 只允许「只重试 Save」——保持 OccupancyCleared / Completed=false，
            // 不重跑 Settlement / Catch-up / Carry / Consequence / Clear。
            Debug.LogWarning("[VillageRuntimeBridge] 最终 Return Save 失败：保持 Phase=OccupancyCleared / Completed=false，"
                + "只允许重试 Final Save（不重结算 / 不重补算 / 不重复返还 Carry / 不重复提交 Consequence）。");
        }
    }

    /// <summary>
    /// 丢弃当前 Return 会话记录（失败恢复路径：Failed / 半完成记录不得被误重试，
    /// 恢复 = 丢弃记录并从最后一次成功 Save（离村前状态）重新开始）。
    /// </summary>
    public static void DiscardExpeditionReturnRecord()
    {
        _returnRecord = null;
        _pendingReturnMode = null;
    }

    /// <summary>
    /// 确保运行时实例存在（默认 Test 岗位目录）。
    /// save 非 null 时恢复存档（旧档 / 无档传 null → 保持默认起始状态，保证旧档兼容）。
    /// 重复调用无副作用（幂等）。
    /// </summary>
    public static void EnsureInitialized(VillageSaveData save)
    {
        EnsureCore(save, _testWorkplaces);
    }

    /// <summary>
    /// WP-3（Gate 3）：在默认 Test 岗位目录基础上追加额外岗位定义后确保运行时存在。
    /// 仅供内容/测试接线使用：Farm/Kitchen 岗位的 Capacity 来自传入的 VillageWorkplaceDef
    /// （Capacity 单一权威，仍由既有 Workplace 语义持有）；不改变默认 EnsureInitialized 的
    /// 岗位目录，因此不影响既有 World/UI/既有测试的默认表现。
    /// </summary>
    public static void EnsureInitializedWithExtraWorkplaces(
        VillageSaveData save, IReadOnlyList<VillageWorkplaceDef> extraWorkplaceDefs)
    {
        EnsureCore(save, MergeWorkplaceDefs(_testWorkplaces, extraWorkplaceDefs));
    }

    private static void EnsureCore(VillageSaveData save, IReadOnlyList<VillageWorkplaceDef> workplaceDefs)
    {
        if (Time == null) Time = new VillageTime { AdvanceSink = new DailyAdvanceSink() };
        if (State == null) State = new VillageState();
        if (IntradayWork == null) IntradayWork = new VillageIntradayWork();
        if (Resources == null) Resources = new VillageResourceState(); // WP-3：资源 Authority（运行时）
        if (Assignments == null) Assignments = new VillageAssignmentSystem(workplaceDefs);
        if (Projects == null) Projects = new VillageProjectSystem(_testProjects);
        if (SocialFacts == null) SocialFacts = new VillageSocialFactLedger();
        if (SocialResponse == null) SocialResponse = new VillageSocialResponseLog();
        if (PersonRest == null) PersonRest = new VillagePersonRestState();
        if (World == null)
        {
            World = new VillageWorldRegistry();
            World.EnsureWorkplacePlaces(workplaceDefs);
        }

        if (!_dayTickBound && Time != null)
        {
            Time.DayTicked += HandleDayTick;
            _dayTickBound = true;
        }

        if (save != null)
        {
            if (!_suppressTimeRestore) Time.Restore(save.Time);
            State.Restore(save.State);
            // Stage V2A：恢复 Primary Assignment / 执行事实（旧档缺 Assignments → Restore(null) 空系统）。
            if (Assignments != null && !_stagingBuildingLoad) Assignments.Restore(save.Assignments);
            // Stage V2B：恢复进行中的 Active Project（旧档缺 Projects → Restore(null) 空系统）。
            if (Projects != null && !_stagingBuildingLoad) Projects.Restore(save.Projects);
            _observedProjectCompletions = Projects.CompletedProjects.Count;
            if(!_stagingBuildingLoad) IntradayWork.Restore(save.IntradayWork);
            _workDayProduction = new VillageResourceDayResult();
            // Stage V3A：恢复客观社会事实账本（旧档缺 Social → Restore(null) 空账本）。
            if (SocialFacts != null) SocialFacts.Restore(save.Social);
            // Stage V3B：恢复 fired / pending / ChoiceHistory / RestUntil（旧档缺 SocialResponse → 空默认）。
            if (SocialResponse != null) SocialResponse.Restore(save.SocialResponse);
            // WP-Event-1D §7/§8：恢复正式 Rest Authority；旧档把 SocialResponse.Rests **幂等迁移**过来
            // （不猜 Person、不缩短 Until；重复 Load 不产生重复记录）。
            if (PersonRest != null)
            {
                PersonRest.Restore(save.PersonRest);
                if (save.SocialResponse != null)
                {
                    PersonRest.MigrateFromLegacy(save.SocialResponse.Rests);
                }
            }
            // Minimum World 第一包：恢复 ProjectSite 痕迹（旧档缺 World → 空默认；Workplace 由 Def 派生不落档）。
            if (World != null && !_stagingBuildingLoad) World.Restore(save.World);
            // WP-3（Gate 4）：恢复长期资源（旧档缺 Resources → 默认 0/0；SaveVersion 保持 1）。
            // 不保存 / 不恢复 Runtime Supply / RunState / ExpeditionRecord / Carry。
            if (Resources != null) Resources.Restore(save.Resources);
        }
    }

    /// <summary>合并岗位定义（去重 WorkplaceId；null 安全）。默认目录保持优先。</summary>
    private static List<VillageWorkplaceDef> MergeWorkplaceDefs(
        IReadOnlyList<VillageWorkplaceDef> baseDefs,
        IReadOnlyList<VillageWorkplaceDef> extraDefs)
    {
        var result = new List<VillageWorkplaceDef>();
        if (baseDefs != null)
        {
            foreach (var def in baseDefs)
            {
                if (def != null) result.Add(def);
            }
        }
        if (extraDefs != null)
        {
            foreach (var def in extraDefs)
            {
                if (def == null || string.IsNullOrEmpty(def.WorkplaceId)) continue;
                bool duplicate = false;
                foreach (var existing in result)
                {
                    if (existing != null && existing.WorkplaceId == def.WorkplaceId) { duplicate = true; break; }
                }
                if (!duplicate) result.Add(def);
            }
        }
        return result;
    }

    /// <summary>
    /// Day Tick → VillageState 测试规则 + Active Project 推进 + Primary Assignment 执行结算。
    /// 顺序语义（V2B）：
    ///   1) 取「日初占用快照」——包含当日仍占用的 Active Project 参与者；
    ///   2) Projects.OnDayTick：推进进度，达目标则 Completion 只结算一次并释放参与者；
    ///   3) Assignments.OnDayTick(日初占用)：完成当天该 Person 仍视为整天被 Project 占用，
    ///      不执行 Assignment；从次日起自动恢复实际执行（Opportunity Cost / Auto Return 精确语义）。
    /// </summary>
    private static void HandleDayTick(int day)
    {
        if (State != null) State.OnDayTick();

        var occupiedAtStartOfDay = GetTemporaryOccupiedPersonIds();

        ObserveProjectCompletions();
        var dailyFacts = IntradayWork.TakeDailyEvidence();
        Assignments.CommitLabor(dailyFacts);
        LastResourceExecutedFactCount = dailyFacts.Count;
        FinishWorkDayDiagnostics();
        RunFormationDayEnd(dailyFacts);

        // Earlier feedback must settle before opening a new window or building a Decision Snapshot.
        if (!AutonomousSocialFeedbackRuntimeBridge.DrainBarrier().Settled)
        {
            LastActivityDailyPhaseResult = ActivityDailyPhaseResult.SocialFeedbackUnsettled;
            return;
        }
        // Intraday opportunities are opened only by AdvanceSink at configured boundaries.
        // Midnight performs daily aggregation, never silently completing autonomous activities.
    }

    public enum ActivityDailyPhaseResult { Opened, Duplicate, WindowConflict, MissingPersonSource, InvalidWindow, SocialFeedbackUnsettled }
    public static ActivityDailyPhaseResult LastActivityDailyPhaseResult { get; private set; }

    private static ActivityDailyPhaseResult OpenAutonomousActivityOpportunity(IReadOnlyCollection<string> occupiedAtStartOfDay)
    {
        if (SimulationPersonSource == null)
            return LastActivityDailyPhaseResult = ActivityDailyPhaseResult.MissingPersonSource;
        // Use the existing pure availability resolver; do not sync or mutate Occupancy registrations.
        var views = VillagePersonAvailability.Resolve(BuildPersonInputs(SimulationPersonSource), _expeditionPersonIds);
        var occupied = new HashSet<string>(occupiedAtStartOfDay, System.StringComparer.Ordinal);
        var eligible = new List<string>();
        foreach (var person in views)
            if (person.Availability == VillageAvailability.InVillage && !occupied.Contains(person.PersonId))
                eligible.Add(person.PersonId);
        var window = new ActivityDailyWindow(new ActivityDayStamp(Time.Year, Time.Month, Time.Day), eligible, eligible);
        var result = ActivityRuntimeBridge.OpenDailyWindow(window);
        return LastActivityDailyPhaseResult = result == ActivityResult.Success ? ActivityDailyPhaseResult.Opened
            : result == ActivityResult.Duplicate ? ActivityDailyPhaseResult.Duplicate
            : result == ActivityResult.WindowConflict ? ActivityDailyPhaseResult.WindowConflict
            : ActivityDailyPhaseResult.InvalidWindow;
    }

    // ==================== WP-3：Resource Settlement（Gate 3 接入真实 Day Tick） ====================

    /// <summary>
    /// 在每日结算中运行资源结算（产出/加工/消费/Fulfillment-Hunger）。
    /// executedFacts 直接来自 Assignments.OnDayTick（唯一执行事实源）：
    ///   - Farm/Kitchen 当日实际执行者（非 Temporary Occupancy）才会产出/加工；
    ///   - InVillage Person = SimulationPersonSource 中未死亡且不在远征的 Character/Hero
    ///     （IFulfillmentHost 实现者）；不建立第二套 Person / Presence 权威。
    /// 全函数：任何缺失输入不抛异常（避免污染 Return Catch-up 的 Day 故障模型）。
    /// </summary>
    private static void RunResourceDayEnd(List<VillageDailyExecutionFact> dailyFacts)
    {
        if (Resources == null) return;

        var inVillage = BuildInVillageFulfillmentHosts();
        LastResourceDayResult = VillageResourceSettlement.SettleDay(
            Resources,
            _resourceWorkplaceDefs,
            dailyFacts,
            inVillage,
            ResourceFulfillmentThresholdPerPerson,
            ResourceEmergencyIngredientsPerShare,
            ResourceRequiredConsecutiveUnderfedDays);
        LastResourceInVillageCount = inVillage.Count;
        LastResourceExecutedFactCount = dailyFacts != null ? dailyFacts.Count : 0;
    }

    /// <summary>
    /// 【Test / Not Frozen】**只读诊断引用**：最近一次 Day Tick 的资源结算结果
    /// （Farm 产出 / Kitchen 加工 / 消费与 Fulfillment）。
    /// 仅用于真实验收与数值追踪（不参与规则、不持久化、不进入 Save）；不构成第二套 Settlement。
    /// </summary>
    public static VillageResourceDayResult LastResourceDayResult;

    /// <summary>【Test / Not Frozen】最近一次 Day Tick 的 InVillage Person 数（只读诊断）。</summary>
    public static int LastResourceInVillageCount;

    /// <summary>【Test / Not Frozen】最近一次 Day Tick 的执行事实条数（只读诊断）。</summary>
    public static int LastResourceExecutedFactCount;

    /// <summary>构建今日应在村饮食的 Person host 列表（远征 / 死亡 / 空 PersonId 排除；只用真实 PersonId）。</summary>
    private static List<IFulfillmentHost> BuildInVillageFulfillmentHosts()
    {
        var result = new List<IFulfillmentHost>();
        var source = SimulationPersonSource;
        if (source == null) return result;

        if (source.OwnedCharacters != null)
        {
            foreach (var c in source.OwnedCharacters)
            {
                if (c == null || c.IsDead || string.IsNullOrEmpty(c.PersonId)) continue;
                if (_expeditionPersonIds.Contains(c.PersonId)) continue;
                if (c is IFulfillmentHost host) result.Add(host);
            }
        }
        if (source.OwnedHeroes != null)
        {
            foreach (var h in source.OwnedHeroes)
            {
                if (h == null || string.IsNullOrEmpty(h.PersonId)) continue;
                if (_expeditionPersonIds.Contains(h.PersonId)) continue;
                if (h is IFulfillmentHost host) result.Add(host);
            }
        }
        return result;
    }

    // ==================== WP-3（Gate 4）：Expedition Food Carry 真实转移 ====================

    /// <summary>
    /// Departure：把 Village Food 真实转移为远征 Carry（不复制）。
    /// requestedUnits &lt;= 0 → 返回 0（无携带）。Village Food 不足时按可带量转移（最小 Test 策略，Test / Not Frozen），
    /// 不阻塞出发（保证既有无食物产品流程不回退）。返回实际转移量（= Expedition Carry 增加量）。
    /// </summary>
    public static int CarryFoodForDeparture(int requestedUnits)
    {
        if (!GuardWorldWrite()) return 0;
        EnsureInitialized(null);
        if (Resources == null || requestedUnits <= 0) return 0;
        int actual = Resources.Food < requestedUnits ? Resources.Food : requestedUnits;
        if (actual <= 0) return 0;
        return Resources.TryConsumeFood(actual) ? actual : 0;
    }

    /// <summary>
    /// Return：Catch-up 全部完成后把剩余 Carry 返还 Village Food（真实转移；Session 内 exactly-once）。
    /// 守卫（Return Transaction Authority）：仅当存在 Return 会话、相位已越过 ReturnSettled（CatchUpComplete 及之后）、
    /// 未 Completed / Failed、且 RemainingCarryReturned 尚未置位时才执行一次。
    /// RemainingCarryReturned 属于 ExpeditionRecord，不进 VillageResourceState / UI。
    ///
    /// 返回 bool = 本次是否真实发生一次返还（true 仅当 Successfully Applied）——
    /// 保持既有公共语义不变：已返还（幂等）与拒绝都返回 false。
    /// 需要区分「已返还（可继续）」与「拒绝（必须 Fail Closed）」的 Return orchestration 使用
    /// ReturnRemainingCarryStage（WP-B I-6）。
    /// </summary>
    public static bool ReturnRemainingCarry(int remainingCarryUnits)
    {
        return ReturnRemainingCarryStage(remainingCarryUnits) == ReturnCarryStageResult.Applied;
    }

    /// <summary>
    /// Return 剩余 Carry 返还的完整结果入口（WP-B T4）：Applied / AlreadyApplied / Rejected。
    ///
    /// 守卫与既有 ReturnRemainingCarry 完全一致（同一次返还、同一 exactly-once 标记，不建立第二套资源语义）；
    /// 本方法只把「已返还（幂等）」与「拒绝（失败）」区分开：
    ///   - Applied / AlreadyApplied → 可继续 Person Consequence 与 Clear Occupancy；
    ///   - Rejected → 必须 Fail Closed（不 Consequence、不 Clear、不落盘）。
    /// </summary>
    public static ReturnCarryStageResult ReturnRemainingCarryStage(int remainingCarryUnits)
    {
        if (_returnRecord == null || _returnRecord.Completed) return ReturnCarryStageResult.Rejected;
        if (_returnRecord.Phase == ExpeditionReturnPhase.Failed) return ReturnCarryStageResult.Rejected;
        if (_returnRecord.Phase == ExpeditionReturnPhase.Active
            || _returnRecord.Phase == ExpeditionReturnPhase.ReturnSettled) return ReturnCarryStageResult.Rejected;

        // 会话内已返还：幂等命中（不是失败），后续阶段可以继续。
        if (_returnRecord.RemainingCarryReturned) return ReturnCarryStageResult.AlreadyApplied;

        if (remainingCarryUnits < 0) return ReturnCarryStageResult.Rejected;
        if (Resources == null) return ReturnCarryStageResult.Rejected;

        if (remainingCarryUnits > 0) Resources.AddFood(remainingCarryUnits);
        _returnRecord.RemainingCarryReturned = true;
        return ReturnCarryStageResult.Applied;
    }

    /// <summary>
    /// 绑定当前 Village 世界会话的长期人物真相源（场景入口显式调用；只引用同一 PlayerCollection）。
    /// 传 null 可解除绑定。
    /// </summary>
    public static void SetSimulationPersonSource(PlayerCollection collection)
    {
        if (ReferenceEquals(SimulationPersonSource, collection)) return;
        // Repair a missing host binding without mutating the paused simulation or its cursor.
        if(Time?.AdvanceSession?.Diagnostic=="MissingActivityPersonSource" && Time.AdvanceSession.State==WorldAdvanceState.Yielded)
        { SimulationPersonSource=collection;return; }
        if (!GuardWorldWrite()) return;
        SimulationPersonSource = collection;
        RefreshWorkIntent();
    }

    /// <summary>
    /// Day End：统一运行一次 Formation Resolver（Stage 2）。
    /// - 未注入 FormationRuntimeServices（或 Config 缺失）→ 跳过（不猜测数值、不产生 Formation 行为）；
    /// - Work Evidence 只来自 Assignment 当日实际执行 delta 事实（VillageDailyExecutionFact），
    ///   经测试映射（WorkEvidenceMapper）转成标准化 FormationEvidenceContribution；
    /// - 有既有 FormationState 但当日无 Work Evidence 的普通 Character 同样参与 Day End
    ///   （inactivity / decay 继续；Project / Temporary Occupancy 未执行日不产生 Work Evidence）；
    /// - CharacterId / HeroId 绝不作 Person truth：只按精确 PersonId 在 SimulationPersonSource 中查找，
    ///   找不到（含无绑定 / 死亡 / Hero）直接跳过，不创建、不兜底。
    /// </summary>
    private static void RunFormationDayEnd(List<VillageDailyExecutionFact> dailyFacts)
    {
        if (Formation == null || Formation.Config == null) return;
        if (Time == null) return;
        if (SimulationPersonSource == null) return;

        // 按 PersonId 聚合当日 Assignment delta（Work Evidence 输入）。
        var contributionsByPerson = new Dictionary<string, List<FormationEvidenceContribution>>();
        if (Formation.WorkEvidenceMapper != null && dailyFacts != null)
        {
            foreach (var fact in dailyFacts)
            {
                if (fact == null || string.IsNullOrEmpty(fact.PersonId)) continue;
                var mapped = Formation.WorkEvidenceMapper.MapWorkplaceDay(fact.WorkplaceId);
                if (mapped == null || mapped.Count == 0) continue;
                if (!contributionsByPerson.TryGetValue(fact.PersonId, out var list))
                {
                    list = new List<FormationEvidenceContribution>();
                    contributionsByPerson[fact.PersonId] = list;
                }
                foreach (var c in mapped) list.Add(new FormationEvidenceContribution(c.ProfessionId, c.Amount * fact.WorkedMinutes / 480f, c.Source));
            }
        }

        var day = new FormationDayContext(Time.Year, Time.Month, Time.Day);
        foreach (var character in SimulationPersonSource.OwnedCharacters)
        {
            if (character == null) continue;
            if (character.IsDead) continue; // 已死亡人物不继续 Formation 演化
            if (string.IsNullOrEmpty(character.PersonId)) continue; // 无 PersonId 不猜测（no fallback）
            if (!(character is IFormationHost host)) continue; // 普通 Character 才拥有 Formation（Hero 不含）

            bool hasState = host.FormationState != null
                && host.FormationState.Entries != null
                && host.FormationState.Entries.Count > 0;
            bool hasContribution = contributionsByPerson.TryGetValue(character.PersonId, out var contribs)
                                   && contribs != null && contribs.Count > 0;
            if (!hasState && !hasContribution) continue;

            FormationResolver.AdvanceDay(
                host, day,
                hasContribution ? contribs : null,
                Formation.Config,
                Formation.Registry);
        }
    }

    /// <summary>生成 Village 存档快照（供 SaveSystem；未初始化时返回 null → 存档不含 Village 区块）。</summary>
    public static VillageSaveData BuildSaveData()
    {
        if (Time == null || State == null) return null;
        return new VillageSaveData
        {
            Time = Time.ToSaveData(),
            Buildings = Buildings.ToSaveData(),
            IntradayWork = IntradayWork?.ToSaveData(),
            State = State.ToSaveData(),
            Assignments = Assignments != null ? Assignments.ToSaveData() : null,
            Projects = Projects != null ? Projects.ToSaveData() : null,
            Social = SocialFacts != null ? SocialFacts.ToSaveData() : null,
            SocialResponse = SocialResponse != null ? SocialResponse.ToSaveData() : null,
            PersonRest = PersonRest != null ? PersonRest.ToSaveData() : null,
            World = World != null ? World.ToSaveData() : null,
            Resources = Resources != null ? Resources.ToSaveData() : null
        };
    }

    /// <summary>从既有长期人物真相源构建输入列表（角色 + 英雄；只取判可用所需字段，不复制长期数据本体）。</summary>
    public static List<VillagePersonInput> BuildPersonInputs(PlayerCollection playerData)
    {
        var list = new List<VillagePersonInput>();
        if (playerData == null) return list;

        if (playerData.OwnedCharacters != null)
        {
            foreach (var c in playerData.OwnedCharacters)
            {
                if (c == null) continue;
                if (string.IsNullOrEmpty(c.PersonId))
                {
                    // Foundation Identity Cleanup：Village 输入只使用真实 PersonId；空 PersonId 属非法身份，
                    // 不得用模板 CharacterId 兜底，跳过并告警（不进入 Village 可用视图/占用）。
                    Debug.LogWarning("[VillageRuntimeBridge] BuildPersonInputs：角色（CharacterId="
                        + c.CharacterId + "）缺少真实 PersonId，已跳过（不用模板 CharacterId 当 Person）。");
                    continue;
                }
                list.Add(new VillagePersonInput
                {
                    PersonId = c.PersonId,
                    DisplayName = c.DisplayName,
                    IsDead = c.IsDead
                });
            }
        }

        if (playerData.OwnedHeroes != null)
        {
            foreach (var h in playerData.OwnedHeroes)
            {
                if (h == null) continue;
                if (string.IsNullOrEmpty(h.PersonId))
                {
                    // Foundation Identity Cleanup：Village 输入只使用真实 PersonId；空 PersonId 属非法身份，
                    // 不得用模板 HeroId 兜底，跳过并告警。
                    Debug.LogWarning("[VillageRuntimeBridge] BuildPersonInputs：英雄（HeroId="
                        + h.HeroId + "）缺少真实 PersonId，已跳过（不用模板 HeroId 当 Person）。");
                    continue;
                }
                list.Add(new VillagePersonInput
                {
                    PersonId = h.PersonId,
                    DisplayName = h.DisplayName,
                    IsDead = false // 英雄不会永久死亡
                });
            }
        }

        return list;
    }

    /// <summary>
    /// 当前 Village 人物可用状态视图（远征中人物 → Expedition，不会同时 InVillage）。
    /// 解析前从长期人物 IsDead 单向同步 Unavailable 运行时登记（V2A：供 Assignment 暂停执行判断）。
    /// </summary>
    public static List<VillagePersonView> GetPersonAvailability(PlayerCollection playerData)
    {
        var inputs = BuildPersonInputs(playerData);
        SyncUnavailablePersonIds(inputs);
        return VillagePersonAvailability.Resolve(inputs, _expeditionPersonIds);
    }

    /// <summary>从长期人物输入同步暂不可用（已死亡）人物登记：PlayerCollection → 运行时单向，不写长期字段。</summary>
    private static void SyncUnavailablePersonIds(IEnumerable<VillagePersonInput> inputs)
    {
        var dead = new List<string>();
        if (inputs != null)
        {
            foreach (var p in inputs)
            {
                if (p == null || !p.IsDead || string.IsNullOrEmpty(p.PersonId)) continue;
                if (!dead.Contains(p.PersonId)) dead.Add(p.PersonId);
            }
        }
        SetUnavailablePersonIds(dead);
    }

    // ==================== V2A Assignment 公共 API（Stage V2C 前的逻辑/接线入口；V2C 再接入玩家操作） ====================

    /// <summary>尝试给 Person 指定一个 Primary Assignment（委托 AssignmentSystem）。</summary>
    public static VillageAssignmentResult TryAssignPerson(string personId, string workplaceId)
    {
        if (!GuardWorldWrite()) return VillageAssignmentResult.InvalidPersonId;
        EnsureInitialized(null);
        var result = Assignments != null
            ? Assignments.TryAssign(personId, workplaceId)
            : VillageAssignmentResult.InvalidPersonId;
        if (result == VillageAssignmentResult.Success) RefreshWorkIntent();
        return result;
    }

    /// <summary>移除 Person 的 Primary Assignment（玩家主动修改）。</summary>
    public static bool RemovePersonAssignment(string personId)
    {
        if (!GuardWorldWrite()) return false;
        EnsureInitialized(null);
        return Assignments != null && Assignments.RemoveAssignment(personId);
    }

    /// <summary>查询 Person 当前 Primary Assignment 记录；无则 null。</summary>
    public static VillageAssignmentRecord GetPersonAssignmentRecord(string personId)
    {
        return Assignments != null ? Assignments.GetRecord(personId) : null;
    }

    /// <summary>查询 Person 当前岗位 Id；无则 null。</summary>
    public static string GetPersonWorkplaceId(string personId)
    {
        return Assignments != null ? Assignments.GetWorkplaceId(personId) : null;
    }

    /// <summary>查询岗位当前占用 Person 数。</summary>
    public static int GetWorkplaceOccupantCount(string workplaceId)
    {
        return Assignments != null ? Assignments.GetOccupantCount(workplaceId) : 0;
    }

    /// <summary>查询岗位当前绑定的 PersonId 列表。</summary>
    public static List<string> GetWorkplaceOccupantPersonIds(string workplaceId)
    {
        return Assignments != null ? Assignments.GetOccupantPersonIds(workplaceId) : new List<string>();
    }

    /// <summary>岗位是否已满员。</summary>
    public static bool IsWorkplaceFull(string workplaceId)
    {
        return Assignments != null && Assignments.IsWorkplaceFull(workplaceId);
    }

    /// <summary>
    /// Profession Formation Source Hook (Test)：只读执行事实来源（仅实际执行产生；不构成正式 Profession 系统）。
    /// </summary>
    public static List<VillageExecutionFact> GetProfessionFormationSourceHookFacts()
    {
        return Assignments != null ? Assignments.GetProfessionFormationSourceHookFacts() : new List<VillageExecutionFact>();
    }

    // ==================== V2B Project 公共 API（Stage V2C 前的逻辑/接线入口；V2C 再接入玩家操作） ====================

    /// <summary>
    /// 启动一个 Test Project 并一次性加入参与人（任一失败则整体失败并回滚已创建的 Project）。
    /// 参与者中若有人已在远征 / 暂不可用，返回 PersonCurrentlyUnavailable（调度层防御，避免一人多占用）。
    /// </summary>
    public static VillageProjectResult TryStartProject(string projectDefId, string[] participantPersonIds)
    {
        if (!GuardWorldWrite()) return VillageProjectResult.PersonCurrentlyUnavailable;
        EnsureInitialized(null);
        if (Projects == null) return VillageProjectResult.UnknownProjectDef;

        VillageProject created;
        var createResult = Projects.TryCreateProject(projectDefId, out created);
        if (createResult != VillageProjectResult.Success)
        {
            return createResult;
        }

        if (participantPersonIds != null)
        {
            foreach (var personId in participantPersonIds)
            {
                var add = AddProjectParticipantCore(created.ProjectId, personId);
                if (add != VillageProjectResult.Success)
                {
                    // 回滚：剔除刚创建但未成功凑齐参与者的 Project（保留既有 Active / Completed 记录）
                    Projects.TryRemoveActiveProject(created.ProjectId);
                    return add;
                }
            }
        }

        // V3A：仅在整批参与者全部真实成功加入后，记录 Temporary Commitment 客观社会事实。
        // 任一参与者失败已整体回滚 → 不记录任何事实；同一 Person 同一 Project 重复加入 → ledger 幂等去重。
        if (participantPersonIds != null && SocialFacts != null && Time != null)
        {
            foreach (var personId in participantPersonIds)
            {
                if (string.IsNullOrEmpty(personId)) continue;
                SocialFacts.RecordTemporaryCommitment(
                    personId, projectDefId, created.ProjectId, Time.Year, Time.Month, Time.Day);
            }
        }

        // V3B：在真实成功加入且 V3A 事实已记录之后求值 Recognition（post-add，不回滚当前加入）。
        if (participantPersonIds != null)
        {
            foreach (var personId in participantPersonIds)
            {
                if (string.IsNullOrEmpty(personId)) continue;
                EvaluateDraftBuildupResponse(personId);
            }
        }
        return VillageProjectResult.Success;
    }

    /// <summary>尝试给 Active Project 加入参与人（含 Expedition / Unavailable 调度防御）。</summary>
    public static VillageProjectResult TryAddProjectParticipant(string projectId, string personId)
    {
        if (!GuardWorldWrite()) return VillageProjectResult.PersonCurrentlyUnavailable;
        EnsureInitialized(null);
        var result = AddProjectParticipantCore(projectId, personId);
        if (result == VillageProjectResult.Success && SocialFacts != null && Time != null)
        {
            // V3A：仅在真实成功加入后记录 Temporary Commitment 客观社会事实（重复加入 → ledger 幂等去重）。
            var active = Projects != null ? Projects.GetActiveProject(projectId) : null;
            if (active != null)
            {
                SocialFacts.RecordTemporaryCommitment(
                    personId, active.ProjectDefId, active.ProjectId, Time.Year, Time.Month, Time.Day);
            }
        }
        if (result == VillageProjectResult.Success)
        {
            // V3B：post-add 求值（真实成功且事实已记录后）。
            EvaluateDraftBuildupResponse(personId);
        }
        return result;
    }

    // ==================== V3A Social Fact 查询（只读事实查询；无阈值 / 无 Recognition 规则） ====================

    /// <summary>
    /// 查询某 Person 在指定自然年内被成功投入 Active Project（Temporary Commitment）的次数（V3A 事实查询）。
    /// 只读派生事实；本阶段不在此消费任何阈值 / 形成 Recognition（阈值仅允许存在于 Test / Not Frozen 测试）。
    /// </summary>
    public static int CountTemporaryCommitmentsInYear(string personId, int year)
    {
        return SocialFacts != null ? SocialFacts.CountTemporaryCommitmentsInYear(personId, year) : 0;
    }

    // ==================== V3B Recognition → Response → New Decision（Bridge/Social 层） ====================

    /// <summary>当前等待玩家处理的 Recognition 回应（无则 null；V3B 至多一条）。</summary>
    public static VillagePendingResponse GetPendingSocialResponse()
    {
        return SocialResponse != null ? SocialResponse.PendingResponse : null;
    }

    /// <summary>
    /// 解析当前待处理回应（玩家选择 A=GrantRest / B=ContinueUsing）。
    /// A → 写入 ChoiceHistory + RestUntil（当月末，Test 规则）；B → 只写 ChoiceHistory。
    /// 解析后再对当前已成功 Project 调度无任何影响（post-add 语义）。
    /// </summary>
    public static bool ResolveSocialResponse(VillageSocialResponseChoice choice)
    {
        if (SocialResponse == null || Time == null) return false;
        string identity = VillageEventFlow.LegacyAdvanceInstanceId(GetPendingSocialResponse());
        if (!TryBeginBlockingEventOption(identity)) return false;
        bool success = false;
        try { success = SocialResponse.ResolvePending(choice, Time.Year, Time.Month, Time.Day); return success; }
        finally { EndBlockingEventOption(identity, success); }
    }

    /// <summary>
    /// 该 Person 当前是否处于 Rest Gate 恢复窗口（被拦截状态）。
    /// WP-Event-1D：正式 Rest Authority 优先；旧 `SocialResponse.Rests` 继续兼容（OR），因此迁移期间行为不变。
    /// </summary>
    public static bool IsPersonInRestWindow(string personId)
    {
        if (Time == null) return false;
        if (PersonRest != null && PersonRest.IsRestBlocked(personId, Time.Year, Time.Month, Time.Day))
            return true;
        if (SocialResponse == null) return false;
        return SocialResponse.IsPersonRestBlocked(personId, Time.Year, Time.Month, Time.Day);
    }

    /// <summary>
    /// **授予恢复期**（WP-Event-1D 正式写入口；Unified Event 的 `GrantPersonRest` Adapter 调用）。
    /// V0.1 规则（Test / Not Frozen，保持原语义）：休整到**当前 Village 月的最后一天**；
    /// 含当天仍被 Rest Gate 拦截，下月 1 日（CurrentDate &gt; Until）恢复。
    /// </summary>
    public static bool GrantPersonRestUntilMonthEnd(string personId)
    {
        if (!GuardWorldWrite()) return false;
        if (string.IsNullOrEmpty(personId) || Time == null) return false;
        if (PersonRest == null) PersonRest = new VillagePersonRestState();

        int untilDay = VillageTime.DaysInMonth(Time.Year, Time.Month);
        return PersonRest.GrantRestUntil(personId, Time.Year, Time.Month, untilDay);
    }

    /// <summary>该 Person 恢复窗口的可读描述（无则空串；Test / Not Frozen 文案）。</summary>
    public static string GetPersonRestDescription(string personId)
    {
        if (Time == null) return "";

        var rest = PersonRest != null ? PersonRest.GetLatestRest(personId) : null;
        if (rest == null && SocialResponse != null) rest = SocialResponse.GetLatestRest(personId);
        if (rest == null) return "";

        return "休整至 " + rest.UntilYear + " 年 " + rest.UntilMonth + " 月 " + rest.UntilDay + " 日（自选后果；含当天仍拦截，下月 1 日起恢复）";
    }

    /// <summary>最近一次 ChoiceHistory 的响应方向（无则 null）。</summary>
    public static VillageResponseChoiceRecord GetLatestChoice(string personId)
    {
        if (SocialResponse == null) return null;
        VillageResponseChoiceRecord latest = null;
        foreach (var c in SocialResponse.Choices)
        {
            if (c == null || c.PersonId != personId) continue;
            if (latest == null)
            {
                latest = c;
                continue;
            }
            long ca = c.Year * 10000L + c.Month * 100L + c.Day;
            long la = latest.Year * 10000L + latest.Month * 100L + latest.Day;
            if (ca >= la) latest = c;
        }
        return latest;
    }

    /// <summary>
    /// V3B 求值（post-add）—— **WP-Event-1D Gate D1：正式产品路径已切换到 Unified Event Core**。
    ///
    /// 真实时序（冻结）：
    ///   SocialFactLedger 年内计数 → `VillageEventFlow.RaiseRecognition`（TriggerType = VillageRecognitionRaised，
    ///   TriggerKey 稳定派生，Context = RecognitionPerson / RecognitionType / ObservedCount）
    ///   → EventResolver → `EventRuntimeBridge.PendingVillageEvent`。
    ///
    /// 边界（§40）：
    ///   - 本路径**不再**调用 `SocialResponse.OpenBuildupResponse`，即**不创建 Legacy Pending / fired**；
    ///   - 阈值与 PatternKey 仍来自 VillageTestSocialPatterns（Test / Not Frozen），不在此硬编码；
    ///   - 「没有候选事件」（例如年内计数未达条件）是正常 Gameplay 结果，不是错误，也不产生 Pending；
    ///   - 配置不可用 / 运行时不可用 → Fail Closed（不建 Pending，诊断见 `VillageEventFlow.LastDiagnostic`）。
    /// </summary>
    private static void EvaluateDraftBuildupResponse(string personId)
    {
        if (string.IsNullOrEmpty(personId)) return;
        if (SocialFacts == null || Time == null) return;

        int count = SocialFacts.CountTemporaryCommitmentsInYear(personId, Time.Year);
        VillageEventFlow.RaiseRecognition(personId, VillageTestSocialPatterns.BuildupDraftPatternKey, count,
            Time.Year);
    }

    /// <summary>
    /// **【Legacy / 迁移专用】** 清除 Legacy Pending（WP-Event-1D §21 · B4）。
    /// 唯一调用方 = `VillageEventFlow.TryMigrateLegacyPending`（旧档收敛）；正式产品路径不再创建 Legacy Pending。
    /// </summary>
    public static bool ClearLegacyPendingMigratedByEventCutover()
    {
        return SocialResponse != null && SocialResponse.ClearLegacyPendingForMigration();
    }

    /// <summary>Rest Gate 查询（运行时日期快照）。WP-Event-1D：正式 Rest Authority 优先，旧 Rests 兼容。</summary>
    private static bool IsPersonCurrentlyInRestWindow(string personId)
    {
        if (Time == null) return false;
        if (PersonRest != null && PersonRest.IsRestBlocked(personId, Time.Year, Time.Month, Time.Day))
            return true;
        if (SocialResponse == null) return false;
        return SocialResponse.IsPersonRestBlocked(personId, Time.Year, Time.Month, Time.Day);
    }

    private static VillageProjectResult AddProjectParticipantCore(string projectId, string personId)
    {
        if (string.IsNullOrEmpty(personId)) return VillageProjectResult.InvalidPersonId;
        // V3B：Rest Gate 防线（自选后果）。只拦“加入新 Project”；不影响 Expedition / 当前 Active Project / Primary Assignment。
        if (IsPersonCurrentlyInRestWindow(personId))
            return VillageProjectResult.PersonInRestWindow;
        if (IsPersonCurrentlyExpeditionOrUnavailable(personId))
            return VillageProjectResult.PersonCurrentlyUnavailable;

        var result = Projects != null
            ? Projects.TryAddParticipant(projectId, personId)
            : VillageProjectResult.UnknownProject;
        if (result == VillageProjectResult.Success) RefreshWorkIntent();
        return result;
    }

    private static bool IsPersonCurrentlyExpeditionOrUnavailable(string personId)
    {
        foreach (var id in _expeditionPersonIds)
        {
            if (id == personId) return true;
        }
        foreach (var id in _unavailablePersonIds)
        {
            if (id == personId) return true;
        }
        return false;
    }

    /// <summary>当前全部 Active Project 参与者 PersonId（Temporary Occupancy 查询用）。</summary>
    public static List<string> GetActiveProjectParticipantPersonIds()
    {
        return Projects != null ? Projects.GetActiveParticipantPersonIds() : new List<string>();
    }

    /// <summary>Person 当前所在的 Active ProjectId；无则 null。</summary>
    public static string GetActiveProjectIdOfPerson(string personId)
    {
        if (Projects == null) return null;
        var p = Projects.GetActiveProjectOfPerson(personId);
        return p != null ? p.ProjectId : null;
    }

    /// <summary>按 Id 查询 Active Project 进度；无则 -1。</summary>
    public static int GetActiveProjectProgress(string projectId)
    {
        if (Projects == null) return -1;
        var p = Projects.GetActiveProject(projectId);
        return p != null ? p.Progress : -1;
    }

    /// <summary>
    /// 统一 Temporary Occupancy 原因（供远征选人等调度入口只读展示；复用唯一占用真相源，不新增第二套状态）。
    /// 返回空串 = 在村可调度；否则返回具体占用原因（远征中 / 暂不可用 / 投入 Active Project「…」）。
    /// </summary>
    public static string GetPersonOccupancyReason(string personId)
    {
        if (string.IsNullOrEmpty(personId)) return "";
        foreach (var id in _expeditionPersonIds)
        {
            if (id == personId) return "正在远征中（Temporary Occupancy）";
        }
        foreach (var id in _unavailablePersonIds)
        {
            if (id == personId) return "暂不可用（Unavailable）";
        }
        if (Projects != null)
        {
            var p = Projects.GetActiveProjectOfPerson(personId);
            if (p != null && IsExecutingWork(personId))
            {
                var def = Projects.GetProjectDef(p.ProjectDefId);
                string name = def != null && !string.IsNullOrEmpty(def.DisplayName) ? def.DisplayName : p.ProjectId;
                return "正在投入 Active Project「" + name + "」（Temporary Occupancy）";
            }
        }
        return "";
    }

    /// <summary>Person 当前是否处于任一 Temporary Occupancy（Expedition / Unavailable / Active Project）。</summary>
    public static bool IsPersonTemporarilyOccupied(string personId)
    {
        return !string.IsNullOrEmpty(GetPersonOccupancyReason(personId));
    }

    /// <summary>
    /// Person Presence 派生查询（第一包；只读，不新建第二套 occupancy / 人物长期数据）。
    /// 输入全部来自既有真相源：Expedition / Unavailable 登记、Active Project、Primary Assignment。
    /// </summary>
    public static VillagePersonPresence GetPersonPresence(string personId)
    {
        string activeProjectId = null;
        if (Projects != null) activeProjectId = GetActiveProjectIdOfPerson(personId);
        string workplaceId = GetPersonWorkplaceId(personId);
        return VillageWorldPresence.Resolve(personId, _expeditionPersonIds, _unavailablePersonIds, activeProjectId, workplaceId);
    }

    /// <summary>测试专用：清空运行时单例与登记，避免跨测试污染。</summary>
    public static void ResetForTests()
    {
        ResetBuildingsForNewGame();
        // This test-only reset creates a new clock/world fixture. Persistent time-bearing
        // activities and opportunity markers must not leak from the previous fixture.
        ActivityRuntimeBridge.ResetForTests();
        AutonomousSelectionRuntimeBridge.ResetForTests();
        AutonomousSocialFeedbackRuntimeBridge.ResetForTests();
        ResetIntradayHost();
        if (Time != null)
        {
            Time.DayTicked -= HandleDayTick;
        }
        Time = null;
        State = null;
        Assignments = null;
        Projects = null;
        IntradayWork = null; _observedProjectCompletions = 0;
        _workDayProduction = new VillageResourceDayResult();
        SocialFacts = null;
        SocialResponse = null;
        PersonRest = null;
        World = null;
        Formation = null;
        SimulationPersonSource = null;
        Resources = null; // WP-3（Gate 3）：资源 Authority 随 Reset 重建；配置回默认（Test / Not Frozen）
        ResourceFulfillmentThresholdPerPerson = 1;
        ResourceEmergencyIngredientsPerShare = 2;
        ResourceRequiredConsecutiveUnderfedDays = 1;
        ResourceCarryPerPerson = 2;                    // V4 Gate 2：产品链 Test 值（Decision D / OQ2）
        ResourcePerPersonDailyConsumptionRate = 1;     // V4 Gate 2：产品链 Test 值（Decision D / OQ2）
        LastResourceDayResult = null;                  // V4 Functional Repair：只读诊断引用复位
        LastResourceInVillageCount = 0;
        LastResourceExecutedFactCount = 0;
        _dayTickBound = false;
        _expeditionPersonIds.Clear();
        _unavailablePersonIds.Clear();
        // V4F：清空 Return 会话记录 / Catch-up 运行态 / elapsed-days 注入 / 测试钩子。
        _returnRecord = null;
        _pendingReturnMode = null; // WP-B：原始出口模式所有权随会话记录一起清除
        _isCatchUpRunning = false;
        _elapsedDaysProvider = DefaultExpeditionElapsedDays;
        BeforeCatchUpDayTickTestHook = null;
        IsSimulationActive = false;
    }
}
