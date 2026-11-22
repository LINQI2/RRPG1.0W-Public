using System.Collections.Generic;

/// <summary>
/// Battle V0.1 测试人物-角色牌池映射（Milestone A 演示内容，最小且可替换）
///
/// 背景：BattleCardData 是静态牌模板（不承载人物归属）；归属必须存在于 Run/战斗运行时层
/// （BattlePersonCardEntry = 模板 + PersonId + 份数）。正式「Run 人物牌池内容系统」尚未实现，
/// 因此本类提供明确的、按人物模板ID（CharacterData.CharacterId）过滤的测试牌池，供当前演示
/// 内容使用；正式内容系统落地后由它替换本映射，不修改 BattleCardData 分层。
///
/// 语义（Current GameSpec R8.1/R20）：
///   - 角色牌绑定具体人物；只有本场实际参战人物拥有的角色牌才能进入共享牌库；
///   - 不在战场的医师/村长等，其「治疗」「鼓舞」等角色牌不得进入本场。
/// Common 牌（当前 0 张）始终全队有效，不绑定人物。
/// </summary>
public static class BattleV1DemoCardPool
{
    /// <summary>人物模板ID → 该人物拥有的角色牌模板ID列表（Milestone A 测试内容）</summary>
    private static readonly Dictionary<string, string[]> PersonCardMap = new Dictionary<string, string[]>
    {
        { "char_001", new[] { "bcard_aim" } },                // 战士 → 瞄准射击
        { "char_002", new[] { "bcard_sword" } },              // 猎人 → 挥砍
        { "char_003", new[] { "bcard_heal" } },               // 医师 → 治疗
        { "char_004", new[] { "bcard_rally" } },              // 村长 → 鼓舞
        { "hero_001", new[] { "bcard_sword" } }               // 英雄（青鹿）→ 挥砍（基础攻击牌）
    };

    /// <summary>只读牌池映射（供诊断/测试）</summary>
    public static IReadOnlyDictionary<string, string[]> GetPersonCardMap() => PersonCardMap;

    /// <summary>返回指定人物的角色牌模板ID列表（无归属返回空）</summary>
    public static string[] GetCardsForPerson(string personId)
    {
        if (string.IsNullOrEmpty(personId)) return System.Array.Empty<string>();
        return PersonCardMap.TryGetValue(personId, out var ids) ? ids : System.Array.Empty<string>();
    }

    /// <summary>
    /// 按本场上阵人物ID列表 + 全部牌模板，构筑本场角色牌条目：
    ///   - 每个上阵人物只获得其映射中拥有的角色牌模板（每张1份）；
    ///   - Common 模板全量进入（不绑定人物）；
    /// 返回的条目可直接交给 SharedDeck.FilterValidCards / BuildDeck。
    /// </summary>
    public static List<BattlePersonCardEntry> BuildRoster(
        ICollection<string> deployedPersonIds,
        IEnumerable<BattleCardData> allTemplates)
    {
        var roster = new List<BattlePersonCardEntry>();
        if (allTemplates == null) return roster;

        var templates = new List<BattleCardData>();
        foreach (var t in allTemplates)
        {
            if (t == null || string.IsNullOrEmpty(t.CardId)) continue;
            templates.Add(t);
        }

        if (deployedPersonIds != null)
        {
            foreach (var personId in deployedPersonIds)
            {
                if (string.IsNullOrEmpty(personId)) continue;

                foreach (var cardId in GetCardsForPerson(personId))
                {
                    var template = templates.Find(x => x.CardId == cardId);
                    if (template != null && template.CardKind == BattleCardKind.Character)
                        roster.Add(new BattlePersonCardEntry(template, personId, 1));
                }
            }
        }

        // Common 牌：不绑定人物，始终进入本场
        foreach (var template in templates)
        {
            if (template.CardKind == BattleCardKind.Common)
                roster.Add(new BattlePersonCardEntry(template, null, 1));
        }

        return roster;
    }
}
