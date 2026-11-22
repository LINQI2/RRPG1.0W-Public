using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 领地界面
///
/// 负责：领地主界面，显示金币、角色/道具总数
///       提供"领地卡册"检视按钮和"出征"按钮
/// 不负责：卡牌详情显示（由 ExpeditionUI 的检视模式负责）
///
/// 领地是冒险循环的中枢：
///   领地 → 检视卡册（只读查看）→ 退出回到领地
///   领地 → 远征准备（选卡出战）→ 冒险 → 结算 → 回到领地
/// </summary>
public class TownUI : MonoBehaviour
{
    [Header("UI 元素")]
    public GameObject canvasRoot;
    public TMP_Text infoText;
    public TMP_Text goldText;
    public TMP_Text charCountText;
    public TMP_Text itemCountText;

    [Header("按钮")]
    public Button btnViewDeck;    // 领地卡册检视
    public Button btnExpedition;  // 出征

    /// <summary>玩家全局数据</summary>
    private PlayerCollection _playerData;

    /// <summary>玩家数据（供其他 UI 读取，如人物成长界面）</summary>
    public PlayerCollection PlayerData => _playerData;

    /// <summary>远征界面的引用</summary>
    private ExpeditionUI _expeditionUI;

    /// <summary>卡册检视界面的引用</summary>
    private DeckViewerUI _deckViewerUI;

    void Start()
    {
        // Stage V1：确保 Village 运行时存在（旧档/无档以默认状态初始化）。
        VillageRuntimeBridge.EnsureInitialized(null);
        // Stage V1：领地激活 → 显式打开 Village 世界时间模拟（gating，非 HUD 驱动）。
        VillageRuntimeBridge.SetSimulationActive(true);

        // 由 MainMenuUI.Initialize() 传入 PlayerCollection，Start 不再自动初始化
        // 如果 MainMenuUI 未调用 Initialize（测试场景），保持原有逻辑
        if (_playerData == null)
        {
            _playerData = SaveSystem.HasSave() ? SaveSystem.Load() : null;
            if (_playerData == null)
            {
                _playerData = SaveSystem.NewGame();
            }
        }
        // Stage M6：把当前 Village 世界会话的长期人物真相源绑定到桥（Day End Formation Resolver 读取；
        // 只引用同一 PlayerCollection，不复制第二份长期 Person 数据）。
        VillageRuntimeBridge.SetSimulationPersonSource(_playerData);

        if (canvasRoot != null)
            canvasRoot.SetActive(true);

        _expeditionUI = FindObjectOfType<ExpeditionUI>(true);
        _deckViewerUI = FindObjectOfType<DeckViewerUI>(true);

        EnsureCharacterGrowthHost();

        ValidateBindings();
        BindButtons();
        RefreshDisplay();
    }

    /// <summary>
    /// V0.1：确保人物成长宿主存在（场景无固定对象时运行时创建；不依赖 .scene 序列化）。
    /// 人物成长界面由本组件运行时构建，Town 入口按钮同样在运行时注入 TownCanvas。
    /// </summary>
    private void EnsureCharacterGrowthHost()
    {
        if (FindObjectOfType<ProductHomeWorldHost>(true) == null)
            new GameObject("ProductHomeWorld", typeof(ProductHomeWorldHost));
        if (FindObjectOfType<CharacterGrowthUI>(true) == null)
        {
            new GameObject("CharacterGrowthUI", typeof(CharacterGrowthUI));
        }
    }

    /// <summary>
    /// 由 MainMenuUI 调用，传入已加载/新建的 PlayerCollection
    /// 替代 Start() 中的自动初始化逻辑
    /// </summary>
    public void Initialize(PlayerCollection collection)
    {
        _playerData = collection;
        VillageRuntimeBridge.SetSimulationPersonSource(_playerData);

        // Stage V1：确保 Village 运行时存在（加载路径已由 SaveSystem.Load 恢复 Village；此处兜底）。
        VillageRuntimeBridge.EnsureInitialized(null);
        // Stage V1：领地激活 → 显式打开 Village 世界时间模拟（gating，非 HUD 驱动）。
        VillageRuntimeBridge.SetSimulationActive(true);

        if (canvasRoot != null)
            canvasRoot.SetActive(true);

        _expeditionUI = FindObjectOfType<ExpeditionUI>(true);
        _deckViewerUI = FindObjectOfType<DeckViewerUI>(true);

        EnsureCharacterGrowthHost();

        ValidateBindings();
        BindButtons();
        RefreshDisplay();
    }

    void ValidateBindings()
    {
        if (btnViewDeck == null) Debug.LogError("[TownUI] btnViewDeck 未绑定");
        if (btnExpedition == null) Debug.LogError("[TownUI] btnExpedition 未绑定");
    }

    void BindButtons()
    {
        if (btnViewDeck != null)
            btnViewDeck.onClick.AddListener(OnViewDeck);
        if (btnExpedition != null)
            btnExpedition.onClick.AddListener(OnExpedition);
    }

    public void RefreshDisplay()
    {
        if (_playerData == null) return;

        int aliveCount = 0;
        foreach (var c in _playerData.OwnedCharacters)
            if (!c.IsDead) aliveCount++;

        if (goldText != null)
            goldText.text = "金币: " + _playerData.Gold;
        if (charCountText != null)
            charCountText.text = "角色: " + aliveCount + "/" + _playerData.OwnedCharacters.Count;
        if (itemCountText != null)
            itemCountText.text = "道具: " + _playerData.OwnedItemCards.Count;

        // V0.1：人物成长入口红点（有人物可升级时显示提示）
        var growthUI = FindObjectOfType<CharacterGrowthUI>(true);
        if (growthUI != null) growthUI.RefreshTownEntry(_playerData);
    }

    /// <summary>
    /// 领地卡册检视（只读模式）
    /// </summary>
    private void OnViewDeck()
    {
        if (_deckViewerUI != null)
        {
            _deckViewerUI.Show(_playerData);
            VillageRuntimeBridge.SetSimulationActive(false); // 离开领地 → 暂停 Village 世界时间
            if (canvasRoot != null)
                canvasRoot.SetActive(false);
            else
                Log("canvasRoot 未绑定，无法隐藏领地界面");
        }
        else
        {
            Log("未找到 DeckViewerUI 组件");
        }
    }

    /// <summary>
    /// 出征
    /// </summary>
    private void OnExpedition()
    {
        if (_expeditionUI != null)
        {
            _expeditionUI.ShowExpeditionFromTown(_playerData);
            VillageRuntimeBridge.SetSimulationActive(false); // 出征准备/远征中 → 暂停 Village 世界时间（V1 不建模远征耗时）
            if (canvasRoot != null)
                canvasRoot.SetActive(false);
        }
        else
        {
            Log("未找到 ExpeditionUI 组件");
        }
    }

    /// <summary>Product navigation delegates to the established exit-Home path.</summary>
    public void EnterProductExpedition() { OnExpedition(); }

    /// <summary>
    /// 从远征/卡册返回领地。
    /// V4F：返回 bool = 本次最终 Return Save（SaveSystem.Save）是否成功；
    /// Expedition Return 链路用该结果决定 ExpeditionRecord.Completed 是否置位（仅最终保存成功后置 true），
    /// DeckViewer 等既有调用方忽略返回值不受影响。
    /// </summary>
    public bool OnReturnFromExpedition()
    {
        if (!VillageRuntimeBridge.GuardWorldWrite()) return false;
        if (canvasRoot != null)
            canvasRoot.SetActive(true);
        RefreshDisplay();
        bool saveOk = SaveSystem.Save(_playerData);
        VillageRuntimeBridge.SetSimulationPersonSource(_playerData); // M6：回到领地重新绑定当前长期人物真相源
        VillageRuntimeBridge.SetSimulationActive(true); // 回到领地 → 恢复 Village 世界时间
        Log("回到领地");
        return saveOk;
    }

    internal void ShowAfterCompletedExpedition()
    {
        if (VillageRuntimeBridge.ActiveReturnRecord == null || !VillageRuntimeBridge.ActiveReturnRecord.Completed) return;
        if (canvasRoot != null) canvasRoot.SetActive(true);
        RefreshDisplay();
        VillageRuntimeBridge.SetSimulationPersonSource(_playerData);
        VillageRuntimeBridge.SetSimulationActive(true);
        Log("回到领地");
    }

    private void Log(string msg)
    {
        if (infoText != null)
            infoText.text = msg + "\n" + infoText.text;
    }
}
