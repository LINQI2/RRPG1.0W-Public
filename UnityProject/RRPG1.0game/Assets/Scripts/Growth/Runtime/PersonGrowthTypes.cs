using System;
using System.Collections.Generic;

/// <summary>
/// 人物长期成长状态通用类型（Starter / Unlocked Pool / Trait）。
/// 只承载长期数据，不承载 Run / 战斗逻辑。
/// </summary>

/// <summary>
/// Starter Slot 长期状态：一个槽 = 每次 Run 开始时 1 张对应起始牌副本。
/// 同模板牌允许占多个 Starter Slot；Proficiency 只属于当前 Starter 中的牌，
/// Starter 牌被替换后高级 Proficiency 不保留。
/// </summary>
[Serializable]
public class StarterSlotEntry
{
    /// <summary>起始牌模板ID（BattleCardData.CardId）；空字符串 = 空槽</summary>
    public string CardId;

    /// <summary>熟练度（正式上限未冻结；只属于当前 Starter 中的牌）</summary>
    public int Proficiency;

    public StarterSlotEntry()
    {
    }

    public StarterSlotEntry(string cardId, int proficiency = 1)
    {
        CardId = cardId ?? string.Empty;
        Proficiency = proficiency;
    }

    public bool IsEmpty => string.IsNullOrEmpty(CardId);
}

/// <summary>
/// Growth Node 类型（V0.1 至少支持四种）。
/// 具体等级对应哪种节点由 GrowthTableConfig 数据驱动，不在此处硬编码 Lv2/Lv5/Lv10。
/// </summary>
public enum GrowthNodeType
{
    /// <summary>从人物 Unlocked Pool 三选一（可 Skip）→ 放入/替换 Starter 槽或 Proficiency+1</summary>
    CardGrowth,

    /// <summary>增加 1 个 Starter 槽</summary>
    StarterSlotUnlock,

    /// <summary>强化同一个人物唯一 Trait（等级+1，不替换、不随机新增）</summary>
    TraitUpgrade,

    /// <summary>扩展人物 Unlocked Pool（新增牌模板）</summary>
    UnlockedPoolExpand
}

/// <summary>CardGrowth 选定牌后的 Starter 处理策略</summary>
public enum StarterCardStrategy
{
    /// <summary>放入指定空槽（有空槽时也允许主动替换已有槽，故不强制填空槽）</summary>
    AddToEmptySlot,

    /// <summary>替换指定槽（原槽 Proficiency 不保留，从基础熟练重新培养）</summary>
    ReplaceSlot,

    /// <summary>当前 Starter 有同模板牌 → 该牌 Proficiency +1</summary>
    ProficiencyUp
}

/// <summary>
/// 人物长期成长状态统一接口（CharacterInstance / HeroInstance 实现）。
/// 这是共享「人物长期数据契约」：成员始终以宿主自身长期字段（Legacy 语义）读写，
/// 不因 ActiveProfessionProgress 存在而改变宿主（Starter 保持 Character-owned；Hero 全程 Legacy）。
///
/// M4-correction（Narrow Growth Authority Routing）：
///   Growth Resolver（CharacterGrowthService / CardGrowthResolver / CharacterGrowthUI）不得把本接口成员
///   当作 Profession Growth 权威直接读写；必须经 GrowthAuthority.Resolve(person) 显式解析
///   IGrowthAuthority：Hero → Legacy 权威；普通 Character 有 ActiveProfessionProgress → Profession 权威；
///   普通 Character 无 Profession → IsAvailable=false（Growth 不可执行，无 Legacy fallback）。
/// </summary>
public interface IPersonGrowth
{
    string PersonId { get; }

    string DisplayName { get; }

    /// <summary>等级（长期 Legacy 字段；等级本身不增加基础 HP/Attack/AttackRange。
    /// 普通 Character 的 Profession Growth 等级 = IGrowthAuthority.Level）。</summary>
    int Level { get; set; }

    /// <summary>当前长期经验（SafeReturn 兑现 + 主动升级消耗；Legacy 字段。
    /// 普通 Character 的 Profession Growth 经验 = IGrowthAuthority.Exp）。</summary>
    int Exp { get; set; }

    /// <summary>Starter 槽列表（一个槽 = 每次 Run 开始时 1 张对应起始牌副本；人物自身长期字段）。</summary>
    List<StarterSlotEntry> StarterSlots { get; set; }

    /// <summary>Unlocked Pool：Legacy 长期字段（Run 奖励等一般长期语义沿用；
    /// 普通 Character 的 Profession Growth 池 = IGrowthAuthority.UnlockedPoolCardIds）。</summary>
    List<string> UnlockedPoolCardIds { get; set; }

    /// <summary>人物唯一专属 Trait ID（Legacy 长期字段；TraitUpgrade 强化同一个，不替换）。</summary>
    string TraitId { get; set; }

    /// <summary>该 Trait 当前等级（Legacy 长期字段；正式效果与上限未冻结）。</summary>
    int TraitLevel { get; set; }
}
