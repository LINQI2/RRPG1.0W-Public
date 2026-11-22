using System.Collections.Generic;

/// <summary>
/// M4-correction（Narrow Growth Authority Routing）—— Growth 权威视图。
///
/// IPersonGrowth 是共享「人物长期数据契约」，保持 Legacy 语义，不因 ActiveProfessionProgress 改变宿主；
/// 因此 Growth 系统不得直接把 IPersonGrowth.Level/Exp/UnlockedPool/Trait 当 Profession 权威读写。
/// Growth 调用入口（CharacterGrowthService / CardGrowthResolver / CharacterGrowthUI）统一经
/// GrowthAuthority.Resolve(person) 显式解析本接口：
///   - Hero                          → IsAvailable=true（Legacy 权威，视图即 Hero 自身长期字段）；
///   - 普通 Character 有 Active Profession → IsAvailable=true（Profession 权威 = ActiveProfessionProgress）；
///   - 普通 Character 无 Profession      → IsAvailable=false（Growth 不可执行，无 Legacy fallback）。
///
/// StarterSlots 对两类宿主都始终为「人物自身长期字段」（普通 Character 的 Starter 仍 Character-owned）。
/// 不为普通 Character 建立 Person-specific Trait / Dormant / Formation；TraitUpgrade 对 Profession Growth
/// 只对齐 ProfessionTraitLevel 数值状态（正式 Profession Trait Definition 属未来 M 阶段）。
/// </summary>
public interface IGrowthAuthority
{
    /// <summary>当前 Growth 是否可执行（Hero：true；普通 Character：仅已有 Active Profession）。</summary>
    bool IsAvailable { get; }

    /// <summary>当前 Growth 是否属于 Profession Growth（普通 Character 且已有 Active Profession）。</summary>
    bool IsProfessionGrowth { get; }

    /// <summary>权威 Level（Hero：Legacy Level；普通 Character：ProfessionLevel）。</summary>
    int Level { get; set; }

    /// <summary>权威 Exp（Hero：Legacy Exp；普通 Character：ProfessionExp）。</summary>
    int Exp { get; set; }

    /// <summary>权威 Unlocked Pool（Hero：Legacy 池；普通 Character：ProfessionProgress.UnlockedPoolCardIds）。</summary>
    List<string> UnlockedPoolCardIds { get; }

    /// <summary>
    /// 权威 Person Trait ID。Hero（Legacy 路径）为 Legacy TraitId；
    /// 普通 Character（Profession Growth）恒为 null / set 无操作 —— 本 Stage 不为新 Profession 语义
    /// 创建 Person-specific Trait / 正式 TraitId。
    /// </summary>
    string TraitId { get; set; }

    /// <summary>
    /// 权威 Trait 强化数值状态（Hero：Legacy TraitLevel；
    /// 普通 Character：ProfessionProgress.ProfessionTraitLevel）。
    /// </summary>
    int TraitLevel { get; set; }

    /// <summary>Starter 槽（始终人物自身长期字段；普通 Character 的 Starter 仍 Character-owned）。</summary>
    List<StarterSlotEntry> StarterSlots { get; }
}

/// <summary>
/// Growth Authority 解析器（Growth.Runtime 唯一入口；禁止各调用方复制 if(Character+Profession) 判断）。
///   - 宿主（CharacterInstance / HeroInstance）实现了 IGrowthAuthority 时返回宿主自身提供的权威视图；
///   - 其它 IPersonGrowth（纯逻辑测试替身等）统一按 Legacy 权威包装（IsAvailable=true），
///     与 M1/M3 纯逻辑测试语义一致。
/// </summary>
public static class GrowthAuthority
{
    /// <summary>解析某个人物的 Growth 权威视图。person 为 null 时返回 null。</summary>
    public static IGrowthAuthority Resolve(IPersonGrowth person)
    {
        if (person == null) return null;
        if (person is IGrowthAuthority growth) return growth;
        return new LegacyAuthority(person);
    }

    /// <summary>未实现 IGrowthAuthority 的 IPersonGrowth（测试替身）按 Legacy 权威建模的通用适配。</summary>
    private sealed class LegacyAuthority : IGrowthAuthority
    {
        private readonly IPersonGrowth _person;

        public LegacyAuthority(IPersonGrowth person)
        {
            _person = person;
        }

        public bool IsAvailable => true;
        public bool IsProfessionGrowth => false;

        public int Level
        {
            get => _person.Level;
            set => _person.Level = value;
        }

        public int Exp
        {
            get => _person.Exp;
            set => _person.Exp = value;
        }

        public List<string> UnlockedPoolCardIds => _person.UnlockedPoolCardIds;

        public string TraitId
        {
            get => _person.TraitId;
            set => _person.TraitId = value;
        }

        public int TraitLevel
        {
            get => _person.TraitLevel;
            set => _person.TraitLevel = value;
        }

        public List<StarterSlotEntry> StarterSlots => _person.StarterSlots;
    }
}
