using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 游戏数据校验工具（Editor Only）
///
/// 负责：检查所有 SO 配置数据的完整性和引用一致性
/// 不负责：自动修复数据、修改资源、移动文件
///
/// 使用方式：Unity 菜单 → RRPG/Tools/Validate Game Data
/// </summary>
public class DataValidationTool
{
    private static List<string> _errors;

    [MenuItem("RRPG/Tools/Validate Game Data")]
    static void ValidateGameData()
    {
        _errors = new List<string>();

        // 1. 加载所有 SO
        var characters = Resources.LoadAll<CharacterData>("ScriptableObjects/Characters");
        var heroes = Resources.LoadAll<HeroData>("ScriptableObjects/Heroes");
        var skills = Resources.LoadAll<SkillData>("ScriptableObjects/Skills");
        var statuses = Resources.LoadAll<StatusData>("ScriptableObjects/Statuses");
        var charCards = Resources.LoadAll<CharacterCardData>("ScriptableObjects/Cards");
        var itemCards = Resources.LoadAll<ItemCardData>("ScriptableObjects/Cards");

        // 2. 检查空ID
        CheckEmptyIds(characters, c => c.CharacterId, "CharacterData");
        CheckEmptyIds(heroes, h => h.HeroId, "HeroData");
        CheckEmptyIds(skills, s => s.SkillId, "SkillData");
        CheckEmptyIds(statuses, s => s.StatusId, "StatusData");
        CheckEmptyIds(charCards, c => c.CardId, "CharacterCardData");
        CheckEmptyIds(itemCards, c => c.CardId, "ItemCardData");

        // 3. 检查同类型重复ID
        CheckDuplicateIds(characters, c => c.CharacterId, "CharacterData");
        CheckDuplicateIds(heroes, h => h.HeroId, "HeroData");
        CheckDuplicateIds(skills, s => s.SkillId, "SkillData");
        CheckDuplicateIds(statuses, s => s.StatusId, "StatusData");
        CheckDuplicateIds(charCards, c => c.CardId, "CharacterCardData");
        CheckDuplicateIds(itemCards, c => c.CardId, "ItemCardData");

        // 4. 构建查找字典
        var charDataById = characters.ToDictionary(c => c.CharacterId, c => c);
        var statusDataById = statuses.ToDictionary(s => s.StatusId, s => s);

        // 5. CharacterCardData.CharacterId → CharacterData 引用检查
        foreach (var card in charCards)
        {
            if (string.IsNullOrEmpty(card.CardId)) continue; // 已在上方报空ID

            if (string.IsNullOrEmpty(card.CharacterId))
            {
                _errors.Add($"[CharacterCardData] {card.name} (CardId={card.CardId}) 的 CharacterId 为空");
            }
            else if (!charDataById.ContainsKey(card.CharacterId))
            {
                _errors.Add($"[CharacterCardData] {card.name} (CardId={card.CardId}) 引用了不存在的 CharacterId=\"{card.CharacterId}\"");
            }
        }

        // 6. Initial Person &amp; Profession Bootstrap 引用检查（Person.csv / Profession.csv → SO）
        ValidatePersonAndProfessionData();

        // 7. SkillData.Effects[].StatusId → StatusData 引用检查
        foreach (var skill in skills)
        {
            if (string.IsNullOrEmpty(skill.SkillId)) continue;

            if (skill.Effects == null) continue;

            foreach (var effect in skill.Effects)
            {
                CheckStatusReference(effect, $"[SkillData] {skill.name} (SkillId={skill.SkillId})", statusDataById);
            }
        }

        // 8. StatusData.OnExpireStatusId → StatusData 引用检查
        foreach (var status in statuses)
        {
            if (string.IsNullOrEmpty(status.StatusId)) continue;

            if (!string.IsNullOrEmpty(status.OnExpireStatusId))
            {
                if (!statusDataById.ContainsKey(status.OnExpireStatusId))
                {
                    _errors.Add($"[StatusData] {status.name} (StatusId={status.StatusId}) 的 OnExpireStatusId=\"{status.OnExpireStatusId}\" 不存在于 StatusData 中");
                }
                else if (status.OnExpireStatusId == status.StatusId)
                {
                    _errors.Add($"[StatusData] {status.name} (StatusId={status.StatusId}) 的 OnExpireStatusId 指向自身，可能导致死循环");
                }
            }

            // StatusData 自身 Effects 中的 StatusId 引用检查
            if (status.Effects != null)
            {
                foreach (var effect in status.Effects)
                {
                    CheckStatusReference(effect, $"[StatusData] {status.name} (StatusId={status.StatusId})", statusDataById);
                }
            }
        }

        // 9. ItemCardData.Effects[].StatusId → StatusData 引用检查
        foreach (var item in itemCards)
        {
            if (string.IsNullOrEmpty(item.CardId)) continue;

            if (item.Effects == null) continue;

            foreach (var effect in item.Effects)
            {
                CheckStatusReference(effect, $"[ItemCardData] {item.name} (CardId={item.CardId})", statusDataById);
            }
        }

        // 10. 输出结果
        if (_errors.Count == 0)
        {
            Debug.Log("[DataValidation] All game data is valid.");
        }
        else
        {
            Debug.LogError($"[DataValidation] Found {_errors.Count} issue(s):\n" + string.Join("\n", _errors));
        }
    }

    /// <summary>
    /// Initial Person &amp; Profession Bootstrap 数据校验：
    ///   ProfessionDefinitionCatalog：逐条复用 ProfessionDefinitionValidator + 重复 ProfessionId；
    ///   PersonDefinitionCatalog：逐条复用 PersonDefinitionValidator + 重复 ID / 重复初始名册顺序；
    ///   跨引用：CombatDefinitionId → CharacterData、CardDefinitionId → CharacterCardData、
    ///           InitialProfession.ProfessionId → ProfessionDefinition、override 池 ⊆ FullCardPool；
    ///   角色卡：CharacterCardData.PersonDefinitionId（若非空）必须能解析到 PersonDefinition。
    /// </summary>
    static void ValidatePersonAndProfessionData()
    {
        var professionCatalogs = Resources.LoadAll<ProfessionDefinitionCatalog>("ScriptableObjects/Professions");
        var personCatalogs = Resources.LoadAll<PersonDefinitionCatalog>("ScriptableObjects/Persons");

        var professionById = new Dictionary<string, ProfessionDefinition>();
        var professionList = new List<ProfessionDefinition>();
        foreach (var catalog in professionCatalogs)
        {
            if (catalog == null || catalog.Definitions == null) continue;
            foreach (var def in catalog.Definitions)
            {
                if (def == null) continue;
                professionList.Add(def);
                if (!string.IsNullOrEmpty(def.ProfessionId) && !professionById.ContainsKey(def.ProfessionId))
                    professionById[def.ProfessionId] = def;
            }
        }

        if (professionList.Count == 0)
            _errors.Add("[ProfessionDefinition] 未找到任何职业模板（Profession.csv 未导入？初始 Profession 将不可用）");
        else if (!ProfessionDefinitionValidator.TryValidateAll(professionList, out List<string> professionErrors))
            foreach (var e in professionErrors) _errors.Add("[ProfessionDefinition] " + e);

        var personList = new List<PersonDefinition>();
        foreach (var catalog in personCatalogs)
        {
            if (catalog == null || catalog.Definitions == null) continue;
            personList.AddRange(catalog.Definitions.Where(d => d != null));
        }

        if (personList.Count == 0)
            _errors.Add("[PersonDefinition] 未找到任何固定初始人物定义（Person.csv 未导入？New Game 将回退 Legacy 卡册路径）");
        else if (!PersonDefinitionValidator.TryValidateAll(personList, out List<string> personErrors))
            foreach (var e in personErrors) _errors.Add("[PersonDefinition] " + e);

        var charDataById = Resources.LoadAll<CharacterData>("ScriptableObjects/Characters")
            .Where(c => c != null && !string.IsNullOrEmpty(c.CharacterId))
            .GroupBy(c => c.CharacterId).ToDictionary(g => g.Key, g => g.First());
        var cardById = Resources.LoadAll<CharacterCardData>("ScriptableObjects/Cards")
            .Where(c => c != null && !string.IsNullOrEmpty(c.CardId))
            .GroupBy(c => c.CardId).ToDictionary(g => g.Key, g => g.First());

        var personDefinitionById = personList
            .Where(d => !string.IsNullOrEmpty(d.PersonDefinitionId))
            .GroupBy(d => d.PersonDefinitionId).ToDictionary(g => g.Key, g => g.First());

        int rosterCount = 0;
        foreach (var person in personList)
        {
            string label = "[PersonDefinition] " + person.PersonDefinitionId;
            if (person.IsInInitialRoster) rosterCount++;

            if (!string.IsNullOrEmpty(person.CombatDefinitionId) && !charDataById.ContainsKey(person.CombatDefinitionId))
                _errors.Add(label + " 的 CombatDefinitionId=\"" + person.CombatDefinitionId + "\" 不存在于 CharacterData");

            if (!string.IsNullOrEmpty(person.CardDefinitionId) && !cardById.ContainsKey(person.CardDefinitionId))
                _errors.Add(label + " 的 CardDefinitionId=\"" + person.CardDefinitionId + "\" 不存在于 CharacterCardData");

            var config = person.InitialProfession;
            if (config == null) continue; // 开局无职业 = 合法（未来随机 Person 分支）

            if (!professionById.TryGetValue(config.ProfessionId, out ProfessionDefinition profession))
            {
                _errors.Add(label + " 的 InitialProfessionId=\"" + config.ProfessionId + "\" 不存在于 ProfessionDefinition");
                continue;
            }

            if (config.HasUnlockedPoolOverride)
            {
                foreach (var cardId in config.InitialUnlockedPoolOverride)
                {
                    if (profession.FullCardPool == null || !profession.FullCardPool.Contains(cardId))
                        _errors.Add(label + " 的 InitialUnlockedPoolOverride 卡 \"" + cardId
                            + "\" 不在职业 " + config.ProfessionId + " 的 FullCardPool 中");
                }
            }
        }

        if (personList.Count > 0 && rosterCount == 0)
            _errors.Add("[PersonDefinition] 没有任何 InitialRosterOrder > 0 的定义（New Game 将无固定初始人物）");

        foreach (var card in cardById.Values)
        {
            if (string.IsNullOrEmpty(card.PersonDefinitionId)) continue; // 旧卡/Legacy 卡允许为空
            if (!personDefinitionById.ContainsKey(card.PersonDefinitionId))
                _errors.Add("[CharacterCardData] " + card.CardId + " 的 PersonDefinitionId=\""
                    + card.PersonDefinitionId + "\" 不存在于 PersonDefinition");
        }
    }

    /// <summary>
    /// 检查 SO 数组中是否有空的 ID 字段
    /// </summary>
    static void CheckEmptyIds<T>(T[] items, System.Func<T, string> getId, string typeName) where T : ScriptableObject
    {
        foreach (var item in items)
        {
            string id = getId(item);
            if (string.IsNullOrEmpty(id))
            {
                _errors.Add($"[{typeName}] {item.name} 的 ID 字段为空");
            }
        }
    }

    /// <summary>
    /// 检查 SO 数组中是否有重复的 ID
    /// </summary>
    static void CheckDuplicateIds<T>(T[] items, System.Func<T, string> getId, string typeName) where T : ScriptableObject
    {
        var seen = new Dictionary<string, List<string>>();

        foreach (var item in items)
        {
            string id = getId(item);
            if (string.IsNullOrEmpty(id)) continue; // 空 ID 已在上一步报告

            if (!seen.ContainsKey(id))
            {
                seen[id] = new List<string>();
            }
            seen[id].Add(item.name);
        }

        foreach (var kvp in seen)
        {
            if (kvp.Value.Count > 1)
            {
                _errors.Add($"[{typeName}] 发现重复 ID=\"{kvp.Key}\"，存在于：{string.Join(", ", kvp.Value)}");
            }
        }
    }

    /// <summary>
    /// 检查 EffectData 中的 StatusId 是否能在 StatusData 中找到
    /// </summary>
    static void CheckStatusReference(EffectData effect, string context, Dictionary<string, StatusData> statusDataById)
    {
        if (effect == null) return;

        // 只有当 EffectType 是 AddBuff/RemoveBuff 且 StatusId 非空时才检查
        if (effect.EffectType == EffectType.AddBuff || effect.EffectType == EffectType.RemoveBuff)
        {
            if (string.IsNullOrEmpty(effect.StatusId))
            {
                _errors.Add($"{context} 的 Effect (Type={effect.EffectType}) 的 StatusId 为空");
            }
            else if (!statusDataById.ContainsKey(effect.StatusId))
            {
                _errors.Add($"{context} 的 Effect (Type={effect.EffectType}) 引用了不存在的 StatusId=\"{effect.StatusId}\"");
            }
        }
    }
}