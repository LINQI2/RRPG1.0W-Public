using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

/// <summary>
/// V4F 产品路径验证（GUI/交互式 Editor PlayMode）：
/// Town → Expedition Prep（ShowExpeditionFromTown）→ 选择真实 Person（自动选中）
/// → 确认出征（真实 BtnConfirm）→ 产品 Return 方法（Retreat / Defeat / SafeReturn）
/// → Town 恢复可操作 → Expedition Occupancy 清除 → Save/Load 状态一致；同时检查 Console 无 Error。
///
/// 团结引擎限制 asmdef 引用 Assembly-CSharp：TownUI / ExpeditionUI / AdventureManager /
/// SaveSystem / VillageRuntimeBridge / DataManager / PlayerCollection 经反射访问；
/// Village.Runtime（ExpeditionRecord / ExpeditionReturnPhase / VillageTime / VillageState）与
/// BattleStage1.Runtime（RunState / RunEndMode）可强类型访问。
/// </summary>
public class ExpeditionV4FReturnProductPlayTests
{
    private const BindingFlags F =
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

    private readonly List<string> _consoleErrors = new List<string>();
    private Application.LogCallback _handler;

    private static System.Type GetType(string name) => System.Type.GetType(name + ", Assembly-CSharp");

    private static object FindInstance(string typeName)
    {
        var t = GetType(typeName);
        if (t == null) return null;
        foreach (var o in Object.FindObjectsOfType(t, true))
        {
            if (o != null) return o;
        }
        return null;
    }

    private static object GetField(object o, string name)
    {
        var f = o.GetType().GetField(name, F);
        Assert.IsNotNull(f, "字段不存在：" + name);
        return f.GetValue(o);
    }

    private static object GetMember(object o, string name)
    {
        var f = o.GetType().GetField(name, F);
        if (f != null) return f.GetValue(o);
        var p = o.GetType().GetProperty(name, F);
        Assert.IsNotNull(p, "属性/字段不存在：" + name);
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
        Assert.IsNotNull(m, "方法不存在：" + method);
        return m.Invoke(o, args);
    }

    private static object CallStatic(string typeName, string method, params object[] args)
    {
        var m = GetType(typeName).GetMethod(method, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
        Assert.IsNotNull(m, "静态方法不存在：" + typeName + "." + method);
        return m.Invoke(null, args);
    }

    private static object DataManagerInstance() =>
        GetType("DataManager").GetProperty("Instance", BindingFlags.Public | BindingFlags.Static).GetValue(null);

    private void CaptureConsoleErrors()
    {
        _handler = (condition, stack, type) =>
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
                _consoleErrors.Add(condition);
        };
        Application.logMessageReceived += _handler;
    }

    private void ReleaseConsoleErrors()
    {
        if (_handler != null) Application.logMessageReceived -= _handler;
    }

    private static void ResetBridgeAndDeleteSave()
    {
        CallStatic("VillageRuntimeBridge", "ResetForTests");
        string p = (string)CallStatic("SaveSystem", "GetSavePath");
        if (!string.IsNullOrEmpty(p) && System.IO.File.Exists(p)) System.IO.File.Delete(p);
    }

    private static object NewAdventureFromExpedition(object collection, object expedition, object dataManager)
    {
        var advType = GetType("AdventureManager");
        var ctor = advType.GetConstructor(new[]
        {
            GetType("PlayerCollection"),
            GetMember(expedition, "SelectedCharacters").GetType(),
            GetType("HeroInstance"),
            GetPropertyOrField(dataManager, "AllCharacters").GetType(),
            GetPropertyOrField(dataManager, "AllSkills").GetType(),
            StaticMonsterTestData.Build().GetType(),
            GetType("RunConfig")
        });
        Assert.IsNotNull(ctor, "AdventureManager 构造函数未找到");
        return ctor.Invoke(new object[]
        {
            collection,
            GetMember(expedition, "SelectedCharacters"),
            GetMember(expedition, "SelectedHero"),
            GetPropertyOrField(dataManager, "AllCharacters"),
            GetPropertyOrField(dataManager, "AllSkills"),
            StaticMonsterTestData.Build(),
            null
        });
    }

    private static object GetPropertyOrField(object o, string name)
    {
        var p = o.GetType().GetProperty(name, F);
        if (p != null) return p.GetValue(o, null);
        var f = o.GetType().GetField(name, F);
        Assert.IsNotNull(f, "成员不存在：" + name);
        return f.GetValue(o);
    }

    private static void ResetBridgeOnly()
    {
        CallStatic("VillageRuntimeBridge", "ResetForTests");
    }

    private static object GetBridgeStatic(string name)
    {
        var t = GetType("VillageRuntimeBridge");
        var f = t.GetField(name, F);
        if (f != null) return f.GetValue(null);
        var p = t.GetProperty(name, F);
        Assert.IsNotNull(p, "桥成员不存在：" + name);
        return p.GetValue(null, null);
    }

    private static IEnumerator Pump(IEnumerator inner)
    {
        while (inner.MoveNext()) yield return inner.Current;
    }

    /// <summary>过滤与产品流程无关的外围插件噪音（MCP / SignalR / Socket），只保留本轮流程子系统相关的 Error。</summary>
    private static bool IsFlowError(string c)
    {
        if (string.IsNullOrEmpty(c)) return false;
        if (c.Contains("IvanMurzak") || c.Contains("SignalR") || c.Contains("SocketException")
            || c.Contains("Unity.MCP") || c.Contains("com.ivanmurzak") || c.Contains("HubConnection"))
            return false;
        return true;
    }

    private IEnumerator RunReturnScenario(RunEndMode mode)
    {
        _consoleErrors.Clear();
        CaptureConsoleErrors();
        try
        {
            // ---------- 真实产品入口：Main Menu → Town ----------
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
            Assert.IsNotNull(pc, "Town 应持有 PlayerCollection");

            // ---------- Town → Expedition Prep（真实 UI 宿主） ----------
            var expUI = FindInstance("ExpeditionUI");
            Assert.IsNotNull(expUI, "ExpeditionUI 应存在");
            Call(expUI, "ShowExpeditionFromTown", pc);
            yield return null;
            yield return null;

            // ---------- 选择真实 Person（自动选中）并登记预期名单 ----------
            var expedition = GetField(expUI, "_expedition");
            var selected = (IList)GetMember(expedition, "SelectedCharacters");
            Assert.IsNotNull(selected, "ExpeditionSystem.SelectedCharacters 应存在");
            Assert.Greater(selected.Count, 0, "远征准备应自动选中至少一名真实 Person");

            var expectedIds = new List<string>();
            foreach (var card in selected)
            {
                string pid = (string)Call(pc, "ResolvePersonIdForCard", card);
                if (!string.IsNullOrEmpty(pid)) expectedIds.Add(pid);
            }
            var selHero = GetMember(expedition, "SelectedHero");
            if (selHero != null)
            {
                string heroPid = (string)GetMember(selHero, "PersonId");
                if (!string.IsNullOrEmpty(heroPid)) expectedIds.Add(heroPid);
            }
            Assert.Greater(expectedIds.Count, 0, "出征名单应含至少一个真实 PersonId（不把模板当 Person）");

            // ---------- 出征（真实确认按钮） ----------
            // V4 Functional Repair：产品入口新增 Carry preflight（Carry 无法承担第一个合法节点的成本时会明确拒绝出征）。
            // 本测试目标是「真实确认按钮 → 进入远征 → Return」，因此先提供真实 Village Food
            //（真实 Food → Carry 转移，不伪造 / 不复制 Carry）。
            object villageResources = GetBridgeStatic("Resources");
            Assert.IsNotNull(villageResources, "Village Resources 应已初始化");
            Call(villageResources, "AddFood", 100);

            var btnConfirm = (Button)GetField(expUI, "btnConfirm");
            Assert.IsNotNull(btnConfirm, "btnConfirm 应绑定");
            btnConfirm.onClick.Invoke();
            yield return null;
            yield return null;

            var expGo = (GameObject)GetPropertyOrField(expUI, "gameObject");
            Assert.IsFalse(expGo.activeSelf, "确认后应进入远征（ExpeditionUI 隐藏）");
            var registered = (IReadOnlyList<string>)GetBridgeStatic("ExpeditionPersonIds");
            Assert.AreEqual(expectedIds.Count, registered.Count, "Expedition Occupancy 登记应恰为真实 Person 名单");
            foreach (var pid in expectedIds) CollectionAssert.Contains(registered, pid, "登记应含 " + pid);

            // ---------- 构造与产品同构的 Run（真实 PlayerData + DataManager 全量数据） ----------
            object adventure = NewAdventureFromExpedition(pc, expedition, DataManagerInstance());
            var run = (RunState)GetPropertyOrField(adventure, "RunState");
            Assert.IsNotNull(run, "AdventureManager.RunState 应存在");
            run.AddPendingGold(20); // 仅 SafeReturn 应兑现 +20（Retreat/Defeat 未冻结占位不兑现）
            SetField(expUI, "_currentAdventure", adventure);

            // SafeReturn：把当前 Run 推到 Boss（真实 MapGenerator.SelectNode(BossNode)）
            if (mode == RunEndMode.SafeReturn)
            {
                var mapGen = GetPropertyOrField(adventure, "MapGen");
                Call(mapGen, "Generate", 4, 3);
                var boss = GetPropertyOrField(mapGen, "BossNode");
                Assert.IsNotNull(boss, "生成的地图应有 BossNode");
                Call(mapGen, "SelectNode", boss);
                Assert.IsTrue((bool)GetPropertyOrField(mapGen, "IsAtBoss"), "SafeReturn 前置：应处于 Boss");
            }

            // ---------- 离村基线（elapsed-days=0 → Village 时间不变） ----------
            var timeBase = (VillageTime)GetBridgeStatic("Time");
            int yBase = timeBase.Year, mBase = timeBase.Month, dBase = timeBase.Day;
            int stateBase = ((VillageState)GetBridgeStatic("State")).TestSurvivalValue;
            int goldBase = (int)GetMember(pc, "Gold");

            // ---------- 产品 Return 方法 ----------
            if (mode == RunEndMode.Retreat)
                Call(expUI, "OnReturnToTown");
            else
                Call(expUI, "OnReturnFromBattle", mode == RunEndMode.SafeReturn);
            yield return null;
            yield return null;

            // ---------- Town 恢复可操作 ----------
            var townGo = (GameObject)GetPropertyOrField(town, "gameObject");
            Assert.IsTrue(townGo.activeSelf, "Return 后 Town 应恢复显示");
            Assert.IsTrue((bool)GetBridgeStatic("IsSimulationActive"), "Return 后 Village 模拟应恢复（SetSimulationActive(true)）");

            // ---------- Expedition Occupancy 清除 ----------
            var afterReg = (IReadOnlyList<string>)GetBridgeStatic("ExpeditionPersonIds");
            Assert.AreEqual(0, afterReg.Count, "Return 后 Expedition Occupancy 应清除");
            foreach (var pid in expectedIds)
            {
                string reason = (string)CallStatic("VillageRuntimeBridge", "GetPersonOccupancyReason", pid);
                Assert.AreEqual("", reason, "Return 后 Person 不应残留任何 Temporary Occupancy 原因");
            }

            // ---------- 跨调用 exactly-once：最终 Return Save 成功后 Completed ----------
            var record = (ExpeditionRecord)GetBridgeStatic("ActiveReturnRecord");
            Assert.IsNotNull(record, "Return 会话记录应存在");
            Assert.IsTrue(record.Completed, "最终 Return Save 成功后 Completed 应置 true");
            Assert.AreEqual(ExpeditionReturnPhase.Completed, record.Phase);
            Assert.AreEqual(0, record.DaysSimulated, "elapsed-days 默认 0 → Catch-up 空跑");

            // ---------- Settlement 语义（经产品路径） ----------
            if (mode == RunEndMode.SafeReturn)
                Assert.AreEqual(goldBase + 20, (int)GetMember(pc, "Gold"), "SafeReturn 应兑现 Pending Gold");
            else
                Assert.AreEqual(goldBase, (int)GetMember(pc, "Gold"), "Retreat/Defeat 损失比例未冻结：金币按既有占位语义不兑现");

            // ---------- Save/Load 状态一致（最终 Return Save 已落盘） ----------
            int yAfter = ((VillageTime)GetBridgeStatic("Time")).Year;
            int mAfter = ((VillageTime)GetBridgeStatic("Time")).Month;
            int dAfter = ((VillageTime)GetBridgeStatic("Time")).Day;
            int stateAfter = ((VillageState)GetBridgeStatic("State")).TestSurvivalValue;
            int goldAfter = (int)GetMember(pc, "Gold");

            ResetBridgeOnly(); // 保留存档文件，模拟重启
            object loaded = CallStatic("SaveSystem", "Load");
            Assert.IsNotNull(loaded, "最终 Return Save 的存档应可加载");
            Assert.AreEqual(goldAfter, (int)GetMember(loaded, "Gold"), "Load 后金币一致");
            var t2 = (VillageTime)GetBridgeStatic("Time");
            Assert.AreEqual(yAfter, t2.Year, "Load 后 Village 年一致");
            Assert.AreEqual(mAfter, t2.Month, "Load 后 Village 月一致");
            Assert.AreEqual(dAfter, t2.Day, "Load 后 Village 日一致");
            Assert.AreEqual(stateAfter, ((VillageState)GetBridgeStatic("State")).TestSurvivalValue, "Load 后 VillageState 一致");
            Assert.AreEqual(0, ((IReadOnlyList<string>)GetBridgeStatic("ExpeditionPersonIds")).Count,
                "Expedition 为运行时登记：Load 后不得残留");

            // ---------- Console：全流程无新增 Error / Exception / Assert（过滤外围插件噪音） ----------
            var flowErrors = _consoleErrors.FindAll(IsFlowError);
            Assert.AreEqual(0, flowErrors.Count,
                "产品路径应无流程相关 Error/Exception/Assert，实际：" + string.Join(" | ", flowErrors));

            Assert.AreEqual(yBase, yAfter, "elapsed-days=0：Return 不应推进 Village 时间（年）");
            Assert.AreEqual(mBase, mAfter, "elapsed-days=0：Return 不应推进 Village 时间（月）");
            Assert.AreEqual(dBase, dAfter, "elapsed-days=0：Return 不应推进 Village 时间（日）");
            Assert.AreEqual(stateBase, stateAfter, "elapsed-days=0：Return 不应推进 VillageState");

        }
        finally
        {
            ReleaseConsoleErrors();
        }
    }

    [SetUp]
    public void SetUp()
    {
        _consoleErrors.Clear();
        ResetBridgeAndDeleteSave();
    }

    [TearDown]
    public void TearDown()
    {
        ReleaseConsoleErrors();
        ResetBridgeAndDeleteSave();
    }

    [UnityTest]
    public IEnumerator Retreat_ProductPath_ReturnsToTown_ClearsOccupancy_SaveLoadConsistent()
    {
        return Pump(RunReturnScenario(RunEndMode.Retreat));
    }

    [UnityTest]
    public IEnumerator Defeat_ProductPath_ReturnsToTown_ClearsOccupancy_SaveLoadConsistent()
    {
        return Pump(RunReturnScenario(RunEndMode.Defeat));
    }

    [UnityTest]
    public IEnumerator SafeReturn_ProductPath_CommitsRewards_ReturnsToTown_SaveLoadConsistent()
    {
        return Pump(RunReturnScenario(RunEndMode.SafeReturn));
    }
}
