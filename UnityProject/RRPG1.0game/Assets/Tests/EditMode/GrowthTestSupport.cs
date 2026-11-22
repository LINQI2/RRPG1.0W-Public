using System.Collections.Generic;

/// <summary>
/// 纯逻辑测试用 IPersonGrowth 实现（不依赖 Assembly-CSharp 的 CharacterInstance/HeroInstance，
/// 使 EditMode 测试可直接引用 Growth.Runtime 纯逻辑）。
/// 替身按 Hero Legacy 语义建模：不实现 IGrowthAuthority 时由 GrowthAuthority.Resolve 包装为
/// Legacy 权威（IsAvailable=true、IsProfessionGrowth=false）。
/// </summary>
public class TestPerson : IPersonGrowth
{
    public string PersonId { get; }
    public string DisplayName { get; }
    public int Level { get; set; } = 1;
    public int Exp { get; set; }
    public List<StarterSlotEntry> StarterSlots { get; set; } = new List<StarterSlotEntry>();
    public List<string> UnlockedPoolCardIds { get; set; } = new List<string>();
    public string TraitId { get; set; }
    public int TraitLevel { get; set; }

    public TestPerson(string personId, string displayName = "测试人物")
    {
        PersonId = personId;
        DisplayName = displayName;
    }
}
