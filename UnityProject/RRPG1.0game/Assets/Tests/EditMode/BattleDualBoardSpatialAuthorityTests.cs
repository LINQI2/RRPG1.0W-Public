using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// WP-A 双 Board 空间 Authority 长期回归契约
/// （Battle V0.1 R5.0 / R5.1 / R5.3.1 / R5.3.3 / R6 / R15.4）。
///
/// 保护的不变量：
///   1. Player / Enemy 各自独立 3×3 Board：同 (Row, Column) 可同时存在，同 Board 内不得重叠；
///   2. Move / Swap / Adjacency / Center 占位只在「单位所属 Board」内读取，跨 Board 永不邻接 / 交换；
///   3. Range 继续使用虚拟六排战斗距离投影（R5.3.3），阻挡排规则（R5.3.1）原文不变；
///   4. 0HP 释放所属 Board 的逻辑格，新单位可进入；
///   5. 空间真相只有一套：Board 占位 + unit.Row / unit.Column（无第二套 Position Authority）。
///
/// 团结引擎限制 asmdef 引用运行时预定义程序集（Assembly-CSharp），
/// BattleSystem / BattleData / BattleField / BattleUnit / BattleRuleContextRuntime 通过 System.Reflection 访问；
/// BattleCard / BattleCardRuleProgram 等位于 BattleStage1.Runtime，可强类型引用。
/// </summary>
public class BattleDualBoardSpatialAuthorityTests
{
    private const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

    // ==================== 反射辅助 ====================

    // M2 Slice 2A：MonsterDefinition 位于 StaticConfig.Runtime（不是 Assembly-CSharp）→ 解析器加回退。
    private static Type T(string name) => Type.GetType(name + ", Assembly-CSharp")
        ?? Type.GetType("RRPG.StaticConfig." + name + ", StaticConfig.Runtime");

    private static object Create(string typeName, params object[] args) => Activator.CreateInstance(T(typeName), args);

    private static object CreateSO(string typeName) => ScriptableObject.CreateInstance(T(typeName));

    private static object EnumVal(string enumTypeName, int v) => Enum.ToObject(T(enumTypeName), v);

    private static object Row(int row) => EnumVal("BattleRow", row);

    private static object Col(int column) => EnumVal("BattleColumn", column);

    private static void Set(object o, string field, object value)
    {
        var f = o.GetType().GetField(field, F);
        Assert.IsNotNull(f, "字段不存在：" + field);
        f.SetValue(o, value);
    }

    private static object Get(object o, string field)
    {
        var f = o.GetType().GetField(field, F);
        Assert.IsNotNull(f, "字段不存在：" + field);
        return f.GetValue(o);
    }

    private static object Call(object o, string method, params object[] args)
    {
        var m = o.GetType().GetMethod(method, F);
        Assert.IsNotNull(m, "方法不存在：" + method);
        return m.Invoke(o, args);
    }

    private static IList MakeList(string elementTypeName, params object[] items)
    {
        var list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(T(elementTypeName)));
        foreach (var item in items) list.Add(item);
        return list;
    }

    private static object MakeUnit(string id, string faction, int row, int col, int range = 1, int hp = 50, int atk = 5)
    {
        // BattleUnit(unitId, sourceId, unitType, maxHP, attack, attackCount, attackRange, maxActionPoints, faction)
        int unitType = faction == "enemy" ? 2 : 0; // Monster=2, Character=0
        return Create("BattleUnit", id, id, EnumVal("UnitType", unitType), hp, atk, 1, range, 1, faction);
    }

    private static object NewBattleSystem()
    {
        var config = CreateSO("InjuryProbabilityConfig");
        var instances = MakeList("CharacterInstance");
        return Create("BattleSystem", instances, null, config);
    }

    private static object NewBattle(out object data, out IList players, out IList enemies)
    {
        var bs = NewBattleSystem();
        data = Call(bs, "GetBattleData", null);
        players = (IList)Get(data, "PlayerUnits");
        enemies = (IList)Get(data, "EnemyUnits");
        return bs;
    }

    // ==================== Board 辅助（唯一空间 Authority 读取） ====================

    private static object PlayerBoard(object data) => Get(data, "PlayerField");

    private static object EnemyBoard(object data) => Get(data, "EnemyField");

    private static object UnitAt(object board, int row, int col) => Call(board, "GetUnitAt", Row(row), Col(col));

    private static void DeployPlayer(object data, object unit, int row, int col)
    {
        Call(PlayerBoard(data), "Deploy", unit, Row(row), Col(col));
    }

    private static void DeployEnemy(object data, object unit, int row, int col)
    {
        Call(EnemyBoard(data), "Deploy", unit, Row(row), Col(col));
    }

    private static void AssertCell(object board, object unit, int row, int col, string message)
    {
        Assert.AreSame(unit, UnitAt(board, row, col), message + "：Board 占位应为该单位 [" + row + "," + col + "]");
        Assert.AreEqual(row, (int)Get(unit, "Row"), message + "：单位 Row 必须与占位一致");
        Assert.AreEqual(col, (int)Get(unit, "Column"), message + "：单位 Column 必须与占位一致");
    }

    private static void AssertFree(object board, int row, int col, string message)
    {
        Assert.IsNull(UnitAt(board, row, col), message + "：该格应已释放/为空 [" + row + "," + col + "]");
    }

    // ==================== Gate 0 规则接线辅助 ====================

    private static object NewRuleContext(object battleSystem)
    {
        // BattleRuleContextRuntime(BattleSystem, BattleV1Runtime, Func<BattleUnit,string>)：
        // 本组契约只验证空间 Fact / 空间写入接缝，不需要共享牌库（传 null 时 PersonId 回退 SourceId）。
        var ctor = T("BattleRuleContextRuntime").GetConstructors()[0];
        return ctor.Invoke(new object[] { battleSystem, null, null });
    }

    private static BattleCard CreateRuleCard(string ownerPersonId, BattleRuleOperation operation,
                                            BattleRuleManualTargetKind manualKind,
                                            BattleRuleTargetSource stepSource,
                                            BattleRuleTargetSource requirementSource)
    {
        return new BattleCard
        {
            CardId = "test_" + operation,
            TemplateId = "test_" + operation,
            DisplayName = "Test " + operation,
            CardKind = BattleCardKind.Character,
            OwnerPersonId = ownerPersonId,
            Cost = 1,
            RuleProgram = new BattleCardRuleProgram
            {
                Trigger = BattleRuleTrigger.OnPlay,
                ManualTargetKind = manualKind,
                PlayRequirement = new BattleCardPlayRequirement
                {
                    TargetSource = requirementSource,
                    Select = BattleRuleSelect.All,
                    MinCount = 1
                },
                Steps = new List<BattleCardRuleStep>
                {
                    new BattleCardRuleStep
                    {
                        Operation = operation,
                        TargetSource = stepSource,
                        Select = BattleRuleSelect.All
                    }
                }
            }
        };
    }

    // ==================== 1. 双 Board 同坐标共存 / 同 Board 不重叠 ====================

    [Test]
    public void DualBoard_SameCoordinate_CanCoexist()
    {
        object data; IList players, enemies;
        NewBattle(out data, out players, out enemies);

        var p = MakeUnit("p1", "player", 2, 1);
        players.Add(p);
        DeployPlayer(data, p, 2, 1);

        var e = MakeUnit("e1", "enemy", 2, 1);
        enemies.Add(e);
        DeployEnemy(data, e, 2, 1);

        AssertCell(PlayerBoard(data), p, 2, 1, "我方单位进入 Player Board");
        AssertCell(EnemyBoard(data), e, 2, 1, "敌方单位进入 Enemy Board");
        Assert.AreNotSame(p, e, "两侧单位必须是不同实例（同坐标不等于同占位）");
    }

    [Test]
    public void SameBoard_OverlapRejected()
    {
        object data; IList players, enemies;
        NewBattle(out data, out players, out enemies);

        var p1 = MakeUnit("p1", "player", 0, 0);
        var p2 = MakeUnit("p2", "player", 0, 2);
        players.Add(p1);
        players.Add(p2);
        DeployPlayer(data, p1, 0, 0);
        DeployPlayer(data, p2, 0, 2);

        var e = MakeUnit("e1", "enemy", 0, 2);
        enemies.Add(e);
        DeployEnemy(data, e, 0, 2);

        // 目标格在本 Board 已被占据 → Move 失败且零写入
        bool moved = (bool)Call(PlayerBoard(data), "TryMoveUnit", p1, Row(0), Col(2));
        Assert.IsFalse(moved, "同一 Board 内目标格被占用时 Move 必须失败");
        AssertCell(PlayerBoard(data), p1, 0, 0, "失败 Move 必须零写入（牌主不动）");
        AssertCell(PlayerBoard(data), p2, 0, 2, "失败 Move 不得影响占位单位");
        AssertCell(EnemyBoard(data), e, 0, 2, "失败 Move 不得影响敌方 Board");
    }

    // ==================== 2. Move 只读取所属 Board ====================

    [Test]
    public void Move_IgnoresOpposingBoardOccupancy()
    {
        object data; IList players, enemies;
        NewBattle(out data, out players, out enemies);

        var p = MakeUnit("p1", "player", 0, 0);
        players.Add(p);
        DeployPlayer(data, p, 0, 0);

        var e = MakeUnit("e1", "enemy", 0, 1);
        enemies.Add(e);
        DeployEnemy(data, e, 0, 1);

        // Enemy Board 的 (0,1) 不构成本 Board 占位
        bool moved = (bool)Call(PlayerBoard(data), "TryMoveUnit", p, Row(0), Col(1));
        Assert.IsTrue(moved, "对方 Board 同坐标不得封锁我方 Move 目标格");
        AssertCell(PlayerBoard(data), p, 0, 1, "Move 后我方占位与坐标同步");
        AssertFree(PlayerBoard(data), 0, 0, "Move 后旧格释放");
        AssertCell(EnemyBoard(data), e, 0, 1, "Move 不得影响敌方 Board 同坐标单位");
    }

    // ==================== 3. 0HP 释放所属 Board 逻辑格 ====================

    [Test]
    public void DownedUnit_ReleasesBoardCell()
    {
        object data; IList players, enemies;
        var bs = NewBattle(out data, out players, out enemies);

        var p = MakeUnit("p1", "player", 0, 1, hp: 5);
        players.Add(p);
        DeployPlayer(data, p, 0, 1);

        var e = MakeUnit("e1", "enemy", 0, 1);
        enemies.Add(e);
        DeployEnemy(data, e, 0, 1);

        Assert.IsTrue((bool)Call(bs, "ApplyBattleDamage", p, 10), "伤害应正常结算");
        Assert.IsTrue((bool)Get(p, "IsDown"), "0HP 应立即失去战斗能力");

        AssertFree(PlayerBoard(data), 0, 1, "0HP 后应释放所属 Board 逻辑格");
        AssertCell(EnemyBoard(data), e, 0, 1, "0HP 不得释放对方 Board 的同坐标格");
        Assert.AreEqual(0, (int)Get(p, "Row"), "表现层保留最后逻辑位置（不改写 Row）");
        Assert.AreEqual(1, (int)Get(p, "Column"), "表现层保留最后逻辑位置（不改写 Column）");

        // 新单位可以进入被释放的格
        var p2 = MakeUnit("p2", "player", 1, 1);
        players.Add(p2);
        DeployPlayer(data, p2, 1, 1);
        Assert.IsTrue((bool)Call(PlayerBoard(data), "TryMoveUnit", p2, Row(0), Col(1)),
            "释放后的格应允许新单位进入");
        AssertCell(PlayerBoard(data), p2, 0, 1, "新单位进入释放格后占位与坐标一致");
    }

    // ==================== 4. Swap 只在同一 Board ====================

    [Test]
    public void Swap_RequiresSameBoard()
    {
        object data; IList players, enemies;
        var bs = NewBattle(out data, out players, out enemies);
        var ctx = NewRuleContext(bs);

        var owner = MakeUnit("owner", "player", 0, 0);
        var ally = MakeUnit("ally", "player", 2, 2);
        players.Add(owner);
        players.Add(ally);
        DeployPlayer(data, owner, 0, 0);
        DeployPlayer(data, ally, 2, 2);

        var enemy = MakeUnit("e1", "enemy", 0, 0);
        enemies.Add(enemy);
        DeployEnemy(data, enemy, 0, 0);

        var swapCard = CreateRuleCard("owner", BattleRuleOperation.Swap, BattleRuleManualTargetKind.AllyUnit,
                                      BattleRuleTargetSource.ManualTarget, BattleRuleTargetSource.OtherAllyUnits);

        Assert.IsTrue((bool)Call(ctx, "TrySwapOwnerWith", swapCard, "ally"), "同 Board 我方单位应可交换");
        AssertCell(PlayerBoard(data), owner, 2, 2, "Swap 后牌主进入对方位置");
        AssertCell(PlayerBoard(data), ally, 0, 0, "Swap 后友方进入牌主位置");
        AssertCell(EnemyBoard(data), enemy, 0, 0, "Swap 不得触碰敌方 Board 同坐标");

        int ownerRow = (int)Get(owner, "Row");
        int ownerCol = (int)Get(owner, "Column");
        Assert.IsFalse((bool)Call(ctx, "TrySwapOwnerWith", swapCard, "e1"), "跨 Board 交换必须失败");
        Assert.AreEqual(ownerRow, (int)Get(owner, "Row"), "失败 Swap 零写入（牌主 Row 不变）");
        Assert.AreEqual(ownerCol, (int)Get(owner, "Column"), "失败 Swap 零写入（牌主 Column 不变）");
        AssertCell(EnemyBoard(data), enemy, 0, 0, "跨 Board 失败 Swap 不得影响敌方");
    }

    // ==================== 5. Adjacency 永不跨 Board ====================

    [Test]
    public void Adjacency_DoesNotCrossBoards()
    {
        object data; IList players, enemies;
        var bs = NewBattle(out data, out players, out enemies);

        var a = MakeUnit("a", "player", 0, 1);
        players.Add(a);
        DeployPlayer(data, a, 0, 1);

        // 敌方与我方坐标相邻（相邻排同列 / 同排相邻列），但属于另一条 Board → 永不邻接
        var eFront = MakeUnit("e1", "enemy", 1, 1);
        var eSide = MakeUnit("e2", "enemy", 0, 0);
        enemies.Add(eFront);
        enemies.Add(eSide);
        DeployEnemy(data, eFront, 1, 1);
        DeployEnemy(data, eSide, 0, 0);

        Assert.IsFalse((bool)Call(bs, "IsAdjacentAlly", a, eFront), "跨 Board 相邻排同列不得邻接");
        Assert.IsFalse((bool)Call(bs, "IsAdjacentAlly", a, eSide), "跨 Board 同排相邻列不得邻接");

        // 同 Board 同排相邻列仍然邻接（邻接规则本身不变）
        var allySide = MakeUnit("a2", "player", 0, 0);
        players.Add(allySide);
        DeployPlayer(data, allySide, 0, 0);
        Assert.IsTrue((bool)Call(bs, "IsAdjacentAlly", a, allySide), "同 Board 同排相邻列应邻接");
    }

    // ==================== 6. Center 占位只读取同一 Board ====================

    [Test]
    public void CenterBlocking_UsesSameBoardOnly()
    {
        object data; IList players, enemies;
        var bs = NewBattle(out data, out players, out enemies);

        var left = MakeUnit("l", "player", 0, 0);
        var right = MakeUnit("r", "player", 0, 2);
        players.Add(left);
        players.Add(right);
        DeployPlayer(data, left, 0, 0);
        DeployPlayer(data, right, 0, 2);

        Assert.IsTrue((bool)Call(bs, "IsAdjacentAlly", left, right), "本 Board Center 空 → 同排左右列邻接");

        // 敌方 Board 占据同一 (0, Center) → 不阻断我方
        var enemy = MakeUnit("e1", "enemy", 0, 1);
        enemies.Add(enemy);
        DeployEnemy(data, enemy, 0, 1);
        Assert.IsTrue((bool)Call(bs, "IsAdjacentAlly", left, right), "对方 Board 同坐标不得阻断我方 Center 邻接");

        // 我方 Board 占据 Center → 阻断
        var ally = MakeUnit("c", "player", 0, 1);
        players.Add(ally);
        DeployPlayer(data, ally, 0, 1);
        Assert.IsFalse((bool)Call(bs, "IsAdjacentAlly", left, right), "本 Board Center 被占据应阻断同排左右列邻接");
    }

    // ==================== 7. Range 使用虚拟六排投影（R5.3.3） ====================

    [Test]
    public void Range_UsesVirtualSixRowProjection()
    {
        object data; IList players, enemies;
        var bs = NewBattle(out data, out players, out enemies);

        var pFront = MakeUnit("pF", "player", 0, 1); // Player Front → 物理 2
        var pBack = MakeUnit("pB", "player", 2, 1);  // Player Back  → 物理 0
        players.Add(pFront);
        players.Add(pBack);
        DeployPlayer(data, pFront, 0, 1);
        DeployPlayer(data, pBack, 2, 1);

        var eFront = MakeUnit("eF", "enemy", 0, 1);  // Enemy Front → 物理 3
        var eBack = MakeUnit("eB", "enemy", 2, 1);   // Enemy Back  → 物理 5
        enemies.Add(eFront);
        enemies.Add(eBack);
        DeployEnemy(data, eFront, 0, 1);
        DeployEnemy(data, eBack, 2, 1);

        // Player Front(2) 与 Enemy Front(3) 在投影中相邻：同坐标不代表同一排
        Assert.AreEqual(0, (int)Call(bs, "GetBlockingRowCount", pFront, eFront),
            "Player Front 与 Enemy Front 之间没有中间排");
        Assert.IsTrue((bool)Call(bs, "IsInRange", pFront, eFront, 0),
            "投影相邻排 AttackRange=1 应可达");

        // Player Front(2) → Enemy Back(5)：中间排 3 = Enemy Front（非空）→ 1 阻挡排
        Assert.AreEqual(1, (int)Call(bs, "GetBlockingRowCount", pFront, eBack),
            "虚拟六排投影应把 Enemy Front 计入阻挡");
        Assert.IsFalse((bool)Call(bs, "IsInRange", pFront, eBack, 0), "1 个阻挡排不应被 AttackRange=1 命中");
        Assert.IsTrue((bool)Call(bs, "IsInRange", pFront, eBack, 1), "RangeModifier=1 应能跨 1 个阻挡排");

        // Player Back(0) → Enemy Front(3)：中间排 2 = Player Front（非空）→ 1 阻挡排
        Assert.AreEqual(1, (int)Call(bs, "GetBlockingRowCount", pBack, eFront),
            "虚拟六排投影应把 Player Front 计入阻挡");
    }

    // ==================== 8. 阻挡排 Current 规则保持（R5.3.1） ====================

    [Test]
    public void BlockingRows_CurrentRulePreserved()
    {
        object data; IList players, enemies;
        var bs = NewBattle(out data, out players, out enemies);

        var attacker = MakeUnit("pB", "player", 2, 1);   // Player Back → 物理 0（攻击者）
        var middleA = MakeUnit("pM1", "player", 1, 0);   // Player Middle → 物理 1
        var middleB = MakeUnit("pM2", "player", 1, 2);   // Player Middle → 物理 1（同排第二个单位）
        var sameRow = MakeUnit("pB2", "player", 2, 0);   // 与攻击者同排 → 两端不计
        players.Add(attacker); players.Add(middleA); players.Add(middleB); players.Add(sameRow);
        DeployPlayer(data, attacker, 2, 1);
        DeployPlayer(data, middleA, 1, 0);
        DeployPlayer(data, middleB, 1, 2);
        DeployPlayer(data, sameRow, 2, 0);

        var target = MakeUnit("eB", "enemy", 2, 1);      // Enemy Back → 物理 5（目标）
        enemies.Add(target);
        DeployEnemy(data, target, 2, 1);

        Assert.AreEqual(1, (int)Call(bs, "GetBlockingRowCount", attacker, target),
            "同排多单位只计 1 排；空排与两端不计");

        var pFront = MakeUnit("pF", "player", 0, 1);     // Player Front → 物理 2（新增非空排）
        players.Add(pFront);
        DeployPlayer(data, pFront, 0, 1);
        Assert.AreEqual(2, (int)Call(bs, "GetBlockingRowCount", attacker, target), "新增非空排应计入阻挡");

        var eMiddle = MakeUnit("eM", "enemy", 1, 1);     // Enemy Middle → 物理 4（新增非空排）
        enemies.Add(eMiddle);
        DeployEnemy(data, eMiddle, 1, 1);
        Assert.AreEqual(3, (int)Call(bs, "GetBlockingRowCount", attacker, target),
            "敌方新增非空排同样计入阻挡（敌我对称）");

        var extraSameRow = MakeUnit("pB3", "player", 2, 2); // 仍与攻击者同排
        players.Add(extraSameRow);
        DeployPlayer(data, extraSameRow, 2, 2);
        Assert.AreEqual(3, (int)Call(bs, "GetBlockingRowCount", attacker, target), "攻击者所在排不得计入阻挡");
    }

    // ==================== 9. Enemy Intent 与统一距离源未被破坏 ====================

    [Test]
    public void EnemyIntent_RangeStillValid()
    {
        object data; IList players, enemies;
        var bs = NewBattle(out data, out players, out enemies);

        var p = MakeUnit("p1", "player", 0, 1, hp: 100);      // Player Front → 物理 2
        players.Add(p);
        DeployPlayer(data, p, 0, 1);

        var e = MakeUnit("mon_001", "enemy", 2, 1, range: 1, hp: 50, atk: 5); // Enemy Back → 物理 5
        enemies.Add(e);
        DeployEnemy(data, e, 2, 1);

        Call(bs, "LockEnemyIntents", null);
        Assert.IsNotNull(Call(bs, "GetEnemyIntent", e), "敌人 Intent 仍应生成（本 WP 不改 Intent 行为）");

        Assert.AreEqual(0, (int)Call(bs, "GetBlockingRowCount", e, p), "敌方攻击路径的中间排应为空");
        Assert.IsTrue((bool)Call(bs, "IsInRange", e, p, 0), "敌方 AttackRange=1 应可达 Player Front");
        Assert.AreSame(p, Call(bs, "FindEnemyTarget", e), "敌方目标选择仍使用统一距离源");

        var pBack = MakeUnit("p2", "player", 2, 1, hp: 100);  // 我方 Back（与敌方 Back 同坐标）
        players.Add(pBack);
        DeployPlayer(data, pBack, 2, 1);
        Assert.AreSame(p, Call(bs, "FindEnemyTarget", e),
            "我方 Board 后排与敌方 Board 同坐标不影响敌方可达性判定");
    }

    // ==================== 10. 《补位》：合法空格只看 Player Board ====================

    [Test]
    public void FillGap_UsesPlayerBoardEmptyCells()
    {
        object data; IList players, enemies;
        var bs = NewBattle(out data, out players, out enemies);
        var ctx = NewRuleContext(bs);

        var owner = MakeUnit("owner", "player", 0, 0);
        var ally = MakeUnit("ally", "player", 0, 2);
        players.Add(owner);
        players.Add(ally);
        DeployPlayer(data, owner, 0, 0);
        DeployPlayer(data, ally, 0, 2);

        // 敌方占据同坐标 (0,1)：不得让 (0,1) 从「我方合法空格」中消失
        var enemy = MakeUnit("e1", "enemy", 0, 1);
        enemies.Add(enemy);
        DeployEnemy(data, enemy, 0, 1);

        var fillGap = CreateRuleCard("owner", BattleRuleOperation.Move, BattleRuleManualTargetKind.AllyCell,
                                     BattleRuleTargetSource.ManualTarget, BattleRuleTargetSource.EmptyAllyCells);
        var cellTarget = BattleRuleTargetRef.ForCell(0, 1, null);

        var legality = BattleCardRuleResolver.CheckPlayable(fillGap, (IBattleRuleContext)ctx, cellTarget);
        Assert.IsTrue(legality.IsLegal,
            "《补位》类 Move 应把手选空格 (0,1) 视为合法（对方 Board 同坐标不封锁）：" + legality.Reason);

        var resolution = BattleCardRuleExecutor.Execute(
            BattleCardRuleResolver.BuildPlan(fillGap, (IBattleRuleContext)ctx, cellTarget), (IBattleRuleContext)ctx);

        Assert.AreEqual(1, resolution.Outcomes.Count, "应产出一条 Move 结算结果");
        Assert.AreEqual(BattleRuleStepOutcomeKind.Applied, resolution.Outcomes[0].Kind, "Move 应真实结算成功");
        AssertCell(PlayerBoard(data), owner, 0, 1, "《补位》后牌主进入 Player Board 空格");
        AssertFree(PlayerBoard(data), 0, 0, "《补位》后旧格释放");
        AssertCell(EnemyBoard(data), enemy, 0, 1, "《补位》不得触碰敌方 Board 同坐标");
    }

    // ==================== 11. 《换岗》：Swap 后仍只有一套 Position Authority ====================

    [Test]
    public void SwapPost_UsesSingleSpatialAuthority()
    {
        object data; IList players, enemies;
        var bs = NewBattle(out data, out players, out enemies);
        var ctx = NewRuleContext(bs);

        var owner = MakeUnit("owner", "player", 0, 0);
        var ally = MakeUnit("ally", "player", 1, 2);
        players.Add(owner);
        players.Add(ally);
        DeployPlayer(data, owner, 0, 0);
        DeployPlayer(data, ally, 1, 2);

        var swapPost = CreateRuleCard("owner", BattleRuleOperation.Swap, BattleRuleManualTargetKind.AllyUnit,
                                      BattleRuleTargetSource.ManualTarget, BattleRuleTargetSource.OtherAllyUnits);
        var unitTarget = BattleRuleTargetRef.ForUnit(new BattleRuleUnitFacts
        {
            UnitId = "ally",
            PersonId = "ally",
            IsAlly = true,
            CurrentHP = 50,
            MaxHP = 50
        });

        var legality = BattleCardRuleResolver.CheckPlayable(swapPost, (IBattleRuleContext)ctx, unitTarget);
        Assert.IsTrue(legality.IsLegal, "《换岗》类 Swap 应有合法目标：" + legality.Reason);

        var resolution = BattleCardRuleExecutor.Execute(
            BattleCardRuleResolver.BuildPlan(swapPost, (IBattleRuleContext)ctx, unitTarget), (IBattleRuleContext)ctx);

        Assert.AreEqual(1, resolution.Outcomes.Count, "应产出一条 Swap 结算结果");
        Assert.AreEqual(BattleRuleStepOutcomeKind.Applied, resolution.Outcomes[0].Kind, "Swap 应真实结算成功");
        AssertCell(PlayerBoard(data), owner, 1, 2, "Swap 后 Board 占位与单位坐标一致（唯一 Position Authority）");
        AssertCell(PlayerBoard(data), ally, 0, 0, "Swap 后 Board 占位与单位坐标一致（唯一 Position Authority）");

        Assert.AreEqual(2, ((IList)Call(PlayerBoard(data), "GetAllUnits")).Count,
            "Player Board 占位数量应等于实际有效单位数（无第二套位置记录）");
        Assert.AreEqual(0, ((IList)Call(EnemyBoard(data), "GetAllUnits")).Count, "Enemy Board 不得被 Swap 写入");
    }

    // ==================== 12. 上阵不得覆盖同 Board 已有占位 ====================

    [Test]
    public void Deploy_OccupiedPlayerBoardCellRejected()
    {
        var cardA = CreateSO("CharacterCardData");
        Set(cardA, "CardId", "ccard_a");
        Set(cardA, "CharacterId", "char_a");
        var cardB = CreateSO("CharacterCardData");
        Set(cardB, "CardId", "ccard_b");
        Set(cardB, "CharacterId", "char_b");

        var dataA = CreateSO("CharacterData");
        Set(dataA, "CharacterId", "char_a");
        Set(dataA, "DisplayName", "甲");
        Set(dataA, "BaseHP", 30);
        Set(dataA, "BaseAttack", 5);
        Set(dataA, "AttackRange", 1);
        Set(dataA, "AttackCount", 1);
        Set(dataA, "ActionPoints", 1);
        Set(dataA, "Faction", "player");

        var dataB = CreateSO("CharacterData");
        Set(dataB, "CharacterId", "char_b");
        Set(dataB, "DisplayName", "乙");
        Set(dataB, "BaseHP", 30);
        Set(dataB, "BaseAttack", 5);
        Set(dataB, "AttackRange", 1);
        Set(dataB, "AttackCount", 1);
        Set(dataB, "ActionPoints", 1);
        Set(dataB, "Faction", "player");

        var bs = NewBattleSystem();
        Call(bs, "InitializeBattle", MakeList("CharacterCardData", cardA, cardB),
            MakeList("MonsterDefinition"), MakeList("SkillData"), MakeList("SkillData"));

        var ciA = Create("CharacterInstance", "char_a", "甲", 30, 5);
        var ciB = Create("CharacterInstance", "char_b", "乙", 30, 5);

        Assert.IsTrue((bool)Call(bs, "DeployCharacter", cardA, Row(0), Col(1), ciA, dataA, MakeList("SkillData")),
            "首个上阵应成功");
        Assert.IsFalse((bool)Call(bs, "DeployCharacter", cardB, Row(0), Col(1), ciB, dataB, MakeList("SkillData")),
            "同一 Board 已被占据的格应拒绝上阵（失败零写入）");

        var battleData = Call(bs, "GetBattleData", null);
        Assert.AreEqual(1, ((IList)Get(battleData, "PlayerUnits")).Count, "被拒绝的上阵不得进入本场单位列表");
        Assert.AreEqual(1, ((IList)Call(PlayerBoard(battleData), "GetAllUnits")).Count,
            "Player Board 占位数量应与成功上阵数量一致");
    }
}
