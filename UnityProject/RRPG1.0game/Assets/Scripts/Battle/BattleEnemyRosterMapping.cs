using System.Collections.Generic;
using RRPG.Encounter;

/// <summary>
/// **EnemyRoster → Battle 侧映射**（WP-Encounter-1C §9 / §11）。
///
/// 唯一 Authority：Encounter 域的 <see cref="EnemyRosterRow"/> / <see cref="EnemyRosterColumn"/>
/// 与 Battle 域的 <see cref="BattleRow"/> / <see cref="BattleColumn"/> 逐值一致（都按 0/1/2 冻结），
/// 但语义归属不同域 —— 因此只在**这一处**做转换，Prep 与 Battle 都必须走本类，
/// 不允许任何调用方自己 `(BattleRow)(int)` 就地转换（否则等于第二套映射）。
///
/// 边界：本类只做「值语义转换」与「MonsterId → MonsterDefinition」查表；
/// 不重新计算站位（站位由 1B Generator 求解并冻结）、不复制怪物数值。
/// </summary>
public static class EnemyRosterBattleMapping
{
    /// <summary>Encounter 排 → Battle 排（1:1；值域一致，集中在此以防漂移）。</summary>
    public static BattleRow ToBattleRow(EnemyRosterRow row)
    {
        switch (row)
        {
            case EnemyRosterRow.Front: return BattleRow.Front;
            case EnemyRosterRow.Middle: return BattleRow.Middle;
            default: return BattleRow.Back;
        }
    }

    /// <summary>Encounter 列 → Battle 列（1:1；值域一致，集中在此以防漂移）。</summary>
    public static BattleColumn ToBattleColumn(EnemyRosterColumn column)
    {
        switch (column)
        {
            case EnemyRosterColumn.Left: return BattleColumn.Left;
            case EnemyRosterColumn.Center: return BattleColumn.Center;
            default: return BattleColumn.Right;
        }
    }

    /// <summary>
    /// 按 MonsterId 在**当前静态配置目录**中解析 MonsterDefinition。
    ///
    /// 为什么必须有目录参数：Battle 侧只拿 EnemyRoster（模板身份的引用）与正式 Monster 目录；
    /// 解析不到一律**明确失败**（调用方不得静默跳过该敌人 —— 那会造成「Prep 5 只、开战 4 只」）。
    /// </summary>
    public static bool TryResolveDefinition(IReadOnlyList<RRPG.StaticConfig.MonsterDefinition> catalog,
        string monsterId, out RRPG.StaticConfig.MonsterDefinition definition)
    {
        definition = null;
        if (catalog == null || string.IsNullOrEmpty(monsterId)) return false;

        for (int i = 0; i < catalog.Count; i++)
        {
            var candidate = catalog[i];
            if (candidate != null && candidate.MonsterId == monsterId)
            {
                definition = candidate;
                return true;
            }
        }

        return false;
    }
}
