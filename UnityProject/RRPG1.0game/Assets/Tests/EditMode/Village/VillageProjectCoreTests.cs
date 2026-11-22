using System.Collections.Generic;
using NUnit.Framework;

/// <summary>
/// V2B Project Commitment 纯逻辑测试（Village.Runtime 强类型）。
/// 覆盖：Project 保存多个 Participant / Participant Capacity / 同一 Person 禁双 Active Project /
///      非法状态防御 / Active 参与者集合（Temporary Occupancy 来源）。
/// </summary>
public class VillageProjectCoreTests
{
    private static List<VillageProjectDef> Defs()
    {
        return new List<VillageProjectDef>
        {
            new VillageProjectDef
            {
                ProjectDefId = "prj_a",
                DisplayName = "A(Test)",
                ParticipantCapacity = 2,
                TestProgressPerParticipantPerDay = 1,
                TestProgressTarget = 6
            },
            new VillageProjectDef
            {
                ProjectDefId = "prj_b",
                DisplayName = "B(Test)",
                ParticipantCapacity = 1,
                TestProgressPerParticipantPerDay = 2,
                TestProgressTarget = 4
            }
        };
    }

    private static VillageProjectSystem NewSystem()
    {
        return new VillageProjectSystem(Defs());
    }

    // ==================== Participants / Capacity ====================

    [Test]
    public void CreateActiveProject_StartsEmptyAndActive()
    {
        var s = NewSystem();
        VillageProject project;
        Assert.AreEqual(VillageProjectResult.Success, s.TryCreateProject("prj_a", out project));
        Assert.IsNotNull(project);
        Assert.IsTrue(project.IsActive);
        Assert.AreEqual(0, project.ParticipantPersonIds.Count);
        Assert.AreEqual(1, s.ActiveProjects.Count);
    }

    [Test]
    public void AddMultipleParticipants_ProjectSavesPersonIdsInOrder()
    {
        var s = NewSystem();
        VillageProject project;
        Assert.AreEqual(VillageProjectResult.Success, s.TryCreateProject("prj_a", out project));

        Assert.AreEqual(VillageProjectResult.Success, s.TryAddParticipant(project.ProjectId, "char_001"));
        Assert.AreEqual(VillageProjectResult.Success, s.TryAddParticipant(project.ProjectId, "char_002"));

        Assert.AreEqual(2, project.ParticipantPersonIds.Count, "Project 应保存多个 Participant Person");
        Assert.AreEqual("char_001", project.ParticipantPersonIds[0]);
        Assert.AreEqual("char_002", project.ParticipantPersonIds[1]);
    }

    [Test]
    public void ParticipantCapacity_RejectsAdditionalPerson()
    {
        var s = NewSystem();
        VillageProject project;
        Assert.AreEqual(VillageProjectResult.Success, s.TryCreateProject("prj_a", out project)); // Capacity=2

        Assert.AreEqual(VillageProjectResult.Success, s.TryAddParticipant(project.ProjectId, "char_001"));
        Assert.AreEqual(VillageProjectResult.Success, s.TryAddParticipant(project.ProjectId, "char_002"));
        Assert.AreEqual(VillageProjectResult.CapacityFull, s.TryAddParticipant(project.ProjectId, "char_003"),
            "Participant Capacity 满员后拒绝继续加入");
        Assert.AreEqual(2, project.ParticipantPersonIds.Count);
    }

    [Test]
    public void DifferentProjects_HaveIndependentCapacity()
    {
        var s = NewSystem();
        VillageProject a;
        VillageProject b;
        Assert.AreEqual(VillageProjectResult.Success, s.TryCreateProject("prj_a", out a));
        Assert.AreEqual(VillageProjectResult.Success, s.TryCreateProject("prj_b", out b));

        Assert.AreEqual(VillageProjectResult.Success, s.TryAddParticipant(a.ProjectId, "char_001"));
        Assert.AreEqual(VillageProjectResult.Success, s.TryAddParticipant(b.ProjectId, "char_002"));
        Assert.AreEqual(1, a.ParticipantPersonIds.Count);
        Assert.AreEqual(1, b.ParticipantPersonIds.Count);
    }

    // ==================== 同一 Person 禁双 Active Project ====================

    [Test]
    public void SamePerson_CannotJoinTwoActiveProjects()
    {
        var s = NewSystem();
        VillageProject a;
        VillageProject b;
        Assert.AreEqual(VillageProjectResult.Success, s.TryCreateProject("prj_a", out a));
        Assert.AreEqual(VillageProjectResult.Success, s.TryCreateProject("prj_b", out b));

        Assert.AreEqual(VillageProjectResult.Success, s.TryAddParticipant(a.ProjectId, "char_001"));
        Assert.AreEqual(VillageProjectResult.PersonAlreadyInActiveProject, s.TryAddParticipant(b.ProjectId, "char_001"),
            "同一 Person 默认不能同时参与两个 Active Project");
        Assert.AreEqual(0, b.ParticipantPersonIds.Count);

        var active = s.GetActiveProjectOfPerson("char_001");
        Assert.AreEqual(a.ProjectId, active.ProjectId);
    }

    [Test]
    public void AddSamePersonToSameProject_IsIdempotent()
    {
        var s = NewSystem();
        VillageProject project;
        Assert.AreEqual(VillageProjectResult.Success, s.TryCreateProject("prj_a", out project));
        Assert.AreEqual(VillageProjectResult.Success, s.TryAddParticipant(project.ProjectId, "char_001"));
        Assert.AreEqual(VillageProjectResult.Success, s.TryAddParticipant(project.ProjectId, "char_001"));
        Assert.AreEqual(1, project.ParticipantPersonIds.Count, "重复加入同一 Project 幂等成功，不得产生重复参与者");
    }

    // ==================== 非法状态防御 ====================

    [Test]
    public void InvalidDefOrProjectOrPerson_Rejected()
    {
        var s = NewSystem();
        VillageProject ignored;
        Assert.AreEqual(VillageProjectResult.UnknownProjectDef, s.TryCreateProject("prj_ghost", out ignored));
        Assert.AreEqual(VillageProjectResult.UnknownProject, s.TryAddParticipant("prj_ghost_1", "char_001"));
        Assert.AreEqual(VillageProjectResult.InvalidPersonId, s.TryAddParticipant("does_not_matter", ""));
    }

    [Test]
    public void ActiveParticipantPersonIds_ReturnsUnion_NoDuplicate()
    {
        var s = NewSystem();
        VillageProject a;
        VillageProject b;
        Assert.AreEqual(VillageProjectResult.Success, s.TryCreateProject("prj_a", out a));
        Assert.AreEqual(VillageProjectResult.Success, s.TryCreateProject("prj_b", out b));
        Assert.AreEqual(VillageProjectResult.Success, s.TryAddParticipant(a.ProjectId, "char_001"));
        Assert.AreEqual(VillageProjectResult.Success, s.TryAddParticipant(a.ProjectId, "char_002"));
        Assert.AreEqual(VillageProjectResult.Success, s.TryAddParticipant(b.ProjectId, "char_003"));

        var ids = s.GetActiveParticipantPersonIds();
        Assert.AreEqual(3, ids.Count, "Active 参与者集合应包含全部项目参与者（去重）");
        Assert.Contains("char_001", ids);
        Assert.Contains("char_002", ids);
        Assert.Contains("char_003", ids);
    }
}
