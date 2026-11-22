using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

/// <summary>
/// Battle V0.1 Playable Milestone A — PlayMode 闭环验证
///
/// 从正式产品路径数据流（SaveSystem.NewGame → ExpeditionSystem.ConfirmExpedition → BattleUI）进入战斗，
/// 通过真实 UI 按钮完成一个完整卡牌回合闭环：
///   玩家回合开始（恢复共享行动力 + 抽牌）→ 点击手牌出牌（支付共享行动力、牌进弃牌堆）
///   → 点击结束回合 → 敌方阶段 → 下一玩家回合（再次恢复行动力 + 抽牌）。
///
/// 团结引擎限制 asmdef 引用运行时预定义程序集（Assembly-CSharp），因此本测试通过 System.Reflection
/// 访问 BattleUI/BattleV1Runtime 等类型；规则逻辑类型（SharedDeck/AP/BattleCard/BattleV1TurnFlow）
/// 位于 BattleStage1.Runtime，可直接强类型引用。
/// </summary>
public class BattleMilestoneAPlayTests
{
    private static BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

    /// <summary>
    /// M1：把本场一个我方 BattleUnit 映射到其具体人物 PersonId（与 BattleUI.GetPersonIdOfUnit 一致）。
    /// 供牌归属断言使用（同模板多实例不会塌缩）。
    /// </summary>
    private string PersonIdOfUnit(object battleUI, object unit)
    {
        if (battleUI == null || unit == null) return null;
        var m = battleUI.GetType().GetMethod("GetPersonIdOfUnit",
            BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
        if (m != null) return (string)m.Invoke(battleUI, new[] { unit });
        return (string)GetMember(unit, "SourceId");
    }

    [UnityTest]
    public IEnumerator PlayerCanCompleteCardTurnLoop_InFormalBattle()
    {
        IEnumerator run = RunLoop();
        while (true)
        {
            bool moved;
            try
            {
                moved = run.MoveNext();
            }
            catch (System.Exception e)
            {
                Debug.LogError("[PlayTest] 异常：" + e);
                throw;
            }
            if (!moved) break;
            yield return run.Current;
        }
    }

    /// <summary>
    /// 0 人上阵禁止开战：远征携带角色但战前不上阵任何人时，
    /// 点「开始战斗」应被阻止（不进入正式战斗、不初始化共享牌库），并提示至少上阵 1 名角色。
    /// </summary>
    [UnityTest]
    public IEnumerator ZeroDeployed_StartBattleIsBlocked()
    {
        IEnumerator run = RunZeroDeployedBlock();
        while (true)
        {
            bool moved;
            try
            {
                moved = run.MoveNext();
            }
            catch (System.Exception e)
            {
                Debug.LogError("[PlayTest] 异常：" + e);
                throw;
            }
            if (!moved) break;
            yield return run.Current;
        }
    }

    private IEnumerator RunZeroDeployedBlock()
    {
        SceneManager.LoadScene("SampleScene");
        yield return null;
        yield return null;

        var battleUI = FindInstance("BattleUI");
        var expUI = FindInstance("ExpeditionUI");
        Assert.IsNotNull(battleUI, "BattleUI（正式宿主）未找到");

        var collection = CallStatic("SaveSystem", "NewGame", null);
        var exp = System.Activator.CreateInstance(GetType("ExpeditionSystem"), new object[] { collection });
        var dataManager = GetDataManager();

        var heroes = (IList)GetMember(collection, "OwnedHeroes");
        if (heroes != null && heroes.Count > 0)
        {
            var hi = heroes[0];
            var hd = Call(dataManager, "GetHeroById", GetMember(hi, "HeroId"));
            if (hd != null) Call(exp, "SelectHero", hi, hd);
        }
        var charCards = (IList)GetMember(collection, "OwnedCharacterCards");
        if (charCards != null && charCards.Count > 0)
            Call(exp, "AddCharacterCard", charCards[0]); // 远征携带角色

        var confirm = GetType("ExpeditionSystem").GetMethod("ConfirmExpedition");
        var args = new object[2];
        Assert.IsTrue((bool)confirm.Invoke(exp, args), "确认出征失败");

        Call(battleUI, "InitFromExpedition", args[0], args[1],
            GetProperty(dataManager, "AllCharacters"), GetProperty(dataManager, "AllSkills"),
            StaticMonsterTestData.Build(), null);
        var canvas = GetField(expUI, "battleCanvas");
        if (expUI != null && canvas != null) ((GameObject)canvas).SetActive(true);
        var battleGo = GetProperty(battleUI, "gameObject");
        if (battleGo != null) ((GameObject)battleGo).SetActive(true);
        yield return null;

        Call(battleUI, "StartBattleWithExpedition", null);
        yield return null;

        // 战前不上阵任何人，直接点「开始战斗」→ 应被阻止
        Call(battleUI, "OnStartBattle", null);
        yield return null;

        Assert.IsFalse((bool)GetProperty(battleUI, "IsBattleStarted"), "0 人上阵不应进入正式战斗");
        var v1 = GetField(battleUI, "v1Runtime");
        Assert.IsNull(GetProperty(v1, "Deck"), "0 人上阵不应初始化共享牌库");
    }

    private IEnumerator RunLoop()
    {
        // 1. 加载正式场景
        SceneManager.LoadScene("SampleScene");
        yield return null;
        yield return null;

        var battleUI = FindInstance("BattleUI");
        var expUI = FindInstance("ExpeditionUI");
        Assert.IsNotNull(battleUI, "BattleUI（正式宿主）未找到");

        // 2. 正式远征数据流
        var collection = CallStatic("SaveSystem", "NewGame", null);
        Assert.IsNotNull(collection, "NewGame 失败");

        var exp = System.Activator.CreateInstance(GetType("ExpeditionSystem"), new object[] { collection });
        var dataManager = GetDataManager();

        var heroes = (IList)GetMember(collection, "OwnedHeroes");
        if (heroes != null && heroes.Count > 0)
        {
            var heroInst = heroes[0];
            var heroData = Call(dataManager, "GetHeroById", GetMember(heroInst, "HeroId"));
            if (heroData != null) Call(exp, "SelectHero", heroInst, heroData);
        }
        var charCards = (IList)GetMember(collection, "OwnedCharacterCards");
        if (charCards != null && charCards.Count > 0)
        {
            Call(exp, "AddCharacterCard", charCards[0]); // 战士 ccard_001
            if (charCards.Count > 1)
                Call(exp, "AddCharacterCard", charCards[1]); // 猎人 ccard_002
            if (charCards.Count > 2)
                Call(exp, "AddCharacterCard", charCards[2]); // 医师 ccard_003（本次不上阵）
        }
        var confirm = GetType("ExpeditionSystem").GetMethod("ConfirmExpedition");
        Assert.IsNotNull(confirm, "ConfirmExpedition 方法未找到");
        var args = new object[2];
        bool confirmed = (bool)confirm.Invoke(exp, args);
        Assert.IsTrue(confirmed, "确认出征失败");
        var chars = args[0];
        var hero = args[1];

        // 3. 进入正式 Battle 宿主（与 ExpeditionUI.HandleNodeClick(Battle) 相同入口）
        Call(battleUI, "InitFromExpedition", chars, hero,
            GetProperty(dataManager, "AllCharacters"), GetProperty(dataManager, "AllSkills"),
            StaticMonsterTestData.Build(), null);
        var canvas = GetField(expUI, "battleCanvas");
        if (expUI != null && canvas != null) ((GameObject)canvas).SetActive(true);
        var battleGo = GetProperty(battleUI, "gameObject");
        if (battleGo != null) ((GameObject)battleGo).SetActive(true);
        yield return null;

        // 4. 启动战斗协程（先初始化 _pendingRows 等战前状态，挂起等待开始战斗）
        Call(battleUI, "StartBattleWithExpedition", null);
        yield return null;

        // 5. 战前布阵：战士 → 中排，猎人 → 前排，英雄 → 后排（医师在远征队伍但未上阵）
        var charList = (IList)chars;
        Assert.GreaterOrEqual(charList.Count, 3, "需要至少 3 名远征角色（战士+猎人+医师）");
        var charData1 = Call(dataManager, "GetCharacterById", GetMember(charList[0], "CharacterId"));
        Assert.IsNotNull(charData1, "战士角色数据缺失");
        Call(battleUI, "PlaceUnit", charList[0], charData1, GetField(battleUI, "playerMiddleRow"));
        var charData2 = Call(dataManager, "GetCharacterById", GetMember(charList[1], "CharacterId"));
        Assert.IsNotNull(charData2, "猎人角色数据缺失");
        Call(battleUI, "PlaceUnit", charList[1], charData2, GetField(battleUI, "playerFrontRow"));
        if (hero != null)
        {
            var heroData3 = Call(dataManager, "GetHeroById", GetMember(hero, "HeroId"));
            if (heroData3 != null)
                Call(battleUI, "PlaceHero", heroData3, hero, GetField(battleUI, "playerBackRow"));
        }
        yield return null;

        // 6. 开始战斗（_battleStarted=true，协程进入 ExecuteBattle）
        Call(battleUI, "OnStartBattle", null);
        yield return null;

        // Case C：开战后阵型锁定——剩余未上阵的战前卡应被隐藏，不能再免费拖入战场
        var ctObj = GetProperty(battleUI, "PrepCardHost");
        Assert.IsNotNull(ctObj, "准备卡宿主未绑定");
        bool prepHidden = true;
        foreach (Transform child in (Transform)ctObj)
        {
            if (child == null) continue;
            if ((child.name.StartsWith("PrepCard_") || child.name.StartsWith("HeroPrepCard_")) && child.gameObject.activeSelf)
            {
                prepHidden = false;
                break;
            }
        }
        Assert.IsTrue(prepHidden, "开战后战前未上阵卡应被隐藏（阵型锁定）");

        // 6. 等待玩家回合开始（V1 手牌生成）
        var v1 = GetField(battleUI, "v1Runtime");
        Assert.IsNotNull(v1, "BattleV1Runtime 未绑定");
        float wait = 0f;
        while (true)
        {
            var phase = (BattleV1TurnFlow.PhaseType)GetProperty(v1, "TurnPhase");
            var deck = (SharedDeck)GetProperty(v1, "Deck");
            if (phase == BattleV1TurnFlow.PhaseType.PlayerTurn && deck != null && deck.Hand.Count > 0) break;
            wait += Time.deltaTime;
            if (wait > 8f) break;
            yield return null;
        }

        Assert.AreEqual(BattleV1TurnFlow.PhaseType.PlayerTurn, (BattleV1TurnFlow.PhaseType)GetProperty(v1, "TurnPhase"), "未进入玩家回合");
        var sharedDeck = (SharedDeck)GetProperty(v1, "Deck");
        var sharedAp = (SharedActionPointPool)GetProperty(v1, "ActionPoints");
        Assert.Greater(sharedDeck.Hand.Count, 0, "玩家回合开始后手牌为空");
        Assert.AreEqual(sharedAp.Max, sharedAp.Current, "玩家回合开始共享行动力未恢复");

        // 归属过滤（Case A/B）：只有实际上阵成员（本场上阵的 2 名角色 + 英雄）的角色牌进入共享牌库；
        // 未上阵成员（携带或未携带）的专属牌不得进入。
        // Initial Person & Profession Bootstrap：期望集合按实际上阵成员 + 同一测试映射推导，
        // 不再写死「战士/猎人/医师」模板名与 bcard_heal / bcard_rally 具体牌
        //（人物是谁来自 Person.csv，牌归属来自配置，不按职业词推断）。
        var expectedCardIds = new List<string>();
        expectedCardIds.AddRange(BattleV1DemoCardPool.GetCardsForPerson((string)GetMember(charList[0], "CharacterId")));
        expectedCardIds.AddRange(BattleV1DemoCardPool.GetCardsForPerson((string)GetMember(charList[1], "CharacterId")));
        if (hero != null)
            expectedCardIds.AddRange(BattleV1DemoCardPool.GetCardsForPerson((string)GetMember(hero, "HeroId")));

        int rosterTotal = sharedDeck.DrawPile.Count + sharedDeck.Hand.Count;
        Assert.AreEqual(3, rosterTotal, "共享牌库基础数量应为 3（上阵 3 人的角色牌，每人 1 张）");

        foreach (var expectedId in expectedCardIds)
        {
            Assert.IsTrue(
                sharedDeck.DrawPile.Exists(c => c.TemplateId == expectedId)
                || sharedDeck.Hand.Exists(c => c.TemplateId == expectedId),
                "上阵成员应有的牌应进入本场共享牌库：" + expectedId);
        }

        // 未上阵成员（未布阵）的专属牌：其映射中不被任何上阵成员拥有的牌不得进入（手牌与牌库皆然）
        var undeployedOnlyCardIds = new List<string>();
        foreach (var ownedCard in (IList)GetMember(collection, "OwnedCharacterCards"))
        {
            if (ownedCard == null) continue;
            if (ReferenceEquals(ownedCard, charList[0]) || ReferenceEquals(ownedCard, charList[1])) continue;
            string templateId = (string)GetMember(ownedCard, "CharacterId");
            foreach (var id in BattleV1DemoCardPool.GetCardsForPerson(templateId))
            {
                if (expectedCardIds.Contains(id) || undeployedOnlyCardIds.Contains(id)) continue;
                undeployedOnlyCardIds.Add(id);
            }
        }
        foreach (var undeployedId in undeployedOnlyCardIds)
        {
            Assert.IsFalse(sharedDeck.DrawPile.Exists(c => c.TemplateId == undeployedId),
                "未上阵成员专属牌不应进入牌库：" + undeployedId);
            Assert.IsFalse(sharedDeck.Hand.Exists(c => c.TemplateId == undeployedId),
                "未上阵成员专属牌不应出现在手牌：" + undeployedId);
        }

        // 等一帧让 RefreshHand 的 Destroy 完成（Unity Destroy 在帧末生效），再校验手牌 UI
        yield return null;
        Assert.AreEqual(sharedDeck.Hand.Count, CountHandCards(battleUI), "共享手牌 UI 张数与逻辑手牌不一致");
        Assert.GreaterOrEqual(CountDistinctHandPositions(battleUI), sharedDeck.Hand.Count, "多张共享手牌在同一位置重叠");

        // 可见性硬断言：V1 手牌必须位于 handContainer（BattleActionHandRoot）矩形内，否则玩家看不到
        var handRt = (RectTransform)GetField(battleUI, "handContainer");
        Assert.IsNotNull(handRt, "handContainer 未绑定");
        Assert.AreSame(handRt, GetField(v1, "handContainer"), "正式 Battle Hand 必须复用 BattleUI 的 BattleActionHandRoot");
        var handRect = handRt.rect;
        float minHandX = float.PositiveInfinity, maxHandX = float.NegativeInfinity;
        foreach (Transform child in handRt)
        {
            if (child == null || !child.name.StartsWith("V1Card_")) continue;
            var local = handRt.InverseTransformPoint(child.position);
            minHandX = Mathf.Min(minHandX, local.x);
            maxHandX = Mathf.Max(maxHandX, local.x);
            bool inside = local.x >= handRect.xMin && local.x <= handRect.xMax
                          && local.y >= handRect.yMin && local.y <= handRect.yMax;
            Assert.IsTrue(inside, "V1 共享手牌未位于玩家可见容器区域内：" + child.name + " local=" + local);
        }
        Assert.AreEqual(0f, (minHandX + maxHandX) * 0.5f, 0.5f, "正式共享手牌须围绕容器中心排布");

        // WP-C：Legacy 道具手牌（Item Hand）已退出正式产品路径 ——
        // 运行时 itemCardRoot 引用被清除，旧道具卡不再进入产品 UI（无“仅显示不交互”的平行展示）。
        var itemRoot = (RectTransform)GetField(battleUI, "itemCardRoot");
        Assert.IsNull(itemRoot, "Legacy Item Hand 根不应在正式战斗中保留（WP-C 已清理该产品路径）");

        // 7. 玩家出牌：点击可打出的手牌（需目标时点击对应单位卡）
        int apBefore = sharedAp.Current;
        bool played = TryPlayCards(battleUI, v1, sharedDeck);
        Assert.IsTrue(played, "无法打出任何手牌");
        Assert.Less(sharedAp.Current, apBefore, "出牌后共享行动力未扣减");
        yield return null;

        // 8. 结束回合 → 敌方阶段 → 下一玩家回合
        // Current Feedback 在出牌表现期间锁定 EndTurn；按玩家可操作时机点击，不能只等待一帧。
        float feedbackDeadline = Time.realtimeSinceStartup + 15f;
        while ((bool)GetProperty(battleUI, "IsFeedbackPlaying"))
        {
            Assert.Less(Time.realtimeSinceStartup, feedbackDeadline, "出牌反馈未结束，EndTurn 输入未释放");
            yield return null;
        }
        int turnBefore = (int)GetProperty(v1, "TurnNumber");
        var endTurnBtn = (Button)GetField(battleUI, "btnEndTurn");
        Assert.IsNotNull(endTurnBtn, "结束回合按钮未绑定");
        endTurnBtn.onClick.Invoke();
        yield return null;

        wait = 0f;
        while ((int)GetProperty(v1, "TurnNumber") == turnBefore && wait < 8f)
        {
            wait += Time.deltaTime;
            yield return null;
        }

        Assert.Greater((int)GetProperty(v1, "TurnNumber"), turnBefore, "结束回合后未进入下一玩家回合");
        Assert.AreEqual(sharedAp.Max, sharedAp.Current, "下一玩家回合共享行动力未恢复");
        Assert.Greater(sharedDeck.Hand.Count, 0, "下一玩家回合未重新抽牌");
    }

    /// <summary>
    /// 模拟玩家点击手牌出牌：需目标的牌进入目标选择后点击对应单位卡完成结算。
    /// 若打出被拒（如目标超出攻击距离，Current GameSpec 距离校验），取消选择后换下一张牌继续尝试。
    /// 牌离开手牌即视为打出成功。
    /// </summary>
    private bool TryPlayCards(object battleUI, object v1, SharedDeck deck)
    {
        var skipped = new HashSet<string>();
        for (int attempt = 0; attempt < 8; attempt++)
        {
            BattleCard playable = null;
            foreach (var card in deck.Hand)
            {
                if (skipped.Contains(card.CardId)) continue;
                if ((bool)Call(v1, "CanPlayCard", card)) { playable = card; break; }
            }
            if (playable == null) return false;

            if (!ClickHandCard(battleUI, playable)) return false;

            if (playable.TargetScope == BattleCardTargetScope.EnemyUnit)
                ClickTarget(battleUI, false);
            else if (playable.TargetScope == BattleCardTargetScope.AllyUnit)
                ClickTarget(battleUI, true);

            if (!deck.Hand.Contains(playable)) return true;

            // 未打出（目标超出攻击距离等被拒）：记入跳过、再点一次取消选择，下一轮尝试别的牌
            skipped.Add(playable.CardId);
            ClickHandCard(battleUI, playable);
        }

        return false;
    }

    private bool ClickHandCard(object battleUI, BattleCard card)
    {
        var container = (RectTransform)GetField(battleUI, "handContainer");
        if (container == null) return false;
        foreach (Transform child in container)
        {
            if (child == null) continue;
            if (child.name != "V1Card_" + card.CardId) continue;
            var btn = child.GetComponent<Button>();
            if (btn == null) continue;
            btn.onClick.Invoke(); // 玩家点击手牌
            return true;
        }
        return false;
    }

    private bool ClickTarget(object battleUI, bool isPlayer)
    {
        var rowFields = isPlayer
            ? new[] { "playerFrontRow", "playerMiddleRow", "playerBackRow" }
            : new[] { "enemyFrontRow", "enemyMiddleRow", "enemyBackRow" };

        foreach (var name in rowFields)
        {
            var row = (RectTransform)GetField(battleUI, name);
            if (row == null) continue;
            foreach (Transform child in row)
            {
                if (child == null) continue;
                if (!child.name.StartsWith("Card_")) continue;
                var btn = child.GetComponent<Button>();
                if (btn != null && btn.interactable) { btn.onClick.Invoke(); return true; }
            }
        }

        return false;
    }

    // ---------- Stage 3：上阵上限 6 + 正式抽牌数（R4.1 / R10.2） ----------

    /// <summary>
    /// 集成验证：战前最多上阵 6 个我方单位（英雄占正常位置，R4.1）。
    /// 6 人（5 角色 + 英雄）后可正常开战，第 7 个候选被拒绝。
    /// </summary>
    [UnityTest]
    public IEnumerator MaxSixDeployment_HeroCountsTowardCap()
    {
        IEnumerator run = RunMaxSixDeployment();
        while (true)
        {
            bool moved;
            try
            {
                moved = run.MoveNext();
            }
            catch (System.Exception e)
            {
                Debug.LogError("[PlayTest] 异常：" + e);
                throw;
            }
            if (!moved) break;
            yield return run.Current;
        }
    }

    private IEnumerator RunMaxSixDeployment()
    {
        SceneManager.LoadScene("SampleScene");
        yield return null;
        yield return null;

        var battleUI = FindInstance("BattleUI");
        var expUI = FindInstance("ExpeditionUI");
        Assert.IsNotNull(battleUI, "BattleUI（正式宿主）未找到");

        var collection = CallStatic("SaveSystem", "NewGame", null);
        Assert.IsNotNull(collection, "NewGame 失败");
        var exp = System.Activator.CreateInstance(GetType("ExpeditionSystem"), new object[] { collection });
        var dataManager = GetDataManager();

        var heroes = (IList)GetMember(collection, "OwnedHeroes");
        if (heroes != null && heroes.Count > 0)
        {
            var hi = heroes[0];
            var hd = Call(dataManager, "GetHeroById", GetMember(hi, "HeroId"));
            if (hd != null) Call(exp, "SelectHero", hi, hd);
        }

        var charCards = (IList)GetMember(collection, "OwnedCharacterCards");
        Assert.IsNotNull(charCards, "角色卡册为空");
        Assert.GreaterOrEqual(charCards.Count, 3, "默认卡册应至少 3 名角色");
        // 全部角色卡入远征候选（英雄另计；出征编队可大于本场上阵数 R4.2）
        for (int i = 0; i < charCards.Count; i++)
            Call(exp, "AddCharacterCard", charCards[i]);

        var confirm = GetType("ExpeditionSystem").GetMethod("ConfirmExpedition");
        Assert.IsNotNull(confirm, "ConfirmExpedition 方法未找到");
        var args = new object[2];
        Assert.IsTrue((bool)confirm.Invoke(exp, args), "确认出征失败（当前英雄门槛已迁移：英雄可选）");
        var chars = args[0];
        var hero = args[1];

        Call(battleUI, "InitFromExpedition", chars, hero,
            GetProperty(dataManager, "AllCharacters"), GetProperty(dataManager, "AllSkills"),
            StaticMonsterTestData.Build(), null);
        var canvas = GetField(expUI, "battleCanvas");
        if (expUI != null && canvas != null) ((GameObject)canvas).SetActive(true);
        var battleGo = GetProperty(battleUI, "gameObject");
        if (battleGo != null) ((GameObject)battleGo).SetActive(true);
        yield return null;

        Call(battleUI, "StartBattleWithExpedition", null);
        yield return null;

        // 布阵：全部角色 + 英雄（英雄占正常上阵位置，最多 6）
        var charList = (IList)chars;
        for (int i = 0; i < charList.Count; i++)
        {
            var card = charList[i];
            var cd = Call(dataManager, "GetCharacterById", GetMember(card, "CharacterId"));
            Assert.IsNotNull(cd, "角色数据缺失：" + GetMember(card, "CharacterId"));
            RectTransform rowRt = i % 3 == 0 ? (RectTransform)GetField(battleUI, "playerFrontRow")
                                : i % 3 == 1 ? (RectTransform)GetField(battleUI, "playerMiddleRow")
                                             : (RectTransform)GetField(battleUI, "playerBackRow");
            Call(battleUI, "PlaceUnit", card, cd, rowRt);
        }
        if (hero != null)
        {
            var heroData3 = Call(dataManager, "GetHeroById", GetMember(hero, "HeroId"));
            if (heroData3 != null)
                Call(battleUI, "PlaceHero", heroData3, hero, GetField(battleUI, "playerBackRow"));
        }
        yield return null;

        // 上阵上限校验：英雄占正常位置，总数最多 6（R4.1）
        var pendingRows = GetField(battleUI, "_pendingRows");
        Assert.IsNotNull(pendingRows, "_pendingRows 未初始化");
        int pendingCount = ((IDictionary)pendingRows).Count;
        int expected = charList.Count + (hero != null ? 1 : 0);
        Assert.AreEqual(expected, pendingCount, "布阵人数应与候选部署一致");
        Assert.LessOrEqual(pendingCount, 6, "上阵人数不得超过 6");
        // 默认卡册 5 角色 + 英雄 = 6，达到上限
        if (expected == 6)
        {
            var extraCard = charList[0];
            var extraCd = Call(dataManager, "GetCharacterById", GetMember(extraCard, "CharacterId"));
            Call(battleUI, "PlaceUnit", extraCard, extraCd, GetField(battleUI, "playerFrontRow"));
            Assert.AreEqual(6, ((IDictionary)GetField(battleUI, "_pendingRows")).Count, "已达上限后不应再新增上阵（第 7 个被拒）");
        }

        Call(battleUI, "OnStartBattle", null);
        yield return null;

        // 等待玩家回合开始
        var v1 = GetField(battleUI, "v1Runtime");
        Assert.IsNotNull(v1, "BattleV1Runtime 未绑定");
        float wait = 0f;
        while (true)
        {
            var phase = (BattleV1TurnFlow.PhaseType)GetProperty(v1, "TurnPhase");
            if (phase == BattleV1TurnFlow.PhaseType.PlayerTurn) break;
            wait += Time.deltaTime;
            if (wait > 8f) break;
            yield return null;
        }
        Assert.AreEqual(BattleV1TurnFlow.PhaseType.PlayerTurn, (BattleV1TurnFlow.PhaseType)GetProperty(v1, "TurnPhase"), "未进入玩家回合");

        var battleSystem = GetField(battleUI, "_battleSystem");
        var bsData = Call(battleSystem, "GetBattleData", null);
        var playerUnits = (IList)GetMember(bsData, "PlayerUnits");
        Assert.AreEqual(6, playerUnits.Count, "开战后应有 6 名我方单位（5 角色 + 英雄占正常位置）");
    }

    // ---------- 反射辅助 ----------

    // ---------- Stage 3A：V1 回合 TurnStart 技能触发迁移 ----------

    // ---------- Stage 4：0HP 失能闭环（R15.x） ----------

    /// <summary>
    /// 集成验证：一名角色 0HP 后立即失能（无死亡之门），其角色牌从共享牌库全部牌区移除；
    /// 其余角色牌仍正常循环；下一玩家回合基础抽牌数同步减少（R10.2），AP 与空间规则正常。
    /// </summary>
    [UnityTest]
    public IEnumerator DownedCharacter_ExitsFight_AndCardsRemoved()
    {
        IEnumerator run = RunDownedCharacter();
        while (true)
        {
            bool moved;
            try
            {
                moved = run.MoveNext();
            }
            catch (System.Exception e)
            {
                Debug.LogError("[PlayTest] 异常：" + e);
                throw;
            }
            if (!moved) break;
            yield return run.Current;
        }
    }

    private IEnumerator RunDownedCharacter()
    {
        SceneManager.LoadScene("SampleScene");
        yield return null;
        yield return null;

        var battleUI = FindInstance("BattleUI");
        var expUI = FindInstance("ExpeditionUI");
        Assert.IsNotNull(battleUI, "BattleUI（正式宿主）未找到");

        var collection = CallStatic("SaveSystem", "NewGame", null);
        Assert.IsNotNull(collection, "NewGame 失败");
        var exp = System.Activator.CreateInstance(GetType("ExpeditionSystem"), new object[] { collection });
        var dataManager = GetDataManager();

        var heroes = (IList)GetMember(collection, "OwnedHeroes");
        if (heroes != null && heroes.Count > 0)
        {
            var hi = heroes[0];
            var hd = Call(dataManager, "GetHeroById", GetMember(hi, "HeroId"));
            if (hd != null) Call(exp, "SelectHero", hi, hd);
        }

        object warriorCard = null;
        object rallyCard = null;
        var charCards = (IList)GetMember(collection, "OwnedCharacterCards");
        Assert.IsNotNull(charCards, "角色卡册为空");
        foreach (var c in charCards)
        {
            string charId = (string)GetMember(c, "CharacterId");
            if (charId == "char_001") warriorCard = c;
            if (charId == "char_004") rallyCard = c;
        }
        Assert.IsNotNull(warriorCard, "未找到战士卡 char_001");
        Assert.IsNotNull(rallyCard, "未找到村长卡 char_004");
        Call(exp, "AddCharacterCard", warriorCard);
        Call(exp, "AddCharacterCard", rallyCard);

        var confirm = GetType("ExpeditionSystem").GetMethod("ConfirmExpedition");
        Assert.IsNotNull(confirm, "ConfirmExpedition 方法未找到");
        var args = new object[2];
        Assert.IsTrue((bool)confirm.Invoke(exp, args), "确认出征失败");
        var chars = args[0];
        var hero = args[1];

        Call(battleUI, "InitFromExpedition", chars, hero,
            GetProperty(dataManager, "AllCharacters"), GetProperty(dataManager, "AllSkills"),
            StaticMonsterTestData.Build(), null);
        var canvas = GetField(expUI, "battleCanvas");
        if (expUI != null && canvas != null) ((GameObject)canvas).SetActive(true);
        var battleGo = GetProperty(battleUI, "gameObject");
        if (battleGo != null) ((GameObject)battleGo).SetActive(true);
        yield return null;

        Call(battleUI, "StartBattleWithExpedition", null);
        yield return null;

        // 布阵：战士/村长同排（相邻列），英雄后排
        var charDataWarrior = Call(dataManager, "GetCharacterById", "char_001");
        var charDataRally = Call(dataManager, "GetCharacterById", "char_004");
        Assert.IsNotNull(charDataWarrior, "战士角色数据缺失");
        Assert.IsNotNull(charDataRally, "村长角色数据缺失");
        Call(battleUI, "PlaceUnit", warriorCard, charDataWarrior, GetField(battleUI, "playerFrontRow"));
        Call(battleUI, "PlaceUnit", rallyCard, charDataRally, GetField(battleUI, "playerFrontRow"));
        if (hero != null)
        {
            var heroData3 = Call(dataManager, "GetHeroById", GetMember(hero, "HeroId"));
            if (heroData3 != null)
                Call(battleUI, "PlaceHero", heroData3, hero, GetField(battleUI, "playerBackRow"));
        }
        yield return null;

        var pendingCols = GetField(battleUI, "_pendingCols");
        Assert.IsNotNull(pendingCols, "_pendingCols 未初始化");
        var dictSetter = pendingCols.GetType().GetProperty("Item").GetSetMethod();
        Assert.IsNotNull(dictSetter, "_pendingCols 索引器 setter 未找到");
        dictSetter.Invoke(pendingCols, new object[] { GetMember(warriorCard, "CardId"), EnumVal("BattleColumn", 0) });
        dictSetter.Invoke(pendingCols, new object[] { GetMember(rallyCard, "CardId"), EnumVal("BattleColumn", 1) });

        Call(battleUI, "OnStartBattle", null);
        yield return null;

        // 等待玩家回合开始
        var v1 = GetField(battleUI, "v1Runtime");
        Assert.IsNotNull(v1, "BattleV1Runtime 未绑定");
        float wait = 0f;
        var sharedDeck = (SharedDeck)GetProperty(v1, "Deck");
        while (true)
        {
            var phase = (BattleV1TurnFlow.PhaseType)GetProperty(v1, "TurnPhase");
            if (phase == BattleV1TurnFlow.PhaseType.PlayerTurn && sharedDeck != null && sharedDeck.Hand.Count > 0) break;
            sharedDeck = (SharedDeck)GetProperty(v1, "Deck");
            wait += Time.deltaTime;
            if (wait > 8f) break;
            yield return null;
        }
        Assert.AreEqual(BattleV1TurnFlow.PhaseType.PlayerTurn, (BattleV1TurnFlow.PhaseType)GetProperty(v1, "TurnPhase"), "未进入玩家回合");

        // 首回合：3 名有效上阵 → 抽 3 张（R10.2）
        Assert.AreEqual(3, sharedDeck.Hand.Count, "首回合基础抽牌数应等于当前有效上阵人数（3）");

        var battleSystem = GetField(battleUI, "_battleSystem");
        var bsData = Call(battleSystem, "GetBattleData", null);
        var playerUnits = (IList)GetMember(bsData, "PlayerUnits");
        object warrior = null;
        object rally = null;
        foreach (var u in playerUnits)
        {
            if ((string)GetMember(u, "SourceId") == "char_001") warrior = u;
            if ((string)GetMember(u, "SourceId") == "char_004") rally = u;
        }
        Assert.IsNotNull(warrior, "未找到战士单位");

        // 战士 0HP → 立即失能，不进入死亡之门
        Call(battleSystem, "ApplyBattleDamage", warrior, 999);
        Assert.IsTrue((bool)GetMember(warrior, "IsDown"), "战士 0HP 应立即失能");
        Assert.IsFalse((bool)GetMember(warrior, "InDeathDoor"), "失能不进入死亡之门");

        // 失能角色牌从共享牌库全部牌区移除
        // M1：按战士单位对应的真实 PersonId 断言（不再用模板 CharacterId）
        string warriorPid = PersonIdOfUnit(battleUI, warrior);
        Call(battleUI, "SyncDownedPersonCards", null);
        Assert.IsFalse(sharedDeck.Hand.Exists(c => c.OwnerPersonId == warriorPid), "手牌不应含失能角色牌");
        Assert.IsFalse(sharedDeck.DrawPile.Exists(c => c.OwnerPersonId == warriorPid), "抽牌堆不应含失能角色牌");
        Assert.IsFalse(sharedDeck.DiscardPile.Exists(c => c.OwnerPersonId == warriorPid), "弃牌堆不应含失能角色牌");
        Assert.IsFalse(sharedDeck.ExhaustPile.Exists(c => c.OwnerPersonId == warriorPid), "消耗区不应含失能角色牌");
        string rallyPid = rally != null ? PersonIdOfUnit(battleUI, rally) : null;
        string heroPid = hero != null ? (string)GetMember(hero, "PersonId") : null;
        Assert.IsTrue((rallyPid != null && sharedDeck.Hand.Exists(c => c.OwnerPersonId == rallyPid))
                      || (heroPid != null && sharedDeck.Hand.Exists(c => c.OwnerPersonId == heroPid)),
                      "其余角色牌应保留正常循环");

        // 结束回合 → 下一玩家回合：有效上阵 2 人 → 抽 2；AP 恢复至上限
        int turnBefore = (int)GetProperty(v1, "TurnNumber");
        var endTurnBtn = (Button)GetField(battleUI, "btnEndTurn");
        Assert.IsNotNull(endTurnBtn, "结束回合按钮未绑定");
        endTurnBtn.onClick.Invoke();
        yield return null;
        wait = 0f;
        while ((int)GetProperty(v1, "TurnNumber") == turnBefore && wait < 8f)
        {
            wait += Time.deltaTime;
            yield return null;
        }
        Assert.Greater((int)GetProperty(v1, "TurnNumber"), turnBefore, "结束回合后未进入下一玩家回合");

        var sharedAp = (SharedActionPointPool)GetProperty(v1, "ActionPoints");
        Assert.AreEqual(sharedAp.Max, sharedAp.Current, "下一玩家回合共享行动力应恢复至上限");
        Assert.AreEqual(2, sharedDeck.Hand.Count, "失能一名角色后，下一回合基础抽牌数应减少为 2");
    }

    // ---------- 反射辅助 ----------

    // ---------- Stage 4：敌方多回合持续行动 + 0HP 失能闭环（R15.x） ----------

    /// <summary>
    /// 集成验证：敌人每次 EnemyTurn 恢复行动力并持续攻击（第二、第三次 EnemyTurn 仍攻击）；
    /// 最终通过真实敌方伤害使某我方角色 0HP → Current 失能（无死亡之门）→ 其角色牌移除 → 剩余角色继续。
    /// </summary>
    [UnityTest]
    public IEnumerator EnemyAttacksAcrossTurns_AndDownsPlayer_CurrentRules()
    {
        IEnumerator run = RunEnemyMultiTurnAttack();
        while (true)
        {
            bool moved;
            try
            {
                moved = run.MoveNext();
            }
            catch (System.Exception e)
            {
                Debug.LogError("[PlayTest] 异常：" + e);
                throw;
            }
            if (!moved) break;
            yield return run.Current;
        }
    }

    private IEnumerator RunEnemyMultiTurnAttack()
    {
        SceneManager.LoadScene("SampleScene");
        yield return null;
        yield return null;

        var battleUI = FindInstance("BattleUI");
        var expUI = FindInstance("ExpeditionUI");
        Assert.IsNotNull(battleUI, "BattleUI（正式宿主）未找到");

        var collection = CallStatic("SaveSystem", "NewGame", null);
        Assert.IsNotNull(collection, "NewGame 失败");
        var exp = System.Activator.CreateInstance(GetType("ExpeditionSystem"), new object[] { collection });
        var dataManager = GetDataManager();

        var heroes = (IList)GetMember(collection, "OwnedHeroes");
        if (heroes != null && heroes.Count > 0)
        {
            var hi = heroes[0];
            var hd = Call(dataManager, "GetHeroById", GetMember(hi, "HeroId"));
            if (hd != null) Call(exp, "SelectHero", hi, hd);
        }

        object warriorCard = null;
        object hunterCard = null;
        var charCards = (IList)GetMember(collection, "OwnedCharacterCards");
        Assert.IsNotNull(charCards, "角色卡册为空");
        foreach (var c in charCards)
        {
            string charId = (string)GetMember(c, "CharacterId");
            if (charId == "char_001") warriorCard = c;
            if (charId == "char_002") hunterCard = c;
        }
        Assert.IsNotNull(warriorCard, "未找到战士卡 char_001");
        Assert.IsNotNull(hunterCard, "未找到猎人卡 char_002");
        Call(exp, "AddCharacterCard", warriorCard);
        Call(exp, "AddCharacterCard", hunterCard);

        var confirm = GetType("ExpeditionSystem").GetMethod("ConfirmExpedition");
        Assert.IsNotNull(confirm, "ConfirmExpedition 方法未找到");
        var args = new object[2];
        Assert.IsTrue((bool)confirm.Invoke(exp, args), "确认出征失败");
        var chars = args[0];
        var hero = args[1];

        Call(battleUI, "InitFromExpedition", chars, hero,
            GetProperty(dataManager, "AllCharacters"), GetProperty(dataManager, "AllSkills"),
            StaticMonsterTestData.Build(), null);
        var canvas = GetField(expUI, "battleCanvas");
        if (expUI != null && canvas != null) ((GameObject)canvas).SetActive(true);
        var battleGo = GetProperty(battleUI, "gameObject");
        if (battleGo != null) ((GameObject)battleGo).SetActive(true);
        yield return null;

        Call(battleUI, "StartBattleWithExpedition", null);
        yield return null;

        // 布阵：战士/猎人同排（相邻列），不布英雄（英雄可选，验证英雄不上阵路径）
        var charDataWarrior = Call(dataManager, "GetCharacterById", "char_001");
        var charDataHunter = Call(dataManager, "GetCharacterById", "char_002");
        Assert.IsNotNull(charDataWarrior, "战士角色数据缺失");
        Assert.IsNotNull(charDataHunter, "猎人角色数据缺失");
        Call(battleUI, "PlaceUnit", warriorCard, charDataWarrior, GetField(battleUI, "playerFrontRow"));
        Call(battleUI, "PlaceUnit", hunterCard, charDataHunter, GetField(battleUI, "playerFrontRow"));
        yield return null;

        var pendingCols = GetField(battleUI, "_pendingCols");
        Assert.IsNotNull(pendingCols, "_pendingCols 未初始化");
        var dictSetter = pendingCols.GetType().GetProperty("Item").GetSetMethod();
        Assert.IsNotNull(dictSetter, "_pendingCols 索引器 setter 未找到");
        dictSetter.Invoke(pendingCols, new object[] { GetMember(warriorCard, "CardId"), EnumVal("BattleColumn", 0) });
        dictSetter.Invoke(pendingCols, new object[] { GetMember(hunterCard, "CardId"), EnumVal("BattleColumn", 1) });

        Call(battleUI, "OnStartBattle", null);
        yield return null;

        // 等待首个玩家回合
        var v1 = GetField(battleUI, "v1Runtime");
        Assert.IsNotNull(v1, "BattleV1Runtime 未绑定");
        float wait = 0f;
        while (true)
        {
            var phase = (BattleV1TurnFlow.PhaseType)GetProperty(v1, "TurnPhase");
            if (phase == BattleV1TurnFlow.PhaseType.PlayerTurn) break;
            wait += Time.deltaTime;
            if (wait > 8f) break;
            yield return null;
        }
        Assert.AreEqual(BattleV1TurnFlow.PhaseType.PlayerTurn, (BattleV1TurnFlow.PhaseType)GetProperty(v1, "TurnPhase"), "未进入首个玩家回合");

        var battleSystem = GetField(battleUI, "_battleSystem");
        var bsData = Call(battleSystem, "GetBattleData", null);
        var playerUnits = (IList)GetMember(bsData, "PlayerUnits");
        Assert.AreEqual(2, playerUnits.Count, "应有 2 名我方单位（战士 + 猎人，英雄未上阵）");

        // 连续多个 EnemyTurn：敌人每次恢复行动力并持续攻击 → 某角色 0HP 失能 → 牌移除 → 剩余继续
        var sharedDeck = (SharedDeck)GetProperty(v1, "Deck");
        var endTurnBtn = (Button)GetField(battleUI, "btnEndTurn");
        Assert.IsNotNull(endTurnBtn, "结束回合按钮未绑定");

        object downed = null;
        object survivor = null;
        bool downedOccurred = false;
        int turnsAfterDown = 0;
        int lastTurn = (int)GetProperty(v1, "TurnNumber");
        int maxEnemyTurns = 6;

        for (int t = 0; t < maxEnemyTurns; t++)
        {
            var hpBefore = new List<int>();
            foreach (var u in playerUnits)
                hpBefore.Add((bool)GetMember(u, "IsDown") ? -1 : (int)GetMember(u, "CurrentHP"));

            endTurnBtn.onClick.Invoke();
            yield return null;
            wait = 0f;
            while ((int)GetProperty(v1, "TurnNumber") == lastTurn && wait < 8f)
            {
                wait += Time.deltaTime;
                yield return null;
            }
            Assert.Greater((int)GetProperty(v1, "TurnNumber"), lastTurn, "结束回合后未进入下一玩家回合");
            lastTurn = (int)GetProperty(v1, "TurnNumber");

            // 敌人应持续攻击（至少一个存活玩家 HP 下降），直到我方全员失能
            bool anyHit = false;
            bool anyAlive = false;
            int idx = 0;
            foreach (var u in playerUnits)
            {
                if (!(bool)GetMember(u, "IsDown"))
                {
                    anyAlive = true;
                    if (hpBefore[idx] > 0 && (int)GetMember(u, "CurrentHP") < hpBefore[idx])
                        anyHit = true;
                }
                idx++;
            }
            if (anyAlive)
                Assert.IsTrue(anyHit, "第 " + (t + 1) + " 个 EnemyTurn 敌人应持续攻击（恢复行动力）");

            // 记录首个失能者；失能后再跑 1 个 EnemyTurn 确认剩余角色继续被攻击
            foreach (var u in playerUnits)
            {
                if (!downedOccurred && (bool)GetMember(u, "IsDown"))
                {
                    downed = u;
                    downedOccurred = true;
                }
            }
            if (downedOccurred)
            {
                turnsAfterDown++;
                if (turnsAfterDown >= 2) break; // 失能回合 + 1 个后续回合已确认
            }
        }

        Assert.IsTrue(downedOccurred, "连续多个 EnemyTurn 后应有角色 0HP 失能");
        Assert.IsFalse((bool)GetMember(downed, "InDeathDoor"), "失能不进入死亡之门");

        // 失能角色牌从共享牌库全部牌区移除（OnEndTurn 的 SyncDownedPersonCards 已执行）
        // M1：按失能单位对应的真实 PersonId 移除/断言（不再用模板 SourceId）
        string downedPersonId = PersonIdOfUnit(battleUI, downed);
        Assert.IsFalse(sharedDeck.Hand.Exists(c => c.OwnerPersonId == downedPersonId), "失能角色牌应从手牌移除");
        Assert.IsFalse(sharedDeck.DrawPile.Exists(c => c.OwnerPersonId == downedPersonId), "失能角色牌应从抽牌堆移除");
        Assert.IsFalse(sharedDeck.DiscardPile.Exists(c => c.OwnerPersonId == downedPersonId), "失能角色牌应从弃牌堆移除");

        // 剩余角色继续：仍存活、HP > 0，且后续玩家回合正常
        foreach (var u in playerUnits)
        {
            if (!ReferenceEquals(u, downed))
            {
                survivor = u;
                Assert.IsFalse((bool)GetMember(survivor, "IsDown"), "另一角色应仍具战斗能力");
                Assert.Greater((int)GetMember(survivor, "CurrentHP"), 0, "另一角色 HP 应 > 0");
            }
        }
        Assert.IsNotNull(survivor, "应存在继续战斗的剩余角色");
        Assert.AreEqual(BattleV1TurnFlow.PhaseType.PlayerTurn, (BattleV1TurnFlow.PhaseType)GetProperty(v1, "TurnPhase"), "后续玩家回合应正常");
        Assert.AreEqual(1, sharedDeck.Hand.Count, "仅剩 1 名有效上阵时应抽 1 张（R10.2）");
    }

    // ---------- 反射辅助 ----------

    /// <summary>
    /// Stage 3A 集成验证：V1 玩家回合开始触发 TriggerType.TurnStart 技能。
    /// 村长（char_004，sk_003 鼓舞 TurnStart/AddAttack+2/AdjacentAlly）与战士（char_001）同排相邻时，
    /// 玩家回合开始自动触发鼓舞，战士攻击 +2；改变布阵（战士移走）后下一回合不再加攻（邻接动态变化）。
    /// </summary>
    [UnityTest]
    public IEnumerator V1TurnStart_AdjacentBuff_AndDynamicRedeploy()
    {
        IEnumerator run = RunStage3ATurnStart();
        while (true)
        {
            bool moved;
            try
            {
                moved = run.MoveNext();
            }
            catch (System.Exception e)
            {
                Debug.LogError("[PlayTest] 异常：" + e);
                throw;
            }
            if (!moved) break;
            yield return run.Current;
        }
    }

    private IEnumerator RunStage3ATurnStart()
    {
        SceneManager.LoadScene("SampleScene");
        yield return null;
        yield return null;

        var battleUI = FindInstance("BattleUI");
        var expUI = FindInstance("ExpeditionUI");
        Assert.IsNotNull(battleUI, "BattleUI（正式宿主）未找到");

        var collection = CallStatic("SaveSystem", "NewGame", null);
        Assert.IsNotNull(collection, "NewGame 失败");
        var exp = System.Activator.CreateInstance(GetType("ExpeditionSystem"), new object[] { collection });
        var dataManager = GetDataManager();

        var heroes = (IList)GetMember(collection, "OwnedHeroes");
        if (heroes != null && heroes.Count > 0)
        {
            var hi = heroes[0];
            var hd = Call(dataManager, "GetHeroById", GetMember(hi, "HeroId"));
            if (hd != null) Call(exp, "SelectHero", hi, hd);
        }

        object warriorCard = null;
        object rallyCard = null;
        var charCards = (IList)GetMember(collection, "OwnedCharacterCards");
        Assert.IsNotNull(charCards, "角色卡册为空");
        foreach (var c in charCards)
        {
            string charId = (string)GetMember(c, "CharacterId");
            if (charId == "char_001") warriorCard = c;
            if (charId == "char_004") rallyCard = c;
        }
        Assert.IsNotNull(warriorCard, "未找到战士卡 char_001");
        Assert.IsNotNull(rallyCard, "未找到村长卡 char_004");
        Call(exp, "AddCharacterCard", warriorCard);
        Call(exp, "AddCharacterCard", rallyCard);

        var confirm = GetType("ExpeditionSystem").GetMethod("ConfirmExpedition");
        Assert.IsNotNull(confirm, "ConfirmExpedition 方法未找到");
        var args = new object[2];
        Assert.IsTrue((bool)confirm.Invoke(exp, args), "确认出征失败");
        var chars = args[0];
        var hero = args[1];

        Call(battleUI, "InitFromExpedition", chars, hero,
            GetProperty(dataManager, "AllCharacters"), GetProperty(dataManager, "AllSkills"),
            StaticMonsterTestData.Build(), null);
        var canvas = GetField(expUI, "battleCanvas");
        if (expUI != null && canvas != null) ((GameObject)canvas).SetActive(true);
        var battleGo = GetProperty(battleUI, "gameObject");
        if (battleGo != null) ((GameObject)battleGo).SetActive(true);
        yield return null;

        Call(battleUI, "StartBattleWithExpedition", null);
        yield return null;

        // 战前布阵：战士/村长同排（相邻列），英雄后排
        var charDataWarrior = Call(dataManager, "GetCharacterById", "char_001");
        var charDataRally = Call(dataManager, "GetCharacterById", "char_004");
        Assert.IsNotNull(charDataWarrior, "战士角色数据缺失");
        Assert.IsNotNull(charDataRally, "村长角色数据缺失");
        Call(battleUI, "PlaceUnit", warriorCard, charDataWarrior, GetField(battleUI, "playerFrontRow"));
        Call(battleUI, "PlaceUnit", rallyCard, charDataRally, GetField(battleUI, "playerFrontRow"));
        if (hero != null)
        {
            var heroData3 = Call(dataManager, "GetHeroById", GetMember(hero, "HeroId"));
            if (heroData3 != null)
                Call(battleUI, "PlaceHero", heroData3, hero, GetField(battleUI, "playerBackRow"));
        }
        yield return null;

        // 固定列：战士 Front Left(0)，村长 Front Center(1)（测试环境屏幕 Y 排序不可控，直接覆盖）
        var pendingCols = GetField(battleUI, "_pendingCols");
        Assert.IsNotNull(pendingCols, "_pendingCols 未初始化");
        var dictSetter = pendingCols.GetType().GetProperty("Item").GetSetMethod();
        Assert.IsNotNull(dictSetter, "_pendingCols 索引器 setter 未找到");
        dictSetter.Invoke(pendingCols, new object[] { GetMember(warriorCard, "CardId"), EnumVal("BattleColumn", 0) });
        dictSetter.Invoke(pendingCols, new object[] { GetMember(rallyCard, "CardId"), EnumVal("BattleColumn", 1) });

        Call(battleUI, "OnStartBattle", null);
        yield return null;

        // 等待玩家回合开始
        var v1 = GetField(battleUI, "v1Runtime");
        Assert.IsNotNull(v1, "BattleV1Runtime 未绑定");
        float wait = 0f;
        while (true)
        {
            var phase = (BattleV1TurnFlow.PhaseType)GetProperty(v1, "TurnPhase");
            var deck = (SharedDeck)GetProperty(v1, "Deck");
            if (phase == BattleV1TurnFlow.PhaseType.PlayerTurn && deck != null && deck.Hand.Count > 0) break;
            wait += Time.deltaTime;
            if (wait > 8f) break;
            yield return null;
        }
        Assert.AreEqual(BattleV1TurnFlow.PhaseType.PlayerTurn, (BattleV1TurnFlow.PhaseType)GetProperty(v1, "TurnPhase"), "未进入玩家回合");

        // TurnStart 触发：村长鼓舞使邻接战士攻击 +2（战士 BaseAttack=20 → 22）
        var battleSystem = GetField(battleUI, "_battleSystem");
        var bsData = Call(battleSystem, "GetBattleData", null);
        var playerUnits = (IList)GetMember(bsData, "PlayerUnits");
        object warrior = null;
        foreach (var u in playerUnits)
        {
            if ((string)GetMember(u, "SourceId") == "char_001") warrior = u;
        }
        Assert.IsNotNull(warrior, "未找到战士单位");
        Assert.AreEqual(22, (int)GetMember(warrior, "Attack"), "村长 TurnStart 鼓舞应使邻接战士攻击 +2");

        // 动态：把战士移到后排（与村长 Front 排差 2 → 不邻接），结束回合后下一回合不再加攻
        Call(GetMember(bsData, "PlayerField"), "Deploy", warrior, EnumVal("BattleRow", 2), EnumVal("BattleColumn", 1));
        int beforeMove = (int)GetMember(warrior, "Attack");
        int turnBefore = (int)GetProperty(v1, "TurnNumber");
        var endTurnBtn = (Button)GetField(battleUI, "btnEndTurn");
        Assert.IsNotNull(endTurnBtn, "结束回合按钮未绑定");
        endTurnBtn.onClick.Invoke();
        yield return null;
        wait = 0f;
        while ((int)GetProperty(v1, "TurnNumber") == turnBefore && wait < 8f)
        {
            wait += Time.deltaTime;
            yield return null;
        }
        Assert.Greater((int)GetProperty(v1, "TurnNumber"), turnBefore, "结束回合后未进入下一玩家回合");
        Assert.AreEqual(beforeMove, (int)GetMember(warrior, "Attack"), "改变布阵后邻接结果应动态变化（不再加攻）");
    }

    // ---------- Stage 3A：V1 伤害牌统一攻击距离判定 ----------

    /// <summary>
    /// Stage 3A 集成验证：V1 伤害牌必须通过统一距离判定（BattleSystem.IsInRange，RangeModifier 生效）。
    /// 超距离目标不可结算（牌不离手、不扣费、无伤害）；RangeModifier+1 后可结算。
    /// </summary>
    [UnityTest]
    public IEnumerator V1DamageCard_RangeCheck_BlocksOutOfRange()
    {
        IEnumerator run = RunStage3ACardRange();
        while (true)
        {
            bool moved;
            try
            {
                moved = run.MoveNext();
            }
            catch (System.Exception e)
            {
                Debug.LogError("[PlayTest] 异常：" + e);
                throw;
            }
            if (!moved) break;
            yield return run.Current;
        }
    }

    private IEnumerator RunStage3ACardRange()
    {
        SceneManager.LoadScene("SampleScene");
        yield return null;
        yield return null;

        var battleUI = FindInstance("BattleUI");
        var expUI = FindInstance("ExpeditionUI");
        Assert.IsNotNull(battleUI, "BattleUI（正式宿主）未找到");

        var collection = CallStatic("SaveSystem", "NewGame", null);
        Assert.IsNotNull(collection, "NewGame 失败");
        var exp = System.Activator.CreateInstance(GetType("ExpeditionSystem"), new object[] { collection });
        var dataManager = GetDataManager();

        var heroes = (IList)GetMember(collection, "OwnedHeroes");
        if (heroes != null && heroes.Count > 0)
        {
            var hi = heroes[0];
            var hd = Call(dataManager, "GetHeroById", GetMember(hi, "HeroId"));
            if (hd != null) Call(exp, "SelectHero", hi, hd);
        }

        object warriorCard = null;
        var charCards = (IList)GetMember(collection, "OwnedCharacterCards");
        Assert.IsNotNull(charCards, "角色卡册为空");
        foreach (var c in charCards)
        {
            if ((string)GetMember(c, "CharacterId") == "char_001") { warriorCard = c; break; }
        }
        Assert.IsNotNull(warriorCard, "未找到战士卡 char_001");
        Call(exp, "AddCharacterCard", warriorCard);

        var confirm = GetType("ExpeditionSystem").GetMethod("ConfirmExpedition");
        Assert.IsNotNull(confirm, "ConfirmExpedition 方法未找到");
        var args = new object[2];
        Assert.IsTrue((bool)confirm.Invoke(exp, args), "确认出征失败");
        var chars = args[0];
        var hero = args[1];

        // WP-Event-1D-R：Range 契约需要**确定的敌方站位**（阻挡排数必须恰好为 1）。
        // fixture = 恰好 1 只敌人，位于敌方 Back / Center（物理最远排）：唯一的“阻挡”来自玩家侧英雄（Front）。
        // 不得再用「整个 Monster Registry 当本场敌人」：Legacy 列表路径只部署前 3 只，其余单位留在
        // EnemyUnits 且 Row 为默认排，会被 BattleSystem.GetBlockingRowCount（按单位自身 Row 统计阻挡排）
        // 计入阻挡，令 RangeModifier 判定失真。
        var monsterCatalog = StaticMonsterTestData.Build();
        Call(battleUI, "BindActiveEnemyRoster",
            EnemyRosterTestFixture.NewRoster(
                "fixture_range_contract",
                new[] { EnemyRosterTestFixture.MonsterIdAt(monsterCatalog, 0) },
                2, 1));   // 敌方 Back / Center（1 只 → Center 列合法）
        Call(battleUI, "InitFromExpedition", chars, hero,
            GetProperty(dataManager, "AllCharacters"), GetProperty(dataManager, "AllSkills"),
            monsterCatalog, null);
        var canvas = GetField(expUI, "battleCanvas");
        if (expUI != null && canvas != null) ((GameObject)canvas).SetActive(true);
        var battleGo = GetProperty(battleUI, "gameObject");
        if (battleGo != null) ((GameObject)battleGo).SetActive(true);
        yield return null;

        Call(battleUI, "StartBattleWithExpedition", null);
        yield return null;

        // 战前布阵：战士前排，英雄后排
        var charDataWarrior = Call(dataManager, "GetCharacterById", "char_001");
        Assert.IsNotNull(charDataWarrior, "战士角色数据缺失");
        Call(battleUI, "PlaceUnit", warriorCard, charDataWarrior, GetField(battleUI, "playerFrontRow"));
        if (hero != null)
        {
            var heroData3 = Call(dataManager, "GetHeroById", GetMember(hero, "HeroId"));
            if (heroData3 != null)
                Call(battleUI, "PlaceHero", heroData3, hero, GetField(battleUI, "playerBackRow"));
        }
        yield return null;

        Call(battleUI, "OnStartBattle", null);
        yield return null;

        // 等待玩家回合开始
        var v1 = GetField(battleUI, "v1Runtime");
        Assert.IsNotNull(v1, "BattleV1Runtime 未绑定");
        float wait = 0f;
        while (true)
        {
            var phase = (BattleV1TurnFlow.PhaseType)GetProperty(v1, "TurnPhase");
            var deck = (SharedDeck)GetProperty(v1, "Deck");
            if (phase == BattleV1TurnFlow.PhaseType.PlayerTurn && deck != null && deck.Hand.Count > 0) break;
            wait += Time.deltaTime;
            if (wait > 8f) break;
            yield return null;
        }
        Assert.AreEqual(BattleV1TurnFlow.PhaseType.PlayerTurn, (BattleV1TurnFlow.PhaseType)GetProperty(v1, "TurnPhase"), "未进入玩家回合");

        var battleSystem = GetField(battleUI, "_battleSystem");
        var bsData = Call(battleSystem, "GetBattleData", null);
        var playerUnits = (IList)GetMember(bsData, "PlayerUnits");
        object warrior = null;
        object heroUnit = null;
        string heroSourceId = hero != null ? (string)GetMember(hero, "HeroId") : null;
        foreach (var u in playerUnits)
        {
            string sid = (string)GetMember(u, "SourceId");
            if (sid == "char_001") warrior = u;
            if (heroSourceId != null && sid == heroSourceId) heroUnit = u;
        }
        Assert.IsNotNull(warrior, "未找到战士单位");
        Assert.IsNotNull(heroUnit, "未找到英雄单位");

        // 布置为"超距离"：英雄移到前排（物理2 作阻挡），战士移到后排（物理0），敌方在后排（物理5）
        var field = GetMember(bsData, "PlayerField");
        Call(field, "Deploy", heroUnit, EnumVal("BattleRow", 0), EnumVal("BattleColumn", 1));
        Call(field, "Deploy", warrior, EnumVal("BattleRow", 2), EnumVal("BattleColumn", 1));

        var enemyUnits = (IList)GetMember(bsData, "EnemyUnits");
        object enemyTarget = null;
        foreach (var u in enemyUnits)
        {
            if (!(bool)GetMember(u, "IsDown")) { enemyTarget = u; break; }
        }
        Assert.IsNotNull(enemyTarget, "未找到存活敌方单位");

        // WP-Event-1D-R：Range 契约前置（确定性 fixture）——
        // 本场敌方 = fixture EnemyRoster（1 只），且位于敌方 Back（Row=2）/ Center（Column=1）。
        Assert.AreEqual(1, enemyUnits.Count, "本场敌方数量必须等于 fixture EnemyRoster（1 只）");
        Assert.AreEqual(2, (int)GetMember(enemyTarget, "Row"), "Range 契约前置：目标必须在敌方 Back 排");
        Assert.AreEqual(1, (int)GetMember(enemyTarget, "Column"), "Range 契约前置：目标必须在 Center 列");

        // 构造 V1 伤害牌（owner=战士 char_001），加入共享手牌
        var sharedDeck = (SharedDeck)GetProperty(v1, "Deck");
        var sharedAp = (SharedActionPointPool)GetProperty(v1, "ActionPoints");
        var card = new BattleCard
        {
            CardId = "stage3a_test_dmg",
            TemplateId = "bcard_sword",
            DisplayName = "Stage3A测试伤害",
            CardKind = BattleCardKind.Character,
            OwnerPersonId = "char_001",
            Cost = 1,
            Effect = BattleCardEffectType.Damage,
            EffectValue = 5,
            TargetScope = BattleCardTargetScope.EnemyUnit,
            RangeModifier = 0
        };
        sharedDeck.Hand.Add(card);

        // 超距离（英雄 Front 阻挡，blocking=1 ≥ AttackRange 1）：不可结算
        int enemyHpBefore = (int)GetMember(enemyTarget, "CurrentHP");
        int apBefore = sharedAp.Current;
        int handBefore = sharedDeck.Hand.Count;
        Call(battleUI, "PlayV1Card", card, enemyTarget);
        Assert.AreEqual(enemyHpBefore, (int)GetMember(enemyTarget, "CurrentHP"), "超距离伤害牌不应造成伤害");
        Assert.AreEqual(handBefore, sharedDeck.Hand.Count, "超距离伤害牌不应离手");
        Assert.AreEqual(apBefore, sharedAp.Current, "超距离伤害牌不应扣费");

        // RangeModifier=1：可跨 1 个阻挡排 → 结算成功
        card.RangeModifier = 1;
        int enemyHpAfter = (int)GetMember(enemyTarget, "CurrentHP");
        Call(battleUI, "PlayV1Card", card, enemyTarget);
        Assert.Less((int)GetMember(enemyTarget, "CurrentHP"), enemyHpAfter, "RangeModifier 生效后应结算伤害");
        Assert.IsFalse(sharedDeck.Hand.Contains(card), "结算后牌应离手（进弃牌堆）");
    }

    // ---------- 反射辅助 ----------

    // ---------- 反射辅助 ----------

    // ---------- 反射辅助 ----------

    private static int CountHandCards(object battleUI)
    {
        var container = (RectTransform)GetField(battleUI, "handContainer");
        if (container == null) return 0;
        int count = 0;
        foreach (Transform child in container)
        {
            if (child != null && child.name.StartsWith("V1Card_")) count++;
        }
        return count;
    }

    private static int CountDistinctHandPositions(object battleUI)
    {
        var container = (RectTransform)GetField(battleUI, "handContainer");
        if (container == null) return 0;
        var positions = new System.Collections.Generic.HashSet<float>();
        foreach (Transform child in container)
        {
            if (child != null && child.name.StartsWith("V1Card_")) positions.Add(child.localPosition.x);
        }
        return positions.Count;
    }

    private static System.Type GetType(string name) => System.Type.GetType(name + ", Assembly-CSharp");

    private static object Call(object target, string method, params object[] args)
    {
        if (target == null) return null;
        var m = target.GetType().GetMethod(method, Flags);
        return m != null ? m.Invoke(target, args) : null;
    }

    private static object CallStatic(string typeName, string method, object[] args)
    {
        var t = GetType(typeName);
        var m = t.GetMethod(method, Flags);
        return m != null ? m.Invoke(null, args) : null;
    }

    private static object GetField(object target, string name)
    {
        if (target == null) return null;
        var f = target.GetType().GetField(name, Flags);
        return f != null ? f.GetValue(target) : null;
    }

    private static object GetProperty(object target, string name)
    {
        if (target == null) return null;
        var p = target.GetType().GetProperty(name, Flags);
        return p != null ? p.GetValue(target, null) : null;
    }

    /// <summary>先按字段查找，再按属性查找（PlayerCollection.Owned* 为 public 字段）</summary>
    private static object GetMember(object target, string name)
    {
        if (target == null) return null;
        var f = target.GetType().GetField(name, Flags);
        if (f != null) return f.GetValue(target);
        var p = target.GetType().GetProperty(name, Flags);
        return p != null ? p.GetValue(target, null) : null;
    }

    private static object GetDataManager()
    {
        var t = GetType("DataManager");
        var p = t.GetProperty("Instance", Flags);
        return p.GetValue(null, null);
    }

    /// <summary>按枚举整数值创建 Assembly-CSharp 枚举（BattleRow/BattleColumn/TriggerType 等）</summary>
    private static object EnumVal(string enumTypeName, int v)
    {
        var t = GetType(enumTypeName);
        Assert.IsNotNull(t, "枚举类型不存在：" + enumTypeName);
        return System.Enum.ToObject(t, v);
    }

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
}

