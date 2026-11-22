using System.Collections.Generic;

/// <summary>
/// 人物长期成长初始种子（仅初始化/测试用；不冻结）。
/// DemoCardPool 只能作为初始化/测试种子，不得成为正式长期牌池结构。
/// 正式初始 Unlocked Pool / Starter 内容未冻结；下列均为演示/测试值。
///
/// 注意：本类位于 Growth.Runtime（纯逻辑，不依赖 Assembly-CSharp 的 PlayerCollection）；
/// 从 PlayerCollection 查找人物的逻辑见 Assembly-CSharp 的 PlayerGrowthLookup。
/// </summary>
public static class GrowthDefaults
{
    /// <summary>
    /// 初始 Unlocked Pool 测试种子：人物 → 初始解锁的角色牌模板ID。
    /// 比单张基础攻击丰富，便于验证 CardGrowth 三选一与 Run 奖励读取新牌。
    /// 【测试值】未冻结。
    /// </summary>
    private static readonly Dictionary<string, string[]> TestUnlockedPoolSeed = new Dictionary<string, string[]>
    {
        { "char_001", new[] { "bcard_aim", "bcard_sword", "bcard_rally" } },   // 战士
        { "char_002", new[] { "bcard_sword", "bcard_aim", "bcard_heal" } },    // 猎人
        { "char_003", new[] { "bcard_heal", "bcard_rally", "bcard_sword" } },  // 医师
        { "char_004", new[] { "bcard_rally", "bcard_heal", "bcard_aim" } },    // 村长
        { "hero_001", new[] { "bcard_sword", "bcard_heal", "bcard_aim" } }     // 青鹿
    };

    public static string[] GetTestUnlockedPoolSeed(string personId)
    {
        return TestUnlockedPoolSeed.TryGetValue(personId ?? string.Empty, out var ids)
            ? ids
            : System.Array.Empty<string>();
    }

    /// <summary>基础攻击牌（初始 Starter Slot 默认占用）：DemoCardPool 第 0 张（测试内容）</summary>
    public static string GetBasicAttackCardId(string personId)
    {
        var ids = BattleV1DemoCardPool.GetCardsForPerson(personId);
        return ids != null && ids.Length > 0 ? ids[0] : string.Empty;
    }

    /// <summary>
    /// 初始化人物长期成长（仅当对应长期状态为空时播种；幂等）。
    /// 在人物入册（初始卡册 / 存档恢复 / 招募）时调用；Run 期间不回写长期状态。
    /// templateId：人物模板 ID（CharacterId / HeroId）。
    /// M1：模板 ID 与实例 PersonId 已分离，播种测试种子必须按模板 ID 查
    /// （TestUnlockedPoolSeed / DemoCardPool 均以模板 ID 组织内容）。
    /// </summary>
    public static void EnsureSeeded(IPersonGrowth person)
    {
        EnsureSeeded(person, null);
    }

    public static void EnsureSeeded(IPersonGrowth person, string templateId)
    {
        if (person == null) return;

        // Initial Person &amp; Profession Bootstrap（§11）：已拥有 Active Profession 的普通 Character，
        // 其 Starter / Unlocked Pool 已由 PersonBootstrapper 按职业模板初始化；
        // 不得再用 Legacy Demo 种子覆盖/污染 Profession 初始内容。
        // Hero（Legacy 权威；IsProfessionGrowth=false）与未来无职业 Person 保持既有播种语义不变。
        IGrowthAuthority authority = GrowthAuthority.Resolve(person);
        if (authority != null && authority.IsProfessionGrowth) return;

        // M1：种子 key 必须是模板 ID（CharacterId/HeroId）；纯逻辑测试（PersonId==模板）下由调用方回退。
        string seedKey = !string.IsNullOrEmpty(templateId) ? templateId : person.PersonId;

        if (person.StarterSlots == null) person.StarterSlots = new List<StarterSlotEntry>();
        if (person.StarterSlots.Count == 0)
        {
            string basic = GetBasicAttackCardId(seedKey);
            person.StarterSlots.Add(new StarterSlotEntry(basic, 1));
        }

        if (person.UnlockedPoolCardIds == null) person.UnlockedPoolCardIds = new List<string>();
        if (person.UnlockedPoolCardIds.Count == 0)
        {
            foreach (var id in GetTestUnlockedPoolSeed(seedKey))
            {
                if (!person.UnlockedPoolCardIds.Contains(id)) person.UnlockedPoolCardIds.Add(id);
            }
        }
    }
}
