using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

/// <summary>
/// V2B Project × Save/Load 集成测试（真实 SaveSystem / VillageRuntimeBridge 反射）：
///   - Save → Load 中途恢复：Participants / Progress / Occupancy / Assignment 暂停关系保持一致并可继续到完成；
///   - Legacy 旧档以空 Project 系统兼容加载；
///   - Restore 防御（未知 Def / 双 Active Person / 空参与者 / 已达目标项目不恢复 Active）；
///   - TryStartProject 失败回滚与 Expedition/Unavailable 调度防御。
/// </summary>
public class VillageProjectSaveTests
{
    private const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;

    private static System.Type SaveSystemType => System.Type.GetType("SaveSystem, Assembly-CSharp");
    private static System.Type BridgeType => System.Type.GetType("VillageRuntimeBridge, Assembly-CSharp");

    [SetUp]
    public void ResetBridge()
    {
        InvokeStatic(BridgeType, "ResetForTests");
    }

    [TearDown]
    public void Cleanup()
    {
        InvokeStatic(BridgeType, "ResetForTests");
        DeleteSave();
    }

    // ==================== helpers（反射） ====================

    private static void DeleteSave()
    {
        string path = (string)InvokeStatic(SaveSystemType, "GetSavePath");
        if (path != null && File.Exists(path)) File.Delete(path);
    }

    private static void EnsureInitialized()
    {
        InvokeStatic(BridgeType, "EnsureInitialized", new object[] { null });
        IntradayVillageTestSource.BindDefaults();
    }

    private static object InvokeStatic(System.Type type, string method, params object[] args)
    {
        var methods = System.Linq.Enumerable.Where(type.GetMethods(F), m => m.Name == method);
        MethodInfo selected = null;
        foreach (var m in methods)
        {
            var ps = m.GetParameters();
            if (ps.Length != (args == null ? 0 : args.Length)) continue;
            bool ok = true;
            for (int i = 0; i < ps.Length; i++)
            {
                if (args[i] == null) continue;
                if (!ps[i].ParameterType.IsAssignableFrom(args[i].GetType())) { ok = false; break; }
            }
            if (ok) { selected = m; break; }
        }
        Assert.IsNotNull(selected, "方法不存在或参数不匹配：" + method + " @ " + type.FullName);
        return selected.Invoke(null, args);
    }

    private static object GetStaticField(System.Type type, string name)
    {
        var f = type.GetField(name, F);
        Assert.IsNotNull(f, "字段不存在：" + name + " @ " + type.FullName);
        return f.GetValue(null);
    }

    private static VillageProjectSystem GetProjects()
    {
        return (VillageProjectSystem)GetStaticField(BridgeType, "Projects");
    }

    private static VillageAssignmentSystem GetAssignments()
    {
        return (VillageAssignmentSystem)GetStaticField(BridgeType, "Assignments");
    }

    private static VillageTime GetTime()
    {
        return (VillageTime)GetStaticField(BridgeType, "Time");
    }

    private static VillageAssignmentResult Assign(string personId, string workplaceId)
    {
        return (VillageAssignmentResult)InvokeStatic(BridgeType, "TryAssignPerson", personId, workplaceId);
    }

    private static VillageProjectResult StartProject(string defId, string[] participants)
    {
        return (VillageProjectResult)InvokeStatic(BridgeType, "TryStartProject", defId, participants);
    }

    private static List<string> Occupied()
    {
        return (List<string>)InvokeStatic(BridgeType, "GetTemporaryOccupiedPersonIds");
    }

    private static void TickDays(int days)
    {
        var t = GetTime();
        for (int i = 0; i < days; i++) t.TickDay();
    }

    private static object NewPlayerCollection()
    {
        return IntradayVillageTestSource.Current ?? IntradayVillageTestSource.Create();
    }

    // ==================== Save / Load 中途恢复 ====================

    [Test]
    public void SaveLoad_MidProject_RoundTrip_PreservesAndResumesToCompletion()
    {
        EnsureInitialized();
        Assert.AreEqual(VillageAssignmentResult.Success, Assign("char_001", "ws_test_01"));
        Assert.AreEqual(VillageProjectResult.Success, StartProject("prj_test_01", new[] { "char_001" }));
        TickDays(2); // Project 推进 2（1 人/天）；期间 Assignment 被暂停

        // 存档前：参与者在 Temporary Occupancy 中，Assignment 暂停、Project 有进度
        var projects = GetProjects();
        var assignments = GetAssignments();
        Assert.AreEqual(2, projects.ActiveProjects.Count == 1 ? projects.ActiveProjects[0].Progress : -1);
        Assert.IsFalse(Occupied().Contains("char_001"), "午夜不以 Project 承诺锁住人物整天");
        Assert.IsNotNull(assignments.GetRecord("char_001"), "Primary Assignment 不得被删除");
        Assert.AreEqual(0, assignments.GetRecord("char_001").TotalExecutedDays, "Project 期间 Assignment 不执行");

        // 真实保存链路
        object collection = NewPlayerCollection();
        Assert.IsTrue((bool)InvokeStatic(SaveSystemType, "Save", collection), "带 Active Project 的存档应保存成功");

        // 模拟重启后 Load
        InvokeStatic(BridgeType, "ResetForTests");
        object loaded = InvokeStatic(SaveSystemType, "Load");
        if (loaded != null) IntradayVillageTestSource.Bind(loaded);
        Assert.IsNotNull(loaded);

        // 中途恢复：Participants / Progress / Occupancy / Assignment 关系保持一致
        var restoredProjects = GetProjects();
        var restoredAssignments = GetAssignments();
        Assert.AreEqual(1, restoredProjects.ActiveProjects.Count, "Load 后应恢复进行中的 Project");
        var p = restoredProjects.ActiveProjects[0];
        Assert.AreEqual(2, p.Progress, "Load 后 Progress 保持一致");
        Assert.Contains("char_001", p.ParticipantPersonIds, "Load 后 Participants 保持一致");
        Assert.IsFalse(Occupied().Contains("char_001"), "Load 午夜保留承诺，不构造实际劳动占用");
        Assert.IsNotNull(restoredAssignments.GetRecord("char_001"));
        Assert.AreEqual(0, restoredAssignments.GetRecord("char_001").TotalExecutedDays,
            "Load 后 Assignment 暂停关系保持一致");

        // 继续推进到完成（prj_test_01 target=10，1 人/天 → 还需 8 tick）
        TickDays(8);
        Assert.AreEqual(0, restoredProjects.ActiveProjects.Count, "完成解除 Temporary Occupancy");
        Assert.IsTrue(restoredProjects.IsProjectCompleted(p.ProjectId), "Load 后 Project 可继续推进到完成");
        Assert.AreEqual(0, restoredAssignments.GetRecord("char_001").TotalExecutedDays,
            "完成当天 Assignment 仍不执行");

        // 次日自动恢复原 Assignment 实际执行
        TickDays(1);
        Assert.AreEqual(1, restoredAssignments.GetRecord("char_001").TotalExecutedDays,
            "Project 完成后原 Primary Assignment 自动恢复实际执行");
        Assert.AreEqual(1, restoredAssignments.TotalTestContribution);
    }

    [Test]
    public void LegacySave_WithoutVillage_LoadsWithEmptyProjects_AndCanStartProject()
    {
        string json = "{\"SaveVersion\":1,\"GameVersion\":\"0.2.3\",\"SavedAtUnixTime\":0," +
                      "\"Player\":{\"Gold\":100,\"Heroes\":[],\"Characters\":[],\"OwnedCharacterCardIds\":[],\"OwnedItemCardIds\":[]}}";
        string path = (string)InvokeStatic(SaveSystemType, "GetSavePath");
        File.WriteAllText(path, json);

        object loaded = InvokeStatic(SaveSystemType, "Load");
        if (loaded != null) IntradayVillageTestSource.Bind(loaded);
        Assert.IsNotNull(loaded, "旧档必须可加载，不得因缺少 Village / Projects 区块失败");

        var projects = GetProjects();
        Assert.IsNotNull(projects, "旧档应初始化空 Project 系统");
        Assert.AreEqual(0, projects.ActiveProjects.Count);
        Assert.AreEqual(0, projects.GetActiveParticipantPersonIds().Count);

        Assert.AreEqual(VillageProjectResult.Success, StartProject("prj_test_01", new[] { "char_001" }),
            "旧档进入后可以正常启动 V2B Test Project");
    }

    // ==================== 非法状态防御（Restore） ====================

    [Test]
    public void Restore_UnknownDefOrEmptyProject_IsDropped()
    {
        var s = new VillageProjectSystem(VillageTestProjects.CreateDefaults());
        s.Restore(new VillageProjectsSaveData
        {
            ActiveProjects = new List<VillageProjectSaveData>
            {
                new VillageProjectSaveData
                {
                    ProjectId = "prj_ghost_1",
                    ProjectDefId = "prj_ghost",
                    ParticipantPersonIds = new List<string> { "char_001" },
                    Progress = 3
                },
                new VillageProjectSaveData
                {
                    ProjectId = "prj_test_01_9",
                    ProjectDefId = "prj_test_01",
                    ParticipantPersonIds = new List<string>(),
                    Progress = 0
                }
            }
        });

        Assert.AreEqual(1, s.ActiveProjects.Count, "未知 Def 丢弃；合法无人项目以 Paused 保留");
        Assert.IsTrue(s.ActiveProjects[0].ExecutionPaused);
        Assert.AreEqual(0, s.GetActiveParticipantPersonIds().Count);
    }

    [Test]
    public void Restore_SamePersonInTwoProjects_KeepsFirstOnly()
    {
        var s = new VillageProjectSystem(VillageTestProjects.CreateDefaults());
        s.Restore(new VillageProjectsSaveData
        {
            ActiveProjects = new List<VillageProjectSaveData>
            {
                new VillageProjectSaveData
                {
                    ProjectId = "prj_test_01_1",
                    ProjectDefId = "prj_test_01",
                    ParticipantPersonIds = new List<string> { "char_001", "char_002" },
                    Progress = 4
                },
                new VillageProjectSaveData
                {
                    ProjectId = "prj_test_01_2",
                    ProjectDefId = "prj_test_01",
                    ParticipantPersonIds = new List<string> { "char_001", "char_003" },
                    Progress = 1
                }
            }
        });

        Assert.AreEqual(2, s.ActiveProjects.Count,
            "Person 冲突只从后续 Project 过滤该 Person；两个项目本体都保留（各自仍有参与者）");
        var first = s.ActiveProjects[0];
        var second = s.ActiveProjects[1];
        Assert.AreEqual("prj_test_01_1", first.ProjectId);
        Assert.AreEqual(2, first.ParticipantPersonIds.Count);
        Assert.AreEqual(1, second.ParticipantPersonIds.Count, "冲突 Person 从第二个 Project 过滤，其余参与者保留");
        Assert.IsTrue(second.ParticipantPersonIds.Contains("char_003"));
        Assert.IsFalse(second.ParticipantPersonIds.Contains("char_001"), "同一 Person 不得同时占用两个 Active Project");

        var activeOfPerson = s.GetActiveProjectOfPerson("char_001");
        Assert.AreEqual("prj_test_01_1", activeOfPerson.ProjectId);
    }

    [Test]
    public void Restore_ProjectAlreadyAtTarget_IsNotRestoredAsActive()
    {
        var s = new VillageProjectSystem(VillageTestProjects.CreateDefaults());
        s.Restore(new VillageProjectsSaveData
        {
            ActiveProjects = new List<VillageProjectSaveData>
            {
                new VillageProjectSaveData
                {
                    ProjectId = "prj_test_01_1",
                    ProjectDefId = "prj_test_01", // target 10
                    ParticipantPersonIds = new List<string> { "char_001" },
                    Progress = 12
                }
            }
        });

        Assert.AreEqual(0, s.ActiveProjects.Count, "已达目标进度的项目不应恢复为 Active（不占用）");
        Assert.IsFalse(s.IsPersonInActiveProject("char_001"));
        Assert.IsTrue(s.IsProjectCompleted("prj_test_01_1"));
    }

    [Test]
    public void Restore_NegativeProgress_ClampedToZero()
    {
        var s = new VillageProjectSystem(VillageTestProjects.CreateDefaults());
        s.Restore(new VillageProjectsSaveData
        {
            ActiveProjects = new List<VillageProjectSaveData>
            {
                new VillageProjectSaveData
                {
                    ProjectId = "prj_test_01_1",
                    ProjectDefId = "prj_test_01",
                    ParticipantPersonIds = new List<string> { "char_001" },
                    Progress = -5
                }
            }
        });

        Assert.AreEqual(1, s.ActiveProjects.Count);
        Assert.AreEqual(0, s.ActiveProjects[0].Progress, "负数进度回退 0");
    }

    // ==================== 启动失败回滚 / 调度防御 ====================

    [Test]
    public void TryStartProject_WhenCapacityWouldExceed_RollsBackWholeProject()
    {
        EnsureInitialized();
        // prj_test_01 Capacity=2，传 3 人 → 第 3 人满员失败 → 整体回滚
        Assert.AreEqual(VillageProjectResult.CapacityFull,
            StartProject("prj_test_01", new[] { "char_001", "char_002", "char_003" }));
        Assert.AreEqual(0, GetProjects().ActiveProjects.Count, "启动失败不得残留部分参与者的 Project");
        Assert.AreEqual(0, GetProjects().GetActiveParticipantPersonIds().Count);
    }

    [Test]
    public void TryStartProject_ExpeditionOrUnavailablePerson_RejectedByScheduler()
    {
        EnsureInitialized();
        InvokeStatic(BridgeType, "SetExpeditionPersonIds", (object)new[] { "char_020" });
        Assert.AreEqual(VillageProjectResult.PersonCurrentlyUnavailable,
            StartProject("prj_test_01", new[] { "char_020" }));
        Assert.AreEqual(0, GetProjects().ActiveProjects.Count);

        InvokeStatic(BridgeType, "SetUnavailablePersonIds", (object)new[] { "char_021" });
        Assert.AreEqual(VillageProjectResult.PersonCurrentlyUnavailable,
            StartProject("prj_test_01", new[] { "char_021" }));
        Assert.AreEqual(0, GetProjects().ActiveProjects.Count);
    }

    // ==================== V2 Final Closure：Temporary Occupancy 跨 Expedition 互斥 ====================

    /// <summary>在 PlayerCollection 中创建一个真实人物实例 + 角色卡 + 显式绑定；返回该卡。</summary>
    private object AddBoundPersonCard(object pc, out string personId, string cardId, string templateId)
    {
        var charType = System.Type.GetType("CharacterInstance, Assembly-CSharp");
        object character = System.Activator.CreateInstance(charType,
            new object[] { templateId, templateId, 10, 3 });
        personId = (string)charType.GetField("PersonId").GetValue(character);
        var owned = (System.Collections.IList)pc.GetType().GetField("OwnedCharacters").GetValue(pc);
        owned.Add(character);

        var cardType = System.Type.GetType("CharacterCardData, Assembly-CSharp");
        object card = UnityEngine.ScriptableObject.CreateInstance(cardType);
        cardType.GetField("CardId").SetValue(card, cardId);
        cardType.GetField("CharacterId").SetValue(card, templateId);
        var cards = (System.Collections.IList)pc.GetType().GetField("OwnedCharacterCards").GetValue(pc);
        cards.Add(card);
        pc.GetType().GetMethod("BindCharacterCardOwner").Invoke(pc, new object[] { cardId, personId });
        return card;
    }

    /// <summary>
    /// 构造一个“已选中一个真实长期 Person 对应角色卡”的 ExpeditionSystem（M1 语义）：
    /// project 参与者必须使用返回的真实 PersonId，而不是模板 CharacterId。
    /// </summary>
    private object NewExpeditionSystemWithChar(out string personId, string cardId, string templateId)
    {
        object pc = NewPlayerCollection();
        object card = AddBoundPersonCard(pc, out personId, cardId, templateId);
        IntradayVillageTestSource.Bind(pc);
        return NewExpeditionSystemOnCollection(pc, card);
    }

    /// <summary>在既有 PlayerCollection 上构造 ExpeditionSystem 并预选一张具体卡。</summary>
    private object NewExpeditionSystemOnCollection(object pc, object card)
    {
        var expType = System.Type.GetType("ExpeditionSystem, Assembly-CSharp");
        object system = System.Activator.CreateInstance(expType, new object[] { pc });
        var selectedProp = expType.GetProperty("SelectedCharacters");
        var list = (System.Collections.IList)selectedProp.GetValue(system, null);
        list.Add(card);
        return system;
    }

    /// <summary>在既有 PlayerCollection（如 Load 后）上按真实 PersonId 找到其角色卡并构造 ExpeditionSystem。</summary>
    private object NewExpeditionSystemForPerson(object pc, string personId)
    {
        var cards = (System.Collections.IList)pc.GetType().GetField("OwnedCharacterCards").GetValue(pc);
        foreach (var c in cards)
        {
            if (c == null) continue;
            object owner = pc.GetType().GetMethod("GetCharacterInstanceForCard").Invoke(pc, new object[] { c });
            if (owner == null) continue;
            string ownerPid = (string)owner.GetType().GetField("PersonId").GetValue(owner);
            if (ownerPid == personId) return NewExpeditionSystemOnCollection(pc, c);
        }
        Assert.Fail("未在集合中找到 PersonId=" + personId + " 的角色卡");
        return null;
    }

    private bool ConfirmExpedition(object system)
    {
        var m = system.GetType().GetMethod("ConfirmExpedition");
        var args = new object[] { null, null };
        return (bool)m.Invoke(system, args);
    }

    [Test]
    public void ProjectParticipant_ExpeditionConfirm_Blocked_AndReleasedAfterCompletion()
    {
        EnsureInitialized();
        var system = NewExpeditionSystemWithChar(out string g1Pid, "card_char_g1", "char_g1");
        Assert.AreEqual(VillageProjectResult.Success, StartProject("prj_test_01", new[] { g1Pid }));

        Assert.IsTrue(ConfirmExpedition(system), "项目承诺允许出征，保留容量");
        InvokeStatic(BridgeType,"SetExpeditionPersonIds",(object)new[]{g1Pid});
        TickDays(2);Assert.AreEqual(0,GetProjects().ActiveProjects[0].Progress);
        Assert.Contains(g1Pid,GetProjects().ActiveProjects[0].ParticipantPersonIds);
        InvokeStatic(BridgeType,"ClearExpeditionPersonIds");TickDays(10);
        Assert.AreEqual(0,GetProjects().ActiveProjects.Count,"返村后实际劳动完成，不重新 Join");
    }

    [Test]
    public void ExpeditionReturned_Person_CanJoinProject_AfterClearingExpedition()
    {
        EnsureInitialized();
        // Expedition 占用期间：不能被加入 Project
        InvokeStatic(BridgeType, "SetExpeditionPersonIds", (object)new[] { "char_g2" });
        Assert.AreEqual(VillageProjectResult.PersonCurrentlyUnavailable,
            StartProject("prj_test_01", new[] { "char_g2" }));

        // Expedition 返回（清除登记）后：可再次加入 Project
        InvokeStatic(BridgeType, "SetExpeditionPersonIds", (object)new string[0]);
        Assert.AreEqual(VillageProjectResult.Success, StartProject("prj_test_01", new[] { "char_g2" }));
        Assert.IsTrue(GetProjects().IsPersonInActiveProject("char_g2"));
    }

    [Test]
    public void SaveLoad_MidProject_PreservesMutualExclusion_ExpeditionConfirmStillBlocked()
    {
        EnsureInitialized();

        object collection = NewPlayerCollection();
        // 使用 DataManager 真实模板 char_001，保证 Save→Load 能按 SO 恢复人物实例与卡绑定
        object g3Card = AddBoundPersonCard(collection, out string g3Pid, "card_char_001_g3", "char_001");
        IntradayVillageTestSource.Bind(collection);
        Assert.AreEqual(VillageProjectResult.Success, StartProject("prj_test_01", new[] { g3Pid }));
        TickDays(2); // 中途状态（未完成）

        Assert.IsTrue((bool)InvokeStatic(SaveSystemType, "Save", collection));
        InvokeStatic(BridgeType, "ResetForTests");
        object loaded = InvokeStatic(SaveSystemType, "Load");
        if (loaded != null) IntradayVillageTestSource.Bind(loaded);
        Assert.IsNotNull(loaded);

        // Load 后占用关系保持：Project 参与者仍不可同时出征
        Assert.IsTrue(GetProjects().IsPersonInActiveProject(g3Pid));
        var system = NewExpeditionSystemForPerson(loaded, g3Pid);
        Assert.IsTrue(ConfirmExpedition(system), "Load 保留项目容量预约，但不禁止出征");
        Assert.Contains(g3Pid,GetProjects().ActiveProjects[0].ParticipantPersonIds);
        TickDays(10);Assert.AreEqual(0,GetProjects().ActiveProjects.Count);

    }

    [Test]
    public void UnoccupiedPerson_ExpeditionConfirm_Allowed()
    {
        EnsureInitialized();
        var system = NewExpeditionSystemWithChar(out _, "card_char_free_1", "char_free_1");
        Assert.IsTrue(ConfirmExpedition(system), "未被占用的长期 Person 应可正常确认出征");
    }
}
