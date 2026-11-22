using TMPro;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// VillageCommitmentUI —— V2C Functional UI（Viewer + Controller，非 Simulation Host）。
///
/// 数据与行为原则：
///   - 只读取 Runtime 权威状态：VillageRuntimeBridge（Time/Assignments/Projects/occupancy）与
///     真实 PlayerCollection 的长期人物身份（经 VillageRuntimeBridge.GetPersonAvailability 只读映射）；
///   - 展示行由 VillageCommitmentView（纯逻辑）合成；本组件只做 UI 渲染与控制器转发；
///   - 操作全部调用 VillageRuntimeBridge 公共 API（TryAssignPerson / RemovePersonAssignment /
///     TryStartProject / TryAddProjectParticipant），不在 UI 内维护第二份 Assignment / Project / Progress；
///   - 不驱动 VillageTime（时间仍由 VillageTimeDriver + TownUI gating 负责；打开/关闭本面板不影响模拟）；
///   - 不实现正式 UI / 美术 / 职业 / 资源成本；所有文案与数值 Test / Not Frozen。
/// </summary>
public class VillageCommitmentUI : MonoBehaviour
{
    [Header("Root / 结构（Edit Mode 可绑定；未绑定时按场景路径解析）")]
    public GameObject root;                    // 整个面板根（含 Blocker/背景），Show/Hide 控制此对象
    public RectTransform panelBody;            // 内容列容器
    public TMP_Text titleText;
    public TMP_Text timeText;
    public TMP_Text villageContributionText;
    public TMP_Text messageText;
    public Button closeButton;
    public Button removeAssignmentButton;
    public Button entryButton;                 // Town 主界面入口按钮 BtnVillageCommitment
    public Button startProjectButton;

    [Header("动态容器")]
    public RectTransform personListContainer;      // Person 行（Runtime 生成）
    public TMP_Text selectedInfoText;                  // 选中 Person 详情
    public RectTransform workplaceListContainer;   // Test Workplace 行（Capacity 可读）
    public RectTransform projectDefListContainer;  // Test Project 定义选择行
    public RectTransform participantListContainer; // Participant 勾选行（Runtime 生成）
    public RectTransform activeProjectListContainer; // Active Project 进度行

    [Header("V3C 只读回看（Test / Not Frozen）")]
    [Tooltip("最近社会回应（ChoiceHistory / RestUntil）只读行容器；未绑定时按名称 SocialHistoryList 解析，" +
             "仍缺失则在运行时用现有 Section 范式兜底创建。")]
    public RectTransform socialHistoryListContainer;
    [Tooltip("最多回看的条数（Test / Not Frozen）。")]
    public int socialHistoryMaxRows = 8;

    private readonly List<GameObject> _personRows = new List<GameObject>();
    private readonly List<GameObject> _workplaceRows = new List<GameObject>();
    private readonly List<GameObject> _defRows = new List<GameObject>();
    private readonly List<GameObject> _participantRows = new List<GameObject>();
    private readonly List<GameObject> _projectRows = new List<GameObject>();

    private string _selectedPersonId;
    private string _selectedDefId = "prj_test_01";
    private readonly HashSet<string> _chosenParticipants = new HashSet<string>();
    private bool _isOpen;
    private float _nextRefreshTime;

    private const float RefreshInterval = 0.5f;
    private readonly Dictionary<RectTransform, Dictionary<string, GameObject>> _renderRows = new Dictionary<RectTransform, Dictionary<string, GameObject>>();
    private readonly HashSet<GameObject> _retainedRows = new HashSet<GameObject>();
    private readonly Dictionary<string, int> _rowOccurrences = new Dictionary<string, int>();
    private readonly Dictionary<RectTransform, int> _rowPositions = new Dictionary<RectTransform, int>();

    private void Start()
    {
        VillageRuntimeBridge.EnsureInitialized(null);
        ResolveBindings();
        BindButtons();

        // 默认隐藏面板本体；入口按钮在 Town 主界面常驻
        SetVisible(false);

        if (titleText != null) titleText.text = "Village 承诺管理（V2C Functional UI + V3C 只读回看 / Test / Not Frozen）";
    }

    private void Update()
    {
        if (!_isOpen) return;
        if (Time.unscaledTime < _nextRefreshTime) return;
        _nextRefreshTime = Time.unscaledTime + RefreshInterval;
        Refresh();
    }

    private void OnEnable()
    {
        _nextRefreshTime = 0f;
        if (_isOpen) Refresh();
    }

    /// <summary>打开面板（不影响 Simulation gating）。</summary>
    public void Show()
    {
        VillageRuntimeBridge.EnsureInitialized(null);
        SetVisible(true);
        _nextRefreshTime = 0f;
        Refresh();
    }

    /// <summary>关闭面板（不影响 Simulation gating）。</summary>
    public void Hide()
    {
        SetVisible(false);
    }

    private void SetVisible(bool visible)
    {
        _isOpen = visible;
        if (root != null) root.SetActive(visible);
    }

    private void ResolveBindings()
    {
        if (root == null) root = gameObject;
        if (panelBody == null) panelBody = Find<RectTransform>("PanelBody");
        if (titleText == null) titleText = FindText("TitleText");
        if (timeText == null) timeText = FindText("TimeText");
        if (villageContributionText == null) villageContributionText = FindText("VillageContributionText");
        if (messageText == null) messageText = FindText("MessageText");
        if (closeButton == null) closeButton = FindButton("BtnClose");
        if (removeAssignmentButton == null) removeAssignmentButton = FindButton("BtnRemoveAssignment");
        if (startProjectButton == null) startProjectButton = FindButton("BtnStartProject");
        if (personListContainer == null) personListContainer = Find<RectTransform>("PersonList");
        if (selectedInfoText == null) selectedInfoText = FindText("SelectedInfoText");
        if (workplaceListContainer == null) workplaceListContainer = Find<RectTransform>("WorkplaceList");
        if (projectDefListContainer == null) projectDefListContainer = Find<RectTransform>("ProjectDefList");
        if (participantListContainer == null) participantListContainer = Find<RectTransform>("ParticipantList");
        if (activeProjectListContainer == null) activeProjectListContainer = Find<RectTransform>("ActiveProjectList");

        // V3C（R1）：只读回看容器；场景缺失时用既有 Section 范式兜底创建（不新建页面、不改 Scene 结构语义）。
        if (socialHistoryListContainer == null) socialHistoryListContainer = Find<RectTransform>("SocialHistoryList");
        if (socialHistoryListContainer == null) EnsureSocialHistorySection();

        if (entryButton == null)
        {
            // Town 主界面入口按钮：由 Editor Authoring 创建在 TownCanvas 下
            var town = FindTown();
            if (town != null)
            {
                var canvas = town.transform.Find("TownCanvas");
                if (canvas != null)
                {
                    var b = canvas.Find("BtnVillageCommitment");
                    if (b != null) entryButton = b.GetComponent<Button>();
                }
            }
        }
    }

    private void BindButtons()
    {
        if (entryButton != null)
        {
            entryButton.onClick.RemoveAllListeners();
            entryButton.onClick.AddListener(Show);
        }
        if (closeButton != null)
        {
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(Hide);
        }
        if (removeAssignmentButton != null)
        {
            removeAssignmentButton.onClick.RemoveAllListeners();
            removeAssignmentButton.onClick.AddListener(OnRemoveAssignmentClicked);
        }
        if (startProjectButton != null)
        {
            startProjectButton.onClick.RemoveAllListeners();
            startProjectButton.onClick.AddListener(OnStartProjectClicked);
        }
    }

    private T Find<T>(string name) where T : Component
    {
        var t = FindDeep(name);
        return t != null ? t.GetComponent<T>() : null;
    }

    private Transform FindDeep(string name)
    {
        if (transform.name == name) return transform;
        foreach (var t in GetComponentsInChildren<Transform>(true))
        {
            if (t.name == name) return t;
        }
        return null;
    }

    private TMP_Text FindText(string name)
    {
        return Find<TMP_Text>(name);
    }

    private Button FindButton(string name)
    {
        return Find<Button>(name);
    }

    private TownUI FindTown()
    {
        return FindObjectOfType<TownUI>(true);
    }

    private PlayerCollection GetPlayerData()
    {
        var town = FindTown();
        if (town != null && town.PlayerData != null) return town.PlayerData;
        return null;
    }

    // ==================== 权威刷新（Runtime → VillageCommitmentView → 渲染） ====================

    private void Refresh()
    {
        var player = GetPlayerData();
        if (player == null)
        {
            if (messageText != null) messageText.text = "尚未进入真实 Town（PlayerCollection 不可用）";
            return;
        }

        if (timeText != null)
        {
            timeText.text = VillageRuntimeBridge.Time != null
                ? "日期：" + VillageRuntimeBridge.Time.GetDateText()
                : "日期：--";
        }

        int contribution = VillageRuntimeBridge.Assignments != null
            ? VillageRuntimeBridge.Assignments.TotalTestContribution
            : 0;
        if (villageContributionText != null)
        {
            villageContributionText.text = "Village Test Contribution（全部岗位合计）：" + contribution +
                                           "（Test / Not Frozen）";
        }

        var views = VillageRuntimeBridge.GetPersonAvailability(player);
        var personNames = new Dictionary<string, string>();
        foreach (var v in views)
        {
            if (v == null || string.IsNullOrEmpty(v.PersonId)) continue;
            personNames[v.PersonId] = string.IsNullOrEmpty(v.DisplayName) ? v.PersonId : v.DisplayName;
        }

        _retainedRows.Clear();
        _rowOccurrences.Clear();
        _rowPositions.Clear();
        RebuildPersonRows(views, personNames);
        RebuildWorkplaceRows(personNames);
        RebuildProjectDefRows();
        RebuildParticipantRows(views);
        RebuildActiveProjectRows(personNames);
        RebuildSocialHistoryRows(personNames);
        UpdateSelectedInfo(views, personNames);
        RemoveObsoleteRows();
    }

    /// <summary>
    /// V3C：统一从 Runtime 权威状态构建 Person 行（含 Rest 可见性）。
    /// Rest 判定复用 VillageSocialResponseLog 权威规则；UI 不得自行推导社会规则。
    /// </summary>
    private static List<VillageCommitmentView.CommitmentPersonRow> BuildPersonRows(List<VillagePersonView> views)
    {
        var t = VillageRuntimeBridge.Time;

        // WP-Event-1D Gate D1：Rest 权威已独立 → 只读展示改读正式 Rest Authority
        //（新权威优先；旧 `SocialResponse.Rests` 仅在该 Authority 缺失时兼容，与 Rest Gate 同口径）。
        if (VillageRuntimeBridge.PersonRest != null)
        {
            return VillageCommitmentView.BuildPersonRows(
                views,
                VillageRuntimeBridge.Assignments,
                VillageRuntimeBridge.Projects,
                VillageRuntimeBridge.PersonRest,
                t != null ? t.Year : 0,
                t != null ? t.Month : 0,
                t != null ? t.Day : 0);
        }

        return VillageCommitmentView.BuildPersonRows(
            views,
            VillageRuntimeBridge.Assignments,
            VillageRuntimeBridge.Projects,
            VillageRuntimeBridge.SocialResponse,
            t != null ? t.Year : 0,
            t != null ? t.Month : 0,
            t != null ? t.Day : 0);
    }

    private void RebuildPersonRows(List<VillagePersonView> views, Dictionary<string, string> personNames)
    {
        ClearContainer(personListContainer);
        var rows = BuildPersonRows(views);
        foreach (var r in rows)
        {
            var go = CreateRow(personListContainer, "PersonRow_" + r.PersonId);
            string line = r.DisplayName + " ｜ " + AvailabilityLabel(r.Availability) + " ｜ " + r.StatusText;
            // V3C（R2）：Rest 状态主动可见（只读；数据来源 = Rest 权威规则）。
            if (r.IsRestActive && !string.IsNullOrEmpty(r.RestText)) line += " ｜ " + r.RestText;
            SetText(go, line, r.IsRestActive ? RestColor() : StatusColor(r.Status));

            var button = go.GetComponent<Button>() ?? go.AddComponent<Button>();
            var target = r.PersonId;
            button.onClick.AddListener(() => OnPersonClicked(target));
        }
    }

    private void RebuildWorkplaceRows(Dictionary<string, string> personNames)
    {
        ClearContainer(workplaceListContainer);
        var rows = VillageCommitmentView.BuildWorkplaceRows(
            VillageRuntimeBridge.Assignments.WorkplaceDefs, VillageRuntimeBridge.Assignments, personNames);
        foreach (var r in rows)
        {
            var go = CreateRow(workplaceListContainer, "WorkplaceRow_" + r.WorkplaceId);
            string prefix = r.IsFull ? "【已满：不能继续分配】" : "【指派到此处】";
            string line = prefix + "　" + r.DisplayName + "　占用 " + r.CapacityText;
            if (!string.IsNullOrEmpty(r.OccupantText)) line += "｜" + r.OccupantText;
            SetText(go, line, r.IsFull ? new Color(0.85f, 0.6f, 0.35f, 1f) : new Color(0.55f, 0.85f, 0.55f, 1f));

            var button = go.GetComponent<Button>() ?? go.AddComponent<Button>();
            button.interactable = !r.IsFull;
            var workplaceId = r.WorkplaceId;
            if (!r.IsFull) button.onClick.AddListener(() => OnAssignClicked(workplaceId));
        }
    }

    private void RebuildProjectDefRows()
    {
        ClearContainer(projectDefListContainer);
        var rows = VillageCommitmentView.BuildProjectDefRows(VillageRuntimeBridge.TestProjectDefs);
        foreach (var r in rows)
        {
            var go = CreateRow(projectDefListContainer, "ProjectDefRow_" + r.ProjectDefId);
            string selectedMark = _selectedDefId == r.ProjectDefId ? "【选中】" : "";
            SetText(go, selectedMark + r.DisplayName + "　" + r.RuleText,
                _selectedDefId == r.ProjectDefId ? new Color(0.85f, 0.8f, 0.4f, 1f) : Color.white);
            var btn = go.GetComponent<Button>() ?? go.AddComponent<Button>();
            var defId = r.ProjectDefId;
            btn.onClick.AddListener(() => { _selectedDefId = defId; Refresh(); });
        }
    }

    private void RebuildParticipantRows(List<VillagePersonView> views)
    {
        ClearContainer(participantListContainer);
        var rows = BuildPersonRows(views);
        foreach (var r in rows)
        {
            // 参与者候选 = 在村（含已有 Assignment，用于演示 Opportunity Cost）且未被占用
            bool selectable = r.Availability == VillageAvailability.InVillage
                && r.Status != VillageCommitmentView.PersonCommitmentStatus.InProject;
            string reason = selectable ? "" : "（不可选：已占用/远征/暂不可用）";
            // V3C（R2）：恢复窗口内的 Person 在被拒绝前即可预知其不可安排（不改变调度语义）。
            if (r.IsRestActive && !string.IsNullOrEmpty(r.RestText))
            {
                reason += "（本轮不会被安排加入新的临时工程：" + r.RestText + "）";
            }

            var go = CreateRow(participantListContainer, "ParticipantRow_" + r.PersonId);
            var textComp = SetText(go, r.DisplayName + "　" + r.StatusText + reason,
                selectable ? Color.white : new Color(0.55f, 0.55f, 0.55f, 1f));

            var toggle = go.GetComponent<Toggle>() ?? go.AddComponent<Toggle>();
            var personId = r.PersonId;
            toggle.isOn = _chosenParticipants.Contains(personId);
            toggle.interactable = selectable;
            toggle.onValueChanged.AddListener(isOn =>
            {
                if (isOn) _chosenParticipants.Add(personId);
                else _chosenParticipants.Remove(personId);
                textComp.color = isOn ? new Color(0.5f, 0.9f, 0.5f, 1f) : Color.white;
            });
            if (toggle.isOn) textComp.color = new Color(0.5f, 0.9f, 0.5f, 1f);
        }
    }

    private void RebuildActiveProjectRows(Dictionary<string, string> personNames)
    {
        ClearContainer(activeProjectListContainer);
        var rows = VillageCommitmentView.BuildActiveProjectRows(VillageRuntimeBridge.Projects, personNames);
        if (rows.Count == 0)
        {
            var empty = CreateRow(activeProjectListContainer, "NoActiveProject");
            SetText(empty, "（当前没有进行中的 Test Project）", new Color(0.6f, 0.6f, 0.6f, 1f));
            return;
        }
        foreach (var r in rows)
        {
            var go = CreateRow(activeProjectListContainer, "ActiveProjectRow_" + r.ProjectId);
            SetText(go, r.SummaryText, new Color(0.55f, 0.85f, 0.9f, 1f));
        }

        // 已完成提示（读取 Runtime 运行期 Completed 记录，只读展示）
        if (VillageRuntimeBridge.Projects != null
            && VillageRuntimeBridge.Projects.CompletedProjects.Count > 0)
        {
            var last = VillageRuntimeBridge.Projects.CompletedProjects[
                VillageRuntimeBridge.Projects.CompletedProjects.Count - 1];
            var done = CreateRow(activeProjectListContainer, "RecentlyCompleted");
            SetText(done, "已完成并自动解除占用：" + last.ProjectId + "（参与者已回到原岗位）",
                new Color(0.4f, 0.8f, 0.4f, 1f));
        }
    }

    private void UpdateSelectedInfo(List<VillagePersonView> views, Dictionary<string, string> personNames)
    {
        if (selectedInfoText == null) return;
        if (string.IsNullOrEmpty(_selectedPersonId))
        {
            selectedInfoText.text = "（点击上方一位 Person 查看其承诺详情并指派岗位 / 参与 Project）";
            return;
        }

        var rows = BuildPersonRows(views);
        foreach (var r in rows)
        {
            if (r.PersonId != _selectedPersonId) continue;
            string line = r.DisplayName + "（" + r.PersonId + "）\n" + r.StatusText;
            line += "\n实际执行：" + VillageRuntimeBridge.GetWorkExecutionLabel(r.PersonId);
            if (!string.IsNullOrEmpty(r.WorkplaceDisplayName))
                line += "\nPrimary Assignment：" + r.WorkplaceDisplayName;
            if (!string.IsNullOrEmpty(r.ActiveProjectDisplayName))
                line += "\n当前 Project：" + r.ActiveProjectDisplayName;

            // V3C（R2）：Rest 状态主动可见——复用桥接层 Rest 权威 API（不新增 Rest State）。
            if (VillageRuntimeBridge.IsPersonInRestWindow(_selectedPersonId))
            {
                string restDesc = VillageRuntimeBridge.GetPersonRestDescription(_selectedPersonId);
                line += "\n状态：恢复中（" +
                        (string.IsNullOrEmpty(restDesc) ? "恢复窗口内" : restDesc) + "）";
            }

            selectedInfoText.text = line;
            return;
        }
        selectedInfoText.text = "（选中 Person 已不在当前长期人物列表中）";
    }

    // ==================== V3C（R1）：最近社会回应只读回看 ====================

    /// <summary>
    /// 渲染「最近的村民回应」只读行（ChoiceHistory / RestUntil）。
    /// 只渲染 VillageCommitmentView 合成结果；本组件不推导社会规则、不读取 fired。
    /// </summary>
    private void RebuildSocialHistoryRows(Dictionary<string, string> personNames)
    {
        if (socialHistoryListContainer == null) return;

        ClearContainer(socialHistoryListContainer);
        var rows = VillageCommitmentView.BuildRecentSocialHistoryRows(
            VillageRuntimeBridge.SocialResponse,
            VillageRuntimeBridge.PersonRest,
            personNames, socialHistoryMaxRows);

        if (rows.Count == 0)
        {
            var empty = CreateRow(socialHistoryListContainer, "NoSocialHistory");
            SetText(empty, "（还没有需要回看的村民回应 / 选择记录）", new Color(0.6f, 0.6f, 0.6f, 1f));
            return;
        }

        foreach (var r in rows)
        {
            string prefix = r.Kind == VillageCommitmentView.SocialHistoryKind.RestWindow
                ? "［恢复窗口］"
                : "［回应选择］";
            var go = CreateRow(socialHistoryListContainer,
                "SocialHistoryRow_" + r.Kind + "_" + r.PersonId + "_" + r.Year + r.Month + r.Day);
            SetText(go, prefix + r.DetailText,
                r.Kind == VillageCommitmentView.SocialHistoryKind.RestWindow ? RestColor() : Color.white);
        }
    }

    /// <summary>
    /// 场景中没有 SocialHistoryList 时的最小兜底：用既有 Section 范式（Image + VerticalLayoutGroup +
    /// ContentSizeFitter + LayoutElement）在 ActiveProjectList 之后插入一个只读区段与说明标签。
    /// 不新建页面、不改变既有区段结构与顺序。
    /// </summary>
    private void EnsureSocialHistorySection()
    {
        var anchor = activeProjectListContainer;
        if (anchor == null) return;
        var parent = anchor.parent;
        if (parent == null) return;

        int insertIndex = anchor.GetSiblingIndex() + 1;

        var labelGo = new GameObject("SectionSocialHistoryLabel",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI), typeof(LayoutElement));
        labelGo.transform.SetParent(parent, false);
        var labelRt = labelGo.GetComponent<RectTransform>();
        labelRt.anchorMin = new Vector2(0f, 0.5f);
        labelRt.anchorMax = new Vector2(1f, 0.5f);
        labelRt.sizeDelta = new Vector2(0f, 32f);
        var labelText = labelGo.GetComponent<TMP_Text>();
        labelText.text = "⑥ 最近的村民回应（只读回看：选择记录 / 恢复窗口；Test / Not Frozen）";
        labelText.fontSize = 17;
        labelText.alignment = TextAlignmentOptions.Left;
        labelText.color = Color.white;
        labelText.enableWordWrapping = true;
        labelText.overflowMode = TextOverflowModes.Overflow;
        labelText.raycastTarget = false;
        labelText.font = CurrentUIFontAuthority.Default;
        var labelLayout = labelGo.GetComponent<LayoutElement>();
        labelLayout.minHeight = 32f;
        labelLayout.preferredHeight = 32f;
        labelGo.transform.SetSiblingIndex(insertIndex);

        var sectionGo = new GameObject("SocialHistoryList",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image),
            typeof(VerticalLayoutGroup), typeof(ContentSizeFitter), typeof(LayoutElement));
        sectionGo.transform.SetParent(parent, false);
        var sectionRt = sectionGo.GetComponent<RectTransform>();
        sectionRt.anchorMin = new Vector2(0f, 0.5f);
        sectionRt.anchorMax = new Vector2(1f, 0.5f);
        sectionRt.sizeDelta = new Vector2(0f, 0f);

        var image = sectionGo.GetComponent<Image>();
        image.color = new Color(0.16f, 0.2f, 0.26f, 0.55f);
        image.raycastTarget = false;

        var layout = sectionGo.GetComponent<VerticalLayoutGroup>();
        layout.spacing = 2f;
        layout.padding = new RectOffset(4, 4, 4, 4);
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        var fitter = sectionGo.GetComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        sectionGo.transform.SetSiblingIndex(insertIndex + 1);
        socialHistoryListContainer = sectionRt;
    }

    // ==================== UI 渲染 helpers ====================

    private void ClearContainer(RectTransform container)
    {
        if (container == null) return;
        if (!_renderRows.ContainsKey(container)) _renderRows.Add(container, new Dictionary<string, GameObject>());
    }

    private void RemoveObsoleteRows()
    {
        foreach (var rows in _renderRows.Values)
        {
            var obsolete = new List<string>();
            foreach (var pair in rows)
            {
                if (pair.Value != null && _retainedRows.Contains(pair.Value)) continue;
                if (pair.Value != null) { pair.Value.SetActive(false); Destroy(pair.Value); }
                obsolete.Add(pair.Key);
            }
            foreach (var key in obsolete) rows.Remove(key);
        }
    }

    private GameObject CreateRow(RectTransform container, string name)
    {
        ClearContainer(container);
        string occurrenceKey = container.GetInstanceID() + ":" + name;
        int occurrence;
        _rowOccurrences.TryGetValue(occurrenceKey, out occurrence);
        _rowOccurrences[occurrenceKey] = occurrence + 1;
        string rowKey = name + ":" + occurrence;
        int position;
        _rowPositions.TryGetValue(container, out position);
        _rowPositions[container] = position + 1;
        GameObject existing;
        if (_renderRows[container].TryGetValue(rowKey, out existing) && existing != null)
        {
            _retainedRows.Add(existing);
            existing.transform.SetSiblingIndex(position);
            var button = existing.GetComponent<Button>(); if (button != null) button.onClick.RemoveAllListeners();
            var toggle = existing.GetComponent<Toggle>(); if (toggle != null) toggle.onValueChanged.RemoveAllListeners();
            return existing;
        }
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI),
            typeof(LayoutElement));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(container, false);
        rt.SetSiblingIndex(position);
        rt.anchorMin = new Vector2(0f, 0.5f);
        rt.anchorMax = new Vector2(1f, 0.5f);
        rt.sizeDelta = new Vector2(0f, 34f);

        var layout = go.GetComponent<LayoutElement>();
        layout.minHeight = 34f;
        layout.preferredHeight = 34f;
        _renderRows[container][rowKey] = go;
        _retainedRows.Add(go);
        return go;
    }

    private TMP_Text SetText(GameObject go, string text, Color color)
    {
        var textComp = go.GetComponent<TMP_Text>();
        textComp.text = text;
        textComp.fontSize = 15;
        textComp.alignment = TextAlignmentOptions.Left;
        textComp.color = color;
        textComp.enableWordWrapping = true;
        textComp.overflowMode = TextOverflowModes.Overflow;
        textComp.font = CurrentUIFontAuthority.Default;
        return textComp;
    }

    private TMP_FontAsset GetAnyFont()
    {
        // 遍历场景 TMP_Text，取第一个 font 非空的引用（否则文字不可见）
        var texts = FindObjectsOfType<TMP_Text>(true);
        foreach (var t in texts)
        {
            if (t != null && t.font != null) return t.font;
        }
        return null;
    }

    private static string AvailabilityLabel(VillageAvailability a)
    {
        switch (a)
        {
            case VillageAvailability.InVillage: return "在村";
            case VillageAvailability.Expedition: return "远征中";
            default: return "暂不可用";
        }
    }

    private static Color StatusColor(VillageCommitmentView.PersonCommitmentStatus status)
    {
        switch (status)
        {
            case VillageCommitmentView.PersonCommitmentStatus.Working:
                return new Color(0.55f, 0.85f, 0.55f, 1f);
            case VillageCommitmentView.PersonCommitmentStatus.InProject:
                return new Color(0.55f, 0.85f, 0.9f, 1f);
            case VillageCommitmentView.PersonCommitmentStatus.Expedition:
                return new Color(0.95f, 0.75f, 0.3f, 1f);
            case VillageCommitmentView.PersonCommitmentStatus.Unavailable:
                return new Color(0.6f, 0.6f, 0.6f, 1f);
            default:
                return Color.white;
        }
    }

    /// <summary>V3C（R2）：恢复窗口状态行颜色（Test / Not Frozen 表现，不代表正式 UI/美术）。</summary>
    private static Color RestColor()
    {
        return new Color(0.85f, 0.7f, 0.95f, 1f);
    }

    // ==================== 控制器：Person / Assignment ====================

    private void OnPersonClicked(string personId)
    {
        _selectedPersonId = personId;
        ShowMessage("已选择：" + PersonDisplayName(personId) + "（" + personId + "）。点击岗位行即可指派。");
        Refresh();
    }

    private string PersonDisplayName(string personId)
    {
        var player = GetPlayerData();
        if (player == null) return personId;
        var views = VillageRuntimeBridge.GetPersonAvailability(player);
        foreach (var v in views)
        {
            if (v.PersonId == personId) return string.IsNullOrEmpty(v.DisplayName) ? personId : v.DisplayName;
        }
        return personId;
    }

    private void OnAssignClicked(string workplaceId)
    {
        if (string.IsNullOrEmpty(_selectedPersonId))
        {
            ShowMessage("请先在上方点击选择一位 Person。");
            return;
        }

        var result = VillageRuntimeBridge.TryAssignPerson(_selectedPersonId, workplaceId);
        switch (result)
        {
            case VillageAssignmentResult.Success:
                ShowMessage("已给 " + PersonDisplayName(_selectedPersonId) + " 指定 Primary Assignment。");
                break;
            case VillageAssignmentResult.CapacityFull:
                ShowMessage("无法指派：该岗位已满员（Capacity 限制）。");
                break;
            case VillageAssignmentResult.AlreadyAssigned:
                ShowMessage("该 Person 已有 Primary Assignment。请先移除当前岗位再改派。");
                break;
            default:
                ShowMessage("指派失败：" + result);
                break;
        }
        Refresh();
    }

    private void OnRemoveAssignmentClicked()
    {
        if (string.IsNullOrEmpty(_selectedPersonId))
        {
            ShowMessage("请先在上方点击选择一位 Person。");
            return;
        }

        bool removed = VillageRuntimeBridge.RemovePersonAssignment(_selectedPersonId);
        ShowMessage(removed
            ? "已移除 " + PersonDisplayName(_selectedPersonId) + " 的 Primary Assignment。"
            : "该 Person 当前没有 Primary Assignment。");
        Refresh();
    }

    private void ShowMessage(string text)
    {
        if (messageText != null) messageText.text = text;
    }

    // ==================== 控制器：Project ====================

    private void OnStartProjectClicked()
    {
        if (string.IsNullOrEmpty(_selectedDefId))
        {
            ShowMessage("请先选择一个 Test Project。");
            return;
        }

        // 只保留仍然可加入（在村且未被占用）的人；其余由 Runtime 校验拒绝
        var player = GetPlayerData();
        var participants = new List<string>();
        if (player != null)
        {
            var views = VillageRuntimeBridge.GetPersonAvailability(player);
            foreach (var v in views)
            {
                if (v == null || !_chosenParticipants.Contains(v.PersonId)) continue;
                participants.Add(v.PersonId);
            }
        }

        if (participants.Count == 0)
        {
            ShowMessage("请至少勾选一位在村 Person 作为 Project 参与者。");
            return;
        }

        var result = VillageRuntimeBridge.TryStartProject(_selectedDefId, participants.ToArray());
        switch (result)
        {
            case VillageProjectResult.Success:
                ShowMessage("Test Project 已启动，参与者：" + JoinNames(participants) +
                            "。其原岗位将暂停执行，完成后自动回来。");
                break;
            case VillageProjectResult.CapacityFull:
                ShowMessage("Project 参与者容量不足（最多容纳该 Project 定义容量）。");
                break;
            case VillageProjectResult.PersonAlreadyInActiveProject:
                ShowMessage("存在已在其它 Active Project 的 Person，不能同时参与两个 Active Project。");
                break;
            case VillageProjectResult.PersonCurrentlyUnavailable:
                ShowMessage("存在当前不可用（远征/暂不可用）的 Person。");
                break;
            case VillageProjectResult.PersonInRestWindow:
                ShowMessage("存在正处于恢复窗口的 Person（自选后果）：恢复期结束前不会被安排加入新的临时工程。");
                break;
            default:
                ShowMessage("Project 启动失败：" + result);
                break;
        }
        _chosenParticipants.Clear();
        Refresh();
    }

    private string JoinNames(List<string> ids)
    {
        var parts = new List<string>();
        foreach (var id in ids) parts.Add(PersonDisplayName(id));
        return parts.Count > 0 ? string.Join("、", parts) : "（无）";
    }
}
