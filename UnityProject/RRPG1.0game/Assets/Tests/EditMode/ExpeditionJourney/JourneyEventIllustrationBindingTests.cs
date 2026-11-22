using TMPro;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

/// <summary>
/// Journey Event Illustration Binding V0.1｜长期契约测试（EditMode）。
///
/// 冻结的是**解析契约与配置面边界**（不是数据形态、不是正式 Event System）：
///   1. 两个不同 PresentationKey → 两张不同 Illustration（逐事件插图真的生效）；
///   2. 未配置 Key → 确定性走类别级 fallback（Battle / Content / Branch 各自默认图）；
///   3. 空 Key 不参与解析（不猜、不随机、不塌缩）+ 明确诊断；重复 Key 保留第一条 + 明确诊断；
///   4. 配置行无图 / 类别图也为空 → 回退 / 隐藏 Illustration Host，卡面其它内容继续正常工作；
///   5. 只改插图不得改变 Title / Body / Options / NodeId / Option 角色（Node legality 与 Battle 入口参数不变）；
///   6. Branch 当前是「路线选择表面」，走类别 fallback 且**不伪造**逐 Branch 表现键；
///   7. Catalog 不是 Event Content Authority（条目只有 PresentationKey + Illustration），
///      且「唯一卡面 / 唯一 Catalog」不得被第二套实现替代。
///
/// 说明：asmdef 测试程序集不能引用预定义程序集 Assembly-CSharp，
/// 运行时类型（Catalog / Resolver / ExpeditionJourneyView / JourneyEventCard*）全部通过反射访问
/// （与本目录既有测试一致；JourneyEncounterKind / JourneyNodeRef 直接引用 ExpeditionJourney.Runtime）。
/// </summary>
public class JourneyEventIllustrationBindingTests
{
    private const BindingFlags F =
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

    private static Type Asm(string name)
    {
        var t = Type.GetType(name + ", Assembly-CSharp");
        Assert.IsNotNull(t, "未找到 Assembly-CSharp 类型：" + name);
        return t;
    }

    private static Type EntryType => Asm("JourneyEventPresentationEntry");
    private static Type CatalogType => Asm("JourneyEventPresentationCatalog");
    private static Type ResolverType => Asm("JourneyEventIllustrationResolver");
    private static Type ViewType => Asm("ExpeditionJourneyView");
    private static Type CardViewType => Asm("JourneyEventCardView");

    private Sprite _spriteA;
    private Sprite _spriteB;
    private Sprite _battleFallback;
    private Sprite _contentFallback;
    private Sprite _branchFallback;

    private GameObject _host;
    private UnityEngine.Object _catalogObject;

    [SetUp]
    public void SetUp()
    {
        CallStatic(ResolverType, "ClearWarningsForTests");

        _spriteA = NewSprite();
        _spriteB = NewSprite();
        _battleFallback = NewSprite();
        _contentFallback = NewSprite();
        _branchFallback = NewSprite();
    }

    [TearDown]
    public void TearDown()
    {
        CallStatic(CatalogType, "ResetForTests");
        CallStatic(ResolverType, "ClearWarningsForTests");
        if (_catalogObject != null) UnityEngine.Object.DestroyImmediate(_catalogObject);
        _catalogObject = null;
        if (_cardHost != null) UnityEngine.Object.DestroyImmediate(_cardHost);
        _cardHost = null;
        if (_host != null) UnityEngine.Object.DestroyImmediate(_host);
        _host = null;
    }

    private static Sprite NewSprite()
    {
        return Sprite.Create(new Texture2D(4, 4), new Rect(0f, 0f, 4f, 4f), new Vector2(0.5f, 0.5f));
    }

    // ==================== 反射辅助 ====================

    private static object NewEntry(string presentationKey, Sprite illustration)
    {
        object entry = Activator.CreateInstance(EntryType);
        EntryType.GetField("PresentationKey", F).SetValue(entry, presentationKey);
        EntryType.GetField("Illustration", F).SetValue(entry, illustration);
        return entry;
    }

    /// <summary>注入内存配置面（生产读取路径 = Resources 固定路径；此处只替换实例，不写盘）。</summary>
    private object UseCatalog(params object[] rows)
    {
        var array = Array.CreateInstance(EntryType, rows.Length);
        for (int i = 0; i < rows.Length; i++) array.SetValue(rows[i], i);

        var catalog = ScriptableObject.CreateInstance(CatalogType);
        Call(catalog, "SetEntriesForTests", array);
        CallStatic(CatalogType, "SetInstanceForTests", catalog);
        _catalogObject = catalog;
        return catalog;
    }

    private static T Field<T>(object o, string name)
    {
        var f = o.GetType().GetField(name, F);
        Assert.IsNotNull(f, "字段不存在：" + name + " @ " + o.GetType().FullName);
        return (T)f.GetValue(o);
    }

    private static void SetField(object o, string name, object value)
    {
        var f = o.GetType().GetField(name, F);
        Assert.IsNotNull(f, "字段不存在：" + name + " @ " + o.GetType().FullName);
        f.SetValue(o, value);
    }

    private static object Call(object o, string method, params object[] args)
    {
        var m = o.GetType().GetMethod(method, F);
        Assert.IsNotNull(m, "方法不存在：" + method + " @ " + o.GetType().FullName);
        return m.Invoke(o, args);
    }

    private static object CallStatic(Type type, string method, params object[] args)
    {
        var m = type.GetMethod(method, F);
        Assert.IsNotNull(m, "静态方法不存在：" + method + " @ " + type.FullName);
        return m.Invoke(null, args);
    }

    private static object Resolve(string presentationKey, JourneyEncounterKind kind, Sprite categoryFallback)
    {
        // 反射必须显式传满 4 个参数（可选参数不会被自动填充）。
        return CallStatic(ResolverType, "ResolveForEvent", presentationKey, kind, categoryFallback, null);
    }

    /// <summary>按契约：逐事件插图 → 类别 fallback → null。</summary>
    private static object ResolveCategoryOnly(JourneyEncounterKind kind, Sprite categoryFallback)
    {
        return CallStatic(ResolverType, "ResolveCategoryFallback", kind, categoryFallback);
    }

    private object NewJourneyView()
    {
        _host = new GameObject("JourneyEventIllustrationTestRoot", typeof(RectTransform));
        object view = _host.AddComponent(ViewType);
        SetField(view, "battleEncounterIllustration", _battleFallback);
        SetField(view, "contentEncounterIllustration", _contentFallback);
        SetField(view, "branchIllustration", _branchFallback);
        return view;
    }

    private static IList Options(object config)
    {
        return (IList)Field<object>(config, "Options");
    }

    // ==================== 1. 不同 Key → 不同插图 ====================

    [Test]
    public void Resolve_DifferentPresentationKeys_YieldDifferentIllustrations()
    {
        UseCatalog(
            NewEntry("node_event_a", _spriteA),
            NewEntry("node_event_b", _spriteB));

        object a = Resolve("node_event_a", JourneyEncounterKind.Battle, _battleFallback);
        object b = Resolve("node_event_b", JourneyEncounterKind.Battle, _battleFallback);

        Assert.AreSame(_spriteA, Field<Sprite>(a, "Illustration"), "Event A 必须使用自己的插图");
        Assert.AreSame(_spriteB, Field<Sprite>(b, "Illustration"), "Event B 必须使用自己的插图");
        Assert.AreNotSame(Field<Sprite>(a, "Illustration"), Field<Sprite>(b, "Illustration"),
            "两个不同 PresentationKey 不得共用同一张图（本任务的核心目标）");
        Assert.IsFalse(Field<bool>(a, "UsedCategoryFallback"), "命中具体键时不得声称走了类别 fallback");
        Assert.AreEqual("binding:node_event_a", Field<string>(a, "Source"));
        Assert.AreEqual("binding:node_event_b", Field<string>(b, "Source"));

        LogAssert.NoUnexpectedReceived();
    }

    [Test]
    public void Resolve_ConcreteKeyBeatsCategoryFallback_ForBothBattleAndContent()
    {
        UseCatalog(
            NewEntry("node_battle_x", _spriteA),
            NewEntry("node_content_x", _spriteB));

        object battle = Resolve("node_battle_x", JourneyEncounterKind.Battle, _battleFallback);
        object content = Resolve("node_content_x", JourneyEncounterKind.Content, _contentFallback);

        Assert.AreSame(_spriteA, Field<Sprite>(battle, "Illustration"), "具体 Key 必须优先于 Battle 类别图");
        Assert.AreSame(_spriteB, Field<Sprite>(content, "Illustration"), "具体 Key 必须优先于 Content 类别图");
        Assert.IsFalse(Field<bool>(battle, "UsedCategoryFallback"));
        Assert.IsFalse(Field<bool>(content, "UsedCategoryFallback"));

        LogAssert.NoUnexpectedReceived();
    }

    // ==================== 2. 未配置 Key → 类别 fallback ====================

    [Test]
    public void Resolve_UnconfiguredKey_UsesCategoryFallback_WithDiagnostic()
    {
        LogAssert.Expect(LogType.Warning, new Regex("PresentationKey=node_unknown 没有配置行"));

        UseCatalog(NewEntry("node_known", _spriteA));

        object r = Resolve("node_unknown", JourneyEncounterKind.Content, _contentFallback);

        Assert.AreSame(_contentFallback, Field<Sprite>(r, "Illustration"),
            "未配置 Key 必须回退到 Content 类别图（不得借用其它事件的图）");
        Assert.IsTrue(Field<bool>(r, "UsedCategoryFallback"));
        Assert.AreEqual("category:Content", Field<string>(r, "Source"));
        Assert.AreEqual("node_unknown", Field<string>(r, "PresentationKey"),
            "fallback 仍保留请求的表现键（诊断不丢上下文）");
    }

    [Test]
    public void Resolve_CategoryOnly_DoesNotWarn_AndUsesCategoryIllustration()
    {
        object r = ResolveCategoryOnly(JourneyEncounterKind.Branch, _branchFallback);

        Assert.AreSame(_branchFallback, Field<Sprite>(r, "Illustration"), "Branch 类别面必须使用类别图");
        Assert.IsTrue(Field<bool>(r, "UsedCategoryFallback"));
        Assert.AreEqual("category:Branch", Field<string>(r, "Source"));
        Assert.IsTrue(string.IsNullOrEmpty(Field<string>(r, "PresentationKey")),
            "类别-only 路径不得伪造表现键");

        // 类别-only 是设计内路径（Branch 无稳定唯一键），不得输出「空键」告警。
        LogAssert.NoUnexpectedReceived();
    }

    [Test]
    public void Resolve_ConfiguredRowWithoutIllustration_FallsBackToCategory()
    {
        LogAssert.Expect(LogType.Warning, new Regex("node_no_image 的配置行未绑定 Illustration"));

        UseCatalog(
            NewEntry("node_ok", _spriteA),
            NewEntry("node_no_image", null));

        object r = Resolve("node_no_image", JourneyEncounterKind.Battle, _battleFallback);

        Assert.AreSame(_battleFallback, Field<Sprite>(r, "Illustration"),
            "生效行没有图时必须回退类别图，不得静默改用其它行的图");
        Assert.IsTrue(Field<bool>(r, "UsedCategoryFallback"));
    }

    // ==================== 3. 配置安全：空 Key / 重复 Key ====================

    [Test]
    public void Resolve_EmptyPresentationKey_DoesNotParticipate_AndWarnsOnce()
    {
        LogAssert.Expect(LogType.Warning, new Regex("PresentationKey 为空"));

        UseCatalog(NewEntry("node_a", _spriteA));

        object r = Resolve(string.Empty, JourneyEncounterKind.Battle, _battleFallback);

        Assert.AreSame(_battleFallback, Field<Sprite>(r, "Illustration"), "空键必须走类别 fallback");
        Assert.IsTrue(string.IsNullOrEmpty(Field<string>(r, "PresentationKey")),
            "空键解析结果不得携带任何表现键（不塌缩到已配置行）");
        Assert.IsTrue(Field<bool>(r, "UsedCategoryFallback"));

        // 去重：同一会话内重复空键解析不得重复告警
        Resolve(string.Empty, JourneyEncounterKind.Battle, _battleFallback);
        LogAssert.NoUnexpectedReceived();
    }

    // ==================== 3b. 配置安全（Catalog 侧诊断） ====================

    [Test]
    public void Catalog_EmptyKeyRow_IsIgnoredWithDiagnostic_AndValidRowStillResolves()
    {
        LogAssert.Expect(LogType.Warning, new Regex("PresentationKey 为空（行 1）"));

        object catalog = UseCatalog(
            NewEntry("node_a", _spriteA),
            NewEntry(string.Empty, _spriteB));   // 制作人最容易误配：只拖了图、没填键

        Assert.IsNull(Call(catalog, "FindEntry", string.Empty), "空键行不得被解析使用");
        Assert.IsNotNull(Call(catalog, "FindEntry", "node_a"), "同一资产中的有效行不得因存在无效行而失效");

        object r = Resolve("node_a", JourneyEncounterKind.Battle, _battleFallback);
        Assert.AreSame(_spriteA, Field<Sprite>(r, "Illustration"));

        // 去重：同一索引构建期内重复查询不得重复告警
        Call(catalog, "FindEntry", string.Empty);
        Call(catalog, "FindEntry", "node_a");
        LogAssert.NoUnexpectedReceived();
    }

    [Test]
    public void Catalog_DuplicateKey_KeepsFirstRowWithDiagnostic_AndResolutionIsDeterministic()
    {
        LogAssert.Expect(LogType.Warning,
            new Regex("重复 PresentationKey：node_dup（生效：行 0；已忽略：行 1）"));

        object catalog = UseCatalog(
            NewEntry("node_dup", _spriteA),
            NewEntry("node_dup", _spriteB));

        object entry = Call(catalog, "FindEntry", "node_dup");
        Assert.IsNotNull(entry);
        Assert.AreSame(_spriteA, Field<Sprite>(entry, "Illustration"),
            "重复键必须确定性地使用第一条（不得静默换成第二条）");

        object r = Resolve("node_dup", JourneyEncounterKind.Battle, _battleFallback);
        Assert.AreSame(_spriteA, Field<Sprite>(r, "Illustration"), "解析必须始终使用生效行（行 0）的素材");

        Call(catalog, "FindEntry", "node_dup");
        LogAssert.NoUnexpectedReceived();
    }

    [Test]
    public void Resolver_UnconfiguredKeyWarning_IsDeduplicatedPerKey()
    {
        LogAssert.Expect(LogType.Warning, new Regex("PresentationKey=node_p 没有配置行"));
        LogAssert.Expect(LogType.Warning, new Regex("PresentationKey=node_q 没有配置行"));

        UseCatalog(NewEntry("node_a", _spriteA));

        Resolve("node_p", JourneyEncounterKind.Battle, _battleFallback);
        Resolve("node_q", JourneyEncounterKind.Battle, _battleFallback);
        Resolve("node_p", JourneyEncounterKind.Battle, _battleFallback);   // 不得重复告警
        Resolve("node_q", JourneyEncounterKind.Battle, _battleFallback);

        LogAssert.NoUnexpectedReceived();
    }

    [Test]
    public void Resolver_MissingCatalogAsset_FallsBackWithDiagnostic_WithoutBlocking()
    {
        LogAssert.Expect(LogType.Warning, new Regex("未找到配置资产"));
        LogAssert.Expect(LogType.Warning, new Regex("PresentationKey=node_x 没有配置行"));

        CallStatic(CatalogType, "SetInstanceForTests", new object[] { null });

        object r = Resolve("node_x", JourneyEncounterKind.Battle, _battleFallback);

        Assert.AreSame(_battleFallback, Field<Sprite>(r, "Illustration"),
            "配置资产缺失必须走类别 fallback（不得抛异常、不得阻断流程）");
        Assert.IsTrue(Field<bool>(r, "UsedCategoryFallback"));
    }

    // ==================== 4. 卡面装配（真实 View 路径） ====================

    private GameObject _cardHost;

    private object NewCardView(out Image imageHost, out TMP_Text titleHost, out TMP_Text bodyHost, out RectTransform optionRoot)
    {
        _cardHost = new GameObject("JourneyEventCardTestRoot", typeof(RectTransform));
        object card = _cardHost.AddComponent(CardViewType);

        var imageGo = new GameObject("ImageHost", typeof(RectTransform), typeof(Image));
        imageGo.transform.SetParent(_cardHost.transform, false);
        imageHost = imageGo.GetComponent<Image>();

        var titleGo = new GameObject("TitleHost", typeof(RectTransform), typeof(TextMeshProUGUI));
        titleGo.transform.SetParent(_cardHost.transform, false);
        titleHost = titleGo.GetComponent<TMP_Text>();

        var bodyGo = new GameObject("BodyHost", typeof(RectTransform), typeof(TextMeshProUGUI));
        bodyGo.transform.SetParent(_cardHost.transform, false);
        bodyHost = bodyGo.GetComponent<TMP_Text>();

        var optionGo = new GameObject("ChoiceRoot", typeof(RectTransform));
        optionGo.transform.SetParent(_cardHost.transform, false);
        optionRoot = optionGo.GetComponent<RectTransform>();

        SetField(card, "root", _cardHost);
        SetField(card, "illustrationHost", imageHost);
        SetField(card, "titleHost", titleHost);
        SetField(card, "bodyHost", bodyHost);
        SetField(card, "optionRoot", optionRoot);
        return card;
    }

    [Test]
    public void View_EncounterCard_DifferentKeys_YieldDifferentIllustrations_AndNothingElseChanges()
    {
        UseCatalog(
            NewEntry("node_battle_a", _spriteA),
            NewEntry("node_battle_b", _spriteB));
        object view = NewJourneyView();

        object cfgA = Call(view, "BuildEncounterCardConfig",
            new JourneyNodeRef("node_battle_a", string.Empty, JourneyEncounterKind.Battle));
        object cfgB = Call(view, "BuildEncounterCardConfig",
            new JourneyNodeRef("node_battle_b", string.Empty, JourneyEncounterKind.Battle));

        Assert.AreSame(_spriteA, Field<Sprite>(cfgA, "Illustration"), "Event A 卡面必须使用 A 的独立插图");
        Assert.AreSame(_spriteB, Field<Sprite>(cfgB, "Illustration"), "Event B 卡面必须使用 B 的独立插图");
        Assert.AreNotSame(Field<Sprite>(cfgA, "Illustration"), Field<Sprite>(cfgB, "Illustration"));

        // 除插图外的一切必须与配置前完全一致（卡面原行为不变）。
        Assert.AreEqual(JourneyEncounterKind.Battle, Field<JourneyEncounterKind>(cfgA, "Kind"));
        Assert.AreEqual(JourneyEncounterKind.Battle, Field<JourneyEncounterKind>(cfgB, "Kind"));
        Assert.AreEqual("遭遇战斗", Field<string>(cfgA, "Title"), "Battle 卡面标题原行为不变");
        Assert.AreEqual(Field<string>(cfgA, "Title"), Field<string>(cfgB, "Title"));
        Assert.AreEqual(Field<string>(cfgA, "Body"), Field<string>(cfgB, "Body"));

        IList optA = Options(cfgA);
        IList optB = Options(cfgB);
        Assert.AreEqual(1, optA.Count, "Battle 卡面仍然只有一个 Option");
        Assert.AreEqual(optA.Count, optB.Count);
        for (int i = 0; i < optA.Count; i++)
        {
            Assert.AreEqual(Field<string>(optA[i], "Label"), Field<string>(optB[i], "Label"));
            Assert.AreEqual(Field<string>(optA[i], "SubLabel"), Field<string>(optB[i], "SubLabel"));
            Assert.AreEqual(Field<object>(optA[i], "Role").ToString(), Field<object>(optB[i], "Role").ToString());
        }
        Assert.AreEqual("EnterBattle", Field<object>(optA[0], "Role").ToString(), "Battle 入口角色不变");
        Assert.AreEqual("node_battle_a", Field<string>(optA[0], "NodeId"),
            "Option 仍携带原 NodeId：Node legality / Battle 入口参数不受插图配置影响");
        Assert.AreEqual("node_battle_b", Field<string>(optB[0], "NodeId"));

        LogAssert.NoUnexpectedReceived();
    }

    [Test]
    public void View_EncounterCard_UnconfiguredKey_UsesCategoryFallback_ByKind()
    {
        LogAssert.Expect(LogType.Warning, new Regex("PresentationKey=node_battle_new 没有配置行"));
        LogAssert.Expect(LogType.Warning, new Regex("PresentationKey=node_content_new 没有配置行"));

        UseCatalog(NewEntry("node_other", _spriteA));
        object view = NewJourneyView();

        object battle = Call(view, "BuildEncounterCardConfig",
            new JourneyNodeRef("node_battle_new", string.Empty, JourneyEncounterKind.Battle));
        object content = Call(view, "BuildEncounterCardConfig",
            new JourneyNodeRef("node_content_new", string.Empty, JourneyEncounterKind.Content));

        Assert.AreSame(_battleFallback, Field<Sprite>(battle, "Illustration"),
            "Battle 未配置具体键 → battleEncounterIllustration");
        Assert.AreSame(_contentFallback, Field<Sprite>(content, "Illustration"),
            "Content 未配置具体键 → contentEncounterIllustration");
        Assert.AreNotSame(_branchFallback, Field<Sprite>(battle, "Illustration"),
            "类别 fallback 不得错用其它类别的图");
    }

    [Test]
    public void View_BranchCard_UsesCategoryFallback_WithoutFabricatingPresentationKey()
    {
        UseCatalog();   // 空 Catalog：Branch 必须仍然正常工作
        object view = NewJourneyView();

        var nodes = new List<JourneyNodeRef>
        {
            new JourneyNodeRef("node_road_1", "（占位路线 1）"),
            new JourneyNodeRef("node_road_2", "（占位路线 2）")
        };
        object cfg = Call(view, "BuildBranchCardConfig", nodes);

        Assert.AreSame(_branchFallback, Field<Sprite>(cfg, "Illustration"),
            "Branch 当前是路线选择表面：走 branchIllustration 类别图");
        Assert.AreEqual(JourneyEncounterKind.Branch, Field<JourneyEncounterKind>(cfg, "Kind"));

        IList options = Options(cfg);
        Assert.AreEqual(2, options.Count, "本行为不变：每条合法后继一个 Option");
        Assert.AreEqual("node_road_1", Field<string>(options[0], "NodeId"));
        Assert.AreEqual("node_road_2", Field<string>(options[1], "NodeId"));
        Assert.AreEqual("ChooseBranch", Field<object>(options[0], "Role").ToString());

        // Branch 无稳定唯一表现键：不得伪造键，也不得输出「空键」诊断（本测试若多出 warning 即失败）。
        LogAssert.NoUnexpectedReceived();
    }

    [Test]
    public void Card_IllustrationNull_HidesIllustrationHost_ButCardContentStillWorks()
    {
        LogAssert.Expect(LogType.Warning, new Regex("PresentationKey=node_no_art 没有配置行"));
        LogAssert.Expect(LogType.Warning, new Regex("类别 Battle 既没有具体"));

        UseCatalog();
        object view = NewJourneyView();
        SetField(view, "battleEncounterIllustration", null);   // 类别图也为空

        object cfg = Call(view, "BuildEncounterCardConfig",
            new JourneyNodeRef("node_no_art", string.Empty, JourneyEncounterKind.Battle));
        Assert.IsNull(Field<Sprite>(cfg, "Illustration"), "既无具体键图、类别图也为空 → 插图为空（不抛异常）");

        object card = NewCardView(out Image imageHost, out TMP_Text titleHost, out TMP_Text bodyHost, out RectTransform optionRoot);
        Call(card, "Render", cfg, null);

        Assert.IsFalse(imageHost.enabled, "插图为空时只有 Illustration Host 隐藏");
        Assert.IsNull(imageHost.sprite);
        Assert.IsTrue((bool)CardViewType.GetProperty("IsVisible", F).GetValue(card, null), "卡面必须仍然显示");
        Assert.AreEqual("遭遇战斗", (string)CardViewType.GetProperty("RenderedTitle", F).GetValue(card, null),
            "标题继续正常工作");
        Assert.IsNotEmpty((string)CardViewType.GetProperty("RenderedBody", F).GetValue(card, null),
            "正文继续正常工作");
        Assert.AreEqual(1, (int)CardViewType.GetProperty("OptionCount", F).GetValue(card, null),
            "Option 继续正常生成（不因插图缺失而阻断战斗入口）");
        Assert.AreEqual(1, optionRoot.childCount, "唯一 Option 按钮已生成");
    }

    [Test]
    public void EventCard_And_IllustrationCatalog_AreSingleImplementations()
    {
        var asm = EntryType.Assembly;   // Assembly-CSharp

        var cardViews = asm.GetTypes()
            .Where(t => typeof(MonoBehaviour).IsAssignableFrom(t) && t.Name.EndsWith("EventCardView")).ToArray();
        Assert.AreEqual(1, cardViews.Length, "不得产生第二套 Event Card View");
        Assert.AreEqual("JourneyEventCardView", cardViews[0].Name);

        var cardConfigs = asm.GetTypes().Where(t => t.Name.EndsWith("EventCardConfig")).ToArray();
        Assert.AreEqual(1, cardConfigs.Length, "不得产生第二套 Event Card 数据模型");
        Assert.AreEqual("JourneyEventCardConfig", cardConfigs[0].Name);

        var catalogs = asm.GetTypes()
            .Where(t => typeof(ScriptableObject).IsAssignableFrom(t) && t.Name.EndsWith("EventPresentationCatalog")).ToArray();
        Assert.AreEqual(1, catalogs.Length, "事件卡插图配置面必须唯一（不得出现第二份插图表）");
        Assert.AreEqual("JourneyEventPresentationCatalog", catalogs[0].Name);
    }

    [Test]
    public void Catalog_IsNotEventContentAuthority_EntryHasOnlyPresentationKeyAndIllustration()
    {
        var fieldNames = EntryType.GetFields(F).Select(f => f.Name).OrderBy(n => n).ToArray();
        CollectionAssert.AreEqual(new[] { "Illustration", "PresentationKey" }, fieldNames,
            "Catalog 条目只允许 PresentationKey + Illustration：Title / Body / Options / 奖励 / 条件 / 概率不得进入本 Catalog");

        string[] forbidden = { "title", "body", "option", "reward", "condition", "weight", "probability" };
        foreach (var type in new[] { EntryType, CatalogType })
        {
            foreach (var member in type.GetMembers(F))
            {
                string lower = member.Name.ToLowerInvariant();
                foreach (var bad in forbidden)
                {
                    Assert.IsFalse(lower.Contains(bad),
                        type.Name + "." + member.Name + " 疑似把 Event Content Authority 建进插图配置面");
                }
            }
        }
    }

    [Test]
    public void IllustrationBinding_DeclaresNoRuleAuthorityDependency()
    {
        string[] forbidden =
        {
            "AdventureManager", "ExpeditionProgressionResolver", "ExpeditionUI",
            "BattleUI", "PlayerCollection", "SaveData", "BattleSystem"
        };

        foreach (var type in new[] { ResolverType, CatalogType, EntryType })
        {
            var signatureTypes = new List<string>();
            foreach (var m in type.GetMethods(F))
            {
                signatureTypes.Add(m.ReturnType.FullName ?? string.Empty);
                foreach (var p in m.GetParameters()) signatureTypes.Add(p.ParameterType.FullName ?? string.Empty);
            }
            foreach (var f in type.GetFields(F)) signatureTypes.Add(f.FieldType.FullName ?? string.Empty);

            foreach (var sig in signatureTypes)
            {
                foreach (var bad in forbidden)
                {
                    Assert.IsFalse(sig.Contains(bad),
                        type.Name + " 的插图解析链不得接触规则 Authority 类型：" + sig);
                }
            }
        }
    }
}
