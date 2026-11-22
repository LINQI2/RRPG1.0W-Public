using TMPro;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Run 卡牌奖励界面（Stage 6B）。
/// 职责：当 RunState.PendingCardRewardCount &gt; 0 时，依次展示三选一/Skip；全部机会处理完后回调继续远征。
/// Binding Spec 功能身份：Panel_CardReward(canvasRoot) / Container_RewardCards(cardContainer) / Btn_SkipReward(btnSkip)。
/// 不负责：正式奖励数值/概率（测试值未冻结）、正式美术（视觉由 Inspector 可调，用户可后续调整）。
/// </summary>
public class RunRewardUI : MonoBehaviour
{
    [Header("UI 元素（Binding Spec 功能身份）")]
    [Tooltip("Panel_CardReward（奖励面板 Canvas；启动时隐藏，进入选牌时显示）")]
    public GameObject canvasRoot;

    [Tooltip("Container_RewardCards（三张候选牌容器）")]
    public RectTransform cardContainer;

    [Tooltip("候选牌 Prefab（复用 BattleCard.prefab；未绑定时回退 BattleV1Runtime.battleCardPrefab）")]
    public GameObject cardPrefab;

    [Tooltip("Btn_SkipReward（跳过本次奖励机会）")]
    public Button btnSkip;

    [Tooltip("Mask_Dim（全屏半透明遮罩：压暗战场背景、拦截点击穿透；颜色/透明度在 Inspector 可调）")]
    public Image maskDim;

    public TMP_Text titleText;
    public TMP_Text infoText;

    [Header("卡牌布局（Inspector 可调）")]
    public Vector3 cardScale = Vector3.one;
    public Vector2 cardStartPosition = new Vector2(-300, 0);
    public float cardSpacing = 200f;

    [Header("候选生成（可替换；默认 DemoCardPoolRewardProvider 临时内容种子，非正式长期人物学习系统）")]
    [Tooltip("留空使用默认 Provider（测试内容来源）")]
    public IRunRewardCandidateProvider provider;

    /// <summary>每次三选一候选数</summary>
    public const int CardsPerOffer = 3;

    private RunState _runState;
    private System.Action _onFinished;
    private List<BattleCardData> _allTemplates = new List<BattleCardData>();
    private readonly List<GameObject> _cardObjects = new List<GameObject>();
    private readonly List<RunRewardCandidate> _currentCandidates = new List<RunRewardCandidate>();

    void Start()
    {
        ResolveBindings();
        if (canvasRoot != null) canvasRoot.SetActive(false);
        if (btnSkip != null) btnSkip.onClick.AddListener(OnSkip);
    }

    /// <summary>
    /// 未在 Inspector 绑定时按场景路径解析（场景对象已按 Binding Spec 命名；Inspector 绑定优先）。
    /// </summary>
    private void ResolveBindings()
    {
        if (canvasRoot == null) canvasRoot = gameObject;

        if (cardContainer == null)
        {
            var t = transform.Find("RunRewardCanvas/Container_RewardCards");
            if (t != null) cardContainer = t as RectTransform;
        }
        if (btnSkip == null)
        {
            var t = transform.Find("RunRewardCanvas/Btn_SkipReward");
            if (t != null) btnSkip = t.GetComponent<Button>();
        }
        if (titleText == null)
        {
            var t = transform.Find("RunRewardCanvas/TitleText");
            if (t != null) titleText = t.GetComponent<TMP_Text>();
        }
        if (infoText == null)
        {
            var t = transform.Find("RunRewardCanvas/InfoText");
            if (t != null) infoText = t.GetComponent<TMP_Text>();
        }
        if (maskDim == null)
        {
            var t = transform.Find("RunRewardCanvas/Mask_Dim");
            if (t != null) maskDim = t.GetComponent<Image>();
        }
    }

    /// <summary>
    /// 开始处理卡牌奖励（须 PendingCardRewardCount &gt; 0）。
    /// 依次展示每个奖励机会；全部处理完后调用 onFinished（返回远征继续）。
    /// </summary>
    /// <param name="playerData">长期玩家数据（供 UnlockedPoolRewardProvider 读取人物长期牌池；可为 null）</param>
    public void BeginRewards(RunState runState, IEnumerable<BattleCardData> allTemplates, System.Action onFinished, PlayerCollection playerData = null)
    {
        _runState = runState;
        _onFinished = onFinished;
        _allTemplates = allTemplates != null ? new List<BattleCardData>(allTemplates) : new List<BattleCardData>();

        // V0.1：候选 Provider 优先使用长期 Unlocked Pool（同一池同时供 CardGrowth 与 Run 奖励）；
        // DemoCardPoolRewardProvider 仅作临时内容种子回退。
        if (provider == null)
        {
            if (playerData != null)
                provider = new UnlockedPoolRewardProvider(
                    personId => PlayerGrowthLookup.FindPerson(playerData, personId));
            else
                provider = new DemoCardPoolRewardProvider();
        }

        if (cardPrefab == null)
        {
            var v1 = FindObjectOfType<BattleV1Runtime>(true);
            if (v1 != null) cardPrefab = v1.battleCardPrefab;
        }

        ShowNextReward();
    }

    private void ShowNextReward()
    {
        if (_runState == null) { Finish(); return; }
        if (_runState.PendingCardRewardCount <= 0) { Finish(); return; }

        // 候选池 = 当前远征所有人物 + 通用牌（扁平抽取，不先随机人物）
        var pool = provider.BuildCandidates(_runState, _allTemplates);
        var pick = RunRewardPicker.PickDistinct(pool, CardsPerOffer, new System.Random());
        if (pick.Count == 0)
        {
            // 无候选：消耗一次机会（占位不获得牌），继续下一次
            Log("无可用奖励候选，自动跳过本次机会");
            _runState.ConsumePendingCardReward();
            ShowNextReward();
            return;
        }

        _currentCandidates.Clear();
        foreach (var c in pick) _currentCandidates.Add(c);
        RefreshCardDisplay();

        // 人工验收修复：奖励阶段必须成为最上层交互 UI——
        //  1) Canvas 必须是 ScreenSpaceOverlay（防御误配置导致与战场叠层）；
        //  2) 全屏遮罩置为最底层子对象（压暗战场背景 + 拦截点击穿透），奖励牌/Skip 在遮罩之上。
        if (canvasRoot != null)
        {
            var cv = canvasRoot.GetComponent<Canvas>();
            if (cv != null && cv.renderMode == RenderMode.WorldSpace)
            {
                Debug.LogWarning("[RunRewardUI] canvasRoot 被误配置为 WorldSpace，已强制 ScreenSpaceOverlay（奖励阶段最上层）");
                cv.renderMode = RenderMode.ScreenSpaceOverlay;
            }
            canvasRoot.SetActive(true);
        }
        if (maskDim != null)
            maskDim.transform.SetAsFirstSibling();

        if (titleText != null) titleText.text = "卡牌奖励（" + _runState.PendingCardRewardCount + "）";
        Log("选择一张牌加入 Run 牌组，或跳过");
    }

    private void RefreshCardDisplay()
    {
        foreach (var go in _cardObjects)
        {
            if (go != null) Destroy(go);
        }
        _cardObjects.Clear();

        if (cardPrefab == null || cardContainer == null) return;

        for (int i = 0; i < _currentCandidates.Count; i++)
        {
            var cand = _currentCandidates[i];
            var go = Instantiate(cardPrefab, cardContainer, false);
            go.name = "RewardCard_" + cand.IdentityKey + "_" + i;

            var rt = go.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = new Vector2(cardStartPosition.x + i * cardSpacing, cardStartPosition.y);
            }
            go.transform.localScale = cardScale;

            var view = go.GetComponent<BattleCardView>();
            if (view == null) view = go.AddComponent<BattleCardView>();
            var bc = BattleCard.FromEntry(new BattlePersonCardEntry(cand.Template, cand.PersonId, 1), 0);
            view.Bind(bc);

            // 明确显示所属具体人物（优先人物显示名）
            if (view.ownerText != null && !string.IsNullOrEmpty(cand.PersonId))
            {
                var p = _runState != null ? _runState.GetPerson(cand.PersonId) : null;
                view.ownerText.text = p != null && !string.IsNullOrEmpty(p.DisplayName) ? p.DisplayName : cand.PersonId;
            }

            var btn = go.GetComponent<Button>();
            if (btn == null) btn = go.AddComponent<Button>();
            var img = go.GetComponent<Image>();
            if (img != null && btn.targetGraphic == null) btn.targetGraphic = img;
            int idx = i;
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() => OnSelect(idx));
        }
    }

    private void OnSelect(int index)
    {
        if (index < 0 || index >= _currentCandidates.Count) return;
        var cand = _currentCandidates[index];
        if (_runState != null && cand.Template != null)
        {
            // 增加到对应具体人物的 Run Deck Entry（Copies+1）；消耗 1 次机会
            _runState.AddRunCard(cand.PersonId, cand.Template);
            _runState.ConsumePendingCardReward();
        }
        Log("获得牌：" + (cand.Template != null ? cand.Template.DisplayName : "") + " → "
            + (string.IsNullOrEmpty(cand.PersonId) ? "通用" : cand.PersonId));
        _currentCandidates.Clear();
        ShowNextReward();
    }

    private void OnSkip()
    {
        if (_runState != null) _runState.ConsumePendingCardReward(); // Skip 消耗本次机会，不获得牌
        Log("跳过奖励");
        _currentCandidates.Clear();
        ShowNextReward();
    }

    private void Finish()
    {
        if (canvasRoot != null) canvasRoot.SetActive(false);
        var cb = _onFinished;
        _onFinished = null;
        cb?.Invoke();
    }

    private void Log(string msg)
    {
        if (infoText != null) infoText.text = msg + "\n" + infoText.text;
    }
}

