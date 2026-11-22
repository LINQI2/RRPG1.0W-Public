using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// M2 专项测试 6–7：按 ProfessionId 的最小查询能力 + duplicate ProfessionId 显式失败。
/// 内容全部为 Test / Not Frozen。
/// </summary>
public class ProfessionDefinitionCatalogLookupTests
{
    [Test]
    public void GetById_ValidProfessionId_ReturnsCorrectDefinition()
    {
        // Test 6a — 合法 ProfessionId → 返回正确定义
        var registry = ProfessionDefinitionRegistry.Build(new[] { ProfessionM2TestKit.NewDefinitionA() });

        ProfessionDefinition def = registry.GetById("profession_test_a");

        Assert.IsNotNull(def);
        Assert.AreEqual("profession_test_a", def.ProfessionId);
        CollectionAssert.AreEqual(new[] { "card_a", "card_b", "card_c" }, def.FullCardPool);
        CollectionAssert.AreEqual(new[] { "card_a" }, def.InitialUnlockedCards);
    }

    [Test]
    public void MissingProfessionId_IsExplicitFailure()
    {
        // Test 6b — 不存在 ProfessionId → 明确失败（不得返回 null 冒充成功）
        var registry = ProfessionDefinitionRegistry.Build(new[] { ProfessionM2TestKit.NewDefinitionA() });

        Assert.Throws<KeyNotFoundException>(() => registry.GetById("profession_test_nonexistent"),
            "GetById 对不存在 ID 必须抛 KeyNotFoundException");
        Assert.IsFalse(registry.TryGetById("profession_test_nonexistent", out var missing),
            "TryGetById 对不存在 ID 必须返回 false");
        Assert.IsNull(missing);
    }

    [Test]
    public void NullOrEmptyProfessionId_IsExplicitFailure()
    {
        var registry = ProfessionDefinitionRegistry.Build(new[] { ProfessionM2TestKit.NewDefinitionA() });

        Assert.Throws<ArgumentException>(() => registry.GetById(null), "GetById(null) 必须失败");
        Assert.Throws<ArgumentException>(() => registry.GetById(string.Empty), "GetById(empty) 必须失败");
        Assert.IsFalse(registry.TryGetById(null, out _));
        Assert.IsFalse(registry.TryGetById(string.Empty, out _));
    }

    [Test]
    public void Catalog_BuildRegistry_ProvidesStableLookup()
    {
        // 验证 authoring 容器（ScriptableObject Catalog）→ Registry → GetById 的配置路径可用
        var catalog = ScriptableObject.CreateInstance<ProfessionDefinitionCatalog>();
        catalog.Definitions.Add(ProfessionM2TestKit.NewDefinitionA());

        ProfessionDefinitionRegistry registry = catalog.BuildRegistry();

        Assert.IsTrue(registry.TryGetById("profession_test_a", out var def));
        Assert.IsNotNull(def);
        Assert.AreEqual("profession_test_a", def.ProfessionId);
    }

    [Test]
    public void DuplicateProfessionId_NeverReturnsArbitraryFirst()
    {
        // Test 7 — 两个定义使用同一 ProfessionId → 必须显式失败，不得返回任意第一个
        var def1 = ProfessionM2TestKit.NewDefinition("profession_test_dup", new[] { "card_a" }, new[] { "card_a" });
        var def2 = ProfessionM2TestKit.NewDefinition("profession_test_dup", new[] { "card_b" }, new[] { "card_b" });

        Assert.IsFalse(ProfessionDefinitionValidator.TryValidateAll(new[] { def1, def2 }, out var errors),
            "重复 ProfessionId 的集合必须校验失败");
        Assert.That(System.Linq.Enumerable.Any(errors, e => e.Contains("重复 ProfessionId")), Is.True,
            "错误信息应指出重复 ProfessionId。错误: " + string.Join("; ", errors));

        // Registry 构造直接失败：调用方永远拿不到"任意第一个"
        var ex = Assert.Throws<ProfessionDefinitionValidationException>(
            () => ProfessionDefinitionRegistry.Build(new[] { def1, def2 }));
        Assert.IsTrue(ex.Errors.Count > 0, "构造失败必须携带错误明细");

        // Catalog 路径同样失败
        var catalog = ScriptableObject.CreateInstance<ProfessionDefinitionCatalog>();
        catalog.Definitions.Add(def1);
        catalog.Definitions.Add(def2);
        Assert.Throws<ProfessionDefinitionValidationException>(() => catalog.BuildRegistry());
    }
}
