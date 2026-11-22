using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// WP-C Battle Legacy Product-Path Cleanup 长期回归契约。
///
/// 保护的不变量（Battle V0.1 已冻结的 Battle × Expedition 产品模型）：
///   1. 远征 Prep 不存在独立 Item Card 选择（无 SelectedItems / AddItemCard / Item Tab 行为）；
///   2. Battle Runtime 不存在独立 Item Hand / Item DrawPile / Item DiscardPile / TP；
///   3. 玩家行动只走共享牌库 + 共享行动力，不存在玩家个人 AP 驱动入口；
///   4. 旧 Attack / Skill / Guard 产品入口已退出（且不留下第二套正式路径）；
///   5. Current Card Cancel 仍可取消待出牌与目标选择；
///   6. ApplyBattleDamage 仍是伤害 Authority；TurnStart 技能触发仍有效；Enemy Intent 执行行为未变；
///   7. 旧档含 Legacy Item 字段时可安全加载，SaveVersion 不变，Current Save 不再持久化 Legacy Item。
///
/// 团结引擎限制 asmdef 引用 Assembly-CSharp：BattleSystem / BattleData / BattleUnit / BattleUI /
/// ExpeditionSystem / AdventureManager / SaveSystem 经 System.Reflection 访问；
/// BattleCard / BattleCardKind / SharedDeck / SharedActionPointPool 位于 BattleStage1.Runtime，可强类型引用。
/// </summary>
public class BattleLegacyProductPathCleanupTests
{
    private const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

    // ==================== 反射辅助 ====================

    // M2 Slice 2A：MonsterDefinition 位于 StaticConfig.Runtime（不是 Assembly-CSharp）→ 解析器加回退。
    private static Type Asm(string name) => Type.GetType(name + ", Assembly-CSharp")
        ?? Type.GetType("RRPG.StaticConfig." + name + ", StaticConfig.Runtime");

    /// <summary>BattleStage1.Runtime 程序集中的类型（共享牌库 / 共享行动力 / 战斗牌类别）。</summary>
    private static Type Runtime(string name) => Type.GetType(name + ", BattleStage1.Runtime");

    private static object Create(string typeName, params object[] args) => Activator.CreateInstance(Asm(typeName), args);

    private static object CreateSO(string typeName) => ScriptableObject.CreateInstance(Asm(typeName));

    private static object EnumVal(string enumTypeName, int v) => Enum.ToObject(Asm(enumTypeName), v);

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
        var list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(Asm(elementTypeName)));
        foreach (var item in items) list.Add(item);
        return list;
    }

    private static object MakeUnit(string id, string faction, int row, int col, int range = 1, int hp = 50, int atk = 5,
                                   int actionPoints = 1)
    {
        int unitType = faction == "enemy" ? 2 : 0; // Monster=2, Character=0
        return Create("BattleUnit", id, id, EnumVal("UnitType", unitType), hp, atk, 1, range, actionPoints, faction);
    }

    private static void Deploy(object data, object unit, int row, int col)
    {
        var board = (row < 3) ? Get(data, "PlayerField") : Get(data, "EnemyField");
        Call(board, "Deploy", unit, EnumVal("BattleRow", row % 3), EnumVal("BattleColumn", col));
    }

    private static object NewBattleSystem()
    {
        var config = CreateSO("InjuryProbabilityConfig");
        return Create("BattleSystem", MakeList("CharacterInstance"), null, config);
    }

    /// <summary>某类型是否有任何成员（字段/属性/方法参数/返回值）仍引用 Legacy ItemCardData。</summary>
    private static bool DeclaresItemCardDataMember(Type t)
    {
        if (t == null) return false;

        foreach (var f in t.GetFields(F))
        {
            if (MentionsItemCardData(f.FieldType)) return true;
        }
        foreach (var p in t.GetProperties(F))
        {
            if (MentionsItemCardData(p.PropertyType)) return true;
        }
        foreach (var m in t.GetMethods(F))
        {
            if (MentionsItemCardData(m.ReturnType)) return true;
            foreach (var p in m.GetParameters())
            {
                if (MentionsItemCardData(p.ParameterType)) return true;
            }
        }
        return false;
    }

    /// <summary>类型名是否指向 Legacy ItemCardData（开放泛型 / 类型参数时 FullName 可能为 null）。</summary>
    private static bool MentionsItemCardData(Type t)
    {
        if (t == null) return false;
        string name = t.FullName ?? t.Name;
        return name != null && name.Contains("ItemCardData");
    }

    // ==================== T1 / T2：Expedition Prep ====================

    [Test]
    public void ExpeditionPrep_HasNoLegacyItemSelection()
    {
        var exp = Asm("ExpeditionSystem");
        Assert.IsNotNull(exp, "ExpeditionSystem 必须存在");

        Assert.IsNull(exp.GetProperty("SelectedItems", F), "Prep 不应再有独立道具卡选择列表 SelectedItems");
        Assert.IsNull(exp.GetMethod("AddItemCard", F), "Prep 不应再有 AddItemCard 产品入口");
        Assert.IsNull(exp.GetMethod("RemoveItemCard", F), "Prep 不应再有 RemoveItemCard 产品入口");
        Assert.IsNull(exp.GetField("MaxItemCards", F), "Prep 不应再有道具卡选择上限");

        var confirm = exp.GetMethod("ConfirmExpedition", F);
        Assert.IsNotNull(confirm, "ConfirmExpedition 必须存在");
        var ps = confirm.GetParameters();
        Assert.AreEqual(2, ps.Length, "ConfirmExpedition 只应输出（角色卡列表, 英雄），不得再输出独立 item list");
        foreach (var p in ps)
        {
            Assert.IsTrue(p.IsOut, "ConfirmExpedition 参数应为 out 输出：" + p.Name);
            Assert.IsFalse(p.ParameterType.FullName.Contains("ItemCardData"),
                "ConfirmExpedition 不得再传递独立 Item Card 数据：" + p.Name);
        }

        var ui = Asm("ExpeditionUI");
        Assert.IsNotNull(ui, "ExpeditionUI 必须存在");
        Assert.IsNull(ui.GetMethod("ShowItemSelection", F), "Prep UI 不应再有独立 Item 选择呈现");
        Assert.IsNull(ui.GetMethod("OnItemCardClick", F), "Prep UI 不应再有道具卡点击入口");
        Assert.IsNull(ui.GetField("itemCardPrefab", F), "Prep UI 不应再持有 ItemCard prefab");
        Assert.IsNull(ui.GetField("_itemCards", F), "Prep UI 不应再缓存道具卡对象");
        Assert.IsNull(ui.GetField("_selectedItemCardKeys", F), "Prep UI 不应再记录道具卡选中状态");

        // 旧 Item Tab 仅作为 Legacy 序列化引用保留（运行时清除），其清除入口必须存在
        Assert.IsNotNull(ui.GetMethod("RemoveLegacyItemTabUI", F), "必须存在运行时清除旧 Item Tab 的入口");
    }

    // ==================== T3：Battle Item Hand / TP ====================

    [Test]
    public void BattleRuntime_HasNoIndependentItemHand()
    {
        Assert.IsNull(Asm("TPSystem"), "TP 系统类型必须已退出正式产品路径");
        Assert.IsNull(Asm("DrawPile"), "独立道具抽牌堆类型必须已退出");
        Assert.IsNull(Asm("DiscardPile"), "独立道具弃牌堆类型必须已退出");
        Assert.IsNull(Asm("ItemCardDragHandler"), "道具卡拖拽处理器必须已退出");
        Assert.IsNull(Asm("BattleUIItems"), "Legacy 道具手牌 partial 类型必须已删除");
        Assert.IsNull(Asm("BattleUIActionMenu"), "Legacy 操作菜单 partial 类型必须已删除");

        var bs = Asm("BattleSystem");
        Assert.IsNull(bs.GetMethod("UseItem", F), "BattleSystem 不应再有 UseItem 产品入口");
        Assert.IsNull(bs.GetMethod("GetTPSystem", F), "BattleSystem 不应再暴露 TP 系统");
        Assert.IsNull(bs.GetField("_tpSystem", F), "BattleSystem 不应再持有 TP 系统");

        Assert.IsNull(Asm("BattleData").GetField("DrawPile", F), "BattleData 不应再有独立道具抽牌堆");
        Assert.IsNull(Asm("BattleData").GetField("DiscardPile", F), "BattleData 不应再有独立道具弃牌堆");
        Assert.IsNull(Asm("Hand").GetField("ItemCards", F), "Hand 不应再有独立道具手牌");
        Assert.IsNull(Asm("CardSystem").GetMethod("DrawItemCards", F), "CardSystem 不应再有道具抽牌流程");
        Assert.IsNull(Asm("CardSystem").GetMethod("InitDrawPile", F), "CardSystem 不应再初始化道具抽牌堆");

        var ui = Asm("BattleUI");
        Assert.IsNull(ui.GetMethod("RefreshItemCards", F), "BattleUI 不应再刷新 Item Hand");
        Assert.IsNull(ui.GetField("itemCardPrefab", F), "BattleUI 不应再持有 ItemCard prefab");
        Assert.IsNull(ui.GetField("_playerItems", F), "BattleUI 不应再保存独立道具卡列表");
        Assert.IsNotNull(ui.GetMethod("RemoveLegacyProductUIObjects", F), "必须存在运行时清除旧道具手牌宿主的入口");
    }

    // ==================== T4 / T11：玩家只走 Shared Deck / Shared AP ====================

    [Test]
    public void BattlePlayerTurn_UsesSharedAPOnly()
    {
        var ui = Asm("BattleUI");
        Assert.IsNull(ui.GetNestedType("ActionMode", F), "旧玩家操作状态机 ActionMode 必须已退出");
        Assert.IsNull(ui.GetField("_currentAction", F), "旧玩家操作状态字段必须已退出");
        Assert.IsNull(ui.GetField("_selectedUnit", F), "旧玩家操作选择字段必须已退出");
        Assert.IsNull(ui.GetMethod("ClearAction", F), "旧操作清理入口必须已退出");

        Assert.IsNotNull(Runtime("SharedActionPointPool"), "共享行动力池必须存在（BattleStage1.Runtime）");
        var runtime = Asm("BattleV1Runtime");
        Assert.IsNotNull(runtime, "共享牌库 / 共享行动力运行时必须存在");
        Assert.IsNotNull(runtime.GetProperty("ActionPoints", F), "运行时必须暴露共享行动力");
        Assert.IsNotNull(runtime.GetProperty("Deck", F), "运行时必须暴露共享牌库");

        // TurnSystem.StartTurn 不得再给玩家单位恢复角色个人 AP（玩家个人 AP 已退出资源模型）
        var bs = NewBattleSystem();
        var data = Call(bs, "GetBattleData", null);
        var players = (IList)Get(data, "PlayerUnits");
        var p = MakeUnit("p_ap", "player", 0, 1, actionPoints: 1);
        Set(p, "CurrentActionPoints", 0);
        players.Add(p);
        Deploy(data, p, 0, 1);

        var turnSystem = Get(bs, "_turnSystem");
        Call(turnSystem, "StartTurn", null);
        Assert.AreEqual(0, (int)Get(p, "CurrentActionPoints"),
            "玩家个人 AP 不得再被回合系统恢复（玩家资源只有共享行动力）");
    }

    // ==================== T4：旧玩家操作菜单退出 ====================

    [Test]
    public void LegacyAttackMenu_NotProductReachableOrRemoved()
    {
        Assert.IsNull(Asm("BattleSystem").GetMethod("PlayerAttack", F), "旧玩家普通攻击入口必须已退出");
        var ui = Asm("BattleUI");
        Assert.IsNull(ui.GetMethod("OnAttackOp", F), "旧 Attack 菜单处理必须已退出");
        AssertLegacyMenuObjectsAreClearedAtRuntime(ui);
    }

    [Test]
    public void LegacySkillMenu_NotProductReachableOrRemoved()
    {
        var ui = Asm("BattleUI");
        Assert.IsNull(ui.GetMethod("OnSkillOp", F), "旧 Skill 菜单处理必须已退出");
        Assert.IsNull(ui.GetField("_selectedSkill", F), "旧 Skill 选择状态必须已退出");

        // Current 能力保留：技能系统与 TurnStart 触发仍存在（不是把 SkillSystem 整体删除）
        var skillSystem = Asm("SkillSystem");
        Assert.IsNotNull(skillSystem, "SkillSystem 不得被整体删除");
        Assert.IsNotNull(skillSystem.GetMethod("CastSkill", F), "SkillSystem.CastSkill 能力应保留");
        Assert.IsNotNull(skillSystem.GetMethod("TriggerSkills", F), "TurnStart 技能触发能力应保留");
        AssertLegacyMenuObjectsAreClearedAtRuntime(ui);
    }

    [Test]
    public void LegacyGuardMenu_NotProductReachableOrRemoved()
    {
        var bs = Asm("BattleSystem");
        Assert.IsNull(bs.GetMethod("PerformGuard", F), "旧玩家援护执行入口必须已退出（消费方证明无 Current writer）");
        Assert.IsNull(bs.GetMethod("ClearAllGuards", F), "旧援护生命周期入口应随写侧一并退出");

        var unit = Asm("BattleUnit");
        Assert.IsNull(unit.GetField("GuardTargetId", F), "旧援护目标字段必须已退出");
        Assert.IsNull(unit.GetField("IsGuarding", F), "旧援护状态字段必须已退出");
        Assert.IsNull(unit.GetField("GuardedBy", F), "旧援护读侧字段必须已退出（无 writer）");

        var ui = Asm("BattleUI");
        Assert.IsNull(ui.GetMethod("OnGuardOp", F), "BattleUI 不应再有旧 Guard 入口");
        AssertLegacyMenuObjectsAreClearedAtRuntime(ui);
    }

    /// <summary>
    /// 旧 Attack / Skill / Guard 场景对象若仍被序列化引用绑定，必须由 BattleUI 在 Start 时清除，
    /// 保证旧 UI 不进入正式产品路径（序列化资产物理清理另行任务处理）。
    /// </summary>
    private static void AssertLegacyMenuObjectsAreClearedAtRuntime(Type uiType)
    {
        var host = new GameObject("WP_C_LegacyMenuHost");
        host.SetActive(false); // 避免 Start / 战斗协程运行
        GameObject atkGo = null, skillGo = null, guardGo = null, itemRootGo = null;
        try
        {
            var ui = host.AddComponent(uiType);
            atkGo = new GameObject("WPC_BtnAttack");
            skillGo = new GameObject("WPC_BtnSkill");
            guardGo = new GameObject("WPC_BtnGuard");
            itemRootGo = new GameObject("WPC_ItemCardRoot");
            var atk = atkGo.AddComponent<UnityEngine.UI.Button>();
            var skill = skillGo.AddComponent<UnityEngine.UI.Button>();
            var guard = guardGo.AddComponent<UnityEngine.UI.Button>();
            var itemRoot = itemRootGo.AddComponent<RectTransform>();
            atkGo.transform.SetParent(host.transform);
            skillGo.transform.SetParent(host.transform);
            guardGo.transform.SetParent(host.transform);
            itemRootGo.transform.SetParent(host.transform);

            Set(ui, "btnAttackOp", atk);
            Set(ui, "btnSkillOp", skill);
            Set(ui, "btnGuardOp", guard);
            Set(ui, "itemCardRoot", itemRoot);

            var cleanup = uiType.GetMethod("RemoveLegacyProductUIObjects", F);
            Assert.IsNotNull(cleanup, "BattleUI 必须提供运行时清除旧菜单对象入口");
            cleanup.Invoke(ui, null);

            Assert.IsNull(Get(ui, "btnAttackOp"), "旧 Attack 按钮引用必须被清除");
            Assert.IsNull(Get(ui, "btnSkillOp"), "旧 Skill 按钮引用必须被清除");
            Assert.IsNull(Get(ui, "btnGuardOp"), "旧 Guard 按钮引用必须被清除");
            Assert.IsNull(Get(ui, "itemCardRoot"), "旧 Item Hand 根引用必须被清除");
        }
        finally
        {
            if (atkGo != null) UnityEngine.Object.DestroyImmediate(atkGo);
            if (skillGo != null) UnityEngine.Object.DestroyImmediate(skillGo);
            if (guardGo != null) UnityEngine.Object.DestroyImmediate(guardGo);
            if (itemRootGo != null) UnityEngine.Object.DestroyImmediate(itemRootGo);
            UnityEngine.Object.DestroyImmediate(host);
        }
    }

    // ==================== Current Cancel ====================

    [Test]
    public void CurrentCancel_StillCancelsPendingCard()
    {
        var uiType = Asm("BattleUI");
        var host = new GameObject("WP_C_CancelHost");
        host.SetActive(false);
        try
        {
            var ui = host.AddComponent(uiType);
            var card = new BattleCard { CardId = "bcard_wpc_cancel", DisplayName = "取消契约牌" };
            Set(ui, "_v1PendingCard", card);

            var cancel = uiType.GetMethod("CancelPendingCard", F);
            Assert.IsNotNull(cancel, "Current Card Cancel 入口必须存在");
            cancel.Invoke(ui, null);
            Assert.IsNull(Get(ui, "_v1PendingCard"), "Current Card Cancel 必须清空待出牌");
            Assert.IsNotNull(uiType.GetMethod("OnV1CardClicked", F), "再次点击同一张牌取消的路径必须保留");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(host);
        }
    }

    // ==================== 保留的 Current 能力 ====================

    [Test]
    public void ApplyBattleDamage_RemainsCurrentAuthority()
    {
        var bs = NewBattleSystem();
        var data = Call(bs, "GetBattleData", null);
        var enemies = (IList)Get(data, "EnemyUnits");
        var e = MakeUnit("e_dmg", "enemy", 3, 1, hp: 20);
        enemies.Add(e);
        Deploy(data, e, 3, 1);

        Assert.IsTrue((bool)Call(bs, "ApplyBattleDamage", e, 5), "ApplyBattleDamage 应可用");
        Assert.AreEqual(15, (int)Get(e, "CurrentHP"), "ApplyBattleDamage 必须按 R15.x 扣减 HP");

        Assert.IsTrue((bool)Call(bs, "ApplyBattleDamage", e, 999), "致 0HP 应成功");
        Assert.IsTrue((bool)Get(e, "IsDown"), "0HP 必须立即失能（不进入死亡之门）");
    }

    /// <summary>取一个枚举成员（不依赖强类型引用 Assembly-CSharp 枚举）。</summary>
    private static object EnumMember(string enumTypeName, string memberName)
    {
        var t = Asm(enumTypeName);
        Assert.IsNotNull(t, "枚举不存在：" + enumTypeName);
        return Enum.Parse(t, memberName);
    }

    [Test]
    public void TurnStartSkillTrigger_StillWorks()
    {
        var bs = NewBattleSystem();
        var data = Call(bs, "GetBattleData", null);
        var players = (IList)Get(data, "PlayerUnits");

        var card = CreateSO("CharacterCardData");
        Set(card, "CardId", "ccard_wpc_ts");
        Set(card, "CharacterId", "char_wpc_ts");
        var cd = CreateSO("CharacterData");
        Set(cd, "CharacterId", "char_wpc_ts");
        Set(cd, "DisplayName", "契约人物");
        Set(cd, "BaseHP", 30);
        Set(cd, "BaseAttack", 5);
        Set(cd, "AttackRange", 1);
        Set(cd, "AttackCount", 1);
        Set(cd, "ActionPoints", 1);
        Set(cd, "Faction", "player");
        Set(cd, "InitialSkillIds", new string[0]);

        var ci = Create("CharacterInstance", "char_wpc_ts", "契约人物", 30, 5);

        var skill = CreateSO("SkillData");
        Set(skill, "SkillId", "sk_wpc_turnstart");
        Set(skill, "Name", "契约鼓舞");
        Set(skill, "Trigger", EnumMember("TriggerType", "TurnStart"));
        Set(skill, "CostActionPoints", 0);
        var effectData = Create("EffectData");
        Set(effectData, "EffectType", EnumMember("EffectType", "AddAttack"));
        Set(effectData, "Value", 2);
        Set(effectData, "TargetRule", EnumMember("TargetRule", "Self"));
        var effectArray = Array.CreateInstance(Asm("EffectData"), 1);
        effectArray.SetValue(effectData, 0);
        Set(skill, "Effects", effectArray);

        Call(bs, "InitializeBattle", MakeList("CharacterCardData", card), MakeList("MonsterDefinition"),
            MakeList("SkillData"), MakeList("SkillData"));
        Assert.IsTrue((bool)Call(bs, "DeployCharacter", card, EnumVal("BattleRow", 0), EnumVal("BattleColumn", 1),
            ci, cd, MakeList("SkillData", skill)), "角色应能上阵并注册 TurnStart 技能");

        var unit = players[players.Count - 1];
        int attackBefore = (int)Get(unit, "Attack");
        Call(bs, "TriggerTurnStartSkillsForPlayer", null);
        Assert.AreEqual(attackBefore + 2, (int)Get(unit, "Attack"),
            "TurnStart 技能触发必须仍生效（Current 能力未丢失）");
    }

    [Test]
    public void EnemyIntentExecution_Unchanged()
    {
        var bs = Asm("BattleSystem");
        Assert.IsNotNull(bs.GetMethod("LockEnemyIntents", F), "Enemy Intent 锁定入口必须保留");
        Assert.IsNotNull(bs.GetMethod("GetEnemyIntent", F), "Enemy Intent 读取入口必须保留");
        Assert.IsNotNull(bs.GetMethod("ExecuteTurnPart2", F), "玩家回合结束（敌方阶段）入口必须保留");
        Assert.IsNotNull(bs.GetMethod("FindEnemyTarget", F), "敌方目标选择入口必须保留");

        // 敌方阶段：敌人按既定规则消耗自身行动力并执行行为；
        // 玩家个人 AP 不再被恢复（不得顺手改敌方行动次数 / 敌方 AP 系统）
        var system = NewBattleSystem();
        var data = Call(system, "GetBattleData", null);
        var players = (IList)Get(data, "PlayerUnits");
        var enemies = (IList)Get(data, "EnemyUnits");

        var p = MakeUnit("p_intent", "player", 2, 1, hp: 100, actionPoints: 1);
        Set(p, "CurrentActionPoints", 0);
        players.Add(p);
        Deploy(data, p, 2, 1);

        var e = MakeUnit("e_intent", "enemy", 3, 1, hp: 50, atk: 7, actionPoints: 1);
        enemies.Add(e);
        Deploy(data, e, 3, 1);

        Call(system, "LockEnemyIntents", null);
        int hpBefore = (int)Get(p, "CurrentHP");
        Call(system, "ExecuteTurnPart2", null);

        Assert.AreEqual(0, (int)Get(e, "CurrentActionPoints"),
            "敌方行动仍应消耗其自身行动力（敌方行为未改变）");
        Assert.Less((int)Get(p, "CurrentHP"), hpBefore, "敌方攻击仍应造成伤害（Enemy 执行行为未改变）");
        Assert.AreEqual(0, (int)Get(p, "CurrentActionPoints"), "敌方阶段不得恢复玩家个人 AP");
    }

    // ==================== Save 兼容 ====================

    [Test]
    public void LegacyItemSaveData_LoadDoesNotBreak()
    {
        var saveSystemType = Asm("SaveSystem");
        Assert.IsNotNull(saveSystemType, "SaveSystem 必须存在");
        var bridgeType = Asm("VillageRuntimeBridge");
        if (bridgeType != null && bridgeType.GetMethod("ResetForTests", F) != null)
            bridgeType.GetMethod("ResetForTests", F).Invoke(null, null);

        string savePath = (string)saveSystemType.GetMethod("GetSavePath", F).Invoke(null, null);
        Assert.IsNotNull(savePath, "SaveSystem.GetSavePath 必须可用");
        try
        {
            // 旧档：SaveVersion=1；Player 含 Legacy OwnedItemCardIds（独立 Item 产品模型时期写入）
            string json = "{\"SaveVersion\":1,\"GameVersion\":\"0.2.3\",\"SavedAtUnixTime\":0," +
                          "\"Player\":{\"Gold\":123,\"Heroes\":[],\"Characters\":[],\"OwnedCharacterCardIds\":[]," +
                          "\"OwnedItemCardIds\":[\"icard_001\",\"icard_002\",\"icard_not_exist\"]}}";
            File.WriteAllText(savePath, json);

            object collection = saveSystemType.GetMethod("Load", F).Invoke(null, null);
            Assert.IsNotNull(collection, "旧档包含 Legacy Item 字段时必须可加载，不得崩溃");
            Assert.AreEqual(123, (int)Get(collection, "Gold"), "旧档其余玩家数据应正常恢复");

            // Current Save：不再依赖 OwnedItemCards runtime container；SaveVersion 不变
            bool saved = (bool)saveSystemType.GetMethod("Save", F).Invoke(null, new object[] { collection });
            Assert.IsTrue(saved, "重新保存应成功");
            string resaved = File.ReadAllText(savePath);
            StringAssert.IsMatch("\\\"SaveVersion\\\"\\s*:\\s*1", resaved, "SaveVersion 必须保持 1");
            StringAssert.DoesNotContain("icard_001", resaved, "Current Save 不得再持久化 Legacy Item ids");
        }
        finally
        {
            if (File.Exists(savePath)) File.Delete(savePath);
        }
    }

    // ==================== Shared Deck / Character + Common 回归 ====================

    [Test]
    public void SharedDeckCharacterCommon_Regression()
    {
        var kinds = Enum.GetNames(typeof(BattleCardKind));
        CollectionAssert.AreEquivalent(new[] { "Character", "Common" }, kinds,
            "战斗牌顶层类别只能有角色牌与通用牌（不存在第三套 Item Card 类别）");

        Assert.IsNotNull(Runtime("SharedDeck"), "共享牌库必须保留（BattleStage1.Runtime）");
        Assert.IsNotNull(Runtime("SharedActionPointPool"), "共享行动力必须保留（BattleStage1.Runtime）");

        // Battle / Expedition / Adventure 运行时不得再声明 ItemCardData 成员
        // （Legacy 容器 PlayerCollection 与未清理的 Legacy UI 不在本 WP 清理范围内）
        foreach (var typeName in new[] { "BattleSystem", "BattleData", "Hand", "CardSystem", "Deck",
                                         "ExpeditionSystem", "AdventureManager", "BattleUI" })
        {
            var t = Asm(typeName);
            Assert.IsNotNull(t, typeName + " 必须存在");
            Assert.IsFalse(DeclaresItemCardDataMember(t),
                typeName + " 不得再声明独立 Item Card 成员（Item 产品语义已退出）");
        }

        // ItemCardData 作为 Legacy 内容资产类型仍存在（不在本 WP 物理删除范围内）
        Assert.IsNotNull(Asm("ItemCardData"), "Legacy ItemCardData 内容类型应保留给未清理的 Legacy UI");
    }
}
