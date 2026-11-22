using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.Serialization;

/// <summary>
/// 领地卡册检视界面（独立页面，只读）
/// 布局参数独立于 ExpeditionUI，各自可单独调整
/// </summary>
public class DeckViewerUI : MonoBehaviour
{
    [Header("根对象")]
    public GameObject canvasRoot;

    [Header("预制体")]
    public GameObject charCardPrefab;   // 角色卡（PrepCard）
    public GameObject itemCardPrefab;   // 道具卡（ItemCard）

    [Header("UI 容器")]
    public RectTransform heroPanel;
    public RectTransform cardPanel;

    [Header("标签按钮")]
    public Button btnCharTab;
    public Button btnItemTab;

    [Header("信息文本")]
    public TMP_Text infoText;
    public TMP_Text titleText;

    [Header("按钮")]
    public Button btnBack;

    [Header("Town / Deck Viewer Only — 不影响 Battle")]
    [Tooltip("仅 Town 卡册的卡间距；Battle 参数在 BattleController。")]
    [FormerlySerializedAs("cardSpacing")]
    public float deckViewerCardSpacing = 200f;
    public Vector2 cardStartPosition = new Vector2(-400, 0);
    public Vector2 heroCardStartPosition = new Vector2(-400, 0);

    [Header("Town / Deck Viewer Only — 卡册排列 / 缩放")]
    [Tooltip("仅 Town 卡册的卡根缩放；不影响 Prep 或 Battle Cards。")]
    [FormerlySerializedAs("cardScale")]
    public Vector3 deckViewerCardScale = Vector3.one;
    public Vector3 heroCardScale = Vector3.one;
    public int cardsPerRow = 4;
    public float rowSpacing = 250f;

    private PlayerCollection _playerData;
    private bool _isCharTab = true;

    void Awake()
    {
        if (btnCharTab != null)
            btnCharTab.onClick.AddListener(() => SwitchTab(true));
        if (btnItemTab != null)
            btnItemTab.onClick.AddListener(() => SwitchTab(false));
        if (btnBack != null)
            btnBack.onClick.AddListener(OnBack);

        if (canvasRoot != null)
            canvasRoot.SetActive(false);
        else
            gameObject.SetActive(false);
    }

    public void Show(PlayerCollection playerData)
    {
        _playerData = playerData;
        _isCharTab = true;

        if (canvasRoot != null)
            canvasRoot.SetActive(true);
        else
            gameObject.SetActive(true);

        RefreshDisplay();
        Debug.Log("[DeckViewer] 领地卡册");
    }

    public void OnBack()
    {
        if (canvasRoot != null)
            canvasRoot.SetActive(false);
        else
            gameObject.SetActive(false);

        var townUI = FindObjectOfType<TownUI>(true);
        if (townUI != null)
            townUI.OnReturnFromExpedition();
    }

    private void SwitchTab(bool isChar)
    {
        _isCharTab = isChar;
        RefreshDisplay();
    }

    private void RefreshDisplay()
    {
        ClearContainer(heroPanel);
        ClearContainer(cardPanel);

        if (_playerData == null) return;

        // 英雄区
        ShowCards(heroPanel, _playerData.OwnedHeroes.Select(h =>
        {
            var hd = DataManager.Instance.GetHeroById(h.HeroId);
            string status = h.Injury != InjuryStatus.Normal ? " 伤残" : "";
            return new CardInfo
            {
                Name = "[英雄] " + (hd?.DisplayName ?? h.HeroId) + status,
                Desc = "HP:" + h.MaxHP + " Lv." + h.Level,
                Status = ""
            };
        }).ToList(), heroCardStartPosition, charCardPrefab);

        // 卡片区
        if (_isCharTab)
        {
            if (btnCharTab != null) btnCharTab.interactable = false;
            if (btnItemTab != null) btnItemTab.interactable = true;

            ShowCards(cardPanel, _playerData.OwnedCharacterCards.Select(c =>
            {
                var cd = DataManager.Instance.GetCharacterById(c.CharacterId);
                // M1：按卡解析其对应具体人物实例（同模板多卡各自显示自身状态，不塌缩）
                var ci = _playerData.GetCharacterInstanceForCard(c);
                string status = "";
                if (ci != null)
                {
                    if (ci.IsDead) status = "已阵亡";
                    else if (ci.Injury != InjuryStatus.Normal)
                        status = GetInjuryLabel(ci.Injury);
                }
                return new CardInfo
                {
                    Name = _playerData.GetPersonDisplayNameForCard(c) ?? cd?.DisplayName ?? c.CharacterId,
                    Desc = "HP:" + (cd?.BaseHP ?? 0) + " ATK:" + (cd?.BaseAttack ?? 0),
                    Status = status
                };
            }).ToList(), cardStartPosition, charCardPrefab);
        }
        else
        {
            if (btnCharTab != null) btnCharTab.interactable = true;
            if (btnItemTab != null) btnItemTab.interactable = false;

            ShowCards(cardPanel, _playerData.OwnedItemCards.Select(i =>
            {
                string desc = "";
                if (i.Effects != null && i.Effects.Length > 0)
                {
                    var e = i.Effects[0];
                    desc = e.EffectType == EffectType.Heal ? "治疗 " + e.Value
                         : e.EffectType == EffectType.Damage ? "伤害 " + e.Value
                         : e.EffectType.ToString();
                }
                return new CardInfo
                {
                    Name = i.DisplayName ?? i.CardId,
                    Desc = desc + "  TP:" + i.CostTP,
                    Status = ""
                };
            }).ToList(), cardStartPosition, itemCardPrefab);
        }
    }

    private void ShowCards(RectTransform container, List<CardInfo> cards, Vector2 startPos, GameObject prefab)
    {
        if (prefab == null)
        {
            Debug.LogError("[DeckViewer] prefab 未绑定");
            return;
        }
        if (container == null)
        {
            Debug.LogError("[DeckViewer] container 未绑定");
            return;
        }

        // 英雄区使用独立缩放
        bool isHero = container == heroPanel;
        Vector3 scale = isHero ? heroCardScale : deckViewerCardScale;

        int cols = Mathf.Max(1, cardsPerRow);
        for (int i = 0; i < cards.Count; i++)
        {
            var info = cards[i];
            var go = Instantiate(prefab, container, false);
            go.name = "Card_" + info.Name;
            go.transform.localScale = scale;

            int row = i / cols;
            int col = i % cols;
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(
                startPos.x + col * deckViewerCardSpacing,
                startPos.y - row * rowSpacing
            );

            var txt = go.GetComponentInChildren<TMP_Text>();
            if (txt != null)
            {
                txt.text = info.Name + "\n" + info.Desc;
                if (!string.IsNullOrEmpty(info.Status))
                    txt.text += "\n" + info.Status;
            }
        }
    }

    private void ClearContainer(RectTransform container)
    {
        if (container == null) return;
        var toDestroy = new List<GameObject>();
        var keepNames = new HashSet<string> { "BtnCharTab", "BtnItemTab" };
        foreach (Transform child in container)
        {
            if (keepNames.Contains(child.name)) continue;
            toDestroy.Add(child.gameObject);
        }
        foreach (var child in toDestroy)
            GameObject.Destroy(child);
    }

    private string GetInjuryLabel(InjuryStatus injury)
    {
        switch (injury)
        {
            case InjuryStatus.LightInjury: return "轻伤";
            case InjuryStatus.HeavyInjury: return "重伤";
            case InjuryStatus.Disabled: return "残疾";
            default: return "正常";
        }
    }

    public class CardInfo
    {
        public string Name;
        public string Desc;
        public string Status;
    }
}
