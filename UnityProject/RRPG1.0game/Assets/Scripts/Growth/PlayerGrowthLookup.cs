/// <summary>
/// 从 PlayerCollection 查找人物长期成长状态的辅助（Assembly-CSharp 层）。
/// M1：一律按真正 PersonId 查找（具体人物唯一查询），不再把模板 CharacterId/HeroId 当人物身份。
/// 角色优先，其次英雄。
/// </summary>
public static class PlayerGrowthLookup
{
    public static IPersonGrowth FindPerson(PlayerCollection playerData, string personId)
    {
        if (playerData == null || string.IsNullOrEmpty(personId)) return null;

        // M1：PersonId 权威查询——同模板多实例互不塌缩
        return playerData.FindPersonByPersonId(personId);
    }
}
