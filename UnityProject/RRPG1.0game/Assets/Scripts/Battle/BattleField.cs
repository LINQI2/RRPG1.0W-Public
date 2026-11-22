using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 战场 Board —— 单一阵营的 3 排 × 3 列逻辑空间（9 个逻辑位置）
///
/// 负责：一个阵营 Board 内的空间占位管理（Deploy / Remove / 占位查询 / 原子 Move / 原子 Swap）
/// 不负责：战斗流程控制、技能判定、伤害计算
///
/// Battle V0.1 R5.0 / R5.1（双 Board 空间 Authority）：
///   - Player 与 Enemy 各自拥有独立 Board（BattleData.PlayerField / BattleData.EnemyField），
///     共享 BattleRow / BattleColumn 空间语言，但不共享 Occupancy；
///   - 一个 BattleField 实例只承载一个阵营的占位，因此双方可同时存在相同 (Row, Column)；
///   - 不建立第二套 Position 真相源：位置真相 = 本条 Board 的占位 + unit.Row / unit.Column。
///
/// 战场结构：
///   FrontRow:  [L][M][R]
///   MiddleRow: [L][M][R]
///   BackRow:   [L][M][R]
///
/// 视觉表现不影响逻辑判定
/// </summary>
public class BattleField
{
    // 三排三列存储
    private Dictionary<BattleRow, Dictionary<BattleColumn, BattleUnit>> _grid;

    /// <summary>
    /// 初始化战场
    /// 创建 3x3 空网格
    /// </summary>
    public BattleField()
    {
        _grid = new Dictionary<BattleRow, Dictionary<BattleColumn, BattleUnit>>();

        foreach (BattleRow row in System.Enum.GetValues(typeof(BattleRow)))
        {
            _grid[row] = new Dictionary<BattleColumn, BattleUnit>();
            foreach (BattleColumn col in System.Enum.GetValues(typeof(BattleColumn)))
            {
                _grid[row][col] = null;
            }
        }
    }

    /// <summary>
    /// 上阵单位到指定位置
    /// 同一排可容纳多个单位（通过列区分），不检查占位
    /// </summary>
    public void Deploy(BattleUnit unit, BattleRow row, BattleColumn col)
    {
        _grid[row][col] = unit;
        unit.Row = row;
        unit.Column = col;
    }

    /// <summary>
    /// 从战场移除单位
    /// </summary>
    public void Remove(BattleUnit unit)
    {
        foreach (var row in _grid)
        {
            foreach (var col in row.Value)
            {
                if (col.Value == unit)
                {
                    _grid[row.Key][col.Key] = null;
                    return;
                }
            }
        }
    }

    /// <summary>
    /// 根据位置获取单位
    /// </summary>
    public BattleUnit GetUnitAt(BattleRow row, BattleColumn col)
    {
        return _grid[row][col];
    }

    /// <summary>
    /// 获取指定排的所有单位
    /// </summary>
    public List<BattleUnit> GetUnitsInRow(BattleRow row)
    {
        return _grid[row].Values.Where(u => u != null).ToList();
    }

    /// <summary>
    /// 指定位置是否被指定阵营的存活单位占据
    /// （Stage 3A：邻接占位只看同阵营，避免敌我共享网格导致对方单位污染同阵营邻接判断）
    /// </summary>
    public bool HasAliveUnitInFaction(BattleRow row, BattleColumn col, string faction)
    {
        var unit = _grid[row][col];
        return unit != null && !unit.IsDown && unit.CurrentHP > 0 && unit.Faction == faction;
    }

    /// <summary>
    /// 获取所有存活单位
    /// </summary>
    public List<BattleUnit> GetAllUnits()
    {
        var units = new List<BattleUnit>();
        foreach (var row in _grid)
        {
            foreach (var col in row.Value)
            {
                if (col.Value != null)
                    units.Add(col.Value);
            }
        }
        return units;
    }

    /// <summary>
    /// 该单位当前是否占据本条 Board 的逻辑格（只读；用于判定单位所属 Board，不新建 Position 真相源）
    /// </summary>
    public bool Contains(BattleUnit unit)
    {
        if (unit == null) return false;
        return GetPosition(unit).Row != null;
    }

    /// <summary>
    /// 目标格是否可写入新单位（R5.x：同一 Board 内两个仍具战斗能力的单位不得占同一格）。
    /// 空格、或仅由已失去战斗能力的单位占位时视为可写入；对方 Board 同坐标与本条 Board 无关。
    /// </summary>
    public bool CanDeploy(BattleRow row, BattleColumn col)
    {
        var occupant = _grid[row][col];
        return occupant == null || occupant.IsDown || occupant.CurrentHP <= 0;
    }

    /// <summary>
    /// 指定位置是否被占用（本条 Board 内；对方 Board 同坐标不构成本 Board 占位）
    /// </summary>
    public bool IsOccupied(BattleRow row, BattleColumn col)
    {
        return _grid[row][col] != null;
    }

    /// <summary>
    /// 获取单位的当前位置
    /// </summary>
    /// <returns>(row, col)，未找到时返回 (null, null)</returns>
    public (BattleRow? Row, BattleColumn? Col) GetPosition(BattleUnit unit)
    {
        foreach (var row in _grid)
        {
            foreach (var col in row.Value)
            {
                if (col.Value == unit)
                    return (row.Key, col.Key);
            }
        }
        return (null, null);
    }

    /// <summary>
    /// 获取指定位置的相邻位置列表
    /// 同排相邻列 + 相邻排对应列
    /// </summary>
    public List<(BattleRow Row, BattleColumn Col)> GetAdjacentPositions(BattleRow row, BattleColumn col)
    {
        var result = new List<(BattleRow, BattleColumn)>();

        // 同排左邻
        if (col == BattleColumn.Center || col == BattleColumn.Right)
            result.Add((row, col - 1));

        // 同排右邻
        if (col == BattleColumn.Left || col == BattleColumn.Center)
            result.Add((row, col + 1));

        // 前列相邻排
        int rowIndex = (int)row;
        if (rowIndex > 0)
            result.Add(((BattleRow)(rowIndex - 1), col));
        if (rowIndex < 2)
            result.Add(((BattleRow)(rowIndex + 1), col));

        return result;
    }

    /// <summary>
    /// 清空战场
    /// </summary>
    public void Clear()
    {
        foreach (var row in _grid)
        {
            foreach (var col in row.Value.Keys.ToList())
            {
                _grid[row.Key][col] = null;
            }
        }
    }

    /// <summary>
    /// 战中原子移动：把单位移动到目标格（Battle V0.1 Gate 0《补位》）。
    ///
    /// 唯一安全空间写入口：先整轮校验，再一次性写入（失败零部分写入）；
    /// 成功后 Grid 占位与 unit.Row / unit.Column 同时更新，Range / Adjacency 立即读取新位置。
    /// 不建立第二套空间真相源。
    /// </summary>
    /// <param name="unit">要移动的单位（必须当前在场上）</param>
    /// <param name="row">目标排</param>
    /// <param name="col">目标列</param>
    /// <returns>true=移动成功；false=非法（零写入）</returns>
    public bool TryMoveUnit(BattleUnit unit, BattleRow row, BattleColumn col)
    {
        if (unit == null) return false;
        if (unit.IsDown || unit.CurrentHP <= 0) return false;

        var position = GetPosition(unit);
        if (position.Row == null || position.Col == null) return false;

        // 目标格必须为空（含敌我任一方占位），避免两个单位占同一位置
        if (_grid[row][col] != null) return false;

        // 原子写入：清旧格 → 写新格 + 同步单位自身坐标
        _grid[position.Row.Value][position.Col.Value] = null;
        _grid[row][col] = unit;
        unit.Row = row;
        unit.Column = col;
        return true;
    }

    /// <summary>
    /// 战中原子交换：交换两个单位的位置（Battle V0.1 Gate 0《换岗》）。
    ///
    /// 先整轮校验（两者都在场、不是同一单位、均仍具备战斗能力），再一次性写入；
    /// 失败零部分写入；成功后双方 unit.Row / unit.Column 与 Grid 一致。
    /// </summary>
    /// <param name="first">第一个单位</param>
    /// <param name="second">第二个单位</param>
    /// <returns>true=交换成功；false=非法（零写入）</returns>
    public bool TrySwapUnits(BattleUnit first, BattleUnit second)
    {
        if (first == null || second == null || first == second) return false;
        if (first.IsDown || first.CurrentHP <= 0) return false;
        if (second.IsDown || second.CurrentHP <= 0) return false;

        var firstPosition = GetPosition(first);
        var secondPosition = GetPosition(second);
        if (firstPosition.Row == null || firstPosition.Col == null) return false;
        if (secondPosition.Row == null || secondPosition.Col == null) return false;

        BattleRow firstRow = firstPosition.Row.Value;
        BattleColumn firstCol = firstPosition.Col.Value;
        BattleRow secondRow = secondPosition.Row.Value;
        BattleColumn secondCol = secondPosition.Col.Value;

        // 原子写入：两个格子互相交换，并同步双方自身坐标
        _grid[firstRow][firstCol] = second;
        _grid[secondRow][secondCol] = first;
        first.Row = secondRow;
        first.Column = secondCol;
        second.Row = firstRow;
        second.Column = firstCol;
        return true;
    }
}