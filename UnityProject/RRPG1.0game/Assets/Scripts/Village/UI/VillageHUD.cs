using TMPro;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Village 功能 HUD（Stage V1 / Town Functional HUD；非正式 Village UI）。
///
/// 宿主与静态面板元素在场景中 Edit Mode 可见可调；运行时只生成「人物可用状态」动态行（重复内容）。
/// 职责：
///   - 驱动 VillageTime（Pause / Resume / Speed / JumpToNextMonth）；
///   - 显示：当前日期、Pause / Speed 状态、Test State（Not Frozen）、人物当前可用状态；
///   - 数据一律读取 VillageRuntimeBridge（单一真相），不复制第二份状态。
/// 所有数值与格式均为 Test / Not Frozen。
/// </summary>
public class VillageHUD : MonoBehaviour
{
    [Header("显示（Edit Mode 绑定）")]
    [Tooltip("当前日期文本")]
    public TMP_Text dateText;

    [Tooltip("Village Test State 文本")]
    public TMP_Text stateText;

    [Tooltip("人物可用状态标题/汇总文本")]
    public TMP_Text availabilityTitleText;

    [Tooltip("人物可用状态动态行容器（运行时只生成重复行）")]
    public RectTransform availabilityContainer;

    [Header("控制（Edit Mode 绑定）")]
    public Button btnPause;
    public Button btnSpeed;
    public Button btnJumpMonth;
    public TMP_Text pauseLabel;
    public TMP_Text speedLabel;

    [Header("VillageTime 参数（Test / Not Frozen）")]
    [Tooltip("遗留 Inspector 字段；时钟默认由 VillageTime 定义为现实 24 分钟一天，本字段不驱动时间。")]
    public float secondsPerDay = VillageTime.DefaultSecondsPerDay;

    [Header("人物行布局（Inspector 可调）")]
    public Vector2 personRowSize = new Vector2(700, 26);
    public float personRowSpacing = 30f;
    public int personRowFontSize = 16;

    private readonly Dictionary<string, TMP_Text> _rows = new Dictionary<string, TMP_Text>();

    void Start()
    {
        ResolveBindings();

        VillageRuntimeBridge.EnsureInitialized(null);
        // Clock/legacy conversion is initialized by the domain before Restore, never HUD Start.

        if (btnPause != null) btnPause.onClick.AddListener(OnPauseToggle);
        if (btnSpeed != null) btnSpeed.onClick.AddListener(OnSpeedToggle);
        if (btnJumpMonth != null) btnJumpMonth.onClick.AddListener(OnJumpMonth);

        Refresh();
    }

    /// <summary>
    /// 未在 Inspector 绑定时按场景路径解析（场景对象已按固定命名创建；Inspector 绑定优先）。
    /// 与 RunRewardUI.ResolveBindings 一致的模式，Edit Mode 中字段仍可在 Inspector 调整。
    /// </summary>
    private void ResolveBindings()
    {
        if (dateText == null) dateText = FindText("VillageDateText");
        if (stateText == null) stateText = FindText("VillageStateText");
        if (availabilityTitleText == null) availabilityTitleText = FindText("VillageAvailabilityTitle");
        if (availabilityContainer == null)
        {
            var t = transform.Find("VillagePersonList");
            if (t != null) availabilityContainer = t as RectTransform;
        }
        if (btnPause == null) btnPause = FindButton("BtnPause");
        if (btnSpeed == null) btnSpeed = FindButton("BtnSpeed");
        if (btnJumpMonth == null) btnJumpMonth = FindButton("BtnJumpMonth");
        if (pauseLabel == null && btnPause != null) pauseLabel = btnPause.GetComponentInChildren<TMP_Text>(true);
        if (speedLabel == null && btnSpeed != null) speedLabel = btnSpeed.GetComponentInChildren<TMP_Text>(true);
    }

    private TMP_Text FindText(string name)
    {
        var t = transform.Find(name);
        return t != null ? t.GetComponent<TMP_Text>() : null;
    }

    private Button FindButton(string name)
    {
        var t = transform.Find(name);
        return t != null ? t.GetComponent<Button>() : null;
    }

    /// <summary>静态文本和控制标签统一使用 Current UI Font Authority。</summary>
    private void EnsureFonts()
    {
        TMP_FontAsset font = CurrentUIFontAuthority.Default;
        var texts = new List<TMP_Text>();
        if (dateText != null) texts.Add(dateText);
        if (stateText != null) texts.Add(stateText);
        if (availabilityTitleText != null) texts.Add(availabilityTitleText);
        if (pauseLabel != null) texts.Add(pauseLabel);
        if (speedLabel != null) texts.Add(speedLabel);
        foreach (var btn in new[] { btnPause, btnSpeed, btnJumpMonth })
        {
            if (btn != null)
            {
                var label = btn.GetComponentInChildren<TMP_Text>(true);
                if (label != null) texts.Add(label);
            }
        }

        foreach (var t in texts)
        {
            if (t == null) continue;
            if (font != null && t.font != font) t.font = font;
            // Display labels must not shield world entities; buttons keep their Image targets.
            t.raycastTarget = false;
        }
    }

    void Update()
    {
        // 时间推进由常驻 VillageTimeDriver + VillageRuntimeBridge.IsSimulationActive gating 负责；
        // HUD 只负责显示与操作，不承载世界时间生命周期。
        Refresh();
    }

    // ==================== 控制 ====================

    private void OnPauseToggle()
    {
        var time = VillageRuntimeBridge.Time;
        if (time == null) return;
        if (time.Speed == VillageTime.VillageTimeSpeed.Paused) time.Resume();
        else time.Pause();
    }

    private void OnSpeedToggle()
    {
        var time = VillageRuntimeBridge.Time;
        if (time == null) return;
        time.CycleSpeed();
    }

    private void OnJumpMonth()
    {
        if (VillageRuntimeBridge.Time != null) VillageRuntimeBridge.Time.JumpToNextMonth();
    }

    // ==================== 显示 ====================

    void Refresh()
    {
        var time = VillageRuntimeBridge.Time;
        var state = VillageRuntimeBridge.State;

        EnsureFonts();

        if (dateText != null)
            dateText.text = "日期: " + (time != null ? time.GetDateText() : "（无时间）");

        if (stateText != null)
            stateText.text = "Village Test State（Not Frozen）: " + (state != null ? state.TestSurvivalValue : 0);

        RefreshControlButtons(time);
        RefreshAvailability();
    }

    private void RefreshControlButtons(VillageTime time)
    {
        if (time == null) return;
        if (btnPause != null)
        {
            TMP_Text t = pauseLabel;
            if (t != null)
                t.text = time.Speed == VillageTime.VillageTimeSpeed.Paused ? "继续" : "暂停";
        }
        if (btnSpeed != null)
        {
            TMP_Text t = speedLabel;
            if (t != null)
                t.text = "x" + time.SpeedMultiplier.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);
        }
    }

    private void RefreshAvailability()
    {
        if (availabilityTitleText != null)
        {
            var views = VillageRuntimeBridge.GetPersonAvailability(GetPlayerData());
            int inVillage = 0, expedition = 0, unavailable = 0;
            foreach (var v in views)
            {
                switch (v.Availability)
                {
                    case VillageAvailability.InVillage: inVillage++; break;
                    case VillageAvailability.Expedition: expedition++; break;
                    default: unavailable++; break;
                }
            }
            availabilityTitleText.text = string.Format("人物（在村 {0} / 远征 {1} / 不可用 {2}）", inVillage, expedition, unavailable);
        }

        if (availabilityContainer == null) return;

        var retained = new HashSet<string>();
        var all = VillageRuntimeBridge.GetPersonAvailability(GetPlayerData());
        for (int i = 0; i < all.Count; i++)
        {
            var v = all[i];
            retained.Add(v.PersonId);
            TMP_Text text;
            if (!_rows.TryGetValue(v.PersonId, out text) || text == null)
            {
                var row = new GameObject("VillagePersonRow_" + v.PersonId, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                row.transform.SetParent(availabilityContainer, false);
                text = row.GetComponent<TMP_Text>();
                _rows[v.PersonId] = text;
            }

            var rt = text.rectTransform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(0f, -i * personRowSpacing);
            rt.sizeDelta = personRowSize;

            text.font = CurrentUIFontAuthority.Default;
            text.raycastTarget = false;
            text.text = v.DisplayName + "  →  " + AvailabilityLabel(v.Availability);
            text.fontSize = personRowFontSize;
            text.alignment = TextAlignmentOptions.Left;
            text.color = AvailabilityColor(v.Availability);
            text.enableWordWrapping = true;
            text.overflowMode = TextOverflowModes.Overflow;
        }
        var obsolete = new List<string>();
        foreach (var pair in _rows)
        {
            if (retained.Contains(pair.Key)) continue;
            if (pair.Value != null) { pair.Value.gameObject.SetActive(false); Destroy(pair.Value.gameObject); }
            obsolete.Add(pair.Key);
        }
        foreach (var id in obsolete) _rows.Remove(id);
    }

    private string AvailabilityLabel(VillageAvailability a)
    {
        switch (a)
        {
            case VillageAvailability.InVillage: return "在村";
            case VillageAvailability.Expedition: return "远征中";
            default: return "暂不可用";
        }
    }

    private Color AvailabilityColor(VillageAvailability a)
    {
        switch (a)
        {
            case VillageAvailability.InVillage: return new Color(0.55f, 0.85f, 0.55f, 1f);
            case VillageAvailability.Expedition: return new Color(0.95f, 0.75f, 0.3f, 1f);
            default: return new Color(0.6f, 0.6f, 0.6f, 1f);
        }
    }

    private PlayerCollection GetPlayerData()
    {
        var town = FindObjectOfType<TownUI>(true);
        if (town != null && town.PlayerData != null) return town.PlayerData;
        return SaveSystem.HasSave() ? SaveSystem.Load() : null;
    }
}

