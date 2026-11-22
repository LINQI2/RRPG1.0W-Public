using System;

/// <summary>
/// Expedition 会话内 Supply / Carry 消费侧状态（FP-V0.1 抽象；不建第二套 Village Food Authority）。
///
/// - InitialCarryUnits：出发时转入的 Carry（真实转移属 WP-3 Departure seam；本层只消费会话内数值）；
/// - BaseSupplyConsumption = PartySize × NodeElapsedDays × PerPersonDailyConsumptionRate；
/// - TotalSupplyCost = BaseSupplyConsumption + Explicit Extra Supply Cost；
/// - CurrentCarryUnits 只经 <see cref="TryConsume"/> 扣减（exactly-once 由 Resolver 保证）；
/// - Return / Retreat 不清空 Carry（返还 hook 归 WP-3 FDS）。
/// 所有数值 Test / Not Frozen。
/// </summary>
public class ExpeditionSupplyState
{
    /// <summary>本次远征参与者数（>= 1）。</summary>
    public int PartySize { get; }

    /// <summary>每人每天基础消耗率（>= 0）。</summary>
    public int PerPersonDailyConsumptionRate { get; }

    /// <summary>出发 Carry（>= 0，只读记录）。</summary>
    public int InitialCarryUnits { get; }

    /// <summary>当前剩余 Carry。</summary>
    public int CurrentCarryUnits { get; private set; }

    public ExpeditionSupplyState(int partySize, int initialCarryUnits, int perPersonDailyConsumptionRate)
    {
        if (partySize < 1)
            throw new ArgumentOutOfRangeException(nameof(partySize), "PartySize 必须 >= 1");
        if (initialCarryUnits < 0)
            throw new ArgumentOutOfRangeException(nameof(initialCarryUnits), "初始 Carry 必须 >= 0");
        if (perPersonDailyConsumptionRate < 0)
            throw new ArgumentOutOfRangeException(nameof(perPersonDailyConsumptionRate), "每日消耗率必须 >= 0");

        PartySize = partySize;
        InitialCarryUnits = initialCarryUnits;
        CurrentCarryUnits = initialCarryUnits;
        PerPersonDailyConsumptionRate = perPersonDailyConsumptionRate;
    }

    /// <summary>
    /// 能否应用一个 Supply 增量（delta 正负均可）。
    /// 语义：结果必须落在 `[0, int.MaxValue]`；不满足 → false 且不改变状态（不夹取、不溢出）。
    /// </summary>
    public bool CanApplyDelta(int delta)
    {
        int ignored;
        return TryComputeDelta(delta, out ignored);
    }

    /// <summary>
    /// **WP-Event-1D §21 正式 Supply 增量入口**：原子应用 delta（正 = 增加，负 = 扣减）。
    /// 结果 &lt; 0 或溢出 → false 且**不改变状态**；Supply 仍然只有这一个 Authority。
    /// </summary>
    public bool TryApplyDelta(int delta)
    {
        int after;
        if (!TryComputeDelta(delta, out after)) return false;

        CurrentCarryUnits = after;
        return true;
    }

    private bool TryComputeDelta(int delta, out int after)
    {
        after = 0;
        long result = (long)CurrentCarryUnits + delta;
        if (result < 0) return false;
        if (result > int.MaxValue) return false;

        after = (int)result;
        return true;
    }

    /// <summary>
    /// 计算本 Node 的预计总成本（Enter 前 affordability validation 与 Resolve 结算使用同一公式，杜绝双扣差异）。
    /// </summary>
    public int ComputeTotalCost(int nodeElapsedDays, int extraSupplyCost)
    {
        if (nodeElapsedDays < 0)
            throw new ArgumentOutOfRangeException(nameof(nodeElapsedDays), "NodeElapsedDays 必须 >= 0");
        if (extraSupplyCost < 0)
            throw new ArgumentOutOfRangeException(nameof(extraSupplyCost), "ExtraSupplyCost 必须 >= 0");
        if (!TryComputeMinuteCost(0, checked((long)nodeElapsedDays * 1440), extraSupplyCost,
            out int cost, out _)) throw new OverflowException("Supply cost overflow");
        return cost;
    }

    /// <summary>Whole-expedition ceiling; each node consumes only the new base increment.</summary>
    public bool TryComputeMinuteCost(long priorMinutes, long additionalMinutes, int extraSupplyCost,
        out int totalCost, out int baseIncrement)
    {
        totalCost = 0; baseIncrement = 0;
        if (priorMinutes < 0 || additionalMinutes < 0 || extraSupplyCost < 0) return false;
        try
        {
            long afterMinutes = checked(priorMinutes + additionalMinutes);
            long factor = checked((long)PartySize * PerPersonDailyConsumptionRate);
            long before = checked(factor * priorMinutes);
            long after = checked(factor * afterMinutes);
            long beforeRequired = before / 1440 + (before % 1440 == 0 ? 0 : 1);
            long afterRequired = after / 1440 + (after % 1440 == 0 ? 0 : 1);
            long increment = checked(afterRequired - beforeRequired);
            long cost = checked(increment + extraSupplyCost);
            if (increment > int.MaxValue || cost > int.MaxValue) return false;
            baseIncrement = (int)increment; totalCost = (int)cost; return true;
        }
        catch (OverflowException) { return false; }
    }

    /// <summary>能否承担总成本（CurrentCarry &gt;= totalCost）。</summary>
    public bool CanAfford(int totalCost)
    {
        return totalCost >= 0 && CurrentCarryUnits >= totalCost;
    }

    /// <summary>
    /// 原子扣减；不足或非法成本返回 false 且不改变状态。
    /// 只应在 Resolver 的 exactly-once resolution 中调用（Enter 只校验、不扣减）。
    /// </summary>
    public bool TryConsume(int totalCost)
    {
        if (totalCost < 0 || !CanAfford(totalCost))
            return false;
        CurrentCarryUnits -= totalCost;
        return true;
    }
}
