using System;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

/// <summary>
/// **WP-Encounter-1C §16：真实产品路径 PlayMode 验证**（不是独立 Debug Runner）。
///
/// 走的路径与玩家完全一致：
///   SampleScene → MainMenuUI.OnContinue（Town）→ ExpeditionUI.ShowExpeditionFromTown →
///   ExpeditionSystem.AddCharacterCard → AdventureManager + StartProgression（Departure 真实 Food→Carry）→
///   **ExpeditionUI.HandleNodeClick（= 玩家点击 Journey 战斗节点时的同一方法）** →
///   Active EnemyRoster 冻结 + BindActiveEnemyRoster + BattleUI.InitFromExpedition + StartBattleWithExpedition →
///   战前 Prep → PlaceUnit → OnStartBattle → 真实 BattleData.EnemyUnits → OnReturnFromBattle（真实 Resolve）。
///
/// 验证目标（制作人发现的旧缺陷「战前看似 3 只、开战变 6 只、单位重叠」）：
///   - Prep 敌人数 = Active EnemyRoster 数（不再只显示前 3 只）；
///   - 开战后 EnemyUnits 数、UnitId（= EnemyInstanceId）、SourceId（= MonsterId）、(排, 列) 与阵容逐项一致；
///   - 每只敌方单位在 Enemy Board 上唯一占格（无重叠）、无「创建了但没有位置」的单位；
///   - Battle 消费的是 Prep 消费的**同一份** EnemyRoster 对象（会话内不重抽）；
///   - Battle 往返（Resolve）后 Active EnemyRoster 清除、Progression 仍可继续。
///
/// 程序集边界：BattleUI / AdventureManager / ExpeditionUI 位于 Assembly-CSharp（asmdef 不可引用）→ 反射访问。
/// </summary>
public class EncounterProductPathPlayTests
{
    static void CapturePresentationEvidence(string name)
    {
        var directory = Environment.GetEnvironmentVariable("RRPG_PRESENTATION_EVIDENCE");
        if (!string.IsNullOrEmpty(directory)) ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(directory, name));
    }
    static bool PlayOneCurrentHandCard(object battleUI, object runtime, SharedDeck deck)
    {
        var hand = (RectTransform)GetMember(runtime, "handContainer");
        foreach (var card in deck.Hand.ToArray())
        {
            if (!(bool)Call(runtime, "CanPlayCard", card)) continue;
            var host = hand.Find("V1Card_" + card.CardId);
            var button = host != null ? host.GetComponent<Button>() : null;
            if (button == null) continue;
            button.onClick.Invoke();
            foreach (string rowName in new[] { "enemyFrontRow", "enemyMiddleRow", "enemyBackRow", "playerFrontRow", "playerMiddleRow", "playerBackRow" })
            {
                var row = (RectTransform)GetMember(battleUI, rowName);
                foreach (Transform child in row)
                {
                    if (!deck.Hand.Contains(card)) return true;
                    if (!child.name.StartsWith("Card_")) continue;
                    var targetButton = child.GetComponent<Button>();
                    if (targetButton != null && targetButton.interactable) targetButton.onClick.Invoke();
                }
            }
            var owner = ((Component)battleUI).gameObject;
            foreach (var marker in owner.GetComponentsInChildren(Cs("BattleRuleCellMarker"), true))
            {
                if (!deck.Hand.Contains(card)) return true;
                var markerButton = marker.GetComponent<Button>();
                if (markerButton != null && markerButton.interactable) markerButton.onClick.Invoke();
            }
            if (!deck.Hand.Contains(card)) return true;
            button.onClick.Invoke(); // Cancel an unplayed selection before trying the next real card.
        }
        return false;
    }
    /// <summary>
    /// 防御：失败路径测试会临时打开 <see cref="LogAssert.ignoreFailingMessages"/>；
    /// 若该测试中途 Assert 失败，必须在 TearDown 复位，避免把「忽略错误日志」泄漏给其它 PlayMode 测试。
    /// </summary>
    [TearDown]
    public void ResetLogAssertAfterEachTest()
    {
        LogAssert.ignoreFailingMessages = false;
    }

    private const BindingFlags F =
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

    private const int InitialFood = 100;

    private static Type Cs(string name) => Type.GetType(name + ", Assembly-CSharp");

    private static object FindInstance(string typeName)
    {
        var type = Cs(typeName);
        Assert.IsNotNull(type, "类型不存在：" + typeName);
        foreach (var o in UnityEngine.Object.FindObjectsOfType(type, true))
            if (o != null) return o;
        return null;
    }

    private static object Call(object target, string method, params object[] args)
    {
        Assert.IsNotNull(target, "调用目标为空：" + method);
        var m = target.GetType().GetMethod(method, F);
        Assert.IsNotNull(m, "方法不存在：" + method);
        return m.Invoke(target, args);
    }

    private static object CallStatic(string typeName, string method, params object[] args)
    {
        var type = Cs(typeName);
        foreach (var m in type.GetMethods(F))
        {
            if (m.Name != method) continue;
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

    private static object GetMember(object target, string name)
    {
        Assert.IsNotNull(target, "读取目标为空：" + name);
        var f = target.GetType().GetField(name, F);
        if (f != null) return f.GetValue(target);

        var p = target.GetType().GetProperty(name, F);
        Assert.IsNotNull(p, "成员不存在：" + name + " @ " + target.GetType().FullName);
        return p.GetValue(target, null);
    }

    private static void SetMember(object target, string name, object value)
    {
        var f = target.GetType().GetField(name, F);
        if (f != null) { f.SetValue(target, value); return; }

        var p = target.GetType().GetProperty(name, F);
        Assert.IsNotNull(p, "成员不存在：" + name);
        p.SetValue(target, value, null);
    }

    private static object StaticMember(string typeName, string name)
    {
        var type = Cs(typeName);
        var f = type.GetField(name, F);
        if (f != null) return f.GetValue(null);

        var p = type.GetProperty(name, F);
        Assert.IsNotNull(p, "静态成员不存在：" + typeName + "." + name);
        return p.GetValue(null, null);
    }

    private static object DataManagerInstance() =>
        Cs("DataManager").GetProperty("Instance", BindingFlags.Public | BindingFlags.Static).GetValue(null);

    private static void ResetBridgeAndDeleteSave()
    {
        CallStatic("VillageRuntimeBridge", "ResetForTests");
        string path = (string)CallStatic("SaveSystem", "GetSavePath");
        if (!string.IsNullOrEmpty(path) && System.IO.File.Exists(path)) System.IO.File.Delete(path);
    }

    private static int VillageFood() =>
        (int)StaticMember("VillageRuntimeBridge", "Resources").GetType()
            .GetProperty("Food", BindingFlags.Public | BindingFlags.Instance)
            .GetValue(StaticMember("VillageRuntimeBridge", "Resources"), null);

    private static void AddVillageFood(int amount) =>
        Call(StaticMember("VillageRuntimeBridge", "Resources"), "AddFood", amount);

    // ==================== Expedition / Progression 工具 ====================

    private static object NewAdventureFromExpedition(object collection, object expedition)
    {
        var dataManager = DataManagerInstance();
        var ctor = Cs("AdventureManager").GetConstructor(new[]
        {
            Cs("PlayerCollection"),
            GetMember(expedition, "SelectedCharacters").GetType(),
            Cs("HeroInstance"),
            GetMember(dataManager, "AllCharacters").GetType(),
            GetMember(dataManager, "AllSkills").GetType(),
            StaticMonsterTestData.Build().GetType(),
            Cs("RunConfig")
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

    private static List<string> ReadAvailableIds(object resolver)
    {
        var ids = new List<string>();
        var nodes = (IEnumerable)Call(resolver, "GetAvailableNextNodes");
        if (nodes == null) return ids;

        foreach (var node in nodes)
        {
            if (node == null) continue;
            ids.Add((string)GetMember(node, "NodeId"));
        }
        return ids;
    }

    private static string KindOfNode(object resolver, string nodeId)
    {
        var graph = GetMember(GetMember(resolver, "State"), "Graph");
        var method = graph.GetType().GetMethod("TryGetNode");
        var args = new object[] { nodeId, null };
        method.Invoke(graph, args);
        Assert.IsNotNull(args[1], "node 不存在：" + nodeId);
        return GetMember(args[1], "NodeKind").ToString();
    }

    // ==================== EnemyRoster / 敌方单位读取 ====================

    private static List<string> RosterInstanceIds(object roster)
    {
        var ids = new List<string>();
        foreach (var entry in (IEnumerable)GetMember(roster, "Entries"))
            ids.Add((string)GetMember(entry, "EnemyInstanceId"));
        return ids;
    }

    private static List<string> RosterMonsterIds(object roster)
    {
        var ids = new List<string>();
        foreach (var entry in (IEnumerable)GetMember(roster, "Entries"))
            ids.Add((string)GetMember(entry, "MonsterId"));
        return ids;
    }

    private static List<string> RosterCells(object roster)
    {
        var cells = new List<string>();
        foreach (var entry in (IEnumerable)GetMember(roster, "Entries"))
        {
            // 数值归一化：Encounter 排 / 列与 Battle 排 / 列逐值一致（0=Front/Left … 2=Back/Right）。
            cells.Add(((int)GetMember(entry, "Row")) + ":" + ((int)GetMember(entry, "Column")));
        }
        return cells;
    }

    private static List<GameObject> EnemyPrepCards(object battleUI)
    {
        var cards = new List<GameObject>();
        foreach (string rowName in new[] { "enemyFrontRow", "enemyMiddleRow", "enemyBackRow" })
        {
            var row = (RectTransform)GetMember(battleUI, rowName);
            if (row == null) continue;

            foreach (Transform child in row)
                if (child != null && child.name.StartsWith("EnemyPrep_")) cards.Add(child.gameObject);
        }
        return cards;
    }

    private static int EnteredCount(object adventure)
    {
        var state = GetMember(adventure, "ProgressionState");
        var entered = (ICollection)Call(state, "GetEnteredNodeIds");
        return entered.Count;
    }

    private static int SupplyCarry(object adventure)
    {
        var state = GetMember(adventure, "ProgressionState");
        return (int)GetMember(GetMember(state, "Supply"), "CurrentCarryUnits");
    }

    private static List<GameObject> EnemyBattleCards(object battleUI)
    {
        var cards = new List<GameObject>();
        foreach (string rowName in new[] { "enemyFrontRow", "enemyMiddleRow", "enemyBackRow" })
        {
            var row = (RectTransform)GetMember(battleUI, rowName);
            if (row == null) continue;

            foreach (Transform child in row)
                if (child != null && child.name.StartsWith("Card_")) cards.Add(child.gameObject);
        }
        return cards;
    }

    // ==================== 真实产品路径 ====================

    private object _adventure;
    private object _expedition;
    private object _expUI;
    private object _battleUI;
    private object _playerData;
    private object _firstCard;

    /// <summary>真实产品链引导：SampleScene → Town → Expedition Prep → Confirm → Departure → StartProgression。</summary>
    private IEnumerator BootstrapProductExpedition()
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

        var mainMenu = FindInstance("MainMenuUI");
        Assert.IsNotNull(mainMenu, "MainMenuUI 应存在");
        Call(mainMenu, "OnContinue");
        yield return null;
        yield return null;

        var town = FindInstance("TownUI");
        Assert.IsNotNull(town, "TownUI 应存在");
        _playerData = GetMember(town, "PlayerData");
        Assert.IsNotNull(_playerData, "TownUI.PlayerData 应存在");

        _expUI = FindInstance("ExpeditionUI");
        Assert.IsNotNull(_expUI, "ExpeditionUI 应存在");
        _battleUI = FindInstance("BattleUI");
        Assert.IsNotNull(_battleUI, "BattleUI（正式宿主）应存在");

        Call(_expUI, "ShowExpeditionFromTown", _playerData);
        yield return null;
        yield return null;

        _expedition = GetMember(_expUI, "_expedition");
        Assert.IsNotNull(_expedition, "ExpeditionSystem 应已创建");

        object firstCard = null;
        foreach (var card in (IEnumerable)GetMember(_playerData, "OwnedCharacterCards")) { firstCard = card; break; }
        Assert.IsNotNull(firstCard, "需要至少一张 owned 角色卡");
        _firstCard = firstCard;
        Call(_expedition, "AddCharacterCard", firstCard);
        yield return null;

        _adventure = NewAdventureFromExpedition(_playerData, _expedition);
        int party = 0;
        foreach (var _ in (IEnumerable)Call(_adventure, "GetExpeditionPersonIds")) party++;
        Assert.GreaterOrEqual(party, 1, "出征队伍应至少有 1 人");

        AddVillageFood(InitialFood);
        int carryPerPerson = (int)StaticMember("VillageRuntimeBridge", "ResourceCarryPerPerson");
        int rate = (int)StaticMember("VillageRuntimeBridge", "ResourcePerPersonDailyConsumptionRate");
        int actualCarry = (int)CallStatic("VillageRuntimeBridge", "CarryFoodForDeparture", party * carryPerPerson);
        Assert.Greater(actualCarry, 0, "Departure 应发生真实 Food → Carry 转移");

        object elapsedLookup = CallStatic("LegacyNodeElapsedDaysTestPolicy", "BuildLookup",
            GetMember(_adventure, "MapGen"));
        Call(_adventure, "StartProgression", 4, 3, party, actualCarry, rate, elapsedLookup);

        SetMember(_expUI, "_currentAdventure", _adventure);
        Assert.IsNotNull(GetMember(_adventure, "Progression"), "Progression 会话必须建立");
    }

    private string FirstBattleLikeNodeId()
    {
        foreach (string id in ReadAvailableIds(GetMember(_adventure, "Progression")))
        {
            string kind = KindOfNode(GetMember(_adventure, "Progression"), id);
            if (kind == "Battle" || kind == "Elite" || kind == "Boss") return id;
        }
        return null;
    }

    [UnityTest]
    public IEnumerator ProductBattleNode_ConsumesFrozenRoster_PrepEqualsBattle_AndResolveClears()
    {
        IEnumerator bootstrap = BootstrapProductExpedition();
        while (bootstrap.MoveNext()) yield return bootstrap.Current;

        object adventure = _adventure;
        object expUI = _expUI;
        object battleUI = _battleUI;

        // ---------- 选一个 Battle 类节点（Journey 上玩家会点的那个） ----------
        string battleNodeId = FirstBattleLikeNodeId();
        Assert.IsNotNull(battleNodeId, "当前地图必须有可进入的 Battle 类节点（否则本测试无意义）");

        // WP-Encounter-1C-R（R1）：点击前必须处于「未进入 + 无 ActiveEncounter」
        Assert.IsNull(GetMember(adventure, "ActiveEncounter"), "点击前不得存在 Active Encounter（R1 原子性前置）");
        Assert.IsFalse((bool)Call(GetMember(adventure, "ProgressionState"), "IsNodeEntered", battleNodeId),
            "点击前节点不得处于 Entered");

        // ---------- 真实产品入口：ExpeditionUI.HandleNodeClick ----------
        Call(expUI, "HandleNodeClick", adventure, battleNodeId, battleUI);
        yield return null;
        yield return null;

        // R1：Encounter 预生成成功之后才真正 Enter（顺序保证）
        Assert.IsTrue((bool)Call(GetMember(adventure, "ProgressionState"), "IsNodeEntered", battleNodeId),
            "点击后节点必须已进入（预生成成功后 Enter）");

        // 1) Active EnemyRoster 已冻结，且来自本节点
        object activeEncounter = GetMember(adventure, "ActiveEncounter");
        Assert.IsNotNull(activeEncounter, "产品进入 Battle 节点后必须存在 Active Encounter");
        Assert.AreEqual(battleNodeId, GetMember(activeEncounter, "NodeId"), "Active Encounter 必须属于当前节点");

        object roster = GetMember(activeEncounter, "Roster");
        Assert.IsNotNull(roster, "Active EnemyRoster 必须已冻结");
        int rosterCount = (int)GetMember(roster, "Count");
        Assert.Greater(rosterCount, 0, "本场敌方阵容不得为空");

        var rosterInstanceIds = RosterInstanceIds(roster);
        var rosterMonsterIds = RosterMonsterIds(roster);
        var rosterCells = RosterCells(roster);
        Assert.AreEqual(rosterCount, rosterInstanceIds.Count);
        CollectionAssert.AllItemsAreUnique(rosterCells, "阵容内每格最多 1 只");

        // 2) Prep：敌人数 = 阵容数（旧缺陷：战前只显示 3 只）
        var prepCards = EnemyPrepCards(battleUI);
        Assert.AreEqual(rosterCount, prepCards.Count,
            "Prep 敌人数必须等于本场阵容数（" + rosterCount + "）；实际 " + prepCards.Count);

        foreach (string instanceId in rosterInstanceIds)
        {
            Assert.IsTrue(prepCards.Any(c => c != null && c.name == "EnemyPrep_" + instanceId),
                "Prep 必须为每只敌人建立本场身份卡：EnemyPrep_" + instanceId);
        }

        // 3) 战前部署我方 → 开战（真实 OnStartBattle）
        var frontRow = (RectTransform)GetMember(battleUI, "playerFrontRow");
        Assert.IsNotNull(frontRow, "playerFrontRow 未绑定");
        var dataManager = DataManagerInstance();
        var charData = Call(dataManager, "GetCharacterById", (string)GetMember(_firstCard, "CharacterId"));
        Assert.IsNotNull(charData, "角色数据缺失：" + GetMember(_firstCard, "CharacterId"));
        Call(battleUI, "PlaceUnit", _firstCard, charData, frontRow);
        yield return null;

        Call(battleUI, "OnStartBattle");
        yield return null;
        yield return null;

        object battleSystem = GetMember(battleUI, "_battleSystem");
        Assert.IsNotNull(battleSystem, "BattleSystem 必须已创建（正式战斗已开始）");

        var battleData = Call(battleSystem, "GetBattleData");
        var enemies = (IList)GetMember(battleData, "EnemyUnits");
        Assert.AreEqual(rosterCount, enemies.Count, "开战后敌人数必须等于 Prep 敌人数（不换怪、不丢怪）");

        var battleInstanceIds = new List<string>();
        var battleMonsterIds = new List<string>();
        var battleCells = new List<string>();
        var enemyField = GetMember(battleData, "EnemyField");

        foreach (var unit in enemies)
        {
            battleInstanceIds.Add((string)GetMember(unit, "UnitId"));
            battleMonsterIds.Add((string)GetMember(unit, "SourceId"));

            int row = (int)GetMember(unit, "Row");
            int column = (int)GetMember(unit, "Column");
            battleCells.Add(row + ":" + column);

            Assert.IsTrue((bool)Call(enemyField, "Contains", unit),
                "敌方单位必须占据 Enemy Board 格（不得存在无位置单位）：" + GetMember(unit, "UnitId"));
            Assert.AreSame(unit, Call(enemyField, "GetUnitAt", Enum.ToObject(Cs("BattleRow"), row),
                Enum.ToObject(Cs("BattleColumn"), column)), "Board 占位必须与单位坐标一致");
        }

        CollectionAssert.AreEquivalent(rosterInstanceIds, battleInstanceIds,
            "每个敌方 BattleUnit.UnitId 必须是本场 EnemyInstanceId");
        CollectionAssert.AreEquivalent(rosterMonsterIds, battleMonsterIds,
            "每个敌方 BattleUnit.SourceId 必须是模板 MonsterId（美术 / 数值 / AI 键）");
        CollectionAssert.AreEquivalent(rosterCells, battleCells, "开战后站位必须与本场阵容逐项一致");
        CollectionAssert.AllItemsAreUnique(battleCells, "敌方单位不得重叠占格");

        Assert.AreSame(roster, GetMember(battleUI, "ActiveEnemyRoster"),
            "Battle 必须消费 Prep 消费的同一份 EnemyRoster（会话内不重抽、不换怪）");

        // Current TMP surfaces consume the exact Runtime facts in this real Town → Journey → Battle path.
        var runtime = GetMember(battleUI, "v1Runtime");
        var deck = (SharedDeck)GetMember(runtime, "Deck");
        var pool = (SharedActionPointPool)GetMember(runtime, "ActionPoints");
        var hud = ((Component)runtime).GetComponent(Cs("BattleHudPresenter"));
        Call(hud, "Refresh");
        Assert.AreEqual(pool.Current + "/" + pool.Max, ((TMP_Text)GetMember(runtime, "txtSharedActionPoint")).text);
        Assert.AreEqual(GetMember(runtime, "TurnNumber").ToString(), ((TMP_Text)GetMember(hud, "turnHost")).text);
        Assert.AreEqual(deck.DrawPileCount.ToString(), ((TMP_Text)GetMember(hud, "drawPileHost")).text);
        Assert.AreEqual(deck.DiscardPileCount.ToString(), ((TMP_Text)GetMember(hud, "discardPileHost")).text);
        Assert.IsFalse(((TMP_Text)GetMember(battleUI, "infoText")).gameObject.activeSelf);
        var views = ((Component)runtime).GetComponentsInChildren(Cs("BattleCardView"), true);
        Assert.Greater(views.Length, 0);
        foreach (var view in views)
        {
            Assert.IsNotNull(GetMember(view, "ownerPortraitImage"));
            Assert.IsFalse(((TMP_Text)GetMember(view, "ownerText")).enabled);
            Assert.IsFalse(((TMP_Text)GetMember(view, "flagsText")).enabled);
        }

        // Exercise two real authored rule templates through an existing hand view and real Battle facts.
        // Test-only preview restores the binding and Attack before gameplay continues.
        var handView = views[0]; var originalCard = (BattleCard)GetMember(handView, "_card");
        var factsProvider = GetMember(runtime, "RuleContextProvider");
        var peopleProvider = GetMember(runtime, "PresentationPeopleProvider");
        var ownerId = deck.Hand.First(c => c.CardKind == BattleCardKind.Character).OwnerPersonId;
        var attackData = Resources.LoadAll<BattleCardData>("ScriptableObjects/BattleCards").First(c => c.CardId == "bcard_solo_hunt");
        var fixedData = Resources.LoadAll<BattleCardData>("ScriptableObjects/BattleCards").First(c => c.CardId == "bcard_break_poise");
        var attackCard = BattleCard.FromEntry(new BattlePersonCardEntry(attackData, ownerId, 1), 0);
        var fixedCard = BattleCard.FromEntry(new BattlePersonCardEntry(fixedData, ownerId, 1), 0);
        var caster = Call(battleUI, "GetV1CardCaster", attackCard); int originalAttack = (int)GetMember(caster, "Attack");
        try
        {
            Call(handView, "BindPresentation", fixedCard, null, factsProvider, peopleProvider);
            StringAssert.Contains("<b>4</b>", ((TMP_Text)GetMember(handView, "descriptionText")).text);
            SetMember(caster, "Attack", 8);
            Call(handView, "BindPresentation", attackCard, null, factsProvider, peopleProvider);
            StringAssert.Contains("<b>8</b>", ((TMP_Text)GetMember(handView, "descriptionText")).text);
            var description = (TMP_Text)GetMember(handView, "descriptionText"); description.ForceMeshUpdate(true, true);
            Assert.Greater(description.textInfo.linkCount, 0);
            Assert.AreEqual("keyword:damage", description.textInfo.linkInfo[0].GetLinkID());
            Assert.IsNotNull(((Image)GetMember(handView, "ownerPortraitImage")).sprite);
            CapturePresentationEvidence("product-battle-tmp-attack8.png");
            yield return null; yield return null;
            SetMember(caster, "Attack", 10); Call(handView, "RefreshPresentation");
            StringAssert.Contains("<b>10</b>", ((TMP_Text)GetMember(handView, "descriptionText")).text);
            CapturePresentationEvidence("product-battle-tmp-attack10.png");
            yield return null; yield return null;
        }
        finally
        {
            SetMember(caster, "Attack", originalAttack);
            Call(handView, "BindPresentation", originalCard, null, factsProvider, peopleProvider);
        }

        // 4) 敌方 Battle 卡：每只都有 Card_<EnemyInstanceId>
        var battleCards = EnemyBattleCards(battleUI);
        Assert.AreEqual(rosterCount, battleCards.Count, "开战后敌方卡数必须等于本场敌人数");
        foreach (string instanceId in rosterInstanceIds)
        {
            Assert.IsTrue(battleCards.Any(c => c != null && c.name == "Card_" + instanceId),
                "每只敌方单位必须有唯一卡（Card_<EnemyInstanceId>）：" + instanceId);
        }

        // Continue this same real product session through Play and EndTurn.
        // 一人初始抽一张，随机牌可能要求其它手牌/单位而合法不可打；测试补抽真实牌，不能把随机不可打当入口失败。
        bool played=PlayOneCurrentHandCard(battleUI,runtime,deck);
        int remaining=deck.DrawPileCount;
        while(!played && remaining-- > 0)
        {
            deck.Draw(1);Call(runtime,"RefreshAll");yield return null;
            played=PlayOneCurrentHandCard(battleUI,runtime,deck);
        }
        Assert.IsTrue(played, "At least one real drawn hand card must play through its UI button and official resolution; "
            + "waiting=" + GetMember(battleUI,"_waitingForAction") + " ending=" + GetMember(battleUI,"_isEndingTurn")
            + " busy=" + GetMember(battleUI,"IsFeedbackPlaying") + " phase=" + GetMember(runtime,"TurnPhase")
            + " hand=" + string.Join(",",deck.Hand.Select(c=>c.TemplateId)) + " log=" + ((TMP_Text)GetMember(battleUI,"infoText")).text);
        yield return null;
        Call(hud, "Refresh");
        Assert.AreEqual(deck.DiscardPileCount.ToString(), ((TMP_Text)GetMember(hud, "discardPileHost")).text);
        int turnBeforeEnd = (int)GetMember(runtime, "TurnNumber");
        ((Button)GetMember(battleUI, "btnEndTurn")).onClick.Invoke();
        for (int frame = 0; frame < 120 && (int)GetMember(runtime, "TurnNumber") <= turnBeforeEnd; frame++) yield return null;
        Assert.Greater((int)GetMember(runtime, "TurnNumber"), turnBeforeEnd, "The same product battle must complete EndTurn and begin its next turn");
        Call(hud, "Refresh");
        Assert.AreEqual(((int)GetMember(runtime, "TurnNumber")).ToString(), ((TMP_Text)GetMember(hud, "turnHost")).text);

        // 5) 真实 Resolve：Active EnemyRoster 清除，Progression 未被破坏
        Call(expUI, "OnReturnFromBattle", true);
        yield return null;
        yield return null;

        Assert.IsNull(GetMember(adventure, "ActiveEncounter"), "Battle Resolve 后必须清除 Active EnemyRoster");
        Assert.IsTrue((bool)GetMember(adventure, "IsActive"), "Return 后 Adventure 仍应处于进行中");
        Assert.Greater(ReadAvailableIds(GetMember(adventure, "Progression")).Count, 0,
            "Resolve 后 Progression 必须仍能继续（Battle 往返不破坏 Journey）");
    }

    // ==================== WP-Encounter-1C-R（R1）：失败路径不产生半进入状态 ====================

    /// <summary>
    /// **真实产品路径下的 R1 验证**：Encounter 生成失败时，节点不得进入 Entered，
    /// Progression / Supply / elapsed 不得改变，ActiveEncounter 不得留下半成品，BattleUI 不得开战；
    /// 且玩家可**直接重试同一节点**（换回可用池后成功进入）——不需要 Retreat。
    /// </summary>
    [UnityTest]
    public IEnumerator ProductBattleNode_GenerationFailure_DoesNotEnterNode_AndAllowsRetry()
    {
        LogAssert.ignoreFailingMessages = true;   // 该失败路径必须产生 LogError（产品要求）

        IEnumerator bootstrap = BootstrapProductExpedition();
        while (bootstrap.MoveNext()) yield return bootstrap.Current;

        object adventure = _adventure;
        object expUI = _expUI;
        object battleUI = _battleUI;
        object state = GetMember(adventure, "ProgressionState");

        string battleNodeId = FirstBattleLikeNodeId();
        Assert.IsNotNull(battleNodeId, "当前地图必须有可进入的 Battle 类节点");

        // 制造「合法可进入但无法生成 Encounter」：兼容池 override 指向不存在的池
        SetMember(adventure, "EncounterPoolOverrideForDiagnostics", "pool_this_does_not_exist");

        string phaseBefore = GetMember(state, "Phase").ToString();
        object currentNodeBefore = GetMember(state, "CurrentNodeId");
        int enteredBefore = EnteredCount(adventure);
        int supplyBefore = SupplyCarry(adventure);
        int elapsedBefore = (int)GetMember(adventure, "ExpeditionElapsedDays");

        Call(expUI, "HandleNodeClick", adventure, battleNodeId, battleUI);
        yield return null;
        yield return null;

        // ---- 零 mutation：节点未进入、Progression 未卡在 Entered ----
        Assert.IsFalse((bool)Call(state, "IsNodeEntered", battleNodeId), "生成失败时节点不得进入 Entered");
        Assert.AreEqual(phaseBefore, GetMember(state, "Phase").ToString(), "Phase 不得改变");
        Assert.AreEqual(currentNodeBefore, GetMember(state, "CurrentNodeId"), "CurrentNodeId 不得改变");
        Assert.AreEqual(enteredBefore, EnteredCount(adventure), "已进入集合不得增加");
        Assert.AreEqual(supplyBefore, SupplyCarry(adventure), "Supply 不得改变");
        Assert.AreEqual(elapsedBefore, (int)GetMember(adventure, "ExpeditionElapsedDays"), "elapsed 不得改变");

        // ---- 不得进入 Battle、不得留下半成品 ----
        Assert.IsNull(GetMember(adventure, "ActiveEncounter"), "不得留下半成品 ActiveEncounter");
        Assert.IsFalse((bool)GetMember(battleUI, "IsEnemyRosterMode"), "不得把「失败」绑定为权威阵容");
        Assert.IsNull(GetMember(battleUI, "ActiveEnemyRoster"), "BattleUI 不得持有任何阵容");
        Assert.IsNull(GetMember(battleUI, "_battleSystem"), "不得创建 BattleSystem（不得空战斗 / 默认 3 怪）");
        Assert.AreEqual(0, EnemyPrepCards(battleUI).Count, "失败时不得生成 Prep 敌人卡");

        // ---- 玩家未被卡死：换回正常池后重试同一节点必须成功 ----
        SetMember(adventure, "EncounterPoolOverrideForDiagnostics", null);
        Call(expUI, "HandleNodeClick", adventure, battleNodeId, battleUI);
        yield return null;
        yield return null;

        Assert.IsTrue((bool)Call(state, "IsNodeEntered", battleNodeId), "重试后节点必须已进入（不需要 Retreat）");
        object activeEncounter = GetMember(adventure, "ActiveEncounter");
        Assert.IsNotNull(activeEncounter, "重试成功后必须存在 Active Encounter");
        Assert.AreEqual(battleNodeId, GetMember(activeEncounter, "NodeId"));
        Assert.IsNotNull(GetMember(battleUI, "ActiveEnemyRoster"), "重试成功后 BattleUI 必须持有本场阵容");

        // 清理：把本场 Battle 结算回 Journey（保持测试之间互不影响）
        Call(expUI, "OnReturnFromBattle", true);
        yield return null;

        LogAssert.ignoreFailingMessages = false;
    }
}
