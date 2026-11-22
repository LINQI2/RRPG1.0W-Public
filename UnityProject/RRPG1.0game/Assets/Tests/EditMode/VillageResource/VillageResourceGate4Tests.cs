using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;

/// <summary>
/// WP-3 Gate 4：长期资源持久化 + Expedition Food Carry 耦合（EditMode 定向）。
/// 覆盖：
///   A. VillageResourceState → VillageSaveData.Resources → Restore（Build/Restore 往返 + 旧档缺省 0/0）；
///   B. Departure 真实转移（Food 扣除 = Carry 增加；不足按可带量，不阻塞；无复制）；
///   C. Return Remaining Carry 严格时机（Catch-up 完成后才可返还）+ Session 内 exactly-once
///      （RemainingCarryReturned 属 ExpeditionRecord）+ 重复 Return / Catch-up 失败守卫 + 0-day 返还。
/// 桥/SaveSystem 在 Assembly-CSharp 经反射调用；DTO 与 Village.Runtime 类型可直接使用。
/// </summary>
public class VillageResourceGate4Tests
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
        if (f != null) return f.GetValue(null);
        var p = type.GetProperty(name, StaticFlags);
        Assert.IsNotNull(p, "静态字段/属性不存在：" + name + " @ " + type.FullName);
        return p.GetValue(null, null);
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

    // ==================== 场景工具 ====================

    private VillageResourceState Resources() => (VillageResourceState)GetStaticField(BridgeType, "Resources");

    private void EnsureDefault()
    {
        InvokeStatic(BridgeType, "EnsureInitialized", new object[] { null });
    }

    private VillageSaveData BuildVillageSaveData()
    {
        return (VillageSaveData)InvokeStatic(BridgeType, "BuildSaveData");
    }

    private void EnsureWithSave(VillageSaveData save)
    {
        InvokeStatic(BridgeType, "EnsureInitialized", save);
    }

    private void AddFood(int amount)
    {
        Resources().AddFood(amount);
    }

    // ==================== A. Resource Persistence ====================

    [Test]
    public void BuildSaveData_ContainsResourceSnapshot()
    {
        EnsureDefault();
        AddFood(7);
        Resources().AddIngredients(3);

        var save = BuildVillageSaveData();
        Assert.IsNotNull(save.Resources, "VillageSaveData.Resources 应为非空（已接线）");
        Assert.AreEqual(7, save.Resources.Food);
        Assert.AreEqual(3, save.Resources.Ingredients);
    }

    [Test]
    public void Restore_FromSave_RoundtripsResources()
    {
        EnsureDefault();
        AddFood(11);
        Resources().AddIngredients(2);
        var save = BuildVillageSaveData();

        ResetBridge(); // 清空运行时
        EnsureWithSave(save);

        Assert.AreEqual(11, Resources().Food, "Restore 应恢复 Food");
        Assert.AreEqual(2, Resources().Ingredients, "Restore 应恢复 Ingredients");
    }

    [Test]
    public void OldSave_WithoutResources_DefaultsZero()
    {
        // 旧档缺 Resources 区块：DTO 未含该字段（null）→ 默认 0/0，SaveVersion 保持 1。
        var oldSave = new VillageSaveData { Time = new VillageTimeSaveData(), State = new VillageStateSaveData() };
        EnsureWithSave(oldSave);

        Assert.AreEqual(0, Resources().Food);
        Assert.AreEqual(0, Resources().Ingredients);
    }

    // ==================== B. Departure Carry ====================

    [Test]
    public void Departure_TransfersFoodToCarry_WithoutCopy()
    {
        EnsureDefault();
        AddFood(10);
        int actual = (int)InvokeStatic(BridgeType, "CarryFoodForDeparture", 4);
        Assert.AreEqual(4, actual, "应带出请求量");
        Assert.AreEqual(6, Resources().Food, "Food 应减少（真实转移，不复制）");
    }

    [Test]
    public void Departure_NoFood_ReturnsZero_NoBlock()
    {
        EnsureDefault();
        Assert.AreEqual(0, (int)InvokeStatic(BridgeType, "CarryFoodForDeparture", 4),
            "无 Food 时按可带量 0 出发（不阻塞既有无食物流程）");
        Assert.AreEqual(0, Resources().Food);
    }

    [Test]
    public void Departure_InsufficientFood_TransfersAvailable()
    {
        EnsureDefault();
        AddFood(2);
        Assert.AreEqual(2, (int)InvokeStatic(BridgeType, "CarryFoodForDeparture", 5),
            "Food 不足时按可带量转移（最小 Test 策略）");
        Assert.AreEqual(0, Resources().Food);
    }

    // ==================== C. Return Remaining Carry ====================

    private ExpeditionCatchUpResult RunCatchUp(int effectiveDays)
    {
        Assert.AreEqual(true, InvokeStatic(BridgeType, "BeginExpeditionReturn", effectiveDays),
            "应能开始 Return 会话");
        InvokeStatic(BridgeType, "MarkExpeditionReturnSettled");
        return (ExpeditionCatchUpResult)InvokeStatic(BridgeType, "RunReturnCatchUp");
    }

    private bool TryRefund(int amount)
    {
        return (bool)InvokeStatic(BridgeType, "ReturnRemainingCarry", amount);
    }

    private bool RecordCarryReturned()
    {
        object record = GetStaticField(BridgeType, "ActiveReturnRecord");
        Assert.IsNotNull(record, "应有 Active Return 记录");
        return (bool)GetValue(record, "RemainingCarryReturned");
    }

    [Test]
    public void ReturnRefund_RejectedBeforeCatchUpComplete()
    {
        EnsureDefault();
        Assert.AreEqual(true, InvokeStatic(BridgeType, "BeginExpeditionReturn", 0));
        InvokeStatic(BridgeType, "MarkExpeditionReturnSettled");

        // 相位仍 ReturnSettled（Catch-up 尚未完成）→ 禁止提前返还。
        Assert.IsFalse(TryRefund(5), "Catch-up 完成前不得返还 Carry");
        Assert.AreEqual(0, Resources().Food);
    }

    [Test]
    public void ReturnRefund_AfterCatchUp_AddsFoodOnce_AndRejectsSecond()
    {
        EnsureDefault();
        ExpeditionCatchUpResult result = RunCatchUp(0); // 0-day Return
        Assert.AreNotEqual(ExpeditionCatchUpResult.PartialDayFailure, result);
        Assert.AreNotEqual(ExpeditionCatchUpResult.RejectedFailedRecord, result);

        Assert.IsTrue(TryRefund(6), "Catch-up 完成后首次返还应成功");
        Assert.AreEqual(6, Resources().Food);
        Assert.IsTrue(RecordCarryReturned(), "RemainingCarryReturned 应挂在 ExpeditionRecord");

        Assert.IsFalse(TryRefund(6), "第二次返还应被 exactly-once 拒绝");
        Assert.AreEqual(6, Resources().Food, "Food 不得二次增加");
    }

    [Test]
    public void ReturnRefund_RejectedWhenReturnAlreadyCompleted()
    {
        EnsureDefault();
        RunCatchUp(0);
        TryRefund(3);
        // 冻结顺序：返还后 Clear Occupancy（→ OccupancyCleared）→ 最终 Save 成功 → Completed。
        InvokeStatic(BridgeType, "ClearExpeditionPersonIds");
        InvokeStatic(BridgeType, "CompleteExpeditionReturnSave", true);

        Assert.AreEqual(false, InvokeStatic(BridgeType, "BeginExpeditionReturn", 0),
            "Completed 后重复 Return 应被拒绝");
        Assert.IsFalse(TryRefund(3), "Completed 后不得再返还");
        Assert.AreEqual(3, Resources().Food);
    }

    [Test]
    public void ReturnRefund_Rejected_WhenCatchUpFails()
    {
        EnsureDefault();
        // Between-Day 注入失败 → 可重试失败，Catch-up 未完成 → 不允许返还。
        SetStaticField(BridgeType, "BeforeCatchUpDayTickTestHook",
            (System.Action<int>)(day => { throw new System.Exception("Gate4 test failure"); }));
        try
        {
            Assert.AreEqual(ExpeditionCatchUpResult.RetryableFailure, RunCatchUp(1),
                "Catch-up 应报告可重试失败（Between-Day）");
        }
        finally
        {
            SetStaticField(BridgeType, "BeforeCatchUpDayTickTestHook", null);
        }
        Assert.IsFalse(TryRefund(6), "Catch-up 未完成时不得返还 Carry");
        Assert.AreEqual(0, Resources().Food);
    }

    [Test]
    public void ZeroDayReturn_RefundsFullCarry()
    {
        EnsureDefault();
        AddFood(2);
        int carry = (int)InvokeStatic(BridgeType, "CarryFoodForDeparture", 2);
        Assert.AreEqual(2, carry);
        Assert.AreEqual(0, Resources().Food);

        ExpeditionCatchUpResult result = RunCatchUp(0); // 0 天无消耗 → 全额返还
        Assert.AreEqual(ExpeditionCatchUpResult.AlreadyCaughtUp, result);

        Assert.IsTrue(TryRefund(2), "0-day Return 应全额返还剩余 Carry");
        Assert.AreEqual(2, Resources().Food, "返还后 Food 恢复（无消耗）");
    }
}
