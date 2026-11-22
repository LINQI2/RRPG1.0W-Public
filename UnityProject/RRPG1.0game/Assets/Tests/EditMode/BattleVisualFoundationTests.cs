using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// WP-D｜Battle Visual Foundation 定向 EditMode 契约测试
///
/// 覆盖：
///   1. Formation Presentation 视觉规则（1 人居中 / 2 人聚拢 / 3 人展开、顺序与逻辑 Column 一致）；
///   2. BattleUI 可替换视觉宿主字段与宿主对象名契约（未来换素材 = Import → Assign → Inspector Tuning）；
///   3. UnitCard.prefab / BattleCard.prefab 视觉宿主结构；
///   4. 占位 Sprite 位于正式资源路径且以 Sprite 正常导入；
///   5. WP-C Legacy 序列化参数不再回到 BattleUI（旧左/中/右固定偏移已退出）。
///
/// 说明：asmdef 测试程序集不能引用预定义程序集 Assembly-CSharp，
/// 运行时类型（BattleUI / BattleFormationVisualLayout）通过反射访问（与既有测试一致）。
/// </summary>
public class BattleVisualFoundationTests
{
    private const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    private const string ArtFolder = "Assets/Art/Prototype/Battle";
    private const string UnitCardPrefabPath = "Assets/Prefabs/UI/UnitCard.prefab";
    private const string BattleCardPrefabPath = "Assets/Prefabs/UI/BattleCard.prefab";

    private static System.Type Type(string name) => System.Type.GetType(name + ", Assembly-CSharp");

    private static object CallStatic(System.Type t, string method, params object[] args)
    {
        var m = t.GetMethod(method, BindingFlags.Public | BindingFlags.Static);
        Assert.IsNotNull(m, "静态方法不存在：" + t.Name + "." + method);
        return m.Invoke(null, args);
    }

    private static float Offset(int count, int slot, float single, float two, float three)
    {
        var t = Type("BattleFormationVisualLayout");
        Assert.IsNotNull(t, "BattleFormationVisualLayout 未找到");
        return (float)CallStatic(t, "GetOffset", count, slot, single, two, three);
    }

    private static Vector2 HandPosition(int count, int index, float width = 700f)
    {
        return (Vector2)CallStatic(Type("BattleHandVisualLayout"), "GetPosition",
            count, index, width, 180f, 200f, 70f, Vector2.zero);
    }

    [Test]
    public void SharedBattleHand_OneAndThreeCardsStayCentered()
    {
        Assert.AreEqual(Vector2.zero, HandPosition(1, 0));
        Assert.AreEqual(0f, (HandPosition(3, 0).x + HandPosition(3, 2).x) * 0.5f, 0.001f);
        Assert.AreEqual(0f, HandPosition(3, 1).x, 0.001f);
    }

    [Test]
    public void SharedBattleHand_SixCardsFitAndStepContracts()
    {
        float step = HandPosition(6, 1).x - HandPosition(6, 0).x;
        Assert.Less(step, 200f, "6 张应根据容器宽度收缩间距");
        Assert.GreaterOrEqual(HandPosition(6, 0).x - 90f, -350f);
        Assert.LessOrEqual(HandPosition(6, 5).x + 90f, 350f);
        Assert.AreEqual(0f, (HandPosition(6, 0).x + HandPosition(6, 5).x) * 0.5f, 0.001f);
    }

    [Test]
    public void SharedBattleHand_CountChangesDoNotShiftCenterOrCapAtSix()
    {
        foreach (int count in new[] { 1, 3, 6, 9 })
            Assert.AreEqual(0f, (HandPosition(count, 0).x + HandPosition(count, count - 1).x) * 0.5f, 0.001f);
        Assert.Greater(HandPosition(9, 8).x, HandPosition(9, 0).x);
    }

    // ---------- 1. Formation Presentation ----------

    [Test]
    public void Formation_OneUnit_IsCentered()
    {
        float o = Offset(1, 0, 7f, 110f, 180f);
        Assert.AreEqual(7f, o, 0.0001f, "1 人排必须使用居中偏移（singleUnitVisualOffset）");
    }

    [Test]
    public void Formation_TwoUnits_GatherTowardsRowCenter()
    {
        float left = Offset(2, 0, 0f, 110f, 180f);
        float right = Offset(2, 1, 0f, 110f, 180f);

        Assert.AreEqual(55f, left, 0.0001f);
        Assert.AreEqual(-55f, right, 0.0001f);
        Assert.AreEqual(-left, right, 0.0001f, "两人排必须围绕排中心对称");
        Assert.Less(Mathf.Abs(left), 180f, "两人排不得使用三人排最外侧偏移（必须聚拢）");
    }

    [Test]
    public void Formation_ThreeUnits_SpreadAndKeepOrder()
    {
        float s0 = Offset(3, 0, 0f, 110f, 180f);
        float s1 = Offset(3, 1, 0f, 110f, 180f);
        float s2 = Offset(3, 2, 0f, 110f, 180f);

        Assert.Greater(s0, s1, "排内第一个槽位必须位于中心之上（保持 Left→Center→Right 顺序）");
        Assert.Greater(s1, s2, "排内第三个槽位必须位于中心之下（保持 Left→Center→Right 顺序）");
        Assert.AreEqual(0f, s1, 0.0001f, "三人排中间槽位必须在排中心");
        Assert.Greater(Mathf.Abs(s0), Mathf.Abs(Offset(2, 0, 0f, 110f, 180f)), "三人排必须比两人排更展开");
    }

    [Test]
    public void Formation_SlotCount_IsClampedToOneThroughThree()
    {
        var t = Type("BattleFormationVisualLayout");
        Assert.AreEqual(1, (int)CallStatic(t, "SlotCount", 0));
        Assert.AreEqual(1, (int)CallStatic(t, "SlotCount", 1));
        Assert.AreEqual(2, (int)CallStatic(t, "SlotCount", 2));
        Assert.AreEqual(3, (int)CallStatic(t, "SlotCount", 3));
        Assert.AreEqual(3, (int)CallStatic(t, "SlotCount", 8));
    }

    // ---------- 2. BattleUI 视觉宿主契约 ----------

    [Test]
    public void BattleUI_ExposesReplaceableVisualHostFields()
    {
        var t = Type("BattleUI");
        Assert.IsNotNull(t, "BattleUI 未找到");

        string[] fields =
        {
            "singleUnitVisualOffset", "twoUnitVisualSpacing", "threeUnitVisualSpacing", "rowLocalVisualOffset",
            "playerBodyPlaceholder", "shieldIconPlaceholder",
            "intentAttackPlaceholder", "intentDefendPlaceholder", "intentSpecialPlaceholder",
            "playerBodyScale", "enemyBodyScale", "unitBodyVisualOffset",
            "battleBackground", "battleBackgroundPlaceholder", "rowVisualTintEnabled"
        };
        foreach (var f in fields)
            Assert.IsNotNull(t.GetField(f, F), "BattleUI 缺少可替换视觉宿主 / 可调参数字段：" + f);
    }

    [Test]
    public void BattleUI_ExposesStableHostNamesAndPresentationEntryPoints()
    {
        var t = Type("BattleUI");

        string[] consts = { "HostAnchorVisual", "HostBodyVisual", "HostTextHP", "HostAnchorShield", "HostTextShield",
                            "HostAnchorIntent", "HostTextIntent", "HostAnchorStatus", "HostAnchorEffect", "RowVisualChildName" };
        foreach (var c in consts)
            Assert.IsNotNull(t.GetField(c, BindingFlags.Public | BindingFlags.Static | BindingFlags.NonPublic),
                "BattleUI 缺少宿主对象名常量：" + c);

        Assert.IsNotNull(t.GetMethod("ApplyFormationPresentation", F), "缺少 Formation Presentation 单一入口");
        Assert.IsNotNull(t.GetMethod("ApplyFormationPresentationToAllRows", F), "缺少六排 Formation 重排入口");
        Assert.IsNotNull(t.GetMethod("ApplyUnitVisualHosts", F), "缺少 Unit Visual Host 绑定入口");
        Assert.IsNotNull(t.GetMethod("SetShieldHost", F), "缺少独立 Shield 宿主入口");
        Assert.IsNotNull(t.GetMethod("SetIntentHost", F), "缺少稳定 Intent 宿主入口");
        Assert.IsNotNull(t.GetMethod("RefreshBattleBackgroundHost", F), "缺少 Background 宿主刷新入口");
    }

    [Test]
    public void BattleUI_LegacyFixedColumnOffsetsAreRemoved()
    {
        var t = Type("BattleUI");
        Assert.IsNull(t.GetField("leftColumnOffset", F), "旧左列固定偏移参数应已退出");
        Assert.IsNull(t.GetField("centerColumnOffset", F), "旧中列固定偏移参数应已退出");
        Assert.IsNull(t.GetField("rightColumnOffset", F), "旧右列固定偏移参数应已退出");
    }

    // ---------- 3. Prefab 视觉宿主结构 ----------

    private static Transform Find(Transform root, string path)
    {
        return root == null ? null : root.Find(path);
    }

    [Test]
    public void UnitCardPrefab_HasUnitVisualHosts()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(UnitCardPrefabPath);
        Assert.IsNotNull(prefab, "UnitCard.prefab 未找到");

        var bodyHost = Find(prefab.transform, "Anchor_Visual/Body_Visual");
        Assert.IsNotNull(bodyHost, "缺少完整小人身体宿主 Anchor_Visual/Body_Visual");
        var body = bodyHost.GetComponent<Image>();
        Assert.IsNotNull(body, "Body_Visual 必须是 Image");
        Assert.IsNotNull(body.sprite, "Body_Visual 必须绑定占位 Sprite（未来替换为正式 Sprite）");

        Assert.IsNotNull(Find(prefab.transform, "Anchor_Shield/Text_Shield"), "缺少独立 Shield 文本宿主");
        Assert.IsNotNull(Find(prefab.transform, "Anchor_Shield/Image_ShieldIcon"), "缺少 Shield 图标宿主");

        Assert.IsNotNull(Find(prefab.transform, "Anchor_Intent/Text_Intent"), "缺少稳定 Intent 文本宿主");
        Assert.IsNotNull(Find(prefab.transform, "Anchor_Intent/Image_IntentIcon"), "缺少 Intent 图标宿主");
        Assert.IsNotNull(Find(prefab.transform, "Anchor_Status"), "缺少 Anchor_Status（可为空）");
        Assert.IsNotNull(Find(prefab.transform, "Anchor_Effect"), "缺少 Anchor_Effect（可为空）");

        // 独立 HP 宿主沿用现有 Text_HP；敌方行动次数宿主 Text_AP 必须保留
        Assert.IsNotNull(Find(prefab.transform, "Text_HP"), "缺少 HP 宿主 Text_HP");
        Assert.IsNotNull(Find(prefab.transform, "Text_AP"), "敌方行动次数宿主 Text_AP 不得被清理");
    }

    [Test]
    public void BattleCardPrefab_HasArtAndDescriptionSlots()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BattleCardPrefabPath);
        Assert.IsNotNull(prefab, "BattleCard.prefab 未找到");

        var art = Find(prefab.transform, "Image_Art");
        Assert.IsNotNull(art, "缺少卡面 Art 视觉槽 Image_Art");
        var artImage = art.GetComponent<Image>();
        Assert.IsNotNull(artImage, "Image_Art 必须是 Image");
        Assert.IsNotNull(artImage.sprite, "Art 槽必须绑定占位 Sprite");

        Assert.IsNotNull(Find(prefab.transform, "Text_Description"), "缺少卡牌说明宿主 Text_Description");

        var viewType = Type("BattleCardView");
        Assert.IsNotNull(viewType, "BattleCardView 未找到");
        var view = prefab.GetComponent(viewType);
        Assert.IsNotNull(view, "BattleCard.prefab 必须保留 BattleCardView");
        Assert.IsNotNull(viewType.GetField("artImage", F).GetValue(view), "BattleCardView.artImage 未绑定");
        Assert.IsNotNull(viewType.GetField("artPlaceholder", F).GetValue(view), "BattleCardView.artPlaceholder 未绑定");
        Assert.IsNotNull(viewType.GetField("descriptionText", F).GetValue(view), "BattleCardView.descriptionText 未绑定");
        Assert.IsNotNull(viewType.GetField("nameText", F).GetValue(view), "BattleCardView 既有的 Name 绑定不得回归");
    }

    // ---------- 4. 占位资源与场景清理 ----------

    [Test]
    public void PlaceholderSprites_ExistAsImportedSprites()
    {
        string[] names =
        {
            "battle_bg_placeholder", "player_body_placeholder", "enemy_body_placeholder",
            "shield_placeholder", "intent_attack_placeholder", "intent_defend_placeholder",
            "intent_special_placeholder", "card_art_placeholder"
        };
        foreach (var n in names)
        {
            var path = ArtFolder + "/" + n + ".png";
            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<Sprite>(path), "占位 Sprite 缺失或未按 Sprite 导入：" + path);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            Assert.IsNotNull(importer, "占位贴图 Import 设置缺失：" + path);
            Assert.AreEqual(SpriteImportMode.Single, importer.spriteImportMode, "占位 Sprite 必须使用 Single 导入：" + path);
        }
    }

    [Test]
    public void LegacyBattleSceneObjects_AreRemovedFromScene()
    {
        var sceneText = System.IO.File.ReadAllText("Assets/Scenes/SampleScene.scene");
        string[] dead = { "BtnAttackOp", "BtnSkillOp", "BtnGuardOp", "ItemCardRoot" };
        foreach (var d in dead)
            Assert.IsFalse(sceneText.Contains(d), "WP-C Dead 遗留对象仍在场景文件中：" + d);

        Assert.IsTrue(sceneText.Contains("BtnCancelOp"), "Current Card Cancel 不得被清理");
        Assert.IsTrue(sceneText.Contains("BattleBackground"), "Battle Background 宿主缺失");
        Assert.IsTrue(sceneText.Contains("RowVisual"), "Row Visual 宿主缺失");
    }
}
