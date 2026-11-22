using System.Collections;
using System.Reflection;
using NUnit.Framework;

/// <summary>
/// V4F Project ↔ Expedition 双向互斥冻结（Final Development Spec 修订 3 / 测试计划 T15）。
///   (a) Active Project 参与者被确认出征 → 拒绝；
///   (b) 已登记远征者被加入 Project → PersonCurrentlyUnavailable（含整批/追加两条路径）；
///   (c) 构造重叠数据（Restore/异常层防御）→ Catch-up 拒绝推进、不产生 Project 进度 / World 痕迹。
/// </summary>
public class ProjectExpeditionMutualExclusionTests
{
    [SetUp]
    public void SetUp()
    {
        V4FTestKit.ResetBridge();
        V4FTestKit.DeleteSave();
        V4FTestKit.EnsureInitialized();
        V4FTestKit.SetTime(1, 1, 1);
    }

    [TearDown]
    public void TearDown()
    {
        V4FTestKit.ResetBridge();
        V4FTestKit.DeleteSave();
    }

    private static object NewExpeditionSystem(object collection, object card)
    {
        object exp = System.Activator.CreateInstance(V4FTestKit.Asm("ExpeditionSystem"), new object[] { collection });
        var selected = (IList)exp.GetType().GetProperty("SelectedCharacters").GetValue(exp, null);
        selected.Add(card);
        return exp;
    }

    private static bool ConfirmExpedition(object expedition)
    {
        var m = expedition.GetType().GetMethod("ConfirmExpedition",
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(m, "ConfirmExpedition 应存在");
        return (bool)m.Invoke(expedition, new object[] { null, null });
    }

    [Test]
    public void T15a_ProjectReservation_DepartureAllowed_NoPrematureExpeditionRegistration()
    {
        var collection = V4FTestKit.NewPlayerCollection();
        object character;
        string pid;
        V4FTestKit.AddCharacterWithCard(collection, "char_001", "card_v4f_prj", out character, out pid);

        Assert.AreEqual(VillageProjectResult.Success, V4FTestKit.StartProject("prj_test_01", pid),
            "该 Person 先成为 Active Project 参与者");

        object card = V4FTestKit.OwnedCharacterCards(collection)[0];
        object expedition = NewExpeditionSystem(collection, card);
        Assert.IsTrue(ConfirmExpedition(expedition), "容量预约保留但允许出征");
        Assert.AreEqual(0, V4FTestKit.ExpeditionIds().Count, "被拦截时不得登记 Expedition Occupancy");
    }

    [Test]
    public void T15b_ExpeditionPerson_JoinProject_IsRejected_PersonCurrentlyUnavailable()
    {
        V4FTestKit.SetExpedition(new[] { "char_busy" });

        // 整批启动：远征者直接作为项目首批参与者 → 拒绝。
        Assert.AreEqual(VillageProjectResult.PersonCurrentlyUnavailable,
            V4FTestKit.StartProject("prj_test_01", "char_busy"));

        // 追加路径：先建立不含远征者的 Active Project，再把远征者追加为参与者 → 拒绝。
        Assert.AreEqual(VillageProjectResult.Success, V4FTestKit.StartProject("prj_test_01", "char_ok"));
        var project = V4FTestKit.Projects().ActiveProjects[0];
        Assert.AreEqual(VillageProjectResult.PersonCurrentlyUnavailable,
            V4FTestKit.AddProjectParticipant(project.ProjectId, "char_busy"));
    }

    [Test]
    public void T15c_ProjectReservation_AwayNoLabor_ReturnResumesWithoutJoin()
    {
        const string id="char_reserved";V4FTestKit.BindWorkPersons(id);
        Assert.AreEqual(VillageProjectResult.Success,V4FTestKit.StartProject("prj_test_01",id));
        V4FTestKit.SetExpedition(new[]{id});Assert.IsTrue(V4FTestKit.BeginReturn(2));Assert.IsTrue(V4FTestKit.MarkSettled());
        Assert.AreEqual(ExpeditionCatchUpResult.Completed,V4FTestKit.RunCatchUp());
        var project=V4FTestKit.Projects().ActiveProjects[0];Assert.AreEqual(0,project.Progress);Assert.IsTrue(project.ExecutionPaused);
        Assert.Contains(id,project.ParticipantPersonIds);Assert.AreEqual(2,V4FTestKit.ActiveRecord().DaysSimulated);
        V4FTestKit.ClearExpedition();V4FTestKit.CompleteSave(true);V4FTestKit.BridgeTime().TickDay();
        Assert.AreEqual(1,project.Progress);Assert.AreEqual(1,project.ParticipantPersonIds.Count);
    }
}
