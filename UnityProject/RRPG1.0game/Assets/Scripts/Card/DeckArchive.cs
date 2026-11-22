using System.Collections.Generic;

/// <summary>
/// 全局角色卡册
///
/// 负责：永久存储已拥有的角色卡
/// 不负责：战斗中的牌组管理
///
/// 出征前从卡册选择卡牌组成 Deck
/// 角色卡册无硬上限，超员触发舒适度软惩罚
/// Battle V0.1：不再保存独立道具卡册（Item Deck 已退出正式产品路径）。
/// </summary>
public class DeckArchive
{
    /// <summary>
    /// 已拥有的角色卡
    /// </summary>
    public List<CharacterCardData> OwnedCharacters;

    public DeckArchive()
    {
        OwnedCharacters = new List<CharacterCardData>();
    }
}