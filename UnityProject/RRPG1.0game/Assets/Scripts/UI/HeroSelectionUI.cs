using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// 英雄选择界面
///
/// 负责：新游戏时让玩家从可用英雄中选择一个
/// 不负责：存档管理、领地逻辑
///
/// 由 MainMenuUI 在点击 New Game 时调用
/// </summary>
public class HeroSelectionUI : MonoBehaviour
{
    [Header("UI 元素")]
    public GameObject canvasRoot;

    [Header("英雄卡片预制体（需要你手动制作）")]
    public GameObject heroCardPrefab;

    [Header("Person Visual Binding V0.1（Hero Selection 肖像；配置唯一来源 = PersonVisualBindingCatalog 资产）")]
    [Tooltip("缺配置 / 缺素材时的 placeholder（未绑定时保留 prefab 自带占位图，不阻断选择）")]
    public Sprite heroSelectPlaceholder;

    [Header("英雄卡片容器")]
    public RectTransform cardContainer;

    [Header("确认按钮")]
    public Button btnConfirm;

    [Header("引用")]
    public TownUI townUI;

    [Header("卡片布局参数")]
    public Vector3 cardScale = Vector3.one;
    public Vector2 startPosition = new Vector2(-200, 0);
    public float cardSpacing = 250f;

    /// <summary>当前所有可选的英雄列表</summary>
    private List<HeroData> _availableHeroes;

    /// <summary>当前选中的英雄索引（-1=未选择）</summary>
    private int _selectedIndex = -1;

    /// <summary>生成的卡片对象列表</summary>
    private List<GameObject> _heroCards = new List<GameObject>();

    void Start()
    {
        if (canvasRoot != null)
            canvasRoot.SetActive(false);

        if (btnConfirm != null)
            btnConfirm.onClick.AddListener(OnConfirm);
    }

    /// <summary>
    /// 显示英雄选择界面
    /// </summary>
    public void Show()
    {
        _selectedIndex = -1;
        _availableHeroes = new List<HeroData>();

        // 从 DataManager 加载所有英雄
        if (DataManager.Instance.AllHeroes != null)
        {
            _availableHeroes.AddRange(DataManager.Instance.AllHeroes);
        }

        if (canvasRoot != null)
            canvasRoot.SetActive(true);

        if (btnConfirm != null)
            btnConfirm.interactable = false;

        RefreshDisplay();
    }

    /// <summary>
    /// 隐藏界面
    /// </summary>
    public void Hide()
    {
        if (canvasRoot != null)
            canvasRoot.SetActive(false);
    }

    void RefreshDisplay()
    {
        foreach (var card in _heroCards)
            Destroy(card);
        _heroCards.Clear();

        if (heroCardPrefab == null || cardContainer == null)
        {
            Debug.LogError("[HeroSelectionUI] heroCardPrefab 或 cardContainer 未绑定");
            return;
        }

        for (int i = 0; i < _availableHeroes.Count; i++)
        {
            var hero = _availableHeroes[i];
            var go = Instantiate(heroCardPrefab, cardContainer, false);
            go.name = "HeroCard_" + hero.HeroId;
            go.transform.localScale = cardScale;

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(startPosition.x + i * cardSpacing, startPosition.y);

            // 设置英雄信息
            string skillInfo = "技能：无";
            if (hero.InitialSkillIds != null && hero.InitialSkillIds.Length > 0)
            {
                var skillNames = new List<string>();
                foreach (var skillId in hero.InitialSkillIds)
                {
                    var skillData = DataManager.Instance.GetSkillById(skillId);
                    if (skillData != null)
                        skillNames.Add(skillData.Name);
                }
                if (skillNames.Count > 0)
                    skillInfo = "技能：" + string.Join("、", skillNames);
            }

            var texts = go.GetComponentsInChildren<TMP_Text>();
            foreach (var t in texts)
            {
                if (t.name == "Text_Name") t.text = hero.DisplayName;
                else if (t.name == "Text_HP") t.text = "HP: " + hero.BaseHP;
                else if (t.name == "Text_ATK") t.text = "ATK: " + hero.BaseAttack;
                else if (t.name == "Text_Skill") t.text = skillInfo;
            }

            // Person Visual Binding V0.1：HeroSelect 表面读取统一 Visual Binding
            //（HeroId 作外观键 → 统一配置 → placeholder；缺素材不影响文字信息与选择，Root Button 仍是唯一交互入口）
            PersonVisualHostBinder.ApplyForAppearanceKey(go, hero.HeroId,
                PersonVisualSurface.HeroSelect, heroSelectPlaceholder);

            // 高亮选中的英雄
            var img = go.GetComponent<Image>();
            if (img != null && _selectedIndex == i)
                img.color = Color.green;

            // 绑定点击
            int idx = i;
            var btn = go.GetComponent<Button>();
            if (btn != null)
                btn.onClick.AddListener(() => OnHeroCardClick(idx));

            _heroCards.Add(go);
        }
    }

    void OnHeroCardClick(int index)
    {
        if (index < 0 || index >= _availableHeroes.Count) return;

        // 取消之前的高亮
        if (_selectedIndex >= 0 && _selectedIndex < _heroCards.Count)
        {
            var prevImg = _heroCards[_selectedIndex].GetComponent<Image>();
            if (prevImg != null) prevImg.color = Color.white;
        }

        _selectedIndex = index;

        // 高亮当前选中的
        var img = _heroCards[index].GetComponent<Image>();
        if (img != null) img.color = Color.green;

        if (btnConfirm != null)
            btnConfirm.interactable = true;
    }

    void OnConfirm()
    {
        if (_selectedIndex < 0 || _selectedIndex >= _availableHeroes.Count) return;

        var selectedHeroData = _availableHeroes[_selectedIndex];

        // 创建新游戏数据并设置所选英雄
        SaveSystem.DeleteSave();
        PlayerCollection collection = SaveSystem.NewGame();

        // 替换英雄为所选英雄（NewGame 默认 hero_001）
        collection.OwnedHeroes.Clear();
        var heroInstance = new HeroInstance(selectedHeroData.HeroId, selectedHeroData.DisplayName, selectedHeroData.BaseHP);
        collection.OwnedHeroes.Add(heroInstance);

        SaveSystem.Save(collection);

        // 进入领地
        if (townUI == null)
        {
            Debug.LogError("[HeroSelectionUI] townUI 未绑定");
            return;
        }

        Hide();
        townUI.Initialize(collection);
        townUI.gameObject.SetActive(true);
    }
}