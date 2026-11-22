using System;
using System.Collections;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

/// <summary>
/// Person Visual Binding V0.1｜长期契约测试（EditMode）。
///
/// 冻结的是**解析契约与配置面边界**（不是数据形态）：
///   1. PersonId → exact Person → AppearanceKey → Visual Binding → placeholder；
///   2. 同一 Definition 的不同 Person 不合并：仍是两个独立人物，但可以显示同一素材；
///   3. 不同 AppearanceKey 在 Journey / Battle / Prep / HeroSelect 四个 surface 上各自显示不同视觉；
///   4. surface override 优先 → DefaultBodySprite → 消费者 placeholder（全缺失也绝不隐藏身体 / 阻断流程）；
///   5. Hero 在 Journey 与 Hero Selection 使用同一份 Hero visual（HeroId 作外观键）；
///   6. Journey 迁移：`ExpeditionJourneyView` 不再序列化人物质表，Scene 内不得再出现遗留言物表键；
///   7. Prep / HeroSelect 卡片 Prefab 具备稳定、非交互、可调的表现宿主，且 Root Button 未被破坏。
///
/// 说明：asmdef 测试程序集不能引用预定义程序集 Assembly-CSharp，
/// 运行时类型（PersonVisualResolver / PersonVisualBindingCatalog / PlayerCollection / ExpeditionJourneyView …）
/// 全部通过反射访问（与既有测试一致）。
/// </summary>
public class PersonVisualBindingTests
{
    private const BindingFlags F =
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

    private const int Journey = 0, Battle = 1, Prep = 2, HeroSelect = 3;

    private const string CatalogAssetPath =
        "Assets/Resources/ScriptableObjects/Presentation/PersonVisualBindingCatalog.asset";
    private const string CharCardPrefabPath = "Assets/Prefabs/UI/CharCard.prefab";
    private const string HeroSelectCardPrefabPath = "Assets/Prefabs/UI/HeroSelectCard.prefab";
    private const string ScenePath = "Assets/Scenes/SampleScene.scene";

    private Sprite _default;
    private Sprite _spriteA;
    private Sprite _spriteB;
    private Sprite _placeholder;

    private static Type Asm(string name)
    {
        var t = Type.GetType(name + ", Assembly-CSharp");
        Assert.IsNotNull(t, "未找到 Assembly-CSharp 类型：" + name);
        return t;
    }

    private static Type EntryType => Asm("PersonVisualBindingEntry");
    private static Type CatalogType => Asm("PersonVisualBindingCatalog");
    private static Type ResolverType => Asm("PersonVisualResolver");
    private static Type SurfaceType => Asm("PersonVisualSurface");

    private static object Surface(int value) => Enum.ToObject(SurfaceType, value);

    [SetUp]
    public void SetUp()
    {
        ResolverType.GetMethod("ClearWarningsForTests", BindingFlags.Public | BindingFlags.Static)
            .Invoke(null, null);

        _spriteA = NewSprite();
        _spriteB = NewSprite();
        _default = NewSprite();
        _placeholder = NewSprite();
    }

    [TearDown]
    public void TearDown()
    {
        CatalogType.GetMethod("ResetForTests", BindingFlags.Public | BindingFlags.Static).Invoke(null, null);
        ResolverType.GetMethod("ClearWarningsForTests", BindingFlags.Public | BindingFlags.Static)
            .Invoke(null, null);
    }

    private static Sprite NewSprite()
    {
        return Sprite.Create(new Texture2D(4, 4), new Rect(0f, 0f, 4f, 4f), new Vector2(0.5f, 0.5f));
    }

    // ==================== 构造辅助（全部反射） ====================

    private static object NewPlayerCollection()
    {
        return Activator.CreateInstance(Asm("PlayerCollection"));
    }

    [Test]
    public void CardPortraitUsesExactPersonThenOptionalSurfaceOverride()
    {
        var pc = NewPlayerCollection();
        AddCharacter(pc, "same_character", "甲", "person_exact_a", "appearance_a");
        AddCharacter(pc, "same_character", "乙", "person_exact_b", "appearance_b");
        var a = NewEntry("appearance_a", _spriteA); var b = NewEntry("appearance_b", _spriteB);
        SetEntryField(a, "CardPortraitSpriteOverride", _default);
        var surface = Enum.ToObject(Asm("PersonVisualSurface"), 4);
        var method = Asm("PersonVisualResolver").GetMethod("ResolveForPerson", F);
        var resultA = method.Invoke(null, new object[] { pc, "person_exact_a", surface, _placeholder, NewEntries(a, b) });
        var resultB = method.Invoke(null, new object[] { pc, "person_exact_b", surface, _placeholder, NewEntries(a, b) });
        Assert.AreEqual("person_exact_a", resultA.GetType().GetField("PersonId").GetValue(resultA));
        Assert.AreEqual("person_exact_b", resultB.GetType().GetField("PersonId").GetValue(resultB));
        Assert.AreSame(_default, resultA.GetType().GetField("Sprite").GetValue(resultA));
        Assert.AreSame(_spriteB, resultB.GetType().GetField("Sprite").GetValue(resultB));
    }

    private static void AddCharacter(object pc, string characterId, string displayName, string personId,
        string personDefinitionId)
    {
        var ciType = Asm("CharacterInstance");
        object inst = Activator.CreateInstance(ciType, new object[] { characterId, displayName, 30, 5 });
        ciType.GetField("PersonId", F).SetValue(inst, personId);
        ciType.GetField("PersonDefinitionId", F).SetValue(inst, personDefinitionId);
        ((IList)pc.GetType().GetField("OwnedCharacters", F).GetValue(pc)).Add(inst);
    }

    private static void AddHero(object pc, string heroId, string displayName, string personId)
    {
        var hiType = Asm("HeroInstance");
        object inst = Activator.CreateInstance(hiType, new object[] { heroId, displayName, 40 });
        hiType.GetField("PersonId", F).SetValue(inst, personId);
        ((IList)pc.GetType().GetField("OwnedHeroes", F).GetValue(pc)).Add(inst);
    }

    private static object NewEntry(string appearanceKey, Sprite defaultBody)
    {
        object entry = Activator.CreateInstance(EntryType);
        EntryType.GetField("AppearanceKey", F).SetValue(entry, appearanceKey);
        EntryType.GetField("DefaultBodySprite", F).SetValue(entry, defaultBody);
        return entry;
    }

    private static void SetEntryField(object entry, string field, object value)
    {
        var f = EntryType.GetField(field, F);
        Assert.IsNotNull(f, "PersonVisualBindingEntry 缺少字段：" + field);
        f.SetValue(entry, value);
    }

    private static Array NewEntries(params object[] entries)
    {
        var array = Array.CreateInstance(EntryType, entries.Length);
        for (int i = 0; i < entries.Length; i++) array.SetValue(entries[i], i);
        return array;
    }

    /// <summary>构造内存配置面并注入为当前生效配置（不写盘）；返回该内存配置面供配置安全诊断断言使用。</summary>
    private static object UseCatalog(params object[] entries)
    {
        var create = typeof(ScriptableObject).GetMethod("CreateInstance", new[] { typeof(Type) });
        object catalog = create.Invoke(null, new object[] { CatalogType });
        CatalogType.GetMethod("SetEntriesForTests", F).Invoke(catalog, new object[] { NewEntries(entries) });
        CatalogType.GetMethod("SetInstanceForTests", BindingFlags.Public | BindingFlags.Static)
            .Invoke(null, new object[] { catalog });
        return catalog;
    }

    private static object ResolveForPerson(object pc, string personId, int surface, Sprite placeholder,
        Array entriesOverride = null)
    {
        var m = ResolverType.GetMethod("ResolveForPerson", F);
        Assert.IsNotNull(m, "缺少统一解析入口 PersonVisualResolver.ResolveForPerson");
        return m.Invoke(null, new object[] { pc, personId, Surface(surface), placeholder, entriesOverride });
    }

    private static object ResolveForKey(string appearanceKey, int surface, Sprite placeholder,
        Array entriesOverride = null)
    {
        var m = ResolverType.GetMethod("ResolveForAppearanceKey", F);
        Assert.IsNotNull(m, "缺少外观键解析入口 PersonVisualResolver.ResolveForAppearanceKey");
        return m.Invoke(null, new object[] { appearanceKey, Surface(surface), placeholder, entriesOverride });
    }

    private static T Field<T>(object o, string name)
    {
        var f = o.GetType().GetField(name, F);
        Assert.IsNotNull(f, "字段不存在：" + name + " @ " + o.GetType().Name);
        return (T)f.GetValue(o);
    }

    /// <summary>按名反射调用实例方法（与既有测试同一风格）。</summary>
    private static object Call(object o, string method, params object[] args)
    {
        var m = o.GetType().GetMethod(method, F);
        Assert.IsNotNull(m, "方法不存在：" + method + " @ " + o.GetType().FullName);
        return m.Invoke(o, args);
    }

    // ==================== 1. 身份不合并 / 素材可共享 ====================

    [Test]
    public void TwoPersonIds_SharingOneAppearanceKey_RemainTwoIdentities_AndShareTheSameMaterial()
    {
        object pc = NewPlayerCollection();
        AddCharacter(pc, "char_shared", "甲", "pid_x", "def_shared");
        AddCharacter(pc, "char_shared", "甲", "pid_y", "def_shared");

        UseCatalog(NewEntry("def_shared", _spriteA));

        object rx = ResolveForPerson(pc, "pid_x", Journey, _placeholder);
        object ry = ResolveForPerson(pc, "pid_y", Journey, _placeholder);

        Assert.AreNotEqual(Field<string>(rx, "PersonId"), Field<string>(ry, "PersonId"),
            "同模板两名 Person 必须仍是两个独立人物（PersonId 不得塌缩）");
        Assert.AreEqual("def_shared", Field<string>(rx, "AppearanceKey"));
        Assert.AreEqual("def_shared", Field<string>(ry, "AppearanceKey"),
            "同一 Definition 的不同 Person 可以共享同一外观键与素材");
        Assert.AreSame(_spriteA, Field<Sprite>(rx, "Sprite"), "共享外观键必须解析到同一素材");
        Assert.AreSame(_spriteA, Field<Sprite>(ry, "Sprite"));
        Assert.IsFalse(Field<bool>(rx, "UsedFallback"), "命中配置不得标记 fallback");
    }

    // ==================== 2. 不同 AppearanceKey → 四个表面视觉不同 ====================

    [TestCase(Journey)]
    [TestCase(Battle)]
    [TestCase(Prep)]
    [TestCase(HeroSelect)]
    public void DifferentAppearanceKeys_ProduceDifferentVisuals_OnEverySurface(int surface)
    {
        object pc = NewPlayerCollection();
        AddCharacter(pc, "char_a", "甲", "pid_a", "def_a");
        AddCharacter(pc, "char_b", "乙", "pid_b", "def_b");

        UseCatalog(NewEntry("def_a", _spriteA), NewEntry("def_b", _spriteB));

        object ra = ResolveForPerson(pc, "pid_a", surface, _placeholder);
        object rb = ResolveForPerson(pc, "pid_b", surface, _placeholder);

        Assert.AreSame(_spriteA, Field<Sprite>(ra, "Sprite"), "def_a 必须解析到 A 素材（surface " + surface + "）");
        Assert.AreSame(_spriteB, Field<Sprite>(rb, "Sprite"), "def_b 必须解析到 B 素材（surface " + surface + "）");
        Assert.AreNotSame(Field<Sprite>(ra, "Sprite"), Field<Sprite>(rb, "Sprite"),
            "不同 AppearanceKey 在同一表面必须显示不同视觉（surface " + surface + "）");
    }

    // ==================== 3. Surface Override 与 fallback 链 ====================

    [Test]
    public void SurfaceOverride_WinsOverDefaultBody()
    {
        object pc = NewPlayerCollection();
        AddCharacter(pc, "char_a", "甲", "pid_a", "def_a");

        object entry = NewEntry("def_a", _default);
        SetEntryField(entry, "JourneySpriteOverride", _spriteA);
        SetEntryField(entry, "BattleSpriteOverride", _spriteB);
        UseCatalog(entry);

        Assert.AreSame(_spriteA, Field<Sprite>(ResolveForPerson(pc, "pid_a", Journey, _placeholder), "Sprite"),
            "Journey 有 override 时必须使用 override");
        Assert.AreSame(_spriteB, Field<Sprite>(ResolveForPerson(pc, "pid_a", Battle, _placeholder), "Sprite"),
            "Battle 有 override 时必须使用 override");
        Assert.AreSame(_default, Field<Sprite>(ResolveForPerson(pc, "pid_a", Prep, _placeholder), "Sprite"),
            "Prep 无 override 时必须回退 DefaultBodySprite");
        Assert.AreSame(_default, Field<Sprite>(ResolveForPerson(pc, "pid_a", HeroSelect, _placeholder), "Sprite"),
            "HeroSelect 无 override 时必须回退 DefaultBodySprite");
    }

    [Test]
    public void MissingBinding_FallsBackToPlaceholder_AndNeverHidesBody()
    {
        object pc = NewPlayerCollection();
        AddCharacter(pc, "char_a", "甲", "pid_a", "def_missing");

        UseCatalog(NewEntry("def_other", _spriteA));

        object r = ResolveForPerson(pc, "pid_a", Battle, _placeholder);

        Assert.IsTrue(Field<bool>(r, "UsedFallback"), "未配置外观必须走 placeholder");
        Assert.AreSame(_placeholder, Field<Sprite>(r, "Sprite"), "未配置外观必须回退消费者 placeholder");
        Assert.AreEqual("def_missing", Field<string>(r, "AppearanceKey"),
            "即使视觉缺失，外观键仍来自 exact Person（不得因缺失改变身份 / 外观键来源）");
    }

    [Test]
    public void MissingEverything_KeepsAppearanceKey_AndReturnsNullSprite_InsteadOfThrowing()
    {
        object pc = NewPlayerCollection();
        AddCharacter(pc, "char_a", "甲", "pid_a", "def_missing");

        UseCatalog(NewEntry("def_other", _spriteA));

        object r = ResolveForPerson(pc, "pid_a", Prep, null);

        Assert.IsTrue(Field<bool>(r, "UsedFallback"), "全缺失时必须标记 fallback（调用方保留 prefab 自带占位图）");
        Assert.IsNull(Field<Sprite>(r, "Sprite"), "无 placeholder 时返回 null Sprite，不得隐藏身体或抛异常");
        Assert.AreEqual("def_missing", Field<string>(r, "AppearanceKey"));
    }

    [Test]
    public void EntryWithoutDefaultBodySprite_FallsBackToPlaceholder_OnOtherSurfaces()
    {
        object pc = NewPlayerCollection();
        AddCharacter(pc, "char_a", "甲", "pid_a", "def_a");

        object entry = NewEntry("def_a", null); // 只填 Journey override，其它表面无图
        SetEntryField(entry, "JourneySpriteOverride", _spriteA);
        UseCatalog(entry);

        Assert.AreSame(_spriteA, Field<Sprite>(ResolveForPerson(pc, "pid_a", Journey, _placeholder), "Sprite"));

        object battle = ResolveForPerson(pc, "pid_a", Battle, _placeholder);
        Assert.IsTrue(Field<bool>(battle, "UsedFallback"));
        Assert.AreSame(_placeholder, Field<Sprite>(battle, "Sprite"),
            "DefaultBodySprite 为空的表面必须回退 placeholder");
    }

    [Test]
    public void SurfaceScaleAndOffset_AreAdopted_PerSurface()
    {
        object pc = NewPlayerCollection();
        AddCharacter(pc, "char_a", "甲", "pid_a", "def_a");

        object entry = NewEntry("def_a", _default);
        SetEntryField(entry, "JourneyScale", 1.5f);
        SetEntryField(entry, "JourneyOffset", new Vector2(12f, -8f));
        SetEntryField(entry, "BattleScale", 0f); // ≤ 0 视为 1，避免把 0 缩放写进视觉宿主
        UseCatalog(entry);

        object journey = ResolveForPerson(pc, "pid_a", Journey, _placeholder);
        Assert.AreEqual(1.5f, Field<float>(journey, "Scale"), 0.0001f, "Journey 既有 scale 参数必须保留");
        Assert.AreEqual(new Vector2(12f, -8f), Field<Vector2>(journey, "Offset"), "Journey 既有 offset 参数必须保留");

        object battle = ResolveForPerson(pc, "pid_a", Battle, _placeholder);
        Assert.AreEqual(1f, Field<float>(battle, "Scale"), 0.0001f, "非法 scale（≤0）必须收敛为 1");
    }

    // ==================== 4. Hero：Journey 与 Hero Selection 同一份 visual ====================

    [Test]
    public void Hero_UsesSameBindingOnJourneyAndHeroSelection()
    {
        object pc = NewPlayerCollection();
        AddHero(pc, "hero_001", "青鹿", "pid_hero");

        UseCatalog(NewEntry("hero_001", _spriteA));

        object journey = ResolveForPerson(pc, "pid_hero", Journey, _placeholder);
        Assert.AreSame(_spriteA, Field<Sprite>(journey, "Sprite"), "Journey 必须使用 Hero 绑定的 visual");
        Assert.IsTrue(Field<bool>(journey, "IsHero"), "Hero 解析必须标记 IsHero");

        // Hero Selection 尚无 Person 实例：按外观键（HeroId）取同一份配置
        object select = ResolveForKey("hero_001", HeroSelect, _placeholder);
        Assert.AreSame(_spriteA, Field<Sprite>(select, "Sprite"),
            "Hero Selection 必须与 Journey 读取同一份 Hero visual");
        Assert.IsNull(Field<string>(select, "PersonId"), "按外观键解析不得制造临时 Person 身份");
        Assert.AreEqual("hero_001", Field<string>(select, "AppearanceKey"));
    }

    // ==================== 5. Journey 迁移：单一配置面 + 无遗留人物质表 ====================

    [Test]
    public void JourneyView_NoLongerOwnsAPersonVisualTable()
    {
        var viewType = Asm("ExpeditionJourneyView");

        bool hasLegacyTable = false;
        foreach (var f in viewType.GetFields(BindingFlags.Public | BindingFlags.Instance))
        {
            if (f.Name == "personVisuals") hasLegacyTable = true;
        }

        Assert.IsFalse(hasLegacyTable,
            "Journey 不得再保留独立人物质表字段（正式配置只允许存在于 PersonVisualBindingCatalog）");
        Assert.IsNotNull(viewType.GetField("defaultPersonBody", BindingFlags.Public | BindingFlags.Instance),
            "Journey 必须保留 default placeholder 字段（缺素材时不显示身体但不影响身份 / 布局）");
    }

    [Test]
    public void SceneFile_ContainsNoLegacyJourneyPersonVisualTable()
    {
        var text = System.IO.File.ReadAllText(ScenePath);
        Assert.IsFalse(text.Contains("personVisuals:"),
            "SampleScene 不得再序列化 Journey 人物质表（迁移后唯一配置面是 Catalog 资产）");
        Assert.IsTrue(text.Contains("defaultPersonBody:"),
            "SampleScene 必须保留 Journey default placeholder 绑定");
    }

    [Test]
    public void JourneyResolver_ReadsTheUnifiedConfigurationSurface()
    {
        object pc = NewPlayerCollection();
        AddCharacter(pc, "char_a", "甲", "pid_a", "def_a");
        UseCatalog(NewEntry("def_a", _spriteA));

        var resolverType = Asm("ExpeditionJourneyPersonVisualResolver");
        var m = resolverType.GetMethod("Resolve", F);
        Assert.IsNotNull(m, "缺少 Journey 兼容解析入口");

        // entries = null → 正式路径：必须读统一配置面
        object r = m.Invoke(null, new object[] { pc, "pid_a", null, _placeholder });

        Assert.AreSame(_spriteA, Field<Sprite>(r, "Body"),
            "Journey 正式路径必须从 PersonVisualBindingCatalog 取视觉");
        Assert.AreEqual("def_a", Field<string>(r, "AppearanceKey"));
        Assert.IsFalse(Field<bool>(r, "UsedFallback"));
    }

    [Test]
    public void CatalogAsset_ExistsAsTheOnlyConfigurationSurface()
    {
        var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(CatalogAssetPath);
        Assert.IsNotNull(asset, "统一配置资产缺失：" + CatalogAssetPath);
        Assert.AreEqual(CatalogType, asset.GetType(),
            "配置资产必须是轻量 PersonVisualBindingCatalog（可整体替换，不是长期美术 Authority）");

        int count = (int)CatalogType.GetProperty("EntryCount", F).GetValue(asset, null);
        Assert.GreaterOrEqual(count, 3,
            "迁移后至少应包含 scene 原有 3 条人物外观绑定（person_lin_en / person_han_zhao / hero_001）");

        var keys = new System.Collections.Generic.List<string>();
        var entries = (IEnumerable)CatalogType.GetProperty("Entries", F).GetValue(asset, null);
        foreach (var row in entries)
        {
            Assert.IsNotNull(row, "配置行不得为空");
            string key = Field<string>(row, "AppearanceKey");
            Assert.IsFalse(string.IsNullOrEmpty(key), "配置行必须有 AppearanceKey");
            Assert.IsNotNull(Field<Sprite>(row, "DefaultBodySprite"),
                "配置行必须有 DefaultBodySprite（否则四个表面都会退化为 placeholder）：" + key);
            keys.Add(key);
        }

        CollectionAssert.Contains(keys, "person_lin_en");
        CollectionAssert.Contains(keys, "person_han_zhao");
        CollectionAssert.Contains(keys, "hero_001");
    }

    // ==================== 6. 卡片视觉宿主（Prep / Hero Selection） ====================

    private static Image BodyImageOf(GameObject prefab)
    {
        var t = prefab.transform.Find("Anchor_Visual/Body_Visual");
        return t != null ? t.GetComponent<Image>() : null;
    }

    private static void AssertCardVisualHost(string prefabPath, string label)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        Assert.IsNotNull(prefab, label + " 未找到：" + prefabPath);

        var body = BodyImageOf(prefab);
        Assert.IsNotNull(body, label + " 缺少纯表现视觉宿主 Anchor_Visual/Body_Visual");
        Assert.AreEqual(Image.Type.Simple, body.type, label + " Body_Visual 必须使用 Image Simple");
        Assert.IsTrue(body.preserveAspect, label + " Body_Visual 必须保持 PreserveAspect");
        Assert.IsFalse(body.raycastTarget, label + " 视觉节点不得拦截 Raycast（raycastTarget=false）");
        Assert.IsNotNull(body.sprite, label + " Body_Visual 必须绑定 placeholder Sprite");
        Assert.IsNull(body.GetComponent<Button>(), label + " 视觉节点不得带 Button（交互仍由 Root Button 承担）");

        Assert.IsNotNull(prefab.GetComponent<Button>(), label + " Root Button 不得被破坏");
        Assert.IsNotNull(prefab.transform.Find("Anchor_Visual"), label + " 缺少 Anchor_Visual 锚点（可调 Offset / Scale）");
    }

    [Test]
    public void CharCardPrefab_HasNonInteractiveBodyVisualHost()
    {
        AssertCardVisualHost(CharCardPrefabPath, "CharCard.prefab（远征准备 / 战前准备共用）");
    }

    [Test]
    public void PrepBinder_MissingBindingUsesNeutralSprite_AndExactBindingWins()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CharCardPrefabPath);
        Assert.IsNotNull(prefab);
        var instance = UnityEngine.Object.Instantiate(prefab);
        try
        {
            var image = BodyImageOf(instance);
            Assert.IsNotNull(image);
            var historicalSprite = image.sprite;
            var beforePosition = image.rectTransform.anchoredPosition;
            var beforeScale = image.rectTransform.localScale;
            UseCatalog(NewEntry("person_x", _spriteA));
            var method = Asm("PersonVisualHostBinder").GetMethod("ApplyForPrepPerson", F);
            Assert.IsNotNull(method);

            bool missing = (bool)method.Invoke(null, new object[] { instance, null, null, "missing", null });
            Assert.IsFalse(missing);
            Assert.IsNotNull(image.sprite, "Prep 缺视觉绑定必须显示中性占位");
            Assert.AreNotSame(historicalSprite, image.sprite, "历史 Prefab 人物图不能泄漏到缺绑定卡");
            Assert.AreEqual("NeutralPrepSilhouette", image.sprite.name);
            Assert.AreEqual(beforePosition, image.rectTransform.anchoredPosition);
            Assert.AreEqual(beforeScale, image.rectTransform.localScale);

            bool exact = (bool)method.Invoke(null, new object[] { instance, null, null, "person_x", null });
            Assert.IsTrue(exact);
            Assert.AreSame(_spriteA, image.sprite, "正式视觉绑定必须覆盖中性占位");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(instance);
        }
    }

    [Test]
    public void HeroSelectCardPrefab_HasNonInteractiveBodyVisualHost()
    {
        AssertCardVisualHost(HeroSelectCardPrefabPath, "HeroSelectCard.prefab");
    }

    [Test]
    public void HostBinder_WritesSpriteAndLayout_WithoutEnablingRaycast()
    {
        var binder = Asm("PersonVisualHostBinder");
        var root = new GameObject("binder_test", typeof(RectTransform));
        try
        {
            var anchor = new GameObject("Anchor_Visual", typeof(RectTransform));
            anchor.transform.SetParent(root.transform, false);
            var body = new GameObject("Body_Visual", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            body.transform.SetParent(anchor.transform, false);
            body.GetComponent<Image>().raycastTarget = true;

            var m = binder.GetMethod("ApplyBodyVisual", F);
            Assert.IsNotNull(m, "缺少 PersonVisualHostBinder.ApplyBodyVisual");

            bool applied = (bool)m.Invoke(null, new object[] { root, _spriteA, new Vector2(3f, 4f), 1.25f });
            Assert.IsTrue(applied);

            var image = body.GetComponent<Image>();
            Assert.AreSame(_spriteA, image.sprite);
            Assert.AreEqual(Image.Type.Simple, image.type, "视觉宿主必须为 Simple（可替换素材不被九宫格拉伸）");
            Assert.IsTrue(image.preserveAspect, "PreserveAspect 必须保持");
            Assert.IsFalse(image.raycastTarget, "绑定后视觉节点不得拦截 Raycast");
            Assert.AreEqual(1.25f, body.GetComponent<RectTransform>().localScale.x, 0.0001f, "Scale 必须可调");
            Assert.AreEqual(new Vector2(3f, 4f), body.GetComponent<RectTransform>().anchoredPosition, "Offset 必须可调");
            Assert.IsNull(root.GetComponent<Button>(), "宿主工具不得创建 / 修改 Button");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    // ==================== 7. Catalog 配置安全（错误 Inspector 配置不得静默歧义） ====================

    /// <summary>
    /// 要求 1：AppearanceKey 为空的行不得被解析使用；必须给出明确诊断；
    /// 且无效行不得影响同一资产中的有效行。
    /// </summary>
    [Test]
    public void Catalog_EmptyKeyRow_IsIgnoredWithDiagnostic_AndDoesNotBreakOtherRows()
    {
        LogAssert.Expect(LogType.Warning, new Regex("AppearanceKey 为空（行 1）"));

        object valid = NewEntry("def_a", _spriteA);
        object broken = NewEntry(string.Empty, _spriteB); // 制作人最容易误配：只填了图、没填键
        object catalog = UseCatalog(valid, broken);

        Assert.IsNull(Call(catalog, "FindEntry", string.Empty),
            "空键行不得被解析使用（空键查询必须返回 null）");
        Assert.IsNotNull(Call(catalog, "FindEntry", "def_a"),
            "同一资产中的有效行不得因存在无效行而失效");

        // 去重：同一索引构建期内重复查询不得重复告警
        Call(catalog, "FindEntry", string.Empty);
        Call(catalog, "FindEntry", "def_a");
        LogAssert.NoUnexpectedReceived();
    }

    /// <summary>
    /// 要求 2：重复 AppearanceKey 不得静默歧义 —— 必须指出「生效行 / 被忽略行」，
    /// 并且生效行确定（第一条），不自动删除 / 修改制作人配置。
    /// </summary>
    [Test]
    public void Catalog_DuplicateKey_KeepsFirstRowWithDiagnostic_AndLookupIsDeterministic()
    {
        LogAssert.Expect(LogType.Warning,
            new Regex("重复 AppearanceKey：def_dup（生效：行 0；已忽略：行 1）"));

        object first = NewEntry("def_dup", _spriteA);
        object second = NewEntry("def_dup", _spriteB);
        object catalog = UseCatalog(first, second);

        object entry = Call(catalog, "FindEntry", "def_dup");
        Assert.IsNotNull(entry);
        Assert.AreSame(_spriteA, Field<Sprite>(entry, "DefaultBodySprite"),
            "重复键必须确定性地使用第一条（不得静默换成第二条）");

        // 解析层同样确定：同一 Person 得到第一条的视觉
        object pc = NewPlayerCollection();
        AddCharacter(pc, "char_a", "甲", "pid_a", "def_dup");
        Assert.AreSame(_spriteA, Field<Sprite>(ResolveForPerson(pc, "pid_a", Prep, _placeholder), "Sprite"),
            "解析必须始终使用生效行（行 0）的素材");

        // 去重：再次查询不得重复告警
        Call(catalog, "FindEntry", "def_dup");
        LogAssert.NoUnexpectedReceived();
    }

    /// <summary>
    /// 要求 2 延伸：生效行没有 Sprite 时，**不得**静默改用被忽略行的图 ——
    /// 必须按契约回退到 consumer placeholder。
    /// </summary>
    [Test]
    public void Catalog_DuplicateKey_DoesNotFallThroughToIgnoredRow_WhenWinningRowHasNoSprite()
    {
        LogAssert.Expect(LogType.Warning, new Regex("重复 AppearanceKey：def_dup（生效：行 0；已忽略：行 1）"));
        LogAssert.Expect(LogType.Warning, new Regex("没有可用的视觉绑定"));

        object winningRowWithoutSprite = NewEntry("def_dup", null);
        object ignoredRowWithSprite = NewEntry("def_dup", _spriteB);
        UseCatalog(winningRowWithoutSprite, ignoredRowWithSprite);

        object pc = NewPlayerCollection();
        AddCharacter(pc, "char_a", "甲", "pid_a", "def_dup");

        object r = ResolveForPerson(pc, "pid_a", Prep, _placeholder);
        Assert.IsTrue(Field<bool>(r, "UsedFallback"),
            "生效行无 Sprite 时必须回退 placeholder，不得静默改用被忽略行的图");
        Assert.AreSame(_placeholder, Field<Sprite>(r, "Sprite"));
    }

    /// <summary>
    /// 要求 1（解析侧）：空外观键输入不得命中任何已配置行，必须走既有 placeholder + 既有 warning，
    /// 且不得因此改变 fallback / 去重语义。
    /// </summary>
    [Test]
    public void EmptyAppearanceKeyInput_NeverResolvesToAConfiguredRow_AndWarns()
    {
        LogAssert.Expect(LogType.Warning, new Regex("外观键为空"));

        UseCatalog(NewEntry("def_a", _spriteA));

        object r = ResolveForKey(string.Empty, Prep, _placeholder);
        Assert.IsTrue(Field<bool>(r, "UsedFallback"), "空外观键必须走 placeholder");
        Assert.AreSame(_placeholder, Field<Sprite>(r, "Sprite"), "空键不得命中任何已配置行");
        Assert.IsTrue(string.IsNullOrEmpty(Field<string>(r, "AppearanceKey")),
            "空键解析结果不得携带任何外观键");
    }
}
