using UnityEngine;

/// <summary>
/// Run 级可配置参数（Stage 6A）。
/// 所有数值均为测试值、未冻结；正式数值待用户/策划确认，不得自行定稿。
/// 可在 Inspector 中新建/调整 RunConfig 资产覆盖默认值。
/// </summary>
[CreateAssetMenu(fileName = "NewRunConfig", menuName = "RRPG/Battle V0.1/Run 配置")]
public class RunConfig : ScriptableObject
{
    [Header("战后恢复（测试值，未冻结）")]
    [Tooltip("战后恢复 = MaxHP × 百分比（向上取整）；0 表示不恢复")]
    [Range(0f, 1f)]
    public float PostBattleRecoveryPercent = 0.05f;

    [Tooltip("战后恢复最低值（百分比>0 时至少恢复该值，保证 0HP 战后可恢复）")]
    public int PostBattleRecoveryMin = 1;

    [Header("领悟（测试值，未冻结）")]
    [Tooltip("触发一次卡牌奖励机会所需领悟阈值；触发只扣阈值不清零、溢出保留。\n【测试值】当前=每场胜利默认获得量，便于 Milestone D 一场触发一次奖励；正式数值未冻结")]
    public int InsightThreshold = 30;

    [Tooltip("每场胜利提供的领悟量（遭遇源 BattleVictory 的测试值，未冻结）")]
    public int InsightPerBattle = 30;
}
