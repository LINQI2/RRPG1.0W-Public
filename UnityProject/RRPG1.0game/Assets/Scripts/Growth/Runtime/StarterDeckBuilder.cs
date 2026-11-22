using System.Collections.Generic;

/// <summary>
/// 从人物长期 Starter 槽构筑 Run 初始牌条目：
///   - 每个非空 Starter Slot 生成 1 张对应起始牌副本（归属 = 该人物）；
///   - 通用牌全量进入（不绑定人物）。
///
/// Run 内新增 Copies 不回写长期 Starter / Proficiency；
/// 人物不上阵时，其角色牌仍由既有 SharedDeck.FilterValidCards 上阵过滤规则暂时不可抽。
/// </summary>
public static class StarterDeckBuilder
{
    public static List<BattlePersonCardEntry> BuildStarterEntries(
        IEnumerable<IPersonGrowth> persons, IEnumerable<BattleCardData> allTemplates)
    {
        var result = new List<BattlePersonCardEntry>();
        if (persons == null || allTemplates == null) return result;

        var templates = new List<BattleCardData>();
        foreach (var t in allTemplates)
        {
            if (t != null && !string.IsNullOrEmpty(t.CardId)) templates.Add(t);
        }

        foreach (var person in persons)
        {
            if (person == null || string.IsNullOrEmpty(person.PersonId) || person.StarterSlots == null) continue;

            foreach (var slot in person.StarterSlots)
            {
                if (slot == null || slot.IsEmpty) continue;
                var template = templates.Find(x => x.CardId == slot.CardId && x.CardKind == BattleCardKind.Character);
                if (template != null)
                    result.Add(new BattlePersonCardEntry(template, person.PersonId, 1));
            }
        }

        // 通用牌：不绑定人物，始终进入本 Run
        foreach (var template in templates)
        {
            if (template.CardKind == BattleCardKind.Common)
                result.Add(new BattlePersonCardEntry(template, null, 1));
        }

        return result;
    }
}
