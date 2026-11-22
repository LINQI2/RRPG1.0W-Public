using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Battle V0.1 Stage 3A EditMode 逻辑测试
///
/// 覆盖（Stage 3A 范围）：
///   1. 邻接统一接口（SkillSystem.IsAdjacentAlly）——同阵营占位判断，敌方占 Center 不阻断；
///   2. 统一攻击距离规则源（BattleSystem.GetBlockingRowCount / IsInRange）——非空阻挡排 + RangeModifier；
///   3. 旧玩家普通攻击（PlayerAttack）与敌方 AI（FindEnemyTarget）复用同一距离源；
///   4. V1 玩家回合开始 TurnStart 技能触发迁移（TriggerTurnStartSkillsForPlayer）——村长鼓舞对邻接友军 +2，
///      改变布阵后邻接结果动态变化；
///   5. BattleCard.FromEntry 正确拷贝 RangeModifier（强类型，BattleStage1.Runtime）。
///
/// 团结引擎限制 asmdef 引用运行时预定义程序集（Assembly-CSharp），
/// 因此 BattleSystem / SkillSystem / BattleField / BattleUnit 等类型通过 System.Reflection 访问；
/// BattleCard / BattleCardData / BattlePersonCardEntry 位于 BattleStage1.Runtime，可强类型引用。
/// </summary>
public class BattleStage3ASpaceTests
{
    private const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

    // ---------- 反射辅助 ----------

    // M2 Slice 2A：MonsterDefinition 位于 StaticConfig.Runtime（不是 Assembly-CSharp）→ 解析器加回退。
    private static Type T(string name) => Type.GetType(name + ", Assembly-CSharp")
        ?? Type.GetType("RRPG.StaticConfig." + name + ", StaticConfig.Runtime");

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
        // 敌方必须为 Monster，否则统一距离公式会把它按玩家侧物理索引计算
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

    private static object NewSkillSystem(object data)
    {
        // SkillSystem(BattleData, StatusSystem = null)：Activator 不支持默认参数，需显式传 null
        return Create("SkillSystem", data, null);
    }

    // ---------- 1. 邻接统一接口：同阵营占位 ----------

    [Test]
    public void IsAdjacentAlly_OnlySameFactionOccupiesCenter()
    {
        var data = Create("BattleData");
        var skillSys = NewSkillSystem(data);

        var a = MakeUnit("a", "player", 0, 0); // 前排 Left
        var b = MakeUnit("b", "player", 0, 2); // 前排 Right
        Deploy(data, a, 0, 0);
        Deploy(data, b, 0, 2);

        // Center 空：同排 L↔R 邻接
        Assert.IsTrue((bool)Call(skillSys, "IsAdjacentAlly", a, b), "Center 空时同排左右列应邻接");

        // 敌方占据同一 (Row, Column)：位于 Enemy Board，不构成本 Board 占位（R5.0 / R5.1）
        var enemy = MakeUnit("e", "enemy", 0, 1);
        Deploy(data, enemy, 0, 1);
        Assert.IsTrue((bool)Call(skillSys, "IsAdjacentAlly", a, b), "敌方占 Center 不应阻断我方同排左右邻接");

        // 我方占 Center：阻断
        var ally = MakeUnit("c", "player", 0, 1);
        Deploy(data, ally, 0, 1);
        Assert.IsFalse((bool)Call(skillSys, "IsAdjacentAlly", a, b), "我方占 Center 应阻断同排左右邻接");
    }

    [Test]
    public void IsAdjacentAlly_AdjacentRowCenterRules()
    {
        var data = Create("BattleData");
        var skillSys = NewSkillSystem(data);

        var front = MakeUnit("f", "player", 0, 1);    // 前排 Center
        var middle = MakeUnit("m", "player", 1, 1);   // 中排 Center
        var back = MakeUnit("bk", "player", 2, 1);    // 后排 Center
        var backRight = MakeUnit("br", "player", 2, 2); // 后排 Right
        Deploy(data, front, 0, 1);
        Deploy(data, middle, 1, 1);
        Deploy(data, back, 2, 1);
        Deploy(data, backRight, 2, 2);

        // 相邻排同列（Front↔Middle，排差 1）：邻接
        Assert.IsTrue((bool)Call(skillSys, "IsAdjacentAlly", front, middle), "相邻排同列应邻接");

        // 跨排 colDiff=1：middle(中排Center) vs backRight(后排Right)，
        // backRight 所在排(后排) Center 有 back → 不邻接
        Assert.IsFalse((bool)Call(skillSys, "IsAdjacentAlly", middle, backRight), "目标排 Center 被占用时跨排 Left/Right↔Center 不应邻接");

        // 移走后排 Center（back）→ 后排 Center 空 → middle(中排Center) 与 backRight(后排Right) 邻接
        Call(PlayerBoard(data), "Remove", back);
        Assert.IsTrue((bool)Call(skillSys, "IsAdjacentAlly", middle, backRight), "目标排 Center 空时跨排 Center↔Left/Right 应邻接");
    }


    // ---------- 2. 统一攻击距离规则源 ----------

    [Test]
    public void IsInRange_UsesBlockingRows_AndRangeModifier()
    {
        var bs = NewBattleSystem();
        var data = Call(bs, "GetBattleData", null);
        var playerUnits = (IList)Get(data, "PlayerUnits");
        var enemyUnits = (IList)Get(data, "EnemyUnits");

        var pFront = MakeUnit("p1", "player", 0, 1); // 玩家 Front
        var pBack = MakeUnit("p2", "player", 2, 1);  // 玩家 Back
        playerUnits.Add(pFront);
        playerUnits.Add(pBack);
        Deploy(data, pFront, 0, 1);
        Deploy(data, pBack, 2, 1);

        var e = MakeUnit("e1", "enemy", 2, 1);       // 敌方 Back
        enemyUnits.Add(e);
        Deploy(data, e, 2, 1);

        // 物理排：玩家 Front=2，敌方 Back=5。中间排 3、4 空 → 0 阻挡 → 可打
        Assert.IsTrue((bool)Call(bs, "IsInRange", pFront, e, 0), "无阻挡排时 AttackRange=1 应可命中");

        // 玩家 Back=0 → 敌方 Back=5：中间 1、2（pFront 在 2）→ 1 个阻挡排 → AttackRange=1 不可打
        Assert.AreEqual(1, (int)Call(bs, "GetBlockingRowCount", pBack, e), "阻挡排计数错误");
        Assert.IsFalse((bool)Call(bs, "IsInRange", pBack, e, 0), "1 个阻挡排不应被 AttackRange=1 命中");

        // RangeModifier=1 → 可跨 1 个阻挡排
        Assert.IsTrue((bool)Call(bs, "IsInRange", pBack, e, 1), "RangeModifier=1 应能跨 1 个阻挡排");
    }

    [Test]
    public void LegacyPlayerAttack_Removed_RangeAuthorityStillSingleSource()
    {
        var bs = NewBattleSystem();
        var data = Call(bs, "GetBattleData", null);
        var playerUnits = (IList)Get(data, "PlayerUnits");
        var enemyUnits = (IList)Get(data, "EnemyUnits");

        // WP-C：旧玩家普通攻击入口（BattleSystem.PlayerAttack）已退出正式产品路径
        Assert.IsNull(T("BattleSystem").GetMethod("PlayerAttack", F), "旧玩家普通攻击入口不应存在");

        var pFront = MakeUnit("p1", "player", 0, 1);
        var pBack = MakeUnit("p2", "player", 2, 1);
        playerUnits.Add(pFront);
        playerUnits.Add(pBack);
        Deploy(data, pFront, 0, 1);
        Deploy(data, pBack, 2, 1);

        var e = MakeUnit("e1", "enemy", 2, 1, hp: 100);
        enemyUnits.Add(e);
        Deploy(data, e, 2, 1);

        // 统一距离源仍为唯一 Authority（同一 Board 内阻挡排 + AttackRange）
        Assert.IsFalse((bool)Call(bs, "IsInRange", pBack, e, 0), "超距离应判定为不在攻击距离内");
        Assert.IsTrue((bool)Call(bs, "IsInRange", pFront, e, 0), "范围内应判定为在攻击距离内");

        // 敌方 AI 复用同一距离源（FindEnemyTarget 只能选到够得着的目标）
        Assert.IsNotNull(Call(bs, "FindEnemyTarget", e), "敌方 AI 目标选择必须仍可执行");
        Assert.GreaterOrEqual((int)Get(e, "CurrentHP"), 1, "本测试不执行敌方伤害，仅校验距离 Authority");
    }

    [Test]
    public void FindEnemyTarget_RespectsUnifiedRange()
    {
        var bs = NewBattleSystem();
        var data = Call(bs, "GetBattleData", null);
        var playerUnits = (IList)Get(data, "PlayerUnits");
        var enemyUnits = (IList)Get(data, "EnemyUnits");

        var pFront = MakeUnit("p1", "player", 0, 1);
        var pBack = MakeUnit("p2", "player", 2, 1);
        playerUnits.Add(pFront);
        playerUnits.Add(pBack);
        Deploy(data, pFront, 0, 1);
        Deploy(data, pBack, 2, 1);

        var e = MakeUnit("e1", "enemy", 2, 1, hp: 100);
        enemyUnits.Add(e);
        Deploy(data, e, 2, 1);

        // 敌方 Back(5)、射程 1：pFront(Front=2) 在范围内（中间空），pBack(Back=0) 被 pFront 阻挡
        var target = Call(bs, "FindEnemyTarget", e);
        Assert.IsNotNull(target, "应找到射程内目标");
        Assert.AreEqual("p1", (string)Get(target, "SourceId"), "敌方 AI 应只选择统一距离判定范围内的目标");
    }

    // ---------- 3. V1 玩家回合开始 TurnStart 技能触发迁移 ----------

    [Test]
    public void TriggerTurnStart_AdjacentAllyGetsBuff_AndRedeployStops()
    {
        // 村长 char_004：sk_003 鼓舞（TurnStart / AddAttack+2 / AdjacentAlly）
        var rallyChar = CreateSO("CharacterData");
        Set(rallyChar, "CharacterId", "char_004");
        Set(rallyChar, "DisplayName", "村长");
        Set(rallyChar, "BaseHP", 30);
        Set(rallyChar, "BaseAttack", 2);
        Set(rallyChar, "AttackRange", 1);
        Set(rallyChar, "AttackCount", 1);
        Set(rallyChar, "ActionPoints", 1);
        Set(rallyChar, "Faction", "player");
        Set(rallyChar, "InitialSkillIds", new[] { "sk_003" });

        var warChar = CreateSO("CharacterData");
        Set(warChar, "CharacterId", "char_001");
        Set(warChar, "DisplayName", "战士");
        Set(warChar, "BaseHP", 30);
        Set(warChar, "BaseAttack", 20);
        Set(warChar, "AttackRange", 1);
        Set(warChar, "AttackCount", 1);
        Set(warChar, "ActionPoints", 1);
        Set(warChar, "Faction", "player");
        Set(warChar, "InitialSkillIds", new string[0]);

        var skRally = CreateSO("SkillData");
        Set(skRally, "SkillId", "sk_003");
        Set(skRally, "Name", "鼓舞");
        Set(skRally, "Trigger", EnumVal("TriggerType", 1)); // TurnStart
        Set(skRally, "CostActionPoints", 0);
        var effect = Create("EffectData");
        Set(effect, "EffectType", EnumVal("EffectType", 2)); // AddAttack
        Set(effect, "Value", 2);
        Set(effect, "TargetRule", EnumVal("TargetRule", 8)); // AdjacentAlly
        var effectsArray = Array.CreateInstance(T("EffectData"), 1);
        effectsArray.SetValue(effect, 0);
        Set(skRally, "Effects", effectsArray);

        var cardRally = CreateSO("CharacterCardData");
        Set(cardRally, "CardId", "ccard_rally");
        Set(cardRally, "CharacterId", "char_004");
        var cardWar = CreateSO("CharacterCardData");
        Set(cardWar, "CardId", "ccard_war");
        Set(cardWar, "CharacterId", "char_001");

        var ciRally = Create("CharacterInstance", "char_004", "村长", 30, 2);
        var ciWar = Create("CharacterInstance", "char_001", "战士", 30, 20);

        // M2 Slice 2A：MonsterDefinition 是纯数据类（非 ScriptableObject）→ 用 Activator 构造
        var mon = Create("MonsterDefinition");
        Set(mon, "MonsterId", "mon_001");
        Set(mon, "Name", "史莱姆");
        Set(mon, "MaxHP", 50);
        Set(mon, "Attack", 4);
        Set(mon, "AttackRange", 1);
        Set(mon, "AttackCount", 1);
        Set(mon, "ActionPoints", 1);
        Set(mon, "Faction", "enemy");

        var instances = MakeList("CharacterInstance", ciRally, ciWar);
        var config = CreateSO("InjuryProbabilityConfig");
        var bs = Create("BattleSystem", instances, null, config);

        var chars = MakeList("CharacterCardData", cardRally, cardWar);
        var monsters = MakeList("MonsterDefinition", mon);
        var playerSkills = MakeList("SkillData", skRally);
        var enemySkills = MakeList("SkillData");
        Call(bs, "InitializeBattle", chars, monsters, playerSkills, enemySkills);

        // 部署：村长 Front Center，战士 Front Left（同排相邻列 → 邻接）
        var skillsRally = MakeList("SkillData", skRally);
        var skillsWar = MakeList("SkillData");
        Call(bs, "DeployCharacter", cardRally, EnumVal("BattleRow", 0), EnumVal("BattleColumn", 1), ciRally, rallyChar, skillsRally);
        Call(bs, "DeployCharacter", cardWar, EnumVal("BattleRow", 0), EnumVal("BattleColumn", 0), ciWar, warChar, skillsWar);

        var data = Call(bs, "GetBattleData", null);
        var playerUnits = (IList)Get(data, "PlayerUnits");
        Assert.AreEqual(2, playerUnits.Count, "应部署 2 名我方单位");
        object war = null;
        object rally = null;
        foreach (var u in playerUnits)
        {
            if ((string)Get(u, "SourceId") == "char_001") war = u;
            if ((string)Get(u, "SourceId") == "char_004") rally = u;
        }
        Assert.IsNotNull(war, "未找到战士单位");
        Assert.IsNotNull(rally, "未找到村长单位");

        int warAttack0 = (int)Get(war, "Attack");

        // 第一次触发：战士（邻接友军）+2
        Call(bs, "TriggerTurnStartSkillsForPlayer", null);
        Assert.AreEqual(warAttack0 + 2, (int)Get(war, "Attack"), "村长 TurnStart 鼓舞应对邻接友军 +2");
        Assert.AreEqual(2, (int)Get(rally, "Attack"), "村长自身不应被鼓舞加攻");

        // 改变布阵：战士移到 Back Center（与村长 Front Center 排差 2 → 不邻接）→ 不再触发
        Deploy(data, war, 2, 1);
        int afterMove = (int)Get(war, "Attack");
        Call(bs, "TriggerTurnStartSkillsForPlayer", null);
        Assert.AreEqual(afterMove, (int)Get(war, "Attack"), "改变布阵后邻接结果应动态变化（不再加攻）");
    }

    // ---------- 4. BattleCard.FromEntry RangeModifier 拷贝（强类型，BattleStage1.Runtime） ----------

    [Test]
    public void BattleCard_FromEntry_CopiesRangeModifier()
    {
        var template = ScriptableObject.CreateInstance<BattleCardData>();
        template.CardId = "bcard_test";
        template.CardKind = BattleCardKind.Character;
        template.Cost = 1;
        template.RangeModifier = 2;

        var entry = new BattlePersonCardEntry(template, "person_001");
        var card = BattleCard.FromEntry(entry, 0);

        Assert.AreEqual(2, card.RangeModifier, "运行时牌实例应拷贝模板 RangeModifier");
    }

    // ---------- 5. 上阵上限 6（Current GameSpec R4.1，英雄占正常位置） ----------

    [Test]
    public void DeployCharacter_CapAtSixUnits()
    {
        var charData = CreateSO("CharacterData");
        Set(charData, "CharacterId", "char_001");
        Set(charData, "DisplayName", "战士");
        Set(charData, "BaseHP", 30);
        Set(charData, "BaseAttack", 20);
        Set(charData, "AttackRange", 1);
        Set(charData, "AttackCount", 1);
        Set(charData, "ActionPoints", 1);
        Set(charData, "Faction", "player");
        Set(charData, "InitialSkillIds", new string[0]);

        var ci = Create("CharacterInstance", "char_001", "战士", 30, 20);
        var instances = MakeList("CharacterInstance", ci);
        var config = CreateSO("InjuryProbabilityConfig");
        var bs = Create("BattleSystem", instances, null, config);

        var monsters = MakeList("MonsterDefinition");
        var playerSkills = MakeList("SkillData");
        var enemySkills = MakeList("SkillData");
        Call(bs, "InitializeBattle", MakeList("CharacterCardData"), monsters, playerSkills, enemySkills);

        var skills = MakeList("SkillData");
        // 双 Board（R5.0 / R5.1）：同一 Board 内两个有效单位不得占同一格，
        // 因此 6 名我方单位使用 6 个不同逻辑格（3 排 × 3 列中的 6 格）。
        for (int i = 0; i < 6; i++)
        {
            var cardN = CreateSO("CharacterCardData");
            Set(cardN, "CardId", "ccard_cap_0" + i);
            Set(cardN, "CharacterId", "char_001");
            Assert.IsTrue((bool)Call(bs, "DeployCharacter", cardN, EnumVal("BattleRow", i / 3), EnumVal("BattleColumn", i % 3), ci, charData, skills),
                "第 " + (i + 1) + " 个我方单位应成功上阵");
        }

        // 第 7 个被拒绝（Current GameSpec R4.1：最多 6）；目标为空格，证明拒绝原因是上阵上限
        var card7 = CreateSO("CharacterCardData");
        Set(card7, "CardId", "ccard_cap_07");
        Set(card7, "CharacterId", "char_001");
        Assert.IsFalse((bool)Call(bs, "DeployCharacter", card7, EnumVal("BattleRow", 2), EnumVal("BattleColumn", 0), ci, charData, skills),
            "第 7 个我方单位应被上阵上限拒绝");

        var data = Call(bs, "GetBattleData", null);
        Assert.AreEqual(6, ((IList)Get(data, "PlayerUnits")).Count, "上阵单位数应保持为 6");
    }

    // ---------- 6. 失能角色牌移除（Current GameSpec R15.3，强类型 BattleStage1.Runtime） ----------

    [Test]
    public void SharedDeck_RemovePersonCards_RemovesFromAllPiles()
    {
        var deck = new SharedDeck();
        var t1 = ScriptableObject.CreateInstance<BattleCardData>();
        t1.CardId = "bcard_sword";
        t1.CardKind = BattleCardKind.Character;
        t1.Cost = 1;
        var t2 = ScriptableObject.CreateInstance<BattleCardData>();
        t2.CardId = "bcard_heal";
        t2.CardKind = BattleCardKind.Character;
        t2.Cost = 1;

        deck.BuildDeck(new[]
        {
            new BattlePersonCardEntry(t1, "p1", 1),
            new BattlePersonCardEntry(t2, "p2", 2)
        });
        deck.Reshuffle();
        deck.Draw(3); // 全部进手牌：p1×1 + p2×2

        int removed = deck.RemovePersonCards("p1");
        Assert.Greater(removed, 0, "应移除失能人物的角色牌");
        Assert.IsFalse(deck.Hand.Exists(c => c.OwnerPersonId == "p1"), "手牌不应再含失能人物牌");
        Assert.IsFalse(deck.DrawPile.Exists(c => c.OwnerPersonId == "p1"), "抽牌堆不应再含失能人物牌");
        Assert.IsFalse(deck.DiscardPile.Exists(c => c.OwnerPersonId == "p1"), "弃牌堆不应再含失能人物牌");
        Assert.IsFalse(deck.ExhaustPile.Exists(c => c.OwnerPersonId == "p1"), "消耗区不应再含失能人物牌");
        Assert.IsTrue(deck.Hand.Exists(c => c.OwnerPersonId == "p2"), "其他人物牌应保留");
        Assert.AreEqual(0, deck.RemovePersonCards("p1"), "重复移除应幂等返回 0");
    }

    // ---------- 7. 失能单位：不占邻接位（Current GameSpec R15.x） ----------

    [Test]
    public void IsAdjacentAlly_DownedUnitDoesNotBlockCenter()
    {
        var data = Create("BattleData");
        var skillSys = NewSkillSystem(data);

        var a = MakeUnit("a", "player", 0, 0); // 前排 Left
        var b = MakeUnit("b", "player", 0, 2); // 前排 Right
        Deploy(data, a, 0, 0);
        Deploy(data, b, 0, 2);

        // 我方单位占 Center 但已失能 → 不阻断同排左右邻接
        var downed = MakeUnit("d", "player", 0, 1, hp: 0);
        Set(downed, "IsDown", true);
        Deploy(data, downed, 0, 1);

        Assert.IsTrue((bool)Call(skillSys, "IsAdjacentAlly", a, b), "失能单位不应阻断同排左右邻接");
    }

    // ---------- 8. 敌方攻击造成 0HP → Current 失能，不进入死亡之门（Current GameSpec R15.x） ----------

    [Test]
    public void EnemyAttack_DownsPlayer_NoDeathDoor()
    {
        var bs = NewBattleSystem();
        var data = Call(bs, "GetBattleData", null);
        var playerUnits = (IList)Get(data, "PlayerUnits");
        var enemyUnits = (IList)Get(data, "EnemyUnits");

        var p = MakeUnit("p1", "player", 0, 1, range: 1, hp: 5, atk: 1);   // 玩家 HP5
        playerUnits.Add(p);
        Deploy(data, p, 0, 1);

        var e = MakeUnit("e1", "enemy", 2, 1, range: 1, hp: 100, atk: 10); // 敌 ATK10
        enemyUnits.Add(e);
        Deploy(data, e, 2, 1);

        // 敌方 Back(5) 打玩家 Front(2)：中间无阻挡 → 命中
        Call(bs, "ExecuteEnemyPhase", null);

        Assert.IsTrue((bool)Get(p, "IsDown"), "敌方攻击使玩家 0HP 应立即失能");
        Assert.IsFalse((bool)Get(p, "InDeathDoor"), "失能不进入死亡之门");
        Assert.AreEqual(0, (int)Get(p, "CurrentHP"), "失能后 HP 应为 0");
    }

    // ---------- 9. AttackRange 阻挡规则确认（R5.x 已冻结继承规则） ----------
    // 攻击者与目标之间所有存活单位形成的非空排均算阻挡（含己方与敌方）；空排不计。

    [Test]
    public void GetBlockingRowCount_AllyRowBlocks_AndRangeModifierRelaxes()
    {
        var bs = NewBattleSystem();
        var data = Call(bs, "GetBattleData", null);
        var playerUnits = (IList)Get(data, "PlayerUnits");
        var enemyUnits = (IList)Get(data, "EnemyUnits");

        var pBack = MakeUnit("p2", "player", 2, 1);  // 玩家 Back
        var pFront = MakeUnit("p1", "player", 0, 1); // 玩家 Front（己方阻挡排）
        playerUnits.Add(pBack);
        playerUnits.Add(pFront);
        Deploy(data, pBack, 2, 1);
        Deploy(data, pFront, 0, 1);

        var eBack = MakeUnit("e1", "enemy", 2, 1);   // 敌方 Back
        enemyUnits.Add(eBack);
        Deploy(data, eBack, 2, 1);

        // pBack(物理0) 打 eBack(物理5)：中间排 1、2，己方 pFront(物理2) 形成 1 个阻挡排
        Assert.AreEqual(1, (int)Call(bs, "GetBlockingRowCount", pBack, eBack), "己方非空排应计入阻挡");
        Assert.IsFalse((bool)Call(bs, "IsInRange", pBack, eBack, 0), "AttackRange=1 不应穿透己方阻挡排");
        Assert.IsTrue((bool)Call(bs, "IsInRange", pBack, eBack, 1), "RangeModifier=1 应按同一规则放宽可达性");
    }

    [Test]
    public void GetBlockingRowCount_EnemyRowBlocks_AndRangeModifierRelaxes()
    {
        var bs = NewBattleSystem();
        var data = Call(bs, "GetBattleData", null);
        var playerUnits = (IList)Get(data, "PlayerUnits");
        var enemyUnits = (IList)Get(data, "EnemyUnits");

        var pFront = MakeUnit("p1", "player", 0, 1);  // 玩家 Front
        playerUnits.Add(pFront);
        Deploy(data, pFront, 0, 1);

        var eFront = MakeUnit("e2", "enemy", 0, 1);   // 敌方 Front（敌方阻挡排，模拟更前排敌人）
        var eBack = MakeUnit("e1", "enemy", 2, 1);    // 敌方 Back
        enemyUnits.Add(eFront);
        enemyUnits.Add(eBack);
        Deploy(data, eFront, 0, 1);
        Deploy(data, eBack, 2, 1);

        // pFront(物理2) 打 eBack(物理5)：中间排 3、4，敌方 eFront(物理3) 形成 1 个阻挡排
        Assert.AreEqual(1, (int)Call(bs, "GetBlockingRowCount", pFront, eBack), "敌方非空前排应计入阻挡");
        Assert.IsFalse((bool)Call(bs, "IsInRange", pFront, eBack, 0), "AttackRange=1 不应穿透敌方前排阻挡");
        Assert.IsTrue((bool)Call(bs, "IsInRange", pFront, eBack, 1), "RangeModifier=1 应按同一规则放宽可达性");

        // 直接打敌方 Front（无中间排）→ 0 阻挡，AttackRange=1 可命中
        Assert.AreEqual(0, (int)Call(bs, "GetBlockingRowCount", pFront, eFront), "无中间排不应有阻挡");
        Assert.IsTrue((bool)Call(bs, "IsInRange", pFront, eFront, 0), "直接相邻排 AttackRange=1 可命中");
    }

    // ---------- 10. 敌方多回合行动力恢复（R15.x EnemyTurn 生命周期） ----------

    [Test]
    public void EnemyActionPoints_AreRestoredEachEnemyTurn()
    {
        var bs = NewBattleSystem();
        var data = Call(bs, "GetBattleData", null);
        var playerUnits = (IList)Get(data, "PlayerUnits");
        var enemyUnits = (IList)Get(data, "EnemyUnits");

        var p = MakeUnit("p1", "player", 0, 1, range: 1, hp: 100, atk: 1); // 玩家 Front HP100
        playerUnits.Add(p);
        Deploy(data, p, 0, 1);

        var e = MakeUnit("e1", "enemy", 2, 1, range: 1, hp: 100, atk: 5); // 敌方 Back
        enemyUnits.Add(e);
        Deploy(data, e, 2, 1);

        // 第一次 EnemyTurn：敌方消耗 AP 攻击
        Call(bs, "ExecuteTurnPart2", null);
        Assert.AreEqual(0, (int)Get(e, "CurrentActionPoints"), "敌方攻击后应消耗行动力");
        Assert.Less((int)Get(p, "CurrentHP"), 100, "敌方第一次 EnemyTurn 应造成伤害");

        // 第二次 EnemyTurn：行动力恢复 → 再次攻击
        int hpAfter1 = (int)Get(p, "CurrentHP");
        Call(bs, "ExecuteTurnPart2", null);
        Assert.AreEqual(0, (int)Get(e, "CurrentActionPoints"), "第二次 EnemyTurn 攻击后仍应消耗行动力");
        Assert.Less((int)Get(p, "CurrentHP"), hpAfter1, "敌方第二次 EnemyTurn 应再次造成伤害");

        // 第三次 EnemyTurn：仍能攻击
        int hpAfter2 = (int)Get(p, "CurrentHP");
        Call(bs, "ExecuteTurnPart2", null);
        Assert.Less((int)Get(p, "CurrentHP"), hpAfter2, "敌方第三次 EnemyTurn 仍应造成伤害");
    }

    // ---------- 11. AttackRange 对称规则（R5.x 敌我同一种非空阻挡排语言） ----------

    [Test]
    public void EnemyAttack_OwnFrontRowBlocks_AndRangeModifierRelaxes()
    {
        var bs = NewBattleSystem();
        var data = Call(bs, "GetBattleData", null);
        var playerUnits = (IList)Get(data, "PlayerUnits");
        var enemyUnits = (IList)Get(data, "EnemyUnits");

        var pFront = MakeUnit("p1", "player", 0, 1); // 玩家 Front（敌方目标）
        playerUnits.Add(pFront);
        Deploy(data, pFront, 0, 1);

        var eFront = MakeUnit("e2", "enemy", 0, 1);  // 敌方 Front（敌方自己前方阻挡排）
        var eBack = MakeUnit("e1", "enemy", 2, 1);   // 敌方 Back（攻击者）
        enemyUnits.Add(eFront);
        enemyUnits.Add(eBack);
        Deploy(data, eFront, 0, 1);
        Deploy(data, eBack, 2, 1);

        // eBack(物理5) 打 pFront(物理2)：中间排 3、4，敌方 eFront(物理3) 形成 1 个阻挡排
        Assert.AreEqual(1, (int)Call(bs, "GetBlockingRowCount", eBack, pFront), "敌方攻击时，敌方自己的非空前排应计入阻挡（对称规则）");
        Assert.IsFalse((bool)Call(bs, "IsInRange", eBack, pFront, 0), "敌方 AttackRange=1 不应穿透己方前排阻挡");
        Assert.IsTrue((bool)Call(bs, "IsInRange", eBack, pFront, 1), "敌方 RangeModifier=1 应按同一规则放宽可达性");
    }
}
