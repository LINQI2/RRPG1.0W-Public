/// <summary>
/// Node 内容种类（FP-V0.1 最小集合；Stage 2 由 LegacyMapGraphAdapter 把 Legacy NodeType 映射到本枚举，
/// 二者不混为单一 Authority）。
/// 本种类只用于内容分派 / 展示元数据；推进规则由 Resolver 统一执行。
/// </summary>
public enum ExpeditionNodeKind
{
    /// <summary>普通战斗内容（Legacy: Battle）。</summary>
    Battle,

    /// <summary>精英战斗内容（Legacy: Elite）。</summary>
    Elite,

    /// <summary>Boss 内容（Legacy: Boss；是否成功路线终点由 NodeData.SuccessfulRouteTerminal 显式决定）。</summary>
    Boss,

    /// <summary>事件内容（Legacy: Event；正式内容池未冻结）。</summary>
    Event,

    /// <summary>剧情内容（正式内容池未冻结）。</summary>
    Story,

    /// <summary>发现内容（正式内容池未冻结）。</summary>
    Discovery,

    /// <summary>休息内容（正式内容生态未冻结）。</summary>
    Rest,

    /// <summary>路线终点内容（仅当显式 SuccessfulRouteTerminal 成功 outcome 才产生 RouteCompleted）。</summary>
    Terminal
}
