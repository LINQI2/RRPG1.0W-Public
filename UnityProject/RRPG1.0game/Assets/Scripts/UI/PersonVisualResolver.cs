using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// PersonVisualResolver —— **统一人物视觉解析入口**（Person Visual Binding V0.1｜Implementation-only / Not Frozen）。
///
/// 解析链（硬约束，四个表面完全一致）：
///
///   PersonId（具体人物身份真相）
///     → exact runtime Person（CharacterInstance / HeroInstance）
///     → AppearanceKey（PersonDefinitionId → CharacterId / HeroId；只回答「用哪份外观」）
///     → PersonVisualBindingCatalog 配置行
///     → surface override → DefaultBodySprite
///     → 消费方 placeholder（仍为空则保留宿主自带占位图，不隐藏身体）
///
/// 边界（硬约束）：
///   - 只读：不写 PlayerCollection / 不建第二套 Person Authority / 不碰 SaveData / SaveVersion；
///   - 模板键 / 外观键只用于外观查询，绝不回答「这个人是谁」；不按 DisplayName / CardName 猜身份；
///   - 同一 Definition 的不同 Person 可以共享 AppearanceKey 与素材，但身份仍由 PersonId 区分；
///   - 找不到 Person / 未配置外观 / 缺 Sprite → placeholder + 去重后的可诊断 warning，绝不抛异常、绝不阻断流程；
///   - 本类的数据形态（一张 Inspector 配置资产）是 V0.1 当前实现，**不得**表述为已冻结的长期美术架构。
/// </summary>
public static class PersonVisualResolver
{
    private static readonly HashSet<string> _warnedKeys = new HashSet<string>();

    /// <summary>测试 / 诊断用：清空已输出过的去重 warning 记录。</summary>
    public static void ClearWarningsForTests()
    {
        _warnedKeys.Clear();
    }

    /// <summary>
    /// 由 PersonId 解析该具体人物的外观配置键（Character 优先 PersonDefinitionId，其次 CharacterId；Hero 为 HeroId）。
    /// 返回 null = 找不到该 Person（调用方必须走 placeholder，禁止猜模板）。
    /// </summary>
    public static string ResolveAppearanceKey(PlayerCollection collection, string personId)
    {
        var character = collection != null ? collection.GetCharacterByPersonId(personId) : null;
        if (character != null)
        {
            return !string.IsNullOrEmpty(character.PersonDefinitionId)
                ? character.PersonDefinitionId
                : character.CharacterId;
        }

        var hero = collection != null ? collection.GetHeroByPersonId(personId) : null;
        if (hero != null) return hero.HeroId;

        return null;
    }

    /// <summary>
    /// 严格身份路径：PersonId → exact Person → 外观键 → binding → placeholder。
    /// 找不到 exact Person 时为 placeholder（不会用模板键或其它键替代身份）。
    /// </summary>
    public static PersonVisualResult ResolveForPerson(PlayerCollection collection, string personId,
        PersonVisualSurface surface, Sprite placeholder,
        IList<PersonVisualBindingEntry> entriesOverride = null)
    {
        var result = NewResult(surface, personId);

        if (collection == null || string.IsNullOrEmpty(personId))
        {
            WarnOnce("person-unresolved",
                "[PersonVisual] 无法解析人物视觉：PlayerCollection 或 PersonId 为空 → 使用 placeholder（表面 " + surface + "）。");
            return ApplyPlaceholder(result, placeholder, "person-unresolved");
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
                    "[PersonVisual] 找不到 PersonId=" + personId + " 对应的具体人物（Character / Hero 均未命中）"
                    + " → 使用 placeholder（表面 " + surface + "）。");
                return ApplyPlaceholder(result, placeholder, "person-not-found");
            }

            result.DisplayName = hero.DisplayName;
            result.IsHero = true;
            result.AppearanceKey = hero.HeroId;
        }

        return ApplyBinding(result, placeholder, entriesOverride);
    }

    /// <summary>
    /// 外观键路径：当消费方**尚无** Person 实例、但已能确定外观配置键时使用
    ///（例如英雄选择：当前只持有 HeroData.HeroId，尚未创建任何 Person）。
    /// 结果中的 PersonId 保持 null —— 外观键不是身份，不制造临时身份。
    /// </summary>
    public static PersonVisualResult ResolveForAppearanceKey(string appearanceKey,
        PersonVisualSurface surface, Sprite placeholder,
        IList<PersonVisualBindingEntry> entriesOverride = null)
    {
        var result = NewResult(surface, null);
        result.AppearanceKey = appearanceKey;

        if (string.IsNullOrEmpty(appearanceKey))
        {
            WarnOnce("appearance-key-missing",
                "[PersonVisual] 外观键为空，无法读取人物视觉配置 → 使用 placeholder（表面 " + surface + "）。");
            return ApplyPlaceholder(result, placeholder, "appearance-key-missing");
        }

        return ApplyBinding(result, placeholder, entriesOverride);
    }

    /// <summary>
    /// 由角色卡的**静态配置**解析外观键兜底（PersonDefinitionId → CharacterId）。
    /// 只用于选择「用哪份外观」，不代表人物身份，不替代 PersonId。
    /// </summary>
    public static string ResolveAppearanceKeyForCard(CharacterCardData card)
    {
        if (card == null) return null;
        return !string.IsNullOrEmpty(card.PersonDefinitionId) ? card.PersonDefinitionId : card.CharacterId;
    }

    // ==================== 内部 ====================

    private static PersonVisualResult NewResult(PersonVisualSurface surface, string personId)
    {
        return new PersonVisualResult
        {
            PersonId = personId,
            Surface = surface,
            Scale = 1f,
            Offset = Vector2.zero,
            Source = "unresolved"
        };
    }

    private static PersonVisualResult ApplyBinding(PersonVisualResult result, Sprite placeholder,
        IList<PersonVisualBindingEntry> entriesOverride)
    {
        var entry = FindEntry(entriesOverride, result.AppearanceKey);
        if (entry == null)
        {
            var catalog = PersonVisualBindingCatalog.Instance;
            if (catalog != null) entry = catalog.FindEntry(result.AppearanceKey);
        }

        if (entry != null)
        {
            var sprite = entry.ResolveSprite(result.Surface);
            if (sprite != null)
            {
                bool overridden = entry.HasSurfaceOverride(result.Surface);
                result.Sprite = sprite;
                result.Offset = entry.ResolveOffset(result.Surface);
                result.Scale = entry.ResolveScale(result.Surface);
                result.Source = "binding:" + result.AppearanceKey + (overridden ? "@" + result.Surface : "");
                return result;
            }
        }

        WarnOnce("visual-missing:" + (result.AppearanceKey ?? "<null>"),
            "[PersonVisual] 外观键 " + (result.AppearanceKey ?? "<null>")
            + "（PersonId=" + (result.PersonId ?? "<null>") + "）没有可用的视觉绑定"
            + " → 使用 placeholder（表面 " + result.Surface + "，身份不由外观键决定）。");
        return ApplyPlaceholder(result, placeholder, "visual-missing");
    }

    private static PersonVisualResult ApplyPlaceholder(PersonVisualResult result, Sprite placeholder, string reason)
    {
        result.Sprite = placeholder;
        // 注意：placeholder 为空时不是「没有表现」——宿主必须保留 prefab 自带占位图，
        // 因此这里只记录 fallback，不强制清空 Sprite / 不禁用 Image / 不影响 raycast 与选人。
        result.UsedFallback = true;
        result.Offset = Vector2.zero;
        result.Scale = 1f;
        result.Source = "default:" + reason;

        if (placeholder == null)
        {
            WarnOnce("placeholder-missing:" + result.Surface,
                "[PersonVisual] 表面 " + result.Surface + " 未绑定 placeholder：保留宿主自带占位图"
                + "（不隐藏身体、不影响身份 / 布局 / 交互）。");
        }

        return result;
    }

    private static PersonVisualBindingEntry FindEntry(IList<PersonVisualBindingEntry> entries, string appearanceKey)
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
