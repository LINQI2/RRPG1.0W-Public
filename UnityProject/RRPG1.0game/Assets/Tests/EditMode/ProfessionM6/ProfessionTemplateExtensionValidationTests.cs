using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

/// <summary>
/// M6-B 专项 — ProfessionDefinition 扩展字段（InitialStarterBuild / ProfessionTraitId）的
/// Validator 合法性规则。内容全部为 Test / Not Frozen。
/// </summary>
public class ProfessionTemplateExtensionValidationTests
{
    [Test]
    public void MissingProfessionTraitId_IsRejected()
    {
        var def = new ProfessionDefinition
        {
            ProfessionId = "profession_test_m6_trait",
            FullCardPool = new List<string> { "card_a" },
            InitialUnlockedCards = new List<string> { "card_a" },
            ProfessionTraitId = null,
            InitialStarterBuild = new List<StarterSlotEntry>()
        };

        Assert.IsFalse(ProfessionDefinitionValidator.TryValidate(def, out var errors));
        Assert.That(errors.Any(e => e.Contains("ProfessionTraitId")), Is.True,
            "错误信息应指出 ProfessionTraitId 缺失。错误: " + string.Join("; ", errors));

        Assert.Throws<ProfessionDefinitionValidationException>(
            () => ProfessionDefinitionRegistry.Build(new[] { def }));
    }

    [Test]
    public void NullInitialStarterBuild_IsRejected()
    {
        var def = new ProfessionDefinition
        {
            ProfessionId = "profession_test_m6_build",
            FullCardPool = new List<string> { "card_a" },
            InitialUnlockedCards = new List<string> { "card_a" },
            ProfessionTraitId = "trait_profession_build",
            InitialStarterBuild = null
        };

        Assert.IsFalse(ProfessionDefinitionValidator.TryValidate(def, out var errors));
        Assert.That(errors.Any(e => e.Contains("InitialStarterBuild") && e.Contains("null")), Is.True,
            "错误信息应指出 InitialStarterBuild 为 null。错误: " + string.Join("; ", errors));
    }

    [Test]
    public void EmptyCardIdOrNegativeProficiency_InStarterBuild_IsRejected()
    {
        var def = new ProfessionDefinition
        {
            ProfessionId = "profession_test_m6_build2",
            FullCardPool = new List<string> { "card_a" },
            InitialUnlockedCards = new List<string> { "card_a" },
            ProfessionTraitId = "trait_profession_build2",
            InitialStarterBuild = new List<StarterSlotEntry>
            {
                new StarterSlotEntry("", 1),
                new StarterSlotEntry("card_a", -2)
            }
        };

        Assert.IsFalse(ProfessionDefinitionValidator.TryValidate(def, out var errors));
        Assert.That(errors.Any(e => e.Contains("InitialStarterBuild") && e.Contains("CardId")), Is.True,
            "应指出空 CardId。错误: " + string.Join("; ", errors));
        Assert.That(errors.Any(e => e.Contains("InitialStarterBuild") && e.Contains("Proficiency")), Is.True,
            "应指出负 Proficiency。错误: " + string.Join("; ", errors));
    }
}
