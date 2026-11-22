using System.Collections.Generic;

/// <summary>
/// VillageResourceRole —— WP-3 最小资源岗位角色（Village.Runtime 纯逻辑）。
/// Farm：产出 Ingredients；Kitchen：把 Ingredients 加工为 Food。
/// 不做 Building System：Farm / Kitchen 表达为具体 WorkplaceId 的资源角色，
/// 不创建 BuildingManager / FacilitySystem / BuildingInstance。
/// </summary>
public enum VillageResourceRole
{
    /// <summary>农田：每名当日实际执行者产出 Ingredients。</summary>
    Farm,

    /// <summary>厨房：把 Ingredients 按配方转换为 Food（受每日处理上限约束）。</summary>
    Kitchen
}

/// <summary>
/// VillageResourceWorkplaceDef —— WP-3 资源岗位定义（配置驱动；Village.Runtime 纯逻辑；Test / Not Frozen）。
///
/// 复用边界（冻结决定）：
///   - 本 Def 只表达“该 WorkplaceId 的资源角色与每日单位数值”，**不复制 Capacity**；
///     上岗人数与满员/占用仍由既有 VillageAssignmentSystem + VillageWorkplaceDef.Capacity
///     唯一决定（线性 Staffing 由“当日实际执行人数”自然表达，不建立第二套 Capacity）；
///   - 不是通用 Recipe Framework；不做 Building / 升级 / 建筑摆放。
///
/// 字段语义（按 Role 生效；非本 Role 使用的字段保持默认 0 不参与结算）：
///   Farm：PerWorkerDailyOutput = 每名实际执行者每个 Day Tick 产出的 Ingredients。
///   Kitchen：InputIngredientsPerUnit 份 Ingredients → OutputFoodPerUnit 份 Food；
///           ProcessingUnitsPerWorkerPerDay = 每位实际执行者每日最多处理的配方单位数（处理上限）。
/// </summary>
public class VillageResourceWorkplaceDef
{
    /// <summary>引用的既有岗位唯一身份（与 VillageWorkplaceDef.WorkplaceId 同源）。</summary>
    public string WorkplaceId;

    /// <summary>资源角色（Farm 产出 Ingredients / Kitchen 加工 Food）。</summary>
    public VillageResourceRole Role;

    /// <summary>【Test / Not Frozen】Farm：每名实际执行者每 Day 产出 Ingredients。</summary>
    public int PerWorkerDailyOutput;

    /// <summary>【Test / Not Frozen】Kitchen：每单位转换消耗 Ingredients 数。</summary>
    public int InputIngredientsPerUnit;

    /// <summary>【Test / Not Frozen】Kitchen：每单位转换产出 Food 数。</summary>
    public int OutputFoodPerUnit;

    /// <summary>【Test / Not Frozen】Kitchen：每名实际执行者每日最多处理的配方单位数（处理上限）。</summary>
    public int ProcessingUnitsPerWorkerPerDay;
}

/// <summary>
/// VillageTestResourceWorkplaces —— WP-3 测试资源岗位目录（Test / Not Frozen；不得升级为正式内容）。
/// 每次 CreateDefaults 返回独立副本，避免调用方修改共享内容。
/// 对应的 Farm / Kitchen Workplace（含 Capacity 的 VillageWorkplaceDef）由 Gate 3 接入
/// VillageTestWorkplaces / VillageRuntimeBridge 时按同一 WorkplaceId 对齐。
/// </summary>
public static class VillageTestResourceWorkplaces
{
    public static List<VillageResourceWorkplaceDef> CreateDefaults()
    {
        return new List<VillageResourceWorkplaceDef>
        {
            new VillageResourceWorkplaceDef
            {
                WorkplaceId = "ws_farm_01",
                Role = VillageResourceRole.Farm,
                // V4 Functional Repair（Test / Not Frozen，非冻结数值）：
                // 实测追踪（PlayMode Resource Trace）：Fresh New Game 在村 Person = 6（5 名 Character + 1 Hero），
                // 每人每日消耗 1 Food（ResourceFulfillmentThresholdPerPerson）。
                // 为使「最小合法 Staffing（1 Farm + 1 Kitchen）在一个 Day Tick 内、完成 Village Consumption 之后
                // Food Net Delta > 0」成立（V4 Functional Acceptance 的剩余粮可达性要求），测试数值取 12：
                //   1 Farm worker → +12 Ingredients；1 Kitchen worker → 12 单位（1 Ingredients → 1 Food）→ +12 Food；
                //   完成 Village Consumption（6）后 Food Net Delta = +6 > 0，Ingredients Net Delta = 0。
                // 该数值只表达“Test Economy 必须能产生远征剩余粮”，不构成正式平衡数值。
                PerWorkerDailyOutput = 12
            },
            new VillageResourceWorkplaceDef
            {
                WorkplaceId = "ws_kitchen_01",
                Role = VillageResourceRole.Kitchen,
                InputIngredientsPerUnit = 1,
                OutputFoodPerUnit = 1,
                ProcessingUnitsPerWorkerPerDay = 12
            }
        };
    }
}
