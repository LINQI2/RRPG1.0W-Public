using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// JourneyEventIllustrationResolver —— **统一事件卡插图解析入口**
///（Journey Event Illustration Binding V0.1｜Implementation-only / Not Frozen）。
///
/// 解析链（硬约束）：
///
///   PresentationKey（V0.1 = 当前稳定 NodeId；Implementation-level 临时来源，
///                     未来可整体替换为 EventDefinitionId / EncounterDefinitionId）
///     → JourneyEventPresentationCatalog 配置行
///     → 类别级 fallback（Battle → battleEncounterIllustration；
///                        Content → contentEncounterIllustration；
///                        Branch → branchIllustration）
///     → null（卡面隐藏 Illustration Host；Title / Body / Options 继续正常工作）
///
/// 边界（硬约束）：
///   - 只读：不决定合法 Node、不写 Progression / Supply / elapsed / CurrentNode、不进入也不跳过 Battle；
///   - 不建立正式 EventId / Encounter Pool / Event Definition；
///   - 空 PresentationKey 不参与解析（不猜、不随机、不塌缩到其它键），并给出明确诊断；
///   - 未配置键 / 配置行无图 → 类别 fallback + 明确诊断（绝不静默随机取图）；
///   - 本类的数据形态（一张 Inspector 配置资产）是 V0.1 当前实现，
///     **不得**表述为已冻结的长期美术架构或 Event Content Authority。
/// </summary>
public static class JourneyEventIllustrationResolver
{
    private static readonly HashSet<string> _warnedKeys = new HashSet<string>();

    /// <summary>测试 / 诊断用：清空已输出过的去重 warning 记录。</summary>
    public static void ClearWarningsForTests()
    {
        _warnedKeys.Clear();
    }

    /// <summary>
    /// 逐事件插图解析：具体 PresentationKey → 类别级 fallback → null。
    /// 空键 / 未配置键 / 配置行无图 / 配置资产缺失都会给出**去重**诊断，并确定性地回退到类别图。
    /// </summary>
    public static JourneyEventIllustrationResult ResolveForEvent(string presentationKey,
        JourneyEncounterKind kind, Sprite categoryFallback,
        IList<JourneyEventPresentationEntry> entriesOverride = null)
    {
        if (string.IsNullOrEmpty(presentationKey))
        {
            WarnOnce("empty-key",
                "[JourneyEventIllustration] PresentationKey 为空：本次不参与逐事件插图解析"
                + "（不猜、不随机、不塌缩到其它键）→ 使用类别级 fallback 图（kind=" + kind + "）。"
                + "请检查传入的表现键来源（V0.1 = 稳定 NodeId）。");
            return CategoryFallback(kind, categoryFallback, null);
        }

        var entry = FindEntry(entriesOverride, presentationKey);
        if (entry == null)
        {
            var catalog = JourneyEventPresentationCatalog.Instance;
            if (catalog != null) entry = catalog.FindEntry(presentationKey);
            else WarnOnce("catalog-missing",
                "[JourneyEventIllustration] 未找到配置资产 Resources/" + JourneyEventPresentationCatalog.ResourcePath
                + ".asset → 全部事件卡使用类别级 fallback 图（逐事件插图不可用，游戏流程不受影响）。");
        }

        if (entry == null || entry.Illustration == null)
        {
            WarnOnce("unbound:" + presentationKey,
                "[JourneyEventIllustration] PresentationKey=" + presentationKey
                + (entry == null ? " 没有配置行" : " 的配置行未绑定 Illustration")
                + " → 使用类别级 fallback 图（kind=" + kind + "）。"
                + "请在 JourneyEventPresentationCatalog 补一行或拖入插图，不要依赖静默回退。");
            return CategoryFallback(kind, categoryFallback, presentationKey);
        }

        return new JourneyEventIllustrationResult
        {
            PresentationKey = presentationKey,
            Kind = kind,
            Illustration = entry.Illustration,
            UsedCategoryFallback = false,
            Source = "binding:" + presentationKey
        };
    }

    /// <summary>
    /// 类别级 fallback 解析（**无逐事件键**的表面）。
    ///
    /// Branch 当前是「路线选择表面」而不是正式 Event Content：没有稳定、唯一、合理的逐 Branch 表现键，
    /// 因此**不伪造** EventId，直接走类别图；未来 Branch 拥有正式 Event / Encounter Content 键后，
    /// 只需把调用点从本入口换成 `ResolveForEvent(key, …)`，Catalog 与 View 不需要重做。
    /// </summary>
    public static JourneyEventIllustrationResult ResolveCategoryFallback(
        JourneyEncounterKind kind, Sprite categoryFallback)
    {
        return CategoryFallback(kind, categoryFallback, null);
    }

    // ==================== 内部 ====================

    private static JourneyEventIllustrationResult CategoryFallback(
        JourneyEncounterKind kind, Sprite categoryFallback, string presentationKey)
    {
        if (categoryFallback == null)
        {
            WarnOnce("category-missing:" + kind,
                "[JourneyEventIllustration] 类别 " + kind
                + " 既没有具体 PresentationKey 的插图，也没有类别级 fallback 图"
                + " → 卡面隐藏 Illustration Host（Title / Body / Options 继续正常工作）。");
        }

        return new JourneyEventIllustrationResult
        {
            PresentationKey = presentationKey,
            Kind = kind,
            Illustration = categoryFallback,
            UsedCategoryFallback = true,
            Source = categoryFallback != null ? "category:" + kind : "none:" + kind
        };
    }

    private static JourneyEventPresentationEntry FindEntry(
        IList<JourneyEventPresentationEntry> entries, string presentationKey)
    {
        if (entries == null || string.IsNullOrEmpty(presentationKey)) return null;
        for (int i = 0; i < entries.Count; i++)
        {
            var e = entries[i];
            if (e == null || string.IsNullOrEmpty(e.PresentationKey)) continue;
            if (e.PresentationKey == presentationKey) return e;
        }
        return null;
    }

    private static void WarnOnce(string key, string message)
    {
        if (_warnedKeys.Contains(key)) return;
        _warnedKeys.Add(key);
        Debug.LogWarning(message);
    }
}
