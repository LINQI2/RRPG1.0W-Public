using UnityEngine;

/// <summary>Battle-local Inspector 配置。资源 Hook 预留，不引入动画或音频框架。</summary>
[System.Serializable]
public sealed class BattleFeedbackProfile
{
    public float CardSelectScale = 1.08f;
    public Vector2 CardSelectOffset = new Vector2(0, 24);
    public float CardCommitDuration = .16f;
    public float ActorMoveDistance = 12;
    public float ActorMoveDuration = .12f;
    public float HitScalePunch = .08f;
    public float HitDuration = .12f;
    public float FloatingNumberDuration = .3f;
    public Vector2 FloatingNumberOffset = new Vector2(0, 40);
    public float HpTweenDuration = .18f;
    public float ShieldTweenDuration = .18f;
    public float DownDuration = .25f;
    public float FormationMoveDuration = .2f;
    public Color TargetHighlight = new Color(.65f, 1f, .65f, 1f);
    public AnimationClip ActorClip;
    public string SpineAnimationKey;
    public GameObject VfxPrefab;
    public AudioClip AudioClip;
    public string CameraCue;
}
