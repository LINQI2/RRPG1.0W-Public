using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using RRPG.StaticConfig;
using UnityEngine.Serialization;

public partial class BattleUI : MonoBehaviour
{
    [Header("预制体")]
    public GameObject unitCardPrefab;
    public GameObject prepCardPrefab;

    [Header("Unit Presentation — Player / Monster Unit Scale")]
    [Tooltip("我方人物 / 英雄单位根缩放；Prep 人物手牌另由 Prep Card Scale 调整。")]
    public Vector3 unitCardScale = Vector3.one;     // 角色/英雄卡缩放
    [Tooltip("敌方单位根缩放；不影响人物 Prep Hand 或 Battle Action Cards。")]
    public Vector3 monsterCardScale = Vector3.one;   // 怪物卡缩放

    [Header("字体")]
    public TMP_FontAsset uiFont;

    [Header("我方战场排")]
    public RectTransform playerFrontRow;
    public RectTransform playerMiddleRow;
    public RectTransform playerBackRow;

    [Header("敌方战场排（public 供 BattlePrepCard 访问）")]
    public RectTransform enemyFrontRow;
    public RectTransform enemyMiddleRow;
    public RectTransform enemyBackRow;

    [Header("Battle Action Hand — 战中行动牌容器")]
    public RectTransform handContainer;

    [Header("Prep Character Hand — 战前人物卡容器")]
    [Tooltip("准备人物卡的创建、回手、拖拽和清理宿主；与 BattleCanvas 同尺寸和坐标原点，不复用战中行动牌容器。")]
    [SerializeField] private RectTransform prepCardContainer;

    // 旧场景及局部测试尚未绑定时保留原 Canvas 行为；正式场景显式绑定专用容器。
    // 只读解析，不创建或重挂对象。
    private RectTransform PrepCardHost => prepCardContainer != null
        ? prepCardContainer
        : infoText != null && infoText.canvas != null ? infoText.canvas.transform as RectTransform : null;

    [Header("Legacy 序列化引用（已退出正式产品路径，仅用于运行时清除旧 UI；序列化资产待 Unity Editor 清理）")]
    [Tooltip("旧道具手牌根：WP-C 起 Battle 不再存在独立 Item Hand / Item Card 展示")]
    [HideInInspector] public RectTransform itemCardRoot;

    [Tooltip("旧玩家操作菜单按钮（Attack / Skill / Guard）：WP-C 起退出正式产品路径，仅用于运行时清除")]
    [HideInInspector] public Button btnAttackOp;
    [HideInInspector] public Button btnSkillOp;
    [HideInInspector] public Button btnGuardOp;

    [Header("当前卡牌操作")]
    [Tooltip("Current Card Cancel：取消待出牌与目标选择（Battle V0.1 有效操作）")]
    public Button btnCancelOp;

    [Header("全局按钮")]
    public Button btnStartBattle;
    public Button btnEndTurn;

    [Header("Debug / Diagnostics — 日志宿主")]
    public TMP_Text infoText;
    public bool showDebugInfo;

    [Header("Battle V0.1 共享牌库 / 共享行动力（Stage 1 集成）")]
    [Tooltip("挂载于本 GameObject 的 BattleV1Runtime（正式战斗运行时控制器，复用 handContainer）")]
    public BattleV1Runtime v1Runtime;

    // WP-D：旧「左列 / 中列 / 右列」固定偏移参数已退出。
    // 排内视觉位置改由 BattleUIFormationPresentation.cs 的 Formation Presentation 统一计算
    //（singleUnitVisualOffset / twoUnitVisualSpacing / threeUnitVisualSpacing / rowLocalVisualOffset）。

    [Header("调试信息（运行时查看）")]
    [SerializeField, HideInInspector] private List<string> _deployedUnitsDebug = new List<string>();

    [Header("Prep Character Hand — Canvas 中心坐标；不影响 Battle Hand")]
    [Tooltip("整个可见人物手牌的中心，相对 BattleCanvas 中心。旧 Start Position 数值保留，但现在表示整组中心。")]
    [FormerlySerializedAs("handStartPosition")]
    public Vector2 prepHandCenterOffset = new Vector2(-200, -300);
    [Tooltip("Prep 卡根缩放乘数，相对人物卡 Prefab 根缩放；默认 1 保留制作人 Prefab 缩放。")]
    [Min(0.01f)] public float prepCardScale = 1f;
    [Tooltip("Prep 卡中心的偏好间距；数量增加时按 Canvas 可用宽度缩小。")]
    [FormerlySerializedAs("handCardSpacing"), Min(0f)]
    public float prepHandBaseStep = 200f;
    [Tooltip("Prep 间距的偏好下限；容器不足时允许更多重叠以保留可见性。")]
    [Min(0f)] public float prepHandMinStep = 70f;

    [Header("Formation Presentation — Temporary Drag Preview")]
    [Tooltip("只叠加到目标排成员的正常人数布局；不改变正常位置或逻辑列。")]
    public float dragPreviewOffset = 30f;

    private BattleSystem _battleSystem;
    private bool _battleStarted;
    private bool _waitingForAction;
    private bool _actionTaken;
    private bool _isEndingTurn;
    private BattleCard _v1PendingCard;
    // Battle V0.1 / WP-C：不再存在角色个人 AP / TP 门槛，也不再存在旧 Attack / Skill / Guard / UsingItem 操作状态

    /// <summary>手牌拖拽中标记（由 BattlePrepCard 设置）</summary>
    public bool IsPrepDragging { get; set; }

    private Dictionary<RectTransform, List<GameObject>> _rowCards;
    private List<RectTransform> _allRows;

    private Dictionary<string, BattleRow> _pendingRows;
    private Dictionary<string, BattleColumn> _pendingCols;

    private List<CharacterCardData> _allCharCards;
    private List<CharacterInstance> _allCharacters;
    private List<CharacterData> _allCharDatas;
    private List<SkillData> _allSkills;
    /// <summary>
    /// M2 Slice 2A（C3）：敌方怪物定义 = StaticConfig <see cref="MonsterDefinition"/>。
    /// 物化为 List 以保持既有的 Count / 索引器 / Find 语义（不改为 Registry 按 ID 查找：本 Slice 是行为等价迁移）。
    /// 产品链数据来自 StaticConfigRegistry；本类不做 fallback 判定。
    /// </summary>
    private List<MonsterDefinition> _enemyMonsters;
    private HeroInstance _playerHero;
    private HeroData _playerHeroData;
    private List<CharacterCardData> _selectedChars;
    private bool _heroDeployed;
    private bool _initializedFromExpedition = false;
    private Coroutine _battleLoopCoroutine;

    /// <summary>按 CardId（GUID）查找 CharacterInstance，防止同 CharacterId 塌缩</summary>
    private Dictionary<string, CharacterInstance> _charInstByCardId = new Dictionary<string, CharacterInstance>();

    /// <summary>
    /// 从远征系统初始化战斗数据
    /// 由 ExpeditionUI 在确认出征后调用
    /// Battle V0.1 / WP-C：不再接收独立道具卡列表（Item 产品语义已退出正式路径）。
    /// M2 Slice 2A（C3）：敌方怪物入参类型 = IReadOnlyList&lt;MonsterDefinition&gt;（StaticConfig Authority）。
    /// </summary>
    public void InitFromExpedition(
        List<CharacterCardData> playerChars,
        HeroInstance playerHero,
        List<CharacterData> allCharDatas,
        List<SkillData> allSkills,
        IReadOnlyList<MonsterDefinition> enemyMonsters,
        PlayerCollection playerData = null)
    {
        _initializedFromExpedition = true;
        _allCharCards = playerChars;
        _playerHero = playerHero;
        _playerData = playerData;
        // BattleUI 会跨节点复用；CardId → 实例只属于本次初始化。
        _charInstByCardId.Clear();
        _playerHeroData = null;

        // 兜底：自动查找结算 UI
        if (_resultUI == null)
            _resultUI = FindObjectOfType<BattleResultUI>(true);

        // 从 DataManager 加载剩余数据
        _allCharDatas = allCharDatas;
        _allSkills = allSkills;
        // M2 Slice 2A（C3）：物化 IReadOnlyList → List，保持既有 Count / 索引器 / Find 调用点零改动。
        // M4：null（生成根缺失 / 未导表）时不得抛异常、也不得静默继续 —— 明确报错，并由既有开战入口
        //（OnStartBattle / ExecuteBattle）拒绝本次战斗初始化。
        _enemyMonsters = enemyMonsters != null
            ? new List<MonsterDefinition>(enemyMonsters)
            : new List<MonsterDefinition>();
        if (enemyMonsters == null)
        {
            Debug.LogWarning("[BattleUI] InitFromExpedition 收到 null 敌方定义（生成根缺失 / 未导表？）："
                + StaticConfigRegistry.MonsterDiagnostic);
        }

        if (_playerHero != null)
        {
            _playerHeroData = DataManager.Instance.GetHeroById(_playerHero.HeroId);
        }

        // 从 PlayerCollection 加载角色实例（每个 card 创建一个独立实例，防止同 CharacterId 塌缩）
        _allCharacters = new List<CharacterInstance>();
        foreach (var card in _allCharCards)
        {
            var cd = allCharDatas.Find(d => d.CharacterId == card.CharacterId);
            // 人物显示名优先（具体 Person / PersonDefinition）；战斗定义名仅作 Legacy 兜底。
            var ci = new CharacterInstance(card.CharacterId,
                ResolvePersonDisplayName(card) ?? cd?.DisplayName ?? card.CardId,
                cd?.BaseHP ?? 10,
                cd?.BaseAttack ?? 3);

            // M1：战斗视图实例必须与卡所对应的长期人物共享同一 PersonId，
            // 这样 Run HP 初始化/回写/结算/牌过滤全部落到同一个具体人物（同模板多实例不塌缩）。
            if (_playerData != null)
            {
                string longTermPersonId = _playerData.ResolvePersonIdForCard(card);
                if (!string.IsNullOrEmpty(longTermPersonId)) ci.PersonId = longTermPersonId;
            }

            // Stage 6A：Run 跨战 HP 继承（从 Run 状态按 PersonId 初始化，而非每战满血）
            if (_runState != null)
            {
                int runHp = _runState.GetPersonCurrentHp(ci.PersonId, ci.MaxHP);
                if (runHp > 0) ci.CurrentHP = runHp;
            }
            // 保存到字典以便按 CardId 查找（而非按 CharacterId）
            _charInstByCardId[card.CardId] = ci;
            _allCharacters.Add(ci);
        }

    }

    /// <summary>
    /// 以远征准备的数据启动战斗
    /// 跳过 InitializeDefaultBattleData，直接进入战斗流程
    /// </summary>
    public void StartBattleWithExpedition()
    {
        // Scene 中 BattleUI 可能已激活并启动 Demo loop；正式节点只允许一个本场 loop。
        if (_battleLoopCoroutine != null) StopCoroutine(_battleLoopCoroutine);
        _battleLoopCoroutine = StartCoroutine(RunBattleWithExpedition());
    }

    /// <summary>
    /// 初始化 Battle V0.1 共享牌库与共享行动力（正式战斗运行时）。
    /// 由 ExecuteBattle 在正式战斗开始时调用；集成验证亦直接调用此方法。
    /// 阵容来源：本场我方阵容（_allCharCards 的 CardId 作为具体人物实例ID占位 + 英雄）。
    /// </summary>
    public void InitV1Runtime()
    {
        if (v1Runtime == null)
        {
            Debug.LogWarning("[BattleUI] v1Runtime 未绑定，跳过 V0.1 共享牌库初始化");
            return;
        }

        // 正式 Battle Card Hand 始终使用此 BattleUI 已绑定的 BattleActionHandRoot。
        v1Runtime.handContainer = handContainer;

        // 本场实际参战名单（Active Battle Roster）的唯一事实来源 = 真实成功部署的 BattleData.PlayerUnits：
        //   只有真正部署成功的角色 / 英雄才进入本场共享牌库（Current GameSpec R8.1），
        //   不再用 _pendingRows 推断「已参战」——避免 UI 卡 / PlayerUnits / 共享牌库三方不一致与 ghost 牌主。
        // M1 人物身份语义仍走 GetPersonIdOfUnit 单一入口（不建立第二套身份 Authority）。
        var deployed = BuildActiveRosterPersonIds();
        // Stage 6A：优先从当前 Run Deck 构筑本场牌库（跨战保留）；无 Run 状态时回退 DemoCardPool 种子
        v1Runtime.InitForBattle(deployed, _runState != null ? _runState.GetRunDeckEntries() : null);

        // Current GameSpec R10.2：正式基础抽牌数 = 当前仍具备战斗能力的我方角色数量（配置化，不写死）
        v1Runtime.DrawCountProvider = GetActiveRosterCount;

        // Stage 2：共享手牌点击 → 出牌/选目标（幂等订阅，避免多次进入战斗重复挂接）
        v1Runtime.CardClicked -= OnV1CardClicked;
        v1Runtime.CardClicked += OnV1CardClicked;

        // Gate 0：接线配置化卡牌规则管线（真实 Battle Fact 视图 + 自动目标预览 + 回合开始护盾清理）
        InitGate0RulePipeline();
    }

    /// <summary>
    /// 远征版 RunBattle（跳过 InitializeDefaultBattleData，直接进入战前准备）
    /// </summary>
    private System.Collections.IEnumerator RunBattleWithExpedition()
    {
        // 清理上一场战斗残留的 UI 物体
        CleanupBattleUI();
        
        _selectedChars = new List<CharacterCardData>(_allCharCards);
        _pendingRows = new Dictionary<string, BattleRow>();
        _pendingCols = new Dictionary<string, BattleColumn>();
        _battleStarted = false;
        _heroDeployed = false;

        _allRows = new List<RectTransform> { playerFrontRow, playerMiddleRow, playerBackRow, enemyFrontRow, enemyMiddleRow, enemyBackRow };
        _rowCards = new Dictionary<RectTransform, List<GameObject>>();
        foreach (var row in _allRows) _rowCards[row] = new List<GameObject>();

        ShowPrepPhase();
        Log("战前准备：拖拽角色/英雄卡到战场排上阵");
        // 等待玩家点击「开始战斗」。部署完整性失败时 ExecuteBattle 会把 _battleStarted 复位并回到本循环，
        // 保持战前准备阶段并等待玩家重新开始（不缩减阵容继续、不锁死在半启动状态）。
        while (true)
        {
            yield return new WaitUntil(() => _battleStarted);
            yield return StartCoroutine(ExecuteBattle());
            if (_battleStarted) break;
        }
    }

    void Start()
    {
        RemoveLegacyProductUIObjects();
        ValidateBindings();
        BindButtons();
        ApplyVisualHostDefaults();

        // 如果已经从远征系统初始化过，跳过默认战斗启动
        if (_initializedFromExpedition)
        {
            return;
        }

        // SampleScene 的 Town/MainMenu 产品入口可能先激活 BattleUI 宿主。
        // 未进入 Battle 节点时不能自行生成 Demo Prep（否则失败节点也出现幽灵敌人卡）。
        if (FindObjectOfType<MainMenuUI>(true) != null) return;

        _battleLoopCoroutine = StartCoroutine(RunBattle());
    }

    void ValidateBindings()
    {
        if (playerFrontRow == null) Debug.LogError("playerFrontRow 未绑定");
        if (playerMiddleRow == null) Debug.LogError("playerMiddleRow 未绑定");
        if (playerBackRow == null) Debug.LogError("playerBackRow 未绑定");
        if (enemyFrontRow == null) Debug.LogError("enemyFrontRow 未绑定");
        if (enemyMiddleRow == null) Debug.LogError("enemyMiddleRow 未绑定");
        if (enemyBackRow == null) Debug.LogError("enemyBackRow 未绑定");
        if (infoText == null) Debug.LogError("infoText 未绑定");
        if (uiFont == null) uiFont = CurrentUIFontAuthority.Default;
        if (infoText != null) infoText.gameObject.SetActive(showDebugInfo);
        if (unitCardPrefab == null) Debug.LogError("unitCardPrefab 未设置");
        if (prepCardPrefab == null) Debug.LogError("prepCardPrefab 未设置");
        if (handContainer == null) Debug.LogError("handContainer 未设置");
    }

    /// <summary>
    /// WP-C：正式产品路径不再包含 Legacy 玩家操作菜单（Attack / Skill / Guard）与独立道具手牌（Item Hand）。
    /// 旧场景对象如仍被序列化引用绑定，在此清除其 GameObject；
    /// 序列化资产本身仍待 Unity Editor 物理清理（本任务无 MCP 通道，不直接编辑 YAML）。
    /// </summary>
    private void RemoveLegacyProductUIObjects()
    {
        bool removedAny = false;

        if (btnAttackOp != null) { btnAttackOp.onClick.RemoveAllListeners(); DestroyLegacyObject(btnAttackOp.gameObject); btnAttackOp = null; removedAny = true; }
        if (btnSkillOp != null) { btnSkillOp.onClick.RemoveAllListeners(); DestroyLegacyObject(btnSkillOp.gameObject); btnSkillOp = null; removedAny = true; }
        if (btnGuardOp != null) { btnGuardOp.onClick.RemoveAllListeners(); DestroyLegacyObject(btnGuardOp.gameObject); btnGuardOp = null; removedAny = true; }

        if (itemCardRoot != null)
        {
            foreach (Transform child in itemCardRoot) DestroyLegacyObject(child.gameObject);
            DestroyLegacyObject(itemCardRoot.gameObject);
            itemCardRoot = null;
            removedAny = true;
        }

        if (removedAny)
            Debug.LogWarning("[BattleUI] WP-C：旧 Attack / Skill / Guard 菜单与独立道具手牌（Item Hand）已从正式产品路径移除。");
    }

    /// <summary>
    /// 旧 UI 对象销毁：运行中用 Destroy，EditMode / Editor 上下文中用 DestroyImmediate
    /// （EditMode 直接调用 Destroy 会被引擎拒绝并报错）。
    /// </summary>
    private static void DestroyLegacyObject(Object obj)
    {
        if (obj == null) return;
        if (Application.isPlaying) Destroy(obj);
        else DestroyImmediate(obj);
    }

    void BindButtons()
    {
        // Current Card Cancel：只取消共享手牌待出牌与目标选择，不复活旧操作菜单状态机
        BindButton(btnCancelOp, CancelPendingCard);
        BindButton(btnStartBattle, OnStartBattle);
        BindButton(btnEndTurn, OnEndTurn);
    }

    private static void BindButton(Button button, UnityEngine.Events.UnityAction callback)
    {
        if (button == null) return;
        // Own only this callback; preserve Inspector events and other listeners.
        button.onClick.RemoveListener(callback);
        for (int i = 0; i < button.onClick.GetPersistentEventCount(); i++)
        {
            if (button.onClick.GetPersistentTarget(i) == callback.Target as Object
                && button.onClick.GetPersistentMethodName(i) == callback.Method.Name
                && button.onClick.GetPersistentListenerState(i) != UnityEngine.Events.UnityEventCallState.Off)
                return;
        }
        button.onClick.AddListener(callback);
    }

    void ReassignColumns(RectTransform row, float dropScreenY)
    {
        var cards = _rowCards[row].Where(c => c != null && !c.name.StartsWith("EnemyPrep")).ToList();
        if (cards.Count == 0) return;

        // 分离出正在拖拽的卡（如果有）
        GameObject dragCard = null;
        if (!string.IsNullOrEmpty(_dragCardId))
        {
            dragCard = cards.Find(c => c != null && c.name == "Card_" + _dragCardId);
        }

        // 对已有卡（不含拖拽卡）按屏幕 Y 从高到低排序
        var sortedExisting = cards
            .Where(c => c != dragCard)
            .OrderByDescending(c => RectTransformUtility.WorldToScreenPoint(null, c.transform.position).y)
            .ToList();

        // 根据拖拽掉落位置决定插入点
        int insertIdx = sortedExisting.Count;
        for (int i = 0; i < sortedExisting.Count; i++)
        {
            float cardY = RectTransformUtility.WorldToScreenPoint(null, sortedExisting[i].transform.position).y;
            if (dropScreenY > cardY)
            {
                insertIdx = i;
                break;
            }
        }

        // 构建最终顺序
        var finalOrder = new List<GameObject>();
        for (int i = 0; i < sortedExisting.Count; i++)
        {
            if (i == insertIdx && dragCard != null)
                finalOrder.Add(dragCard);
            finalOrder.Add(sortedExisting[i]);
        }
        if (insertIdx == sortedExisting.Count && dragCard != null)
            finalOrder.Add(dragCard);

        if (finalOrder.Count == 0)
        {
            finalOrder = cards.OrderByDescending(c =>
                RectTransformUtility.WorldToScreenPoint(null, c.transform.position).y).ToList();
        }

        // 分配列（R5.0：一排只有 Left / Center / Right 三个逻辑列；第 4 名成员不是合法上阵，不获得任何列写入）
        AssignRowColumns(row, finalOrder);
    }

    string GetCardId(string name)
    {
        if (name.StartsWith("Card_")) return name.Substring(5);
        return null;
    }

    IEnumerator RunBattle()
    {
        InitializeDefaultBattleData();
        _selectedChars = new List<CharacterCardData>();
        _pendingRows = new Dictionary<string, BattleRow>();
        _pendingCols = new Dictionary<string, BattleColumn>();
        _battleStarted = false;
        _heroDeployed = false;

        _allRows = new List<RectTransform> { playerFrontRow, playerMiddleRow, playerBackRow, enemyFrontRow, enemyMiddleRow, enemyBackRow };
        _rowCards = new Dictionary<RectTransform, List<GameObject>>();
        foreach (var row in _allRows) _rowCards[row] = new List<GameObject>();

        ShowPrepPhase();
        Log("战前准备：拖拽角色/英雄卡到战场排上阵");
        // 等待玩家点击「开始战斗」。部署完整性失败时 ExecuteBattle 会把 _battleStarted 复位并回到本循环，
        // 保持战前准备阶段并等待玩家重新开始（不缩减阵容继续、不锁死在半启动状态）。
        while (true)
        {
            yield return new WaitUntil(() => _battleStarted);
            yield return StartCoroutine(ExecuteBattle());
            if (_battleStarted) break;
        }
    }

    void ShowPrepPhase()
    {
        if (btnStartBattle != null) btnStartBattle.gameObject.SetActive(true);
        if (btnEndTurn != null) btnEndTurn.gameObject.SetActive(false);

        var ct = PrepCardHost;
        if (ct == null)
        {
            Debug.LogError("[BattleUI] 缺少准备卡容器，无法显示战前人物卡。");
            return;
        }

        CreateInitialPrepHandCards(ct);

        // WP-Encounter-1C §10：产品路径的敌人显示**只**来自 Active EnemyRoster
        // （本场身份 = EnemyInstanceId；站位 = 1B 已冻结的 Row / Column；模板信息走 MonsterId）。
        // Legacy / Demo 路径（未绑定 roster）保持既有目录驱动行为不变。
        if (_activeEnemyRosterBound)
        {
            CreateEnemyPrepCardsFromActiveRoster();
            return;
        }

        for (int i = 0; i < _enemyMonsters.Count; i++)
        {
            var m = _enemyMonsters[i];
            BattleColumn col = (BattleColumn)(i % 3);
            float vo = GetFormationColumnOffset(col);   // WP-D：统一走 Formation Presentation 视觉网格
            var go = Instantiate(unitCardPrefab, enemyBackRow, false);
            go.transform.localScale = monsterCardScale;
            go.name = "EnemyPrep_" + m.MonsterId;
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0, vo);

            var tn = go.transform.Find("Text_Name")?.GetComponent<TMP_Text>();
            var th = go.transform.Find("Text_HP")?.GetComponent<TMP_Text>();
            var ta = go.transform.Find("Text_ATK")?.GetComponent<TMP_Text>();
            if (tn != null) tn.text = m.Name;
            if (th != null) th.text = m.MaxHP + "/" + m.MaxHP;
            if (ta != null) ta.text = m.Attack + "";

            // M3 Slice 1D-1：Prep 敌方身体视觉与 Battle 走**同一个** Resolver
            //（MonsterId → EnemyPresentationResolver → Sprite）；缺数据时的 fallback / 诊断在 Resolver 内。
            ApplyEnemyBodyVisual(go, m.MonsterId);

            _rowCards[enemyBackRow].Add(go);
        }
    }

    /// <summary>
    /// 上阵英雄卡（战前布阵或战中补上阵）。
    /// </summary>
    /// <returns>true=上阵成功（UI 卡已建立）；false=被拒绝（零写入，调用方不得隐藏准备卡）</returns>
    public bool PlaceHero(HeroData heroData, HeroInstance heroInstance, RectTransform targetRow)
    {
        BattleRow bRow = RectToBattleRow(targetRow);
        string heroId = heroData.HeroId;

        if (_battleStarted)
        {
            // 战斗中上阵：DeployHero 的 bool 返回值是防御纵深检查（Board 占位 / 上阵数量失败不得建 ghost UI）
            bool deployed = _battleSystem != null
                && _battleSystem.DeployHero(heroInstance, heroData, new List<SkillData>(), bRow, BattleColumn.Center);
            if (deployed)
            {
                var go = Instantiate(unitCardPrefab, targetRow, false);
                go.name = "Card_" + heroId;
                var rt = go.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = Vector2.zero;
                FillUnitCard(go, "[英雄]" + heroData.DisplayName, heroData.BaseHP, heroData.BaseAttack, heroData.ActionPoints,
                    heroInstance != null ? heroInstance.PersonId : null, heroId);
                var btn = go.GetComponent<Button>();
                if (btn != null) { string sid = heroId; btn.onClick.AddListener(() => OnUnitCardClick(sid)); btn.interactable = true; }
                _rowCards[targetRow].Add(go);
                ReassignColumns(targetRow, Input.mousePosition.y);
                Log("战斗中上阵英雄 " + heroData.DisplayName + " → " + bRow);
            }
            else
            {
                Log("英雄上阵失败（Board 占位或上阵数量限制），未创建单位");
            }
            RearrangeHandCards();
            return deployed;
        }

        EnsureRowDataReady();

        // Current GameSpec R4.1（总数 ≤ 6）+ R5.0（一排 ≤ 3）：失败零写入，不产生部分布阵
        string reason;
        if (!TryValidateRowPlacement(heroId, targetRow, out reason))
        {
            Log(reason);
            return false;
        }

        _heroDeployed = true;
        _pendingRows[heroId] = bRow;

        string cardName = "Card_" + heroId;
        GameObject existingCard = null;
        RectTransform previousRow = null;
        foreach (var kv in _rowCards)
        {
            var c = kv.Value.Find(x => x != null && x.name == cardName);
            if (c != null) { existingCard = c; previousRow = kv.Key; break; }
        }

        GameObject go2;
        if (existingCard != null)
        {
            go2 = existingCard;
            foreach (var kv in _rowCards) kv.Value.Remove(go2);
            go2.transform.SetParent(targetRow, false);
        }
        else
        {
            go2 = Instantiate(unitCardPrefab, targetRow, false);
            go2.name = cardName;
        }

        var rt2 = go2.GetComponent<RectTransform>();
        rt2.anchorMin = new Vector2(0.5f, 0.5f);
        rt2.anchorMax = new Vector2(0.5f, 0.5f);
        rt2.anchoredPosition = Vector2.zero;

        FillUnitCard(go2, "[英雄]" + heroData.DisplayName, heroData.BaseHP, heroData.BaseAttack, 0,
            heroInstance != null ? heroInstance.PersonId : null, heroId);
        _rowCards[targetRow].Add(go2);

        ReassignColumns(targetRow, Input.mousePosition.y);
        AddUnitCardDrag(go2, heroId);
        RearrangeHandCards();

        // 同成员换排（移动而非新增）：重算源排剩余成员的逻辑列，保持 _rowCards ↔ _pendingCols 一致
        if (previousRow != null && previousRow != targetRow && _rowCards.ContainsKey(previousRow))
            ReassignColumns(previousRow, Input.mousePosition.y);

        Log("上阵英雄 " + heroData.DisplayName + " → " + bRow);
        return true;
    }

    /// <summary>
    /// 上阵角色卡（战前布阵或战中补上阵）。
    /// </summary>
    /// <returns>true=上阵成功（UI 卡已建立）；false=被拒绝（零写入，调用方不得隐藏准备卡）</returns>
    public bool PlaceUnit(CharacterCardData cardData, CharacterData charData, RectTransform targetRow)
    {
        BattleRow bRow = RectToBattleRow(targetRow);

        // 人物显示名优先（具体 Person）；战斗定义名仅作 Legacy 兜底（不把职业/模板名当人物身份）。
        string personDisplayName = ResolvePersonDisplayName(cardData) ?? charData.DisplayName;

        if (_battleStarted)
        {
            if (_battleSystem != null)
            {
                var card = _allCharCards.Find(c => c.CardId == cardData.CardId);
                var cd = _allCharDatas.Find(d => d.CharacterId == charData.CharacterId);
                // 用 CardId 字典查找独立的 CharacterInstance，防止同 CharacterId 塌缩
                CharacterInstance ci;
                if (!_charInstByCardId.TryGetValue(cardData.CardId, out ci))
                    ci = _allCharacters.Find(c => c.CharacterId == charData.CharacterId);
                if (card != null && cd != null && ci != null)
                {
                    var battleSkills = new List<SkillData>();
                    if (cd.InitialSkillIds != null)
                    {
                        foreach (var skillId in cd.InitialSkillIds)
                        {
                            var skill = _allSkills.Find(s => s.SkillId == skillId);
                            if (skill != null) battleSkills.Add(skill);
                        }
                    }
                    // 战斗中上阵：DeployCharacter 的 bool 返回值是防御纵深检查（失败不得建 ghost UI）
                    bool deployed = _battleSystem.DeployCharacter(card, bRow, BattleColumn.Center, ci, cd, battleSkills);
                    if (deployed)
                    {
                        var go2 = Instantiate(unitCardPrefab, targetRow, false);
                        go2.name = "Card_" + card.CardId;
                        var rt2 = go2.GetComponent<RectTransform>();
                        rt2.anchorMin = new Vector2(0.5f, 0.5f);
                        rt2.anchorMax = new Vector2(0.5f, 0.5f);
                        rt2.anchoredPosition = Vector2.zero;
                        FillUnitCard(go2, personDisplayName, charData.BaseHP, charData.BaseAttack, charData.ActionPoints,
                            ci.PersonId, AppearanceKeyFallbackOfCharCard(cardData));
                        var btn = go2.GetComponent<Button>();
                        if (btn != null) { string sid = charData.CharacterId; btn.onClick.AddListener(() => OnUnitCardClick(sid)); btn.interactable = true; }
                        _rowCards[targetRow].Add(go2);
                        ReassignColumns(targetRow, Input.mousePosition.y);
                        Log("战斗中上阵 " + personDisplayName + " → " + bRow);
                    }
                    else
                    {
                        Log("上阵失败（Board 占位或上阵数量限制），未创建单位");
                    }
                    RearrangeHandCards();
                    return deployed;
                }
            }
            RearrangeHandCards();
            return false;
        }

        EnsureRowDataReady();

        // Current GameSpec R4.1（总数 ≤ 6）+ R5.0（一排 ≤ 3）：失败零写入，不产生部分布阵
        string reason;
        if (!TryValidateRowPlacement(cardData.CardId, targetRow, out reason))
        {
            Log(reason);
            return false;
        }

        _pendingRows[cardData.CardId] = bRow;
        _pendingCols[cardData.CardId] = BattleColumn.Center;
        if (!_selectedChars.Contains(cardData)) _selectedChars.Add(cardData);

        string cardName = "Card_" + cardData.CardId;
        GameObject existingCard = null;
        RectTransform previousRow = null;
        foreach (var kv in _rowCards)
        {
            var c = kv.Value.Find(x => x != null && x.name == cardName);
            if (c != null) { existingCard = c; previousRow = kv.Key; break; }
        }

        GameObject go;
        if (existingCard != null)
        {
            go = existingCard;
            foreach (var kv in _rowCards) kv.Value.Remove(go);
            go.transform.SetParent(targetRow, false);
        }
        else
        {
            go = Instantiate(unitCardPrefab, targetRow, false);
            go.name = cardName;
        }

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;

        FillUnitCard(go, personDisplayName, charData.BaseHP, charData.BaseAttack, 0,
            ResolveCardPersonIdForVisual(cardData), AppearanceKeyFallbackOfCharCard(cardData));
        _rowCards[targetRow].Add(go);

        ReassignColumns(targetRow, Input.mousePosition.y);
        AddUnitCardDrag(go, cardData.CardId);
        RearrangeHandCards();

        // 同成员换排（移动而非新增）：重算源排剩余成员的逻辑列，保持 _rowCards ↔ _pendingCols 一致
        if (previousRow != null && previousRow != targetRow && _rowCards.ContainsKey(previousRow))
            ReassignColumns(previousRow, Input.mousePosition.y);

        Log("上阵 " + charData.DisplayName + " → " + bRow);
        return true;
    }

    /// <summary>
    /// 确保 _allRows / _rowCards 已初始化（直接调用 PlaceUnit/PlaceHero 战前布阵时的保护）。
    /// 正常产品流程由 RunBattleWithExpedition 初始化。
    /// </summary>
    private void EnsureRowDataReady()
    {
        if (_rowCards != null) return;

        _allRows = new List<RectTransform>
        {
            playerFrontRow, playerMiddleRow, playerBackRow,
            enemyFrontRow, enemyMiddleRow, enemyBackRow
        };
        _rowCards = new Dictionary<RectTransform, List<GameObject>>();
        foreach (var row in _allRows)
        {
            if (row != null) _rowCards[row] = new List<GameObject>();
        }
    }

    void FillUnitCard(GameObject card, string name, int hp, int atk, int ap,
        string personId = null, string appearanceKeyFallback = null)
    {
        card.transform.localScale = unitCardScale;
        var tn = card.transform.Find(HostTextName)?.GetComponent<TMP_Text>();
        var ta = card.transform.Find(HostTextAP)?.GetComponent<TMP_Text>();
        var tk = card.transform.Find(HostTextATK)?.GetComponent<TMP_Text>();
        if (tn != null) tn.text = name;
        if (ta != null) ta.text = ap > 0 ? ap + "" : "";
        if (tk != null) tk.text = atk + "";
        // WP-D：HP / Shield 使用独立视觉宿主，Shield 不再拼进 HP 字符串；身体使用可替换占位
        SetHPHost(card, hp + "/" + hp);
        SetShieldHost(card, 0);
        // Person Visual Binding V0.1：战前布阵按「Card → PersonId / 外观键 → Visual Binding」绑定身体
        //（未配置 / 缺素材 → 既有 playerBodyPlaceholder；不改任何 Gameplay 状态）。
        ApplyPlayerBodyVisual(card, personId, appearanceKeyFallback);
    }

    void RemoveCard(string cardName)
    {
        foreach (var kv in _rowCards) kv.Value.RemoveAll(c => c != null && c.name == cardName);
        foreach (var row in _allRows)
        {
            if (row == null) continue;
            foreach (Transform child in row)
                if (child.name == cardName) Destroy(child.gameObject);
        }
    }

    void AddUnitCardDrag(GameObject card, string cardId)
    {
        var trigger = card.GetComponent<EventTrigger>() ?? card.AddComponent<EventTrigger>();
        trigger.triggers.Clear();

        var eb = new EventTrigger.Entry { eventID = EventTriggerType.BeginDrag };
        string cid = cardId;
        eb.callback.AddListener((d) => OnUnitDragBegin(cid));
        trigger.triggers.Add(eb);

        var ed = new EventTrigger.Entry { eventID = EventTriggerType.Drag };
        ed.callback.AddListener((d) => { });
        trigger.triggers.Add(ed);

        var ee = new EventTrigger.Entry { eventID = EventTriggerType.EndDrag };
        string cid2 = cardId;
        ee.callback.AddListener((d) => OnUnitDragEnd(cid2));
        trigger.triggers.Add(ee);
    }

    void OnUnitDragBegin(string cardId)
    {
        if (_battleStarted) return;
        var host = PrepCardHost;
        if (host == null) return;

        foreach (var kv in _rowCards)
        {
            var card = kv.Value.Find(c => c != null && c.name == "Card_" + cardId);
            if (card != null)
            {
                // 记录源排成员顺序（含本卡）：拒绝移动时据此原子恢复原 Column / Pending 状态
                _dragSourceOrder = new List<string>();
                foreach (var orderedCard in GetFormationOrderedCards(kv.Key))
                {
                    string orderedId = GetCardId(orderedCard.name);
                    if (!string.IsNullOrEmpty(orderedId)) _dragSourceOrder.Add(orderedId);
                }

                kv.Value.Remove(card);
                card.transform.SetParent(host, false);
                card.transform.SetAsLastSibling();
                _pendingRows.Remove(cardId);
                _pendingCols.Remove(cardId);

                if (_rowCards.ContainsKey(kv.Key))
                    ReassignColumns(kv.Key, Input.mousePosition.y);

                _dragCard = card;
                _dragCardId = cardId;
                _dragSourceRow = kv.Key;
                _isDragging = true;

                Vector2 lp;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    host, Input.mousePosition, null, out lp);
                card.GetComponent<RectTransform>().anchoredPosition = lp;
                break;
            }
        }
    }

    private bool _isDragging;
    private GameObject _dragCard;
    private string _dragCardId;
    private RectTransform _dragSourceRow;

    /// <summary>
    /// 供 BattlePrepCard 在拖拽时调用，更新战场预览偏移
    /// </summary>
    public void UpdateDragPreview(Vector2 mouseScreenPos)
    {
        if (_rowCards == null || _rowCards.Count == 0) return;

        if (_battleStarted) return;
        // 每帧先从当前成员集合 / 相对顺序恢复正常位置；Preview 不拥有正常布局。
        ApplyFormationPresentationToAllRows();

        var previewRow = HitTestRow(mouseScreenPos);
        if (!IsPlayerRow(previewRow) || !_rowCards.ContainsKey(previewRow)) return;
        var ordered = GetFormationOrderedCards(previewRow);
        if (ordered.Count == 0 || ordered.Count >= BattleFormationVisualLayout.MaxVisualSlots) return;

        int insertIdx = ordered.Count;
        for (int i = 0; i < ordered.Count; i++)
        {
            float cardY = RectTransformUtility.WorldToScreenPoint(null, ordered[i].transform.position).y;
            if (mouseScreenPos.y > cardY) { insertIdx = i; break; }
        }
        for (int i = 0; i < ordered.Count; i++)
        {
            int targetSlot = i >= insertIdx ? i + 1 : i;
            float normalY = GetFormationSlotOffset(ordered.Count, i);
            float targetY = GetFormationSlotOffset(ordered.Count + 1, targetSlot);
            var rt = ordered[i].GetComponent<RectTransform>();
            var delta = Mathf.Approximately(targetY, normalY) ? 0f : Mathf.Sign(targetY - normalY) * dragPreviewOffset;
            rt.anchoredPosition += new Vector2(0f, delta);
        }
    }

    void Update()
    {
        _deployedUnitsDebug.Clear();
        if (_battleSystem != null)
        {
            var data = _battleSystem.GetBattleData();
            if (data != null)
            {
                foreach (var u in data.PlayerUnits)
                    _deployedUnitsDebug.Add(string.Format("[{0}] {1} | 排:{2} 列:{3} | HP:{4}/{5} AP:{6}", 
                        u.UnitType == UnitType.Hero ? "英雄" : "角色", u.SourceId, u.Row, u.Column, u.CurrentHP, u.MaxHP, u.CurrentActionPoints));
                foreach (var u in data.EnemyUnits)
                    _deployedUnitsDebug.Add(string.Format("[敌方] {0} | 排:{1} 列:{2} | HP:{3}/{4}", u.SourceId, u.Row, u.Column, u.CurrentHP, u.MaxHP));
            }
        }

        if (_isDragging && _dragCard != null)
        {
            Vector2 lp;
            var parent = _dragCard.transform.parent as RectTransform;
            if (parent != null)
            {
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    parent, Input.mousePosition, null, out lp);
                _dragCard.GetComponent<RectTransform>().anchoredPosition = lp;
            }

            UpdateDragPreview(Input.mousePosition);
        }
    }

    void OnUnitDragEnd(string cardId)
    {
        CompleteUnitDrag(cardId, Input.mousePosition);
    }

    private void CompleteUnitDrag(string cardId, Vector2 screenPosition)
    {
        if (!_isDragging || _dragCard == null) return;
        _isDragging = false;
        // 清除上一帧临时让位量，落点排序与取消恢复都消费正常 Formation 位置。
        ApplyFormationPresentationToAllRows();

        var hitRow = HitTestRow(screenPosition);

        if (hitRow != null && hitRow != enemyFrontRow && hitRow != enemyMiddleRow && hitRow != enemyBackRow)
        {
            // 唯一写入入口：目标排满 / 非法 → 失败零写入并原子恢复源排（卡不丢失、无重复 Column）
            TryReassignUnitCardToRow(cardId, hitRow, screenPosition.y);
            _dragCard = null;
        }
        else
        {
            Destroy(_dragCard);
            _dragCard = null;
            _pendingRows.Remove(cardId);
            _pendingCols.Remove(cardId);

            if (cardId == _playerHeroData?.HeroId)
            {
                _heroDeployed = false;
                CreateReturnedHeroPrepCard();
            }
            else
            {
                // cardId 现在是 CardId（GUID唯一），通过 _allCharCards 找到对应的卡
                var foundCard = _allCharCards.Find(cc => cc.CardId == cardId);
                if (foundCard != null)
                {
                    var cd = _allCharDatas.Find(d => d.CharacterId == foundCard.CharacterId);
                    if (cd != null)
                    {
                        CreateReturnedCharacterPrepCard(foundCard, cd);
                    }
                }
            }
            RearrangeHandCards();
        }

        // 成员退出操作返回前完成源排逻辑列与人数布局；不依赖 StartBattle 或下一帧。
        // 拒绝移动已原子恢复原顺序，不能再次按非法落点重新插入该卡。
        if (hitRow == null || !IsPlayerRow(hitRow))
        {
            if (_dragSourceRow != null && _rowCards.ContainsKey(_dragSourceRow))
            {
                ReassignColumns(_dragSourceRow, screenPosition.y);
                ApplyFormationPresentation(_dragSourceRow);
            }
        }
        ApplyFormationPresentationToAllRows();
        // 拖拽上下文收尾（避免残留 _dragCardId 影响后续列分配）
        _dragCardId = null;
        _dragSourceRow = null;
        _dragSourceOrder = null;
    }

    void OnUnitCardClick(string id)
    {
        if (_feedbackBusy) return;
        if (_battleSystem == null) return;
        var data = _battleSystem.GetBattleData();
        if (data == null) return;
        // 先用 UnitId（GUID）查找
        var unit = data.PlayerUnits.Find(u => u.UnitId == id);
        if (unit != null)
        {
            // 倒地或死亡单位不可选择
            if (unit.IsDown || unit.CurrentHP <= 0) return;
            OnPlayerUnitClick(unit);
            return;
        }
        unit = data.EnemyUnits.Find(u => u.UnitId == id);
        if (unit != null)
        {
            // 倒地或死亡单位不可选择
            if (unit.IsDown || unit.CurrentHP <= 0) return;
            OnEnemyUnitClick(unit);
            return;
        }
        // UnitId 未命中，按 SourceId 查找（先己方，再敌方，防止阵营误判）
        unit = data.PlayerUnits.Find(u => u.SourceId == id);
        if (unit != null && !unit.IsDown && unit.CurrentHP > 0) { OnPlayerUnitClick(unit); return; }
        unit = data.EnemyUnits.Find(u => u.SourceId == id);
        if (unit != null && !unit.IsDown && unit.CurrentHP > 0) { OnEnemyUnitClick(unit); return; }
    }

    public void OnPlayerUnitClick(BattleUnit unit)
    {
        if (_feedbackBusy) return;
        if (_battleSystem.GetBattleData().IsBattleOver) return;

        // Stage 2：V1 卡牌目标选择优先（复用单位点击宿主）
        if (TryResolveV1CardTarget(unit)) return;

        if (!_waitingForAction) return;

        // Battle V0.1 / WP-C：玩家行动只由共享手牌 + 共享行动力承担；
        // 旧 Attack / Skill / Guard / 使用道具操作菜单已退出正式产品路径。
        Log("选中 " + (unit.UnitType == UnitType.Hero ? "[英雄]" : "") + unit.SourceId + "（点击共享手牌出牌，再点目标）");
    }

    public void OnEnemyUnitClick(BattleUnit target)
    {
        if (_feedbackBusy) return;
        // Stage 2：V1 卡牌目标选择优先（复用单位点击宿主）
        if (_v1PendingCard != null)
        {
            var ruleTargetKind = BattleCardRuleResolver.GetManualTargetKind(_v1PendingCard);
            bool isEnemyScope = ruleTargetKind == BattleRuleManualTargetKind.EnemyUnit
                                || (ruleTargetKind == BattleRuleManualTargetKind.None &&
                                    !BattleCardRuleResolver.HasRuleProgram(_v1PendingCard) &&
                                    _v1PendingCard.TargetScope == BattleCardTargetScope.EnemyUnit);

            if (isEnemyScope && target.Faction == "enemy" && !target.IsDown)
            {
                // Gate 0：配置化牌先按规则来源校验目标合法性；Legacy 牌沿用 TargetScope 校验
                if (BattleCardRuleResolver.HasRuleProgram(_v1PendingCard))
                {
                    var legality = BattleCardRuleResolver.CheckPlayable(_v1PendingCard, EnsureRuleContext(),
                                                                       UnitRefFor(target));
                    if (!legality.IsLegal)
                    {
                        Log(legality.Reason);
                        return;
                    }
                }

                // Stage 3A：V1 伤害牌统一距离判定（RangeModifier 生效），超距离目标不可选
                if (!CanV1CardReachTarget(_v1PendingCard, target))
                {
                    Log("目标超出攻击距离，无法选择 " + _v1PendingCard.DisplayName);
                    return;
                }
                PlayV1Card(_v1PendingCard, target);
            }
            else
            {
                Log("该卡牌不能选择此目标");
            }
            return;
        }

        // Battle V0.1 / WP-C：旧玩家操作菜单（Attack / Skill / Guard / 使用道具）已退出正式产品路径；
        // 共享手牌目标选择与目标校验由上方 V1 分支承担。
    }

    /// <summary>
    /// Current Card Cancel（正式产品有效操作）：
    /// 取消待出牌与目标选择，清理 Rule Cell Marker，回到可继续选择的玩家回合。
    /// 由 btnCancelOp（Inspector 绑定）/ 玩家再次点击同一张手牌触发。
    /// </summary>
    public void CancelPendingCard()
    {
        if (_feedbackBusy) return;
        RestoreFeedbackSelection();
        if (_v1PendingCard == null)
        {
            Log("没有待出牌可取消");
            return;
        }

        _v1PendingCard = null;
        ClearRuleCellMarkers();
        Log("已取消待出牌");
    }

    /// <summary>战斗是否已正式开始（供 BattlePrepCard 等判断阵型是否锁定）</summary>
    public bool IsBattleStarted => _battleStarted;

    public void OnStartBattle()
    {
        // WP-Encounter-1C §14：产品路径（ExpeditionUI 已绑定 Active EnemyRoster）必须用**已冻结阵容**开战。
        // 绑定但 roster 为 null（遭遇生成失败）或校验不通过 → 在既有开战入口拒绝：
        // 不置 _battleStarted、不隐藏准备 UI、不创建 BattleSystem、**不回退整个 Monster Registry**、
        // 不生成默认 3 怪、不出现 0 敌人或半初始化战斗。
        if (_activeEnemyRosterBound)
        {
            string enemyReason;
            if (!TryValidateEnemyRosterForBattle(out enemyReason))
            {
                Debug.LogWarning("[BattleUI] OnStartBattle 拒绝开战：本场敌方阵容不可用（不回退整个 Monster Registry）："
                    + enemyReason);
                Log("本场敌方阵容不可用，无法开始战斗：" + enemyReason);
                return;
            }
        }

        // M4（Monster Legacy Physical Retirement）：敌方静态定义 Authority 可用性 + 非空预检。
        // 生成根缺失 / 未导表时 StaticConfigRegistry.Monsters == null → 在**既有开战入口**拒绝本次战斗初始化：
        // 不置 _battleStarted、不隐藏准备 UI、不创建 BattleSystem、不出现 0 敌人或半初始化战斗。
        if (_enemyMonsters == null || _enemyMonsters.Count == 0)
        {
            string authorityDiagnostic = StaticConfigRegistry.MonsterDiagnostic;
            // 与既有阵型预检同一拒绝语义与日志级别（不新增流程、不改产品行为）：
            // 明确失败（不可静默）由 StaticConfigRegistry（Error + 可操作诊断）与 ExpeditionUI 拒绝出征承担。
            Debug.LogWarning("[BattleUI] OnStartBattle 拒绝开战：敌方定义不可用（本次战斗未初始化）。"
                + (string.IsNullOrEmpty(authorityDiagnostic) ? "敌方定义列表为空。" : authorityDiagnostic));
            Log("怪物静态定义不可用，无法开始战斗。请先运行 RRPG_一键导表.cmd。");
            return;
        }

        // 开战前最终预检（_rowCards ↔ _pendingRows ↔ _pendingCols 成员一致 + 非空 / ≤6 / 每排 ≤3 / 同排列唯一）：
        // 失败 = 保持战前准备阶段：不置 _battleStarted、不隐藏准备 UI、不部分启动 Battle。
        string reason;
        if (!TryValidatePendingFormation(out reason))
        {
            Debug.LogWarning("[BattleUI] OnStartBattle 预检未通过：" + reason);
            Log(reason);
            return;
        }
        _battleStarted = true;

        // 开战后阵型锁定：隐藏所有剩余未上阵的战前卡，关闭免费布阵/加入入口
        var prepHost = PrepCardHost;
        if (prepHost != null)
        {
            foreach (Transform child in prepHost)
            {
                if (child != null && (child.name.StartsWith("PrepCard_") || child.name.StartsWith("HeroPrepCard_")))
                    child.gameObject.SetActive(false);
            }
        }

        if (btnStartBattle != null) btnStartBattle.gameObject.SetActive(false);
        if (btnEndTurn != null) btnEndTurn.gameObject.SetActive(true);
    }

    /// <summary>
    /// 共享手牌点击（Stage 2）：进入出牌流程。
    /// 需要目标的牌进入"选择目标"状态（复用单位点击宿主）；无需目标的牌直接打出。
    /// 再次点击同一张牌 = 取消选择。
    /// </summary>
    private void OnV1CardClicked(BattleCard card)
    {
        if (_feedbackBusy) return;
        if (card == null || v1Runtime == null) return;
        if (!_waitingForAction || _isEndingTurn) return;                     // 输入锁
        if (v1Runtime.TurnPhase != BattleV1TurnFlow.PhaseType.PlayerTurn) return;

        if (_v1PendingCard == card)
        {
            RestoreFeedbackSelection();
            _v1PendingCard = null;
            ClearRuleCellMarkers();
            Log("已取消卡牌选择");
            return;
        }

        if (!v1Runtime.CanPlayCard(card))
        {
            Log("行动力不足或当前不可打出：" + card.DisplayName);
            return;
        }

        // Gate 0：配置化牌的合法目标与后续合法性校验共用同一规则来源
        SelectFeedbackCard(card);
        var ruleTargetKind = BattleCardRuleResolver.GetManualTargetKind(card);
        if (ruleTargetKind == BattleRuleManualTargetKind.AllyCell)
        {
            _v1PendingCard = card;
            ShowRuleCellMarkers(card);
            Log("请点击高亮位置使用 " + card.DisplayName + "（再点该卡可取消）");
            return;
        }

        bool needsUnitTarget = ruleTargetKind == BattleRuleManualTargetKind.EnemyUnit
                               || ruleTargetKind == BattleRuleManualTargetKind.AllyUnit
                               || (!BattleCardRuleResolver.HasRuleProgram(card) &&
                                   (card.TargetScope == BattleCardTargetScope.EnemyUnit ||
                                    card.TargetScope == BattleCardTargetScope.AllyUnit));

        if (needsUnitTarget)
        {
            _v1PendingCard = card;
            string side = (ruleTargetKind == BattleRuleManualTargetKind.EnemyUnit
                           || (ruleTargetKind == BattleRuleManualTargetKind.None &&
                               card.TargetScope == BattleCardTargetScope.EnemyUnit))
                ? "敌方" : "我方";
            Log("请点击" + side + "目标使用 " + card.DisplayName + "（再点该卡可取消）");
        }
        else
        {
            PlayV1Card(card, null);
        }
    }

    /// <summary>
    /// 打出共享牌。
    /// Gate 0：已迁移到配置化规则管线的牌走 PlayRuleV1Card；
    /// 未迁移的 Legacy 牌保持既有最小效果路径（两套路径互斥，不会重复结算）。
    /// </summary>
    private void PlayV1Card(BattleCard card, BattleUnit target)
    {
        if (_feedbackBusy) return;
        if (card == null || v1Runtime == null) return;

        if (BattleCardRuleResolver.HasRuleProgram(card))
        {
            var manual = BattleRuleTargetRef.None();
            if (target != null)
            {
                manual = BattleRuleTargetRef.ForUnit(new BattleRuleUnitFacts
                {
                    UnitId = target.UnitId,
                    PersonId = GetPersonIdOfUnit(target)
                });
            }

            PlayRuleV1Card(card, manual);
            return;
        }

        PlayLegacyV1Card(card, target);
    }

    /// <summary>
    /// Legacy 最小效果出牌路径（Stage 2 起既有行为，保持原样）：
    /// 校验/支付共享行动力 → 最小效果结算（Damage/Heal）→ 普通牌进弃牌堆 / Exhaust 牌进消耗区 → 刷新 UI。
    /// </summary>
    private void PlayLegacyV1Card(BattleCard card, BattleUnit target)
    {
        if (card == null || v1Runtime == null) return;

        if (!v1Runtime.CanPlayCard(card))
        {
            Log("无法打出：" + card.DisplayName);
            return;
        }

        // 目标校验（最小：Damage→敌方单位，Heal→我方单位）
        if (card.TargetScope == BattleCardTargetScope.EnemyUnit &&
            (target == null || target.Faction != "enemy" || target.IsDown))
        {
            Log("需要选择敌方目标");
            return;
        }
        if (card.TargetScope == BattleCardTargetScope.AllyUnit &&
            (target == null || target.Faction != "player" || target.IsDown))
        {
            Log("需要选择我方目标");
            return;
        }

        // Stage 3A：V1 伤害牌统一距离判定（RangeModifier 生效）——选目标时已校验，此处为安全网
        if (!CanV1CardReachTarget(card, target))
        {
            Log("目标超出攻击距离，无法打出 " + card.DisplayName);
            return;
        }

        ExecuteCardWithFeedback(card, records =>
        {
            var before = BattleSystem.CaptureUnitFact(target);
            if (card.Effect == BattleCardEffectType.Damage && target != null)
                _battleSystem.ApplyBattleDamage(target, card.EffectValue);
            else if (card.Effect == BattleCardEffectType.Heal && target != null)
                _battleSystem.ApplyBattleHeal(target, card.EffectValue);
            if (before != null)
            {
                string source = records[0].SourceUnitId;
                records.Add(BattleFeedbackRecord.From(card.Effect == BattleCardEffectType.Heal ? BattleFeedbackType.Heal : BattleFeedbackType.Damage,
                    source,
                    new BattleRuleFactDelta { Before = before, After = BattleSystem.CaptureUnitFact(target) }, card.EffectValue, card));
            }
            Log(card.DisplayName + " 已使用" + (target != null ? " → " + target.SourceId : ""));
        });
    }

    /// <summary>
    /// V1 牌目标选择复用单位点击宿主：我方单位点击分支。
    /// 返回 true 表示已由 V1 目标选择消费（不再走旧操作菜单逻辑）。
    /// </summary>
    private bool TryResolveV1CardTarget(BattleUnit unit)
    {
        if (_v1PendingCard == null) return false;

        // Gate 0：配置化牌按规则程序声明的手选目标类别校验（与出牌合法性共用同一来源）
        var ruleTargetKind = BattleCardRuleResolver.GetManualTargetKind(_v1PendingCard);
        if (ruleTargetKind == BattleRuleManualTargetKind.AllyUnit)
        {
            var legality = BattleCardRuleResolver.CheckPlayable(_v1PendingCard, EnsureRuleContext(),
                                                               UnitRefFor(unit));
            if (!legality.IsLegal)
            {
                Log(legality.Reason);
                return true;
            }
            PlayV1Card(_v1PendingCard, unit);
            return true;
        }

        if (ruleTargetKind == BattleRuleManualTargetKind.EnemyUnit)
        {
            Log("该卡牌需要选择敌方目标");
            return true;
        }

        if (ruleTargetKind == BattleRuleManualTargetKind.AllyCell)
        {
            Log("该卡牌需要选择高亮位置");
            return true;
        }

        if (_v1PendingCard.TargetScope == BattleCardTargetScope.AllyUnit)
        {
            if (unit.Faction == "player" && !unit.IsDown)
            {
                PlayV1Card(_v1PendingCard, unit);
                return true;
            }
            Log("该卡牌需要选择我方目标");
            return true;
        }

        if (_v1PendingCard.TargetScope == BattleCardTargetScope.EnemyUnit)
        {
            Log("该卡牌需要选择敌方目标");
            return true;
        }

        return false;
    }

    /// <summary>把 BattleUnit 转成规则层目标引用（不制造临时 Person，PersonId 由既有映射解析）</summary>
    private BattleRuleTargetRef UnitRefFor(BattleUnit unit)
    {
        if (unit == null) return BattleRuleTargetRef.None();
        return BattleRuleTargetRef.ForUnit(new BattleRuleUnitFacts
        {
            UnitId = unit.UnitId,
            PersonId = GetPersonIdOfUnit(unit)
        });
    }

    /// <summary>
    /// Stage 3A：V1 伤害牌统一攻击距离判定。
    /// 距离源 = BattleSystem.IsInRange（与敌方 AI / 旧普通攻击同一公式，RangeModifier 参与）。
    /// 角色牌以牌主单位（OwnerPersonId 对应的存活 PlayerUnit）为施法者；
    /// 找不到施法者（如未来通用牌或牌主已失能）时不做距离限制（最小实现）。
    /// </summary>
    private bool CanV1CardReachTarget(BattleCard card, BattleUnit target)
    {
        if (card == null || card.Effect != BattleCardEffectType.Damage || target == null) return true;
        var caster = GetV1CardCaster(card);
        if (caster == null)
        {
            // Current GameSpec AC-09：通用牌（无牌主）不绑定角色距离，行为不变。
            if (string.IsNullOrEmpty(card.OwnerPersonId)) return true;

            // 角色牌必须能解析到本场实际参战的牌主：
            // 解析失败 = 完整性错误（ghost owner），不得因此绕过 Range 约束。
            Log("角色牌「" + card.DisplayName + "」在本场找不到对应牌主，无法进行距离判定，已阻止打出");
            return false;
        }
        return _battleSystem.IsInRange(caster, target, card.RangeModifier);
    }

    /// <summary>
    /// PlayerCollection 存在时，CardId → exact PersonId 是身份真相，与 RunState 无关。
    /// 无 PlayerCollection 的 Demo/Test 才沿用本场实例或 SourceId 临时标识。
    /// </summary>
    private string GetPersonIdOfUnit(BattleUnit unit)
    {
        if (unit == null) return null;
        if (_playerData != null)
        {
            // 长期人物身份取决于 PlayerCollection，与本场 RunState 是否存在无关。
            if (unit.UnitType == UnitType.Hero && _playerHero != null)
            {
                if (string.IsNullOrEmpty(_playerHero.PersonId))
                {
                    Debug.LogWarning("[BattleUI] GetPersonIdOfUnit：产品英雄（HeroId=" + _playerHero.HeroId
                        + "）缺少真实 PersonId，返回 null（不制造临时 Person truth）。");
                    return null;
                }
                return _playerHero.PersonId;
            }
            if (unit.UnitType == UnitType.Character && !string.IsNullOrEmpty(unit.UnitId))
            {
                CharacterCardData card = _allCharCards != null
                    ? _allCharCards.Find(c => c != null && c.CardId == unit.UnitId) : null;
                string pid = card != null ? _playerData.ResolvePersonIdForCard(card) : null;
                if (string.IsNullOrEmpty(pid))
                {
                    Debug.LogWarning("[BattleUI] GetPersonIdOfUnit：产品单位 " + unit.UnitId
                        + "（模板 " + unit.SourceId + "）无显式 Card→Person 绑定，返回 null（不制造临时 Person truth）。");
                }
                return pid;
            }
            if (unit.UnitType == UnitType.Character || unit.UnitType == UnitType.Hero)
                Debug.LogWarning("[BattleUI] GetPersonIdOfUnit：产品单位 " + unit.UnitId
                    + " 缺少可解析的当前人物卡或英雄实例，返回 null。");
            return null;
        }

        if (_runState == null) return unit.SourceId; // 既有纯 Demo 牌池使用模板临时键

        // 纯演示/测试（无 PlayerData）：沿用本场临时标识（不进入长期 truth）。
        if (unit.UnitType == UnitType.Hero && _playerHero != null)
        {
            return !string.IsNullOrEmpty(_playerHero.PersonId) ? _playerHero.PersonId : _playerHero.HeroId;
        }

        // 角色：优先使用本场实例的临时 PersonId（若存在），否则回退模板 SourceId。
        // 必须与共享牌库构筑（BuildActiveRosterPersonIds）使用同一个身份函数，
        // 否则会出现「牌归属有主、却找不到当前 caster」的 ghost 角色牌。
        if (_charInstByCardId != null && !string.IsNullOrEmpty(unit.UnitId)
            && _charInstByCardId.TryGetValue(unit.UnitId, out var demoUnitCi) && demoUnitCi != null
            && !string.IsNullOrEmpty(demoUnitCi.PersonId))
        {
            return demoUnitCi.PersonId;
        }
        return unit.SourceId; // Demo 临时模板 key
    }

    /// <summary>
    /// Stage 3A：按牌主（OwnerPersonId = 具体人物 PersonId，M1）查找本场存活玩家单位，作为距离判定的施法者。
    /// </summary>
    private BattleUnit GetV1CardCaster(BattleCard card)
    {
        if (_battleSystem == null || card == null || string.IsNullOrEmpty(card.OwnerPersonId)) return null;
        var data = _battleSystem.GetBattleData();
        if (data == null) return null;
        return data.PlayerUnits.Find(u => !u.IsDown && u.CurrentHP > 0
            && string.Equals(GetPersonIdOfUnit(u), card.OwnerPersonId));
    }

    /// <summary>
    /// Current GameSpec R10.2：当前仍具备战斗能力且已上阵的我方角色数量（正式基础抽牌数公式）。
    /// </summary>
    private int GetActiveRosterCount()
    {
        if (_battleSystem == null) return 0;
        var data = _battleSystem.GetBattleData();
        if (data == null) return 0;

        int count = 0;
        foreach (var u in data.PlayerUnits)
        {
            if (!u.IsDown && u.CurrentHP > 0)
                count++;
        }
        return count;
    }

    /// <summary>
    /// Current GameSpec R15.3：本场失能角色的角色牌从共享牌库全部牌区移除（幂等）。
    /// 在每轮抽牌前、出牌结算后、敌方阶段后调用，确保失能角色的牌退出本场循环。
    /// M1：必须按 BattleUnit 对应人物的真实 PersonId 移除（不再用模板 SourceId）。
    /// </summary>
    private void SyncDownedPersonCards()
    {
        if (_battleSystem == null || v1Runtime == null) return;
        var data = _battleSystem.GetBattleData();
        if (data == null) return;

        foreach (var u in data.PlayerUnits)
        {
            if (u.IsDown)
            {
                string personId = GetPersonIdOfUnit(u);
                if (!string.IsNullOrEmpty(personId))
                    v1Runtime.RemovePersonCards(personId);
            }
        }
    }

    public void OnEndTurn()
    {
        if (_feedbackBusy) return;
        if (!_waitingForAction || _isEndingTurn) return;
        _isEndingTurn = true;
        _v1PendingCard = null;

        // Stage 2：V1 玩家回合结束（弃置未 Retain 手牌）→ 敌方阶段（复用现有敌方行动）→ 下一玩家回合
        ExecuteEnemyWithFeedback();
        Log("回合结束");

        // Stage 4（R15.3）：敌方阶段造成失能后移除其角色牌（下一轮抽牌前生效）
        SyncDownedPersonCards();
        RefreshFieldUI();
        var data = _battleSystem.GetBattleData();
        if (data.IsBattleOver) Log(data.EnemyUnits.TrueForAll(u => u.IsDown) ? "🎉 胜利！" : "💀 失败...");
        // _actionTaken 由表现完成接缝设置，避免下一回合 Intent 抢先显示。
    }

    IEnumerator ExecuteBattle()
    {
        // WP-Encounter-1C §14（与 OnStartBattle 同一拒绝语义）：产品路径下敌方阵容不可用时
        // 不得创建 BattleSystem、不得回退整个 Monster Registry；回到战前准备状态。
        if (_activeEnemyRosterBound)
        {
            string rosterReason;
            if (!TryValidateEnemyRosterForBattle(out rosterReason))
            {
                Debug.LogWarning("[BattleUI] ExecuteBattle 被拒绝：本场敌方阵容不可用（不回退整个 Monster Registry）："
                    + rosterReason);
                _battleStarted = false;
                yield break;
            }
        }

        // M4 防御纵深（与 OnStartBattle 同一拒绝语义）：敌方定义不可用时不得创建 BattleSystem /
        // 不得初始化半套战斗；回到战前准备状态，由既有循环继续等待玩家操作。
        if (_enemyMonsters == null || _enemyMonsters.Count == 0)
        {
            Debug.LogWarning("[BattleUI] ExecuteBattle 被拒绝：敌方定义不可用（本次战斗未初始化）："
                + StaticConfigRegistry.MonsterDiagnostic);
            _battleStarted = false;
            yield break;
        }

        var config = ScriptableObject.CreateInstance<InjuryProbabilityConfig>();
        config.LightInjuryMax = 20; config.HeavyInjuryMax = 60; config.DisabledMin = 61;

        var instances = new List<CharacterInstance>();
        foreach (var card in _selectedChars)
        {
            // 用 CardId 字典查找独立的 CharacterInstance，防止同 CharacterId 塌缩
            CharacterInstance ci;
            if (!_charInstByCardId.TryGetValue(card.CardId, out ci))
                ci = _allCharacters.Find(x => x.CharacterId == card.CharacterId);
            if (ci != null && !ci.IsDead) instances.Add(ci);
        }

        _battleSystem = new BattleSystem(instances, _playerHero, config);

        if (DataManager.Instance.AllStatuses.Count > 0)
        {
            _battleSystem.RegisterStatusData(DataManager.Instance.AllStatuses);
        }
        else
        {
            var tipsy = ScriptableObject.CreateInstance<StatusData>();
            tipsy.StatusId = "status_tipsy"; tipsy.Name = "微醺"; tipsy.Duration = 1;
            tipsy.OnExpireStatusId = "status_hungover";
            tipsy.Effects = new[] { new EffectData { EffectType = EffectType.AddAttack, Value = 5, TargetRule = TargetRule.Self } };
            var hungover = ScriptableObject.CreateInstance<StatusData>();
            hungover.StatusId = "status_hungover"; hungover.Name = "醉醺"; hungover.Duration = 1;
            hungover.Effects = new[] { new EffectData { EffectType = EffectType.SubAttack, Value = 6, TargetRule = TargetRule.Self } };
            _battleSystem.RegisterStatusData(new List<StatusData> { tipsy, hungover });
        }

        if (_activeEnemyRosterBound)
        {
            // WP-Encounter-1C §9：敌方单位**全部**来自本场冻结阵容（1..9 只全部创建并合法部署）。
            string enemyDiagnostic;
            if (!_battleSystem.InitializeBattleFromEnemyRoster(_selectedChars, _enemyMonsters, _enemyRoster,
                    _allSkills, new List<SkillData>(), out enemyDiagnostic))
            {
                AbortBattleStart("敌方阵容入场失败（完整性错误）：" + enemyDiagnostic);
                yield break;
            }
        }
        else
        {
            // Legacy / Demo 路径（未绑定 Active EnemyRoster）：保持既有目录驱动行为不变。
            _battleSystem.InitializeBattle(_selectedChars, _enemyMonsters, _allSkills, new List<SkillData>());
        }

        // C 不变量：任一部署失败 = 完整性错误（不得静默缩减阵容继续）
        var deployFailures = new List<string>();

        foreach (var card in _selectedChars)
        {
            string cardId = card.CardId;
            if (!_pendingRows.TryGetValue(cardId, out var row)) continue;
            var cd = _allCharDatas.Find(d => d.CharacterId == card.CharacterId);
            // 用 CardId 字典查找独立的 CharacterInstance
            CharacterInstance ci;
            if (!_charInstByCardId.TryGetValue(cardId, out ci))
                ci = _allCharacters.Find(cc => cc.CharacterId == card.CharacterId);
            if (cd == null || ci == null)
            {
                deployFailures.Add("角色 " + cardId + " 缺少角色数据，无法上阵");
                continue;
            }
            BattleColumn col = _pendingCols.ContainsKey(cardId) ? _pendingCols[cardId] : BattleColumn.Center;
            var charSkills = new List<SkillData>();
            if (cd.InitialSkillIds != null)
            {
                foreach (var skillId in cd.InitialSkillIds)
                {
                    var skill = _allSkills.Find(s => s.SkillId == skillId);
                    if (skill != null) charSkills.Add(skill);
                }
            }
            if (!_battleSystem.DeployCharacter(card, row, col, ci, cd, charSkills))
                deployFailures.Add("角色 " + cardId + " 上阵失败（Board 占位或上阵数量限制）");
        }

        if (_heroDeployed && _playerHero != null && _playerHeroData != null)
        {
            string heroId = _playerHero.HeroId;
            if (_pendingRows.TryGetValue(heroId, out var heroRow))
            {
                // Stage 6A：Run 跨战 HP 继承（英雄 BattleUnit 从 Run CurrentHP 初始化）
                // M1：按英雄实例 PersonId 查询 Run 人物状态（不再用模板 HeroId）
                if (_runState != null)
                {
                    string heroPersonId = !string.IsNullOrEmpty(_playerHero.PersonId) ? _playerHero.PersonId : heroId;
                    int heroHp = _runState.GetPersonCurrentHp(heroPersonId, _playerHero.MaxHP);
                    if (heroHp > 0) _playerHero.CurrentHP = heroHp;
                }

                BattleColumn heroCol = _pendingCols.ContainsKey(heroId) ? _pendingCols[heroId] : BattleColumn.Center;
                var heroSkills = new List<SkillData>();
                if (_playerHeroData.InitialSkillIds != null)
                {
                    foreach (var skillId in _playerHeroData.InitialSkillIds)
                    {
                        var skill = _allSkills.Find(s => s.SkillId == skillId);
                        if (skill != null) heroSkills.Add(skill);
                    }
                }
                if (!_battleSystem.DeployHero(_playerHero, _playerHeroData, heroSkills, heroRow, heroCol))
                    deployFailures.Add("英雄 " + heroId + " 上阵失败（Board 占位或上阵数量限制）");
            }
        }

        if (deployFailures.Count > 0)
        {
            // C 不变量：部署完整性错误 = 安全中止本次开战流程（回到战前准备），不缩减阵容继续
            AbortBattleStart("部署失败（完整性错误）：" + string.Join("；", deployFailures));
            yield break;
        }

        _battleSystem.StartBattle();

        // Battle V0.1 Stage 1 集成：正式战斗使用新版共享牌库 + 一套共享行动力
        InitV1Runtime();

        foreach (var kv in _rowCards.ToList())
        {
            kv.Value.RemoveAll(c => c != null && c.name.StartsWith("EnemyPrep_"));
        }
        foreach (var row in _allRows)
        {
            if (row == null) continue;
            foreach (Transform child in row)
                if (child.name.StartsWith("EnemyPrep_"))
                    Destroy(child.gameObject);
        }

        foreach (var kv in _rowCards)
            foreach (var c in kv.Value)
                if (c != null)
                {
                    var et = c.GetComponent<EventTrigger>();
                    if (et != null) et.enabled = false;
                }

        var data = _battleSystem.GetBattleData();

        foreach (var u in data.PlayerUnits)
        {
            var rowRect = GetRowRect(true, u.Row);
            if (rowRect == null || !_rowCards.ContainsKey(rowRect)) continue;
            // UI 卡名用 UnitId（= CardId = GUID）匹配
            var card = _rowCards[rowRect].Find(c => c != null && c.name == "Card_" + u.UnitId);
            if (card == null) continue;
            var btn = card.GetComponent<Button>();
            if (btn != null)
            {
                btn.onClick.RemoveAllListeners();
                string uid = u.UnitId;
                btn.onClick.AddListener(() => OnUnitCardClick(uid));
                btn.interactable = !u.IsDown && u.CurrentHP > 0;
            }
        }

        foreach (var u in data.EnemyUnits)
        {
            var rowRect = GetRowRect(false, u.Row);
            if (rowRect == null) continue;
            var go = Instantiate(unitCardPrefab, rowRect, false);
            go.transform.localScale = monsterCardScale;
            // WP-Encounter-1C §8：敌方 UI 卡名用**本场身份** UnitId（= EnemyInstanceId）；
            // 同种怪多只不再共用同一个卡名（否则互相覆盖 / 点击命中错误单位）。
            go.name = "Card_" + u.UnitId;
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;   // WP-D：排内视觉位置统一由 Formation Presentation 计算

            var monsterData = _enemyMonsters.Find(m => m.MonsterId == u.SourceId);
            string enemyName = monsterData != null ? monsterData.Name : u.SourceId;
            var tn = go.transform.Find("Text_Name")?.GetComponent<TMP_Text>();
            if (tn != null) tn.text = enemyName;
            var th = go.transform.Find("Text_HP")?.GetComponent<TMP_Text>();
            if (th != null) th.text = u.CurrentHP + "/" + u.MaxHP;
            var tk = go.transform.Find("Text_ATK")?.GetComponent<TMP_Text>();
            if (tk != null) tk.text = u.Attack + "";

            var btn = go.GetComponent<Button>();
            if (btn != null) { string uid = u.UnitId; btn.onClick.AddListener(() => OnUnitCardClick(uid)); btn.interactable = true; }
            _rowCards[rowRect].Add(go);
        }

        // WP-D：敌方全部生成后，六排统一按 Formation Presentation 重排（战前 / 开战共用同一入口）
        ApplyFormationPresentationToAllRows();

        Log("战斗开始！点击角色操作");
        if (btnEndTurn != null) btnEndTurn.gameObject.SetActive(true);
        RefreshFieldUI();

        while (!_battleSystem.GetBattleData().IsBattleOver)
        {
            // Stage 4（R15.3）：失能角色的角色牌在本轮抽牌前退出本场循环
            SyncDownedPersonCards();

            // Stage 2：玩家回合开始（共享行动力恢复至上限 + 按 V1 共享牌库抽牌）
            v1Runtime.BeginPlayerTurn();
            // Stage 3A：V1 玩家回合开始触发 TurnStart 技能（只迁移技能触发职责，不启用旧 ExecuteTurnPart1）
            _battleSystem.TriggerTurnStartSkillsForPlayer();
            // Stage 5：玩家回合开始锁定/确认敌人下一 Action（Intent 在玩家看到前生成并锁定；玩家行动期间不重随机）
            _battleSystem.LockEnemyIntents();
            Log("玩家回合 " + v1Runtime.TurnNumber + " 开始");
            RefreshFieldUI();
            _isEndingTurn = false; _waitingForAction = true; _actionTaken = false;
            float t = 0;
            // Stage 5：玩家回合内最后一击全灭敌人 / 敌方失能全部我方 → 立即结束等待进入结算
            while (!_actionTaken && !_battleSystem.GetBattleData().IsBattleOver && t < 60f) { if (!_feedbackBusy) t += Time.deltaTime; yield return null; }
            while (_feedbackBusy) yield return null;
        }
        var result = _battleSystem.GetBattleData();
        bool isVictory = result.EnemyUnits.TrueForAll(u => u.IsDown);
        Log(isVictory ? "🎉 胜利！" : "💀 失败...");

        DoSettlement(isVictory);
    }

    public void RefreshFieldUI()
    {
        if (_feedbackBusy && !_feedbackFinalizing) return;
        var data = _battleSystem.GetBattleData();

        string s = "回合 " + data.Turn;
        // Battle V0.1：共享行动力显示宿主 = infoText（原旧公共 TP 文本宿主，原位复用）；
        // 正式数据源 = SharedActionPointPool.Current/Max。不并行保留临时共享行动力文本，
        // 也不再显示 Legacy TP（已由 Current GameSpec 淘汰）。
        if (v1Runtime != null && v1Runtime.ActionPoints != null)
        {
            string phase = v1Runtime.TurnPhase == BattleV1TurnFlow.PhaseType.PlayerTurn ? "玩家回合" : "敌人回合";
            s += "  [" + phase + "]  共享行动力 " + v1Runtime.ActionPoints.Current + "/" + v1Runtime.ActionPoints.Max;
            if (v1Runtime.Deck != null)
            {
                s += "\n  [V1牌堆] 抽:" + v1Runtime.Deck.DrawPileCount +
                     " 手:" + v1Runtime.Deck.HandCount +
                     " 弃:" + v1Runtime.Deck.DiscardPileCount +
                     " 耗:" + v1Runtime.Deck.ExhaustPileCount;
            }
        }
        s += "\n";
        foreach (var u in data.PlayerUnits)
        {
            string tag = u.UnitType == UnitType.Hero ? "[英雄]" : "[角色]";
            // Battle V0.1 / WP-C：玩家不再有个人行动力（Player Personal AP）显示，
            // 玩家资源信息只有上方共享行动力。
            s += tag + u.SourceId + " HP:" + u.CurrentHP + "/" + u.MaxHP + (u.IsDown ? " 倒地" : "") + "\n";
        }
        foreach (var u in data.EnemyUnits)
            s += "敌方 " + u.SourceId + " HP:" + u.CurrentHP + "/" + u.MaxHP + (u.IsDown ? " 倒地" : "") + "\n";
        infoText.text = s + "\n" + infoText.text;

        foreach (var u in data.PlayerUnits.Concat(data.EnemyUnits))
        {
            bool ip = data.PlayerUnits.Contains(u);
            var rr = GetRowRect(ip, u.Row);
            if (rr == null || !_rowCards.ContainsKey(rr)) continue;
            // WP-Encounter-1C §8：UI 卡名统一用**本场身份** UnitId
            //（玩家 = CardId / 英雄 = HeroId / 敌方 = EnemyInstanceId）。
            string cardName = "Card_" + u.UnitId;
            var card = _rowCards[rr].Find(c => c != null && c.name == cardName);
            if (card == null) continue;

            // 失能单位退出当前 Row 的可见成员集合；剩余单位随后按人数重新居中。
            card.SetActive(!u.IsDown && u.CurrentHP > 0);

            var tn = card.transform.Find(HostTextName)?.GetComponent<TMP_Text>();
            var ta = card.transform.Find(HostTextAP)?.GetComponent<TMP_Text>();
            var tk = card.transform.Find(HostTextATK)?.GetComponent<TMP_Text>();
            if (tn != null)
            {
                tn.text = GetBattleUnitDisplayName(u);
            }
            if (ta != null) ta.text = ip ? "" : (u.CurrentActionPoints + "");
            if (tk != null) tk.text = u.Attack + "";
            // WP-D：Body / HP / Shield / Intent 走同一套可替换视觉宿主（Shield 不再拼进 HP 字符串）
            ApplyUnitVisualHosts(card, u, ip);
            var ud = card.GetComponent<UnitCardDebug>();
            if (ud != null)
            {
                string displayName = GetBattleUnitDisplayName(u);
                ud.UpdateInfo(displayName, u.Row.ToString(), u.Column.ToString(), u.CurrentHP, u.MaxHP, u.CurrentActionPoints, u.Attack, u.IsDown, u.InDeathDoor);
            }
            var btn = card.GetComponent<Button>();
            if (btn != null) btn.interactable = !u.IsDown && u.CurrentHP > 0;
        }

        ApplyFormationPresentationToAllRows();

        // Stage 5：敌方卡 Intent / AttackRange 原位显示（复用现有敌方卡宿主，Inspector 可调）
        RefreshEnemyIntentUI();
    }

    private BattleSettlement _settlement;
    private BattleResultUI _resultUI;

    /// <summary>
    /// 本场上阵角色的 PersonId 列表（字段名沿用 LEGACY，但 M1 起只承载真正 PersonId，
    /// 供 BattleSettlement 结算/成长归属使用，不再承载模板 CharacterId）。
    /// </summary>
    private List<string> _deployedCharIds = new List<string>();
    private PlayerCollection _playerData;

    /// <summary>Run 运行时状态宿主（Stage 6A；null=非 Run 链路/兼容路径）</summary>
    private RunState _runState;

    /// <summary>Run 配置（Stage 6A；null 时使用默认测试值）</summary>
    private RunConfig _runConfig;

    public void SetSettlementDependencies(BattleResultUI resultUI, PlayerCollection playerData)
    {
        if (resultUI != null) _resultUI = resultUI;
        if (playerData != null) _playerData = playerData;
    }

    /// <summary>
    /// 注入 Run 运行时状态与配置（Stage 6A）。
    /// 须在 InitFromExpedition 之前调用，使本场角色从 Run CurrentHP 初始化、本场牌库从 Run Deck 构筑。
    /// </summary>
    public void SetRunState(RunState runState, RunConfig runConfig)
    {
        _runState = runState;
        _runConfig = runConfig;
    }

    private void DoSettlement(bool isVictory)
    {
        if (_resultUI == null)
        {
            Debug.LogError("[BattleUI] 结算失败：_resultUI 为 null");
            return;
        }

        // Stage 6B（人工验收修复）：战斗结束立即清理上一战 V1 手牌 UI，防止残留到下一战战前布阵。
        if (v1Runtime != null)
            v1Runtime.ClearHandCards();

        // M1：结算的上阵列表 = 各上阵角色的真实 PersonId（不再收集模板 CharacterId）
        _deployedCharIds.Clear();
        if (_battleSystem != null)
        {
            var data = _battleSystem.GetBattleData();
            if (data != null)
            {
                foreach (var unit in data.PlayerUnits)
                {
                    if (unit.UnitType != UnitType.Character) continue;
                    string personId = GetPersonIdOfUnit(unit);
                    if (!string.IsNullOrEmpty(personId) && !_deployedCharIds.Contains(personId))
                        _deployedCharIds.Add(personId);
                }
            }
        }

        // 参战角色实例：按 CardId 取各自独立实例（同模板多实例不塌缩）
        var instances = new List<CharacterInstance>();
        foreach (var card in _selectedChars)
        {
            if (card == null) continue;
            CharacterInstance ci = null;
            if (!_charInstByCardId.TryGetValue(card.CardId, out ci) || ci == null)
            {
                ci = _allCharacters.Find(x => x.CharacterId == card.CharacterId);
            }
            if (ci != null) instances.Add(ci);
        }

        // Stage 6A：战斗结束 HP 回写 Run 人物状态 + 战后恢复（下一场战斗从 Run CurrentHP 初始化）
        if (_runState != null)
            WriteBackRunHp();

        _settlement = new BattleSettlement();
        // M5/M1（Battle → Person EXP 回归修复）：结算判定「该具体人物是否有 Active Profession」必须来自
        // **真实长期 Person Authority** —— 本场 `instances` 是 Battle 临时视图副本（只共享 PersonId，
        // 不携带长期 ActiveProfessionProgress），仅用于本场伤残 / 死亡等战斗状态。
        // 只注入只读解析器：不复制 Authority、不写临时实例、不 fallback Legacy。
        PlayerCollection longTermPersonSource = _playerData;
        if (longTermPersonSource != null)
            _settlement.LongTermPersonLookup = personId => longTermPersonSource.FindPersonByPersonId(personId);
        _settlement.ProcessSettlement(isVictory, instances, _deployedCharIds, _playerHero, _runState);

        // Stage 6A：胜利提供全队共享领悟（遭遇源 BattleVictory，测试值未冻结）
        if (isVictory && _runState != null)
        {
            int insightPerBattle = _runConfig != null ? _runConfig.InsightPerBattle : 30;
            _runState.AddInsightFromEncounter(RunEncounterType.BattleVictory, insightPerBattle);
        }

        // Stage 6B：仅当存在 PendingCardRewardCount > 0 才进入选牌流程（非每战固定三选一）；
        // 全部奖励机会处理完后再显示结算结果，允许继续远征。
        if (_runState != null && _runState.PendingCardRewardCount > 0)
        {
            var rewardUI = FindObjectOfType<RunRewardUI>(true);
            if (rewardUI == null)
            {
                // 场景缺少 RunRewardUI 时的运行时兜底（场景已有则复用）
                var go = new GameObject("RunRewardUI_Auto", typeof(RunRewardUI));
                rewardUI = go.GetComponent<RunRewardUI>();
            }
            rewardUI.BeginRewards(_runState,
                DataManager.Instance != null ? DataManager.Instance.AllBattleCards : null,
                () => ShowBattleResult(isVictory),
                _playerData); // V0.1：Run 奖励从人物长期 Unlocked Pool 读取
            return;
        }

        ShowBattleResult(isVictory);
    }

    /// <summary>
    /// 显示战斗结算结果（在奖励流程全部处理完后调用）。
    /// </summary>
    private void ShowBattleResult(bool isVictory)
    {
        _resultUI.ShowResult(_settlement, () => {
            _resultUI.Hide();
            HideBattleCanvas();
            gameObject.SetActive(false);
            var expUI = FindObjectOfType<ExpeditionUI>(true);
            if (expUI != null) expUI.OnReturnFromBattle(isVictory);
        });
    }

    private void HideBattleCanvas()
    {
        // Resolve only this battle host's Canvas, including an already hidden one.
        // A same-named Canvas in another UI flow must not receive this lifecycle change.
        var canvas = GetComponentInChildren<Canvas>(true);
        if (canvas != null) canvas.gameObject.SetActive(false);
    }

    /// <summary>
    /// Stage 6A：把每个我方 BattleUnit.CurrentHP 回写 Run 人物状态，并按 RunConfig 执行战后恢复。
    /// M1：按 unit 对应的真实 PersonId 回写（不再用模板 SourceId）。
    /// </summary>
    private void WriteBackRunHp()
    {
        if (_runState == null || _battleSystem == null) return;
        var data = _battleSystem.GetBattleData();
        if (data == null) return;

        foreach (var unit in data.PlayerUnits)
        {
            if (unit == null) continue;
            string personId = GetPersonIdOfUnit(unit);
            if (string.IsNullOrEmpty(personId)) continue;
            _runState.ApplyBattleEndHp(personId, unit.CurrentHP, unit.MaxHP);
        }
    }

    public void Log(string msg) { if (infoText != null) infoText.text = msg + "\n" + infoText.text; }

    TMP_FontAsset GetFont() { return uiFont != null ? uiFont : (infoText != null ? infoText.font : null); }

    private void CleanupBattleUI()
    {
        if (_allRows != null)
        {
            foreach (var row in _allRows)
            {
                if (row == null) continue;
                // WP-D：只清理上一场战斗的单位卡（Card_ / EnemyPrep_），
                // 保留 Row 上的持久视觉宿主（RowVisual）等非单位卡子对象。
                var toDestroy = new List<GameObject>();
                foreach (Transform child in row)
                {
                    if (child == null) continue;
                    if (child.name.StartsWith("Card_") || child.name.StartsWith("EnemyPrep"))
                        toDestroy.Add(child.gameObject);
                }
                foreach (var go in toDestroy)
                    Destroy(go);
            }
        }

        var prepHost = PrepCardHost;
        if (prepHost != null)
        {
            var toDestroy = new List<Transform>();
            foreach (Transform child in prepHost)
            {
                if (child.name.StartsWith("PrepCard_") || child.name.StartsWith("HeroPrepCard_"))
                    toDestroy.Add(child);
            }
            foreach (var t in toDestroy)
                Destroy(t.gameObject);
        }

        // Stage 6B（人工验收修复）：战前准备时兜底清理上一战 V1 手牌 UI（正常路径已在 DoSettlement 清理）
        if (v1Runtime != null)
            v1Runtime.ClearHandCards();

        if (_rowCards != null)
        {
            foreach (var kv in _rowCards)
                kv.Value.Clear();
        }

        _battleStarted = false;
        _heroDeployed = false;
        _battleSystem = null;

    }

    static CharacterData CreateCharacterData(string id, string name, int hp, int atk, int range, int count, int ap, string faction, string[] skillIds)
    {
        var c = ScriptableObject.CreateInstance<CharacterData>();
        c.CharacterId = id; c.DisplayName = name; c.BaseHP = hp; c.BaseAttack = atk;
        c.AttackRange = range; c.AttackCount = count; c.ActionPoints = ap; c.Faction = faction;
        c.InitialSkillIds = skillIds;
        return c;
    }

    static SkillData CreateSkillData(string id, string name, TriggerType trigger, int cost, EffectType effectType, int value, TargetRule rule)
    {
        var s = ScriptableObject.CreateInstance<SkillData>();
        s.SkillId = id; s.Name = name; s.Trigger = trigger; s.CostActionPoints = cost;
        s.Effects = new[] { new EffectData { EffectType = effectType, Value = value, TargetRule = rule } };
        return s;
    }

    /// <summary>
    /// M2 Slice 2C（Legacy Freeze，FDS §10.4-C）：Demo / 回退分支的敌方定义来源 = **StaticConfigRegistry 单一 Authority**。
    ///
    /// 本 Slice 移除了 BattleUI 内的一切实体（inline）Monster 静态源：
    ///   - 删除 `CreateMonsterData` helper 与内联 mon_001 / mon_002 数值；
    ///   - 删除 2A 的 Disposable `ToMonsterDefinitions` 适配（其删除条件即为本 Slice）。
    ///
    /// 行为边界：Demo 行为、敌方顺序（Canonical 列表顺序）与战斗规则均不变；
    /// 不可用时**明确报错并返回空列表**（不静默、不退回 Legacy 内联）。
    /// </summary>
    static List<MonsterDefinition> ResolveDemoEnemyMonsters()
    {
        var registry = StaticConfigRegistry.Monsters;
        if (registry == null)
        {
            Debug.LogError("[BattleUI] Demo 路径无法取得 Monster 静态定义（StaticConfigRegistry.Monsters == null）："
                + StaticConfigRegistry.MonsterDiagnostic);
            return new List<MonsterDefinition>();
        }

        return new List<MonsterDefinition>(registry.All);
    }

    void InitializeDefaultBattleData()
    {
        // WP-Encounter-1C：Demo / Legacy 路径不消费 Active EnemyRoster；若本会话此前绑定过（产品路径），
        // 在此解绑，避免 Demo 战斗误用上一场远征的阵容（两套入口互不污染）。
        ClearActiveEnemyRosterBinding();
        _allCharacters = new List<CharacterInstance>
        {
            new CharacterInstance("char_001", "战士", 30, 6),
            new CharacterInstance("char_002", "猎人", 22, 7),
            new CharacterInstance("char_003", "医师", 25, 4),
            new CharacterInstance("char_004", "村长", 30, 2)
        };
        _playerHero = new HeroInstance("hero_001", "青鹿", 35);

        if (DataManager.Instance.AllCharacters.Count > 0)
        {
            _allCharDatas = DataManager.Instance.AllCharacters;
            _allSkills = DataManager.Instance.AllSkills;
            // M2 Slice 2C（Legacy Freeze）：敌方定义来源 = StaticConfigRegistry（不再读 DataManager.AllMonsters）
            _enemyMonsters = ResolveDemoEnemyMonsters();
            _playerHeroData = DataManager.Instance.GetHeroById("hero_001");
            _allCharCards = new List<CharacterCardData>();
            if (DataManager.Instance.AllCharCards != null)
                _allCharCards.AddRange(DataManager.Instance.AllCharCards);
            foreach (var cd in _allCharDatas)
            {
                if (!_allCharCards.Any(c => c.CharacterId == cd.CharacterId))
                {
                    var newCard = ScriptableObject.CreateInstance<CharacterCardData>();
                    newCard.CardId = "card_" + cd.CharacterId;
                    newCard.CharacterId = cd.CharacterId;
                    _allCharCards.Add(newCard);
                }
            }
        }
        else
        {
            _allCharDatas = new List<CharacterData>
            {
                CreateCharacterData("char_001", "战士", 30, 6, 1, 1, 1, "player", new[] { "sk_001" }),
                CreateCharacterData("char_002", "猎人", 22, 7, 2, 1, 1, "player", null),
                CreateCharacterData("char_003", "医师", 25, 4, 1, 1, 1, "player", new[] { "sk_002" }),
                CreateCharacterData("char_004", "村长", 30, 2, 1, 1, 1, "player", new[] { "sk_003" })
            };
            _allCharCards = new List<CharacterCardData>();
            for (int i = 0; i < 4; i++)
            {
                var c = ScriptableObject.CreateInstance<CharacterCardData>();
                c.CardId = "ccard_00" + (i + 1);
                c.CharacterId = _allCharDatas[i].CharacterId;
                _allCharCards.Add(c);
            }
            _allSkills = new List<SkillData>
            {
                CreateSkillData("sk_001", "劈砍", TriggerType.Active, 1, EffectType.Damage, 8, TargetRule.Single),
                CreateSkillData("sk_002", "治疗", TriggerType.Active, 1, EffectType.Heal, 6, TargetRule.Self),
                CreateSkillData("sk_003", "鼓舞", TriggerType.TurnStart, 0, EffectType.AddAttack, 2, TargetRule.AdjacentAlly)
            };
            // M2 Slice 2C（FDS §10.4-C）：BattleUI 内 inline Monster 静态源已移除 → 同样取自 StaticConfigRegistry
            _enemyMonsters = ResolveDemoEnemyMonsters();
            _playerHeroData = ScriptableObject.CreateInstance<HeroData>();
            _playerHeroData.HeroId = "hero_001";
            _playerHeroData.DisplayName = "青鹿";
            _playerHeroData.BaseHP = 35;
            _playerHeroData.BaseAttack = 8;
            _playerHeroData.AttackRange = 1;
            _playerHeroData.AttackCount = 1;
            _playerHeroData.ActionPoints = 1;
            _playerHeroData.Faction = "player";
        }
    }
}
