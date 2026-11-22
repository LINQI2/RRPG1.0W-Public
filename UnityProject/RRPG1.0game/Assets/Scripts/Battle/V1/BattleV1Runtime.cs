using TMPro;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

/// <summary>正式 Battle Card Hand 的无状态视觉排布；不读取或写入牌堆规则。</summary>
public static class BattleHandVisualLayout
{
    /// <summary>Minimum Scale 是相对 Base Scale 的比例；关闭 count scaling 时保持 Base Scale。</summary>
    public static float GetScale(int count, float baseScale, bool scaleByCount, float minimumRatio)
    {
        float ratio = scaleByCount ? Mathf.Max(Mathf.Clamp(minimumRatio, 0.01f, 1f), 6f / Mathf.Max(6, count)) : 1f;
        return Mathf.Max(0.01f, baseScale) * ratio;
    }

    public static float GetStep(int count, float availableWidth, float cardWidth, float baseStep, float minStep)
    {
        if (count <= 1) return 0f;
        float fittingStep = Mathf.Max(0f, (availableWidth - cardWidth) / (count - 1));
        float preferredStep = Mathf.Max(Mathf.Min(baseStep, fittingStep), minStep);
        // 容器过窄时仍以可见性为先；允许比 Inspector 的偏好最小值更大幅度重叠。
        return Mathf.Min(preferredStep, fittingStep);
    }

    public static Vector2 GetPosition(int count, int index, float availableWidth, float cardWidth,
        float baseStep, float minStep, Vector2 centerOffset)
    {
        if (count <= 0) return centerOffset;
        float step = GetStep(count, availableWidth, cardWidth, baseStep, minStep);
        return new Vector2(centerOffset.x + (index - (count - 1) * 0.5f) * step, centerOffset.y);
    }
}

/// <summary>
/// Battle V0.1 正式战斗运行时控制器（共享牌库 + 共享行动力）
///
/// 挂载于正式战斗 UI（BattleUI 所在 GameObject），**不构成第二套 HUD**。
/// 职责：
///   - 按正式远征阵容过滤本场有效牌 → 构筑共享牌库（SharedDeck）→ 洗牌 → 初始抽牌；
///   - 创建一套全队共享行动力（SharedActionPointPool）；
///   - 把共享手牌渲染到现有 BattleUI.handContainer（不新建手牌区）。
///
/// 数据来源：正式链路传入的 deployedPersonIds（具体人物实例ID）
///          + DataManager.AllBattleCards（BattleCardData 模板）。
/// 内容桥接（Stage 1 占位）：每个上阵人物拥有全部 Character 模板；
///                          未来由"人物-牌归属"内容系统替换。
/// 不负责：回合生命周期（Stage 2）、上阵/布阵（Stage 3）、HP/失能（Stage 4）。
/// </summary>
public class BattleV1Runtime : MonoBehaviour
{
    [Header("V0.1 共享牌库 / 共享行动力（Stage 1 集成）")]
    [Tooltip("复用 BattleUI.handContainer（现有手牌区）")]
    public RectTransform handContainer;

    [Tooltip("复用 Assets/Prefabs/UI/BattleCard.prefab")]
    public GameObject battleCardPrefab;

    [Tooltip("共享行动力文本（可选；未绑定时由 BattleUI.infoText 显示）")]
    public TMP_Text txtSharedActionPoint;
    public string sharedActionPointFormat = "{0}/{1}";
    public System.Func<PlayerCollection> PresentationPeopleProvider;

    [Tooltip("共享行动力上限（配置化，不硬编码）")]
    public int maxActionPoints = 3;

    [Tooltip("基础抽牌数（兜底配置；正式流程由 DrawCountProvider 注入，公式=当前仍具战斗能力且已上阵的我方角色数，R10.2）")]
    public int baseDrawCount = 3;

    [Tooltip("正式基础抽牌数提供者（Current GameSpec R10.2：当前仍具备战斗能力的我方角色数量）。由 BattleUI 注入；为空时回退 baseDrawCount")]
    public System.Func<int> DrawCountProvider;

    [Header("Gate 0 规则管线（由 BattleUI 注入真实 Battle Fact 视图）")]
    [Tooltip("规则执行上下文提供者：每次进入玩家回合重新取一份当前 Battle 事实视图（BattleSystem 每战重建）")]
    public System.Func<IBattleRuleContext> RuleContextProvider;

    [Tooltip("手牌附加提示提供者（加工状态 / 自动目标预览）；返回空串表示不显示")]
    public System.Func<BattleCard, string> CardBadgeProvider;

    [Tooltip("玩家回合开始附加动作（Gate 0：清除上一玩家回合遗留的临时护盾）")]
    public System.Action OnPlayerTurnStarted;

    [Header("Battle Card Hand Layout（相对 handContainer / BattleActionHandRoot 中心）")]
    [Tooltip("正式 Battle Action Card 根基础缩放；默认 1。运行时以此参数覆盖 Prefab 根缩放。")]
    [Min(0.01f)] public float battleCardBaseScale = 1f;
    [Tooltip("正式行动牌中心的偏好步长；不影响 Prep 人物牌。")]
    [Min(0f)] public float handBaseStep = 200f;
    [Tooltip("步长偏好下限；容器不足时允许更多重叠。")]
    [Min(0f)] public float handMinStep = 70f;
    [Tooltip("整组正式行动手牌的位置，相对 handContainer 中心。")]
    public Vector2 handCenterOffset = Vector2.zero;
    [Tooltip("关闭时最终 Scale = Base Scale；开启时 = Base Scale × 数量缩放比例。")]
    public bool handScaleByCount = false;
    [Tooltip("相对 Base Scale 的最小比例（0.8 表示 Base Scale 的 80%，不是最终绝对缩放）。")]
    [Range(0.01f, 1f)] public float handMinimumScale = 0.8f;

    private SharedDeck _deck;
    private SharedActionPointPool _actionPoints;
    private BattleV1TurnFlow _turnFlow;
    private bool _suppressRefresh;
    private int _presentationDepth;
    public bool IsPresentationDeferred => _presentationDepth > 0;

    /// <summary>只延迟 View；牌库/AP 事件与 Gameplay 仍同步执行。Dispose 幂等且可嵌套。</summary>
    public System.IDisposable BeginPresentationBatch()
    {
        _presentationDepth++;
        return new PresentationBatch(this);
    }

    private sealed class PresentationBatch : System.IDisposable
    {
        private BattleV1Runtime _owner;
        public PresentationBatch(BattleV1Runtime owner) { _owner = owner; }
        public void Dispose()
        {
            var owner = _owner;
            _owner = null;
            if (owner == null) return;
            owner._presentationDepth = System.Math.Max(0, owner._presentationDepth - 1);
            if (owner._presentationDepth == 0) owner.RefreshAll();
        }
    }
    private readonly List<GameObject> _handCardObjects = new List<GameObject>();

    public SharedDeck Deck => _deck;
    public SharedActionPointPool ActionPoints => _actionPoints;

    /// <summary>当前回合生命周期（Stage 2 接入；null=尚未初始化）</summary>
    public BattleV1TurnFlow TurnFlow => _turnFlow;

    /// <summary>当前阶段（PlayerTurn / EnemyTurn）</summary>
    public BattleV1TurnFlow.PhaseType TurnPhase => _turnFlow != null ? _turnFlow.Phase : BattleV1TurnFlow.PhaseType.EnemyTurn;

    /// <summary>当前玩家回合号</summary>
    public int TurnNumber => _turnFlow != null ? _turnFlow.TurnNumber : 0;

    /// <summary>玩家点击一张共享手牌（由 BattleUI 订阅处理出牌/选目标）</summary>
    public event System.Action<BattleCard> CardClicked;

    void OnDisable()
    {
        Unsubscribe();
    }

    /// <summary>
    /// 以正式远征阵容初始化共享牌库与共享行动力
    /// </summary>
    /// <param name="deployedPersonIds">本场上阵的具体人物实例ID列表（正式链路传入）</param>
    /// <param name="runDeckEntries">Run 级牌条目（Stage 6A：优先从此构筑；null 时回退 DemoCardPool 种子）</param>
    public void InitForBattle(List<string> deployedPersonIds, IEnumerable<BattlePersonCardEntry> runDeckEntries = null)
    {
        Unsubscribe();
        _suppressRefresh = true;

        try
        {
            List<BattlePersonCardEntry> candidates;
            if (runDeckEntries != null)
            {
                // Stage 6A：每场 Battle 从当前 Run Deck 构筑（Run 牌组跨战保留，不每战重新种子）。
                candidates = new List<BattlePersonCardEntry>(runDeckEntries);
            }
            else
            {
                // Fallback：BattleV1DemoCardPool 仅作为 Run 开始时的临时种子 / 非 Run 链路的兼容来源；
                // 正式 Run 链路必须在 Run 创建时种子一次，此后不得每战回退到这里。
                candidates = BattleV1DemoCardPool.BuildRoster(deployedPersonIds,
                    DataManager.Instance != null ? DataManager.Instance.AllBattleCards : null);
            }
            var valid = SharedDeck.FilterValidCards(candidates, deployedPersonIds);

            _deck = new SharedDeck();
            _deck.DeckChanged += OnDeckChanged;
            _deck.BuildDeck(valid);
            _deck.Reshuffle();

            _actionPoints = new SharedActionPointPool(maxActionPoints);
            _actionPoints.Changed += OnActionPointChanged;
            _actionPoints.RestoreToMax();

            // Stage 2：回合生命周期（玩家回合开始恢复 AP + 抽牌由 BeginPlayerTurn 负责）
            _turnFlow = new BattleV1TurnFlow(_deck, _actionPoints);
            // Gate 0：注入规则执行上下文（配置化牌在支付 AP 前判断 Primary Resolution 合法性）
            _turnFlow.RuleContext = RuleContextProvider != null ? RuleContextProvider() : null;
        }
        finally
        {
            _suppressRefresh = false;
        }

        RefreshAll(); // 初始化完成后再一次性刷新（避免中途事件导致重复渲染）
    }

    /// <summary>
    /// 玩家回合开始：共享行动力恢复至上限 + 按正式基础抽牌数抽牌（Current GameSpec R10.2/R12.3）。
    /// 抽牌数来源：DrawCountProvider（BattleUI 注入=当前仍具战斗能力且已上阵的我方角色数）；
    /// 未注入时回退 baseDrawCount。
    /// 首回合由 BattleUI.ExecuteBattle 的回合循环调用；此后每轮玩家回合都会调用。
    /// </summary>
    public void BeginPlayerTurn()
    {
        if (_turnFlow == null) return;

        // Gate 0：玩家回合开始刷新规则上下文（每场 Battle 的 BattleSystem 会重建）
        _turnFlow.RuleContext = RuleContextProvider != null ? RuleContextProvider() : null;

        // Gate 0：下一玩家回合开始时清除上一回合遗留的临时护盾（Shield 不进 Run / Save）
        if (OnPlayerTurnStarted != null) OnPlayerTurnStarted();

        int drawCount = DrawCountProvider != null ? DrawCountProvider() : baseDrawCount;
        _turnFlow.BeginPlayerTurn(drawCount);
        RefreshAll();
    }

    /// <summary>
    /// 玩家回合结束：弃置手牌中未标记 Retain 的普通牌，进入敌方阶段。
    /// </summary>
    public void EndPlayerTurn()
    {
        if (_turnFlow == null) return;

        _turnFlow.EndPlayerTurn();
        RefreshAll();
    }

    /// <summary>显式进入敌方阶段（供敌方行动宿主调用；阶段内玩家不可出牌）</summary>
    public void BeginEnemyTurn()
    {
        if (_turnFlow == null) return;

        _turnFlow.BeginEnemyTurn();
        RefreshAll();
    }

    /// <summary>校验一张共享牌当前是否可合法打出（阶段/在手牌/行动力足够）</summary>
    public bool CanPlayCard(BattleCard card)
    {
        return _turnFlow != null && _turnFlow.CanPlay(card);
    }

    /// <summary>支付共享行动力费用（仅当 CanPlayCard 通过后调用）</summary>
    public bool PayCardCost(BattleCard card)
    {
        return _turnFlow != null && _turnFlow.PayCost(card);
    }

    /// <summary>结算一张已打出牌的去向：普通牌进弃牌堆，Exhaust 牌进消耗区</summary>
    public bool ResolvePlayedCard(BattleCard card)
    {
        return _turnFlow != null && _turnFlow.ResolvePlayedCard(card);
    }

    /// <summary>
    /// Current GameSpec R15.3：角色本场失去战斗能力后，从共享牌库全部牌区移除其角色牌。
    /// 由 BattleUI 在检测到失能后调用（幂等）。
    /// </summary>
    public int RemovePersonCards(string personId)
    {
        if (_deck == null) return 0;
        int removed = _deck.RemovePersonCards(personId);
        if (removed > 0)
            RefreshAll();
        return removed;
    }

    /// <summary>
    /// 刷新全部显示（手牌 + 计数）
    /// </summary>
    public void RefreshAll()
    {
        if (_suppressRefresh || IsPresentationDeferred) return;
        RefreshHand();
        RefreshCounters();
    }

    /// <summary>
    /// 立即销毁本运行时创建的手牌 UI 对象（战斗结束离开 Battle 时调用；不触碰逻辑牌堆）。
    /// 防止上一战 V1 手牌残留到下一战战前布阵阶段。
    /// </summary>
    public void ClearHandCards()
    {
        if (handContainer != null)
        {
            for (int i = handContainer.childCount - 1; i >= 0; i--)
            {
                var child = handContainer.GetChild(i);
                if (child != null && child.name.StartsWith("V1Card_"))
                {
                    if (Application.isPlaying) Destroy(child.gameObject);
                    else DestroyImmediate(child.gameObject);
                }
            }
        }
        _handCardObjects.Clear();
    }

    private void RefreshHand()
    {
        // 清理由本运行时创建的手牌对象（含编辑模式下 Destroy 延迟/未生效的残留）
        if (handContainer != null)
        {
            for (int i = handContainer.childCount - 1; i >= 0; i--)
            {
                var child = handContainer.GetChild(i);
                if (child != null && child.name.StartsWith("V1Card_"))
                {
                    if (Application.isPlaying) Destroy(child.gameObject);
                    else DestroyImmediate(child.gameObject);
                }
            }
        }
        _handCardObjects.Clear();

        if (handContainer == null || battleCardPrefab == null)
        {
            Debug.LogWarning("[BattleV1Runtime] handContainer 或 battleCardPrefab 未绑定，无法显示共享手牌");
            return;
        }

        if (_deck == null) return;

        int count = _deck.Hand.Count;
        int index = 0;
        foreach (var card in _deck.Hand)
        {
            var go = Instantiate(battleCardPrefab, handContainer, false);
            go.name = "V1Card_" + card.CardId;

            // 只布置正式 Battle Card；战前人物 PrepCard 由 BattleUIHandLayout 独立管理。
            var rt = go.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                float scale = BattleHandVisualLayout.GetScale(count, battleCardBaseScale, handScaleByCount, handMinimumScale);
                rt.localScale = Vector3.one * scale;
                float width = rt.rect.width * scale;
                rt.anchoredPosition = BattleHandVisualLayout.GetPosition(
                    count, index, handContainer.rect.width, width, handBaseStep, handMinStep, handCenterOffset);
            }

            var view = go.GetComponent<BattleCardView>();
            if (view == null) view = go.AddComponent<BattleCardView>();

            string badge = CardBadgeProvider != null ? CardBadgeProvider(card) : null;
            view.BindPresentation(card, badge, RuleContextProvider, PresentationPeopleProvider);

            // Stage 2：共享手牌可点击（出牌/选目标由 BattleUI 订阅 CardClicked 处理）
            AddCardClick(go, card);

            _handCardObjects.Add(go);
            index++;
        }
    }

    /// <summary>
    /// 给一张共享手牌挂接点击交互（复用 BattleCard.prefab 上的 Image 作为按钮底图）。
    /// </summary>
    private void AddCardClick(GameObject go, BattleCard card)
    {
        var btn = go.GetComponent<Button>();
        if (btn == null) btn = go.AddComponent<Button>();

        var graphic = go.GetComponent<Image>();
        if (graphic != null && btn.targetGraphic == null) btn.targetGraphic = graphic;

        btn.onClick.RemoveAllListeners();
        btn.onClick.AddListener(() => CardClicked?.Invoke(card));
    }

    private void RefreshCounters()
    {
        if (txtSharedActionPoint != null)
        {
            int cur = _actionPoints != null ? _actionPoints.Current : 0;
            int max = _actionPoints != null ? _actionPoints.Max : 0;
            var presenter = BattleUI.ValueHost(txtSharedActionPoint.transform.parent.gameObject, txtSharedActionPoint, true);
            presenter.NumberFormat = sharedActionPointFormat;
            presenter.SetImmediate(cur, max);
        }
    }

    /// <summary>
    /// 测试抽牌（调试用；正式回合抽牌由 Stage 2 接入）
    /// </summary>
    [ContextMenu("V1_TestDrawOnce")]
    public void TestDrawOnce()
    {
        if (_deck == null) return;

        _deck.Draw(1);
        RefreshAll();
    }

    private void OnDeckChanged()
    {
        if (!_suppressRefresh) RefreshAll();
    }

    private void OnActionPointChanged(int current, int max)
    {
        if (!_suppressRefresh) RefreshAll();
    }

    private void Unsubscribe()
    {
        if (_deck != null) _deck.DeckChanged -= OnDeckChanged;
        if (_actionPoints != null) _actionPoints.Changed -= OnActionPointChanged;
        _deck = null;
        _actionPoints = null;
        _turnFlow = null;
    }
}
