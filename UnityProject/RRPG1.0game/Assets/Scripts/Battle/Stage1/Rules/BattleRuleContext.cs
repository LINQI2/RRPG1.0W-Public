using System.Collections.Generic;

/// <summary>
/// Battle V0.1 Gate 0 规则执行所需的最小 Battle Fact 视图 + 写入接缝。
///
/// 负责：
///   - 以只读快照形式向规则层暴露当前战场事实（我方 / 敌方单位、我方格子、共享手牌）；
///   - 以显式方法暴露规则层需要的写入操作，并由宿主实现转发到既有 Battle Authority。
///
/// 不负责：
///   - 存储或持有第二套战斗状态（实现类是只读视图 + 转发器）；
///   - 玩家规则判断以外的表现（UI 由 BattleUI 负责）。
///
/// 说明：本程序集（BattleStage1.Runtime）不引用 Assembly-CSharp，
/// 因此规则层只依赖这些中立结构与接口，具体 BattleUnit / BattleField 映射由宿主实现。
/// </summary>
public class BattleRuleUnitFacts
{
    /// <summary>本场单位实例 ID（BattleUnit.UnitId）</summary>
    public string UnitId;

    /// <summary>单位对应人物的真实 PersonId（用于归属判定与日志）</summary>
    public string PersonId;

    /// <summary>是否为我方单位</summary>
    public bool IsAlly;

    /// <summary>当前生命</summary>
    public int CurrentHP;

    /// <summary>最大生命</summary>
    public int MaxHP;

    /// <summary>当前攻击力</summary>
    public int Attack;

    /// <summary>当前排（0 起，按战场物理顺序）</summary>
    public int Row;

    /// <summary>当前列（0=左 / 1=中 / 2=右）</summary>
    public int Column;

    /// <summary>本场临时护盾</summary>
    public int Shield;

    /// <summary>Authority 的实际失能事实，仅供结果快照读取。</summary>
    public bool IsDown;

    /// <summary>敌方单位当前 Intent 是否为防御（我方单位恒为 false）</summary>
    public bool IntentIsDefend;

    /// <summary>是否仍具备战斗能力（未失能且 HP &gt; 0）</summary>
    public bool IsCombatCapable
    {
        get { return !string.IsNullOrEmpty(UnitId) && CurrentHP > 0; }
    }
}

/// <summary>我方战场格子事实（用于 Move 合法空格判定）。</summary>
public class BattleRuleCellFacts
{
    /// <summary>排索引（0 起）</summary>
    public int Row;

    /// <summary>列索引（0=左 / 1=中 / 2=右）</summary>
    public int Column;

    /// <summary>该格是否已被任一单位占据</summary>
    public bool Occupied;

    /// <summary>占据者的 PersonId（未占据时为空）</summary>
    public string OccupantPersonId;
}

/// <summary>
/// 规则层与真实 Battle 之间的唯一接缝。
/// 读取 = 当前 Battle Fact 的只读视图；写入 = 转发到既有 Authority（BattleSystem / BattleField / SharedDeck）。
/// </summary>
public interface IBattleRuleContext
{
    /// <summary>当前共享行动力（共享牌库唯一资源池）</summary>
    int SharedActionPointsCurrent { get; }

    /// <summary>全部我方单位快照</summary>
    IReadOnlyList<BattleRuleUnitFacts> GetAllies();

    /// <summary>全部敌方单位快照</summary>
    IReadOnlyList<BattleRuleUnitFacts> GetEnemies();

    /// <summary>全部我方战场格子快照（含空格与占据格）</summary>
    IReadOnlyList<BattleRuleCellFacts> GetAlliedCells();

    /// <summary>当前共享手牌快照（按手牌从左到右顺序）</summary>
    IReadOnlyList<BattleCard> GetHandCards();

    /// <summary>按本场单位 ID 查询单位事实</summary>
    bool TryGetUnit(string unitId, out BattleRuleUnitFacts unit);

    /// <summary>解析牌主单位（角色牌归属 Person 对应的本场单位）</summary>
    bool TryGetOwnerUnitId(BattleCard card, out string unitId);

    /// <summary>某单位当前邻接的我方单位（使用既有动态邻接规则源）</summary>
    IReadOnlyList<BattleRuleUnitFacts> GetAdjacentAllies(string unitId);

    /// <summary>牌主单位是否为独狼（所在排无其他仍具战斗能力的我方单位）</summary>
    bool OwnerIsAloneInRow(BattleCard card);

    /// <summary>造成伤害（走既有伤害 Authority；返回 false = 该操作对本目标 Not Applicable）</summary>
    bool ApplyDamage(string unitId, int amount);

    /// <summary>恢复生命（走既有治疗 Authority）</summary>
    bool ApplyHeal(string unitId, int amount);

    /// <summary>赋予本场临时护盾（Battle-only runtime state）</summary>
    bool ApplyShield(string unitId, int amount);

    /// <summary>从共享牌库抽牌</summary>
    bool DrawCards(int count);

    /// <summary>给一张手牌追加 Actual Cost 修正（Additive）</summary>
    bool ApplyCostModifier(BattleCard card, int delta);

    /// <summary>给一张手牌追加 Actual Damage 修正（Additive；目标无 Damage 时返回 false = Not Applicable）</summary>
    bool ApplyDamageModifier(BattleCard card, int delta);

    /// <summary>原子移动牌主单位到指定我方格子（非法时零写入）</summary>
    bool TryMoveOwnerTo(BattleCard card, int row, int column);

    /// <summary>原子交换牌主单位与另一名我方单位的位置</summary>
    bool TrySwapOwnerWith(BattleCard card, string otherUnitId);
}
