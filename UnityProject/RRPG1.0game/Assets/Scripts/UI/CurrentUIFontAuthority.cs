using TMPro;
using UnityEngine;

/// <summary>唯一 Current UI 字体配置入口。未提供中文资产时仅使用 TMP 默认字体验证结构。</summary>
[CreateAssetMenu(menuName = "RRPG/Presentation/Current UI TMP Font")]
public sealed class CurrentUIFontAuthority : ScriptableObject
{
    public TMP_FontAsset font;
    public const string ResourcePath = "ScriptableObjects/Presentation/CurrentUIFontAuthority";
    public static TMP_FontAsset Default
    {
        get
        {
            var config = Resources.Load<CurrentUIFontAuthority>(ResourcePath);
            return config != null && config.font != null ? config.font : TMP_Settings.defaultFontAsset;
        }
    }
}

