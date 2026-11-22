/// <summary>
/// VillageState —— 共同体级事实（Village.Runtime 纯逻辑）。
///
/// V0.1（Stage V1）只保存共同体级事实，不保存人物私有状态。
/// 本类当前只承载 1 个明确标记为 Test / Not Frozen 的测试生存状态，用于验证
/// Day Tick → State 变化 → Save / Load 闭环。
///
/// 禁止：正式化 Food / Wood / Stone / BuildingMaterial / 资源经济。
/// 所有字段均为测试值、未冻结（Village-GameSpec-V0.1 §2.7 / §8）。
/// </summary>
public class VillageState
{
    /// <summary>
    /// 【Test / Not Frozen】测试生存状态值。
    /// 测试规则：每个 Day Tick → TestSurvivalValue += TestDailyChange。
    /// 不是正式资源、不是正式经济数值。
    /// </summary>
    public int TestSurvivalValue;

    /// <summary>【Test / Not Frozen】每个 Day Tick 的变化量（默认 1）。</summary>
    public int TestDailyChange = 1;

    /// <summary>Day Tick 处理：应用测试变化规则。</summary>
    public void OnDayTick()
    {
        TestSurvivalValue += TestDailyChange;
    }

    public VillageStateSaveData ToSaveData()
    {
        return new VillageStateSaveData
        {
            TestSurvivalValue = TestSurvivalValue
        };
    }

    public void Restore(VillageStateSaveData data)
    {
        if (data == null) return;
        TestSurvivalValue = data.TestSurvivalValue;
    }
}
