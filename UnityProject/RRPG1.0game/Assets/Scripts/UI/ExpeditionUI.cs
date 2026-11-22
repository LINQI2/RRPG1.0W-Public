using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Linq;
using RRPG.Encounter;
using RRPG.Event;
using RRPG.StaticConfig;

/// <summary>
/// 远征准备界面
///
/// 负责：显示远征准备的 UI，包括英雄展示、角色卡选择、确认出征
/// 不负责：战斗流程
///
/// 视觉布局（用户可在 Unity Inspector 中调整）：
///   heroPanel — 英雄展示区（只读）
///   cardPanel — 角色卡区域
///   btnCharTab — 角色卡标签（当前唯一标签）
///   selectedSummaryText — 当前选择摘要
///   btnConfirm — 确认出征按钮
///
/// 操作流程：
///   1. 英雄区只读展示当前英雄信息
///   2. 展示角色卡列表（点击选/取消）
///   3. 点击确认出征 → 进入战斗
///
/// Battle V0.1 / WP-C：Prep 不再存在独立道具卡（Item Card）选择；
/// `btnItemTab` 仅为 Legacy 序列化引用（旧场景对象），运行时清除，待 Unity Editor 物理清理。
/// </summary>
public class ExpeditionUI : MonoBehaviour
{
    [Header("预制体")]
    public GameObject charCardPrefab;   // 角色卡（PrepCard）

    [Header("Person Visual Binding V0.1（Prep 视觉；配置唯一来源 = PersonVisualBindingCatalog 资产）")]
    [Tooltip("人物卡缺配置 / 缺素材时的 placeholder（未绑定时保留 prefab 自带占位图，不影响选人）")]
    public Sprite prepCardBodyPlaceholder;

    [Header("UI 容器")]
    public RectTransform heroPanel;
    public RectTransform cardPanel;  // 角色卡区域

    [Header("战斗界面的 Canvas（启动时隐藏，出征后显示）")]
    public GameObject battleCanvas;

    [Header("标签按钮")]
    public Button btnCharTab;   // 角色卡标签

    [Header("Legacy 序列化引用（已退出正式产品路径，仅用于运行时清除旧 UI；待 Unity Editor 清理）")]
    [Tooltip("旧「道具」Tab：WP-C 起 Prep 不再存在独立 Item Card 选择，运行时会被清除")]
    public Button btnItemTab;

    [Header("信息文本")]
    public TMP_Text infoText;
    public TMP_Text selectedSummaryText;

    [Header("按钮")]
    public Button btnConfirm;
    public Button btnReturn;   // 返回领地按钮（从领地进入远征准备时可返回）

    [Header("卡牌显示偏移（排版用）")]
    public float cardSpacing = 200f;
    public Vector2 cardStartPosition = new Vector2(-400, 0);
    public Vector2 heroCardStartPosition = new Vector2(-400, 0);  // 英雄卡单独位置

    [Header("卡牌排列")]
    public Vector3 cardScale = new Vector3(1f, 1f, 1f);
    public Vector3 heroCardScale = new Vector3(1f, 1f, 1f);  // 英雄卡单独缩放
    public int cardsPerRow = 4;         // 每行几张，超了自动换行
    public float rowSpacing = 250f;     // 行间距

    [Header("Compatibility Graph 来源（临时生成参数，不是地图 UI 布局）")]
    [Tooltip("当前兼容图行数；正式内容图来源迁移前由出征宿主显式配置")]
    [SerializeField, Min(2)] private int compatibilityGraphRows = 4;
    [Tooltip("当前兼容图每行节点数；不从旧 MapUI 读取规则初始化配置")]
    [SerializeField, Min(1)] private int compatibilityGraphColumns = 3;

    // 运行时数据
    private PlayerCollection _playerData;
    private ExpeditionSystem _expedition;

    // 缓存 UI 对象
    private Dictionary<string, GameObject> _heroCards = new Dictionary<string, GameObject>();
    private Dictionary<string, GameObject> _charCards = new Dictionary<string, GameObject>();

    // 高亮状态
    private HashSet<string> _selectedCharCardNames = new HashSet<string>();

    // 模式控制
    private bool _fromTown = false;
    private TownUI _townUI;

    /// <summary>
    /// M1 修复：成功确认后置位，阻止同一物理点击内的重复 listener / 重入再次执行 ConfirmExpedition。
    /// 此时本次远征已被正式登记为 Expedition Occupancy，重复确认会把自己误判为“既有占用”。
    /// 失败路径不置位（玩家解除真实占用后可再次确认）。
    /// </summary>
    private bool _confirmInFlight;

    /// <summary>
    /// 重置 UI 状态（每次显示前调用）
    /// </summary>
    private void ResetUIState()
    {
        _selectedCharCardNames.Clear();
    }

    void Start()
    {
        // Start 模式下直接初始化为默认玩家集合（无领地时使用）
        if (_playerData == null)
        {
            _playerData = new PlayerCollection();
            _playerData.InitializeDefaultCollection();
        }
        if (_expedition == null)
            _expedition = new ExpeditionSystem(_playerData);

        // 启动时隐藏战斗 Canvas
        if (battleCanvas != null)
            battleCanvas.SetActive(false);

        RemoveLegacyItemTabUI();
        ValidateBindings();
        BindButtons();
        ShowExpeditionUI();
    }

    /// <summary>
    /// WP-C：Prep 不再存在独立 Item Card 选择。旧场景中的「道具」Tab 若是序列化引用仍存在，
    /// 在此清除其 GameObject，保证旧 Item 选择 UI 不进入正式产品路径。
    /// 序列化资产本身仍待 Unity Editor 物理清理（本任务无 MCP 通道）。
    /// </summary>
    private void RemoveLegacyItemTabUI()
    {
        if (btnItemTab == null) return;
        btnItemTab.onClick.RemoveAllListeners();
        Debug.LogWarning("[ExpeditionUI] WP-C：旧「道具」Tab（Legacy Item Selection 入口）已从正式产品路径移除。");
        // 运行中用 Destroy，EditMode / Editor 上下文用 DestroyImmediate（EditMode 下 Destroy 会被引擎拒绝）
        if (Application.isPlaying) Destroy(btnItemTab.gameObject);
        else DestroyImmediate(btnItemTab.gameObject);
        btnItemTab = null;
    }

    void ValidateBindings()
    {
        if (charCardPrefab == null) Debug.LogError("[ExpeditionUI] charCardPrefab 未设置");
        if (heroPanel == null) Debug.LogError("[ExpeditionUI] heroPanel 未绑定");
        if (cardPanel == null) Debug.LogError("[ExpeditionUI] cardPanel 未绑定");
        if (btnConfirm == null) Debug.LogError("[ExpeditionUI] btnConfirm 未绑定");
    }

    void BindButtons()
    {
        // M1 修复：幂等绑定——每次先清除旧监听再添加，避免多次打开界面（Start + ShowExpeditionFromTown）
        // 累积多个 Confirm/Return 监听，导致“一次物理点击”重复执行确认逻辑。
        if (btnCharTab != null)
        {
            btnCharTab.onClick.RemoveAllListeners();
            btnCharTab.onClick.AddListener(() => SwitchTab(true));
        }
        if (btnConfirm != null)
        {
            btnConfirm.onClick.RemoveAllListeners();
            btnConfirm.onClick.AddListener(OnConfirmExpedition);
        }
        if (btnReturn != null)
        {
            btnReturn.onClick.RemoveAllListeners();
            btnReturn.onClick.AddListener(OnReturnToTown);
        }
    }

    void SwitchTab(bool isChar)
    {
        RefreshCardPanel();
    }

    void RefreshCardPanel()
    {
        ClearPanel(cardPanel);

        ShowCharacterSelection();
        if (btnCharTab != null) { btnCharTab.interactable = false; btnCharTab.transform.SetAsLastSibling(); }

        UpdateSummary();
    }

    void ShowExpeditionUI()
    {
        // 每次（重新）打开远征准备界面时重置防重入 guard：上一次成功确认的 guard 不得影响下一次出征
        _confirmInFlight = false;

        ClearPanel(heroPanel);
        ClearPanel(cardPanel);

        ShowHeroSelection();
        RefreshCardPanel();

        // 默认选中第一张「可出征」（未被 Temporary Occupancy 占用）的角色卡
        if (_selectedCharCardNames.Count == 0)
        {
            var deployable = FindFirstDeployableCharacterCard();
            if (deployable != null)
                OnCharacterCardClick(deployable.CardId);
        }

        Log("远征准备：选择角色卡和道具卡");

        // V4 Functional Repair：进入准备界面显示「人物详情」入口 + 出征前补给可见性（Popup 默认关闭）
        EnsureIdentityPanel().Show();
        RefreshIdentityPanel();
        RefreshPrepSupplyAndPreflight();
    }

    /// <summary>在卡册中找第一个可出征角色卡（无占用且能解析唯一人物，M1：同模板多候选无绑定者跳过）。</summary>
    CharacterCardData FindFirstDeployableCharacterCard()
    {
        if (_playerData == null || _playerData.OwnedCharacterCards == null) return null;
        foreach (var card in _playerData.OwnedCharacterCards)
        {
            if (card == null) continue;
            if (string.IsNullOrEmpty(GetCardUnavailabilityReason(card)))
                return card;
        }
        return null;
    }

    void ShowHeroSelection()
    {
        _heroCards.Clear();

        for (int i = 0; i < _playerData.OwnedHeroes.Count; i++)
        {
            var heroInstance = _playerData.OwnedHeroes[i];
            HeroData heroData = DataManager.Instance.GetHeroById(heroInstance.HeroId);
            if (heroData == null) continue;

            string skillInfo = "技能：无";
            if (heroData.InitialSkillIds != null && heroData.InitialSkillIds.Length > 0)
            {
                var skillNames = new List<string>();
                foreach (var skillId in heroData.InitialSkillIds)
                {
                    var skillData = DataManager.Instance.GetSkillById(skillId);
                    if (skillData != null)
                        skillNames.Add(skillData.Name);
                }
                if (skillNames.Count > 0)
                    skillInfo = "技能：" + string.Join("、", skillNames);
            }

            // 卡片保持最小摘要：身份信息改由 ExpeditionIdentityPanel（可见独立表面）承载——
            // 卡片 Rect 160×190 / font 50 / 垂直 Truncate，长文本会被裁切（V4 Functional Closure 诊断结论）。
            var go = CreateCard(heroPanel, i, "HeroCard_" + heroInstance.HeroId,
                "[英雄] " + heroData.DisplayName + "\nHP:" + heroData.BaseHP + " ATK:" + heroData.BaseAttack + "\n" + skillInfo,
                heroCardStartPosition);
            go.transform.localScale = heroCardScale;

            _heroCards[heroData.HeroId] = go;

            // Person Visual Binding V0.1：Prep 英雄视觉（PersonId → HeroId 外观键 → 统一配置 → placeholder）
            PersonVisualHostBinder.ApplyForPrepPerson(go, _playerData,
                heroInstance != null ? heroInstance.PersonId : null, heroData.HeroId,
                prepCardBodyPlaceholder);
        }
    }

    /// <summary>
    /// 出征选人卡面显示名：优先该卡对应具体人物的显示名（PersonId 显式绑定）；
    /// 无绑定/无人物名时回退到战斗定义名（Legacy fallback）。不猜人物、不用模板名冒充人物名。
    /// </summary>
    private string GetPersonCardDisplayName(CharacterCardData card, CharacterData charData)
    {
        string personName = _playerData != null ? _playerData.GetPersonDisplayNameForCard(card) : null;
        if (!string.IsNullOrEmpty(personName)) return personName;
        return charData != null ? charData.DisplayName : (card != null ? card.CardId : string.Empty);
    }

    void ShowCharacterSelection()
    {
        _charCards.Clear();

        for (int i = 0; i < _playerData.OwnedCharacterCards.Count; i++)
        {
            var card = _playerData.OwnedCharacterCards[i];
            CharacterData charData = DataManager.Instance.GetCharacterById(card.CharacterId);
            if (charData == null) continue;

            // V2 Final Closure：Expedition 选人读取统一 Temporary Occupancy，占用者不可选并给出原因
            // M1：占用必须按该卡对应具体人物的 PersonId 查询（同模板多实例独立，不互相占用）；
            //     同模板多候选且无显式绑定时同样禁止出征（不给模板 ID 静默当人物身份）。
            string cardPersonId = PersonIdOfCard(card);
            string occupancyReason = GetCardUnavailabilityReason(card, cardPersonId);
            bool occupied = !string.IsNullOrEmpty(occupancyReason);

            string cardText = GetPersonCardDisplayName(card, charData) + "\nHP:" + charData.BaseHP + " ATK:" + charData.BaseAttack;
            if (occupied)
                cardText += "\n〔不可出征：" + occupancyReason + "〕";

            //（身份信息不写入卡片：卡片为 160×190 / font 50 且垂直 Truncate，会被裁切；
            //  改由 ExpeditionIdentityPanel 在可见主表面展示当前选中人物。）

            var go = CreateCard(cardPanel, charCardPrefab, i, "CharCard_" + card.CardId,
                cardText, cardStartPosition);

            if (_selectedCharCardNames.Contains(card.CardId))
                SetCardHighlight(go, true);

            string cardId = card.CardId;
            var btn = go.GetComponent<Button>();
            if (btn != null)
            {
                if (occupied)
                    btn.interactable = false; // 占用者禁止选中
                else
                    btn.onClick.AddListener(() => OnCharacterCardClick(cardId));
            }

            _charCards[card.CardId] = go;

            // Person Visual Binding V0.1：Prep 人物视觉（Card → PersonId / 外观键 → 统一配置 → placeholder）
            // 纯表现：缺素材显示中性占位，不泄漏 prefab 上的历史人物图。
            PersonVisualHostBinder.ApplyForPrepPerson(go, _playerData, cardPersonId,
                PersonVisualResolver.ResolveAppearanceKeyForCard(card),
                prepCardBodyPlaceholder);
        }
    }

    GameObject CreateCard(RectTransform parent, int index, string name, string text)
    {
        return CreateCard(parent, charCardPrefab, index, name, text, cardStartPosition);
    }

    GameObject CreateCard(RectTransform parent, GameObject prefab, int index, string name, string text, Vector2 startPos)
    {
        var go = Instantiate(prefab, parent, false);
        go.name = name;
        go.transform.localScale = cardScale;

        int row = index / cardsPerRow;
        int col = index % cardsPerRow;

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(startPos.x + col * cardSpacing, startPos.y - row * rowSpacing);

        var txt = go.GetComponentInChildren<TMP_Text>();
        if (txt != null) txt.text = text;

        return go;
    }

    GameObject CreateCard(RectTransform parent, int index, string name, string text, Vector2 startPos)
    {
        return CreateCard(parent, charCardPrefab, index, name, text, startPos);
    }

    /// <summary>
    /// M1：解析一张角色卡对应的具体人物 PersonId（占用检查/登记统一入口）。
    /// Foundation Identity Cleanup：有 PlayerData 时只接受显式 Card→Person 绑定；
    /// 无显式绑定（即使模板候选唯一）→ 返回 null（拒绝把模板 CharacterId 当 Person 使用）。
    /// 仅纯演示（无 PlayerData）链路才回退模板 CharacterId 作临时标识。
    /// </summary>
    private string PersonIdOfCard(CharacterCardData card)
    {
        if (card == null) return null;
        if (_playerData != null)
        {
            return _playerData.ResolvePersonIdForCard(card);
        }
        return card.CharacterId; // 演示（无 PlayerData）
    }

    /// <summary>
    /// M1：返回该卡不可出征的原因（占用 或 缺少显式 Card→Person 绑定）；null = 可出征。
    /// Foundation Identity Cleanup：无显式绑定（即使模板候选唯一）时禁止把模板 ID 当人物身份静默出征。
    /// </summary>
    private string GetCardUnavailabilityReason(CharacterCardData card)
    {
        return GetCardUnavailabilityReason(card, PersonIdOfCard(card));
    }

    /// <summary>
    /// 同上的重载：调用方已解析过该卡的 PersonId 时复用，避免重复解析（同一次显示不在 Console 里重复告警）。
    /// </summary>
    private string GetCardUnavailabilityReason(CharacterCardData card, string cardPersonId)
    {
        if (card == null) return null;
        if (_playerData != null && string.IsNullOrEmpty(cardPersonId))
        {
            return "该角色卡缺少显式 Card→Person 绑定，无法确定出征的具体人物";
        }
        return VillageRuntimeBridge.GetDepartureOccupancyReason(cardPersonId);
    }

    void OnCharacterCardClick(string cardId)
    {
        var card = _playerData.OwnedCharacterCards.Find(c => c.CardId == cardId);
        if (card == null) return;

        // 点击守卫：即使卡片状态在显示后发生变化，也再次读取统一占用 / 绑定可解析性
        // M1：占用/可出征判定按卡对应的具体人物 PersonId；多候选无绑定则拒绝选择
        string guardReason = GetCardUnavailabilityReason(card);
        if (!string.IsNullOrEmpty(guardReason))
        {
            var cd = DataManager.Instance.GetCharacterById(card.CharacterId);
            Log("无法选择「" + (cd?.DisplayName ?? card.CharacterId) + "」出征：" + guardReason + "（同一 Person 不能同时处于两个占用 / 或缺少具体人物绑定）");
            UpdateSummary();
            return;
        }

        if (_expedition.SelectedCharacters.Exists(c => c.CardId == cardId))
        {
            _expedition.RemoveCharacterCard(card);
            if (_charCards.ContainsKey(cardId))
                SetCardHighlight(_charCards[cardId], false);
            _selectedCharCardNames.Remove(cardId);
            var charData = DataManager.Instance.GetCharacterById(card.CharacterId);
            Log("移除角色卡: " + (charData?.DisplayName ?? card.CharacterId));
        }
        else
        {
            if (_expedition.AddCharacterCard(card))
            {
                if (_charCards.ContainsKey(cardId))
                    SetCardHighlight(_charCards[cardId], true);
                _selectedCharCardNames.Add(cardId);
                var charData = DataManager.Instance.GetCharacterById(card.CharacterId);
                Log("添加角色卡: " + (charData?.DisplayName ?? card.CharacterId));
            }
        }

        UpdateSummary();
    }

    void UpdateSummary()
    {
        if (selectedSummaryText == null) return;

        string summary = "=== 当前阵容 ===\n";

        if (_expedition.SelectedHero != null)
        {
            summary += "英雄: " + _expedition.SelectedHero.DisplayName + "\n";
        }

        summary += "角色卡: " + _expedition.SelectedCharacters.Count + "/" + ExpeditionSystem.MaxCharacterCards;

        selectedSummaryText.text = summary;

        if (btnConfirm != null)
            btnConfirm.interactable = _expedition.IsReady;

        // V4 Functional Repair：身份信息仅在局部 Popup 中显示（默认关闭）；补给可见性在准备界面常驻显示
        RefreshIdentityPanel();
        RefreshPrepSupplyAndPreflight();
    }

    void SetCardHighlight(GameObject card, bool highlighted)
    {
        if (card == null) return;
        var img = card.GetComponent<Image>();
        if (img != null)
            img.color = highlighted ? Color.green : Color.white;
    }

    void OnConfirmExpedition()
    {
        if (!VillageRuntimeBridge.GuardWorldWrite()) return;
        // M1 修复：成功确认已把本次远征登记为 Expedition Occupancy；任何重复触发都不得再次执行确认
        //（否则第二次校验会把“本次刚建立的远征”误判为“既有远征占用”）。
        if (_confirmInFlight) return;

        if (!_expedition.IsReady)
        {
            Log("请至少选择 1 个角色卡（英雄可选，不选英雄也可出征）");
            return;
        }

        // V4 Functional Repair（产品入口 0 Supply 防护）：Carry 无法承担第一个合法推进节点的
        // Base Supply Cost 时，明确拒绝进入一个无任何可执行动作的远征会话。
        // 复用现有来源（Village Food / Carry policy / elapsed policy），不复制第二套 affordability 公式。
        RefreshPrepSupplyAndPreflight();
        if (!_preflightCarryOk)
        {
            Log("当前补给不足以开始远征：Village Food / 实际可携带量不足第一步成本。"
                + "请先安排 Farm / Kitchen 生产食物（或将食物积累到足够）后再出发。");
            return;
        }

        // M2 Slice 2A（C1）：Monster 静态定义 Authority = StaticConfigRegistry（唯一入口）。
        // Root 缺失时由 StaticConfigRegistry 内部（D-M2-1 Disposable 失败保护）兜底；业务层**不判断 fallback**。
        // 彻底不可用时明确拒绝出征（不静默进入 0 敌人的战斗）；此时尚未发生任何确认状态变更。
        var enemyMonsterRegistry = StaticConfigRegistry.Monsters;
        if (enemyMonsterRegistry == null)
        {
            Debug.LogError("[ExpeditionUI] Monster 静态定义不可用，拒绝出征："
                + StaticConfigRegistry.MonsterDiagnostic);
            Log("怪物静态定义不可用，无法出征。请先运行 RRPG_一键导表.cmd。");
            return;
        }

        if (_expedition.ConfirmExpedition(out var playerChars, out var playerHero))
        {
            // 成功确认 → 防重入（同帧重复 listener / 双击），随后才进入正式 Expedition Occupancy 登记
            _confirmInFlight = true;

            Log("远征确认！开始战斗...");

            var battleUI = FindObjectOfType<BattleUI>(true);
            if (battleUI != null)
            {
                var allCharDatas = DataManager.Instance.AllCharacters;
                var allSkills = DataManager.Instance.AllSkills;
                // M2 Slice 2A（C1）：敌方定义来源 = StaticConfigRegistry（保持 Canonical 列表顺序，不排序、不抽样）
                var allEnemyMonsters = enemyMonsterRegistry.All;

                gameObject.SetActive(false);

                var mapUI = FindObjectOfType<MapUI>();
                int mapRows = Mathf.Max(2, compatibilityGraphRows);
                int mapCols = Mathf.Max(1, compatibilityGraphColumns);
                var adventure = new AdventureManager(_playerData, playerChars, playerHero,
                    allCharDatas, allSkills, allEnemyMonsters);

                // WP-1 Stage2：产品流程切换到 ExpeditionProgression Authority
                //（Legacy MapGenerator 只作 Compatibility Graph 源，不再驱动推进）。
                // 先清除上一会话残留 provider，再以本次 Progression 会话注册（FDS §10.3）。
                VillageRuntimeBridge.SetExpeditionElapsedDaysProvider(null);
                var expeditionPersonIds = adventure.GetExpeditionPersonIds();
                int partySize = Mathf.Max(1, expeditionPersonIds.Count);

                // WP-3（Gate 4）Departure：Village Food 真实转移为远征 Carry（不复制；不足按可带量转移，
                // 最小 Test 策略 Test/Not Frozen，不阻塞出发——保证既有无食物产品流程不回退）。
                int carryUnits = VillageRuntimeBridge.CarryFoodForDeparture(
                    partySize * VillageRuntimeBridge.ResourceCarryPerPerson);
                adventure.StartProgression(mapRows, mapCols, partySize, carryUnits,
                    VillageRuntimeBridge.ResourcePerPersonDailyConsumptionRate,
                    LegacyNodeElapsedDaysTestPolicy.BuildLookup(adventure.MapGen));

                // Stage V1：确认成功后才登记当前远征人物（Village Person Availability 运行时查询；不持久化）。
                // Every confirmation entry stops realtime village simulation, including direct UI
                // callers. Return alone accounts for the expedition's elapsed world minutes.
                VillageRuntimeBridge.SetSimulationActive(false);
                VillageRuntimeBridge.SetExpeditionPersonIds(expeditionPersonIds);
                VillageRuntimeBridge.SetExpeditionElapsedMinutesProvider(() => adventure.ExpeditionElapsedMinutes);

                // V4 Functional Closure：确认出征即绑定当前 Adventure，
                // 使推进面板与 Retreat 在**任何节点之前**也可用（否则首次点击/撤退为 no-op → 软锁死）。
                _currentAdventure = adventure;

                // V4 Functional Closure：Current 产品路径改用最小 Node-based Progression 表现
                // （Legacy MapUI 保留 Compatibility 代码，但不再作为主要推进交互 / 不再展示完整 4×3 拓扑）。
                if (mapUI != null) mapUI.HideMap();
                if (_identityPanel != null) _identityPanel.Hide();

                // WP-E：Current 产品路径只显示 Journey Surface（不与 Debug Progression Panel 同屏）。
                var journeyOnConfirm = JourneyRef;
                if (journeyOnConfirm != null)
                {
                    if (_progressionPanel != null) _progressionPanel.Hide();
                    journeyOnConfirm.Show(adventure, this);
                }
                else
                {
                    EnsureProgressionPanel().Show(adventure, this); // Compatibility fallback（非正式表现）
                }
            }
            else
            {
                Debug.LogError("[ExpeditionUI] 未找到 BattleUI 组件");
            }
        }
    }

    void ClearPanel(RectTransform panel)
    {
        if (panel == null) return;
        var keepNames = new HashSet<string> { "BtnCharTab" };
        foreach (Transform child in panel)
        {
            if (keepNames.Contains(child.name))
                continue;
            Destroy(child.gameObject);
        }
    }

    private bool HandleNodeClick(AdventureManager adventure, string nodeId, BattleUI battleUI)
    {
        if (adventure == null) return false;

        // WP-1 Stage2：MapUI 只上报 NodeId 意图；合法性与 affordability 校验在此执行（Resolver）。
        ExpeditionNodeKind? kind = NodeKindOf(adventure, nodeId);
        if (kind == null)
        {
            Log("节点不存在或 Progression 未初始化。");
            return false;
        }

        bool battleLike = kind == ExpeditionNodeKind.Battle
            || kind == ExpeditionNodeKind.Elite
            || kind == ExpeditionNodeKind.Boss;

        // ==================== WP-Encounter-1C-R（R1）：Battle 类节点的原子进入 ====================
        // 规则：先「进入校验（纯查询）→ 预生成 Encounter」全部成功，之后才真正 mutation 为 Entered。
        // 遭遇生成失败 ⇒ 零 mutation：节点不进入、CurrentNode 不变、Supply / elapsed 不变、
        // 不留半成品 ActiveEncounter；玩家仍停在原 Journey 状态，可重新选择或重试（不需要 Retreat）。
        if (battleLike)
        {
            // 战斗宿主必须先可用：否则不允许进入节点（避免「已进入但无法开战」的半进入状态）。
            if (battleUI == null)
            {
                Debug.LogError("[ExpeditionUI] 未找到 BattleUI 组件：拒绝进入战斗节点（节点未被进入）。");
                Log("未找到 BattleUI 组件，无法进入战斗节点。");
                return false;
            }

            EnemyRoster preparedRoster;
            string prepareDiagnostic;
            if (!adventure.TryPrepareAndEnterBattleNode(nodeId, out preparedRoster, out prepareDiagnostic))
            {
                Debug.LogError("[ExpeditionUI] 拒绝进入战斗节点（节点未被进入）：" + prepareDiagnostic);
                Log("该战斗节点当前无法进入。" + prepareDiagnostic);

                var journeyDeniedByEncounter = JourneyRef;
                if (journeyDeniedByEncounter != null && journeyDeniedByEncounter.IsActive)
                    journeyDeniedByEncounter.ShowDenied("无法进入该战斗节点：" + prepareDiagnostic);

                var deniedMapForEncounter = FindObjectOfType<MapUI>();
                if (deniedMapForEncounter != null && deniedMapForEncounter.canvasRoot != null
                    && deniedMapForEncounter.canvasRoot.activeSelf)
                    RefreshProgressionMap(deniedMapForEncounter, adventure); // Legacy 仅在自身上可见时同步（Compatibility）

                return false;
            }

            // 进入成功 → 清除上一轮粘性拒绝原因（不得让过期的「无法进入」留在 Journey 表面）。
            var journeyEnteredForBattle = JourneyRef;
            if (journeyEnteredForBattle != null && journeyEnteredForBattle.IsActive)
                journeyEnteredForBattle.ClearDeniedMessage();

            StartPreparedBattle(adventure, battleUI, preparedRoster);
            return true;   // Issue E：Battle 节点已原子进入（Battle 生命周期已开始）
        }

        // ==================== WP-Event-1D Gate D2：正式 Event 节点（原子进入 + 正式事件卡） ====================
        // 非战斗类节点全部走 Unified Event Core：
        //   ValidateEnterNode → EventResolver → 成功才 TryEnterNode + 提交同一实例为 Active Event。
        // 失败 ⇒ 零 mutation（节点未进入 / Supply / elapsed 未变）+ 明确拒绝原因；
        // 不再存在「进入后立即 Success 结算」的占位 Continue 路径。
        ResolvedEventInstance eventInstance;
        string enterEventDiagnostic;
        if (!ExpeditionEventFlow.TryEnterEventNode(adventure, nodeId, out eventInstance, out enterEventDiagnostic))
        {
            Debug.LogError("[ExpeditionUI] 拒绝进入 Event 节点（节点未被进入）：" + enterEventDiagnostic);
            Log("该事件节点当前无法进入。" + enterEventDiagnostic);

            var journeyDeniedByEvent = JourneyRef;
            if (journeyDeniedByEvent != null && journeyDeniedByEvent.IsActive)
                journeyDeniedByEvent.ShowDenied("无法进入该事件节点：" + enterEventDiagnostic);
            else
                EnsureProgressionPanel().ShowDenied(ExpeditionEnterResult.NotAvailableNow, enterEventDiagnostic);

            var deniedMapForEvent = FindObjectOfType<MapUI>();
            if (deniedMapForEvent != null && deniedMapForEvent.canvasRoot != null
                && deniedMapForEvent.canvasRoot.activeSelf)
                RefreshProgressionMap(deniedMapForEvent, adventure); // Legacy 仅在自身上可见时同步（Compatibility）
            return false;
        }

        var mapUI = FindObjectOfType<MapUI>();
        var journeyForEvent = JourneyRef;

        // WP-E：成功进入 → 清除上一轮粘性拒绝原因（不得让过期的「无法进入」留在 Journey 表面）。
        if (journeyForEvent != null && journeyForEvent.IsActive) journeyForEvent.ClearDeniedMessage();

        if (journeyForEvent == null)
        {
            Debug.LogError("[ExpeditionUI] 未找到 Journey 表现宿主：无法渲染正式事件卡"
                + "（节点已进入并停留 Event Pending；修复 Scene 绑定后重开界面会复用同一事件实例）。");
            return true;   // Issue E：Event Node 已原子进入（仅表现宿主缺失）
        }

        // 审计证据（真实产品链）：原子进入成功后的稳定身份（同一次远征 / 同一 Node 不重抽）。
        Debug.Log("[ExpeditionUI] Event Node 原子进入成功：" + nodeId + " → " + eventInstance);

        // 正式事件卡：Title / Body / Options 全部来自 Event Config（不再走 ContinueContent 固定语义）。
        JourneyEventCardConfig eventCardConfig;
        string eventCardDiagnostic;
        if (!ExpeditionEventCardAdapter.TryBuildOptionCard(adventure,
                journeyForEvent.ContentEncounterIllustration, out eventCardConfig, out eventCardDiagnostic))
        {
            Debug.LogError("[ExpeditionUI] 正式事件卡无法渲染（节点停留 Event Pending）：" + eventCardDiagnostic);
            journeyForEvent.ShowDenied("事件卡面无法渲染：" + eventCardDiagnostic);
            return true;   // Issue E：Event Node 已原子进入（仅卡面构造失败）
        }

        journeyForEvent.PresentFormalEventCard(eventCardConfig,
            index => OnEventOptionChosen(adventure, eventCardConfig, index));

        if (_progressionPanel != null && _progressionPanel.IsVisible)
            _progressionPanel.Refresh(); // Compatibility（面板未显示时不得创建 / 显示）

        return true;   // Issue E：Battle 已开始 / Event Node 已原子进入 → 产品 Node Entry 成功
    }

    /// <summary>
    /// **正式 Event Option 选择入口**（WP-Event-1D Gate D2 §16）：
    /// 把玩家选择交回 Event Core；成功后的分支：
    ///   - StartBattle（Terminal Effect）→ 用 Preflight 已提交的**同一份** EnemyRoster 走既有 Prep / Battle；
    ///   - 其它 Outcome → Result Mode（同一卡面；玩家「继续」后才结算当前 Node）。
    /// 失败 = 明确诊断 + 零 mutation（节点停留 Event Pending，可重试或改选其它 Option）。
    /// </summary>
    private void OnEventOptionChosen(AdventureManager adventure, JourneyEventCardConfig cardConfig, int index)
    {
        if (adventure == null || cardConfig == null || cardConfig.Options == null) return;
        if (index < 0 || index >= cardConfig.Options.Count) return;

        var option = cardConfig.Options[index];
        if (option == null) return;

        var journey = JourneyRef;

        // 结果卡的「继续」：结算当前 Event Node（elapsed + Supply exactly-once）
        if (option.Role == JourneyEventOptionRole.ContinueResult)
        {
            ExpeditionNodeResolutionData data;
            string continueDiagnostic;
            if (!ExpeditionEventFlow.TryContinueActiveEvent(adventure, out data, out continueDiagnostic))
            {
                Debug.LogError("[ExpeditionUI] 事件结果「继续」未能结算当前 Node：" + continueDiagnostic);
                Log("事件结算未成功。" + continueDiagnostic);
                if (journey != null && journey.IsActive) journey.ShowDenied("事件结算未成功：" + continueDiagnostic);
                return;
            }

            AfterEventNodeResolved(adventure, data);
            return;
        }

        if (!option.Interactable)
        {
            Log("该选项当前不可选。");
            return;
        }

        EventOptionExecutionResult execution;
        string executeDiagnostic;
        if (!ExpeditionEventFlow.TryExecuteActiveOption(adventure, option.OptionId, out execution,
                out executeDiagnostic))
        {
            Debug.LogError("[ExpeditionUI] Event Option 执行失败（节点停留 Event Pending）：" + executeDiagnostic);
            Log("该事件选项当前无法选择。" + executeDiagnostic);
            if (journey != null && journey.IsActive) journey.ShowDenied("该选项当前无法选择：" + executeDiagnostic);
            return;
        }

        // StartBattle：使用 Preflight 生成、Commit 提交的**同一份** EnemyRoster 进入既有 Prep / Battle
        if (execution.HasTerminalEffect)
        {
            var battleUI = FindObjectOfType<BattleUI>(true);
            var activeBattle = ExpeditionEventFlow.ActiveOf(adventure);
            if (battleUI == null || activeBattle == null || activeBattle.CommittedBattle == null
                || activeBattle.CommittedBattle.Roster == null)
            {
                Debug.LogError("[ExpeditionUI] StartBattle 已提交但 Battle 宿主 / 预备阵容不可用："
                    + "节点停留 Event Pending（不得开空战斗）。");
                Log("无法开始事件战斗：缺少 Battle 宿主或预备阵容。");
                return;
            }

            StartPreparedBattle(adventure, battleUI, activeBattle.CommittedBattle.Roster);
            return;
        }

        // 非战斗结果 → Result Mode（玩家「继续」后才结算；此处不推进 Progression）
        JourneyEventCardConfig resultConfig;
        string resultDiagnostic = string.Empty;
        if (journey == null || !ExpeditionEventCardAdapter.TryBuildResultCard(adventure,
                journey.ContentEncounterIllustration, out resultConfig, out resultDiagnostic))
        {
            Debug.LogError("[ExpeditionUI] 事件结果卡无法渲染（节点停留 Event Pending）：" + resultDiagnostic);
            if (journey != null && journey.IsActive) journey.ShowDenied("事件结果卡无法渲染：" + resultDiagnostic);
            return;
        }

        journey.PresentFormalEventCard(resultConfig, idx => OnEventOptionChosen(adventure, resultConfig, idx));
    }


    /// <summary>
    /// Event Node 结算成功后的产品收口（与 Battle 胜利路径同一语义）：
    ///   - 显式成功路线终点（Boss）→ SafeReturn（Return 事务 + Journey 隐藏）；
    ///   - 其它 → 回到 Journey（旧 Cycle 失效 + 重新读取 Current Progression State）。
    /// </summary>
    private void AfterEventNodeResolved(AdventureManager adventure, ExpeditionNodeResolutionData data)
    {
        bool routeCompleted = data != null && data.RouteCompleted;

        if (routeCompleted)
        {
            RequestProgressionReturn(adventure);
            if (FinalizeReturn(RunEndMode.SafeReturn))
            {
                ResetElapsedProvider();
                var journeyOnSafeReturn = JourneyRef;
                if (journeyOnSafeReturn != null) journeyOnSafeReturn.Hide();
            }
            else
            {
                Debug.LogError("[ExpeditionUI] Event Node 成功路线终点 Return 未 Completed：保留恢复状态。");
            }
            return;
        }

        if (TryResumeIncompleteReturnTransaction()) return;

        var journeyAfterEvent = JourneyRef;
        if (journeyAfterEvent != null)
        {
            if (_progressionPanel != null) _progressionPanel.Hide();
            journeyAfterEvent.ShowAfterEncounter(adventure, this);
        }
        else
        {
            EnsureProgressionPanel().Show(adventure, this); // Compatibility fallback（非正式表现）
        }
    }

    /// <summary>
    /// **使用已预生成并提交的 Active EnemyRoster 启动正式 Battle**（WP-Encounter-1C / 1C-R）。
    ///
    /// 关键：本方法**不再生成 / 不再重抽**任何敌人 —— 阵容已在
    /// <see cref="AdventureManager.TryPrepareAndEnterBattleNode"/> 中预生成、随 Enter 提交；
    /// 这里只把**同一份**对象绑定给 BattleUI（Prep 与 Battle 消费同一份）。
    /// </summary>
    private void StartPreparedBattle(AdventureManager adventure, BattleUI battleUI, EnemyRoster enemyRoster)
    {
        var chars = adventure.GetPlayerChars();
        var hero = adventure.GetPlayerHero();
        var charDatas = adventure.GetAllCharDatas();
        var skills = adventure.GetAllSkills();
        // Monster 定义目录（名称 / 数值 / 美术的模板来源；**不再**决定本场有几只怪）
        var monsters = adventure.GetEnemyMonsters();

        battleUI.SetRunState(adventure.RunState, adventure.RunConfig); // Stage 6A：Run HP / Run Deck / 领悟 / Pending
        // WP-Encounter-1C §6 / §10：绑定本场冻结阵容（Prep 与 Battle 消费的**同一份**对象）
        battleUI.BindActiveEnemyRoster(enemyRoster);
        battleUI.InitFromExpedition(chars, hero, charDatas, skills, monsters, _playerData);

        var resultUI = FindObjectOfType<BattleResultUI>(true);
        if (resultUI != null)
            battleUI.SetSettlementDependencies(resultUI, _playerData);

        var mapUI = FindObjectOfType<MapUI>();
        if (mapUI != null) mapUI.HideMap();
        // WP-E：Battle 前隐藏 / 冻结 Journey 并取消当前 Presentation Cycle（旧回调失效）。
        var journeyBeforeBattle = JourneyRef;
        if (journeyBeforeBattle != null) journeyBeforeBattle.HideForEncounter();
        if (_progressionPanel != null) _progressionPanel.Hide(); // Compatibility（Journey 存在时本就不显示）
        if (battleCanvas != null) battleCanvas.SetActive(true);
        battleUI.gameObject.SetActive(true);
        battleUI.StartBattleWithExpedition();

        _currentAdventure = adventure;
        _currentBattleUI = battleUI;
        // Battle 内容已启动；最终 outcome 由 BattleResultUI 确认后经 OnReturnFromBattle 送回 Resolver 结算。
    }

    private AdventureManager _currentAdventure;
    private BattleUI _currentBattleUI;
    private bool _lastBattleResult;

    /// <summary>只读：当前 Prep 使用的长期人物集合（Journey 视觉解析的唯一只读来源；不新增 Authority）。</summary>
    public PlayerCollection PlayerDataForJourney => _playerData;

    // ==================== WP-E：Journey Presentation（Current 产品表现表面） ====================

    private ExpeditionJourneyCoordinator _journey;
    private bool _journeyMissingLogged;

    /// <summary>
    /// WP-E：Journey 表现协调器，宿主为 Scene 稳定对象 `ExpeditionJourneyRoot`。
    /// 找不到时不新建第二套 Canvas（明确报错一次并回退既有 Compatibility 推进面板，避免软锁死）。
    /// </summary>
    private ExpeditionJourneyCoordinator JourneyRef
    {
        get
        {
            if (_journey == null) _journey = FindObjectOfType<ExpeditionJourneyCoordinator>(true);
            if (_journey == null && !_journeyMissingLogged)
            {
                _journeyMissingLogged = true;
                Debug.LogError("[ExpeditionUI] 未找到 ExpeditionJourneyCoordinator（Scene 宿主 ExpeditionJourneyRoot）："
                    + "本次回退到 Compatibility 推进面板（非正式玩家表现）。");
            }
            return _journey;
        }
    }

    // ==================== V4 Functional Closure：可见表面（运行时自建；Test / Not Frozen） ====================

    private ExpeditionProgressionPanel _progressionPanel;
    private ExpeditionIdentityPanel _identityPanel;

    // 观察引用不创建对象；只有下方显式 Ensure 方法可以创建运行时面板。
    private ExpeditionProgressionPanel ProgressionPanelRef => _progressionPanel;
    private ExpeditionIdentityPanel IdentityPanelRef => _identityPanel;

    private ExpeditionProgressionPanel EnsureProgressionPanel()
    {
        if (_progressionPanel == null)
        {
            var go = new GameObject("ExpeditionProgressionPanel", typeof(ExpeditionProgressionPanel));
            _progressionPanel = go.GetComponent<ExpeditionProgressionPanel>();
        }
        return _progressionPanel;
    }

    private ExpeditionIdentityPanel EnsureIdentityPanel()
    {
        if (_identityPanel == null)
        {
            var go = new GameObject("ExpeditionIdentityPanel", typeof(ExpeditionIdentityPanel));
            go.transform.SetParent(transform, false); // 随 ExpeditionUI 显隐（远征开始后一并隐藏）
            _identityPanel = go.GetComponent<ExpeditionIdentityPanel>();
        }
        return _identityPanel;
    }

    /// <summary>推进面板按钮回调：复用既有 node click 产品路径（合法性与 affordability 仍由 Resolver 判定）。</summary>
    public bool OnProgressionNodeChosen(string nodeId)
    {
        // WP-Event-1 Producer Acceptance Repair 2（Issue E）：返回值 = 产品 Node Entry 的**真实成败**。
        // 表现层（Journey Presentation）只消费该结果，**不**自行判断 Supply / Node legality / Progression。
        if (_currentAdventure == null || string.IsNullOrEmpty(nodeId)) return false;
        var battleUI = FindObjectOfType<BattleUI>(true);
        return HandleNodeClick(_currentAdventure, nodeId, battleUI);
    }

    /// <summary>
    /// Retreat：复用既有 `RequestProgressionReturn` + `FinalizeReturn(RunEndMode.Retreat)`（无第二套 Return 逻辑）。
    /// Supply 不足时同样可用（不形成软锁死）。
    /// WP-B：只有 Return 真正 Completed 才 Reset elapsed provider / Hide progression UI；
    /// 未完成（Settlement / Catch-up / Carry / Consequence / Save 任一阶段失败）时保留恢复入口与
    /// 未完成事务状态，允许同一会话再次触发只重试未完成阶段。
    /// </summary>
    public void RequestRetreatFromProgression()
    {
        if (_currentAdventure == null) return;
        RequestProgressionReturn(_currentAdventure);
        if (FinalizeReturn(RunEndMode.Retreat))
        {
            // WP-Event-1D Gate D2 §3：Retreat / Return → 清除活动事件与会话事件状态（Runtime Only）
            ExpeditionEventFlow.ClearForSessionEnd(_currentAdventure);
            ResetElapsedProvider();
            // WP-E：Return 完成 → 取消未完成 Presentation callback 并隐藏 Journey。
            var journeyOnRetreat = JourneyRef;
            if (journeyOnRetreat != null) journeyOnRetreat.Hide();
            if (_progressionPanel != null) _progressionPanel.Hide();
        }
        else
        {
            Debug.LogError("[ExpeditionUI] Retreat Return 未 Completed：保留 elapsed provider / 恢复入口与推进面板"
                + "（未完成事务不得执行完成态清理）。");
        }
    }

    /// <summary>
    /// 刷新「人物详情」Popup 内容 —— **全部当前合法候选**（只读 Viewer；不改变任何出征选择）。
    ///
    /// 候选集 = 准备界面实际呈现的候选：全部 `OwnedHeroes` + 全部 `OwnedCharacterCards`
    ///（与 ShowHeroSelection / ShowCharacterSelection 同源；不可出征者保留既有不可出征原因）。
    /// Person 身份只取**显式 Card→Person 绑定**（复用既有 Authority；不按模板 ID 猜测人物）。
    /// </summary>
    private void RefreshIdentityPanel()
    {
        if (_playerData == null) return;

        var rows = new List<ExpeditionPersonDetailRow>();

        // 1) 全部 Hero 候选（Hero 直接持有 PersonId；不新增 Hero LifeHistory）
        for (int i = 0; i < _playerData.OwnedHeroes.Count; i++)
        {
            var hero = _playerData.OwnedHeroes[i];
            if (hero == null) continue;

            bool heroSelected = _expedition != null && _expedition.SelectedHero != null
                && _expedition.SelectedHero.PersonId == hero.PersonId;

            rows.Add(new ExpeditionPersonDetailRow
            {
                PersonId = hero.PersonId,
                DisplayName = string.IsNullOrEmpty(hero.DisplayName) ? hero.HeroId : hero.DisplayName,
                IsHero = true,
                IsSelected = heroSelected,
                UnavailableReason = string.IsNullOrEmpty(hero.PersonId)
                    ? "缺少 PersonId（无法确定具体人物）" : null
            });
        }

        // 2) 全部角色卡候选（含不可出征者 → 保留既有原因）
        for (int i = 0; i < _playerData.OwnedCharacterCards.Count; i++)
        {
            var card = _playerData.OwnedCharacterCards[i];
            if (card == null) continue;

            // M1：只接受显式 Card→Person 绑定；无绑定返回 null（不猜模板 ID），此处仍列出该候选并说明原因
            string pid = PersonIdOfCard(card);

            string displayName;
            if (!string.IsNullOrEmpty(pid))
            {
                var person = _playerData.FindPersonByPersonId(pid);
                displayName = person != null && !string.IsNullOrEmpty(person.DisplayName) ? person.DisplayName : pid;
            }
            else
            {
                var cd = DataManager.Instance != null ? DataManager.Instance.GetCharacterById(card.CharacterId) : null;
                displayName = cd != null && !string.IsNullOrEmpty(cd.DisplayName) ? cd.DisplayName : card.CardId;
            }

            rows.Add(new ExpeditionPersonDetailRow
            {
                PersonId = pid,
                DisplayName = displayName,
                IsHero = false,
                IsSelected = _selectedCharCardNames.Contains(card.CardId),
                UnavailableReason = GetCardUnavailabilityReason(card)
            });
        }

        EnsureIdentityPanel().SetCandidateRows(_playerData, rows);
    }

    /// <summary>
    /// V4 Functional Repair：出征准备界面的补给可见性 + 出征前 affordability preflight。
    /// 只读复用现有来源：`VillageRuntimeBridge.Resources.Food`（Village Food Authority）、
    /// `ResourceCarryPerPerson`（现有 Carry policy）、`ResourcePerPersonDailyConsumptionRate`
    /// 与 `LegacyNodeElapsedDaysTestPolicy`（现有 elapsed policy）——不复制第二套 affordability 公式、
    /// 不新建资源状态。
    /// </summary>
    private void RefreshPrepSupplyAndPreflight()
    {
        int partySize = 0;
        if (_expedition != null)
        {
            partySize = (_expedition.SelectedCharacters != null ? _expedition.SelectedCharacters.Count : 0)
                + (_expedition.SelectedHero != null ? 1 : 0);
        }
        if (partySize <= 0) partySize = 1; // 未选人时按最少 1 人给出参考值

        int villageFood = VillageRuntimeBridge.Resources != null ? VillageRuntimeBridge.Resources.Food : 0;
        int carryPerPerson = VillageRuntimeBridge.ResourceCarryPerPerson;
        int rate = VillageRuntimeBridge.ResourcePerPersonDailyConsumptionRate;
        int requested = partySize * carryPerPerson;
        int actualCarry = Mathf.Min(villageFood, requested);
        int firstStepCost = partySize * LegacyNodeElapsedDaysTestPolicy.BattleNodeElapsedDays * rate;

        _preflightCarryOk = actualCarry >= firstStepCost;

        string text = "村庄食物：" + villageFood
            + "\n预计携带：" + requested + "（实际可携带：" + actualCarry + "）"
            + "\n第一步预计成本：" + firstStepCost;
        if (!_preflightCarryOk) text += "\n【当前补给不足以开始远征】";

        EnsureIdentityPanel().SetSupplyText(text);

        if (btnConfirm != null && _expedition != null)
            btnConfirm.interactable = _expedition.IsReady && _preflightCarryOk;
    }

    private bool _preflightCarryOk = true;

    /// <summary>
    /// Stage 6B：地图刷新后同步 Run 领悟显示（读取 _currentAdventure.RunState）。
    /// </summary>
    private void UpdateMapInsight(MapUI mapUI)
    {
        if (mapUI == null || _currentAdventure == null) return;
        mapUI.UpdateInsightDisplay(_currentAdventure.RunState.CurrentInsight, _currentAdventure.RunState.InsightThreshold);
    }

    // ==================== WP-1 Stage2：Progression 产品接线辅助 ====================

    /// <summary>读取 Node 定义（来自 Progression Graph；MapUI 只上报 NodeId）。</summary>
    private static ExpeditionNodeKind? NodeKindOf(AdventureManager adventure, string nodeId)
    {
        var state = adventure?.ProgressionState;
        if (state == null || string.IsNullOrEmpty(nodeId)) return null;
        return state.Graph.TryGetNode(nodeId, out var node) ? node.NodeKind : (ExpeditionNodeKind?)null;
    }

    private static string DescribeEnterResult(ExpeditionEnterResult result)
    {
        switch (result)
        {
            case ExpeditionEnterResult.Success: return "Success";
            case ExpeditionEnterResult.NotInitialized: return "Progression 未初始化";
            case ExpeditionEnterResult.NoSuchNode: return "节点不存在";
            case ExpeditionEnterResult.NotAvailableNow: return "当前不可达（非入口 / 非上一完成 Node 后继）";
            case ExpeditionEnterResult.AlreadyEntered: return "已进入";
            case ExpeditionEnterResult.AlreadyCompleted: return "已完成";
            case ExpeditionEnterResult.WrongPhase: return "当前阶段不允许进入";
            case ExpeditionEnterResult.InsufficientSupply: return "补给不足（进入前校验拒绝）";
            default: return result.ToString();
        }
    }

    /// <summary>
    /// WP-1 Stage2：用 Progression 派生视图刷新地图
    ///（可用 = GetAvailableNextNodes、完成 = CompletedNodeIds、当前 = CurrentNodeId）。
    /// MapUI 只展示；本方法不产生任何规则推进。
    /// </summary>
    private void RefreshProgressionMap(MapUI mapUI, AdventureManager adventure)
    {
        if (mapUI == null || adventure == null) return;

        var available = adventure.GetAvailableProgressionNodes();
        var availableIds = new List<string>();
        foreach (var n in available) availableIds.Add(n.NodeId);

        var state = adventure.ProgressionState;
        IReadOnlyCollection<string> completed = state != null ? state.GetCompletedNodeIds() : null;

        mapUI.SetProgressionView(availableIds,
            state != null ? state.CurrentNodeId : null,
            completed ?? new List<string>());
        mapUI.RefreshDisplayOnly();
        UpdateMapInsight(mapUI);
    }

    /// <summary>Return 完成/会话结束：把 elapsed provider 还原为默认（避免跨会话残留引用旧 AdventureManager）。</summary>
    private void ResetElapsedProvider()
    {
        VillageRuntimeBridge.SetExpeditionElapsedDaysProvider(null);
    }

    /// <summary>Expedition 层 Return 请求置位（Progression 独立操作，不属于 Node 后继）。</summary>
    private void RequestProgressionReturn(AdventureManager adventure)
    {
        if (adventure == null) return;
        var result = adventure.RequestReturn();
        if (result == ExpeditionReturnResult.NodePendingResolution)
            Debug.LogWarning("[ExpeditionUI] RequestReturn=NodePendingResolution：Return 前应先结算当前 Node 已发生成本。");
    }

    /// <summary>
    /// WP-1 Stage2：Battle 最终 outcome → Progression Resolver（产品主路径）。
    /// - 胜利：TryResolveCurrentNode(Success)；仅显式 SuccessfulRouteTerminal（Boss）成功 outcome 才 SafeReturn；
    /// - 失败：TryResolveCurrentNode(Failure)（Defeat 也 exactly-once 结算已发生 elapsed/supply）→ Defeat Return；
    /// - SafeReturn 判定不再使用 MapGen.IsAtBoss（仅保留防御性 Legacy 回退）。
    /// </summary>
    private void HandleProgressionReturnFromBattle(AdventureManager adventure, bool isVictory)
    {
        bool adventureOver = false;
        RunEndMode endMode = RunEndMode.SafeReturn;

        // WP-Event-1D Gate D2 §14：若当前 Battle 是 **Event 内战斗**，必须结算**同一个** Event Node，
        // 并使用 Active Event 的 PendingExtraElapsedDays 完成 elapsed + Supply 的 exactly-once settlement。
        var activeEventBattle = ExpeditionEventFlow.ActiveOf(adventure);
        bool eventBattlePending = activeEventBattle != null
            && activeEventBattle.Phase == ExpeditionEventPhase.AwaitingBattleOutcome;

        if (isVictory)
        {
            ExpeditionResolveResult resolve;
            ExpeditionNodeResolutionData data;

            if (eventBattlePending)
            {
                string eventDiagnostic;
                if (!ExpeditionEventFlow.TryResolveActiveEventBattle(adventure, true, out data, out resolve,
                        out eventDiagnostic))
                {
                    // Event 结算失败：不得假装成功继续（保持 Event Pending，允许同会话重试 / 撤退）。
                    Debug.LogError("[ExpeditionUI] Event 内战斗胜利结算失败：" + eventDiagnostic);
                    Log("事件战斗结算未成功。" + eventDiagnostic);
                    var journeyDeniedOnEventWin = JourneyRef;
                    if (journeyDeniedOnEventWin != null && journeyDeniedOnEventWin.IsActive)
                        journeyDeniedOnEventWin.ShowDenied("事件战斗结算未成功：" + eventDiagnostic);
                    return;
                }
            }
            else
            {
                resolve = adventure.TryResolveCurrentNode(ExpeditionNodeOutcome.Success, out data);
            }

            if (resolve == ExpeditionResolveResult.Success)
            {
                if (data.RouteCompleted)
                {
                    endMode = RunEndMode.SafeReturn; // 显式成功路线终点（Boss）完成
                    adventureOver = true;
                }
            }
            else if (!eventBattlePending)
            {
                // 防御回退（非产品正常路径）：Progression 无活动 Node 却报告胜利 → 沿用旧 Boss 判定，避免流程卡死。
                if (adventure.MapGen.IsAtBoss)
                {
                    endMode = RunEndMode.SafeReturn;
                    adventureOver = true;
                }
            }
        }
        else
        {
            if (eventBattlePending)
            {
                ExpeditionNodeResolutionData defeatData;
                ExpeditionResolveResult defeatResolve;
                string defeatDiagnostic;
                if (!ExpeditionEventFlow.TryResolveActiveEventBattle(adventure, false, out defeatData,
                        out defeatResolve, out defeatDiagnostic))
                {
                    Debug.LogError("[ExpeditionUI] Event 内战斗失败结算失败：" + defeatDiagnostic);
                    Log("事件战斗结算未成功。" + defeatDiagnostic);
                    var journeyDeniedOnEventLose = JourneyRef;
                    if (journeyDeniedOnEventLose != null && journeyDeniedOnEventLose.IsActive)
                        journeyDeniedOnEventLose.ShowDenied("事件战斗结算未成功：" + defeatDiagnostic);
                    return;
                }
            }
            else
            {
                adventure.TryResolveCurrentNode(ExpeditionNodeOutcome.Failure, out _);
            }

            endMode = RunEndMode.Defeat;
            adventureOver = true;
        }

        if (adventureOver)
        {
            RequestProgressionReturn(adventure);
            // WP-B：只有 Return 真正 Completed 才 Reset elapsed provider；
            // 未完成时保留 elapsed provider / adventure / 恢复入口，同会话可安全 retry。
            if (FinalizeReturn(endMode))
            {
                ResetElapsedProvider();
                // WP-E：Return 完成（SafeReturn / Defeat）→ 取消未完成 callback 并隐藏 Journey。
                var journeyOnBattleReturn = JourneyRef;
                if (journeyOnBattleReturn != null) journeyOnBattleReturn.Hide();
            }
            else
            {
                Debug.LogError("[ExpeditionUI] Return 未 Completed（Battle 出口 " + endMode
                    + "）：保留恢复状态（不 Reset elapsed provider、不清 adventure、不隐藏恢复入口）。");
            }
            return;
        }

        // WP-B：IsActive == false 不得绕过未完成的 Return Transaction（先 resume，再考虑既有防御分支）。
        if (TryResumeIncompleteReturnTransaction()) return;

        // WP-2 Defensive Return 收口：在当前产品链中，IsActive=false 的【唯一】生产路径是
        // AdventureManager.SettleReturnDetailed（其先执行 CommitPendingProfessionExp / CommitPendingRewards，
        // 全部 ProfessionExp ack 后 Pending 已清空，才在最后置 IsActive=false）；Legacy SetVictory/SetDefeat 无当前产品调用方。
        // 因此到达本防御分支时不存在“未 Commit 且可兑现的 PendingProfessionExp”，
        // 本分支不会绕过 Current Settlement 静默落盘——不得改造成第二套 Settlement / 额外 Save 入口。
        if (!adventure.IsActive)
        {
            // 防御：冒险已结束但未走 adventureOver 分支（沿用既有兜底语义）。
            VillageRuntimeBridge.ClearExpeditionPersonIds();
            if (_fromTown && _townUI != null)
            {
                gameObject.SetActive(false);
                _fromTown = false;
                _townUI.OnReturnFromExpedition();
            }
            else
            {
                gameObject.SetActive(true);
                ShowExpeditionUI();
            }
            return;
        }

        // 普通胜利 → 回到 Journey：结束旧 Cycle，并重新读取 Current Progression State 建立新 Cycle。
        var journeyAfterBattle = JourneyRef;
        if (journeyAfterBattle != null)
        {
            if (_progressionPanel != null) _progressionPanel.Hide();
            journeyAfterBattle.ShowAfterEncounter(adventure, this);
        }
        else
        {
            EnsureProgressionPanel().Show(adventure, this); // Compatibility fallback（非正式表现）
        }
    }

    public void ShowExpeditionFromTown(PlayerCollection playerData)
    {
        _fromTown = true;
        _townUI = FindObjectOfType<TownUI>(true);
        _playerData = playerData;
        _expedition = new ExpeditionSystem(_playerData);
        _selectedCharCardNames.Clear();

        // 自动选中第一个英雄（玩家在 NewGame 时已选择）
        if (playerData.OwnedHeroes.Count > 0)
        {
            var heroInstance = playerData.OwnedHeroes[0];
            var heroData = DataManager.Instance.GetHeroById(heroInstance.HeroId);
            if (heroData != null)
            {
                _expedition.SelectHero(heroInstance, heroData);
            }
        }

        gameObject.SetActive(true);
        BindButtons();
        ShowExpeditionUI();
    }

    /// <summary>
    /// 返回领地（从领地进入远征准备时使用；V4F 主动返程 = Retreat 统一 Return 路径）。
    /// </summary>
    public void OnReturnToTown()
    {
        if (_currentAdventure != null && _currentAdventure.IsActive)
        {
            // V4F：主动返程 → 统一 Return（Retreat）。损失比例未冻结，仅预留策略入口（SettleReturn 沿用既有占位语义）。
            RequestProgressionReturn(_currentAdventure);
            // WP-B：只有 Return 真正 Completed 才执行完成态清理（Reset elapsed provider / 清 adventure）；
            // 未完成时保留恢复状态，同一会话可直接再次触发只重试未完成阶段。
            if (FinalizeReturn(RunEndMode.Retreat))
            {
                ResetElapsedProvider();
                _currentAdventure = null;
                // WP-E：Return 完成 → 取消未完成 Presentation callback 并隐藏 Journey。
                var journeyOnTownReturn = JourneyRef;
                if (journeyOnTownReturn != null) journeyOnTownReturn.Hide();
            }
            return;
        }

        // WP-B：AdventureManager.IsActive == false 不得绕过未完成的 Return Transaction ——
        // 存在未 Completed Return 记录时优先 resume（不新建、不做「仅回领地」旁路、不在此处落盘）。
        if (TryResumeIncompleteReturnTransaction()) return;

        // WP-2 Defensive Return 收口：仅当冒险已结束（IsActive=false）且不存在未完成 Return 事务才到达此处——
        // IsActive=false 只由 SettleReturn 在全部 ProfessionExp ack 后产生（或 Legacy SetVictory/SetDefeat，
        // 无产品调用方），因此不存在未 Commit 的可兑现 Pending 被此处“仅回领地”旁路静默落盘。
        if (_currentAdventure != null)
        {
            _currentAdventure = null; // 已结束冒险（防御）
        }

        // 非活跃冒险：仅回领地（不产生 Return Settlement）
        if (_fromTown && _townUI != null)
        {
            gameObject.SetActive(false);
            _fromTown = false;
            _townUI.OnReturnFromExpedition();
        }
        else
        {
            // 非领地模式（如测试场景）：仅隐藏远征界面
            gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// V4F：统一 Run Return 产品路径（Retreat / SafeReturn / Defeat 共用）。
    ///
    /// 冻结顺序（不可改动）：
    ///   Run Return Settlement（不内含 Save）
    ///     → 保持 Expedition Occupancy（不清除、不登记为在村）
    ///     → Return Catch-up 完整逐日完成（elapsedEffectiveDays 全部逐日 TickDay）
    ///     → Return Remaining Carry
    ///     → Return Person Consequence
    ///     → Clear Expedition Occupancy
    ///     → 最终 Return Save（唯一一次把「Return 兑现 + Catch-up 后村庄状态」一起落盘）
    ///     → 进入 Town（TownUI.OnReturnFromExpedition 内部 SetSimulationActive(true)）
    ///
    /// WP-B Expedition Return Integrity：本方法不再假设每次调用都从 Begin 开始，
    /// 而是按现有 Return Record 相位 Begin or Resume
    ///（Fail Closed + Resume From Existing Phase + No Re-Begin + No Silent Loss）：
    ///   无 Record → Begin 新 Return 会话；
    ///   Active → 执行/重试 Settlement（全部 ProfessionExp ack 才可越过）；
    ///   ReturnSettled → 从 DaysSimulated 继续 Catch-up；
    ///   CatchUpComplete → 只补做 Carry / Consequence / Clear；
    ///   OccupancyCleared → 只重试 Final Save；
    ///   Completed → no-op；Failed → 拒绝（不自动恢复）。
    ///
    /// 前置阶段未成功时绝不越过：不 MarkSettled / 不 Catch-up / 不 Consequence / 不 Clear / 不落盘、不置 Completed。
    /// 返回值 = 本次 Return 是否**真正 Completed**；产品 caller 只有拿到 true 才允许
    /// Reset elapsed provider / 清 _currentAdventure / 隐藏恢复入口。
    /// </summary>
    public bool FinalizeReturn(RunEndMode mode)
    {
        // ---------- 0｜Begin or Resume（未完成 Record 绝不重新 Begin） ----------
        ExpeditionRecord record = VillageRuntimeBridge.ActiveReturnRecord;

        // Completed：跨调用 exactly-once 终态 → no-op（不重跑任何阶段、不重复 Save）。
        if (record != null && record.Completed) { FinishCompletedReturnPresentation(); return true; }

        // Failed：半完成记录禁止自动恢复（恢复 = Discard 记录 + 从最后一次成功 Save 重开）。
        if (record != null && record.Phase == ExpeditionReturnPhase.Failed)
        {
            Debug.LogError("[ExpeditionUI] Return 未完成：存在 Failed 会话记录（禁止自动续跑该日、禁止重新 Begin）。"
                + "恢复路径 = 丢弃会话记录并从最后一次成功存档（离村前状态）重新开始。");
            return false;
        }

        // resume 必须沿用会话原始出口模式：Defeat / SafeReturn 失败重试不得经通用 Retreat 入口被改写。
        if (record != null)
        {
            if (!VillageRuntimeBridge.TryGetPendingReturnMode(out RunEndMode originalMode))
            {
                Debug.LogError("[ExpeditionUI] 存在未完成 Return 记录但无法解析原始 RunEndMode：拒绝 resume"
                    + "（不得用其它出口模式改写原始 Return 出口）。");
                return false;
            }
            if (originalMode != mode)
            {
                Debug.LogWarning("[ExpeditionUI] Return resume 沿用会话原始出口模式 " + originalMode
                    + "（本次调用传入 " + mode + "，不改写）。");
            }
            mode = originalMode;
        }

        // OccupancyCleared 之后只剩「重试 Final Save」，不依赖活跃 Adventure 引用。
        if (_currentAdventure == null && (record == null || record.Phase != ExpeditionReturnPhase.OccupancyCleared))
        {
            return false;
        }

        // ---------- 1｜Begin（仅当无会话记录；Settlement 尚未执行过） ----------
        if (record == null)
        {
            long elapsedMinutes = VillageRuntimeBridge.ResolveExpeditionElapsedMinutes();
            if (!VillageRuntimeBridge.BeginExpeditionReturnMinutes(elapsedMinutes, mode))
            {
                Debug.LogWarning("[ExpeditionUI] Return 被忽略（已完成 / Catch-up 运行中 / 未完成记录待 resume / Failed 记录待恢复）：" + mode);
                return false;
            }
            record = VillageRuntimeBridge.ActiveReturnRecord;
        }
        if (record == null) return false;


        // ---------- 2｜T1 Settlement（Phase=Active；全部 ProfessionExp ack 才可越过） ----------
        if (record.Phase == ExpeditionReturnPhase.Active)
        {
            if (_currentAdventure == null) return false;

            ReturnSettlementResult settlement = _currentAdventure.SettleReturnDetailed(mode);
            if (!settlement.AllProfessionExpAcknowledged)
            {
                Debug.LogError("[ExpeditionUI] Return Settlement 未成功：仍有 " + settlement.ProfessionExpPendingRemaining
                    + " 点 ProfessionExp 未 ack（Phase 保持 Active、Pending 保留）。"
                    + "不 MarkSettled / 不 Catch-up / 不 Clear Occupancy / 不 Final Save；允许同会话重试 Settlement。");
                return false;
            }
            if (!VillageRuntimeBridge.MarkExpeditionReturnSettled())
            {
                Debug.LogError("[ExpeditionUI] Return Settlement 已 ack，但无法推进到 ReturnSettled（Phase=" + record.Phase + "）。");
                return false;
            }
            record = VillageRuntimeBridge.ActiveReturnRecord;
            if (record == null) return false;
        }

        // ---------- 3｜T3 Return Catch-up（Phase=ReturnSettled；从 DaysSimulated 继续） ----------
        if (record.Phase == ExpeditionReturnPhase.ReturnSettled)
        {
            ExpeditionCatchUpResult catchUp = VillageRuntimeBridge.RunReturnCatchUp();
            if (catchUp != ExpeditionCatchUpResult.Completed
                && catchUp != ExpeditionCatchUpResult.AlreadyCaughtUp
                && catchUp != ExpeditionCatchUpResult.AlreadyCompleted)
            {
                if (catchUp == ExpeditionCatchUpResult.WaitingForInteraction || catchUp == ExpeditionCatchUpResult.Yielded)
                {
                    JourneyRef?.ShowMessage("返程等待处理 / 续跑：" + VillageRuntimeBridge.WorldAdvanceDiagnostic);
                    return false;
                }
                Debug.LogError("[ExpeditionUI] Return Catch-up 未完成（" + catchUp
                    + "）：不 Clear、不落盘、不置 Completed。"
                    + "RetryableFailure / OverlapRejected 可在问题解除后同会话从 DaysSimulated 继续；"
                    + "PartialDayFailure → Failed（不自动续跑该日）。"
                    + "Failed 恢复 = 丢弃会话记录并从最后一次成功存档（离村前状态）重新开始。");
                return false;
            }
            record = VillageRuntimeBridge.ActiveReturnRecord;
            if (record == null) return false;
        }

        // ---------- 4｜T4 Carry + Person Consequence + Clear（Phase=CatchUpComplete） ----------
        if (record.Phase == ExpeditionReturnPhase.CatchUpComplete)
        {
            if (_currentAdventure == null)
            {
                Debug.LogError("[ExpeditionUI] Return resume 需要活跃 Adventure（RunState）才能提交 Remaining Carry / Person Consequence；"
                    + "当前引用不可用 → 保持未完成状态（不 Clear、不落盘）。");
                return false;
            }

            // WP-3（Gate 4）Return：Catch-up 全部完成后、Clear Occupancy 之前返还剩余 Carry。
            // 不提前返还（不参与其返回前那些天的 Village Consumption）；ExpeditionRecord 内 exactly-once。
            // WP-B：只有「成功返还 / 已返还」才能继续；Rejected → Fail Closed。
            int remainingCarry = 0;
            if (_currentAdventure.ProgressionState != null)
                remainingCarry = _currentAdventure.ProgressionState.Supply.CurrentCarryUnits;

            ReturnCarryStageResult carry = VillageRuntimeBridge.ReturnRemainingCarryStage(remainingCarry);
            if (carry == ReturnCarryStageResult.Rejected)
            {
                Debug.LogError("[ExpeditionUI] Return Remaining Carry 未成功（Rejected，本次剩余 " + remainingCarry
                    + "）：不提交 Person Consequence、不 Clear Occupancy、不落盘。");
                return false;
            }

            // V4 Gate 2：Return Person Consequence（Health + Character Expedition Experience）。
            // 只能在 Catch-up 成功之后、Clear 之前、Final Save 之前提交（会话内 exactly-once；
            // 重复/失败由 ExpeditionRecord 标记与 max(Injury) 单调规则保证）。
            // WP-B：Rejected 必须 Fail Closed（不得 warning 后继续 Clear / Save）。
            var consequence = VillageReturnPersonConsequence.TryCommit(
                VillageRuntimeBridge.ActiveReturnRecord,
                _currentAdventure.RunState,
                mode,
                VillageRuntimeBridge.Time,
                _playerData);
            if (consequence.Rejected)
            {
                Debug.LogError("[ExpeditionUI] Return Person Consequence 未提交（" + consequence.RejectReason
                    + "）：不 Clear Occupancy、不落盘（Fail Closed）。");
                return false;
            }

            // 只有前面全部满足才允许 Clear Expedition Occupancy。
            var suppliedProgression = _currentAdventure.ProgressionState;
            VillageRuntimeBridge.ApplyReturnedExpeditionNutrition(_playerData,
                suppliedProgression != null && suppliedProgression.ExpeditionElapsedMinutes > 0
                && suppliedProgression.Supply.PerPersonDailyConsumptionRate > 0);
            VillageRuntimeBridge.ClearExpeditionPersonIds();
            record = VillageRuntimeBridge.ActiveReturnRecord;
            if (record == null) return false;
            if (!record.Completed && record.Phase != ExpeditionReturnPhase.OccupancyCleared)
            {
                Debug.LogError("[ExpeditionUI] Clear Expedition Occupancy 后相位异常（Phase=" + record.Phase
                    + "）：不落盘、不置 Completed。");
                return false;
            }
        }

        // ---------- 5｜T5 Final Save（Phase=OccupancyCleared；false 时只允许 retry Save） ----------
        if (record.Phase == ExpeditionReturnPhase.OccupancyCleared)
        {
            // 首次尝试走既有产品收尾（唯一一次最终 Return Save + 进入 Town）；
            // resume 重试只重做 Final Save 本身（Settlement / Catch-up / Carry / Consequence / Clear
            // 与既有 UI 转换都已在前一次尝试完成，不得重跑）。
            bool saveOk = RetryFinalReturnSave();
            VillageRuntimeBridge.CompleteExpeditionReturnSave(saveOk);
            if (!saveOk)
            {
                JourneyRef?.ShowMessage("保存失败。世界保持暂停；请重试返程保存。已完成阶段不会重跑。");
                Debug.LogError("[ExpeditionUI] 最终 Return Save 失败：保持 OccupancyCleared / Completed=false，"
                    + "只允许重试 Final Save（不重结算 / 不重补算 / 不重复返还 Carry / 不重复提交 Consequence）。");
                return false;
            }
        }

        ExpeditionRecord finalRecord = VillageRuntimeBridge.ActiveReturnRecord;
        bool completed = finalRecord != null && finalRecord.Completed;
        if (completed) FinishCompletedReturnPresentation();
        if (!completed)
        {
            Debug.LogError("[ExpeditionUI] FinalizeReturn 结束时 Return 未 Completed（Phase="
                + (finalRecord != null ? finalRecord.Phase.ToString() : "null")
                + "）：不得执行完成态清理（Reset elapsed provider / 清 adventure / 隐藏恢复入口）。");
        }
        return completed;
    }

    /// <summary>
    /// WP-B：仅重试最终 Return Save（Phase=OccupancyCleared 的 resume 路径）。
    /// 不重跑 Settlement / Catch-up / Carry / Consequence / Clear，也不重复任何 UI 转换副作用
    /// （首次尝试已完成「进入 Town」的 UI 转换）；只做唯一一次最终 Return Save 本身。
    /// </summary>
    private bool RetryFinalReturnSave()
    {
        return _playerData != null && SaveSystem.SaveReturnFinal(_playerData);
    }

    /// <summary>
    /// WP-B：存在未 Completed 的 Return 记录时优先 resume Return Transaction，
    /// 阻止「AdventureManager.IsActive == false」的旧分支绕过未完成 Return 事务
    /// （不得在未完成事务上清 Adventure / 落盘 / 返回 Town）。
    /// 返回 true = 本次调用已被 Return 事务接管（调用方不得再做普通返回或完成态清理）；
    /// resume 真正 Completed 时由本方法执行完成态清理（Reset elapsed provider / 清 adventure / 隐藏推进面板），
    /// 与 OnReturnToTown / RequestRetreatFromProgression 的成功语义一致；
    /// 无法解析原始出口模式或 resume 未完成时同样返回 true（Fail Closed：保持未完成状态，不做半途清理）。
    /// </summary>
    private bool TryResumeIncompleteReturnTransaction()
    {
        ExpeditionRecord record = VillageRuntimeBridge.ActiveReturnRecord;
        if (record == null || record.Completed) return false;

        if (!VillageRuntimeBridge.TryGetPendingReturnMode(out RunEndMode originalMode))
        {
            Debug.LogError("[ExpeditionUI] 存在未完成 Return 记录（Phase=" + record.Phase
                + "）但无法解析原始 RunEndMode：拒绝普通返回旁路（保持未完成状态，不做完成态清理）。");
            return true;
        }

        if (FinalizeReturn(originalMode))
        {
            // Return 已真正 Completed：此时才允许完成态清理（与其它产品出口同一语义）。
            ResetElapsedProvider();
            _currentAdventure = null;
            var journeyOnResume = JourneyRef;
            if (journeyOnResume != null) journeyOnResume.Hide();
            if (_progressionPanel != null) _progressionPanel.Hide();
        }
        else
        {
            Debug.LogError("[ExpeditionUI] Return resume 未 Completed：保留 elapsed provider / adventure / 恢复入口"
                + "（未完成事务不得执行完成态清理）。");
        }
        return true;
    }

    /// <summary>
    /// Final Return Save + 进入 Town 的 UI 收尾（WP-B：仅用于 Return 首次到达 OccupancyCleared 的那一次尝试）。
    /// 领地路径：TownUI.OnReturnFromExpedition（内部 SaveSystem.Save = 最终 Return Save + sim=true）；
    /// 非领地演示/测试路径：直接执行最终 Save 并留在远征准备界面（沿用既有调试行为）。
    /// Final Save 失败后的重试只走 RetryFinalReturnSave（只重做 Save，不重复 UI 转换）。
    /// </summary>
    private void FinishCompletedReturnPresentation()
    {
        if (VillageRuntimeBridge.ActiveReturnRecord == null || !VillageRuntimeBridge.ActiveReturnRecord.Completed) return;
        if (_fromTown && _townUI != null)
        {
            gameObject.SetActive(false);
            _fromTown = false;
            _townUI.ShowAfterCompletedExpedition();
            return;
        }

        gameObject.SetActive(true);
        ShowExpeditionUI();
    }

    public void OnReturnFromBattle(bool isVictory = false)
    {
        if (_currentAdventure == null) return;

        // WP-1 Stage2：Progression 会话存在 → 产品主流程走 Resolver 结算
        //（SafeReturn 判定基于 RouteCompleted；Defeat/胜利均 exactly-once 结算当前 Node）。
        var progressionAdventure = _currentAdventure;
        if (progressionAdventure.ProgressionState != null)
        {
            HandleProgressionReturnFromBattle(progressionAdventure, isVictory);
            return;
        }

        bool adventureOver = false;
        RunEndMode endMode = RunEndMode.SafeReturn;

        if (!isVictory)
        {
            // V4F：全灭/被迫撤退 → 统一 Return（Defeat）。损失比例未冻结，SettleReturn 沿用既有占位语义。
            Log("💀 冒险失败...");
            endMode = RunEndMode.Defeat;
            adventureOver = true;
        }
        else if (_currentAdventure.MapGen.IsAtBoss)
        {
            // V4F：击败 Boss 安全返程 → 统一 Return（SafeReturn）全额兑现 Pending Rewards。
            Log("🎉 击败 Boss！冒险胜利！");
            endMode = RunEndMode.SafeReturn;
            adventureOver = true;
        }

        if (adventureOver)
        {
            FinalizeReturn(endMode);
            return;
        }

        // WP-B：IsActive == false 不得绕过未完成的 Return Transaction（先 resume，再考虑既有防御分支）。
        if (TryResumeIncompleteReturnTransaction()) return;

        // WP-2 Defensive Return 收口（Legacy 兜底分支）：同 HandleProgressionReturnFromBattle——IsActive=false
        // 只由 SettleReturnDetailed 在全部 ProfessionExp ack 后产生（或 Legacy SetVictory/SetDefeat，无产品调用方），
        // 到达本分支时不存在未 Commit 的可兑现 ProfessionExp；不得改造成 Settlement 旁路 / 额外 Save。
        if (!_currentAdventure.IsActive)
        {
            // 防御：冒险已结束但未走 adventureOver 分支（沿用既有兜底，非 V4F 主 Return 路径）。
            VillageRuntimeBridge.ClearExpeditionPersonIds();
            if (_fromTown && _townUI != null)
            {
                gameObject.SetActive(false);
                _fromTown = false;
                _townUI.OnReturnFromExpedition();
            }
            else
            {
                gameObject.SetActive(true);
                ShowExpeditionUI();
            }
            return;
        }

        if (_currentAdventure != null)
        {
            var journeyAfterLegacyBattle = JourneyRef;
            if (journeyAfterLegacyBattle != null)
            {
                if (_progressionPanel != null) _progressionPanel.Hide();
                journeyAfterLegacyBattle.ShowAfterEncounter(_currentAdventure, this);
            }
            else
            {
                EnsureProgressionPanel().Show(_currentAdventure, this); // Compatibility fallback（非正式表现）
            }
        }
    }

    void Log(string msg)
    {
        if (infoText != null)
            infoText.text = msg + "\n" + infoText.text;
    }
}
