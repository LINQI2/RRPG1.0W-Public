using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

/// <summary>
/// WP-A 双 Board 空间 Authority —— 真实产品 Battle 路径集成验证（PlayMode）。
///
/// 必须证明的是「产品路径真的按双 Board 运行」，而不是模块单独能跑：
///   - 真实入口：SaveSystem.NewGame → ExpeditionSystem.ConfirmExpedition → **BindActiveEnemyRoster
///     （本场冻结敌方阵容；产品正式入口）** → BattleUI.InitFromExpedition
///     → StartBattleWithExpedition → PlaceUnit / PlaceHero → OnStartBattle；
///   - 敌方 lineup = 本场 EnemyRoster（Monster 目录只作模板身份解析源）——
///     不再把「整个 Monster Registry」当成本场敌人（那会让没有 Board 格的单位留在 EnemyUnits）；
///   - 真实 BattleData 的两条 Board（PlayerField / EnemyField）、真实 BattleRuleContextRuntime（Move 接缝）、
///     真实伤害 Authority（ApplyBattleDamage）、真实 Enemy Intent 与真实共享牌库 / 共享行动力；
///   - Player / Enemy 同 (Row, Column) 可同时存在；Move 不受对方 Board 同坐标影响；
///   - 0HP 释放所属 Board 逻辑格，新单位可以进入；没有第二套 Position / Spatial Authority。
///
/// 团结引擎限制 asmdef 引用 Assembly-CSharp：BattleUI / BattleSystem 经反射访问；
/// BattleCard 位于 BattleStage1.Runtime，可强类型引用。
/// </summary>
public class BattleDualBoardProductPlayTests
{
    private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

    private object _battleUI;
    private object _battleSystem;

    // ==================== 反射辅助 ====================

    private static Type GetType2(string name) => Type.GetType(name + ", Assembly-CSharp");

    private static object Call(object target, string method, params object[] args)
    {
        Assert.IsNotNull(target, "调用目标为空：" + method);
        var m = target.GetType().GetMethod(method, Flags);
        Assert.IsNotNull(m, "方法不存在：" + method);
        return m.Invoke(target, args);
    }

    private static object GetMember(object target, string name)
    {
        Assert.IsNotNull(target, "读取目标为空：" + name);
        var f = target.GetType().GetField(name, Flags);
        Assert.IsNotNull(f, "字段不存在：" + name);
        return f.GetValue(target);
    }

    private static object GetProperty(object target, string name)
    {
        Assert.IsNotNull(target, "读取目标为空：" + name);
        var p = target.GetType().GetProperty(name, Flags);
        Assert.IsNotNull(p, "属性不存在：" + name);
        return p.GetValue(target);
    }

    private static object GetField(object target, string name) => GetMember(target, name);

    private static object CallStatic(string typeName, string method, params object[] args)
    {
        var t = GetType2(typeName);
        Assert.IsNotNull(t, "类型不存在：" + typeName);
        var m = t.GetMethod(method, Flags);
        Assert.IsNotNull(m, "静态方法不存在：" + typeName + "." + method);
        return m.Invoke(null, args ?? new object[0]);
    }

    private static object FindInstance(string typeName)
    {
        var t = GetType2(typeName);
        if (t == null) return null;
        // 与既有 Battle PlayMode 测试一致：BattleUI 宿主 GameObject 可能未激活，需 includeInactive
        foreach (var o in UnityEngine.Object.FindObjectsOfType(t, true))
        {
            if (o != null) return o;
        }
        return null;
    }

    private static object DataManagerInstance()
    {
        var t = GetType2("DataManager");
        var p = t.GetProperty("Instance", Flags);
        Assert.IsNotNull(p, "DataManager.Instance 未找到");
        return p.GetValue(null);
    }

    private static object RowEnum(int row) => Enum.ToObject(GetType2("BattleRow"), row);

    private static object ColEnum(int column) => Enum.ToObject(GetType2("BattleColumn"), column);

    private static object UnitAt(object board, int row, int col) => Call(board, "GetUnitAt", RowEnum(row), ColEnum(col));

    /// <summary>每条 Board 的占位必须与单位自身坐标一致（唯一 Position Authority）</summary>
    private static void AssertNoSecondPositionAuthority(object board, IList units, string sideName)
    {
        foreach (var unit in units)
        {
            if (unit == null) continue;
            if ((bool)GetMember(unit, "IsDown")) continue;
            int row = (int)GetMember(unit, "Row");
            int col = (int)GetMember(unit, "Column");
            Assert.AreSame(unit, Call(board, "GetUnitAt", RowEnum(row), ColEnum(col)),
                sideName + " Board 占位必须与单位坐标一致（不得存在第二套位置记录）");
        }
    }

    // ==================== 真实产品路径进入 Battle ====================

    private IEnumerator EnterProductBattle(int characterCount)
    {
        SceneManager.LoadScene("SampleScene");
        yield return null;
        yield return null;

        _battleUI = FindInstance("BattleUI");
        Assert.IsNotNull(_battleUI, "BattleUI（正式宿主）未找到");

        var collection = CallStatic("SaveSystem", "NewGame");
        Assert.IsNotNull(collection, "SaveSystem.NewGame 失败");
        var exp = Activator.CreateInstance(GetType2("ExpeditionSystem"), new object[] { collection });
        var dataManager = DataManagerInstance();
        Assert.IsNotNull(dataManager, "DataManager.Instance 为空");

        var heroes = (IList)GetMember(collection, "OwnedHeroes");
        if (heroes != null && heroes.Count > 0)
        {
            var hi = heroes[0];
            var hd = Call(dataManager, "GetHeroById", GetMember(hi, "HeroId"));
            if (hd != null) Call(exp, "SelectHero", hi, hd);
        }

        var charCards = (IList)GetMember(collection, "OwnedCharacterCards");
        Assert.IsNotNull(charCards, "角色卡册为空");
        var selected = new List<object>();
        foreach (var c in charCards)
        {
            if (selected.Count >= characterCount) break;
            var cid = (string)GetMember(c, "CharacterId");
            if (cid == "char_001" || cid == "char_002" || cid == "char_003")
            {
                Call(exp, "AddCharacterCard", c);
                selected.Add(c);
            }
        }
        Assert.GreaterOrEqual(selected.Count, characterCount, "角色卡不足以完成产品路径布阵");

        var confirm = GetType2("ExpeditionSystem").GetMethod("ConfirmExpedition");
        Assert.IsNotNull(confirm, "ConfirmExpedition 未找到");
        var args = new object[2];
        Assert.IsTrue((bool)confirm.Invoke(exp, args), "确认出征失败");

        // WP-Event-1D-R：敌方 lineup 必须来自**本场冻结的 EnemyRoster**（产品正式入口
        // BattleUI.BindActiveEnemyRoster，与 ExpeditionUI.StartPreparedBattle 同一入口）。
        // 不再把「整个 Monster Registry」当作本场敌人走 Legacy 列表路径 ——
        // 那条路径只把前 3 只放进 Enemy Board，其余单位留在 EnemyUnits 且没有 Board 格。
        // Monster 目录在本测试只作**模板身份解析源**（MonsterId → 名称 / 数值 / 美术 / AI）。
        var monsterCatalog = StaticMonsterTestData.Build();
        var roster = EnemyRosterTestFixture.NewRoster(
            "fixture_dual_board",
            new[]
            {
                EnemyRosterTestFixture.MonsterIdAt(monsterCatalog, 0),
                EnemyRosterTestFixture.MonsterIdAt(monsterCatalog, 1)
            },
            2, 0,      // 敌方 Back / Left
            2, 2);     // 敌方 Back / Right（同排 2 只 → Left + Right；每格唯一、合法）
        Call(_battleUI, "BindActiveEnemyRoster", roster);
        Assert.IsTrue((bool)GetProperty(_battleUI, "IsEnemyRosterMode"),
            "绑定本场阵容后必须处于产品 EnemyRoster 模式（Prep / Battle 只消费这一份）");

        Call(_battleUI, "InitFromExpedition", args[0], args[1],
            GetProperty(dataManager, "AllCharacters"), GetProperty(dataManager, "AllSkills"),
            monsterCatalog, collection);

        var expUI = FindInstance("ExpeditionUI");
        if (expUI != null)
        {
            var canvas = GetMember(expUI, "battleCanvas");
            if (canvas != null) ((GameObject)canvas).SetActive(true);
        }
        ((GameObject)GetProperty(_battleUI, "gameObject")).SetActive(true);
        yield return null;

        Call(_battleUI, "StartBattleWithExpedition", null);
        yield return null;

        // 战前布阵：第 1 名角色 Front，第 2 名角色 Back（两侧共享 (Row, Column) 语言，但 Board 独立）
        var frontRow = (RectTransform)GetMember(_battleUI, "playerFrontRow");
        var backRow = (RectTransform)GetMember(_battleUI, "playerBackRow");
        Assert.IsNotNull(frontRow, "playerFrontRow 未绑定");
        Assert.IsNotNull(backRow, "playerBackRow 未绑定");

        var cd1 = Call(dataManager, "GetCharacterById", (string)GetMember(selected[0], "CharacterId"));
        Assert.IsNotNull(cd1, "角色数据缺失：" + GetMember(selected[0], "CharacterId"));
        Call(_battleUI, "PlaceUnit", selected[0], cd1, frontRow);
        if (characterCount > 1)
        {
            var cd2 = Call(dataManager, "GetCharacterById", (string)GetMember(selected[1], "CharacterId"));
            Assert.IsNotNull(cd2, "角色数据缺失：" + GetMember(selected[1], "CharacterId"));
            Call(_battleUI, "PlaceUnit", selected[1], cd2, backRow);
        }
        yield return null;

        Call(_battleUI, "OnStartBattle", null);
        yield return null;

        _battleSystem = GetMember(_battleUI, "_battleSystem");
        Assert.IsNotNull(_battleSystem, "BattleSystem 未创建（正式战斗未开始）");
    }

    // ==================== 1. 产品路径：同坐标共存 + Move 不受对方 Board 影响 ====================

    [UnityTest]
    public IEnumerator ProductBattle_SameCoordinateCoexists_MoveIgnoresOpposingBoard()
    {
        IEnumerator entry = EnterProductBattle(2);
        while (entry.MoveNext()) yield return entry.Current;

        var data = Call(_battleSystem, "GetBattleData", null);
        var playerBoard = GetMember(data, "PlayerField");
        var enemyBoard = GetMember(data, "EnemyField");
        var players = (IList)GetMember(data, "PlayerUnits");
        var enemies = (IList)GetMember(data, "EnemyUnits");

        Assert.GreaterOrEqual(players.Count, 2, "产品路径应上阵 2 名我方单位");
        Assert.GreaterOrEqual(enemies.Count, 1, "产品路径应存在敌方单位");
        Assert.AreEqual(2, enemies.Count,
            "敌方数量必须等于本场 EnemyRoster（不得把整个 Monster 目录当成本场敌人）");

        var enemy0 = enemies[0];
        int eRow = (int)GetMember(enemy0, "Row");
        int eCol = (int)GetMember(enemy0, "Column");
        Assert.AreSame(enemy0, UnitAt(enemyBoard, eRow, eCol), "敌方单位应占据 Enemy Board");

        var mover = players[0];
        int mRow = (int)GetMember(mover, "Row");
        int mCol = (int)GetMember(mover, "Column");

        var ctx = Call(_battleUI, "EnsureRuleContext");
        Assert.IsNotNull(ctx, "正式规则执行上下文（BattleRuleContextRuntime）应可用");

        var personId = (string)Call(_battleUI, "GetPersonIdOfUnit", mover);
        var moveCard = new BattleCard
        {
            CardId = "wp_a_move",
            TemplateId = "wp_a_move",
            DisplayName = "WP-A Move",
            CardKind = BattleCardKind.Character,
            OwnerPersonId = personId,
            Cost = 1
        };

        // 目标：我方 Board 上与敌方单位完全相同的 (Row, Column)
        Assert.IsTrue((bool)Call(ctx, "TryMoveOwnerTo", moveCard, eRow, eCol),
            "Move 到对方 Board 同坐标的 Player Board 空格必须成功（R5.1：Occupancy 独立）");

        // Player / Enemy 同 (Row, Column) 在产品路径中同时存在
        Assert.AreSame(mover, UnitAt(playerBoard, eRow, eCol), "我方 Board 占位应为被移动的单位");
        Assert.AreSame(enemy0, UnitAt(enemyBoard, eRow, eCol), "敌方 Board 同坐标占位不得被 Move 改变");
        Assert.IsNull(UnitAt(playerBoard, mRow, mCol), "Move 后旧格应释放");
        AssertNoSecondPositionAuthority(playerBoard, players, "Player");
        AssertNoSecondPositionAuthority(enemyBoard, enemies, "Enemy");

        // Enemy Intent 与共享资源未受影响
        Call(_battleSystem, "LockEnemyIntents", null);
        Assert.IsNotNull(Call(_battleSystem, "GetEnemyIntent", enemy0), "Enemy Intent 应仍正常生成");

        var v1 = GetMember(_battleUI, "v1Runtime");
        Assert.IsNotNull(v1, "共享牌库运行时未初始化");
        var ap = GetProperty(v1, "ActionPoints");
        Assert.IsNotNull(ap, "共享行动力池未初始化");
        Assert.Greater((int)GetProperty(ap, "Max"), 0, "共享行动力上限应来自正式配置");
        Assert.IsNotNull(GetProperty(v1, "Deck"), "共享牌库未初始化");
    }

    // ==================== 2. 产品路径：0HP 释放格 + 新单位可进入 ====================

    [UnityTest]
    public IEnumerator ProductBattle_DownedUnit_ReleasesCell_AndNewUnitCanEnter()
    {
        IEnumerator entry = EnterProductBattle(2);
        while (entry.MoveNext()) yield return entry.Current;

        var data = Call(_battleSystem, "GetBattleData", null);
        var playerBoard = GetMember(data, "PlayerField");
        var enemyBoard = GetMember(data, "EnemyField");
        var players = (IList)GetMember(data, "PlayerUnits");
        var enemies = (IList)GetMember(data, "EnemyUnits");

        Assert.GreaterOrEqual(players.Count, 2, "产品路径应上阵 2 名我方单位");
        Assert.GreaterOrEqual(enemies.Count, 1, "产品路径应存在敌方单位");
        Assert.AreEqual(2, enemies.Count,
            "敌方数量必须等于本场 EnemyRoster（不得把整个 Monster 目录当成本场敌人）");

        var victim = players[0];
        int vRow = (int)GetMember(victim, "Row");
        int vCol = (int)GetMember(victim, "Column");
        Assert.AreSame(victim, UnitAt(playerBoard, vRow, vCol), "受击前应占据 Player Board");

        // 真实伤害 Authority：0HP → 立即失能（无死亡之门）
        Assert.IsTrue((bool)Call(_battleSystem, "ApplyBattleDamage", victim, 9999), "伤害应正常结算");
        Assert.IsTrue((bool)GetMember(victim, "IsDown"), "0HP 应立即失去战斗能力");

        Assert.IsNull(UnitAt(playerBoard, vRow, vCol), "0HP 后必须释放所属 Board 逻辑格");
        Assert.IsTrue((bool)Call(playerBoard, "CanDeploy", RowEnum(vRow), ColEnum(vCol)),
            "释放后的格应可再次被单位占用");

        // 新单位进入释放格（真实 Move 接缝 = 《补位》使用的同一 Authority）
        object newcomer = null;
        foreach (var u in players)
        {
            if (u == victim) continue;
            if (!(bool)GetMember(u, "IsDown")) { newcomer = u; break; }
        }
        Assert.IsNotNull(newcomer, "应存在仍具战斗能力的我方单位");

        var ctx = Call(_battleUI, "EnsureRuleContext");
        Assert.IsNotNull(ctx, "正式规则执行上下文（BattleRuleContextRuntime）应可用");
        var refillCard = new BattleCard
        {
            CardId = "wp_a_refill",
            TemplateId = "wp_a_refill",
            DisplayName = "WP-A Refill",
            CardKind = BattleCardKind.Character,
            OwnerPersonId = (string)Call(_battleUI, "GetPersonIdOfUnit", newcomer),
            Cost = 1
        };
        Assert.IsTrue((bool)Call(ctx, "TryMoveOwnerTo", refillCard, vRow, vCol), "新单位应可进入被释放的格");
        Assert.AreSame(newcomer, UnitAt(playerBoard, vRow, vCol), "进入释放格后 Board 占位与单位坐标一致");

        // 敌方 Board 同坐标不受影响，Enemy Intent 仍正常
        var enemy0 = enemies[0];
        int eRow = (int)GetMember(enemy0, "Row");
        int eCol = (int)GetMember(enemy0, "Column");
        Assert.AreSame(enemy0, UnitAt(enemyBoard, eRow, eCol), "我方失能不得影响 Enemy Board 占位");
        Call(_battleSystem, "LockEnemyIntents", null);
        Assert.IsNotNull(Call(_battleSystem, "GetEnemyIntent", enemy0), "失能后 Enemy Intent 仍应正常生成");

        AssertNoSecondPositionAuthority(playerBoard, players, "Player");
        AssertNoSecondPositionAuthority(enemyBoard, enemies, "Enemy");
        Assert.IsFalse((bool)GetMember(data, "IsBattleOver"), "仍有可战斗我方单位时不得判定失败");
    }
}
