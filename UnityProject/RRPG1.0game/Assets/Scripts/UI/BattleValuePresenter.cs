using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>只写绑定的数值宿主；线性、圆形与数字共享同一 normalized value。</summary>
public sealed class BattleValuePresenter : MonoBehaviour
{
    public Slider Linear;
    public Image Radial;
    public TMP_Text Number;
    public bool ShowMaximum = true;
    public string NumberFormat;
    private float _current;
    public static float Normalize(float current, float max) => max <= 0 ? 0 : Mathf.Clamp01(current / max);
    public void SetImmediate(float current, float max)
    {
        _current = current;
        float normalized = Normalize(current, max);
        if (Linear != null) { Linear.minValue = 0; Linear.maxValue = 1; Linear.SetValueWithoutNotify(normalized); }
        if (Radial != null) Radial.fillAmount = normalized;
        if (Number != null) Number.text = string.IsNullOrEmpty(NumberFormat)
            ? Mathf.RoundToInt(current) + (ShowMaximum ? "/" + Mathf.RoundToInt(max) : "")
            : BattleHudFormatting.Format(NumberFormat, Mathf.RoundToInt(current), Mathf.RoundToInt(max));
    }
    public IEnumerator AnimateTo(float current, float max, float duration)
    {
        return AnimateTo(current, max, duration, null);
    }
    public IEnumerator AnimateTo(float current, float max, float duration, BattleFeedbackPlayer clock)
    {
        float start = _current;
        if (clock != null) yield return clock.Tween(duration, t => SetImmediate(Mathf.Lerp(start, current, t), max));
        else
        {
            float elapsed = 0;
            while (elapsed < duration) { elapsed += Time.unscaledDeltaTime; SetImmediate(Mathf.Lerp(start, current, Mathf.Clamp01(elapsed / duration)), max); yield return null; }
        }
        SetImmediate(current, max);
    }
}
