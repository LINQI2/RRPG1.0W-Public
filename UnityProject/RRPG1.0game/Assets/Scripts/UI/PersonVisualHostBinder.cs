using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// PersonVisualHostBinder —— 把统一解析结果写进一个纯表现视觉宿主（**表现层工具，不做任何规则判断**）。
///
/// 稳定宿主约定（与 Battle `UnitCard.prefab` 的 WP-D 宿主命名一致）：
///   Anchor_Visual / Body_Visual   —— Image（Simple、PreserveAspect、raycastTarget = false）
///
/// 硬约束：
///   - 只写 Image 的 sprite / type / preserveAspect / raycastTarget 与 Body 的 scale / offset；
///   - **不**触碰 Button、CanvasGroup、EventTrigger、拖拽脚本或任何 Gameplay 字段；
///   - **不**拦截 raycast（视觉节点永远 raycastTarget = false，交互仍由 Root Button 承担）；
///   - 一般表面未配置时保留 prefab 占位；Prep 使用显式中性占位覆盖旧人物图，并保留作者布局。
/// </summary>
public static class PersonVisualHostBinder
{
    public const string AnchorVisualName = "Anchor_Visual";
    public const string BodyVisualName = "Body_Visual";
    private static Sprite _neutralPrepSprite;

    /// <summary>在卡片下查找身体视觉宿主（Anchor_Visual/Body_Visual → Body_Visual → Body）。</summary>
    public static Image FindBodyImage(GameObject root)
    {
        if (root == null) return null;

        var t = root.transform.Find(AnchorVisualName + "/" + BodyVisualName);
        if (t == null) t = root.transform.Find(BodyVisualName);
        if (t == null) t = root.transform.Find("Body");
        return t != null ? t.GetComponent<Image>() : null;
    }

    /// <summary>
    /// 显式应用（调用方已确定最终 Sprite / Offset / Scale；仍保证非交互与 PreserveAspect）。
    /// sprite 为 null 时不改写现有 Sprite（保留宿主自带占位图）。
    /// </summary>
    public static bool ApplyBodyVisual(GameObject root, Sprite sprite, Vector2 offset, float scale)
    {
        var image = FindBodyImage(root);
        if (image == null) return false;

        bool applied = false;
        if (sprite != null)
        {
            if (image.sprite != sprite) image.sprite = sprite;
            applied = true;
        }

        // 视觉节点：Simple + PreserveAspect + 不拦截 raycast（Root Button / Drag 保持唯一交互入口）
        image.type = Image.Type.Simple;
        image.preserveAspect = true;
        image.raycastTarget = false;

        float s = scale > 0f ? scale : 1f;
        image.rectTransform.localScale = new Vector3(s, s, 1f);
        image.rectTransform.anchoredPosition = offset;
        return applied;
    }

    /// <summary>
    /// Prep / Hero Selection 路径：仅当统一配置**真的命中**时才写入 Sprite / Offset / Scale；
    /// fallback（未配置 / 缺素材）时保持 prefab 作者布局与自带占位图，绝不移动制作人排好的卡面。
    /// </summary>
    public static bool ApplyForPerson(GameObject root, PlayerCollection collection, string personId,
        string appearanceKeyFallback, PersonVisualSurface surface, Sprite placeholder)
    {
        return ApplyBindingOnly(root, Resolve(collection, personId, appearanceKeyFallback, surface, placeholder));
    }

    /// <summary>
    /// 通用 Prep 卡缺少绑定时必须盖掉 prefab 上可能遗留的具体人物图。
    /// 仅替换 Sprite，不移动作者排好的视觉宿主；成功绑定仍走原解析结果。
    /// </summary>
    public static bool ApplyForPrepPerson(GameObject root, PlayerCollection collection, string personId,
        string appearanceKeyFallback, Sprite placeholder)
    {
        Sprite neutral = placeholder != null ? placeholder : NeutralPrepSprite;
        var result = Resolve(collection, personId, appearanceKeyFallback, PersonVisualSurface.Prep, neutral);
        if (!result.UsedFallback && result.Sprite != null)
            return ApplyBindingOnly(root, result);

        var image = FindBodyImage(root);
        if (image != null)
        {
            image.sprite = neutral;
            image.type = Image.Type.Simple;
            image.preserveAspect = true;
            image.raycastTarget = false;
        }
        return false;
    }

    private static Sprite NeutralPrepSprite
    {
        get
        {
            if (_neutralPrepSprite != null) return _neutralPrepSprite;
            // 纯表现、无人物语义的灰色剪影；不依赖制作人正在接入的美术资产。
            var texture = new Texture2D(16, 16, TextureFormat.RGBA32, false);
            texture.name = "NeutralPrepSilhouette";
            texture.filterMode = FilterMode.Point;
            var pixels = new Color32[16 * 16];
            var grey = new Color32(125, 130, 138, 255);
            for (int y = 0; y < 16; y++)
            for (int x = 0; x < 16; x++)
            {
                bool head = (x - 8) * (x - 8) + (y - 11) * (y - 11) <= 9;
                bool body = y >= 1 && y <= 7 && x >= 3 && x <= 12;
                pixels[y * 16 + x] = head || body ? grey : new Color32(0, 0, 0, 0);
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            _neutralPrepSprite = Sprite.Create(texture, new Rect(0, 0, 16, 16), new Vector2(0.5f, 0.5f), 16);
            _neutralPrepSprite.name = "NeutralPrepSilhouette";
            return _neutralPrepSprite;
        }
    }

    /// <summary>Hero Selection 路径：当前只持有 HeroId（尚无 Person 实例）时按外观键取图。</summary>
    public static bool ApplyForAppearanceKey(GameObject root, string appearanceKey,
        PersonVisualSurface surface, Sprite placeholder)
    {
        return ApplyBindingOnly(root,
            PersonVisualResolver.ResolveForAppearanceKey(appearanceKey, surface, placeholder));
    }

    private static PersonVisualResult Resolve(PlayerCollection collection, string personId,
        string appearanceKeyFallback, PersonVisualSurface surface, Sprite placeholder)
    {
        // 能确定 exact Person → 严格身份路径；否则退到外观键路径（表现容错，绝不制造临时身份）。
        bool exactPerson = collection != null && !string.IsNullOrEmpty(personId)
            && (collection.GetCharacterByPersonId(personId) != null
                || collection.GetHeroByPersonId(personId) != null);

        if (exactPerson)
            return PersonVisualResolver.ResolveForPerson(collection, personId, surface, placeholder);

        // 产品集合存在却找不到人物时，不用模板外观键伪装成具体人物。
        if (collection != null)
            return PersonVisualResolver.ResolveForAppearanceKey(null, surface, placeholder);

        string key = !string.IsNullOrEmpty(appearanceKeyFallback) ? appearanceKeyFallback : personId;
        return PersonVisualResolver.ResolveForAppearanceKey(key, surface, placeholder);
    }

    private static bool ApplyBindingOnly(GameObject root, PersonVisualResult result)
    {
        if (result.UsedFallback || result.Sprite == null) return false;
        return ApplyBodyVisual(root, result.Sprite, result.Offset, result.Scale);
    }
}
