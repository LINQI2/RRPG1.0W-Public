using System.Collections.Generic;
using RRPG.Encounter;
using RRPG.StaticConfig;

/// <summary>
/// BattleSystem 的 **EnemyRoster 入场路径**（WP-Encounter-1C §8 / §9 / §11）。
///
/// 产品路径唯一敌方入场入口：`Active EnemyRoster` → 逐项创建独立 Enemy BattleUnit → 全部合法部署。
///
/// 硬约束：
///   - **不再**用「整个 Monster Registry 当本场敌人」，也**不再**只部署前 3 只；
///   - 1..9 只全部创建、全部进入 Enemy Board、每格唯一占位（无重叠、无丢失）；
///   - UnitId = EnemyInstanceId（本场身份）；SourceId = MonsterId（模板身份）；
///   - 任一敌人无法解析定义 / 位置非法 / 实例 ID 重复 → **整场拒绝**（零部分写入，不缩小阵容继续）。
/// </summary>
public partial class BattleSystem
{
    /// <summary>
    /// 以**冻结的敌方阵容**初始化战斗（WP-Encounter-1C 正式路径）。
    ///
    /// 注意：刻意**不**叫 `InitializeBattle` —— 既有 Legacy / Demo 重载与本方法若同名，
    /// 会让既有回归测试的 `Type.GetMethod("InitializeBattle")` 反射调用变成
    /// `AmbiguousMatchException`（旧测试必须继续按 list 顺序语义运行）。
    ///
    /// 先全量校验、再一次性建立（失败时战斗数据保持未初始化状态，调用方必须拒绝开战），
    /// 与既有「部署完整性错误不得静默缩减阵容」的 C 不变量同一语义。
    /// </summary>
    /// <param name="playerCharacters">玩家选择的角色卡</param>
    /// <param name="monsterCatalog">正式 Monster 定义目录（名称 / 数值 / 美术的模板来源）</param>
    /// <param name="enemyRoster">已冻结的本场敌方阵容（Generator 输出；不得在此重新随机）</param>
    /// <param name="playerSkills">玩家初始技能</param>
    /// <param name="enemySkills">敌方初始技能</param>
    /// <param name="diagnostic">失败原因（成功时为空字符串）</param>
    /// <returns>true = 敌方单位已全部创建并部署；false = 未做任何写入</returns>
    public bool InitializeBattleFromEnemyRoster(List<CharacterCardData> playerCharacters,
                                 IReadOnlyList<MonsterDefinition> monsterCatalog,
                                 EnemyRoster enemyRoster,
                                 List<SkillData> playerSkills, List<SkillData> enemySkills,
                                 out string diagnostic)
    {
        List<EnemyUnitPlan> plan;
        if (!TryBuildEnemyUnitPlan(monsterCatalog, enemyRoster, out plan, out diagnostic)) return false;

        PrepareBattleBeforeEnemySetup(playerCharacters, enemySkills);

        for (int i = 0; i < plan.Count; i++)
        {
            var step = plan[i];
            var battleUnit = CreateMonsterUnit(step.Definition, step.EnemyInstanceId);
            _battleData.EnemyUnits.Add(battleUnit);

            // 站位 = EnemyRoster 已求解并冻结的逻辑 Row / Column（不再随机、不再只放前 3 只）
            _battleData.EnemyField.Deploy(battleUnit, step.Row, step.Column);
        }

        diagnostic = string.Empty;
        return true;
    }

    /// <summary>
    /// 全量校验并构建「敌方单位计划」（不写任何战斗状态）。
    /// 校验项：Roster 非空且 1..9；每个 EnemyInstanceId 唯一；每格唯一；MonsterId 可在目录解析；
    /// 且**每只都有合法站位**（不得出现「创建了但没有棋盘位置」的单位）。
    /// </summary>
    public static bool TryBuildEnemyUnitPlan(IReadOnlyList<MonsterDefinition> monsterCatalog,
        EnemyRoster enemyRoster, out List<EnemyUnitPlan> plan, out string diagnostic)
    {
        plan = new List<EnemyUnitPlan>();

        if (enemyRoster == null)
        {
            diagnostic = "敌方阵容不可用（EnemyRoster 为 null）：本次战斗未初始化。";
            return false;
        }

        var entries = enemyRoster.Entries;
        if (entries == null || entries.Count == 0)
        {
            diagnostic = "敌方阵容为空（EnemyRoster：" + enemyRoster.EncounterId + "）：不进入一场 0 敌人的战斗。";
            return false;
        }

        if (entries.Count > EnemyBoardFormation.MaxEnemyUnits)
        {
            diagnostic = "敌方阵容超过 3×3 容量（" + entries.Count + " > "
                + EnemyBoardFormation.MaxEnemyUnits + "）。";
            return false;
        }

        var instanceIds = new HashSet<string>();
        var cells = new HashSet<string>();

        for (int i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            if (entry == null)
            {
                diagnostic = "敌方阵容第 " + i + " 项为 null。";
                return false;
            }

            if (string.IsNullOrEmpty(entry.EnemyInstanceId))
            {
                diagnostic = "敌方阵容第 " + i + " 项缺少 EnemyInstanceId（本场身份不得为空）。";
                return false;
            }

            if (!instanceIds.Add(entry.EnemyInstanceId))
            {
                diagnostic = "敌方阵容内 EnemyInstanceId 重复：" + entry.EnemyInstanceId
                    + "（同种怪多只必须实例身份唯一）。";
                return false;
            }

            MonsterDefinition definition;
            if (!EnemyRosterBattleMapping.TryResolveDefinition(monsterCatalog, entry.MonsterId, out definition))
            {
                diagnostic = "敌方阵容引用的 MonsterId 在正式 Monster 定义中不存在：" + entry.MonsterId
                    + "（InstanceId=" + entry.EnemyInstanceId + "）；不得跳过该敌人继续开战。";
                return false;
            }

            BattleRow row;
            BattleColumn column;
            if (!TryMapPosition(entry, out row, out column, out diagnostic)) return false;

            string cellKey = ((int)row) + ":" + ((int)column);
            if (!cells.Add(cellKey))
            {
                diagnostic = "敌方阵容出现同一格多个单位（排 " + row + " / 列 " + column
                    + "）；每格必须唯一占位。";
                return false;
            }

            plan.Add(new EnemyUnitPlan
            {
                EnemyInstanceId = entry.EnemyInstanceId,
                MonsterId = entry.MonsterId,
                Definition = definition,
                Row = row,
                Column = column
            });
        }

        // 排上限：每排 ≤ 3（与玩家侧 / 1B Formation 规则一致）
        for (int r = 0; r < 3; r++)
        {
            int inRow = 0;
            for (int i = 0; i < plan.Count; i++) if ((int)plan[i].Row == r) inRow++;
            if (inRow > EnemyBoardFormation.MaxUnitsPerRow)
            {
                diagnostic = "敌方阵容第 " + r + " 排超过 " + EnemyBoardFormation.MaxUnitsPerRow + " 只。";
                return false;
            }
        }

        diagnostic = string.Empty;
        return true;
    }

    private static bool TryMapPosition(GeneratedEnemyEntry entry, out BattleRow row, out BattleColumn column,
        out string diagnostic)
    {
        row = EnemyRosterBattleMapping.ToBattleRow(entry.Row);
        column = EnemyRosterBattleMapping.ToBattleColumn(entry.Column);

        if ((int)entry.Row < 0 || (int)entry.Row > 2 || (int)entry.Column < 0 || (int)entry.Column > 2)
        {
            diagnostic = "敌方阵容站位越界（row=" + entry.Row + " column=" + entry.Column
                + "；InstanceId=" + entry.EnemyInstanceId + "）。";
            return false;
        }

        diagnostic = string.Empty;
        return true;
    }

    /// <summary>敌方单位计划项（校验通过后才会被用于创建 BattleUnit）。</summary>
    public sealed class EnemyUnitPlan
    {
        public string EnemyInstanceId;
        public string MonsterId;
        public MonsterDefinition Definition;
        public BattleRow Row;
        public BattleColumn Column;
    }
}
