using System;

/// <summary>
/// Battle V0.1 玩家回合生命周期（纯逻辑，不依赖 UnityEngine）
///
/// 职责：在现有共享牌库（SharedDeck）与共享行动力（SharedActionPointPool）之上，
/// 管理「玩家回合 ↔ 敌方阶段」的最小回合推进，以及出牌的校验/支付/去向。
///
/// 设计依据（Battle GameSpec V0.1）：
///   - 玩家回合开始：恢复共享行动力（R12.3）、按当前有效抽牌规则抽牌（R10.2）；
///   - 出牌：行动力充足才可合法使用（R12.2），支付后普通牌进弃牌堆、Exhaust 牌进消耗区（R11.1/R11.5）；
///   - 玩家回合结束：普通剩余手牌弃置，Retain 牌保留（R11.2/R11.4）；
///   - 抽牌堆不足时，SharedDeck.Draw 会把弃牌堆洗回抽牌堆继续抽（R11.3）。
///
/// 不负责：效果结算（伤害/治疗等由上层对 BattleUnit 执行）、UI 渲染、敌方具体行动。
/// 上阵人物来源为 Stage 3 正式迁移项：本类只消费 drawCount，不关心上阵名单如何产生。
/// </summary>
public class BattleV1TurnFlow
{
    /// <summary>当前回合阶段</summary>
    public enum PhaseType
    {
        /// <summary>玩家回合（可以出牌、可以结束回合）</summary>
        PlayerTurn,

        /// <summary>敌方阶段（玩家不可出牌）</summary>
        EnemyTurn
    }

    public SharedDeck Deck { get; }
    public SharedActionPointPool ActionPoints { get; }

    /// <summary>
    /// 规则执行上下文（由宿主注入的真实 Battle 只读视图 + 写入接缝）。
    /// 为 null 时只做阶段/手牌/行动力校验，不做配置化出牌前置校验（纯模块测试场景）。
    /// </summary>
    public IBattleRuleContext RuleContext { get; set; }

    /// <summary>当前阶段（初始为敌方阶段，首个 BeginPlayerTurn 才进入玩家回合）</summary>
    public PhaseType Phase { get; private set; }

    /// <summary>当前玩家回合号（每次 BeginPlayerTurn 递增）</summary>
    public int TurnNumber { get; private set; }

    public BattleV1TurnFlow(SharedDeck deck, SharedActionPointPool actionPoints)
    {
        Deck = deck ?? throw new ArgumentNullException(nameof(deck));
        ActionPoints = actionPoints ?? throw new ArgumentNullException(nameof(actionPoints));
        Phase = PhaseType.EnemyTurn;
        TurnNumber = 0;
    }

    /// <summary>
    /// 玩家回合开始：回合号+1、共享行动力恢复至上限、按 drawCount 抽牌。
    /// 返回实际抽到张数（抽牌堆与弃牌堆都耗尽时可能少于 drawCount）。
    /// </summary>
    public int BeginPlayerTurn(int drawCount)
    {
        TurnNumber++;
        Phase = PhaseType.PlayerTurn;
        ActionPoints.RestoreToMax();
        return Deck.Draw(Math.Max(0, drawCount)).Count;
    }

    /// <summary>
    /// 校验一张牌当前是否可合法打出：
    ///   处于玩家回合；牌在手牌中；共享行动力足以支付 Actual Cost；
    ///   配置化牌还必须证明其 Primary Resolution 已存在合法结果（在支付 AP 之前判断）。
    /// </summary>
    public bool CanPlay(BattleCard card)
    {
        if (Phase != PhaseType.PlayerTurn) return false;
        if (card == null) return false;
        if (!Deck.Hand.Contains(card)) return false;
        if (!ActionPoints.CanAfford(card.ActualCost)) return false;

        if (BattleCardRuleResolver.HasRuleProgram(card))
        {
            var legality = BattleCardRuleResolver.CheckPlayable(card, RuleContext);
            if (!legality.IsLegal) return false;
        }

        return true;
    }

    /// <summary>支付牌的实际费用（仅在 CanPlay 通过后调用）</summary>
    public bool PayCost(BattleCard card)
    {
        if (card == null) return false;
        return ActionPoints.Spend(card.ActualCost);
    }

    /// <summary>结算一张已打出牌的去向：Exhaust 牌进消耗区，普通牌进弃牌堆</summary>
    public bool ResolvePlayedCard(BattleCard card)
    {
        if (card == null) return false;
        return Deck.ResolvePlayedCard(card);
    }

    /// <summary>
    /// 玩家回合结束：弃置手牌中所有未标记 Retain 的普通牌，然后进入敌方阶段。
    /// 返回被弃置的张数。
    /// </summary>
    public int EndPlayerTurn()
    {
        if (Phase != PhaseType.PlayerTurn) return 0;

        var toDiscard = Deck.GetNonRetainedHand();
        foreach (var card in toDiscard)
        {
            Deck.Discard(card);
        }

        Phase = PhaseType.EnemyTurn;
        return toDiscard.Count;
    }

    /// <summary>显式进入敌方阶段（供需要独立触发敌方阶段的宿主调用）</summary>
    public void BeginEnemyTurn()
    {
        Phase = PhaseType.EnemyTurn;
    }
}
