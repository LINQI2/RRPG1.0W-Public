using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

/// <summary>
/// **StaticConfig Monster 测试数据 Helper**（M2 Slice 2B：命名统一为 <c>StaticMonsterTestData</c>）。
///
/// 职责边界（M2 Slice 2B §原则3）：
///   - **只返回** <c>List&lt;MonsterDefinition&gt;</c>（StaticConfig Authority 的同一批实例）；
///   - **禁止返回** `MonsterData`（Legacy）——Legacy 等价证据只允许存在于迁移验证测试（MonsterShadowTests /
///     MonsterRegistryEntryTests）中，且必须显式注明用途，**不得**成为产品测试的默认输入。
///   - 无 Mock / 无第二套数据系统：数据一律来自 <c>StaticConfigRegistry.Monsters</c>；
///     Root 缺失时由 Registry 内部的 D-M2-1 Disposable 失败保护兜底（Helper 不自行判断 fallback）。
///
/// 程序集注意：PlayMode 各测试程序集互不引用，因此本 Helper 在每个所需程序集**各存在一份同名副本**
/// （PlayMode 根 / ExpeditionV4F / VillageV4），语义必须保持一致。
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
