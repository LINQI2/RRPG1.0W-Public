using UnityEngine;

/// <summary>
/// Person Visual Binding V0.1｜表现表面枚举（**Implementation-only / Not Frozen**）。
///
/// 表面只回答「在哪一个消费者上显示」，不承载任何规则含义：
///   - Journey      ：Expedition Journey Party Member（`ExpeditionJourneyView`）
///   - Battle       ：Battle 我方单位卡身体（`UnitCard.prefab` 的 `Anchor_Visual/Body_Visual`）
///   - Prep         ：远征准备 / 战前准备的人物卡（`CharCard.prefab`）
///   - HeroSelect   ：英雄选择肖像（`HeroSelectCard.prefab`）
/// </summary>
public enum PersonVisualSurface
{
    Journey = 0,
    Battle = 1,
    Prep = 2,
    HeroSelect = 3,
    CardPortrait = 4
}

/// <summary>
/// PersonVisualBindingEntry —— 一条「AppearanceKey → 一份外观配置」的最小绑定条目。
///
/// 语义（硬约束）：
///   - `AppearanceKey` 只回答「用哪份外观」（Character 优先 PersonDefinitionId，其次 CharacterId；Hero 为 HeroId）；
///   - 它**不是**人物身份，禁止用它反推或替代 PersonId；
///   - 只承载最小 visual handle（Sprite + 可选 surface override + 最小 scale / offset），
///     不承载 Animator / Spine / 换装 / 表情 / Atlas / Addressables。
///
/// fallback 语义（解析顺序）：
///   surface override → 空 → DefaultBodySprite → 空 → 消费者自己的 placeholder。
/// 制作人不需要为第一版制作 4 张不同人物图：只填 DefaultBodySprite 即可让四个表面都显示。
/// </summary>
[System.Serializable]
public class PersonVisualBindingEntry
{
    /// <summary>外观配置键（PersonDefinitionId / CharacterId / HeroId）。不是 Person identity。</summary>
    [Tooltip("外观配置键：Character 用 PersonDefinitionId（缺失时 CharacterId），Hero 用 HeroId。不代表人物身份。")]
    public string AppearanceKey;

    /// <summary>默认身体 Sprite（所有表面在无 override 时使用；未来替换正式素材只改这里）。</summary>
    [Tooltip("默认身体 Sprite：四个表面的共同 fallback。只做一张图时可以只填这一项。")]
    public Sprite DefaultBodySprite;

    // ---------------- 表面 override（可空 = 回退 DefaultBodySprite） ----------------

    [Tooltip("Journey Party 专用 Sprite（可空）")]
    public Sprite JourneySpriteOverride;

    [Tooltip("Battle 单位卡专用 Sprite（可空）")]
    public Sprite BattleSpriteOverride;

    [Tooltip("远征准备 / 战前准备人物卡专用 Sprite（可空）")]
    public Sprite PrepSpriteOverride;

    [Tooltip("英雄选择肖像专用 Sprite（可空）")]
    public Sprite HeroSelectSpriteOverride;

    public Sprite CardPortraitSpriteOverride;

    // ---------------- 表面级最小表现参数（Scale / Offset；不是 Layout Profile） ----------------

    [Tooltip("Journey 局部偏移（像素）")]
    public Vector2 JourneyOffset = Vector2.zero;
    [Tooltip("Journey 缩放（≤0 视为 1）")]
    public float JourneyScale = 1f;

    [Tooltip("Battle 局部偏移（像素）")]
    public Vector2 BattleOffset = Vector2.zero;
    [Tooltip("Battle 缩放（≤0 视为 1）")]
    public float BattleScale = 1f;

    [Tooltip("Prep 局部偏移（像素）")]
    public Vector2 PrepOffset = Vector2.zero;
    [Tooltip("Prep 缩放（≤0 视为 1）")]
    public float PrepScale = 1f;

    [Tooltip("HeroSelect 局部偏移（像素）")]
    public Vector2 HeroSelectOffset = Vector2.zero;
    [Tooltip("HeroSelect 缩放（≤0 视为 1）")]
    public float HeroSelectScale = 1f;

    /// <summary>该表面是否有显式 Sprite override。</summary>
    public bool HasSurfaceOverride(PersonVisualSurface surface)
    {
        return SurfaceOverride(surface) != null;
    }

    /// <summary>该表面的显式 Sprite override（无则 null）。</summary>
    public Sprite SurfaceOverride(PersonVisualSurface surface)
    {
        switch (surface)
        {
            case PersonVisualSurface.Journey: return JourneySpriteOverride;
            case PersonVisualSurface.Battle: return BattleSpriteOverride;
            case PersonVisualSurface.Prep: return PrepSpriteOverride;
            case PersonVisualSurface.HeroSelect: return HeroSelectSpriteOverride;
            case PersonVisualSurface.CardPortrait: return CardPortraitSpriteOverride;
            default: return null;
        }
    }

    /// <summary>该表面实际应显示的 Sprite：override → DefaultBodySprite → null（调用方走 placeholder）。</summary>
    public Sprite ResolveSprite(PersonVisualSurface surface)
    {
        var over = SurfaceOverride(surface);
        return over != null ? over : DefaultBodySprite;
    }

    /// <summary>该表面的局部偏移。</summary>
    public Vector2 ResolveOffset(PersonVisualSurface surface)
    {
        switch (surface)
        {
            case PersonVisualSurface.Journey: return JourneyOffset;
            case PersonVisualSurface.Battle: return BattleOffset;
            case PersonVisualSurface.Prep: return PrepOffset;
            case PersonVisualSurface.HeroSelect: return HeroSelectOffset;
            default: return Vector2.zero;
        }
    }

    /// <summary>该表面的缩放（≤0 视为 1，避免把 0 缩放写进视觉宿主）。</summary>
    public float ResolveScale(PersonVisualSurface surface)
    {
        float scale;
        switch (surface)
        {
            case PersonVisualSurface.Journey: scale = JourneyScale; break;
            case PersonVisualSurface.Battle: scale = BattleScale; break;
            case PersonVisualSurface.Prep: scale = PrepScale; break;
            case PersonVisualSurface.HeroSelect: scale = HeroSelectScale; break;
            default: scale = 1f; break;
        }
        return scale > 0f ? scale : 1f;
    }
}

/// <summary>
/// PersonVisualResult —— 一次视觉解析结果（只读值对象）。
/// Source / UsedFallback 仅供诊断与测试；不写回任何长期数据，不构成第二套 Identity Authority。
/// </summary>
public struct PersonVisualResult
{
    /// <summary>具体人物身份（可为 null：Hero Selection 等尚无 Person 实例，只按外观键取图）。</summary>
    public string PersonId;
    public string DisplayName;
    public bool IsHero;

    /// <summary>外观配置键（只回答「用哪份外观」）。</summary>
    public string AppearanceKey;
    public PersonVisualSurface Surface;

    /// <summary>最终 Sprite；null = 消费方应使用自己的 placeholder（或保留 prefab 自带占位图）。</summary>
    public Sprite Sprite;
    public Vector2 Offset;
    public float Scale;
    public bool UsedFallback;

    /// <summary>诊断来源：binding:<key> / binding:<key>@<surface-override> / default:<reason>。</summary>
    public string Source;
}
