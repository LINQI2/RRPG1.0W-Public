/// <summary>
/// 状态运行时数据
///
/// 负责：追踪战斗中的状态动态数据
/// 不负责：状态静态配置（名称、效果）
///
/// 每个 BattleUnit 的 StatusList 中存放 StatusRuntime 实例
///
/// 与 StatusData 的关系：
/// StatusData（配置层）：定义状态的静态属性
/// StatusRuntime（运行时层）：追踪战斗中的动态数据
/// </summary>
public class StatusRuntime
{
    /// <summary>
    /// 引用 StatusData.StatusId
    /// </summary>
    public string StatusId;

    /// <summary>
    /// 剩余回合
    /// 按作用方回合递减
    /// </summary>
    public int RemainingTurns;

    /// <summary>
    /// 叠加层数
    /// 同类状态可叠加多层
    /// </summary>
    public int StackCount;

    /// <summary>
    /// 来源单位ID
    /// 是谁施加了此状态
    /// </summary>
    public string SourceUnitId;

    /// <summary>
    /// 创建状态运行时实例
    /// </summary>
    /// <param name="statusId">引用 StatusData.StatusId</param>
    /// <param name="duration">持续回合数</param>
    /// <param name="sourceUnitId">来源单位ID</param>
    public StatusRuntime(string statusId, int duration, string sourceUnitId)
    {
        StatusId = statusId;
        RemainingTurns = duration;
        StackCount = 1;
        SourceUnitId = sourceUnitId;
    }
}