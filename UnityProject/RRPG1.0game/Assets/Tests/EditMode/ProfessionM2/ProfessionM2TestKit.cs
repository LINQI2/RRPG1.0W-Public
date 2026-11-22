using System.Collections.Generic;
using System.Linq;

/// <summary>
/// M2 Profession Template 专项测试共享辅助。
/// 只构造 Test / Not Frozen 内容（profession_test_* 与 card_*），
/// 不设计正式职业生态，不把测试内容写回 GameSpec。
/// M6-B：模板扩展为五字段（ProfessionId / FullCardPool / InitialUnlockedCards /
///       InitialStarterBuild / ProfessionTraitId），helper 提供合法默认值。
/// </summary>
public static class ProfessionM2TestKit
{
    /// <summary>构造测试用 ProfessionDefinition（Test / Not Frozen）。</summary>
    public static ProfessionDefinition NewDefinition(
        string professionId,
        IEnumerable<string> fullCardPool,
        IEnumerable<string> initialUnlockedCards,
        string professionTraitId = "trait_profession_test",
        IEnumerable<StarterSlotEntry> initialStarterBuild = null)
    {
        return new ProfessionDefinition
        {
            ProfessionId = professionId,
            FullCardPool = fullCardPool == null ? new List<string>() : fullCardPool.ToList(),
            InitialUnlockedCards = initialUnlockedCards == null ? new List<string>() : initialUnlockedCards.ToList(),
            ProfessionTraitId = professionTraitId,
            InitialStarterBuild = initialStarterBuild == null
                ? new List<StarterSlotEntry>()
                : initialStarterBuild.ToList()
        };
    }

    public static ProfessionDefinition NewDefinitionA()
    {
        return NewDefinition("profession_test_a",
            new[] { "card_a", "card_b", "card_c" }, new[] { "card_a" },
            "trait_profession_a", new[] { new StarterSlotEntry("card_a", 1) });
    }
}
