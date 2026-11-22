using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

/// <summary>
/// Battle V0.1 Gate 0（3P Shared Deck）— 产品集成验证（PlayMode）。
///
/// 必须证明的是“新规则真实接入了正式产品路径”，而不是模块能跑：
///   - 真实 PlayerCollection 人物（Cook / Worker / Hunter 三个具体 Person）+ 真实 Run Deck（Profession Starter 槽）；
///   - 真实 SharedDeck / SharedActionPointPool / BattleUI / BattleField / Enemy Intent；
///   - 九张 Prototype 牌通过配置化规则结算；
///   - 没有第二套 Battle / Deck / Owner / Position Authority。
///
/// 进入方式与现有产品路径一致：SaveSystem.NewGame → ExpeditionSystem.ConfirmExpedition →
/// BattleUI.SetRunState + InitFromExpedition → StartBattleWithExpedition → PlaceUnit → OnStartBattle；
/// 出牌经真实 BattleUI 手牌点击 / 单位点击 / 高亮位置点击（Button.onClick）。
///
/// 团结引擎限制 asmdef 引用 Assembly-CSharp：BattleUI 等经反射访问；
/// RunState / SharedDeck / BattleCard 位于 BattleStage1.Runtime，可直接强类型访问。
/// </summary>
public class BattleGate0RulePipelinePlayTests
{
    private const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

    private static readonly string[] PrototypeTemplateIds =
    {
        "bcard_stoke_fire", "bcard_prep_cut", "bcard_share_meal",
        "bcard_lend_hand", "bcard_fill_gap", "bcard_swap_post",
        "bcard_finish_prey", "bcard_break_poise", "bcard_solo_hunt"
    };

    private object _battleUI;
    private object _v1;
    private object _battleSystem;
    private RunState _runState;
    private readonly List<string> _personIds = new List<string>();

    [UnityTest]
    public IEnumerator PrototypeDeck_IsBuiltFromRealRunDeckChain_WithRealPersonOwners()
    {
        IEnumerator run = EnterBattleWithPrototypeRoster();
        while (run.MoveNext()) yield return run.Current;

        var deck = (SharedDeck)GetProperty(_v1, "Deck");
        Assert.IsNotNull(deck, "共享牌库未初始化（正式 BattleV1Runtime）");

        var allCards = new List<BattleCard>();
        allCards.AddRange(deck.Hand);
        allCards.AddRange(deck.DrawPile);
        allCards.AddRange(deck.DiscardPile);
        allCards.AddRange(deck.ExhaustPile);

        foreach (var templateId in PrototypeTemplateIds)
        {
            BattleCard found = null;
            foreach (var card in allCards)
            {
                if (card.TemplateId == templateId) { found = card; break; }
            }
            Assert.IsNotNull(found, "本场共享牌库应包含 Prototype 牌：" + templateId);
            Assert.IsFalse(string.IsNullOrEmpty(found.OwnerPersonId),
                "Prototype 角色牌必须绑定真实 PersonId（Run Deck / Person Owner 链）：" + templateId);
            Assert.IsTrue(_personIds.Contains(found.OwnerPersonId),
                "牌主必须是本场真实上阵 Person：" + templateId + " → " + found.OwnerPersonId);
            Assert.IsTrue(BattleCardRuleResolver.HasRuleProgram(found),
                "Prototype 牌必须由配置化规则程序表达：" + templateId);
        }

        // 真实共享行动力池（不是第二套资源）
        var actionPoints = (SharedActionPointPool)GetProperty(_v1, "ActionPoints");
        Assert.IsNotNull(actionPoints, "共享行动力池未初始化");
        Assert.Greater(actionPoints.Max, 0, "共享行动力上限必须来自正式配置");

        // 手牌 UI 由正式 BattleUI.handContainer 渲染（没有平行第二套手牌宿主）
        // 说明：RefreshHand 重建时会先 Destroy 旧对象（Unity 延迟到帧末生效），因此先等一帧再计数。
        yield return null;

        var handContainer = (RectTransform)GetField(_battleUI, "handContainer");
        int handCardViews = 0;
        foreach (Transform child in handContainer)
        {
            if (child != null && child.name.StartsWith("V1Card_")) handCardViews++;
        }
        Assert.AreEqual(deck.Hand.Count, handCardViews, "正式手牌区应逐张渲染共享手牌（无平行第二套手牌宿主）");
    }

    [UnityTest]
    public IEnumerator PrepCut_ResolvesPredictedTarget_ThroughRealBattleUI()
    {
        IEnumerator run = EnterBattleWithPrototypeRoster();
        while (run.MoveNext()) yield return run.Current;

        var deck = (SharedDeck)GetProperty(_v1, "Deck");
        var actionPoints = (SharedActionPointPool)GetProperty(_v1, "ActionPoints");
        var prepCut = PullToHand(deck, "bcard_prep_cut");
        Assert.IsNotNull(prepCut, "应能把《切配》放入手牌（来自本场真实 Run Deck）");

        // 玩家在打出前必须能预测目标：除自身外 Actual Cost 最高、并列取最左
        var expectedTarget = PredictPrepCutTarget(deck, prepCut);
        Assert.IsNotNull(expectedTarget, "《切配》应有可预测的自动目标");

        int apBefore = actionPoints.Current;
        int targetCostBefore = expectedTarget.ActualCost;
        int targetDamageBefore = expectedTarget.ActualDamage;

        var otherCostsBefore = new List<KeyValuePair<BattleCard, int>>();
        foreach (var card in deck.Hand)
        {
            if (card == prepCut || card == expectedTarget) continue;
            otherCostsBefore.Add(new KeyValuePair<BattleCard, int>(card, card.ActualCost));
        }

        // 真实 UI 入口：手牌点击
        Call(_battleUI, "OnV1CardClicked", prepCut);
        yield return null;

        Assert.AreEqual(apBefore - prepCut.BaseCost, actionPoints.Current, "共享行动力应按 Actual Cost 真实支付");
        Assert.AreEqual(targetCostBefore - 1, expectedTarget.ActualCost, "预测目标的费用应真实下降 1");

        // Operation Applicability（Gate 0 §4.3）：目标没有 Damage 时 Damage 修改为 Not Applicable，
        // 但 Cost 修改照常执行，且整张牌结算不失败（手牌顺序由真实洗牌决定，因此断言必须与顺序无关）。
        // 产品语义：ActualDamage = Max(0, EffectValue + Modifier)（Clamp ≥ 0）；
        // 规则管线伤害牌（Legacy EffectValue 可能为 0）在 -2 后应为 0，期望值必须同样 Clamp。
        int expectedDamageAfter = expectedTarget.HasDamage
            ? System.Math.Max(0, targetDamageBefore - 2)
            : targetDamageBefore;
        Assert.AreEqual(expectedDamageAfter, expectedTarget.ActualDamage,
            expectedTarget.HasDamage
                ? "有 Damage 的目标牌伤害应真实下降 2"
                : "无 Damage 的目标牌：Damage 修改不适用，数值不变");
        foreach (var pair in otherCostsBefore)
        {
            Assert.AreEqual(pair.Value, pair.Key.ActualCost, "不应误加工其他手牌：" + pair.Key.DisplayName);
        }
        Assert.IsTrue(deck.DiscardPile.Contains(prepCut), "打出的牌应进入弃牌堆（真实 SharedDeck 循环）");
        Assert.IsFalse(deck.Hand.Contains(prepCut), "打出的牌不得留在手牌");
    }

    [UnityTest]
    public IEnumerator FillGapAndSwapPost_UseSinglePositionAuthority()
    {
        IEnumerator run = EnterBattleWithPrototypeRoster();
        while (run.MoveNext()) yield return run.Current;

        var deck = (SharedDeck)GetProperty(_v1, "Deck");
        var data = Call(_battleSystem, "GetBattleData", null);
        var field = GetMember(data, "PlayerField");
        var workerUnit = FindPlayerUnitByPersonId(_personIds[1]); // Worker（第 2 名原型 Person）
        Assert.IsNotNull(workerUnit, "Worker 单位未上阵");

        int rowBefore = (int)GetMember(workerUnit, "Row");
        int colBefore = (int)GetMember(workerUnit, "Column");

        // ----《补位》：点击真实高亮位置标记（真实 UI 点击路径）----
        var fillGap = PullToHand(deck, "bcard_fill_gap");
        Assert.IsNotNull(fillGap, "应能把《补位》放入手牌");
        Call(_battleUI, "OnV1CardClicked", fillGap);
        yield return null;

        var markerCell = FindFirstFreeCellOtherThan(rowBefore, colBefore);
        var marker = FindCellMarker(markerCell.x, markerCell.y);
        Assert.IsNotNull(marker, "《补位》应显示合法空格高亮标记（玩家可识别合法 Move 目标）");
        marker.GetComponent<Button>().onClick.Invoke();
        yield return null;

        Assert.AreEqual(markerCell.x, (int)GetMember(workerUnit, "Row"), "Worker 应移动到点击的排");
        Assert.AreEqual(markerCell.y, (int)GetMember(workerUnit, "Column"), "Worker 应移动到点击的列");

        // 单一 Position Authority：BattleField 记录与单位自身坐标一致
        AssertCellMatchesUnit(field, workerUnit, markerCell.x, markerCell.y);
        Assert.IsNull(Call(field, "GetUnitAt", RowEnum(rowBefore), ColEnum(colBefore)), "旧格应被释放");

        // 单位卡表现随逻辑位置移动（仍渲染在正式行容器中）
        var rowRect = GetPlayerRowRect(markerCell.x);
        bool cardInRow = false;
        foreach (Transform child in rowRect)
        {
            if (child != null && child.name == "Card_" + (string)GetMember(workerUnit, "UnitId")) cardInRow = true;
        }
        Assert.IsTrue(cardInRow, "Move 后单位卡应渲染在新排的正式行容器中");

        // ----《换岗》：点击真实我方单位 ----
        var swapPost = PullToHand(deck, "bcard_swap_post");
        Assert.IsNotNull(swapPost, "应能把《换岗》放入手牌");
        var cookUnit = FindPlayerUnitByPersonId(_personIds[0]); // Cook
        Assert.IsNotNull(cookUnit, "Cook 单位未上阵");

        int workerRowBeforeSwap = (int)GetMember(workerUnit, "Row");
        int workerColBeforeSwap = (int)GetMember(workerUnit, "Column");
        int cookRowBeforeSwap = (int)GetMember(cookUnit, "Row");
        int cookColBeforeSwap = (int)GetMember(cookUnit, "Column");

        Call(_battleUI, "OnV1CardClicked", swapPost);
        yield return null;
        Call(_battleUI, "OnPlayerUnitClick", cookUnit);
        yield return null;

        Assert.AreEqual(cookRowBeforeSwap, (int)GetMember(workerUnit, "Row"), "Worker 应移动到 Cook 的位置");
        Assert.AreEqual(cookColBeforeSwap, (int)GetMember(workerUnit, "Column"), "Worker 应移动到 Cook 的位置");
        Assert.AreEqual(workerRowBeforeSwap, (int)GetMember(cookUnit, "Row"), "Cook 应移动到 Worker 的位置");
        Assert.AreEqual(workerColBeforeSwap, (int)GetMember(cookUnit, "Column"), "Cook 应移动到 Worker 的位置");

        AssertCellMatchesUnit(field, workerUnit, cookRowBeforeSwap, cookColBeforeSwap);
        AssertCellMatchesUnit(field, cookUnit, workerRowBeforeSwap, workerColBeforeSwap);
    }

    [UnityTest]
    public IEnumerator Shield_AbsorbsDamage_AndClearsAtNextPlayerTurnStart()
    {
        IEnumerator run = EnterBattleWithPrototypeRoster();
        while (run.MoveNext()) yield return run.Current;

        var deck = (SharedDeck)GetProperty(_v1, "Deck");
        var cookUnit = FindPlayerUnitByPersonId(_personIds[0]);
        Assert.IsNotNull(cookUnit, "Cook 单位未上阵");

        var stokeFire = PullToHand(deck, "bcard_stoke_fire");
        Assert.IsNotNull(stokeFire, "应能把《收火》放入手牌");

        Call(_battleUI, "OnV1CardClicked", stokeFire);
        yield return null;

        Assert.Greater((int)GetMember(cookUnit, "Shield"), 0, "《收火》应赋予真实临时护盾");

        // 护盾先行吸收：Shield = 4，伤害 7 → Shield 0，HP -3
        int hpBefore = (int)GetMember(cookUnit, "CurrentHP");
        Call(_battleSystem, "ClearAllShields", null);
        Call(_battleSystem, "ApplyBattleShield", cookUnit, 4);
        Call(_battleSystem, "ApplyBattleDamage", cookUnit, 7);

        Assert.AreEqual(0, (int)GetMember(cookUnit, "Shield"), "护盾不足部分应完全吸收并归零");
        Assert.AreEqual(hpBefore - 3, (int)GetMember(cookUnit, "CurrentHP"), "剩余伤害应正确进入 HP");

        // 下一玩家回合开始：清除剩余护盾
        Call(_battleSystem, "ApplyBattleShield", cookUnit, 5);
        Call(_v1, "BeginPlayerTurn", null);
        yield return null;

        Assert.AreEqual(0, (int)GetMember(cookUnit, "Shield"), "下一玩家回合开始时剩余 Shield 应被清除");
    }

    // ==================== 产品路径搭建 ====================

    /// <summary>
    /// 从真实产品路径进入战斗：NewGame → 远征确认 → BattleUI + Run Deck（Profession Starter 槽）→ 上阵 3 名原型 Person。
    /// </summary>
    private IEnumerator EnterBattleWithPrototypeRoster()
    {
        _personIds.Clear();

        SceneManager.LoadScene("SampleScene");
        yield return null;
        yield return null;

        _battleUI = FindInstance("BattleUI");
        Assert.IsNotNull(_battleUI, "BattleUI（正式宿主）未找到");

        var collection = CallStatic("SaveSystem", "NewGame", null);
        Assert.IsNotNull(collection, "NewGame 失败");
        var dataManager = GetDataManager();

        var exp = System.Activator.CreateInstance(GetType("ExpeditionSystem"), new object[] { collection });

        object hero = null;
        var heroes = (IList)GetMember(collection, "OwnedHeroes");
        if (heroes != null && heroes.Count > 0)
        {
            hero = heroes[0];
            var hd = Call(dataManager, "GetHeroById", GetMember(hero, "HeroId"));
            if (hd != null) Call(exp, "SelectHero", hero, hd);
        }

        // Cook / Worker / Hunter 三名真实 Person（Person.csv 初始名册前三位）
        var ownedCharacters = (IList)GetMember(collection, "OwnedCharacters");
        Assert.IsNotNull(ownedCharacters, "长期人物列表为空");
        var identityCharacterIds = new[] { "char_001", "char_003", "char_002" };

        var ownedCards = (IList)GetMember(collection, "OwnedCharacterCards");
        Assert.IsNotNull(ownedCards, "角色卡册为空");

        var charCards = new List<object>();
        foreach (var characterId in identityCharacterIds)
        {
            object card = null;
            foreach (var candidate in ownedCards)
            {
                if (candidate == null) continue;
                if ((string)GetMember(candidate, "CharacterId") == characterId) { card = candidate; break; }
            }
            if (card == null) continue;

            _personIds.Add(PersonIdForCharacter(ownedCharacters, characterId));
            charCards.Add(card);
            Call(exp, "AddCharacterCard", card);
        }
        Assert.AreEqual(3, charCards.Count, "需要 3 名原型 Person（Cook / Worker / Hunter）");
        Assert.AreEqual(3, _personIds.Count, "必须解析出 3 个真实 PersonId");

        var confirm = GetType("ExpeditionSystem").GetMethod("ConfirmExpedition");
        Assert.IsNotNull(confirm, "ConfirmExpedition 方法未找到");
        var args = new object[2];
        Assert.IsTrue((bool)confirm.Invoke(exp, args), "确认出征失败");
        var chars = args[0];
        hero = args[1];

        // Run Deck：与 AdventureManager.SeedRunState 相同的生产链（Profession Starter 槽 → Run Deck）
        _runState = new RunState();
        var growthPersons = new List<IPersonGrowth>();
        foreach (var personId in _personIds)
        {
            var growth = CallStatic("PlayerGrowthLookup", "FindPerson", collection, personId);
            var typed = growth as IPersonGrowth;
            if (typed != null) growthPersons.Add(typed);
        }
        Assert.AreEqual(3, growthPersons.Count, "应能从长期人物解析出 3 个 IPersonGrowth");

        var allBattleCards = (IEnumerable<BattleCardData>)GetProperty(dataManager, "AllBattleCards");
        var starterEntries = StarterDeckBuilder.BuildStarterEntries(growthPersons, allBattleCards);
        Assert.Greater(starterEntries.Count, 0, "Run 初始牌条目为空（Profession Starter 未生效）");
        _runState.AddRunDeckEntries(starterEntries);

        Call(_battleUI, "SetRunState", _runState, null);
        Call(_battleUI, "InitFromExpedition", chars, hero,
            GetProperty(dataManager, "AllCharacters"), GetProperty(dataManager, "AllSkills"),
            StaticMonsterTestData.Build(), collection);

        var expUI = FindInstance("ExpeditionUI");
        if (expUI != null)
        {
            var canvas = GetField(expUI, "battleCanvas");
            if (canvas != null) ((GameObject)canvas).SetActive(true);
        }
        var battleGo = GetProperty(_battleUI, "gameObject");
        if (battleGo != null) ((GameObject)battleGo).SetActive(true);
        yield return null;

        Call(_battleUI, "StartBattleWithExpedition", null);
        yield return null;

        // 布阵：Cook → 前排, Worker → 中排, Hunter → 后排（真实战前布阵宿主）
        for (int i = 0; i < charCards.Count; i++)
        {
            var cd = Call(dataManager, "GetCharacterById", GetMember(charCards[i], "CharacterId"));
            if (cd == null) continue;
            Call(_battleUI, "PlaceUnit", charCards[i], cd, GetPlayerRowRect(i));
        }
        yield return null;

        Call(_battleUI, "OnStartBattle", null);
        yield return null;

        _v1 = GetField(_battleUI, "v1Runtime");
        Assert.IsNotNull(_v1, "BattleV1Runtime 未绑定");

        float wait = 0f;
        while ((BattleV1TurnFlow.PhaseType)GetProperty(_v1, "TurnPhase") != BattleV1TurnFlow.PhaseType.PlayerTurn)
        {
            wait += Time.deltaTime;
            if (wait > 15f) break;
            yield return null;
        }
        Assert.AreEqual(BattleV1TurnFlow.PhaseType.PlayerTurn,
            (BattleV1TurnFlow.PhaseType)GetProperty(_v1, "TurnPhase"), "未进入玩家回合");

        _battleSystem = GetField(_battleUI, "_battleSystem");
        Assert.IsNotNull(_battleSystem, "_battleSystem 未初始化");
    }

    // ==================== 反射与测试辅助 ====================

    private static System.Type GetType(string name) => System.Type.GetType(name + ", Assembly-CSharp");

    private static object GetDataManager() =>
        GetType("DataManager").GetProperty("Instance", BindingFlags.Public | BindingFlags.Static).GetValue(null);

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

    private static object CallStatic(string typeName, string method, params object[] args)
    {
        var m = GetType(typeName).GetMethod(method, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
        Assert.IsNotNull(m, "静态方法不存在：" + typeName + "." + method);
        return m.Invoke(null, args);
    }

    private static string PersonIdForCharacter(IList ownedCharacters, string characterId)
    {
        foreach (var character in ownedCharacters)
        {
            if (character == null) continue;
            if ((string)GetMember(character, "CharacterId") != characterId) continue;
            string personId = (string)GetMember(character, "PersonId");
            Assert.IsFalse(string.IsNullOrEmpty(personId), "人物缺少真实 PersonId：" + characterId);
            return personId;
        }
        Assert.Fail("未找到人物实例：" + characterId);
        return null;
    }

    private RectTransform GetPlayerRowRect(int battleRowIndex)
    {
        switch (battleRowIndex)
        {
            case 0: return (RectTransform)GetField(_battleUI, "playerFrontRow");
            case 1: return (RectTransform)GetField(_battleUI, "playerMiddleRow");
            default: return (RectTransform)GetField(_battleUI, "playerBackRow");
        }
    }

    private object FindPlayerUnitByPersonId(string personId)
    {
        var data = Call(_battleSystem, "GetBattleData", null);
        var units = (IList)GetMember(data, "PlayerUnits");
        foreach (var unit in units)
        {
            if (unit == null) continue;
            var resolved = Call(_battleUI, "GetPersonIdOfUnit", unit) as string;
            if (resolved == personId) return unit;
        }
        return null;
    }

    /// <summary>把一张指定模板的牌从本场真实牌堆移入手牌（保证测试可确定地打出指定牌；不替换牌堆 Authority）</summary>
    private static BattleCard PullToHand(SharedDeck deck, string templateId)
    {
        foreach (var card in new List<BattleCard>(deck.DrawPile))
        {
            if (card.TemplateId != templateId) continue;
            deck.DrawPile.Remove(card);
            deck.Hand.Add(card);
            return card;
        }
        foreach (var card in new List<BattleCard>(deck.DiscardPile))
        {
            if (card.TemplateId != templateId) continue;
            deck.DiscardPile.Remove(card);
            deck.Hand.Add(card);
            return card;
        }
        foreach (var card in deck.Hand)
        {
            if (card.TemplateId == templateId) return card;
        }
        return null;
    }

    /// <summary>《切配》可预测目标：除自身外 Actual Cost 最高、并列取手牌最左</summary>
    private static BattleCard PredictPrepCutTarget(SharedDeck deck, BattleCard source)
    {
        BattleCard best = null;
        int bestCost = int.MinValue;
        foreach (var card in deck.Hand)
        {
            if (card == null || card == source) continue;
            if (best == null || card.ActualCost > bestCost)
            {
                best = card;
                bestCost = card.ActualCost;
            }
        }
        return best;
    }

    private GameObject FindCellMarker(int row, int column)
    {
        var rowRect = GetPlayerRowRect(row);
        var child = rowRect.Find("RuleCellMarker_" + row + "_" + column);
        return child != null ? child.gameObject : null;
    }

    private Vector2Int FindFirstFreeCellOtherThan(int row, int column)
    {
        var data = Call(_battleSystem, "GetBattleData", null);
        var field = GetMember(data, "PlayerField");
        for (int r = 0; r < 3; r++)
        {
            for (int c = 0; c < 3; c++)
            {
                if (r == row && c == column) continue;
                if (Call(field, "GetUnitAt", RowEnum(r), ColEnum(c)) == null) return new Vector2Int(r, c);
            }
        }
        return new Vector2Int(row, column);
    }

    private static void AssertCellMatchesUnit(object field, object unit, int row, int column)
    {
        var at = Call(field, "GetUnitAt", RowEnum(row), ColEnum(column));
        Assert.IsNotNull(at, "目标格应被该单位占据：[" + row + "," + column + "]");
        Assert.AreSame(unit, at, "BattleField 占位必须与单位坐标一致（唯一 Position Authority）");
        Assert.AreEqual(row, (int)GetMember(unit, "Row"), "单位自身坐标必须与占位一致");
        Assert.AreEqual(column, (int)GetMember(unit, "Column"), "单位自身坐标必须与占位一致");
    }

    /// <summary>BattleRow / BattleColumn 位于 Assembly-CSharp，本程序集不引用；按枚举值构造</summary>
    private static object RowEnum(int row)
    {
        return System.Enum.ToObject(GetType("BattleRow"), row);
    }

    private static object ColEnum(int column)
    {
        return System.Enum.ToObject(GetType("BattleColumn"), column);
    }
}
