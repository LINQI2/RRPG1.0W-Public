using TMPro;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Battle Formation &amp; Active Roster 一致性长期回归契约
/// （Current Battle GameSpec：R4.1 上阵数量上限 / R5.0 一排 = Left-Center-Right 三列 / R8.1 本场有效角色牌）。
///
/// 保护的不变量（Preparation / Active / Downed 三阶段分离）：
///   Preparation：_rowCards ↔ _pendingRows ↔ _pendingCols 成员一致；每排 ≤ 3；同排列唯一；总数 ≤ 6；
///                非法第 4 人既不进入 Pending、也不创建视觉槽，Prep Card 保持可用；
///   OpenBattle ：OnStartBattle 在写入任何开战状态之前做全量预检；非法阵型不得把 _battleStarted 置 true；
///   Active     ：成功部署的 PlayerUnits 是共享牌库人物集合的事实来源（不得存在 ghost 牌主）；
///   Downed     ：0HP 释放 Board Occupancy，但 BattleUnit 与视觉卡继续存在（不得当成 ghost / 不得要求永远占格）。
///
/// 说明：BattleUI / BattleFormationPlan / BattleSystem 位于预定义程序集 Assembly-CSharp，
/// 与既有 EditMode 契约测试一致通过 System.Reflection 访问；本组用最小 EditMode 宿主
/// （Canvas + 六排 RectTransform + 真实 UnitCard.prefab）驱动真实 PlaceUnit / 拖拽写入路径。
/// </summary>
public class BattleFormationIntegrityTests
{
    private const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    private const string UnitCardPrefabPath = "Assets/Prefabs/UI/UnitCard.prefab";

    private GameObject _root;
    private GameObject _uiGo;
    private object _ui;
    private RectTransform _front, _middle, _back, _eFront, _eMiddle, _eBack;
    private readonly List<UnityEngine.Object> _createdScriptables = new List<UnityEngine.Object>();
    private readonly List<Member> _members = new List<Member>();

    private sealed class Member
    {
        public int Index;
        public object Card;
        public object CharData;
        public string CardId;
        public string CharacterId;
    }

    // ==================== 反射工具 ====================

    // M2 Slice 2A：MonsterDefinition 位于 StaticConfig.Runtime（不是 Assembly-CSharp）→ 解析器加回退。
    private static Type T(string name) => Type.GetType(name + ", Assembly-CSharp")
        ?? Type.GetType("RRPG.StaticConfig." + name + ", StaticConfig.Runtime");

    private static object CallStatic(string typeName, string method, params object[] args)
    {
        var t = T(typeName);
        Assert.IsNotNull(t, "类型未找到：" + typeName);
        var m = t.GetMethod(method, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
        Assert.IsNotNull(m, "静态方法不存在：" + typeName + "." + method);
        return m.Invoke(null, args);
    }

    private static object Call(object target, string method, params object[] args)
    {
        var m = target.GetType().GetMethod(method, F);
        Assert.IsNotNull(m, "方法不存在：" + method);
        return m.Invoke(target, args);
    }

    /// <summary>调用带单个 out 参数的方法，返回方法返回值，并取回 out 值。</summary>
    private static object CallWithOut(object target, string method, out object outValue, params object[] inArgs)
    {
        var m = target.GetType().GetMethod(method, F);
        Assert.IsNotNull(m, "方法不存在：" + method);
        var args = new object[inArgs.Length + 1];
        for (int i = 0; i < inArgs.Length; i++) args[i] = inArgs[i];
        object ret = m.Invoke(target, args);
        outValue = args[args.Length - 1];
        return ret;
    }

    private static object GetField(object target, string field)
    {
        var f = target.GetType().GetField(field, F);
        Assert.IsNotNull(f, "字段不存在：" + field);
        return f.GetValue(target);
    }

    private static object GetProperty(object target, string name)
    {
        var p = target.GetType().GetProperty(name, F);
        Assert.IsNotNull(p, "属性不存在：" + name);
        return p.GetValue(target, null);
    }

    private static void SetField(object target, string field, object value)
    {
        var f = target.GetType().GetField(field, F);
        Assert.IsNotNull(f, "字段不存在：" + field);
        f.SetValue(target, value);
    }

    private static void SetMember(object target, string name, object value)
    {
        var t = target.GetType();
        var f = t.GetField(name, F);
        if (f != null) { f.SetValue(target, value); return; }
        var p = t.GetProperty(name, F);
        Assert.IsNotNull(p, "成员不存在：" + name);
        p.SetValue(target, value, null);
    }

    private static IList NewList(string elementTypeName)
    {
        var t = T(elementTypeName);
        Assert.IsNotNull(t, "类型未找到：" + elementTypeName);
        return (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(t));
    }

    private static object NewDict(string valueTypeName)
    {
        return Activator.CreateInstance(typeof(Dictionary<,>).MakeGenericType(typeof(string), T(valueTypeName)));
    }

    private static object EnumVal(string enumTypeName, int v) => Enum.ToObject(T(enumTypeName), v);

    private static int RowIndex(object battleRow) => Convert.ToInt32(battleRow);
    private static int ColIndex(object battleColumn) => Convert.ToInt32(battleColumn);

    private UnityEngine.Object NewScriptable(string typeName)
    {
        var so = (UnityEngine.Object)ScriptableObject.CreateInstance(T(typeName));
        _createdScriptables.Add(so);
        return so;
    }

    // ==================== EditMode 最小宿主 ====================

    [SetUp]
    public void SetUp()
    {
        _root = new GameObject("FormationContractCanvas", typeof(RectTransform), typeof(Canvas));
        _root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;

        _front = NewRow("playerFrontRow");
        _middle = NewRow("playerMiddleRow");
        _back = NewRow("playerBackRow");
        _eFront = NewRow("enemyFrontRow");
        _eMiddle = NewRow("enemyMiddleRow");
        _eBack = NewRow("enemyBackRow");

        var infoGo = new GameObject("InfoText", typeof(RectTransform), typeof(TextMeshProUGUI));
        infoGo.transform.SetParent(_root.transform, false);
        var info = infoGo.GetComponent<TMP_Text>();

        _uiGo = new GameObject("BattleUIHost", typeof(RectTransform));
        _ui = _uiGo.AddComponent(T("BattleUI"));

        // 产品宿主引用（真实 UnitCard.prefab）
        SetField(_ui, "playerFrontRow", _front);
        SetField(_ui, "playerMiddleRow", _middle);
        SetField(_ui, "playerBackRow", _back);
        SetField(_ui, "enemyFrontRow", _eFront);
        SetField(_ui, "enemyMiddleRow", _eMiddle);
        SetField(_ui, "enemyBackRow", _eBack);
        SetField(_ui, "infoText", info);
        SetField(_ui, "handContainer", _root.GetComponent<RectTransform>());
        SetField(_ui, "unitCardPrefab", AssetDatabase.LoadAssetAtPath<GameObject>(UnitCardPrefabPath));

        // 战前准备阶段状态（与 RunBattleWithExpedition 的初始化一致）
        SetField(_ui, "_allRows", new List<RectTransform> { _front, _middle, _back, _eFront, _eMiddle, _eBack });
        var rowCards = new Dictionary<RectTransform, List<GameObject>>();
        foreach (var row in new[] { _front, _middle, _back, _eFront, _eMiddle, _eBack })
            rowCards[row] = new List<GameObject>();
        SetField(_ui, "_rowCards", rowCards);
        SetField(_ui, "_pendingRows", NewDict("BattleRow"));
        SetField(_ui, "_pendingCols", NewDict("BattleColumn"));
        SetField(_ui, "_selectedChars", NewList("CharacterCardData"));
        SetField(_ui, "_allCharCards", NewList("CharacterCardData"));
        SetField(_ui, "_allCharDatas", NewList("CharacterData"));
        SetField(_ui, "_allCharacters", NewList("CharacterInstance"));
        SetField(_ui, "_allSkills", NewList("SkillData"));
        SetField(_ui, "_enemyMonsters", NewList("MonsterDefinition"));
        SetField(_ui, "_charInstByCardId", NewDict("CharacterInstance"));

        Assert.IsNotNull(GetField(_ui, "unitCardPrefab"), "UnitCard.prefab 未找到：" + UnitCardPrefabPath);
    }

    [TearDown]
    public void TearDown()
    {
        if (_uiGo != null) UnityEngine.Object.DestroyImmediate(_uiGo);
        if (_root != null) UnityEngine.Object.DestroyImmediate(_root);
        _uiGo = null;
        _root = null;
        _ui = null;
        foreach (var so in _createdScriptables)
            if (so != null) UnityEngine.Object.DestroyImmediate(so);
        _createdScriptables.Clear();
        _members.Clear();
    }

    private RectTransform NewRow(string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(_root.transform, false);
        return go.GetComponent<RectTransform>();
    }

    // ==================== 成员工厂 ====================

    private Member NewMember(int index)
    {
        var m = new Member
        {
            Index = index,
            CardId = "ccard_contract_" + index,
            CharacterId = "char_contract_" + index
        };

        var card = NewScriptable("CharacterCardData");
        SetMember(card, "CardId", m.CardId);
        SetMember(card, "CharacterId", m.CharacterId);
        m.Card = card;

        var cd = NewScriptable("CharacterData");
        SetMember(cd, "CharacterId", m.CharacterId);
        SetMember(cd, "DisplayName", "契约成员" + index);
        SetMember(cd, "BaseHP", 100);
        SetMember(cd, "BaseAttack", 10);
        SetMember(cd, "AttackRange", 1);
        SetMember(cd, "AttackCount", 1);
        SetMember(cd, "ActionPoints", 1);
        SetMember(cd, "Faction", "player");
        m.CharData = cd;

        ((IList)GetField(_ui, "_allCharCards")).Add(card);
        ((IList)GetField(_ui, "_allCharDatas")).Add(cd);
        _members.Add(m);
        return m;
    }

    private Member MemberAt(int index)
    {
        while (_members.Count <= index) NewMember(_members.Count);
        return _members[index];
    }

    private bool PlaceMember(Member m, RectTransform row)
    {
        return (bool)Call(_ui, "PlaceUnit", m.Card, m.CharData, row);
    }

    private bool PlaceMembers(int count, RectTransform row, int firstIndex = 0)
    {
        bool all = true;
        for (int i = 0; i < count; i++) all &= PlaceMember(MemberAt(firstIndex + i), row);
        return all;
    }

    // ==================== 状态读取 ====================

    private IDictionary PendingRows => (IDictionary)GetField(_ui, "_pendingRows");
    private IDictionary PendingCols => (IDictionary)GetField(_ui, "_pendingCols");
    private Dictionary<RectTransform, List<GameObject>> RowCards =>
        (Dictionary<RectTransform, List<GameObject>>)GetField(_ui, "_rowCards");

    private List<GameObject> AliveCards(RectTransform row) => RowCards[row].FindAll(c => c != null);

    private static float VisualY(GameObject card) => card.GetComponent<RectTransform>().anchoredPosition.y;

    [Test]
    public void RowPresentation_RefreshKeepsOneTwoThreeUnitPositions()
    {
        Assert.IsTrue(PlaceMembers(1, _front, 0));
        Assert.IsTrue(PlaceMembers(2, _middle, 1));
        Assert.IsTrue(PlaceMembers(3, _back, 3));

        var before = new Dictionary<GameObject, float>();
        foreach (var row in new[] { _front, _middle, _back })
            foreach (var card in AliveCards(row)) before[card] = VisualY(card);

        Call(_ui, "ApplyFormationPresentationToAllRows");
        foreach (var pair in before)
            Assert.AreEqual(pair.Value, VisualY(pair.Key), 0.001f, "初始布局和刷新后必须一致");

        Assert.AreEqual(0f, VisualY(AliveCards(_front)[0]), 0.001f);
        Assert.AreEqual(0f, VisualY(AliveCards(_middle)[0]) + VisualY(AliveCards(_middle)[1]), 0.001f);
        var three = AliveCards(_back);
        Assert.IsTrue(three.Exists(c => Mathf.Abs(VisualY(c)) < 0.001f));
        Assert.IsTrue(three.Exists(c => VisualY(c) > 0f));
        Assert.IsTrue(three.Exists(c => VisualY(c) < 0f));
    }

    [Test]
    public void RowPresentation_VisibleMemberCountRecentersAfterExitAndExpandsAfterJoin()
    {
        Assert.IsTrue(PlaceMembers(2, _front, 0));
        var cards = AliveCards(_front);
        cards[0].SetActive(false); // 表现层模拟失能离场；不写 Gameplay Column。
        Call(_ui, "ApplyFormationPresentation", _front);
        Assert.AreEqual(0f, VisualY(cards[1]), 0.001f);

        cards[0].SetActive(true);
        Call(_ui, "ApplyFormationPresentation", _front);
        Assert.AreEqual(0f, VisualY(cards[0]) + VisualY(cards[1]), 0.001f);
        Assert.AreNotEqual(VisualY(cards[0]), VisualY(cards[1]));
    }

    [Test]
    public void ActiveRowPresentation_MoveAndSwapResolveWholeAffectedRows()
    {
        Assert.IsTrue(PlaceMembers(2, _front, 0));
        Assert.IsTrue(PlaceMembers(2, _middle, 2));

        var config = ScriptableObject.CreateInstance(T("InjuryProbabilityConfig"));
        _createdScriptables.Add(config);
        var instances = NewList("CharacterInstance");
        for (int i = 0; i < 4; i++) instances.Add(NewCharInstance(MemberAt(i).CharacterId));
        var battleSystem = Activator.CreateInstance(T("BattleSystem"), instances, null, config);
        Call(battleSystem, "InitializeBattle", NewList("CharacterCardData"), NewList("MonsterDefinition"),
            NewList("SkillData"), NewList("SkillData"));
        for (int i = 0; i < 4; i++)
        {
            var member = MemberAt(i);
            Assert.IsTrue((bool)Call(battleSystem, "DeployCharacter", member.Card,
                EnumVal("BattleRow", i < 2 ? 0 : 1), PendingCols[member.CardId], instances[i], member.CharData,
                NewList("SkillData")));
        }
        SetField(_ui, "_battleSystem", battleSystem);
        Call(_ui, "RepositionAllUnitCards");

        var data = Call(battleSystem, "GetBattleData");
        var units = (IList)GetField(data, "PlayerUnits");
        var field = GetField(data, "PlayerField");
        Assert.IsTrue((bool)Call(field, "TryMoveUnit", units[0], EnumVal("BattleRow", 2), EnumVal("BattleColumn", 1)));
        Call(_ui, "RepositionAllUnitCards");
        Assert.AreEqual(0f, VisualY(AliveCards(_front)[0]), 0.001f, "Move 后源排一人居中");
        Assert.AreEqual(0f, VisualY(AliveCards(_back)[0]), 0.001f, "Move 后目标排一人居中");
        Assert.AreEqual(0f, VisualY(AliveCards(_middle)[0]) + VisualY(AliveCards(_middle)[1]), 0.001f);

        Assert.IsTrue((bool)Call(field, "TrySwapUnits", units[1], units[2]));
        Call(_ui, "RepositionAllUnitCards");
        Assert.AreEqual(0f, VisualY(AliveCards(_front)[0]), 0.001f, "Swap 后源排仍按人数布局");
        Assert.AreEqual(0f, VisualY(AliveCards(_middle)[0]) + VisualY(AliveCards(_middle)[1]), 0.001f,
            "Swap 后目标排仍对称");
    }

    private HashSet<int> ColumnIndexesOf(RectTransform row)
    {
        var set = new HashSet<int>();
        foreach (var card in AliveCards(row))
        {
            string cid = card.name.Substring("Card_".Length);
            Assert.IsTrue(PendingCols.Contains(cid), "单位卡缺少逻辑列：" + cid);
            set.Add(ColIndex(PendingCols[cid]));
        }
        return set;
    }

    private int PendingCountInRow(int rowIndex)
    {
        int count = 0;
        foreach (DictionaryEntry e in PendingRows)
            if (RowIndex(e.Value) == rowIndex) count++;
        return count;
    }

    /// <summary>一排的完整合法性断言：卡片数 = Pending 成员数、同排列唯一、卡与成员一一对应。</summary>
    private void AssertRowLegal(RectTransform row, int rowIndex, int expectedCount)
    {
        var cards = AliveCards(row);
        Assert.AreEqual(expectedCount, cards.Count, row.name + " 视觉卡数量应为 " + expectedCount);
        Assert.AreEqual(expectedCount, PendingCountInRow(rowIndex),
            row.name + " Pending 成员数量必须与视觉卡一致");

        var members = new HashSet<string>();
        foreach (var card in cards)
        {
            string cid = card.name.Substring("Card_".Length);
            Assert.IsTrue(members.Add(cid), "同一排出现重复成员卡：" + cid);
            Assert.IsTrue(PendingRows.Contains(cid), "单位卡没有对应 Pending 成员：" + cid);
            Assert.AreEqual(rowIndex, RowIndex(PendingRows[cid]), "单位卡所在排与 Pending 排不一致：" + cid);
        }
        Assert.AreEqual(expectedCount, ColumnIndexesOf(row).Count, row.name + " 同一排 Column 必须唯一");
    }

    private bool ValidatePendingFormation(out string reason)
    {
        object outVal;
        bool ok = (bool)CallWithOut(_ui, "TryValidatePendingFormation", out outVal);
        reason = outVal as string;
        return ok;
    }

    // ==================== 1. 纯规则：排内列 / 每排容量 / 总数上限 ====================

    [Test]
    public void FormationPlan_ColumnLayout_MatchesVisualOneTwoThreeRule()
    {
        Assert.AreEqual(1, (int)CallStatic("BattleFormationPlan", "ColumnIndexForSlot", 0, 1), "1 人排必须居中（Center）");
        Assert.AreEqual(0, (int)CallStatic("BattleFormationPlan", "ColumnIndexForSlot", 0, 2), "2 人排第一个 = Left");
        Assert.AreEqual(2, (int)CallStatic("BattleFormationPlan", "ColumnIndexForSlot", 1, 2), "2 人排第二个 = Right");
        Assert.AreEqual(0, (int)CallStatic("BattleFormationPlan", "ColumnIndexForSlot", 0, 3), "3 人排 = Left");
        Assert.AreEqual(1, (int)CallStatic("BattleFormationPlan", "ColumnIndexForSlot", 1, 3), "3 人排 = Center");
        Assert.AreEqual(2, (int)CallStatic("BattleFormationPlan", "ColumnIndexForSlot", 2, 3), "3 人排 = Right");
    }

    [Test]
    public void FormationPlan_NeverProducesAFourthColumn()
    {
        Assert.AreEqual(3, (int)CallStatic("BattleFormationPlan", "SlotCount", 8), "一排视觉槽位上限必须仍是 3");

        for (int slot = 0; slot < 8; slot++)
        {
            int col = (int)CallStatic("BattleFormationPlan", "ColumnIndexForSlot", slot, 8);
            Assert.GreaterOrEqual(col, 0, "列索引不得越界（负数）");
            Assert.Less(col, 3, "不得产生第 4 个逻辑列（slot=" + slot + "）");
        }
    }

    [Test]
    public void FormationPlan_RejectsFourthUnitInSameRow()
    {
        Assert.IsFalse((bool)CallStatic("BattleFormationPlan", "CanPlace", 3, false, 3),
            "一排已有 3 人时第 4 人必须被拒绝");
        Assert.IsTrue((bool)CallStatic("BattleFormationPlan", "CanPlace", 3, false, 2), "一排 2 人时允许第 3 人");
    }

    [Test]
    public void FormationPlan_EnforcesSixUnitTeamLimit()
    {
        Assert.IsTrue((bool)CallStatic("BattleFormationPlan", "CanPlace", 5, false, 0), "第 6 人允许上阵（R4.1）");
        Assert.IsFalse((bool)CallStatic("BattleFormationPlan", "CanPlace", 6, false, 0), "第 7 人必须被拒绝（R4.1 上限 6）");
    }

    [Test]
    public void FormationPlan_MoveDoesNotIncreaseTeamTotal()
    {
        Assert.IsTrue((bool)CallStatic("BattleFormationPlan", "CanPlace", 6, true, 2),
            "已上阵成员换排属于移动，不得被 6 人上限误拦");
        Assert.IsFalse((bool)CallStatic("BattleFormationPlan", "CanPlace", 6, true, 3),
            "移动目标排已满 3 人时仍必须被拒绝");
    }

    [Test]
    public void FormationPlan_ValidateSnapshot_AcceptsLegalFormation()
    {
        // Front 1 + Middle 2 + Back 3（每排 ≤ 3、同排列唯一、总数 ≤ 6）
        var rows = new[] { 0, 1, 1, 2, 2, 2 };
        var cols = new[] { 1, 0, 2, 0, 1, 2 };
        Assert.IsNull(CallStatic("BattleFormationPlan", "ValidateSnapshot", rows, cols), "合法阵型不得报错");
    }

    [Test]
    public void FormationPlan_ValidateSnapshot_ReportsAllIllegalCases()
    {
        // 每排 4 人
        Assert.IsNotNull(CallStatic("BattleFormationPlan", "ValidateSnapshot",
            new[] { 0, 0, 0, 0 }, new[] { 0, 1, 2, 0 }), "一排 4 人必须报错");

        // 同排重复列
        Assert.IsNotNull(CallStatic("BattleFormationPlan", "ValidateSnapshot",
            new[] { 0, 0 }, new[] { 1, 1 }), "同排重复列必须报错");

        // 总数 7
        Assert.IsNotNull(CallStatic("BattleFormationPlan", "ValidateSnapshot",
            new[] { 0, 0, 1, 1, 2, 2, 2 }, new[] { 0, 1, 0, 1, 0, 1, 2 }), "总数 7 必须报错");

        // 空阵型
        Assert.AreEqual("请至少上阵一名角色或英雄",
            (string)CallStatic("BattleFormationPlan", "ValidateSnapshot", new int[0], new int[0]),
            "空阵型必须给出明确的玩家反馈");
    }

    // ==================== 2. 战前准备：一排第 4 人被拒绝（零写入） ====================

    private int CountAllRowCards()
    {
        int count = 0;
        foreach (var kv in RowCards)
            foreach (var card in kv.Value)
                if (card != null) count++;
        return count;
    }

    [Test]
    public void PrepHand_FourthMemberInFullRow_IsRejectedWithZeroWrite()
    {
        Assert.IsTrue(PlaceMembers(3, _front, 0), "前 3 人应正常上阵");
        AssertRowLegal(_front, 0, 3);

        var colsBefore = new Dictionary<string, int>();
        foreach (DictionaryEntry e in PendingCols) colsBefore[(string)e.Key] = ColIndex(e.Value);
        var yBefore = new Dictionary<string, float>();
        foreach (var card in AliveCards(_front))
            yBefore[card.name] = card.GetComponent<RectTransform>().anchoredPosition.y;

        var fourth = MemberAt(3);
        Assert.IsFalse(PlaceMember(fourth, _front), "一排已有 3 人时第 4 人必须上阵失败");

        AssertRowLegal(_front, 0, 3);
        Assert.AreEqual(3, PendingRows.Count, "失败不得新增 Pending 成员");
        Assert.IsFalse(PendingRows.Contains(fourth.CardId), "失败成员不得进入 Pending");
        Assert.IsFalse(PendingCols.Contains(fourth.CardId), "失败成员不得获得逻辑列");
        Assert.AreEqual(3, CountAllRowCards(), "失败不得创建第 4 个视觉槽");

        foreach (DictionaryEntry e in PendingCols)
            Assert.AreEqual(colsBefore[(string)e.Key], ColIndex(e.Value), "原有 3 人的 Column 不得改变");
        foreach (var card in AliveCards(_front))
            Assert.AreEqual(yBefore[card.name], card.GetComponent<RectTransform>().anchoredPosition.y, 0.001f,
                "原有 3 人的视觉位置不得改变：" + card.name);
    }

    [Test]
    public void PrepHand_HeroIntoFullRow_IsRejected()
    {
        Assert.IsTrue(PlaceMembers(3, _middle, 0), "前 3 人应正常上阵");

        var heroData = NewScriptable("HeroData");
        SetMember(heroData, "HeroId", "hero_contract");
        SetMember(heroData, "DisplayName", "契约英雄");
        SetMember(heroData, "BaseHP", 120);
        SetMember(heroData, "BaseAttack", 12);
        SetMember(heroData, "ActionPoints", 1);
        var heroInstance = Activator.CreateInstance(T("HeroInstance"), "hero_contract", "契约英雄", 120);

        Assert.IsFalse((bool)Call(_ui, "PlaceHero", heroData, heroInstance, _middle), "满排不得上阵英雄");
        AssertRowLegal(_middle, 1, 3);
        Assert.AreEqual(3, PendingRows.Count, "失败不得新增 Pending 成员");

        Assert.IsTrue((bool)Call(_ui, "PlaceHero", heroData, heroInstance, _back), "有空位的排应允许上阵英雄");
        AssertRowLegal(_back, 2, 1);
        Assert.AreEqual(4, PendingRows.Count);
    }

    [Test]
    public void PrepHand_SeventhMember_IsRejected()
    {
        Assert.IsTrue(PlaceMembers(3, _front, 0));
        Assert.IsTrue(PlaceMembers(3, _middle, 3));
        Assert.AreEqual(6, PendingRows.Count, "6 人为 R4.1 上限阵容");

        Assert.IsFalse(PlaceMember(MemberAt(6), _back), "第 7 名成员必须被拒绝（R4.1）");
        Assert.AreEqual(6, PendingRows.Count, "拒绝不得改变成员总数");
        AssertRowLegal(_back, 2, 0);
    }

    // ==================== 3. 开战边界：最终预检 ====================

    [Test]
    public void PendingFormation_LegalFormation_PassesFinalPrecheck()
    {
        Assert.IsTrue(PlaceMembers(3, _front, 0));
        Assert.IsTrue(PlaceMembers(3, _middle, 3));

        string reason;
        Assert.IsTrue(ValidatePendingFormation(out reason), "合法阵型必须通过开战前最终预检：" + reason);
    }

    [Test]
    public void PendingFormation_VisualMemberMismatch_IsRejected()
    {
        Assert.IsTrue(PlaceMembers(3, _front, 0));

        // 人为制造「UI 卡 ≠ Pending 成员」：新增一个没有视觉卡的 Pending 成员（合法排 / 合法列）
        var ghost = MemberAt(5);
        PendingRows[ghost.CardId] = EnumVal("BattleRow", 2);
        PendingCols[ghost.CardId] = EnumVal("BattleColumn", 1);

        string reason;
        Assert.IsFalse(ValidatePendingFormation(out reason), "视觉卡与 Pending 成员不一致必须被拒绝");
        Assert.IsNotNull(reason, "必须给出明确原因");
    }

    [Test]
    public void PendingFormation_MemberWithoutColumn_IsRejected()
    {
        Assert.IsTrue(PlaceMembers(2, _front, 0));
        PendingCols.Remove(MemberAt(0).CardId);

        string reason;
        Assert.IsFalse(ValidatePendingFormation(out reason), "成员缺少列信息必须被拒绝");
        Assert.IsNotNull(reason, "必须给出明确原因");
    }

    [Test]
    public void OpenBattleBoundary_NoDeployedMember_DoesNotStartBattle()
    {
        Call(_ui, "OnStartBattle");
        Assert.IsFalse((bool)GetProperty(_ui, "IsBattleStarted"), "0 人上阵不得进入战斗");
    }

    [Test]
    public void OpenBattleBoundary_IllegalFormation_DoesNotStartBattleOrHidePrepUi()
    {
        Assert.IsTrue(PlaceMembers(3, _front, 0));

        // 人为制造非法 Pending（第 4 人进同一排）
        var extra = MemberAt(4);
        PendingRows[extra.CardId] = EnumVal("BattleRow", 0);
        PendingCols[extra.CardId] = EnumVal("BattleColumn", 0);

        var prepCard = new GameObject("PrepCard_" + extra.CardId, typeof(RectTransform), typeof(Image));
        prepCard.transform.SetParent(_root.transform, false);
        var startGo = new GameObject("BtnStartBattle", typeof(RectTransform), typeof(Image), typeof(Button));
        startGo.transform.SetParent(_root.transform, false);
        var endGo = new GameObject("BtnEndTurn", typeof(RectTransform), typeof(Image), typeof(Button));
        endGo.transform.SetParent(_root.transform, false);
        SetField(_ui, "btnStartBattle", startGo.GetComponent<Button>());
        SetField(_ui, "btnEndTurn", endGo.GetComponent<Button>());

        Call(_ui, "OnStartBattle");

        Assert.IsFalse((bool)GetProperty(_ui, "IsBattleStarted"),
            "非法阵型不得把 _battleStarted 置 true（不得部分启动 Battle）");
        Assert.IsTrue(prepCard.activeSelf, "预检失败时准备 UI 不得被隐藏");
        Assert.IsTrue(startGo.activeSelf, "预检失败时「开始战斗」入口必须仍可用");
    }

    // ==================== 4. 已上阵单位跨排拖拽 ====================

    [Test]
    public void DeployedUnit_MoveToFullRow_IsRejectedAndSourceRowRestoredAtomically()
    {
        Assert.IsTrue(PlaceMembers(3, _front, 0), "Front 满排");
        Assert.IsTrue(PlaceMembers(3, _middle, 3), "Middle 满排");

        var moving = MemberAt(3);
        int colBefore = ColIndex(PendingCols[moving.CardId]);
        AssertRowLegal(_front, 0, 3);
        AssertRowLegal(_middle, 1, 3);

        Call(_ui, "OnUnitDragBegin", moving.CardId);            // 拖拽开始：卡离开源排
        bool moved = (bool)Call(_ui, "TryReassignUnitCardToRow", moving.CardId, _front, 0f);

        Assert.IsFalse(moved, "目标排已满 3 人时移动必须被拒绝");
        Assert.AreEqual(6, PendingRows.Count, "拒绝移动不得改变成员总数");
        Assert.AreEqual(1, RowIndex(PendingRows[moving.CardId]), "被拒绝的成员必须恢复到源排");
        Assert.AreEqual(colBefore, ColIndex(PendingCols[moving.CardId]), "被拒绝的成员必须恢复原 Column");

        AssertRowLegal(_middle, 1, 3);
        AssertRowLegal(_front, 0, 3);
        Assert.AreEqual(6, CountAllRowCards(), "不得丢失任何卡、也不得产生重复卡");

        var card = AliveCards(_middle).Find(c => c.name == "Card_" + moving.CardId);
        Assert.IsNotNull(card, "被拒绝的移动不得丢失卡");
        Assert.AreSame(_middle, card.transform.parent.GetComponent<RectTransform>(), "卡必须回到源排");
    }

    [Test]
    public void DeployedUnit_MoveToRowWithSpace_IsAccepted()
    {
        Assert.IsTrue(PlaceMembers(3, _front, 0));
        Assert.IsTrue(PlaceMembers(2, _middle, 3));

        var moving = MemberAt(3);
        Call(_ui, "OnUnitDragBegin", moving.CardId);
        bool moved = (bool)Call(_ui, "TryReassignUnitCardToRow", moving.CardId, _back, 0f);

        Assert.IsTrue(moved, "目标排有空位时移动必须成功");
        Assert.AreEqual(2, RowIndex(PendingRows[moving.CardId]), "成员 Pending 排必须更新为 Back");
        Assert.IsTrue(AliveCards(_back).Exists(c => c.name == "Card_" + moving.CardId), "卡必须进入目标排");
        Assert.IsFalse(AliveCards(_middle).Exists(c => c.name == "Card_" + moving.CardId), "卡必须离开源排");

        AssertRowLegal(_front, 0, 3);
        AssertRowLegal(_middle, 1, 1);
        AssertRowLegal(_back, 2, 1);
        Assert.AreEqual(5, PendingRows.Count, "移动不得改变成员总数");
        Assert.AreEqual(0f, VisualY(AliveCards(_middle)[0]), 0.001f, "源排由两人变一人后须居中");
        Assert.AreEqual(0f, VisualY(AliveCards(_back)[0]), 0.001f, "目标排一人须居中");
    }

    // ==================== 5. 准备卡表现：失败不得先永久隐藏 ====================

    private object NewPrepCard(string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(_root.transform, false);
        var prep = go.AddComponent(T("BattlePrepCard"));
        Assert.IsNotNull(prep, "BattlePrepCard 未找到");

        // EditMode 下 AddComponent 不触发 Awake：显式补齐运行时引用（与 Awake 行为一致）
        SetField(prep, "_rt", go.GetComponent<RectTransform>());
        Call(prep, "Init", _ui);
        return prep;
    }

    [Test]
    public void PrepCard_FailedDrop_StaysVisibleAndUsable()
    {
        var prep = NewPrepCard("PrepCard_contract_fail");
        var go = (Component)prep;
        var rt = go.GetComponent<RectTransform>();
        var handPosition = rt.anchoredPosition;              // Init 记录的手牌原位置
        rt.anchoredPosition = new Vector2(500f, 500f);       // 模拟被拖到满排上方

        Call(prep, "ApplyDropResult", false);

        Assert.IsTrue(go.gameObject.activeSelf, "上阵失败时准备卡必须保持可见（可继续拖拽）");
        Assert.AreEqual(handPosition, rt.anchoredPosition, "失败时必须回到原手牌位置");
    }

    [Test]
    public void PrepCard_SuccessfulDrop_IsHidden()
    {
        var prep = NewPrepCard("PrepCard_contract_ok");
        var go = (Component)prep;

        Call(prep, "ApplyDropResult", true);

        Assert.IsFalse(go.gameObject.activeSelf, "上阵成功后准备卡必须隐藏（该成员已进入战场）");
    }

    // Authoring regression: shared count layout and formation are Presentation authorities.
    private List<RectTransform> VisiblePrepCards()
    {
        var cards = new List<RectTransform>();
        foreach (Transform child in _root.transform)
            if (child.gameObject.activeSelf && (child.name.StartsWith("PrepCard_") || child.name.StartsWith("HeroPrepCard_")))
                cards.Add(child.GetComponent<RectTransform>());
        return cards;
    }

    private void AssertPrepCentered(int count)
    {
        var cards = VisiblePrepCards();
        Assert.AreEqual(count, cards.Count);
        var center = (Vector2)GetField(_ui, "prepHandCenterOffset");
        float sum = 0f;
        cards.Sort((a, b) => a.anchoredPosition.x.CompareTo(b.anchoredPosition.x));
        for (int i = 0; i < cards.Count; i++)
        {
            sum += cards[i].anchoredPosition.x;
            Assert.AreEqual(center.y, cards[i].anchoredPosition.y, 0.001f);
            Assert.AreEqual(center.x * 2f, cards[i].anchoredPosition.x + cards[count - 1 - i].anchoredPosition.x, 0.001f);
        }
        if (count > 0) Assert.AreEqual(center.x, sum / count, 0.001f);
    }

    [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(6)]
    public void Authoring_PrepVisibleCountAlwaysCentersAndRearrangeIsStable(int count)
    {
        SetField(_ui, "prepHandCenterOffset", new Vector2(25f, -380f));
        SetField(_ui, "prepCardScale", 1.2f);
        for (int i = 0; i < count; i++) NewPrepCard("PrepCard_layout_" + i);
        Call(_ui, "RearrangeHandCards");
        AssertPrepCentered(count);
        var before = new List<Vector2>();
        foreach (var card in VisiblePrepCards()) before.Add(card.anchoredPosition);
        Call(_ui, "RearrangeHandCards");
        var after = VisiblePrepCards();
        for (int i = 0; i < after.Count; i++)
        {
            Assert.AreEqual(before[i], after[i].anchoredPosition);
            Assert.AreEqual(Vector3.one * 1.2f, after[i].localScale, "重复布局不能累积缩放乘数");
        }
        if (count > 1)
        {
            after[0].gameObject.SetActive(false);
            Call(_ui, "RearrangeHandCards");
            AssertPrepCentered(count - 1);
            after[0].gameObject.SetActive(true);
            Call(_ui, "RearrangeHandCards");
            AssertPrepCentered(count);
        }
    }

    [Test]
    public void Authoring_PrepHeroAndCharactersShareCenter()
    {
        NewPrepCard("HeroPrepCard_layout_hero");
        for (int i = 0; i < 6; i++) NewPrepCard("PrepCard_layout_" + i);
        Call(_ui, "RearrangeHandCards");
        AssertPrepCentered(7);
    }

    [TestCase(1f, false, 9, 1f)]
    [TestCase(1.2f, false, 9, 1.2f)]
    [TestCase(1.2f, true, 6, 1.2f)]
    [TestCase(1.2f, true, 8, 0.96f)]
    public void Authoring_ActionHandScaleUsesBaseAndRelativeMinimum(float basis, bool byCount, int count, float expected)
    {
        float scale = (float)CallStatic("BattleHandVisualLayout", "GetScale", count, basis, byCount, 0.8f);
        Assert.AreEqual(expected, scale, 0.001f);
        Vector2 center = new Vector2(42f, -12f);
        float sum = 0f;
        for (int i = 0; i < count; i++)
            sum += ((Vector2)CallStatic("BattleHandVisualLayout", "GetPosition", count, i, 1520f, 200f * scale, 200f, 70f, center)).x;
        Assert.AreEqual(center.x, sum / count, 0.001f, "基础缩放变化不能漂移手牌中心");
    }

    [TestCase(2)] [TestCase(3)]
    public void Authoring_DragPreviewOutsideRowPreservesReducedCountLayout(int count)
    {
        Assert.IsTrue(PlaceMembers(count, _front));
        SetField(_ui, "rowLocalVisualOffset", new Vector2(17f, 23f));
        Call(_ui, "OnUnitDragBegin", MemberAt(0).CardId);
        var before = new Dictionary<GameObject, Vector2>();
        foreach (var card in AliveCards(_front)) before.Add(card, card.GetComponent<RectTransform>().anchoredPosition);
        Call(_ui, "UpdateDragPreview", new Vector2(-100000f, -100000f));
        foreach (var pair in before) Assert.AreEqual(pair.Value, pair.Key.GetComponent<RectTransform>().anchoredPosition);
        Assert.AreEqual(count - 1, before.Count);
        if (count == 2) Assert.AreEqual(new Vector2(17f, 23f), AliveCards(_front)[0].GetComponent<RectTransform>().anchoredPosition);
        else Assert.AreEqual(46f, VisualY(AliveCards(_front)[0]) + VisualY(AliveCards(_front)[1]), 0.001f);
    }

    [Test]
    public void Authoring_DragRejectedRestoresOriginalFormation()
    {
        Assert.IsTrue(PlaceMembers(3, _front));
        var before = new Dictionary<GameObject, Vector2>();
        foreach (var card in AliveCards(_front)) before.Add(card, card.GetComponent<RectTransform>().anchoredPosition);
        Call(_ui, "OnUnitDragBegin", MemberAt(0).CardId);
        Call(_ui, "UpdateDragPreview", new Vector2(-100000f, -100000f));
        Assert.IsFalse((bool)Call(_ui, "TryReassignUnitCardToRow", MemberAt(0).CardId, _eFront, 0f));
        foreach (var pair in before) Assert.AreEqual(pair.Value, pair.Key.GetComponent<RectTransform>().anchoredPosition);
        AssertRowLegal(_front, 0, 3);
    }

    [TestCase("BattleUI", "prepHandCenterOffset", "handStartPosition")]
    [TestCase("BattleUI", "prepHandBaseStep", "handCardSpacing")]
    [TestCase("DeckViewerUI", "deckViewerCardScale", "cardScale")]
    [TestCase("DeckViewerUI", "deckViewerCardSpacing", "cardSpacing")]
    public void Authoring_RenamedFieldsDeclareSerializedMigration(string type, string field, string oldName)
    {
        var attrs = T(type).GetField(field).GetCustomAttributes(typeof(UnityEngine.Serialization.FormerlySerializedAsAttribute), false);
        Assert.IsTrue(Array.Exists(attrs, a => ((UnityEngine.Serialization.FormerlySerializedAsAttribute)a).oldName == oldName));
    }

    // ==================== 6. Downed 阶段：释放占位但保留 BattleUnit ====================

    [Test]
    public void DownedUnit_ReleasesBoardOccupancyButKeepsBattleUnit()
    {
        var config = ScriptableObject.CreateInstance(T("InjuryProbabilityConfig"));
        _createdScriptables.Add(config);
        var instances = NewList("CharacterInstance");
        var battleSystem = Activator.CreateInstance(T("BattleSystem"), instances, null, config);
        var data = Call(battleSystem, "GetBattleData");
        var playerField = GetField(data, "PlayerField");
        var playerUnits = (IList)GetField(data, "PlayerUnits");

        var charData = (UnityEngine.Object)MemberAt(0).CharData;
        var card = MemberAt(0).Card;

        var ci = NewCharInstance(MemberAt(0).CharacterId);
        var ci2 = NewCharInstance(MemberAt(1).CharacterId);
        instances.Add(ci);
        instances.Add(ci2);

        Call(battleSystem, "InitializeBattle", NewList("CharacterCardData"), NewList("MonsterDefinition"),
            NewList("SkillData"), NewList("SkillData"));

        var row0 = EnumVal("BattleRow", 0);
        var col0 = EnumVal("BattleColumn", 0);
        var row1 = EnumVal("BattleRow", 1);
        var col1 = EnumVal("BattleColumn", 1);

        Assert.IsTrue((bool)Call(battleSystem, "DeployCharacter", card, row0, col0, ci, charData, NewList("SkillData")),
            "合法上阵必须成功");
        Assert.IsTrue((bool)Call(battleSystem, "DeployCharacter", card, row1, col1, ci2, charData, NewList("SkillData")),
            "第二名合法上阵必须成功");
        Assert.AreEqual(2, playerUnits.Count);

        var target = playerUnits[0];
        Assert.IsTrue((bool)Call(battleSystem, "ApplyBattleDamage", target, 999), "致死伤害必须结算成功");

        // Downed 阶段：BattleUnit 仍存在（不是 ghost），但已释放 Board Occupancy
        Assert.AreEqual(2, playerUnits.Count, "0HP 单位必须保留 BattleUnit（不得从参战名单删除）");
        Assert.IsTrue((bool)GetField(target, "IsDown"), "0HP 必须标记为失去战斗能力");
        Assert.AreEqual(0, (int)GetField(target, "CurrentHP"));
        Assert.IsFalse((bool)Call(playerField, "HasAliveUnitInFaction", row0, col0, "player"), "0HP 单位不得继续占格");
        Assert.IsTrue((bool)Call(playerField, "CanDeploy", row0, col0), "释放后的格必须可被新单位进入");
        Assert.AreEqual(0, (int)GetField(target, "Row"), "表现层仍可读取最后逻辑排（表现不构成 Occupancy）");

        // Active 阶段：仍具战斗能力的单位继续合法占格
        var alive = playerUnits[1];
        Assert.AreSame(alive, Call(playerField, "GetUnitAt", row1, col1), "仍具战斗能力单位必须保持占位");
        Assert.IsFalse((bool)Call(playerField, "CanDeploy", row1, col1), "被存活单位占据的格不得可写");
    }

    private object NewCharInstance(string characterId)
    {
        var ci = Activator.CreateInstance(T("CharacterInstance"), characterId, "契约实例-" + characterId, 50, 5);
        SetMember(ci, "CurrentHP", 50);
        return ci;
    }
}
