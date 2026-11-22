using System.Collections.Generic;

/// <summary>
/// 出征牌组
///
/// 负责：存储出征前选定的角色卡
/// 不负责：永久卡册管理
///
/// Battle V0.1：出征不再选择独立道具卡（Item Card 已退出正式产品路径），
/// 本牌组只承载角色卡；战斗牌库由共享牌库 BattleV1Runtime / SharedDeck 承担。
/// </summary>
public class Deck
{
    /// <summary>
    /// 角色卡列表
    /// </summary>
    public List<CharacterCardData> CharacterCards;

    /// <summary>
    /// 创建出征牌组
    /// </summary>
    public Deck()
    {
        CharacterCards = new List<CharacterCardData>();
    }
}