using System.Collections.Generic;

/// <summary>
/// 战斗手牌区
///
/// 负责：管理战斗中的角色手牌（战前未上阵角色卡）
/// 不负责：卡组构筑、抽牌堆管理
///
/// Battle V0.1：玩家手牌由共享牌库 BattleV1Runtime / SharedDeck 承担；
/// 本容器不再保存独立道具手牌（Item Hand 已退出正式产品路径）。
/// </summary>
public class Hand
{
    /// <summary>
    /// 角色手牌
    /// 上限：3
    /// </summary>
    public List<CharacterCardData> CharacterCards;

    /// <summary>
    /// 创建手牌区
    /// </summary>
    public Hand()
    {
        CharacterCards = new List<CharacterCardData>();
    }
}