using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 人物成长配置加载（数据驱动优先：Resources 资产 → 运行时测试兜底）。
/// 正式曲线/表/Trait 以 Resources/ScriptableObjects/Growth 下资产为准；
/// 资产缺失时返回明确标记的测试默认实例（演示/测试值，未冻结）。
/// </summary>
public static class GrowthConfigProvider
{
    public const string CurveAssetPath = "ScriptableObjects/Growth/TestGrowthCurve";
    public const string TableAssetPath = "ScriptableObjects/Growth/TestGrowthTable";
    public const string TraitCatalogAssetPath = "ScriptableObjects/Growth/TestTraitCatalog";

    public static GrowthCurveConfig Curve { get; private set; }
    public static GrowthTableConfig Table { get; private set; }
    public static TraitCatalog TraitCatalog { get; private set; }

    public static void EnsureLoaded()
    {
        if (Curve != null && Table != null && TraitCatalog != null) return;

        Curve = Resources.Load<GrowthCurveConfig>(CurveAssetPath);
        if (Curve == null)
        {
            // 资产缺失时的演示兜底（明确为测试值）
            Curve = ScriptableObject.CreateInstance<GrowthCurveConfig>();
            Curve.MaxLevel = 10;
            Curve.ProficiencyCap = 5;
            Curve.ExpRequiredToLevel = new[] { 10, 15, 20, 25, 30, 40, 50, 60, 80 };
        }

        Table = Resources.Load<GrowthTableConfig>(TableAssetPath);
        if (Table == null)
        {
            // 资产缺失时为空表：不硬编码 Lv2/Lv5/Lv10，升级不产生节点（仅提供等级）
            Table = ScriptableObject.CreateInstance<GrowthTableConfig>();
            Table.TableId = "empty_fallback";
            Table.Nodes = new List<GrowthNodeConfig>();
        }

        TraitCatalog = Resources.Load<TraitCatalog>(TraitCatalogAssetPath);
        if (TraitCatalog == null)
            TraitCatalog = ScriptableObject.CreateInstance<TraitCatalog>();
    }
}
