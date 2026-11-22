using System;
using System.Collections.Generic;

/// <summary>
/// Initial Person &amp; Profession Bootstrap —— 固定初始人物的「开局已有 Profession」配置（纯配置，不承载运行时状态）。
///
/// 语义边界（与审计结论一致）：
///   1) 这是**初始化配置**，不是运行时状态：不进入 Save（Save 仍只存 Person-owned 的
///      ProfessionProgress / StarterSlots 等既有区块），Bootstrap 只在 New Game 执行一次。
///   2) 缺省规则 = fallback 到 ProfessionDefinition 默认（Level 1 / Exp 0 / InitialUnlockedCards /
///      InitialStarterBuild / TraitLevel 1）；未配置的 override 一律用模板默认值。
///   3) Override 语义 = **replace whole**（整值替换），不做 merge / append：
///      配置了 override 就必须整体替换该维度，避免出现「两套 Authority 合并」。
///   4) InitialLevel &gt; 1 表示**初始化快照**：不追溯模拟 Lv1→当前等级，也不补发低等级成长节点。
///   5) 「开局已有职业」不是一次人生 Formation 事件：本配置只经 PersonBootstrapper 使用，
///      绝不经过 FormationResolver / FormationCommitter，不写 FormationState / ProfessionFormed。
///
/// Unity 序列化注意：
///   本类的实例会随 PersonDefinition 存在于 SO 资产与 CSV 导入路径中；Unity 会把「null List 字段」
///   序列化为空列表并在反序列化时物化为非 null，因此「未配置」不能靠 List 是否为 null 表达，
///   必须使用显式 Has*Override 布尔标记（与 SaveData 里 HasActiveProfessionProgress 同类模式）。
/// </summary>
[Serializable]
public class InitialProfessionConfig
{
    /// <summary>必填：该固定初始人物开局拥有的 ProfessionId（引用 ProfessionDefinition.ProfessionId）。</summary>
    public string ProfessionId;

    /// <summary>初始 ProfessionLevel（默认 1；&gt;1 表示初始化快照，不补发历史成长节点）。</summary>
    public int InitialLevel = 1;

    /// <summary>初始 ProfessionExp（默认 0）。</summary>
    public int InitialExp;

    /// <summary>初始 ProfessionTraitLevel（默认 1）。</summary>
    public int InitialTraitLevel = 1;

    /// <summary>
    /// true = 显式配置了 InitialUnlockedPoolOverride（整值替换模板 InitialUnlockedCards）；
    /// false = 未配置 → 使用 ProfessionDefinition.InitialUnlockedCards。
    /// </summary>
    public bool HasUnlockedPoolOverride;

    /// <summary>显式覆盖的初始 Unlocked Pool（CardId 列表；仅 HasUnlockedPoolOverride=true 时有语义）。</summary>
    public List<string> InitialUnlockedPoolOverride = new List<string>();

    /// <summary>
    /// true = 显式配置了 InitialStarterBuildOverride（整值替换模板 InitialStarterBuild）；
    /// false = 未配置 → 使用 ProfessionDefinition.InitialStarterBuild。
    /// </summary>
    public bool HasStarterBuildOverride;

    /// <summary>显式覆盖的初始 Starter 构建（仅 HasStarterBuildOverride=true 时有语义）。</summary>
    public List<StarterSlotEntry> InitialStarterBuildOverride = new List<StarterSlotEntry>();

    /// <summary>ProfessionId 是否已配置（空 = 未配置；固定初始人物配置缺失职业属配置错误，由 Validator/Importer 报告）。</summary>
    public bool IsConfigured => !string.IsNullOrEmpty(ProfessionId);

    /// <summary>解析初始 Level（防御下限钳制；非法配置由 Validator 报错，不在此处静默修正业务语义）。</summary>
    public int ResolveLevel() => InitialLevel >= 1 ? InitialLevel : 1;

    /// <summary>解析初始 Exp（防御下限钳制）。</summary>
    public int ResolveExp() => InitialExp > 0 ? InitialExp : 0;

    /// <summary>解析初始 TraitLevel（防御下限钳制）。</summary>
    public int ResolveTraitLevel() => InitialTraitLevel >= 1 ? InitialTraitLevel : 1;

    /// <summary>
    /// 解析初始 Unlocked Pool 来源（override 整值替换，否则模板默认）。
    /// 只返回来源引用；深拷贝由 PersonBootstrapper 负责（绝不共享模板 List 引用）。
    /// </summary>
    public IReadOnlyList<string> ResolveUnlockedPoolSource(ProfessionDefinition definition)
    {
        if (HasUnlockedPoolOverride) return InitialUnlockedPoolOverride;
        return definition != null ? definition.InitialUnlockedCards : null;
    }

    /// <summary>
    /// 解析初始 Starter 构建来源（override 整值替换，否则模板默认）。
    /// 只返回来源引用；深拷贝由 PersonBootstrapper 负责。
    /// </summary>
    public IReadOnlyList<StarterSlotEntry> ResolveStarterBuildSource(ProfessionDefinition definition)
    {
        if (HasStarterBuildOverride) return InitialStarterBuildOverride;
        return definition != null ? definition.InitialStarterBuild : null;
    }
}
