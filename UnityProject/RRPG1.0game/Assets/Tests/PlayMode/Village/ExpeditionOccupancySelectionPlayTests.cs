using TMPro;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

/// <summary>
/// V2 Final Closure：Expedition 选人入口与统一 Temporary Occupancy 的互斥产品集成测试。
/// 从真实 Town 产品路径打开远征准备：Active Project 参与者卡片应不可选并显示占用原因；
/// 其它未占用 Person 默认自动选中（可出征）。占用点击守卫与 ExpeditionSystem.ConfirmExpedition
/// 权威防线在其它测试覆盖。
/// </summary>
public class ExpeditionOccupancySelectionPlayTests
{
    private const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
    private const BindingFlags IF = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

    private static System.Type BridgeType => System.Type.GetType("VillageRuntimeBridge, Assembly-CSharp");
    private static System.Type SaveSystemType => System.Type.GetType("SaveSystem, Assembly-CSharp");

    [SetUp]
    public void Reset()
    {
        ResetBridge();
        DeleteSaveFile();
    }

    [TearDown]
    public void Cleanup()
    {
        ResetBridge();
        DeleteSaveFile();
    }

    private static void ResetBridge()
    {
        if (BridgeType != null) InvokeStatic(BridgeType, "ResetForTests");
    }

    private static void DeleteSaveFile()
    {
        if (SaveSystemType == null) return;
        string path = (string)InvokeStatic(SaveSystemType, "GetSavePath");
        if (!string.IsNullOrEmpty(path) && System.IO.File.Exists(path))
            System.IO.File.Delete(path);
    }

    private static object InvokeStatic(System.Type type, string method, params object[] args)
    {
        var methods = System.Linq.Enumerable.Where(type.GetMethods(F), m => m.Name == method);
        MethodInfo selected = null;
        foreach (var m in methods)
        {
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
        Assert.IsNotNull(selected, "方法不存在：" + method + " @ " + type.FullName);
        return selected.Invoke(null, args);
    }

    private static object FindInstance(string typeName)
    {
        var t = System.Type.GetType(typeName + ", Assembly-CSharp");
        if (t == null) return null;
        foreach (var o in Object.FindObjectsOfType(t, true))
        {
            if (o != null) return o;
        }
        return null;
    }

    private static object InvokeInstance(object instance, string method, params object[] args)
    {
        var m = instance.GetType().GetMethod(method, IF);
        Assert.IsNotNull(m, "实例方法不存在：" + method);
        return m.Invoke(instance, args);
    }

    private static Transform FindDescendant(Transform t, string name)
    {
        if (t.name == name) return t;
        for (int i = 0; i < t.childCount; i++)
        {
            var r = FindDescendant(t.GetChild(i), name);
            if (r != null) return r;
        }
        return null;
    }

    // ==================== 真实 Town 选人互斥 ====================

    private static string GetTownFirstCharacter(out string cardId)
    {
        var town = FindInstance("TownUI");
        Assert.IsNotNull(town, "应有 TownUI");
        ((GameObject)town.GetType().GetProperty("gameObject").GetValue(town, null)).SetActive(true);
        var pc = town.GetType().GetProperty("PlayerData").GetValue(town, null);
        Assert.IsNotNull(pc, "Town 应持有 PlayerCollection");

        var chars = (System.Collections.IList)pc.GetType().GetField("OwnedCharacters").GetValue(pc);
        Assert.IsTrue(chars.Count > 0, "应有长期角色");
        // M1：人物唯一身份 = 实例 PersonId（不再把模板 CharacterId 当 PersonId）
        string pid = (string)chars[0].GetType().GetField("PersonId").GetValue(chars[0]);

        var ownedCards = (System.Collections.IList)pc.GetType().GetField("OwnedCharacterCards").GetValue(pc);
        cardId = null;
        foreach (var c in ownedCards)
        {
            if (c == null) continue;
            var owner = InvokeInstance(pc, "GetCharacterInstanceForCard", c);
            if (owner == null) continue;
            string ownerPid = (string)owner.GetType().GetField("PersonId").GetValue(owner);
            if (ownerPid == pid)
            {
                cardId = (string)c.GetType().GetField("CardId").GetValue(c);
                break;
            }
        }
        Assert.IsFalse(string.IsNullOrEmpty(cardId), "应能找到对应角色卡");
        return pid;
    }

    [UnityTest]
    public IEnumerator ExpeditionSelection_ProjectReservationAllowsDeparture_DutyVisible_RealTown()
    {
        SceneManager.LoadScene("SampleScene");
        yield return null;
        yield return null;

        string pid = GetTownFirstCharacter(out string occupiedCardId);
        yield return null;

        // 让该 Person 投入 Active Project（占用）
        int startResult = (int)InvokeStatic(BridgeType, "TryStartProject", "prj_test_01", new[] { pid });
        Assert.AreEqual(0, startResult, "TryStartProject 应成功（Success=0）");

        // 真实 Town → 远征准备
        var expUi = FindInstance("ExpeditionUI");
        Assert.IsNotNull(expUi, "应有 ExpeditionUI");
        var town = FindInstance("TownUI");
        object pc = town.GetType().GetProperty("PlayerData").GetValue(town, null);
        InvokeInstance(expUi, "ShowExpeditionFromTown", pc);
        yield return null;
        yield return null;

        // 占用者卡片：存在、不可选、显示原因
        var expTransform = ((Component)expUi).transform;
        var occupiedCard = FindDescendant(expTransform, "CharCard_" + occupiedCardId);
        Assert.IsNotNull(occupiedCard, "应存在占用者角色卡 CharCard_" + occupiedCardId);
        var btn = occupiedCard.GetComponent<Button>();
        Assert.IsNotNull(btn);
        Assert.IsTrue(btn.interactable, "项目容量预约允许出征，缺席不劳动");
        var text = occupiedCard.GetComponentInChildren<TMP_Text>();
        Assert.IsNotNull(text, "卡片应有文本");
        StringAssert.DoesNotContain("不可出征", text.text, "项目承诺不得显示成整天出征禁令");
        // 既有卡面只显示战斗摘要；长期职责位于真实可点击的详情面板。
        var identity = FindInstance("ExpeditionIdentityPanel");
        Assert.IsNotNull(identity);
        var detailButton = (Button)identity.GetType().GetProperty("DetailButton").GetValue(identity);
        Assert.IsTrue(detailButton.gameObject.activeInHierarchy && detailButton.interactable);
        detailButton.onClick.Invoke();
        yield return null;
        Assert.IsTrue((bool)identity.GetType().GetProperty("IsPopupOpen").GetValue(identity));
        StringAssert.Contains("投入工程", (string)identity.GetType().GetProperty("DetailText").GetValue(identity),
            "打开真实详情后仍显示保留的长期工程职责");
        ((Button)identity.GetType().GetProperty("CloseButton").GetValue(identity)).onClick.Invoke();

        // 其它未占用 Person 应被默认选中可出征（Project participant 不进入选择）
        var expedition = expUi.GetType().GetField("_expedition", IF).GetValue(expUi);
        var selected = (System.Collections.IList)expedition.GetType().GetProperty("SelectedCharacters").GetValue(expedition, null);
        Assert.Greater(selected.Count, 0, "应自动选中一个可出征角色卡");
        bool containsOccupied = false;
        foreach (var c in selected)
        {
            if (c == null) continue;
            var owner = InvokeInstance(pc, "GetCharacterInstanceForCard", c);
            if (owner != null && (string)owner.GetType().GetField("PersonId").GetValue(owner) == pid)
            {
                containsOccupied = true;
                break;
            }
        }
        Assert.IsTrue(containsOccupied, "项目预约者可进入出征选择，身份仍按 PersonId 绑定");
    }
}
