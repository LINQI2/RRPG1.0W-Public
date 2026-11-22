using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Journey Event Illustration Binding V0.1｜唯一 Catalog 资产的**幂等**创建 / 检查工具。
///
/// 为什么需要它：
///   - 制作人的配置入口必须**唯一且确定存在**（`Assets/Resources/ScriptableObjects/Presentation/
///     JourneyEventPresentationCatalog.asset`），不能要求每个人手工 new 一个 ScriptableObject，
///     也不能把同一张表复制到多个 Scene / Prefab；
///   - 本工具只负责「资产不存在时创建空表」；**绝不覆盖 / 不删除 / 不改写**制作人已经填好的 Entries
///     （已存在 → 只输出路径，不 SetDirty、不写盘）。
///
/// 与仓库既有约定一致：正式 SO 路径 = `Assets/Resources/ScriptableObjects/`；
/// 参考 `GrowthDataInitializer` 的「编辑器加载时幂等生成」模式
/// （避免在 Test Runner 等特殊上下文中生成导致 `m_Script` 引用不稳定）。
///
/// 边界：本工具不做 Excel 导入、不建立 EventId / Encounter Pool / Event Definition，
/// 也不写入任何 Title / Body / Options（Catalog 不是 Event Content Authority）。
/// </summary>
public static class JourneyEventPresentationCatalogInitializer
{
    /// <summary>唯一 Catalog 资产路径（必须与 JourneyEventPresentationCatalog.ResourcePath 对应）。</summary>
    public const string CatalogAssetPath =
        "Assets/Resources/ScriptableObjects/Presentation/JourneyEventPresentationCatalog.asset";

    [InitializeOnLoadMethod]
    private static void EnsureCatalogOnLoad()
    {
        EditorApplication.delayCall += EnsureCatalogAsset;
    }

    /// <summary>幂等创建 / 检查唯一 Catalog（菜单入口；也可由编辑器加载自动执行）。</summary>
    [MenuItem("Tools/Journey Event Presentation Catalog（创建/检查）")]
    public static void EnsureCatalogAsset()
    {
        var existing = AssetDatabase.LoadAssetAtPath<JourneyEventPresentationCatalog>(CatalogAssetPath);
        if (existing != null)
        {
            // 已存在：只报告，不触碰制作人配置（不自动删除 / 不改写 Entries）。
            Debug.Log("[JourneyEventIllustration] Catalog 已存在（" + existing.EntryCount + " 条）：" + CatalogAssetPath);
            return;
        }

        string dir = Path.GetDirectoryName(CatalogAssetPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);

        var asset = ScriptableObject.CreateInstance<JourneyEventPresentationCatalog>();
        AssetDatabase.CreateAsset(asset, CatalogAssetPath);
        EditorUtility.SetDirty(asset);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

        var reloaded = AssetDatabase.LoadAssetAtPath<JourneyEventPresentationCatalog>(CatalogAssetPath);
        Debug.Log("[JourneyEventIllustration] Catalog 已创建（0 条；制作人在此 Entries +1 配置逐事件插图）："
            + CatalogAssetPath + "（reloaded=" + (reloaded != null) + "）");
    }
}
