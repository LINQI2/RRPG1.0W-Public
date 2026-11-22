using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// M2 Profession Template authoring 容器（ScriptableObject）。
///
/// 职责：把一组 ProfessionDefinition 作为可序列化/可配置资产组织在一起。
/// 正式内容管线落地前，本 Catalog 只承载 Test / Not Frozen 内容；
/// 不在本 M2 阶段把猎人/农夫/铁匠等正式职业内容写回 GameSpec。
///
/// 使用方式：
///   var registry = catalog.BuildRegistry();   // 非法（含 duplicate ProfessionId）时抛异常
///   registry.GetById("profession_test_a");
///
/// 注意：本 Catalog 只是"静态模板定义"容器，绝不保存任何 Person 的动态成长状态。
/// </summary>
[CreateAssetMenu(fileName = "NewProfessionDefinitionCatalog", menuName = "RRPG/职业模板/职业目录（测试值，未冻结）")]
public class ProfessionDefinitionCatalog : ScriptableObject
{
    [Tooltip("职业模板定义列表。同一 ProfessionId 只允许出现一次；重复会令 BuildRegistry 校验失败（不得静默取首个）")]
    public List<ProfessionDefinition> Definitions = new List<ProfessionDefinition>();

    /// <summary>
    /// 对当前 Definitions 做全量校验并构造查询 Registry。
    /// 任何非法定义（空 ProfessionId / 空或重复 CardId / Initial ⊄ Full / 重复 ProfessionId）
    /// 都会抛出 ProfessionDefinitionValidationException，不会把非法状态当作正式数据继续使用。
    /// </summary>
    public ProfessionDefinitionRegistry BuildRegistry()
    {
        return ProfessionDefinitionRegistry.Build(Definitions);
    }
}
