using System.Collections.Generic;
using NUnit.Framework;

/// <summary>
/// V2B Project 生命周期与时序纯逻辑测试（Village.Runtime 强类型）。
/// 覆盖：Progress 精确推进 / Completion 只结算一次并释放参与者 / Auto Return（参与者加入新项目）/
///      Opportunity Cost：Project 参与者暂停 Primary Assignment，完成当日仍暂停、次日起自动恢复。
/// 日序模拟与 VillageRuntimeBridge.HandleDayTick 保持一致：
///   先取日初占用 → 结算 Assignment（占用暂停）→ 推进 Project（当日可能完成并释放）。
/// </summary>
public class VillageProjectFlowTests
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
                ProjectDefId = "prj_solo",
                DisplayName = "Solo(Test)",
                ParticipantCapacity = 1,
                TestProgressPerParticipantPerDay = 1,
                TestProgressTarget = 2
            }
        };
    }

    private static VillageProjectSystem NewProjects()
    {
        return new VillageProjectSystem(Defs());
    }

    private static VillageAssignmentSystem NewAssignments()
    {
        return new VillageAssignmentSystem(new List<VillageWorkplaceDef>
        {
            new VillageWorkplaceDef { WorkplaceId = "ws_a", DisplayName = "A(Test)", Capacity = 2, TestContributionPerDay = 1 }
        });
    }

    // ==================== Progress 精确推进 ====================

    [Test]
    public void Progress_AdvancesByParticipantsPerDay()
    {
        var s = NewSystem_WithProjectAndPersons(); // helper 见下方
        // 使用 prj_a：2 名参与者 × 每人每天 1 = 每天 +2
        Assert.AreEqual(2, s.ActiveProjects[0].Progress, "第 1 天两名参与者应推进 2 单位");
        s.OnDayTick();
        Assert.AreEqual(4, s.ActiveProjects[0].Progress, "第 2 天再推进 2 单位");
    }

    private static VillageProjectSystem NewSystem_WithProjectAndPersons()
    {
        var s = NewProjects();
        VillageProject p;
        Assert.AreEqual(VillageProjectResult.Success, s.TryCreateProject("prj_a", out p));
        Assert.AreEqual(VillageProjectResult.Success, s.TryAddParticipant(p.ProjectId, "char_001"));
        Assert.AreEqual(VillageProjectResult.Success, s.TryAddParticipant(p.ProjectId, "char_002"));
        s.OnDayTick();
        return s;
    }

    [Test]
    public void NoParticipants_ProjectDoesNotProgress()
    {
        var s = NewProjects();
        VillageProject p;
        Assert.AreEqual(VillageProjectResult.Success, s.TryCreateProject("prj_a", out p));
        s.OnDayTick();
        s.OnDayTick();
        Assert.AreEqual(0, p.Progress);
        Assert.IsTrue(p.IsActive, "无参与者的 Project 保持 Active 但不推进（防御：不应出现，由启动规则保证）");
    }

    // ==================== Completion ====================

    [Test]
    public void Completion_AfterExactTicks_SettlesOnce_AndReleasesParticipants()
    {
        var s = NewProjects();
        VillageProject p;
        Assert.AreEqual(VillageProjectResult.Success, s.TryCreateProject("prj_a", out p)); // target 6
        Assert.AreEqual(VillageProjectResult.Success, s.TryAddParticipant(p.ProjectId, "char_001"));
        Assert.AreEqual(VillageProjectResult.Success, s.TryAddParticipant(p.ProjectId, "char_002"));

        s.OnDayTick(); // 2
        s.OnDayTick(); // 4
        Assert.IsTrue(p.IsActive, "未达目标前应保持 Active");

        s.OnDayTick(); // 6 → Completed
        Assert.IsTrue(p.IsCompleted, "进度达到目标后应 Completed");
        Assert.AreEqual(6, p.Progress, "完成时进度截断到目标");
        Assert.AreEqual(1, s.CompletedProjects.Count, "Completion 只结算一次");
        Assert.AreEqual(0, s.ActiveProjects.Count, "完成后从 Active 释放");
        Assert.AreEqual(0, s.GetActiveParticipantPersonIds().Count, "完成后参与者不再占用");

        // 再 tick 不重复结算
        s.OnDayTick();
        Assert.AreEqual(1, s.CompletedProjects.Count, "已完成 Project 不重复结算");
    }

    [Test]
    public void Completion_ReleasesPerson_ForAnotherActiveProject()
    {
        var s = NewProjects();
        VillageProject solo;
        Assert.AreEqual(VillageProjectResult.Success, s.TryCreateProject("prj_solo", out solo)); // target 2, cap 1
        Assert.AreEqual(VillageProjectResult.Success, s.TryAddParticipant(solo.ProjectId, "char_001"));

        s.OnDayTick(); // 1
        s.OnDayTick(); // 2 → Completed

        VillageProject next;
        Assert.AreEqual(VillageProjectResult.Success, s.TryCreateProject("prj_a", out next));
        Assert.AreEqual(VillageProjectResult.Success, s.TryAddParticipant(next.ProjectId, "char_001"),
            "Project 完成后原参与者可加入新的 Active Project");
    }

    // ==================== Opportunity Cost / Auto Return 时序 ====================

    [Test]
    public void ProjectParticipant_AssignmentPaused_CompletionDayStillPaused_ResumesNextDay()
    {
        var projects = NewProjects();
        var assignments = NewAssignments();

        // 先建立长期 Primary Assignment 并让其实际执行 1 天（产生 1 贡献）
        Assert.AreEqual(VillageAssignmentResult.Success, assignments.TryAssign("char_001", "ws_a"));
        assignments.OnDayTick(new List<string>()); // 未占用
        Assert.AreEqual(1, assignments.GetRecord("char_001").TotalExecutedDays);
        Assert.AreEqual(1, assignments.TotalTestContribution);

        // 把该 Person 抽去 Solo Project（target 2，1 人/天 → 2 天完成）
        VillageProject p;
        Assert.AreEqual(VillageProjectResult.Success, projects.TryCreateProject("prj_solo", out p));
        Assert.AreEqual(VillageProjectResult.Success, projects.TryAddParticipant(p.ProjectId, "char_001"));

        // 第 1 天：Project 占用中 → Assignment 不执行；Project 推进 1
        assignments.OnDayTick(projects.GetActiveParticipantPersonIds());
        projects.OnDayTick();
        Assert.AreEqual(1, assignments.GetRecord("char_001").TotalExecutedDays, "Project 期间 Assignment 不执行");
        Assert.AreEqual(1, assignments.TotalTestContribution, "Project 期间原岗位 Test Contribution 停止");
        Assert.AreEqual(1, p.Progress);

        // 第 2 天（完成日）：日初仍占用 → Assignment 仍不执行；Project 完成并释放
        assignments.OnDayTick(projects.GetActiveParticipantPersonIds());
        projects.OnDayTick();
        Assert.IsTrue(p.IsCompleted, "第 2 天应完成 Project");
        Assert.AreEqual(1, assignments.GetRecord("char_001").TotalExecutedDays,
            "完成当天该 Person 仍视为整天被 Project 占用，Assignment 不执行（避免一天双得）");
        Assert.AreEqual(1, assignments.TotalTestContribution);
        Assert.AreEqual(0, projects.GetActiveParticipantPersonIds().Count, "完成后自动解除 Temporary Occupancy");

        // 第 3 天起：无占用 → 原 Assignment 自动恢复实际执行（Auto Return）
        assignments.OnDayTick(projects.GetActiveParticipantPersonIds());
        Assert.AreEqual(2, assignments.GetRecord("char_001").TotalExecutedDays, "完成后原 Assignment 自动恢复实际执行");
        Assert.AreEqual(2, assignments.TotalTestContribution);
    }

    [Test]
    public void AssignmentRecord_IsNeverDeleted_WhileProjectOccupied()
    {
        var projects = NewProjects();
        var assignments = NewAssignments();

        Assert.AreEqual(VillageAssignmentResult.Success, assignments.TryAssign("char_001", "ws_a"));
        VillageProject p;
        Assert.AreEqual(VillageProjectResult.Success, projects.TryCreateProject("prj_a", out p));
        Assert.AreEqual(VillageProjectResult.Success, projects.TryAddParticipant(p.ProjectId, "char_001"));

        for (int i = 0; i < 2; i++)
        {
            assignments.OnDayTick(projects.GetActiveParticipantPersonIds());
            projects.OnDayTick();
        }

        Assert.IsNotNull(assignments.GetRecord("char_001"),
            "Project 是 Temporary Occupancy，不得覆盖删除 Primary Assignment（长期安排保留）");
        Assert.AreEqual("ws_a", assignments.GetWorkplaceId("char_001"));
    }

    [Test]
    public void ProfessionHookFacts_DoNotGrow_WhileProjectOccupied()
    {
        var projects = NewProjects();
        var assignments = NewAssignments();

        Assert.AreEqual(VillageAssignmentResult.Success, assignments.TryAssign("char_001", "ws_a"));
        assignments.OnDayTick(new List<string>());
        Assert.AreEqual(1, assignments.GetProfessionFormationSourceHookFacts()[0].TotalExecutedDays);

        VillageProject p;
        Assert.AreEqual(VillageProjectResult.Success, projects.TryCreateProject("prj_a", out p));
        Assert.AreEqual(VillageProjectResult.Success, projects.TryAddParticipant(p.ProjectId, "char_001"));

        assignments.OnDayTick(projects.GetActiveParticipantPersonIds());
        projects.OnDayTick();
        assignments.OnDayTick(projects.GetActiveParticipantPersonIds());
        projects.OnDayTick();

        Assert.AreEqual(1, assignments.GetProfessionFormationSourceHookFacts()[0].TotalExecutedDays,
            "Project 占用期间不得继续产生该 Assignment 的 Profession Formation Source Hook (Test) 事实");
    }
}
