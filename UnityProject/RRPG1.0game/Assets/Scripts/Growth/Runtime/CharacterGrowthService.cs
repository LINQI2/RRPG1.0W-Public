using System.Collections.Generic;

/// <summary>
/// 人物长期成长服务（纯逻辑）。
/// 负责：经验是否满、主动逐级升级、成长节点查找与固定型节点应用。
/// 不负责：UI、存档写入、Run 逻辑。
///
/// 关键约束：
///   - 经验达到升级需求时【不】自动升级；必须玩家主动调用 TryLevelUpOnce。
///   - 每次调用只处理一级；经验足够可连续逐级升级，也可暂不升级（经验与资格保留）。
///   - 等级本身不增加 HP / Attack / AttackRange 等基础数值。
///   - M4-correction（Narrow Growth Authority Routing）：本服务不直接以 IPersonGrowth 成员作为
///     Profession Growth 权威，统一经 GrowthAuthority.Resolve(person) 显式解析 IGrowthAuthority：
///       Hero → Legacy 权威；普通 Character 有 ActiveProfessionProgress → Profession 权威；
///       普通 Character 无 Profession → IsAvailable=false，本服务一律不执行（无 Legacy fallback）。
/// </summary>
public static class CharacterGrowthService
{
    /// <summary>是否可主动升级（经验达到升级需求且未达最高等级；无可用 Growth 的人物不可升级）</summary>
    public static bool CanLevelUp(IPersonGrowth person, GrowthCurveConfig curve)
    {
        if (person == null || curve == null) return false;
        IGrowthAuthority auth = GrowthAuthority.Resolve(person);
        if (auth == null || !auth.IsAvailable) return false;
        if (curve.IsMaxLevel(auth.Level)) return false;
        return auth.Exp >= curve.GetExpRequired(auth.Level);
    }

    /// <summary>从当前等级升到下一级所需经验（已达最高等级返回 0；无可用 Growth 返回 0）</summary>
    public static int GetExpRequired(IPersonGrowth person, GrowthCurveConfig curve)
    {
        if (person == null || curve == null) return 0;
        IGrowthAuthority auth = GrowthAuthority.Resolve(person);
        if (auth == null || !auth.IsAvailable) return 0;
        return curve.GetExpRequired(auth.Level);
    }

    /// <summary>数据驱动：等级 → 成长节点（无节点返回 null）</summary>
    public static GrowthNodeConfig GetNodeForLevel(GrowthTableConfig table, int level)
    {
        return table != null ? table.GetNodeForLevel(level) : null;
    }

    /// <summary>
    /// 玩家点击一次“升级”：消耗经验 + 等级 +1 + 返回该级成长节点。
    /// 固定型节点由 ApplyFixedNode 应用；CardGrowth 为选择型，由调用方进入交互流程。
    /// 返回 false = 不可升级（不消耗经验、不升等级）。
    /// 升级读写经 GrowthAuthority.Resolve(person) 解析的权威（Hero Legacy / 普通 Character Profession）。
    /// </summary>
    public static bool TryLevelUpOnce(IPersonGrowth person, GrowthCurveConfig curve,
        GrowthTableConfig table, out GrowthNodeConfig node)
    {
        node = null;
        if (!CanLevelUp(person, curve)) return false;

        IGrowthAuthority auth = GrowthAuthority.Resolve(person);
        int required = curve.GetExpRequired(auth.Level);
        auth.Exp = System.Math.Max(0, auth.Exp - required);
        auth.Level += 1;
        node = GetNodeForLevel(table, auth.Level);
        return true;
    }

    /// <summary>固定型成长节点应用结果</summary>
    public struct NodeApplyResult
    {
        public bool Applied;
        public string Message;
    }


    /// <summary>
    /// 应用固定型节点（StarterSlotUnlock / UnlockedPoolExpand / TraitUpgrade）。
    /// CardGrowth 为选择型，不在此应用。
    /// M4-correction：无可用 Growth（普通 Character 无 Active Profession）的人物一律不执行，且不 fallback 旧字段。
    /// </summary>
    public static NodeApplyResult ApplyFixedNode(IPersonGrowth person, GrowthNodeConfig node,
        TraitCatalog traitCatalog)
    {
        if (person == null || node == null)
            return new NodeApplyResult { Applied = false, Message = "无效节点" };

        IGrowthAuthority auth = GrowthAuthority.Resolve(person);
        if (auth == null || !auth.IsAvailable)
            return new NodeApplyResult { Applied = false, Message = "未形成职业，暂不可执行成长" };

        switch (node.NodeType)
        {
            case GrowthNodeType.StarterSlotUnlock:
                // Starter 始终人物自身长期字段（普通 Character 的 Starter 仍 Character-owned）
                auth.StarterSlots.Add(new StarterSlotEntry(string.Empty, 0));
                return new NodeApplyResult { Applied = true, Message = "解锁 1 个新 Starter 槽（空槽）" };

            case GrowthNodeType.UnlockedPoolExpand:
            {
                if (node.ExpandCardIds == null || node.ExpandCardIds.Length == 0)
                    return new NodeApplyResult { Applied = false, Message = "UnlockedPoolExpand 未配置扩展牌" };

                int added = 0;
                foreach (var id in node.ExpandCardIds)
                {
                    if (string.IsNullOrEmpty(id)) continue;
                    if (!auth.UnlockedPoolCardIds.Contains(id))
                    {
                        auth.UnlockedPoolCardIds.Add(id);
                        added++;
                    }
                }
                return new NodeApplyResult { Applied = true, Message = "Unlocked Pool 扩展 +" + added + " 张牌" };
            }

            case GrowthNodeType.TraitUpgrade:
            {
                if (auth.IsProfessionGrowth)
                {
                    // M4-correction：普通 Character（有 Active Profession）——TraitUpgrade 只把
                    // 「个人职业特性强化数值状态」对齐到 ProfessionProgress.ProfessionTraitLevel。
                    // 不创建 Profession Trait Definition / 正式 TraitId；不读 TraitCatalog 默认 Trait；
                    // 不按 PersonId→TraitCatalog 分配 Person-specific Trait；不实施正式 Trait 数值/上限
                    // （正式职业特性定义属未来 M 阶段，不在 M4）。
                    auth.TraitLevel += 1;
                    return new NodeApplyResult { Applied = true, Message = "职业特性强化 → Lv " + auth.TraitLevel };
                }

                // Legacy / Hero 路径（不变）：强化同一个人物唯一专属 Trait（TraitId 不存在时按节点或目录取默认）。
                if (string.IsNullOrEmpty(auth.TraitId))
                {
                    var trait = node.Trait;
                    if (trait == null && traitCatalog != null) trait = traitCatalog.GetDefaultTrait(person.PersonId);
                    if (trait == null)
                        return new NodeApplyResult { Applied = false, Message = "TraitUpgrade 无可用 Trait 配置" };
                    auth.TraitId = trait.TraitId;
                    auth.TraitLevel = 0;
                }

                int max = 0;
                if (node.Trait != null) max = node.Trait.MaxLevel;
                else if (traitCatalog != null && traitCatalog.GetDefaultTrait(person.PersonId) != null)
                    max = traitCatalog.GetDefaultTrait(person.PersonId).MaxLevel;

                if (max > 0 && auth.TraitLevel >= max)
                    return new NodeApplyResult { Applied = false, Message = "Trait 已达测试上限" };

                auth.TraitLevel += 1;
                return new NodeApplyResult { Applied = true, Message = "Trait 强化 → Lv " + auth.TraitLevel };
            }

            default:
                return new NodeApplyResult { Applied = false, Message = "选择型节点不在此处应用" };
        }
    }
}
