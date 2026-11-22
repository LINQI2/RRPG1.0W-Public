using System.Collections.Generic;
using NUnit.Framework;

/// <summary>
/// 【Long-term Regression / V3C】Village 社会回应「只读回看 / Rest 可见性 / Urgency·PauseOnOpen 策略」
/// 的最小契约保护（Village.Runtime 强类型，不依赖 Assembly-CSharp）。
///
/// V3C 开发与人工验收期间的一次性专项证据已在收尾时删除；本文件只保留未来重构绝不能破坏的不变量：
///   - 只回看真实发生过的 Choice / Rest（fired 是防重复技术事实，不得当作人生历史）；
///   - Rest 可见性必须复用 Rest 权威规则（不在 View 内复制规则）+ 三态（窗口内 / 截止日当天 / 次月 1 日恢复）；
///   - PersonId 无显示名时回退 PersonId（禁止猜 Person）；
///   - 未传入 Rest 权威时不改变既有调用方行为；
///   - Urgency 两种最小区分 + PauseOnOpen 策略（Ignorable 恒不强制暂停）。
/// 全部走真实 Runtime 路径（OpenBuildupResponse → ResolvePending）；文案与阈值均 Test / Not Frozen。
///
/// 保留理由（Closeout 判定）：本文件保护的是「单一真相源 / 禁止猜 Person / 后果规则不在 View 复制」
/// 三类不变量，属未来重构也绝不能破坏的契约；V3C 其余一次性开发与验收证据已在收尾时删除。
/// </summary>
public class VillageV3CSocialHistoryViewTests
{
    // ==================== helpers ====================

    /// <summary>走真实路径建立一次 Recognition 回应并解析（GrantRest 时由 Choice 产生 Rest）。</summary>
    private static void ResolveOne(VillageSocialResponseLog log, string personId, int year,
        int month, int day, VillageSocialResponseChoice choice)
    {
        Assert.AreEqual(VillageResponseOpenResult.Created,
            log.OpenBuildupResponse(VillageTestSocialPatterns.BuildupDraftPatternKey, personId, year,
                VillageTestSocialPatterns.BuildupDraftThreshold, VillageTestSocialPatterns.BuildupDraftThreshold),
            "真实路径应能建立 Recognition 回应");
        Assert.IsTrue(log.ResolvePending(choice, year, month, day), "解析 pending 应成功");
    }

    private static VillagePersonView View(string id, string name)
    {
        return new VillagePersonView { PersonId = id, DisplayName = name, Availability = VillageAvailability.InVillage };
    }

    private static Dictionary<string, string> Names(params string[] ids)
    {
        var map = new Dictionary<string, string>();
        foreach (var id in ids) map[id] = "名字_" + id;
        return map;
    }

    // ==================== R1：只读回看合成 ====================

    [Test]
    public void SocialHistory_FiredOnly_IsNotPresentedAsHistory()
    {
        // fired 只是防重复技术事实：建立 pending（写 fired）但尚未解析 → 不得产生“历史”行。
        var log = new VillageSocialResponseLog();
        Assert.AreEqual(VillageResponseOpenResult.Created,
            log.OpenBuildupResponse(VillageTestSocialPatterns.BuildupDraftPatternKey, "p1", 1,
                VillageTestSocialPatterns.BuildupDraftThreshold, VillageTestSocialPatterns.BuildupDraftThreshold));
        Assert.IsTrue(log.IsFired(VillageTestSocialPatterns.BuildupDraftPatternKey, "p1", 1), "fired 应已写入");

        var rows = VillageCommitmentView.BuildRecentSocialHistoryRows(log, Names("p1"), 8);
        Assert.AreEqual(0, rows.Count, "fired / pending 不得被当作人生历史呈现");
    }

    [Test]
    public void SocialHistory_ChoiceRows_NewestFirst_AndCarryDecisionText()
    {
        // 注意：fired 规则 = 同一 Pattern/Person/自然年只建立一次回应 → 每个 Person 各用一次
        var log = new VillageSocialResponseLog();
        ResolveOne(log, "p1", 1, 2, 5, VillageSocialResponseChoice.ContinueUsing);
        ResolveOne(log, "p2", 1, 3, 9, VillageSocialResponseChoice.ContinueUsing);
        ResolveOne(log, "p3", 1, 3, 12, VillageSocialResponseChoice.ContinueUsing);

        var rows = VillageCommitmentView.BuildRecentSocialHistoryRows(log, Names("p1", "p2", "p3"), 8);

        Assert.AreEqual(3, rows.Count);
        Assert.AreEqual("p3", rows[0].PersonId);
        Assert.AreEqual(3, rows[0].Month);
        Assert.AreEqual(12, rows[0].Day, "最近一次选择应排在最前");
        StringAssert.Contains("回应选择", rows[0].DetailText);
        StringAssert.Contains("继续承担临时任务", rows[0].DetailText, "方向文案应可读且中性");
        StringAssert.Contains("名字_p3", rows[0].DetailText, "应使用显示名");

        Assert.AreEqual("p2", rows[1].PersonId);
        Assert.AreEqual(9, rows[1].Day);
        Assert.AreEqual("p1", rows[2].PersonId);
        Assert.AreEqual(5, rows[2].Day, "应严格按日期降序");
    }

    [Test]
    public void SocialHistory_GrantRest_ProducesReadableChoiceAndRestWindowRows()
    {
        var log = new VillageSocialResponseLog();
        ResolveOne(log, "p1", 1, 3, 10, VillageSocialResponseChoice.GrantRest);

        var rows = VillageCommitmentView.BuildRecentSocialHistoryRows(log, Names("p1"), 8);
        Assert.AreEqual(2, rows.Count, "Choice 行 + Rest 行");

        var choice = rows.Find(r => r.Kind == VillageCommitmentView.SocialHistoryKind.Choice);
        Assert.IsNotNull(choice);
        StringAssert.Contains("恢复窗口", choice.DetailText);

        var rest = rows.Find(r => r.Kind == VillageCommitmentView.SocialHistoryKind.RestWindow);
        Assert.IsNotNull(rest);
        Assert.AreEqual(31, rest.Day, "Rest 行日期 = 恢复窗口截止日（当月末）");
        StringAssert.Contains("恢复窗口", rest.DetailText);
        StringAssert.Contains("3月31日", rest.DetailText);
    }

    [Test]
    public void SocialHistory_UnknownPersonId_FallsBackToPersonId()
    {
        var log = new VillageSocialResponseLog();
        ResolveOne(log, "p_unknown", 1, 4, 2, VillageSocialResponseChoice.ContinueUsing);

        // 名字映射里没有该 PersonId → 回退显示 PersonId（禁止猜 Person）
        var rows = VillageCommitmentView.BuildRecentSocialHistoryRows(log, Names("p_other"), 8);
        Assert.AreEqual(1, rows.Count);
        Assert.AreEqual("p_unknown", rows[0].DisplayName);
        StringAssert.Contains("p_unknown", rows[0].DetailText);
    }

    // ==================== R2：Rest 主动可见三态 ====================

    [Test]
    public void BuildPersonRows_WithoutRestInput_KeepsLegacyBehaviour()
    {
        var views = new List<VillagePersonView> { View("p1", "甲") };
        var rows = VillageCommitmentView.BuildPersonRows(views, null, null);

        Assert.AreEqual(1, rows.Count);
        Assert.IsFalse(rows[0].IsRestActive, "未传入 Rest 权威时应保持无 Rest 信息（既有调用方行为不变）");
        Assert.IsTrue(string.IsNullOrEmpty(rows[0].RestText));
    }

    [Test]
    public void RestVisibility_InsideWindow_LastDayStillVisible_RecoversNextMonthFirstDay()
    {
        var log = new VillageSocialResponseLog();
        ResolveOne(log, "p1", 1, 3, 10, VillageSocialResponseChoice.GrantRest);   // Until = 1年3月31日

        var views = new List<VillagePersonView> { View("p1", "甲"), View("p2", "乙") };

        // ① 窗口内可见
        var inside = VillageCommitmentView.BuildPersonRows(views, null, null, log, 1, 3, 10);
        var insideRow = inside.Find(r => r.PersonId == "p1");
        Assert.IsTrue(insideRow.IsRestActive, "恢复窗口内应可见");
        StringAssert.Contains("恢复中", insideRow.RestText);
        StringAssert.Contains("1年3月31日", insideRow.RestText);
        Assert.IsFalse(inside.Find(r => r.PersonId == "p2").IsRestActive, "未选择恢复的 Person 不受影响");

        // ② 截止日当天仍可见
        var lastDay = VillageCommitmentView.BuildPersonRows(views, null, null, log, 1, 3, 31);
        Assert.IsTrue(lastDay.Find(r => r.PersonId == "p1").IsRestActive, "截止日当天仍应可见（仍被拦截）");

        // ③ 次月 1 日恢复 → 消失
        var nextMonth = VillageCommitmentView.BuildPersonRows(views, null, null, log, 1, 4, 1);
        Assert.IsFalse(nextMonth.Find(r => r.PersonId == "p1").IsRestActive, "下月 1 日恢复后不应再显示");
        Assert.IsTrue(string.IsNullOrEmpty(nextMonth.Find(r => r.PersonId == "p1").RestText));

        // 可见性必须与 Rest 权威规则一致（不得在 View 内复制规则）
        Assert.AreEqual(log.IsPersonRestBlocked("p1", 1, 3, 20), insideRow.IsRestActive);
    }

    // ==================== R3：Urgency / PauseOnOpen 最小语义 ====================

    [Test]
    public void Urgency_CaseA_IsTimely_AndUnknownPatternKeyIsSafeDefault()
    {
        Assert.AreEqual(VillageSocialUrgency.Timely, VillageTestSocialPatterns.BuildupDraftUrgency,
            "当前 Case A 使用 Timely（需要及时处理）");
        Assert.AreEqual(VillageSocialUrgency.Timely,
            VillageTestSocialPatterns.ResolveUrgency(VillageTestSocialPatterns.BuildupDraftPatternKey));
        Assert.AreEqual(VillageSocialUrgency.Timely,
            VillageTestSocialPatterns.ResolveUrgency("NotRegisteredPattern"),
            "未知 PatternKey → 安全默认 Timely（不得静默降级为可忽略）");
        Assert.AreEqual(VillageSocialUrgency.Timely, VillageTestSocialPatterns.ResolveUrgency(null));
    }

    [Test]
    public void UrgencyAndPauseOnOpen_MinimalContract()
    {
        // 两种最小语义可区分 + 默认值由 Pattern 目录唯一承载
        Assert.IsFalse(VillageSocialResponseLog.IsIgnorable(VillageSocialUrgency.Timely));
        Assert.IsTrue(VillageSocialResponseLog.IsIgnorable(VillageSocialUrgency.Ignorable));
        Assert.IsTrue(VillageTestSocialPatterns.DefaultPauseOnOpen,
            "PauseOnOpen 默认值（Test / Not Frozen）= true，由 Pattern 目录唯一承载");

        Assert.IsTrue(VillageTestSocialPatterns.ShouldPauseOnOpen(true, VillageSocialUrgency.Timely),
            "Timely + 配置 true → 打开应暂停");
        Assert.IsFalse(VillageTestSocialPatterns.ShouldPauseOnOpen(false, VillageSocialUrgency.Timely),
            "Timely + 配置 false → 打开不暂停");
        Assert.IsFalse(VillageTestSocialPatterns.ShouldPauseOnOpen(true, VillageSocialUrgency.Ignorable),
            "Ignorable 恒不强制暂停（即使配置 true）");
        Assert.IsFalse(VillageTestSocialPatterns.ShouldPauseOnOpen(false, VillageSocialUrgency.Ignorable));
    }
}
