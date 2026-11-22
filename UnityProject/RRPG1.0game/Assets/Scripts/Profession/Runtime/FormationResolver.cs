using System;
using System.Collections.Generic;

/// <summary>
/// 一次 Day End Formation 结算的日期上下文（Life History 记录 Date 用；年/月/日）。
/// M6 只要求 ProfessionFormed 可靠记录日期；不建设完整日历 / Timeline。
/// </summary>
public struct FormationDayContext
{
    public int Year;
    public int Month;
    public int Day;

    public FormationDayContext(int year, int month, int day)
    {
        Year = year;
        Month = month;
        Day = day;
    }
}

/// <summary>
/// 一次 Day End Formation Resolver 运行的报告（结果可观测，供调用方 / 测试使用）。
/// </summary>
public sealed class FormationDayReport
{
    /// <summary>本次是否真正运行（false = 缺配置 / 空人物 / 空状态且无贡献等合法 no-op）。</summary>
    public bool Ran;

    /// <summary>本次 Day End 后解析出的 Formation Target ProfessionId（无则 null）。</summary>
    public string TargetAfter;

    /// <summary>本次 Day End 后是否 Ambiguous。</summary>
    public bool AmbiguousAfter;

    /// <summary>本次 Day End 后的候选数量（观察用）。</summary>
    public int CandidateCountAfter;

    /// <summary>本次 Day End 尝试自动 Formation 的结果（未尝试为 null）。</summary>
    public FormationCommitResult Formation;
}

/// <summary>Formation Commit 结果（明确失败原因；禁止静默把非法状态当成功）。</summary>
public enum FormationCommitStatus
{
    Success = 0,
    /// <summary>host 为 null / PersonId 为空（不是可形成职业的普通 Person）。</summary>
    NotOrdinaryPerson,
    /// <summary>ProfessionId 为空 / 非法。</summary>
    InvalidProfession,
    /// <summary>该 Person 已有 Active Profession（M6 首次 Formation 完成即禁止自动替换 / Transition）。</summary>
    AlreadyHasActiveProfession,
    /// <summary>配置缺失（FormationTestConfig 未注入）。</summary>
    NoConfig,
    /// <summary>Registry 缺失或不存在该 ProfessionDefinition（未知职业条目绝不改投其它职业）。</summary>
    UnknownProfession,
    /// <summary>目标 Profession 当前不是 Formation Candidate。</summary>
    NotCandidate,
    /// <summary>目标 Profession 不是当前明确 Formation Target（禁止点击旁路直接指定职业）。</summary>
    NotFormationTarget,
    /// <summary>Stability 尚未达到自动 Formation 所需下限。</summary>
    StabilityBelowThreshold,
    /// <summary>Life History 已记录过该 Profession 的 ProfessionFormed（exactly-once 防护；重复 Commit 为 no-op）。</summary>
    AlreadyFormed
}

/// <summary>Formation Commit 结果载体（携带失败原因；成功时给出模板来源的 Trait 定义 ID）。</summary>
public sealed class FormationCommitResult
{
    public FormationCommitStatus Status;
    public string ProfessionId;

    /// <summary>成功时 = ProfessionDefinition.ProfessionTraitId（Trait Definition 来自 Profession Template 的共享初始配置）。</summary>
    public string TraitDefinitionId;

    public bool IsSuccess => Status == FormationCommitStatus.Success;

    public static FormationCommitResult Fail(FormationCommitStatus status, string professionId)
    {
        return new FormationCommitResult { Status = status, ProfessionId = professionId };
    }
}

/// <summary>
/// M6-A Formation Resolver —— 统一 Day End Formation 算法（§12.10）。
///
/// 管线：Day 内 Work / Life / Event 先产生标准化 Evidence Contribution →
///       Day End 统一：汇总当日 Evidence → 更新 Strength → 更新 inactivity/decay →
///       Candidate → Target / Ambiguity → Stability → 判断首次 Formation。
///
/// 边界（M6 首次 Formation，不做 Transition / Dormant / 多职业）：
///   - Person 已有 Active Profession 时，本 Resolver 仍继续更新 Evidence / Strength /
///     Candidate / Stability（§12.16：形成后其它 Evidence 继续变化），但绝不自动替换
///     Active Profession（不 Commit）。
///   - 所有阈值 / 速率 / 曲线来自 FormationTestConfig（Test / Not Frozen），本类不冻结数值。
///   - Day End 只由唯一 Village DayTick 接线（VillageRuntimeBridge.HandleDayTick）调用；
///     本类不建立任何第二套 Day Loop / Coroutine / Update。
/// </summary>
public static class FormationResolver
{
    /// <summary>
    /// Day End 结算一个普通 Person 的 Formation 派生状态。
    /// </summary>
    /// <param name="host">普通 Person 宿主（CharacterInstance）。null / 空 PersonId → no-op。</param>
    /// <param name="day">本次结算日期（Life History / Commit 记录 Date 用）。</param>
    /// <param name="contributions">当日该 Person 的全部标准化 Evidence Contribution（可空/空 = 无当日活动）。</param>
    /// <param name="config">Formation 数值配置（Test / Not Frozen）。null → no-op（不猜测数值）。</param>
    /// <param name="registry">ProfessionDefinition 唯一合法 Registry（commit 模板权威来源；可为 null）。</param>
    public static FormationDayReport AdvanceDay(
        IFormationHost host,
        FormationDayContext day,
        IEnumerable<FormationEvidenceContribution> contributions,
        FormationTestConfig config,
        ProfessionDefinitionRegistry registry)
    {
        var report = new FormationDayReport();
        if (host == null || string.IsNullOrEmpty(host.PersonId)) return report;
        if (config == null) return report;

        FormationState state = host.FormationState;
        if (state == null) return report;

        // ---------- 1) 汇总当日 Contribution（同一 Profession 当日多来源合并为总量） ----------
        var activeAmounts = new Dictionary<string, float>();
        if (contributions != null)
        {
            foreach (var c in contributions)
            {
                if (c == null || string.IsNullOrEmpty(c.ProfessionId) || c.Amount <= 0f) continue;
                if (activeAmounts.TryGetValue(c.ProfessionId, out float existing))
                    activeAmounts[c.ProfessionId] = existing + c.Amount;
                else
                    activeAmounts[c.ProfessionId] = c.Amount;
            }
        }

        bool hadAnyStateBefore = state.Entries != null && state.Entries.Count > 0;
        if (!hadAnyStateBefore && activeAmounts.Count == 0) return report;

        // ---------- 2) inactivity / decay：无当日活动的既有条目 中断天数 +1 并衰减 ----------
        if (state.Entries != null)
        {
            for (int i = 0; i < state.Entries.Count; i++)
            {
                var entry = state.Entries[i];
                if (entry == null || string.IsNullOrEmpty(entry.ProfessionId)) continue;
                if (activeAmounts.ContainsKey(entry.ProfessionId))
                {
                    entry.InactiveDays = 0;
                }
                else
                {
                    entry.InactiveDays++;
                    if (entry.Strength > 0f && entry.InactiveDays > 0)
                    {
                        float loss = config.DecayDelta(entry.InactiveDays);
                        if (loss > 0f) entry.Strength = Math.Max(0f, entry.Strength - loss);
                    }
                }
            }
        }

        // ---------- 3) 首次收到 Evidence 的 Profession 建立独立条目（unknown 条目保留原样，不改投） ----------
        foreach (var kv in activeAmounts)
        {
            if (state.FindEntry(kv.Key) == null) state.GetOrAddEntry(kv.Key);
        }

        // ---------- 4) Strength 成长（慢 → 快 → 慢；ActiveCareerDays 每有效活动日 +1） ----------
        foreach (var kv in activeAmounts)
        {
            var entry = state.FindEntry(kv.Key);
            if (entry == null) continue;
            entry.ActiveCareerDays++;
            float delta = config.GrowthDelta(entry.ActiveCareerDays);
            if (delta > 0f)
            {
                entry.Strength = Math.Min(config.StrengthMax, entry.Strength + delta * kv.Value);
            }
        }

        // ---------- 5) Qualification / Candidate（轻度滞后，§12.8） ----------
        if (state.Entries != null)
        {
            foreach (var entry in state.Entries)
            {
                if (entry == null || string.IsNullOrEmpty(entry.ProfessionId)) continue;
                if (entry.Strength >= config.QualificationThreshold)
                {
                    entry.IsCandidate = true;
                }
                else if (entry.IsCandidate
                         && entry.Strength >= config.QualificationThreshold - config.CandidateExitHysteresis)
                {
                    // 已是 Candidate：跌到 Threshold - Hysteresis 以下才退出，避免边界来回跳。
                    entry.IsCandidate = true;
                }
                else
                {
                    entry.IsCandidate = false;
                }
            }
        }

        // ---------- 6) Formation Window（§12.9） ----------
        if (CountCandidates(state) > 0) state.FormationWindowOpen = true;

        // ---------- 7) Target / Ambiguity（§12.11）与 Stability（§12.12） ----------
        string previousTarget = state.FormationTargetProfessionId;
        string nextTarget = ResolveTarget(state, config, out bool ambiguous);
        UpdateStability(state, previousTarget, nextTarget, ambiguous, config);
        state.FormationTargetProfessionId = nextTarget;
        state.IsAmbiguous = ambiguous;

        report.Ran = true;
        report.TargetAfter = nextTarget;
        report.AmbiguousAfter = ambiguous;
        report.CandidateCountAfter = CountCandidates(state);

        // ---------- 8) 首次自动 Formation（§12.13） ----------
        // 当前有 Active Profession → 禁止 M6 自动 Transition（只继续 Evidence / Strength / 派生状态演化）。
        if (!host.HasActiveProfession && !string.IsNullOrEmpty(nextTarget))
        {
            var targetEntry = state.FindEntry(nextTarget);
            if (targetEntry != null && targetEntry.IsCandidate
                && targetEntry.Stability + 1e-4f >= config.FormationStabilityThreshold)
            {
                report.Formation = FormationCommitter.Commit(host, nextTarget, day, config, registry);
            }
        }

        return report;
    }


    /// <summary>统计当前 Candidate 数量。</summary>
    public static int CountCandidates(FormationState state)
    {
        if (state == null || state.Entries == null) return 0;
        int count = 0;
        for (int i = 0; i < state.Entries.Count; i++)
        {
            if (state.Entries[i] != null && state.Entries[i].IsCandidate) count++;
        }
        return count;
    }

    private static string ResolveTarget(FormationState state, FormationTestConfig config, out bool ambiguous)
    {
        ambiguous = false;
        if (state == null || state.Entries == null) return null;

        string leaderId = null;
        float leaderStrength = float.MinValue;
        float secondStrength = float.MinValue;
        int candidateCount = 0;

        foreach (var entry in state.Entries)
        {
            if (entry == null || !entry.IsCandidate || string.IsNullOrEmpty(entry.ProfessionId)) continue;
            candidateCount++;
            if (entry.Strength > leaderStrength)
            {
                secondStrength = leaderStrength;
                leaderStrength = entry.Strength;
                leaderId = entry.ProfessionId;
            }
            else if (entry.Strength > secondStrength)
            {
                secondStrength = entry.Strength;
            }
        }

        if (candidateCount == 0) return null;
        if (candidateCount == 1) return leaderId;

        // 多个 Candidate：有明显 Leader（领先幅度 ≥ DominanceGap）→ Target = Leader；否则 Ambiguous。
        if (leaderStrength - secondStrength >= config.DominanceStrengthGap) return leaderId;

        ambiguous = true;
        return null;
    }

    private static void UpdateStability(FormationState state, string previousTarget, string nextTarget,
        bool ambiguous, FormationTestConfig config)
    {
        if (state == null || state.Entries == null) return;

        foreach (var entry in state.Entries)
        {
            if (entry == null || string.IsNullOrEmpty(entry.ProfessionId)) continue;

            if (!string.IsNullOrEmpty(nextTarget) && entry.ProfessionId == nextTarget)
            {
                // 当前 Target：持续一致（前一天同为该 Target）→ Stability 增长。
                if (previousTarget == nextTarget)
                    entry.Stability = Math.Min(config.StabilityMax, entry.Stability + config.StabilityGainPerConsistentDay);
                continue;
            }

            if (previousTarget == entry.ProfessionId)
            {
                // 明确转向：旧 Target Stability 更快下降。
                entry.Stability = Math.Max(0f, entry.Stability - config.StabilityLossOnTargetChange);
                continue;
            }

            if (entry.Stability <= 0f) continue;
            float passive = ambiguous ? config.StabilityDecayPerAmbiguousDay : config.StabilityPassiveDecayPerDay;
            entry.Stability = Math.Max(0f, entry.Stability - passive);
        }
    }
}

/// <summary>
/// M6 原子 Formation Commit（§12.15 / Stage 3 Atomic Formation Commit）。
/// 由 FormationResolver 在满足 Candidate + Target + Stability 时调用；测试也可在
/// 状态已合法的前提下直接调用（Commit 仍会全量重校验，禁止点击旁路直接指定职业）。
/// </summary>
public static class FormationCommitter
{

    /// <summary>
    /// 原子执行首次 Profession Formation Commit。
    /// 成功时：创建 ProfessionProgress（Level=1 / Exp=0 / clone InitialUnlockedCards /
    /// TraitLevel=1）、StarterSlots 整 List 原子替换为模板 InitialStarterBuild 深拷贝、
    /// Life History 追加一次 ProfessionFormed；Formation Evidence 不清零；Assignment 不改变。
    /// 重复 Commit（已有 Active Profession / Life History 已记录）为 no-op。
    /// </summary>
    public static FormationCommitResult Commit(
        IFormationHost host,
        string professionId,
        FormationDayContext day,
        FormationTestConfig config,
        ProfessionDefinitionRegistry registry)
    {
        if (host == null || string.IsNullOrEmpty(host.PersonId))
            return FormationCommitResult.Fail(FormationCommitStatus.NotOrdinaryPerson, professionId);

        if (string.IsNullOrEmpty(professionId))
            return FormationCommitResult.Fail(FormationCommitStatus.InvalidProfession, professionId);

        if (config == null)
            return FormationCommitResult.Fail(FormationCommitStatus.NoConfig, professionId);

        if (host.HasActiveProfession)
            return FormationCommitResult.Fail(FormationCommitStatus.AlreadyHasActiveProfession, professionId);

        if (registry == null || !registry.TryGetById(professionId, out ProfessionDefinition definition))
        {
            // unknown Profession 条目不得改投其它职业：保留原样、显式失败。
            return FormationCommitResult.Fail(FormationCommitStatus.UnknownProfession, professionId);
        }

        FormationState state = host.FormationState;
        var entry = state != null ? state.FindEntry(professionId) : null;
        if (entry == null || !entry.IsCandidate)
            return FormationCommitResult.Fail(FormationCommitStatus.NotCandidate, professionId);

        if (state.FormationTargetProfessionId != professionId)
            return FormationCommitResult.Fail(FormationCommitStatus.NotFormationTarget, professionId);

        if (entry.Stability + 1e-4f < config.FormationStabilityThreshold)
            return FormationCommitResult.Fail(FormationCommitStatus.StabilityBelowThreshold, professionId);

        // Life History exactly-once：正常重复 Commit 已被 HasActiveProfession 拦截；
        // 这里再以人生事实账本防御（防止任何异常状态下重复记录人生事实）。
        if (host.LifeHistory != null && host.LifeHistory.HasProfessionFormed(professionId))
            return FormationCommitResult.Fail(FormationCommitStatus.AlreadyFormed, professionId);


        // ---------- 以下进入提交区块：前置全部通过后才开始任何写入（无失败中途路径） ----------

        // 1) 原子创建 ProfessionProgress（UnlockedPool 深拷贝，绝不共享模板 List 引用）。
        var progress = new ProfessionProgress
        {
            ProfessionId = definition.ProfessionId,
            ProfessionLevel = 1,
            ProfessionExp = 0,
            UnlockedPoolCardIds = ClonePool(definition.InitialUnlockedCards),
            ProfessionTraitLevel = 1
        };

        // 2) StarterSlots 整 List 原子替换为模板 InitialStarterBuild 的深拷贝。
        //    不合并 Legacy / Test Starter；不采用「仅为空才初始化」；不共享模板引用。
        var starter = new List<StarterSlotEntry>();
        if (definition.InitialStarterBuild != null)
        {
            foreach (var s in definition.InitialStarterBuild)
            {
                starter.Add(s == null
                    ? new StarterSlotEntry(string.Empty, 0)
                    : new StarterSlotEntry(s.CardId, s.Proficiency));
            }
        }
        host.StarterSlots = starter;

        // 3) 挂载 Active Profession（此后 HasActiveProfession == true → 重复 Commit no-op）。
        host.ActiveProfessionProgress = progress;

        // 4) Life History 追加一次 ProfessionFormed（Source of Record；exactly-once）。
        if (host.LifeHistory != null)
            host.LifeHistory.AppendProfessionFormed(day.Year, day.Month, day.Day, definition.ProfessionId);

        // 5) Formation Evidence 不清零：host.FormationState 原样保留（§12.15 / §12.16）。

        return new FormationCommitResult
        {
            Status = FormationCommitStatus.Success,
            ProfessionId = definition.ProfessionId,
            TraitDefinitionId = definition.ProfessionTraitId
        };
    }

    private static List<string> ClonePool(IEnumerable<string> source)
    {
        var result = new List<string>();
        if (source != null)
        {
            foreach (var id in source)
            {
                if (string.IsNullOrEmpty(id)) continue;
                result.Add(id);
            }
        }
        return result;
    }
}


