using TMPro;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 人物长期成长界面（V0.1；正式 UI 宿主来自 Prefab/场景，动态内容运行时生成）。
///
/// 职责：人物列表（等级/经验/可升级红点）→ 人物详情 → 主动逐级升级 → 处理该等级成长节点：
///   - 固定型节点（StarterSlotUnlock / UnlockedPoolExpand / TraitUpgrade）领取后立即生效；
///   - 选择型 CardGrowth 进入三选一/Skip → 选定牌后再处理 Starter（放入空槽 / 替换槽 / Proficiency+1）。
///
/// 模态语义：打开期间为前景模态界面（Canvas sortingOrder 高于 Town 等常规 Canvas），
///   全屏遮罩拦截底层 Raycast；关闭后恢复底层交互（不允许只靠视觉遮挡代替真实交互阻断）。
///
/// UI Authoring 约定（用户确认）：
///   - 正式宿主（Root / Canvas / Panel / Modal Blocker / 标题 / 关闭 / 列表容器 / 详情容器 / 升级区域）
///     由 Prefab 持久化，可在 Edit Mode / Prefab Mode 查看与调整；
///   - 运行时只生成动态内容（人物列表项、详情文本、三选一卡牌、流程按钮）；
///   - 无 Prefab 绑定（测试/兜底）时仍可由本组件运行时构建等价宿主。
///
/// 不负责：战斗、Run 逻辑、正式数值（全部为演示/测试值）。
/// </summary>
public class CharacterGrowthUI : MonoBehaviour
{
    [Header("正式 UI 宿主（Prefab/场景绑定；留空时运行时构建等价宿主）")]
    [Tooltip("GrowthCanvas（ScreenSpaceOverlay + GraphicRaycaster；打开时激活、关闭时隐藏）")]
    public GameObject canvasRoot;

    [Tooltip("Modal Blocker：全屏遮罩（压暗背景 + 拦截底层 Raycast；真实交互阻断，非视觉遮挡）")]
    public GameObject maskDim;

    [Tooltip("人物列表容器（动态列表项生成于此处）")]
    public RectTransform listRoot;

    [Tooltip("人物详情容器（动态详情/节点流程生成于此处）")]
    public RectTransform detailRoot;

    [Tooltip("持久消息区文本（升级结果 / 节点流程提示）")]
    public TMP_Text infoText;

    [Tooltip("升级按钮宿主（成长/升级主要操作区域；由 ShowDetail 更新状态与监听）")]
    public Button levelUpButton;

    [Header("数据（留空自动从 TownUI/SaveSystem 获取）")]
    [Tooltip("长期玩家数据")]
    public PlayerCollection playerData;

    [Tooltip("UI 字体；留空时从场景已有 TMP_Text 复制")]
    public TMP_FontAsset uiFont;

    [Header("画布布局（Inspector 可调）")]
    [Tooltip("模态前景 sortingOrder：高于 Town 等常规 Canvas，保证遮罩拦截底层点击")]
    public int canvasSortingOrder = 100;
    public Vector2 canvasReferenceResolution = new Vector2(1600, 900);
    public Color backgroundDim = new Color(0f, 0f, 0f, 0.88f);
    public Vector2 panelSize = new Vector2(1440, 780);
    public Vector2 titlePosition = new Vector2(0, 340);
    public Vector2 closeButtonPosition = new Vector2(660, 340);
    public Vector2 listStartPosition = new Vector2(-580, 240);
    public Vector2 listCardSize = new Vector2(300, 70);
    public float listSpacingY = 84f;
    public Vector2 detailPosition = new Vector2(180, 120);
    public Vector2 detailSize = new Vector2(760, 520);

    [Header("可升级红点")]
    public Color redDotColor = new Color(1f, 0.25f, 0.25f, 1f);
    public float redDotSize = 20f;

    [Header("Town 入口按钮（运行时注入 TownCanvas，所有位置 Inspector 可调）")]
    public Vector2 townEntryPosition = new Vector2(300, 170);
    public Vector2 townEntrySize = new Vector2(180, 60);

    // ---------------- 运行时状态 ----------------
    private RectTransform _contentRoot;
    private bool _hostReady;
    private GameObject _townEntryButton;
    private RectTransform _townEntryDot;

    private readonly List<IPersonGrowth> _persons = new List<IPersonGrowth>();
    private IPersonGrowth _selected;
    private List<BattleCardData> _allTemplates = new List<BattleCardData>();

    // CardGrowth 交互流程状态
    private GrowthNodeConfig _pendingNode;
    private readonly List<BattleCardData> _pendingOffer = new List<BattleCardData>();
    private string _pendingCardId;

    private void Start()
    {
        if (playerData == null)
        {
            var town = FindObjectOfType<TownUI>(true);
            if (town != null) playerData = town.PlayerData;
        }
        if (playerData == null)
            playerData = SaveSystem.HasSave() ? SaveSystem.Load() : SaveSystem.NewGame();

        if (uiFont == null)
        {
            var anyText = FindObjectOfType<TMP_Text>(true);
            if (anyText != null) uiFont = anyText.font;
        }

        GrowthConfigProvider.EnsureLoaded();
        _allTemplates = DataManager.Instance != null && DataManager.Instance.AllBattleCards != null
            ? new List<BattleCardData>(DataManager.Instance.AllBattleCards)
            : new List<BattleCardData>();

        EnsureHostReady();
        EnsureTownEntry();
        RefreshTownEntry(playerData);
        Hide();
    }

    /// <summary>
    /// 正式宿主就绪：优先使用 Prefab/场景绑定的静态宿主（长期 UI 规则：正式核心 UI 必须
    /// Edit Mode / Prefab Mode 序列化可调，Root/Canvas/Panel/Blocker/关闭/列表容器/详情容器
    /// 由 Assets/Prefabs/UI/CharacterGrowthUI.prefab 提供）。
    /// 仅当宿主字段未绑定（测试/兜底场景）时才运行时构建等价宿主，并输出明确警示——
    /// 该路径不应用于正式产品流程。
    /// </summary>
    private void EnsureHostReady()
    {
        if (_hostReady) return;

        if (canvasRoot == null || listRoot == null || detailRoot == null)
        {
            Debug.LogWarning("[CharacterGrowthUI] 未检测到 Edit Mode 序列化的正式宿主"
                + "（canvasRoot/listRoot/detailRoot 未绑定）。"
                + " 已按运行时兜底构建等价宿主；正式 UI 宿主请在场景中实例化"
                + " Assets/Prefabs/UI/CharacterGrowthUI.prefab 并保持字段绑定。");
            BuildCanvas(); // 仅测试/无宿主兜底，不应用于正式产品路径
        }

        if (canvasRoot != null)
        {
            var cv = canvasRoot.GetComponent<Canvas>();
            if (cv != null) cv.sortingOrder = canvasSortingOrder;
            _contentRoot = canvasRoot.transform.Find("Content") as RectTransform;
        }

        BindCloseButton(); // 关闭按钮统一绑定 Hide（Prefab 静态宿主也生效）

        _hostReady = true;
    }

    /// <summary>
    /// 关闭按钮绑定 Hide：Prefab/运行时兜底两路都保证 BtnClose 可关闭模态界面。
    /// 幂等：先清空再重绑。
    /// </summary>
    private void BindCloseButton()
    {
        if (canvasRoot == null) return;
        var btnClose = canvasRoot.transform.Find("Content/BtnClose");
        if (btnClose == null) return;
        var btn = btnClose.GetComponent<Button>();
        if (btn == null) return;
        btn.onClick.RemoveAllListeners();
        btn.onClick.AddListener(Hide);
    }

    // ==================== 对外入口 ====================

    public void Open()
    {
        if (playerData == null)
        {
            var town = FindObjectOfType<TownUI>(true);
            if (town != null) playerData = town.PlayerData;
        }
        if (playerData == null)
            playerData = SaveSystem.HasSave() ? SaveSystem.Load() : SaveSystem.NewGame();

        EnsureHostReady();
        _selected = null;
        ClearFlow();
        if (canvasRoot != null) canvasRoot.SetActive(true);
        Refresh();
    }

    public void Hide()
    {
        if (canvasRoot != null) canvasRoot.SetActive(false);
    }

    /// <summary>
    /// Town 入口红点刷新（TownUI.RefreshDisplay 调用）：有人物可升级时显示红点。
    /// </summary>
    public void RefreshTownEntry(PlayerCollection collection)
    {
        if (collection != null) playerData = collection;
        if (_townEntryDot == null) return;
        if (playerData == null || GrowthConfigProvider.Curve == null)
        {
            _townEntryDot.gameObject.SetActive(false);
            return;
        }

        bool any = false;
        foreach (var p in GetAllPersons())
        {
            if (CharacterGrowthService.CanLevelUp(p, GrowthConfigProvider.Curve)) { any = true; break; }
        }
        _townEntryDot.gameObject.SetActive(any);
    }

    /// <summary>重新构建人物列表与详情显示</summary>
    public void Refresh()
    {
        RefreshList();
        if (_selected != null) ShowDetail(_selected);
        RefreshTownEntry(playerData); // 升级后 Town 入口红点即时同步
    }

    // ==================== 人物数据 ====================

    private List<IPersonGrowth> GetAllPersons()
    {
        _persons.Clear();
        if (playerData == null) return _persons;

        if (playerData.OwnedHeroes != null)
        {
            foreach (var h in playerData.OwnedHeroes)
                if (h != null) _persons.Add(h);
        }
        if (playerData.OwnedCharacters != null)
        {
            foreach (var c in playerData.OwnedCharacters)
                if (c != null && !c.IsDead) _persons.Add(c);
        }
        return _persons;
    }

    // ==================== 运行时兜底宿主构建 ====================
    // 仅当 Prefab/场景未绑定正式宿主时使用（测试/兜底）；正式宿主结构与此等价。

    private void BuildCanvas()
    {
        if (canvasRoot != null) return;

        var canvasGo = NewRect("GrowthCanvas", transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            Vector2.zero, canvasReferenceResolution, false);
        canvasRoot = canvasGo.gameObject;
        var cv = canvasGo.gameObject.AddComponent<Canvas>();
        cv.renderMode = RenderMode.ScreenSpaceOverlay;
        cv.sortingOrder = canvasSortingOrder; // 模态前景：高于 Town
        var scaler = canvasGo.gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = canvasReferenceResolution;
        canvasGo.gameObject.AddComponent<GraphicRaycaster>();

        // 全屏遮罩（Modal Blocker）：铺满 Canvas 并拦截点击穿透
        var maskGo = NewRect("Mask_Dim", canvasGo, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, false);
        var maskImg = maskGo.gameObject.AddComponent<Image>();
        maskImg.color = backgroundDim;
        maskImg.raycastTarget = true;
        maskGo.SetAsFirstSibling();
        maskDim = maskGo.gameObject;

        _contentRoot = NewRect("Content", canvasGo, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            Vector2.zero, panelSize, false);
        NewImage(_contentRoot, "Panel_Bg", new Color(0.12f, 0.12f, 0.14f, 0.96f),
            Vector2.zero, panelSize, true);

        NewText(_contentRoot, "Title", "人物成长", 40, Color.white, titlePosition, new Vector2(500, 60), false);
        NewButton(_contentRoot, "BtnClose", "关闭", closeButtonPosition, new Vector2(140, 60),
            new Color(0.3f, 0.3f, 0.35f, 1f), () => Hide(), false);

        // 左侧：人物列表容器
        listRoot = NewRect("ListRoot", _contentRoot, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            listStartPosition, new Vector2(listCardSize.x + 40, detailSize.y + 40), false);
        NewText(listRoot, "ListTitle", "人物列表", 24, Color.white,
            new Vector2(0, listCardSize.y * 0.5f + 30), new Vector2(300, 40), false);

        // 右侧：详情容器 + 节点流程
        detailRoot = NewRect("DetailRoot", _contentRoot, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            detailPosition, detailSize, false);
        NewText(detailRoot, "DetailTitle", "人物详情", 24, Color.white,
            new Vector2(0, detailSize.y * 0.5f - 30), new Vector2(300, 40), false);

        // 持久消息区（升级结果 / 节点流程提示；不被列表/详情重建清除）
        infoText = NewText(detailRoot, "InfoText", "", 16, new Color(1f, 0.9f, 0.4f, 1f),
            new Vector2(0, -260), new Vector2(detailSize.x - 80, 120), false);
    }

    private void RefreshList()
    {
        if (listRoot == null) return;
        DestroyListItems();

        var persons = GetAllPersons();
        for (int i = 0; i < persons.Count; i++)
        {
            var p = persons[i];
            Vector2 pos = new Vector2(0, listStartPosition.y - 60 - i * listSpacingY);
            var card = (RectTransform)NewButton(listRoot, "CharCard_" + p.PersonId, "", pos, listCardSize,
                _selected == p ? new Color(0.25f, 0.4f, 0.25f, 1f) : new Color(0.2f, 0.2f, 0.25f, 1f),
                () => { _selected = p; ClearFlow(); Refresh(); }).transform;

            bool can = CharacterGrowthService.CanLevelUp(p, GrowthConfigProvider.Curve);
            IGrowthAuthority auth = GrowthAuthority.Resolve(p);
            bool unavailable = auth == null || !auth.IsAvailable; // 普通 Character 无 Active Profession
            string nameText = unavailable
                ? p.DisplayName + "（未形成职业）"
                : p.DisplayName + "  Lv." + auth.Level;
            NewText(card, "Text_Name", nameText, 22, Color.white, new Vector2(-40, 12), new Vector2(260, 30));
            string expText = unavailable
                ? "未形成职业，暂不可成长"
                : "EXP " + auth.Exp + " / " + CharacterGrowthService.GetExpRequired(p, GrowthConfigProvider.Curve);
            NewText(card, "Text_Exp", expText, 16, can ? Color.yellow : Color.gray,
                new Vector2(-40, -18), new Vector2(260, 26));

            if (can)
                NewImage(card, "RedDot", redDotColor, new Vector2(listCardSize.x * 0.5f - 18, listCardSize.y * 0.5f - 18),
                    new Vector2(redDotSize, redDotSize), false);
        }
    }

    private void ShowDetail(IPersonGrowth p)
    {
        if (detailRoot == null || p == null) return;
        DestroyDetailContent();

        IGrowthAuthority auth = GrowthAuthority.Resolve(p);
        bool can = CharacterGrowthService.CanLevelUp(p, GrowthConfigProvider.Curve);
        bool unavailable = auth == null || !auth.IsAvailable; // 普通 Character 无 Active Profession

        NewText(detailRoot, "D_Name", p.DisplayName + "（" + p.PersonId + "）", 28, Color.white,
            new Vector2(0, detailSize.y * 0.5f - 90), new Vector2(600, 40));

        string traitName = "无";
        if (auth != null && auth.IsProfessionGrowth)
        {
            // M4-correction：普通 Character（有 Active Profession）——Trait 行只显示职业特性强化数值状态
            //（无正式 Profession Trait Definition，仅数字对齐）。
            traitName = "职业特性 Lv." + auth.TraitLevel;
        }
        else
        {
            var trait = GetTrait(p);
            if (trait != null) traitName = trait.DisplayName + "  Lv." + p.TraitLevel;
        }

        string poolNames = auth != null ? BuildPoolNames(auth.UnlockedPoolCardIds) : "（无）";
        string starterText = BuildStarterNames(auth != null ? auth.StarterSlots : p.StarterSlots);

        string info;
        if (unavailable)
        {
            // M4-correction：无 Profession 的普通 Character 不可执行 Profession Growth，也不 fallback 到旧 Level/Exp。
            // Starter 槽仍 Character-owned（Starter 数据保留展示）。
            info =
                "未形成职业，暂不可执行成长\n" +
                "（正式形成 Profession 后，等级 / 经验 / 牌池 / 职业特性才开放）\n\n" +
                "Starter 槽（每个槽 = 下次 Run 起始 1 张副本）:\n" + starterText;
        }
        else
        {
            info =
                "等级: " + auth.Level + "\n" +
                "经验: " + auth.Exp + " / " + CharacterGrowthService.GetExpRequired(p, GrowthConfigProvider.Curve) + "\n" +
                "Trait: " + traitName + "\n" +
                "Unlocked Pool(" + (auth.UnlockedPoolCardIds != null ? auth.UnlockedPoolCardIds.Count : 0) + "): " + poolNames + "\n\n" +
                "Starter 槽（每个槽 = 下次 Run 起始 1 张副本）:\n" + starterText;
        }

        NewText(detailRoot, "D_Info", info, 18, Color.white, new Vector2(0, 60), new Vector2(detailSize.x - 80, 300));

        // 升级按钮：优先复用正式宿主（Prefab/场景可调）；未绑定时动态创建（兜底）
        string buttonLabel = can ? "升级" : (unavailable ? "未形成职业" : "经验不足");
        if (levelUpButton != null)
        {
            levelUpButton.gameObject.SetActive(true);
            levelUpButton.interactable = can;
            levelUpButton.onClick.RemoveAllListeners();
            levelUpButton.onClick.AddListener(OnLevelUpClick);
            var label = levelUpButton.transform.Find("Label");
            if (label != null)
            {
                var t = label.GetComponent<TMP_Text>();
                if (t != null) t.text = buttonLabel;
            }
        }
        else
        {
            var btn = NewButton(detailRoot, "BtnLevelUp", buttonLabel,
                new Vector2(0, -170), new Vector2(220, 70), can ? new Color(0.3f, 0.5f, 0.3f, 1f) : new Color(0.3f, 0.3f, 0.3f, 1f),
                () => OnLevelUpClick());
            btn.interactable = can;
        }

        if (infoText != null) infoText.text = "";
    }

    private TraitData GetTrait(IPersonGrowth p)
    {
        if (string.IsNullOrEmpty(p.TraitId)) return null;
        var catalog = GrowthConfigProvider.TraitCatalog;
        if (catalog != null && catalog.DefaultTraits != null)
        {
            foreach (var e in catalog.DefaultTraits)
                if (e != null && e.Trait != null && e.Trait.TraitId == p.TraitId) return e.Trait;
        }
        return null;
    }

    private string BuildStarterNames(List<StarterSlotEntry> slots)
    {
        if (slots == null || slots.Count == 0) return "（无 Starter 槽）";
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < slots.Count; i++)
        {
            var s = slots[i];
            string cardName = "（空）";
            if (s != null && !string.IsNullOrEmpty(s.CardId))
                cardName = CardNameById(s.CardId) + " 熟练" + s.Proficiency;
            sb.Append("  [槽").Append(i).Append("] ").Append(cardName);
        }
        return sb.ToString();
    }

    private string BuildPoolNames(List<string> pool)
    {
        if (pool == null || pool.Count == 0) return "（空）";
        var names = new List<string>();
        foreach (var id in pool)
            names.Add(CardNameById(id));
        return string.Join("、", names);
    }

    private string CardNameById(string cardId)
    {
        if (string.IsNullOrEmpty(cardId)) return "?";
        foreach (var t in _allTemplates)
            if (t != null && t.CardId == cardId) return t.DisplayName ?? cardId;
        return cardId;
    }

    // ==================== 升级 / 节点流程 ====================

    private void OnLevelUpClick()
    {
        if (!VillageRuntimeBridge.GuardWorldWrite()) { ShowMessage("世界正在推进或等待返程保存。"); return; }
        if (_selected == null) return;
        var curve = GrowthConfigProvider.Curve;
        var table = GrowthConfigProvider.Table;

        if (!CharacterGrowthService.TryLevelUpOnce(_selected, curve, table, out var node))
        {
            ShowMessage("经验不足，无法升级");
            return;
        }

        IGrowthAuthority auth = GrowthAuthority.Resolve(_selected);
        string baseMsg = "已升级到 Lv." + (auth != null ? auth.Level : _selected.Level) + "（消耗经验）";

        if (node == null)
        {
            ShowMessage(baseMsg + " ｜ 该等级暂无成长节点");
            SaveSystem.Save(playerData);
            Refresh();
            return;
        }

        if (node.IsCardGrowth)
        {
            _pendingNode = node;
            Refresh(); // 升级后的等级/经验先刷新显示
            ShowMessage(baseMsg + " ｜ 该等级为「牌成长」，请选择一张牌或跳过");
            BeginCardGrowthOffer(_selected);
            return;
        }

        // 固定型节点：领取后立即生效
        var result = CharacterGrowthService.ApplyFixedNode(_selected, node, GrowthConfigProvider.TraitCatalog);
        SaveSystem.Save(playerData);
        Refresh();
        ShowMessage(baseMsg + " ｜ " + result.Message
            + "（已领取 Lv." + node.Level + " 成长节点：" + NodeTypeName(node.NodeType) + "）");
    }

    private void BeginCardGrowthOffer(IPersonGrowth p)
    {
        if (!VillageRuntimeBridge.GuardWorldWrite()) { ShowMessage("世界正在推进或等待返程保存。"); return; }
        _pendingOffer.Clear();
        _pendingOffer.AddRange(CardGrowthResolver.BuildOffer(p, _allTemplates));

        DestroyFlowObjects();

        if (_pendingOffer.Count == 0)
        {
            ShowMessage("Unlocked Pool 为空，本次成长选牌自动跳过");
            _pendingNode = null;
            SaveSystem.Save(playerData);
            Refresh();
            return;
        }

        NewText(detailRoot, "F_Title", "Lv." + _pendingNode.Level + " 成长节点：选择一张牌（或跳过）", 20,
            Color.white, new Vector2(0, -130), new Vector2(detailSize.x - 80, 30));

        float x0 = -290f;
        float spacing = 240f;
        for (int i = 0; i < _pendingOffer.Count && i < 3; i++)
        {
            var card = _pendingOffer[i];
            int idx = i;
            NewButton(detailRoot, "F_Card" + i, (card.DisplayName ?? card.CardId) + "\n" + (card.Description ?? ""),
                new Vector2(x0 + i * spacing, -205), new Vector2(200, 100), new Color(0.25f, 0.35f, 0.5f, 1f),
                () => OnOfferPick(idx));
        }

        NewButton(detailRoot, "F_Skip", "Skip", new Vector2(290, -215), new Vector2(120, 48),
            new Color(0.35f, 0.35f, 0.35f, 1f), OnOfferSkip);
    }

    private void OnOfferPick(int index)
    {
        if (!VillageRuntimeBridge.GuardWorldWrite()) { ShowMessage("世界正在推进或等待返程保存。"); return; }
        if (index < 0 || index >= _pendingOffer.Count) return;
        var card = _pendingOffer[index];
        _pendingCardId = card.CardId;
        ShowMessage("已选牌：" + (card.DisplayName ?? card.CardId) + " —— 请选择 Starter 处理方式");
        ShowStarterStrategy(_pendingCardId);
    }

    private void OnOfferSkip()
    {
        if (!VillageRuntimeBridge.GuardWorldWrite()) { ShowMessage("世界正在推进或等待返程保存。"); return; }
        _pendingNode = null;
        _pendingOffer.Clear();
        _pendingCardId = null;
        ShowMessage("已跳过本次成长选牌（等级已保留，经验已消耗）");
        SaveSystem.Save(playerData);
        Refresh();
    }

    private void ShowStarterStrategy(string cardId)
    {
        if (_selected == null) return;
        DestroyFlowObjects();

        var p = _selected;
        var curve = GrowthConfigProvider.Curve;
        IGrowthAuthority strategyAuth = GrowthAuthority.Resolve(p);
        List<StarterSlotEntry> starterSlots = strategyAuth != null ? strategyAuth.StarterSlots : p.StarterSlots;
        var strategies = new List<System.Tuple<string, StarterCardStrategy, int>>();

        bool canProficiency = false;
        if (starterSlots != null)
        {
            foreach (var s in starterSlots)
            {
                if (s != null && s.CardId == cardId)
                {
                    int cap = curve != null && curve.ProficiencyCap > 0 ? curve.ProficiencyCap : int.MaxValue;
                    if (s.Proficiency < cap) { canProficiency = true; break; }
                }
            }
        }
        if (canProficiency)
            strategies.Add(new System.Tuple<string, StarterCardStrategy, int>("熟练 +1（同模板）", StarterCardStrategy.ProficiencyUp, -1));

        if (starterSlots != null)
        {
            for (int i = 0; i < starterSlots.Count; i++)
            {
                if (starterSlots[i] == null || starterSlots[i].IsEmpty)
                    strategies.Add(new System.Tuple<string, StarterCardStrategy, int>("放入空槽 " + i, StarterCardStrategy.AddToEmptySlot, i));
                strategies.Add(new System.Tuple<string, StarterCardStrategy, int>("替换槽 " + i, StarterCardStrategy.ReplaceSlot, i));
            }
        }

        NewText(detailRoot, "F_Title", "Starter 处理（" + CardNameById(cardId) + "）", 20,
            Color.white, new Vector2(0, -130), new Vector2(detailSize.x - 80, 30));

        float y = -170f;
        for (int i = 0; i < strategies.Count; i++)
        {
            var s = strategies[i];
            int captured = i;
            NewButton(detailRoot, "F_Strat" + i, s.Item1, new Vector2(0, y - i * 52), new Vector2(300, 44),
                new Color(0.25f, 0.45f, 0.35f, 1f),
                () => OnStrategyPick(strategies[captured].Item2, strategies[captured].Item3));
        }

        NewButton(detailRoot, "F_Back", "返回选牌", new Vector2(0, y - strategies.Count * 52 - 40), new Vector2(140, 44),
            new Color(0.35f, 0.35f, 0.35f, 1f), () => { if (_pendingNode != null) BeginCardGrowthOffer(p); });
    }

    private void OnStrategyPick(StarterCardStrategy strategy, int slotIndex)
    {
        if (!VillageRuntimeBridge.GuardWorldWrite()) { ShowMessage("世界正在推进或等待返程保存。"); return; }
        if (_selected == null || string.IsNullOrEmpty(_pendingCardId)) return;
        var result = CardGrowthResolver.ApplyToStarter(_selected, _pendingCardId, strategy, slotIndex,
            GrowthConfigProvider.Curve);

        _pendingNode = null;
        _pendingOffer.Clear();
        _pendingCardId = null;
        ShowMessage(result.Message + " ｜ 成长节点已处理");
        SaveSystem.Save(playerData);
        Refresh();
    }

    private void ShowMessage(string msg)
    {
        if (infoText != null) infoText.text = msg;
    }

    private string NodeTypeName(GrowthNodeType type)
    {
        switch (type)
        {
            case GrowthNodeType.CardGrowth: return "牌成长";
            case GrowthNodeType.StarterSlotUnlock: return "Starter 槽解锁";
            case GrowthNodeType.TraitUpgrade: return "Trait 强化";
            case GrowthNodeType.UnlockedPoolExpand: return "牌池扩展";
            default: return type.ToString();
        }
    }

    // ==================== Town 入口 ====================

    /// <summary>
    /// 人物成长固定入口按钮绑定。
    /// 长期 UI 规则：正式核心入口按钮必须在 Edit Mode / Prefab Mode 序列化可调，
    /// Runtime 仅允许动态重复内容。因此本方法【只绑定既有序列化按钮】，不再 Runtime 创建。
    /// 场景缺失时输出明确错误（由 Edit Mode 配置补上），保证入口按钮可被直接选中并调整视觉。
    /// </summary>
    private void EnsureTownEntry()
    {
        if (_townEntryButton != null) return;

        var town = FindObjectOfType<TownUI>(true);
        var parent = town != null && town.canvasRoot != null ? town.canvasRoot.transform : null;
        if (parent == null)
        {
            Debug.LogError("[CharacterGrowthUI] 未找到 TownUI.canvasRoot，无法绑定人物成长入口按钮。");
            return;
        }

        var existing = parent.Find("BtnCharacterGrowth");
        if (existing == null)
        {
            Debug.LogError("[CharacterGrowthUI] TownCanvas 缺少序列化的 BtnCharacterGrowth。"
                + " 请按长期 UI 规则在 Edit Mode 配置该固定入口按钮（含 Label 子文本与 RedDot 红点子对象）。");
            return;
        }

        _townEntryButton = existing.gameObject;
        var dot = existing.Find("RedDot");
        if (dot != null) _townEntryDot = dot as RectTransform;

        // 点击回调运行时绑定到既有按钮（幂等），打开人物成长界面。
        var btn = existing.GetComponent<Button>();
        if (btn != null)
        {
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(Open);
        }
    }

    // ==================== 基础 UI 辅助 ====================

    private RectTransform NewRect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax,
        Vector2 anchoredPos, Vector2 size, bool track = true)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = anchoredPos;
        // track 参数保留（静态宿主传 false）；动态对象按容器/名称清理，不再统一跟踪
        return rt;
    }

    private Image NewImage(RectTransform parent, string name, Color color, Vector2 pos, Vector2 size, bool raycast)
    {
        var rt = NewRect(name, parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, size);
        var img = rt.gameObject.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = raycast;
        return img;
    }

    private TMP_Text NewText(RectTransform parent, string name, string content, int fontSize, Color color,
        Vector2 pos, Vector2 size, bool track = true)
    {
        var rt = NewRect(name, parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, size, track);
        var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        t.text = content;
        t.font = CurrentUIFontAuthority.Default;
        t.fontSize = fontSize;
        t.color = color;
        t.alignment = TextAlignmentOptions.Left;
        t.enableWordWrapping = true;
        t.overflowMode = TextOverflowModes.Overflow;
        return t;
    }

    private Button NewButton(RectTransform parent, string name, string label, Vector2 pos, Vector2 size,
        Color color, UnityEngine.Events.UnityAction onClick, bool track = true)
    {
        var rt = NewRect(name, parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, size, track);
        var img = rt.gameObject.AddComponent<Image>();
        img.color = color;
        var btn = rt.gameObject.AddComponent<Button>();
        btn.targetGraphic = img;
        if (onClick != null) btn.onClick.AddListener(onClick);

        if (!string.IsNullOrEmpty(label))
        {
            var labelRt = NewRect("Label", rt, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var t = labelRt.gameObject.AddComponent<TextMeshProUGUI>();
            t.text = label;
            t.font = CurrentUIFontAuthority.Default;
            t.fontSize = 20;
            t.color = Color.white;
            t.alignment = TextAlignmentOptions.Center;
            t.enableWordWrapping = true;
            t.overflowMode = TextOverflowModes.Overflow;
        }
        return btn;
    }

    /// <summary>按名称前缀销毁某容器下的动态子对象（保留正式宿主）</summary>
    private void DestroyChildrenNamed(RectTransform root, System.Predicate<string> matchName)
    {
        if (root == null) return;
        for (int i = root.childCount - 1; i >= 0; i--)
        {
            var child = root.GetChild(i);
            if (child != null && matchName(child.name))
                Destroy(child.gameObject);
        }
    }

    /// <summary>清理人物列表项（CharCard_*）</summary>
    private void DestroyListItems()
    {
        DestroyChildrenNamed(listRoot, n => n.StartsWith("CharCard_"));
    }

    /// <summary>清理详情动态内容（D_* 名称/信息文本 与 F_* 节点流程）</summary>
    private void DestroyDetailContent()
    {
        DestroyChildrenNamed(detailRoot, n => n.StartsWith("D_") || n.StartsWith("F_"));
    }

    /// <summary>
    /// 清理节点流程对象（F_*）。静态宿主（升级按钮等）只隐藏不销毁；
    /// 流程结束后由 ShowDetail 重新显示并刷新状态。
    /// </summary>
    private void DestroyFlowObjects()
    {
        DestroyChildrenNamed(detailRoot, n => n.StartsWith("F_"));
        if (levelUpButton != null) levelUpButton.gameObject.SetActive(false);
    }

    private void ClearFlow()
    {
        _pendingNode = null;
        _pendingOffer.Clear();
        _pendingCardId = null;
    }
}








