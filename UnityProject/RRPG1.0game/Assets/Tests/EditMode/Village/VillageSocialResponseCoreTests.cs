using NUnit.Framework;

/// <summary>
/// V3B VillageSocialResponseLog 纯逻辑测试（Village.Runtime 强类型）。
/// 覆盖：阈值判定 / fired 防重复 / 单条 pending / Choice A·B /
///      月末 Rest Gate 边界（含当天仍拦、下月 1 日恢复）/ 跨年 / Save→Load DTO 往返 / 防御恢复。
/// 阈值与窗口只引用 VillageTestSocialPatterns（Test / Not Frozen），不在此写死产品数值。
/// </summary>
public class VillageSocialResponseCoreTests
{
    private const string Key = VillageTestSocialPatterns.BuildupDraftPatternKey;

    private static VillageSocialResponseLog NewLog()
    {
        return new VillageSocialResponseLog();
    }

    [Test]
    public void BelowThreshold_NoEvent_NoFired()
    {
        var l = NewLog();
        Assert.AreEqual(VillageResponseOpenResult.BelowThreshold,
            l.OpenBuildupResponse(Key, "char_001", 1, VillageTestSocialPatterns.BuildupDraftThreshold, 2));
        Assert.IsFalse(l.HasPendingResponse);
        Assert.IsFalse(l.IsFired(Key, "char_001", 1));
    }

    [Test]
    public void AtThreshold_EventCreated_FiredWritten_PendingVisible()
    {
        var l = NewLog();
        Assert.AreEqual(VillageResponseOpenResult.Created,
            l.OpenBuildupResponse(Key, "char_001", 1, VillageTestSocialPatterns.BuildupDraftThreshold, 3));
        Assert.IsTrue(l.HasPendingResponse);
        Assert.IsNotNull(l.PendingResponse);
        Assert.AreEqual("char_001", l.PendingResponse.PersonId);
        Assert.AreEqual(1, l.PendingResponse.Year);
        Assert.AreEqual(Key, l.PendingResponse.PatternKey);
        Assert.IsTrue(l.IsFired(Key, "char_001", 1), "创建 Event 时应立即写 fired（防重）");
    }

    [Test]
    public void SameWindowRepeat_AlreadyFired_NoSecondEvent()
    {
        var l = NewLog();
        l.OpenBuildupResponse(Key, "char_001", 1, VillageTestSocialPatterns.BuildupDraftThreshold, 3);
        l.ResolvePending(VillageSocialResponseChoice.GrantRest, 1, 1, 5);

        // 同人同年再达阈值：即使已 resolve 也不得再触发（fired 持久）
        Assert.AreEqual(VillageResponseOpenResult.AlreadyFired,
            l.OpenBuildupResponse(Key, "char_001", 1, VillageTestSocialPatterns.BuildupDraftThreshold, 4));
        Assert.IsFalse(l.HasPendingResponse);
        Assert.AreEqual(1, l.Choices.Count);
    }

    [Test]
    public void PendingExists_BlocksSecondEvent_DifferentPerson()
    {
        var l = NewLog();
        Assert.AreEqual(VillageResponseOpenResult.Created,
            l.OpenBuildupResponse(Key, "char_001", 1, VillageTestSocialPatterns.BuildupDraftThreshold, 3));
        Assert.AreEqual(VillageResponseOpenResult.PendingResponseExists,
            l.OpenBuildupResponse(Key, "char_002", 1, VillageTestSocialPatterns.BuildupDraftThreshold, 3),
            "V3B 至多一条 pending：其它 Person 触发时暂不建第二条");
        Assert.IsFalse(l.IsFired(Key, "char_002", 1), "未创建 Event 的 Person 不应写 fired（保留未来触发机会）");
    }

    [Test]
    public void CrossYear_NewWindowCanFireAgain()
    {
        var l = NewLog();
        l.OpenBuildupResponse(Key, "char_001", 1, VillageTestSocialPatterns.BuildupDraftThreshold, 3);
        l.ResolvePending(VillageSocialResponseChoice.GrantRest, 1, 1, 5);
        Assert.AreEqual(VillageResponseOpenResult.AlreadyFired,
            l.OpenBuildupResponse(Key, "char_001", 1, VillageTestSocialPatterns.BuildupDraftThreshold, 5));
        Assert.AreEqual(VillageResponseOpenResult.Created,
            l.OpenBuildupResponse(Key, "char_001", 2, VillageTestSocialPatterns.BuildupDraftThreshold, 3),
            "窗口=自然年（Test）：跨年可重新触发");
    }

    [Test]
    public void GrantRest_WritesChoice_AndRestUntil_EndOfMonth()
    {
        var l = NewLog();
        l.OpenBuildupResponse(Key, "char_001", 1, VillageTestSocialPatterns.BuildupDraftThreshold, 3);
        Assert.IsTrue(l.ResolvePending(VillageSocialResponseChoice.GrantRest, 1, 2, 10));

        Assert.IsFalse(l.HasPendingResponse);
        Assert.AreEqual(1, l.Choices.Count);
        Assert.AreEqual(VillageSocialResponseChoice.GrantRest, l.Choices[0].Choice);
        Assert.AreEqual(1, l.Choices[0].Year);
        Assert.AreEqual(2, l.Choices[0].Month);
        Assert.AreEqual(10, l.Choices[0].Day);

        var rest = l.GetLatestRest("char_001");
        Assert.IsNotNull(rest);
        Assert.AreEqual(1, rest.UntilYear);
        Assert.AreEqual(2, rest.UntilMonth);
        Assert.AreEqual(VillageTime.DaysInMonth(1, 2), rest.UntilDay, "RestUntil = 当月末最后一天");
    }

    [Test]
    public void RestGate_MonthEndBoundary_LastDayBlocked_NextMonthFirstDayFree()
    {
        var l = NewLog();
        // 1 月 10 日 GrantRest → until = 1/31
        l.OpenBuildupResponse(Key, "char_001", 1, VillageTestSocialPatterns.BuildupDraftThreshold, 3);
        Assert.IsTrue(l.ResolvePending(VillageSocialResponseChoice.GrantRest, 1, 1, 10));

        Assert.IsTrue(l.IsPersonRestBlocked("char_001", 1, 1, 30), "月末前仍拦截");
        Assert.IsTrue(l.IsPersonRestBlocked("char_001", 1, 1, 31), "月末最后一天（Until 当天）仍拦截");
        Assert.IsFalse(l.IsPersonRestBlocked("char_001", 1, 2, 1), "下月 1 日（CurrentDate > Until）恢复");
        Assert.IsFalse(l.IsPersonRestBlocked("char_001", 2, 1, 1), "远月自然恢复");
        Assert.IsFalse(l.IsPersonRestBlocked("char_002", 1, 1, 31), "其它 Person 不受影响");
    }

    [Test]
    public void RestGate_MonthEndBoundary_CrossYear_Month12FreeOnNextYearDay1()
    {
        var l = NewLog();
        l.OpenBuildupResponse(Key, "char_001", 1, VillageTestSocialPatterns.BuildupDraftThreshold, 3);
        Assert.IsTrue(l.ResolvePending(VillageSocialResponseChoice.GrantRest, 1, 12, 20));
        Assert.IsTrue(l.IsPersonRestBlocked("char_001", 1, 12, 31), "12/31（Until 当天）仍拦截");
        Assert.IsFalse(l.IsPersonRestBlocked("char_001", 2, 1, 1), "次年 1 月 1 日恢复");
    }

    [Test]
    public void ContinueUsing_NoRest_OnlyChoiceHistory()
    {
        var l = NewLog();
        l.OpenBuildupResponse(Key, "char_001", 1, VillageTestSocialPatterns.BuildupDraftThreshold, 3);
        Assert.IsTrue(l.ResolvePending(VillageSocialResponseChoice.ContinueUsing, 1, 1, 5));

        Assert.IsFalse(l.HasPendingResponse);
        Assert.AreEqual(1, l.Choices.Count);
        Assert.AreEqual(VillageSocialResponseChoice.ContinueUsing, l.Choices[0].Choice);
        Assert.IsNull(l.GetLatestRest("char_001"), "Choice B 不产生 Rest Gate");
        Assert.IsFalse(l.IsPersonRestBlocked("char_001", 1, 1, 6));
        Assert.IsTrue(l.IsFired(Key, "char_001", 1), "fired 仍生效：同窗口不重复骚扰");
    }

    [Test]
    public void SaveRoundTrip_ToSaveDataRestore_PreservesAll()
    {
        var l = NewLog();
        l.OpenBuildupResponse(Key, "char_001", 1, VillageTestSocialPatterns.BuildupDraftThreshold, 3);
        Assert.IsTrue(l.ResolvePending(VillageSocialResponseChoice.GrantRest, 1, 1, 10));
        l.OpenBuildupResponse(Key, "char_002", 1, VillageTestSocialPatterns.BuildupDraftThreshold, 3);

        var l2 = NewLog();
        l2.Restore(l.ToSaveData());

        Assert.AreEqual(2, l2.Fired.Count);
        Assert.IsTrue(l2.HasPendingResponse);
        Assert.AreEqual("char_002", l2.PendingResponse.PersonId);
        Assert.AreEqual(1, l2.Choices.Count);
        Assert.AreEqual(VillageSocialResponseChoice.GrantRest, l2.Choices[0].Choice);
        var rest = l2.GetLatestRest("char_001");
        Assert.IsNotNull(rest);
        Assert.AreEqual(31, rest.UntilDay);
        Assert.IsTrue(l2.IsPersonRestBlocked("char_001", 1, 1, 31));
        Assert.IsFalse(l2.IsPersonRestBlocked("char_001", 1, 2, 1));
    }

    [Test]
    public void Restore_Null_DefaultsEmpty()
    {
        var l = NewLog();
        l.OpenBuildupResponse(Key, "char_001", 1, VillageTestSocialPatterns.BuildupDraftThreshold, 3);
        Assert.IsTrue(l.ResolvePending(VillageSocialResponseChoice.GrantRest, 1, 1, 10));

        l.Restore(null);
        Assert.AreEqual(0, l.Fired.Count);
        Assert.AreEqual(0, l.Choices.Count);
        Assert.AreEqual(0, l.Rests.Count);
        Assert.IsFalse(l.HasPendingResponse);
        Assert.IsFalse(l.IsFired(Key, "char_001", 1));
        Assert.IsFalse(l.IsPersonRestBlocked("char_001", 1, 1, 20));
    }

    [Test]
    public void Restore_Defensive_DropsInvalid_DeduplicatesFired_KeepsSinglePending()
    {
        var data = new VillageSocialResponseSaveData
        {
            Fired = new System.Collections.Generic.List<VillageSocialFiredSaveData>
            {
                new VillageSocialFiredSaveData { PatternKey = Key, PersonId = "char_a", Year = 1 },
                new VillageSocialFiredSaveData { PatternKey = Key, PersonId = "char_a", Year = 1 },
                new VillageSocialFiredSaveData { PatternKey = "", PersonId = "char_b", Year = 1 },
                new VillageSocialFiredSaveData { PatternKey = Key, PersonId = "char_b", Year = 0 }
            },
            Pending = new VillagePendingResponseSaveData
            {
                EventId = "e1", PatternKey = Key, PersonId = "char_p", Year = 1
            },
            Choices = new System.Collections.Generic.List<VillageResponseChoiceSaveData>
            {
                new VillageResponseChoiceSaveData { PatternKey = Key, PersonId = "char_c", Choice = 0, Year = 1, Month = 1, Day = 1 },
                new VillageResponseChoiceSaveData { PatternKey = Key, PersonId = "char_c", Choice = 99, Year = 1, Month = 1, Day = 1 }
            },
            Rests = new System.Collections.Generic.List<VillageRestSaveData>
            {
                new VillageRestSaveData { PersonId = "char_d", UntilYear = 1, UntilMonth = 1, UntilDay = 31 },
                new VillageRestSaveData { PersonId = "", UntilYear = 1, UntilMonth = 1, UntilDay = 31 },
                new VillageRestSaveData { PersonId = "char_e", UntilYear = 1, UntilMonth = 2, UntilDay = 30 }
            }
        };

        var l = NewLog();
        l.Restore(data);

        Assert.AreEqual(1, l.Fired.Count, "重复 fired 只保留首条；非法行丢弃");
        Assert.IsTrue(l.HasPendingResponse);
        Assert.AreEqual("char_p", l.PendingResponse.PersonId);
        Assert.AreEqual(1, l.Choices.Count, "未知 Choice 枚举序应被丢弃");
        Assert.AreEqual(1, l.Rests.Count, "空 PersonId / 非法日期 rest 应被丢弃");
        Assert.AreEqual("char_d", l.Rests[0].PersonId);
    }
}
