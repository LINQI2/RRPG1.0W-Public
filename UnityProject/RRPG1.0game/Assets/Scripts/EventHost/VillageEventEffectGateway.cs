using System;
using RRPG.Event;

/// <summary>
/// **VillageEventEffectGateway（Village 事件效果网关）** —— WP-Event-1D Gate D1 §3/§4。
///
/// 职责：把 Event Core 已经 preflight 通过的效果**如实**落到 Village 既有 Authority 上。
/// 它不做规则推理、不做二次校验、不拥有状态；失败必须如实返回 false + 可操作原因。
///
/// 允许的效果（Village 域）：
///   - `VillageResourceDelta` → `VillageResourceState.AddIngredients / AddFood`（正）/ `TryConsume*`（负）；
///   - `GrantPersonRest` → `VillageRuntimeBridge.GrantPersonRestUntilMonthEnd`（D0 正式 Rest Authority）；
///   - `SetFlag` / `ClearFlag` → **由 Event Core 自己拥有**，本网关不参与。
///
/// **Expedition-only 效果一律 Fail Closed**（§3）：`SupplyDelta` / `AdvanceExpeditionTime` / `StartBattle`
/// 返回 false + 明确原因。Village 网关**不得**跨域调用 Expedition / Battle Authority ——
/// 即使 Runtime 配置损坏（Compiler 域校验 DAT043 已禁止这种组合）也不会越域操作。
///
/// **不写 Legacy**：`GrantPersonRest` 只走新 Rest Authority，**不**再写 `VillageSocialResponseLog.Rests`。
/// </summary>
public sealed class VillageEventEffectGateway : IEventEffectGateway
{
    public const string ResourceIngredients = "Ingredients";
    public const string ResourceFood = "Food";

    private const string CrossDomainRefusal =
        "Village 事件网关不拥有 Expedition Authority：该效果属于远征域（Fail Closed，不跨域操作）。";

    // ---------------- 只读投影输入 ----------------

    public bool TryGetVillageResource(string resourceKey, out int amount)
    {
        amount = 0;
        var resources = VillageRuntimeBridge.Resources;
        if (resources == null) return false;

        if (string.Equals(resourceKey, ResourceIngredients, StringComparison.Ordinal))
        {
            amount = resources.Ingredients;
            return true;
        }

        if (string.Equals(resourceKey, ResourceFood, StringComparison.Ordinal))
        {
            amount = resources.Food;
            return true;
        }

        return false;   // 未知资源键 → 明确不可用（不猜、不返回 0 冒充真实值）
    }

    /// <summary>Village 域不拥有远征补给 → 明确不可用（不得伪造 0 冒充真实补给）。</summary>
    public bool TryGetExpeditionSupply(out int supply)
    {
        supply = 0;
        return false;
    }

    // ---------------- 纯查询（preflight；不得 mutation） ----------------

    public bool CanGrantRest(string personId, out string reason)
    {
        if (string.IsNullOrEmpty(personId))
        {
            reason = "缺少人物 Id：无法授予恢复期。";
            return false;
        }

        if (!new VillageEventWorldView().PersonExists(personId))
        {
            reason = "人物 '" + personId + "' 不存在（长期人物真相源中没有该 Person）。";
            return false;
        }

        if (VillageRuntimeBridge.Time == null)
        {
            reason = "Village 时间未初始化：无法计算恢复期（本月最后一天）。";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    public bool CanAdvanceExpeditionTime(int days, out string reason)
    {
        reason = CrossDomainRefusal;
        return false;
    }

    public bool CanStartBattle(string encounterPoolId, out string reason)
    {
        reason = CrossDomainRefusal;
        return false;
    }

    // ---------------- 正式提交（只在 Commit 阶段调用） ----------------

    public bool ApplyVillageResourceDelta(string resourceKey, int delta, out string reason)
    {
        var resources = VillageRuntimeBridge.Resources;
        if (resources == null)
        {
            reason = "Village 资源 Authority 未初始化。";
            return false;
        }

        bool isIngredients = string.Equals(resourceKey, ResourceIngredients, StringComparison.Ordinal);
        bool isFood = string.Equals(resourceKey, ResourceFood, StringComparison.Ordinal);
        if (!isIngredients && !isFood)
        {
            reason = "未知的 Village 资源键 '" + resourceKey + "'（允许：Ingredients / Food）。";
            return false;
        }

        if (delta == 0)
        {
            reason = string.Empty;
            return true;   // 0 增量 = 合法 no-op
        }

        if (delta > 0)
        {
            if (isIngredients) resources.AddIngredients(delta);
            else resources.AddFood(delta);
            reason = string.Empty;
            return true;
        }

        int consume = -delta;
        bool consumed = isIngredients ? resources.TryConsumeIngredients(consume) : resources.TryConsumeFood(consume);
        if (!consumed)
        {
            reason = "Village 资源不足：无法扣除 " + consume + " 点 " + resourceKey + "。";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    public bool ApplySupplyDelta(int delta, out string reason)
    {
        reason = CrossDomainRefusal;
        return false;
    }

    /// <summary>
    /// 授予恢复期 —— 正式入口 = `VillageRuntimeBridge.GrantPersonRestUntilMonthEnd`
    /// （V0.1 规则：休整到当前 Village 月最后一天；含当天仍被 Rest Gate 拦截，下月 1 日恢复）。
    /// **不写** Legacy `VillageSocialResponseLog.Rests`。
    /// </summary>
    public bool GrantRest(string personId, out string reason)
    {
        if (!CanGrantRest(personId, out reason)) return false;

        if (!VillageRuntimeBridge.GrantPersonRestUntilMonthEnd(personId))
        {
            reason = "正式 Rest Authority 拒绝了本次授予（人物 '" + personId + "'；详见 VillagePersonRestState）。";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    public bool AdvanceExpeditionTime(int days, out string reason)
    {
        reason = CrossDomainRefusal;
        return false;
    }

    public bool StartBattle(string encounterPoolId, out string reason)
    {
        reason = CrossDomainRefusal;
        return false;
    }
}
