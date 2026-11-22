using TMPro;
using System;
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
/// Initial Person &amp; Profession Bootstrap — C. Fresh New Game PlayMode（Disposable Development Test）。
///
/// 真实产品入口（不绕过产品流程）：
///   SampleScene → MainMenuUI.OnNewGame（真实按钮回调）→ HeroSelectionUI 选择并确认
///   → SaveSystem.NewGame → PlayerCollection.InitializeDefaultCollection
///   → PersonDefinitionCatalog 初始名册 → PersonBootstrapper → Save → TownUI。
///
/// 断言：
///   - 开局人数 = 配置行数（读取产品 PersonDefinitionCatalog，不写死 5）；
///   - 人名来自 Person.csv（PersonDefinition.DisplayName）；
///   - 当前初始名册全部拥有 Active Profession（GrowthAuthority.IsAvailable == true）；
///   - CharacterGrowthUI 不再显示「未形成职业」；
///   - Save → Load（Continue）后人数与 Profession 保持一致。
/// </summary>
public class PersonBootstrapFreshNewGamePlayTests
{
    private const BindingFlags F =
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

    private static Type GetType(string name) => Type.GetType(name + ", Assembly-CSharp");

    private static object FindInstance(string typeName)
    {
        var t = GetType(typeName);
        if (t == null) return null;
        foreach (var o in UnityEngine.Object.FindObjectsOfType(t, true)) if (o != null) return o;
        return null;
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
        int count = args == null ? 0 : args.Length;
        foreach (var m in o.GetType().GetMethods(F).Where(m => m.Name == method && m.GetParameters().Length == count))
            return m.Invoke(o, args);
        Assert.Fail("方法不存在或参数数量不匹配：" + method + " @ " + o.GetType().FullName);
        return null;
    }

    private static object CallStatic(string typeName, string method, params object[] args)
    {
        var t = GetType(typeName);
        Assert.IsNotNull(t, "类型不存在：" + typeName);
        int count = args == null ? 0 : args.Length;
        foreach (var m in t.GetMethods(F).Where(m => m.Name == method && m.GetParameters().Length == count))
            return m.Invoke(null, args);
        Assert.Fail("静态方法不存在或参数数量不匹配：" + typeName + "." + method);
        return null;
    }

    private static string SavePath() => (string)CallStatic("SaveSystem", "GetSavePath");

    private static void DeleteSave()
    {
        string p = SavePath();
        if (!string.IsNullOrEmpty(p) && System.IO.File.Exists(p)) System.IO.File.Delete(p);
    }

    /// <summary>读取产品配置的初始名册（Person.csv → PersonDefinitionCatalog）。</summary>
    private static List<PersonDefinition> ConfiguredRoster()
    {
        var roster = new List<PersonDefinition>();
        foreach (var catalog in Resources.LoadAll<PersonDefinitionCatalog>("ScriptableObjects/Persons"))
        {
            if (catalog == null || catalog.Definitions == null) continue;
            roster.AddRange(catalog.BuildInitialRoster());
        }
        return roster;
    }

    private static object OwnedCharacters(object collection) => GetMember(collection, "OwnedCharacters");

    private static object PersonOfDefinition(object collection, string personDefinitionId)
    {
        foreach (var c in (IEnumerable)OwnedCharacters(collection))
        {
            if (c == null) continue;
            if ((string)GetMember(c, "PersonDefinitionId") == personDefinitionId) return c;
        }
        return null;
    }

    private static bool HasActiveProfession(object character)
        => (bool)GetMember(character, "HasActiveProfession");

    private static object ActiveProgress(object character) => GetMember(character, "ActiveProfessionProgress");

    [SetUp]
    public void SetUp()
    {
        CallStatic("VillageRuntimeBridge", "ResetForTests");
        DeleteSave();
    }

    [TearDown]
    public void TearDown()
    {
        DeleteSave();
        CallStatic("VillageRuntimeBridge", "ResetForTests");
    }

    [UnityTest]
    public IEnumerator FreshNewGame_CreatesConfiguredRosterWithInitialProfession()
    {
        var roster = ConfiguredRoster();
        Assert.IsNotEmpty(roster, "产品 Person.csv 必须已导入（InitialRosterOrder > 0 的初始名册）");

        SceneManager.LoadScene("SampleScene");
        yield return null;
        yield return null;

        // 真实 New Game 入口：MainMenuUI.OnNewGame → HeroSelectionUI → OnConfirm
        var mainMenu = FindInstance("MainMenuUI");
        Assert.IsNotNull(mainMenu, "MainMenuUI 应存在");
        Call(mainMenu, "OnNewGame");
        yield return null;

        var heroSelection = FindInstance("HeroSelectionUI");
        Assert.IsNotNull(heroSelection, "HeroSelectionUI 应存在（场景已绑定）");
        var heroCards = (IList)GetMember(heroSelection, "_heroCards");
        Assert.IsNotEmpty(heroCards, "英雄选择界面应有可选英雄");
        Call(heroSelection, "OnHeroCardClick", 0);
        Call(heroSelection, "OnConfirm");
        yield return null;
        yield return null;

        var town = FindInstance("TownUI");
        Assert.IsNotNull(town, "确认新游戏后应进入领地");
        object collection = GetMember(town, "PlayerData");
        Assert.IsNotNull(collection, "TownUI 应持有刚创建的 PlayerCollection");

        var characters = (IList)OwnedCharacters(collection);
        Assert.AreEqual(roster.Count, characters.Count,
            "开局人数必须等于配置行数（代码不写死初始人数）");

        foreach (var definition in roster)
        {
            object person = PersonOfDefinition(collection, definition.PersonDefinitionId);
            Assert.IsNotNull(person, "配置人物未创建：" + definition.PersonDefinitionId);
            Assert.AreEqual(definition.DisplayName, (string)GetMember(person, "DisplayName"),
                "人名必须来自 PersonDefinition（Person.csv）");
            string personId = (string)GetMember(person, "PersonId");
            Assert.AreNotEqual(definition.PersonDefinitionId, personId,
                "PersonId 不得等于 PersonDefinitionId");
            Assert.IsTrue(HasActiveProfession(person),
                definition.PersonDefinitionId + " 开局必须有 Active Profession");

            object progress = ActiveProgress(person);
            Assert.IsNotNull(progress);
            Assert.AreEqual(definition.InitialProfession.ProfessionId, (string)GetMember(progress, "ProfessionId"),
                "开局职业必须来自配置");
            Assert.AreEqual(definition.InitialProfession.InitialLevel, (int)GetMember(progress, "ProfessionLevel"));
        }

        // CharacterGrowthUI：不再显示「未形成职业」
        var growthUI = FindInstance("CharacterGrowthUI");
        Assert.IsNotNull(growthUI, "TownUI 应确保 CharacterGrowthUI 宿主存在");
        Call(growthUI, "Open");
        yield return null;

        var texts = ((Component)growthUI).GetComponentsInChildren<TMP_Text>(true)
            .Where(t => t != null && !string.IsNullOrEmpty(t.text)).Select(t => t.text).ToList();
        Assert.IsNotEmpty(texts, "CharacterGrowthUI 应渲染人物列表");
        Assert.IsFalse(texts.Any(t => t.Contains("未形成职业")),
            "开局固定人物已有 Profession，成长界面不得再显示「未形成职业」：" + string.Join(" | ", texts));
        Call(growthUI, "Hide");
        yield return null;

        // Save → Load（Continue）一致性
        object loaded = CallStatic("SaveSystem", "Load");
        Assert.IsNotNull(loaded, "新档必须可加载");
        var loadedCharacters = (IList)OwnedCharacters(loaded);
        Assert.AreEqual(roster.Count, loadedCharacters.Count, "Continue 后人数必须一致");
        foreach (var definition in roster)
        {
            object person = PersonOfDefinition(loaded, definition.PersonDefinitionId);
            Assert.IsNotNull(person, "Continue 后必须仍能找到：" + definition.PersonDefinitionId);
            Assert.IsTrue(HasActiveProfession(person), "Continue 后必须保持 Active Profession");
        }
    }
}
