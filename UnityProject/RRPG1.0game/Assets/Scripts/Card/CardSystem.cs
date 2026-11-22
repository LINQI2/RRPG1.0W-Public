using System.Collections.Generic;

/// <summary>
/// 卡牌系统
///
/// 负责：协调出征牌组与战前部署区容器
/// 不负责：战斗流程控制、技能逻辑
///
/// 管理容器：Hand（角色手牌）/ Deck / PreparationZone / DeckArchive
/// Battle V0.1：不再管理独立道具牌堆（DrawPile / DiscardPile ItemCards 已退出正式产品路径），
/// 战斗抽牌与弃牌由共享牌库 BattleV1Runtime / SharedDeck 承担。
/// </summary>
public class CardSystem
{
    /// <summary>
    /// 全局卡册（永久存储）
    /// </summary>
    public DeckArchive Archive { get; private set; }

    /// <summary>
    /// 出征牌组
    /// </summary>
    public Deck CurrentDeck { get; private set; }

    /// <summary>
    /// 创建卡牌系统
    /// </summary>
    public CardSystem()
    {
        Archive = new DeckArchive();
    }

    /// <summary>
    /// 准备出征：从卡册选择角色卡组成出征牌组
    /// </summary>
    /// <param name="characterCards">选择的角色卡</param>
    public void PrepareDeck(List<CharacterCardData> characterCards)
    {
        CurrentDeck = new Deck();
        CurrentDeck.CharacterCards = new List<CharacterCardData>(characterCards);
    }

    /// <summary>
    /// 初始化战前部署区
    /// </summary>
    /// <param name="hand">手牌</param>
    /// <returns>部署区</returns>
    public PreparationZone SetupPreparationZone(Hand hand)
    {
        var prepZone = new PreparationZone(CurrentDeck);
        prepZone.Finalize(hand);
        return prepZone;
    }

    /// <summary>
    /// 从卡册获取所有可用角色
    /// </summary>
    public List<CharacterCardData> GetAvailableCharacters()
    {
        var available = new List<CharacterCardData>();
        foreach (var card in Archive.OwnedCharacters)
        {
            available.Add(card);
        }
        return available;
    }

    /// <summary>
    /// 角色死亡后从卡册移除
    /// </summary>
    /// <param name="characterId">角色ID</param>
    public void RemoveDeadCharacter(string characterId)
    {
        Archive.OwnedCharacters.RemoveAll(c => c.CharacterId == characterId);
    }
}
