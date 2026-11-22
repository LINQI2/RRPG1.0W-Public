using System.Collections.Generic;

/// <summary>
/// Gate 0 规则执行上下文在正式 Battle 宿主中的实现（Assembly-CSharp）。
///
/// 负责：
///   - 把 BattleData（含 Player Board / Enemy Board）/ SharedDeck 的当前事实投影为中立的只读视图
///     （BattleRuleUnitFacts / BattleRuleCellFacts），每次读取即时投影，不缓存第二套真相；
///   - 把规则层写入请求转发到既有 Battle Authority
///     （BattleSystem.ApplyBattleDamage / ApplyBattleHeal / ApplyBattleShield、
///      BattleField.TryMoveUnit / TrySwapUnits（按单位所属 Board）、BattleCard 的 Current Deck Cycle 修正）。
///
/// 不负责：
///   - 玩家规则判断（BattleCardRuleResolver / BattleCardRuleExecutor）；
///   - UI 表现（BattleUI 在结算后刷新战场与手牌）。
/// </summary>
public sealed class BattleRuleContextRuntime : IBattleRuleContext
{
    private readonly BattleSystem _battleSystem;
    private readonly BattleV1Runtime _v1Runtime;
    private readonly System.Func<BattleUnit, string> _personIdOfUnit;

    /// <summary>创建规则执行上下文。</summary>
    /// <param name="battleSystem">本场 Battle 宿主（Battle Fact 与写入 Authority）</param>
    /// <param name="v1Runtime">共享牌库 / 共享行动力运行时</param>
    /// <param name="personIdOfUnit">BattleUnit → 真实 PersonId 的映射（由 BattleUI 提供，不制造临时 Person）</param>
    public BattleRuleContextRuntime(BattleSystem battleSystem, BattleV1Runtime v1Runtime,
                                    System.Func<BattleUnit, string> personIdOfUnit)
    {
        _battleSystem = battleSystem;
        _v1Runtime = v1Runtime;
        _personIdOfUnit = personIdOfUnit;
    }

    /// <summary>当前共享行动力（唯一共享资源池）</summary>
    public int SharedActionPointsCurrent
    {
        get
        {
            if (_v1Runtime == null || _v1Runtime.ActionPoints == null) return 0;
            return _v1Runtime.ActionPoints.Current;
        }
    }

    /// <summary>全部我方单位事实快照</summary>
    public IReadOnlyList<BattleRuleUnitFacts> GetAllies()
    {
        return ProjectUnits(GetPlayerUnits(), true);
    }

    /// <summary>全部敌方单位事实快照（含 Intent 是否为防御）</summary>
    public IReadOnlyList<BattleRuleUnitFacts> GetEnemies()
    {
        return ProjectUnits(GetEnemyUnits(), false);
    }

    /// <summary>
    /// 我方 Board 格子事实快照（Player Board 3 排 × 3 列，只含我方单位占位）。
    /// 占位读取既有 Player Board（BattleData.PlayerField，唯一空间 Authority）；
    /// 敌方 Board 同坐标不构成本 Board 占位（Battle V0.1 R5.0 / R5.1）。
    /// </summary>
    public IReadOnlyList<BattleRuleCellFacts> GetAlliedCells()
    {
        var cells = new List<BattleRuleCellFacts>();
        var board = GetPlayerBoard();
        if (board == null) return cells;

        for (int row = 0; row < 3; row++)
        {
            for (int col = 0; col < 3; col++)
            {
                var unit = board.GetUnitAt((BattleRow)row, (BattleColumn)col);
                cells.Add(new BattleRuleCellFacts
                {
                    Row = row,
                    Column = col,
                    Occupied = unit != null,
                    OccupantPersonId = unit != null ? PersonIdOf(unit) : null
                });
            }
        }

        return cells;
    }

    /// <summary>当前共享手牌快照（按手牌从左到右顺序，用于确定性的 Selector 并列规则）</summary>
    public IReadOnlyList<BattleCard> GetHandCards()
    {
        if (_v1Runtime == null || _v1Runtime.Deck == null) return new List<BattleCard>();
        return new List<BattleCard>(_v1Runtime.Deck.Hand);
    }

    /// <summary>按本场单位 ID 查询单位事实（含敌我）</summary>
    public bool TryGetUnit(string unitId, out BattleRuleUnitFacts unit)
    {
        unit = null;
        if (string.IsNullOrEmpty(unitId)) return false;

        var found = FindUnit(unitId);
        if (found == null) return false;

        unit = ProjectUnit(found, IsPlayerUnit(found));
        return true;
    }

    /// <summary>解析牌主单位：角色牌归属 PersonId 对应的本场我方单位（英雄同理）</summary>
    public bool TryGetOwnerUnitId(BattleCard card, out string unitId)
    {
        unitId = null;
        if (card == null || string.IsNullOrEmpty(card.OwnerPersonId)) return false;

        foreach (var unit in GetPlayerUnits())
        {
            if (unit == null) continue;
            if (PersonIdOf(unit) == card.OwnerPersonId)
            {
                unitId = unit.UnitId;
                return true;
            }
        }

        return false;
    }

    /// <summary>某单位当前邻接的我方单位（复用既有动态邻接规则源）</summary>
    public IReadOnlyList<BattleRuleUnitFacts> GetAdjacentAllies(string unitId)
    {
        var result = new List<BattleRuleUnitFacts>();
        if (_battleSystem == null || string.IsNullOrEmpty(unitId)) return result;

        var origin = FindUnit(unitId);
        if (origin == null) return result;

        foreach (var ally in GetPlayerUnits())
        {
            if (ally == null || ally == origin) continue;
            if (ally.IsDown || ally.CurrentHP <= 0) continue;
            if (_battleSystem.IsAdjacentAlly(origin, ally))
                result.Add(ProjectUnit(ally, true));
        }

        return result;
    }

    /// <summary>牌主单位是否为独狼（同一排没有其他仍具战斗能力的我方单位）</summary>
    public bool OwnerIsAloneInRow(BattleCard card)
    {
        if (card == null) return false;

        string ownerUnitId;
        if (!TryGetOwnerUnitId(card, out ownerUnitId)) return false;

        var owner = FindUnit(ownerUnitId);
        if (owner == null) return false;

        foreach (var ally in GetPlayerUnits())
        {
            if (ally == null || ally == owner) continue;
            if (ally.IsDown || ally.CurrentHP <= 0) continue;
            if (ally.Row == owner.Row) return false;
        }

        return true;
    }

    /// <summary>造成伤害（转发到既有伤害结算 Authority）</summary>
    public bool ApplyDamage(string unitId, int amount)
    {
        var target = FindUnit(unitId);
        if (target == null) return false;
        return _battleSystem.ApplyBattleDamage(target, amount);
    }

    /// <summary>恢复生命（转发到既有治疗结算 Authority）</summary>
    public bool ApplyHeal(string unitId, int amount)
    {
        var target = FindUnit(unitId);
        if (target == null) return false;
        return _battleSystem.ApplyBattleHeal(target, amount);
    }

    /// <summary>赋予本场临时护盾（Battle-only runtime state）</summary>
    public bool ApplyShield(string unitId, int amount)
    {
        var target = FindUnit(unitId);
        if (target == null) return false;
        return _battleSystem.ApplyBattleShield(target, amount);
    }

    /// <summary>从共享牌库抽牌</summary>
    public bool DrawCards(int count)
    {
        if (_v1Runtime == null || _v1Runtime.Deck == null || count <= 0) return false;
        return _v1Runtime.Deck.Draw(count).Count > 0;
    }

    /// <summary>给一张手牌追加 Actual Cost 修正（Additive，只写运行时实例）</summary>
    public bool ApplyCostModifier(BattleCard card, int delta)
    {
        if (card == null || delta == 0) return false;
        card.AddCostModifier(delta);
        return true;
    }

    /// <summary>给一张手牌追加 Actual Damage 修正（Additive，只写运行时实例）</summary>
    public bool ApplyDamageModifier(BattleCard card, int delta)
    {
        if (card == null || delta == 0) return false;
        card.AddDamageModifier(delta);
        return true;
    }

    /// <summary>原子移动牌主单位到我方合法空格（唯一空间写入口，失败零写入）</summary>
    public bool TryMoveOwnerTo(BattleCard card, int row, int column)
    {
        if (row < 0 || row > 2 || column < 0 || column > 2) return false;

        string ownerUnitId;
        if (!TryGetOwnerUnitId(card, out ownerUnitId)) return false;

        var owner = FindUnit(ownerUnitId);
        if (owner == null) return false;

        // 双 Board：Move 只写入牌主单位所属 Board（Player Board），空格判定只读取该 Board
        var board = GetBoardOf(owner);
        if (board == null) return false;

        return board.TryMoveUnit(owner, (BattleRow)row, (BattleColumn)column);
    }

    /// <summary>原子交换牌主单位与另一名我方单位的位置（失败零写入）</summary>
    public bool TrySwapOwnerWith(BattleCard card, string otherUnitId)
    {
        string ownerUnitId;
        if (!TryGetOwnerUnitId(card, out ownerUnitId)) return false;
        if (string.IsNullOrEmpty(otherUnitId) || otherUnitId == ownerUnitId) return false;

        var owner = FindUnit(ownerUnitId);
        var other = FindAllyUnit(otherUnitId);
        if (owner == null || other == null) return false;

        // 双 Board：Swap 只允许在同一 Board 内交换（跨 Board 永不交换，失败零写入）
        var ownerBoard = GetBoardOf(owner);
        if (ownerBoard == null || ownerBoard != GetBoardOf(other)) return false;

        return ownerBoard.TrySwapUnits(owner, other);
    }

    // ==================== 内部：Battle 事实投影 ====================

    private List<BattleUnit> GetPlayerUnits()
    {
        var data = _battleSystem != null ? _battleSystem.GetBattleData() : null;
        return data != null && data.PlayerUnits != null ? data.PlayerUnits : new List<BattleUnit>();
    }

    private List<BattleUnit> GetEnemyUnits()
    {
        var data = _battleSystem != null ? _battleSystem.GetBattleData() : null;
        return data != null && data.EnemyUnits != null ? data.EnemyUnits : new List<BattleUnit>();
    }

    /// <summary>我方 Board（Player Board，双 Board 空间 Authority 的玩家侧）</summary>
    private BattleField GetPlayerBoard()
    {
        var data = _battleSystem != null ? _battleSystem.GetBattleData() : null;
        return data != null ? data.PlayerField : null;
    }

    /// <summary>单位所属 Board（转发 BattleData.GetBoardOf 的唯一判定入口）</summary>
    private BattleField GetBoardOf(BattleUnit unit)
    {
        var data = _battleSystem != null ? _battleSystem.GetBattleData() : null;
        return data != null ? data.GetBoardOf(unit) : null;
    }

    private bool IsPlayerUnit(BattleUnit unit)
    {
        return unit != null && GetPlayerUnits().Contains(unit);
    }

    private BattleUnit FindUnit(string unitId)
    {
        if (string.IsNullOrEmpty(unitId)) return null;

        foreach (var unit in GetPlayerUnits())
        {
            if (unit != null && unit.UnitId == unitId) return unit;
        }
        foreach (var unit in GetEnemyUnits())
        {
            if (unit != null && unit.UnitId == unitId) return unit;
        }
        return null;
    }

    private BattleUnit FindAllyUnit(string unitId)
    {
        foreach (var unit in GetPlayerUnits())
        {
            if (unit != null && unit.UnitId == unitId) return unit;
        }
        return null;
    }

    private string PersonIdOf(BattleUnit unit)
    {
        if (unit == null) return null;
        return _personIdOfUnit != null ? _personIdOfUnit(unit) : unit.SourceId;
    }

    private List<BattleRuleUnitFacts> ProjectUnits(List<BattleUnit> units, bool isAlly)
    {
        var result = new List<BattleRuleUnitFacts>();
        if (units == null) return result;

        foreach (var unit in units)
        {
            if (unit == null) continue;
            result.Add(ProjectUnit(unit, isAlly));
        }

        return result;
    }

    private BattleRuleUnitFacts ProjectUnit(BattleUnit unit, bool isAlly)
    {
        var facts = new BattleRuleUnitFacts
        {
            UnitId = unit.UnitId,
            PersonId = PersonIdOf(unit),
            IsAlly = isAlly,
            CurrentHP = unit.CurrentHP,
            MaxHP = unit.MaxHP,
            Attack = unit.Attack,
            Row = (int)unit.Row,
            Column = (int)unit.Column,
            Shield = unit.Shield,
            IsDown = unit.IsDown
        };

        if (!isAlly && _battleSystem != null)
        {
            var intent = _battleSystem.GetEnemyIntent(unit);
            facts.IntentIsDefend = intent != null && intent.ActionType == EnemyActionType.Defend;
        }

        return facts;
    }
}
