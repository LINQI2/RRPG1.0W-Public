using System.Collections.Generic;

/// <summary>
/// IFulfillmentHost —— WP-3 Person Fulfillment / Hunger 长期状态接缝（Village.Runtime 纯逻辑）。
///
/// 职责边界（WP-3 冻结决定）：
///   - Fulfillment / Hunger 属于具体 Person（CharacterInstance / HeroInstance 在 Gate 2 实现本接口），
///     禁止存放于 VillageState.PersonHungerDictionary 或任何 Village 级集合；
///   - 本接口只暴露每日结算所需的最小状态（Hungry + ConsecutiveUnderfedDays）；
///   - Village.Runtime 只依赖本接口，不引用 PlayerCollection / Assembly-CSharp（依赖方向不倒灌）。
///
/// Hungry 语义（Test / Not Frozen）：Hungry 表达“持续未满足达到配置阈值”状态——
/// 当日未获得满足（Food + Emergency Ingredients 均不足）时 ConsecutiveUnderfedDays + 1，
/// 当日满足则清零；Hungry = ConsecutiveUnderfedDays >= RequiredConsecutiveUnderfedDays
/// （该阈值由 VillageResourceSettlement.SettleDay 参数配置，默认 1；1 时即当日不足即 Hungry）。
/// 禁止把“当天没吃饱”硬编码等价于 Hungry。
/// 具体 Hunger Consequence 数值 Not Frozen，不在此定义。
/// </summary>
public interface IFulfillmentHost
{
    /// <summary>具体人物唯一身份（只读；长期 PersonId）。</summary>
    string PersonId { get; }

    /// <summary>是否处于“持续未满足达阈值”的 Hungry 状态（每日结算写回；默认 false）。</summary>
    bool Hungry { get; set; }

    /// <summary>连续未满足天数（满足日清零；长期 Person-owned 字段）。</summary>
    int ConsecutiveUnderfedDays { get; set; }
}

/// <summary>
/// VillageResourceDayResult —— 单日资源结算结果摘要（只读结果数据，供测试与 UI/日志断言）。
/// 不含长期状态、不参与 Authority 判定。
/// </summary>
public class VillageResourceDayResult
{
    /// <summary>Farm 当日产出 Ingredients。</summary>
    public int IngredientsProduced;

    /// <summary>Kitchen 当日产出 Food。</summary>
    public int FoodProduced;

    /// <summary>Kitchen 当日消耗 Ingredients（加工用）。</summary>
    public int IngredientsConsumedByKitchen;

    /// <summary>当日正常 Food 消费量。</summary>
    public int FoodConsumed;

    /// <summary>当日 Emergency 消耗 Ingredients 量。</summary>
    public int EmergencyIngredientsConsumed;

    /// <summary>当日以 Food 足额满足的人数。</summary>
    public int PersonsFoodFed;

    /// <summary>当日以 Emergency Ingredients 满足的人数。</summary>
    public int PersonsEmergencyFed;

    /// <summary>当日未满足（Underfed）人数。</summary>
    public int PersonsUnderfed;
}

/// <summary>
/// VillageResourceSettlement —— WP-3 每日资源结算权威（Village.Runtime 纯逻辑）。
///
/// 负责一日内的固定顺序：
///   Farm → Ingredients → Kitchen → Food → Person Consumption → Fulfillment/Hunger。
///
/// 规则（Village-Resource-Coupling-GameSpec-V0.1 / WP-3 冻结决定）：
///   - 产出主体是 Workplace，不是具体 Person：按“当日实际执行者”（VillageDailyExecutionFact）
///     线性产出；实际执行人数已由上游 VillageAssignmentSystem + Capacity/Occupancy 唯一决定，
///     本类不读取、不复制 Capacity（无第二套 Capacity / 上岗判定）；
///   - Kitchen 不是通用 Recipe Framework：只执行 Farm 产出 → Kitchen 转换两阶段固定语义，
///     转换数值来自 VillageResourceWorkplaceDef（配置驱动，不代码写死倍率）；
///   - 消费只针对传入的 InVillage Person（远征 / 死亡人员由调用方在传入前过滤，
///     本类不自行判定占用）；等份消费：Food 优先，不足按 Emergency Ingredients 低效补足，
///     仍不足则该人当日 Underfed；
///   - 本类为全函数：任何输入（含空 defs / 空执行 / 0 需求 / 库存不足）不抛异常，
///     保证后续接入 Return Catch-up 时不会因资源逻辑产生 PartialDayFailure 噪音。
///
/// 全部数值 Test / Not Frozen。
/// </summary>
public class VillageResourceSettlement
{
    /// <summary>
    /// 结算一天。执行顺序：Farm 产出 → Kitchen 加工 → 消费 → Fulfillment/Hunger 写回 host。
    /// </summary>
    /// <param name="resources">Village 长期资源唯一 Authority（唯一写入口由此结算调用）。</param>
    /// <param name="defs">资源岗位定义（含 Farm/Kitchen）。</param>
    /// <param name="executedFacts">当日实际执行事实（来自 VillageAssignmentSystem.OnDayTick），
    /// 只包含当日确实执行者；为空则本日零产出零加工。</param>
    /// <param name="inVillagePersons">当日应在村饮食的 Person host（调用方已过滤远征/死亡；
    /// 空则本日无人消费）。</param>
    /// <param name="dailyFoodDemandPerPerson">每名在村 Person 每日 Food 需求（Test / Not Frozen；
    /// &lt;=0 表示本日无人需要消费）。</param>
    /// <param name="emergencyIngredientsPerShare">Food 不足时每份需求的 Emergency Ingredients
    /// 低效消耗量（Test / Not Frozen；&lt;=0 表示不提供 Emergency 补足）。</param>
    /// <param name="requiredConsecutiveUnderfedDays">Hungry 触发阈值：连续未满足达到该天数才置 Hungry
    /// （Test / Not Frozen；默认 1 = 当日不足即 Hungry，天然可用；&lt;=0 按 1 处理）。</param>
    public static VillageResourceDayResult SettleDay(
        VillageResourceState resources,
        IReadOnlyList<VillageResourceWorkplaceDef> defs,
        IReadOnlyList<VillageDailyExecutionFact> executedFacts,
        IReadOnlyList<IFulfillmentHost> inVillagePersons,
        int dailyFoodDemandPerPerson,
        int emergencyIngredientsPerShare,
        int requiredConsecutiveUnderfedDays = 1)
    {
        var result = new VillageResourceDayResult();
        if (resources == null) return result;

        // ---------- 1) Farm → Ingredients（按当日实际执行者线性产出） ----------
        result.IngredientsProduced = ProduceIngredients(resources, defs, executedFacts);

        // ---------- 2) Kitchen → Food（Ingredients → Food，受每日处理上限约束） ----------
        ProcessFood(resources, defs, executedFacts, result);

        // ---------- 3/4) Person Consumption → Fulfillment/Hunger（等份；Food 优先） ----------
        if (dailyFoodDemandPerPerson > 0 && inVillagePersons != null && inVillagePersons.Count > 0)
        {
            ConsumeAndApplyFulfillment(
                resources, inVillagePersons,
                dailyFoodDemandPerPerson, emergencyIngredientsPerShare,
                requiredConsecutiveUnderfedDays, result);
        }

        return result;
    }

    // ==================== Farm ====================

    private static int ProduceIngredients(
        VillageResourceState resources,
        IReadOnlyList<VillageResourceWorkplaceDef> defs,
        IReadOnlyList<VillageDailyExecutionFact> executedFacts)
    {
        var farmExecuted = CountExecutedByRole(defs, executedFacts, VillageResourceRole.Farm);
        int total = 0;
        if (farmExecuted != null)
        {
            foreach (var kv in farmExecuted)
            {
                int workers = kv.Value;
                int outputPerWorker = Max0(FindDef(defs, kv.Key)?.PerWorkerDailyOutput ?? 0);
                if (workers <= 0 || outputPerWorker <= 0) continue;
                total = ClampAdd(total, ClampMul(workers, outputPerWorker));
            }
        }
        if (total > 0) resources.AddIngredients(total);
        return total;
    }

    // ==================== Kitchen ====================

    private static void ProcessFood(
        VillageResourceState resources,
        IReadOnlyList<VillageResourceWorkplaceDef> defs,
        IReadOnlyList<VillageDailyExecutionFact> executedFacts,
        VillageResourceDayResult result)
    {
        var kitchenExecuted = CountExecutedByRole(defs, executedFacts, VillageResourceRole.Kitchen);
        if (kitchenExecuted == null || kitchenExecuted.Count == 0) return;

        foreach (var kv in kitchenExecuted)
        {
            var def = FindDef(defs, kv.Key);
            if (def == null) continue;

            int workers = kv.Value;
            int input = Max0(def.InputIngredientsPerUnit);
            int output = Max0(def.OutputFoodPerUnit);
            int unitsPerWorker = Max0(def.ProcessingUnitsPerWorkerPerDay);
            if (workers <= 0 || input <= 0 || output <= 0 || unitsPerWorker <= 0) continue;

            int maxUnitsByWorkers = ClampMul(workers, unitsPerWorker);
            int maxUnitsByIngredients = resources.Ingredients / input;
            int units = maxUnitsByWorkers < maxUnitsByIngredients ? maxUnitsByWorkers : maxUnitsByIngredients;
            if (units <= 0) continue;

            int ingredientsNeeded = units * input;
            int foodProduced = units * output;
            if (resources.TryConsumeIngredients(ingredientsNeeded))
            {
                resources.AddFood(foodProduced);
                result.IngredientsConsumedByKitchen = ClampAdd(result.IngredientsConsumedByKitchen, ingredientsNeeded);
                result.FoodProduced = ClampAdd(result.FoodProduced, foodProduced);
            }
        }
    }

    // ==================== Consumption / Fulfillment ====================

    private static void ConsumeAndApplyFulfillment(
        VillageResourceState resources,
        IReadOnlyList<IFulfillmentHost> inVillagePersons,
        int dailyFoodDemandPerPerson,
        int emergencyIngredientsPerShare,
        int requiredConsecutiveUnderfedDays,
        VillageResourceDayResult result)
    {
        int personCount = inVillagePersons.Count;

        // 等份分派：同一类别内所有人待遇一致（Food 足额 → Emergency 补足 → Underfed），
        // 不做逐人先后消费（避免队列顺序造成不公平）。
        int satisfiedByFood = 0;
        int foodAvailableShares = resources.Food / dailyFoodDemandPerPerson;
        if (foodAvailableShares > 0)
        {
            satisfiedByFood = foodAvailableShares < personCount ? foodAvailableShares : personCount;
            int foodConsumed = satisfiedByFood * dailyFoodDemandPerPerson;
            if (resources.TryConsumeFood(foodConsumed))
            {
                result.FoodConsumed = ClampAdd(result.FoodConsumed, foodConsumed);
            }
            else
            {
                satisfiedByFood = 0; // 防御：不应发生（TryConsume 前置校验），失败则整体回落
            }
        }

        int remainingAfterFood = personCount - satisfiedByFood;
        int satisfiedByEmergency = 0;
        if (remainingAfterFood > 0 && emergencyIngredientsPerShare > 0)
        {
            int emergencyShares = resources.Ingredients / emergencyIngredientsPerShare;
            if (emergencyShares > 0)
            {
                satisfiedByEmergency = emergencyShares < remainingAfterFood ? emergencyShares : remainingAfterFood;
                int ingredientsNeeded = satisfiedByEmergency * emergencyIngredientsPerShare;
                if (resources.TryConsumeIngredients(ingredientsNeeded))
                {
                    result.EmergencyIngredientsConsumed = ClampAdd(result.EmergencyIngredientsConsumed, ingredientsNeeded);
                }
                else
                {
                    satisfiedByEmergency = 0; // 防御：同 Food 分支
                }
            }
        }

        int underfedCount = personCount - satisfiedByFood - satisfiedByEmergency;

        // 按类别同步写回 host 状态：满足 → 清零（Hungry=false）；
        // 未满足 → streak +1，Hungry = streak >= requiredConsecutiveUnderfedDays。
        int threshold = requiredConsecutiveUnderfedDays > 0 ? requiredConsecutiveUnderfedDays : 1;
        int index = 0;
        for (int i = 0; i < satisfiedByFood; i++, index++) ApplyFulfilled(inVillagePersons[index]);
        for (int i = 0; i < satisfiedByEmergency; i++, index++) ApplyFulfilled(inVillagePersons[index]);
        for (int i = 0; i < underfedCount; i++, index++) ApplyUnderfed(inVillagePersons[index], threshold);

        result.PersonsFoodFed = satisfiedByFood;
        result.PersonsEmergencyFed = satisfiedByEmergency;
        result.PersonsUnderfed = underfedCount;
    }

    private static void ApplyFulfilled(IFulfillmentHost host)
    {
        if (host == null) return;
        host.Hungry = false;
        host.ConsecutiveUnderfedDays = 0;
    }

    private static void ApplyUnderfed(IFulfillmentHost host, int requiredConsecutiveUnderfedDays)
    {
        if (host == null) return;
        int streak = ClampAdd(Max0(host.ConsecutiveUnderfedDays), 1);
        host.ConsecutiveUnderfedDays = streak;
        host.Hungry = streak >= requiredConsecutiveUnderfedDays;
    }

    // ==================== Helpers（纯计算；全部不抛异常） ====================

    /// <summary>统计指定 Role 岗位当日实际执行人数（key = WorkplaceId）；无执行/无 def 返回空字典。</summary>
    private static Dictionary<string, int> CountExecutedByRole(
        IReadOnlyList<VillageResourceWorkplaceDef> defs,
        IReadOnlyList<VillageDailyExecutionFact> executedFacts,
        VillageResourceRole role)
    {
        if (defs == null || defs.Count == 0 || executedFacts == null || executedFacts.Count == 0)
            return null;

        var matched = new Dictionary<string, int>();
        foreach (var fact in executedFacts)
        {
            if (fact == null || string.IsNullOrEmpty(fact.WorkplaceId)) continue;
            var def = FindDef(defs, fact.WorkplaceId);
            if (def == null || def.Role != role) continue;
            matched.TryGetValue(fact.WorkplaceId, out int count);
            matched[fact.WorkplaceId] = count + 1;
        }
        return matched.Count > 0 ? matched : null;
    }

    private static VillageResourceWorkplaceDef FindDef(
        IReadOnlyList<VillageResourceWorkplaceDef> defs, string workplaceId)
    {
        if (defs == null || string.IsNullOrEmpty(workplaceId)) return null;
        foreach (var def in defs)
        {
            if (def != null && def.WorkplaceId == workplaceId) return def;
        }
        return null;
    }

    private static int Max0(int value)
    {
        return value > 0 ? value : 0;
    }

    private static int ClampAdd(int current, int amount)
    {
        long sum = (long)current + amount;
        return sum > int.MaxValue ? int.MaxValue : (int)sum;
    }

    private static int ClampMul(int a, int b)
    {
        long value = (long)Max0(a) * Max0(b);
        return value > int.MaxValue ? int.MaxValue : (int)value;
    }
}
