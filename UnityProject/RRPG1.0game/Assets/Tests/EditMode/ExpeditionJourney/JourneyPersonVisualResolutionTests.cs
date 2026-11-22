using System;
using System.Collections;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// WP-E｜T1 Minimal Person Visual Resolution 长期契约测试（EditMode）。
///
/// 冻结的是**解析契约**：PersonId → exact Person → Presentation Visual（或 default placeholder）。
/// 覆盖：
///   1. Character 以 PersonDefinitionId 优先、缺失时回退 CharacterId 作为外观候选键；
///   2. Hero 以 HeroId 作为外观候选键；
///   3. 模板 / 外观键不承担人物身份；未知 Person 不得被猜成模板键；
///   4. 同模板两名 Person 不合并（各自保留自己的 PersonId / DisplayName）；
///   5. 未知 Person / 缺少视觉绑定 → default placeholder + 去重 warning；
///   6. entry 上的 scale / offset 属于表现参数并被采用。
///
/// 说明：asmdef 测试程序集不能引用预定义程序集 Assembly-CSharp，
/// 运行时类型（ExpeditionJourneyPersonVisualResolver / PlayerCollection / CharacterInstance / HeroInstance）
/// 全部通过反射访问（与既有测试一致）。
/// </summary>
public class JourneyPersonVisualResolutionTests
{
    private const BindingFlags F =
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

    private static Type Asm(string name)
    {
        var t = Type.GetType(name + ", Assembly-CSharp");
        Assert.IsNotNull(t, "未找到 Assembly-CSharp 类型：" + name);
        return t;
    }

    private static Type ResolverType => Asm("ExpeditionJourneyPersonVisualResolver");
    private static Type EntryType => Asm("JourneyPersonVisualEntry");
    private static Type ResultType => Asm("JourneyPersonVisualResult");
    private static Type PcType => Asm("PlayerCollection");

    private Sprite _spriteA;
    private Sprite _spriteB;
    private Sprite _spriteDefault;

    [SetUp]
    public void SetUp()
    {
        ResolverType.GetMethod("ClearWarningsForTests", BindingFlags.Public | BindingFlags.Static).Invoke(null, null);
        _spriteA = NewSprite();
        _spriteB = NewSprite();
        _spriteDefault = NewSprite();
    }

    private static Sprite NewSprite()
    {
        return Sprite.Create(new Texture2D(4, 4), new Rect(0f, 0f, 4f, 4f), new Vector2(0.5f, 0.5f));
    }

    // ==================== 构造辅助（全部反射） ====================

    private static object NewPlayerCollection()
    {
        return Activator.CreateInstance(PcType);
    }

    private static object AddCharacter(object pc, string characterId, string displayName, string personId,
        string personDefinitionId)
    {
        var ciType = Asm("CharacterInstance");
        object inst = Activator.CreateInstance(ciType, new object[] { characterId, displayName, 30, 5 });
        ciType.GetField("PersonId", F).SetValue(inst, personId);
        ciType.GetField("PersonDefinitionId", F).SetValue(inst, personDefinitionId);
        ((IList)PcType.GetField("OwnedCharacters", F).GetValue(pc)).Add(inst);
        return inst;
    }

    private static object AddHero(object pc, string heroId, string displayName, string personId)
    {
        var hiType = Asm("HeroInstance");
        object inst = Activator.CreateInstance(hiType, new object[] { heroId, displayName, 40 });
        hiType.GetField("PersonId", F).SetValue(inst, personId);
        ((IList)PcType.GetField("OwnedHeroes", F).GetValue(pc)).Add(inst);
        return inst;
    }

    private static Array NewEntries(params object[] keySpritePairs)
    {
        var array = Array.CreateInstance(EntryType, keySpritePairs.Length / 2);
        for (int i = 0; i < array.Length; i++)
        {
            object entry = Activator.CreateInstance(EntryType);
            EntryType.GetField("AppearanceKey", F).SetValue(entry, keySpritePairs[i * 2]);
            EntryType.GetField("Body", F).SetValue(entry, keySpritePairs[i * 2 + 1]);
            EntryType.GetField("Scale", F).SetValue(entry, 1f);
            EntryType.GetField("Offset", F).SetValue(entry, Vector2.zero);
            array.SetValue(entry, i);
        }
        return array;
    }

    private static object Resolve(object pc, string personId, Array entries, Sprite defaultBody)
    {
        var m = ResolverType.GetMethod("Resolve", BindingFlags.Public | BindingFlags.Static);
        Assert.IsNotNull(m, "Resolve 未找到");
        return m.Invoke(null, new object[] { pc, personId, entries, defaultBody });
    }

    private static object ResolveAppearanceKey(object pc, string personId)
    {
        var m = ResolverType.GetMethod("ResolveAppearanceKey", BindingFlags.Public | BindingFlags.Static);
        Assert.IsNotNull(m, "ResolveAppearanceKey 未找到");
        return m.Invoke(null, new object[] { pc, personId });
    }

    private static T Field<T>(object boxed, string name)
    {
        var f = ResultType.GetField(name, F);
        Assert.IsNotNull(f, "结果字段不存在：" + name);
        return (T)f.GetValue(boxed);
    }

    // ==================== 1. Character 外观键 ====================

    [Test]
    public void CharacterVisualKey_PrefersPersonDefinitionId_ThenCharacterId()
    {
        object pc = NewPlayerCollection();
        AddCharacter(pc, "char_shared", "甲", "pid_a", "def_a");
        AddCharacter(pc, "char_shared", "乙", "pid_b", null);

        Assert.AreEqual("def_a", ResolveAppearanceKey(pc, "pid_a"), "有 PersonDefinitionId 时必须优先作为外观键");
        Assert.AreEqual("char_shared", ResolveAppearanceKey(pc, "pid_b"),
            "缺少 PersonDefinitionId 时外观键回退 CharacterId");
    }

    [Test]
    public void CharacterWithBinding_UsesBoundVisual_AndKeepsPersonIdIdentity()
    {
        object pc = NewPlayerCollection();
        AddCharacter(pc, "char_shared", "甲", "pid_a", "def_a");

        object result = Resolve(pc, "pid_a", NewEntries("def_a", _spriteA), _spriteDefault);

        Assert.AreEqual("pid_a", Field<string>(result, "PersonId"), "身份键必须是传入的 PersonId");
        Assert.AreEqual("def_a", Field<string>(result, "AppearanceKey"), "外观键来自 exact Person 的字段");
        Assert.AreSame(_spriteA, Field<Sprite>(result, "Body"), "有绑定时必须使用绑定视觉");
        Assert.IsFalse(Field<bool>(result, "UsedFallback"), "有绑定时不得标记 fallback");
        Assert.IsFalse(Field<bool>(result, "IsHero"), "Character 不得被标记为 Hero");
        Assert.AreEqual("甲", Field<string>(result, "DisplayName"), "DisplayName 只用于表现，不参与身份判定");
    }

    // ==================== 2. Hero 外观键 ====================

    [Test]
    public void HeroVisualKey_UsesHeroId_AndStaysIndependentPerson()
    {
        object pc = NewPlayerCollection();
        AddHero(pc, "hero_001", "青鹿", "pid_h");

        Assert.AreEqual("hero_001", ResolveAppearanceKey(pc, "pid_h"), "Hero 外观键必须来自 HeroId");

        object result = Resolve(pc, "pid_h", NewEntries("hero_001", _spriteB), _spriteDefault);

        Assert.IsTrue(Field<bool>(result, "IsHero"), "Hero 必须被识别为 Hero");
        Assert.AreEqual("pid_h", Field<string>(result, "PersonId"));
        Assert.AreSame(_spriteB, Field<Sprite>(result, "Body"));
    }

    // ==================== 3. 身份不得由模板键 / DisplayName 推断 ====================

    [Test]
    public void UnknownPerson_AppearanceKey_IsNull_AndNeverGuessedFromTemplateOrPersonId()
    {
        object pc = NewPlayerCollection();
        AddCharacter(pc, "char_shared", "甲", "pid_a", "def_a");

        Assert.IsNull(ResolveAppearanceKey(pc, "pid_unknown"),
            "找不到具体人物时必须返回 null，禁止把模板键或 PersonId 当作外观键猜出来");
        Assert.IsNull(ResolveAppearanceKey(pc, null), "空 PersonId 必须返回 null");
        Assert.IsNull(ResolveAppearanceKey(null, "pid_a"), "空集合必须返回 null");
    }

    // ==================== 4. 同模板两名 Person 不合并 ====================

    [Test]
    public void SameTemplate_TwoPersons_AreNotMerged()
    {
        object pc = NewPlayerCollection();
        AddCharacter(pc, "char_shared", "同名", "pid_x", null);
        AddCharacter(pc, "char_shared", "同名", "pid_y", null);

        object rx = Resolve(pc, "pid_x", NewEntries("char_shared", _spriteA), _spriteDefault);
        object ry = Resolve(pc, "pid_y", NewEntries("char_shared", _spriteA), _spriteDefault);

        Assert.AreNotEqual(Field<string>(rx, "PersonId"), Field<string>(ry, "PersonId"),
            "同模板两名 Person 的解析结果必须保留各自 PersonId（不得塌缩成一个）");
        Assert.AreEqual("char_shared", Field<string>(rx, "AppearanceKey"));
        Assert.AreEqual("char_shared", Field<string>(ry, "AppearanceKey"),
            "同模板可共享同一份外观资源，但身份仍由 PersonId 区分");
    }

    // ==================== 5. Fallback ====================

    [Test]
    public void UnknownPerson_UsesDefaultPlaceholder_WithWarnOnce()
    {
        LogAssert.Expect(LogType.Warning, new Regex("找不到 PersonId=pid_missing"));

        object pc = NewPlayerCollection();
        AddCharacter(pc, "char_shared", "甲", "pid_a", "def_a");

        object result = Resolve(pc, "pid_missing", NewEntries("def_a", _spriteA), _spriteDefault);

        Assert.IsTrue(Field<bool>(result, "UsedFallback"), "找不到人物必须走 default placeholder");
        Assert.AreSame(_spriteDefault, Field<Sprite>(result, "Body"));
        Assert.AreEqual("pid_missing", Field<string>(result, "PersonId"), "fallback 仍保留真实 PersonId");
    }

    [Test]
    public void KnownPersonWithoutVisualBinding_UsesDefaultPlaceholder_WithWarnOnce()
    {
        LogAssert.Expect(LogType.Warning, new Regex("没有可用的视觉绑定"));

        object pc = NewPlayerCollection();
        AddCharacter(pc, "char_only", "甲", "pid_a", "def_a");

        object result = Resolve(pc, "pid_a", NewEntries("def_other", _spriteA), _spriteDefault);

        Assert.IsTrue(Field<bool>(result, "UsedFallback"), "缺少绑定时必须走 default placeholder");
        Assert.AreSame(_spriteDefault, Field<Sprite>(result, "Body"));
        Assert.AreEqual("def_a", Field<string>(result, "AppearanceKey"),
            "即使视觉缺失，外观键仍来自 exact Person（不得因缺失而改变身份 / 外观键来源）");
    }

    [Test]
    public void MissingPerson_WarningIsDeduplicated()
    {
        LogAssert.Expect(LogType.Warning, new Regex("找不到 PersonId=pid_gone"));

        object pc = NewPlayerCollection();
        Resolve(pc, "pid_gone", NewEntries("def_a", _spriteA), _spriteDefault);
        Resolve(pc, "pid_gone", NewEntries("def_a", _spriteA), _spriteDefault);
        // 第二次不得再输出同一条 warning；若重复输出，本测试会因为多出的未预期 log 失败。
        LogAssert.NoUnexpectedReceived();
    }

    // ==================== 6. 表现参数 ====================

    [Test]
    public void EntryScaleAndOffset_AreAppliedToResult()
    {
        object pc = NewPlayerCollection();
        AddCharacter(pc, "char_shared", "甲", "pid_a", "def_a");

        Array entries = Array.CreateInstance(EntryType, 1);
        object entry = Activator.CreateInstance(EntryType);
        EntryType.GetField("AppearanceKey", F).SetValue(entry, "def_a");
        EntryType.GetField("Body", F).SetValue(entry, _spriteA);
        EntryType.GetField("Scale", F).SetValue(entry, 1.5f);
        EntryType.GetField("Offset", F).SetValue(entry, new Vector2(12f, -8f));
        entries.SetValue(entry, 0);

        object result = Resolve(pc, "pid_a", entries, _spriteDefault);

        Assert.AreEqual(1.5f, Field<float>(result, "Scale"), 0.0001f, "entry 上的 scale 必须被采用");
        Assert.AreEqual(new Vector2(12f, -8f), Field<Vector2>(result, "Offset"), "entry 上的 offset 必须被采用");
    }
}
