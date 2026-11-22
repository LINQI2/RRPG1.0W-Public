using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

/// <summary>Battle-local sequence 时钟；不读取或写入 Gameplay。</summary>
public sealed class BattleFeedbackPlayer : MonoBehaviour
{
    /// <summary>新播放器的默认播放模式；Editor Test Runner 适配器可令测试默认 Instant。</summary>
    public static bool DefaultInstant;
    [Min(.01f)] public float PlaybackSpeed = 1;
    public bool Instant;
    public bool IsPlaying { get; private set; }
    private bool _skip;
    private Action _complete;
    private readonly List<Action> _restore = new List<Action>();
    private TMP_FontAsset _floatingFont;
    public TMP_FontAsset FloatingFont(TMP_FontAsset source)
    {
        if (_floatingFont == null && source != null && source.sourceFontFile != null)
        {
            // 临时飘数可能含首次出现的负号；不让动态字形写回制作人的正式字体资产。
            _floatingFont = TMP_FontAsset.CreateFontAsset(source.sourceFontFile, (int)source.faceInfo.pointSize,
                source.atlasPadding, source.atlasRenderMode, source.atlasWidth, source.atlasHeight,
                AtlasPopulationMode.Dynamic, true);
            _floatingFont.name = "BattleFeedbackTransientFont";
        }
        return _floatingFont != null ? _floatingFont : source;
    }
    public float Duration(float duration) => Instant || _skip ? 0 : Mathf.Max(0, duration) / Mathf.Max(.01f, PlaybackSpeed);
    public void SkipCurrent() { _skip = true; }
    public void CompleteImmediately() { StopAllCoroutines(); Finish(); }
    public void RestoreOnFinish(Action restore) { _restore.Add(restore); }
    public IEnumerator Tween(float duration, Action<float> apply)
    {
        float elapsed = 0;
        while (elapsed < duration && !Instant && !_skip)
        {
            elapsed += Time.unscaledDeltaTime * Mathf.Max(.01f, PlaybackSpeed);
            apply(Mathf.Clamp01(elapsed / duration));
            if (elapsed < duration) yield return null;
        }
        apply(1);
    }
    public void Play(IReadOnlyList<BattleFeedbackRecord> records,
        Func<BattleFeedbackRecord, IEnumerator> present, Action complete)
    {
        if (IsPlaying) throw new InvalidOperationException("Battle feedback already playing");
        _skip = false; IsPlaying = true; _complete = complete;
        if (Instant || !Application.isPlaying || !isActiveAndEnabled) { Finish(); return; }
        StartCoroutine(Sequence(records, present));
    }
    private IEnumerator Sequence(IReadOnlyList<BattleFeedbackRecord> records, Func<BattleFeedbackRecord, IEnumerator> present)
    {
        // 自己驱动嵌套 Enumerator，确保子动画异常也进入本播放器 finally。
        try
        {
            foreach (var record in records)
            {
                var stack = new Stack<IEnumerator>();
                stack.Push(present(record));
                try
                {
                    while (stack.Count > 0)
                    {
                        var top = stack.Peek();
                        if (!top.MoveNext()) { (top as IDisposable)?.Dispose(); stack.Pop(); continue; }
                        var nested = top.Current as IEnumerator;
                        if (nested != null) stack.Push(nested);
                        else yield return top.Current;
                    }
                }
                finally { while (stack.Count > 0) (stack.Pop() as IDisposable)?.Dispose(); }
            }
        }
        finally { Finish(); }
    }
    private void Finish()
    {
        if (!IsPlaying) return;
        IsPlaying = false;
        try { foreach (var restore in _restore) restore(); }
        finally { _restore.Clear(); var complete = _complete; _complete = null; complete?.Invoke(); }
    }
    private void OnDisable() { CompleteImmediately(); }
    private void OnDestroy()
    {
        if (_floatingFont == null) return;
        foreach (var atlas in _floatingFont.atlasTextures) if (atlas != null) Destroy(atlas);
        if (_floatingFont.material != null) Destroy(_floatingFont.material);
        Destroy(_floatingFont);
    }
}
