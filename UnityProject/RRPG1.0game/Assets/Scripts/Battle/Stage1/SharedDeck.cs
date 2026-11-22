using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Battle V0.1 共享牌库（纯逻辑，不依赖 UnityEngine）
///
/// 职责：管理一套全队共享牌库的完整牌堆循环：
///   Draw → Hand → Discard → Reshuffle
/// 并保留扩展能力：
///   Exhaust（消耗区：牌打出后退出本场循环）
///   Retain（滞留：回合结束时仍留手牌）
///
/// 设计要点（对应 Battle GameSpec V0.1 相关规则）：
///   - 一套共享牌库：本场有效牌 = 上阵具体人物的角色牌 + 有效通用牌。
///     牌库由运行时"人物牌条目"（BattlePersonCardEntry = 模板 + 具体人物归属 + 本Run份数）构筑，
///     静态模板不承载归属与份数（见 BattleCardData / BattlePersonCardEntry）。
///   - 抽牌堆为空时，弃牌堆洗回抽牌堆（标准 Reshuffle）。
///   - 牌堆变化通过 DeckChanged 事件对外广播（普通 C# 事件，不依赖 EventBus，
///     以便本类保持在独立运行时程序集中，绕开团结引擎 asmdef→预定义程序集引用限制）。
///
/// 不负责：共享行动力（SharedActionPointPool）、出牌交互、回合流程（后续 Stage）。
/// </summary>
public class SharedDeck
{
    /// <summary>牌堆变化事件（构筑/洗牌/抽牌/弃牌/消耗后触发）</summary>
    public event System.Action DeckChanged;

    /// <summary>抽牌堆</summary>
    public List<BattleCard> DrawPile { get; private set; }

    /// <summary>手牌</summary>
    public List<BattleCard> Hand { get; private set; }

    /// <summary>弃牌堆</summary>
    public List<BattleCard> DiscardPile { get; private set; }

    /// <summary>消耗区（Exhaust）</summary>
    public List<BattleCard> ExhaustPile { get; private set; }

    private readonly System.Random _rng;

    public int DrawPileCount => DrawPile.Count;
    public int HandCount => Hand.Count;
    public int DiscardPileCount => DiscardPile.Count;
    public int ExhaustPileCount => ExhaustPile.Count;

    /// <summary>
    /// 创建共享牌库
    /// </summary>
    /// <param name="rng">随机源（可注入种子使测试确定化；默认 System.Random）</param>
    public SharedDeck(System.Random rng = null)
    {
        _rng = rng ?? new System.Random();
        DrawPile = new List<BattleCard>();
        Hand = new List<BattleCard>();
        DiscardPile = new List<BattleCard>();
        ExhaustPile = new List<BattleCard>();
    }

    /// <summary>
    /// 本场有效牌过滤
    /// 规则：
    ///   角色牌（条目含具体人物归属 PersonId）：仅当该人物在本场上阵时有效；
    ///   通用牌（条目 PersonId 为空）：始终有效（不依赖具体人物）。
    /// 同一模板可被多个不同人物各自拥有（各自独立条目），仅上阵人物的条目生效。
    /// </summary>
    /// <param name="candidates">全部"人物牌条目"候选（本 Run 各人物拥有的牌）</param>
    /// <param name="deployedPersonIds">本场已上阵的具体人物实例ID集合</param>
    /// <returns>本场有效人物牌条目列表</returns>
    public static List<BattlePersonCardEntry> FilterValidCards(IEnumerable<BattlePersonCardEntry> candidates, ICollection<string> deployedPersonIds)
    {
        var result = new List<BattlePersonCardEntry>();
        if (candidates == null) return result;

        foreach (var entry in candidates)
        {
            if (entry == null || entry.Template == null) continue;

            if (string.IsNullOrEmpty(entry.PersonId))
            {
                // 通用牌总是有效
                result.Add(entry);
            }
            else
            {
                if (deployedPersonIds != null && deployedPersonIds.Contains(entry.PersonId))
                {
                    result.Add(entry);
                }
            }
        }

        return result;
    }

    /// <summary>
    /// 构筑本场牌库
    /// 将每个"人物牌条目"按条目份数实例化放入抽牌堆（尚未洗牌，调用方需自行 Reshuffle）。
    /// 份数来自运行时条目（本 Run 状态），不来自静态牌模板。
    /// </summary>
    public void BuildDeck(IEnumerable<BattlePersonCardEntry> entries)
    {
        DrawPile.Clear();
        Hand.Clear();
        DiscardPile.Clear();
        ExhaustPile.Clear();

        if (entries == null) return;

        foreach (var entry in entries)
        {
            if (entry == null || entry.Template == null || string.IsNullOrEmpty(entry.Template.CardId)) continue;

            int copies = Math.Max(1, entry.Copies);
            for (int i = 0; i < copies; i++)
            {
                DrawPile.Add(BattleCard.FromEntry(entry, i));
            }
        }

        DeckChanged?.Invoke();
    }

    /// <summary>
    /// 洗牌（Fisher-Yates）
    /// 对抽牌堆重排。
    /// </summary>
    public void Reshuffle()
    {
        for (int i = DrawPile.Count - 1; i > 0; i--)
        {
            int j = _rng.Next(i + 1);
            BattleCard tmp = DrawPile[i];
            DrawPile[i] = DrawPile[j];
            DrawPile[j] = tmp;
        }

        DeckChanged?.Invoke();
    }

    /// <summary>
    /// 抽牌
    /// 从抽牌堆顶部抽取指定张数；抽牌堆为空时把弃牌堆洗回抽牌堆再抽。
    /// 抽牌堆与弃牌堆同时为空时停止。
    /// </summary>
    /// <param name="count">期望抽取张数</param>
    /// <returns>实际抽到的牌（移入手牌）</returns>
    public List<BattleCard> Draw(int count)
    {
        var drawn = new List<BattleCard>();

        for (int i = 0; i < count; i++)
        {
            if (DrawPile.Count == 0)
            {
                if (DiscardPile.Count == 0) break;

                // 弃牌堆洗回抽牌堆（Current Deck Cycle Reset 接缝）：
                // 只有真正进入新 Draw Cycle 的具体牌才清空本 Cycle 临时修正；
                // Hand 中 Retain 的牌没有进入新 Cycle，因此不因其它牌重洗而重置。
                foreach (var recycled in DiscardPile)
                {
                    if (recycled != null) recycled.ResetCurrentDeckCycleModifiers();
                }

                DrawPile.AddRange(DiscardPile);
                DiscardPile.Clear();
                ShuffleInPlace(DrawPile);
            }

            var card = DrawPile[DrawPile.Count - 1];
            DrawPile.RemoveAt(DrawPile.Count - 1);
            Hand.Add(card);
            drawn.Add(card);
        }

        if (drawn.Count > 0)
        {
            DeckChanged?.Invoke();
        }

        return drawn;
    }

    /// <summary>
    /// 弃牌：从手牌移入弃牌堆
    /// </summary>
    public bool Discard(BattleCard card)
    {
        if (card == null || !Hand.Remove(card)) return false;

        DiscardPile.Add(card);
        DeckChanged?.Invoke();
        return true;
    }

    /// <summary>
    /// 消耗：从手牌移入消耗区（Exhaust）
    /// </summary>
    public bool Exhaust(BattleCard card)
    {
        if (card == null || !Hand.Remove(card)) return false;

        ExhaustPile.Add(card);
        DeckChanged?.Invoke();
        return true;
    }

    /// <summary>
    /// Retain 接口：标记手牌中的牌为滞留
    /// 滞留牌在回合结束时不会进入弃牌堆（具体回合结束逻辑在 Stage 2 接入）。
    /// </summary>
    public bool Retain(BattleCard card)
    {
        if (card == null || !Hand.Contains(card)) return false;

        card.Retain = true;
        return true;
    }

    /// <summary>
    /// Current GameSpec R15.3：角色本场失去战斗能力后，其角色牌退出本场全部牌区循环。
    /// 从手牌 / 抽牌堆 / 弃牌堆 / 消耗区移除指定人物的全部角色牌（幂等）。
    /// </summary>
    /// <param name="personId">具体人物归属ID（BattleCard.OwnerPersonId）</param>
    /// <returns>被移除的牌数量</returns>
    public int RemovePersonCards(string personId)
    {
        if (string.IsNullOrEmpty(personId)) return 0;

        int removed = 0;
        removed += Hand.RemoveAll(c => c != null && c.OwnerPersonId == personId);
        removed += DrawPile.RemoveAll(c => c != null && c.OwnerPersonId == personId);
        removed += DiscardPile.RemoveAll(c => c != null && c.OwnerPersonId == personId);
        removed += ExhaustPile.RemoveAll(c => c != null && c.OwnerPersonId == personId);

        if (removed > 0)
            DeckChanged?.Invoke();

        return removed;
    }

    /// <summary>
    /// 取出本回合结束时应当弃入弃牌堆的牌（手牌中未标记 Retain 的牌）
    /// 供 Stage 2 回合结束阶段使用。
    /// </summary>
    public List<BattleCard> GetNonRetainedHand()
    {
        return Hand.Where(c => !c.Retain).ToList();
    }

    /// <summary>
    /// 结算一张被打出的牌的去向：
    ///   Exhaust 牌 → 消耗区；
    ///   其他牌 → 弃牌堆。
    /// </summary>
    public bool ResolvePlayedCard(BattleCard card)
    {
        if (card == null) return false;

        return card.IsExhaust ? Exhaust(card) : Discard(card);
    }

    private void ShuffleInPlace(List<BattleCard> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = _rng.Next(i + 1);
            BattleCard tmp = list[i];
            list[i] = list[j];
            list[j] = tmp;
        }
    }
}

