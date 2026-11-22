using System.Collections.Generic;
using NUnit.Framework;

/// <summary>
/// V2C VillageCommitmentView（纯逻辑视图模型）测试（Village.Runtime 强类型）。
/// 覆盖：以 Person 为主体的状态行合成（在岗/空闲/远征/不可用/投入 Project）、
///      Workplace Capacity 可读、Project 定义/Active 进度行（含参与者名与预计天数）。
/// </summary>
public class VillageCommitmentViewTests
{
    private static VillageAssignmentSystem NewAssignments()
    {
        return new VillageAssignmentSystem(new List<VillageWorkplaceDef>
        {
            new VillageWorkplaceDef { WorkplaceId = "ws_a", DisplayName = "岗位甲(Test)", Capacity = 1, TestContributionPerDay = 1 },
            new VillageWorkplaceDef { WorkplaceId = "ws_b", DisplayName = "岗位乙(Test)", Capacity = 2, TestContributionPerDay = 1 }
        });
    }

    private static VillageProjectSystem NewProjects()
    {
        return new VillageProjectSystem(new List<VillageProjectDef>
        {
            new VillageProjectDef
            {
                ProjectDefId = "prj_t",
                DisplayName = "测试项目(Test)",
                ParticipantCapacity = 2,
                TestProgressPerParticipantPerDay = 1,
                TestProgressTarget = 6
            }
        });
    }

    private static VillagePersonView View(string id, string name, VillageAvailability availability)
    {
        return new VillagePersonView { PersonId = id, DisplayName = name, Availability = availability };
    }

    [Test]
    public void BuildPersonRows_DistinguishesWorking_Idle_Expedition_Unavailable_Project()
    {
        var assignments = NewAssignments();
        var projects = NewProjects();

        // char_001 在岗
        Assert.AreEqual(VillageAssignmentResult.Success, assignments.TryAssign("char_001", "ws_b"));
        // char_005 投入 Project
        VillageProject p;
        Assert.AreEqual(VillageProjectResult.Success, projects.TryCreateProject("prj_t", out p));
        Assert.AreEqual(VillageProjectResult.Success, projects.TryAddParticipant(p.ProjectId, "char_005"));

        var views = new List<VillagePersonView>
        {
            View("char_001", "甲", VillageAvailability.InVillage),
            View("char_002", "乙", VillageAvailability.Expedition),
            View("char_003", "丙", VillageAvailability.Unavailable),
            View("char_004", "丁", VillageAvailability.InVillage),
            View("char_005", "戊", VillageAvailability.InVillage)
        };

        var rows = VillageCommitmentView.BuildPersonRows(views, assignments, projects);
        Assert.AreEqual(5, rows.Count);

        var r1 = rows.Find(r => r.PersonId == "char_001");
        Assert.AreEqual(VillageCommitmentView.PersonCommitmentStatus.Working, r1.Status);
        Assert.AreEqual("岗位乙(Test)", r1.WorkplaceDisplayName);
        StringAssert.Contains("在岗执行中", r1.StatusText);
        Assert.AreEqual(0, r1.TotalExecutedDays);

        Assert.AreEqual(VillageCommitmentView.PersonCommitmentStatus.Idle,
            rows.Find(r => r.PersonId == "char_004").Status);
        Assert.AreEqual(VillageCommitmentView.PersonCommitmentStatus.Expedition,
            rows.Find(r => r.PersonId == "char_002").Status);
        StringAssert.Contains("远征中", rows.Find(r => r.PersonId == "char_002").StatusText);
        Assert.AreEqual(VillageCommitmentView.PersonCommitmentStatus.Unavailable,
            rows.Find(r => r.PersonId == "char_003").Status);

        var r5 = rows.Find(r => r.PersonId == "char_005");
        Assert.AreEqual(VillageCommitmentView.PersonCommitmentStatus.InProject, r5.Status);
        Assert.AreEqual("测试项目(Test)", r5.ActiveProjectDisplayName);
        StringAssert.Contains("暂停", r5.StatusText);
    }

    [Test]
    public void BuildWorkplaceRows_ShowsCapacityFullAndOccupantNames()
    {
        var assignments = NewAssignments();
        var personNames = new Dictionary<string, string> { { "char_001", "甲" }, { "char_002", "乙" } };

        // 先占满 ws_b（cap2）
        assignments.TryAssign("char_001", "ws_b");
        assignments.TryAssign("char_002", "ws_b");

        var rows = VillageCommitmentView.BuildWorkplaceRows(
            new List<VillageWorkplaceDef>
            {
                new VillageWorkplaceDef { WorkplaceId = "ws_b", DisplayName = "岗位乙(Test)", Capacity = 2, TestContributionPerDay = 1 }
            },
            assignments, personNames);

        Assert.AreEqual(1, rows.Count);
        var row = rows[0];
        Assert.IsTrue(row.IsFull, "Capacity=2 已满时应标记满员");
        StringAssert.Contains("已满", row.CapacityText);
        StringAssert.Contains("甲", row.OccupantText);
        StringAssert.Contains("乙", row.OccupantText);
    }

    [Test]
    public void BuildActiveProjectRows_ShowsProgressParticipantsAndRemainingDays()
    {
        var projects = NewProjects();
        VillageProject p;
        Assert.AreEqual(VillageProjectResult.Success, projects.TryCreateProject("prj_t", out p));
        Assert.AreEqual(VillageProjectResult.Success, projects.TryAddParticipant(p.ProjectId, "char_001"));
        projects.OnDayTick(); // 1/6

        var personNames = new Dictionary<string, string> { { "char_001", "甲" } };
        var defRows = VillageCommitmentView.BuildProjectDefRows(new List<VillageProjectDef>
        {
            new VillageProjectDef { ProjectDefId = "prj_t", DisplayName = "测试项目(Test)", ParticipantCapacity = 2,
                TestProgressPerParticipantPerDay = 1, TestProgressTarget = 6 }
        });
        Assert.AreEqual(1, defRows.Count);
        StringAssert.Contains("容量 2", defRows[0].RuleText);

        var activeRows = VillageCommitmentView.BuildActiveProjectRows(projects, personNames);
        Assert.AreEqual(1, activeRows.Count);
        var row = activeRows[0];
        Assert.AreEqual("1 / 6", row.ProgressText);
        Assert.AreEqual(5, row.RemainingDays, "1 名参与者每天 1 进度，剩 5 需 5 天");
        StringAssert.Contains("甲", row.ParticipantNamesText);
        StringAssert.Contains("1 / 6", row.SummaryText);
    }

    [Test]
    public void BuildPersonRows_AssignmentsExecutedDaysReflectRuntimeFact()
    {
        var assignments = NewAssignments();
        assignments.TryAssign("char_001", "ws_a");
        assignments.OnDayTick(new List<string>()); // 实际执行 1 天

        var views = new List<VillagePersonView> { View("char_001", "甲", VillageAvailability.InVillage) };
        var rows = VillageCommitmentView.BuildPersonRows(views, assignments, null);
        Assert.AreEqual(1, rows[0].TotalExecutedDays, "视图应反映 Runtime 执行事实（已执行 1 天）");
        StringAssert.Contains("1 天", rows[0].StatusText);
    }
}
