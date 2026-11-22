using UnityEngine;

public partial class BattleUI : MonoBehaviour
{
    private string GetBattleUnitDisplayName(BattleUnit unit)
    {
        if (unit == null)
            return string.Empty;

        if (unit.UnitType == UnitType.Hero)
        {
            string heroName = _playerHero != null && !string.IsNullOrEmpty(_playerHero.DisplayName)
                ? _playerHero.DisplayName
                : (_playerHeroData != null ? _playerHeroData.DisplayName : null);
            return "[英雄]" + (string.IsNullOrEmpty(heroName) ? unit.SourceId : heroName);
        }

        if (unit.UnitType == UnitType.Monster)
        {
            var monsterData = _enemyMonsters?.Find(m => m.MonsterId == unit.SourceId);
            return monsterData != null ? monsterData.Name : unit.SourceId;
        }

        // 角色：优先具体人物显示名（部署时带入）；仅当缺失时才回退到战斗定义名（Legacy fallback）。
        if (!string.IsNullOrEmpty(unit.DisplayName)) return unit.DisplayName;

        var characterData = _allCharDatas?.Find(c => c.CharacterId == unit.SourceId);
        return characterData != null ? characterData.DisplayName : unit.SourceId;
    }

    /// <summary>
    /// 解析一张角色卡所对应具体人物的显示名（人物身份优先）。
    /// 只接受显式 Card→Person 绑定解析出的具体人物；解析不到返回 null（不猜人物、不用模板名冒充人物名）。
    /// </summary>
    private string ResolvePersonDisplayName(CharacterCardData card)
    {
        if (card == null) return null;

        if (_playerData != null)
        {
            string boundName = _playerData.GetPersonDisplayNameForCard(card);
            if (!string.IsNullOrEmpty(boundName)) return boundName;
        }

        if (_charInstByCardId != null
            && _charInstByCardId.TryGetValue(card.CardId, out var ci)
            && ci != null
            && !string.IsNullOrEmpty(ci.DisplayName))
        {
            return ci.DisplayName;
        }

        return null;
    }

    /// <summary>
    /// 表现用：角色卡 → 具体人物 PersonId（**只读**；不写任何 Gameplay 状态、不建立第二套身份映射）。
    /// 正式路径只读 PlayerCollection 显式 Card→Person 绑定；无 PlayerCollection 的兼容路径才读本场实例；
    /// 解析不到返回 null（调用方回退到外观键 → placeholder，不猜人物）。
    /// </summary>
    private string ResolveCardPersonIdForVisual(CharacterCardData card)
    {
        if (card == null) return null;

        if (_playerData != null) return _playerData.ResolvePersonIdForCard(card);

        if (_charInstByCardId != null
            && !string.IsNullOrEmpty(card.CardId)
            && _charInstByCardId.TryGetValue(card.CardId, out var ci)
            && ci != null
            && !string.IsNullOrEmpty(ci.PersonId))
        {
            return ci.PersonId;
        }

        return null;
    }

    /// <summary>
    /// 角色卡的表现外观键兜底（PersonDefinitionId → CharacterId）。
    /// 只用于选择「用哪份外观」，不代表人物身份，不替代 PersonId。
    /// </summary>
    private static string AppearanceKeyFallbackOfCharCard(CharacterCardData card)
    {
        return PersonVisualResolver.ResolveAppearanceKeyForCard(card);
    }
}
