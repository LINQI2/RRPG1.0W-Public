using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 商店界面
///
/// 负责：显示可购买的物品列表（预制体）、处理购买逻辑
/// 不负责：商店物品配置（后续通过 CSV/配置系统管理）
///
/// 使用方式：
///   1. 场景中挂 ShopUI 组件（默认隐藏）
///   2. 点击商店节点时调用 ShowShop()
///   3. 玩家点击商品预制体上的购买按钮，扣金币后添加道具到卡册
///
/// 视觉布局（用户可在 Unity Inspector 中调整）：
///   canvasRoot — 商店 UI 的根对象（默认隐藏）
///   shopItemPrefab — 商品卡片预制体
///   shopContainer — 商品卡片生成容器
///   cardScale — 卡片缩放
///   cardSpacing — 卡片间距
///   rowSpacing — 行间距
///   cardsPerRow — 每行几张
///   startPosition — 起始位置
///   infoText — 操作反馈文本
///   btnClose — 关闭商店返回地图
/// </summary>
public class ShopUI : MonoBehaviour
{
    [Header("UI 元素")]
    public GameObject canvasRoot;
    public GameObject shopItemPrefab;
    public RectTransform shopContainer;
    public Text infoText;
    public Button btnClose;

    [Header("卡片布局")]
    public Vector3 cardScale = Vector3.one;
    public float cardSpacing = 200f;
    public float rowSpacing = 250f;
    public int cardsPerRow = 4;
    public Vector2 startPosition = new Vector2(-400, 0);

    /// <summary>商店商品列表（硬编码占位，后续改为 CSV 配置）</summary>
    private List<ShopItem> _shopItems = new List<ShopItem>();

    /// <summary>医疗服务列表（三个固定按钮）</summary>
    private List<MedicalService> _medicalServices = new List<MedicalService>();

    /// <summary>已购买的商品索引集合</summary>
    private HashSet<int> _soldOutItems = new HashSet<int>();

    /// <summary>玩家数据引用</summary>
    private PlayerCollection _playerData;

    /// <summary>关闭回调</summary>
    private System.Action _onClose;

    /// <summary>当前标签：true=商品，false=医疗</summary>
    private bool _isShopTab = true;

    /// <summary>生成的卡片列表</summary>
    private List<GameObject> _itemCards = new List<GameObject>();

    /// <summary>
    /// 商店商品数据结构
    /// </summary>
    public class ShopItem
    {
        public string ItemId;
        public string DisplayName;
        public int Price;
        public string Description;
    }

    /// <summary>
    /// 医疗服务数据结构
    /// </summary>
    public class MedicalService
    {
        public InjuryStatus TargetInjury;
        public string DisplayName;
        public int Price;
        public string Description;
    }

    void Start()
    {
        if (canvasRoot != null)
            canvasRoot.SetActive(false);
        else
            gameObject.SetActive(false);

        if (btnClose != null)
            btnClose.onClick.AddListener(OnClose);
    }

    /// <summary>
    /// 初始化并显示商店
    /// </summary>
    public void ShowShop(PlayerCollection playerData, System.Action onClose)
    {
        _playerData = playerData;
        _onClose = onClose;
        _isShopTab = true;

        InitShopItems();
        InitMedicalServices();
        _soldOutItems.Clear();

        if (canvasRoot != null)
            canvasRoot.SetActive(true);
        else
            gameObject.SetActive(true);

        RefreshDisplay();
        Log("欢迎光临！");
    }

    /// <summary>
    /// 初始化商品列表
    /// </summary>
    private void InitShopItems()
    {
        _shopItems.Clear();
        _shopItems.Add(new ShopItem { ItemId = "icard_001", DisplayName = "治疗药水", Price = 15, Description = "治疗 8 HP" });
        _shopItems.Add(new ShopItem { ItemId = "icard_002", DisplayName = "炸药", Price = 25, Description = "造成 12 伤害" });
        _shopItems.Add(new ShopItem { ItemId = "icard_003", DisplayName = "酒", Price = 10, Description = "获得微醺状态 ATK+5" });
    }

    /// <summary>
    /// 初始化医疗服务
    /// </summary>
    private void InitMedicalServices()
    {
        _medicalServices.Clear();
        _medicalServices.Add(new MedicalService { TargetInjury = InjuryStatus.LightInjury, DisplayName = "轻伤治疗", Price = 20, Description = "治疗所有轻伤角色" });
        _medicalServices.Add(new MedicalService { TargetInjury = InjuryStatus.HeavyInjury, DisplayName = "重伤治疗", Price = 50, Description = "治疗所有重伤角色" });
        _medicalServices.Add(new MedicalService { TargetInjury = InjuryStatus.Disabled, DisplayName = "残疾治疗", Price = 100, Description = "治疗所有残疾角色" });
    }

    /// <summary>
    /// 刷新商店显示
    /// </summary>
    private void RefreshDisplay()
    {
        foreach (var card in _itemCards)
            Destroy(card);
        _itemCards.Clear();

        if (shopItemPrefab == null || shopContainer == null)
        {
            Log("shopItemPrefab 或 shopContainer 未绑定");
            return;
        }

        if (_isShopTab)
            RefreshShopItems();
        else
            RefreshMedicalServices();
    }

    /// <summary>
    /// 显示商品列表（已售罄商品标记为灰色）
    /// </summary>
    private void RefreshShopItems()
    {
        int cols = Mathf.Max(1, cardsPerRow);

        for (int i = 0; i < _shopItems.Count; i++)
        {
            var item = _shopItems[i];
            bool isSoldOut = _soldOutItems.Contains(i);

            string priceText = isSoldOut ? "已售罄" : "价格: " + item.Price + "金币";
            string descText = isSoldOut ? "" : item.Description;
            string goldText = "金币: " + (_playerData?.Gold ?? 0);

            var go = CreateCard("ShopItem_" + item.DisplayName, i, cols,
                item.DisplayName,
                priceText,
                descText,
                goldText);

            // 已售罄：变灰禁用
            if (isSoldOut)
            {
                var canvasGroup = go.GetComponent<CanvasGroup>();
                if (canvasGroup == null) canvasGroup = go.AddComponent<CanvasGroup>();
                canvasGroup.alpha = 0.5f;
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
            }
            else
            {
                int index = i;
                var btns = go.GetComponentsInChildren<Button>();
                foreach (var btn in btns)
                {
                    if (btn.name == "BtnBuy")
                        btn.onClick.AddListener(() => TryBuyItem(index));
                }
            }

            _itemCards.Add(go);
        }

        // 添加"医疗"标签按钮
        AddTabButton("🏥 医疗服务", () => { _isShopTab = false; RefreshDisplay(); }, _shopItems.Count, cols);
    }

    /// <summary>
    /// 显示医疗服务（三个固定按钮，按伤势类型分类）
    /// 批量治疗同一伤势类型的所有角色
    /// </summary>
    private void RefreshMedicalServices()
    {
        int cols = Mathf.Max(1, cardsPerRow);
        int cardIndex = 0;

        foreach (var service in _medicalServices)
        {
            var injuredChars = _playerData.OwnedCharacters
                .Where(c => !c.IsDead && c.Injury == service.TargetInjury)
                .ToList();

            string title = service.DisplayName;
            string desc = service.Description;
            if (injuredChars.Count > 0)
                desc += "\n可治疗: " + string.Join(", ", injuredChars.Select(c => c.DisplayName));
            else
                desc += "\n（无需要治疗的伤员）";

            var go = CreateCard("Medical_" + service.TargetInjury, cardIndex++, cols,
                title,
                "治疗费: " + service.Price + "金币",
                desc,
                "金币: " + (_playerData?.Gold ?? 0));

            InjuryStatus targetInjury = service.TargetInjury;
            int price = service.Price;
            var btns = go.GetComponentsInChildren<Button>();
            foreach (var btn in btns)
            {
                if (btn.name == "BtnBuy")
                    btn.onClick.AddListener(() => TryHealByInjury(targetInjury, price));
            }

            _itemCards.Add(go);
        }

        // 添加"商品"标签按钮
        AddTabButton("🛒 商品", () => { _isShopTab = true; RefreshDisplay(); }, cardIndex, cols);
    }

    /// <summary>
    /// 创建一张卡片
    /// </summary>
    private GameObject CreateCard(string name, int index, int cols, string title, string price, string desc, string gold)
    {
        var go = Instantiate(shopItemPrefab, shopContainer, false);
        go.name = name;
        go.transform.localScale = cardScale;

        int row = index / cols;
        int col = index % cols;

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(
            startPosition.x + col * cardSpacing,
            startPosition.y - row * rowSpacing
        );

        var texts = go.GetComponentsInChildren<Text>();
        foreach (var t in texts)
        {
            if (t.name == "Text_Name") t.text = title;
            else if (t.name == "Text_Price") t.text = price;
            else if (t.name == "Text_Desc") t.text = desc;
            else if (t.name == "Text_Gold") t.text = gold;
        }

        return go;
    }

    /// <summary>
    /// 添加标签切换按钮
    /// </summary>
    private void AddTabButton(string text, UnityEngine.Events.UnityAction onClick, int startIndex, int cols)
    {
        var go = Instantiate(shopItemPrefab, shopContainer, false);
        go.name = "TabButton_" + text;
        go.transform.localScale = cardScale;

        int row = startIndex / cols;
        int col = startIndex % cols;

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(
            startPosition.x + col * cardSpacing,
            startPosition.y - row * rowSpacing
        );

        var texts = go.GetComponentsInChildren<Text>();
        foreach (var t in texts)
        {
            if (t.name == "Text_Name") t.text = text;
            else if (t.name == "Text_Price") t.text = "点击切换";
            else if (t.name == "Text_Desc") t.text = "";
            else if (t.name == "Text_Gold") t.text = "金币: " + (_playerData?.Gold ?? 0);
        }

        var btn = go.GetComponent<Button>();
        if (btn != null)
        {
            btn.onClick.AddListener(onClick);
        }
    }

    /// <summary>
    /// 尝试购买指定商品
    /// 购买后标记为已售罄（不删除），刷新显示
    /// </summary>
    public bool TryBuyItem(int itemIndex)
    {
        if (_playerData == null) return false;
        if (itemIndex < 0 || itemIndex >= _shopItems.Count)
        {
            Log("无效的商品索引");
            return false;
        }

        // 已售罄的不允许再购买
        if (_soldOutItems.Contains(itemIndex))
        {
            Log("该商品已售罄");
            return false;
        }

        var shopItem = _shopItems[itemIndex];

        if (_playerData.Gold < shopItem.Price)
        {
            Log("金币不足！需要 " + shopItem.Price + " 金币");
            return false;
        }

        _playerData.Gold -= shopItem.Price;

        var templateData = DataManager.Instance.GetItemById(shopItem.ItemId);
        if (templateData != null)
        {
            // 创建副本而非引用同一 SO 实例，使多次购买获得独立道具卡
            var newItem = ScriptableObject.CreateInstance<ItemCardData>();
            newItem.CardId = templateData.CardId;
            newItem.DisplayName = templateData.DisplayName;
            newItem.CostTP = templateData.CostTP;
            newItem.TargetType = templateData.TargetType; // 必须复制目标类型，否则购买副本默认 Ally
            newItem.Effects = templateData.Effects; // Effects 是只读数组，共享安全
            _playerData.OwnedItemCards.Add(newItem);
            Log("购买成功！获得 " + shopItem.DisplayName);
        }
        else
        {
            Log("购买失败：未找到道具数据 " + shopItem.ItemId);
            _playerData.Gold += shopItem.Price;
            return false;
        }

        // 标记为已售罄而不是删除（保持布局不变）
        _soldOutItems.Add(itemIndex);
        RefreshDisplay();
        return true;
    }

    /// <summary>
    /// 尝试批量治疗同一伤势类型的所有角色
    /// </summary>
    public bool TryHealByInjury(InjuryStatus targetInjury, int price)
    {
        if (_playerData == null) return false;

        // 找出所有该伤势类型的角色
        var injuredChars = _playerData.OwnedCharacters
            .Where(c => !c.IsDead && c.Injury == targetInjury)
            .ToList();

        if (injuredChars.Count == 0)
        {
            Log("没有需要治疗的 " + GetInjuryLabel(targetInjury) + " 角色");
            return false;
        }

        if (_playerData.Gold < price)
        {
            Log("金币不足！需要 " + price + " 金币");
            return false;
        }

        // 扣除金币
        _playerData.Gold -= price;

        // 批量治疗所有同伤势角色
        foreach (var ci in injuredChars)
        {
            ci.Injury = InjuryStatus.Normal;
        }

        Log("成功治疗 " + injuredChars.Count + " 名 " + GetInjuryLabel(targetInjury) + " 角色！");

        RefreshDisplay();
        return true;
    }

    /// <summary>
    /// 获取伤残标签
    /// </summary>
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

    /// <summary>
    /// 关闭商店
    /// </summary>
    public void OnClose()
    {
        if (canvasRoot != null)
            canvasRoot.SetActive(false);
        else
            gameObject.SetActive(false);

        _onClose?.Invoke();
    }

    private void Log(string msg)
    {
        if (infoText != null)
            infoText.text = msg + "\n" + infoText.text;
    }
}