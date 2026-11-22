using System.Collections.Generic;

/// <summary>
/// **VillagePersonRestState（人物恢复期权威）** —— WP-Event-1D §7 正式抽离。
///
/// 为什么需要它：`VillageSocialResponseLog` 把 fired / pending / ChoiceHistory / RestUntil 四类状态捆在一起，
/// 那是 V3B「Recognition → 单一 Response」旧流程的产物；Unified Event Core 接管事件之后，
/// 「某人物当前是否处于恢复窗口」是**独立于事件载体**的长期状态，必须有自己的 Authority。
///
/// 职责（且仅此）：
///   - 记录 Person → RestUntil（日期）；
///   - Rest Gate 查询：`IsRestBlocked`（含 Until 当天仍拦截，CurrentDate &gt; Until 才解除）；
///   - 单调性：`GrantRestUntil` **绝不缩短**已有恢复期（只追加，查询取最新 Until）；
///   - Save / Restore（复用既有 `VillageRestSaveData` DTO，不新增第二套存档形态）；
///   - 旧档迁移：`MigrateFromLegacy`（幂等、不猜人物、不缩短）。
///
/// 边界：
///   - 只服务「把该 Person 新加入 Project 的调度」这一条现有 Rest Gate；不影响 Expedition / Active Project /
///     Assignment / Person 长期状态；
///   - 不保存 ChoiceHistory / fired / pending（那些属各自 Authority 或已退出正式路径）；
///   - 数值语义仍 **Test / Not Frozen**（当前 V0.1：休整到当月最后一天）。
/// </summary>
public class VillagePersonRestState
{
    private readonly List<VillageRestRecord> _rests = new List<VillageRestRecord>();

    /// <summary>全部恢复期记录（只读视图；顺序 = 写入顺序）。</summary>
    public IReadOnlyList<VillageRestRecord> Rests => _rests;

    /// <summary>
    /// 授予恢复期至指定日期（含当天仍拦截）。
    /// 语义：**只追加**；若已存在更晚（或相同）的 Until，本次调用不缩短任何东西（查询始终取最新 Until）。
    /// 日期非法 / PersonId 为空 → false（不写入、不猜值）。
    /// </summary>
    public bool GrantRestUntil(string personId, int untilYear, int untilMonth, int untilDay)
    {
        if (string.IsNullOrEmpty(personId)) return false;
        if (!IsValidDate(untilYear, untilMonth, untilDay)) return false;

        _rests.Add(new VillageRestRecord
        {
            PersonId = personId,
            UntilYear = untilYear,
            UntilMonth = untilMonth,
            UntilDay = untilDay
        });
        return true;
    }

    /// <summary>
    /// Rest Gate：该 Person 当前是否处于恢复窗口（被拦截状态）。
    /// 规则（与 V3B 旧实现同公式，迁移不改变玩家可见行为）：当前日期 &lt;= Until → 拦截。
    /// </summary>
    public bool IsRestBlocked(string personId, int currentYear, int currentMonth, int currentDay)
    {
        var rest = GetLatestRest(personId);
        if (rest == null) return false;

        return DateSequence(currentYear, currentMonth, currentDay)
            <= DateSequence(rest.UntilYear, rest.UntilMonth, rest.UntilDay);
    }

    public bool HasRest(string personId)
    {
        return GetLatestRest(personId) != null;
    }

    /// <summary>最新（Until 最晚）的一条恢复期记录（无则 null）。</summary>
    public VillageRestRecord GetLatestRest(string personId)
    {
        if (string.IsNullOrEmpty(personId)) return null;

        VillageRestRecord best = null;
        foreach (var rest in _rests)
        {
            if (rest == null || rest.PersonId != personId) continue;
            if (best == null)
            {
                best = rest;
                continue;
            }

            if (DateSequence(rest.UntilYear, rest.UntilMonth, rest.UntilDay)
                > DateSequence(best.UntilYear, best.UntilMonth, best.UntilDay))
            {
                best = rest;
            }
        }
        return best;
    }

    // ==================== Save / Restore ====================

    public List<VillageRestSaveData> ToSaveData()
    {
        var list = new List<VillageRestSaveData>();
        foreach (var rest in _rests)
        {
            if (rest == null) continue;
            list.Add(new VillageRestSaveData
            {
                PersonId = rest.PersonId,
                UntilYear = rest.UntilYear,
                UntilMonth = rest.UntilMonth,
                UntilDay = rest.UntilDay
            });
        }
        return list;
    }

    /// <summary>从存档恢复（null / 空 → 空状态；非法记录丢弃，不抛异常）。</summary>
    public void Restore(List<VillageRestSaveData> data)
    {
        _rests.Clear();
        if (data == null) return;

        foreach (var record in data)
        {
            if (record == null) continue;
            if (string.IsNullOrEmpty(record.PersonId)) continue;
            if (!IsValidDate(record.UntilYear, record.UntilMonth, record.UntilDay)) continue;

            _rests.Add(new VillageRestRecord
            {
                PersonId = record.PersonId,
                UntilYear = record.UntilYear,
                UntilMonth = record.UntilMonth,
                UntilDay = record.UntilDay
            });
        }
    }

    /// <summary>
    /// **旧档迁移**（WP-Event-1D §8）：把 `VillageSocialResponseSaveData.Rests` 迁移到本 Authority。
    ///
    /// 硬要求：
    ///   - **幂等**：同 (PersonId, Until) 已存在 → 跳过；
    ///   - **不缩短**：只追加，绝不删除 / 覆盖更晚的 Until；
    ///   - **不猜 Person**：空 PersonId 直接丢弃（不按 Pattern / DisplayName 猜）。
    ///
    /// 返回真正迁移的记录数（0 = 无旧数据或全部已迁移）。
    /// </summary>
    public int MigrateFromLegacy(List<VillageRestSaveData> legacyRests)
    {
        if (legacyRests == null) return 0;

        int migrated = 0;
        foreach (var record in legacyRests)
        {
            if (record == null) continue;
            if (string.IsNullOrEmpty(record.PersonId)) continue;
            if (!IsValidDate(record.UntilYear, record.UntilMonth, record.UntilDay)) continue;
            if (HasSameRest(record.PersonId, record.UntilYear, record.UntilMonth, record.UntilDay)) continue;

            _rests.Add(new VillageRestRecord
            {
                PersonId = record.PersonId,
                UntilYear = record.UntilYear,
                UntilMonth = record.UntilMonth,
                UntilDay = record.UntilDay
            });
            migrated++;
        }
        return migrated;
    }

    private bool HasSameRest(string personId, int year, int month, int day)
    {
        foreach (var rest in _rests)
        {
            if (rest == null || rest.PersonId != personId) continue;
            if (rest.UntilYear == year && rest.UntilMonth == month && rest.UntilDay == day) return true;
        }
        return false;
    }

    public void Reset()
    {
        _rests.Clear();
    }

    private static bool IsValidDate(int year, int month, int day)
    {
        if (year < 1 || month < 1 || month > 12) return false;
        return day >= 1 && day <= VillageTime.DaysInMonth(year, month);
    }

    /// <summary>单调日期序数（与 V3B 旧实现同公式；只做比较，不做日历发明）。</summary>
    private static long DateSequence(int year, int month, int day)
    {
        long total = 0;
        for (int y = 1; y < year; y++)
        {
            total += VillageTime.IsLeapYear(y) ? 366 : 365;
        }
        for (int m = 1; m < month; m++)
        {
            total += VillageTime.DaysInMonth(year, m);
        }
        return total + day;
    }
}
