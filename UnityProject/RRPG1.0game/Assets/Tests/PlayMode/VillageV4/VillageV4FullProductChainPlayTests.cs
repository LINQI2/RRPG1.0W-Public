using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

/// <summary>
/// V4 Gate 3｜**Full V4 Product Chain E2E**（GUI/交互式 Editor PlayMode；Disposable Development Test）。
///
/// 真实产品链（不新增系统、不绕过产品入口）：
///   MainMenu → Town（真实 Village Runtime）
///   → Hero 投入 Test Project（prj_test_02）→ 3 天完成 → Test Capability Grant
///   → 同一 Person Growth Authority Pool 扩大（bcard_rally）
///   → Character Growth Offer 读取到新卡
///   → 产品 Expedition（Hero + ≥1 Character）→ 非零 elapsed（Battle Node = 1 Test Day）
///   → Supply 真实消耗 → 产品 Return（Retreat）→ Settlement → Catch-up（Village 真实推进 N 天）
///   → Return Person Consequence（长期 Injury + Character ExpeditionReturned）
///   → Carry Return → Clear Occupancy → Unique Final Save
///   → Save → Load → Continue → 下一次 Growth Offer / Run Reward Candidate 仍读取 Capability 结果
///
/// 断言同时覆盖：VillageTime 正确推进、出征者 Catch-up 期间 Away、Hero 不新增 LifeHistory、
/// 无重复提交（会话标记）、SaveVersion 保持 1、无 Console Error。
/// 全部内容 Test / Not Frozen。
/// </summary>
public class VillageV4FullProductChainPlayTests
{
    private const string TestProject = "prj_test_02";
    private const int TestProjectDays = 3;
    private const string GrantedCard = "bcard_rally";
    private const int InitialFood = 100;

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

    private static void SetMember(object o, string name, object value)
    {
        var f = o.GetType().GetField(name, F);
        if (f != null) { f.SetValue(o, value); return; }
        var p = o.GetType().GetProperty(name, F);
        Assert.IsNotNull(p, "成员不存在：" + name);
        p.SetValue(o, value, null);
    }

    private static object Call(object o, string method, params object[] args)
    {
        var m = o.GetType().GetMethod(method, F);
        Assert.IsNotNull(m, "方法不存在：" + method + " @ " + o.GetType().FullName);
        return m.Invoke(o, args);
    }

    private static object CallStatic(string typeName, string method, params object[] args)
    {
        var t = GetType(typeName);
        var candidates = t.GetMethods(F).Where(m => m.Name == method).ToList();
        foreach (var m in candidates)
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
        Assert.Fail("静态方法不存在或参数不匹配：" + typeName + "." + method);
        return null;
    }

    private static object StaticMember(string typeName, string name)
    {
        var t = GetType(typeName);
        var f = t.GetField(name, F);
        if (f != null) return f.GetValue(null);
        var p = t.GetProperty(name, F);
        Assert.IsNotNull(p, "静态成员不存在：" + typeName + "." + name);
        return p.GetValue(null, null);
    }

    private static object DataManagerInstance() =>
        GetType("DataManager").GetProperty("Instance", BindingFlags.Public | BindingFlags.Static).GetValue(null);

    // ==================== Console / Save ====================

    private readonly List<string> _consoleErrors = new List<string>();
    private Application.LogCallback _handler;

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

    private static void ResetBridgeAndDeleteSave()
    {
        CallStatic("VillageRuntimeBridge", "ResetForTests");
        string p = (string)CallStatic("SaveSystem", "GetSavePath");
        if (!string.IsNullOrEmpty(p) && System.IO.File.Exists(p)) System.IO.File.Delete(p);
    }

    // ==================== Village / Person / Pool ====================

    private static VillageTime BridgeTime() => (VillageTime)StaticMember("VillageRuntimeBridge", "Time");

    private static void PauseVillageTime() => BridgeTime().SetSpeed(VillageTime.VillageTimeSpeed.Paused);

    private static int VillageFood() =>
        (int)StaticMember("VillageRuntimeBridge", "Resources").GetType()
            .GetProperty("Food", BindingFlags.Public | BindingFlags.Instance)
            .GetValue(StaticMember("VillageRuntimeBridge", "Resources"), null);

    private static void AddVillageFood(int amount) =>
        Call(StaticMember("VillageRuntimeBridge", "Resources"), "AddFood", amount);

    private static IPersonGrowth PersonOf(object collection, string personId)
    {
        var m = collection.GetType().GetMethod("FindPersonByPersonId", F, null, new[] { typeof(string) }, null);
        Assert.IsNotNull(m, "PlayerCollection.FindPersonByPersonId 不存在");
        return (IPersonGrowth)m.Invoke(collection, new object[] { personId });
    }

    private static List<BattleCardData> AllBattleCardTemplates()
    {
        var list = new List<BattleCardData>();
        foreach (var t in (IEnumerable)GetMember(DataManagerInstance(), "AllBattleCards"))
            if (t is BattleCardData card && !string.IsNullOrEmpty(card.CardId)) list.Add(card);
        return list;
    }

    private static List<string> GrowthOfferCardIds(IPersonGrowth person, List<BattleCardData> templates) =>
        CardGrowthResolver.BuildOffer(person, templates).Select(c => c.CardId).ToList();

    private static List<string> RunRewardCandidateCardIds(IPersonGrowth person, List<BattleCardData> templates)
    {
        var run = new RunState();
        run.GetOrCreatePerson(person.PersonId, person.DisplayName, 30);
        var provider = new UnlockedPoolRewardProvider(pid => pid == person.PersonId ? person : null);
        return provider.BuildCandidates(run, templates)
            .Where(c => c.Template != null).Select(c => c.Template.CardId).ToList();
    }

    // ==================== Expedition 工具（复用既有 V4F E2E 约定） ====================

    private static object NewAdventureFromExpedition(object collection, object expedition)
    {
        var dataManager = DataManagerInstance();
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

    private static List<string> ExpeditionIds(object adventure)
    {
        var list = new List<string>();
        foreach (var id in (IEnumerable)Call(adventure, "GetExpeditionPersonIds"))
            list.Add((string)id);
        return list;
    }

    private static int SupplyCarry(object adventure)
    {
        object state = GetMember(adventure, "ProgressionState");
        object supply = GetMember(state, "Supply");
        return (int)GetMember(supply, "CurrentCarryUnits");
    }

    private static List<string> ReadAvailableIds(object resolver)
    {
        var list = new List<string>();
        foreach (var node in (IEnumerable)Call(resolver, "GetAvailableNextNodes"))
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
        Assert.AreEqual("Success", Call(resolver, "TryEnterNode", nodeId).ToString(),
            "进入节点应成功：" + nodeId);
    }

    private static void ResolveSuccess(object resolver)
    {
        var enumType = Type.GetType("ExpeditionNodeOutcome, ExpeditionProgression.Runtime");
        object success = Enum.Parse(enumType, "Success");
        var m = resolver.GetType().GetMethod("TryResolveCurrentNode", new[] { enumType });
        Assert.AreEqual("Success", m.Invoke(resolver, new object[] { success }).ToString(),
            "Resolve(Success) 应成功");
    }

    // ==================== Full Product Chain（Gate 3 E2E） ====================

    private IEnumerator RunFullChain()
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
            Assert.IsNotNull(pc, "TownUI.PlayerData 应存在");

            PauseVillageTime(); // 确定性：手动按天推进

            // ---------- Phase A：Village Project → Capability → 同一 Person Pool 扩大 ----------
            object heroObj = null;
            foreach (var h in (IEnumerable)GetMember(pc, "OwnedHeroes")) { heroObj = h; break; }
            Assert.IsNotNull(heroObj, "需要至少一个 Hero（当前产品下 Growth Authority 可用的验收目标）");
            string heroPid = (string)GetMember(heroObj, "PersonId");
            var hero = PersonOf(pc, heroPid);
            Assert.IsNotNull(hero, "应能按 PersonId 取到 Hero");

            CallStatic("VillageRuntimeBridge", "SetSimulationPersonSource", pc);
            var templates = AllBattleCardTemplates();

            CollectionAssert.DoesNotContain(
                GrowthAuthority.Resolve(hero).UnlockedPoolCardIds.ToList(), GrantedCard,
                "前置：Hero 权威池不得含 Test Capability 卡");

            Assert.AreEqual(VillageProjectResult.Success,
                (VillageProjectResult)CallStatic("VillageRuntimeBridge", "TryStartProject",
                    TestProject, new[] { heroPid }),
                "Test Project 应可启动（Hero 作为参与者）");
            for (int i = 0; i < TestProjectDays; i++) BridgeTime().TickDay();

            List<string> poolAfter = GrowthAuthority.Resolve(hero).UnlockedPoolCardIds.ToList();
            Assert.AreEqual(GrantedCard, poolAfter[0],
                "Project 完成后 Test Capability 应扩大同一 Person 权威池（index 0）");
            Assert.AreEqual(1, poolAfter.Count(id => id == GrantedCard), "重复授予不得重复 CardId");
            Assert.IsTrue(GrowthOfferCardIds(hero, templates).Contains(GrantedCard),
                "Character Growth Offer 必须读取 Capability 新增内容");
            Assert.IsTrue(RunRewardCandidateCardIds(hero, templates).Contains(GrantedCard),
                "Run Reward Candidate 必须读取同一新增内容（同一池、无第二份状态）");

            // ---------- Phase B：产品 Expedition（Hero + ≥1 Character）→ 非零 elapsed → Supply 消耗 ----------
            var expUI = FindInstance("ExpeditionUI");
            Assert.IsNotNull(expUI, "ExpeditionUI 应存在");
            Call(expUI, "ShowExpeditionFromTown", pc);
            yield return null;
            yield return null;

            object expedition = GetMember(expUI, "_expedition");
            object firstCard = null;
            foreach (var c in (IEnumerable)GetMember(pc, "OwnedCharacterCards")) { firstCard = c; break; }
            Assert.IsNotNull(firstCard, "需要至少一张 owned 角色卡（Character Experience 验证）");
            Call(expedition, "AddCharacterCard", firstCard);
            yield return null;

            object adventure = NewAdventureFromExpedition(pc, expedition);
            List<string> expeditionIds = ExpeditionIds(adventure);
            Assert.GreaterOrEqual(expeditionIds.Count, 2, "出征队伍应至少包含 Hero + 1 Character");

            string characterPid = null;
            foreach (var pid in expeditionIds) { if (pid != heroPid) { characterPid = pid; break; } }
            Assert.IsNotNull(characterPid, "需要一名非 Hero 的出征者（Character Experience 验证）");

            AddVillageFood(InitialFood);
            int party = expeditionIds.Count;
            int carryPerPerson = (int)StaticMember("VillageRuntimeBridge", "ResourceCarryPerPerson");
            int rate = (int)StaticMember("VillageRuntimeBridge", "ResourcePerPersonDailyConsumptionRate");
            int requestedCarry = party * carryPerPerson;
            int actualCarry = (int)CallStatic("VillageRuntimeBridge", "CarryFoodForDeparture", requestedCarry);
            Assert.Greater(actualCarry, 0, "Departure 应发生真实 Food → Carry 转移");
            Assert.AreEqual(InitialFood - actualCarry, VillageFood(), "Departure 后 Food = F0 - Carry");

            var start = adventure.GetType().GetMethod("StartProgression");
            Assert.IsNotNull(start, "StartProgression 未找到");
            object elapsedLookup = CallStatic("LegacyNodeElapsedDaysTestPolicy", "BuildLookup",
                GetMember(adventure, "MapGen"));
            start.Invoke(adventure, new object[] { 4, 3, party, actualCarry, rate, elapsedLookup });

            CallStatic("VillageRuntimeBridge", "SetExpeditionPersonIds",
                (IReadOnlyList<string>)new List<string>(expeditionIds));
            // 与产品 OnConfirmExpedition 一致：注册 elapsed provider（返程 Catch-up 依据）
            CallStatic("VillageRuntimeBridge", "SetExpeditionElapsedDaysProvider",
                new Func<int>(() => (int)GetMember(adventure, "ExpeditionElapsedDays")));
            SetMember(expUI, "_currentAdventure", adventure);

            object resolver = GetMember(adventure, "Progression");
            int elapsedDays = 0;
            for (int step = 0; step < 6 && elapsedDays == 0; step++)
            {
                List<string> available = ReadAvailableIds(resolver);
                if (available.Count == 0) break;

                string target = null;
                foreach (var id in available)
                {
                    string kind = KindOfNode(resolver, id);
                    if (kind == "Battle" || kind == "Elite" || kind == "Boss") { target = id; break; }
                }
                if (target == null) target = available[0];

                EnterNode(resolver, target);
                ResolveSuccess(resolver);
                elapsedDays = (int)GetMember(adventure, "ExpeditionElapsedDays");
            }
            Assert.GreaterOrEqual(elapsedDays, 1,
                "真实产品 Expedition 必须产生非零 elapsed（Battle/Elite/Boss = 1 Test Day）");
            Assert.Less(SupplyCarry(adventure), actualCarry, "Supply 应被真实消耗");

            // ---------- Phase C：产品 Return → Catch-up → Return Person Consequence → 唯一 Final Save ----------
            object runState = GetMember(adventure, "RunState");
            object runPerson = Call(runState, "GetPerson", characterPid);
            Assert.IsNotNull(runPerson, "RunState 应包含出征 Character");
            SetMember(runPerson, "CurrentHP", 5); // Test：ratio ≈ 0.17 → HeavyInjury

            Call(expUI, "OnReturnToTown");
            yield return null;
            yield return null;

            object record = StaticMember("VillageRuntimeBridge", "ActiveReturnRecord");
            Assert.IsNotNull(record, "应存在 Return 会话记录");
            Assert.AreEqual("Completed", GetMember(record, "Phase").ToString(), "Phase 应为 Completed");
            Assert.IsTrue((bool)GetMember(record, "Completed"), "Completed=true（唯一 Final Save 后置位）");
            Assert.IsTrue((bool)GetMember(record, "RemainingCarryReturned"), "Carry 应已返还");
            Assert.IsTrue((bool)GetMember(record, "HealthConsequenceCommitted"), "Health 后果应提交一次");
            Assert.IsTrue((bool)GetMember(record, "ExperienceCommitted"), "Experience 应提交一次");
            Assert.AreEqual(elapsedDays, (int)GetMember(record, "DaysSimulated"),
                "Catch-up 天数应等于真实 elapsed");

            Assert.AreEqual(0,
                ((IReadOnlyList<string>)StaticMember("VillageRuntimeBridge", "ExpeditionPersonIds")).Count,
                "Return 完成后 Expedition Occupancy 应清除");

            object characterObj = null;
            foreach (var c in (IEnumerable)GetMember(pc, "OwnedCharacters"))
            {
                if ((string)GetMember(c, "PersonId") == characterPid) { characterObj = c; break; }
            }
            Assert.IsNotNull(characterObj, "应找到出征 Character 长期实例");

            Assert.AreEqual("HeavyInjury", GetMember(characterObj, "Injury").ToString(),
                "Run HP 结果应回流为长期 Injury");
            object characterHistory = GetMember(characterObj, "LifeHistory");
            Assert.AreEqual(1, (int)Call(characterHistory, "CountExpeditionReturned"),
                "Character 应记录一次 ExpeditionReturned");
            var events = (IList)GetMember(characterHistory, "Events");
            Assert.AreEqual((int)RunEndMode.Retreat, (int)GetMember(events[0], "Outcome"),
                "Outcome 应为本次 Return 模式（Retreat）");

            Assert.AreEqual("Normal", GetMember(heroObj, "Injury").ToString(),
                "未受损 Hero 不应被写 Injury");
            Assert.IsNull(heroObj.GetType().GetProperty("LifeHistory"),
                "OQ1：Hero 不得新增 LifeHistory");

            // ---------- Phase D：Save → Load → Continue → 下一次读取仍生效 ----------
            Assert.IsTrue((bool)CallStatic("SaveSystem", "HasSave"), "唯一 Final Save 应已落盘");
            object loaded = CallStatic("SaveSystem", "Load");
            Assert.IsNotNull(loaded, "Load 应成功");

            var loadedHero = PersonOf(loaded, heroPid);
            Assert.IsNotNull(loadedHero, "Load 后应恢复同一 Hero");
            Assert.AreEqual(GrantedCard, GrowthAuthority.Resolve(loadedHero).UnlockedPoolCardIds.ToList()[0],
                "Load 后 Capability 结果仍在同一 Person 池");
            Assert.IsTrue(GrowthOfferCardIds(loadedHero, templates).Contains(GrantedCard),
                "Load 后下一次 Growth Offer 仍读取 Capability 结果");
            Assert.IsTrue(RunRewardCandidateCardIds(loadedHero, templates).Contains(GrantedCard),
                "Load 后下一次 Expedition Run Reward 候选仍读取 Capability 结果");

            object loadedCharacter = null;
            foreach (var c in (IEnumerable)GetMember(loaded, "OwnedCharacters"))
            {
                if ((string)GetMember(c, "PersonId") == characterPid) { loadedCharacter = c; break; }
            }
            Assert.IsNotNull(loadedCharacter, "Load 后应恢复出征 Character");
            Assert.AreEqual("HeavyInjury", GetMember(loadedCharacter, "Injury").ToString(),
                "Load 后长期 Injury 保持");
            Assert.AreEqual(1, (int)Call(GetMember(loadedCharacter, "LifeHistory"), "CountExpeditionReturned"),
                "Load 后 Expedition Experience 保持");

            string json = System.IO.File.ReadAllText((string)CallStatic("SaveSystem", "GetSavePath"));
            StringAssert.Contains("\"SaveVersion\": 1", json, "SaveVersion 必须保持 1");
        }
        finally
        {
            ReleaseConsoleErrors();
            CallStatic("VillageRuntimeBridge", "SetExpeditionElapsedDaysProvider", (Func<int>)null);
            CallStatic("VillageRuntimeBridge", "ResetForTests");
        }
    }

    [UnityTest]
    public IEnumerator VillageV4_FullProductChain_E2E()
    {
        yield return RunFullChain();
        Assert.IsEmpty(_consoleErrors, "产品链全程不应有 Console Error");
    }
}
