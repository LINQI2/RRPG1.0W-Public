using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public class CurrentUIPresentationContractTests
{
    static Type UI(string name) { return Type.GetType(name + ", Assembly-CSharp", true); }
    static object Field(object obj, string name) { return obj.GetType().GetField(name).GetValue(obj); }
    static void Set(object obj, string name, object value) { obj.GetType().GetField(name).SetValue(obj, value); }
    static object Resolve(BattleCard card, IBattleRuleContext context, ScriptableObject catalog, Sprite placeholder = null, Sprite common = null, object people = null)
    {
        return UI("BattleCardPresentationResolver").GetMethod("Resolve").Invoke(null, new object[] { card, context, people, catalog, placeholder, placeholder, common });
    }
    static ScriptableObject Catalog(string template, string description, Sprite art = null)
    {
        var asset = ScriptableObject.CreateInstance(UI("BattleCardPresentationCatalog"));
        var entry = Activator.CreateInstance(UI("BattleCardPresentationCatalog+Entry"));
        Set(entry, "TemplateId", template); Set(entry, "DescriptionTemplate", description); Set(entry, "ArtSprite", art);
        ((IList)Field(asset, "entries")).Add(entry); return asset;
    }
    static BattleCard Card(BattleRuleValueSource source, int amount = 6)
    {
        return new BattleCard { TemplateId = "contract-template", CardKind = BattleCardKind.Common, Cost = 2, Description = "旧说明",
            RuleProgram = new BattleCardRuleProgram { Trigger = BattleRuleTrigger.OnPlay, Steps = new List<BattleCardRuleStep> {
                new BattleCardRuleStep { Operation = BattleRuleOperation.Damage, ValueSource = source, ValueParam = amount,
                    BonusValueSource = BattleRuleValueSource.Fixed, BonusValueParam = 3 } } } };
    }
    [Test]
    public void FixedDescriptionAndBonusUseExecutorAuthority()
    {
        var card = Card(BattleRuleValueSource.Fixed); var catalog = Catalog(card.TemplateId, "{step:0.value}/{step:0.bonus}");
        try
        {
            Assert.AreEqual("6/3", Field(Resolve(card, null, catalog), "RichDescription"));
            Assert.AreEqual(6, BattleCardRuleExecutor.ResolveValue(BattleRuleValueSource.Fixed, 6, card, null));
            Assert.AreEqual(6, BattleRuleValueResolver.Resolve(BattleRuleValueSource.Fixed, 6, card, null));
        }
        finally { UnityEngine.Object.DestroyImmediate(catalog); }
    }
    [Test]
    public void AttackAndActualCostRefreshWithLiveFacts()
    {
        var card = Card(BattleRuleValueSource.CasterAttack); var facts = new Facts();
        var catalog = Catalog(card.TemplateId, "{step:0.value}/{owner.attack}/{cost}");
        try
        {
            Assert.AreEqual("8/8/2", Field(Resolve(card, facts, catalog), "RichDescription"));
            facts.Owner.Attack = 10; card.AddCostModifier(-1);
            Assert.AreEqual("10/10/1", Field(Resolve(card, facts, catalog), "RichDescription"));
            Assert.AreEqual(10, BattleCardRuleExecutor.ResolveValue(BattleRuleValueSource.CasterAttack, 0, card, facts));
        }
        finally { UnityEngine.Object.DestroyImmediate(catalog); }
    }
    [Test]
    public void UnknownOrUnavailableTokenIsDiagnosticAndNeverFalseZero()
    {
        var card = Card(BattleRuleValueSource.CasterAttack); var catalog = Catalog(card.TemplateId, "{step:0.value}/{step:99.value}/{arbitrary()}");
        try
        {
            var result = Resolve(card, null, catalog);
            Assert.AreEqual("<?>/<?>/<?>", Field(result, "RichDescription"));
            Assert.AreEqual(3, ((IList)Field(result, "Diagnostics")).Count);
        }
        finally { UnityEngine.Object.DestroyImmediate(catalog); }
    }
    [Test]
    public void RichKeywordLinkHasStableIdAndTMPPlainTextDoesNotContainMarkup()
    {
        var card = Card(BattleRuleValueSource.Fixed); var catalog = Catalog(card.TemplateId, "获得 <b>{step:0.value}</b> 点 <link=\"keyword:shield\">护盾</link>。");
        var canvas = new GameObject("TMP contract canvas", typeof(Canvas));
        var go = new GameObject("TMP contract", typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(canvas.transform, false);
        try
        {
            var tmp = go.GetComponent<TMP_Text>(); tmp.font = TMP_Settings.defaultFontAsset;
            tmp.text = (string)Field(Resolve(card, null, catalog), "RichDescription");
            tmp.richText = true; tmp.ForceMeshUpdate(true, true);
            Assert.AreEqual(1, tmp.textInfo.linkCount); Assert.AreEqual("keyword:shield", tmp.textInfo.linkInfo[0].GetLinkID());
            StringAssert.DoesNotContain("<link", tmp.GetParsedText()); StringAssert.DoesNotContain("<b>", tmp.GetParsedText());
        }
        finally { UnityEngine.Object.DestroyImmediate(canvas); UnityEngine.Object.DestroyImmediate(catalog); }
    }
    [Test]
    public void ArtHitPlaceholderCommonOwnerAndLegacyDescription()
    {
        var texture = new Texture2D(2, 2); var art = Sprite.Create(texture, new Rect(0, 0, 2, 2), Vector2.zero);
        var fallbackTexture = new Texture2D(2, 2); var fallback = Sprite.Create(fallbackTexture, new Rect(0, 0, 2, 2), Vector2.zero);
        var card = Card(BattleRuleValueSource.Fixed); var catalog = Catalog(card.TemplateId, null, art);
        try
        {
            var output = Resolve(card, null, catalog, fallback, art);
            Assert.AreSame(art, Field(output, "Art")); Assert.AreSame(art, Field(output, "OwnerPortrait"));
            Assert.AreEqual(card.Description, Field(output, "RichDescription"));
            var neutral = Resolve(card, null, catalog, fallback);
            Assert.AreSame(fallback, Field(neutral, "OwnerPortrait"), "Missing Common icon uses the existing neutral owner placeholder.");
            Assert.AreEqual("通用", Field(neutral, "OwnerName"));
            card.TemplateId = "missing"; Assert.AreSame(fallback, Field(Resolve(card, null, catalog, fallback), "Art"));
        }
        finally { UnityEngine.Object.DestroyImmediate(catalog); UnityEngine.Object.DestroyImmediate(art); UnityEngine.Object.DestroyImmediate(fallback); UnityEngine.Object.DestroyImmediate(texture); UnityEngine.Object.DestroyImmediate(fallbackTexture); }
    }
    [Test]
    public void CurrentSceneAndPrefabsContainNoLegacyTextOutsideExplicitWhitelist()
    {
        foreach (string name in new[] { "BattleCard", "UnitCard", "CharCard", "HeroSelectCard", "JourneyPartyMemberView", "CharacterGrowthUI", "ItemCard" })
            Assert.IsEmpty(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/" + name + ".prefab").GetComponentsInChildren<Text>(true), name);
        var scene = UnityEngine.SceneManagement.SceneManager.GetSceneByPath("Assets/Scenes/SampleScene.scene"); bool opened = !scene.isLoaded;
        if (opened) scene = EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.scene", OpenSceneMode.Additive);
        try
        {
            // 白名单属于旧界面宿主，不依赖该宿主是否恰为场景根对象。
            foreach (var root in scene.GetRootGameObjects())
                foreach (var text in root.GetComponentsInChildren<Text>(true))
                    Assert.IsTrue(new[] { "MapUI", "ShopUI", "RecruitUI" }
                        .Any(name => text.GetComponentInParent(UI(name), true) != null), text.name);
        }
        finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
    }
    [Test]
    public void RuntimeCannotRegrowLegacyTextOutsideWhitelist()
    {
        foreach (var file in Directory.GetFiles(Application.dataPath + "/Scripts", "*.cs", SearchOption.AllDirectories))
        {
            if (new[] { "ShopUI.cs", "RecruitUI.cs", "MapUI.cs" }.Contains(Path.GetFileName(file))) continue;
            Assert.IsFalse(Regex.IsMatch(File.ReadAllText(file), @"(?:AddComponent\s*<\s*(?:UnityEngine\.UI\.)?Text\s*>|typeof\s*\(\s*(?:UnityEngine\.UI\.)?Text\s*\))"), file);
        }
    }
    [Test]
    public void PlayerCardViewWritesTMPAndHidesInternalIdentityAndFlags()
    {
        var root = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/BattleCard.prefab"));
        try
        {
            var view = root.GetComponent(UI("BattleCardView")); var card = Card(BattleRuleValueSource.Fixed); card.OwnerPersonId = "person_internal";
            view.GetType().GetMethod("Bind", new[] { typeof(BattleCard) }).Invoke(view, new object[] { card });
            Assert.AreEqual("2", ((TMP_Text)Field(view, "costText")).text);
            Assert.IsFalse(((TMP_Text)Field(view, "ownerText")).enabled);
            Assert.IsFalse(((TMP_Text)Field(view, "flagsText")).enabled);
            Assert.IsFalse(root.GetComponentsInChildren<TMP_Text>(true).Any(t => t.enabled && t.text != null && t.text.Contains("person_internal")));
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }
    [TestCase("{0}/{1}", 3, 5, "3/5")]
    [TestCase("第{0}回合", 4, 0, "第4回合")]
    [TestCase("{0}", 12, 0, "12")]
    public void InspectorHudFormattingIsReadOnly(string format, int a, int b, string expected)
    {
        Assert.AreEqual(expected, UI("BattleHudFormatting").GetMethod("Format").Invoke(null, new object[] { format, new object[] { a, b } }));
    }
    sealed class Facts : IBattleRuleContext
    {
        public BattleRuleUnitFacts Owner = new BattleRuleUnitFacts { UnitId = "unit_exact", PersonId = "person_exact", Attack = 8 };
        public int SharedActionPointsCurrent { get { return 3; } }
        public bool TryGetOwnerUnitId(BattleCard c, out string id) { id = Owner.UnitId; return true; }
        public bool TryGetUnit(string id, out BattleRuleUnitFacts unit) { unit = Owner; return id == Owner.UnitId; }
        public IReadOnlyList<BattleRuleUnitFacts> GetAllies() { return new[] { Owner }; }
        public IReadOnlyList<BattleRuleUnitFacts> GetEnemies() { return new BattleRuleUnitFacts[0]; }
        public IReadOnlyList<BattleRuleCellFacts> GetAlliedCells() { return new BattleRuleCellFacts[0]; }
        public IReadOnlyList<BattleCard> GetHandCards() { return new BattleCard[0]; }
        public IReadOnlyList<BattleRuleUnitFacts> GetAdjacentAllies(string id) { return GetAllies(); }
        public bool OwnerIsAloneInRow(BattleCard c) { return true; }
        public bool ApplyDamage(string id, int amount) { throw new InvalidOperationException("Presentation wrote Gameplay"); }
        public bool ApplyHeal(string id, int amount) { throw new InvalidOperationException(); }
        public bool ApplyShield(string id, int amount) { throw new InvalidOperationException(); }
        public bool DrawCards(int n) { throw new InvalidOperationException(); }
        public bool ApplyCostModifier(BattleCard c, int n) { throw new InvalidOperationException(); }
        public bool ApplyDamageModifier(BattleCard c, int n) { throw new InvalidOperationException(); }
        public bool TryMoveOwnerTo(BattleCard c, int r, int col) { throw new InvalidOperationException(); }
        public bool TrySwapOwnerWith(BattleCard c, string id) { throw new InvalidOperationException(); }
    }
}
