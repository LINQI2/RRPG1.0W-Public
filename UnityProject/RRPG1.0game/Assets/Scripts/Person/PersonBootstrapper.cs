using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Initial Person &amp; Profession Bootstrap —— New Game 创建固定初始人物的唯一入口。
///
/// 输入：PersonDefinition（静态配置）+ ProfessionDefinitionRegistry（职业模板权威）
/// 输出：一个具体 Runtime Person（CharacterInstance，拥有独立 PersonId）
///
/// 职责与红线（与审计结论一致）：
///   - PersonId 仍由 PersonIdentity.NewPersonId() 生成（CharacterInstance 构造内），本类不伪造身份；
///   - DisplayName 来自 PersonDefinition（人物名字）；不把职业当人物身份；
///   - CombatDefinitionId 只作战斗定义引用（写入 CharacterInstance.CharacterId，供战斗读取）；
///   - PersonDefinitionId 作为配置 provenance 写入人物（供 Save/Load 恢复表现/默认值），**不是** PersonId；
///   - InitialProfession 存在时创建 Person-owned ActiveProfessionProgress（Level/Exp/Pool/TraitLevel）；
///   - Unlocked Pool 与 StarterSlots 一律**深拷贝**，绝不与 ProfessionDefinition 共享引用；
///   - 绝不调用 FormationResolver / FormationCommitter，绝不写 FormationState / ProfessionFormed LifeHistory
///     （「开局已有职业」不是一次人生 Formation 事件）；
///   - 绝不根据 CharacterId / 旧 Level·Exp / Assignment 推断职业；配置缺失一律显式告警并跳过；
///   - InitialProfession == null 是合法配置（未来随机无职业 Person → M6 Formation 分支），
///     此时人物创建成功且 ActiveProfessionProgress == null，不报错、不猜职业。
///
/// 只提供这一层入口（不建 Factory / Initializer 多层封装）。
/// </summary>
public static class PersonBootstrapper
{
    /// <summary>战斗定义缺失时的最小兜底（与既有 BattleUI 创建口径一致，Test / Not Frozen）。</summary>
    private const int FallbackBaseHp = 10;
    private const int FallbackBaseAttack = 3;

    /// <summary>
    /// 由 PersonDefinition 创建一个具体 Runtime Person；无法安全创建时返回 null（已告警）。
    /// </summary>
    public static CharacterInstance CreatePerson(PersonDefinition definition, ProfessionDefinitionRegistry registry)
    {
        if (definition == null)
        {
            Debug.LogWarning("[PersonBootstrapper] PersonDefinition 为 null，跳过创建。");
            return null;
        }

        if (string.IsNullOrEmpty(definition.PersonDefinitionId))
        {
            Debug.LogWarning("[PersonBootstrapper] PersonDefinition 缺少 PersonDefinitionId，跳过创建（不猜身份）。");
            return null;
        }

        if (string.IsNullOrEmpty(definition.DisplayName))
        {
            Debug.LogWarning("[PersonBootstrapper] 人物定义 " + definition.PersonDefinitionId
                + " 缺少 DisplayName，跳过创建（不把职业/模板 ID 当人物名字）。");
            return null;
        }

        if (string.IsNullOrEmpty(definition.CombatDefinitionId))
        {
            Debug.LogWarning("[PersonBootstrapper] 人物定义 " + definition.PersonDefinitionId
                + " 缺少 CombatDefinitionId，跳过创建（必须有战斗定义引用）。");
            return null;
        }

        // 战斗定义只提供初始战斗数值；缺失时用最小兜底值，不阻断（战斗定义不是人物身份）。
        CharacterData combat = DataManager.Instance != null
            ? DataManager.Instance.GetCharacterById(definition.CombatDefinitionId)
            : null;
        if (combat == null)
        {
            Debug.LogWarning("[PersonBootstrapper] 人物定义 " + definition.PersonDefinitionId
                + " 的 CombatDefinitionId=" + definition.CombatDefinitionId
                + " 找不到战斗定义；使用最小兜底战斗数值创建人物。");
        }

        int baseHp = combat != null && combat.BaseHP > 0 ? combat.BaseHP : FallbackBaseHp;
        int baseAttack = combat != null && combat.BaseAttack > 0 ? combat.BaseAttack : FallbackBaseAttack;

        // CharacterId 写入的是战斗定义引用（不是人物身份）；PersonId 由构造函数内 PersonIdentity 生成。
        var person = new CharacterInstance(definition.CombatDefinitionId, definition.DisplayName, baseHp, baseAttack)
        {
            PersonDefinitionId = definition.PersonDefinitionId
        };

        ApplyInitialProfession(person, definition, registry);
        return person;
    }

    /// <summary>
    /// 应用开局已有的 Profession（不经过 Formation）。InitialProfession 未配置 = 合法「开局无职业」。
    /// </summary>
    private static void ApplyInitialProfession(CharacterInstance person, PersonDefinition definition,
        ProfessionDefinitionRegistry registry)
    {
        InitialProfessionConfig config = definition.InitialProfession;
        if (config == null || !config.IsConfigured) return;

        if (registry == null)
        {
            Debug.LogWarning("[PersonBootstrapper] 人物定义 " + definition.PersonDefinitionId
                + " 配置了 InitialProfession=" + config.ProfessionId
                + "，但当前没有 ProfessionDefinition Registry（职业模板缺失）；保持开局无职业（不猜职业）。");
            return;
        }

        if (!registry.TryGetById(config.ProfessionId, out ProfessionDefinition professionDefinition))
        {
            // 未知职业条目不得改投其它职业：显式失败并保持无职业。
            Debug.LogWarning("[PersonBootstrapper] 人物定义 " + definition.PersonDefinitionId
                + " 的 InitialProfession=" + config.ProfessionId
                + " 在 ProfessionDefinition Registry 中不存在；保持开局无职业（不改投其它职业）。");
            return;
        }

        // Person-owned 进度容器：五字段与 Formation Commit 的起始形状同构（但不经过 Formation）。
        person.ActiveProfessionProgress = new ProfessionProgress
        {
            ProfessionId = professionDefinition.ProfessionId,
            ProfessionLevel = config.ResolveLevel(),
            ProfessionExp = config.ResolveExp(),
            UnlockedPoolCardIds = ClonePool(config.ResolveUnlockedPoolSource(professionDefinition)),
            ProfessionTraitLevel = config.ResolveTraitLevel()
        };

        // Starter 仍 Character-owned：整 List 深拷贝替换（override 整值替换，否则模板默认）。
        person.StarterSlots = CloneStarterBuild(config.ResolveStarterBuildSource(professionDefinition));

        // 明确不做：FormationState / LifeHistory 写入、FormationResolver/Committer 调用、Legacy 字段推断。
    }

    /// <summary>Unlocked Pool 深拷贝（绝不共享 ProfessionDefinition / 配置的 List 引用）。</summary>
    private static List<string> ClonePool(IReadOnlyList<string> source)
    {
        var result = new List<string>();
        if (source == null) return result;
        for (int i = 0; i < source.Count; i++)
        {
            string id = source[i];
            if (string.IsNullOrEmpty(id)) continue;
            result.Add(id);
        }
        return result;
    }

    /// <summary>Starter 构建深拷贝（条目同样新建，绝不共享 StarterSlotEntry 引用）。</summary>
    private static List<StarterSlotEntry> CloneStarterBuild(IReadOnlyList<StarterSlotEntry> source)
    {
        var result = new List<StarterSlotEntry>();
        if (source == null) return result;
        for (int i = 0; i < source.Count; i++)
        {
            var entry = source[i];
            result.Add(entry == null
                ? new StarterSlotEntry(string.Empty, 0)
                : new StarterSlotEntry(entry.CardId, entry.Proficiency));
        }
        return result;
    }
}
