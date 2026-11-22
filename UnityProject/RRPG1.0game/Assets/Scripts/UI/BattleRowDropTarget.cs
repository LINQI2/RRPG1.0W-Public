using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 战场排拖拽目标 — 供 Prefab 检测使用
/// </summary>
public class BattleRowDropTarget : MonoBehaviour
{
    [HideInInspector] public RectTransform _rt;

    void Awake() { _rt = GetComponent<RectTransform>(); }
    public RectTransform GetRectTransform() { return _rt; }
}
