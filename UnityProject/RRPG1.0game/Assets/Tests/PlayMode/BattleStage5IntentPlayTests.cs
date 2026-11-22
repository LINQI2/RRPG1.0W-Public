using TMPro;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

/// <summary>
/// Battle V0.1 Stage 5（Milestone C）PlayMode 集成验证
///
/// 从正式产品路径（SaveSystem.NewGame → ExpeditionSystem.ConfirmExpedition → BattleUI）
/// 进入战斗，验证：
///   1. 完整闭环：玩家回合 → 查看 Intent（敌方卡显示与逻辑一致）→ 出牌 → EnemyTurn 执行
///      → 下一玩家回合（Intent 重新锁定）；
///   2. 所有敌人失能 → 战斗胜利 → 进入现有 BattleResultUI 结算宿主。
///
/// 团结引擎限制 asmdef 引用运行时预定义程序集（Assembly-CSharp），
/// BattleUI / BattleV1Runtime / BattleSystem 通过 System.Reflection 访问；
/// SharedDeck / BattleCard / BattleV1TurnFlow / EnemyIntent 位于 BattleStage1.Runtime，可强类型引用。
/// </summary>
public class BattleStage5IntentPlayTests
{
    private const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

    // ---------- 反射辅助 ----------

    private static System.Type GetType(string name) => System.Type.GetType(name + ", Assembly-CSharp");

    private static object GetDataManager() =>
        GetType("DataManager").GetProperty("Instance", BindingFlags.Public | BindingFlags.Static).GetValue(null);

    private static object FindInstance(string typeName)
    {
        var t = GetType(typeName);
        if (t == null) return null;
        foreach (var o in Object.FindObjectsOfType(t, true))
        {
            if (o != null) return o;
        }
        return null;
    }

    private static object GetField(object o, string name)
    {
        var f = o.GetType().GetField(name, F);
        Assert.IsNotNull(f, "字段不存在：" + name);
        return f.GetValue(o);
    }

    private static object GetProperty(object o, string name)
    {
        var p = o.GetType().GetProperty(name, F);
        Assert.IsNotNull(p, "属性不存在：" + name);
        return p.GetValue(o, null);
    }

    private static object GetMember(object o, string name)
    {
        var f = o.GetType().GetField(name, F);
        if (f != null) return f.GetValue(o);
        var p = o.GetType().GetProperty(name, F);
        Assert.IsNotNull(p, "成员不存在：" + name);
        return p.GetValue(o, null);
    }

    private static object Call(object o, string method, params object[] args)
    {
        var m = o.GetType().GetMethod(method, F);
        Assert.IsNotNull(m, "方法不存在：" + method);
        return m.Invoke(o, args);
    }

    private static object CallStatic(string typeName, string method, params object[] args)
    {
        var m = GetType(typeName).GetMethod(method, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
        Assert.IsNotNull(m, "静态方法不存在：" + typeName + "." + method);
        return m.Invoke(null, args);
    }

    private static object EnumVal(string typeName, int v) => System.Enum.ToObject(GetType(typeName), v);

    private sealed class BattleContext
    {
        public object BattleUI;
        public object DataManager;
        public object Collection;
        public object BattleSystem;
    }

    // ---------- 正式产品路径进入战斗 ----------

    private IEnumerator EnterFormalBattle(BattleContext ctx)
    {
        SceneManager.LoadScene("SampleScene");
        yield return null;
        yield return null;

        var battleUI = FindInstance("BattleUI");
        var expUI = FindInstance("ExpeditionUI");
        Assert.IsNotNull(battleUI, "BattleUI（正式宿主）未找到");
        Assert.IsNotNull(expUI, "ExpeditionUI 未找到");

        var collection = CallStatic("SaveSystem", "NewGame", null);
        Assert.IsNotNull(collection, "NewGame 失败");

        var exp = System.Activator.CreateInstance(GetType("ExpeditionSystem"), new object[] { collection });
        var dataManager = GetDataManager();

        var heroes = (IList)GetMember(collection, "OwnedHeroes");
        if (heroes != null && heroes.Count > 0)
        {
            var hi = heroes[0];
            var hd = Call(dataManager, "GetHeroById", GetMember(hi, "HeroId"));
            if (hd != null) Call(exp, "SelectHero", hi, hd);
        }
        var charCards = (IList)GetMember(collection, "OwnedCharacterCards");
        Assert.IsNotNull(charCards, "角色卡册为空");
        if (charCards.Count > 0) Call(exp, "AddCharacterCard", charCards[0]); // 战士
        if (charCards.Count > 1) Call(exp, "AddCharacterCard", charCards[1]); // 猎人

        var confirm = GetType("ExpeditionSystem").GetMethod("ConfirmExpedition");
        Assert.IsNotNull(confirm, "ConfirmExpedition 方法未找到");
        var args = new object[2];
        Assert.IsTrue((bool)confirm.Invoke(exp, args), "确认出征失败");
        var chars = args[0];
        var hero = args[1];

        Call(battleUI, "InitFromExpedition", chars, hero,
            GetProperty(dataManager, "AllCharacters"), GetProperty(dataManager, "AllSkills"),
            StaticMonsterTestData.Build(), null);
        var canvas = GetField(expUI, "battleCanvas");
        if (expUI != null && canvas != null) ((GameObject)canvas).SetActive(true);
        var battleGo = GetProperty(battleUI, "gameObject");
        if (battleGo != null) ((GameObject)battleGo).SetActive(true);
        yield return null;

        Call(battleUI, "StartBattleWithExpedition", null);
        yield return null;

        // 布阵：战士 Front、猎人 Middle、英雄 Back
        var charList = (IList)chars;
        if (charList.Count > 0)
        {
            var cd = Call(dataManager, "GetCharacterById", GetMember(charList[0], "CharacterId"));
            if (cd != null) Call(battleUI, "PlaceUnit", charList[0], cd, GetField(battleUI, "playerFrontRow"));
        }
        if (charList.Count > 1)
        {
            var cd = Call(dataManager, "GetCharacterById", GetMember(charList[1], "CharacterId"));
            if (cd != null) Call(battleUI, "PlaceUnit", charList[1], cd, GetField(battleUI, "playerMiddleRow"));
        }
        if (hero != null)
        {
            var hd = Call(dataManager, "GetHeroById", GetMember(hero, "HeroId"));
            if (hd != null) Call(battleUI, "PlaceHero", hd, hero, GetField(battleUI, "playerBackRow"));
        }
        yield return null;

        Call(battleUI, "OnStartBattle", null);
        yield return null;

        // 等待玩家回合开始（Intent 在玩家回合开始已被 LockEnemyIntents 锁定）
        var v1 = GetField(battleUI, "v1Runtime");
        Assert.IsNotNull(v1, "BattleV1Runtime 未绑定");
        float wait = 0f;
        while (true)
        {
            var phase = (BattleV1TurnFlow.PhaseType)GetProperty(v1, "TurnPhase");
            if (phase == BattleV1TurnFlow.PhaseType.PlayerTurn) break;
            wait += Time.deltaTime;
            if (wait > 10f) break;
            yield return null;
        }
        Assert.AreEqual(BattleV1TurnFlow.PhaseType.PlayerTurn, (BattleV1TurnFlow.PhaseType)GetProperty(v1, "TurnPhase"), "未进入玩家回合");

        ctx.BattleUI = battleUI;
        ctx.DataManager = dataManager;
        ctx.Collection = collection;
        ctx.BattleSystem = GetField(battleUI, "_battleSystem");
        Assert.IsNotNull(ctx.BattleSystem, "_battleSystem 未初始化");
    }

    private static IList EnemyUnitsOf(object battleSystem)
    {
        var data = Call(battleSystem, "GetBattleData", null);
        return (IList)GetMember(data, "EnemyUnits");
    }

    /// <summary>在敌方排容器中查找指定敌人的 UI 卡 GameObject</summary>
    private static GameObject FindEnemyCard(object battleUI, string sourceId)
    {
        var data = Call(GetField(battleUI, "_battleSystem"), "GetBattleData", null);
        foreach (var e in (IList)GetMember(data, "EnemyUnits"))
        {
            if ((string)GetMember(e, "SourceId") != sourceId) continue;
            string rowStr = GetMember(e, "Row").ToString();
            var rowName = rowStr == "Back" ? "enemyBackRow"
                        : rowStr == "Middle" ? "enemyMiddleRow" : "enemyFrontRow";
            var row = (RectTransform)GetField(battleUI, rowName);
            if (row == null) return null;
            foreach (Transform child in row)
            {
                if (child != null && child.name == "Card_" + sourceId) return child.gameObject;
            }
            return null;
        }
        return null;
    }

    // ---------- 1. 完整闭环：Intent 显示 → 出牌 → EnemyTurn → 下一玩家回合 ----------

    [UnityTest]
    public IEnumerator FullTurnLoop_IntentShown_EnemyActs_NextTurn()
    {
        IEnumerator run = RunFullTurnLoop();
        while (true)
        {
            bool moved;
            try { moved = run.MoveNext(); }
            catch (System.Exception e) { Debug.LogError("[Stage5PlayTest] 异常：" + e); throw; }
            if (!moved) break;
            yield return run.Current;
        }
    }

    private IEnumerator RunFullTurnLoop()
    {
        var ctx = new BattleContext();
        yield return EnterFormalBattle(ctx);
        var battleUI = ctx.BattleUI;
        var battleSystem = ctx.BattleSystem;

        var v1 = GetField(battleUI, "v1Runtime");

        // 1a. 所有存活敌人已锁定 Intent（玩家回合开始 LockEnemyIntents 已执行）
        var enemies = EnemyUnitsOf(battleSystem);
        int alive = 0;
        foreach (var e in enemies)
        {
            if ((bool)GetMember(e, "IsDown")) continue;
            alive++;
            var intent = Call(battleSystem, "GetEnemyIntent", e);
            Assert.IsNotNull(intent, "玩家回合开始后存活敌人应有锁定 Intent：" + GetMember(e, "SourceId"));
        }
        Assert.Greater(alive, 0, "应有存活敌人");

        // 1b. 敌方卡 UI：Intent 文本与逻辑一致（mon_001 普通 → 进攻）
        var enemyCard = FindEnemyCard(battleUI, "mon_001");
        Assert.IsNotNull(enemyCard, "敌方卡（mon_001）未找到");
        // WP-D：Intent 视觉宿主迁移到 UnitCard Prefab 上的 Anchor_Intent（旧卡根直挂 Text_Intent 仅作兼容回退）
        var intentTxt = enemyCard.transform.Find("Anchor_Intent/Text_Intent")?.GetComponent<TMP_Text>()
                        ?? enemyCard.transform.Find("Text_Intent")?.GetComponent<TMP_Text>();
        Assert.IsNotNull(intentTxt, "敌方卡未创建 Intent 文本宿主（Anchor_Intent/Text_Intent）");
        Assert.IsEmpty(intentTxt.text, "Current Intent 没有正式数值时不显示解释性文字");
        var intentIcon = enemyCard.transform.Find("Anchor_Intent/Image_IntentIcon").GetComponent<Image>();
        Assert.IsTrue(intentIcon.enabled);
        Assert.AreSame(GetField(battleUI, "intentAttackPlaceholder"), intentIcon.sprite, "mon_001 Intent 应显示进攻图标");
        var rangeTxt = enemyCard.transform.Find("Text_Range")?.GetComponent<TMP_Text>();
        Assert.IsNotNull(rangeTxt, "敌方卡未创建 AttackRange 文本（Text_Range）");
        Assert.AreEqual("射程1", rangeTxt.text, "mon_001 AttackRange 显示错误");

        // 1c. 出牌：点击可打出的手牌（需目标时点击目标）→ 共享行动力扣减
        var deck = (SharedDeck)GetProperty(v1, "Deck");
        var sharedAp = (SharedActionPointPool)GetProperty(v1, "ActionPoints");
        int apBefore = sharedAp.Current;
        bool played = TryPlayCards(battleUI, v1, deck);
        Assert.IsTrue(played, "无法打出任何手牌");
        Assert.Less(sharedAp.Current, apBefore, "出牌后共享行动力未扣减");
        yield return null;

        // 1d. 结束回合 → EnemyTurn 执行（敌人按锁定 Action 攻击）→ 下一玩家回合
        int turnBefore = (int)GetProperty(v1, "TurnNumber");
        var playerHpBefore = PlayerHpSnapshot(battleSystem);
        var endTurnBtn = (Button)GetField(battleUI, "btnEndTurn");
        Assert.IsNotNull(endTurnBtn, "结束回合按钮未绑定");
        endTurnBtn.onClick.Invoke();
        yield return null;

        float wait = 0f;
        while ((int)GetProperty(v1, "TurnNumber") == turnBefore && wait < 10f)
        {
            wait += Time.deltaTime;
            yield return null;
        }
        Assert.Greater((int)GetProperty(v1, "TurnNumber"), turnBefore, "结束回合后未进入下一玩家回合");

        // EnemyTurn 已执行：至少一名我方单位受到伤害
        bool anyDamaged = false;
        foreach (var kv in playerHpBefore)
        {
            var unit = kv.Key;
            int hpNow = (int)GetMember(unit, "CurrentHP");
            if (hpNow < kv.Value) anyDamaged = true;
        }
        Assert.IsTrue(anyDamaged, "EnemyTurn 应至少造成一次伤害（敌人按锁定 Action 执行）");

        // 1e. 下一玩家回合：存活敌人 Intent 重新锁定（EnemyTurn 已消费上一回合 Intent）
        enemies = EnemyUnitsOf(battleSystem);
        foreach (var e in enemies)
        {
            if ((bool)GetMember(e, "IsDown")) continue;
            Assert.IsNotNull(Call(battleSystem, "GetEnemyIntent", e), "下一玩家回合存活敌人应重新锁定 Intent");
        }
        Assert.AreEqual(sharedAp.Max, sharedAp.Current, "下一玩家回合共享行动力应恢复至上限");
    }

    private static Dictionary<object, int> PlayerHpSnapshot(object battleSystem)
    {
        var data = Call(battleSystem, "GetBattleData", null);
        var map = new Dictionary<object, int>();
        foreach (var u in (IList)GetMember(data, "PlayerUnits"))
            map[u] = (int)GetMember(u, "CurrentHP");
        return map;
    }

    private bool TryPlayCards(object battleUI, object v1, SharedDeck deck)
    {
        var skipped = new HashSet<string>();
        for (int attempt = 0; attempt < 8; attempt++)
        {
            BattleCard playable = null;
            foreach (var card in deck.Hand)
            {
                if (skipped.Contains(card.CardId)) continue;
                if ((bool)Call(v1, "CanPlayCard", card)) { playable = card; break; }
            }
            if (playable == null) return false;

            if (!ClickHandCard(battleUI, playable)) return false;

            if (playable.TargetScope == BattleCardTargetScope.EnemyUnit)
                ClickTarget(battleUI, false);
            else if (playable.TargetScope == BattleCardTargetScope.AllyUnit)
                ClickTarget(battleUI, true);

            if (!deck.Hand.Contains(playable)) return true;

            skipped.Add(playable.CardId);
            ClickHandCard(battleUI, playable);
        }
        return false;
    }

    private bool ClickHandCard(object battleUI, BattleCard card)
    {
        var container = (RectTransform)GetField(battleUI, "handContainer");
        if (container == null) return false;
        foreach (Transform child in container)
        {
            if (child == null || child.name != "V1Card_" + card.CardId) continue;
            var btn = child.GetComponent<Button>();
            if (btn == null) continue;
            btn.onClick.Invoke();
            return true;
        }
        return false;
    }

    private bool ClickTarget(object battleUI, bool isPlayer)
    {
        var rowFields = isPlayer
            ? new[] { "playerFrontRow", "playerMiddleRow", "playerBackRow" }
            : new[] { "enemyFrontRow", "enemyMiddleRow", "enemyBackRow" };

        foreach (var name in rowFields)
        {
            var row = (RectTransform)GetField(battleUI, name);
            if (row == null) continue;
            foreach (Transform child in row)
            {
                if (child == null || !child.name.StartsWith("Card_")) continue;
                var btn = child.GetComponent<Button>();
                if (btn != null && btn.interactable) { btn.onClick.Invoke(); return true; }
            }
        }
        return false;
    }

    // ---------- 2. 所有敌人失能 → 胜利 → 现有 BattleResultUI 结算宿主 ----------

    [UnityTest]
    public IEnumerator AllEnemiesDown_Victory_SettlementShown()
    {
        IEnumerator run = RunVictorySettlement();
        while (true)
        {
            bool moved;
            try { moved = run.MoveNext(); }
            catch (System.Exception e) { Debug.LogError("[Stage5PlayTest] 异常：" + e); throw; }
            if (!moved) break;
            yield return run.Current;
        }
    }

    private IEnumerator RunVictorySettlement()
    {
        string savePath = (string)CallStatic("SaveSystem", "GetSavePath", null);
        try
        {
            var ctx = new BattleContext();
            yield return EnterFormalBattle(ctx);
            var battleUI = ctx.BattleUI;
            var dataManager = ctx.DataManager;
            var collection = ctx.Collection;
            var battleSystem = ctx.BattleSystem;

            // 玩家回合：击杀全部敌人（最后一击 → CheckBattleResult → 战斗结束 → 协程立即退出）
            var enemies = EnemyUnitsOf(battleSystem);
            Assert.Greater(enemies.Count, 0, "应存在敌人");
            foreach (var e in enemies)
                Call(battleSystem, "ApplyBattleDamage", e, 9999);

            var data = Call(battleSystem, "GetBattleData", null);
            Assert.IsTrue((bool)GetMember(data, "IsBattleOver"), "所有敌人失能后战斗应结束");

            // 等待协程执行 DoSettlement → BattleResultUI 显示胜利
            var resultUI = GetField(battleUI, "_resultUI");
            Assert.IsNotNull(resultUI, "_resultUI 未绑定");
            float wait = 0f;
            while (true)
            {
                var canvasRoot = (GameObject)GetField(resultUI, "canvasRoot");
                if (canvasRoot != null && canvasRoot.activeSelf) break;
                wait += Time.deltaTime;
                if (wait > 6f) break;
                yield return null;
            }

            var canvasRootFinal = (GameObject)GetField(resultUI, "canvasRoot");
            Assert.IsNotNull(canvasRootFinal, "结算 Canvas 未找到");
            Assert.IsTrue(canvasRootFinal.activeSelf, "胜利结算界面应显示（现有 BattleResultUI 宿主）");
            var titleText = (TMP_Text)GetField(resultUI, "titleText");
            Assert.IsTrue(titleText.text.Contains("胜利"), "结算标题应显示胜利");
        }
        finally
        {
            // 清理结算保存的存档（ProcessSettlement → SaveSystem.Save），不污染真实存档
            if (!string.IsNullOrEmpty(savePath) && System.IO.File.Exists(savePath))
                System.IO.File.Delete(savePath);
        }
    }
}
