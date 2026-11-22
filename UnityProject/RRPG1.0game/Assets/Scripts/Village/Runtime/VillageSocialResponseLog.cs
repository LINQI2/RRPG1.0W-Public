using System.Collections.Generic;

/// <summary>
/// VillageSocialResponseChoice —— V3B 玩家回应方向（Village.Runtime 纯逻辑）。
/// 只表达玩家对一个 Recognition Event 的两种合法选择；均为中性治理决定，
/// Choice B 不附带任何惩罚 / 隐藏负值（Response ≠ Complaint，Consequence ≠ Penalty）。
/// </summary>
public enum VillageSocialResponseChoice
{
    /// <summary>认可长期安排带来的负担节奏问题：给该 Person 明确恢复窗口（Test 窗口=至本月最后一天）。</summary>
    GrantRest = 0,
    /// <summary>当前仍需要他继续承担临时任务（无后续限制）。</summary>
    ContinueUsing = 1
}

/// <summary>
/// VillageSocialUrgency —— V3C 最小 Event Urgency 语义（Village.Runtime 纯逻辑，Test / Not Frozen）。
///
/// 只表达「这个回应是否需要玩家及时处理」两种最小语义，作为 GameSpec §5.4 的最小技术闭合：
///   - 不是正式 Event 分类体系，不冻结命名与数量；
///   - 不建立 Event 队列 / Deadline Scheduler / Ignore Penalty / 多事件排队；
///   - 本枚举不承载任何数值窗口；具体分类由 Pattern 目录（VillageTestSocialPatterns）承载。
///
/// 具体某个 Event 属于哪一类，由 VillageTestSocialPatterns.ResolveUrgency(patternKey) 唯一决定；
/// 本类不判断、不持久化（Urgency 是 Pattern 定义的派生语义，不是长期事实）。
/// </summary>
public enum VillageSocialUrgency
{
    /// <summary>需要及时处理：事件正在堵塞玩家的下一次决定。</summary>
    Timely = 0,
    /// <summary>可忽略 / 非强制处理：不处理也不产生任何惩罚（V3 不提供忽略按钮，只表达紧迫性语义）。</summary>
    Ignorable = 1
}

/// <summary>VillageSocialResponseLog.OpenBuildupResponse 的创建结果。</summary>
public enum VillageResponseOpenResult
{
    Created,
    /// <summary>计数未达 Test 阈值（不建立 Recognition）。</summary>
    BelowThreshold,
    /// <summary>已存在另一条待处理回应（V3B 全局至多一条 pending，不建立 Event 队列）。</summary>
    PendingResponseExists,
    /// <summary>同一 Pattern 在该窗口内已回应过（防重复触发）。</summary>
    AlreadyFired,
    InvalidArguments
}

/// <summary>fired：某 Pattern 在某窗口（自然年，Test/Not Frozen）已对该 Person 回应过的标记。</summary>
public class VillageSocialFired
{
    public string PatternKey;
    public string PersonId;
    public int Year;
}

/// <summary>
/// VillagePendingResponse —— 单条等待玩家处理的 Recognition 回应（V3B 至多一条）。
/// 在真实 Project 加入成功、V3A ledger 已记录事实之后创建；创建即写 fired。
/// 不表达任何情绪 / 态度，只引用客观计数。
/// </summary>
public class VillagePendingResponse
{
    public string EventId;
    public string PatternKey;
    public string PersonId;

    /// <summary>创建时的 Village 年（用于 fired 窗口 key 与年度计数）。</summary>
    public int Year;
}

/// <summary>ChoiceHistory：玩家对一个 Event 的实际选择记录（日期为解析时 VillageTime）。</summary>
public class VillageResponseChoiceRecord
{
    public string PatternKey;
    public string PersonId;
    public VillageSocialResponseChoice Choice;
    public int Year;
    public int Month;
    public int Day;
}

/// <summary>
/// VillageRestRecord —— Choice A 的局部后果（一次事件选择的承诺，非人物长期状态系统）。
/// 语义：休整至 Until（含当日仍被 Rest Gate 拦截），下月 1 日（CurrentDate &gt; Until）起恢复。
/// 只拦截“把该 Person 新加入 Project 的调度”；不影响 Expedition / 当前 Active Project / Primary Assignment。
/// </summary>
public class VillageRestRecord
{
    public string PersonId;
    public int UntilYear;
    public int UntilMonth;
    public int UntilDay;
}

/// <summary>
/// VillageSocialResponseLog —— V3B 最小 Recognition → Response → New Decision 状态（Village.Runtime 纯逻辑）。
///
/// 职责边界：
///   - fired / 单条 pending / ChoiceHistory / RestUntil 四类轻量状态（持久化）；
///   - Recognition 本身不存：由调用方（VillageRuntimeBridge）以「V3A ledger 年度计数 ≥ Test 阈值」派生后调用本类；
///   - 不建立 Event 队列 / Deadline / Ignore / PauseOnOpen；不做 Mood / Opinion / Loyalty / 任何连续指标。
///
/// 本类不含任何「3 / 自然年 / 月末」等产品数值；阈值与窗口常量只存在于 VillageTestSocialPatterns（Test / Not Frozen）。
/// </summary>
public class VillageSocialResponseLog
{
    private readonly List<VillageSocialFired> _fired = new List<VillageSocialFired>();
    private readonly List<VillageResponseChoiceRecord> _choices = new List<VillageResponseChoiceRecord>();
    private readonly List<VillageRestRecord> _rests = new List<VillageRestRecord>();
    private VillagePendingResponse _pending;
    private int _nextEventSeq;

    /// <summary>fired 记录（只读视图）。</summary>
    public IReadOnlyList<VillageSocialFired> Fired => _fired;

    /// <summary>玩家已作选择的历史（只读视图）。</summary>
    public IReadOnlyList<VillageResponseChoiceRecord> Choices => _choices;

    /// <summary>Rest 记录（只读视图）。</summary>
    public IReadOnlyList<VillageRestRecord> Rests => _rests;

    /// <summary>是否有一条等待玩家处理的回应。</summary>
    public bool HasPendingResponse => _pending != null;

    /// <summary>当前待处理回应（无则 null）。</summary>
    public VillagePendingResponse PendingResponse => _pending;

    /// <summary>
    /// 在一次真实成功加入（V3A 事实已记录）后调用：
    /// 年度计数达到 Test 阈值且未 fired 且无其它 pending 时创建 Event（并写 fired）。
    /// 本方法不做任何内容决策；阈值参数由 Test 规则注入。
    /// </summary>
    public VillageResponseOpenResult OpenBuildupResponse(
        string patternKey, string personId, int year, int threshold, int countInYear)
    {
        if (string.IsNullOrEmpty(patternKey) || string.IsNullOrEmpty(personId) || year < 1 || threshold < 1)
            return VillageResponseOpenResult.InvalidArguments;

        if (countInYear < threshold) return VillageResponseOpenResult.BelowThreshold;
        if (IsFired(patternKey, personId, year)) return VillageResponseOpenResult.AlreadyFired;
        if (_pending != null) return VillageResponseOpenResult.PendingResponseExists;

        _fired.Add(new VillageSocialFired { PatternKey = patternKey, PersonId = personId, Year = year });
        _nextEventSeq++;
        _pending = new VillagePendingResponse
        {
            EventId = patternKey + "_" + year + "_" + _nextEventSeq,
            PatternKey = patternKey,
            PersonId = personId,
            Year = year
        };
        return VillageResponseOpenResult.Created;
    }

    /// <summary>
    /// **【Legacy / 迁移专用】清除当前 Legacy Pending**（WP-Event-1D §21 · B4）。
    ///
    /// 唯一合法调用方 = `VillageEventFlow.TryMigrateLegacyPending`：旧 Pending 已按**既成事实**
    /// 收敛为 Unified Event Pending 之后，清除旧载体，避免 Legacy 兼容路径重复展示同一件事。
    ///
    /// 边界：只清 pending（不写 ChoiceHistory、不写 Rest、不动 fired）；正式产品 Recognition 路径
    /// **不再**创建 Legacy Pending，因此本入口不会出现在新玩家行为里。
    /// </summary>
    public bool ClearLegacyPendingForMigration()
    {
        if (_pending == null) return false;

        _pending = null;
        return true;
    }

    /// <summary>某 Pattern 在该窗口（person, year）是否已回应过。</summary>
    public bool IsFired(string patternKey, string personId, int year)
    {
        if (string.IsNullOrEmpty(patternKey) || string.IsNullOrEmpty(personId) || year < 1) return false;
        foreach (var f in _fired)
        {
            if (f == null) continue;
            if (f.PatternKey == patternKey && f.PersonId == personId && f.Year == year) return true;
        }
        return false;
    }

    /// <summary>
    /// 解析当前 pending（date 为解析时 VillageTime 年月日）。
    /// GrantRest → 写入 RestUntil = 当月末最后一天（含当天仍拦截；CurrentDate &gt; Until 才解除）。
    /// ContinueUsing → 只记 ChoiceHistory，不写 Rest。
    /// </summary>
    public bool ResolvePending(VillageSocialResponseChoice choice, int year, int month, int day)
    {
        if (_pending == null) return false;
        if (year < 1 || month < 1 || month > 12 || day < 1) return false;

        _choices.Add(new VillageResponseChoiceRecord
        {
            PatternKey = _pending.PatternKey,
            PersonId = _pending.PersonId,
            Choice = choice,
            Year = year,
            Month = month,
            Day = day
        });

        if (choice == VillageSocialResponseChoice.GrantRest)
        {
            int untilDay = VillageTime.DaysInMonth(year, month);
            _rests.Add(new VillageRestRecord
            {
                PersonId = _pending.PersonId,
                UntilYear = year,
                UntilMonth = month,
                UntilDay = untilDay
            });
        }

        _pending = null;
        return true;
    }

    /// <summary>
    /// Rest Gate：该 Person 当前是否处于恢复窗口（被拦截状态）。
    /// 规则：存在该 Person 的 Rest 记录，且 当前日期 &lt;= Until（含 Until 当天）→ 拦截；
    /// 下月 1 日（当前日期 &gt; Until）→ 恢复。多次 Grant 取 Until 最新的一条。
    /// </summary>
    public bool IsPersonRestBlocked(string personId, int currentYear, int currentMonth, int currentDay)
    {
        var rest = GetLatestRest(personId);
        if (rest == null) return false;
        return DateSequence(currentYear, currentMonth, currentDay)
            <= DateSequence(rest.UntilYear, rest.UntilMonth, rest.UntilDay);
    }

    /// <summary>最新 Rest 记录（无则 null）。</summary>
    public VillageRestRecord GetLatestRest(string personId)
    {
        if (string.IsNullOrEmpty(personId)) return null;
        VillageRestRecord best = null;
        foreach (var r in _rests)
        {
            if (r == null || r.PersonId != personId) continue;
            if (best == null)
            {
                best = r;
                continue;
            }
            if (DateSequence(r.UntilYear, r.UntilMonth, r.UntilDay)
                > DateSequence(best.UntilYear, best.UntilMonth, best.UntilDay))
            {
                best = r;
            }
        }
        return best;
    }

    /// <summary>存档快照。</summary>
    public VillageSocialResponseSaveData ToSaveData()
    {
        var fired = new List<VillageSocialFiredSaveData>();
        foreach (var f in _fired)
        {
            if (f == null) continue;
            fired.Add(new VillageSocialFiredSaveData { PatternKey = f.PatternKey, PersonId = f.PersonId, Year = f.Year });
        }

        VillagePendingResponseSaveData pending = null;
        if (_pending != null)
        {
            pending = new VillagePendingResponseSaveData
            {
                EventId = _pending.EventId,
                PatternKey = _pending.PatternKey,
                PersonId = _pending.PersonId,
                Year = _pending.Year
            };
        }

        var choices = new List<VillageResponseChoiceSaveData>();
        foreach (var c in _choices)
        {
            if (c == null) continue;
            choices.Add(new VillageResponseChoiceSaveData
            {
                PatternKey = c.PatternKey,
                PersonId = c.PersonId,
                Choice = (int)c.Choice,
                Year = c.Year,
                Month = c.Month,
                Day = c.Day
            });
        }

        var rests = new List<VillageRestSaveData>();
        foreach (var r in _rests)
        {
            if (r == null) continue;
            rests.Add(new VillageRestSaveData
            {
                PersonId = r.PersonId,
                UntilYear = r.UntilYear,
                UntilMonth = r.UntilMonth,
                UntilDay = r.UntilDay
            });
        }

        return new VillageSocialResponseSaveData
        {
            Fired = fired,
            Pending = pending,
            Choices = choices,
            Rests = rests
        };
    }

    /// <summary>
    /// 从存档恢复（旧档缺失 → 空默认）。防御：
    ///   - 空 PatternKey / PersonId / 非法 Year → 丢弃 fired；
    ///   - fired 重复（同 Pattern/Person/Year）→ 只保留首条；
    ///   - pending 只恢复一条；空字段 → 丢弃；
    ///   - choice 未知枚举序 / 非法日期 → 丢弃；
    ///   - rest 空 PersonId / 非法日期 → 丢弃。
    /// </summary>
    public void Restore(VillageSocialResponseSaveData data)
    {
        _fired.Clear();
        _choices.Clear();
        _rests.Clear();
        _pending = null;
        _nextEventSeq = 0;

        if (data == null) return;

        if (data.Fired != null)
        {
            foreach (var f in data.Fired)
            {
                if (f == null) continue;
                if (string.IsNullOrEmpty(f.PatternKey) || string.IsNullOrEmpty(f.PersonId) || f.Year < 1) continue;
                if (IsFired(f.PatternKey, f.PersonId, f.Year)) continue;
                _fired.Add(new VillageSocialFired { PatternKey = f.PatternKey, PersonId = f.PersonId, Year = f.Year });
            }
        }

        if (data.Pending != null && _pending == null)
        {
            var p = data.Pending;
            if (!string.IsNullOrEmpty(p.EventId) && !string.IsNullOrEmpty(p.PatternKey)
                && !string.IsNullOrEmpty(p.PersonId) && p.Year >= 1)
            {
                _pending = new VillagePendingResponse
                {
                    EventId = p.EventId,
                    PatternKey = p.PatternKey,
                    PersonId = p.PersonId,
                    Year = p.Year
                };
            }
        }

        if (data.Choices != null)
        {
            foreach (var c in data.Choices)
            {
                if (c == null) continue;
                if (string.IsNullOrEmpty(c.PatternKey) || string.IsNullOrEmpty(c.PersonId)) continue;
                if (!System.Enum.IsDefined(typeof(VillageSocialResponseChoice), c.Choice)) continue;
                if (!IsValidDate(c.Year, c.Month, c.Day)) continue;
                _choices.Add(new VillageResponseChoiceRecord
                {
                    PatternKey = c.PatternKey,
                    PersonId = c.PersonId,
                    Choice = (VillageSocialResponseChoice)c.Choice,
                    Year = c.Year,
                    Month = c.Month,
                    Day = c.Day
                });
            }
        }

        if (data.Rests != null)
        {
            foreach (var r in data.Rests)
            {
                if (r == null) continue;
                if (string.IsNullOrEmpty(r.PersonId)) continue;
                if (!IsValidDate(r.UntilYear, r.UntilMonth, r.UntilDay)) continue;
                _rests.Add(new VillageRestRecord
                {
                    PersonId = r.PersonId,
                    UntilYear = r.UntilYear,
                    UntilMonth = r.UntilMonth,
                    UntilDay = r.UntilDay
                });
            }
        }
    }

    /// <summary>
    /// 【Test / Not Frozen】最小 Urgency 语义判定：该 Urgency 是否属于「可忽略 / 非强制处理」。
    /// 只做语义区分；不产生忽略按钮、不计惩罚、不改变 pending 生命周期。
    /// </summary>
    public static bool IsIgnorable(VillageSocialUrgency urgency)
    {
        return urgency == VillageSocialUrgency.Ignorable;
    }

    private static long DateSequence(int year, int month, int day)
    {
        return year * 10000L + month * 100L + day;
    }

    private static bool IsValidDate(int year, int month, int day)
    {
        if (year < 1 || month < 1 || month > 12 || day < 1) return false;
        return day <= VillageTime.DaysInMonth(year, month);
    }
}
