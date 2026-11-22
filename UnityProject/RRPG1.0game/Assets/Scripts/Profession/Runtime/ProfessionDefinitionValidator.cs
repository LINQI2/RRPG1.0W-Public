using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// M2 Profession Template 合法性校验（纯逻辑，无 Unity 依赖）。
///
/// 覆盖的最低合法性要求：
///   1) ProfessionId 不允许 null/empty；
///   2) FullCardPool 不允许空 CardId，不允许重复 CardId；
///   3) InitialUnlockedCards 不允许空 CardId，不允许重复 CardId；
///   4) InitialUnlockedCards ⊆ FullCardPool；
///   5) InitialStarterBuild（M6-B 扩展）列表不允许 null；条目不允许 null / 空 CardId / 负 Proficiency；
///   6) ProfessionTraitId（M6-B 扩展）不允许 null/empty；
///   7) 集合维度：同一配置内 ProfessionId 不允许重复。
///
/// 校验失败必须被明确暴露（返回 false + 错误列表，或由 Registry 构造抛异常），
/// 禁止静默把非法状态当作正式数据继续使用。
/// </summary>
public static class ProfessionDefinitionValidator
{
    /// <summary>校验单个 ProfessionDefinition。返回 true = 合法；false 时 errors 包含全部问题。</summary>
    public static bool TryValidate(ProfessionDefinition definition, out List<string> errors)
    {
        errors = new List<string>();
        if (definition == null)
        {
            errors.Add("ProfessionDefinition 为 null");
            return false;
        }

        if (string.IsNullOrEmpty(definition.ProfessionId))
            errors.Add("ProfessionId 不能为 null/empty（定义缺少职业模板 ID）");

        ValidateCardIdList(definition.FullCardPool, "FullCardPool", errors);
        ValidateCardIdList(definition.InitialUnlockedCards, "InitialUnlockedCards", errors);
        ValidateStarterBuild(definition.InitialStarterBuild, errors);

        if (string.IsNullOrEmpty(definition.ProfessionTraitId))
            errors.Add("ProfessionTraitId 不能为 null/empty（M6-B：职业模板必须声明固定初始 Profession Trait 定义 ID）");

        // Initial ⊆ Full（按 CardId 的 ordinal 语义比较；空/null 条目已在上面单独报错，不参与子集判定）
        if (definition.FullCardPool != null && definition.InitialUnlockedCards != null)
        {
            var fullSet = new HashSet<string>(
                definition.FullCardPool.Where(id => !string.IsNullOrEmpty(id)));
            for (int i = 0; i < definition.InitialUnlockedCards.Count; i++)
            {
                string id = definition.InitialUnlockedCards[i];
                if (!string.IsNullOrEmpty(id) && !fullSet.Contains(id))
                {
                    errors.Add(string.Format(
                        "InitialUnlockedCards[{0}] = '{1}' 不在 FullCardPool 中（InitialUnlockedCards 必须是 FullCardPool 的子集）",
                        i, id));
                }
            }
        }

        return errors.Count == 0;
    }

    private static void ValidateStarterBuild(List<StarterSlotEntry> starterBuild, List<string> errors)
    {
        if (starterBuild == null)
        {
            errors.Add("InitialStarterBuild 为 null（应配置为列表；允许空构建）");
            return;
        }

        for (int i = 0; i < starterBuild.Count; i++)
        {
            var s = starterBuild[i];
            if (s == null)
            {
                errors.Add(string.Format("InitialStarterBuild[{0}] 为 null（不允许 null 条目）", i));
                continue;
            }
            if (string.IsNullOrEmpty(s.CardId))
                errors.Add(string.Format("InitialStarterBuild[{0}] 的 CardId 不能为 null/empty", i));
            if (s.Proficiency < 0)
                errors.Add(string.Format("InitialStarterBuild[{0}] 的 Proficiency 不能为负数", i));
        }
    }

    /// <summary>
    /// 校验一个 ProfessionDefinition 集合（例如某个 Catalog 的全部定义）。
    /// 除逐条校验外，还检查重复 ProfessionId。返回 true = 全部合法。
    /// </summary>
    public static bool TryValidateAll(IEnumerable<ProfessionDefinition> definitions, out List<string> errors)
    {
        errors = new List<string>();
        if (definitions == null)
        {
            errors.Add("ProfessionDefinition 集合为 null");
            return false;
        }

        var seenIds = new HashSet<string>();
        var reportedDuplicateIds = new HashSet<string>();

        foreach (ProfessionDefinition definition in definitions)
        {
            if (definition == null)
            {
                errors.Add("ProfessionDefinition 集合包含 null 条目");
                continue;
            }

            if (!string.IsNullOrEmpty(definition.ProfessionId))
            {
                if (!seenIds.Add(definition.ProfessionId) && reportedDuplicateIds.Add(definition.ProfessionId))
                {
                    errors.Add(string.Format(
                        "重复 ProfessionId：'{0}' 在同一配置集合中出现多次（必须唯一，不得静默取任意一个）",
                        definition.ProfessionId));
                }
            }

            if (!TryValidate(definition, out List<string> singleErrors))
                errors.AddRange(singleErrors);
        }

        return errors.Count == 0;
    }

    private static void ValidateCardIdList(List<string> cardIds, string fieldLabel, List<string> errors)
    {
        if (cardIds == null)
        {
            errors.Add(fieldLabel + " 为 null（应配置为列表；允许空列表）");
            return;
        }

        var seen = new HashSet<string>();
        var reportedDuplicates = new HashSet<string>();
        for (int i = 0; i < cardIds.Count; i++)
        {
            string id = cardIds[i];
            if (string.IsNullOrEmpty(id))
            {
                errors.Add(string.Format("{0}[{1}] 的 CardId 不能为 null/empty", fieldLabel, i));
            }
            else if (!seen.Add(id) && reportedDuplicates.Add(id))
            {
                errors.Add(string.Format("{0} 出现重复 CardId：'{1}'（同一集合内不得重复）", fieldLabel, id));
            }
        }
    }
}

/// <summary>
/// ProfessionDefinition 配置不合法时抛出的异常（M2）。
/// Errors 提供逐条错误，便于调用方与测试看到明确失败原因，而不是静默接受非法配置。
/// </summary>
public sealed class ProfessionDefinitionValidationException : Exception
{
    /// <summary>逐条错误信息（只读快照）</summary>
    public IReadOnlyList<string> Errors { get; }

    public ProfessionDefinitionValidationException(IReadOnlyList<string> errors)
        : base("ProfessionDefinition 配置不合法：\n" + string.Join("\n", errors ?? Array.Empty<string>()))
    {
        Errors = errors ?? Array.Empty<string>();
    }
}
