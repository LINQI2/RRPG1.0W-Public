using System.Collections.Generic;

/// <summary>
/// VillageWorkplaceDef —— Test 岗位定义（Village.Runtime 纯逻辑；Test / Not Frozen）。
///
/// V2A 只实现 Assignment Core 所需的「有限岗位 / Production Opportunity Capacity」承载，
/// 不冻结正式农田 / 铁匠铺 / 学校 / 正式岗位数量 / 建筑升级 / 建筑摆放 / 美术
/// （Village-GameSpec-V0.1 §4.4）。
///
/// TestContributionPerDay 仅用于证明 Assignment 正在实际执行（Test Contribution），
/// 不是正式 Food / 经济 / Production 数值（Village-GameSpec-V0.1 §4.2 / §8）。
/// </summary>
public class VillageWorkplaceDef
{
    /// <summary>岗位唯一身份（Test）。</summary>
    public string WorkplaceId;

    /// <summary>展示名（Test；不得当作正式岗位名冻结）。</summary>
    public string DisplayName;

    /// <summary>该岗位可容纳的最大 Person 数（满员后拒绝继续分配）。</summary>
    public int Capacity;

    /// <summary>【Test / Not Frozen】每名实际执行的 Person 每个 Day Tick 产生的 Test Contribution。</summary>
    public int TestContributionPerDay;
}

/// <summary>
/// VillageTestWorkplaces —— V2A 测试岗位目录（Test / Not Frozen；不得升级为正式内容）。
///
/// 只用于验证「Village Structure 可以限制可用 Assignment Opportunity」；
/// 每次 CreateDefaults 返回独立副本，避免调用方修改共享内容。
/// </summary>
public static class VillageTestWorkplaces
{
    public static List<VillageWorkplaceDef> CreateDefaults()
    {
        return new List<VillageWorkplaceDef>
        {
            new VillageWorkplaceDef
            {
                WorkplaceId = "ws_test_01",
                DisplayName = "Test Workplace 01（Test / Not Frozen）",
                Capacity = 2,
                TestContributionPerDay = 1
            },
            new VillageWorkplaceDef
            {
                WorkplaceId = "ws_test_02",
                DisplayName = "Test Workplace 02（Test / Not Frozen）",
                Capacity = 1,
                TestContributionPerDay = 1
            }
        };
    }
}
