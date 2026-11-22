using System;
using System.Collections.Generic;

/// <summary>
/// Node 定义（只读数据，不承载运行进度）。
///
/// WP-1 语义（FDS v2 制作人修订）：
/// - 0 successor 只表示 progression terminal，绝不自动等于成功完成远征；
/// - RouteCompleted / SafeReturn 只能由显式 <see cref="SuccessfulRouteTerminal"/> 的成功 outcome 产生；
/// - 普通、成本可预知的 Node 的 <see cref="NodeElapsedDays"/> 与 <see cref="ExtraSupplyCost"/>
///   在进入前即可确定：Resolver 必须在真正启动 Battle / Event 内容前完成 Supply affordability validation。
/// - NodeElapsedDays 直接由定义持有（Test / Not Frozen），不引入无当前消费者的 Policy / Strategy 抽象。
/// </summary>
public class ExpeditionNodeData
{
    /// <summary>会话内唯一 NodeId。</summary>
    public string NodeId { get; }

    /// <summary>内容种类（分派 / 展示用）。</summary>
    public ExpeditionNodeKind NodeKind { get; }

    /// <summary>合法后继 NodeId 列表（只读）。0 后继 = progression terminal。</summary>
    public IReadOnlyList<string> Connections { get; }

    /// <summary>本 Node 推进耗时（integer Village Days，>= 0）。Test / Not Frozen。</summary>
    public long DurationMinutes { get; }
    /// <summary>Legacy whole-day view; never used for native settlement.</summary>
    public int NodeElapsedDays => checked((int)(DurationMinutes / 1440));

    /// <summary>Explicit Extra Supply Cost（默认 0；仅特殊内容提供，普通 Node 禁止 Time + fixed Supply 双扣）。</summary>
    public int ExtraSupplyCost { get; }

    /// <summary>
    /// 是否显式成功路线终点。只有成功 outcome 且本标记为 true 时，Progression 才置 RouteCompleted。
    /// 不得用「Connections.Count == 0」推导本标记。
    /// </summary>
    public bool SuccessfulRouteTerminal { get; }

    public ExpeditionNodeData(
        string nodeId,
        ExpeditionNodeKind nodeKind,
        IEnumerable<string> connections = null,
        int nodeElapsedDays = 0,
        int extraSupplyCost = 0,
        bool successfulRouteTerminal = false,
        long? durationMinutes = null)
    {
        if (string.IsNullOrEmpty(nodeId))
            throw new ArgumentException("NodeId 不能为空", nameof(nodeId));
        if (nodeElapsedDays < 0)
            throw new ArgumentOutOfRangeException(nameof(nodeElapsedDays), "NodeElapsedDays 必须 >= 0");
        if (extraSupplyCost < 0)
            throw new ArgumentOutOfRangeException(nameof(extraSupplyCost), "ExtraSupplyCost 必须 >= 0");

        NodeId = nodeId;
        NodeKind = nodeKind;
        DurationMinutes = durationMinutes ?? checked((long)nodeElapsedDays * 1440);
        if (DurationMinutes < 0) throw new ArgumentOutOfRangeException(nameof(durationMinutes));
        ExtraSupplyCost = extraSupplyCost;
        SuccessfulRouteTerminal = successfulRouteTerminal;

        var list = new List<string>();
        if (connections != null)
        {
            foreach (var id in connections)
            {
                if (!string.IsNullOrEmpty(id) && !list.Contains(id))
                    list.Add(id);
            }
        }
        Connections = list.ToArray();
    }
}
