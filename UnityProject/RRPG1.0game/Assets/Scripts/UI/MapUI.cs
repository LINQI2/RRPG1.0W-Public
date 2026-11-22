using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// 地图界面
/// 拖拽通过 ScrollRect 处理，Start() 自动在 MapCanvas 加透明 Image 接收事件
/// </summary>
public class MapUI : MonoBehaviour
{
    [Header("预制体")]
    public GameObject nodePrefab;

    [Header("地图布局")]
    public Vector2 mapCenter = new Vector2(0, 0);
    public float nodeSpacingX = 200f;
    public float nodeSpacingY = 150f;
    public Vector2 nodeSize = new Vector2(80, 80);

    [Header("Legacy 预览参数（正式出征的兼容图配置见 ExpeditionUI）")]
    public int rows = 4;
    public int columns = 3;

    [Header("连线样式")]
    public Color lineColor = Color.white;
    public float lineWidth = 3f;

    [Header("状态颜色")]
    public Color reachedColor = Color.white;
    public Color visitedColor = Color.gray;
    public Color lockedColor = new Color(0.3f, 0.3f, 0.3f);
    public Color currentColor = Color.yellow;

    [Header("地图 Canvas 和容器")]
    public GameObject canvasRoot;
    public RectTransform mapContainer;

    [Header("引用")]
    public Text infoText;

    [Header("Run 领悟显示（Stage 6B：当前领悟 / 阈值；Inspector 可调，不新建第二套 HUD）")]
    [Tooltip("显示 Run 级共享领悟进度（如 领悟 30 / 30）；由 ExpeditionUI 在地图显示/刷新时调用 UpdateInsightDisplay")]
    public Text insightText;

    private MapGenerator _generator;
    private Dictionary<string, GameObject> _nodeObjects = new Dictionary<string, GameObject>();
    private List<GameObject> _lines = new List<GameObject>();
    private System.Action<string> _onNodeClick;

    // WP-1 Stage2：Progression 派生视图镜像（MapUI 只展示；规则真相在 Resolver / Progression State）。
    private readonly HashSet<string> _viewAvailable = new HashSet<string>();
    private readonly HashSet<string> _viewCompleted = new HashSet<string>();
    private string _viewCurrentNodeId;
    private bool _hasProgressionView;

    void Start()
    {
        if (canvasRoot != null)
            canvasRoot.SetActive(false);
        else
            gameObject.SetActive(false);
    }

    /// <summary>
    /// 编辑器 OnValidate：修改 Inspector 参数后即时重建预览
    /// </summary>
    void OnValidate()
    {
        rows = Mathf.Max(2, rows);
        columns = Mathf.Max(1, columns);
    }

    /// <summary>
    /// 展示 Map（WP-1 Stage2：MapUI 只负责展示与输入；点击只上报 NodeId 意图）。
    /// Progression 已在 StartProgression 阶段 Generate 一次并导入 ExpeditionGraph；
    /// 本方法不再二次 Generate（否则 Progression Graph 与视图布局错位）。
    /// 防御：仅当外部调用方未预先 Generate 时才补一次（产品路径不会触发）。
    /// </summary>
    public void ShowMap(MapGenerator generator, System.Action<string> onNodeClick)
    {
        _generator = generator;
        _onNodeClick = onNodeClick;
        ClearMap();

        // 确保 MapCanvas 上有 Image 接收 ScrollRect 事件
        EnsureCanvasHasImage();

        if (_generator.Nodes == null || _generator.Nodes.Count == 0)
            _generator.Generate(rows, columns);

        CreateNodes();
        CreateLines();
        UpdateDisplay();
        ResizeContainer();

        if (canvasRoot != null) canvasRoot.SetActive(true);
        else gameObject.SetActive(true);
        Log("选择下一个节点");
    }

    /// <summary>
    /// 确保 MapCanvas 上的 Image 铺满全屏（ScrollRect 需要 Image 接收事件）
    /// Image 需要你手动在 MapCanvas 上添加：
    ///   1. 选中 MapCanvas → Add Component → Image
    ///   2. Color Alpha = 0（透明），Raycast Target = ✅
    ///   3. RectTransform 锚点拉满全屏（或由代码自动设为全屏）
    /// </summary>
    private void EnsureCanvasHasImage()
    {
        if (canvasRoot == null) return;
        var img = canvasRoot.GetComponent<Image>();
        if (img == null)
        {
            return;
        }
        // 拉伸 Image 到整个 Canvas
        var rt = img.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.sizeDelta = Vector2.zero;
        rt.anchoredPosition = Vector2.zero;
    }

    void ClearMap()
    {
        foreach (var kv in _nodeObjects) { if (kv.Value != null) Destroy(kv.Value); }
        _nodeObjects.Clear();
        foreach (var line in _lines) { if (line != null) Destroy(line); }
        _lines.Clear();
    }

    void CreateNodes()
    {
        // 统计节点数量用于调试
        int battleCount = 0, eliteCount = 0, bossCount = 0;
        int eventCount = 0, shopCount = 0, recruitCount = 0;

        foreach (var node in _generator.Nodes)
        {
            // 用固定间距计算位置，不依赖 rows/columns
            float x = mapCenter.x + (node.PosX - 0.5f) * (3 * nodeSpacingX);
            float y = mapCenter.y + (node.PosY - 0.5f) * (4 * nodeSpacingY);

            // 统计
            switch (node.Type)
            {
                case NodeType.Battle: battleCount++; break;
                case NodeType.Elite: eliteCount++; break;
                case NodeType.Boss: bossCount++; break;
                case NodeType.Event: eventCount++; break;
                case NodeType.Shop: shopCount++; break;
                case NodeType.Recruit: recruitCount++; break;
            }
            var go = Instantiate(nodePrefab, mapContainer != null ? mapContainer : transform, false);
            go.name = node.NodeId;
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = nodeSize;
            rt.anchoredPosition = new Vector2(x, y);
            var txt = go.GetComponentInChildren<Text>();
            if (txt != null) txt.text = GetNodeTypeText(node);
            var btn = go.GetComponent<Button>();
            if (btn != null) { string cid = node.NodeId; btn.onClick.AddListener(() => OnNodeClicked(cid)); }
            _nodeObjects[node.NodeId] = go;
        }
    }

    void CreateLines()
    {
        foreach (var node in _generator.Nodes)
        {
            if (!_nodeObjects.ContainsKey(node.NodeId)) continue;
            var fromRt = _nodeObjects[node.NodeId].GetComponent<RectTransform>();
            Vector2 fromPos = fromRt.anchoredPosition;
            foreach (var connId in node.Connections)
            {
                if (!_nodeObjects.ContainsKey(connId)) continue;
                var toRt = _nodeObjects[connId].GetComponent<RectTransform>();
                Vector2 toPos = toRt.anchoredPosition;
                var line = new GameObject("Line_" + node.NodeId + "_to_" + connId);
                line.transform.SetParent(mapContainer != null ? mapContainer : transform, false);
                var img = line.AddComponent<Image>();
                img.color = lineColor;
                var rt = line.GetComponent<RectTransform>();
                Vector2 dir = toPos - fromPos;
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(dir.magnitude, lineWidth);
                rt.anchoredPosition = (fromPos + toPos) / 2f;
                rt.localEulerAngles = new Vector3(0, 0, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
                rt.SetAsFirstSibling();
                _lines.Add(line);
            }
        }
    }

    public void UpdateDisplay()
    {
        if (_generator == null) return;
        foreach (var node in _generator.Nodes)
        {
            if (!_nodeObjects.ContainsKey(node.NodeId)) continue;
            var img = _nodeObjects[node.NodeId].GetComponent<Image>();
            if (img == null) continue;
            if (node.Visited) img.color = visitedColor;
            else if (node.Reachable) img.color = reachedColor;
            else img.color = lockedColor;
        }
        if (_generator.CurrentNode != null && _nodeObjects.ContainsKey(_generator.CurrentNode.NodeId))
        {
            var img = _nodeObjects[_generator.CurrentNode.NodeId].GetComponent<Image>();
            if (img != null) img.color = currentColor;
        }

        // WP-1 Stage2：存在 Progression 视图提供方时，以 Progression 派生镜像重绘覆盖
        //（可用=白、已进入/已完成=灰、当前=黄；Legacy flags 只作无视图提供方时的兼容回退）。
        if (!_hasProgressionView) return;
        foreach (var node in _generator.Nodes)
        {
            if (!_nodeObjects.ContainsKey(node.NodeId)) continue;
            var img = _nodeObjects[node.NodeId].GetComponent<Image>();
            if (img == null) continue;
            if (_viewCurrentNodeId == node.NodeId)
                img.color = currentColor;
            else if (_viewCompleted.Contains(node.NodeId))
                img.color = visitedColor;
            else if (_viewAvailable.Contains(node.NodeId))
                img.color = reachedColor;
            else
                img.color = lockedColor;
        }
    }

    /// <summary>
    /// WP-1 Stage2：由 ExpeditionUI 提供 Progression 派生的视图镜像（可用 / 完成 / 当前）。
    /// MapUI 只读展示；本方法不产生任何规则推进。
    /// </summary>
    public void SetProgressionView(IEnumerable<string> availableNodeIds, string currentNodeId,
        IEnumerable<string> completedNodeIds)
    {
        _hasProgressionView = true;
        _viewCurrentNodeId = currentNodeId;

        _viewAvailable.Clear();
        if (availableNodeIds != null)
            foreach (var id in availableNodeIds)
                if (!string.IsNullOrEmpty(id)) _viewAvailable.Add(id);

        _viewCompleted.Clear();
        if (completedNodeIds != null)
            foreach (var id in completedNodeIds)
                if (!string.IsNullOrEmpty(id)) _viewCompleted.Add(id);
    }

    string GetNodeTypeText(MapNode node)
    {
        switch (node.Type)
        {
            case NodeType.Battle: return "战";
            case NodeType.Elite: return "精";
            case NodeType.Boss: return "王";
            case NodeType.Event: return "?";
            case NodeType.Shop: return "店";
            case NodeType.Rest: return "休";
            case NodeType.Recruit: return "招";
            default: return "?";
        }
    }

    void OnNodeClicked(string nodeId)
    {
        var node = _generator?.Nodes.Find(n => n.NodeId == nodeId);
        if (node == null) return;

        // WP-1 Stage2：只上报 Node 点击意图（NodeId）。
        // 合法性与 affordability 校验由 ExpeditionUI → AdventureManager → Resolver 执行；
        // MapUI 不再用 Reachable/Visited（Legacy flags 不是 Progression Authority）作进入门槛。
        _onNodeClick?.Invoke(node.NodeId);
    }

    /// <summary>
    /// 刷新显示（不重新生成地图，保留已访问状态）
    /// 供 OnReturnFromBattle 使用
    /// </summary>
    public void RefreshDisplayOnly()
    {
        if (_generator == null) return;
        ClearMap();
        CreateNodes();
        CreateLines();
        UpdateDisplay();
        ResizeContainer();

        if (canvasRoot != null) canvasRoot.SetActive(true);
        else gameObject.SetActive(true);
    }

    /// <summary>
    /// Stage 6B：更新 Run 领悟显示（当前 / 阈值）。
    /// 由 ExpeditionUI 在地图显示/刷新时调用；数值直接读取 RunState，不复制第二份状态。
    /// </summary>
    public void UpdateInsightDisplay(int current, int threshold)
    {
        if (insightText == null) return;
        insightText.text = "领悟 " + current + " / " + threshold;
    }

    public void HideMap()
    {
        if (canvasRoot != null) canvasRoot.SetActive(false);
        else gameObject.SetActive(false);
    }

    /// <summary>容器最小尺寸（确保 ScrollRect 始终可以滚动）</summary>
    private Vector2 _minContainerSize = new Vector2(1200, 1200);

    void ResizeContainer()
    {
        if (mapContainer == null) return;
        float minX = float.MaxValue, maxX = float.MinValue;
        float minY = float.MaxValue, maxY = float.MinValue;
        foreach (var kv in _nodeObjects)
        {
            if (kv.Value == null) continue;
            var rt = kv.Value.GetComponent<RectTransform>();
            if (rt == null) continue;
            Vector2 pos = rt.anchoredPosition;
            if (pos.x < minX) minX = pos.x;
            if (pos.x > maxX) maxX = pos.x;
            if (pos.y < minY) minY = pos.y;
            if (pos.y > maxY) maxY = pos.y;
        }
        float padding = 100f;
        float width = (maxX - minX) + nodeSize.x + padding * 2;
        float height = (maxY - minY) + nodeSize.y + padding * 2;

        // 确保容器始终大于屏幕，ScrollRect 才会启用滚动
        width = Mathf.Max(width, _minContainerSize.x);
        height = Mathf.Max(height, _minContainerSize.y);

        mapContainer.sizeDelta = new Vector2(width, height);
    }

    void Log(string msg) { if (infoText != null) infoText.text = msg; }
}
