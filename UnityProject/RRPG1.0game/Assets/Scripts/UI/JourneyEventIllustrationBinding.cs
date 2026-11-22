using UnityEngine;

/// <summary>
/// JourneyEventPresentationEntry —— 一条「PresentationKey → Illustration」的最小绑定条目
/// （Journey Event Illustration Binding V0.1｜Implementation-only / Not Frozen）。
///
/// 语义（硬约束）：
///   - `PresentationKey` 只回答「这张卡面用哪张插图」；V0.1 允许 = 当前稳定 NodeId（Implementation-level 临时来源）；
///   - 它**不是** EventId / EncounterId，不冻结 `NodeId == EventId`；
///   - 未来正式 Event / Encounter Content 系统建立后，PresentationKey 的来源可整体替换为
///     `EventDefinitionId / EncounterDefinitionId`，本条目与 Catalog 不需要重做；
///   - 它**不承载**任何 Event Content Authority：本条目只有键与图，
///     禁止把 Title / Body / Options / 奖励 / 条件 / 概率 / 文案搬进这里。
///
/// 解析顺序（见 JourneyEventIllustrationResolver）：
///   具体 PresentationKey 的 Illustration → 类别级 fallback → 隐藏 Illustration Host（其它卡面内容继续工作）。
/// </summary>
[System.Serializable]
public class JourneyEventPresentationEntry
{
    /// <summary>表现键（V0.1 = 当前稳定 NodeId）。只用于决定「用哪张插图」。</summary>
    [Tooltip("表现键：V0.1 填当前稳定 NodeId。只决定「用哪张插图」，不是 EventId，也不是事件内容 Authority。")]
    public string PresentationKey;

    /// <summary>该键对应的事件卡插图（空 = 该行不生效，回退类别级 fallback；不自动删除行）。</summary>
    [Tooltip("事件卡插图 Sprite：导入图片后拖到这里。留空 = 该行不生效（回退类别级 fallback，不报错）。")]
    public Sprite Illustration;
}

/// <summary>
/// JourneyEventIllustrationResult —— 一次事件卡插图解析结果（只读值对象；仅用于表现与诊断）。
/// Source / UsedCategoryFallback 仅供诊断与测试；不写回任何长期数据，不构成第二套 Event Authority。
/// </summary>
public struct JourneyEventIllustrationResult
{
    /// <summary>本次使用的表现键（类别-only 路径为空：不伪造键）。</summary>
    public string PresentationKey;

    /// <summary>本次卡面类别（只用于类别级 fallback 与诊断）。</summary>
    public JourneyEncounterKind Kind;

    /// <summary>最终插图；null = 卡面应隐藏 Illustration Host（Title / Body / Options 继续正常工作）。</summary>
    public Sprite Illustration;

    /// <summary>是否使用了类别级 fallback（未配置具体键 / 配置行无图 / 类别-only 表面）。</summary>
    public bool UsedCategoryFallback;

    /// <summary>诊断来源：binding:&lt;key&gt; / category:&lt;kind&gt; / none:&lt;kind&gt;。</summary>
    public string Source;
}
