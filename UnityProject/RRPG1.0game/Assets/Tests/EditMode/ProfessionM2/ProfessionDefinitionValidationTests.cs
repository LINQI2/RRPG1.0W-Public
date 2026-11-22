using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

/// <summary>
/// M2 专项测试 1–6：ProfessionDefinition 结构与模板内部合法性验证。
/// M6-B：模板扩展为五字段（含 InitialStarterBuild / ProfessionTraitId），本文件同步覆盖新合法性规则。
/// 内容全部为 Test / Not Frozen。
/// </summary>
public class ProfessionDefinitionValidationTests
{
    [Test]
    public void ValidDefinition_ProfessionIdFullPoolInitial_CorrectlyReadable()
    {
        // Test 1 — Definition 基础：profession_test_a / [card_a,card_b,card_c] / [card_a]
        var def = ProfessionM2TestKit.NewDefinitionA();

        Assert.IsTrue(ProfessionDefinitionValidator.TryValidate(def, out var errors),
            "合法定义应通过校验。错误: " + string.Join("; ", errors));

        var registry = ProfessionDefinitionRegistry.Build(new[] { def });

        Assert.IsTrue(registry.TryGetById("profession_test_a", out var found), "合法 ProfessionId 应能查回");
        Assert.IsNotNull(found);
        Assert.AreEqual("profession_test_a", found.ProfessionId);
        CollectionAssert.AreEqual(new[] { "card_a", "card_b", "card_c" }, found.FullCardPool);
        CollectionAssert.AreEqual(new[] { "card_a" }, found.InitialUnlockedCards);
        Assert.AreEqual("trait_profession_a", found.ProfessionTraitId, "M6-B：模板应可读固定初始 Profession Trait 定义 ID");
        Assert.AreEqual(1, found.InitialStarterBuild.Count, "M6-B：模板应携带固定 InitialStarterBuild");
        Assert.AreEqual("card_a", found.InitialStarterBuild[0].CardId);
        Assert.AreEqual(1, registry.Count);
    }

    [Test]
    public void InitialNotSubsetOfFull_IsRejected()
    {
        // Test 2 — 非法：Full=[card_a]，Initial=[card_b]（Initial ⊄ Full）必须被拒绝
        var def = ProfessionM2TestKit.NewDefinition("profession_test_b", new[] { "card_a" }, new[] { "card_b" });

        Assert.IsFalse(ProfessionDefinitionValidator.TryValidate(def, out var errors),
            "Initial 不在 Full 中的定义必须校验失败");
        Assert.That(errors.Any(e => e.Contains("不在 FullCardPool")), Is.True,
            "错误信息应明确指出子集违规。错误: " + string.Join("; ", errors));

        var ex = Assert.Throws<ProfessionDefinitionValidationException>(
            () => ProfessionDefinitionRegistry.Build(new[] { def }));
        Assert.IsTrue(ex.Errors.Count > 0, "Registry 构造失败必须携带错误明细");
    }

    [Test]
    public void FullCardPoolDuplicate_IsRejected()
    {
        // Test 3 — FullCardPool duplicate [card_a,card_a] 不得静默接受
        var def = ProfessionM2TestKit.NewDefinition("profession_test_c", new[] { "card_a", "card_a" }, new[] { "card_a" });

        Assert.IsFalse(ProfessionDefinitionValidator.TryValidate(def, out var errors));
        Assert.That(errors.Any(e => e.Contains("FullCardPool") && e.Contains("重复")), Is.True,
            "错误信息应指出 FullCardPool 重复。错误: " + string.Join("; ", errors));

        Assert.Throws<ProfessionDefinitionValidationException>(
            () => ProfessionDefinitionRegistry.Build(new[] { def }));
    }

    [Test]
    public void InitialUnlockedDuplicate_IsRejected()
    {
        // Test 4 — InitialUnlockedCards duplicate 不得静默接受
        var def = ProfessionM2TestKit.NewDefinition("profession_test_d", new[] { "card_a", "card_b" }, new[] { "card_a", "card_a" });

        Assert.IsFalse(ProfessionDefinitionValidator.TryValidate(def, out var errors));
        Assert.That(errors.Any(e => e.Contains("InitialUnlockedCards") && e.Contains("重复")), Is.True,
            "错误信息应指出 InitialUnlockedCards 重复。错误: " + string.Join("; ", errors));

        Assert.Throws<ProfessionDefinitionValidationException>(
            () => ProfessionDefinitionRegistry.Build(new[] { def }));
    }

    [Test]
    public void EmptyProfessionId_IsRejected()
    {
        // Test 5a — 空 ProfessionId 必须识别为非法
        var def = ProfessionM2TestKit.NewDefinition(string.Empty, new[] { "card_a" }, new[] { "card_a" });

        Assert.IsFalse(ProfessionDefinitionValidator.TryValidate(def, out var errors));
        Assert.That(errors.Any(e => e.Contains("ProfessionId")), Is.True,
            "错误信息应指出 ProfessionId 为空。错误: " + string.Join("; ", errors));
    }

    [Test]
    public void EmptyCardIdInFullCardPool_IsRejected()
    {
        // Test 5b — FullCardPool 含空 CardId 必须识别为非法
        var def = ProfessionM2TestKit.NewDefinition("profession_test_e", new[] { "card_a", "" }, new[] { "card_a" });

        Assert.IsFalse(ProfessionDefinitionValidator.TryValidate(def, out var errors));
        Assert.That(errors.Any(e => e.Contains("FullCardPool") && e.Contains("null/empty")), Is.True,
            "错误信息应指出 FullCardPool 含空 CardId。错误: " + string.Join("; ", errors));
    }

    [Test]
    public void EmptyCardIdInInitialUnlocked_IsRejected()
    {
        // Test 5c — InitialUnlockedCards 含空 CardId 必须识别为非法
        var def = ProfessionM2TestKit.NewDefinition("profession_test_f", new[] { "card_a", "card_b" }, new[] { "card_a", null });

        Assert.IsFalse(ProfessionDefinitionValidator.TryValidate(def, out var errors));
        Assert.That(errors.Any(e => e.Contains("InitialUnlockedCards") && e.Contains("null/empty")), Is.True,
            "错误信息应指出 InitialUnlockedCards 含空 CardId。错误: " + string.Join("; ", errors));
    }

    [Test]
    public void NullList_IsExplicitlyRejected_NotSilentlyTreatedAsValid()
    {
        // 防御：null 集合引用（绕过 TestKit 的规范化，直接构造）不得被当作正式合法数据继续使用
        var def = new ProfessionDefinition
        {
            ProfessionId = "profession_test_g",
            FullCardPool = null,
            InitialUnlockedCards = null,
            ProfessionTraitId = "trait_profession_g"
        };

        Assert.IsFalse(ProfessionDefinitionValidator.TryValidate(def, out var errors),
            "null 集合引用的定义必须校验失败");
        Assert.That(errors.Any(e => e.Contains("FullCardPool") && e.Contains("null")), Is.True,
            "错误信息应指出 FullCardPool 为 null。错误: " + string.Join("; ", errors));

        Assert.Throws<ProfessionDefinitionValidationException>(
            () => ProfessionDefinitionRegistry.Build(new[] { def }));
    }
}


