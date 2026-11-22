using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Battle V0.1 Stage 5（Milestone C）EditMode 逻辑测试
///
/// 覆盖（Stage 5 范围）：
///   1. 决策框架（EnemyActionPlanner，BattleStage1.Runtime 强类型）：
///      Condition 过滤 / Priority 优先 / Weight 加权；
///   2. Action 与 Target 解耦：锁定只含 ActionType+TargetRule，不暴露具体目标；
///   3. Intent 显示与已生成 Action 一致；玩家行动期间 Action 不被重新随机；
///   4. EnemyTurn 执行时 TargetRule 根据当前战况选择目标（目标不锁定）；
///   5. Condition / Priority 生效（刺客型条件特殊 / 防御型低血转防御）；
///   6. 敌人仍遵守 AttackRange / 阻挡排（复用统一距离源）；
///   7. 敌人失能后不再生成/执行 Intent；
///   8. 全部敌人失能 → 胜利；全部我方失能 → 失败。
///
/// 团结引擎限制 asmdef 引用运行时预定义程序集（Assembly-CSharp），
/// BattleSystem 等类型通过 System.Reflection 访问；
/// EnemyIntent / EnemyActionPlanner / EnemyBehaviorDemoConfig 位于 BattleStage1.Runtime，可强类型引用。
/// </summary>
public class BattleStage5EnemyIntentTests
{
    private const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

    // ---------- 反射辅助 ----------

    private static Type T(string name) => Type.GetType(name + ", Assembly-CSharp");

    private static object Create(string typeName, params object[] args) => Activator.CreateInstance(T(typeName), args);

    private static object CreateSO(string typeName) => ScriptableObject.CreateInstance(T(typeName));

    private static object EnumVal(string enumTypeName, int v) => Enum.ToObject(T(enumTypeName), v);

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

    /// <summary>我方 Board（Player Field）：双 Board 空间 Authority 的玩家侧读取入口</summary>
    private static object PlayerBoard(object data) => Get(data, "PlayerField");

    /// <summary>把单位部署到其所属 Board（敌方 → Enemy Field，我方 → Player Field；每条 Board 独立 Occupancy）</summary>

    private static void Deploy(object data, object unit, int row, int col)
    {
        bool isEnemy = (int)Get(unit, "UnitType") == 2; // Monster = 2
        object board = Get(data, isEnemy ? "EnemyField" : "PlayerField");
        Call(board, "Deploy", unit, EnumVal("BattleRow", row), EnumVal("BattleColumn", col));
    }

    private static object NewBattleSystem()
    {
        var config = CreateSO("InjuryProbabilityConfig");
        var instances = MakeList("CharacterInstance");
        return Create("BattleSystem", instances, null, config);
    }

    // ---------- 1. 决策框架（BattleStage1.Runtime 强类型） ----------

    [Test]
    public void Planner_ConditionFiltersOutNonMatching()
    {
        var rules = new List<EnemyActionRule>
        {
            new EnemyActionRule(EnemyActionType.Attack, EnemyConditionId.None, 0, 100),
            new EnemyActionRule(EnemyActionType.Defend, EnemyConditionId.SelfLowHp, 1, 100)
        };

        // SelfLowHp 不满足 → 只剩 Attack
        var intent = EnemyActionPlanner.Pick(rules, r => r.Condition == EnemyConditionId.None, null, new System.Random(42));
        Assert.IsNotNull(intent, "条件满足的规则应至少可选一条");
        Assert.AreEqual(EnemyActionType.Attack, intent.ActionType, "不满足条件的规则不应被选");

        // SelfLowHp 满足 → Defend（Priority 1）优先
        var intent2 = EnemyActionPlanner.Pick(rules, r => true, null, new System.Random(42));
        Assert.AreEqual(EnemyActionType.Defend, intent2.ActionType, "满足条件时高优先级规则应被选中");
    }

    [Test]
    public void Planner_HighestPriorityWins()
    {
        var rules = new List<EnemyActionRule>
        {
            new EnemyActionRule(EnemyActionType.Attack, EnemyConditionId.None, 0, 100),
            new EnemyActionRule(EnemyActionType.Special, EnemyConditionId.None, 2, 1),
            new EnemyActionRule(EnemyActionType.Defend, EnemyConditionId.None, 1, 100)
        };

        var intent = EnemyActionPlanner.Pick(rules, r => true, null, new System.Random(7));
        Assert.AreEqual(EnemyActionType.Special, intent.ActionType, "同满足时最高 Priority 应优先（与权重无关）");
    }

    [Test]
    public void Planner_WeightDistributesWithinSamePriority()
    {
        var rules = new List<EnemyActionRule>
        {
            new EnemyActionRule(EnemyActionType.Attack, EnemyConditionId.None, 0, 90),
            new EnemyActionRule(EnemyActionType.Defend, EnemyConditionId.None, 0, 10)
        };

        int attack = 0, defend = 0;
        for (int i = 0; i < 2000; i++)
        {
            var intent = EnemyActionPlanner.Pick(rules, r => true, null, new System.Random(i));
            if (intent.ActionType == EnemyActionType.Attack) attack++;
            else defend++;
        }
        Assert.Greater(attack, defend * 3, "同 Priority 时权重更高的行动应被更频繁选中（90/10 分布）");
    }

    [Test]
    public void Planner_OncePerBattleExcludesUsedRule()
    {
        var used = new HashSet<EnemyActionRule>();
        var rule = new EnemyActionRule(EnemyActionType.Special, EnemyConditionId.None, 1, 100, oncePerBattle: true);
        var rules = new List<EnemyActionRule>
        {
            rule,
            new EnemyActionRule(EnemyActionType.Attack, EnemyConditionId.None, 0, 100)
        };

        var first = EnemyActionPlanner.Pick(rules, r => true, r => !(r.OncePerBattle && used.Contains(r)), new System.Random(1));
        Assert.AreEqual(EnemyActionType.Special, first.ActionType, "首次可选用 OncePerBattle 规则");

        used.Add(first.SourceRule);
        var second = EnemyActionPlanner.Pick(rules, r => true, r => !(r.OncePerBattle && used.Contains(r)), new System.Random(1));
        Assert.AreEqual(EnemyActionType.Attack, second.ActionType, "OncePerBattle 规则用过后不再可选");
    }

    // ---------- 2. Action 与 Target 解耦 ----------

    [Test]
    public void Intent_DoesNotExposeConcreteTarget()
    {
        var battleUnitType = T("BattleUnit");
        var fields = typeof(EnemyIntent).GetFields();
        foreach (var f in fields)
        {
            Assert.IsFalse(f.FieldType == battleUnitType, "Intent 不得包含具体单位引用：" + f.Name);
            Assert.IsFalse(f.Name.Contains("TargetId") || f.Name.Contains("UnitId"),
                "Intent 不得包含目标ID字段：" + f.Name);
        }
        // Intent 至少公开粗粒度 ActionType
        Assert.IsNotNull(typeof(EnemyIntent).GetField("ActionType"), "Intent 必须公开 ActionType");
    }

    // ---------- 3. 锁定 / 失能 / 玩家行动期间不重随机 ----------

    [Test]
    public void LockEnemyIntents_LocksAliveEnemies_SkipsDowned()
    {
        var bs = NewBattleSystem();
        var data = Call(bs, "GetBattleData", null);
        var players = (IList)Get(data, "PlayerUnits");
        var enemies = (IList)Get(data, "EnemyUnits");

        var p = MakeUnit("p1", "player", 0, 1, hp: 100);
        players.Add(p);
        Deploy(data, p, 0, 1);

        var eAlive = MakeUnit("mon_001", "enemy", 2, 1, hp: 50, atk: 5);
        var eDown = MakeUnit("mon_002", "enemy", 2, 2, hp: 15, atk: 5);
        enemies.Add(eAlive);
        enemies.Add(eDown);
        Deploy(data, eAlive, 2, 1);
        Deploy(data, eDown, 2, 2);

        Call(bs, "LockEnemyIntents", null);
        Assert.AreEqual(2, (int)Call(bs, "GetLockedEnemyIntentCount", null), "两个存活敌人都应锁定");

        Set(eDown, "IsDown", true);
        Set(eDown, "CurrentHP", 0);
        Call(bs, "LockEnemyIntents", null);
        Assert.AreEqual(1, (int)Call(bs, "GetLockedEnemyIntentCount", null), "失能敌人不应再锁定 Intent");
        Assert.IsNull((EnemyIntent)Call(bs, "GetEnemyIntent", eDown), "失能敌人的 Intent 应为空");
        Assert.IsNotNull((EnemyIntent)Call(bs, "GetEnemyIntent", eAlive), "存活敌人仍应保留 Intent");
    }

    [Test]
    public void PlayerActionPeriod_DoesNotReRollAction_AndLockIsConfirming()
    {
        var bs = NewBattleSystem();
        var data = Call(bs, "GetBattleData", null);
        var players = (IList)Get(data, "PlayerUnits");
        var enemies = (IList)Get(data, "EnemyUnits");

        var p = MakeUnit("p1", "player", 0, 1, hp: 100);
        players.Add(p);
        Deploy(data, p, 0, 1);

        var e = MakeUnit("mon_001", "enemy", 2, 1, hp: 50, atk: 5);
        enemies.Add(e);
        Deploy(data, e, 2, 1);

        Call(bs, "LockEnemyIntents", null);
        var before = (EnemyIntent)Call(bs, "GetEnemyIntent", e);
        Assert.IsNotNull(before);

        // 玩家行动期间（造成伤害/治疗）不得触碰已锁定 Action
        Call(bs, "ApplyBattleDamage", e, 5);
        Call(bs, "ApplyBattleHeal", p, 5);
        Assert.AreSame(before, (EnemyIntent)Call(bs, "GetEnemyIntent", e), "玩家行动期间 Action 不应被重新随机");

        // 再次 LockEnemyIntents（下一玩家回合确认语义）：已锁定 Action 保持不变
        Call(bs, "LockEnemyIntents", null);
        Assert.AreSame(before, (EnemyIntent)Call(bs, "GetEnemyIntent", e), "Lock 确认语义不应重随机已锁定 Action");
    }

    // ---------- 4. EnemyTurn 执行：Target 不锁定，按 TargetRule 在当前战况选目标 ----------

    [Test]
    public void EnemyTurn_SpecialLowestHp_HitsCurrentLowestHpTarget()
    {
        var bs = NewBattleSystem();
        var data = Call(bs, "GetBattleData", null);
        var players = (IList)Get(data, "PlayerUnits");
        var enemies = (IList)Get(data, "EnemyUnits");

        // 中排满血（物理前排优先） + 前排低血（当前生命最低，10/100=10% ≤ 40% 阈值）
        var pHigh = MakeUnit("p1", "player", 1, 1, hp: 100, atk: 1);
        var pLow = MakeUnit("p2", "player", 0, 1, hp: 100, atk: 1);
        Set(pLow, "CurrentHP", 10);
        players.Add(pHigh);
        players.Add(pLow);
        Deploy(data, pHigh, 1, 1);
        Deploy(data, pLow, 0, 1);

        // 刺客（mon_003_wolf）：射程 2 保证两个目标都在范围内
        var e = MakeUnit("mon_003_wolf", "enemy", 2, 1, range: 2, hp: 100, atk: 5);
        enemies.Add(e);
        Deploy(data, e, 2, 1);

        Call(bs, "LockEnemyIntents", null);
        var intent = (EnemyIntent)Call(bs, "GetEnemyIntent", e);
        Assert.AreEqual(EnemyActionType.Special, intent.ActionType, "存在低血目标时刺客应锁定特殊");
        Assert.AreEqual(EnemyTargetRule.LowestHp, intent.TargetRule, "刺客应锁定最低生命目标规则");

        int hpLowBefore = (int)Get(pLow, "CurrentHP");
        int hpHighBefore = (int)Get(pHigh, "CurrentHP");
        Call(bs, "ExecuteEnemyPhase", null);

        Assert.Less((int)Get(pLow, "CurrentHP"), hpLowBefore, "特殊应命中当前最低生命目标");
        Assert.AreEqual(hpHighBefore, (int)Get(pHigh, "CurrentHP"), "高血目标不应被命中");
    }

    [Test]
    public void EnemyTurn_ActionLocked_ButTargetRePickedOnChangedSituation()
    {
        var bs = NewBattleSystem();
        var data = Call(bs, "GetBattleData", null);
        var players = (IList)Get(data, "PlayerUnits");
        var enemies = (IList)Get(data, "EnemyUnits");

        var pHigh = MakeUnit("p1", "player", 1, 1, hp: 100, atk: 1);
        var pLow = MakeUnit("p2", "player", 0, 1, hp: 100, atk: 1);
        Set(pLow, "CurrentHP", 10); // 10% ≤ 40% 阈值，触发刺客条件
        players.Add(pHigh);
        players.Add(pLow);
        Deploy(data, pHigh, 1, 1);
        Deploy(data, pLow, 0, 1);

        var e = MakeUnit("mon_003_wolf", "enemy", 2, 1, range: 2, hp: 100, atk: 5);
        enemies.Add(e);
        Deploy(data, e, 2, 1);

        Call(bs, "LockEnemyIntents", null);
        Assert.AreEqual(EnemyActionType.Special, ((EnemyIntent)Call(bs, "GetEnemyIntent", e)).ActionType);

        // 玩家行动：治疗低血目标回满（改变战况）→ 执行时 TargetRule 重新选当前最低生命目标
        Set(pLow, "CurrentHP", 100);
        int hpHighBefore = (int)Get(pHigh, "CurrentHP");
        Call(bs, "ExecuteEnemyPhase", null);

        Assert.Less((int)Get(pHigh, "CurrentHP"), hpHighBefore,
            "Action 锁定但 Target 不锁定：战况改变后执行时重新选择目标");
    }

    // ---------- 5. 防御行为（Condition + Priority 表达） ----------

    [Test]
    public void Defend_LowHpUsesDefendAction_AndReducesNextHit()
    {
        Set(NewBattleSystem(), "EnemySelfLowHpRatio", 0.5f); // 仅确认字段可配置

        var bs = NewBattleSystem();
        Set(bs, "EnemySelfLowHpRatio", 0.5f);
        Set(bs, "EnemyDefendDamageRatio", 0.5f);

        var data = Call(bs, "GetBattleData", null);
        var players = (IList)Get(data, "PlayerUnits");
        var enemies = (IList)Get(data, "EnemyUnits");

        var p = MakeUnit("p1", "player", 0, 1, hp: 100, atk: 1);
        players.Add(p);
        Deploy(data, p, 0, 1);

        var e = MakeUnit("mon_demo_defender", "enemy", 2, 1, hp: 100, atk: 5);
        enemies.Add(e);
        Deploy(data, e, 2, 1);

        // 满血：SelfLowHp 不满足 → 普通攻击
        Call(bs, "LockEnemyIntents", null);
        Assert.AreEqual(EnemyActionType.Attack, ((EnemyIntent)Call(bs, "GetEnemyIntent", e)).ActionType, "满血防御型敌人应普通攻击");

        // 低血（20% ≤ 50%）：SelfLowHp 满足 → 防御（Priority 1 优先于 Attack）
        Set(e, "CurrentHP", 20);
        Call(bs, "ConsumeEnemyIntents", null);
        Call(bs, "LockEnemyIntents", null);
        Assert.AreEqual(EnemyActionType.Defend, ((EnemyIntent)Call(bs, "GetEnemyIntent", e)).ActionType, "低血防御型敌人应转防御");

        // 执行防御回合：不攻击 + 进入防御状态
        int hpBefore = (int)Get(p, "CurrentHP");
        Call(bs, "ExecuteEnemyPhase", null);
        Assert.AreEqual(hpBefore, (int)Get(p, "CurrentHP"), "防御回合不应攻击");
        Assert.IsTrue((bool)Call(bs, "IsEnemyDefending", e), "防御行动应设置防御状态");

        // 玩家攻击防御中的敌人：伤害减半（一次性；敌人当前 HP=20，10→5）
        Call(bs, "ApplyBattleDamage", e, 10);
        Assert.AreEqual(20 - 5, (int)Get(e, "CurrentHP"), "防御减伤应生效（10→5）");
        Assert.IsFalse((bool)Call(bs, "IsEnemyDefending", e), "受击后防御状态应移除");
    }

    // ---------- 6. 攻击距离 / 阻挡排（复用 Milestone B 统一距离源） ----------

    [Test]
    public void EnemyAttack_StillRespectsAttackRangeAndBlockingRow()
    {
        var bs = NewBattleSystem();
        var data = Call(bs, "GetBattleData", null);
        var players = (IList)Get(data, "PlayerUnits");
        var enemies = (IList)Get(data, "EnemyUnits");

        var pFront = MakeUnit("p1", "player", 0, 1, hp: 100, atk: 1);
        var pBack = MakeUnit("p2", "player", 2, 1, hp: 100, atk: 1);
        players.Add(pFront);
        players.Add(pBack);
        Deploy(data, pFront, 0, 1);
        Deploy(data, pBack, 2, 1);

        var e = MakeUnit("mon_001", "enemy", 2, 1, range: 1, hp: 100, atk: 5); // 射程 1
        enemies.Add(e);
        Deploy(data, e, 2, 1);

        Call(bs, "LockEnemyIntents", null);
        int hpBackBefore = (int)Get(pBack, "CurrentHP");
        int hpFrontBefore = (int)Get(pFront, "CurrentHP");
        Call(bs, "ExecuteEnemyPhase", null);

        Assert.AreEqual(hpBackBefore, (int)Get(pBack, "CurrentHP"), "被阻挡排掩护的后排不应被攻击");
        Assert.Less((int)Get(pFront, "CurrentHP"), hpFrontBefore, "射程内前排应被攻击（阻挡排规则仍生效）");
    }

    // ---------- 7. 失能后不再生成/执行 Intent ----------

    [Test]
    public void DownedEnemy_DoesNotExecuteIntent()
    {
        var bs = NewBattleSystem();
        var data = Call(bs, "GetBattleData", null);
        var players = (IList)Get(data, "PlayerUnits");
        var enemies = (IList)Get(data, "EnemyUnits");

        var p = MakeUnit("p1", "player", 0, 1, hp: 100, atk: 1);
        players.Add(p);
        Deploy(data, p, 0, 1);

        var e = MakeUnit("mon_001", "enemy", 2, 1, hp: 50, atk: 5);
        enemies.Add(e);
        Deploy(data, e, 2, 1);

        Call(bs, "LockEnemyIntents", null);
        Assert.IsNotNull((EnemyIntent)Call(bs, "GetEnemyIntent", e));

        // 玩家行动击杀该敌人 → 失能
        Call(bs, "ApplyBattleDamage", e, 50);
        Assert.IsTrue((bool)Get(e, "IsDown"), "敌人应已失能");

        // 下一玩家回合 Lock：失能敌人不生成 Intent；EnemyTurn 不执行
        Call(bs, "LockEnemyIntents", null);
        Assert.IsNull((EnemyIntent)Call(bs, "GetEnemyIntent", e), "失能敌人不应有 Intent");

        int hpBefore = (int)Get(p, "CurrentHP");
        Call(bs, "ExecuteEnemyPhase", null);
        Assert.AreEqual(hpBefore, (int)Get(p, "CurrentHP"), "失能敌人不应执行任何行动");
    }

    // ---------- 8. 胜负：全部敌人失能 → 胜利；全部我方失能 → 失败 ----------

    [Test]
    public void AllEnemiesDown_Victory_AndSingleDownKeepsBattle()
    {
        var bs = NewBattleSystem();
        var data = Call(bs, "GetBattleData", null);
        var players = (IList)Get(data, "PlayerUnits");
        var enemies = (IList)Get(data, "EnemyUnits");

        var p = MakeUnit("p1", "player", 0, 1, hp: 100, atk: 10);
        players.Add(p);
        Deploy(data, p, 0, 1);

        var e1 = MakeUnit("mon_001", "enemy", 2, 1, hp: 20, atk: 1);
        var e2 = MakeUnit("mon_002", "enemy", 2, 2, hp: 20, atk: 1);
        enemies.Add(e1);
        enemies.Add(e2);
        Deploy(data, e1, 2, 1);
        Deploy(data, e2, 2, 2);

        Call(bs, "ApplyBattleDamage", e1, 20);
        Assert.IsFalse((bool)Get(data, "IsBattleOver"), "单一敌人失能不应结束战斗");

        Call(bs, "ApplyBattleDamage", e2, 20);
        Assert.IsTrue((bool)Get(data, "IsBattleOver"), "全部敌人失能 → 战斗胜利");
    }

    [Test]
    public void AllPlayersDown_Defeat()
    {
        var bs = NewBattleSystem();
        var data = Call(bs, "GetBattleData", null);
        var players = (IList)Get(data, "PlayerUnits");
        var enemies = (IList)Get(data, "EnemyUnits");

        var p = MakeUnit("p1", "player", 0, 1, hp: 20, atk: 1);
        players.Add(p);
        Deploy(data, p, 0, 1);

        var e = MakeUnit("mon_001", "enemy", 2, 1, hp: 100, atk: 10);
        enemies.Add(e);
        Deploy(data, e, 2, 1);

        Call(bs, "ApplyBattleDamage", p, 20);
        Assert.IsTrue((bool)Get(data, "IsBattleOver"), "全部我方失能 → 战斗失败");
    }

    // ---------- 9. 未配置怪物回退普通攻击 ----------

    [Test]
    public void FallbackAttack_WhenNoCustomRules()
    {
        var bs = NewBattleSystem();
        var data = Call(bs, "GetBattleData", null);
        var players = (IList)Get(data, "PlayerUnits");
        var enemies = (IList)Get(data, "EnemyUnits");

        var p = MakeUnit("p1", "player", 0, 1, hp: 100);
        players.Add(p);
        Deploy(data, p, 0, 1);

        var e = MakeUnit("mon_unknown", "enemy", 2, 1, hp: 50, atk: 5);
        enemies.Add(e);
        Deploy(data, e, 2, 1);

        Call(bs, "LockEnemyIntents", null);
        var intent = (EnemyIntent)Call(bs, "GetEnemyIntent", e);
        Assert.AreEqual(EnemyActionType.Attack, intent.ActionType, "未配置行为规则的敌人应回退普通攻击");
        Assert.AreEqual(EnemyTargetRule.Frontmost, intent.TargetRule, "回退普通攻击应为前排优先");
    }
}
