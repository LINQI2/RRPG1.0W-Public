using System;
using System.Collections.Generic;

/// <summary>
/// 战前部署区
///
/// 负责：管理战前角色卡的抽取、部署与流转
/// 不负责：战斗流程控制、卡组管理
///
/// 流程：
///   从 Deck 随机抽取6张角色卡 → DrawnCards
///   玩家选择上阵 → DeployedCards
///   未上阵角色卡 → RemainingCards → Hand
///
/// 部署区不受手牌上限限制
/// 上阵后角色卡消失，转化为 BattleUnit
/// </summary>
public class PreparationZone
{
    /// <summary>
    /// 被抽出的6张角色卡
    /// </summary>
    public List<CharacterCardData> DrawnCards { get; private set; }

    /// <summary>
    /// 被选择上阵的角色卡
    /// </summary>
    public List<CharacterCardData> DeployedCards { get; private set; }

    /// <summary>
    /// 未上阵的角色卡（将进入手牌）
    /// </summary>
    public List<CharacterCardData> RemainingCards { get; private set; }

    /// <summary>
    /// 创建战前部署区
    /// 从 Deck 中随机抽取6张角色卡
    /// </summary>
    /// <param name="deck">出征牌组</param>
    public PreparationZone(Deck deck)
    {
        DrawnCards = new List<CharacterCardData>();
        DeployedCards = new List<CharacterCardData>();
        RemainingCards = new List<CharacterCardData>();

        // 从 Deck 随机抽取最多6张角色卡
        var shuffled = new List<CharacterCardData>(deck.CharacterCards);
        Shuffle(shuffled);

        int drawCount = Math.Min(6, shuffled.Count);
        for (int i = 0; i < drawCount; i++)
        {
            DrawnCards.Add(shuffled[i]);
        }
    }

    /// <summary>
    /// 上阵指定角色卡
    /// 将卡从部署区移入上阵列表
    /// </summary>
    /// <param name="card">要上阵的角色卡</param>
    /// <returns>true=上阵成功，false=卡不在部署区</returns>
    public bool DeployCard(CharacterCardData card)
    {
        if (!DrawnCards.Contains(card) || DeployedCards.Contains(card))
            return false;

        DrawnCards.Remove(card);
        DeployedCards.Add(card);
        return true;
    }

    /// <summary>
    /// 完成战前部署
    /// 未上阵的角色卡进入手牌
    /// </summary>
    /// <param name="hand">战斗手牌</param>
    public void Finalize(Hand hand)
    {
        foreach (var card in DrawnCards)
        {
            RemainingCards.Add(card);
            hand.CharacterCards.Add(card);
        }
        DrawnCards.Clear();
    }

    /// <summary>
    /// Fisher-Yates 洗牌
    /// </summary>
    private void Shuffle<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            T temp = list[i];
            list[i] = list[j];
            list[j] = temp;
        }
    }
}