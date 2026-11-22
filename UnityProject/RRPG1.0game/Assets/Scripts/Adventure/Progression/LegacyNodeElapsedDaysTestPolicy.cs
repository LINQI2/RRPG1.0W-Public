/// <summary>
/// LegacyNodeElapsedDaysTestPolicy —— V4 Gate 2 的最小 **Test / Not Frozen** 节点耗时策略。
///
/// 目的（Village-GameSpec §6.3 / Decision D）：让真实产品远征产生非零 elapsed，
/// 从而驱动 Supply 消耗与 Return Catch-up 让 Village 真正经历这些天。
///
/// 边界：
///   - 只提供「NodeId → NodeElapsedDays」的配置函数，供既有 seam
///     `AdventureManager.StartProgression(..., nodeElapsedDaysFor)` → `LegacyMapGraphAdapter` 使用；
///   - **不改** `ExpeditionProgressionResolver` / `ExpeditionNodeData` / `LegacyMapGraphAdapter` 的核心规则；
///   - **不建立第二套 ExpeditionTime**；不直接推进 VillageTime；
///   - 数值全部 Test / Not Frozen（Battle / Elite / Boss = 1 Test Day，其它 Legacy 节点 = 0）。
///
/// 实现注意：本 lookup 是**惰性**的 —— 它在 `BuildGraph` 逐 Node 求值时读取 `MapGenerator.Nodes`，
/// 因此可在 `MapGen.Generate(...)` 之前构建（产品调用点即如此使用）。
/// </summary>
public static class LegacyNodeElapsedDaysTestPolicy
{
    /// <summary>【Test / Not Frozen】真实战斗类节点消耗的 Village Days。</summary>
    public const int BattleNodeElapsedDays = 1;

    /// <summary>
    /// 【Test / Not Frozen】按 Legacy 节点类型解析耗时：Battle / Elite / Boss = 1，其它 = 0。
    /// </summary>
    public static int Resolve(NodeType type)
    {
        switch (type)
        {
            case NodeType.Battle:
            case NodeType.Elite:
            case NodeType.Boss:
                return BattleNodeElapsedDays;
            default:
                return 0;
        }
    }

    /// <summary>
    /// 构建 `nodeElapsedDaysFor`（惰性读取 generator.Nodes；未知名 / null 安全返回 0）。
    /// </summary>
    public static System.Func<string, int> BuildLookup(MapGenerator generator)
    {
        return nodeId =>
        {
            if (generator == null || generator.Nodes == null || string.IsNullOrEmpty(nodeId)) return 0;
            var node = generator.Nodes.Find(n => n != null && n.NodeId == nodeId);
            return node != null ? Resolve(node.Type) : 0;
        };
    }
}
