using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

/// <summary>
/// M3 专项 — 真实宿主（CharacterInstance，经反射）上的 Profession Progress 语义守卫：
///   Test 1  新普通 Character：ActiveProfessionProgress == null 合法；
///   Test 2  Character 能正确持有初始化好的 ProfessionProgress；
///   Test 3  两个不同 PersonId、同 ProfessionId 的 Character：改 A 不影响 B；
///   Test 4  改 Person Progress 不改 ProfessionDefinition；
///   Test 9  Hero Guard：HeroInstance / HeroSaveData 不进入 M3（无 Profession 结构）。
/// 不修改 Growth Resolver / CharacterGrowthService（本文件不做任何读写旧 Growth 字段的断言迁移）。
/// </summary>
public class ProfessionProgressHostGuardTests
{
    [Test]
    public void NewCharacter_ActiveProfessionProgressIsNull_Legal()
    {
        // Test 1 — Profession 形成前允许为空：新普通人物长期生活 / 工作也不自动获得职业
        object character = ProfessionM3TestKit.NewCharacter("char_001", "测试人物", 30, 6);

        Assert.IsFalse(string.IsNullOrEmpty(ProfessionM3TestKit.PersonIdOf(character)));
        Assert.IsNull(ProfessionM3TestKit.ActiveProgress(character),
            "新 Character 未形成 Profession 时 ActiveProfessionProgress 必须为 null（合法，非异常）");
        Assert.IsFalse(ProfessionM3TestKit.HasActiveProfession(character));

        // 未自动 Formation / 未伪造 ProfessionId
        Assert.AreEqual("char_001", (string)ProfessionM3TestKit.Get(character, "CharacterId"),
            "CharacterId 仍只是模板 ID，不得因“无职业”被改写成 ProfessionId");
    }

    [Test]
    public void Character_CanHoldInitializedProfessionProgress()
    {
        // Test 2（宿主层）— 挂载后 Character 持有同一实例，五字段可读
        object character = ProfessionM3TestKit.NewCharacter("char_001", "测试人物", 30, 6);
        ProfessionProgress progress = ProfessionM3TestKit.NewHunterProgress();

        ProfessionM3TestKit.Set(character, "ActiveProfessionProgress", progress);

        Assert.IsTrue(ProfessionM3TestKit.HasActiveProfession(character));
        Assert.AreSame(progress, ProfessionM3TestKit.ActiveProgress(character));

        ProfessionProgress held = ProfessionM3TestKit.ActiveProgress(character);
        Assert.AreEqual("profession_test_hunter", held.ProfessionId);
        Assert.AreEqual(1, held.ProfessionLevel);
        Assert.AreEqual(0, held.ProfessionExp);
        CollectionAssert.AreEqual(new[] { "card_a", "card_b" }, held.UnlockedPoolCardIds);
        Assert.AreEqual(1, held.ProfessionTraitLevel);
    }

    [Test]
    public void TwoSameProfessionPersons_ProgressFullyIndependent()
    {
        // Test 3 — Person A / Person B 拥有各自独立 ProfessionProgress（同 ProfessionId）
        object personA = ProfessionM3TestKit.NewCharacter("char_001", "猎人甲", 30, 6);
        object personB = ProfessionM3TestKit.NewCharacter("char_001", "猎人乙", 30, 6);

        string pidA = ProfessionM3TestKit.PersonIdOf(personA);
        string pidB = ProfessionM3TestKit.PersonIdOf(personB);
        Assert.AreNotEqual(pidA, pidB, "两个具体人物必须拥有不同 PersonId");

        ProfessionProgress progressA = ProfessionM3TestKit.NewHunterProgress();
        ProfessionProgress progressB = ProfessionM3TestKit.NewHunterProgress();
        ProfessionM3TestKit.Set(personA, "ActiveProfessionProgress", progressA);
        ProfessionM3TestKit.Set(personB, "ActiveProfessionProgress", progressB);

        // 修改 A：Level / Exp / UnlockedPool / TraitLevel
        progressA.ProfessionLevel = 4;
        progressA.ProfessionExp = 220;
        progressA.UnlockedPoolCardIds.Add("card_hunter_extra");
        progressA.UnlockedPoolCardIds.Remove("card_a");
        progressA.ProfessionTraitLevel = 2;

        // B 完全不受影响
        ProfessionProgress heldB = ProfessionM3TestKit.ActiveProgress(personB);
        Assert.AreNotSame(progressA, heldB);
        Assert.AreNotSame(progressA.UnlockedPoolCardIds, heldB.UnlockedPoolCardIds);
        Assert.AreEqual(1, heldB.ProfessionLevel, "Person A 的 Level 成长不得影响同职业 Person B");
        Assert.AreEqual(0, heldB.ProfessionExp, "Person A 的 Exp 成长不得影响同职业 Person B");
        CollectionAssert.AreEqual(new[] { "card_a", "card_b" }, heldB.UnlockedPoolCardIds,
            "Person A 的 UnlockedPool 成长不得影响同职业 Person B");
        Assert.AreEqual(1, heldB.ProfessionTraitLevel, "Person A 的 TraitLevel 成长不得影响同职业 Person B");
    }

    [Test]
    public void PersonProgressMutation_DoesNotChangeProfessionDefinition()
    {
        // Test 4 — 模板独立性（宿主层）：改 Person Progress 不影响共享 ProfessionDefinition 的任何列表
        ProfessionDefinition definition = ProfessionM3TestKit.NewHunterDefinition();

        object person = ProfessionM3TestKit.NewCharacter("char_002", "猎人丙", 30, 6);
        ProfessionProgress progress = ProfessionM3TestKit.NewProgress(
            definition.ProfessionId, 1, 0,
            new List<string>(definition.InitialUnlockedCards), 1);
        ProfessionM3TestKit.Set(person, "ActiveProfessionProgress", progress);

        // Person 独立成长：加新牌、升 Lv / Exp / Trait
        progress.UnlockedPoolCardIds.Add("card_x");
        progress.UnlockedPoolCardIds.Remove("card_a");
        progress.ProfessionLevel = 3;
        progress.ProfessionExp = 90;
        progress.ProfessionTraitLevel = 2;

        // 模板仍保持共享静态内容
        Assert.AreEqual("profession_test_hunter", definition.ProfessionId);
        CollectionAssert.AreEqual(new[] { "card_a", "card_b" }, definition.InitialUnlockedCards,
            "Person 成长不得写回 ProfessionDefinition.InitialUnlockedCards");
        CollectionAssert.AreEqual(
            new[] { "card_a", "card_b", "card_c", "card_d" }, definition.FullCardPool,
            "Person 成长不得写回 ProfessionDefinition.FullCardPool");

        // 不共享 List 引用（防未来偷偷共享动态进度）
        Assert.AreNotSame(progress.UnlockedPoolCardIds, definition.InitialUnlockedCards);
        Assert.AreNotSame(progress.UnlockedPoolCardIds, definition.FullCardPool);
    }

    [Test]
    public void HeroInstance_HasNoProfessionProgressStructure()
    {
        // Test 9a — Hero 不进入 M3：HeroInstance 不得出现任何 Profession 结构成员
        System.Type heroType = ProfessionM3TestKit.Asm("HeroInstance");
        Assert.IsNotNull(heroType, "HeroInstance 应存在（Assembly-CSharp）");

        var members = heroType.GetMembers(BindingFlags.Public | BindingFlags.Instance)
            .Select(m => m.Name)
            .Where(n => n.IndexOf("Profession", System.StringComparison.Ordinal) >= 0)
            .ToList();

        CollectionAssert.IsEmpty(members,
            "HeroInstance 不得新增 Profession 相关结构（M3 只处理 Ordinary Character Person；Hero 专项未冻结，不得顺手迁移）");
    }

    [Test]
    public void HeroSaveData_HasNoProfessionField_WhileCharacterSaveDataDoes()
    {
        // Test 9b — Save 层同样 Hero Guard：
        //   HeroSaveData 不得出现 Profession 字段；CharacterSaveData 才有 M3 的 ActiveProfessionProgress。
        System.Type heroSaveType = ProfessionM3TestKit.Asm("HeroSaveData");
        System.Type characterSaveType = ProfessionM3TestKit.Asm("CharacterSaveData");
        Assert.IsNotNull(heroSaveType);
        Assert.IsNotNull(characterSaveType);

        var heroSaveFields = heroSaveType
            .GetFields(BindingFlags.Public | BindingFlags.Instance)
            .Select(f => f.Name)
            .Where(n => n.IndexOf("Profession", System.StringComparison.Ordinal) >= 0)
            .ToList();
        CollectionAssert.IsEmpty(heroSaveFields,
            "HeroSaveData 不得新增 Profession 存档字段（M3 Hero Guard）");

        Assert.IsNotNull(
            characterSaveType.GetField("ActiveProfessionProgress", BindingFlags.Public | BindingFlags.Instance),
            "CharacterSaveData 应包含 M3 的 ActiveProfessionProgress（普通 Person 存档）");
    }

    [Test]
    public void ClearActiveProfessionProgress_IsOnlyDataOperation()
    {
        // 最小数据操作能力：置空即清空 Active Profession（M3 不附带转职 / Dormant / Build 清空 / Formation）。
        // 本测试也守卫“置 null 后当前状态即无 Active Profession”（数据层面不残留）。
        object character = ProfessionM3TestKit.NewCharacter("char_001", "测试人物", 30, 6);
        ProfessionM3TestKit.Set(character, "ActiveProfessionProgress", ProfessionM3TestKit.NewHunterProgress());
        Assert.IsTrue(ProfessionM3TestKit.HasActiveProfession(character));

        ProfessionM3TestKit.Set(character, "ActiveProfessionProgress", null);

        Assert.IsFalse(ProfessionM3TestKit.HasActiveProfession(character));
        Assert.IsNull(ProfessionM3TestKit.ActiveProgress(character));
    }
}
