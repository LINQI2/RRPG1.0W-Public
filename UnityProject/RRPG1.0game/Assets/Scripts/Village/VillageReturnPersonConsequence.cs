using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// VillageReturnPersonConsequence —— V4 Gate 2 的 **Test / Not Frozen** Return Person Consequence 提交器。
///
/// 目的（Village-GameSpec §6.4 / Decision C + OQ1/OQ3）：
///   远征结束时把「Run 内健康结果」与「Character 远征经历」提交为**长期人物后果**：
///     RunPerson.CurrentHP / MaxHP → 长期 Injury（复用现有字段，不新建 Health Authority）
///     Character → PersonLifeHistory.AppendExpeditionReturned（Hero 不写，OQ1）
///
/// 边界（硬约束）：
///   - 不保存 CurrentHP、不新建 Health Save DTO、不使用 DeathDoorSystem、不写 IsDead、不做治疗；
///   - 不新建第二套 Transaction Manager：exactly-once 复用 `ExpeditionRecord` 会话标记
///     （HealthConsequenceCommitted / ExperienceCommitted，不持久化）；
///   - 只在 **Catch-up 全部成功之后、Clear Occupancy 之前、Final Save 之前** 调用；
///   - Injury 写入规则 = `max(现有 Injury, 本次解析结果)`（单调，天然幂等，不因重复提交恶化）；
///   - 所有阈值 / floor / severity 均为 Test / Not Frozen。
/// </summary>
public static class VillageReturnPersonConsequence
{
    /// <summary>【Test / Not Frozen】轻伤阈值：`ratio <= 0.5` → LightInjury。</summary>
    public const float LightInjuryRatioThreshold = 0.5f;

    /// <summary>【Test / Not Frozen】重伤阈值：`ratio <= 0.25`（或 CurrentHP &lt;= 0）→ HeavyInjury。</summary>
    public const float HeavyInjuryRatioThreshold = 0.25f;

    /// <summary>
    /// 【Test / Not Frozen】Run 最终 HP → 长期 Injury 等级解析。
    /// 基础映射按 HP 比例；`Defeat` 提供 floor，但 **只对最终 HP &lt; MaxHP 的 Person 生效**（OQ3：
    /// 满 HP Person 不得仅因队伍 Defeat 自动受伤）。`maxHp &lt;= 0` 视为数据非法 → 不降级（Normal）。
    /// </summary>
    public static InjuryStatus ResolveInjury(int currentHp, int maxHp, RunEndMode mode)
    {
        if (maxHp <= 0) return InjuryStatus.Normal;

        float ratio = (float)currentHp / maxHp;
        InjuryStatus severity;
        if (currentHp <= 0 || ratio <= HeavyInjuryRatioThreshold) severity = InjuryStatus.HeavyInjury;
        else if (ratio <= LightInjuryRatioThreshold) severity = InjuryStatus.LightInjury;
        else severity = InjuryStatus.Normal;

        if (mode == RunEndMode.Defeat
            && currentHp < maxHp
            && (int)severity < (int)InjuryStatus.LightInjury)
        {
            severity = InjuryStatus.LightInjury;
        }
        return severity;
    }

    /// <summary>按严重度取较大者（已存在的 Injury / 残疾不可被降级）。</summary>
    public static InjuryStatus MaxSeverity(InjuryStatus a, InjuryStatus b)
    {
        return (int)a >= (int)b ? a : b;
    }

    /// <summary>
    /// 提交本次 Return 的长期人物后果（Health + Character Expedition Experience）。
    ///
    /// 前置守卫（任一不满足 → 拒绝，不写任何长期状态）：
    ///   - 存在未 Completed / 非 Failed 的 Return 会话；
    ///   - 相位已越过 Catch-up（CatchUpComplete / OccupancyCleared）；
    ///   - RunState / Person 真相源 / VillageTime 均可用。
    ///
    /// 幂等：会话标记 + `max(Injury)` 单调；重复调用不会重复写入或重复追加经历。
    /// </summary>
    public static VillageReturnCommitResult TryCommit(
        ExpeditionRecord record, RunState runState, RunEndMode mode,
        VillageTime time, PlayerCollection personSource)
    {
        var result = new VillageReturnCommitResult();

        if (record == null) return result.Reject("no return session record");
        if (record.Completed) return result.Reject("return session already completed");
        if (record.Phase == ExpeditionReturnPhase.Failed) return result.Reject("return session failed");
        if (record.Phase != ExpeditionReturnPhase.CatchUpComplete
            && record.Phase != ExpeditionReturnPhase.OccupancyCleared)
        {
            return result.Reject("catch-up not completed (phase=" + record.Phase + ")");
        }
        if (runState == null) return result.Reject("run state unavailable");
        if (personSource == null) return result.Reject("person source unavailable");
        if (time == null) return result.Reject("village time unavailable");

        if (!record.HealthConsequenceCommitted)
        {
            result.HealthWritten = CommitHealth(record.PersonIds, runState, mode, personSource);
            record.HealthConsequenceCommitted = true;
        }
        if (!record.ExperienceCommitted)
        {
            result.ExperienceWritten = CommitExperience(record.PersonIds, mode, time, personSource);
            record.ExperienceCommitted = true;
        }
        return result;
    }

    /// <summary>Run HP 最终结果 → 长期 Injury（Character / Hero 均复用现有字段；不写 IsDead）。</summary>
    private static int CommitHealth(List<string> personIds, RunState runState, RunEndMode mode,
        PlayerCollection personSource)
    {
        if (personIds == null) return 0;
        var seen = new HashSet<string>();
        int written = 0;

        foreach (var personId in personIds)
        {
            if (string.IsNullOrEmpty(personId) || !seen.Add(personId)) continue;
            if (!runState.TryGetPerson(personId, out var runPerson) || runPerson == null) continue;

            InjuryStatus severity = ResolveInjury(runPerson.CurrentHP, runPerson.MaxHP, mode);
            if (severity == InjuryStatus.Normal) continue;

            var character = personSource.GetCharacterByPersonId(personId);
            if (character != null)
            {
                var before = character.Injury;
                character.Injury = MaxSeverity(before, severity);
                if (character.Injury != before) written++;
                continue;
            }

            var hero = personSource.GetHeroByPersonId(personId);
            if (hero != null)
            {
                var before = hero.Injury;
                hero.Injury = MaxSeverity(before, severity);
                if (hero.Injury != before) written++;
                continue;
            }

            Debug.LogWarning("[VillageReturnPersonConsequence] 找不到长期 Person（personId=" + personId
                + "），跳过 Health Consequence（不猜测身份）。");
        }
        return written;
    }

    /// <summary>
    /// Character Expedition Experience（OQ1：仅普通 Character；Hero 不新增 LifeHistory）。
    /// 最小事实：Type = ExpeditionReturned，Date = 回到村庄当日 VillageTime，Outcome = Return 模式。
    /// </summary>
    private static int CommitExperience(List<string> personIds, RunEndMode mode, VillageTime time,
        PlayerCollection personSource)
    {
        if (personIds == null) return 0;
        var seen = new HashSet<string>();
        int written = 0;

        foreach (var personId in personIds)
        {
            if (string.IsNullOrEmpty(personId) || !seen.Add(personId)) continue;

            var character = personSource.GetCharacterByPersonId(personId);
            if (character == null) continue; // Hero / 未知 Person → 跳过（不新增 Hero LifeHistory）

            if (character.LifeHistory == null)
            {
                Debug.LogWarning("[VillageReturnPersonConsequence] Character LifeHistory 为 null（personId="
                    + personId + "），跳过 Experience 记录。");
                continue;
            }

            character.LifeHistory.AppendExpeditionReturned(time.Year, time.Month, time.Day, (int)mode);
            written++;
        }
        return written;
    }
}

/// <summary>VillageReturnPersonConsequence.TryCommit 的结果（Test / Not Frozen）。</summary>
public class VillageReturnCommitResult
{
    /// <summary>实际发生变化的长期 Injury 写入人数。</summary>
    public int HealthWritten;

    /// <summary>实际追加的 Character Expedition Experience 条数。</summary>
    public int ExperienceWritten;

    /// <summary>是否被守卫拒绝（true = 未写入任何长期状态）。</summary>
    public bool Rejected;

    /// <summary>拒绝原因（可读；仅诊断用）。</summary>
    public string RejectReason;

    internal VillageReturnCommitResult Reject(string reason)
    {
        Rejected = true;
        RejectReason = reason;
        return this;
    }
}
