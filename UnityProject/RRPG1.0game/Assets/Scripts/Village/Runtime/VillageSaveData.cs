using System;
using System.Collections.Generic;

/// <summary>
/// Village 存档 DTO（Village.Runtime 纯数据）。
/// 由 SaveSystem 作为 SaveData 顶层可空区块序列化；旧档缺失该字段时以默认状态初始化（见 SaveSystem）。
/// </summary>
[Serializable]
public class VillageSaveData
{
    public VillageBuildingsSaveData Buildings;
    public VillageIntradayWorkSaveData IntradayWork;
    public VillageTimeSaveData Time;
    public VillageStateSaveData State;

    /// <summary>
    /// Primary Assignment 区块（Stage V2A，可空）。
    /// 旧档缺失时以空系统初始化（向后兼容，不提升 SaveVersion）；
    /// 记录以 PersonId 为键的 Village-specific 长期安排，不复制长期 Person 数据。
    /// </summary>
    public VillageAssignmentsSaveData Assignments;

    /// <summary>
    /// Active Project 区块（Stage V2B，可空）。
    /// 只保存进行中的 Project（Completed 不存档、不再占用）；
    /// 旧档缺失时以空系统初始化（向后兼容，不提升 SaveVersion）。
    /// </summary>
    public VillageProjectsSaveData Projects;

    /// <summary>
    /// 客观社会事实账本区块（Stage V3A，可空）。
    /// 只保存已发生的 Temporary Commitment 动作事实（追加式，不复制 Assignment / Project 当前状态）；
    /// 旧档缺失时以空账本初始化（向后兼容，不提升 SaveVersion）。
    /// </summary>
    public VillageSocialSaveData Social;

    /// <summary>
    /// WP-3 长期资源区块（Gate 4；可空）。
    /// 只保存 Ingredients / Food（VillageResourceState 存档镜像）；不保存 Runtime / Carry / Person / Building 状态。
    /// 旧档缺失 → 默认 0/0（SaveVersion 保持 1，向后兼容）。
    /// </summary>
    public VillageResourceSaveData Resources;

    /// <summary>
    /// V3B Recognition → Response → New Decision 最小状态区块（可空）。
    /// 只保存 fired / 单条 pending / ChoiceHistory / RestUntil 四类轻量状态；
    /// Recognition / Observer / Pattern 均为派生，不持久化。旧档缺失 → 空默认（不提升 SaveVersion）。
    /// </summary>
    public VillageSocialResponseSaveData SocialResponse;

    /// <summary>
    /// 人物恢复期权威区块（WP-Event-1D §7 正式抽离，可空增量）。
    /// 只保存 Person → RestUntil（日期）；旧档缺失 → 空默认（不提升 SaveVersion）。
    /// 旧档若只有 `SocialResponse.Rests`，由 VillageRuntimeBridge 在 Load 时**幂等迁移**到本区块
    /// （见 `VillagePersonRestState.MigrateFromLegacy`）。
    /// </summary>
    public List<VillageRestSaveData> PersonRest;

    /// <summary>
    /// Minimum Village World Integration 第一包最小 World 区块（可空）。
    /// 只保存不能从现有 Runtime 派生的 ProjectSite 痕迹（Workplace 由岗位定义派生，不落档）；
    /// 旧档缺失 → 空默认（不提升 SaveVersion）。
    /// </summary>
    public VillageWorldSaveData World;
}

/// <summary>VillageTime 存档快照。</summary>
[Serializable]
public class VillageTimeSaveData
{
    public int Year;
    public int Month;
    public int Day;

    /// <summary>VillageTimeSpeed 存档标识（0=暂停,1=1倍,2=0.5倍,3=2倍,4=4倍,8/12/24=对应倍率），不是通用倍率值。</summary>
    public int Speed;

    public float SecondsIntoDay;
    public VillageIntradayTimeSaveData IntradayTime;
}

/// <summary>Additive minute schema; no Advance/Return session persistence.</summary>
[Serializable]
public class VillageIntradayTimeSaveData
{
    public int SchemaVersion;
    public long CurrentWorldMinute;
    public double DriverRemainder;
    public long LastCompletedDailyBoundaryMinute;
}

/// <summary>VillageState 存档快照。</summary>
[Serializable]
public class VillageStateSaveData
{
    public int TestSurvivalValue;
}

/// <summary>Primary Assignment 运行数据存档快照（Stage V2A；Test / Not Frozen）。</summary>
[Serializable]
public class VillageAssignmentsSaveData
{
    public List<VillageAssignmentSaveData> Records;

    /// <summary>【Test / Not Frozen】Village 级累计 Test Contribution。</summary>
    public int TotalTestContribution;
}

/// <summary>单条 Primary Assignment 存档（Person → Workplace + 执行事实计数）。</summary>
[Serializable]
public class VillageAssignmentSaveData
{
    public long WorkedMinutes;
    public long ContributionRemainder;
    public string PersonId;
    public string WorkplaceId;

    /// <summary>实际执行累计天数（Profession Formation Source Hook (Test) 事实；Test / Not Frozen）。</summary>
    public int TotalExecutedDays;
}

/// <summary>Active Project 运行数据存档快照（Stage V2B；只含进行中项目，Test / Not Frozen）。</summary>
[Serializable]
public class VillageProjectsSaveData
{
    public int LastAllocatedSequence;
    public List<VillageProjectSaveData> ActiveProjects;
}

/// <summary>单条 Active Project 存档（Participants + Progress；Project 定义不存档，加载后按注册 Def 恢复）。</summary>
[Serializable]
public class VillageProjectSaveData
{
    public long LaborRemainder;
    public bool ExecutionPaused;
    public string ProjectId;
    public string ProjectDefId;
    public List<string> ParticipantPersonIds;
    public int Progress;

    /// <summary>VillageProjectState 枚举序（V2B 只保存 Active=0）。</summary>
    public int State;
}

/// <summary>客观社会事实账本存档快照（Stage V3A；Test / Not Frozen）。</summary>
[Serializable]
public class VillageSocialSaveData
{
    public List<VillageSocialFactSaveData> Facts;
}

/// <summary>单条客观社会事实存档（Type 为 VillageSocialFactType 枚举序，V3A 只保存 TemporaryCommitment=0）。</summary>
[Serializable]
public class VillageSocialFactSaveData
{
    public int Type;
    public string PersonId;
    public string ProjectDefId;
    public string ProjectId;
    public int Year;
    public int Month;
    public int Day;
}

/// <summary>V3B Social Response 状态存档快照（Stage V3B；Test / Not Frozen 规则不落存档）。</summary>
[Serializable]
public class VillageSocialResponseSaveData
{
    public List<VillageSocialFiredSaveData> Fired;
    /// <summary>单条待处理回应（无则 null；V3B 至多一条）。</summary>
    public VillagePendingResponseSaveData Pending;
    public List<VillageResponseChoiceSaveData> Choices;
    public List<VillageRestSaveData> Rests;
}

/// <summary>fired 存档：PatternKey + PersonId + Year（窗口=自然年，Test/Not Frozen）。</summary>
[Serializable]
public class VillageSocialFiredSaveData
{
    public string PatternKey;
    public string PersonId;
    public int Year;
}

/// <summary>单条待处理回应存档。</summary>
[Serializable]
public class VillagePendingResponseSaveData
{
    public string EventId;
    public string PatternKey;
    public string PersonId;
    public int Year;
}

/// <summary>ChoiceHistory 存档（Choice 为 VillageSocialResponseChoice 枚举序）。</summary>
[Serializable]
public class VillageResponseChoiceSaveData
{
    public string PatternKey;
    public string PersonId;
    public int Choice;
    public int Year;
    public int Month;
    public int Day;
}

/// <summary>RestUntil 存档（Until = 当月末最后一天；含当天仍拦截，CurrentDate &gt; Until 恢复）。</summary>
[Serializable]
public class VillageRestSaveData
{
    public string PersonId;
    public int UntilYear;
    public int UntilMonth;
    public int UntilDay;
}

/// <summary>第一包最小 World 存档快照（Village-GameSpec-V0.1 第 11 章附录；只含不可派生 Place 痕迹）。</summary>
[Serializable]
public class VillageWorldSaveData
{
    public List<VillageWorldPlaceSaveData> Places;
}

/// <summary>单条 World Place 存档（Kind 为 VillagePlaceKind 枚举序；首包只落 ProjectSite=1）。</summary>
[Serializable]
public class VillageWorldPlaceSaveData
{
    public int Kind;
    public string PlaceId;
    public string DisplayName;
    public string SourceRef;
}
