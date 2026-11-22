using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

/// <summary>
/// **StaticConfig Monster 测试数据 Helper**（VillageV4 程序集副本；统一命名 <c>StaticMonsterTestData</c>）。
///
/// 职责边界：只返回 List&lt;MonsterDefinition&gt;（来自 StaticConfigRegistry.Monsters）；
/// **禁止返回** `MonsterData`（Legacy）——Legacy 只允许出现在迁移验证测试并注明用途。
/// 无 Mock / 无第二套数据系统；Root 缺失由 Registry 内部 D-M2-1 失败保护兜底。
///
/// 清理义务：M2 Closeout 视需要删除或升格为长期测试资产。
/// </summary>
public static class StaticMonsterTestData
{
    private const string StaticConfigAssembly = "StaticConfig.Runtime";
    private const BindingFlags PublicStatic = BindingFlags.Public | BindingFlags.Static;
    private const BindingFlags PublicInstance = BindingFlags.Public | BindingFlags.Instance;

    public static Type DefinitionType()
    {
        var type = Type.GetType("RRPG.StaticConfig.MonsterDefinition, " + StaticConfigAssembly, false);
        if (type == null) throw new InvalidOperationException("MonsterDefinition 未找到（" + StaticConfigAssembly + "）");
        return type;
    }

    public static Type ListType()
    {
        return typeof(List<>).MakeGenericType(DefinitionType());
    }

    /// <summary>构造 List&lt;MonsterDefinition&gt;（内容 = StaticConfigRegistry.Monsters.All 的同一批实例）。</summary>
    public static IList Build()
    {
        var registryType = Type.GetType("RRPG.StaticConfig.StaticConfigRegistry, " + StaticConfigAssembly, false);
        if (registryType == null) throw new InvalidOperationException("StaticConfigRegistry 未找到（" + StaticConfigAssembly + "）");

        var registry = registryType.GetProperty("Monsters", PublicStatic).GetValue(null, null);
        if (registry == null)
        {
            var diagnostic = registryType.GetProperty("MonsterDiagnostic", PublicStatic).GetValue(null, null);
            throw new InvalidOperationException("StaticConfigRegistry.Monsters 不可用：" + diagnostic);
        }

        var list = (IList)Activator.CreateInstance(ListType());
        var all = (IEnumerable)registry.GetType().GetProperty("All", PublicInstance).GetValue(registry, null);
        foreach (var definition in all) list.Add(definition);
        return list;
    }
}
