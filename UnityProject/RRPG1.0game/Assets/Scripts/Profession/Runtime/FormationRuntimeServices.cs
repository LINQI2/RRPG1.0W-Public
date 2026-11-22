/// <summary>
/// M6 可注入 Formation Runtime Services（制作人裁决 1 / Stage 2 wiring 注入点）。
///
/// Runtime Core（FormationResolver / FormationCommitter）不内置任何正式数值或正式职业内容；
/// 桥（Assembly-CSharp VillageRuntimeBridge）持有本服务容器，默认 null = 本轮不产生 Formation 行为。
/// 测试 / 正式配置可在以后替换本容器内容而不改 Runtime Core。
///
/// 字段均为可空：
///   - Registry 为空 → 不可 Commit（Formation 需要 ProfessionDefinition 唯一合法模板）；
///   - Config 为空 → 不运行日结算（无 Test/Not Frozen 数值就不猜测）；
///   - WorkEvidenceMapper 为空 → Assignment 实际执行不产生 Work Evidence。
/// </summary>
public sealed class FormationRuntimeServices
{
    /// <summary>唯一合法的 ProfessionDefinition Registry（commit 的模板权威来源）。</summary>
    public ProfessionDefinitionRegistry Registry;

    /// <summary>Formation 数值 / 算法配置（Test / Not Frozen；可整组替换）。</summary>
    public FormationTestConfig Config;

    /// <summary>Workplace 实际执行 → Formation Evidence 的测试映射（Test / Not Frozen）。</summary>
    public IFormationWorkEvidenceMapper WorkEvidenceMapper;
}
