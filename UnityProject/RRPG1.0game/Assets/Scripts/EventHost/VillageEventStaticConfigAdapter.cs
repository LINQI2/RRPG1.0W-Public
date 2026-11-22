using System;
using System.Collections.Generic;
using System.Text;
using RRPG.Event;
using RRPG.StaticConfig;

/// <summary>
/// **VillageEventStaticConfigAdapter（Event 静态配置 → EventStaticCatalog 投影）** —— WP-Event-1D Gate D1 §2。
///
/// 职责（且仅此）：把 **已生成的静态配置**（`StaticConfigRegistry.TryGetEventConfig` →
/// `EventConfigDatabase` 的 10 张表）投影成 Event Core 唯一吃到的纯逻辑视图
/// <see cref="EventStaticCatalog"/>，并提供表现（Presentation）只读查询。
///
/// 为什么需要它：`EventStaticCatalog` 在 `Event.Runtime`（`noEngineReferences = true` 纯逻辑程序集）里，
/// 不能引用 `ScriptableObject`（`EventConfigDatabase`）；反之产品层（Assembly-CSharp）可以引用两者。
///
/// 硬约束：
///   - **只读**：不写配置、不缓存「一半新一半旧」的快照（每次调用都按当前 Registry 重新投影）；
///   - **不静默**：Root / 库缺失或配置自洽性失败 → 明确诊断（不返回「看起来能用」的空 Catalog 冒充成功）；
///   - **不做内容决策**：本类不求解事件、不绑定人物、不判断条件、不执行效果。
/// </summary>
public static class VillageEventStaticConfigAdapter
{
    /// <summary>
    /// 投影为 <see cref="EventStaticCatalog"/>。
    /// 返回 false 时 <paramref name="catalog"/> 为 null 且 <paramref name="diagnostic"/> 可操作。
    /// 返回 true 时 Catalog 一定可用；<paramref name="diagnostic"/> 非空只表示「领域可用但没有任何记录」
    /// （合法状态，不是错误 —— 此时求解结果自然是「没有候选事件」）。
    /// </summary>
    public static bool TryBuild(out EventStaticCatalog catalog, out string diagnostic)
    {
        catalog = null;
        diagnostic = string.Empty;

        EventConfigDatabase database;
        string configDiagnostic;
        if (!StaticConfigRegistry.TryGetEventConfig(out database, out configDiagnostic) || database == null)
        {
            diagnostic = "Event 静态配置不可用："
                + (string.IsNullOrEmpty(configDiagnostic) ? "（无诊断）" : configDiagnostic);
            return false;
        }

        var projected = new EventStaticCatalog(
            database.Events,
            database.Triggers,
            database.Roles,
            database.RoleConditions,
            database.Conditions,
            database.Options,
            database.OptionConditions,
            database.Outcomes,
            database.Effects);

        if (!projected.IsValid)
        {
            diagnostic = "Event 静态配置自洽性校验失败（Catalog 不可用于求解）：" + Describe(projected.Errors);
            return false;
        }

        catalog = projected;
        diagnostic = string.IsNullOrEmpty(configDiagnostic) ? string.Empty : configDiagnostic;
        return true;
    }

    /// <summary>按 EventId 取该事件的**表现配置**（一个事件恰好一条；未命中 → false）。</summary>
    public static bool TryGetPresentation(string eventId, out EventPresentationDefinition presentation)
    {
        presentation = null;
        if (string.IsNullOrEmpty(eventId)) return false;

        EventConfigDatabase database;
        string ignored;
        if (!StaticConfigRegistry.TryGetEventConfig(out database, out ignored) || database == null) return false;

        var list = database.Presentations;
        for (int i = 0; i < list.Count; i++)
        {
            var candidate = list[i];
            if (candidate == null) continue;
            if (!string.Equals(candidate.EventId, eventId, StringComparison.Ordinal)) continue;

            presentation = candidate;
            return true;
        }
        return false;
    }

    /// <summary>按 RoleId 取角色定义（迁移旧档时需要真实 SelectorType / RoleName；未命中 → false）。</summary>
    public static bool TryGetRole(string roleId, out EventRoleDefinition role)
    {
        role = null;
        if (string.IsNullOrEmpty(roleId)) return false;

        EventConfigDatabase database;
        string ignored;
        if (!StaticConfigRegistry.TryGetEventConfig(out database, out ignored) || database == null) return false;

        var list = database.Roles;
        for (int i = 0; i < list.Count; i++)
        {
            var candidate = list[i];
            if (candidate == null) continue;
            if (!string.Equals(candidate.RoleId, roleId, StringComparison.Ordinal)) continue;

            role = candidate;
            return true;
        }
        return false;
    }

    /// <summary>该 EventId 是否在当前静态配置中（Load 后校验 Pending / 迁移前校验映射用）。</summary>
    public static bool EventExists(string eventId)
    {
        if (string.IsNullOrEmpty(eventId)) return false;

        EventConfigDatabase database;
        string ignored;
        if (!StaticConfigRegistry.TryGetEventConfig(out database, out ignored) || database == null) return false;

        EventDefinition definition;
        return database.TryGetEvent(eventId, out definition);
    }

    private static string Describe(IReadOnlyList<string> errors)
    {
        if (errors == null || errors.Count == 0) return "（无明细）";

        var builder = new StringBuilder();
        int max = errors.Count < 3 ? errors.Count : 3;
        for (int i = 0; i < max; i++)
        {
            if (i > 0) builder.Append(" | ");
            builder.Append(errors[i]);
        }
        if (errors.Count > max) builder.Append(" | …（共 ").Append(errors.Count).Append(" 项）");
        return builder.ToString();
    }
}
