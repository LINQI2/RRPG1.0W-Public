using System.Collections.Generic;

/// <summary>
/// M6 纯逻辑测试替身：实现 IFormationHost 的最小普通 Person 宿主。
/// 只用于 Formation Resolver / Committer 的纯逻辑单测（不依赖 Assembly-CSharp）；
/// 真实宿主（CharacterInstance）的语义由 ProfessionM6 的宿主 / 存档 / Village 集成测试覆盖。
/// </summary>
public sealed class M6TestFormationHost : IFormationHost
{
    public M6TestFormationHost(string personId)
    {
        PersonId = personId;
    }

    public string PersonId { get; }

    public bool HasActiveProfession => ActiveProfessionProgress != null;

    public FormationState FormationState { get; set; } = new FormationState();

    public PersonLifeHistory LifeHistory { get; set; } = new PersonLifeHistory();

    public List<StarterSlotEntry> StarterSlots { get; set; } = new List<StarterSlotEntry>();

    public ProfessionProgress ActiveProfessionProgress { get; set; }
}
