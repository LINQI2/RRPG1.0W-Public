using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

/// <summary>
/// M2 专项测试 8 — Person independence guard。
///
/// 目的：防止未来开发误解"职业共享池"——Person 的 UnlockedPool 是具体人物自己的动态成长状态，
/// 修改一个 Person 的 UnlockedPool 不得修改 ProfessionDefinition，也不得影响另一个 Person。
///
/// M2 不修改真实 CharacterInstance/HeroInstance，因此用最小 IPersonGrowth fake 验证
/// （surrogate 允许，且本测试不为实现目的把 ProfessionId 塞入正式人物实例）。
/// </summary>
public class ProfessionDefinitionPersonIndependenceGuardTests
{
    /// <summary>最小人物替身：只承载 Person 动态成长状态，与真实 Person 同构但不依赖 Assembly-CSharp。</summary>
    private sealed class PersonSurrogate : IPersonGrowth
    {
        public PersonSurrogate(string personId, string displayName)
        {
            PersonId = personId;
            DisplayName = displayName;
        }

        public string PersonId { get; }
        public string DisplayName { get; }

        public int Level { get; set; }
        public int Exp { get; set; }

        public List<StarterSlotEntry> StarterSlots { get; set; } = new List<StarterSlotEntry>();
        public List<string> UnlockedPoolCardIds { get; set; } = new List<string>();
        public string TraitId { get; set; }
        public int TraitLevel { get; set; }
    }

    /// <summary>
    /// M3 应遵守的播种契约：人物初始 UnlockedPool 从模板 InitialUnlockedCards 克隆，
    /// 绝不与模板列表共享引用（克隆后人物的动态成长才真正独立）。
    /// </summary>
    private static PersonSurrogate SeedPersonWithProfessionInitial(ProfessionDefinition def, string personId)
    {
        return new PersonSurrogate(personId, "测试人物")
        {
            UnlockedPoolCardIds = new List<string>(def.InitialUnlockedCards)
        };
    }

    [Test]
    public void MutatingOnePersonUnlockedPool_DoesNotChangeDefinition_OrOtherPerson()
    {
        // 共享同一职业定义的 A 与 B：成长历史必须独立
        var def = ProfessionM2TestKit.NewDefinitionA(); // Full=[a,b,c] Initial=[card_a]
        var registry = ProfessionDefinitionRegistry.Build(new[] { def });
        ProfessionDefinition sharedDefinition = registry.GetById("profession_test_a");

        PersonSurrogate personA = SeedPersonWithProfessionInitial(sharedDefinition, "person_A");
        PersonSurrogate personB = SeedPersonWithProfessionInitial(sharedDefinition, "person_B");

        CollectionAssert.AreEqual(new[] { "card_a" }, personA.UnlockedPoolCardIds);
        CollectionAssert.AreEqual(new[] { "card_a" }, personB.UnlockedPoolCardIds);

        // 修改 Person A 的 UnlockedPool（A 的动态成长前进）
        personA.UnlockedPoolCardIds.Add("card_extra_A");
        personA.UnlockedPoolCardIds.Add("card_b");

        // A 自己的状态变化
        CollectionAssert.AreEqual(new[] { "card_a", "card_extra_A", "card_b" }, personA.UnlockedPoolCardIds);

        // B 完全不受影响
        CollectionAssert.AreEqual(new[] { "card_a" }, personB.UnlockedPoolCardIds,
            "Person A 的 UnlockedPool 变化不得影响同职业的 Person B");

        // ProfessionDefinition 完全不受影响（模板仍是模板）
        CollectionAssert.AreEqual(new[] { "card_a" }, sharedDefinition.InitialUnlockedCards,
            "Person 动态解锁变化不得写回 ProfessionDefinition");
        CollectionAssert.AreEqual(new[] { "card_a", "card_b", "card_c" }, sharedDefinition.FullCardPool,
            "Person 动态解锁变化不得修改 Profession FullCardPool");

        // 不存在共享 List 引用（防止未来通过引用别名偷偷共享动态进度）
        Assert.AreNotSame(personA.UnlockedPoolCardIds, personB.UnlockedPoolCardIds);
        Assert.AreNotSame(personA.UnlockedPoolCardIds, sharedDefinition.InitialUnlockedCards);
        Assert.AreNotSame(personB.UnlockedPoolCardIds, sharedDefinition.InitialUnlockedCards);
    }

    [Test]
    public void ProfessionDefinition_StructurallyCarriesOnlyTemplateFields_NoPersonDynamicState()
    {
        // 结构/接口保证：ProfessionDefinition 只允许模板五字段（M6-B 授权扩展）：
        // ProfessionId / FullCardPool / InitialUnlockedCards / InitialStarterBuild / ProfessionTraitId，
        // 禁止任何 Person 动态状态字段（UnlockedPoolCardIds / StarterSlots / TraitLevel…）进入。
        var publicFields = typeof(ProfessionDefinition)
            .GetFields(BindingFlags.Public | BindingFlags.Instance)
            .Select(f => f.Name)
            .ToList();

        CollectionAssert.AreEquivalent(
            new[] { "ProfessionId", "FullCardPool", "InitialUnlockedCards", "InitialStarterBuild", "ProfessionTraitId" },
            publicFields,
            "ProfessionDefinition 只承载职业模板的五个字段（M6-B 扩展 InitialStarterBuild / ProfessionTraitId）；" +
            "UnlockedPoolCardIds / StarterSlots / Trait 等级等人物动态状态必须留在 Person 自身");

        Assert.IsFalse(typeof(IPersonGrowth).IsAssignableFrom(typeof(ProfessionDefinition)),
            "ProfessionDefinition 是共享职业模板，不得实现 IPersonGrowth（那是具体人物的动态成长接口）");
    }

    [Test]
    public void RegistryLookup_ReturnsStableSharedDefinition_AcrossTwoPersons()
    {
        // 同一 Registry / 同一 ProfessionId 稳定返回同一份模板定义（供多人共享），
        // 而动态 UnlockedPool 仍按 Person 独立保存于各自实例。
        var registry = ProfessionDefinitionRegistry.Build(new[] { ProfessionM2TestKit.NewDefinitionA() });

        var p1 = registry.GetById("profession_test_a");
        var p2 = registry.GetById("profession_test_a");

        Assert.AreSame(p1, p2, "同一 ProfessionId 必须稳定返回同一份定义（共享模板）");

        // 两个 Person 各自从模板克隆出独立的初始池
        PersonSurrogate a = SeedPersonWithProfessionInitial(p1, "person_a");
        PersonSurrogate b = SeedPersonWithProfessionInitial(p2, "person_b");
        Assert.AreNotSame(a.UnlockedPoolCardIds, b.UnlockedPoolCardIds,
            "两个 Person 的 UnlockedPool 不得共享同一 List 引用");
    }
}
