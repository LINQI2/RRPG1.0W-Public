using TMPro;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// VillageWorldLayer —— Village World Debug Visualization（Assembly-CSharp；运行时自托管根宿主）。
///
/// 定性：**Debug Visualization / Test / Not Frozen**。仅用于开发观察 Person Presence 派生、
/// Place 语义与 Project 完成痕迹的数据链是否正确；矩形 + 文字的占位视觉**不代表产品 World**，
/// 不用于证明 Spatial Presence / Place Memory / World 产品体验（真正的 Spatial / Visual Product Proof
/// 后置到独立 World Scene Prototype）。
///
/// 生命周期原则（制作人修正 2）：
///   - 视图可见性只服从权威 Village Runtime / Simulation（VillageRuntimeBridge.IsSimulationActive + Time），
///     与 TownCanvas / VillageHUD / CommitmentUI / Roster / SocialEventUI 等面板 active 状态完全解耦；
///   - 打开 DeckViewer / Expedition Preparation 等管理 UI 只可能把 Simulation gating 关掉（视图停绘），
///     不销毁、不重置任何 World 状态；返回时以同一 Runtime 状态直接恢复显示。
///   - MainMenu 为前台产品门禁时 Simulation 为 false（见 MainMenuUI），本视图随之不显示。
/// </summary>
public partial class VillageWorldLayer : MonoBehaviour
{
    private const float RefreshInterval = 0.25f;

    private Canvas _canvas;
    private RectTransform _dynamicRoot;
    private TMP_FontAsset _font;
    private Sprite _white;
    private bool _built;
    private string _signature = string.Empty;
    private float _nextRefreshTime;
    private bool _debugOverlayVisible = true;
    public bool DebugOverlayVisible => _debugOverlayVisible;
    /// <summary>Only hide legacy overlay children; retain queries, Onsite and simulation lifecycle.</summary>
    public void SetDebugOverlayVisible(bool visible)
    {
        _debugOverlayVisible = visible;
        if (_canvas == null) return;
        foreach (Transform child in _canvas.transform) child.gameObject.SetActive(visible);
    }

    /// <summary>World 画布当前是否可见（= Village Simulation 权威状态，非任何面板状态）。</summary>
    public bool CanvasVisible => _canvas != null && _canvas.enabled;

    /// <summary>最近一次刷新生成的摘要（供产品验证/诊断只读）。</summary>
    public string MarkerSummary { get; private set; } = string.Empty;

    private void Update()
    {
        if (_font == null)
        {
            _font = FindExistingTextFont();
            if (_font == null) return; // 场景 TMP_Text 就绪前重试，不做任何状态假设
        }

        if (!_built) BuildBase();

        bool simActive = VillageRuntimeBridge.IsSimulationActive && VillageRuntimeBridge.Time != null;
        if (_canvas != null && _canvas.enabled != simActive) _canvas.enabled = simActive;

        RefreshOnsite(simActive);
        if (!simActive) return;
        if (Time.unscaledTime < _nextRefreshTime) return;
        _nextRefreshTime = Time.unscaledTime + RefreshInterval;
        Refresh();
    }

    // ==================== 基座（只建一次；销毁/隐藏都不发生在这里） ====================

    private void BuildBase()
    {
        var canvasGo = new GameObject("VillageWorldCanvas");
        canvasGo.transform.SetParent(transform, false);
        _canvas = canvasGo.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = -100; // 世界画布在既有 UI（TownUI 等）之下

        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        var rootRt = canvasGo.GetComponent<RectTransform>();
        if (rootRt == null) rootRt = canvasGo.AddComponent<RectTransform>();
        rootRt.anchorMin = Vector2.zero;
        rootRt.anchorMax = Vector2.one;
        rootRt.offsetMin = Vector2.zero;
        rootRt.offsetMax = Vector2.zero;

        var ground = CreateImage("Ground", rootRt, new Color(0.07f, 0.1f, 0.09f, 1f), Vector2.zero, Vector2.zero);
        Stretch(ground.GetComponent<RectTransform>());
        ground.raycastTarget = false;

        AddText("WorldTitle", rootRt, new Vector2(-450f, 520f),
            "Village World Debug Visualization（Test / Not Frozen；不代表产品 World）", 22, Color.white, 1500f);

        var dyn = new GameObject("DynamicMarkers", typeof(RectTransform));
        dyn.transform.SetParent(rootRt, false);
        _dynamicRoot = dyn.GetComponent<RectTransform>();
        Stretch(_dynamicRoot);

        _built = true;
        SetDebugOverlayVisible(_debugOverlayVisible);
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    // ==================== 刷新：签名防抖 → 需要时全量重绘动态 Marker ====================

    private void Refresh()
    {
        var player = GetPlayerData();
        string next = player != null ? BuildSignature(player) : string.Empty;
        if (next == _signature) return;
        _signature = next;
        Rebuild(player);
    }

    private string BuildSignature(PlayerCollection player)
    {
        var parts = new List<string>();
        var registry = VillageRuntimeBridge.World;
        if (registry != null)
        {
            foreach (var p in registry.Places)
            {
                if (p == null) continue;
                parts.Add("P:" + p.PlaceId + ":" + (int)p.Kind + ":" + p.DisplayName);
            }
        }
        if (VillageRuntimeBridge.Projects != null)
        {
            foreach (var pr in VillageRuntimeBridge.Projects.ActiveProjects)
            {
                if (pr == null) continue;
                parts.Add("A:" + pr.ProjectId + ":" + pr.ProjectDefId);
            }
        }
        var views = VillageRuntimeBridge.GetPersonAvailability(player);
        if (views != null)
        {
            foreach (var v in views)
            {
                if (v == null) continue;
                var presence = VillageRuntimeBridge.GetPersonPresence(v.PersonId);
                parts.Add("N:" + v.PersonId + ":" + v.DisplayName + ":" + (int)presence.State + ":" + presence.PlaceId);
            }
        }
        return string.Join("|", parts);
    }

    private class Plot
    {
        public string PlaceId;
        public string Label;
        public Color Color;
    }

    private void Rebuild(PlayerCollection player)
    {
        if (_dynamicRoot == null) return;
        foreach (Transform child in _dynamicRoot) Destroy(child.gameObject);
        _dynamicRoot.DetachChildren();

        var plots = BuildPlots();
        var summary = new List<string>();

        for (int i = 0; i < plots.Count; i++)
        {
            var plot = plots[i];
            float x = ColumnX(i % 3);
            float y = 300f - (i / 3) * 185f;
            DrawPlot(plot, x, y);
            summary.Add(plot.PlaceId + "(" + plot.Label + ")");
        }

        var views = player != null ? VillageRuntimeBridge.GetPersonAvailability(player) : new List<VillagePersonView>();
        var awayLines = new List<string>();
        var slotCounts = new Dictionary<string, int>();
        if (views != null)
        {
            for (int i = 0; i < views.Count; i++)
            {
                var v = views[i];
                if (v == null) continue;
                var presence = VillageRuntimeBridge.GetPersonPresence(v.PersonId);
                if (presence.State == VillagePresenceState.Away)
                {
                    awayLines.Add(v.DisplayName + "（" + AwayLabel(v) + "）");
                    continue;
                }
                string target = !string.IsNullOrEmpty(presence.PlaceId) ? presence.PlaceId : "commons";
                int plotIndex = FindPlotIndex(plots, target);
                if (plotIndex < 0) plotIndex = plots.Count - 1; // 找不到时落到公共区
                int slot;
                slotCounts.TryGetValue(target, out slot);
                slotCounts[target] = slot + 1;
                float px = ColumnX(plotIndex % 3) - 175f + (slot % 3) * 175f;
                float py = 300f - (plotIndex / 3) * 185f - 55f - (slot / 3) * 30f;
                CreateTextLine("Person_" + v.PersonId, px, py, v.DisplayName + PresenceSuffix(presence.State), 16,
                    PresenceColor(presence.State), 380f);
                summary.Add(v.PersonId + "@" + target);
            }
        }

        float ay = -400f;
        if (awayLines.Count > 0)
        {
            AddText("AwayTitle", _dynamicRoot, new Vector2(-500f, ay), "不在村庄（远征中 / 暂不可用）", 18,
                new Color(0.75f, 0.75f, 0.75f, 1f), 900f);
            for (int i = 0; i < awayLines.Count; i++)
            {
                CreateTextLine("Away_" + i, -760f, ay - 40f - i * 26f, awayLines[i], 15,
                    new Color(0.6f, 0.6f, 0.6f, 1f), 900f);
            }
        }

        MarkerSummary = string.Join(" | ", summary);
    }

    private List<Plot> BuildPlots()
    {
        var result = new List<Plot>();
        var registry = VillageRuntimeBridge.World;
        if (registry != null)
        {
            foreach (var p in registry.Places)
            {
                if (p == null) continue;
                result.Add(new Plot
                {
                    PlaceId = p.PlaceId,
                    Label = p.Kind == VillagePlaceKind.ProjectSite ? p.DisplayName + "（已完成）" : p.DisplayName,
                    Color = p.Kind == VillagePlaceKind.ProjectSite ? CompletedColor : WorkplaceColor
                });
            }
        }
        if (VillageRuntimeBridge.Projects != null)
        {
            foreach (var pr in VillageRuntimeBridge.Projects.ActiveProjects)
            {
                if (pr == null) continue;
                string placeId = VillageWorldRegistry.BuildProjectSitePlaceId(pr.ProjectId);
                if (FindPlotIndex(result, placeId) >= 0) continue;
                var def = VillageRuntimeBridge.Projects.GetProjectDef(pr.ProjectDefId);
                string name = def != null && !string.IsNullOrEmpty(def.DisplayName) ? def.DisplayName : pr.ProjectDefId;
                result.Add(new Plot
                {
                    PlaceId = placeId,
                    Label = name + "（施工中）",
                    Color = ActiveProjectColor
                });
            }
        }
        result.Add(new Plot { PlaceId = "commons", Label = "村庄公共区（Commons）", Color = CommonsColor });
        return result;
    }

    private static int FindPlotIndex(List<Plot> plots, string placeId)
    {
        for (int i = 0; i < plots.Count; i++)
        {
            if (plots[i].PlaceId == placeId) return i;
        }
        return -1;
    }

    private void DrawPlot(Plot plot, float x, float y)
    {
        CreateImage("Plot_" + plot.PlaceId, _dynamicRoot, new Color(plot.Color.r, plot.Color.g, plot.Color.b, 0.32f),
            new Vector2(x, y), new Vector2(360f, 92f));
        AddText("PlotLabel_" + plot.PlaceId, _dynamicRoot, new Vector2(x, y), plot.Label, 16, Color.white, 340f);
    }

    // ==================== 图元 / 数据辅助 ====================

    private Image CreateImage(string name, Transform parent, Color color, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        go.AddComponent<CanvasRenderer>();
        var img = go.AddComponent<Image>();
        img.sprite = WhiteSprite();
        img.color = color;
        img.raycastTarget = false;
        var rt = go.GetComponent<RectTransform>();
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        return img;
    }

    private void AddText(string name, Transform parent, Vector2 pos, string content, int size, Color color, float width)
    {
        CreateTextCore(name, pos.x, pos.y, content, size, color, width, parent, true);
    }

    private TMP_Text CreateTextLine(string name, float x, float y, string content, int size, Color color, float width)
    {
        return CreateTextLine(name, x, y, content, size, color, width, _dynamicRoot);
    }

    private TMP_Text CreateTextLine(string name, float x, float y, string content, int size, Color color, float width, Transform parent)
    {
        return CreateTextCore(name, x, y, content, size, color, width, parent, false);
    }

    private TMP_Text CreateTextCore(string name, float x, float y, string content, int size, Color color, float width, Transform parent, bool centered)
    {
        if (_font == null || parent == null) return null;
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        go.AddComponent<CanvasRenderer>();
        var text = go.AddComponent<TextMeshProUGUI>();
        text.font = CurrentUIFontAuthority.Default;
        text.text = content;
        text.fontSize = size;
        text.color = color;
        text.alignment = centered ? TextAlignmentOptions.Center : TextAlignmentOptions.Left;
        text.enableWordWrapping = true;
        text.overflowMode = TextOverflowModes.Overflow;
        text.raycastTarget = false;
        var rt = go.GetComponent<RectTransform>();
        rt.pivot = centered ? new Vector2(0.5f, 0.5f) : new Vector2(0f, 0.5f);
        rt.anchoredPosition = new Vector2(x, y);
        rt.sizeDelta = new Vector2(width, size + 8f);
        return text;
    }

    private static float ColumnX(int col)
    {
        switch (col)
        {
            case 0: return -640f;
            case 1: return 0f;
            default: return 640f;
        }
    }

    private static string PresenceSuffix(VillagePresenceState state)
    {
        switch (state)
        {
            case VillagePresenceState.Workplace: return "（岗位工作中）";
            case VillagePresenceState.ProjectSite: return "（投入工程）";
            case VillagePresenceState.Free: return "（空闲 / 公共区）";
            default: return string.Empty;
        }
    }

    private static Color PresenceColor(VillagePresenceState state)
    {
        switch (state)
        {
            case VillagePresenceState.Workplace: return new Color(0.62f, 0.92f, 0.66f, 1f);
            case VillagePresenceState.ProjectSite: return new Color(1f, 0.78f, 0.4f, 1f);
            case VillagePresenceState.Free: return new Color(0.85f, 0.9f, 1f, 1f);
            default: return new Color(0.65f, 0.65f, 0.65f, 1f);
        }
    }

    private static Color WorkplaceColor => new Color(0.55f, 0.4f, 0.22f, 1f);
    private static Color CompletedColor => new Color(0.24f, 0.55f, 0.35f, 1f);
    private static Color ActiveProjectColor => new Color(0.78f, 0.48f, 0.15f, 1f);
    private static Color CommonsColor => new Color(0.28f, 0.4f, 0.55f, 1f);

    private static string AwayLabel(VillagePersonView v)
    {
        if (v.Availability == VillageAvailability.Expedition) return "远征中";
        return "暂不可用";
    }

    private static TMP_FontAsset FindExistingTextFont()
    {
        var any = Object.FindObjectOfType<TMP_Text>(true);
        return any != null ? any.font : null;
    }

    private Sprite WhiteSprite()
    {
        if (_white == null)
        {
            _white = Sprite.Create(Texture2D.whiteTexture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f));
        }
        return _white;
    }

    private static PlayerCollection GetPlayerData()
    {
        var town = Object.FindObjectOfType<TownUI>(true);
        if (town != null && town.PlayerData != null) return town.PlayerData;
        return SaveSystem.HasSave() ? SaveSystem.Load() : null;
    }
}
