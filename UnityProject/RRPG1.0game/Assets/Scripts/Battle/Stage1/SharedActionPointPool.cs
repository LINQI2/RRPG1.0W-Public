using System;

/// <summary>
/// Battle V0.1 全队共享行动力（纯逻辑，不依赖 UnityEngine）
///
/// 职责：管理一套全队共享的行动力池。
/// 取代旧模型：BattleUnit 个人行动力 + TPSystem 公共 TP（二者在 Battle V0.1 不再具有玩法意义）。
///
/// 设计要点（对应 Battle GameSpec V0.1 相关规则）：
///   - 行动力上限必须配置化，不得硬编码（上限由构造参数/SetMax 注入）。
///   - 变化时通过 Changed 事件对外广播（普通 C# 事件，不依赖 EventBus，
///     以便本类保持在独立运行时程序集中，绕开团结引擎 asmdef→预定义程序集引用限制）。
///
/// 不负责：回合恢复时机（Stage 2）、出牌校验流程（后续 Stage）。
/// </summary>
public class SharedActionPointPool
{
    /// <summary>行动力变化事件（参数：当前值, 上限）</summary>
    public event System.Action<int, int> Changed;

    /// <summary>行动力上限（配置化）</summary>
    public int Max { get; private set; }

    /// <summary>当前行动力</summary>
    public int Current { get; private set; }

    /// <summary>
    /// 创建共享行动力池
    /// </summary>
    /// <param name="max">行动力上限</param>
    public SharedActionPointPool(int max)
    {
        Max = Math.Max(0, max);
        Current = Max;
        Publish();
    }

    /// <summary>是否足够支付指定费用</summary>
    public bool CanAfford(int cost)
    {
        return cost >= 0 && Current >= cost;
    }

    /// <summary>
    /// 消耗行动力
    /// </summary>
    /// <returns>true=消耗成功，false=行动力不足</returns>
    public bool Spend(int cost)
    {
        if (!CanAfford(cost)) return false;

        Current -= cost;
        Publish();
        return true;
    }

    /// <summary>
    /// 恢复到上限（回合开始时调用；具体时机由 Stage 2 回合生命周期接入）
    /// </summary>
    public void RestoreToMax()
    {
        Current = Max;
        Publish();
    }

    /// <summary>
    /// 设置行动力上限（配置化入口）
    /// </summary>
    public void SetMax(int newMax)
    {
        Max = Math.Max(0, newMax);
        if (Current > Max) Current = Max;
        Publish();
    }

    private void Publish()
    {
        Changed?.Invoke(Current, Max);
    }
}
