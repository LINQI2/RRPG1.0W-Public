namespace RRPG.Event
{
    /// <summary>
    /// **IEventEffectGateway（外部效果网关契约）** —— WP-Event-1C §9 / §10 / §20 / §21。
    ///
    /// 为什么需要它：Event Core **不拥有** Village Resource / Rest / Expedition Supply / Expedition Time /
    /// Battle 这些 Authority，也**禁止**直接引用 Village.Runtime / AdventureManager /
    /// ExpeditionProgressionResolver / Battle / Encounter。因此外部效果只能以「契约」表达：
    ///
    ///   1. **只读投影输入**：`TryGetVillageResource` / `TryGetExpeditionSupply`
    ///      —— 让 Event Core 自己维护 projected state 并做完整 preflight（§10）；
    ///   2. **纯查询**：`CanGrantRest` / `CanAdvanceExpeditionTime` / `CanStartBattle`
    ///      —— 必须无副作用（preflight 阶段调用）；
    ///   3. **正式提交**：`Apply*` / `GrantRest` / `AdvanceExpeditionTime` / `StartBattle`
    ///      —— 只在 Commit 阶段调用一次，成功返回 true；失败返回 false + 可操作原因。
    ///
    /// 硬约束（实现方必须保证）：
    ///   - `Can*` 不得 mutation；
    ///   - `Apply*` 只做**已经 preflight 通过**的那一个效果，不做自己的规则推理；
    ///   - 任何失败都必须如实返回 false（不得静默成功）。
    ///
    /// 真实产品 Adapter 属 **1D**：
    ///   - `VillageResourceDelta` → `VillageResourceState.AddFood / TryConsumeFood` 等；
    ///   - `GrantPersonRest` → 正式 Rest Authority / 月末 RestUntil（**不**是 1C 直接调旧 SocialResponseLog）；
    ///   - `SupplyDelta` → 需先由 1D 决定是否给 `ExpeditionSupplyState` 增加正式 Add 入口；
    ///   - `AdvanceExpeditionTime` → 需先与 `ExpeditionProgressionState.ExpeditionElapsedDays` 的
    ///     exactly-once Node 结算正式组合；
    ///   - `StartBattle` → Event → Encounter → Battle → 回到 Node resolution 的产品生命周期。
    /// </summary>
    public interface IEventEffectGateway
    {
        // ---------------- 只读投影输入 ----------------

        /// <summary>读取当前村庄资源（TargetKey = Ingredients / Food）。</summary>
        bool TryGetVillageResource(string resourceKey, out int amount);

        /// <summary>读取当前远征补给（Carry）。</summary>
        bool TryGetExpeditionSupply(out int supply);

        // ---------------- 纯查询（preflight；不得 mutation） ----------------

        bool CanGrantRest(string personId, out string reason);

        bool CanAdvanceExpeditionTime(int days, out string reason);

        bool CanStartBattle(string encounterPoolId, out string reason);

        // ---------------- 正式提交（只在 Commit 阶段调用） ----------------

        bool ApplyVillageResourceDelta(string resourceKey, int delta, out string reason);

        bool ApplySupplyDelta(int delta, out string reason);

        bool GrantRest(string personId, out string reason);

        bool AdvanceExpeditionTime(int days, out string reason);

        bool StartBattle(string encounterPoolId, out string reason);
    }
}
