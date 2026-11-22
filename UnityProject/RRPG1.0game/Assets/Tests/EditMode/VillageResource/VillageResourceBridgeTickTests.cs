using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;

/// <summary>
/// WP-3 Gate 3：VillageRuntimeBridge 真实 Day Tick 资源结算集成测试（EditMode）。
/// 覆盖：实时 Day Tick 执行资源结算、Return Catch-up 每 TickDay 同样结算、
///       Farm/Kitchen 经既有 WorkplaceId/Capacity 语义接入、Expedition Person 不参与 Village Food
///       Demand、Temporary Occupancy（Project）成员不通过 Primary Assignment 产生资源、
///       Person Fulfillment/Hunger 写回真实 CharacterInstance/HeroInstance、Hungry 阈值可配置。
/// 桥在 Assembly-CSharp，经反射调用；成员对象（VillageTime/Resources/Assignments 等）为
/// Village.Runtime 类型可直接使用。默认岗位目录不变；Farm/Kitchen 仅经
/// EnsureInitializedWithExtraWorkplaces 注入（不影响既有默认表现）。
/// </summary>
public class VillageResourceBridgeTickTests
{
    private const BindingFlags StaticFlags =
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
    private const BindingFlags InstanceFlags =
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

    private static System.Type BridgeType => System.Type.GetType("VillageRuntimeBridge, Assembly-CSharp");
    private static System.Type SaveSystemType => System.Type.GetType("SaveSystem, Assembly-CSharp");

    [SetUp]
    public void ResetBridge()
    {
        InvokeStatic(BridgeType, "ResetForTests");
    }

    [TearDown]
    public void Cleanup()
    {
        InvokeStatic(BridgeType, "ResetForTests");
    }

    // ==================== 反射工具 ====================

    private static object InvokeStatic(System.Type type, string method, params object[] args)
    {
        MethodInfo selected = null;
        foreach (var m in type.GetMethods(StaticFlags))
        {
            if (m.Name != method) continue;
            var ps = m.GetParameters();
            if (ps.Length != (args == null ? 0 : args.Length)) continue;
            bool ok = true;
            for (int i = 0; i < ps.Length; i++)
            {
                if (args[i] == null) continue;
                if (!ps[i].ParameterType.IsAssignableFrom(args[i].GetType())) { ok = false; break; }
            }
            if (ok) { selected = m; break; }
        }
        Assert.IsNotNull(selected, "方法不存在或参数不匹配：" + method + " @ " + type.FullName);
        return selected.Invoke(null, args);
    }

    private static object GetStaticField(System.Type type, string name)
    {
        var f = type.GetField(name, StaticFlags);
        Assert.IsNotNull(f, "静态字段不存在：" + name + " @ " + type.FullName);
        return f.GetValue(null);
    }

    private static void SetStaticField(System.Type type, string name, object value)
    {
        var f = type.GetField(name, StaticFlags);
        Assert.IsNotNull(f, "静态字段不存在：" + name + " @ " + type.FullName);
        f.SetValue(null, value);
    }

    private static object GetValue(object target, string name)
    {
        Assert.IsNotNull(target, "读取目标为 null：" + name);
        var t = target.GetType();
        var f = t.GetField(name, InstanceFlags);
        if (f != null) return f.GetValue(target);
        var p = t.GetProperty(name, InstanceFlags);
        Assert.IsNotNull(p, "属性/字段不存在：" + name + " @ " + t.FullName);
        return p.GetValue(target, null);
    }

    private static List<object> ToObjectList(object target, string listFieldName)
    {
        var result = new List<object>();
        var list = GetValue(target, listFieldName) as IEnumerable;
        Assert.IsNotNull(list, listFieldName + " 不是可枚举集合");
        foreach (var item in list)
        {
            if (item != null) result.Add(item);
        }
        return result;
    }

    private static void SetValue(object target, string name, object value)
    {
        var t = target.GetType();
        var p = t.GetProperty(name, InstanceFlags);
        if (p != null && p.CanWrite)
        {
            p.SetValue(target, value, null);
            return;
        }
        var f = t.GetField(name, InstanceFlags);
        Assert.IsNotNull(f, "可写属性/字段不存在：" + name + " @ " + t.FullName);
        f.SetValue(target, value);
    }

    // ==================== 场景构建工具 ====================

    private VillageResourceState Resources() => (VillageResourceState)GetStaticField(BridgeType, "Resources");

    private VillageAssignmentSystem Assignments() => (VillageAssignmentSystem)GetStaticField(BridgeType, "Assignments");

    private VillageProjectSystem Projects() => (VillageProjectSystem)GetStaticField(BridgeType, "Projects");

    private VillageTime VillageTime() => (VillageTime)GetStaticField(BridgeType, "Time");

    private object NewDefaultCollection()
    {
        return InvokeStatic(SaveSystemType, "NewGame");
    }

    private void EnsureDefault()
    {
        // 注意：params object[] 传单个 null 字面量会绑定为“数组本身为 null”，须用显式数组以传“单元素 null”。
        InvokeStatic(BridgeType, "EnsureInitialized", new object[] { null });
    }

    private void EnsureWithFarmKitchen()
    {
        var extras = new List<VillageWorkplaceDef>
        {
            new VillageWorkplaceDef { WorkplaceId = "ws_farm_01", Capacity = 2, TestContributionPerDay = 1 },
            new VillageWorkplaceDef { WorkplaceId = "ws_kitchen_01", Capacity = 1, TestContributionPerDay = 1 }
        };
        InvokeStatic(BridgeType, "EnsureInitializedWithExtraWorkplaces", null, extras);
    }

    private void BindPersonSource(object collection)
    {
        InvokeStatic(BridgeType, "SetSimulationPersonSource", collection);
    }

    private void SetExpedition(IEnumerable<string> personIds)
    {
        var list = personIds != null ? new List<string>(personIds) : new List<string>();
        InvokeStatic(BridgeType, "SetExpeditionPersonIds", list);
    }

    private static int InVillageAliveCount(object collection, HashSet<string> excludedIds)
    {
        int count = 0;
        foreach (var h in ToObjectList(collection, "OwnedHeroes"))
        {
            string pid = (string)GetValue(h, "PersonId");
            if (string.IsNullOrEmpty(pid) || (excludedIds != null && excludedIds.Contains(pid))) continue;
            count++;
        }
        foreach (var c in ToObjectList(collection, "OwnedCharacters"))
        {
            string pid = (string)GetValue(c, "PersonId");
            bool dead = (bool)GetValue(c, "IsDead");
            if (dead || string.IsNullOrEmpty(pid)) continue;
            if (excludedIds != null && excludedIds.Contains(pid)) continue;
            count++;
        }
        return count;
    }

    private static string FirstPersonIdOf(object collection, string listField)
    {
        foreach (var p in ToObjectList(collection, listField))
        {
            string pid = (string)GetValue(p, "PersonId");
            if (!string.IsNullOrEmpty(pid)) return pid;
        }
        return null;
    }

    // ==================== 测试用例 ====================

    [Test]
    public void DayTick_RunsResourceConsumption_AndWritesRealPersonState()
    {
        var collection = NewDefaultCollection();
        EnsureDefault();
        BindPersonSource(collection);

        int inVillage = InVillageAliveCount(collection, null);
        Assert.GreaterOrEqual(inVillage, 1);

        var resources = Resources();
        resources.AddFood(inVillage + 5); // 阈值 1/人·日，留余量
        int initial = resources.Food;

        // 预置某角色为“之前持续饥饿”，食物充足应清零
        var firstChar = ToObjectList(collection, "OwnedCharacters")[0];
        object hostChar = firstChar;
        SetValue(hostChar, "ConsecutiveUnderfedDays", 3);
        SetValue(hostChar, "Hungry", true);

        VillageTime().TickDay();

        Assert.AreEqual(initial - inVillage, resources.Food, "实时 Day Tick 应按在村人数扣减 Food");
        Assert.AreEqual(0, (int)GetValue(hostChar, "ConsecutiveUnderfedDays"), "满足日应清零 streak");
        Assert.IsFalse((bool)GetValue(hostChar, "Hungry"), "满足日 Hungry=false");
    }

    [Test]
    public void ExpeditionPerson_ExcludedFromVillageFoodDemand()
    {
        var collection = NewDefaultCollection();
        EnsureDefault();
        BindPersonSource(collection);

        string awayPid = FirstPersonIdOf(collection, "OwnedCharacters");
        Assert.IsNotNull(awayPid, "默认集合应含角色");
        SetExpeditionCore(new List<string> { awayPid });

        // 找出远征人物实例（用于断言其状态不被改动）
        object awayInstance = null;
        foreach (var c in ToObjectList(collection, "OwnedCharacters"))
        {
            if ((string)GetValue(c, "PersonId") == awayPid) { awayInstance = c; break; }
        }
        SetValue(awayInstance, "ConsecutiveUnderfedDays", 2);
        SetValue(awayInstance, "Hungry", true);

        int inVillageWithoutAway = InVillageAliveCount(collection, new HashSet<string> { awayPid });
        var resources = Resources();
        resources.AddFood(inVillageWithoutAway); // 正好覆盖“在村者”，远征者不被消费

        VillageTime().TickDay();

        Assert.AreEqual(0, resources.Food, "Food 只按在村人数消耗");
        Assert.AreEqual(2, (int)GetValue(awayInstance, "ConsecutiveUnderfedDays"),
            "远征人员不参与 Village Food Demand → 状态不被结算改写");
        Assert.IsTrue((bool)GetValue(awayInstance, "Hungry"), "远征人员状态保持原样");
    }

    [Test]
    public void FarmAndKitchen_ProduceThroughExistingWorkplaceAssignment()
    {
        var collection = NewDefaultCollection();
        EnsureWithFarmKitchen();
        BindPersonSource(collection);

        string farmPid = FirstPersonIdOf(collection, "OwnedCharacters");
        string kitchenPid = FirstPersonIdOf(collection, "OwnedHeroes");
        Assert.IsNotNull(farmPid, "需要 farm 工人（角色）");
        Assert.IsNotNull(kitchenPid, "需要 kitchen 工人（英雄）");

        Assert.AreEqual(VillageAssignmentResult.Success,
            Assignments().TryAssign(farmPid, "ws_farm_01"));
        Assert.AreEqual(VillageAssignmentResult.Success,
            Assignments().TryAssign(kitchenPid, "ws_kitchen_01"));

        int inVillage = InVillageAliveCount(collection, null);
        var resources = Resources();
        resources.AddFood(inVillage); // 初始 Food 正好覆盖全村一餐

        VillageTime().TickDay();

        // V4 Functional Repair（Test / Not Frozen 数值调整）：
        // farm 1 工人 × 12/日 → Ingredients 12；kitchen 1 工人可加工 12 单位（1 Ingredients → 1 Food）→ Food 12。
        // 消费 = inVillage × 1。Food = initial + 11 - inVillage；Ingredients = 0。
        // 数值目的：最小合法 Staffing（1 Farm + 1 Kitchen）在完成 Village Consumption 后必须产生正 Food 净增
        //（Fresh New Game 默认人口下可形成远征剩余粮）。
        Assert.AreEqual(1, resources.Ingredients, "最后一批 Farm 17:00 到货，Kitchen 不追造过去劳动");
        Assert.AreEqual(inVillage + 11 - inVillage, resources.Food,
            "17:00 Farm 最后到货不允许厨房瞬间补出第 12 批");
        Assert.AreEqual(1, Assignments().GetRecord(farmPid).TotalExecutedDays, "farm 工人当日应实际执行");
    }

    private void SetExpeditionCore(List<string> personIds)
    {
        InvokeStatic(BridgeType, "SetExpeditionPersonIds", personIds);
    }

    [Test]
    public void TemporaryOccupancy_ProjectMember_DoesNotProduceThroughAssignment()
    {
        var collection = NewDefaultCollection();
        EnsureWithFarmKitchen();
        BindPersonSource(collection);

        string farmPid = FirstPersonIdOf(collection, "OwnedCharacters");
        Assert.AreEqual(VillageAssignmentResult.Success, Assignments().TryAssign(farmPid, "ws_farm_01"));

        // 把 farm 工人抽去 Active Project（Temporary Occupancy 来源）
        VillageProject created;
        Assert.AreEqual(VillageProjectResult.Success,
            Projects().TryCreateProject("prj_test_01", out created));
        Assert.AreEqual(VillageProjectResult.Success,
            Projects().TryAddParticipant(created.ProjectId, farmPid));

        var resources = Resources();

        VillageTime().TickDay();

        Assert.AreEqual(0, resources.Ingredients, "Project 占用当日不得经 Primary Assignment 产出资源");
        VillageAssignmentRecord record = Assignments().GetRecord(farmPid);
        Assert.IsNotNull(record);
        Assert.AreEqual(0, record.TotalExecutedDays, "占用日不实际执行 → 无执行事实");
        Assert.AreEqual(1, created.Progress, "Project 正常推进（占用语义来自既有系统）");
    }

    [Test]
    public void ReturnCatchUp_RunsResourceConsumption_OnEachCatchUpDay()
    {
        var collection = NewDefaultCollection();
        EnsureDefault();
        BindPersonSource(collection);

        string awayPid = FirstPersonIdOf(collection, "OwnedCharacters");
        SetExpeditionCore(new List<string> { awayPid });
        int inVillage = InVillageAliveCount(collection, new HashSet<string> { awayPid });

        var resources = Resources();
        resources.AddFood(inVillage * 2 + 10); // 2 天 Catch-up 的在村需求 + 余量
        int initial = resources.Food;

        // Return 会话：Begin(2 天) → Settlement 标记 → Catch-up（每 TickDay 走同一 HandleDayTick）
        object beginOk = InvokeStatic(BridgeType, "BeginExpeditionReturn", 2);
        Assert.AreEqual(true, beginOk, "应能开始 2 天 Return 会话");
        InvokeStatic(BridgeType, "MarkExpeditionReturnSettled");
        object catchUpResult = InvokeStatic(BridgeType, "RunReturnCatchUp");
        Assert.AreEqual("Completed", catchUpResult != null ? catchUpResult.ToString() : "null",
            "Catch-up 应完整完成");

        Assert.AreEqual(initial - inVillage * 2, resources.Food,
            "Return Catch-up 的每个 TickDay 都应执行一次资源结算（在村者被扣 2 天需求）");
    }

    [Test]
    public void ConfigurableRequiredConsecutiveDays_DelaysHungryFlag()
    {
        var collection = NewDefaultCollection();
        EnsureDefault();
        BindPersonSource(collection);

        SetStaticField(BridgeType, "ResourceRequiredConsecutiveUnderfedDays", 3);
        var resources = Resources(); // Food/Ingredients = 0 → 全在村者每日不足

        object target = null;
        foreach (var c in ToObjectList(collection, "OwnedCharacters"))
        {
            if (!string.IsNullOrEmpty((string)GetValue(c, "PersonId"))) { target = c; break; }
        }
        Assert.IsNotNull(target, "默认集合应含角色");

        VillageTime().TickDay();
        Assert.AreEqual(1, (int)GetValue(target, "ConsecutiveUnderfedDays"));
        Assert.IsFalse((bool)GetValue(target, "Hungry"), "第 1 天不足：Hungry=false（未达阈值 3）");

        VillageTime().TickDay();
        Assert.AreEqual(2, (int)GetValue(target, "ConsecutiveUnderfedDays"));
        Assert.IsFalse((bool)GetValue(target, "Hungry"), "第 2 天不足：Hungry=false");

        VillageTime().TickDay();
        Assert.AreEqual(3, (int)GetValue(target, "ConsecutiveUnderfedDays"));
        Assert.IsTrue((bool)GetValue(target, "Hungry"), "第 3 天连续不足达阈值 → Hungry=true");
    }
}
