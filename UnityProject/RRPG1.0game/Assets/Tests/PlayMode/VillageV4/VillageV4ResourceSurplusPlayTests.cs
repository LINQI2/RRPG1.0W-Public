using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

/// <summary>
/// V4 Functional Repair｜**Resource 链真实数值追踪 + Food Surplus 可达性**（Disposable Development Test / Test / Not Frozen）。
///
/// 人工验收发现：Fresh New Game → Farm + Kitchen → Jump To Next Month → Expedition Supply 仍为 0 / 0。
/// 本测试先做**真实数值追踪**（Farm DailyExecutionFact 人数 / Farm 产出 / Kitchen 加工 / InVillage 人数 /
/// Food Demand / 每日净变化），判定属于：
///   A. Farm / Kitchen 根本没执行；或
///   B. 确实执行了，但当天产出被 Village Consumption 全部吃掉。
/// 再验证 Current Functional Build 必须满足的可达性：
///   最小合法 Staffing（1 Farm + 1 Kitchen）在一个 Day Tick 内、完成 Village Consumption 之后，
///   Food Net Delta 必须 > 0，并最终形成可携带出征的真实 Carry（不注入、不凭空补粮、不绕过 Resource Authority）。
/// </summary>
public class VillageV4ResourceSurplusPlayTests
{
    private const BindingFlags F =
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

    private static Type GetType(string name) => Type.GetType(name + ", Assembly-CSharp");

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
        Assert.IsNotNull(p, "成员不存在：" + name + " @ " + o.GetType().FullName);
        return p.GetValue(o, null);
    }

    private static object InvokeInstance(object o, string method, params object[] args)
    {
        var m = o.GetType().GetMethod(method, F);
        Assert.IsNotNull(m, "方法不存在：" + method + " @ " + o.GetType().FullName);
        return m.Invoke(o, args);
    }

    private static object InvokeStatic(Type type, string method, params object[] args)
    {
        foreach (var m in type.GetMethods(F).Where(m => m.Name == method))
        {
            var ps = m.GetParameters();
            if (ps.Length != (args == null ? 0 : args.Length)) continue;
            bool ok = true;
            for (int i = 0; i < ps.Length; i++)
            {
                if (args[i] == null) continue;
                if (!ps[i].ParameterType.IsInstanceOfType(args[i])) { ok = false; break; }
            }
            if (ok) return m.Invoke(null, args);
        }
        Assert.Fail("静态方法不存在或参数不匹配：" + type.Name + "." + method);
        return null;
    }

    private static object CallStatic(string typeName, string method, params object[] args) =>
        InvokeStatic(GetType(typeName), method, args);

    // 注意：VillageRuntimeBridge 在 Assembly-CSharp（本测试程序集不引用）→ 统一用反射访问；
    // VillageTime / VillageResourceDayResult / VillageAssignmentResult 在 Village.Runtime，可直接引用类型。
    private static VillageTime BridgeTime() => (VillageTime)StaticMember("VillageRuntimeBridge", "Time");

    private static int Ingredients() =>
        (int)GetMember(StaticMember("VillageRuntimeBridge", "Resources"), "Ingredients");

    private static int Food() =>
        (int)GetMember(StaticMember("VillageRuntimeBridge", "Resources"), "Food");

    private static object StaticMember(string typeName, string name)
    {
        var t = GetType(typeName);
        var f = t.GetField(name, F);
        if (f != null) return f.GetValue(null);
        var p = t.GetProperty(name, F);
        Assert.IsNotNull(p, "静态成员不存在：" + typeName + "." + name);
        return p.GetValue(null, null);
    }

    private static VillageResourceDayResult LastDay() =>
        (VillageResourceDayResult)StaticMember("VillageRuntimeBridge", "LastResourceDayResult");

    private static int LastInVillageCount() =>
        (int)StaticMember("VillageRuntimeBridge", "LastResourceInVillageCount");

    private static int LastExecutedFactCount() =>
        (int)StaticMember("VillageRuntimeBridge", "LastResourceExecutedFactCount");

    private object _pc;

    private IEnumerator EnterTownFreshGame()
    {
        CallStatic("VillageRuntimeBridge", "ResetForTests");
        string savePath = (string)CallStatic("SaveSystem", "GetSavePath");
        if (!string.IsNullOrEmpty(savePath) && System.IO.File.Exists(savePath)) System.IO.File.Delete(savePath);

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
        InvokeInstance(mm, "OnContinue");
        yield return null;
        yield return null;

        var town = FindInstance("TownUI");
        Assert.IsNotNull(town, "TownUI 应存在");
        Assert.IsTrue(((GameObject)GetMember(town, "canvasRoot")).activeSelf, "应已进入 Town");

        _pc = GetMember(town, "PlayerData");
        Assert.IsNotNull(_pc, "TownUI.PlayerData 应存在");

        BridgeTime().SetSpeed(VillageTime.VillageTimeSpeed.Paused); // 确定性：手动按天推进
        CallStatic("VillageRuntimeBridge", "SetSimulationPersonSource", _pc);
    }

    // ==================== Resource Daily Trace + Food Surplus 可达性 ====================

    [UnityTest]
    public IEnumerator FreshNewGame_MinimalFarmKitchen_ProducesFoodSurplus_AndExpeditionCarry()
    {
        yield return EnterTownFreshGame();

        // ---- 最小合法 Staffing：1 Farm + 1 Kitchen（不同 Person；复用既有 Assignment Authority） ----
        var personIds = new List<string>();
        foreach (var c in (IEnumerable)GetMember(_pc, "OwnedCharacters"))
        {
            string pid = (string)GetMember(c, "PersonId");
            if (!string.IsNullOrEmpty(pid)) personIds.Add(pid);
        }
        Assert.GreaterOrEqual(personIds.Count, 2,
            "Fresh New Game 应有 ≥2 名在村 Character（Farm + Kitchen 最小 Staffing 需要）");

        Assert.AreEqual(VillageAssignmentResult.Success,
            (VillageAssignmentResult)CallStatic("VillageRuntimeBridge", "TryAssignPerson", personIds[0], "ws_farm_01"),
            "应能安排 Person 到 Farm（复用既有 Assignment Authority，不建第二套 Staffing）");
        Assert.AreEqual(VillageAssignmentResult.Success,
            (VillageAssignmentResult)CallStatic("VillageRuntimeBridge", "TryAssignPerson", personIds[1], "ws_kitchen_01"),
            "应能安排 Person 到 Kitchen");

        var trace = new System.Text.StringBuilder();
        trace.AppendLine("== Fresh New Game Resource Daily Trace（1 Farm + 1 Kitchen / Test Economy）==");
        trace.AppendLine("初始：Ingredients=" + Ingredients() + " Food=" + Food()
            + " 在村 Person=" + LastInVillageCount());

        int lastFoodNet = 0;
        int lastIngredientsNet = 0;
        for (int day = 1; day <= 3; day++)
        {
            int inBefore = Ingredients();
            int foodBefore = Food();

            BridgeTime().TickDay();
            yield return null;

            var r = LastDay();
            Assert.IsNotNull(r, "每个 Day Tick 必须产生资源结算结果（只读诊断 seam）");

            int inAfter = Ingredients();
            int foodAfter = Food();
            lastFoodNet = foodAfter - foodBefore;
            lastIngredientsNet = inAfter - inBefore;

            trace.AppendLine(string.Format(
                "Day{0}: InVillagePerson={1} 执行事实={2} | Farm→Ingredients={3} Kitchen→Food={4}(耗Ingredients={5}) | "
                + "Food消费={6}(足额{7}/应急{8}/未满足{9}) 应急Ingredients={10} | Ingredients {11}→{12} Δ{13} | Food {14}→{15} Δ{16}",
                day, LastInVillageCount(), LastExecutedFactCount(),
                r.IngredientsProduced, r.FoodProduced, r.IngredientsConsumedByKitchen,
                r.FoodConsumed, r.PersonsFoodFed, r.PersonsEmergencyFed, r.PersonsUnderfed, r.EmergencyIngredientsConsumed,
                inBefore, inAfter, inAfter - inBefore, foodBefore, foodAfter, foodAfter - foodBefore));
        }
        Debug.Log(trace.ToString());

        var lastDay = LastDay();

        // A / B 判定：不是“根本没执行”
        Assert.Greater(lastDay.IngredientsProduced, 0,
            "Farm 必须真实执行并产出 Ingredients（否则属于 A：根本没执行）\n" + trace);
        Assert.Greater(lastDay.FoodProduced, 0,
            "Kitchen 必须真实执行并把 Ingredients 加工为 Food（否则属于 A：根本没执行）\n" + trace);
        Assert.Greater(LastInVillageCount(), 0,
            "InVillage Person > 0（Village Consumption 真实发生）\n" + trace);

        // 功能约束：完成 Village Consumption 之后 Food Net Delta 必须 > 0
        Assert.Greater(lastFoodNet, 0,
            "最小合法 Staffing（1 Farm + 1 Kitchen）在一个 Day Tick 内、完成 Village Consumption 后"
            + " Food Net Delta 必须 > 0（不得靠 ConsumptionRate=0 掩盖）\n" + trace);
        Assert.GreaterOrEqual(lastIngredientsNet, 0, "Ingredients Net Delta 不得为负\n" + trace);

        // ---- Jump To Next Month：Food 必须真实积累（玩家无需 Debug 注入） ----
        int foodBeforeJump = Food();
        BridgeTime().JumpToNextMonth();
        for (int frame=0;BridgeTime().HasActiveAdvance && frame<200;frame++) yield return null;
        Assert.IsFalse(BridgeTime().HasActiveAdvance,"真实 Driver 续完 Jump 的原请求后才能进入出征");
        int foodAfterJump = Food();
        trace.AppendLine("Jump To Next Month：Food " + foodBeforeJump + " → " + foodAfterJump
            + "（Ingredients=" + Ingredients() + "）");
        Debug.Log(trace.ToString());

        Assert.Greater(foodAfterJump, foodBeforeJump + 20,
            "持续正常生产下 Jump To Next Month 后 Village Food 必须显著积累\n" + trace);

        // ---- 出征前可见性 + 真实 Carry（用真实 Food 携带出征） ----
        var expUI = FindInstance("ExpeditionUI");
        Assert.IsNotNull(expUI, "ExpeditionUI 应存在");
        InvokeInstance(expUI, "ShowExpeditionFromTown", _pc);
        yield return null;
        yield return null;

        object identityPanel = GetMember(expUI, "_identityPanel");
        Assert.IsNotNull(identityPanel, "准备界面应存在补给可见性表面");
        string supplyText = (string)GetMember(identityPanel, "SupplyText");
        StringAssert.Contains("村庄食物：" + foodAfterJump, supplyText,
            "准备界面必须显示真实 Village Food（只读，不复制资源状态）\n" + trace);
        StringAssert.Contains("预计携带：", supplyText, "准备界面必须显示预计携带量");
        StringAssert.Contains("第一步预计成本：", supplyText, "准备界面必须显示第一个合法节点的预计成本");
        Debug.Log("准备界面补给可见性文本：\n" + supplyText);

        var confirmBtn = (Button)GetMember(expUI, "btnConfirm");
        Assert.IsTrue(confirmBtn.interactable, "有足够 Food 时 Confirm Expedition 必须可用\n" + supplyText);

        InvokeInstance(expUI, "OnConfirmExpedition");
        yield return null;
        yield return null;

        object adventure = GetMember(expUI, "_currentAdventure");
        Assert.IsNotNull(adventure, "有足够 Food 时必须能真实确认出征（不得进入 0 Supply 无动作会话）\n" + trace);

        object supply = GetMember(GetMember(adventure, "ProgressionState"), "Supply");
        int carry = (int)GetMember(supply, "InitialCarryUnits");
        Assert.Greater(carry, 0, "Departure 必须把真实 Village Food 转移为 Carry\n" + trace);
        Assert.AreEqual(foodAfterJump - carry, Food(),
            "Departure 后 Village Food 必须减少恰好 Carry（不得凭空补粮 / 复制资源）\n" + trace);

        var journey = FindInstance("ExpeditionJourneyView");
        Assert.IsNotNull(journey, "确认出征后必须存在 Journey 表现宿主（Current 玩家表面）");
        Assert.IsTrue((bool)GetMember(journey, "IsVisible"), "确认出征后 Journey 必须可见");
        StringAssert.DoesNotContain("补给：0", (string)GetMember(journey, "SupplyDisplay"),
            "不得进入 0 Supply 的无动作远征会话");

        // 收尾：走既有 Retreat（Return 事务）回到 Town，并清理测试状态
        InvokeInstance(expUI, "RequestRetreatFromProgression");
        yield return null;
        yield return null;
        CallStatic("VillageRuntimeBridge", "ResetForTests");
    }
}
