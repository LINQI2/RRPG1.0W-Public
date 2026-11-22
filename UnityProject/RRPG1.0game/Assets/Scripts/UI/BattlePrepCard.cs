using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// 战前角色卡 / 英雄卡 — 拖拽自身跟随鼠标，松手后 BattleUI 处理落点
/// 支持角色卡（CharacterCardData）和英雄卡（HeroData + HeroInstance）两种模式
/// </summary>
public class BattlePrepCard : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    /// <summary>角色卡数据（角色模式下使用）</summary>
    public CharacterCardData CardData { get; private set; }
    /// <summary>角色模板数据（角色模式下使用）</summary>
    public CharacterData CharData { get; private set; }

    /// <summary>英雄配置数据（英雄模式下使用）</summary>
    public HeroData HeroData { get; private set; }
    /// <summary>英雄运行时数据（英雄模式下使用）</summary>
    public HeroInstance HeroInstance { get; private set; }
    /// <summary>是否为英雄卡</summary>
    public bool IsHero { get; private set; }

    private BattleUI _battleUI;
    private Canvas _canvas;
    private RectTransform _rt;
    private Vector2 _originalPos;
    private CanvasGroup _cg;

    void Awake()
    {
        _rt = GetComponent<RectTransform>();
        _cg = gameObject.AddComponent<CanvasGroup>();
        _canvas = GetComponentInParent<Canvas>();
    }

    public void Init(BattleUI ui)
    {
        _battleUI = ui;
        if (_rt == null) _rt = GetComponent<RectTransform>();
        _originalPos = _rt.anchoredPosition;
    }

    /// <summary>
    /// 设置角色卡数据。
    /// personDisplayName：该卡对应具体人物的显示名（优先）；为空时回退到战斗定义名（Legacy fallback）。
    /// </summary>
    public void SetCardData(CharacterCardData card, CharacterData data, string personDisplayName = null)
    {
        IsHero = false;
        CardData = card;
        CharData = data;
        HeroData = null;
        HeroInstance = null;
        var txt = GetComponentInChildren<TMP_Text>();
        if (txt != null)
        {
            string shown = string.IsNullOrEmpty(personDisplayName) ? data.DisplayName : personDisplayName;
            txt.text = shown + "\nHP" + data.BaseHP;
        }
    }

    /// <summary>
    /// 设置英雄卡数据
    /// </summary>
    public void SetHeroData(HeroData heroData, HeroInstance heroInstance)
    {
        IsHero = true;
        HeroData = heroData;
        HeroInstance = heroInstance;
        CardData = null;
        CharData = null;
        var txt = GetComponentInChildren<TMP_Text>();
        if (txt != null) txt.text = "[英雄]" + heroData.DisplayName + "\nHP" + heroData.BaseHP;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (_battleUI == null || _battleUI.IsBattleStarted || (CardData == null && !IsHero)) return;
        _originalPos = _rt.anchoredPosition;
        _cg.alpha = 0.6f;
        _cg.blocksRaycasts = false;
        _battleUI.IsPrepDragging = true;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (_battleUI == null || _battleUI.IsBattleStarted || (CardData == null && !IsHero)) return;
        _rt.anchoredPosition += eventData.delta / _canvas.scaleFactor;
        // 拖拽中实时更新战场预览偏移
        _battleUI.UpdateDragPreview(eventData.position);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        _cg.alpha = 1f;
        _cg.blocksRaycasts = true;
        if (_battleUI != null)
        {
            _battleUI.IsPrepDragging = false;
            _battleUI.ApplyFormationPresentationToAllRows();
        }

        // 开战后阵型锁定：禁止把战前未上阵人物继续拖入战场
        if (_battleUI == null || _battleUI.IsBattleStarted || (CardData == null && !IsHero)) return;

        // 检查落点是否在排上
        var hitRow = _battleUI.HitTestRow(eventData.position);

        if (hitRow != null && hitRow != _battleUI.enemyFrontRow &&
            hitRow != _battleUI.enemyMiddleRow && hitRow != _battleUI.enemyBackRow)
        {
            // 战前布阵统一放行判定：失败（该排已满 / 超过六人）时不得先隐藏准备卡再失败
            bool placed = IsHero
                ? _battleUI.PlaceHero(HeroData, HeroInstance, hitRow)
                : _battleUI.PlaceUnit(CardData, CharData, hitRow);
            ApplyDropResult(placed);
        }
        else
        {
            // 回到原位
            _rt.anchoredPosition = _originalPos;
        }
    }

    /// <summary>
    /// 拖拽落点结果 → 表现：
    ///   成功：隐藏准备卡（该成员已进入战场）；
    ///   失败：准备卡必须保持 / 恢复可见，并回到正常手牌布局。
    /// 两种情况都重新排列手牌，保证手牌区没有空洞或重叠。
    /// </summary>
    public void ApplyDropResult(bool placed)
    {
        if (placed)
        {
            gameObject.SetActive(false);
        }
        else
        {
            _rt.anchoredPosition = _originalPos;
        }
        // 下一帧重新排列手牌（确保隐藏 / 恢复生效）
        _battleUI.Invoke("RearrangeHandCards", 0f);
    }

}
