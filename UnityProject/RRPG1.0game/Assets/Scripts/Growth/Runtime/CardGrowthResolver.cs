using System.Collections.Generic;
using System.Linq;

/// <summary>
/// CardGrowth（选择型）纯逻辑：从人物 Unlocked Pool 生成三选一候选；把选定牌应用到 Starter。
/// 不负责 UI / 存档。
///
/// Starter 规则：
///   - 有空 Starter 槽：可放入任意空槽，也允许主动替换已有槽（不强制填空槽）；
///   - 槽已满：只能替换一个已有槽；
///   - 选中的牌与当前某个 Starter 中的牌相同：可改为该 Starter 牌 Proficiency +1。
/// </summary>
public static class CardGrowthResolver
{
    /// <summary>一次 CardGrowth 候选数（池不足时返回全量去重候选）</summary>
    public const int OfferCount = 3;

    /// <summary>从人物当前 Unlocked Pool 生成候选（去重；不超过 OfferCount）。
    /// M4-correction：经 GrowthAuthority.Resolve 解析权威 —— 普通 Character（有 Active Profession）只读
    /// ProfessionProgress.UnlockedPoolCardIds；无 Active Profession 的普通 Character 返回空（不 fallback 旧池）。</summary>
    public static List<BattleCardData> BuildOffer(IPersonGrowth person, IEnumerable<BattleCardData> allTemplates)
    {
        var result = new List<BattleCardData>();
        if (person == null || allTemplates == null) return result;

        IGrowthAuthority auth = GrowthAuthority.Resolve(person);
        if (auth == null || !auth.IsAvailable) return result; // 无 Profession 不执行 CardGrowth Offer，不 fallback
        if (auth.UnlockedPoolCardIds == null) return result;

        var templates = new List<BattleCardData>();
        foreach (var t in allTemplates)
        {
            if (t != null && !string.IsNullOrEmpty(t.CardId)) templates.Add(t);
        }

        foreach (var id in auth.UnlockedPoolCardIds)
        {
            if (string.IsNullOrEmpty(id)) continue;
            if (result.Any(c => c.CardId == id)) continue;
            var t = templates.Find(x => x.CardId == id && x.CardKind == BattleCardKind.Character);
            if (t != null) result.Add(t);
            if (result.Count >= OfferCount) break;
        }
        return result;
    }

    /// <summary>应用结果</summary>
    public struct ApplyResult
    {
        public bool Applied;
        public string Message;
    }

    /// <summary>
    /// 把选定的牌应用到人物 Starter（Starter 始终人物自身长期字段 —— 普通 Character 的 Starter 仍 Character-owned）。
    /// slotIndex：仅 AddToEmptySlot / ReplaceSlot 使用；AddToEmptySlot 传 -1 = 自动第一个空槽。
    /// M4-correction：经 GrowthAuthority.Resolve 门禁；无 Active Profession 的普通 Character 不可执行。
    /// </summary>
    public static ApplyResult ApplyToStarter(IPersonGrowth person, string cardId,
        StarterCardStrategy strategy, int slotIndex, GrowthCurveConfig curve)
    {
        if (person == null || string.IsNullOrEmpty(cardId))
            return new ApplyResult { Applied = false, Message = "无效选择" };

        IGrowthAuthority auth = GrowthAuthority.Resolve(person);
        if (auth == null || !auth.IsAvailable)
            return new ApplyResult { Applied = false, Message = "未形成职业，暂不可执行成长" };
        List<StarterSlotEntry> starter = auth.StarterSlots;

        switch (strategy)
        {
            case StarterCardStrategy.ProficiencyUp:
            {
                int cap = curve != null && curve.ProficiencyCap > 0 ? curve.ProficiencyCap : int.MaxValue;
                for (int i = 0; i < starter.Count; i++)
                {
                    if (starter[i] == null) continue;
                    if (starter[i].CardId == cardId && starter[i].Proficiency < cap)
                    {
                        starter[i].Proficiency++;
                        return new ApplyResult
                        {
                            Applied = true,
                            Message = "Starter 牌熟练 +1 → Lv " + starter[i].Proficiency
                        };
                    }
                }
                return new ApplyResult { Applied = false, Message = "无可提升的同模板 Starter 牌（已达上限或无）" };
            }

            case StarterCardStrategy.AddToEmptySlot:
            {
                int idx = slotIndex;
                if (idx < 0)
                {
                    idx = -1;
                    for (int i = 0; i < starter.Count; i++)
                    {
                        if (starter[i] == null || starter[i].IsEmpty) { idx = i; break; }
                    }
                    if (idx < 0)
                        return new ApplyResult { Applied = false, Message = "没有空 Starter 槽（可替换已有槽）" };
                }
                if (idx >= starter.Count)
                    return new ApplyResult { Applied = false, Message = "槽位不存在" };

                var slot = starter[idx];
                if (slot == null) slot = starter[idx] = new StarterSlotEntry();
                if (!slot.IsEmpty)
                    return new ApplyResult { Applied = false, Message = "目标槽已被占用（可替换）" };

                slot.CardId = cardId;
                slot.Proficiency = 1;
                return new ApplyResult { Applied = true, Message = "牌已放入 Starter 槽 " + idx };
            }

            case StarterCardStrategy.ReplaceSlot:
            {
                if (slotIndex < 0 || slotIndex >= starter.Count)
                    return new ApplyResult { Applied = false, Message = "槽位不存在" };

                var slot = starter[slotIndex];
                if (slot == null) slot = starter[slotIndex] = new StarterSlotEntry();
                slot.CardId = cardId;
                slot.Proficiency = 1; // 替换后高级 Proficiency 不保留，从基础熟练重新培养
                return new ApplyResult { Applied = true, Message = "牌已替换 Starter 槽 " + slotIndex + "（熟练重置为 1）" };
            }

            default:
                return new ApplyResult { Applied = false, Message = "未知策略" };
        }
    }
}
