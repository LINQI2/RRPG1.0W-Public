using System.Collections.Generic;

/// <summary>
/// 战斗全局数据
///
/// 负责：存储当前战斗的状态数据
/// 不负责：战斗逻辑计算、技能释放
///
/// BattlePhase 负责当前阶段
/// IsBattleOver 负责结束判定
/// Battle V0.1：不包含 TP / 独立道具牌堆；玩家资源只有共享行动力（SharedActionPointPool）
/// </summary>
public class BattleData
{
    /// <summary>
    /// 当前回合数
    /// </summary>
    public int Turn;

    /// <summary>
    /// 当前战斗阶段
    /// </summary>
    public BattlePhase CurrentPhase;

    /// <summary>
    /// 战斗是否结束
    /// </summary>
    public bool IsBattleOver;

    /// <summary>
    /// 玩家单位列表
    /// </summary>
    public List<BattleUnit> PlayerUnits;

    /// <summary>
    /// 敌方单位列表
    /// </summary>
    public List<BattleUnit> EnemyUnits;

    /// <summary>
    /// 我方逻辑 Board（Player Board，3 排 × 3 列，9 个逻辑位置）
    /// Battle V0.1 R5.0 / R5.1：Player 与 Enemy 各自拥有独立 3×3 Board（共享空间语言，不共享 Occupancy）；
    /// 一个 BattleField 实例只承载一个阵营的占位，本类不新建第二套 Position 真相源。
    /// </summary>
    public BattleField PlayerField;

    /// <summary>
    /// 敌方逻辑 Board（Enemy Board，3 排 × 3 列，9 个逻辑位置）
    /// 与 PlayerField 完全独立：双方可同时占据各自 Board 的相同 (Row, Column)，互不构成占位。
    /// </summary>
    public BattleField EnemyField;

    /// <summary>
    /// 手牌
    /// </summary>
    public Hand PlayerHand;

    /// <summary>
    /// 创建战斗数据
    /// </summary>
    public BattleData()
    {
        Turn = 0;
        CurrentPhase = BattlePhase.Preparation;
        IsBattleOver = false;
        PlayerUnits = new List<BattleUnit>();
        EnemyUnits = new List<BattleUnit>();
        PlayerField = new BattleField();
        EnemyField = new BattleField();
        PlayerHand = new Hand();
    }

    /// <summary>
    /// 单位所属 Board —— 双 Board 下 Occupancy / Adjacency / Range 的唯一判定入口。
    /// 判定顺序：本场单位列表（PlayerUnits → Player Board / EnemyUnits → Enemy Board），
    /// 再回退读取既有 Board 占位；两者都是既有事实，不建立第二套 Position 真相源。
    /// 不属于任何 Board（未上阵）返回 null。
    /// </summary>
    public BattleField GetBoardOf(BattleUnit unit)
    {
        if (unit == null) return null;
        if (PlayerUnits.Contains(unit)) return PlayerField;
        if (EnemyUnits.Contains(unit)) return EnemyField;
        if (PlayerField.Contains(unit)) return PlayerField;
        if (EnemyField.Contains(unit)) return EnemyField;
        return null;
    }
}