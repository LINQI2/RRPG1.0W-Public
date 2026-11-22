using System.Collections.Generic;
using NUnit.Framework;

/// <summary>
/// Gate 1：VillageResourceSettlement 每日结算测试。
/// 覆盖：Farm→Ingredients、Kitchen→Food、Food/Emergency/Underfed 三态消费、
///       Fulfillment/Hunger 写回、空/边界输入不抛异常。
/// 数值使用**本文件局部 Def**（Farm 2/人·日；Kitchen 2→1、3 单位/人·日），
/// 从而与产品 Test 经济数值（VillageTestResourceWorkplaces，V4 Functional Repair 按可达性调整）
/// 解耦：本测试只验证结算算法本身；全部 Test / Not Frozen。
/// </summary>
public class VillageResourceSettlementTests
{
    private const int DemandPerPerson = 4;   // 测试需求（Test/Not Frozen）
    private const int EmergencyPerShare = 5; // Emergency 低效比例（Test/Not Frozen）

    private class FakeHost : IFulfillmentHost
    {
        public string PersonId { get; set; }
        public bool Hungry { get; set; }
        public int ConsecutiveUnderfedDays { get; set; }
    }

    /// <summary>结算算法专用局部 Def（不引用产品 Test 经济目录，避免数值调整影响算法回归）。</summary>
    private static List<VillageResourceWorkplaceDef> Defs()
    {
        return new List<VillageResourceWorkplaceDef>
        {
            new VillageResourceWorkplaceDef
            {
                WorkplaceId = "ws_farm_01",
                Role = VillageResourceRole.Farm,
                PerWorkerDailyOutput = 2
            },
            new VillageResourceWorkplaceDef
            {
                WorkplaceId = "ws_kitchen_01",
                Role = VillageResourceRole.Kitchen,
                InputIngredientsPerUnit = 2,
                OutputFoodPerUnit = 1,
                ProcessingUnitsPerWorkerPerDay = 3
            }
        };
    }

    private static VillageResourceState NewState(int ingredients, int food)
    {
        var s = new VillageResourceState();
        if (ingredients > 0) s.AddIngredients(ingredients);
        if (food > 0) s.AddFood(food);
        return s;
    }

    private static List<VillageDailyExecutionFact> Facts(params VillageDailyExecutionFact[] facts)
    {
        return facts != null ? new List<VillageDailyExecutionFact>(facts) : new List<VillageDailyExecutionFact>();
    }

    private static VillageDailyExecutionFact Executed(string personId, string workplaceId)
    {
        return new VillageDailyExecutionFact { PersonId = personId, WorkplaceId = workplaceId };
    }

    private static List<IFulfillmentHost> Hosts(params FakeHost[] hosts)
    {
        return hosts != null ? new List<IFulfillmentHost>(hosts) : new List<IFulfillmentHost>();
    }

    [Test]
    public void NoExecutedWorkers_ProducesNothing_NoThrow()
    {
        var s = NewState(0, 0);
        var result = VillageResourceSettlement.SettleDay(s, Defs(), Facts(), Hosts(), DemandPerPerson, EmergencyPerShare);
        Assert.AreEqual(0, result.IngredientsProduced);
        Assert.AreEqual(0, result.FoodProduced);
        Assert.AreEqual(0, s.Ingredients);
        Assert.AreEqual(0, s.Food);
    }

    [Test]
    public void Farm_SingleWorker_ProducesIngredientsPerWorker()
    {
        var s = NewState(0, 0);
        var result = VillageResourceSettlement.SettleDay(
            s, Defs(), Facts(Executed("p1", "ws_farm_01")), Hosts(), DemandPerPerson, EmergencyPerShare);
        Assert.AreEqual(2, result.IngredientsProduced);
        Assert.AreEqual(2, s.Ingredients);
    }

    [Test]
    public void Farm_ThreeWorkers_LinearOutput()
    {
        var s = NewState(0, 0);
        var facts = Facts(
            Executed("p1", "ws_farm_01"),
            Executed("p2", "ws_farm_01"),
            Executed("p3", "ws_farm_01"));
        var result = VillageResourceSettlement.SettleDay(s, Defs(), facts, Hosts(), DemandPerPerson, EmergencyPerShare);
        Assert.AreEqual(6, result.IngredientsProduced);
        Assert.AreEqual(6, s.Ingredients);
    }

    [Test]
    public void Kitchen_ConvertsIngredientsToFood_WithinWorkerProcessingLimit()
    {
        var s = NewState(20, 0);
        var result = VillageResourceSettlement.SettleDay(
            s, Defs(), Facts(Executed("k1", "ws_kitchen_01")), Hosts(), DemandPerPerson, EmergencyPerShare);
        // 1 worker × 3 units/worker/day = 3 units；每单位 2 Ingredients → 6 Ingredients，产 3 Food。
        Assert.AreEqual(3, result.FoodProduced);
        Assert.AreEqual(6, result.IngredientsConsumedByKitchen);
        Assert.AreEqual(3, s.Food);
        Assert.AreEqual(14, s.Ingredients);
    }

    [Test]
    public void Kitchen_RespectsIngredientAvailability()
    {
        var s = NewState(2, 0);
        var facts = Facts(
            Executed("k1", "ws_kitchen_01"),
            Executed("k2", "ws_kitchen_01"),
            Executed("k3", "ws_kitchen_01"));
        var result = VillageResourceSettlement.SettleDay(s, Defs(), facts, Hosts(), DemandPerPerson, EmergencyPerShare);
        // 9 units 上限但 Ingredients 只够 1 unit → 1 Food，2 Ingredients 全耗。
        Assert.AreEqual(1, result.FoodProduced);
        Assert.AreEqual(1, s.Food);
        Assert.AreEqual(0, s.Ingredients);
    }

    [Test]
    public void Consumption_FoodSufficient_AllFedAndStreakReset()
    {
        var s = NewState(0, 10); // 2 人 × demand 4 = 8 Food
        var a = new FakeHost { PersonId = "a", Hungry = true, ConsecutiveUnderfedDays = 3 };
        var b = new FakeHost { PersonId = "b", Hungry = false, ConsecutiveUnderfedDays = 2 };
        var result = VillageResourceSettlement.SettleDay(s, Defs(), Facts(), Hosts(a, b), DemandPerPerson, EmergencyPerShare);

        Assert.AreEqual(2, result.PersonsFoodFed);
        Assert.AreEqual(0, result.PersonsEmergencyFed);
        Assert.AreEqual(0, result.PersonsUnderfed);
        Assert.AreEqual(8, result.FoodConsumed);
        Assert.AreEqual(2, s.Food); // 余量不拆分（不足一整份不分派）
        Assert.IsFalse(a.Hungry);
        Assert.AreEqual(0, a.ConsecutiveUnderfedDays);
        Assert.IsFalse(b.Hungry);
        Assert.AreEqual(0, b.ConsecutiveUnderfedDays);
    }

    [Test]
    public void Consumption_FoodShort_EmergencyIngredientsCoversRest()
    {
        var s = NewState(5, 8); // Food 供 2 人；1 人需 Emergency（5 Ingredients/share → 只够 1 人）
        var a = new FakeHost { PersonId = "a" };
        var b = new FakeHost { PersonId = "b" };
        var c = new FakeHost { PersonId = "c" };
        var result = VillageResourceSettlement.SettleDay(s, Defs(), Facts(), Hosts(a, b, c), DemandPerPerson, EmergencyPerShare);

        Assert.AreEqual(2, result.PersonsFoodFed);
        Assert.AreEqual(1, result.PersonsEmergencyFed);
        Assert.AreEqual(0, result.PersonsUnderfed);
        Assert.AreEqual(8, result.FoodConsumed);
        Assert.AreEqual(5, result.EmergencyIngredientsConsumed);
        Assert.AreEqual(0, s.Food);
        Assert.AreEqual(0, s.Ingredients);
        Assert.IsFalse(a.Hungry);
        Assert.IsFalse(b.Hungry);
        Assert.IsFalse(c.Hungry);
    }

    [Test]
    public void Consumption_BothInsufficient_UnderfedIncrementsStreak()
    {
        var s = NewState(0, 0); // Food 与 Emergency 都不足
        var a = new FakeHost { PersonId = "a", ConsecutiveUnderfedDays = 2 };
        var b = new FakeHost { PersonId = "b" };
        var result = VillageResourceSettlement.SettleDay(s, Defs(), Facts(), Hosts(a, b), DemandPerPerson, EmergencyPerShare);

        Assert.AreEqual(0, result.PersonsFoodFed);
        Assert.AreEqual(0, result.PersonsEmergencyFed);
        Assert.AreEqual(2, result.PersonsUnderfed);
        Assert.IsTrue(a.Hungry);
        Assert.AreEqual(3, a.ConsecutiveUnderfedDays);
        Assert.IsTrue(b.Hungry);
        Assert.AreEqual(1, b.ConsecutiveUnderfedDays);
    }

    [Test]
    public void Consumption_NoEmergencyAllowed_AllRemainderUnderfed()
    {
        var s = NewState(50, 4); // Food 只够 1 人；Emergency 关闭（share=0）
        var a = new FakeHost { PersonId = "a" };
        var b = new FakeHost { PersonId = "b" };
        var result = VillageResourceSettlement.SettleDay(s, Defs(), Facts(), Hosts(a, b), DemandPerPerson, 0);

        Assert.AreEqual(1, result.PersonsFoodFed);
        Assert.AreEqual(0, result.PersonsEmergencyFed);
        Assert.AreEqual(1, result.PersonsUnderfed);
        Assert.AreEqual(0, result.EmergencyIngredientsConsumed);
        Assert.AreEqual(50, s.Ingredients); // Emergency 关闭时不消耗 Ingredients
        Assert.IsTrue(b.Hungry);
    }

    [Test]
    public void ZeroDemand_NoConsumption_NoHostMutation()
    {
        var s = NewState(10, 10);
        var a = new FakeHost { PersonId = "a", Hungry = true, ConsecutiveUnderfedDays = 1 };
        var result = VillageResourceSettlement.SettleDay(s, Defs(), Facts(), Hosts(a), 0, EmergencyPerShare);

        Assert.AreEqual(0, result.PersonsFoodFed);
        Assert.AreEqual(0, result.PersonsEmergencyFed);
        Assert.AreEqual(0, result.PersonsUnderfed);
        Assert.AreEqual(0, result.FoodConsumed);
        Assert.AreEqual(0, result.EmergencyIngredientsConsumed);
        Assert.AreEqual(10, s.Food);
        Assert.AreEqual(10, s.Ingredients);
        Assert.IsTrue(a.Hungry); // 无需求日不改写状态
        Assert.AreEqual(1, a.ConsecutiveUnderfedDays);
    }

    [Test]
    public void EmptyInVillagePersons_NothingConsumed_NoThrow()
    {
        var s = NewState(4, 4);
        var result = VillageResourceSettlement.SettleDay(s, Defs(), Facts(), Hosts(), DemandPerPerson, EmergencyPerShare);
        Assert.AreEqual(0, result.PersonsFoodFed);
        Assert.AreEqual(0, result.PersonsUnderfed);
        Assert.AreEqual(4, s.Food);
        Assert.AreEqual(4, s.Ingredients);
    }

    [Test]
    public void NullDefsOrNullFacts_NoThrow_ZeroProduction()
    {
        var s = NewState(0, 0);
        var result = VillageResourceSettlement.SettleDay(s, null, null, Hosts(), DemandPerPerson, EmergencyPerShare);
        Assert.AreEqual(0, result.IngredientsProduced);
        Assert.AreEqual(0, result.FoodProduced);
        Assert.AreEqual(0, s.Ingredients);
    }

    [Test]
    public void ExpeditionMembers_NotPassedIn_DoNotConsumeAndDoNotMutate()
    {
        // 远征人员由调用方过滤（不传入 host）；本测试只验证“不传入者不消费、不改状态”。
        var s = NewState(0, 8); // 只够 2 人
        var inVillageA = new FakeHost { PersonId = "a" };
        var inVillageB = new FakeHost { PersonId = "b" };
        var awayPerson = new FakeHost { PersonId = "expedition_1", Hungry = true, ConsecutiveUnderfedDays = 2 };

        VillageResourceSettlement.SettleDay(s, Defs(), Facts(), Hosts(inVillageA, inVillageB), DemandPerPerson, EmergencyPerShare);

        Assert.AreEqual(0, s.Food);
        Assert.IsFalse(inVillageA.Hungry);
        Assert.IsFalse(inVillageB.Hungry);
        Assert.IsTrue(awayPerson.Hungry); // 未传入 → 完全不改
        Assert.AreEqual(2, awayPerson.ConsecutiveUnderfedDays);
    }

    [Test]
    public void FullChain_FarmKitchenAndConsumption_SameDay()
    {
        // 当日：2 Farm 产出 4 Ingredients → Kitchen（1 人，上限 3 单位/日）按 Ingredients 上限加工 2 单位
        // （耗 2×2=4、产 2 Food）→ 1 名在村人需求 1 消耗 1 Food；
        // 最终：Food=1（2-1）、Ingredients=0（4-4）。
        var s = NewState(0, 0);
        var facts = Facts(
            Executed("f1", "ws_farm_01"),
            Executed("f2", "ws_farm_01"),
            Executed("k1", "ws_kitchen_01"));
        var villager = new FakeHost { PersonId = "v1" };

        var result = VillageResourceSettlement.SettleDay(s, Defs(), facts, Hosts(villager), 1, EmergencyPerShare);

        Assert.AreEqual(4, result.IngredientsProduced);
        Assert.AreEqual(2, result.FoodProduced);
        Assert.AreEqual(4, result.IngredientsConsumedByKitchen);
        Assert.AreEqual(1, result.PersonsFoodFed);
        Assert.AreEqual(1, result.FoodConsumed);
        Assert.AreEqual(1, s.Food);
        Assert.AreEqual(0, s.Ingredients);
        Assert.IsFalse(villager.Hungry);
        Assert.AreEqual(0, villager.ConsecutiveUnderfedDays);
    }

    [Test]
    public void UnderfedStreak_DoesNotSetHungryUntilRequiredThreshold()
    {
        // RequiredConsecutiveUnderfedDays=3：Hungry 不得被硬编码成“当天没吃饱 == Hungry”。
        // 前 2 天连续不足 → streak 累积但 Hungry=false；第 3 天达标 → Hungry=true。
        var defs = Defs();
        var a = new FakeHost { PersonId = "a", Hungry = false, ConsecutiveUnderfedDays = 0 };

        VillageResourceSettlement.SettleDay(NewState(0, 0), defs, Facts(), Hosts(a), DemandPerPerson, EmergencyPerShare, 3);
        Assert.IsFalse(a.Hungry, "不足第 1 天不得置 Hungry（未达阈值）");
        Assert.AreEqual(1, a.ConsecutiveUnderfedDays);

        VillageResourceSettlement.SettleDay(NewState(0, 0), defs, Facts(), Hosts(a), DemandPerPerson, EmergencyPerShare, 3);
        Assert.IsFalse(a.Hungry, "不足第 2 天不得置 Hungry（未达阈值）");
        Assert.AreEqual(2, a.ConsecutiveUnderfedDays);

        VillageResourceSettlement.SettleDay(NewState(0, 0), defs, Facts(), Hosts(a), DemandPerPerson, EmergencyPerShare, 3);
        Assert.IsTrue(a.Hungry, "连续不足达阈值第 3 天应置 Hungry");
        Assert.AreEqual(3, a.ConsecutiveUnderfedDays);
    }

    [Test]
    public void UnderfedStreak_ResetOnFulfilledDay_BeforeThreshold()
    {
        // 连续不足 2 天（Hungry=false）后满足 1 天 → streak 清零，Hungry 保持 false。
        var defs = Defs();
        var a = new FakeHost { PersonId = "a", Hungry = false, ConsecutiveUnderfedDays = 0 };

        VillageResourceSettlement.SettleDay(NewState(0, 0), defs, Facts(), Hosts(a), DemandPerPerson, EmergencyPerShare, 3);
        VillageResourceSettlement.SettleDay(NewState(0, 0), defs, Facts(), Hosts(a), DemandPerPerson, EmergencyPerShare, 3);
        Assert.AreEqual(2, a.ConsecutiveUnderfedDays);

        // 第 3 天食物足够（1 人 × demand）
        VillageResourceSettlement.SettleDay(NewState(0, DemandPerPerson), defs, Facts(), Hosts(a), DemandPerPerson, EmergencyPerShare, 3);
        Assert.IsFalse(a.Hungry);
        Assert.AreEqual(0, a.ConsecutiveUnderfedDays, "满足日应清零连续不足天数");
    }
}
