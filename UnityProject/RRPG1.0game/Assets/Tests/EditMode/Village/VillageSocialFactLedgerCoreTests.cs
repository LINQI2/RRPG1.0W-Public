using System.Collections.Generic;
using NUnit.Framework;

/// <summary>
/// V3A Social Fact Ledger 纯逻辑测试（Village.Runtime 强类型）。
/// 覆盖：事实追加 / 同动作幂等 / 年度计数 / 跨年边界 / Save→Load DTO 往返 /
///      旧档空默认 / 非法记录防御。
/// 阈值语义：本测试只断言「计数是否正确 / 记录点是否幂等」，不建立任何
/// 「第 N 次 = Recognition」规则（阈值只允许存在于 Test / Not Frozen 测试）。
/// </summary>
public class VillageSocialFactLedgerCoreTests
{
    private static VillageSocialFactLedger NewLedger()
    {
        return new VillageSocialFactLedger();
    }

    [Test]
    public void RecordFirstFact_Success_AndVisible()
    {
        var l = NewLedger();
        Assert.AreEqual(VillageSocialFactResult.Success,
            l.RecordTemporaryCommitment("char_001", "prj_test_01", "prj_test_01_1", 1, 1, 1));
        Assert.AreEqual(1, l.Facts.Count);
        var f = l.Facts[0];
        Assert.AreEqual(VillageSocialFactType.TemporaryCommitment, f.Type);
        Assert.AreEqual("char_001", f.PersonId);
        Assert.AreEqual("prj_test_01", f.ProjectDefId);
        Assert.AreEqual("prj_test_01_1", f.ProjectId);
        Assert.AreEqual(1, f.Year);
        Assert.AreEqual(1, f.Month);
        Assert.AreEqual(1, f.Day);
    }

    [Test]
    public void SameAction_Repeated_IsDuplicate_Idempotent()
    {
        var l = NewLedger();
        Assert.AreEqual(VillageSocialFactResult.Success,
            l.RecordTemporaryCommitment("char_001", "prj_test_01", "prj_test_01_1", 1, 1, 1));
        Assert.AreEqual(VillageSocialFactResult.Duplicate,
            l.RecordTemporaryCommitment("char_001", "prj_test_01", "prj_test_01_1", 1, 1, 1),
            "同一 Person 对同一 Project 实例重复加入必须幂等");
        Assert.AreEqual(1, l.Facts.Count, "重复记录不得追加第二条事实");
    }

    [Test]
    public void DifferentProjects_EachCountedSeparately()
    {
        var l = NewLedger();
        Assert.AreEqual(VillageSocialFactResult.Success,
            l.RecordTemporaryCommitment("char_001", "prj_test_01", "prj_test_01_1", 1, 1, 1));
        Assert.AreEqual(VillageSocialFactResult.Success,
            l.RecordTemporaryCommitment("char_001", "prj_test_02", "prj_test_02_1", 1, 3, 5));
        Assert.AreEqual(2, l.Facts.Count, "不同 Project 实例是两次独立 Temporary Commitment");
    }

    [Test]
    public void InvalidPersonOrProject_Rejected_NoFactAppended()
    {
        var l = NewLedger();
        Assert.AreEqual(VillageSocialFactResult.InvalidPersonId,
            l.RecordTemporaryCommitment("", "prj_test_01", "p", 1, 1, 1));
        Assert.AreEqual(VillageSocialFactResult.InvalidPersonId,
            l.RecordTemporaryCommitment(null, "prj_test_01", "p", 1, 1, 1));
        Assert.AreEqual(VillageSocialFactResult.InvalidProjectId,
            l.RecordTemporaryCommitment("char_001", "", "p", 1, 1, 1));
        Assert.AreEqual(VillageSocialFactResult.InvalidProjectId,
            l.RecordTemporaryCommitment("char_001", "d", null, 1, 1, 1));
        Assert.AreEqual(0, l.Facts.Count);
    }

    [Test]
    public void InvalidDates_Rejected_LeapYearDay_Accepted()
    {
        var l = NewLedger();
        Assert.AreEqual(VillageSocialFactResult.InvalidDate,
            l.RecordTemporaryCommitment("char_001", "d", "p1", 0, 1, 1), "Year<1 非法");
        Assert.AreEqual(VillageSocialFactResult.InvalidDate,
            l.RecordTemporaryCommitment("char_001", "d", "p2", 1, 0, 1), "Month<1 非法");
        Assert.AreEqual(VillageSocialFactResult.InvalidDate,
            l.RecordTemporaryCommitment("char_001", "d", "p3", 1, 13, 1), "Month>12 非法");
        Assert.AreEqual(VillageSocialFactResult.InvalidDate,
            l.RecordTemporaryCommitment("char_001", "d", "p4", 1, 2, 30), "非闰年 2 月无 30 日");
        Assert.AreEqual(0, l.Facts.Count);

        Assert.AreEqual(VillageSocialFactResult.Success,
            l.RecordTemporaryCommitment("char_001", "d", "p5", 4, 2, 29), "闰年 2 月 29 日合法");
        Assert.AreEqual(1, l.Facts.Count);
    }

    [Test]
    public void CountTemporaryCommitmentsInYear_FiltersPersonAndYear()
    {
        var l = NewLedger();
        // char_001：Year1 两条、Year2 一条
        l.RecordTemporaryCommitment("char_001", "prj_test_01", "p1", 1, 1, 1);
        l.RecordTemporaryCommitment("char_001", "prj_test_01", "p2", 1, 6, 30);
        l.RecordTemporaryCommitment("char_001", "prj_test_02", "p3", 2, 1, 1);
        // 其它 Person：Year1 一条
        l.RecordTemporaryCommitment("char_002", "prj_test_01", "p4", 1, 12, 31);

        Assert.AreEqual(2, l.CountTemporaryCommitmentsInYear("char_001", 1));
        Assert.AreEqual(1, l.CountTemporaryCommitmentsInYear("char_001", 2));
        Assert.AreEqual(1, l.CountTemporaryCommitmentsInYear("char_002", 1));
        Assert.AreEqual(0, l.CountTemporaryCommitmentsInYear("char_003", 1), "无事实 Person 为 0");
        Assert.AreEqual(0, l.CountTemporaryCommitmentsInYear("char_001", 3), "无事实年度为 0");
    }

    [Test]
    public void CountTemporaryCommitmentsInYear_InvalidArgs_ReturnsZero()
    {
        var l = NewLedger();
        l.RecordTemporaryCommitment("char_001", "d", "p1", 1, 1, 1);
        Assert.AreEqual(0, l.CountTemporaryCommitmentsInYear("", 1));
        Assert.AreEqual(0, l.CountTemporaryCommitmentsInYear(null, 1));
        Assert.AreEqual(0, l.CountTemporaryCommitmentsInYear("char_001", 0), "Year<1 返回 0");
    }

    [Test]
    public void Restore_Null_DefaultsEmptyLedger()
    {
        var l = NewLedger();
        l.RecordTemporaryCommitment("char_001", "prj_test_01", "p1", 1, 1, 1);
        Assert.AreEqual(1, l.Facts.Count);

        l.Restore(null);
        Assert.AreEqual(0, l.Facts.Count, "旧档缺 Social → 空账本默认");
        Assert.AreEqual(0, l.CountTemporaryCommitmentsInYear("char_001", 1));
    }

    [Test]
    public void Restore_Defensive_DropsInvalidAndDuplicatesKeepsValid()
    {
        var data = new VillageSocialSaveData
        {
            Facts = new List<VillageSocialFactSaveData>
            {
                new VillageSocialFactSaveData { Type = 0, PersonId = "char_good", ProjectDefId = "d", ProjectId = "ok1", Year = 1, Month = 1, Day = 1 },
                new VillageSocialFactSaveData { Type = 0, PersonId = "", ProjectDefId = "d", ProjectId = "bad_person", Year = 1, Month = 1, Day = 1 },
                new VillageSocialFactSaveData { Type = 0, PersonId = "char_x", ProjectDefId = "d", ProjectId = "", Year = 1, Month = 1, Day = 1 },
                new VillageSocialFactSaveData { Type = 999, PersonId = "char_x", ProjectDefId = "d", ProjectId = "bad_type", Year = 1, Month = 1, Day = 1 },
                new VillageSocialFactSaveData { Type = 0, PersonId = "char_y", ProjectDefId = "d", ProjectId = "bad_date", Year = 1, Month = 13, Day = 1 },
                new VillageSocialFactSaveData { Type = 0, PersonId = "char_dup", ProjectDefId = "d", ProjectId = "dup1", Year = 1, Month = 1, Day = 1 },
                new VillageSocialFactSaveData { Type = 0, PersonId = "char_dup", ProjectDefId = "d", ProjectId = "dup1", Year = 2, Month = 1, Day = 1 }
            }
        };

        var l = NewLedger();
        l.Restore(data);
        Assert.AreEqual(2, l.Facts.Count, "只应保留 char_good/ok1 与 char_dup/dup1 首条");
        Assert.AreEqual(1, l.CountTemporaryCommitmentsInYear("char_good", 1));
        Assert.AreEqual(1, l.CountTemporaryCommitmentsInYear("char_dup", 1));
        Assert.AreEqual(0, l.CountTemporaryCommitmentsInYear("char_dup", 2), "重复条目第二条不得恢复");
    }

    [Test]
    public void SaveRoundTrip_ToSaveDataRestore_PreservesFactsAndCounts()
    {
        var l = NewLedger();
        l.RecordTemporaryCommitment("char_001", "prj_test_01", "p1", 1, 1, 1);
        l.RecordTemporaryCommitment("char_001", "prj_test_02", "p2", 1, 3, 15);
        l.RecordTemporaryCommitment("char_002", "prj_test_01", "p3", 2, 12, 31);

        var l2 = NewLedger();
        l2.Restore(l.ToSaveData());

        Assert.AreEqual(3, l2.Facts.Count);
        Assert.AreEqual(2, l2.CountTemporaryCommitmentsInYear("char_001", 1));
        Assert.AreEqual(1, l2.CountTemporaryCommitmentsInYear("char_002", 2));
        Assert.AreEqual(0, l2.CountTemporaryCommitmentsInYear("char_001", 2));
    }
}
