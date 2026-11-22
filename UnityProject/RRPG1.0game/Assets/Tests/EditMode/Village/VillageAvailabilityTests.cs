using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

/// <summary>
/// Person Availability 纯逻辑测试（Village.Runtime 强类型）。
/// 覆盖：在村 / 远征 / 暂不可用（死亡）；远征中人物绝不同时 InVillage；不创建第二套 Person 数据。
/// </summary>
public class VillageAvailabilityTests
{
    private static VillagePersonInput Person(string id, string name, bool dead = false)
    {
        return new VillagePersonInput { PersonId = id, DisplayName = name, IsDead = dead };
    }

    [Test]
    public void Resolve_Distinguishes_InVillage_Expedition_Unavailable()
    {
        var persons = new List<VillagePersonInput>
        {
            Person("char_001", "战士"),
            Person("char_002", "猎人"),
            Person("hero_001", "青鹿"),
            Person("char_003", "死者", dead: true)
        };

        var views = VillagePersonAvailability.Resolve(persons, new[] { "char_002", "hero_001" });

        Assert.AreEqual(4, views.Count);
        Assert.AreEqual(VillageAvailability.InVillage, views.Find(v => v.PersonId == "char_001").Availability);
        Assert.AreEqual(VillageAvailability.Expedition, views.Find(v => v.PersonId == "char_002").Availability);
        Assert.AreEqual(VillageAvailability.Expedition, views.Find(v => v.PersonId == "hero_001").Availability);
        Assert.AreEqual(VillageAvailability.Unavailable, views.Find(v => v.PersonId == "char_003").Availability);
    }

    [Test]
    public void ExpeditionPerson_IsNeverAlso_InVillage()
    {
        var persons = new List<VillagePersonInput>
        {
            Person("char_001", "战士"),
            Person("char_002", "猎人"),
            Person("hero_001", "青鹿")
        };

        var views = VillagePersonAvailability.Resolve(persons, new[] { "char_001", "hero_001" });

        foreach (var v in views)
        {
            Assert.IsFalse(
                v.Availability == VillageAvailability.InVillage &&
                persons.Exists(p => p.PersonId == v.PersonId &&
                                    (new[] { "char_001", "hero_001" }).Contains(v.PersonId)),
                "远征集合中的人物不得同时显示为在村可用");
        }

        Assert.AreEqual(VillageAvailability.Expedition, views.Find(v => v.PersonId == "char_001").Availability);
        Assert.AreEqual(VillageAvailability.Expedition, views.Find(v => v.PersonId == "hero_001").Availability);
        Assert.AreEqual(VillageAvailability.InVillage, views.Find(v => v.PersonId == "char_002").Availability);
    }

    [Test]
    public void Resolve_EmptyOrNull_Safe()
    {
        Assert.AreEqual(0, VillagePersonAvailability.Resolve(null, null).Count);
        Assert.AreEqual(0, VillagePersonAvailability.Resolve(new List<VillagePersonInput>(), null).Count);
    }

    [Test]
    public void Resolve_DuplicateInputId_ProducesOneViewPerEntry_NoSecondTruthSource()
    {
        // 同一 PersonId 在输入中出现两次（不应发生）时，也不应复制为两套数据；只做映射。
        var persons = new List<VillagePersonInput>
        {
            Person("char_001", "战士"),
            Person("char_001", "战士")
        };
        var views = VillagePersonAvailability.Resolve(persons, null);
        Assert.AreEqual(2, views.Count, "视图只是逐条映射，不自动去重——长期真相去重由 PlayerCollection 保证");
    }
}
