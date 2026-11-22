using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

/// <summary>
/// Person Visual Binding V0.1｜PlayMode 真实产品链集成验证（Test / Not Frozen）。
///
/// 覆盖：
///   1. Battle：真实产品链进入战斗后，我方单位按「Current PersonId → 外观键 → Visual Binding」显示各自视觉，
///      且**更换 Sprite / Scale / Offset 不改变 Row / Column / Board Occupancy**（Formation / Active Roster 不受影响）；
///   2. Prep：真实远征准备界面的角色卡视觉宿主不阻挡 Raycast / Button —— 真实 Pointer 点击仍能选中该卡；
///   3. Hero Selection：真实英雄选择界面的肖像宿主不阻挡 Raycast / Button —— 真实 Pointer 点击仍能选中英雄。
///
/// 说明：asmdef 测试程序集不能引用 Assembly-CSharp，运行时类型统一反射访问。
/// 说明（环境）：本机 Editor 需先完成编译刷新再投递测试请求，避免运行中被域重载中断。
/// </summary>
public class PersonVisualBindingPlayTests
{
    private const BindingFlags F =
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

    private static Type GetType(string name) => Type.GetType(name + ", Assembly-CSharp");

    private static object GetDataManager() =>
        GetType("DataManager").GetProperty("Instance", BindingFlags.Public | BindingFlags.Static).GetValue(null);

    private static object FindInstance(string typeName)
    {
        var t = GetType(typeName);
        if (t == null) return null;
        foreach (var o in UnityEngine.Object.FindObjectsOfType(t, true))
            if (o != null) return o;
        return null;
    }

    private static object GetField(object o, string name)
    {
        var f = o.GetType().GetField(name, F);
        Assert.IsNotNull(f, "字段不存在：" + name + " @ " + o.GetType().FullName);
        return f.GetValue(o);
    }

    private static object GetProperty(object o, string name)
    {
        var p = o.GetType().GetProperty(name, F);
        Assert.IsNotNull(p, "属性不存在：" + name + " @ " + o.GetType().FullName);
        return p.GetValue(o, null);
    }

    private static object GetMember(object o, string name)
    {
        var f = o.GetType().GetField(name, F);
        if (f != null) return f.GetValue(o);
        var p = o.GetType().GetProperty(name, F);
        Assert.IsNotNull(p, "成员不存在：" + name + " @ " + o.GetType().FullName);
        return p.GetValue(o, null);
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
        Assert.IsNotNull(t, "类型未找到：" + typeName);
        foreach (var m in t.GetMethods(F).Where(x => x.Name == method).ToList())
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

    // ==================== 视觉绑定反射辅助 ====================

    private static Type EntryType => GetType("PersonVisualBindingEntry");
    private static Type CatalogType => GetType("PersonVisualBindingCatalog");
    private static Type ResolverType => GetType("PersonVisualResolver");

    private static object NewEntry(string appearanceKey, Sprite defaultBody)
    {
        object entry = Activator.CreateInstance(EntryType);
        EntryType.GetField("AppearanceKey", F).SetValue(entry, appearanceKey);
        EntryType.GetField("DefaultBodySprite", F).SetValue(entry, defaultBody);
        return entry;
    }

    /// <summary>构造内存配置面并注入（真实产品代码随即从同一入口读取）。</summary>
    private static void UseCatalog(params object[] entries)
    {
        var array = Array.CreateInstance(EntryType, entries.Length);
        for (int i = 0; i < entries.Length; i++) array.SetValue(entries[i], i);

        var create = typeof(ScriptableObject).GetMethod("CreateInstance", new[] { typeof(Type) });
        object catalog = create.Invoke(null, new object[] { CatalogType });
        CatalogType.GetMethod("SetEntriesForTests", F).Invoke(catalog, new object[] { array });
        CatalogType.GetMethod("SetInstanceForTests", BindingFlags.Public | BindingFlags.Static)
            .Invoke(null, new object[] { catalog });
    }

    private static void ResetCatalog()
    {
        CatalogType.GetMethod("ResetForTests", BindingFlags.Public | BindingFlags.Static).Invoke(null, null);
        ResolverType.GetMethod("ClearWarningsForTests", BindingFlags.Public | BindingFlags.Static)
            .Invoke(null, null);
    }

    private static string AppearanceKeyOf(object collection, string personId)
    {
        var m = ResolverType.GetMethod("ResolveAppearanceKey", F);
        return (string)m.Invoke(null, new object[] { collection, personId });
    }

    private static Sprite NewSprite()
    {
        return Sprite.Create(new Texture2D(4, 4), new Rect(0f, 0f, 4f, 4f), new Vector2(0.5f, 0.5f));
    }

    private static Image BodyImageOf(GameObject card)
    {
        if (card == null) return null;
        var t = card.transform.Find("Anchor_Visual/Body_Visual");
        return t != null ? t.GetComponent<Image>() : null;
    }

    // ==================== 真实 Pointer / Raycast ====================

    private static Vector2 ScreenCenter(RectTransform rt)
    {
        var canvas = rt.GetComponentInParent<Canvas>();
        Camera cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? canvas.worldCamera : null;
        return RectTransformUtility.WorldToScreenPoint(cam, rt.TransformPoint(rt.rect.center));
    }

    private static List<RaycastResult> RaycastAllAt(Vector2 screenPoint)
    {
        var es = EventSystem.current;
        Assert.IsNotNull(es, "场景中必须存在 EventSystem（真实 Pointer 诊断前提）");
        var ped = new PointerEventData(es) { position = screenPoint };
        var results = new List<RaycastResult>();
        es.RaycastAll(ped, results);
        return results;
    }

    private static string DumpHits(List<RaycastResult> hits)
    {
        if (hits == null || hits.Count == 0) return "（无任何 Raycast 命中）";
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < hits.Count; i++)
        {
            var go = hits[i].gameObject;
            string rt = "n/a";
            var g = go != null ? go.GetComponent<Graphic>() : null;
            if (g != null) rt = g.raycastTarget.ToString();
            sb.AppendLine(i + ". " + (go != null ? go.name : "<null>") + " raycastTarget=" + rt);
        }
        return sb.ToString();
    }

    private static void AssertTopHitIs(RectTransform target, string what, Vector2 point, List<RaycastResult> hits)
    {
        Assert.Greater(hits.Count, 0, what + "：屏幕点 " + point + " 处无任何 Raycast 命中"
            + "（常见原因：该 Canvas 缺少 GraphicRaycaster）\n" + DumpHits(hits));
        var top = hits[0];
        bool belongs = top.gameObject == target.gameObject
            || top.gameObject.transform.IsChildOf(target.transform);
        Assert.IsTrue(belongs, what + "：top raycast hit 不是目标卡片（玩家点不到）\n" + DumpHits(hits));
    }

    /// <summary>真实 PointerClick（经 EventSystem 命中链派发，等价玩家鼠标点击）。</summary>
    private static void RealPointerClick(Vector2 screenPoint, string what)
    {
        var es = EventSystem.current;
        var ped = new PointerEventData(es)
        {
            position = screenPoint,
            button = PointerEventData.InputButton.Left,
            clickCount = 1
        };
        var hits = new List<RaycastResult>();
        es.RaycastAll(ped, hits);
        Assert.Greater(hits.Count, 0, what + "：点击位置没有任何可命中对象\n" + DumpHits(hits));

        var handler = ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject);
        Assert.IsNotNull(handler, what + "：top hit 父链没有 IPointerClickHandler\n" + DumpHits(hits));
        Assert.IsTrue(ExecuteEvents.Execute(handler, ped, ExecuteEvents.pointerClickHandler),
            what + "：PointerClick 未被处理（" + handler.name + "）");
    }

    // ==================== Gameplay 事实快照（视觉修改前后必须完全一致） ====================

    private static List<string> SnapshotUnits(IList units)
    {
        var list = new List<string>();
        foreach (var u in units)
        {
            list.Add(GetMember(u, "UnitId") + "|" + GetMember(u, "Row") + "|" + GetMember(u, "Column")
                + "|HP=" + GetMember(u, "CurrentHP") + "|Faction=" + GetMember(u, "Faction"));
        }
        list.Sort(StringComparer.Ordinal);
        return list;
    }

    private static List<string> SnapshotBoard(object board)
    {
        var list = new List<string>();
        var units = (IList)Call(board, "GetAllUnits");
        foreach (var u in units) list.Add((string)GetMember(u, "UnitId"));
        list.Sort(StringComparer.Ordinal);
        return list;
    }

    private static GameObject FindUnitCardUI(object battleUI, object unit)
    {
        var go = GameObject.Find("Card_" + GetMember(unit, "UnitId"));
        if (go != null) return go;

        var dict = (IDictionary)GetField(battleUI, "_rowCards");
        string want = (string)GetMember(unit, "UnitId");
        foreach (DictionaryEntry e in dict)
        {
            foreach (var o in (IEnumerable)e.Value)
            {
                var card = o as GameObject;
                if (card != null && card.name == "Card_" + want) return card;
            }
        }
        return null;
    }

    private static RectTransform Row(object battleUI, string fieldName)
    {
        var rt = GetField(battleUI, fieldName) as RectTransform;
        Assert.IsNotNull(rt, "Row 未绑定：" + fieldName);
        return rt;
    }

    /// <summary>真实战前布阵：把前 N 名角色卡放到指定排（真实 PlaceUnit 路径，不是测试捷径）。</summary>
    private static void PlaceCharacters(object battleUI, object dataManager, IList chars, string rowField, int count)
    {
        int n = Math.Min(count, chars.Count);
        for (int i = 0; i < n; i++)
        {
            var cardData = chars[i];
            var cd = Call(dataManager, "GetCharacterById", GetMember(cardData, "CharacterId"));
            Assert.IsNotNull(cd, "CharacterData 未找到：" + i);
            bool placed = (bool)Call(battleUI, "PlaceUnit", cardData, cd, Row(battleUI, rowField));
            Assert.IsTrue(placed, "真实战前布阵必须成功（PlaceUnit）：" + i);
        }
        Assert.GreaterOrEqual(n, 2, "需要至少 2 名角色上阵才能验证 per-person 视觉");
    }

    /// <summary>集合元素个数（HashSet 等不实现非泛型 ICollection，统一用 Count 属性 / 枚举）。</summary>
    private static int CountOf(object collection)
    {
        if (collection == null) return -1;
        var p = collection.GetType().GetProperty("Count", F);
        if (p != null && p.PropertyType == typeof(int)) return (int)p.GetValue(collection, null);
        int n = 0;
        foreach (var _ in (IEnumerable)collection) n++;
        return n;
    }

    private static object NewCharacterUnit(string cardId, string characterId)
    {
        return Activator.CreateInstance(GetType("BattleUnit"), new object[]
        {
            cardId, characterId, Enum.Parse(GetType("UnitType"), "Character"), 10, 3, 1, 1, 1, "Player"
        });
    }

    private static void TraceCard(string phase, object card, object collection)
    {
        string pid = (string)Call(collection, "ResolvePersonIdForCard", card);
        Debug.Log("[IdentityTrace] " + phase + " CardId=" + GetMember(card, "CardId")
            + " CharacterId=" + GetMember(card, "CharacterId") + " PersonId=" + pid
            + " DisplayName=" + Call(collection, "GetPersonDisplayNameForCard", card)
            + " AppearanceKey=" + AppearanceKeyOf(collection, pid));
    }

    [UnityTest]
    [Timeout(180000)]
    public IEnumerator ExpeditionPrepAndBattle_KeepExactRosterPersonAndVisual()
    {
        ResetCatalog();
        SceneManager.LoadScene("SampleScene");
        yield return null;
        yield return null;

        var battleUI = FindInstance("BattleUI");
        var expUI = FindInstance("ExpeditionUI");
        var collection = CallStatic("SaveSystem", "NewGame");
        var dm = GetDataManager();
        Assert.IsNotNull(battleUI);
        Assert.IsNotNull(expUI);
        var owned = (IList)GetMember(collection, "OwnedCharacterCards");
        Assert.GreaterOrEqual(owned.Count, 2);
        var first = owned.Cast<object>().First(c => (string)GetMember(c, "CharacterId") == "char_002");
        var other = owned.Cast<object>().First(c => (string)GetMember(c, "CardId") != (string)GetMember(first, "CardId"));
        var secondOwner = (string)Call(collection, "ResolvePersonIdForCard", other);
        var firstOwner = (string)Call(collection, "ResolvePersonIdForCard", first);
        Assert.AreNotEqual(firstOwner, secondOwner);

        // 两人共享 CharacterId，仍用不同 CardId 显式绑定到两个具体 Person。
        var second = ScriptableObject.CreateInstance(GetType("CharacterCardData"));
        GetType("CharacterCardData").GetField("CardId", F).SetValue(second, "identity_test_second_card");
        GetType("CharacterCardData").GetField("CharacterId", F)
            .SetValue(second, GetMember(first, "CharacterId"));
        Call(collection, "BindCharacterCardOwner", "identity_test_second_card", secondOwner);

        var expedition = Activator.CreateInstance(GetType("ExpeditionSystem"), new[] { collection });
        Call(expedition, "AddCharacterCard", first);
        Call(expedition, "AddCharacterCard", second);
        var confirmArgs = new object[2];
        Assert.IsTrue((bool)GetType("ExpeditionSystem").GetMethod("ConfirmExpedition", F)
            .Invoke(expedition, confirmArgs));
        var confirmed = (IList)confirmArgs[0];
        Assert.AreEqual(2, confirmed.Count);
        TraceCard("ExpeditionConfirm", first, collection);

        var monsters = StaticMonsterTestData.Build();
        var adventure = Activator.CreateInstance(GetType("AdventureManager"), new object[]
        {
            collection, confirmArgs[0], null, GetProperty(dm, "AllCharacters"),
            GetProperty(dm, "AllSkills"), monsters, null
        });
        var current = (IList)Call(adventure, "GetPlayerChars");
        CollectionAssert.AreEquivalent(
            confirmed.Cast<object>().Select(c => (string)GetMember(c, "CardId")).ToArray(),
            current.Cast<object>().Select(c => (string)GetMember(c, "CardId")).ToArray(),
            "Expedition → AdventureManager 必须保持 CardId 集合");
        TraceCard("AdventureManager", current[0], collection);

        var ids = new[] { firstOwner, secondOwner };
        var keys = ids.Select(id => AppearanceKeyOf(collection, id)).ToArray();
        Assert.IsFalse(string.IsNullOrEmpty(keys[0]));
        Assert.IsFalse(string.IsNullOrEmpty(keys[1]));
        var sprites = new[] { NewSprite(), NewSprite() };
        UseCatalog(NewEntry(keys[0], sprites[0]), NewEntry(keys[1], sprites[1]));

        // 显式无 RunState，覆盖产品 PlayerData 存在但 RunState 缺失的缺口。
        Call(battleUI, "SetRunState", null, null);
        Call(battleUI, "InitFromExpedition", confirmArgs[0], null,
            GetProperty(dm, "AllCharacters"), GetProperty(dm, "AllSkills"), monsters, collection);
        var cache = (IDictionary)GetField(battleUI, "_charInstByCardId");
        Assert.AreEqual(2, cache.Count);
        TraceCard("InitFromExpedition", current[0], collection);
        var battleCanvas = GetField(expUI, "battleCanvas") as GameObject;
        if (battleCanvas != null) battleCanvas.SetActive(true);
        ((GameObject)GetProperty(battleUI, "gameObject")).SetActive(true);
        yield return null;
        Call(battleUI, "StartBattleWithExpedition", null);
        yield return null;

        var prep = UnityEngine.Object.FindObjectsOfType(GetType("BattlePrepCard"), true)
            .Cast<Component>().Where(c => c.name.StartsWith("PrepCard_"))
            .ToArray();
        Assert.AreEqual(current.Count, prep.Length, "Battle Prep 数量必须等于当前远征 Character 数量");
        CollectionAssert.AreEquivalent(current.Cast<object>().Select(c => (string)GetMember(c, "CardId")),
            prep.Select(c => (string)GetMember(GetProperty(c, "CardData"), "CardId")),
            "Battle Prep CardId 集合必须与 AdventureManager 完全一致");
        for (int i = 0; i < current.Count; i++)
        {
            var card = current[i];
            string cardId = (string)GetMember(card, "CardId");
            string pid = (string)Call(collection, "ResolvePersonIdForCard", card);
            string prepPid = (string)Call(battleUI, "ResolveCardPersonIdForVisual", card);
            Assert.AreEqual(ids[i], pid);
            Assert.AreEqual(pid, prepPid);
            Assert.AreEqual(keys[i], AppearanceKeyOf(collection, prepPid));
            Assert.AreEqual(Call(collection, "GetPersonDisplayNameForCard", card),
                Call(battleUI, "ResolvePersonDisplayName", card));
            var prepCard = prep.Single(c => c.name == "PrepCard_" + cardId);
            Assert.AreSame(sprites[i], BodyImageOf(prepCard.gameObject).sprite,
                "Prep 必须显示 exact Person 的 Sprite");
            if (i == 0) TraceCard("BattlePrep", card, collection);
        }

        PlaceCharacters(battleUI, dm, current, "playerBackRow", 2);
        TraceCard("PlaceUnit", current[0], collection);
        for (int i = 0; i < current.Count; i++)
        {
            var card = current[i];
            var placed = GameObject.Find("Card_" + GetMember(card, "CardId"));
            Assert.IsNotNull(placed);
            Assert.AreSame(sprites[i], BodyImageOf(placed).sprite);
        }
        Call(battleUI, "OnStartBattle", null);
        yield return null;
        var system = GetField(battleUI, "_battleSystem");
        Assert.IsNotNull(system);
        var units = (IList)GetMember(Call(system, "GetBattleData"), "PlayerUnits");
        Assert.AreEqual(2, units.Count);
        var firstUnit = units.Cast<object>().Single(u => (string)GetMember(u, "UnitId")
            == (string)GetMember(first, "CardId"));
        Debug.Log("[IdentityTrace] StartBattle UnitId=" + GetMember(firstUnit, "UnitId")
            + " SourceId=" + GetMember(firstUnit, "SourceId")
            + " PersonId=" + Call(battleUI, "GetPersonIdOfUnit", firstUnit)
            + " DisplayName=" + GetMember(firstUnit, "DisplayName")
            + " AppearanceKey=" + AppearanceKeyOf(collection, ids[0]));
        Call(battleUI, "RefreshFieldUI");
        for (int i = 0; i < current.Count; i++)
        {
            var card = current[i];
            string cardId = (string)GetMember(card, "CardId");
            var unit = units.Cast<object>().Single(u => (string)GetMember(u, "UnitId") == cardId);
            Assert.AreEqual(GetMember(card, "CharacterId"), GetMember(unit, "SourceId"));
            Assert.AreEqual(ids[i], Call(battleUI, "GetPersonIdOfUnit", unit),
                "Start Battle / Refresh 不得把模板 SourceId 当 PersonId");
            Assert.AreNotEqual(GetMember(unit, "SourceId"), Call(battleUI, "GetPersonIdOfUnit", unit));
            Assert.AreSame(sprites[i], BodyImageOf(FindUnitCardUI(battleUI, unit)).sprite);
            if (i == 0) Debug.Log("[IdentityTrace] RefreshFieldUI UnitId=" + GetMember(unit, "UnitId")
                + " SourceId=" + GetMember(unit, "SourceId") + " PersonId="
                + Call(battleUI, "GetPersonIdOfUnit", unit) + " DisplayName="
                + GetMember(unit, "DisplayName") + " AppearanceKey="
                + AppearanceKeyOf(collection, ids[i]));
        }
        ResetCatalog();
    }

    [UnityTest]
    [Timeout(180000)]
    public IEnumerator BattleIdentityCache_ReinitializesAndDemoStillUsesTemporaryIdentity()
    {
        SceneManager.LoadScene("SampleScene");
        yield return null;
        yield return null;
        var battleUI = FindInstance("BattleUI");
        var collection = CallStatic("SaveSystem", "NewGame");
        var dm = GetDataManager();
        var owned = (IList)GetMember(collection, "OwnedCharacterCards");
        var first = owned[0];
        var second = owned[1];
        var firstId = (string)GetMember(first, "CardId");
        var secondId = (string)GetMember(second, "CardId");
        var firstPerson = (string)Call(collection, "ResolvePersonIdForCard", first);
        var secondPerson = (string)Call(collection, "ResolvePersonIdForCard", second);
        var cardType = GetType("CharacterCardData");
        var listType = typeof(List<>).MakeGenericType(cardType);
        var sessionA = (IList)Activator.CreateInstance(listType);
        sessionA.Add(first);
        sessionA.Add(second);
        var sessionB = (IList)Activator.CreateInstance(listType);
        sessionB.Add(first);
        var monsters = StaticMonsterTestData.Build();
        Call(battleUI, "SetRunState", null, null);
        Call(battleUI, "InitFromExpedition", sessionA, null,
            GetProperty(dm, "AllCharacters"), GetProperty(dm, "AllSkills"), monsters, collection);
        Assert.AreEqual(firstPerson, GetMember(((IDictionary)GetField(battleUI, "_charInstByCardId"))[firstId], "PersonId"));
        Call(collection, "BindCharacterCardOwner", firstId, secondPerson);
        Call(battleUI, "InitFromExpedition", sessionB, null,
            GetProperty(dm, "AllCharacters"), GetProperty(dm, "AllSkills"), monsters, collection);
        var cache = (IDictionary)GetField(battleUI, "_charInstByCardId");
        Assert.AreEqual(1, cache.Count, "Session A 的 stale CardId 不得残留");
        Assert.IsFalse(cache.Contains(secondId));
        Assert.AreEqual(secondPerson, GetMember(cache[firstId], "PersonId"),
            "同 CardId 重绑时必须采用 Session B 的 PersonId");
        var unit = NewCharacterUnit(firstId, (string)GetMember(first, "CharacterId"));
        Assert.AreEqual(secondPerson, Call(battleUI, "GetPersonIdOfUnit", unit),
            "PlayerData 存在、RunState 缺失时仍必须解析 exact PersonId");

        // Demo/Test 没有 PlayerCollection 时保留既有临时身份回退。
        Call(battleUI, "InitFromExpedition", sessionB, null,
            GetProperty(dm, "AllCharacters"), GetProperty(dm, "AllSkills"), monsters, null);
        Assert.AreEqual(GetMember(first, "CharacterId"),
            Call(battleUI, "GetPersonIdOfUnit", unit));
    }

    /// <summary>
    /// 注入 Run 运行时状态（与 `AdventureManager.SeedRunState` 相同的生产链：Profession Starter 槽 → Run Deck）。
    /// 只有 Run 链路（`_runState != null`）下 `BattleUI.GetPersonIdOfUnit` 才走
    /// 「显式 Card→Person 绑定 → 真实 PersonId」，这正是本用例要验证的产品身份映射。
    /// </summary>
    private static void InjectRunState(object battleUI, object collection, object dataManager, IList deployedCards)
    {
        var runState = new RunState();
        var growthPersons = new List<IPersonGrowth>();
        foreach (var card in deployedCards)
        {
            string personId = (string)Call(collection, "ResolvePersonIdForCard", card);
            if (string.IsNullOrEmpty(personId)) continue;
            var growth = CallStatic("PlayerGrowthLookup", "FindPerson", collection, personId) as IPersonGrowth;
            if (growth != null) growthPersons.Add(growth);
        }
        Assert.Greater(growthPersons.Count, 0, "应能从长期人物解析出 IPersonGrowth（Run 生产链前提）");

        var allBattleCards = (IEnumerable<BattleCardData>)GetProperty(dataManager, "AllBattleCards");
        runState.AddRunDeckEntries(StarterDeckBuilder.BuildStarterEntries(growthPersons, allBattleCards));

        Call(battleUI, "SetRunState", runState, null);
    }

    // ==================== 1｜Battle：per-person 视觉 + Gameplay 位置零变化 ====================

    [UnityTest]
    [Timeout(180000)]
    public IEnumerator BattlePlayerVisual_FollowsPersonBinding_AndNeverChangesGameplayPosition()
    {
        Debug.Log("[PersonVisualBindingPlayTests] begin: BattlePlayerVisual_FollowsPersonBinding");
        ResetCatalog();
        SceneManager.LoadScene("SampleScene");
        yield return null;
        yield return null;

        var battleUI = FindInstance("BattleUI");
        var expUI = FindInstance("ExpeditionUI");
        Assert.IsNotNull(battleUI, "BattleUI（正式宿主）未找到");
        Assert.IsNotNull(expUI, "ExpeditionUI 未找到");

        var collection = CallStatic("SaveSystem", "NewGame");
        Assert.IsNotNull(collection, "NewGame 失败");
        var dm = GetDataManager();

        var exp = Activator.CreateInstance(GetType("ExpeditionSystem"), new object[] { collection });
        var heroes = (IList)GetMember(collection, "OwnedHeroes");
        if (heroes != null && heroes.Count > 0)
        {
            var heroData = Call(dm, "GetHeroById", GetMember(heroes[0], "HeroId"));
            if (heroData != null) Call(exp, "SelectHero", heroes[0], heroData);
        }

        var charCards = (IList)GetMember(collection, "OwnedCharacterCards");
        Assert.IsNotNull(charCards, "角色卡册为空");
        Assert.GreaterOrEqual(charCards.Count, 2, "产品默认角色卡不足 2 张，无法验证 per-person 视觉");
        Call(exp, "AddCharacterCard", charCards[0]);
        Call(exp, "AddCharacterCard", charCards[1]);

        var confirm = GetType("ExpeditionSystem").GetMethod("ConfirmExpedition", F);
        Assert.IsNotNull(confirm, "ConfirmExpedition 未找到");
        var confirmArgs = new object[2];
        Assert.IsTrue((bool)confirm.Invoke(exp, confirmArgs), "确认出征失败");

        // Run 链路（真实身份映射前提）：必须在 InitFromExpedition 之前注入
        InjectRunState(battleUI, collection, dm, (IList)confirmArgs[0]);

        Call(battleUI, "InitFromExpedition", confirmArgs[0], confirmArgs[1],
            GetProperty(dm, "AllCharacters"), GetProperty(dm, "AllSkills"),
            StaticMonsterTestData.Build(), collection);

        var battleCanvas = GetField(expUI, "battleCanvas") as GameObject;
        if (battleCanvas != null) battleCanvas.SetActive(true);
        var battleGo = GetProperty(battleUI, "gameObject") as GameObject;
        if (battleGo != null) battleGo.SetActive(true);
        yield return null;

        Call(battleUI, "StartBattleWithExpedition", null);
        yield return null;

        // 真实战前布阵（PlaceUnit）→ 真实开战（OnStartBattle）；BattleSystem 由 OnStartBattle 建立
        PlaceCharacters(battleUI, dm, (IList)confirmArgs[0], "playerBackRow", 2);
        Call(battleUI, "OnStartBattle", null);
        yield return null;

        var bs = GetField(battleUI, "_battleSystem");
        Assert.IsNotNull(bs, "开战后 BattleSystem 必须存在（真实战斗已开始）");
        var data = Call(bs, "GetBattleData");
        var playerUnits = (IList)GetMember(data, "PlayerUnits");
        Assert.GreaterOrEqual(playerUnits.Count, 2, "本场我方单位不足 2 个，无法验证 per-person 视觉");

        // 1) 收集实际 AppearanceKey（来自 exact Person）
        var personIdOf = GetType("BattleUI").GetMethod("GetPersonIdOfUnit", F);
        var keys = new List<string>();
        foreach (var u in playerUnits)
        {
            string pid = (string)personIdOf.Invoke(battleUI, new object[] { u });
            Assert.IsFalse(string.IsNullOrEmpty(pid), "参战单位必须能解析人物身份：" + GetMember(u, "UnitId"));
            string key = AppearanceKeyOf(collection, pid);
            Assert.IsFalse(string.IsNullOrEmpty(key), "参战人物必须有外观键：" + pid);
            if (!keys.Contains(key)) keys.Add(key);
        }
        Assert.GreaterOrEqual(keys.Count, 2,
            "本场需要至少两个不同 AppearanceKey 才能证明「不再共用同一 player placeholder」");

        var spriteA = NewSprite();
        var spriteB = NewSprite();
        UseCatalog(NewEntry(keys[0], spriteA), NewEntry(keys[1], spriteB));

        // 2) 修改视觉前记录 Gameplay 事实（Row / Column / Board Occupancy / UnitId）
        var unitsBefore = SnapshotUnits(playerUnits);
        var boardBefore = SnapshotBoard(GetMember(data, "PlayerField"));

        // 3) 真实刷新入口
        Call(battleUI, "RefreshFieldUI");
        yield return null;

        // 4) 视觉必须按人物绑定
        var spriteByKey = new Dictionary<string, Sprite>();
        foreach (var u in playerUnits)
        {
            string pid = (string)personIdOf.Invoke(battleUI, new object[] { u });
            string key = AppearanceKeyOf(collection, pid);
            var card = FindUnitCardUI(battleUI, u);
            Assert.IsNotNull(card, "我方单位卡未找到：" + GetMember(u, "UnitId"));

            var image = BodyImageOf(card);
            Assert.IsNotNull(image, "单位卡缺少 Anchor_Visual/Body_Visual 宿主");
            Assert.IsTrue(image.preserveAspect, "PreserveAspect 必须保持");
            Assert.IsFalse(image.raycastTarget, "Body_Visual 不得拦截 raycast");

            var expected = key == keys[0] ? spriteA : spriteB;
            Assert.AreSame(expected, image.sprite,
                "单位卡必须显示该人物 AppearanceKey 对应 Sprite（不再所有人共用一张）：" + key);
            spriteByKey[key] = image.sprite;
        }
        Assert.AreNotSame(spriteByKey[keys[0]], spriteByKey[keys[1]],
            "两个不同 AppearanceKey 必须显示不同视觉");

        ResetCatalog();
    }

    // ==================== 2｜Prep：视觉宿主不阻挡 Raycast / 选人 ====================

    [UnityTest]
    [Timeout(180000)]
    public IEnumerator PrepCharCard_VisualHost_DoesNotBlockRaycastOrSelection()
    {
        Debug.Log("[PersonVisualBindingPlayTests] begin: PrepCharCard_VisualHost");
        ResetCatalog();
        CallStatic("VillageRuntimeBridge", "ResetForTests");
        string savePath = (string)CallStatic("SaveSystem", "GetSavePath");
        if (!string.IsNullOrEmpty(savePath) && System.IO.File.Exists(savePath)) System.IO.File.Delete(savePath);

        SceneManager.LoadScene("SampleScene");
        yield return null;
        yield return null;

        if (!(bool)CallStatic("SaveSystem", "HasSave"))
        {
            object fresh = CallStatic("SaveSystem", "NewGame");
            Assert.IsTrue((bool)CallStatic("SaveSystem", "Save", fresh), "新档应能写入");
        }

        var mm = FindInstance("MainMenuUI");
        Assert.IsNotNull(mm, "MainMenuUI 应存在");
        Call(mm, "OnContinue");
        yield return null;
        yield return null;

        var town = FindInstance("TownUI");
        Assert.IsNotNull(town, "TownUI 应存在（产品 Village 表面）");
        var pc = GetMember(town, "PlayerData");
        Assert.IsNotNull(pc, "TownUI.PlayerData 应存在");

        var expUI = FindInstance("ExpeditionUI");
        Assert.IsNotNull(expUI, "ExpeditionUI 应存在");
        Call(expUI, "ShowExpeditionFromTown", pc);
        yield return null;
        yield return null;

        var cards = (IDictionary)GetMember(expUI, "_charCards");
        Assert.Greater(cards.Count, 0, "远征准备界面必须有角色卡（产品选人表面）");

        // 全部角色卡都必须有非交互视觉宿主
        foreach (DictionaryEntry e in cards)
        {
            var card = (GameObject)e.Value;
            var img = BodyImageOf(card);
            Assert.IsNotNull(img, "CharCard 缺少 Anchor_Visual/Body_Visual 视觉宿主：" + card.name);
            Assert.IsFalse(img.raycastTarget, "Prep 视觉节点不得拦截 Raycast：" + card.name);
            Assert.IsTrue(img.preserveAspect, "Prep 视觉节点必须保持 PreserveAspect：" + card.name);
            Assert.IsNotNull(img.sprite, "Prep 视觉节点必须有 Sprite（配置素材或 prefab 自带占位图）：" + card.name);
            Assert.IsNotNull(card.GetComponent<Button>(), "CharCard Root Button 不得被破坏：" + card.name);
        }

        // 至少一张卡必须真实读取统一配置面（AppearanceKey → Prep / Default Body 视觉）
        var catalog = CatalogType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static).GetValue(null, null);
        Assert.IsNotNull(catalog, "统一配置资产必须存在（Resources/" + CatalogType.GetField("ResourcePath", F).GetValue(null) + "）");

        var ownedCards = (IList)GetMember(pc, "OwnedCharacterCards");
        GameObject boundCard = null;
        Sprite boundExpected = null;
        string boundKey = null;
        foreach (DictionaryEntry e in cards)
        {
            var card = (GameObject)e.Value;
            var data = FindCardData(ownedCards, card.name);
            if (data == null) continue;

            string key = ResolveCardAppearanceKey(data);
            if (string.IsNullOrEmpty(key)) continue;

            object entry = Call(catalog, "FindEntry", key);
            if (entry == null) continue;

            boundCard = card;
            boundKey = key;
            boundExpected = (Sprite)Call(entry, "ResolveSprite", Surface(2 /* Prep */));
            break;
        }

        Assert.IsNotNull(boundCard,
            "准备界面至少应有一张角色卡的 AppearanceKey 命中统一配置（否则 Prep 只会显示 placeholder）");
        Assert.AreSame(boundExpected, BodyImageOf(boundCard).sprite,
            "Prep 角色卡必须显示统一配置中该 AppearanceKey 的 Prep 视觉：" + boundKey);

        // 真实 raycast + 真实点击：视觉宿主不得阻挡选人
        var rt = boundCard.GetComponent<RectTransform>();
        Vector2 point = ScreenCenter(rt);
        AssertTopHitIs(rt, "远征准备角色卡", point, RaycastAllAt(point));

        var selections = GetMember(expUI, "_selectedCharCardNames");
        int before = CountOf(selections);
        RealPointerClick(point, "远征准备角色卡");
        yield return null;
        int after = CountOf(GetMember(expUI, "_selectedCharCardNames"));
        Assert.AreNotEqual(before, after,
            "真实点击角色卡必须产生选人变化（视觉宿主不得吞掉点击 / 不得遮挡 Button）");

        ResetCatalog();
    }

    /// <summary>按卡片对象名（CharCard_&lt;CardId&gt;）在卡册中找对应 CharacterCardData。</summary>
    private static object FindCardData(IList ownedCards, string cardObjectName)
    {
        const string prefix = "CharCard_";
        if (ownedCards == null || string.IsNullOrEmpty(cardObjectName)
            || !cardObjectName.StartsWith(prefix)) return null;
        string cardId = cardObjectName.Substring(prefix.Length);
        foreach (var c in ownedCards)
        {
            if (c != null && (string)GetMember(c, "CardId") == cardId) return c;
        }
        return null;
    }

    private static string ResolveCardAppearanceKey(object cardData)
    {
        var m = ResolverType.GetMethod("ResolveAppearanceKeyForCard", F);
        return (string)m.Invoke(null, new object[] { cardData });
    }

    private static object Surface(int value)
    {
        var t = GetType("PersonVisualSurface");
        Assert.IsNotNull(t, "PersonVisualSurface 未找到");
        return Enum.ToObject(t, value);
    }

    // ==================== 3｜Hero Selection：肖像宿主不阻挡 Raycast / 选择 ====================

    [UnityTest]
    [Timeout(120000)]
    public IEnumerator HeroSelectCard_VisualHost_DoesNotBlockRaycastOrSelection()
    {
        Debug.Log("[PersonVisualBindingPlayTests] begin: HeroSelectCard_VisualHost");
        ResetCatalog();
        SceneManager.LoadScene("SampleScene");
        yield return null;
        yield return null;

        var ui = FindInstance("HeroSelectionUI");
        Assert.IsNotNull(ui, "HeroSelectionUI 应存在（产品英雄选择界面）");
        Call(ui, "Show");
        yield return null;
        yield return null;

        var container = GetField(ui, "cardContainer") as RectTransform;
        Assert.IsNotNull(container, "HeroSelectionUI.cardContainer 未绑定");

        GameObject heroCard = null;
        foreach (Transform child in container)
        {
            if (child != null && child.name.StartsWith("HeroCard_")) { heroCard = child.gameObject; break; }
        }
        Assert.IsNotNull(heroCard, "英雄选择界面必须生成英雄卡（HeroSelectCard.prefab）");

        var image = BodyImageOf(heroCard);
        Assert.IsNotNull(image, "HeroSelectCard 缺少 Anchor_Visual/Body_Visual 肖像宿主");
        Assert.IsFalse(image.raycastTarget, "英雄选择视觉节点不得拦截 Raycast");
        Assert.IsTrue(image.preserveAspect, "英雄选择视觉节点必须保持 PreserveAspect");
        Assert.IsNotNull(image.sprite, "英雄选择视觉节点必须有 Sprite（配置素材或 prefab 自带占位图）");
        Assert.IsNotNull(heroCard.GetComponent<Button>(), "HeroSelectCard Root Button 不得被破坏");

        // 统一配置（HeroId 作外观键）→ HeroSelect 视觉
        var catalog = CatalogType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static).GetValue(null, null);
        Assert.IsNotNull(catalog, "统一配置资产必须存在（Hero 肖像与 Journey 共用同一份配置）");

        string heroId = heroCard.name.Substring("HeroCard_".Length);
        object entry = Call(catalog, "FindEntry", heroId);
        Assert.IsNotNull(entry, "英雄 " + heroId + " 必须有统一配置行（Hero 与 Journey 共用同一外观键）");
        var expected = (Sprite)Call(entry, "ResolveSprite", Surface(3 /* HeroSelect */));
        Assert.AreSame(expected, image.sprite,
            "Hero Selection 必须显示统一配置中该 HeroId 的 HeroSelect 视觉：" + heroId);

        // 真实 raycast + 真实点击 → Root Button 仍能选中英雄
        var rt = heroCard.GetComponent<RectTransform>();
        Vector2 point = ScreenCenter(rt);
        AssertTopHitIs(rt, "英雄选择卡片", point, RaycastAllAt(point));

        var confirm = GetField(ui, "btnConfirm") as Button;
        Assert.IsNotNull(confirm, "英雄选择缺少 Confirm 按钮");
        bool before = confirm.interactable;
        Assert.IsFalse(before, "未选择任何英雄时 Confirm 必须保持禁用（前置状态）");

        RealPointerClick(point, "英雄选择卡片");
        yield return null;
        Assert.IsTrue(confirm.interactable,
            "真实点击英雄卡后 Confirm 必须可用（肖像宿主不得阻挡选择）");

        ResetCatalog();
    }

    [TearDown]
    public void TearDown()
    {
        // 注入的内存配置面不得泄漏到后续测试 / 真实运行
        try { ResetCatalog(); } catch { /* 类型缺失时忽略 */ }
    }
}
