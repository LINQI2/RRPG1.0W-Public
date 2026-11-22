using System.Collections.Generic;

namespace RRPG.Encounter
{
    /// <summary>
    /// 敌方 Board 的**纯逻辑排 / 列规则**（WP-Encounter-1B §13 / §14 / §15）。
    ///
    /// 与玩家侧 <c>BattleFormationPlan</c> 的关系（§21）：
    ///   - 排内列规则是**同一条已冻结规则**：1 人 → Center；2 人 → Left + Right；3 人 → Left + Center + Right；
    ///   - 但 `BattleFormationPlan` 位于 Assembly-CSharp（且 `CanPlace` / `ValidateSnapshot` 写死了
    ///     玩家 6 人上限），本程序集无法直接引用，也**不应**为敌方 9 只去改玩家规则；
    ///   - 因此这里按同一规则实现敌方侧最小 helper，并用交叉测试
    ///     （EncounterGeneratorFormationContractTests）逐值比对 `BattleFormationPlan.ColumnIndexForSlot`，
    ///     保证两侧**不会静默分叉**。
    ///
    /// 边界：只回答「一排 N 人时哪些列合法」；不做 EnemyRoster 组装、不做占用分配。
    /// </summary>
    public static class EnemyBoardFormation
    {
        /// <summary>敌方 Board 的排数（Front / Middle / Back）。</summary>
        public const int RowCount = 3;

        /// <summary>一排的列数（不存在第 4 列）。</summary>
        public const int MaxUnitsPerRow = 3;

        /// <summary>敌方战场物理容量：3 排 × 3 列 = 9（Battle GameSpec R5.0 / R6.3）。</summary>
        public const int MaxEnemyUnits = 9;

        /// <summary>该排实际使用的列数（1 / 2 / 3；不存在第 4 列）。</summary>
        public static int ColumnCount(int unitsInRow)
        {
            if (unitsInRow <= 1) return 1;
            if (unitsInRow == 2) return 2;
            return MaxUnitsPerRow;
        }

        /// <summary>
        /// 排内槽位 → 列索引（0=Left / 1=Center / 2=Right）；
        /// 1 人 → Center；2 人 → Left / Right；3 人 → Left / Center / Right。
        /// </summary>
        public static int ColumnIndexForSlot(int slotIndex, int unitsInRow)
        {
            int slots = ColumnCount(unitsInRow);
            int slot = slotIndex < 0 ? 0 : (slotIndex >= slots ? slots - 1 : slotIndex);
            if (slots <= 1) return 1;
            if (slots == 2) return slot == 0 ? 0 : 2;
            return slot;
        }

        /// <summary>一排 N 人时的**合法列集合**（升序）；N = 0（空排）返回空集合。</summary>
        public static IReadOnlyList<EnemyRosterColumn> RequiredColumns(int unitsInRow)
        {
            if (unitsInRow <= 0) return new List<EnemyRosterColumn>();

            int slots = ColumnCount(unitsInRow);
            var columns = new List<EnemyRosterColumn>(slots);
            for (int i = 0; i < slots; i++)
            {
                columns.Add((EnemyRosterColumn)ColumnIndexForSlot(i, unitsInRow));
            }
            return columns;
        }

        /// <summary>该排在最终 N 人时，指定列是否合法。</summary>
        public static bool IsLegalColumn(int unitsInRow, EnemyRosterColumn column)
        {
            if (unitsInRow <= 0 || unitsInRow > MaxUnitsPerRow) return false;

            var required = RequiredColumns(unitsInRow);
            for (int i = 0; i < required.Count; i++)
            {
                if (required[i] == column) return true;
            }
            return false;
        }

        /// <summary>该排人数是否合法（0..3；0 人表示空排）。</summary>
        public static bool IsLegalRowCount(int unitsInRow)
        {
            return unitsInRow >= 0 && unitsInRow <= MaxUnitsPerRow;
        }
    }
}
