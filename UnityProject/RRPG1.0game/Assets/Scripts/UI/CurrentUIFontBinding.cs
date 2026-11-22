using TMPro;
using UnityEngine;

/// <summary>静态宿主在运行时同样消费 Font Authority；不改布局或文案。</summary>
[RequireComponent(typeof(TextMeshProUGUI))]
public sealed class CurrentUIFontBinding : MonoBehaviour
{
    void Awake() { Apply(); }
    void OnEnable() { Apply(); }
    public void Apply()
    {
        var text = GetComponent<TMP_Text>();
        var font = CurrentUIFontAuthority.Default;
        if (text != null && font != null) text.font = font;
    }
}
