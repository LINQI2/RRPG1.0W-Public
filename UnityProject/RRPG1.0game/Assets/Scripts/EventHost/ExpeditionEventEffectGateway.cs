using System;
using RRPG.Encounter;
using RRPG.Event;

/// <summary>
/// **ExpeditionEventEffectGateway（远征事件效果网关）** —— WP-Event-1D Gate D2 §7。
///
/// 职责：把 Event Core 已 preflight 通过的效果**如实**落到远征既有 Authority 上。
/// 不做规则推理、不拥有状态、失败必须如实返回 false + 可操作原因。
///
/// 允许的效果（Expedition 域）：
///   - `SupplyDelta` → `ExpeditionSupplyState.TryApplyDelta`（D0 已建立的唯一 Supply 增量入口；正负均可）；
///   - `AdvanceExpeditionTime` → **只累积到** `ActiveExpeditionEvent.PendingExtraElapsedDays`，
///     绝不直接改 `ExpeditionElapsedDays`（时间与补给必须在 Node Settlement 一次性结算，§8）；
///   - `StartBattle` → Preflight 阶段**预生成**一份合法 EnemyRoster（存于 Active Event State），
///     Commit 阶段把**同一份**对象提交为 Active Encounter（§11 / §12 / §13）；
///   - `SetFlag` / `ClearFlag` → 由 Event Core 自己拥有，本网关不参与。
///
/// **Village-only 效果一律 Fail Closed**（§7）：`VillageResourceDelta` / `GrantPersonRest` 返回 false +
/// 明确原因（远征网关不得跨域写 Village；即使 Runtime 配置损坏也不会越域操作）。
///
/// 结算可行性（§9 / §10）**不**在本类判定：`CanAdvanceExpeditionTime` 只做结构可执行性检查
/// （正值 / 状态存在 / 溢出防护）；「执行完整有序 Effects 后当前 Node 仍能完成 Settlement」由
/// `ExpeditionEventFlow` 在 Commit **之前**用有序投影 + Node Settlement 成本统一证明，
/// 避免出现「Option 已执行但最后才发现补给不足」的软锁。
/// </summary>
public sealed class ExpeditionEventEffectGateway : IEventEffectGateway
{
    private const string CrossDomainRefusal =
        "远征事件网关不拥有 Village Authority：该效果属于村庄域（Fail Closed，不跨域操作）。";

    private readonly AdventureManager _adventure;
    private readonly ActiveExpeditionEventState _active;
    private readonly string _optionId;
    private readonly string _outcomeId;

    /// <summary>
    /// 构造。`optionId` / `outcomeId` 用于 §12 的稳定战斗 Seed 派生
    /// （OutcomeId 由调用方经 `EventOptionExecutor.PreviewOutcome` 预先确定，与正式执行同一 Outcome）。
    /// </summary>
    public ExpeditionEventEffectGateway(AdventureManager adventure, ActiveExpeditionEventState active,
        string optionId, string outcomeId)
    {
        _adventure = adventure;
        _active = active;
        _optionId = optionId;
        _outcomeId = outcomeId;
    }

    // ---------------- 只读投影输入 ----------------

    /// <summary>远征域不拥有村庄资源 → 明确不可用（不得伪造 0 冒充真实值）。</summary>
    public bool TryGetVillageResource(string resourceKey, out int amount)
    {
        amount = 0;
        return false;
    }

    /// <summary>读取当前远征补给（Carry）；无 Progression 会话 → 不可用。</summary>
    public bool TryGetExpeditionSupply(out int supply)
    {
        supply = 0;

        var state = _adventure != null ? _adventure.ProgressionState : null;
        if (state == null || state.Supply == null) return false;

        supply = state.Supply.CurrentCarryUnits;
        return true;
    }

    // ---------------- 纯查询（preflight；不得 mutation） ----------------

    public bool CanGrantRest(string personId, out string reason)
    {
        reason = CrossDomainRefusal;
        return false;
    }

    /// <summary>
    /// 额外远征时间能否被本会话接受（**结构**可行性；不做结算可行性判断，见类注释）：
    ///   - 必须有进行中的远征会话与活动事件；
    ///   - `days` 必须 &gt; 0；
    ///   - 累积后不得溢出 int（超界明确拒绝，不静默截断）。
    /// </summary>
    public bool CanAdvanceExpeditionTime(int days, out string reason)
    {
        if (_adventure == null || _adventure.ProgressionState == null)
        {
            reason = "没有进行中的远征会话：无法计入额外远征时间。";
            return false;
        }

        if (_active == null)
        {
            reason = "当前没有活动事件：无法计入额外远征时间。";
            return false;
        }

        if (days <= 0)
        {
            reason = "AdvanceExpeditionTime 天数必须 > 0（当前 " + days + "）。";
            return false;
        }

        long projected = (long)_active.PendingExtraElapsedDays + days;
        if (projected > int.MaxValue)
        {
            reason = "额外远征时间累积溢出（" + _active.PendingExtraElapsedDays + " + " + days + "）。";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    /// <summary>
    /// **StartBattle 的 Preflight（§11）**：用 Event 指定的 EncounterPoolId + 当前深度 + 稳定派生 Seed
    /// 预生成一份**完整合法** EnemyRoster，并存进 Active Event State。
    ///
    /// 失败 → 整个 Option Preflight 失败（零 mutation：Pending 保留、History 不写、Node 仍停留 Event Pending）；
    /// **不**回退全 Monster Registry、**不**生成默认 3 怪、**不**建 Placeholder Battle。
    /// 幂等：同一 (pool, seed) 重复调用复用已生成的那一份（Preflight 与 Commit 必须同一份，§13）。
    /// </summary>
    public bool CanStartBattle(string encounterPoolId, out string reason)
    {
        if (_adventure == null || _active == null)
        {
            reason = "没有活动事件：无法开始 Event 内战斗。";
            return false;
        }

        if (string.IsNullOrEmpty(encounterPoolId))
        {
            reason = "EncounterPoolId 为空：拒绝开始战斗。";
            return false;
        }

        int depth = _adventure.CurrentExpeditionDepth;
        int seed = DeriveBattleSeed(encounterPoolId);

        if (_active.PreparedBattleRoster != null
            && string.Equals(_active.PreparedBattlePoolId, encounterPoolId, StringComparison.Ordinal)
            && _active.PreparedBattleSeed == seed)
        {
            reason = string.Empty;   // 复用同一份预备阵容（Preflight 幂等）
            return true;
        }

        EnemyRoster prepared;
        string diagnostic;
        if (!_adventure.TryPrepareEventBattleRoster(_active.NodeId, encounterPoolId, depth, seed,
                out prepared, out diagnostic))
        {
            reason = diagnostic;
            return false;
        }

        _active.PreparedBattleRoster = prepared;
        _active.PreparedBattlePoolId = encounterPoolId;
        _active.PreparedBattleDepth = depth;
        _active.PreparedBattleSeed = seed;

        reason = string.Empty;
        return true;
    }

    // ---------------- 正式提交（只在 Commit 阶段调用） ----------------

    public bool ApplyVillageResourceDelta(string resourceKey, int delta, out string reason)
    {
        reason = CrossDomainRefusal;
        return false;
    }

    /// <summary>
    /// 正式应用 Supply 增量（正 = 增加 / 负 = 消耗）：唯一 Authority 仍是
    /// <see cref="ExpeditionSupplyState.TryApplyDelta"/>（不足或溢出 → false 且零 mutation）。
    /// </summary>
    public bool ApplySupplyDelta(int delta, out string reason)
    {
        var state = _adventure != null ? _adventure.ProgressionState : null;
        if (state == null || state.Supply == null)
        {
            reason = "没有进行中的远征会话：无法应用补给增量。";
            return false;
        }

        if (delta == 0)
        {
            reason = string.Empty;
            return true;   // 0 增量 = 合法 no-op（与 Village 资源增量同一语义）
        }

        if (!state.Supply.TryApplyDelta(delta))
        {
            reason = "远征补给无法应用增量 " + delta + "（当前 "
                + state.Supply.CurrentCarryUnits + "）：结果会小于 0 或溢出，已零 mutation 拒绝。";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    public bool GrantRest(string personId, out string reason)
    {
        reason = CrossDomainRefusal;
        return false;
    }

    /// <summary>
    /// **只累积，不结算**：把 Event 请求的额外远征时间记进
    /// `ActiveExpeditionEvent.PendingExtraElapsedDays`；真实 elapsed 与补给消耗由
    /// `TryResolveCurrentNode(outcome, additionalElapsedDays, ...)` 在 Node Settlement 一次性完成（§8）。
    /// </summary>
    public bool AdvanceExpeditionTime(int days, out string reason)
    {
        if (!CanAdvanceExpeditionTime(days, out reason)) return false;

        _active.PendingExtraElapsedDays += days;
        reason = string.Empty;
        return true;
    }

    /// <summary>
    /// **StartBattle 的 Commit（§13）**：把 Preflight 已生成的**同一份** EnemyRoster 提交为
    /// Active Encounter（BattleUI 随后 Prep → Battle 消费同一份，不重新生成）。
    /// </summary>
    public bool StartBattle(string encounterPoolId, out string reason)
    {
        if (_adventure == null || _active == null)
        {
            reason = "没有活动事件：无法提交 Event 内战斗。";
            return false;
        }

        if (_active.PreparedBattleRoster == null
            || !string.Equals(_active.PreparedBattlePoolId, encounterPoolId, StringComparison.Ordinal))
        {
            reason = "缺少与 PoolId '" + encounterPoolId + "' 匹配的预备阵容："
                + "Preflight 必须先成功生成（禁止 Commit 阶段重新生成）。";
            return false;
        }

        ActiveEncounterState committed;
        string diagnostic;
        if (!_adventure.CommitPreparedEventBattleEncounter(_active.NodeId, encounterPoolId,
                _active.PreparedBattleDepth, _active.PreparedBattleSeed,
                _active.PreparedBattleRoster, out diagnostic))
        {
            reason = diagnostic;
            return false;
        }

        committed = _adventure.ActiveEncounter;
        _active.CommittedBattle = committed;
        reason = string.Empty;
        return true;
    }

    // ---------------- 稳定派生（§12；不得使用 Guid / 时间 / 引擎随机） ----------------

    /// <summary>
    /// Event 内战斗的 EncounterSeed：稳定派生自
    /// `Expedition Session Seed + NodeId + EventInstanceId + OptionId + OutcomeId + PoolId`。
    /// 同一个 (EventInstance, Option, Outcome, Pool) 永远得到同一个 Seed → 不重抽敌人。
    /// </summary>
    public int DeriveBattleSeed(string encounterPoolId)
    {
        string material = "expedition-event-battle|1"
            + "|session:" + (_adventure != null ? _adventure.EncounterSessionSeed : 0)
            + "|node:" + (_active != null ? _active.NodeId : string.Empty)
            + "|instance:" + (_active != null && _active.Instance != null ? _active.Instance.EventInstanceId : string.Empty)
            + "|option:" + (_optionId ?? string.Empty)
            + "|outcome:" + (_outcomeId ?? string.Empty)
            + "|pool:" + (encounterPoolId ?? string.Empty);

        return StableHash32(material);
    }

    /// <summary>FNV-1a 32 位（只取非负值，便于诊断 / 复现；与 Event 侧 TriggerKey 派生同一族原语）。</summary>
    private static int StableHash32(string text)
    {
        if (string.IsNullOrEmpty(text)) return 0;

        unchecked
        {
            const uint offsetBasis = 2166136261u;
            const uint prime = 16777619u;

            uint hash = offsetBasis;
            for (int i = 0; i < text.Length; i++)
            {
                hash ^= text[i];
                hash *= prime;
            }
            return (int)(hash & 0x7FFFFFFFu);
        }
    }
}
