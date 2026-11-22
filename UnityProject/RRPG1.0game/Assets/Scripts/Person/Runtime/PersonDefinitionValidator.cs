using System.Collections.Generic;

/// <summary>
/// PersonDefinition 合法性校验（纯逻辑，无 Unity 依赖；与 ProfessionDefinitionValidator 同风格）。
///
/// 覆盖的最低合法性要求：
///   1) PersonDefinitionId 不允许 null/empty；
///   2) DisplayName 不允许 null/empty（玩家表现名称必须有值）；
///   3) CombatDefinitionId 不允许 null/empty（战斗定义引用必须可追踪）；
///   4) InitialProfession（若配置）：ProfessionId 不允许 null/empty，InitialLevel &gt;= 1，
///      InitialExp &gt;= 0，InitialTraitLevel &gt;= 1；override 列表本身不允许 null；
///      配置 override 时列表条目不允许空 CardId / 负 Proficiency；
///   5) InitialRosterOrder 不允许负数；
///   6) 集合维度：PersonDefinitionId 不允许重复；InitialRosterOrder &gt; 0 不允许重复（初始名册顺序必须唯一）。
///
/// 校验失败必须被明确暴露（返回 false + 错误列表），禁止静默把非法状态当正式配置继续使用。
/// 本 Validator 只校验 PersonDefinition 自身一致性；跨定义引用（CombatDefinitionId / CardDefinitionId /
/// ProfessionId / CardId 是否真实存在）由 ExcelImporter / DataValidationTool 负责。
/// </summary>
public static class PersonDefinitionValidator
{
    /// <summary>校验单个 PersonDefinition。返回 true = 合法；false 时 errors 包含全部问题。</summary>
    public static bool TryValidate(PersonDefinition definition, out List<string> errors)
    {
        errors = new List<string>();
        if (definition == null)
        {
            errors.Add("PersonDefinition 为 null");
            return false;
        }

        if (string.IsNullOrEmpty(definition.PersonDefinitionId))
            errors.Add("PersonDefinitionId 不能为 null/empty（固定初始人物定义缺少配置 ID）");

        if (string.IsNullOrEmpty(definition.DisplayName))
            errors.Add("DisplayName 不能为 null/empty（固定初始人物必须有玩家可见的人物名字）");

        if (string.IsNullOrEmpty(definition.CombatDefinitionId))
            errors.Add("CombatDefinitionId 不能为 null/empty（必须引用战斗定义）");

        if (definition.InitialRosterOrder < 0)
            errors.Add("InitialRosterOrder 不能为负数（0 = 不进初始名册）");

        ValidateInitialProfession(definition.InitialProfession, errors);
        return errors.Count == 0;
    }

    /// <summary>
    /// 校验一个 PersonDefinition 集合（例如某个 Catalog 的全部定义）。
    /// 除逐条校验外，还检查重复 PersonDefinitionId 与重复初始名册顺序。返回 true = 全部合法。
    /// </summary>
    public static bool TryValidateAll(IEnumerable<PersonDefinition> definitions, out List<string> errors)
    {
        errors = new List<string>();
        if (definitions == null)
        {
            errors.Add("PersonDefinition 集合为 null");
            return false;
        }

        var seenIds = new HashSet<string>();
        var reportedDuplicateIds = new HashSet<string>();
        var seenRosterOrders = new HashSet<int>();
        var reportedDuplicateOrders = new HashSet<int>();

        foreach (PersonDefinition definition in definitions)
        {
            if (definition == null)
            {
                errors.Add("PersonDefinition 集合包含 null 条目");
                continue;
            }

            if (!string.IsNullOrEmpty(definition.PersonDefinitionId)
                && !seenIds.Add(definition.PersonDefinitionId)
                && reportedDuplicateIds.Add(definition.PersonDefinitionId))
            {
                errors.Add("重复 PersonDefinitionId：'" + definition.PersonDefinitionId
                    + "' 在同一配置集合中出现多次（必须唯一，不得静默取任意一个）");
            }

            if (definition.InitialRosterOrder > 0
                && !seenRosterOrders.Add(definition.InitialRosterOrder)
                && reportedDuplicateOrders.Add(definition.InitialRosterOrder))
            {
                errors.Add("重复 InitialRosterOrder：" + definition.InitialRosterOrder
                    + "（初始名册顺序必须唯一，不得静默决定谁在前）");
            }

            if (!TryValidate(definition, out List<string> singleErrors))
                errors.AddRange(singleErrors);
        }

        return errors.Count == 0;
    }

    private static void ValidateInitialProfession(InitialProfessionConfig config, List<string> errors)
    {
        if (config == null) return; // null = 开局无职业（合法，未来随机 Person 分支）

        if (string.IsNullOrEmpty(config.ProfessionId))
            errors.Add("InitialProfession.ProfessionId 不能为 null/empty（配置了 InitialProfession 就必须给出职业）");

        if (config.InitialLevel < 1)
            errors.Add("InitialProfession.InitialLevel 必须 >= 1");

        if (config.InitialExp < 0)
            errors.Add("InitialProfession.InitialExp 不能为负数");

        if (config.InitialTraitLevel < 1)
            errors.Add("InitialProfession.InitialTraitLevel 必须 >= 1");

        if (config.HasUnlockedPoolOverride)
            ValidateCardIdList(config.InitialUnlockedPoolOverride,
                "InitialProfession.InitialUnlockedPoolOverride", errors);

        if (config.HasStarterBuildOverride)
            ValidateStarterBuild(config.InitialStarterBuildOverride, errors);
    }

    private static void ValidateCardIdList(List<string> cardIds, string fieldLabel, List<string> errors)
    {
        if (cardIds == null)
        {
            errors.Add(fieldLabel + " 为 null（应配置为列表；可为空列表 = 明确替换为空池）");
            return;
        }

        for (int i = 0; i < cardIds.Count; i++)
        {
            if (string.IsNullOrEmpty(cardIds[i]))
                errors.Add(fieldLabel + "[" + i + "] 的 CardId 不能为 null/empty");
        }
    }

    private static void ValidateStarterBuild(List<StarterSlotEntry> starterBuild, List<string> errors)
    {
        if (starterBuild == null)
        {
            errors.Add("InitialProfession.InitialStarterBuildOverride 为 null（应配置为列表；可为空列表）");
            return;
        }

        for (int i = 0; i < starterBuild.Count; i++)
        {
            var s = starterBuild[i];
            if (s == null)
            {
                errors.Add("InitialProfession.InitialStarterBuildOverride[" + i + "] 为 null（不允许 null 条目）");
                continue;
            }
            if (string.IsNullOrEmpty(s.CardId))
                errors.Add("InitialProfession.InitialStarterBuildOverride[" + i + "].CardId 不能为 null/empty");
            if (s.Proficiency < 0)
                errors.Add("InitialProfession.InitialStarterBuildOverride[" + i + "].Proficiency 不能为负数");
        }
    }
}
