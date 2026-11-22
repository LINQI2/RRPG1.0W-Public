using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// JourneyPersonVisualEntry —— WP-E 遗留条目类型（**Compatibility / Test seam only**）。
///
/// 重要（Person Visual Binding V0.1 迁移后）：
///   - Journey 的**正式配置入口**已统一到 `PersonVisualBindingCatalog`（Scene / Prefab 不再序列化任何人物质表）；
///   - 本类型只保留给既有 WP-E 契约测试作**显式覆盖参数**，不是制作人需要维护的第二个配置入口；
///   - 语义未变：AppearanceKey 只回答「用哪份外观」，不是人物身份，不承载 Animator / Spine / 换装 / 表情。
/// </summary>
[System.Serializable]
public class JourneyPersonVisualEntry
{
    /// <summary>外观配置候选键（PersonDefinitionId / CharacterId / HeroId）。不是 Person identity。</summary>
    public string AppearanceKey;

    /// <summary>身体 Sprite（测试 / 迁移覆盖用）。</summary>
    public Sprite Body;

    /// <summary>可选局部偏移（表现参数）。</summary>
    public Vector2 Offset = Vector2.zero;

    /// <summary>可选缩放（表现参数）。</summary>
    public float Scale = 1f;
}

/// <summary>
/// JourneyPersonVisualResult —— 一次 Journey 视觉解析结果（只读值对象；字段与 WP-E 保持一致）。
/// </summary>
public struct JourneyPersonVisualResult
{
    public string PersonId;
    public string DisplayName;
    public bool IsHero;
    public string AppearanceKey;
    public Sprite Body;
    public Vector2 Offset;
    public float Scale;
    public bool UsedFallback;
    public string Source;
}

/// <summary>
/// ExpeditionJourneyPersonVisualResolver —— **Compatibility adapter**（WP-E 冻结的是解析契约，不是数据架构）：
///
///   - 正式路径（entries 为 null / 空）：转交 `PersonVisualResolver.ResolveForPerson(..., PersonVisualSurface.Journey, ...)`，
///     视觉来自唯一配置面 `PersonVisualBindingCatalog`；
///   - 显式覆盖路径（entries 非空）：仅供既有 WP-E 契约测试使用，不是第二个正式配置入口。
///
///   PersonId（具体人物身份）
///     → Resolve exact runtime Person（CharacterInstance / HeroInstance）
///     → 读取该具体人物已有字段作为外观配置候选键（PersonDefinitionId / CharacterId / HeroId）
///     → 在统一 visual binding 中取 visual
///     → visual result 或 default player placeholder
///
/// 边界（硬约束）：
///   - 只读：不写 PlayerCollection / 不建第二套 Person Authority / 不碰 SaveData / SaveVersion；
///   - 模板键只用于外观查询，不回答「这个人是谁」；不按 DisplayName 查询身份；
///   - 找不到 Person 或 visual binding → default placeholder + 去重后的可诊断 warning；
///   - 数据形态（Inspector 配置资产）是 V0.1 当前实现，**不得**表述为已冻结长期美术架构。
/// </summary>
public static class ExpeditionJourneyPersonVisualResolver
{
    private static readonly HashSet<string> _warnedKeys = new HashSet<string>();

    /// <summary>测试 / 诊断用：清空已输出过的去重 warning 记录（同时清空统一解析器的记录）。</summary>
    public static void ClearWarningsForTests()
    {
        _warnedKeys.Clear();
        PersonVisualResolver.ClearWarningsForTests();
    }

    /// <summary>
    /// 由 PersonId 解析该具体人物的外观配置候选键。
    /// 返回 null = 找不到该 Person（调用方必须走 default placeholder，禁止猜模板）。
    /// </summary>
    public static string ResolveAppearanceKey(PlayerCollection collection, string personId)
    {
        return PersonVisualResolver.ResolveAppearanceKey(collection, personId);
    }

    /// <summary>
    /// 主解析入口：PersonId → exact Person → presentation visual（或 default placeholder）。
    /// 不修改任何输入对象。entries 为 null / 空 = 走统一配置面（正式产品路径）。
    /// </summary>
    public static JourneyPersonVisualResult Resolve(PlayerCollection collection, string personId,
        IList<JourneyPersonVisualEntry> entries, Sprite defaultBody)
    {
        if (entries == null || entries.Count == 0)
        {
            return FromUnified(PersonVisualResolver.ResolveForPerson(
                collection, personId, PersonVisualSurface.Journey, defaultBody));
        }

        return ResolveWithExplicitEntries(collection, personId, entries, defaultBody);
    }

    /// <summary>统一解析结果 → WP-E 结果类型（保持既有测试与调用方字段语义）。</summary>
    private static JourneyPersonVisualResult FromUnified(PersonVisualResult r)
    {
        return new JourneyPersonVisualResult
        {
            PersonId = r.PersonId,
            DisplayName = r.DisplayName,
            IsHero = r.IsHero,
            AppearanceKey = r.AppearanceKey,
            Body = r.Sprite,
            Offset = r.Offset,
            Scale = r.Scale,
            UsedFallback = r.UsedFallback,
            Source = r.Source
        };
    }

    /// <summary>显式覆盖路径（WP-E 遗留行为逐字保留；仅测试 / 迁移使用，不是正式配置入口）。</summary>
    private static JourneyPersonVisualResult ResolveWithExplicitEntries(PlayerCollection collection, string personId,
        IList<JourneyPersonVisualEntry> entries, Sprite defaultBody)
    {
        var result = new JourneyPersonVisualResult
        {
            PersonId = personId,
            Scale = 1f,
            Offset = Vector2.zero,
            Source = "unresolved"
        };

        if (collection == null || string.IsNullOrEmpty(personId))
        {
            WarnOnce("person-unresolved",
                "[JourneyVisual] 无法解析人物视觉：PlayerCollection 或 PersonId 为空 → 使用 default placeholder。");
            return ApplyFallback(result, defaultBody, "person-unresolved");
        }

        var character = collection.GetCharacterByPersonId(personId);
        if (character != null)
        {
            result.DisplayName = character.DisplayName;
            result.IsHero = false;
            result.AppearanceKey = !string.IsNullOrEmpty(character.PersonDefinitionId)
                ? character.PersonDefinitionId
                : character.CharacterId;
        }
        else
        {
            var hero = collection.GetHeroByPersonId(personId);
            if (hero == null)
            {
                WarnOnce("person-not-found:" + personId,
                    "[JourneyVisual] 找不到 PersonId=" + personId + " 对应的具体人物（Character / Hero 均未命中）"
                    + " → 使用 default placeholder。");
                return ApplyFallback(result, defaultBody, "person-not-found");
            }

            result.DisplayName = hero.DisplayName;
            result.IsHero = true;
            result.AppearanceKey = hero.HeroId;
        }

        var entry = FindEntry(entries, result.AppearanceKey);
        if (entry != null && entry.Body != null)
        {
            result.Body = entry.Body;
            result.Offset = entry.Offset;
            result.Scale = entry.Scale > 0f ? entry.Scale : 1f;
            result.Source = "binding:" + result.AppearanceKey;
            return result;
        }

        WarnOnce("visual-missing:" + (result.AppearanceKey ?? "<null>"),
            "[JourneyVisual] PersonId=" + personId + "（外观键 " + (result.AppearanceKey ?? "<null>")
            + "）没有可用的视觉绑定 → 使用 default placeholder（身份保持由 PersonId 决定）。");
        return ApplyFallback(result, defaultBody, "visual-missing");
    }

    private static JourneyPersonVisualResult ApplyFallback(JourneyPersonVisualResult result, Sprite defaultBody,
        string reason)
    {
        result.Body = defaultBody;
        result.UsedFallback = true;
        result.Offset = Vector2.zero;
        result.Scale = 1f;
        result.Source = "default:" + reason;

        if (defaultBody == null)
        {
            WarnOnce("default-placeholder-missing",
                "[JourneyVisual] default player placeholder 未绑定：Member View 将不显示身体（仍保留身份 / 布局）。");
        }

        return result;
    }

    private static JourneyPersonVisualEntry FindEntry(IList<JourneyPersonVisualEntry> entries, string appearanceKey)
    {
        if (entries == null || string.IsNullOrEmpty(appearanceKey)) return null;
        for (int i = 0; i < entries.Count; i++)
        {
            var e = entries[i];
            if (e == null || string.IsNullOrEmpty(e.AppearanceKey)) continue;
            if (e.AppearanceKey == appearanceKey) return e;
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
