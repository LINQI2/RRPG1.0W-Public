using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

/// <summary>
/// WP-1 Stage3：Return Contract 产品契约测试（GUI/交互式 Editor PlayMode）。
/// 验证（Retreat / Defeat / SafeReturn 三条产品 Return 路径，Progression elapsed>0 场景）：
///   ProgressionState.ExpeditionElapsedDays == ExpeditionRecord.EffectiveElapsedDays
///   == Village Catch-up DaysSimulated。
/// 经反射访问 Assembly-CSharp（团结引擎 asmdef 限制）；不改 V4F / Bridge / 产品流程。
/// </summary>
public class ExpeditionStage3ReturnContractPlayTests
{
    private const BindingFlags F =
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

    private static Type GetType(string name) => Type.GetType(name + ", Assembly-CSharp");

    private static object FindInstance(string typeName)
    {
        var t = GetType(typeName);
        foreach (var o in UnityEngine.Object.FindObjectsOfType(t, true))
            if (o != null) return o;
        return null;
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
        var m = GetType(typeName).GetMethod(method,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
        Assert.IsNotNull(m, "静态方法不存在：" + typeName + "." + method);
        return m.Invoke(null, args);
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
    private static object StateOf(object adventure) => GetMember(adventure, "ProgressionState");
    private static int ProgressionElapsed(object adventure) => (int)GetMember(StateOf(adventure), "ExpeditionElapsedDays");

    private static void EnterCurrentNode(object resolver, string nodeId)
    {
        object result = Call(resolver, "TryEnterNode", nodeId);
        Assert.AreEqual("Success", result.ToString(), "进入节点应成功：" + nodeId);
    }

    private static void ResolveSuccess(object resolver)
    {
        var enumType = Type.GetType("ExpeditionNodeOutcome, ExpeditionProgression.Runtime");
        object success = Enum.Parse(enumType, "Success");
        var m = resolver.GetType().GetMethod("TryResolveCurrentNode", new[] { enumType });
        Assert.IsNotNull(m, "TryResolveCurrentNode(outcome) 未找到");
        object result = m.Invoke(resolver, new object[] { success });
        Assert.AreEqual("Success", result.ToString(), "Resolve(Success) 应成功");
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

    private static void RegisterPersonsAndProvider(object adventure, object expUI)
    {
        var ids = (System.Collections.IEnumerable)Call(adventure, "GetExpeditionPersonIds");
        var list = new List<string>();
        foreach (var id in ids) list.Add((string)id);
        Assert.Greater(list.Count, 0, "出征名单应非空（需真实 PersonId）");

        CallStatic("VillageRuntimeBridge", "SetExpeditionPersonIds", (IReadOnlyList<string>)list);
        CallStatic("VillageRuntimeBridge", "SetExpeditionElapsedDaysProvider",
            new Func<int>(() => (int)GetMember(adventure, "ExpeditionElapsedDays")));
        SetField(expUI, "_currentAdventure", adventure);
    }

    private static void ClearProvider()
    {
        CallStatic("VillageRuntimeBridge", "SetExpeditionElapsedDaysProvider", (Func<int>)null);
    }

    private static int CountPersons(object adventure)
    {
        int n = 0;
        foreach (var id in (System.Collections.IEnumerable)Call(adventure, "GetExpeditionPersonIds")) n++;
        return n;
    }

    private static object ActiveReturnRecord()
    {
        var bridgeType = GetType("VillageRuntimeBridge");
        var f = bridgeType.GetField("ActiveReturnRecord", F);
        if (f != null) return f.GetValue(null);
        var p = bridgeType.GetProperty("ActiveReturnRecord", F);
        Assert.IsNotNull(p, "ActiveReturnRecord 不存在");
        return p.GetValue(null, null);
    }

    private IEnumerator RunReturnContract(string mode)
    {
        try
        {
            ResetBridgeAndDeleteSave();
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

            var adventure = NewAdventureFromExpedition(pc, expedition, DataManagerInstance());
            int party = CountPersons(adventure);
            Assert.Greater(party, 0, "出征 Person 应非空");

            var start = adventure.GetType().GetMethod("StartProgression");
            Assert.IsNotNull(start, "StartProgression 未找到");
            start.Invoke(adventure, new object[] { 4, 3, party, 5000, 1, new Func<string, int>(id => 1) });
            RegisterPersonsAndProvider(adventure, expUI);

            var resolver = ResolverOf(adventure);
            Assert.IsNotNull(resolver, "Progression 应已初始化");
            int expectedResolved = 0;

            if (mode == "SafeReturn")
            {
                for (int guard = 0; guard < 64; guard++)
                {
                    var avail = ReadAvailableIds(resolver);
                    Assert.Greater(avail.Count, 0, "推进中应始终存在可用节点");
                    string next = avail[0];
                    if (KindOfNode(resolver, next) == "Boss")
                    {
                        EnterCurrentNode(resolver, next);
                        expectedResolved++;
                        break;
                    }
                    EnterCurrentNode(resolver, next);
                    ResolveSuccess(resolver);
                    expectedResolved++;
                }
                Call(expUI, "OnReturnFromBattle", true); // Boss 胜利 → RouteCompleted → SafeReturn
            }
            else if (mode == "Retreat")
            {
                var first = ReadAvailableIds(resolver)[0];
                EnterCurrentNode(resolver, first);
                ResolveSuccess(resolver);
                expectedResolved = 1;
                Call(expUI, "OnReturnToTown"); // Retreat 产品路径
            }
            else // Defeat
            {
                var first = ReadAvailableIds(resolver)[0];
                EnterCurrentNode(resolver, first);
                expectedResolved = 1;
                Call(expUI, "OnReturnFromBattle", false); // 战败 → Failure resolve → Defeat Return
            }
            yield return null;
            yield return null;

            int progressionElapsed = ProgressionElapsed(adventure);
            Assert.AreEqual(expectedResolved, progressionElapsed,
                mode + "：Progression.ExpeditionElapsedDays 应等于已结算节点数（每节点1天）");

            object record = ActiveReturnRecord();
            Assert.IsNotNull(record, mode + "：应存在 Return 会话记录");
            int effective = (int)GetMember(record, "EffectiveElapsedDays");
            int simulated = (int)GetMember(record, "DaysSimulated");
            Assert.AreEqual(progressionElapsed, effective,
                mode + "：EffectiveElapsedDays == Progression.ExpeditionElapsedDays");
            Assert.AreEqual(effective, simulated,
                mode + "：DaysSimulated == EffectiveElapsedDays（Catch-up 逐日完成）");
            Assert.AreEqual("Completed", GetMember(record, "Phase").ToString(), mode + "：Phase 应为 Completed");
            Assert.IsTrue((bool)GetMember(record, "Completed"), mode + "：Completed=true（唯一 Final Save 后置位）");

            int remaining = (int)GetMember(GetMember(StateOf(adventure), "Supply"), "CurrentCarryUnits");
            Assert.AreEqual(5000 - party * expectedResolved, remaining,
                mode + "：Supply 应按已结算节点 exactly-once 扣除，无双扣");
        }
        finally
        {
            ClearProvider();
            CallStatic("VillageRuntimeBridge", "ResetForTests");
        }
    }

    [UnityTest]
    public IEnumerator Retreat_ProgressionElapsed_FeedsV4F_DaysSimulated()
    {
        yield return Pump(RunReturnContract("Retreat"));
    }

    [UnityTest]
    public IEnumerator Defeat_ProgressionElapsed_FeedsV4F_DaysSimulated()
    {
        yield return Pump(RunReturnContract("Defeat"));
    }

    [UnityTest]
    public IEnumerator SafeReturn_ProgressionElapsed_FeedsV4F_DaysSimulated()
    {
        yield return Pump(RunReturnContract("SafeReturn"));
    }
}

