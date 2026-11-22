using TMPro;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ExpeditionJourneyView —— WP-E Journey Visual Root（**表现层；不持有规则 Authority**）。
///
/// 结构（稳定 Scene / Prefab 宿主，不每次运行 new Canvas）：
///   ExpeditionJourneyRoot（Canvas + 本组件）
///   ├─ BackgroundHost
///   ├─ EnvironmentHost（EnvFar / EnvNear；1–2 层简单相对移动）
///   ├─ PartyHost → JourneyPartyMemberView × N（独立 Prefab，每个准确 PersonId 一个）
///   ├─ JourneyHUD（SupplyHost / ElapsedDaysHost / PartyStatusEntry / BtnPauseResume / BtnContinue / BtnReturn）
///   ├─ EncounterOverlay（ImageHost / TitleHost / BodyTextHost / ChoiceRoot）
///   ├─ BranchOverlay（BranchChoiceRoot）
///   └─ MessageHost
///
/// 边界（硬约束）：
///   - 只读 AdventureManager 暴露的状态；不决定合法 Node、不写 Progression / Supply / elapsed / VillageTime；
///   - Pause / Continue 只影响表现播放；
///   - 成员身份来自传入的 PersonId 列表（每个 PersonId 一个独立 Member View，同模板不合并）；
///   - 视觉参数（时长 / 距离 / 间距 / 缩放）Inspector 可调，未冻结；
///   - Placeholder 与未来正式素材走同一 binding 路径（只替换 Sprite）；
///   - 事件卡插图来源 = `JourneyEventIllustrationResolver`（PresentationKey → JourneyEventPresentationCatalog
///     → 类别级 fallback）；本 Scene 的三个类别 Sprite 字段已降级为 fallback / placeholder，
///     制作人新增插图不需要修改本 Scene。
///
/// 显示生命周期（表现宿主契约；不涉及任何游戏规则）：
///   - ExpeditionJourneyRoot 是**稳定 Scene Host 且常驻 Active**；本组件 Awake 时**初始化即 Hidden**；
///   - 显隐的唯一运行时入口 = `SetVisible(bool)`：隐藏只关 Canvas 组件（不永久关闭 Root），
///     显示时最小防御地恢复 Root Active（避免 Host 被意外 inactive 时只看到蓝屏）；
///   - 调用方（产品路径）：Confirm Expedition → Show；Battle 前 → HideForEncounter；
///     Battle 返回 → ShowAfterEncounter；Return / Retreat → Hide。
/// </summary>
public class ExpeditionJourneyView : MonoBehaviour, IJourneyPresentationDriver
{
    public const string MemberPrefabChildBody = "Body";
    public const string MemberPrefabChildLabel = "Label";

    // ==================== Hosts ====================

    [Header("Canvas 根（本对象即 Root）")]
    public Canvas canvas;

    [Header("Environment Hosts")]
    public Image backgroundImage;
    public RectTransform environmentHost;
    public RectTransform envFarHost;
    public RectTransform envNearHost;
    public Image envFarImage;
    public Image envNearImage;

    [Header("Party Host / Member Prefab")]
    public RectTransform partyHost;
    public GameObject partyMemberPrefab;

    [Header("Journey HUD")]
    public TMP_Text supplyText;
    public TMP_Text elapsedDaysText;
    public TMP_Text partyStatusText;
    public Button btnPauseResume;
    public TMP_Text btnPauseResumeLabel;
    public Button btnContinue;
    public Button btnReturn;

    [Header("统一 Event / Encounter Card（唯一卡面；Battle / 普通 Encounter / Branch 共用）")]
    [Tooltip("统一卡面 View（Scene 稳定宿主；承载 Illustration / Title / Body / Options）")]
    public JourneyEventCardView eventCard;
    [Tooltip("统一卡面根对象（通常 = EncounterOverlay；可不绑定）")]
    public GameObject eventCardRoot;
    [Tooltip("战斗 Encounter **类别级 fallback / 占位**插图（逐事件插图配置入口 = JourneyEventPresentationCatalog 资产；制作人不需要改本 Scene 字段）")]
    public Sprite battleEncounterIllustration;
    [Tooltip("普通 / 占位 Encounter **类别级 fallback / 占位**插图（同上）")]
    public Sprite contentEncounterIllustration;
    [Tooltip("Branch **类别级 fallback / 占位**插图（Branch 当前是路线选择表面，暂无稳定唯一的逐 Branch 表现键）")]
    public Sprite branchIllustration;

    [Header("Message Host")]
    public TMP_Text messageText;

    [Header("Person Visual Binding（Person Visual Binding V0.1：配置唯一来源 = PersonVisualBindingCatalog 资产）")]
    [Tooltip("Journey 缺配置 / 缺素材时使用的 placeholder（不显示身体也不影响身份与布局）")]
    public Sprite defaultPersonBody;

    [Header("Presentation Tuning（Inspector 可调；未冻结）")]
    [Tooltip("一次旅行表现时长（秒）；只影响表现，不产生规则时间")]
    public float travelDuration = 1.4f;
    [Tooltip("环境层（近层）左向滚动距离（像素）—— 前进感由环境滚动承担，人物站位固定")]
    public float environmentScrollDistance = 260f;
    [Tooltip("远层相对位移比例（1 = 与近层同速）")]
    public float environmentFarScale = 0.5f;
    [Tooltip("Member 起始间距（像素）")]
    public float memberSpacing = 150f;
    [Tooltip("队伍固定站位（PartyHost 停靠位置，参考分辨率 1920×1080；旅行期间不再发生整体位移）")]
    public Vector2 partyStartPosition = new Vector2(-120f, -150f);
    [Tooltip("人物站位允许的极轻上下浮动幅度（像素；0 = 完全静止）")]
    public float partyBobAmplitude = 3f;
    [Tooltip("站桩浮动的循环速度（仅表现）")]
    public float partyBobSpeed = 2.5f;
    [Tooltip("占位 Encounter 显示时长（秒）")]
    public float placeholderEncounterSeconds = 0.9f;

    [Header("UI 文本字体（优先使用；场景 HUD 已绑定字体作为兜底）")]
    public TMP_FontAsset uiFont;

    // ==================== 运行时 ====================

    private readonly List<GameObject> _members = new List<GameObject>();
    private readonly List<string> _memberPersonIds = new List<string>();
    private Coroutine _travelRoutine;
    private bool _traveling;
    private bool _paused;
    private bool _travelCompleting;
    private Action _travelCompletion;
    private JourneyNodeRef _pendingConfirmNode;
    private string _stickyMessage;
    private string _transientMessage = string.Empty;

    // 表现宿主显隐意图：默认 Hidden（启动即不可见，只有 Confirm Expedition → Show 才显示）。
    // 非序列化字段：不得让 Scene 里 Canvas 的 serialized enabled 决定启动可见性。
    private bool _visible;
    private bool _hostInactiveWarned;

    /// <summary>Return 按钮回调（由协调器接到既有 Return 产品入口）。</summary>
    public Action ReturnRequested;

    // 只读诊断 / 测试

    /// <summary>表现宿主根（Scene 稳定 Root，常驻 Active；隐藏只关 Canvas，不永久关闭 Root）。</summary>
    public GameObject HostRoot => canvas != null ? canvas.gameObject : gameObject;

    /// <summary>宿主是否 Active In Hierarchy（false = 即使 Canvas enabled 也不会渲染）。</summary>
    public bool IsHostActive => HostRoot != null && HostRoot.activeInHierarchy;

    /// <summary>是否真正可见：需要「已请求显示 + Canvas enabled + 宿主 ActiveInHierarchy」三者同时成立。</summary>
    public bool IsVisible => _visible && canvas != null && canvas.enabled && IsHostActive;
    public bool IsTraveling => _traveling;
    public bool IsPaused => _paused;
    public int PartyMemberCount => _memberPersonIds.Count;
    public IReadOnlyList<string> MemberPersonIds => _memberPersonIds;
    public string SupplyDisplay => supplyText != null ? supplyText.text : string.Empty;
    public string MessageDisplay => messageText != null ? messageText.text : string.Empty;
    public string ElapsedDaysDisplay => elapsedDaysText != null ? elapsedDaysText.text : string.Empty;
    public bool HasPendingTravelCompletion => _travelCompletion != null;

    private void Awake()
    {
        EnsureHostRefs();
        if (eventCard != null) eventCard.Hide();
        // 初始化即 Hidden：Journey 是表现宿主，启动 / 非 Journey 阶段必须不覆盖主界面。
        // Scene 中 Canvas 的 serialized enabled 只作编辑期状态，不得决定运行时启动可见性。
        ApplyVisibility();
        if (btnPauseResume != null) btnPauseResume.onClick.AddListener(OnPauseResumeClicked);
        if (btnContinue != null) btnContinue.onClick.AddListener(OnContinueClicked);
        if (btnReturn != null) btnReturn.onClick.AddListener(() => { if (ReturnRequested != null) ReturnRequested(); });
        RefreshPauseLabel();
        ApplyPartyStance();
    }

    private void Update()
    {
        // 人物站位固定：只保留极轻上下浮动（前进感由环境滚动承担）。
        if (_traveling) return;
        if (IsVisible && !_paused) ApplyPartyStance();
    }

    // ==================== 可见性 ====================

    /// <summary>解析宿主引用（Awake 之前被 Show 调用时同样安全）。</summary>
    private void EnsureHostRefs()
    {
        if (canvas == null) canvas = GetComponent<Canvas>();
        if (eventCard == null) eventCard = GetComponentInChildren<JourneyEventCardView>(true);
        if (eventCard != null && eventCard.root == null) eventCard.root = eventCard.gameObject;
    }

    /// <summary>
    /// 最小防御：宿主 Root 被意外置为 Inactive 时恢复 Active。
    /// 否则 SetVisible(true) 只能启用 Canvas **组件**，inactive GameObject 不参与渲染（症状 = 蓝屏）。
    /// </summary>
    private void EnsureHostActive()
    {
        var root = HostRoot;
        if (root == null) return;

        if (!root.activeSelf) root.SetActive(true);

        // 仅当**父级**被关闭时才会在这里报警（Root 自身已 Activate 却仍不 ActiveInHierarchy）。
        if (!root.activeInHierarchy && !_hostInactiveWarned)
        {
            _hostInactiveWarned = true;
            Debug.LogWarning("[JourneyView] 表现宿主 " + root.name
                + " 在层级中仍为 Inactive（父级被关闭）：Journey 不会渲染。请保持 Scene Root Active。");
        }
    }

    /// <summary>把请求的可见性落到 Canvas 实际渲染状态（宿主 inactive 时不得声称可见）。</summary>
    private void ApplyVisibility()
    {
        if (canvas == null) return;
        canvas.enabled = _visible && IsHostActive;
    }

    /// <summary>
    /// Journey 表现显隐的**唯一运行时入口**（启动 / 非 Journey / Battle / Return 都必须隐藏）。
    /// - 隐藏：只关 Canvas 组件，Host Root 保持 Active（Scene 稳定宿主，不依赖永久关闭 Root）；
    /// - 显示：先最小防御恢复 Host Active，再启用 Canvas（避免 Root 被意外 inactive 时只看到蓝屏）。
    /// </summary>
    public void SetVisible(bool visible)
    {
        _visible = visible;
        EnsureHostRefs();
        if (visible)
        {
            EnsureHostActive();
            // WP-Event-1 Producer Acceptance Repair（Issue A）：Journey 再次可见 = 战斗生命周期已结束
            // → 解除「战斗期间不得出现新卡面」的产品硬规则锁。
            UnlockPresentationAfterBattle();
        }
        ApplyVisibility();
        if (!visible) CancelPresentation();
    }

    /// <summary>
    /// **Battle / Prep 期间的表现硬规则（WP-Event-1 Producer Acceptance Repair｜Issue A）**：
    /// 真正开始战斗时上锁；在战斗生命周期结束（Journey 再次可见）之前，**任何**新的 Journey Card
    ///（Branch / 正式 Event / Encounter）都不得取代战斗（见 <see cref="PresentCard"/>）。
    /// </summary>
    public bool PresentationLockedForBattle { get; private set; }

    /// <summary>Battle / Prep 真正开始：上锁（由产品入口在 BindActiveEnemyRoster + StartBattle 之前调用）。</summary>
    public void LockPresentationForBattle()
    {
        PresentationLockedForBattle = true;
        HideEncounter();
    }

    /// <summary>战斗生命周期结束（Journey 重新可见 / 新会话）：解锁。</summary>
    public void UnlockPresentationAfterBattle()
    {
        PresentationLockedForBattle = false;
    }

    /// <summary>Battle / Encounter 开始：隐藏并取消未完成表现（旧回调失效）。</summary>
    public void HideForEncounter()
    {
        CancelPresentation();
        SetVisible(false);
    }

    // ==================== HUD ====================

    /// <summary>HUD 只读展示 Current state（不复制规则公式）。</summary>
    public void SetHud(int currentCarryUnits, int initialCarryUnits, int elapsedDays)
    {
        if (supplyText != null)
            supplyText.text = "补给：" + currentCarryUnits + "（初始 " + initialCarryUnits + "）";
        if (elapsedDaysText != null)
            elapsedDaysText.text = "已过天数：" + elapsedDays;
    }

    public void SetMinuteHud(int currentCarryUnits, int initialCarryUnits, long elapsedMinutes)
    {
        if (supplyText != null) supplyText.text = "补给：" + currentCarryUnits + "（初始 " + initialCarryUnits + "）";
        if (elapsedDaysText != null) elapsedDaysText.text = "累计耗时：" + elapsedMinutes / 60 + "小时 " + elapsedMinutes % 60 + "分";
    }

    public void SetPartyStatus(string text)
    {
        if (partyStatusText != null) partyStatusText.text = text ?? string.Empty;
    }

    public void ShowMessage(string text)
    {
        _transientMessage = text ?? string.Empty;
        RefreshMessageDisplay();
    }

    /// <summary>
    /// 拒绝 / 无法推进原因的**粘性**反馈：自动表现（旅行 / Branch / terminal）不得覆盖它，
    /// 直到玩家成功进入一个 Node（或会话重新显示 / 隐藏）才清除。
    /// </summary>
    public void ShowDeniedMessage(string text)
    {
        _stickyMessage = text ?? string.Empty;
        RefreshMessageDisplay();
    }

    /// <summary>成功推进 / 新会话 / 隐藏后清除粘性拒绝原因。</summary>
    public void ClearStickyMessage()
    {
        _stickyMessage = null;
        RefreshMessageDisplay();
    }

    private void RefreshMessageDisplay()
    {
        if (messageText == null) return;
        messageText.text = !string.IsNullOrEmpty(_stickyMessage) ? _stickyMessage : _transientMessage;
    }

    public void ClearMessage()
    {
        _stickyMessage = null;
        _transientMessage = string.Empty;
        RefreshMessageDisplay();
    }

    // ==================== Party（每个准确 PersonId 一个独立 Member View） ====================

    /// <summary>
    /// 按准确 PersonId 集合同步 Party：同模板两人不同合并；缺 visual 走 default placeholder。
    /// 返回当前显示的成员数（应为传入人数）。
    /// </summary>
    public int ApplyParty(IList<string> personIds, PlayerCollection collection)
    {
        int wanted = personIds != null ? personIds.Count : 0;

        while (_members.Count < wanted)
        {
            GameObject member = CreateMemberView();
            if (member == null) break;
            _members.Add(member);
        }

        // 成员围绕 PartyHost 中心排布（PartyHost 自身承载停靠 / 旅行位移）；
        // 不得把 partyStartPosition 在成员上再加一次，否则首发成员会被推出屏幕。
        float centerOffset = (wanted - 1) * 0.5f * memberSpacing;

        _memberPersonIds.Clear();
        for (int i = 0; i < wanted && i < _members.Count; i++)
        {
            string pid = personIds[i];
            GameObject member = _members[i];
            if (member == null) continue;

            // Person Visual Binding V0.1：视觉唯一来源 = PersonVisualBindingCatalog
            //（Builder 迁移后 Scene 不再序列化任何人物质表；entries 参数传 null = 走统一配置面）。
            JourneyPersonVisualResult visual = ExpeditionJourneyPersonVisualResolver.Resolve(
                collection, pid, null, defaultPersonBody);

            var rt = member.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.anchoredPosition = new Vector2(i * memberSpacing - centerOffset, 0f);
                rt.localScale = Vector3.one * (visual.Scale > 0f ? visual.Scale : 1f);
            }

            var img = member.transform.Find(MemberPrefabChildBody);
            var bodyImage = img != null ? img.GetComponent<Image>() : null;
            if (bodyImage != null)
            {
                bodyImage.sprite = visual.Body;
                bodyImage.enabled = visual.Body != null;
                bodyImage.raycastTarget = false;
                if (img is RectTransform bodyRt) bodyRt.anchoredPosition = visual.Offset;
            }

            var labelTf = member.transform.Find(MemberPrefabChildLabel);
            var label = labelTf != null ? labelTf.GetComponent<TMP_Text>() : null;
            if (label != null)
            {
                label.text = string.IsNullOrEmpty(visual.DisplayName) ? pid : visual.DisplayName;
                label.raycastTarget = false;
            }

            member.SetActive(true);
            _memberPersonIds.Add(pid);
        }

        // 多余（上一次更多人）成员：停用而不是留成幽灵
        for (int i = wanted; i < _members.Count; i++)
        {
            if (_members[i] != null) _members[i].SetActive(false);
        }

        SetPartyStatus("队伍：" + _memberPersonIds.Count + " 人");
        return _memberPersonIds.Count;
    }

    private GameObject CreateMemberView()
    {
        if (partyMemberPrefab == null || partyHost == null) return null;
        var go = Instantiate(partyMemberPrefab, partyHost, false);
        go.name = "JourneyMember_" + _members.Count;
        return go;
    }

    // ==================== Travel 表现 ====================

    public void PresentTravel(JourneyNodeRef node, int cycleIndex, Action onComplete)
    {
        BeginTravel(node, onComplete, false);
    }

    /// <summary>
    /// N&gt;1：播放至 Branch；完成后恰好一次回调 onComplete（此时只显示 Branch Choice）。
    ///
    /// **WP-Event-1 Producer Acceptance Repair（Issue A）**：本投影**不是**「进入某个节点」——
    /// 不得把候选列表的第一个节点当作待进入节点去弹 Encounter / Battle 确认卡
    ///（否则玩家会看到一张「遭遇战斗 → 进入战斗」卡，点了却不进战斗，随后才出现 Branch 卡）。
    /// 某个候选节点真正被选中后，下一个 Cycle（N=1）才会为它显示确认卡。
    /// </summary>
    public void PresentTravelToBranch(IList<JourneyNodeRef> nodes, int cycleIndex, Action onComplete)
    {
        JourneyNodeRef target = (nodes != null && nodes.Count > 0) ? nodes[0] : default;
        BeginTravel(target, onComplete, true);
    }

    /// <summary>
    /// Travel 完成后是否需要**先**显示 Encounter / Battle 确认卡。
    /// 纯函数（不需要跑协程即可测试闸口语义）：
    ///   - Branch 投影恒 false（候选节点尚未被选中 → 不构成「进入」）；
    ///   - 其余按节点自身语义（Battle / 未迁移 Content 需要确认；正式 Event 不需要）。
    /// </summary>
    public static bool ShouldPresentEncounterCard(JourneyNodeRef pendingNode, bool branchProjection)
    {
        return !branchProjection && pendingNode.RequiresEncounterConfirmation;
    }

    public void PresentTerminal(IList<JourneyNodeRef> none, int cycleIndex)
    {
        CancelTravel();
        ShowMessage("当前路线已到终点：可选择「撤退 / 返回领地」。");
    }

    /// <summary>本次 Travel 是否为 Branch 投影（候选节点尚未选中 → 不得弹 Encounter 确认卡）。</summary>
    private bool _travelIsBranchProjection;

    private void BeginTravel(JourneyNodeRef node, Action onComplete, bool branchProjection)
    {
        CancelTravel();
        ResetTravelOffsets();
        // 只在有玩家可见文案时覆盖 Message Host：不得把「无法进入 / 补给不足」等
        // 真实拒绝反馈在下一轮旅行开始时刷掉（玩家必须能看到原因）。
        if (!string.IsNullOrEmpty(node.DisplayLabel)) ShowMessage("前往：" + node.DisplayLabel);

        _travelCompletion = onComplete;
        _pendingConfirmNode = node;
        _travelIsBranchProjection = branchProjection;
        _traveling = true;
        _travelCompleting = false;
        _travelRoutine = StartCoroutine(TravelRoutine());
    }

    private IEnumerator TravelRoutine()
    {
        float duration = Mathf.Max(0.01f, travelDuration);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            if (_paused)
            {
                yield return null;
                continue;
            }

            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            ApplyTravelProgress(t);
            yield return null;
        }

        ApplyTravelProgress(1f);
        _travelRoutine = null;
        _traveling = false;
        CompleteTravelOnce();
    }

    /// <summary>Continue（`继续前进`）跳过当前旅行表现；与自动完成汇合到同一完成入口。</summary>
    public void SkipTravel()
    {
        if (!_traveling)
        {
            ShowMessage("当前没有可以跳过的旅行表现。");
            return;
        }

        if (_travelRoutine != null)
        {
            StopCoroutine(_travelRoutine);
            _travelRoutine = null;
        }

        ApplyTravelProgress(1f);
        _traveling = false;
        CompleteTravelOnce();
    }

    /// <summary>
    /// 旅行完成（自动或 Skip）的唯一收口：内容节点在此插入**统一 Event Card** 确认。
    /// 卡面 Option 点击后才回调 onComplete（= 消费本 Cycle 的 Transition → 走既有规则入口）；
    /// 因此「一次 Cycle 最多消费一个 Transition」与「Battle 前至少一次事件卡确认」同时成立。
    /// </summary>
    private void CompleteTravelOnce()
    {
        if (_travelCompleting) return;      // 同帧自动完成 + 点击只推进一次
        _travelCompleting = true;

        // WP-Event-1 Producer Acceptance Repair（Issue A）：Branch 投影不得借候选节点弹确认卡。
        if (ShouldPresentEncounterCard(_pendingConfirmNode, _travelIsBranchProjection) && eventCard != null)
        {
            PresentEncounterCard(_pendingConfirmNode, InvokeTravelCompletion);
            return;
        }

        InvokeTravelCompletion();
    }

    private void InvokeTravelCompletion()
    {
        var cb = _travelCompletion;
        _travelCompletion = null;
        if (cb != null) cb();
    }

    /// <summary>
    /// 前进感由环境层滚动承担；人物根坐标**不再**发生整体位移（只保留极轻上下浮动）。
    /// </summary>
    private void ApplyTravelProgress(float t)
    {
        if (envNearHost != null)
            envNearHost.anchoredPosition = new Vector2(-environmentScrollDistance * t, envNearHost.anchoredPosition.y);
        if (envFarHost != null)
            envFarHost.anchoredPosition = new Vector2(
                -environmentScrollDistance * environmentFarScale * t, envFarHost.anchoredPosition.y);

        ApplyPartyStance();
    }

    /// <summary>人物固定站位（+ 极轻浮动）；不承担前进位移。</summary>
    private void ApplyPartyStance()
    {
        if (partyHost == null) return;
        float bob = (partyBobAmplitude > 0f && !_paused)
            ? Mathf.Sin(Time.unscaledTime * partyBobSpeed) * partyBobAmplitude
            : 0f;
        partyHost.anchoredPosition = new Vector2(partyStartPosition.x, partyStartPosition.y + bob);
    }

    private void ResetTravelOffsets()
    {
        ApplyTravelProgress(0f);
    }

    private void CancelTravel()
    {
        if (_travelRoutine != null)
        {
            StopCoroutine(_travelRoutine);
            _travelRoutine = null;
        }
        _traveling = false;
        _travelCompletion = null;   // 旧回调失效：不得推进后续 Progression
        _pendingConfirmNode = default;
        _travelCompleting = false;
    }

    // ==================== Pause / Continue ====================

    private void OnPauseResumeClicked()
    {
        SetPaused(!_paused);
    }

    /// <summary>Pause 只暂停表现播放，不修改任何规则时钟。</summary>
    public void SetPaused(bool paused)
    {
        _paused = paused;
        RefreshPauseLabel();
        ShowMessage(paused ? "已暂停（只暂停表现）。" : "继续旅行。");
    }

    private void RefreshPauseLabel()
    {
        if (btnPauseResumeLabel != null) btnPauseResumeLabel.text = _paused ? "继续表现" : "暂停";
    }

    private void OnContinueClicked()
    {
        // Branch / 事件卡 / Terminal 阶段不得借 Continue 代选或伪造下一步。
        if (_traveling) SkipTravel();
        else if (IsEventCardVisible) ShowMessage("请先处理当前事件卡。");
        else ShowMessage("当前没有可以跳过的旅行表现。");
    }

    // ==================== 统一 Event / Encounter Card（唯一卡面） ====================

    /// <summary>卡面是否可见（测试 / 诊断）。</summary>
    public bool IsEventCardVisible => eventCard != null && eventCard.IsVisible;

    /// <summary>当前卡面 Option 数（测试 / 诊断）。</summary>
    public int EventCardOptionCount => eventCard != null ? eventCard.OptionCount : 0;

    /// <summary>当前卡面类型（测试 / 诊断）。</summary>
    public JourneyEncounterKind EventCardKind =>
        eventCard != null ? eventCard.RenderedKind : JourneyEncounterKind.None;

    /// <summary>
    /// 逐事件插图解析（Journey Event Illustration Binding V0.1｜Implementation-only / Not Frozen）：
    ///
    ///   PresentationKey → JourneyEventPresentationCatalog → 类别级 fallback → null（隐藏插图宿主）
    ///
    /// PresentationKey 的 V0.1 来源 = 当前稳定 NodeId（Implementation-level 临时来源；不冻结 NodeId == EventId）。
    /// 未来正式 Event / Encounter Content 建立后，只需把本方法的键来源换成
    /// EventDefinitionId / EncounterDefinitionId —— Catalog 与 JourneyEventCardView 不需要重做。
    /// 本方法只决定插图，不决定合法 Node、不写 Progression、不进入 / 跳过 Battle。
    /// </summary>
    public Sprite ResolveEventIllustration(string presentationKey, JourneyEncounterKind kind, Sprite categoryFallback)
    {
        return JourneyEventIllustrationResolver.ResolveForEvent(presentationKey, kind, categoryFallback).Illustration;
    }

    /// <summary>统一渲染入口（Battle / 普通 Encounter / Branch 共用同一 Config 与 View）。</summary>
    private void PresentCard(JourneyEventCardConfig config, Action<int> onOptionChosen)
    {
        // WP-Event-1 Producer Acceptance Repair（Issue A）产品硬规则：
        // Battle / Prep 真正结束之前，不允许推进或展示下一张 Branch / 正式 Event / Encounter Journey Card。
        if (PresentationLockedForBattle)
        {
            Debug.LogWarning("[JourneyView] 战斗生命周期未结束：拒绝渲染新的 Journey Card"
                + "（Branch / 正式 Event / Encounter 不得取代战斗）。");
            return;
        }

        if (eventCard == null)
        {
            Debug.LogError("[JourneyView] 未绑定统一 Event Card View：无法显示 Encounter / Branch 卡面。");
            return;
        }
        eventCard.Render(config, onOptionChosen);
    }

    /// <summary>内容节点确认卡：Battle → 「进入战斗」；普通 / 占位 → 「继续前进」。</summary>
    private void PresentEncounterCard(JourneyNodeRef node, Action onConfirmed)
    {
        var config = BuildEncounterCardConfig(node);
        PresentCard(config, index => { if (onConfirmed != null) onConfirmed(); });
    }

    /// <summary>构造 Encounter 卡面配置（唯一模型；Option 角色区分 Battle / 普通内容）。</summary>
    public JourneyEventCardConfig BuildEncounterCardConfig(JourneyNodeRef node)
    {
        bool battle = node.EncounterKind == JourneyEncounterKind.Battle;
        Sprite categoryFallback = battle ? battleEncounterIllustration : contentEncounterIllustration;
        return new JourneyEventCardConfig
        {
            Kind = node.EncounterKind,
            // 逐事件插图（V0.1 表现键 = 当前稳定 NodeId；未配置则走类别级 fallback / 隐藏）。
            Illustration = ResolveEventIllustration(node.NodeId, node.EncounterKind, categoryFallback),
            Title = battle ? "遭遇战斗" : "遭遇（占位）",
            Body = battle
                ? "队伍在路线上遭遇敌人。确认后进入战斗。"
                : "占位内容节点：确认后按当前 Compatibility 行为继续推进（本 WP 不发明正式 Event 规则）。",
            Options = new List<JourneyEventCardOption>
            {
                new JourneyEventCardOption
                {
                    Label = battle ? "进入战斗" : "继续前进",
                    SubLabel = battle ? "（沿用现有 Battle 入口）" : "（沿用现有 Resolve 行为）",
                    NodeId = node.NodeId,
                    Role = battle ? JourneyEventOptionRole.EnterBattle : JourneyEventOptionRole.ContinueContent
                }
            }
        };
    }

    /// <summary>
    /// Branch 使用**同一个卡面**：插图 + 分支情境文案 + 每条道路一个可读 Option。
    /// Option 点击 → 若该路线需要事件卡确认（如 Battle），先过战斗卡；随后仍调用同一 Branch 入口。
    /// </summary>
    public void ShowBranchChoice(IList<JourneyNodeRef> nodes, Action<string> onChosen)
    {
        if (nodes == null || nodes.Count == 0) return;

        var config = BuildBranchCardConfig(nodes);
        PresentCard(config, index =>
        {
            if (config.Options == null || index < 0 || index >= config.Options.Count) return;
            string nodeId = config.Options[index].NodeId;
            JourneyNodeRef picked = FindNode(nodes, nodeId);

            if (picked.RequiresEncounterConfirmation)
            {
                PresentEncounterCard(picked, () => { if (onChosen != null) onChosen(nodeId); });
                return;
            }
            if (onChosen != null) onChosen(nodeId);
        });
    }

    /// <summary>构造 Branch 卡面配置（唯一模型；Option 文案必须玩家可读、非空）。</summary>
    public JourneyEventCardConfig BuildBranchCardConfig(IList<JourneyNodeRef> nodes)
    {
        var config = new JourneyEventCardConfig
        {
            Kind = JourneyEncounterKind.Branch,
            // Branch 当前是「路线选择表面」而不是正式 Event Content：没有稳定、唯一、合理的逐 Branch
            // 表现键，因此不伪造 EventId，直接走类别级 fallback（逐 Branch 插图配置的前置条件见 V0.1 Spec）。
            Illustration = JourneyEventIllustrationResolver
                .ResolveCategoryFallback(JourneyEncounterKind.Branch, branchIllustration).Illustration,
            Title = "前方出现岔路",
            Body = "选择接下来要走的路。正式路线名称与预览信息尚未冻结，此处为占位文案。",
            Options = new List<JourneyEventCardOption>()
        };

        for (int i = 0; i < nodes.Count; i++)
        {
            config.Options.Add(new JourneyEventCardOption
            {
                Label = "前往道路 " + (i + 1),
                SubLabel = "（占位路线 " + (i + 1) + "）",
                NodeId = nodes[i].NodeId,
                Role = JourneyEventOptionRole.ChooseBranch
            });
        }
        return config;
    }

    private static JourneyNodeRef FindNode(IList<JourneyNodeRef> nodes, string nodeId)
    {
        for (int i = 0; i < nodes.Count; i++)
        {
            if (nodes[i].NodeId == nodeId) return nodes[i];
        }
        return default;
    }

    /// <summary>控制器隐藏 Branch 选择：只清除 Branch 型卡面，不影响正在进行的 Encounter 卡。</summary>
    public void HideBranchChoice()
    {
        if (eventCard != null && eventCard.RenderedKind == JourneyEncounterKind.Branch) eventCard.Cancel();
    }

    // ==================== 兼容接口与失效 ====================

    /// <summary>控制器要求的表现失效：清除当前卡面（含未完成回调）。</summary>
    public void HideEncounter()
    {
        if (eventCard != null) eventCard.Cancel();
    }

    /// <summary>
    /// **正式 Event 卡渲染入口**（WP-Event-1D Gate D2 §16）：配置来自 Event Config
    /// （由产品 Adapter 投影），本方法只负责渲染与上报「玩家选择了第 index 个 Option」。
    /// 与 Battle / Branch 卡共用**同一个** <see cref="JourneyEventCardView"/>，不新建第二套 Event UI。
    /// </summary>
    public void PresentFormalEventCard(JourneyEventCardConfig config, Action<int> onOptionChosen)
    {
        if (config == null)
        {
            Debug.LogError("[JourneyView] PresentFormalEventCard 收到空配置：拒绝渲染空白事件卡。");
            return;
        }
        PresentCard(config, onOptionChosen);
    }

    /// <summary>
    /// 正式素材缺失时的**类别级占位插图**（正式 Event / Content 共用）。
    /// 只提供 fallback：事件内容与身份由 Event Config 决定，不由本属性决定。
    /// </summary>
    public Sprite ContentEncounterIllustration { get { return contentEncounterIllustration; } }

    /// <summary>Encounter / Branch 卡面是否可见（兼容旧诊断名）。</summary>
    public bool IsEncounterVisible => IsEventCardVisible;

    /// <summary>旧兼容接口：占位 Encounter 表现（现在走统一卡面，不发明 Event 规则）。</summary>
    public void PresentPlaceholderEncounter(JourneyNodeRef node, Action onShown)
    {
        // 兼容入口与正式卡面走同一条插图解析链（V0.1 表现键 = 当前稳定 NodeId）。
        ShowPlaceholderEncounter(node.DisplayLabel, "（占位内容节点：确认后继续）", node.NodeId);
        if (onShown != null) onShown();
    }

    /// <summary>Compatibility：渲染一张统一卡面（单 Option「继续」；无节点上下文 → 类别级 fallback 插图）。</summary>
    public void ShowPlaceholderEncounter(string title, string body)
    {
        ShowPlaceholderEncounter(title, body, null);
    }

    private void ShowPlaceholderEncounter(string title, string body, string presentationKey)
    {
        PresentCard(new JourneyEventCardConfig
        {
            Kind = JourneyEncounterKind.Content,
            Illustration = string.IsNullOrEmpty(presentationKey)
                ? JourneyEventIllustrationResolver
                    .ResolveCategoryFallback(JourneyEncounterKind.Content, contentEncounterIllustration).Illustration
                : ResolveEventIllustration(presentationKey, JourneyEncounterKind.Content, contentEncounterIllustration),
            Title = string.IsNullOrEmpty(title) ? "遭遇（占位）" : title,
            Body = body ?? string.Empty,
            Options = new List<JourneyEventCardOption>
            {
                new JourneyEventCardOption
                {
                    Label = "继续",
                    SubLabel = "（占位）",
                    Role = JourneyEventOptionRole.ContinueContent
                }
            }
        }, null);
    }

    /// <summary>Return / Hide / Battle / dispose / 新 Cycle：取消全部未完成表现（旧回调失效）。</summary>
    public void CancelPresentation()
    {
        CancelTravel();
        _pendingConfirmNode = default;
        if (eventCard != null) eventCard.Cancel();
    }

    private static TMP_FontAsset FindExistingFont()
    {
        var any = FindObjectOfType<TMP_Text>(true);
        return any != null ? any.font : null;
    }
}
