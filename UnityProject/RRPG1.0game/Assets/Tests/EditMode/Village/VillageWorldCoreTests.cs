using System.Collections.Generic;
using NUnit.Framework;

/// <summary>
/// Minimum Village World Integration 第一包核心纯逻辑测试（Village.Runtime 强类型）。
/// 覆盖：Workplace 派生登记幂等 / ProjectSite 完成点单向追加幂等 /
///      Save 只存 ProjectSite（Workplace 不落档）/ 旧档空区块兼容 / Restore 防御 /
///      Presence 派生优先级（Away → ProjectSite → Workplace → Free）。
/// 无产品数字硬编码；表现/坐标/类名均为 Test / Not Frozen。
/// </summary>
public class VillageWorldCoreTests
{
    private static VillageWorldRegistry NewRegistry()
    {
        return new VillageWorldRegistry();
    }

    [Test]
    public void Workplace_SeededFromDefs_Idempotent()
    {
        var r = NewRegistry();
        r.EnsureWorkplacePlaces(VillageTestWorkplaces.CreateDefaults());

        Assert.AreEqual(2, r.CountPlacesOfKind(VillagePlaceKind.Workplace));
        Assert.IsTrue(r.ContainsPlace("ws_test_01"));
        Assert.IsTrue(r.ContainsPlace("ws_test_02"));
        Assert.AreEqual(VillagePlaceKind.Workplace, r.GetPlace("ws_test_01").Kind);

        r.EnsureWorkplacePlaces(VillageTestWorkplaces.CreateDefaults());
        Assert.AreEqual(2, r.CountPlacesOfKind(VillagePlaceKind.Workplace), "重复登记幂等，不产生第二套 Place");
    }

    [Test]
    public void ProjectSite_AppendOnce_And_Idempotent()
    {
        var r = NewRegistry();
        var site = r.AppendCompletedProjectSite("prj_test_01_7", "prj_test_01", "Test Project 01（Test / Not Frozen）");

        Assert.IsNotNull(site);
        Assert.AreEqual("site_prj_test_01_7", site.PlaceId);
        Assert.AreEqual(VillagePlaceKind.ProjectSite, site.Kind);
        Assert.AreEqual("prj_test_01", site.SourceRef);
        Assert.AreEqual(1, r.CountPlacesOfKind(VillagePlaceKind.ProjectSite));

        var again = r.AppendCompletedProjectSite("prj_test_01_7", "prj_test_01", "任意改名");
        Assert.AreSame(site, again, "同 ProjectId 幂等返回既有记录，不重复追加");
        Assert.AreEqual(1, r.CountPlacesOfKind(VillagePlaceKind.ProjectSite));
    }

    [Test]
    public void ProjectSite_EmptyProjectId_Ignored()
    {
        var r = NewRegistry();
        Assert.IsNull(r.AppendCompletedProjectSite(null, "prj_test_01", "x"));
        Assert.IsNull(r.AppendCompletedProjectSite("", "prj_test_01", "x"));
        Assert.AreEqual(0, r.CountPlacesOfKind(VillagePlaceKind.ProjectSite));
    }

    [Test]
    public void SaveData_OnlyProjectSitePersisted_WorkplaceDerived()
    {
        var r = NewRegistry();
        r.EnsureWorkplacePlaces(VillageTestWorkplaces.CreateDefaults());
        r.AppendCompletedProjectSite("prj_test_01_1", "prj_test_01", "Test Project 01（Test / Not Frozen）");

        var data = r.ToSaveData();
        Assert.IsNotNull(data.Places);
        Assert.AreEqual(1, data.Places.Count, "Workplace 派生不落档；只存不可派生 ProjectSite");
        Assert.AreEqual("site_prj_test_01_1", data.Places[0].PlaceId);
    }

    [Test]
    public void Restore_Null_KeepsWorkplaceSeeds_EmptyProjectSites()
    {
        var r = NewRegistry();
        r.EnsureWorkplacePlaces(VillageTestWorkplaces.CreateDefaults());
        r.Restore(null);

        Assert.AreEqual(2, r.CountPlacesOfKind(VillagePlaceKind.Workplace));
        Assert.AreEqual(0, r.CountPlacesOfKind(VillagePlaceKind.ProjectSite));
    }

    [Test]
    public void Restore_ProjectSites_RoundTrip()
    {
        var r1 = NewRegistry();
        r1.EnsureWorkplacePlaces(VillageTestWorkplaces.CreateDefaults());
        r1.AppendCompletedProjectSite("prj_test_02_3", "prj_test_02", "Test Project 02（Test / Not Frozen）");

        var r2 = NewRegistry();
        r2.EnsureWorkplacePlaces(VillageTestWorkplaces.CreateDefaults());
        r2.Restore(r1.ToSaveData());

        Assert.IsTrue(r2.ContainsPlace("site_prj_test_02_3"));
        Assert.AreEqual(1, r2.CountPlacesOfKind(VillagePlaceKind.ProjectSite));
        var site = r2.GetPlace("site_prj_test_02_3");
        Assert.AreEqual("Test Project 02（Test / Not Frozen）", site.DisplayName);
        Assert.AreEqual("prj_test_02", site.SourceRef);
    }

    [Test]
    public void Restore_Defensive_DropsEmpty_Duplicates_UnknownKind()
    {
        var r = NewRegistry();
        var data = new VillageWorldSaveData
        {
            Places = new List<VillageWorldPlaceSaveData>
            {
                new VillageWorldPlaceSaveData { Kind = (int)VillagePlaceKind.ProjectSite, PlaceId = "site_prj_test_01_1", DisplayName = "a", SourceRef = "prj_test_01" },
                new VillageWorldPlaceSaveData { Kind = (int)VillagePlaceKind.ProjectSite, PlaceId = "site_prj_test_01_1", DisplayName = "dup", SourceRef = "prj_test_01" },
                new VillageWorldPlaceSaveData { Kind = (int)VillagePlaceKind.ProjectSite, PlaceId = "", DisplayName = "empty", SourceRef = "prj_test_01" },
                new VillageWorldPlaceSaveData { Kind = 99, PlaceId = "site_bad", DisplayName = "bad", SourceRef = "x" }
            }
        };

        r.Restore(data);
        Assert.AreEqual(1, r.CountPlacesOfKind(VillagePlaceKind.ProjectSite), "空 / 重复 / 未知 Kind 全部丢弃");
        Assert.AreEqual("a", r.GetPlace("site_prj_test_01_1").DisplayName);
    }

    // ==================== Presence 派生（只从既有真相源派生） ====================

    private static VillagePersonPresence ResolvePresence(string personId, string[] awayExpedition, string[] awayUnavailable,
        string activeProjectId, string workplaceId)
    {
        return VillageWorldPresence.Resolve(personId, awayExpedition, awayUnavailable, activeProjectId, workplaceId);
    }

    [Test]
    public void Presence_Free_WhenNoSource()
    {
        var p = ResolvePresence("char_001", new string[0], new string[0], null, null);
        Assert.AreEqual(VillagePresenceState.Free, p.State);
        Assert.IsTrue(string.IsNullOrEmpty(p.PlaceId));
    }

    [Test]
    public void Presence_Workplace_WhenAssigned()
    {
        var p = ResolvePresence("char_001", new string[0], new string[0], null, "ws_test_01");
        Assert.AreEqual(VillagePresenceState.Workplace, p.State);
        Assert.AreEqual("ws_test_01", p.PlaceId);
    }

    [Test]
    public void Presence_ProjectSite_BeatsWorkplace()
    {
        var p = ResolvePresence("char_001", new string[0], new string[0], "prj_test_01_1", "ws_test_01");
        Assert.AreEqual(VillagePresenceState.ProjectSite, p.State);
        Assert.AreEqual("site_prj_test_01_1", p.PlaceId);
    }

    [Test]
    public void Presence_Expedition_IsAway_BeatsAll()
    {
        var p = ResolvePresence("char_001", new[] { "char_001" }, new string[0], "prj_test_01_1", "ws_test_01");
        Assert.AreEqual(VillagePresenceState.Away, p.State);
        Assert.IsTrue(string.IsNullOrEmpty(p.PlaceId));
    }

    [Test]
    public void Presence_Unavailable_IsAway()
    {
        var p = ResolvePresence("char_001", new string[0], new[] { "char_001" }, null, "ws_test_01");
        Assert.AreEqual(VillagePresenceState.Away, p.State);
    }

    [Test]
    public void Presence_OtherPersonAway_DoesNotAffectTarget()
    {
        var p = ResolvePresence("char_001", new[] { "char_002" }, new string[0], null, "ws_test_01");
        Assert.AreEqual(VillagePresenceState.Workplace, p.State);
        Assert.AreEqual("ws_test_01", p.PlaceId);
    }
}
