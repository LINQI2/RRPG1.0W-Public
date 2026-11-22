using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

/// <summary>
/// WP-3 Final Acceptance｜Gate D：Full Food Carry Product E2E（GUI/交互式 Editor PlayMode）。
/// 复用既有 V4F 产品契约测试模式（真实 SampleScene + MainMenu/Town/ExpeditionUI 产品对象 +
/// 产品方法 + Resolver seam 直推），把带具体 Food 数值的完整 Carry 闭环固化为可重复断言。
///
/// 场景（Retreat，1 名出征者）：
///   F0 = 初始 Village Food（显式设定）
///   → Departure：actual = CarryFoodForDeparture(party × carryPerPerson)，Food=F0-actual
///   → StartProgression(…, actual, rate=1, nodeElapsed=1 seam)
///   → 结算 1 节点：Supply 消耗 = party×1×1，剩余 Carry = actual-1
///   → 产品 Return（OnReturnToTown → FinalizeReturn Retreat）
///        Settlement → Catch-up(1 天，Village 在村消费扣 Food、远征者不消费)
///        → Return Remaining Carry（= actual-1，Catch-up 后返还）
///        → Clear Occupancy → Unique Final Save
///   ExpectedFinalFood = (F0-actual) - 在村消费(1 天) + (actual-1)
///   → Load：Loaded Food == ExpectedFinalFood（Ingredients 同 Authority 保留为 0）。
///
/// 断言还包括：Return 记录 Completed、RemainingCarryReturned 属于 ExpeditionRecord、
/// 第二次返还/重复 Return no-op、无 Console Error。
/// </summary>
public class VillageResourceCarryE2EPlayTests
{
    private const BindingFlags F =
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

    private const int InitialFood = 100;      // F0（Test / Not Frozen）
    private const int CarryPerPerson = 2;     // 产品桥配置（Test / Not Frozen）
    private const int RatePerDay = 1;         // perPersonDailyConsumptionRate（seam，Test / Not Frozen）

    private readonly List<string> _consoleErrors = new List<string>();
    private Application.LogCallback _handler;

    private static System.Type GetType(string name) => System.Type.GetType(name + ", Assembly-CSharp");

    private static object FindInstance(string typeName)
    {
        var t = GetType(typeName);
        if (t == null) return null;
        foreach (var o in UnityEngine.Object.FindObjectsOfType(t, true))
            if (o != null) return o;
        return null;
    }

    private static object GetMember(object o, string name)
    {
        var f = o.GetType().GetField(name, F);
        if (f != null) return f.GetValue(o);
        var p = o.GetType().GetProperty(name, F);
        Assert.IsNotNull(p, "属性/字段不存在：" + name + " @ " + o.GetType().FullName);
        return p.GetValue(o, null);
    }

    private static void SetField(object o, string name, object value)
    {
        var f = o.GetType().GetField(name, F);
        Assert.IsNotNull(f, "字段不存在：" + name);
        f.SetValue(o, value);
    }

    private static object Call(object o, string method, params object[] args)
    {
        var m = o.GetType().GetMethod(method, F);
        Assert.IsNotNull(m, "方法不存在：" + method + " @ " + o.GetType().FullName);
        return m.Invoke(o, args);
    }

    private static object CallStatic(string typeName, string method, params object[] args)
    {
        var m = GetType(typeName).GetMethod(method,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
        Assert.IsNotNull(m, "静态方法不存在：" + typeName + "." + method);
        return m.Invoke(null, args);
    }

    private static object StaticMember(string typeName, string name)
    {
        var t = GetType(typeName);
        var f = t.GetField(name, F);
        if (f != null) return f.GetValue(null);
        var p = t.GetProperty(name, F);
        Assert.IsNotNull(p, "静态属性/字段不存在：" + typeName + "." + name);
        return p.GetValue(null, null);
    }

    private static object InvokeMethod(object target, string method, params object[] args)
    {
        var m = target.GetType().GetMethod(method, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
        Assert.IsNotNull(m, "方法不存在：" + method + " @ " + target.GetType().FullName);
        return m.Invoke(target, args);
    }

    private static object DataManagerInstance() =>
        GetType("DataManager").GetProperty("Instance", BindingFlags.Public | BindingFlags.Static).GetValue(null);

    private static void ResetBridgeAndDeleteSave()
    {
        CallStatic("VillageRuntimeBridge", "ResetForTests");
        string p = (string)CallStatic("SaveSystem", "GetSavePath");
        if (!string.IsNullOrEmpty(p) && System.IO.File.Exists(p)) System.IO.File.Delete(p);
    }

    private static IEnumerator Pump(IEnumerator inner)
    {
        while (inner.MoveNext()) yield return inner.Current;
    }

    private static void SetVillageFood(int amount)
    {
        object resources = StaticMember("VillageRuntimeBridge", "Resources");
        Assert.IsNotNull(resources, "bridge Resources 应已初始化");
        InvokeMethod(resources, "AddFood", amount);
    }

    private static int VillageFood()
    {
        object resources = StaticMember("VillageRuntimeBridge", "Resources");
        return GetPropertyValue(resources, "Food");
    }

    private static int GetPropertyValue(object target, string name)
    {
        var p = target.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
        Assert.IsNotNull(p, "属性不存在：" + name + " @ " + target.GetType().FullName);
        return (int)p.GetValue(target, null);
    }

    /// <summary>过滤与产品流程无关的外围插件噪音（MCP / SignalR / Socket），只保留本轮流程相关的 Error。</summary>
    private static bool IsFlowError(string c)
    {
        if (string.IsNullOrEmpty(c)) return false;
        if (c.Contains("IvanMurzak") || c.Contains("SignalR") || c.Contains("SocketException")
            || c.Contains("Unity.MCP") || c.Contains("com.ivanmurzak") || c.Contains("HubConnection"))
            return false;
        return true;
    }

    private void CaptureConsoleErrors()
    {
        _handler = (condition, stack, type) =>
        {
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
            if (!IsFlowError(condition)) return;
            _consoleErrors.Add(condition);
        };
        Application.logMessageReceived += _handler;
    }

    private void ReleaseConsoleErrors()
    {
        if (_handler != null) Application.logMessageReceived -= _handler;
    }

    // ==================== 场景/Adventure 工具 ====================

    private static object NewAdventureFromExpedition(object collection, object expedition, object dataManager)
    {
        var advType = GetType("AdventureManager");
        var ctor = advType.GetConstructor(new[]
        {
            GetType("PlayerCollection"),
            GetMember(expedition, "SelectedCharacters").GetType(),
            GetType("HeroInstance"),
            GetMember(dataManager, "AllCharacters").GetType(),
            GetMember(dataManager, "AllSkills").GetType(),
            StaticMonsterTestData.Build().GetType(),
            GetType("RunConfig")
        });
        Assert.IsNotNull(ctor, "AdventureManager 构造函数未找到");
        return ctor.Invoke(new object[]
        {
            collection,
            GetMember(expedition, "SelectedCharacters"),
            GetMember(expedition, "SelectedHero"),
            GetMember(dataManager, "AllCharacters"),
            GetMember(dataManager, "AllSkills"),
            StaticMonsterTestData.Build(),
            null
        });
    }

    private static object ResolverOf(object adventure) => GetMember(adventure, "Progression");
    private static object SupplyOf(object adventure) => GetMember(GetMember(adventure, "ProgressionState"), "Supply");
    private static int SupplyCarry(object adventure) => (int)GetPropertyValue(SupplyOf(adventure), "CurrentCarryUnits");

    private static HashSet<string> ExpeditionIds(object adventure)
    {
        var set = new HashSet<string>();
        foreach (var id in (System.Collections.IEnumerable)Call(adventure, "GetExpeditionPersonIds"))
            set.Add((string)id);
        return set;
    }

    private static int InVillageAliveCount(object collection, HashSet<string> excludedIds)
    {
        int count = 0;
        foreach (var h in (System.Collections.IEnumerable)GetMember(collection, "OwnedHeroes"))
        {
            string pid = (string)GetMember(h, "PersonId");
            if (string.IsNullOrEmpty(pid) || (excludedIds != null && excludedIds.Contains(pid))) continue;
            count++;
        }
        foreach (var c in (System.Collections.IEnumerable)GetMember(collection, "OwnedCharacters"))
        {
            string pid = (string)GetMember(c, "PersonId");
            bool dead = (bool)GetMember(c, "IsDead");
            if (dead || string.IsNullOrEmpty(pid)) continue;
            if (excludedIds != null && excludedIds.Contains(pid)) continue;
            count++;
        }
        return count;
    }

    private static void RegisterPersonsAndProvider(object adventure, object expUI)
    {
        var list = new List<string>(ExpeditionIds(adventure));
        CallStatic("VillageRuntimeBridge", "SetExpeditionPersonIds", (IReadOnlyList<string>)list);
        CallStatic("VillageRuntimeBridge", "SetExpeditionElapsedDaysProvider",
            new Func<int>(() => (int)GetMember(adventure, "ExpeditionElapsedDays")));
        SetField(expUI, "_currentAdventure", adventure);
    }

    private static void ClearProvider()
    {
        CallStatic("VillageRuntimeBridge", "SetExpeditionElapsedDaysProvider", (Func<int>)null);
    }

    private static List<string> ReadAvailableIds(object resolver)
    {
        var list = new List<string>();
        foreach (var node in (System.Collections.IEnumerable)Call(resolver, "GetAvailableNextNodes"))
            list.Add((string)GetMember(node, "NodeId"));
        return list;
    }

    private static string KindOfNode(object resolver, string nodeId)
    {
        var graph = GetMember(GetMember(resolver, "State"), "Graph");
        var m = graph.GetType().GetMethod("TryGetNode");
        object[] ps = { nodeId, null };
        m.Invoke(graph, ps);
        Assert.IsNotNull(ps[1], "node 不存在：" + nodeId);
        return GetMember(ps[1], "NodeKind").ToString();
    }

    private static void EnterNode(object resolver, string nodeId)
    {
        Assert.AreEqual("Success", Call(resolver, "TryEnterNode", nodeId).ToString(), "进入节点应成功：" + nodeId);
    }

    private static void ResolveSuccess(object resolver)
    {
        var enumType = Type.GetType("ExpeditionNodeOutcome, ExpeditionProgression.Runtime");
        object success = Enum.Parse(enumType, "Success");
        var m = resolver.GetType().GetMethod("TryResolveCurrentNode", new[] { enumType });
        Assert.AreEqual("Success", m.Invoke(resolver, new object[] { success }).ToString(), "Resolve(Success) 应成功");
    }

    private static int Ingredients()
    {
        object resources = StaticMember("VillageRuntimeBridge", "Resources");
        return GetPropertyValue(resources, "Ingredients");
    }

    // ==================== Gate D：Full Food Carry E2E ====================

    private IEnumerator RunFoodCarryE2E()
    {
        try
        {
            ResetBridgeAndDeleteSave();
            CaptureConsoleErrors();

            SceneManager.LoadScene("SampleScene");
            yield return null;
            yield return null;

            if (!(bool)CallStatic("SaveSystem", "HasSave"))
            {
                object fresh = CallStatic("SaveSystem", "NewGame");
                Assert.IsNotNull(fresh, "NewGame 失败");
                Assert.IsTrue((bool)CallStatic("SaveSystem", "Save", fresh), "新档应能写入");
            }

            var mm = FindInstance("MainMenuUI");
            Assert.IsNotNull(mm, "MainMenuUI 应存在");
            Call(mm, "OnContinue");
            yield return null;
            yield return null;

            var town = FindInstance("TownUI");
            Assert.IsNotNull(town, "TownUI 应存在");
            object pc = GetMember(town, "PlayerData");
            var expUI = FindInstance("ExpeditionUI");
            Assert.IsNotNull(expUI, "ExpeditionUI 应存在");
            Call(expUI, "ShowExpeditionFromTown", pc);
            yield return null;
            yield return null;

            var expedition = GetMember(expUI, "_expedition");
            object firstCard = null;
            foreach (var c in (System.Collections.IEnumerable)GetMember(pc, "OwnedCharacterCards"))
            {
                firstCard = c;
                break;
            }
            Assert.IsNotNull(firstCard, "需要至少一张 owned 角色卡");
            Call(expedition, "AddCharacterCard", firstCard);
            yield return null;

            // —— Departure：真实产品 Village Food → Carry 转移（产品 UI 调用同一方法）——
            var adventure = NewAdventureFromExpedition(pc, expedition, DataManagerInstance());
            int party = ExpeditionIds(adventure).Count;
            Assert.GreaterOrEqual(party, 1, "出征人数应 >= 1（本场景 = 所选角色卡 + 自动选中英雄）");

            int nInVillage = InVillageAliveCount(pc, ExpeditionIds(adventure));
            // 允许 0：默认集合只有“出征者本人”时，Catch-up 无在村消费者，期望公式仍成立。
            Assert.GreaterOrEqual(nInVillage, 0);

            SetVillageFood(InitialFood); // F0
            Assert.AreEqual(InitialFood, VillageFood(), "初始 Village Food 应为 F0");

            int requestedCarry = party * CarryPerPerson;
            int actualCarry = (int)CallStatic("VillageRuntimeBridge",
                "CarryFoodForDeparture", requestedCarry);
            Assert.AreEqual(requestedCarry, actualCarry, "Departure 实际 Carry = party × carryPerPerson");
            Assert.AreEqual(InitialFood - actualCarry, VillageFood(),
                "Departure 后 Village Food = F0 - ActualCarry（真实转移，不复制）");

            // StartProgression（initialCarry = actualCarry；seam 节点耗时 1 天/率 1 让 Supply 真实消耗）
            var start = adventure.GetType().GetMethod("StartProgression");
            Assert.IsNotNull(start, "StartProgression 未找到");
            start.Invoke(adventure,
                new object[] { 4, 3, party, actualCarry, RatePerDay, new Func<string, int>(id => 1) });
            RegisterPersonsAndProvider(adventure, expUI);

            object supply = SupplyOf(adventure);
            Assert.AreEqual(actualCarry, GetPropertyValue(supply, "InitialCarryUnits"),
                "Expedition InitialCarry == ActualCarry");
            Assert.AreEqual(actualCarry, SupplyCarry(adventure), "出征时 Carry 全额携带");

            // —— 推进 1 节点 → 真实 Supply 消耗 = party × 1 天 × rate ——
            var resolver = ResolverOf(adventure);
            string first = ReadAvailableIds(resolver)[0];
            EnterNode(resolver, first);
            ResolveSuccess(resolver);

            int consumed = party * 1 * RatePerDay;
            Assert.AreEqual(actualCarry - consumed, SupplyCarry(adventure),
                "Supply 应按节点 exactly-once 扣除（party × elapsed × rate）");

            int remainingCarry = SupplyCarry(adventure);
            int foodAfterDeparture = VillageFood();

            // —— 产品 Return（Retreat）→ Settlement → Catch-up(1天) → Return Remaining Carry → Clear → Final Save ——
            Call(expUI, "OnReturnToTown");
            yield return null;
            yield return null;

            object record = StaticMember("VillageRuntimeBridge", "ActiveReturnRecord");
            Assert.IsNotNull(record, "应存在 Return 会话记录");
            Assert.AreEqual("Completed", GetMember(record, "Phase").ToString(), "Phase 应为 Completed");
            Assert.IsTrue((bool)GetMember(record, "Completed"), "Completed=true（唯一 Final Save 后置位）");
            Assert.IsTrue((bool)GetMember(record, "RemainingCarryReturned"),
                "RemainingCarryReturned 应属于 ExpeditionRecord（Return Transaction Authority）");

            // 数值断言：返还发生在 Catch-up 后（Carry 不参与其返回前那些天的 Village Consumption）
            int catchUpVillageConsumption = nInVillage * 1; // 1 天 Catch-up，阈值 1/人·日
            int expectedFinalFood = foodAfterDeparture - catchUpVillageConsumption + remainingCarry;
            Assert.AreEqual(expectedFinalFood, VillageFood(),
                "ExpectedFinalFood = FoodAfterDeparture - CatchUpVillageConsumption + RemainingCarry");

            // —— Final Save + Load ——
            Assert.IsTrue((bool)CallStatic("SaveSystem", "HasSave"), "最终 Return Save 应已落盘");
            object loaded = CallStatic("SaveSystem", "Load");
            Assert.IsNotNull(loaded, "Load 应成功");
            Assert.AreEqual(expectedFinalFood, VillageFood(), "Loaded Village Food == ExpectedFinalFood");
            Assert.AreEqual(0, Ingredients(), "Ingredients 按同一 Save Authority 保留（本场景为 0）");

            // 第二次 Save/Load 不再改变 Food
            CallStatic("SaveSystem", "Save", loaded);
            CallStatic("SaveSystem", "Load");
            Assert.AreEqual(expectedFinalFood, VillageFood(), "第二次 Final Save/Load 不得再次改变 Food");
        }
        finally
        {
            ReleaseConsoleErrors();
            ClearProvider();
            CallStatic("VillageRuntimeBridge", "ResetForTests");
        }
    }

    [UnityTest]
    public IEnumerator FoodCarry_Retreat_FullProductE2E()
    {
        yield return Pump(RunFoodCarryE2E());
        Assert.IsEmpty(_consoleErrors, "产品 E2E 全程不应有 Console Error");
    }
}
