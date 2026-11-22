using TMPro;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

/// <summary>
/// Battle Formation &amp; Active Roster 一致性 PlayMode 产品路径验证
/// （真实入口：SaveSystem.NewGame → ExpeditionSystem.ConfirmExpedition → BattleUI.InitFromExpedition → 战前布阵 → 开始战斗）。
///
/// 证明的是产品路径真的按不变量运行，而不是模块单独能跑：
///   1. 战前：一排 3 人时第 4 人（英雄）被拒绝，且不产生第 4 视觉槽 / 不写 Pending / 原三人不变；
///   2. 战前：已上阵单位拖向满排被拒绝，源排原子恢复（原 Column / Pending 不变，卡不丢失）；
///   3. 开战边界：非法 Pending Formation 无法进入战斗（_battleStarted 仍 false、准备 UI 不隐藏、不建共享牌库）；
///   4. Active Battle：成功部署的 PlayerUnits = 共享牌库人物集合的事实来源，
///      所有角色牌 owner 都能解析到当前 BattleUnit caster（无 ghost 牌主），
///      每个仍具战斗能力的单位恰有一个合法 Occupancy 且与 Row / Column 一致；
///   5. 部署完整性失败：安全中止本次开战（不缩减阵容继续、不留 ghost UI、不建立错误共享牌库）。
///
/// 说明：BattleUI / BattleSystem / BattleUnit 位于预定义程序集 Assembly-CSharp，与既有 PlayMode 测试一致使用反射访问；
/// BattleCard / SharedDeck 位于 BattleStage1.Runtime，可强类型引用。
/// </summary>
public class BattleFormationRosterConsistencyPlayTests
{
    private const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

    private static System.Type GetType(string name) => System.Type.GetType(name + ", Assembly-CSharp");

    private static object GetDataManager() =>
        GetType("DataManager").GetProperty("Instance", BindingFlags.Public | BindingFlags.Static).GetValue(null);

    private static object FindInstance(string typeName)
    {
        var t = GetType(typeName);
        if (t == null) return null;
        foreach (var o in Object.FindObjectsOfType(t, true))
            if (o != null) return o;
        return null;
    }

    private static object GetField(object o, string name)
    {
        var f = o.GetType().GetField(name, F);
        Assert.IsNotNull(f, "字段不存在：" + name);
        return f.GetValue(o);
    }

    private static object GetProperty(object o, string name)
    {
        var p = o.GetType().GetProperty(name, F);
        Assert.IsNotNull(p, "属性不存在：" + name);
        return p.GetValue(o, null);
    }

    private static object GetMember(object o, string name)
    {
        var f = o.GetType().GetField(name, F);
        if (f != null) return f.GetValue(o);
        var p = o.GetType().GetProperty(name, F);
        Assert.IsNotNull(p, "成员不存在：" + name);
        return p.GetValue(o, null);
    }

    private static object Call(object o, string method, params object[] args)
    {
        var m = o.GetType().GetMethod(method, F);
        Assert.IsNotNull(m, "方法不存在：" + method);
        return m.Invoke(o, args);
    }

    private static object SetField(object o, string name, object value)
    {
        var f = o.GetType().GetField(name, F);
        Assert.IsNotNull(f, "字段不存在：" + name);
        f.SetValue(o, value);
        return value;
    }

    private static object CallStatic(string typeName, string method, params object[] args)
    {
        var m = GetType(typeName).GetMethod(method, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
        Assert.IsNotNull(m, "静态方法不存在：" + typeName + "." + method);
        return m.Invoke(null, args);
    }

    private sealed class Ctx
    {
        public object BattleUI;
        public object DataManager;
        public object Collection;
        public object BattleSystem;
        public IList Chars;
        public object Hero;
    }

    /// <summary>正式产品路径进入战斗（与既有 Battle 产品测试一致的入口，携带最多 3 名角色 + 英雄）。</summary>
    private IEnumerator EnterFormalBattle(Ctx ctx)
    {
        SceneManager.LoadScene("SampleScene");
        yield return null;
        yield return null;

        var battleUI = FindInstance("BattleUI");
        var expUI = FindInstance("ExpeditionUI");
        Assert.IsNotNull(battleUI, "BattleUI（正式宿主）未找到");
        Assert.IsNotNull(expUI, "ExpeditionUI 未找到");
        ctx.BattleUI = battleUI;

        var collection = CallStatic("SaveSystem", "NewGame", null);
        Assert.IsNotNull(collection, "NewGame 失败");
        ctx.Collection = collection;

        var exp = System.Activator.CreateInstance(GetType("ExpeditionSystem"), new object[] { collection });
        var dataManager = GetDataManager();
        ctx.DataManager = dataManager;

        var heroes = (IList)GetMember(collection, "OwnedHeroes");
        if (heroes != null && heroes.Count > 0)
        {
            var hi = heroes[0];
            var hd = Call(dataManager, "GetHeroById", GetMember(hi, "HeroId"));
            if (hd != null) Call(exp, "SelectHero", hi, hd);
        }

        var charCards = (IList)GetMember(collection, "OwnedCharacterCards");
        Assert.IsNotNull(charCards, "角色卡册为空");
        Assert.GreaterOrEqual(charCards.Count, 3, "产品默认角色卡不足 3 张，无法验证「一排 3 人 + 第 4 人」");

        Call(exp, "AddCharacterCard", charCards[0]);
        Call(exp, "AddCharacterCard", charCards[1]);
        Call(exp, "AddCharacterCard", charCards[2]);

        var confirm = GetType("ExpeditionSystem").GetMethod("ConfirmExpedition");
        Assert.IsNotNull(confirm, "ConfirmExpedition 方法未找到");
        var args = new object[2];
        Assert.IsTrue((bool)confirm.Invoke(exp, args), "确认出征失败");
        ctx.Chars = (IList)args[0];
        ctx.Hero = args[1];

        Call(battleUI, "InitFromExpedition", ctx.Chars, ctx.Hero,
            GetProperty(dataManager, "AllCharacters"), GetProperty(dataManager, "AllSkills"),
            StaticMonsterTestData.Build(), null);

        var canvas = GetField(expUI, "battleCanvas");
        if (canvas != null) ((GameObject)canvas).SetActive(true);
        var battleGo = GetProperty(battleUI, "gameObject");
        if (battleGo != null) ((GameObject)battleGo).SetActive(true);
        yield return null;

        Call(battleUI, "StartBattleWithExpedition", null);
        yield return null;
    }

    // ---------- 读取辅助 ----------

    private static RectTransform Row(object battleUI, string fieldName)
    {
        var rt = GetField(battleUI, fieldName) as RectTransform;
        Assert.IsNotNull(rt, "Row 未绑定：" + fieldName);
        return rt;
    }

    private static List<GameObject> CardsInRow(object battleUI, RectTransform row)
    {
        var dict = (System.Collections.IDictionary)GetField(battleUI, "_rowCards");
        var list = new List<GameObject>();
        foreach (DictionaryEntry e in dict)
        {
            if (!ReferenceEquals(e.Key, row)) continue;
            foreach (var o in (IEnumerable)e.Value)
            {
                var go = o as GameObject;
                if (go != null) list.Add(go);
            }
        }
        return list;
    }

    private static int UnitCardsOnPlayerRows(object battleUI)
    {
        int count = 0;
        foreach (var rowField in new[] { "playerFrontRow", "playerMiddleRow", "playerBackRow" })
        {
            foreach (var go in CardsInRow(battleUI, Row(battleUI, rowField)))
                if (!go.name.StartsWith("EnemyPrep")) count++;
        }
        return count;
    }

    private static bool PlaceCharacter(Ctx ctx, int charIndex, string rowField)
    {
        var cardData = ctx.Chars[charIndex];
        var cd = Call(ctx.DataManager, "GetCharacterById", GetMember(cardData, "CharacterId"));
        Assert.IsNotNull(cd, "CharacterData 未找到：" + charIndex);
        return (bool)Call(ctx.BattleUI, "PlaceUnit", cardData, cd, Row(ctx.BattleUI, rowField));
    }

    private static bool PlaceHero(Ctx ctx, string rowField)
    {
        Assert.IsNotNull(ctx.Hero, "本次远征未携带英雄，无法验证英雄上阵守卫");
        var hd = Call(ctx.DataManager, "GetHeroById", GetMember(ctx.Hero, "HeroId"));
        Assert.IsNotNull(hd, "HeroData 未找到");
        return (bool)Call(ctx.BattleUI, "PlaceHero", hd, ctx.Hero, Row(ctx.BattleUI, rowField));
    }

    private static IEnumerator WaitForPlayerTurn(object battleUI, float timeoutSeconds = 20f)
    {
        var v1 = GetField(battleUI, "v1Runtime");
        Assert.IsNotNull(v1, "BattleV1Runtime 未绑定");
        float wait = 0f;
        while (wait < timeoutSeconds)
        {
            var phase = (BattleV1TurnFlow.PhaseType)GetProperty(v1, "TurnPhase");
            var deck = (SharedDeck)GetProperty(v1, "Deck");
            if (phase == BattleV1TurnFlow.PhaseType.PlayerTurn && deck != null) break;
            wait += Time.deltaTime;
            yield return null;
        }
        Assert.IsNotNull((SharedDeck)GetProperty(v1, "Deck"), "共享牌库未建立（本场战斗未真正开始）");
    }

    private static List<BattleCard> AllDeckCards(object battleUI)
    {
        var v1 = GetField(battleUI, "v1Runtime");
        var deck = (SharedDeck)GetProperty(v1, "Deck");
        var all = new List<BattleCard>();
        if (deck == null) return all;
        all.AddRange(deck.Hand);
        all.AddRange(deck.DrawPile);
        all.AddRange(deck.DiscardPile);
        all.AddRange(deck.ExhaustPile);
        return all;
    }

    private static bool TryValidatePendingFormation(object battleUI, out string reason)
    {
        var m = battleUI.GetType().GetMethod("TryValidatePendingFormation", F);
        Assert.IsNotNull(m, "方法不存在：TryValidatePendingFormation");
        var args = new object[1];
        bool ok = (bool)m.Invoke(battleUI, args);
        reason = args[0] as string;
        return ok;
    }

    private static bool PrepCardsVisible(object battleUI)
    {
        var ct = (Transform)GetProperty(battleUI, "PrepCardHost");
        Assert.IsNotNull(ct, "准备卡宿主未绑定");
        foreach (Transform child in ct)
        {
            if (child == null) continue;
            if ((child.name.StartsWith("PrepCard_") || child.name.StartsWith("HeroPrepCard_")) && child.gameObject.activeSelf)
                return true;
        }
        return false;
    }

    private static object EnumVal(string typeName, int value) => System.Enum.ToObject(GetType(typeName), value);

    /// <summary>该排的逻辑列必须存在、唯一，且数量与视觉卡一致（Preparation 不变量）。</summary>
    private static void AssertRowColumnsUnique(object battleUI, RectTransform row, int expected)
    {
        var pendingCols = (System.Collections.IDictionary)GetField(battleUI, "_pendingCols");
        var cols = new HashSet<int>();
        int cards = 0;

        foreach (var go in CardsInRow(battleUI, row))
        {
            if (go == null || go.name.StartsWith("EnemyPrep")) continue;
            cards++;
            string cid = go.name.Substring("Card_".Length);
            Assert.IsTrue(pendingCols.Contains(cid), "单位卡缺少逻辑列：" + cid);
            cols.Add(System.Convert.ToInt32(pendingCols[cid]));
        }

        Assert.AreEqual(expected, cards, row.name + " 视觉卡数量不符");
        Assert.AreEqual(expected, cols.Count, row.name + " 同一排 Column 必须唯一");
    }

    // ============ 1. 一排 3 人 + 第 4 人（英雄）被拒绝 → 合法阵型开战 ============

    [UnityTest]
    public IEnumerator FullRow_RejectsFourthMember_ThenLegalFormationDeploysConsistentActiveRoster()
    {
        var ctx = new Ctx();
        yield return EnterFormalBattle(ctx);

        var front = Row(ctx.BattleUI, "playerFrontRow");
        Assert.IsTrue(PlaceCharacter(ctx, 0, "playerFrontRow"), "第 1 名角色应上阵成功");
        Assert.IsTrue(PlaceCharacter(ctx, 1, "playerFrontRow"), "第 2 名角色应上阵成功");
        Assert.IsTrue(PlaceCharacter(ctx, 2, "playerFrontRow"), "第 3 名角色应上阵成功");
        yield return null;

        var pendingRows = (System.Collections.IDictionary)GetField(ctx.BattleUI, "_pendingRows");
        var pendingCols = (System.Collections.IDictionary)GetField(ctx.BattleUI, "_pendingCols");
        Assert.AreEqual(3, CardsInRow(ctx.BattleUI, front).Count, "Front 排应有 3 个单位");
        Assert.AreEqual(3, pendingRows.Count, "战前准备阶段 Pending 成员应为 3");
        Assert.AreEqual(3, pendingCols.Count, "每个 Pending 成员恰有一个 Column");
        AssertRowColumnsUnique(ctx.BattleUI, front, 3);

        int unitsBefore = UnitCardsOnPlayerRows(ctx.BattleUI);

        // 第 4 人（英雄）拖入满排 → 必须被拒绝（Prep Card 侧由 ApplyDropResult 保证不隐藏）
        Assert.IsFalse(PlaceHero(ctx, "playerFrontRow"), "一排已有 3 人时第 4 人必须上阵失败");
        yield return null;

        Assert.AreEqual(3, CardsInRow(ctx.BattleUI, front).Count, "失败不得创建第 4 个视觉槽");
        Assert.AreEqual(unitsBefore, UnitCardsOnPlayerRows(ctx.BattleUI), "失败不得新增任何单位卡");
        Assert.AreEqual(3, pendingRows.Count, "失败不得新增 Pending 成员");
        Assert.AreEqual(3, pendingCols.Count, "失败不得新增逻辑列");
        AssertRowColumnsUnique(ctx.BattleUI, front, 3);

        // 合法阵型：通过开战前最终预检
        string reason;
        Assert.IsTrue(TryValidatePendingFormation(ctx.BattleUI, out reason), "合法阵型必须通过最终预检：" + reason);

        Call(ctx.BattleUI, "OnStartBattle");
        Assert.IsTrue((bool)GetProperty(ctx.BattleUI, "IsBattleStarted"), "合法阵型必须进入战斗");
        yield return WaitForPlayerTurn(ctx.BattleUI);

        ctx.BattleSystem = GetField(ctx.BattleUI, "_battleSystem");
        Assert.IsNotNull(ctx.BattleSystem, "BattleSystem 未建立（OnStartBattle 后）");
        var data = Call(ctx.BattleSystem, "GetBattleData");
        var playerField = GetField(data, "PlayerField");
        var playerUnits = (IList)GetField(data, "PlayerUnits");

        // Active Battle Roster = 成功部署的真实阵容（不得静默少部署）
        Assert.AreEqual(3, playerUnits.Count, "PlayerUnits 数量必须与实际阵型一致");

        var rosterPersonIds = new HashSet<string>();
        var occupied = new HashSet<string>();
        foreach (var unit in playerUnits)
        {
            Assert.IsFalse((bool)GetField(unit, "IsDown"), "刚开战不应存在失能单位");
            int urow = (int)GetField(unit, "Row");
            int ucol = (int)GetField(unit, "Column");
            Assert.AreSame(unit,
                Call(playerField, "GetUnitAt", EnumVal("BattleRow", urow), EnumVal("BattleColumn", ucol)),
                "每个仍具战斗能力的 PlayerUnit 必须在 Player Board 恰有一个合法 Occupancy");
            Assert.IsTrue(occupied.Add(urow + ":" + ucol), "同格不得有两个仍具战斗能力的单位");

            string pid = (string)Call(ctx.BattleUI, "GetPersonIdOfUnit", unit);
            Assert.IsFalse(string.IsNullOrEmpty(pid), "每个参战单位都必须能解析出人物身份");
            rosterPersonIds.Add(pid);
        }
        Assert.AreEqual(playerUnits.Count, rosterPersonIds.Count, "参战人物身份必须唯一");

        // 共享牌库人物集合 = Active Battle Roster；角色牌 owner 必须能解析到 caster（无 ghost）
        var deckCards = AllDeckCards(ctx.BattleUI);
        Assert.Greater(deckCards.Count, 0, "本场共享牌库不得为空");

        var characterOwners = new HashSet<string>();
        foreach (var card in deckCards)
        {
            if (card.CardKind != BattleCardKind.Character) continue;   // 通用牌无牌主（AC-09）
            Assert.IsFalse(string.IsNullOrEmpty(card.OwnerPersonId), "角色牌必须有具体人物归属");
            characterOwners.Add(card.OwnerPersonId);
            Assert.IsNotNull(Call(ctx.BattleUI, "GetV1CardCaster", card),
                "角色牌 owner 必须能解析到本场 BattleUnit caster（禁止 ghost 牌主）：" + card.CardId);
        }
        Assert.IsTrue(rosterPersonIds.SetEquals(characterOwners),
            "共享牌库角色牌 owner 集合必须等于成功部署的 Active Battle Roster");
    }

    // ============ 2. 已上阵单位拖向满排被拒绝，源排原子恢复 ============

    [UnityTest]
    public IEnumerator MoveDeployedUnitIntoFullRow_IsRejectedAndSourceRowRestored()
    {
        var ctx = new Ctx();
        yield return EnterFormalBattle(ctx);

        var back = Row(ctx.BattleUI, "playerBackRow");
        var front = Row(ctx.BattleUI, "playerFrontRow");

        Assert.IsTrue(PlaceCharacter(ctx, 0, "playerBackRow"));
        Assert.IsTrue(PlaceCharacter(ctx, 1, "playerBackRow"));
        Assert.IsTrue(PlaceCharacter(ctx, 2, "playerBackRow"));
        Assert.IsTrue(PlaceHero(ctx, "playerFrontRow"), "英雄应上阵到有空位的排");
        yield return null;

        var pendingRows = (System.Collections.IDictionary)GetField(ctx.BattleUI, "_pendingRows");
        var pendingCols = (System.Collections.IDictionary)GetField(ctx.BattleUI, "_pendingCols");
        Assert.AreEqual(4, pendingRows.Count);

        string heroId = (string)GetMember(ctx.Hero, "HeroId");
        int colBefore = System.Convert.ToInt32(pendingCols[heroId]);
        int unitsBefore = UnitCardsOnPlayerRows(ctx.BattleUI);

        // 拖拽开始（卡已离开源排）→ 拖向满排 → 必须拒绝并原子恢复源排
        Call(ctx.BattleUI, "OnUnitDragBegin", heroId);
        bool moved = (bool)Call(ctx.BattleUI, "TryReassignUnitCardToRow", heroId, back, 0f);
        yield return null;

        Assert.IsFalse(moved, "目标排已满 3 人时移动必须被拒绝");
        Assert.AreEqual(4, pendingRows.Count, "拒绝移动不得改变成员总数");
        Assert.AreEqual(0, System.Convert.ToInt32(pendingRows[heroId]), "被拒绝的成员必须恢复到源排（Front）");
        Assert.AreEqual(colBefore, System.Convert.ToInt32(pendingCols[heroId]), "被拒绝的成员必须恢复原 Column");
        Assert.AreEqual(unitsBefore, UnitCardsOnPlayerRows(ctx.BattleUI), "拒绝移动不得丢失或新增卡");

        AssertRowColumnsUnique(ctx.BattleUI, back, 3);
        AssertRowColumnsUnique(ctx.BattleUI, front, 1);
    }

    // ============ 3. 非法 Pending Formation 无法进入战斗 ============

    [UnityTest]
    public IEnumerator IllegalPendingFormation_CannotStartBattleOrHidePrepUi()
    {
        var ctx = new Ctx();
        yield return EnterFormalBattle(ctx);

        Assert.IsTrue(PlaceCharacter(ctx, 0, "playerFrontRow"));
        Assert.IsTrue(PlaceCharacter(ctx, 1, "playerFrontRow"));
        Assert.IsTrue(PlaceCharacter(ctx, 2, "playerFrontRow"));
        yield return null;

        // 人为制造非法 Pending（第 4 人进同一排）
        var pendingRows = (System.Collections.IDictionary)GetField(ctx.BattleUI, "_pendingRows");
        var pendingCols = (System.Collections.IDictionary)GetField(ctx.BattleUI, "_pendingCols");
        pendingRows["ccard_illegal_fourth"] = EnumVal("BattleRow", 0);
        pendingCols["ccard_illegal_fourth"] = EnumVal("BattleColumn", 1);

        var v1 = GetField(ctx.BattleUI, "v1Runtime");
        Assert.IsNull(GetProperty(v1, "Deck"), "战前准备阶段不应存在共享牌库");

        Call(ctx.BattleUI, "OnStartBattle");
        yield return null;
        yield return null;

        Assert.IsFalse((bool)GetProperty(ctx.BattleUI, "IsBattleStarted"),
            "非法 Pending Formation 不得把 _battleStarted 置 true");
        Assert.IsNull(GetProperty(v1, "Deck"), "非法阵型不得建立共享牌库（ExecuteBattle 不得进入）");
        Assert.IsNull(GetField(ctx.BattleUI, "_battleSystem"), "非法阵型不得建立 BattleSystem");
        Assert.IsTrue(PrepCardsVisible(ctx.BattleUI), "预检失败时必须保持战前准备 UI");

        var startBtn = GetField(ctx.BattleUI, "btnStartBattle");
        Assert.IsNotNull(startBtn, "「开始战斗」按钮未绑定");
        Assert.IsTrue(((Component)startBtn).gameObject.activeSelf, "预检失败时「开始战斗」入口必须仍可用");
    }

    [UnityTest]
    public IEnumerator Authoring_PrepReturnThreeToTwoToOneRecentersImmediatelyBeforeStart()
    {
        var ctx = new Ctx();
        yield return EnterFormalBattle(ctx);
        var ui = ctx.BattleUI;
        var front = Row(ui, "playerFrontRow");
        AssertAuthoringPrepCenter(ui);
        for (int i = 0; i < 3; i++) Assert.IsTrue(PlaceCharacter(ctx, i, "playerFrontRow"));
        for (int i = 0; i < 2; i++)
        {
            string id = (string)GetMember(ctx.Chars[i], "CardId");
            Call(ui, "OnUnitDragBegin", id);
            Call(ui, "UpdateDragPreview", new Vector2(-100000f, -100000f));
            AssertAuthoringRow(ui, front, 2 - i);
            Call(ui, "CompleteUnitDrag", id, new Vector2(-100000f, -100000f));
            AssertAuthoringRow(ui, front, 2 - i);
            Assert.IsFalse((bool)GetProperty(ui, "IsBattleStarted"));
            AssertAuthoringPrepCenter(ui);
            yield return null;
            AssertAuthoringRow(ui, front, 2 - i);
        }
        string remainingName = CardsInRow(ui, front)[0].name;
        var before = CardsInRow(ui, front)[0].GetComponent<RectTransform>().anchoredPosition;
        Call(ui, "OnStartBattle");
        yield return WaitForPlayerTurn(ui);
        var remaining = CardsInRow(ui, front).Find(c => c.name == remainingName);
        Assert.IsNotNull(remaining);
        Assert.AreEqual(before, remaining.GetComponent<RectTransform>().anchoredPosition);
    }

    private static void AssertAuthoringRow(object ui, RectTransform row, int count)
    {
        var cards = CardsInRow(ui, row);
        Assert.AreEqual(count, cards.Count);
        var positions = new List<float>();
        var offset = (Vector2)GetField(ui, "rowLocalVisualOffset");
        foreach (var card in cards)
        {
            var rt = card.GetComponent<RectTransform>();
            Assert.AreEqual(offset.x, rt.anchoredPosition.x, 0.001f);
            positions.Add(rt.anchoredPosition.y);
        }
        positions.Sort();
        if (count == 1) Assert.AreEqual(offset.y + (float)GetField(ui, "singleUnitVisualOffset"), positions[0], 0.001f);
        if (count == 2)
        {
            float half = (float)GetField(ui, "twoUnitVisualSpacing") * 0.5f;
            Assert.AreEqual(offset.y - half, positions[0], 0.001f);
            Assert.AreEqual(offset.y + half, positions[1], 0.001f);
        }
    }

    private static void AssertAuthoringPrepCenter(object ui)
    {
        var host = (RectTransform)GetProperty(ui, "PrepCardHost");
        Assert.IsNotNull(host, "准备卡宿主未绑定");
        var center = (Vector2)GetField(ui, "prepHandCenterOffset");
        Vector2 sum = Vector2.zero;
        int count = 0;
        foreach (Transform child in host)
            if (child.gameObject.activeSelf && (child.name.StartsWith("PrepCard_") || child.name.StartsWith("HeroPrepCard_")))
            {
                sum += child.GetComponent<RectTransform>().anchoredPosition;
                count++;
            }
        Assert.Greater(count, 0);
        Assert.Less((sum / count - center).magnitude, 0.01f);
    }

    [UnityTest]
    public IEnumerator Authoring_ActionCardsUseBaseScaleAndCountRatioOnRealHand()
    {
        var ctx = new Ctx();
        yield return EnterFormalBattle(ctx);
        Assert.IsTrue(PlaceCharacter(ctx, 0, "playerFrontRow"));
        var v1 = GetField(ctx.BattleUI, "v1Runtime");
        Call(ctx.BattleUI, "OnStartBattle");
        yield return WaitForPlayerTurn(ctx.BattleUI);
        foreach (float basis in new[] { 1f, 1.2f })
        {
            SetField(v1, "battleCardBaseScale", basis);
            SetField(v1, "handScaleByCount", false);
            Call(v1, "RefreshHand");
            yield return null;
            AssertAuthoringActionHand(v1, basis);
        }
        var deck = (SharedDeck)GetProperty(v1, "Deck");
        Assert.Greater(deck.Hand.Count, 0);
        while (deck.Hand.Count < 8) deck.Hand.Add(deck.Hand[0]); // 纯表现数量夹具，不出牌。
        SetField(v1, "handScaleByCount", true);
        SetField(v1, "handMinimumScale", 0.8f);
        Call(v1, "RefreshHand");
        yield return null;
        AssertAuthoringActionHand(v1, 0.96f);
    }

    private static void AssertAuthoringActionHand(object v1, float scale)
    {
        var root = (RectTransform)GetField(v1, "handContainer");
        var center = (Vector2)GetField(v1, "handCenterOffset");
        Vector2 sum = Vector2.zero;
        int count = 0;
        foreach (Transform child in root)
            if (child.name.StartsWith("V1Card_"))
            {
                var rt = child.GetComponent<RectTransform>();
                Assert.Less((rt.localScale - Vector3.one * scale).magnitude, 0.001f);
                sum += rt.anchoredPosition;
                count++;
            }
        Assert.Greater(count, 0);
        Assert.Less((sum / count - center).magnitude, 0.01f);
    }

    // ============ 4. 部署完整性失败 → 安全中止本次开战 ============

    [UnityTest]
    public IEnumerator DeployIntegrityFailure_AbortsBattleStartWithoutGhostUiOrWrongDeck()
    {
        var ctx = new Ctx();
        yield return EnterFormalBattle(ctx);

        Assert.IsTrue(PlaceCharacter(ctx, 0, "playerFrontRow"));
        Assert.IsTrue(PlaceCharacter(ctx, 1, "playerFrontRow"));
        yield return null;

        // 人为制造部署完整性错误：把 BattleUI 的角色模板表替换为一份独立空列表 → ExecuteBattle 无法为已布阵成员解析 CharacterData。
        // ⚠️ 必须"替换字段"，不能就地 Clear()：InitFromExpedition 是按引用赋值 DataManager.AllCharacters，
        //    就地清空会污染同一次 PlayMode 会话中后续所有产品测试（测试隔离）。
        var charDatas = (IList)GetField(ctx.BattleUI, "_allCharDatas");
        Assert.Greater(charDatas.Count, 0, "前置条件：角色模板表非空");
        SetField(ctx.BattleUI, "_allCharDatas",
            System.Activator.CreateInstance(typeof(List<>).MakeGenericType(GetType("CharacterData"))));

        int unitsBefore = UnitCardsOnPlayerRows(ctx.BattleUI);
        var v1 = GetField(ctx.BattleUI, "v1Runtime");

        Call(ctx.BattleUI, "OnStartBattle");
        yield return null;
        yield return null;
        yield return null;
        yield return null;

        Assert.IsFalse((bool)GetProperty(ctx.BattleUI, "IsBattleStarted"),
            "部署失败必须安全中止并回到战前准备（不得留在 _battleStarted=true）");
        Assert.IsNull(GetField(ctx.BattleUI, "_battleSystem"), "中止必须丢弃半成品 BattleSystem");
        Assert.IsNull(GetProperty(v1, "Deck"), "中止后不得建立错误共享牌库");
        Assert.AreEqual(unitsBefore, UnitCardsOnPlayerRows(ctx.BattleUI), "中止不得留下 ghost UI 卡");

        var startBtn = GetField(ctx.BattleUI, "btnStartBattle");
        Assert.IsNotNull(startBtn, "「开始战斗」按钮未绑定");
        Assert.IsTrue(((Component)startBtn).gameObject.activeSelf, "中止后「开始战斗」入口必须恢复可用");
    }
}
