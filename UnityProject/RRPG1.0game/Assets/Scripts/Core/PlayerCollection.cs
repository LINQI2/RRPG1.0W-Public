using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// 玩家全局数据集合
///
/// 负责：管理玩家拥有的角色实例、英雄实例、道具卡册
/// 不负责：战斗中的牌组管理、战斗状态
///
/// 冒险中的获得物进入暂存奖励池，撤离后入库
/// 角色死亡后从卡册中移除
/// </summary>
public class PlayerCollection
{
    /// <summary>
    /// 玩家拥有的英雄实例列表
    /// 初始默认拥有 hero_001（青鹿）
    /// </summary>
    public List<HeroInstance> OwnedHeroes;

    /// <summary>
    /// 玩家拥有的角色实例列表（角色卡对应的 CharacterInstance）
    /// 对应 DeckArchive.OwnedCharacters 中的角色卡
    /// </summary>
    public List<CharacterInstance> OwnedCharacters;

    /// <summary>
    /// 玩家拥有的角色卡数据列表（卡册）
    /// </summary>
    public List<CharacterCardData> OwnedCharacterCards;

    /// <summary>
    /// 玩家拥有的道具卡数据列表（Legacy / Deprecated no-op 容器）。
    /// WP-C：独立 Item Card 产品语义已退出正式产品路径；Battle / Expedition / Save 均不再依赖本容器。
    /// 仅保留给尚未清理的 Legacy UI（TownUI / ShopUI / EventUI / DeckViewerUI）原样读写，避免扩大清理范围。
    /// </summary>
    public List<ItemCardData> OwnedItemCards;

    /// <summary>
    /// 当前金币数量
    /// </summary>
    public int Gold;

    /// <summary>
    /// 角色卡 → 具体人物实例的显式归属映射（M1）。
    /// Key = CharacterCardData.CardId；Value = 该卡所代表的具体人物的 PersonId。
    /// 这是“卡 → 实例”的最小显式绑定，用于 Expedition/Run 从卡解析到唯一人物；
    /// 人物长期数据本体仍只存于 OwnedCharacters（不建立第二套人物真相源）。
    /// 注意：绑定是卡的运行时映射，不等于把 CardId 当作 PersonId。
    /// </summary>
    private readonly Dictionary<string, string> _characterCardOwnerByCardId = new Dictionary<string, string>();

    /// <summary>
    /// 创建玩家全局数据集合
    /// </summary>
    public PlayerCollection()
    {
        OwnedHeroes = new List<HeroInstance>();
        OwnedCharacters = new List<CharacterInstance>();
        OwnedCharacterCards = new List<CharacterCardData>();
        OwnedItemCards = new List<ItemCardData>();
        Gold = 0;
    }

    /// <summary>
    /// 初始化默认玩家集合（从 DataManager 加载）
    /// 供首次进入远征准备时调用
    /// </summary>
    public void InitializeDefaultCollection()
    {
        DataManager dm = DataManager.Instance;

        // 初始化英雄：青鹿
        if (OwnedHeroes.Count == 0)
        {
            HeroData heroData = dm.GetHeroById("hero_001");
            if (heroData != null)
            {
                var hero = new HeroInstance(heroData.HeroId, heroData.DisplayName, heroData.BaseHP);
                GrowthDefaults.EnsureSeeded(hero, heroData.HeroId); // V0.1：初始 Starter 1 槽 + Unlocked Pool 测试种子（按模板 ID 播种）
                OwnedHeroes.Add(hero);
            }
            else
            {
                Debug.LogWarning("[PlayerCollection] 未找到 hero_001 的英雄数据");
            }
        }

        // 初始化角色：唯一来源 = PersonDefinitionCatalog 的初始名册（InitialRosterOrder 升序）。
        // 配置 N 行 → 开局 N 人；代码不写死人数或固定名单（Legacy 路径见 InitializeLegacyCharactersFromCards）。
        if (OwnedCharacters.Count == 0)
        {
            List<PersonDefinition> roster = dm.InitialPersonRoster;
            if (roster != null && roster.Count > 0)
            {
                InitializeCharactersFromPersonDefinitions(dm, roster);
            }
            else
            {
                InitializeLegacyCharactersFromCards(dm);
            }
        }

        // 初始化道具卡册
        if (OwnedItemCards.Count == 0)
        {
            if (dm.AllItems != null && dm.AllItems.Count > 0)
            {
                OwnedItemCards.AddRange(dm.AllItems);
            }
            else
            {
                Debug.LogWarning("[PlayerCollection] 未找到道具卡数据");
            }
        }

        // 初始金币
        if (Gold == 0)
        {
            Gold = 100;
        }
    }

    /// <summary>
    /// New Game 初始人物创建（PersonDefinition 唯一路径）：
    ///   PersonDefinition → PersonBootstrapper（独立 PersonId + 人物名字 + 开局 Profession）
    ///   → OwnedCharacters；角色卡按 CardDefinitionId 解析（缺失则生成运行时卡）→ CardId→PersonId 显式绑定。
    ///
    /// 说明：
    ///   - 人物长期成长（Starter / Profession Pool）已由 Bootstrap 按职业模板初始化，
    ///     因此此处**不再**调用 GrowthDefaults 的 Legacy 测试种子（避免污染 Profession 初始内容）。
    ///   - 人物身份始终是 PersonId；PersonDefinitionId 只作配置 provenance。
    /// </summary>
    private void InitializeCharactersFromPersonDefinitions(DataManager dm, List<PersonDefinition> roster)
    {
        foreach (var definition in roster)
        {
            CharacterInstance person = PersonBootstrapper.CreatePerson(definition, dm.ProfessionRegistry);
            if (person == null) continue; // 无效配置已在 Bootstrapper 内告警并跳过，不影响其它人物

            OwnedCharacters.Add(person);

            CharacterCardData card = ResolveOrCreateInitialCard(dm, definition);
            if (card == null)
            {
                Debug.LogWarning("[PlayerCollection] 人物 " + definition.PersonDefinitionId
                    + " 未能建立角色卡（CardDefinitionId=" + (definition.CardDefinitionId ?? "null")
                    + "）；该人物不会出现在卡册/出征列表，直到有显式卡绑定。");
                continue;
            }

            OwnedCharacterCards.Add(card);
            BindCharacterCardOwner(card.CardId, person.PersonId); // M1：初始卡册每卡显式绑定其具体人物实例
        }
    }

    /// <summary>
    /// 解析人物定义指定的角色卡：配置 CardDefinitionId 且卡册内 CardId 未被占用时复用既有卡资产；
    /// 否则（未配置 / 卡缺失 / CardId 冲突）生成该人物独有的运行时卡（CardId 仍与 PersonId 显式绑定）。
    /// </summary>
    private CharacterCardData ResolveOrCreateInitialCard(DataManager dm, PersonDefinition definition)
    {
        if (!string.IsNullOrEmpty(definition.CardDefinitionId))
        {
            CharacterCardData configured = dm.GetCardById(definition.CardDefinitionId);
            if (configured == null)
            {
                Debug.LogWarning("[PlayerCollection] 人物 " + definition.PersonDefinitionId
                    + " 的 CardDefinitionId=" + definition.CardDefinitionId
                    + " 找不到卡定义；改为生成运行时卡（不改用其它卡）。");
            }
            else if (OwnedCharacterCards.Exists(c => c != null && c.CardId == configured.CardId))
            {
                Debug.LogWarning("[PlayerCollection] 卡 " + configured.CardId
                    + " 已被本集合中的其它人物占用；为人物 " + definition.PersonDefinitionId
                    + " 生成运行时卡（不共享同一 CardId，避免 Card→Person 绑定塌缩）。");
            }
            else
            {
                return configured;
            }
        }

        var runtimeCard = ScriptableObject.CreateInstance<CharacterCardData>();
        runtimeCard.CardId = "pcard_" + definition.PersonDefinitionId + "_"
            + System.Guid.NewGuid().ToString("N").Substring(0, 6);
        runtimeCard.CharacterId = definition.CombatDefinitionId;
        runtimeCard.PersonDefinitionId = definition.PersonDefinitionId;
        return runtimeCard;
    }

    /// <summary>
    /// Legacy 兼容路径（仅当没有任何 InitialRosterOrder &gt; 0 的 PersonDefinition 时）：
    /// 保持迁移前的「全部角色卡 → 模板 → CharacterInstance」行为，供未导入 Person.csv 的工程/测试使用。
    /// 不做任何职业推断（该路径人物开局无 Active Profession）。
    /// </summary>
    private void InitializeLegacyCharactersFromCards(DataManager dm)
    {
        if (OwnedCharacterCards.Count == 0)
        {
            if (dm.AllCharCards != null && dm.AllCharCards.Count > 0)
            {
                OwnedCharacterCards.AddRange(dm.AllCharCards);
            }
            else
            {
                Debug.LogWarning("[PlayerCollection] 未找到角色卡数据");
            }
        }

        foreach (var card in OwnedCharacterCards)
        {
            if (card == null) continue;
            CharacterData charData = dm.GetCharacterById(card.CharacterId);
            if (charData == null) continue;

            var instance = new CharacterInstance(card.CharacterId, charData.DisplayName, charData.BaseHP, charData.BaseAttack);
            GrowthDefaults.EnsureSeeded(instance, card.CharacterId); // Legacy 路径：保留 V0.1 初始 Starter / 测试池播种
            OwnedCharacters.Add(instance);
            BindCharacterCardOwner(card.CardId, instance.PersonId);
        }
    }

    /// <summary>
    /// 【LEGACY 模板查询】按 CharacterId 模板 ID 获取“首个”匹配实例。
    /// 旧接口仅按模板查询，不能作为“具体人物唯一查询”——同模板多实例时只能命中第一个。
    /// 新产品路径一律使用 GetCharacterByPersonId / GetCharacterInstanceForCard 等按人物查询。
    /// M1 保留本接口供 Legacy 路径兼容，禁止删除。
    /// </summary>
    public CharacterInstance GetCharacterInstance(string characterId)
    {
        return OwnedCharacters.Find(c => c.CharacterId == characterId);
    }

    /// <summary>
    /// 【LEGACY 模板查询】按 HeroId 模板 ID 获取“首个”匹配英雄实例。
    /// 不能作为“具体人物唯一查询”；新产品路径使用 GetHeroByPersonId。
    /// </summary>
    public HeroInstance GetHeroInstance(string heroId)
    {
        return OwnedHeroes.Find(h => h.HeroId == heroId);
    }

    /// <summary>
    /// 按 PersonId 查询具体角色实例（M1 权威人物查询）。
    /// </summary>
    public CharacterInstance GetCharacterByPersonId(string personId)
    {
        if (string.IsNullOrEmpty(personId)) return null;
        return OwnedCharacters.Find(c => c != null && c.PersonId == personId);
    }

    /// <summary>
    /// 按 PersonId 查询具体英雄实例（M1 权威人物查询）。
    /// </summary>
    public HeroInstance GetHeroByPersonId(string personId)
    {
        if (string.IsNullOrEmpty(personId)) return null;
        return OwnedHeroes.Find(h => h != null && h.PersonId == personId);
    }

    /// <summary>
    /// 按 PersonId 查询任意具体人物（角色优先，其次英雄）（M1 权威人物查询）。
    /// </summary>
    public IPersonGrowth FindPersonByPersonId(string personId)
    {
        CharacterInstance character = GetCharacterByPersonId(personId);
        if (character != null) return character;
        return GetHeroByPersonId(personId);
    }

    /// <summary>
    /// 绑定一张角色卡到其具体人物实例（M1）。
    /// 用于初始卡册 / 招募 / 存档恢复；同模板多卡各自绑定不同实例。
    /// </summary>
    public void BindCharacterCardOwner(string cardId, string personId)
    {
        if (string.IsNullOrEmpty(cardId) || string.IsNullOrEmpty(personId)) return;
        _characterCardOwnerByCardId[cardId] = personId;
    }

    /// <summary>
    /// 移除一张角色卡的绑定（卡册移除该卡时调用）。
    /// </summary>
    public void UnbindCharacterCard(string cardId)
    {
        if (!string.IsNullOrEmpty(cardId))
            _characterCardOwnerByCardId.Remove(cardId);
    }

    /// <summary>
    /// 解析一张角色卡对应的具体人物 PersonId（M1 / Foundation Identity Cleanup）。
    ///
    /// 绑定键语义：
    ///   - 一张 owned 角色卡的“稳定绑定键” = 本集合内唯一的 CardId
    ///     （创建/招募用 Guid 化 CardId、加载时对重复 CardId 归一化去重来保证集合内唯一，
    ///      因此不允许两条 owned 卡共享同一 CardId——那是非法状态，加载层已防御归一化）。
    ///   - 具体人物身份键始终是 PersonId，绝不返回 CardId / CharacterId / 其它模板 ID 本身。
    ///
    /// Foundation Identity Cleanup：真实产品路径只接受显式 Card→Person 绑定。
    ///   未绑定（无论该模板（CharacterId）候选实例是否唯一）一律返回 null + 告警——
    ///   禁止以“模板候选唯一”猜具体 Person（同模板双实例或未来更多实例都会塌缩）；
    ///   旧档缺绑定的卡保持 unresolved，只由显式绑定恢复/重建，不做隐式推断。
    /// </summary>
    public string ResolvePersonIdForCard(CharacterCardData card)
    {
        if (card == null || string.IsNullOrEmpty(card.CardId)) return null;

        // 只接受显式 Card→Person 绑定：绑定可能指向已移除实例；指向仍存在实例时才返回。
        if (_characterCardOwnerByCardId.TryGetValue(card.CardId, out var bound))
        {
            if (GetCharacterByPersonId(bound) != null) return bound;
        }

        UnityEngine.Debug.LogWarning(
            "[M1/IdentityCleanup] ResolvePersonIdForCard：卡 " + card.CardId + "（模板 " + card.CharacterId
            + "）没有显式 Card→Person 绑定，不按模板候选推断 Person，返回 null（unresolved）；"
            + "请先为该卡建立显式 PersonId 绑定。");
        return null;
    }

    /// <summary>
    /// 解析一张角色卡对应的具体 CharacterInstance（M1）。
    /// 同模板多实例时按卡绑定返回唯一对应实例，不发生模板塌缩；
    /// 无显式绑定（即使模板候选唯一）时返回 null（拒绝模板猜绑）。
    /// </summary>
    public CharacterInstance GetCharacterInstanceForCard(CharacterCardData card)
    {
        string personId = ResolvePersonIdForCard(card);
        return !string.IsNullOrEmpty(personId) ? GetCharacterByPersonId(personId) : null;
    }

    /// <summary>
    /// 解析一张角色卡对应具体人物的显示名（人物身份优先；人物名字来自 PersonDefinition/Bootstrap）。
    /// 无显式绑定或人物名为空 → 返回 null（调用方回退到既有 Legacy 表现，不猜人物）。
    /// </summary>
    public string GetPersonDisplayNameForCard(CharacterCardData card)
    {
        CharacterInstance ci = GetCharacterInstanceForCard(card);
        if (ci == null) return null;
        return string.IsNullOrEmpty(ci.DisplayName) ? null : ci.DisplayName;
    }

    /// <summary>
    /// 导出当前全部有效“角色卡 → PersonId”绑定（供 SaveSystem 持久化）。
    /// 只导出仍能找到对应人物的绑定。
    /// </summary>
    public List<CharacterCardOwnerSaveData> BuildCharacterCardOwnerSaveData()
    {
        var result = new List<CharacterCardOwnerSaveData>();
        foreach (var kv in _characterCardOwnerByCardId)
        {
            if (string.IsNullOrEmpty(kv.Key) || string.IsNullOrEmpty(kv.Value)) continue;
            if (GetCharacterByPersonId(kv.Value) == null) continue;
            result.Add(new CharacterCardOwnerSaveData { CardId = kv.Key, PersonId = kv.Value });
        }
        return result;
    }

    /// <summary>
    /// 恢复“角色卡 → PersonId”绑定（Load 路径）。
    /// 只接受仍能对应到本集合内实例且卡册内存在该卡 ID 的绑定。
    /// </summary>
    public void RestoreCharacterCardOwners(IEnumerable<CharacterCardOwnerSaveData> owners)
    {
        if (owners == null) return;
        foreach (var o in owners)
        {
            if (o == null || string.IsNullOrEmpty(o.CardId) || string.IsNullOrEmpty(o.PersonId)) continue;
            bool hasCard = OwnedCharacterCards.Exists(c => c != null && c.CardId == o.CardId);
            if (!hasCard || GetCharacterByPersonId(o.PersonId) == null) continue;
            BindCharacterCardOwner(o.CardId, o.PersonId);
        }
    }

    /// <summary>
    /// 旧档兼容入口（Foundation Identity Cleanup）：为卡册中“未绑定卡”输出 unresolved 告警。
    /// 规则变更：不再允许以“模板候选唯一 / 按卡册顺序”为旧档自动补绑 Card owner——
    ///   缺显式 Card→Person 绑定的卡一律保持 unresolved（解析返回 null 语义），只输出清晰告警；
    ///   显式绑定只能来自存档中的 OwnedCharacterCardOwners 或上游显式 BindCharacterCardOwner。
    /// 已显式恢复的绑定不覆盖、不受影响。
    /// </summary>
    public void RebuildCharacterCardOwnersFromCardOrder()
    {
        var boundCardIds = new HashSet<string>(_characterCardOwnerByCardId.Keys);

        foreach (var card in OwnedCharacterCards)
        {
            if (card == null || string.IsNullOrEmpty(card.CardId)) continue;
            if (boundCardIds.Contains(card.CardId)) continue; // 已有显式绑定无需处理

            UnityEngine.Debug.LogWarning(
                "[M1/IdentityCleanup] RebuildCharacterCardOwners：卡 " + card.CardId + "（模板 "
                + card.CharacterId + "）在旧档中无显式 Card→Person 绑定；"
                + "不按模板候选唯一/顺序自动补绑，保持 unresolved。请先为该卡建立显式 PersonId 绑定。");
        }
    }

    /// <summary>
    /// 检查角色是否存活（不在死亡状态）
    /// </summary>
    public bool IsCharacterAlive(string characterId)
    {
        var instance = GetCharacterInstance(characterId);
        return instance != null && !instance.IsDead;
    }

    /// <summary>
    /// 从卡册移除已死亡的角色
    /// </summary>
    public void RemoveDeadCharacter(string characterId)
    {
        OwnedCharacters.RemoveAll(c => c.CharacterId == characterId);
        OwnedCharacterCards.RemoveAll(c => c.CharacterId == characterId);
        // M1：同步清理已移除实例遗留的角色卡绑定（保持映射不悬挂）
        var stale = _characterCardOwnerByCardId
            .Where(kv => GetCharacterByPersonId(kv.Value) == null)
            .Select(kv => kv.Key).ToList();
        foreach (var cardId in stale) _characterCardOwnerByCardId.Remove(cardId);
    }
}
