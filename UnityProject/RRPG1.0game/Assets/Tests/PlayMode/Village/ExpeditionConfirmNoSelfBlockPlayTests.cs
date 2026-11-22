using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

/// <summary>
/// M1 人工验收修复（Expedition Occupancy 自误判）PlayMode 回归：
/// 真实 Town → 打开远征准备 → 单击「确认出征」一次。
/// 断言：
///   1) 空闲人物确认成功（ExpeditionUI 隐藏、进入冒险）；
///   2) 只登记本次出征者（无第二次“自己拦自己”的确认执行）；
///   3) Console 不出现 [ExpeditionSystem] 无法出征 warning（自误判回归检测）。
/// </summary>
public class ExpeditionConfirmNoSelfBlockPlayTests
{
    private const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

    private static System.Type GetType(string name) => System.Type.GetType(name + ", Assembly-CSharp");
    private static System.Type BridgeType => GetType("VillageRuntimeBridge");
    private static System.Type SaveSystemType => GetType("SaveSystem");

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

    private static object GetProperty(object o, string name)
    {
        var p = o.GetType().GetProperty(name, F);
        Assert.IsNotNull(p, "属性不存在：" + name);
        return p.GetValue(o, null);
    }

    private static object GetField(object o, string name)
    {
        var f = o.GetType().GetField(name, F);
        Assert.IsNotNull(f, "字段不存在：" + name);
        return f.GetValue(o);
    }

    private static object InvokeInstance(object o, string method, params object[] args)
    {
        var m = o.GetType().GetMethod(method, F);
        Assert.IsNotNull(m, "方法不存在：" + method);
        return m.Invoke(o, args);
    }

    private static object InvokeStatic(System.Type type, string method, params object[] args)
    {
        var m = type.GetMethod(method, F);
        Assert.IsNotNull(m, "静态方法不存在：" + method);
        return m.Invoke(null, args);
    }

    [SetUp]
    public void SetUp()
    {
        InvokeStatic(BridgeType, "ResetForTests");
        DeleteSaveFile();
    }

    [TearDown]
    public void TearDown()
    {
        InvokeStatic(BridgeType, "ResetForTests");
        DeleteSaveFile();
    }

    private static void DeleteSaveFile()
    {
        if (SaveSystemType == null) return;
        string path = (string)InvokeStatic(SaveSystemType, "GetSavePath");
        if (!string.IsNullOrEmpty(path) && System.IO.File.Exists(path))
            System.IO.File.Delete(path);
    }

    private static object EnterTown()
    {
        var town = FindInstance("TownUI");
        Assert.IsNotNull(town, "场景应有 TownUI");
        var mm = FindInstance("MainMenuUI");
        Assert.IsNotNull(mm, "场景应有 MainMenuUI");
        if (!(bool)InvokeStatic(SaveSystemType, "HasSave"))
        {
            var fresh = InvokeStatic(SaveSystemType, "NewGame");
            Assert.IsNotNull(fresh);
            Assert.IsTrue((bool)InvokeStatic(SaveSystemType, "Save", fresh), "新档应能写入");
        }
        InvokeInstance(mm, "OnContinue");
        return FindInstance("TownUI");
    }

    [UnityTest]
    public IEnumerator SingleConfirmClick_DoesNotSelfBlock_AndRegistersExpeditionOnce()
    {
        SceneManager.LoadScene("SampleScene");
        yield return null;
        yield return null;
        var town = EnterTown();
        Assert.IsNotNull(town);
        yield return null;
        yield return null;

        var expUi = FindInstance("ExpeditionUI");
        Assert.IsNotNull(expUi, "应有 ExpeditionUI");
        object pc = GetProperty(town, "PlayerData");
        Assert.IsNotNull(pc, "Town 应持有 PlayerCollection");
        InvokeInstance(expUi, "ShowExpeditionFromTown", pc);
        yield return null;
        yield return null;

        // 自动选择的可出征角色卡数量
        var expedition = GetField(expUi, "_expedition");
        var selected = (IList)GetProperty(expedition, "SelectedCharacters");
        Assert.Greater(selected.Count, 0, "打开远征准备应自动选中至少一张可出征角色卡");

        // 监听自误判 warning
        var selfBlockWarnings = new List<string>();
        Application.LogCallback handler = (condition, stack, type) =>
        {
            if (type == LogType.Warning && condition != null && condition.Contains("[ExpeditionSystem]"))
                selfBlockWarnings.Add(condition);
        };
        Application.logMessageReceived += handler;

        // 单击「确认出征」一次（幂等绑定后仅触发一次 OnConfirmExpedition）
        var btnConfirm = (Button)GetField(expUi, "btnConfirm");
        Assert.IsNotNull(btnConfirm);

        // V4 Functional Repair：产品入口新增 Carry preflight（Carry 无法承担第一个合法节点的成本时会明确拒绝出征）。
        // 本测试目标是「单击不自阻塞」，因此先提供真实 Village Food（真实 Food → Carry 转移，不做 Debug 注入伪造 Carry）。
        var resourcesField = BridgeType.GetField("Resources", F);
        Assert.IsNotNull(resourcesField, "VillageRuntimeBridge.Resources 静态字段应存在");
        object villageResources = resourcesField.GetValue(null);
        Assert.IsNotNull(villageResources, "Village Resources 应已初始化");
        InvokeInstance(villageResources, "AddFood", 100);

        btnConfirm.onClick.Invoke();
        yield return null;
        yield return null;

        var expGo = (GameObject)GetProperty(expUi, "gameObject");
        Assert.IsFalse(expGo.activeSelf, "空闲人物确认后应成功进入远征（ExpeditionUI 隐藏）");

        // 本次实际出征人数 = 选中的角色卡 + 已选英雄（若选）
        int expectedPersons = selected.Count;
        var selectedHero = GetProperty(expedition, "SelectedHero");
        if (selectedHero != null) expectedPersons++;

        var registered = (IReadOnlyList<string>)InvokeStatic(BridgeType, "get_ExpeditionPersonIds");
        Assert.AreEqual(expectedPersons, registered.Count,
            "成功确认只登记本次出征者一次（角色 + 英雄），不得被第二次（重复）确认重复登记");
        Assert.AreEqual(0, selfBlockWarnings.Count,
            "不得出现“无法出征：正在远征中”的自误判 warning");

        Application.logMessageReceived -= handler;
    }
}
