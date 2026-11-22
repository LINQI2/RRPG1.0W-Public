using System.Collections.Generic;

/// <summary>
/// VillageProjectState —— Test Project 生命周期状态（Village.Runtime 纯逻辑）。
/// V2 首版只支持 Active → Completed（Village-GameSpec-V0.1 §2.5 / §4.5）；
/// 玩家主动取消是否提供为 Not Frozen，不实现完整 CancellationPolicy。
/// </summary>
public enum VillageProjectState
{
    Active = 0,
    Completed = 1
}

/// <summary>
/// VillageProjectDef —— Test Project 定义（Test / Not Frozen）。
/// 只用于证明「把具体 Person 抽去 Project 会产生 Opportunity Cost 并推进有限 Project」，
/// 不冻结正式建筑 / 升级 / 项目内容链 / 正式容量数值（Village-GameSpec-V0.1 §2.5 / §4.5 / §4.11 / §8）。
/// </summary>
public class VillageProjectDef
{
    /// <summary>Project 定义唯一身份（Test）。</summary>
    public string ProjectDefId;

    /// <summary>展示名（Test；不得当作正式项目名冻结）。</summary>
    public string DisplayName;

    /// <summary>该 Project 可同时容纳的最大 Participant Person 数（满员后拒绝继续加入）。</summary>
    public int ParticipantCapacity;

    /// <summary>
    /// 【Test / Not Frozen】每名参与者每个 Day Tick 产生的进度单位。
    /// 用于精确验证 Progress 推进（不做 Workload / 工时 / Efficiency）。
    /// </summary>
    public int TestProgressPerParticipantPerDay;

    /// <summary>【Test / Not Frozen】完成所需累计进度（到达后 Completion 结算一次并释放参与者）。</summary>
    public int TestProgressTarget;
}

/// <summary>
/// VillageTestProjects —— V2B 测试项目目录（Test / Not Frozen；不得升级为正式内容）。
/// 每次 CreateDefaults 返回独立副本。
/// </summary>
public static class VillageTestProjects
{
    public static List<VillageProjectDef> CreateDefaults()
    {
        return new List<VillageProjectDef>
        {
            new VillageProjectDef
            {
                ProjectDefId = "prj_test_01",
                DisplayName = "Test Project 01（Test / Not Frozen）",
                ParticipantCapacity = 2,
                TestProgressPerParticipantPerDay = 1,
                TestProgressTarget = 10
            },
            new VillageProjectDef
            {
                ProjectDefId = "prj_test_02",
                DisplayName = "Test Project 02（Test / Not Frozen）",
                ParticipantCapacity = 1,
                TestProgressPerParticipantPerDay = 2,
                TestProgressTarget = 6
            }
        };
    }
}

/// <summary>
/// VillageProject —— 运行中的 Test Project 实例（Village.Runtime 纯逻辑；Temporary Occupancy 的一种）。
/// 保存明确 Participant PersonIds、Progress；完成前不删除参与者的 Primary Assignment（由占用层暂停执行）。
/// 本类为运行时数据；进度推进 / 完成结算统一由 VillageProjectSystem.OnDayTick 驱动。
/// </summary>
public class VillageProject
{
    /// <summary>Project 实例唯一身份（系统生成，如 prj_test_01_1）。</summary>
    public string ProjectId;

    /// <summary>引用的 ProjectDefId（进度规则与容量来自定义；本类不复制 Def 本体）。</summary>
    public string ProjectDefId;

    /// <summary>Participant Person 列表（按加入顺序；V2B 不实现移除/取消）。</summary>
    public List<string> ParticipantPersonIds = new List<string>();

    /// <summary>当前累计进度（完成时截断到 TestProgressTarget；Test / Not Frozen）。</summary>
    public int Progress;
    public long LaborRemainder;
    public bool ExecutionPaused;

    /// <summary>当前生命周期状态。</summary>
    public VillageProjectState State = VillageProjectState.Active;

    /// <summary>是否仍为 Active（占用中）。</summary>
    public bool IsActive => State == VillageProjectState.Active;

    /// <summary>是否已 Completed（已释放参与者）。</summary>
    public bool IsCompleted => State == VillageProjectState.Completed;
}
