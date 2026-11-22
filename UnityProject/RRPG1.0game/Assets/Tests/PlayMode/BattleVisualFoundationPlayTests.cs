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
/// WP-D｜Battle Visual Foundation PlayMode 产品集成验证
///
/// 从正式产品路径（SaveSystem.NewGame → ExpeditionSystem.ConfirmExpedition → BattleUI）
/// 进入真实战斗，验证：
///   1. Player / Enemy 单位显示可替换 Body 占位（视觉宿主已接入产品路径）；
///   2. 1 / 2 / 3 人排 Formation Presentation（1 人居中 / 2 人聚拢 / 3 人展开，顺序与 Column Authority 一致）；
///   3. HP / Shield / Enemy Intent 独立宿主按数据实时刷新；
///   4. Battle Background 与六排 RowVisual 从产品路径可见；
///   5. Shared Hand 的 BattleCard Art / Description 视觉槽生效；
///   6. End Turn / Current Cancel 等现有操作入口未被破坏。
///
/// 并输出 Game View 截图证据（screenshots/WP-D/，仓库忽略目录）。
///
/// 说明：BattleUI / BattleSystem / BattleUnit 位于预定义程序集 Assembly-CSharp，
/// 与既有 PlayMode 测试一致使用反射访问。
/// </summary>
public class BattleVisualFoundationPlayTests
{
    private const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    private const string ShotFolder = "screenshots/WP-D";

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

    private static float GetFloatField(object o, string name) => (float)GetField(o, name);

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

    private sealed class Ctx
    {
        public object BattleUI;
        public object DataManager;
        public object Collection;
        public object BattleSystem;
        public IList Chars;
        public object Hero;
    }

    /// <summary>正式产品路径进入战斗（与既有 Battle Stage 测试一致的入口）</summary>
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
        Assert.GreaterOrEqual(charCards.Count, 2, "产品默认角色卡不足 2 张，无法验证 2 / 3 人排");
        Call(exp, "AddCharacterCard", charCards[0]);
        Call(exp, "AddCharacterCard", charCards[1]);

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

        // BattleSystem 由 OnStartBattle 建立；战前布阵阶段尚未创建，读取放在开战后（见各测试）
    }

    private static RectTransform Row(object battleUI, string fieldName)
    {
        var rt = GetField(battleUI, fieldName) as RectTransform;
        Assert.IsNotNull(rt, "Row 未绑定：" + fieldName);
        return rt;
    }

    /// <summary>取某排上的单位卡（测试读取用）</summary>
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

    private static float VisualY(GameObject card)
    {
        var rt = card.GetComponent<RectTransform>();
        Assert.IsNotNull(rt, "单位卡缺少 RectTransform");
        return rt.anchoredPosition.y;
    }

    private static void CaptureShot(string fileName)
    {
        var dir = System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), ShotFolder);
        System.IO.Directory.CreateDirectory(dir);
        ScreenCapture.CaptureScreenshot(ShotFolder + "/" + fileName);
    }

    /// <summary>
    /// 真实 Game View 整屏截图（best-effort）+ 明确记录是否成功（M3 视觉验收用）。
    ///
    /// 事实（本轮实测）：
    ///   - `-batchmode`（无 Game View）：`ScreenCapture.CaptureScreenshot` **不产出文件** → 记为 NOT produced；
    ///   - 有显示器的编辑器会话（非 batchmode）：产出整屏 PNG → 记为 captured。
    /// 无论成功与否都写同名 `.txt`，避免把「离屏合成」误当成整屏截图。
    /// </summary>
    private static IEnumerator CaptureGameViewAndReport(string fileName)
    {
        string dir = System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), ShotFolder);
        System.IO.Directory.CreateDirectory(dir);
        string path = System.IO.Path.Combine(dir, fileName);

        yield return CaptureShotAndWait(fileName, 5);

        System.IO.File.WriteAllText(path + ".txt",
            System.IO.File.Exists(path)
                ? "Game View screenshot PRODUCED: " + path
                : "Game View screenshot NOT produced (batchmode / no Game View available): " + path);
    }
    /// 注意（M3 Slice 1D-1 修复）：batchmode（无 Game View）下 `WaitForEndOfFrame` **永不触发** → 测试会挂住
    /// （这也是 PlayMode 全量套件挂起的根因之一，见 M4-6）。这里改用帧计数等待；`ScreenCapture` 在 batchmode
    /// 下可能不产出文件（如需真正的画面证据，见 <see cref="WriteRuntimeSpriteEvidence"/> 的离屏合成）。
    /// </summary>
    private static IEnumerator CaptureShotAndWait(string fileName, int ticks = 3)
    {
        CaptureShot(fileName);
        for (int i = 0; i < ticks; i++) yield return null;
    }

    /// <summary>
    /// 把 **Runtime 实际使用的 Sprite**（来自单位卡 Body_Visual）离屏合成为一张 PNG 证据图。
    ///
    /// 为什么不用 ScreenCapture：
    ///   batchmode（无 Game View）下 `ScreenCapture.CaptureScreenshot` 不产出文件，且
    ///   `WaitForEndOfFrame` 永不触发（实测：会挂住测试 —— 这也是 PlayMode 全量套件挂起的根因之一，见 M4-6）。
    ///   本方法改用离屏 RenderTexture + Graphics.DrawTexture + EncodeToPNG，不依赖 Game View。
    ///
    /// 失败不影响断言（证据可选）：失败原因写入同名 `.txt` 便于排查。
    /// </summary>
    private static void WriteRuntimeSpriteEvidence(IList<string> labels, IList<Sprite> sprites, string fileName)
    {
        string dir = System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), ShotFolder);
        System.IO.Directory.CreateDirectory(dir);
        string path = System.IO.Path.Combine(dir, fileName);

        try
        {
            const int cell = 64;
            const int pad = 8;
            int width = Mathf.Max(1, sprites.Count) * (cell + pad) + pad;
            int height = cell + pad * 2;

            var rt = RenderTexture.GetTemporary(width, height, 0);
            var previous = RenderTexture.active;

            RenderTexture.active = rt;
            GL.PushMatrix();
            GL.LoadPixelMatrix(0f, width, height, 0f);
            GL.Clear(true, true, new Color(0.12f, 0.12f, 0.12f, 1f));

            for (int i = 0; i < sprites.Count; i++)
            {
                if (sprites[i] == null || sprites[i].texture == null) continue;
                Graphics.DrawTexture(new Rect(pad + i * (cell + pad), pad, cell, cell), sprites[i].texture);
            }

            GL.PopMatrix();

            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex.Apply(false, false);

            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(rt);

            System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);

            System.IO.File.WriteAllText(path + ".txt",
                string.Join("\n", labels) + "\n"
                + "(offscreen composite of the runtime Body_Visual sprites actually used by BattleUI; cells="
                + sprites.Count + " size=" + cell + "px)");
        }
        catch (System.Exception ex)
        {
            System.IO.File.WriteAllText(path + ".txt",
                "runtime evidence capture failed: " + ex.GetType().Name + ": " + ex.Message);
        }
    }

    private static void Place(Ctx ctx, int charIndex, string rowField)
    {
        var cardData = ctx.Chars[charIndex];
        var cd = Call(ctx.DataManager, "GetCharacterById", GetMember(cardData, "CharacterId"));
        Assert.IsNotNull(cd, "CharacterData 未找到：" + charIndex);
        Call(ctx.BattleUI, "PlaceUnit", cardData, cd, Row(ctx.BattleUI, rowField));
    }

    private static void PlaceHero(Ctx ctx, string rowField)
    {
        var hd = Call(ctx.DataManager, "GetHeroById", GetMember(ctx.Hero, "HeroId"));
        Assert.IsNotNull(hd, "HeroData 未找到");
        Call(ctx.BattleUI, "PlaceHero", hd, ctx.Hero, Row(ctx.BattleUI, rowField));
    }

    /// <summary>
    /// 期望的我方单位身体视觉（Person Visual Binding V0.1）：
    /// AppearanceKey（PersonId → exact Person，无法解析时兜底 SourceId 作表现键）
    /// → 统一配置的 Battle 视觉 → 回退既有 playerBodyPlaceholder。
    /// 与生产代码 BattleUI.ApplyPlayerBodyVisual 使用同一解析链。
    /// </summary>
    private static Sprite ExpectedPlayerBodySprite(Ctx ctx, object unit, Sprite playerPlaceholder)
    {
        var resolverType = GetType("PersonVisualResolver");
        var catalogType = GetType("PersonVisualBindingCatalog");
        var surfaceType = GetType("PersonVisualSurface");
        Assert.IsNotNull(resolverType, "PersonVisualResolver 未找到");
        Assert.IsNotNull(catalogType, "PersonVisualBindingCatalog 未找到");

        string personId = (string)Call(ctx.BattleUI, "GetPersonIdOfUnit", unit);
        string key = null;
        if (!string.IsNullOrEmpty(personId))
        {
            key = (string)resolverType.GetMethod("ResolveAppearanceKey", F)
                .Invoke(null, new object[] { ctx.Collection, personId });
        }
        if (string.IsNullOrEmpty(key)) key = (string)GetMember(unit, "SourceId");

        var catalog = catalogType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)
            .GetValue(null, null);
        if (catalog == null || string.IsNullOrEmpty(key)) return playerPlaceholder;

        object entry = catalogType.GetMethod("FindEntry", F).Invoke(catalog, new object[] { key });
        if (entry == null) return playerPlaceholder;

        var sprite = (Sprite)entry.GetType().GetMethod("ResolveSprite", F)
            .Invoke(entry, new object[] { System.Enum.ToObject(surfaceType, 1 /* Battle */) });
        return sprite != null ? sprite : playerPlaceholder;
    }

    private sealed class TransformScan
    {
        internal static readonly string[] LegacyDead = { "BtnAttackOp", "BtnSkillOp", "BtnGuardOp", "ItemCardRoot" };

        internal static List<string> FoundLegacy()
        {
            var found = new List<string>();
            foreach (var t in Object.FindObjectsOfType<Transform>(true))
            {
                if (t == null) continue;
                foreach (var name in LegacyDead)
                    if (t.name.Trim() == name) found.Add(t.name);
            }
            return found;
        }
    }

    // ---------- 测试 1：1 / 2 / 3 人排 Formation Presentation（战前布阵，真实 PlaceUnit / PlaceHero 路径） ----------

    [UnityTest]
    public IEnumerator FormationPresentation_OneTwoThreeUnitRows_InProductBattle()
    {
        var ctx = new Ctx();
        yield return EnterFormalBattle(ctx);

        float single = GetFloatField(ctx.BattleUI, "singleUnitVisualOffset");
        float two = GetFloatField(ctx.BattleUI, "twoUnitVisualSpacing");
        float three = GetFloatField(ctx.BattleUI, "threeUnitVisualSpacing");
        float rowOffset = ((Vector2)GetField(ctx.BattleUI, "rowLocalVisualOffset")).y;
        Assert.Less(two, three, "两人排间距必须小于三人排间距（两人排聚拢规则）");

        // 1 人排
        PlaceHero(ctx, "playerFrontRow");
        yield return null;
        var front = CardsInRow(ctx.BattleUI, Row(ctx.BattleUI, "playerFrontRow"));
        Assert.AreEqual(1, front.Count, "Front 排应有 1 个单位");
        Assert.AreEqual(single + rowOffset, VisualY(front[0]), 0.01f, "1 人排必须视觉居中");
        yield return CaptureShotAndWait("wp_d_formation_one_unit.png");

        // 2 人排（聚拢）
        Place(ctx, 0, "playerMiddleRow");
        Place(ctx, 1, "playerMiddleRow");
        yield return null;
        var middle = CardsInRow(ctx.BattleUI, Row(ctx.BattleUI, "playerMiddleRow"));
        Assert.AreEqual(2, middle.Count, "Middle 排应有 2 个单位");
        float m0 = VisualY(middle[0]) - rowOffset;
        float m1 = VisualY(middle[1]) - rowOffset;
        Assert.AreEqual(two * 0.5f, Mathf.Max(m0, m1), 0.01f, "两人排外侧偏移 = twoUnitVisualSpacing / 2");
        Assert.AreEqual(-two * 0.5f, Mathf.Min(m0, m1), 0.01f, "两人排必须对称聚拢");
        Assert.Less(Mathf.Abs(Mathf.Max(m0, m1)), three, "两人排不得使用三人排最外侧偏移");
        yield return CaptureShotAndWait("wp_d_formation_two_unit_gathered.png");

        // 3 人排（展开）
        PlaceHero(ctx, "playerBackRow");
        Place(ctx, 0, "playerBackRow");
        Place(ctx, 1, "playerBackRow");
        yield return null;
        var back = CardsInRow(ctx.BattleUI, Row(ctx.BattleUI, "playerBackRow"));
        Assert.AreEqual(3, back.Count, "Back 排应有 3 个单位");

        var ys = new List<float> { VisualY(back[0]) - rowOffset, VisualY(back[1]) - rowOffset, VisualY(back[2]) - rowOffset };
        ys.Sort();
        Assert.AreEqual(-three, ys[0], 0.01f, "三人排最外侧（Right 方向）偏移 = -threeUnitVisualSpacing");
        Assert.AreEqual(0f, ys[1], 0.01f, "三人排中位必须在排中心");
        Assert.AreEqual(three, ys[2], 0.01f, "三人排最外侧（Left 方向）偏移 = threeUnitVisualSpacing");

        // 视觉顺序必须与内部逻辑顺序一致（战前阶段逻辑列为 _pendingCols 写入的预备列）
        var ordered = new List<GameObject>(back);
        ordered.Sort((a, b) => VisualY(b).CompareTo(VisualY(a)));
        var pendingCols = (System.Collections.IDictionary)GetField(ctx.BattleUI, "_pendingCols");
        var colIdx = new List<int>();
        foreach (var card in ordered)
        {
            string cid = (string)Call(ctx.BattleUI, "GetCardId", card.name);
            Assert.IsNotNull(cid, "单位卡名无法解析 CardId：" + card.name);
            Assert.IsTrue(pendingCols.Contains(cid), "单位卡没有对应逻辑列：" + cid);
            colIdx.Add((int)Call(ctx.BattleUI, "ColToIdx", pendingCols[cid]));
        }
        Assert.LessOrEqual(colIdx[0], colIdx[1], "排内视觉顺序与内部逻辑列顺序不一致");
        Assert.LessOrEqual(colIdx[1], colIdx[2], "排内视觉顺序与内部逻辑列顺序不一致");

        yield return CaptureShotAndWait("wp_d_formation_three_unit.png");
    }

    // ---------- 测试 2：开战后视觉宿主按运行时事实刷新 ----------

    [UnityTest]
    public IEnumerator ProductBattle_VisualHosts_RefreshFromRuntimeFacts()
    {
        var ctx = new Ctx();
        yield return EnterFormalBattle(ctx);

        PlaceHero(ctx, "playerBackRow");
        Place(ctx, 0, "playerBackRow");
        Place(ctx, 1, "playerBackRow");
        Call(ctx.BattleUI, "OnStartBattle", null);
        yield return null;

        ctx.BattleSystem = GetField(ctx.BattleUI, "_battleSystem");
        Assert.IsNotNull(ctx.BattleSystem, "BattleSystem 未建立（OnStartBattle 后）");

        var v1 = GetField(ctx.BattleUI, "v1Runtime");
        Assert.IsNotNull(v1, "BattleV1Runtime 未绑定");
        float wait = 0f;
        while ((BattleV1TurnFlow.PhaseType)GetProperty(v1, "TurnPhase") != BattleV1TurnFlow.PhaseType.PlayerTurn && wait < 15f)
        {
            wait += Time.deltaTime;
            yield return null;
        }
        yield return null;

        // 1) Background Host（可替换 Sprite / 不遮挡 UI）
        var bg = GameObject.Find("BattleBackground");
        Assert.IsNotNull(bg, "BattleBackground 宿主未进入产品路径");
        var bgImage = bg.GetComponent<Image>();
        Assert.IsNotNull(bgImage, "BattleBackground 缺少 Image");
        Assert.IsNotNull(bgImage.sprite, "BattleBackground 未绑定可替换 Sprite");
        Assert.IsFalse(bgImage.raycastTarget, "Battle Background 不得参与 Raycast");
        Assert.AreEqual(0, bg.transform.GetSiblingIndex(), "Battle Background 必须位于最底层");

        // 2) 六排 RowVisual（复用现有 Row Rect）
        string[] rows = { "playerFrontRow", "playerMiddleRow", "playerBackRow", "enemyFrontRow", "enemyMiddleRow", "enemyBackRow" };
        foreach (var r in rows)
            Assert.IsNotNull(Row(ctx.BattleUI, r).Find("RowVisual"), "Row 缺少 RowVisual 视觉子对象：" + r);

        // 3) 我方单位：完整小人占位 + 独立 HP 宿主
        var playerSprite = GetField(ctx.BattleUI, "playerBodyPlaceholder") as Sprite;
        Assert.IsNotNull(playerSprite, "playerBodyPlaceholder 未绑定");
        var data = Call(ctx.BattleSystem, "GetBattleData");
        var playerUnits = (IList)GetMember(data, "PlayerUnits");
        Assert.Greater(playerUnits.Count, 0, "本场无我方单位");

        foreach (var unit in playerUnits)
        {
            var card = GameObject.Find("Card_" + GetMember(unit, "UnitId"));
            Assert.IsNotNull(card, "我方单位卡未找到：" + GetMember(unit, "UnitId"));
            var body = card.transform.Find("Anchor_Visual/Body_Visual");
            Assert.IsNotNull(body, "我方单位卡缺少 Body_Visual 宿主");
            var bodyImage = body.GetComponent<Image>();
            Assert.IsNotNull(bodyImage.sprite, "Body_Visual 未绑定占位 Sprite（小人不可见）");
            // Person Visual Binding V0.1（取代 WP-D 旧契约「所有玩家共用一张 player placeholder」）：
            // 我方单位显示「其外观配置的 Battle 视觉」；该人物未配置外观键时回退既有 playerBodyPlaceholder。
            var expectedBody = ExpectedPlayerBodySprite(ctx, unit, playerSprite);
            Assert.AreEqual(expectedBody, bodyImage.sprite,
                "我方单位必须显示其外观配置（或回退 playerBodyPlaceholder）的身体视觉："
                + GetMember(unit, "UnitId"));
            // M4（M3-R2）：旧 enemyBodyPlaceholder 字段与 Scene 序列化条目已物理删除 →
            // 「我方不得使用敌方占位身体」由**结构性保证**承担（敌我各自独立 Resolver；
            // 敌方 fallback 为 Resolver 内 runtime sprite）并另由 EnemyPresentationBattleWiringTests
            // W2（运行时代码 0 命中 + 字段不存在）与 PlayMode 敌方断言覆盖。

            var hp = card.transform.Find("Text_HP").GetComponent<TMP_Text>();
            Assert.AreEqual(GetMember(unit, "CurrentHP") + "/" + GetMember(unit, "MaxHP"), hp.text,
                "HP 宿主未按数据刷新（且 Shield 不得再拼进 HP 字符串）");
        }

        // 4) Shield 独立宿主（数值仍由 BattleUnit.Shield 决定）
        var firstUnit = playerUnits[0];
        Call(ctx.BattleSystem, "ApplyBattleShield", firstUnit, 7);
        Call(ctx.BattleUI, "RefreshFieldUI");
        yield return null;

        var shieldedCard = GameObject.Find("Card_" + GetMember(firstUnit, "UnitId"));
        var shieldAnchor = shieldedCard.transform.Find("Anchor_Shield");
        Assert.IsNotNull(shieldAnchor, "缺少独立 Shield 宿主 Anchor_Shield");
        Assert.IsTrue(shieldAnchor.gameObject.activeSelf, "Shield > 0 时 Anchor_Shield 必须显示");
        Assert.AreEqual("7", shieldAnchor.Find("Text_Shield").GetComponent<TMP_Text>().text,
            "Shield 宿主未按 BattleUnit.Shield 刷新");

        // 5) 敌方单位：身体视觉来自 Enemy Presentation Authority（M3 Slice 1D-1）+ 稳定 Intent 宿主
        // M4（M3-R2）：旧 enemyBodyPlaceholder 字段已物理删除 → 排他基准改为 Resolver 自身的 runtime fallback
        //（见下方断言：敌方身体视觉必须来自 Authority，不得退化为 fallback）。

        var resolver = GetProperty(ctx.BattleUI, "PresentationResolver");
        Assert.IsNotNull(resolver, "BattleUI 必须暴露 Prep / Battle 共用的 PresentationResolver");
        Assert.AreSame(resolver, GetProperty(ctx.BattleUI, "PresentationResolver"),
            "PresentationResolver 必须是同一实例（Prep 与 Battle 共用）");

        var enemyUnits = (IList)GetMember(data, "EnemyUnits");
        Assert.Greater(enemyUnits.Count, 0, "本场无敌方单位");

        var enemySpritesByMonster = new Dictionary<string, Sprite>();

        int intentShown = 0;
        foreach (var unit in enemyUnits)
        {
            var monsterId = (string)GetMember(unit, "SourceId");
            var card = GameObject.Find("Card_" + monsterId);
            Assert.IsNotNull(card, "敌方单位卡未找到：" + monsterId);
            var body = card.transform.Find("Anchor_Visual/Body_Visual");
            Assert.IsNotNull(body, "敌方单位卡缺少 Body_Visual 宿主");

            var sprite = body.GetComponent<Image>().sprite;
            var expected = Call(resolver, "Resolve", monsterId) as Sprite;
            Assert.IsNotNull(expected, "Resolver 必须返回 Sprite（缺失时返回 fallback，绝不返回 null）");
            Assert.AreSame(expected, sprite, "敌方身体视觉必须来自 Presentation Authority：" + monsterId);
            Assert.AreNotEqual(ResolverStaticProperty(resolver, "FallbackSprite") as Sprite, sprite,
                "敌方身体视觉不得退化为 Resolver fallback（旧 enemyBodyPlaceholder 字段已删除）：" + monsterId);

            enemySpritesByMonster[monsterId] = sprite;

            var intent = card.transform.Find("Anchor_Intent/Text_Intent");
            Assert.IsNotNull(intent, "敌方单位卡缺少稳定 Intent 宿主");
            var icon = card.transform.Find("Anchor_Intent/Image_IntentIcon").GetComponent<Image>();
            if (icon.enabled && icon.sprite != null) intentShown++;
        }
        Assert.Greater(intentShown, 0, "战斗开始后至少一名敌人应显示粗粒度 Intent");

        // 一怪一图：不同 MonsterId 必须得到不同 Sprite（临时素材的可区分性）
        if (enemySpritesByMonster.Count >= 2)
        {
            var distinct = new HashSet<Sprite>();
            foreach (var s in enemySpritesByMonster.Values) if (s != null) distinct.Add(s);
            Assert.AreEqual(enemySpritesByMonster.Count, distinct.Count,
                "不同 MonsterId 必须得到不同 Sprite（每只怪物一张）：" + string.Join(", ", enemySpritesByMonster.Keys));
        }

        // 6) Shared Hand BattleCard 视觉槽（Art / Description）
        var cardViewType = GetType("BattleCardView");
        Assert.IsNotNull(cardViewType, "BattleCardView 未找到");
        var views = Object.FindObjectsOfType(cardViewType, true);
        Assert.Greater(views.Length, 0, "共享手牌未生成 BattleCard（视觉槽无法验证）");

        bool artOk = false, descBound = false;
        foreach (var v in views)
        {
            var art = GetMember(v, "artImage") as Image;
            if (art != null && art.sprite != null) artOk = true;
            if (GetMember(v, "descriptionText") != null) descBound = true;
        }
        Assert.IsTrue(artOk, "BattleCard Art 槽未生效（无可替换 Art 占位）");
        Assert.IsTrue(descBound, "BattleCard Description 槽未绑定");

        // 7) 现有操作入口未被破坏，且不重新出现 WP-C 已退出的旧 UI
        Assert.IsNotNull(GetField(ctx.BattleUI, "btnCancelOp"), "Current Card Cancel 按钮缺失");
        var endTurn = GetField(ctx.BattleUI, "btnEndTurn") as Button;
        Assert.IsNotNull(endTurn, "End Turn 按钮缺失");
        Assert.IsTrue(endTurn.gameObject.activeInHierarchy, "End Turn 按钮未激活");
        Assert.IsEmpty(TransformScan.FoundLegacy(), "战斗中重新出现 WP-C 已退出的旧 UI 对象");

        yield return CaptureShotAndWait("wp_d_battle_overview.png");
        yield return CaptureShotAndWait("wp_d_hp_shield_intent.png");
    }

    // ==================== M3 Slice 1D-1：Enemy Presentation Authority 产品路径验证 ====================

    /// <summary>
    /// 从正式产品路径（Town → Expedition → Battle）验证敌方身体视觉：
    ///   - Prep 阶段敌方卡（EnemyPrep_&lt;MonsterId&gt;）与 Battle 阶段敌方单位卡都经**同一** Resolver；
    ///   - 一怪一图（不同 MonsterId → 不同 Sprite）；
    ///   - 旧 enemyBodyPlaceholder 字段已物理删除（M4），敌方视觉不得退化为 Resolver fallback；
    ///   - 缺失配置 → fallback sprite + WarnOnce（fallback 只在 Resolver 内）。
    /// </summary>
    [UnityTest]
    public IEnumerator EnemyPresentation_PrepAndBattle_ResolveFromAuthority()
    {
        var ctx = new Ctx();
        Debug.Log("[M3 1D-1 PM] step1: enter formal battle (Town → Expedition → Battle/Prep)");
        yield return EnterFormalBattle(ctx);
        Debug.Log("[M3 1D-1 PM] step2: battle/Prep entered");

        var resolver = GetProperty(ctx.BattleUI, "PresentationResolver");
        Assert.IsNotNull(resolver, "BattleUI 必须暴露 Prep / Battle 共用的 PresentationResolver");
        Debug.Log("[M3 1D-1 PM] step3: resolver acquired");

        // M4（M3-R2）：旧 enemyBodyPlaceholder 字段已物理删除 → 排他基准 = Resolver 的 runtime fallback
        var enemyFallback = ResolverStaticProperty(resolver, "FallbackSprite") as Sprite;
        Assert.IsNotNull(enemyFallback, "Resolver 必须提供 runtime fallback sprite");

        // ---- Prep：敌方格每张卡的身体视觉来自 Authority ----
        var prepSprites = new Dictionary<string, Sprite>();
        foreach (var go in Object.FindObjectsOfType<GameObject>(true))
        {
            if (go == null || !go.name.StartsWith("EnemyPrep_")) continue;

            string monsterId = go.name.Substring("EnemyPrep_".Length);
            var body = go.transform.Find("Anchor_Visual/Body_Visual");
            Assert.IsNotNull(body, "Prep 敌方卡缺少 Body_Visual 宿主：" + monsterId);

            var sprite = body.GetComponent<Image>().sprite;
            var expected = Call(resolver, "Resolve", monsterId) as Sprite;
            Assert.AreSame(expected, sprite, "Prep 敌方身体视觉必须来自 Presentation Authority：" + monsterId);
            Assert.AreNotEqual(enemyFallback, sprite, "Prep 不得退化为 Resolver fallback：" + monsterId);

            prepSprites[monsterId] = sprite;
        }
        Assert.Greater(prepSprites.Count, 0, "Prep 阶段必须显示敌方卡（EnemyPrep_<MonsterId>）");
        Debug.Log("[M3 1D-1 PM] step4: prep cards checked count=" + prepSprites.Count);

        if (prepSprites.Count >= 2)
        {
            var distinctPrep = new HashSet<Sprite>();
            foreach (var s in prepSprites.Values) if (s != null) distinctPrep.Add(s);
            Assert.AreEqual(prepSprites.Count, distinctPrep.Count, "Prep：不同 MonsterId 必须得到不同 Sprite");
        }
        Debug.Log("[M3 1D-1 PM] step5: write runtime sprite evidence (Prep, offscreen)");
        WriteRuntimeSpriteEvidence(new List<string>(prepSprites.Keys), new List<Sprite>(prepSprites.Values),
            "m3_prep_enemy_presentation_runtime.png");
        Debug.Log("[M3 1D-1 PM] step5b: attempt real Game View screenshot (Prep)");
        yield return CaptureGameViewAndReport("m3_prep_gameview.png");
        Debug.Log("[M3 1D-1 PM] step6: prep evidence written");

        // ---- Battle：同一 Resolver 实例继续解析（Prep 与 Battle 不各自找图） ----
        Debug.Log("[M3 1D-1 PM] step7: start battle");
        PlaceHero(ctx, "playerBackRow");
        Place(ctx, 0, "playerBackRow");
        Place(ctx, 1, "playerBackRow");
        Call(ctx.BattleUI, "OnStartBattle", null);
        yield return null;
        Debug.Log("[M3 1D-1 PM] step8: battle started");

        ctx.BattleSystem = GetField(ctx.BattleUI, "_battleSystem");
        Assert.IsNotNull(ctx.BattleSystem, "BattleSystem 未建立（OnStartBattle 后）");
        Assert.AreSame(resolver, GetProperty(ctx.BattleUI, "PresentationResolver"),
            "Prep 与 Battle 必须共用同一 Resolver 实例");

        var data = Call(ctx.BattleSystem, "GetBattleData");
        var enemyUnits = (IList)GetMember(data, "EnemyUnits");
        Assert.Greater(enemyUnits.Count, 0, "本场无敌方单位");

        var battleLabels = new List<string>();
        var battleSprites = new List<Sprite>();

        foreach (var unit in enemyUnits)
        {
            string monsterId = (string)GetMember(unit, "SourceId");
            var card = GameObject.Find("Card_" + monsterId);
            Assert.IsNotNull(card, "敌方单位卡未找到：" + monsterId);

            var body = card.transform.Find("Anchor_Visual/Body_Visual");
            Assert.IsNotNull(body, "敌方单位卡缺少 Body_Visual 宿主");

            var sprite = body.GetComponent<Image>().sprite;
            var expected = Call(resolver, "Resolve", monsterId) as Sprite;
            Assert.AreSame(expected, sprite, "Battle 敌方身体视觉必须来自 Presentation Authority：" + monsterId);
            Assert.AreNotEqual(enemyFallback, sprite, "Battle 不得退化为 Resolver fallback：" + monsterId);

            if (prepSprites.ContainsKey(monsterId))
            {
                Assert.AreSame(prepSprites[monsterId], sprite, "Prep 与 Battle 必须得到同一 Sprite：" + monsterId);
            }

            battleLabels.Add("battle:" + monsterId + " asset=" + (sprite != null ? sprite.name : "<null>"));
            battleSprites.Add(sprite);
        }

        WriteRuntimeSpriteEvidence(battleLabels, battleSprites, "m3_battle_enemy_presentation_runtime.png");
        Debug.Log("[M3 1D-1 PM] step8b: battle evidence written");

        yield return CaptureGameViewAndReport("m3_battle_gameview.png");
        Debug.Log("[M3 1D-1 PM] step8c: battle Game View attempt done");

        // ---- 缺失配置：fallback + WarnOnce（fallback 只在 Resolver 内；BattleUI 不判断） ----
        Debug.Log("[M3 1D-1 PM] step9: fallback + warn once");
        LogAssert.ignoreFailingMessages = true;   // 回退告警由 WarnIssued / WarnCount 断言
        CallResolverStatic(resolver, "ResetWarnOnce");
        Assert.IsFalse((bool)ResolverStaticProperty(resolver, "WarnIssued"), "WarnOnce 状态必须可重置");

        var fallbackSprite = ResolverStaticProperty(resolver, "FallbackSprite") as Sprite;
        Assert.IsNotNull(fallbackSprite, "Resolver 必须提供 fallback sprite");

        var missingSprite = Call(resolver, "Resolve", "mon_not_registered") as Sprite;
        Assert.AreSame(fallbackSprite, missingSprite, "未登记 MonsterId 必须返回 fallback sprite（不静默、不返回 null）");
        Assert.IsTrue((bool)ResolverStaticProperty(resolver, "WarnIssued"), "回退必须产生 WarnOnce 告警");
        Assert.AreEqual(1, (int)ResolverStaticProperty(resolver, "WarnCount"), "回退次数必须被计数");

        Assert.AreSame(fallbackSprite, Call(resolver, "Resolve", "mon_not_registered") as Sprite);
        Assert.AreEqual(2, (int)ResolverStaticProperty(resolver, "WarnCount"), "每次回退都必须计数（日志仍只一次）");

        LogAssert.ignoreFailingMessages = false;
    }

    private static object ResolverStaticProperty(object resolver, string name)
    {
        var p = resolver.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Static);
        Assert.IsNotNull(p, "Resolver 静态属性不存在：" + name);
        return p.GetValue(null, null);
    }

    private static void CallResolverStatic(object resolver, string name)
    {
        var m = resolver.GetType().GetMethod(name, BindingFlags.Public | BindingFlags.Static);
        Assert.IsNotNull(m, "Resolver 静态方法不存在：" + name);
        m.Invoke(null, null);
    }
}
