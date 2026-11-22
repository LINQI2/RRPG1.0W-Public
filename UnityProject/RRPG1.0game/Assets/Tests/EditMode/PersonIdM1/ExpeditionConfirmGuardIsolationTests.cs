using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;

/// <summary>
/// M1 人工验收修复回归（Expedition Occupancy 自误判）——防线/登记时序语义（EditMode）。
/// 规则：
///   - Confirm 前：本次待出征者不得因“本次选择”被视为既有 Expedition Occupancy；
///   - 已在 Active Project / 既有 Expedition 的人物必须被真正拦截；
///   - 成功 Confirm 后才进入正式 Expedition Occupancy 登记；
///   - 失败 Confirm 不得残留 Expedition Occupancy；
///   - 同 CharacterId 的 P1/P2：P1 占用不得误伤 P2。
/// </summary>
public class ExpeditionConfirmGuardIsolationTests
{
    private static System.Type BridgeType => M1TestKit.Asm("VillageRuntimeBridge");

    private static List<string> ExpeditionIds()
    {
        return new List<string>((IReadOnlyList<string>)M1TestKit.InvokeStatic(BridgeType, "get_ExpeditionPersonIds"));
    }

    private static void SetExpedition(IEnumerable<string> ids)
    {
        M1TestKit.InvokeStatic(BridgeType, "SetExpeditionPersonIds", ids);
    }

    private static void ClearExpedition()
    {
        M1TestKit.InvokeStatic(BridgeType, "ClearExpeditionPersonIds");
    }

    [SetUp]
    public void SetUp()
    {
        M1TestKit.InvokeStatic(BridgeType, "ResetForTests");
        M1TestKit.InvokeStatic(BridgeType, "EnsureInitialized", new object[] { null });
    }

    [TearDown]
    public void TearDown()
    {
        M1TestKit.InvokeStatic(BridgeType, "ResetForTests");
    }

    /// <summary>给集合添加一个真实人物实例 + 角色卡 + 显式绑定。</summary>
    private static object AddCharacterWithCard(object collection, string templateId, string cardId)
    {
        object character = M1TestKit.NewCharacter(templateId, templateId, 30);
        object card = M1TestKit.NewCharacterCard(cardId, templateId);
        M1TestKit.OwnedCharacters(collection).Add(character);
        M1TestKit.OwnedCharacterCards(collection).Add(card);
        M1TestKit.InvokeInstance(collection, "BindCharacterCardOwner", cardId, M1TestKit.PersonIdOf(character));
        return card;
    }

    private static object NewExpedition(object collection, object card)
    {
        object exp = System.Activator.CreateInstance(M1TestKit.Asm("ExpeditionSystem"), new object[] { collection });
        var selected = (IList)exp.GetType().GetProperty("SelectedCharacters").GetValue(exp, null);
        selected.Add(card);
        return exp;
    }

    private static bool Confirm(object exp)
    {
        return (bool)exp.GetType().GetMethod("ConfirmExpedition").Invoke(exp, new object[] { null, null });
    }

    [Test]
    public void IdlePerson_ConfirmSucceeds_AndExpeditionRegistrationIsExpected_NoSelfBlock()
    {
        object collection = M1TestKit.NewPlayerCollection();
        object card = AddCharacterWithCard(collection, "char_001", "card_char_001_idle");
        object exp = NewExpedition(collection, card);
        ClearExpedition();

        Assert.IsTrue(Confirm(exp), "空闲人物 Confirm 必须成功（不得因本次选择被当作既有 Expedition）");
        // 模拟 UI 成功路径登记（仅成功后才登记）
        string pid = M1TestKit.PersonIdOf(M1TestKit.OwnedCharacters(collection)[0]);
        SetExpedition(new List<string> { pid });
        CollectionAssert.Contains(ExpeditionIds(), pid);
        Assert.AreEqual(1, ExpeditionIds().Count, "只登记本次出征者");
    }

    [Test]
    public void ActiveProjectPerson_ConfirmAllowedWithReservationRetained()
    {
        object collection = M1TestKit.NewPlayerCollection();
        object card = AddCharacterWithCard(collection, "char_001", "card_char_001_prj");
        string pid = M1TestKit.PersonIdOf(M1TestKit.OwnedCharacters(collection)[0]);
        ClearExpedition();

        int startResult = (int)M1TestKit.InvokeStatic(BridgeType, "TryStartProject", "prj_test_01", new[] { pid });
        Assert.AreEqual(0, startResult, "TryStartProject 应成功（Success=0）");

        object exp = NewExpedition(collection, card);
        Assert.IsTrue(Confirm(exp), "项目承诺保留，但不阻止出征");
        Assert.AreEqual(0, ExpeditionIds().Count, "Confirm 本身仍不提前写 UI 负责的出征登记");
    }

    [Test]
    public void AlreadyExpeditionPerson_ConfirmIsBlocked()
    {
        object collection = M1TestKit.NewPlayerCollection();
        object card = AddCharacterWithCard(collection, "char_001", "card_char_001_exp");
        string pid = M1TestKit.PersonIdOf(M1TestKit.OwnedCharacters(collection)[0]);
        SetExpedition(new List<string> { pid });

        object exp = NewExpedition(collection, card);
        Assert.IsFalse(Confirm(exp), "已有既有 Expedition Occupancy 的人物必须被真正拦截");
    }

    [Test]
    public void FailedConfirm_LeavesNoExpeditionOccupancyResidue()
    {
        object collection = M1TestKit.NewPlayerCollection();
        object card = AddCharacterWithCard(collection, "char_001", "card_char_001_fail");
        string pid = M1TestKit.PersonIdOf(M1TestKit.OwnedCharacters(collection)[0]);

        // 预置一个无关的既有远征占用（模拟另一人物正在远征）——失败 Confirm 不得触碰它
        object other = M1TestKit.NewCharacter("char_002", "char_002", 22);
        M1TestKit.OwnedCharacters(collection).Add(other);
        SetExpedition(new List<string> { M1TestKit.PersonIdOf(other) });

        // Explicit unavailable gate remains a real failed-confirm source; project reservation is no longer a gate.
        int startResult = (int)M1TestKit.InvokeStatic(BridgeType, "TryStartProject", "prj_test_01", new[] { pid });
        Assert.AreEqual(0, startResult);
        M1TestKit.InvokeStatic(BridgeType,"SetUnavailablePersonIds",(object)new[]{pid});
        object exp = NewExpedition(collection, card);
        Assert.IsFalse(Confirm(exp), "占用者 Confirm 应失败");

        // 失败 Confirm 不得新增 pid 登记，也不得清除其它既有远征登记
        CollectionAssert.DoesNotContain(ExpeditionIds(), pid, "失败 Confirm 不得残留被拦者的 Expedition Occupancy");
        Assert.AreEqual(1, ExpeditionIds().Count, "失败 Confirm 不得破坏既有远征登记");
    }

    [Test]
    public void SameTemplate_P1ProjectOccupancy_DoesNotBlockP2_Confirm()
    {
        object collection = M1TestKit.NewPlayerCollection();
        // 同 CharacterId 双实例 P1/P2
        object p1 = M1TestKit.NewCharacter("char_001", "char_001甲", 30);
        object p2 = M1TestKit.NewCharacter("char_001", "char_001乙", 30);
        M1TestKit.OwnedCharacters(collection).Add(p1);
        M1TestKit.OwnedCharacters(collection).Add(p2);
        object card1 = M1TestKit.NewCharacterCard("card_char_001_p1", "char_001");
        object card2 = M1TestKit.NewCharacterCard("card_char_001_p2", "char_001");
        M1TestKit.OwnedCharacterCards(collection).Add(card1);
        M1TestKit.OwnedCharacterCards(collection).Add(card2);
        M1TestKit.InvokeInstance(collection, "BindCharacterCardOwner", "card_char_001_p1", M1TestKit.PersonIdOf(p1));
        M1TestKit.InvokeInstance(collection, "BindCharacterCardOwner", "card_char_001_p2", M1TestKit.PersonIdOf(p2));

        ClearExpedition();
        // P1 投入 Active Project（占用），P2 空闲
        int startResult = (int)M1TestKit.InvokeStatic(BridgeType, "TryStartProject", "prj_test_01", new[] { M1TestKit.PersonIdOf(p1) });
        Assert.AreEqual(0, startResult);

        // P2 Confirm 必须成功（P1 占用不误伤同模板的 P2）
        object exp2 = NewExpedition(collection, card2);
        Assert.IsTrue(Confirm(exp2), "P1 的占用不得拦截同模板 P2 出征");

        // 模拟 UI 登记 P2 后：只登记 P2，P1 不应被误标为 Expedition
        SetExpedition(new List<string> { M1TestKit.PersonIdOf(p2) });
        CollectionAssert.Contains(ExpeditionIds(), M1TestKit.PersonIdOf(p2));
        CollectionAssert.DoesNotContain(ExpeditionIds(), M1TestKit.PersonIdOf(p1), "P1 不得被误标为 Expedition");
    }
}
