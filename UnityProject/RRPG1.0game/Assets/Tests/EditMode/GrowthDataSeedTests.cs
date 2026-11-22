using System.Reflection;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// 人物长期成长测试数据生成（EditMode 种子测试）。
/// 调用 Editor 工具 GrowthDataInitializer 生成演示/测试资产（数据驱动，不在代码硬编码 Lv2/Lv5/Lv10）。
/// 幂等：重复运行会重新生成同一批测试资产。
/// </summary>
public class GrowthDataSeedTests
{
    [Test]
    public void SeedTestGrowthAssets_ExistAndLoadable()
    {
        var type = System.Type.GetType("GrowthDataInitializer, Assembly-CSharp-Editor");
        Assert.IsNotNull(type, "未找到 GrowthDataInitializer（Editor 程序集）");

        var method = type.GetMethod("CreateTestData", BindingFlags.Public | BindingFlags.Static);
        Assert.IsNotNull(method, "未找到 GrowthDataInitializer.CreateTestData");
        method.Invoke(null, null);

        // 资产加载校验（数据驱动：曲线 / 成长表 / Trait 目录）
        var curve = Resources.Load<GrowthCurveConfig>("ScriptableObjects/Growth/TestGrowthCurve");
        var table = Resources.Load<GrowthTableConfig>("ScriptableObjects/Growth/TestGrowthTable");
        var catalog = Resources.Load<TraitCatalog>("ScriptableObjects/Growth/TestTraitCatalog");

        Assert.IsNotNull(curve, "经验曲线资产缺失");
        Assert.IsNotNull(table, "成长表资产缺失");
        Assert.IsNotNull(catalog, "Trait 目录资产缺失");

        Assert.IsTrue(curve.ExpRequiredToLevel != null && curve.ExpRequiredToLevel.Length > 0, "曲线应含逐级经验");
        Assert.IsTrue(curve.MaxLevel > 1, "曲线应允许升级");
        Assert.IsTrue(table.Nodes.Count >= 4, "测试成长表应包含至少 4 个节点（四种节点类型各一）");

        // 四种节点类型的数据驱动映射（等级 → 节点）
        Assert.IsNotNull(table.GetNodeForLevel(2), "Lv2 应配置 CardGrowth 节点");
        Assert.IsNotNull(table.GetNodeForLevel(3), "Lv3 应配置 StarterSlotUnlock 节点");
        Assert.IsNotNull(table.GetNodeForLevel(4), "Lv4 应配置 TraitUpgrade 节点");
        Assert.IsNotNull(table.GetNodeForLevel(5), "Lv5 应配置 UnlockedPoolExpand 节点");

        Assert.IsTrue(catalog.DefaultTraits.Count > 0, "Trait 目录应包含默认 Trait");
    }
}
