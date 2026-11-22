/// <summary>
/// Battle Formation 合法性规则（纯函数：无状态、无 UnityEngine 依赖，便于独立回归）。
///
/// 与 WP-D Formation Presentation 的分工：
///   - <see cref="BattleFormationVisualLayout"/>：只回答「这一排怎么摆」（视觉槽位与偏移，只影响画面）；
///   - 本类：只回答「这个战前阵型是否合法」（成员总数 / 每排容量 / 同排列唯一）。
///
/// Current GameSpec：
///   - R4.1：最多同时上阵 6 个我方单位（英雄占正常位置）；
///   - R5.0 / R5.1：Player / Enemy 各自独立 3×3 Board，一排只有 Left / Center / Right 三列。
///
/// 不负责：BattleUnit.Row / Column 写入与 Board Occupancy（仍由 BattleData / BattleField 决定），
/// 也不建立第二套 Position Authority。
/// </summary>
public static class BattleFormationPlan
{
    /// <summary>Current GameSpec R4.1：最多同时上阵 6 个我方单位（英雄占正常位置）。</summary>
    public const int MaxTeamUnits = 6;

    /// <summary>Current GameSpec R5.0：一排只有 3 个逻辑列（不存在第 4 Column）。</summary>
    public const int MaxUnitsPerRow = 3;

    /// <summary>我方 Board 的排数（Front / Middle / Back）。</summary>
    public const int RowCount = 3;

    /// <summary>该排实际可用的逻辑槽位数（1 / 2 / 3；不存在第 4 槽）。</summary>
    public static int SlotCount(int unitsInRow)
    {
        if (unitsInRow <= 1) return 1;
        if (unitsInRow == 2) return 2;
        return MaxUnitsPerRow;
    }

    /// <summary>
    /// 排内槽位 → 逻辑列索引（0=Left / 1=Center / 2=Right），与 WP-D Formation Presentation 顺序一致：
    ///   1 人 → Center；2 人 → Left / Right（不使用 Center）；3 人 → Left / Center / Right。
    /// 越界槽位被收敛到合法范围（不会产生第 4 列）。
    /// </summary>
    public static int ColumnIndexForSlot(int slotIndex, int unitsInRow)
    {
        int slots = SlotCount(unitsInRow);
        int slot = slotIndex < 0 ? 0 : (slotIndex >= slots ? slots - 1 : slotIndex);
        if (slots <= 1) return 1;
        if (slots == 2) return slot == 0 ? 0 : 2;
        return slot;
    }

    /// <summary>
    /// 能否把一个成员放到目标排。
    /// </summary>
    /// <param name="totalMembers">当前阵型成员总数（不含尚未上阵的候选）</param>
    /// <param name="memberAlreadyPlaced">该成员是否已在阵型内（已在 = 移动，不增加总数）</param>
    /// <param name="unitsInTargetRowExcludingMember">目标排当前成员数（该成员自己在目标排时不计入）</param>
    /// <returns>true=允许；false=拒绝（调用方必须零写入）</returns>
    public static bool CanPlace(int totalMembers, bool memberAlreadyPlaced, int unitsInTargetRowExcludingMember)
    {
        if (unitsInTargetRowExcludingMember >= MaxUnitsPerRow) return false;
        int total = totalMembers + (memberAlreadyPlaced ? 0 : 1);
        return total <= MaxTeamUnits;
    }

    /// <summary>
    /// 全量校验一个阵型快照。
    /// </summary>
    /// <param name="memberRowIndex">每个成员的排索引（0=Front / 1=Middle / 2=Back）</param>
    /// <param name="memberColumnIndex">每个成员的列索引（0=Left / 1=Center / 2=Right）</param>
    /// <returns>null=合法；否则返回给玩家看的中文原因</returns>
    public static string ValidateSnapshot(int[] memberRowIndex, int[] memberColumnIndex)
    {
        if (memberRowIndex == null || memberColumnIndex == null) return "阵型数据缺失";
        if (memberRowIndex.Length != memberColumnIndex.Length) return "阵型数据不一致（排 / 列数量不同）";

        int count = memberRowIndex.Length;
        if (count == 0) return "请至少上阵一名角色或英雄";
        if (count > MaxTeamUnits) return "最多上阵 " + MaxTeamUnits + " 名成员（英雄占用正常上阵位置）";

        var rowCounts = new int[RowCount];
        var usedColumns = new System.Collections.Generic.HashSet<int>[RowCount];
        for (int r = 0; r < RowCount; r++) usedColumns[r] = new System.Collections.Generic.HashSet<int>();

        for (int i = 0; i < count; i++)
        {
            int r = memberRowIndex[i];
            int c = memberColumnIndex[i];
            if (r < 0 || r >= RowCount) return "成员位置越界（第 " + (i + 1) + " 名成员的排无效）";
            if (c < 0 || c >= MaxUnitsPerRow) return "成员位置越界（第 " + (i + 1) + " 名成员的列无效）";

            rowCounts[r]++;
            if (!usedColumns[r].Add(c)) return "同一排出现重复列（每排同一列最多 1 名成员）";
        }

        for (int r = 0; r < RowCount; r++)
            if (rowCounts[r] > MaxUnitsPerRow) return "每排最多 " + MaxUnitsPerRow + " 名成员";

        return null;
    }
}
