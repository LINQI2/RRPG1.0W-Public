using System.Collections.Generic;

/// <summary>
/// 把 Run Pending Rewards 兑现到长期 PlayerCollection 的 IRunRewardSink 实现。
/// 由 AdventureManager.EndRun 使用；角色/英雄经验按 PersonId 写入长期实例。
///
/// M5 Run/Return Growth Semantic Migration（Correction：Commit Acknowledgement）：
///   - AddGrowthExp（Legacy 通道）保持 M4 语义（写 IPersonGrowth.Exp，Hero Legacy Run Growth）；
///   - TryAddProfessionExp（ProfessionExp 通道）经 GrowthAuthority 显式解析并返回 bool：
///     只有“普通 Character 且当前有 Active Profession”才写入其 ProfessionProgress.ProfessionExp 并返回 true；
///     Hero / 无 Profession 普通 Character / 找不到 Person / sink 无法执行一律返回 false
///     （不 fallback Legacy、不猜测职业；由 RunState 依据返回值决定是否清零 Pending）。
/// </summary>
public class PlayerRunRewardSink : IRunRewardSink
{
    private readonly PlayerCollection _playerData;

    public PlayerRunRewardSink(PlayerCollection playerData)
    {
        _playerData = playerData;
    }

    public void AddGold(int amount)
    {
        if (_playerData != null) _playerData.Gold += amount;
    }

    public void AddBuildingMaterial(int amount)
    {
        // 长期建材字段尚未落地（Stage 6 范围外），本阶段仅保留兑现入口，无写入目标。
    }

    public void AddGrowthExp(string personId, int amount)
    {
        if (_playerData == null) return;

        // M1：按具体人物 PersonId 兑现成长经验（同模板多实例互不塌缩）
        IPersonGrowth person = _playerData.FindPersonByPersonId(personId);
        if (person != null) person.Exp += amount;
    }

    public bool TryAddProfessionExp(string personId, int amount)
    {
        if (_playerData == null || amount <= 0) return false;

        // M5：按具体人物 PersonId 尝试兑现 Run ProfessionExp 到该 Person 当前 Profession Growth。
        // 只写 Profession 权威（普通 Character + Active Profession）；Hero 不进入 Profession 迁移，
        // 无 Profession 的普通 Character / 找不到 Person 一律返回 false（不 fallback 到 Legacy Exp）。
        IPersonGrowth person = _playerData.FindPersonByPersonId(personId);
        if (person == null) return false;

        IGrowthAuthority auth = GrowthAuthority.Resolve(person);
        if (auth == null || !auth.IsAvailable || !auth.IsProfessionGrowth) return false;

        auth.Exp += amount;
        return true;
    }
}
